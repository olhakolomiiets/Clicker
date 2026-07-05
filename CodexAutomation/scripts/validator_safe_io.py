from __future__ import annotations

import json
from collections.abc import Mapping
from pathlib import Path, PureWindowsPath
from typing import Any

from validator_models import ValidationFailure, ValidatorContext, ValidatorReadableScope, ValidatorScopeKind
from workspace_inventory import is_symlink_or_reparse


WINDOWS_RESERVED_NAMES = {
    "CON",
    "PRN",
    "AUX",
    "NUL",
    *(f"COM{index}" for index in range(1, 10)),
    *(f"LPT{index}" for index in range(1, 10)),
}


def resolve_workspace_path(context: ValidatorContext, relative_path: Any, require_existing: bool = True) -> Path:
    if not isinstance(relative_path, str) or not relative_path:
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Validator path must be non-empty repository-relative text.")
    if "\\" in relative_path:
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Backslash path syntax is forbidden.")
    normalized = relative_path
    windows = PureWindowsPath(normalized)
    if Path(normalized).is_absolute() or windows.drive or windows.root or normalized.startswith(("/", "\\")):
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Absolute, UNC, and drive-relative paths are forbidden.")
    if ":" in normalized:
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Colon and NTFS ADS syntax are forbidden.")
    raw_parts = normalized.split("/")
    parts = [part for part in raw_parts if part]
    if not parts or len(parts) != len(raw_parts) or any(part in {"", ".", ".."} for part in parts):
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Empty, dot, and parent path components are forbidden.")
    for part in parts:
        if any(ord(char) < 32 for char in part) or part.endswith((" ", ".")):
            raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Unsafe path component.")
        stem = part.split(".", 1)[0].upper()
        if stem in WINDOWS_RESERVED_NAMES:
            raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Windows reserved path component.")
    if _is_service_path(normalized):
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Validator target cannot be a service path.")
    if not _in_scopes(normalized, context.readableScopes):
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Validator path is outside validation-readable scope.")
    workspace = context.workspaceDirectory.resolve(strict=True)
    target = (workspace / normalized).resolve(strict=False)
    try:
        target.relative_to(workspace)
    except ValueError as exc:
        raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Validator path escapes workspace.") from exc
    _validate_component_chain(workspace, target, require_existing)
    return target


def final_entry(context: ValidatorContext, relative_path: str) -> dict[str, Any] | None:
    return _entries_by_path(context.finalInventory).get(relative_path)


def baseline_entry(context: ValidatorContext, relative_path: str) -> dict[str, Any] | None:
    return _entries_by_path(context.baselineInventory).get(relative_path)


def read_bounded_text(context: ValidatorContext, relative_path: str) -> str:
    path = resolve_workspace_path(context, relative_path, require_existing=True)
    limit = context.validationPolicy.maxTextReadBytes
    data = path.read_bytes()
    if len(data) > limit:
        raise ValidationFailure("REAL_TASK_VALIDATOR_READ_FAILED", "Text target exceeds maxTextReadBytes.")
    if b"\x00" in data:
        raise ValidationFailure("REAL_TASK_VALIDATOR_READ_FAILED", "Text target contains NUL bytes.")
    if data.startswith(b"\xef\xbb\xbf"):
        return data[3:].decode("utf-8")
    return data.decode("utf-8")


def read_strict_json(context: ValidatorContext, relative_path: str) -> Any:
    path = resolve_workspace_path(context, relative_path, require_existing=True)
    data = path.read_bytes()
    if len(data) > context.validationPolicy.maxJsonReadBytes:
        raise ValidationFailure("REAL_TASK_VALIDATOR_READ_FAILED", "JSON target exceeds maxJsonReadBytes.")
    if b"\x00" in data:
        raise ValidationFailure("REAL_TASK_VALIDATOR_READ_FAILED", "JSON target contains NUL bytes.")
    if data.startswith(b"\xef\xbb\xbf"):
        text = data[3:].decode("utf-8")
    else:
        text = data.decode("utf-8")

    def reject_constant(value: str) -> None:
        raise ValueError(f"Unsupported JSON constant {value}.")

    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"Duplicate JSON key {key}.")
            result[key] = value
        return result

    decoder = json.JSONDecoder(object_pairs_hook=reject_duplicates, parse_constant=reject_constant)
    parsed, end = decoder.raw_decode(text)
    if text[end:].strip():
        raise ValueError("Trailing JSON data.")
    return parsed


def changed_file_paths(context: ValidatorContext) -> tuple[str, ...]:
    return tuple(path for path in changed_paths(context) if _changed_path_is_file(context, path))


def changed_paths(context: ValidatorContext) -> tuple[str, ...]:
    paths: set[str] = set()
    for category in ("added", "modified", "deleted", "typeChanged"):
        for item in context.diffReport.get("categories", {}).get(category, []):
            paths.add(str(item["path"]))
    return tuple(sorted(paths))


def added_file_paths(context: ValidatorContext) -> tuple[str, ...]:
    result: list[str] = []
    for item in context.diffReport.get("categories", {}).get("added", []):
        after = item.get("after")
        if after and after.get("type") == "file":
            result.append(str(item["path"]))
    return tuple(sorted(result))


def _validate_component_chain(workspace: Path, target: Path, require_existing: bool) -> None:
    current = workspace
    parts = target.relative_to(workspace).parts
    inspect_parts = parts if require_existing else parts[:-1]
    for part in inspect_parts:
        current = current / part
        try:
            current.stat(follow_symlinks=False)
        except OSError as exc:
            raise ValidationFailure("REAL_TASK_VALIDATOR_READ_FAILED", "Validator path component cannot be inspected.") from exc
        if is_symlink_or_reparse(current):
            raise ValidationFailure("REAL_TASK_VALIDATOR_PATH_UNSAFE", "Validator path contains symlink or reparse component.")
    if require_existing and not target.exists():
        raise ValidationFailure("REAL_TASK_VALIDATOR_READ_FAILED", "Validator target does not exist.")


def _in_scopes(path: str, scopes: tuple[ValidatorReadableScope, ...]) -> bool:
    for scope in scopes:
        if isinstance(scope, str):
            if path == scope:
                return True
            continue
        if scope.service or not isinstance(scope.normalizedPath, str):
            continue
        normalized = scope.normalizedPath.rstrip("/")
        if scope.scopeKind == ValidatorScopeKind.FILE.value:
            if path == normalized:
                return True
        elif scope.scopeKind == ValidatorScopeKind.DIRECTORY.value:
            if path == normalized:
                return True
            if scope.recursive and path.startswith(f"{normalized}/"):
                return True
    return False


def _is_service_path(path: str) -> bool:
    services = {"AGENTS.md", "task.json", "effective_policy.json", ".agents", ".git", ".codex", "CodexAutomation"}
    return path in services or any(path.startswith(f"{service}/") for service in services) or path.endswith("/AGENTS.md")


def _entries_by_path(inventory: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {str(entry["path"]): entry for entry in inventory.get("entries", []) if isinstance(entry, Mapping)}


def _changed_path_is_file(context: ValidatorContext, path: str) -> bool:
    entry = final_entry(context, path) or baseline_entry(context, path)
    return isinstance(entry, Mapping) and entry.get("type") == "file"
