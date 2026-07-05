from __future__ import annotations

import os
import stat
import unicodedata
from pathlib import Path
from typing import Any

from real_task_workspace import git_fingerprint
from workspace_change_models import canonical_sha256, file_sha256
from workspace_change_policy import ChangePolicy, is_service_path
from workspace_inventory import InventoryError, is_symlink_or_reparse


def build_change_inventory(
    workspace: Path,
    run_id: str,
    task_id: str,
    trusted_context_hash: str,
    inventory_kind: str,
    service_registry: list[dict[str, Any]],
    policy: ChangePolicy,
    max_entries: int,
) -> dict[str, Any]:
    root = workspace.resolve(strict=True)
    service_paths = {str(item["workspacePath"]) for item in service_registry if item.get("workspacePath")}
    entries_by_path: dict[str, dict[str, Any]] = {}
    seen_files: set[tuple[int, int] | str] = set()
    git_state: dict[str, Any] | None = None
    try:
        git_state = git_fingerprint(root)
    except InventoryError as exc:
        raise exc
    _scan(root, root, entries_by_path, seen_files, service_paths, policy, max_entries, include_root=False)
    entries = [entries_by_path[path] for path in sorted(entries_by_path)]
    report = {
        "reportVersion": 1,
        "runId": run_id,
        "taskId": task_id,
        "stage": "BOOTSTRAP-03B-2B-B",
        "trustedContextHash": trusted_context_hash,
        "inventoryKind": inventory_kind,
        "workspacePath": "workspace",
        "entries": entries,
        "serviceRegistry": service_registry,
        "isolatedGitFingerprint": git_state,
        "fileCount": sum(1 for item in entries if item["type"] == "file"),
        "directoryCount": sum(1 for item in entries if item["type"] == "directory"),
        "totalBytes": sum(int(item["size"]) for item in entries if item["type"] == "file"),
        "complete": True,
        "errorCode": None,
        "errorMessage": None,
    }
    report["inventorySha256"] = canonical_sha256(_hash_payload(report))
    return report


