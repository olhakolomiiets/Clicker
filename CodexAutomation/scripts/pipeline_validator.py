from __future__ import annotations

import hashlib
import fnmatch
import json
import os
import stat
from pathlib import Path
from pathlib import PureWindowsPath
from typing import Any

from file_utils import read_json
from schema_validator import validate


VALIDATION_TYPES = {
    "required_file",
    "forbidden_file",
    "json_valid",
    "json_field_equals",
    "json_field_type",
    "exact_allowed_changed_paths",
    "forbidden_changed_paths",
    "no_conflict_markers",
    "no_unexpected_files",
}

PATH_ERROR_CODES = {
    "UNSAFE_ABSOLUTE_PATH",
    "UNSAFE_PARENT_TRAVERSAL",
    "UNSAFE_PATH_OUTSIDE_WORKSPACE",
    "UNSAFE_SYMLINK_OR_REPARSE_POINT",
    "INVALID_VALIDATION_PATH",
    "WORKSPACE_ROOT_NOT_FOUND",
    "WORKSPACE_ROOT_NOT_DIRECTORY",
}

WORKSPACE_ERROR_ENTRY = "__workspace_error__"
WILDCARD_CHARS = {"*", "?"}


def validate_pipeline_task(task: dict[str, Any], schema_path: Path) -> list[str]:
    errors = validate(task, read_json(schema_path))
    for key in ("id", "title", "description", "initialImplementationInstructions", "auditInstructions"):
        if not isinstance(task.get(key), str) or not task[key].strip():
            errors.append(f"INVALID_TASK: {key} must be a non-empty string.")
    for key in ("acceptanceCriteria", "allowedWritePaths", "forbiddenWritePaths", "nonGoals"):
        if not isinstance(task.get(key), list) or not all(isinstance(item, str) for item in task.get(key, [])):
            errors.append(f"INVALID_TASK: {key} must be an array of strings.")
    if task.get("mode") != "pipeline_fake_test":
        errors.append("REAL_WORKSPACE_WRITE_DISABLED")
    validations = task.get("validations")
    if not isinstance(validations, list):
        errors.append("INVALID_TASK: validations must be an array.")
    else:
        for validation in validations:
            if not isinstance(validation, dict) or validation.get("type") not in VALIDATION_TYPES:
                errors.append("INVALID_TASK: validation type is not allowed.")
    return errors


def snapshot_workspace(workspace: Path) -> dict[str, dict[str, Any]]:
    snapshot: dict[str, dict[str, Any]] = {}
    if _is_symlink_or_reparse(workspace):
        return _workspace_error("UNSAFE_SYMLINK_OR_REPARSE_POINT", "Workspace root is a symlink or reparse point.")
    if not workspace.exists():
        return _workspace_error("WORKSPACE_ROOT_NOT_FOUND", "Workspace root does not exist.")
    if not workspace.is_dir():
        return _workspace_error("WORKSPACE_ROOT_NOT_DIRECTORY", "Workspace root is not a directory.")
    root = workspace.resolve(strict=True)
    for path in _safe_walk(workspace):
        relative = path.relative_to(root).as_posix()
        if _is_symlink_or_reparse(path):
            snapshot[relative] = {"type": "blocked_link", "size": 0, "sha256": None}
        elif path.is_dir():
            snapshot[relative] = {"type": "directory", "size": 0, "sha256": None}
        elif path.is_file():
            data = path.read_bytes()
            snapshot[relative] = {
                "type": "file",
                "size": len(data),
                "sha256": hashlib.sha256(data).hexdigest(),
            }
    return snapshot


def changed_paths(before: dict[str, Any], after: dict[str, Any]) -> list[str]:
    if snapshot_error(before) or snapshot_error(after):
        return []
    return sorted(set(before.keys()).symmetric_difference(after.keys()) | {path for path in before if path in after and before[path] != after[path]})


def snapshot_error(snapshot: dict[str, Any]) -> dict[str, Any] | None:
    error = snapshot.get(WORKSPACE_ERROR_ENTRY)
    return error if isinstance(error, dict) else None


