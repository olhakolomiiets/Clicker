from __future__ import annotations

import hashlib
import os
import stat
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from real_task_foundation_models import canonical_sha256


@dataclass(frozen=True)
class InventoryError(Exception):
    code: str
    message: str
    path: str | None = None


def is_symlink_or_reparse(path: Path) -> bool:
    if path.is_symlink():
        return True
    try:
        attrs = path.stat(follow_symlinks=False).st_file_attributes
    except (AttributeError, OSError):
        return False
    return bool(attrs & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def ensure_no_reparse_components(path: Path, stop_at: Path | None = None) -> None:
    current = path.resolve(strict=False)
    stop = stop_at.resolve(strict=False) if stop_at is not None else None
    parts = [current]
    parts.extend(current.parents)
    for candidate in reversed(parts):
        if stop is not None:
            try:
                candidate.relative_to(stop)
            except ValueError:
                continue
        if candidate.exists() or candidate.is_symlink():
            if is_symlink_or_reparse(candidate):
                raise InventoryError("REAL_TASK_SOURCE_REPARSE", "Path component is a symlink or reparse point.", _safe_path(candidate))


def file_sha256(path: Path, chunk_bytes: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(chunk_bytes)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def build_source_inventory(
    root: Path,
    source_paths: list[str],
    max_entries: int,
    chunk_bytes: int,
    service_paths: set[str] | None = None,
) -> dict[str, Any]:
    entries_by_path: dict[str, dict[str, Any]] = {}
    seen_files: set[tuple[int, int] | str] = set()
    service_paths = service_paths or set()
    resolved_root = root.resolve(strict=True)
    for relative in source_paths:
        path = resolved_root / relative
        try:
            path.resolve(strict=False).relative_to(resolved_root)
        except ValueError as exc:
            raise InventoryError("REAL_TASK_PATH_UNSAFE", "Source path escapes repository.", relative) from exc
        _scan_path(resolved_root, path, entries_by_path, seen_files, max_entries, chunk_bytes, service_paths)
    return _inventory_report("source", entries_by_path)


def build_workspace_inventory(
    workspace: Path,
    max_entries: int,
    chunk_bytes: int,
    service_paths: set[str] | None = None,
) -> dict[str, Any]:
    root = workspace.resolve(strict=True)
    entries_by_path: dict[str, dict[str, Any]] = {}
    seen_files: set[tuple[int, int] | str] = set()
    service_paths = service_paths or set()
    _scan_path(root, root, entries_by_path, seen_files, max_entries, chunk_bytes, service_paths, include_root=False)
    return _inventory_report("workspace", entries_by_path)


def compare_source_inventories(left: dict[str, Any], right: dict[str, Any]) -> bool:
    return _comparison_projection(left) == _comparison_projection(right)


def inventory_entries_by_path(inventory: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {entry["path"]: entry for entry in inventory.get("entries", []) if isinstance(entry, dict) and isinstance(entry.get("path"), str)}


def _scan_path(
    root: Path,
    path: Path,
    entries_by_path: dict[str, dict[str, Any]],
    seen_files: set[tuple[int, int] | str],
    max_entries: int,
    chunk_bytes: int,
    service_paths: set[str],
    include_root: bool = True,
) -> None:
    try:
        stat_result = path.stat(follow_symlinks=False)
    except OSError as exc:
        raise InventoryError("REAL_TASK_INVENTORY_FAILED", f"Entry could not be inspected: {type(exc).__name__}.", _safe_path(path)) from exc
    if is_symlink_or_reparse(path):
        raise InventoryError("REAL_TASK_SOURCE_REPARSE", "Entry is a symlink or reparse point.", _relative_or_self(root, path))
    mode = stat_result.st_mode
    relative = _relative_or_self(root, path)
    if include_root:
        if stat.S_ISDIR(mode):
            _add_entry(entries_by_path, _directory_entry(relative, service_paths), max_entries)
        elif stat.S_ISREG(mode):
            identity = _file_identity(path, stat_result)
            if identity in seen_files:
                return
            seen_files.add(identity)
            _add_entry(entries_by_path, _file_entry(root, path, stat_result, chunk_bytes, service_paths), max_entries)
            return
        else:
            raise InventoryError("REAL_TASK_UNSUPPORTED_FILE_TYPE", "Only regular files and directories are supported.", relative)
    if stat.S_ISDIR(mode):
        try:
            children = sorted(path.iterdir(), key=lambda item: item.name.lower())
        except OSError as exc:
            raise InventoryError("REAL_TASK_INVENTORY_FAILED", f"Directory could not be enumerated: {type(exc).__name__}.", relative) from exc
        for child in children:
            _scan_path(root, child, entries_by_path, seen_files, max_entries, chunk_bytes, service_paths)


def _add_entry(entries_by_path: dict[str, dict[str, Any]], entry: dict[str, Any], max_entries: int) -> None:
    path = entry["path"]
    if path in entries_by_path:
        if entries_by_path[path] != entry:
            raise InventoryError("REAL_TASK_PATH_COLLISION", "Duplicate path has different inventory data.", path)
        return
    normalized = unicodedata.normalize("NFC", path).casefold()
    for existing in entries_by_path:
        if unicodedata.normalize("NFC", existing).casefold() == normalized and existing != path:
            raise InventoryError("REAL_TASK_PATH_COLLISION", "Case or Unicode-normalized path collision.", path)
    if len(entries_by_path) + 1 > max_entries:
        raise InventoryError("REAL_TASK_SOURCE_LIMIT_EXCEEDED", "Inventory entry limit exceeded.", path)
    entries_by_path[path] = entry


def _directory_entry(path: str, service_paths: set[str]) -> dict[str, Any]:
    return {
        "path": path,
        "type": "directory",
        "size": 0,
        "sha256": None,
        "extension": "",
        "serviceClassification": _service_classification(path, service_paths),
        "sourceRoot": _source_root(path),
        "symlink": False,
        "reparse": False,
    }


def _file_entry(root: Path, path: Path, stat_result: os.stat_result, chunk_bytes: int, service_paths: set[str]) -> dict[str, Any]:
    relative = _relative_or_self(root, path)
    return {
        "path": relative,
        "type": "file",
        "size": stat_result.st_size,
        "sha256": file_sha256(path, chunk_bytes),
        "extension": path.suffix.lower(),
        "serviceClassification": _service_classification(relative, service_paths),
        "sourceRoot": _source_root(relative),
        "symlink": False,
        "reparse": False,
    }


def _inventory_report(kind: str, entries_by_path: dict[str, dict[str, Any]]) -> dict[str, Any]:
    entries = [entries_by_path[path] for path in sorted(entries_by_path)]
    report = {
        "inventoryVersion": 1,
        "rootKind": kind,
        "entries": entries,
        "fileCount": sum(1 for entry in entries if entry["type"] == "file"),
        "directoryCount": sum(1 for entry in entries if entry["type"] == "directory"),
        "totalBytes": sum(int(entry["size"]) for entry in entries if entry["type"] == "file"),
        "complete": True,
        "errors": [],
    }
    report["inventorySha256"] = canonical_sha256({key: value for key, value in report.items() if key != "inventorySha256"})
    return report


def _comparison_projection(inventory: dict[str, Any]) -> list[tuple[str, str, int, str | None]]:
    return [
        (entry["path"], entry["type"], int(entry["size"]), entry.get("sha256"))
        for entry in inventory.get("entries", [])
        if isinstance(entry, dict)
    ]


def _file_identity(path: Path, stat_result: os.stat_result) -> tuple[int, int] | str:
    inode = getattr(stat_result, "st_ino", 0)
    device = getattr(stat_result, "st_dev", 0)
    if inode and device:
        return (int(device), int(inode))
    return str(path.resolve(strict=True)).casefold()


def _service_classification(path: str, service_paths: set[str]) -> str:
    if path in service_paths or any(path.startswith(f"{service}/") for service in service_paths):
        return "immutable_service"
    if path.endswith("AGENTS.md"):
        return "project_instruction"
    return "task_content"


def _source_root(path: str) -> str:
    return path.split("/", 1)[0] if "/" in path else path


def _relative_or_self(root: Path, path: Path) -> str:
    try:
        return path.resolve(strict=False).relative_to(root.resolve(strict=False)).as_posix()
    except ValueError:
        return path.as_posix()


def _safe_path(path: Path) -> str:
    try:
        return path.as_posix()
    except OSError:
        return str(path)