def entries_by_path(inventory: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {entry["path"]: entry for entry in inventory.get("entries", []) if isinstance(entry, dict)}


def compare_inventory_hash(inventory: dict[str, Any]) -> bool:
    return inventory.get("inventorySha256") == canonical_sha256(_hash_payload(inventory))


def _scan(
    root: Path,
    path: Path,
    entries_by_path: dict[str, dict[str, Any]],
    seen_files: set[tuple[int, int] | str],
    service_paths: set[str],
    policy: ChangePolicy,
    max_entries: int,
    include_root: bool = True,
) -> None:
    try:
        stat_result = path.stat(follow_symlinks=False)
    except OSError as exc:
        raise InventoryError("REAL_TASK_INVENTORY_FAILED", f"Entry could not be inspected: {type(exc).__name__}.", _relative(root, path)) from exc
    if is_symlink_or_reparse(path):
        raise InventoryError("REAL_TASK_SOURCE_REPARSE", "Entry is a symlink or reparse point.", _relative(root, path))
    relative = _relative(root, path)
    if relative and len(relative) > policy.maxPathLength:
        raise InventoryError("REAL_TASK_INVENTORY_FAILED", "Path exceeds change policy maxPathLength.", relative)
    mode = stat_result.st_mode
    if include_root:
        if stat.S_ISDIR(mode):
            if relative == ".git":
                _add_entry(entries_by_path, _directory_entry(relative, service_paths), policy, max_entries)
                return
            _add_entry(entries_by_path, _directory_entry(relative, service_paths), policy, max_entries)
        elif stat.S_ISREG(mode):
            identity = _file_identity(path, stat_result)
            if identity in seen_files:
                return
            seen_files.add(identity)
            _add_entry(entries_by_path, _file_entry(root, path, stat_result, service_paths, policy), policy, max_entries)
            return
        else:
            raise InventoryError("REAL_TASK_INVENTORY_FAILED", "Only regular files and directories are supported.", relative)
    if stat.S_ISDIR(mode):
        try:
            children = sorted(path.iterdir(), key=lambda item: item.name.casefold())
        except OSError as exc:
            raise InventoryError("REAL_TASK_INVENTORY_FAILED", f"Directory could not be enumerated: {type(exc).__name__}.", relative) from exc
        for child in children:
            _scan(root, child, entries_by_path, seen_files, service_paths, policy, max_entries)


def _add_entry(entries_by_path: dict[str, dict[str, Any]], entry: dict[str, Any], policy: ChangePolicy, max_entries: int) -> None:
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
        raise InventoryError("REAL_TASK_SOURCE_LIMIT_EXCEEDED", "Change inventory entry limit exceeded.", path)
    entries_by_path[path] = entry


def _directory_entry(path: str, service_paths: set[str]) -> dict[str, Any]:
    service = is_service_path(path, service_paths)
    return {
        "path": path,
        "canonicalPathKey": unicodedata.normalize("NFC", path).casefold(),
        "type": "directory",
        "size": 0,
        "sha256": None,
        "extension": "",
        "contentKind": "service" if service else "directory",
        "textEncoding": None,
        "serviceKind": _service_kind(path, service_paths) if service else None,
        "serviceOrigin": "service_registry" if service else None,
        "expectedMutability": "immutable_service" if service else "mutable_task",
        "sourceClassification": "service" if service else _source_root(path),
        "symlink": False,
        "reparse": False,
    }


def _file_entry(root: Path, path: Path, stat_result: os.stat_result, service_paths: set[str], policy: ChangePolicy) -> dict[str, Any]:
    relative = _relative(root, path)
    service = is_service_path(relative, service_paths)
    content_kind, encoding = _classify_file(path, stat_result.st_size, service, policy)
    return {
        "path": relative,
        "canonicalPathKey": unicodedata.normalize("NFC", relative).casefold(),
        "type": "file",
        "size": stat_result.st_size,
        "sha256": file_sha256(path),
        "extension": path.suffix.lower(),
        "contentKind": content_kind,
        "textEncoding": encoding,
        "serviceKind": _service_kind(relative, service_paths) if service else None,
        "serviceOrigin": "service_registry" if service else None,
        "expectedMutability": "immutable_service" if service else "mutable_task",
        "sourceClassification": "service" if service else _source_root(relative),
        "symlink": False,
        "reparse": False,
    }


def _classify_file(path: Path, size: int, service: bool, policy: ChangePolicy) -> tuple[str, str | None]:
    if service:
        return "service", None
    if size > policy.maxTextReadBytes:
        return "binary", None
    read_bytes = min(size, policy.maxTextReadBytes + 1)
    with path.open("rb") as handle:
        data = handle.read(read_bytes)
    if b"\x00" in data:
        return "binary", None
    if data.startswith(b"\xff\xfe") or data.startswith(b"\xfe\xff"):
        return "binary", None
    if data.startswith(b"\xef\xbb\xbf"):
        try:
            data[3:].decode("utf-8")
        except UnicodeDecodeError:
            return "binary", None
        return "text", "utf-8-bom"
    try:
        data.decode("utf-8")
    except UnicodeDecodeError:
        return "binary", None
    return "text", "utf-8"


def _service_kind(path: str, service_paths: set[str]) -> str:
    if path == ".git" or path.startswith(".git/"):
        return "isolated_git"
    if path == ".agents" or path.startswith(".agents/"):
        return "empty_agents_directory"
    if path.endswith("AGENTS.md"):
        return "agents_instruction"
    if path == "task.json":
        return "task_snapshot"
    if path == "effective_policy.json":
        return "policy_snapshot"
    return "registered_service" if path in service_paths else "service_descendant"


def _source_root(path: str) -> str:
    return path.split("/", 1)[0] if "/" in path else path


def _relative(root: Path, path: Path) -> str:
    try:
        return path.resolve(strict=False).relative_to(root.resolve(strict=False)).as_posix()
    except ValueError:
        return path.as_posix()


def _file_identity(path: Path, stat_result: os.stat_result) -> tuple[int, int] | str:
    inode = getattr(stat_result, "st_ino", 0)
    device = getattr(stat_result, "st_dev", 0)
    if inode and device:
        return (int(device), int(inode))
    return str(path.resolve(strict=True)).casefold()


def _hash_payload(report: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in report.items() if key != "inventorySha256"}