def run_validations(task: dict[str, Any], workspace: Path, before_snapshot: dict[str, Any], after_snapshot: dict[str, Any]) -> dict[str, Any]:
    results: list[dict[str, Any]] = []
    snapshot_problem = snapshot_error(before_snapshot) or snapshot_error(after_snapshot)
    if snapshot_problem:
        result = _result(
            snapshot_problem["errorCode"],
            "BLOCKED",
            snapshot_problem["message"],
            None,
            "safe workspace root",
            snapshot_problem["errorCode"],
        )
        return {
            "verdict": "BLOCKED",
            "results": [result],
            "findings": [_finding_from_validation(result)],
            "actualChangedPaths": [],
        }
    actual_changed = changed_paths(before_snapshot, after_snapshot)
    for validation in task.get("validations", []):
        result = _run_validation(validation, workspace, actual_changed)
        results.append(result)
    verdict = "PASS"
    findings: list[dict[str, Any]] = []
    for result in results:
        if result["verdict"] == "BLOCKED":
            verdict = "BLOCKED"
        elif result["verdict"] == "FIX_REQUIRED" and verdict != "BLOCKED":
            verdict = "FIX_REQUIRED"
        if result["verdict"] != "PASS":
            findings.append(_finding_from_validation(result))
    return {
        "verdict": verdict,
        "results": results,
        "findings": findings,
        "actualChangedPaths": actual_changed,
    }


def _run_validation(validation: dict[str, Any], workspace: Path, actual_changed: list[str]) -> dict[str, Any]:
    code = str(validation.get("code", validation.get("type", "VALIDATION")))
    validation_type = validation.get("type")
    raw_file = validation.get("file", "")
    file_name = raw_file if isinstance(raw_file, str) else ""
    if validation_type in {"required_file", "forbidden_file", "json_valid", "json_field_equals", "json_field_type"}:
        resolved = resolve_workspace_path(workspace, raw_file)
        if resolved["errorCode"]:
            return _result(code, "BLOCKED", resolved["errorCode"], raw_file if isinstance(raw_file, str) else None, "safe workspace path", resolved["errorCode"])
        path = resolved["path"]
    else:
        path = workspace
    if validation_type == "required_file":
        return _result(code, "PASS" if path.is_file() else "FIX_REQUIRED", "Required file exists.", file_name, True, path.is_file())
    if validation_type == "forbidden_file":
        return _result(code, "FIX_REQUIRED" if path.exists() else "PASS", "Forbidden file must not exist.", file_name, False, path.exists())
    if validation_type == "json_valid":
        try:
            json.loads(path.read_text(encoding="utf-8"))
            return _result(code, "PASS", "JSON is valid.", file_name, "valid json", "valid json")
        except Exception as exc:
            return _result(code, "FIX_REQUIRED", "JSON is invalid.", file_name, "valid json", str(exc))
    if validation_type in {"json_field_equals", "json_field_type"}:
        data = json.loads(path.read_text(encoding="utf-8"))
        field = str(validation.get("field"))
        actual = data.get(field)
        if validation_type == "json_field_equals":
            expected = validation.get("expected")
            return _result(code, "PASS" if actual == expected else "FIX_REQUIRED", "JSON field equals expected value.", file_name, expected, actual)
        expected_type = validation.get("expectedType")
        ok = (expected_type == "integer" and isinstance(actual, int) and not isinstance(actual, bool)) or (
            expected_type == "boolean" and isinstance(actual, bool)
        ) or (expected_type == "string" and isinstance(actual, str))
        return _result(code, "PASS" if ok else "FIX_REQUIRED", "JSON field has expected type.", file_name, expected_type, type(actual).__name__)
    if validation_type == "exact_allowed_changed_paths":
        expected = _safe_path_patterns(workspace, validation.get("paths", []))
        if expected["errorCode"]:
            return _result(code, "BLOCKED", expected["errorCode"], None, "safe workspace paths", expected["errorCode"])
        expected_paths = expected["patterns"]
        ok = _changed_paths_match_patterns(actual_changed, expected_paths, exact=True)
        return _result(code, "PASS" if ok else "FIX_REQUIRED", "Changed paths must match exactly.", None, expected_paths, actual_changed)
    if validation_type == "forbidden_changed_paths":
        forbidden_result = _safe_path_patterns(workspace, validation.get("paths", []))
        if forbidden_result["errorCode"]:
            return _result(code, "BLOCKED", forbidden_result["errorCode"], None, "safe workspace paths", forbidden_result["errorCode"])
        forbidden = forbidden_result["patterns"]
        found = sorted(path for path in actual_changed if _matches_any(path, forbidden))
        return _result(code, "PASS" if not found else "FIX_REQUIRED", "Forbidden paths must not change.", None, [], found)
    if validation_type == "no_conflict_markers":
        found = []
        root = workspace.resolve(strict=True)
        for file_path in _safe_walk(workspace):
            if file_path.is_file() and "<<<<<<<" in file_path.read_text(encoding="utf-8", errors="ignore"):
                found.append(file_path.relative_to(root).as_posix())
        return _result(code, "PASS" if not found else "BLOCKED", "No conflict markers.", None, [], found)
    if validation_type == "no_unexpected_files":
        expected_result = _safe_path_patterns(workspace, validation.get("paths", []))
        if expected_result["errorCode"]:
            return _result(code, "BLOCKED", expected_result["errorCode"], None, "safe workspace paths", expected_result["errorCode"])
        expected = expected_result["patterns"]
        current_snapshot = snapshot_workspace(workspace)
        current_error = snapshot_error(current_snapshot)
        if current_error:
            return _result(code, "BLOCKED", current_error["errorCode"], None, "safe workspace root", current_error["errorCode"])
        actual = {path for path, info in current_snapshot.items() if info["type"] != "directory"}
        unexpected = sorted(path for path in actual if not _matches_any(path, expected))
        return _result(code, "PASS" if not unexpected else "FIX_REQUIRED", "No unexpected files.", None, sorted(expected), unexpected)
    return _result(code, "BLOCKED", "Unsupported validation.", file_name, validation_type, None)


def resolve_workspace_path(workspace_root: Path, relative_path: Any) -> dict[str, Any]:
    if not isinstance(relative_path, str) or not relative_path.strip():
        return {"path": None, "errorCode": "INVALID_VALIDATION_PATH"}
    raw = relative_path.strip()
    windows = PureWindowsPath(raw)
    if Path(raw).is_absolute() or windows.drive or windows.root or raw.startswith(("/", "\\")):
        return {"path": None, "errorCode": "UNSAFE_ABSOLUTE_PATH"}
    parts = [part for part in raw.replace("\\", "/").split("/") if part not in {"", "."}]
    if not parts:
        return {"path": None, "errorCode": "INVALID_VALIDATION_PATH"}
    if any(part == ".." for part in parts):
        return {"path": None, "errorCode": "UNSAFE_PARENT_TRAVERSAL"}

    if _is_symlink_or_reparse(workspace_root):
        return {"path": None, "errorCode": "UNSAFE_SYMLINK_OR_REPARSE_POINT"}
    if not workspace_root.exists():
        return {"path": None, "errorCode": "WORKSPACE_ROOT_NOT_FOUND"}
    if not workspace_root.is_dir():
        return {"path": None, "errorCode": "WORKSPACE_ROOT_NOT_DIRECTORY"}
    try:
        root = workspace_root.resolve(strict=True)
    except FileNotFoundError:
        return {"path": None, "errorCode": "WORKSPACE_ROOT_NOT_FOUND"}

    current = root
    for index, part in enumerate(parts):
        candidate = current / part
        if candidate.exists() or candidate.is_symlink():
            if _is_symlink_or_reparse(candidate):
                return {"path": None, "errorCode": "UNSAFE_SYMLINK_OR_REPARSE_POINT"}
            try:
                candidate_resolved = candidate.resolve(strict=True)
                candidate_resolved.relative_to(root)
            except (FileNotFoundError, ValueError):
                return {"path": None, "errorCode": "UNSAFE_PATH_OUTSIDE_WORKSPACE"}
            current = candidate_resolved
        else:
            try:
                current.relative_to(root)
            except ValueError:
                return {"path": None, "errorCode": "UNSAFE_PATH_OUTSIDE_WORKSPACE"}
            missing_tail = parts[index:]
            return {"path": current.joinpath(*missing_tail), "errorCode": None}
    return {"path": current, "errorCode": None}


def validate_workspace_relative_path_pattern(workspace_root: Path, raw_path: Any) -> dict[str, Any]:
    if not isinstance(raw_path, str) or not raw_path.strip():
        return {"pattern": None, "errorCode": "INVALID_VALIDATION_PATH"}
    raw = raw_path.strip()
    windows = PureWindowsPath(raw)
    if Path(raw).is_absolute() or windows.drive or windows.root or raw.startswith(("/", "\\")):
        return {"pattern": None, "errorCode": "UNSAFE_ABSOLUTE_PATH"}
    parts = [part for part in raw.replace("\\", "/").split("/") if part not in {"", "."}]
    if not parts:
        return {"pattern": None, "errorCode": "INVALID_VALIDATION_PATH"}
    if any(part == ".." for part in parts):
        return {"pattern": None, "errorCode": "UNSAFE_PARENT_TRAVERSAL"}
    if not any(_has_wildcard(part) for part in parts):
        resolved = resolve_workspace_path(workspace_root, raw)
        if resolved["errorCode"]:
            return {"pattern": None, "errorCode": resolved["errorCode"]}
        return {"pattern": "/".join(parts), "errorCode": None}

    if _is_symlink_or_reparse(workspace_root):
        return {"pattern": None, "errorCode": "UNSAFE_SYMLINK_OR_REPARSE_POINT"}
    if not workspace_root.exists():
        return {"pattern": None, "errorCode": "WORKSPACE_ROOT_NOT_FOUND"}
    if not workspace_root.is_dir():
        return {"pattern": None, "errorCode": "WORKSPACE_ROOT_NOT_DIRECTORY"}
    try:
        root = workspace_root.resolve(strict=True)
    except FileNotFoundError:
        return {"pattern": None, "errorCode": "WORKSPACE_ROOT_NOT_FOUND"}
    current = root
    for part in parts:
        if _has_wildcard(part):
            break
        candidate = current / part
        if candidate.exists() or candidate.is_symlink():
            if _is_symlink_or_reparse(candidate):
                return {"pattern": None, "errorCode": "UNSAFE_SYMLINK_OR_REPARSE_POINT"}
            try:
                candidate.resolve(strict=True).relative_to(root)
            except (FileNotFoundError, ValueError):
                return {"pattern": None, "errorCode": "UNSAFE_PATH_OUTSIDE_WORKSPACE"}
            current = candidate
        else:
            break
    return {"pattern": "/".join(parts), "errorCode": None}


def _safe_walk(workspace: Path) -> list[Path]:
    root = workspace.resolve(strict=True)
    collected: list[Path] = []
    for current, dir_names, file_names in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        safe_dirs = []
        for dir_name in sorted(dir_names):
            path = current_path / dir_name
            if _is_symlink_or_reparse(path):
                collected.append(path)
                continue
            safe_dirs.append(dir_name)
        dir_names[:] = safe_dirs
        for name in sorted(safe_dirs + file_names):
            path = current_path / name
            if _is_symlink_or_reparse(path):
                collected.append(path)
                continue
            try:
                path.resolve(strict=True).relative_to(root)
            except (FileNotFoundError, ValueError):
                continue
            collected.append(path)
    return sorted(set(collected))


def _is_symlink_or_reparse(path: Path) -> bool:
    if path.is_symlink():
        return True
    try:
        attrs = path.stat(follow_symlinks=False).st_file_attributes
    except (AttributeError, OSError):
        return False
    return bool(attrs & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _workspace_error(error_code: str, message: str) -> dict[str, dict[str, Any]]:
    return {WORKSPACE_ERROR_ENTRY: {"type": "error", "errorCode": error_code, "message": message}}


def _safe_path_patterns(workspace: Path, paths: Any) -> dict[str, Any]:
    if not isinstance(paths, list):
        return {"patterns": [], "errorCode": "INVALID_VALIDATION_PATH"}
    patterns: list[str] = []
    for raw_path in paths:
        result = validate_workspace_relative_path_pattern(workspace, raw_path)
        if result["errorCode"]:
            return {"patterns": [], "errorCode": result["errorCode"]}
        patterns.append(result["pattern"])
    return {"patterns": sorted(patterns), "errorCode": None}


def _has_wildcard(part: str) -> bool:
    return any(char in part for char in WILDCARD_CHARS)


def _matches_any(path: str, patterns: list[str]) -> bool:
    return any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)


def _changed_paths_match_patterns(actual_paths: list[str], patterns: list[str], exact: bool) -> bool:
    if not exact:
        return all(_matches_any(path, patterns) for path in actual_paths)
    if not any(any(char in pattern for char in WILDCARD_CHARS) for pattern in patterns):
        return actual_paths == patterns
    return all(_matches_any(path, patterns) for path in actual_paths) and all(any(fnmatch.fnmatchcase(path, pattern) for path in actual_paths) for pattern in patterns)


def _result(code: str, verdict: str, message: str, file_name: str | None, expected: Any, actual: Any) -> dict[str, Any]:
    return {
        "code": code,
        "verdict": verdict,
        "message": message,
        "evidence": f"expected={expected!r}; actual={actual!r}",
        "file": file_name,
        "expected": expected,
        "actual": actual,
    }


def _finding_from_validation(result: dict[str, Any]) -> dict[str, Any]:
    return {
        "code": result["code"],
        "severity": "blocking" if result["verdict"] == "BLOCKED" else "high",
        "file": result.get("file") or "",
        "symbol": "",
        "problem": result["message"],
        "evidence": result["evidence"],
        "requiredFix": "Satisfy the validation result within the current task scope.",
    }
