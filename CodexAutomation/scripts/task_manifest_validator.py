from __future__ import annotations

import hashlib
import json
import os
import re
import stat
import subprocess
from datetime import datetime, timezone
from pathlib import Path, PureWindowsPath
from typing import Any

from file_utils import read_json, write_json_atomic
from parent_git_snapshot import parent_git_snapshot
from real_task_models import ManifestValidationResult, REAL_TASK_STAGE, RealTaskError, RealTaskPolicy
from schema_validator import validate


REQUIRED_REAL_TASK_CONFIG_FIELDS = {
    "allowExecution",
    "allowParentProjectWrite",
    "allowAutomaticApply",
    "allowNetwork",
    "allowPackageInstall",
    "allowUnityLaunch",
    "allowAutomaticRetry",
    "allowAutomaticModelDowngrade",
    "allowAutomaticCreditUsage",
    "allowDirtyParentWorktree",
    "allowBinaryOutputs",
    "maxInvocations",
    "maxRepairAttempts",
    "maxChangedFiles",
    "maxChangedBytes",
    "maxSingleChangedFileBytes",
    "maxSourceFiles",
    "maxSourceBytes",
    "allowedSourceRoots",
    "forbiddenSourceRoots",
    "allowedTaskTypes",
    "allowedValidatorTypes",
}

DANGEROUS_FALSE_FIELDS = {
    "allowExecution",
    "allowParentProjectWrite",
    "allowAutomaticApply",
    "allowNetwork",
    "allowPackageInstall",
    "allowUnityLaunch",
    "allowAutomaticRetry",
    "allowAutomaticModelDowngrade",
    "allowAutomaticCreditUsage",
    "allowBinaryOutputs",
}

MANIFEST_REQUIRED_FIELDS = {
    "schemaVersion",
    "taskId",
    "title",
    "objective",
    "taskType",
    "sourcePaths",
    "allowedWritePaths",
    "expectedOutputs",
    "validationPlan",
    "completionCriteria",
}

MANIFEST_OPTIONAL_FIELDS = {"allowedDeletePaths", "roleBudget", "repairPolicy", "limits", "metadata"}
FORBIDDEN_USER_POLICY_FIELDS = {
    "networkPolicy",
    "packagePolicy",
    "gitPolicy",
    "unityPolicy",
    "applyPolicy",
    "sandboxPolicy",
    "approvalPolicy",
    "writableRoots",
    "model",
    "provider",
    "credits",
    "retryPolicy",
}
SERVICE_PATHS = {".git", ".agents", "AGENTS.md", "task.json"}
WINDOWS_RESERVED_NAMES = {
    "CON",
    "PRN",
    "AUX",
    "NUL",
    *(f"COM{index}" for index in range(1, 10)),
    *(f"LPT{index}" for index in range(1, 10)),
}
WILDCARD_CHARS = {"*", "?", "[", "]"}
TASK_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$")
TEXT_EXTENSIONS = {".cs", ".json", ".txt", ".md", ".asmdef", ".xml", ".yaml", ".yml", ".uxml", ".uss", ".shader", ".cginc"}
VALIDATOR_REQUIRED = {
    "file_exists": {"id", "type", "path"},
    "file_absent": {"id", "type", "path"},
    "json_valid": {"id", "type", "path"},
    "json_schema": {"id", "type", "path", "schemaName"},
    "json_field_equals": {"id", "type", "path", "jsonPointer", "expected"},
    "text_contains": {"id", "type", "path", "text"},
    "text_not_contains": {"id", "type", "path", "text"},
    "changed_paths_exact": {"id", "type", "paths"},
    "changed_paths_subset": {"id", "type", "paths"},
    "no_unexpected_files": {"id", "type"},
    "no_conflict_markers": {"id", "type"},
    "max_file_size": {"id", "type", "path", "maxBytes"},
    "max_changed_files": {"id", "type", "maxFiles"},
    "extension_allowlist": {"id", "type", "extensions"},
}
GIT_MAX_LINES = 5000
GIT_MAX_OUTPUT_CHARS = 20000
GIT_COMMAND_TIMEOUT_SECONDS = 10
REQUIRED_GIT_SNAPSHOT_COMMANDS = (
    "insideWorkTree",
    "porcelainStatus",
    "branch",
    "head",
    "stagedPaths",
)


def parse_real_task_policy(config: dict[str, Any], allow_execution_enabled: bool = False) -> tuple[RealTaskPolicy | None, list[RealTaskError]]:
    raw = config.get("realTasks")
    errors: list[RealTaskError] = []
    if not isinstance(raw, dict):
        return None, [_error("REAL_TASK_CONFIG_INVALID", "realTasks must be an object.", "realTasks")]
    unknown = sorted(set(raw) - REQUIRED_REAL_TASK_CONFIG_FIELDS)
    missing = sorted(REQUIRED_REAL_TASK_CONFIG_FIELDS - set(raw))
    for field in unknown:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Unknown realTasks field.", f"realTasks.{field}"))
    for field in missing:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Missing realTasks field.", f"realTasks.{field}"))
    dangerous_false = set(DANGEROUS_FALSE_FIELDS)
    if allow_execution_enabled:
        dangerous_false.remove("allowExecution")
    for field in dangerous_false:
        if raw.get(field) is not False:
            errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be exact false for {REAL_TASK_STAGE}.", f"realTasks.{field}"))
    if allow_execution_enabled and raw.get("allowExecution") is not True:
        errors.append(_error("REAL_TASK_PUBLIC_RUN_DISABLED", "realTasks.allowExecution must be exact true for public real-task execution.", "realTasks.allowExecution"))
    if raw.get("allowDirtyParentWorktree") is not True:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "allowDirtyParentWorktree must be exact true for plan mode.", "realTasks.allowDirtyParentWorktree"))
    exact_ints = {
        "maxInvocations": 3,
        "maxRepairAttempts": 1,
    }
    positive_ints = {
        "maxChangedFiles",
        "maxChangedBytes",
        "maxSingleChangedFileBytes",
        "maxSourceFiles",
        "maxSourceBytes",
    }
    for field, expected in exact_ints.items():
        value = raw.get(field)
        if not _is_int(value) or value != expected:
            errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be exact integer {expected}.", f"realTasks.{field}"))
    for field in positive_ints:
        value = raw.get(field)
        if not _is_int(value) or value <= 0:
            errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be a positive integer.", f"realTasks.{field}"))
    lists: dict[str, tuple[str, ...]] = {}
    for field in ("allowedSourceRoots", "forbiddenSourceRoots", "allowedTaskTypes", "allowedValidatorTypes"):
        value = raw.get(field)
        if not isinstance(value, list) or not value or not all(isinstance(item, str) and item.strip() for item in value):
            errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be a non-empty array of strings.", f"realTasks.{field}"))
            lists[field] = ()
            continue
        normalized = tuple(item.strip() for item in value)
        if len(set(normalized)) != len(normalized):
            errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must contain unique strings.", f"realTasks.{field}"))
        lists[field] = normalized
    if set(lists.get("allowedSourceRoots", ())) & set(lists.get("forbiddenSourceRoots", ())):
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Allowed and forbidden source roots must not overlap.", "realTasks"))
    if errors:
        return None, errors
    return RealTaskPolicy(
        allowExecution=raw["allowExecution"],
        allowParentProjectWrite=raw["allowParentProjectWrite"],
        allowAutomaticApply=raw["allowAutomaticApply"],
        allowNetwork=raw["allowNetwork"],
        allowPackageInstall=raw["allowPackageInstall"],
        allowUnityLaunch=raw["allowUnityLaunch"],
        allowAutomaticRetry=raw["allowAutomaticRetry"],
        allowAutomaticModelDowngrade=raw["allowAutomaticModelDowngrade"],
        allowAutomaticCreditUsage=raw["allowAutomaticCreditUsage"],
        allowDirtyParentWorktree=raw["allowDirtyParentWorktree"],
        allowBinaryOutputs=raw["allowBinaryOutputs"],
        maxInvocations=raw["maxInvocations"],
        maxRepairAttempts=raw["maxRepairAttempts"],
        maxChangedFiles=raw["maxChangedFiles"],
        maxChangedBytes=raw["maxChangedBytes"],
        maxSingleChangedFileBytes=raw["maxSingleChangedFileBytes"],
        maxSourceFiles=raw["maxSourceFiles"],
        maxSourceBytes=raw["maxSourceBytes"],
        allowedSourceRoots=lists["allowedSourceRoots"],
        forbiddenSourceRoots=lists["forbiddenSourceRoots"],
        allowedTaskTypes=lists["allowedTaskTypes"],
        allowedValidatorTypes=lists["allowedValidatorTypes"],
    ), []


def validate_task_manifest_file(
    root: Path,
    automation_root: Path,
    config: dict[str, Any],
    manifest_path: Path,
    inspect_sources: bool = False,
    allow_execution_enabled: bool = False,
) -> ManifestValidationResult:
    policy, policy_errors = parse_real_task_policy(config, allow_execution_enabled=allow_execution_enabled)
    manifest, manifest_hash, json_errors = _read_manifest(manifest_path)
    if policy_errors or json_errors or policy is None or manifest is None:
        return ManifestValidationResult(False, manifest, manifest_hash, None, tuple(policy_errors + json_errors))
    schema_errors = validate(manifest, read_json(automation_root / "schemas" / "real_task_manifest.schema.json"))
    errors = [_error("REAL_TASK_MANIFEST_SCHEMA_INVALID", message) for message in schema_errors]
    if isinstance(manifest, dict):
        errors.extend(_validate_manifest_semantics(manifest, policy, root, inspect_sources))
    if errors:
        plan = _build_effective_plan(root, manifest if isinstance(manifest, dict) else {}, policy, manifest_hash, [], None, False, errors, [])
        return ManifestValidationResult(False, manifest if isinstance(manifest, dict) else None, manifest_hash, plan, tuple(errors))
    source_inspection: list[dict[str, Any]] = []
    if inspect_sources:
        source_inspection, source_errors = inspect_source_paths(root, manifest["sourcePaths"], policy)
        errors.extend(source_errors)
    parent_snapshot = None
    if inspect_sources:
        parent_snapshot, git_errors = _parent_git_snapshot(root)
        errors.extend(git_errors)
    plan = _build_effective_plan(root, manifest, policy, manifest_hash, source_inspection, parent_snapshot, inspect_sources, errors, [])
    return ManifestValidationResult(not errors, manifest, manifest_hash, plan, tuple(errors))


def write_real_task_plan(root: Path, automation_root: Path, plan: dict[str, Any]) -> tuple[Path | None, RealTaskError | None]:
    report_errors = validate_real_task_plan_report(automation_root, plan)
    if report_errors:
        return None, _error("REAL_TASK_INTERNAL_ERROR", f"Plan report failed schema contract validation: {report_errors[0]}.")
    plan_id = str(plan["planId"])
    report_dir = automation_root / "runtime" / "real_task_plans" / plan_id
    report_path = report_dir / "REAL_TASK_PLAN_REPORT.json"
    try:
        report_dir.mkdir(parents=True, exist_ok=True)
        write_json_atomic(report_path, plan)
    except OSError as exc:
        return None, _error("REAL_TASK_PLAN_WRITE_FAILED", f"Plan report could not be written: {type(exc).__name__}.")
    return report_path, None


def validate_real_task_plan_report(automation_root: Path, plan: dict[str, Any]) -> list[str]:
    schema_errors = validate(plan, read_json(automation_root / "schemas" / "real_task_plan.schema.json"))
    if schema_errors:
        return schema_errors
    return _validate_parent_git_snapshot_contract(plan)


def _validate_parent_git_snapshot_contract(plan: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    snapshot = plan.get("parentGitSnapshot")
    if not isinstance(snapshot, dict):
        return ["parentGitSnapshot must be an object."]

    status = snapshot.get("status")
    command_results = snapshot.get("commandResults")
    if not isinstance(command_results, dict):
        return ["parentGitSnapshot.commandResults must be an object."]
    missing = [name for name in REQUIRED_GIT_SNAPSHOT_COMMANDS if name not in command_results]
    if missing:
        errors.append(f"parentGitSnapshot.commandResults missing required command {missing[0]}.")
    for name in REQUIRED_GIT_SNAPSHOT_COMMANDS:
        result = command_results.get(name)
        if not isinstance(result, dict):
            errors.append(f"parentGitSnapshot.commandResults.{name} must be an object.")
            continue
        command = result.get("command")
        if not isinstance(command, list) or not command or not all(isinstance(item, str) for item in command):
            errors.append(f"parentGitSnapshot.commandResults.{name}.command must be a non-empty string array.")

    if status == "success":
        errors.extend(_validate_success_git_snapshot(plan, snapshot, command_results))
    elif status == "failed":
        errors.extend(_validate_failed_git_snapshot(plan, snapshot, command_results))
    else:
        errors.append("parentGitSnapshot.status must be success or failed.")

    if plan.get("finalVerdict") == "PASS":
        if status != "success":
            errors.append("PASS finalVerdict requires a successful parentGitSnapshot.")
        if plan.get("errorCode") is not None:
            errors.append("PASS finalVerdict requires errorCode null.")
        if plan.get("errorMessage") is not None:
            errors.append("PASS finalVerdict requires errorMessage null.")
        if not isinstance(plan.get("dirtyParentWorktree"), bool):
            errors.append("PASS finalVerdict requires boolean dirtyParentWorktree.")
    return errors


def _validate_success_git_snapshot(plan: dict[str, Any], snapshot: dict[str, Any], command_results: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if snapshot.get("gitRepository") is not True:
        errors.append("successful parentGitSnapshot requires gitRepository true.")
    head = snapshot.get("head")
    if not isinstance(head, str) or not head.strip():
        errors.append("successful parentGitSnapshot requires a non-empty head.")
    if not isinstance(snapshot.get("dirty"), bool):
        errors.append("successful parentGitSnapshot requires boolean dirty.")
    if not isinstance(plan.get("dirtyParentWorktree"), bool):
        errors.append("successful parentGitSnapshot requires boolean dirtyParentWorktree.")
    elif snapshot.get("dirty") != plan.get("dirtyParentWorktree"):
        errors.append("dirtyParentWorktree must match parentGitSnapshot.dirty.")
    if not isinstance(snapshot.get("detachedHead"), bool):
        errors.append("successful parentGitSnapshot requires boolean detachedHead.")
    elif snapshot["detachedHead"]:
        if snapshot.get("branch") is not None:
            errors.append("detached parentGitSnapshot requires branch null.")
    else:
        branch = snapshot.get("branch")
        if not isinstance(branch, str) or not branch.strip():
            errors.append("non-detached parentGitSnapshot requires a non-empty branch.")
    if not command_results:
        errors.append("successful parentGitSnapshot requires commandResults.")
    for name in REQUIRED_GIT_SNAPSHOT_COMMANDS:
        result = command_results.get(name)
        if isinstance(result, dict):
            if result.get("success") is not True:
                errors.append(f"successful parentGitSnapshot requires {name} command success.")
            if result.get("failure") is not None:
                errors.append(f"successful parentGitSnapshot requires {name} failure null.")
    return errors


def _validate_failed_git_snapshot(plan: dict[str, Any], snapshot: dict[str, Any], command_results: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if snapshot.get("dirty") is not None:
        errors.append("failed parentGitSnapshot requires dirty null.")
    if plan.get("dirtyParentWorktree") is not None:
        errors.append("failed parentGitSnapshot requires dirtyParentWorktree null.")
    if plan.get("finalVerdict") == "PASS":
        errors.append("failed parentGitSnapshot cannot have PASS finalVerdict.")
    if plan.get("errorCode") != "REAL_TASK_GIT_SNAPSHOT_FAILED":
        errors.append("failed parentGitSnapshot requires REAL_TASK_GIT_SNAPSHOT_FAILED errorCode.")
    error_message = plan.get("errorMessage")
    if not isinstance(error_message, str) or not error_message.strip():
        errors.append("failed parentGitSnapshot requires a non-empty errorMessage.")
    failed_command_found = False
    for name in REQUIRED_GIT_SNAPSHOT_COMMANDS:
        result = command_results.get(name)
        if isinstance(result, dict) and (result.get("success") is False or result.get("failure") is not None):
            failed_command_found = True
    if not failed_command_found:
        errors.append("failed parentGitSnapshot requires failed command evidence.")
    return errors


def inspect_source_paths(root: Path, paths: list[str], policy: RealTaskPolicy) -> tuple[list[dict[str, Any]], list[RealTaskError]]:
    inspections: list[dict[str, Any]] = []
    errors: list[RealTaskError] = []
    total_files = 0
    total_bytes = 0
    seen_files: set[str] = set()
    for raw_path in paths:
        resolved = _resolve_repo_path(root, raw_path, policy, require_existing=True)
        if resolved["error"]:
            errors.append(resolved["error"])
            continue
        path = resolved["path"]
        assert isinstance(path, Path)
        entry = _inspect_single_source(root, path, raw_path, policy, seen_files)
        inspections.append(entry)
        if entry.get("errorCode"):
            errors.append(_error(str(entry["errorCode"]), str(entry["errorMessage"]), path=raw_path))
        total_files += int(entry.get("fileCount", 0))
        total_bytes += int(entry.get("totalBytes", 0))
    if total_files > policy.maxSourceFiles:
        errors.append(_error("REAL_TASK_SOURCE_LIMIT_EXCEEDED", "Source file count exceeds host limit.", "sourcePaths"))
    if total_bytes > policy.maxSourceBytes:
        errors.append(_error("REAL_TASK_SOURCE_LIMIT_EXCEEDED", "Source byte count exceeds host limit.", "sourcePaths"))
    return inspections, errors


def _validate_manifest_semantics(manifest: dict[str, Any], policy: RealTaskPolicy, root: Path, inspect_sources: bool) -> list[RealTaskError]:
    errors: list[RealTaskError] = []
    unknown = sorted(set(manifest) - MANIFEST_REQUIRED_FIELDS - MANIFEST_OPTIONAL_FIELDS - FORBIDDEN_USER_POLICY_FIELDS)
    for field in unknown:
        errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Unknown manifest field.", field))
    for field in sorted(FORBIDDEN_USER_POLICY_FIELDS & set(manifest)):
        errors.append(_error("REAL_TASK_POLICY_VIOLATION", "This policy field is host-only.", field))
    if not _is_int(manifest.get("schemaVersion")) or manifest.get("schemaVersion") != 1:
        errors.append(_error("REAL_TASK_MANIFEST_INVALID", "schemaVersion must be exact integer 1.", "schemaVersion"))
    task_id = manifest.get("taskId")
    if not isinstance(task_id, str) or not TASK_ID_RE.match(task_id):
        errors.append(_error("REAL_TASK_MANIFEST_INVALID", "taskId must be 3-64 safe characters and start with an alphanumeric character.", "taskId"))
    _string_field(manifest, "title", 160, errors)
    _string_field(manifest, "objective", 8000, errors)
    if manifest.get("taskType") not in policy.allowedTaskTypes:
        errors.append(_error("REAL_TASK_POLICY_VIOLATION", "taskType is not host-approved.", "taskType"))
    source_paths = _path_array(manifest, "sourcePaths", required=True, non_empty=True, policy=policy, root=root, inspect_sources=inspect_sources, errors=errors)
    errors.extend(_validate_source_overlaps(source_paths))
    write_paths = _path_array(manifest, "allowedWritePaths", required=True, non_empty=True, policy=policy, root=root, inspect_sources=False, errors=errors)
    delete_paths = _path_array(manifest, "allowedDeletePaths", required=False, non_empty=False, policy=policy, root=root, inspect_sources=False, errors=errors)
    for path in write_paths:
        if _is_service_path(path):
            errors.append(_error("REAL_TASK_PATH_UNSAFE", "allowedWritePaths cannot include service paths.", "allowedWritePaths", path))
        if not (_is_contained_by_any(path, source_paths) or _is_expected_output_path(path, manifest)):
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "allowedWritePath must be under a source path or declared expected output.", "allowedWritePaths", path))
    for path in delete_paths:
        if _is_service_path(path):
            errors.append(_error("REAL_TASK_PATH_UNSAFE", "allowedDeletePaths cannot include service paths.", "allowedDeletePaths", path))
        if not (_is_contained_by_any(path, source_paths) or _is_contained_by_any(path, write_paths)):
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "allowedDeletePath must be contained by sourcePaths or allowedWritePaths.", "allowedDeletePaths", path))
        if any(_is_contained_by_any(candidate, [path]) and candidate != path for candidate in source_paths + write_paths):
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "allowedDeletePaths cannot grant broad ancestor deletion.", "allowedDeletePaths", path))
    effective_limits = _effective_limits(manifest, policy, errors)
    errors.extend(_validate_expected_outputs(manifest, write_paths, policy))
    errors.extend(_validate_completion_criteria(manifest))
    errors.extend(_validate_budget(manifest, policy))
    errors.extend(_validate_validators(manifest, policy, source_paths, write_paths, delete_paths, effective_limits))
    errors.extend(_validate_metadata(manifest))
    return errors


def _read_manifest(path: Path) -> tuple[Any | None, str | None, list[RealTaskError]]:
    try:
        raw = path.read_bytes()
    except OSError as exc:
        return None, None, [_error("REAL_TASK_MANIFEST_JSON_INVALID", f"Manifest could not be read: {type(exc).__name__}.")]
    digest = hashlib.sha256(raw).hexdigest()
    try:
        data = json.loads(raw.decode("utf-8"), object_pairs_hook=_reject_duplicate_json_keys)
    except UnicodeDecodeError:
        return None, digest, [_error("REAL_TASK_MANIFEST_JSON_INVALID", "Manifest is not valid UTF-8 JSON.")]
    except json.JSONDecodeError:
        return None, digest, [_error("REAL_TASK_MANIFEST_JSON_INVALID", "Manifest is not valid UTF-8 JSON.")]
    except ValueError as exc:
        return None, digest, [_error("REAL_TASK_MANIFEST_JSON_INVALID", str(exc))]
    if not isinstance(data, dict):
        return data, digest, [_error("REAL_TASK_MANIFEST_SCHEMA_INVALID", "Manifest root must be an object.")]
    return data, digest, []


def _reject_duplicate_json_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key in manifest: {key}")
        result[key] = value
    return result


def _path_array(
    manifest: dict[str, Any],
    field: str,
    required: bool,
    non_empty: bool,
    policy: RealTaskPolicy,
    root: Path,
    inspect_sources: bool,
    errors: list[RealTaskError],
) -> list[str]:
    if field not in manifest:
        if required:
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Required path array is missing.", field))
        return []
    value = manifest.get(field)
    if not isinstance(value, list) or (non_empty and not value) or not all(isinstance(item, str) for item in value):
        errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Path field must be an array of strings.", field))
        return []
    if len(set(value)) != len(value):
        duplicate_code = "REAL_TASK_SOURCE_OVERLAP" if field == "sourcePaths" else "REAL_TASK_MANIFEST_INVALID"
        errors.append(_error(duplicate_code, "Path field contains duplicates.", field))
    valid: list[str] = []
    for path in value:
        path_error = validate_manifest_path(path, policy, source_path=(field == "sourcePaths"))
        if path_error:
            errors.append(RealTaskError(path_error.code, path_error.message, field, path))
            continue
        if inspect_sources:
            resolved = _resolve_repo_path(root, path, policy, require_existing=True)
            if resolved["error"]:
                errors.append(resolved["error"])
                continue
        valid.append(path)
    return valid


def _validate_source_overlaps(source_paths: list[str]) -> list[RealTaskError]:
    errors: list[RealTaskError] = []
    sorted_paths = sorted(source_paths, key=lambda item: (len(_path_parts(item)), item))
    for index, parent in enumerate(sorted_paths):
        for child in sorted_paths[index + 1 :]:
            if _path_contains(parent, child) and parent != child:
                errors.append(
                    _error(
                        "REAL_TASK_SOURCE_OVERLAP",
                        "sourcePaths cannot overlap; declare the narrower or broader path, not both.",
                        "sourcePaths",
                        f"{parent} | {child}",
                    )
                )
    return errors


def validate_manifest_path(path: Any, policy: RealTaskPolicy, source_path: bool = False) -> RealTaskError | None:
    if not isinstance(path, str) or not path:
        return _error("REAL_TASK_PATH_UNSAFE", "Path must be a non-empty string.")
    if any(ord(char) < 32 for char in path):
        return _error("REAL_TASK_PATH_UNSAFE", "Path contains control characters.")
    windows = PureWindowsPath(path)
    if Path(path).is_absolute() or windows.drive or windows.root or path.startswith(("/", "\\")):
        return _error("REAL_TASK_PATH_UNSAFE", "Path must be relative and cannot be absolute, UNC, or drive-relative.")
    if "\\" in path:
        return _error("REAL_TASK_PATH_UNSAFE", "Backslash is not allowed in manifest paths.")
    if ":" in path:
        return _error("REAL_TASK_PATH_UNSAFE", "Colon and NTFS alternate data stream syntax are not allowed.")
    if any(char in path for char in WILDCARD_CHARS):
        return _error("REAL_TASK_PATH_UNSAFE", "Wildcards and glob syntax are not allowed.")
    parts = path.split("/")
    if any(part in {"", "."} for part in parts):
        return _error("REAL_TASK_PATH_UNSAFE", "Path contains empty or dot components.")
    if any(part == ".." for part in parts):
        return _error("REAL_TASK_PATH_UNSAFE", "Parent traversal is not allowed.")
    for part in parts:
        if part.endswith(".") or part.endswith(" "):
            return _error("REAL_TASK_PATH_UNSAFE", "Path components cannot end with dot or space.")
        stem = part.split(".", 1)[0].upper()
        if stem in WINDOWS_RESERVED_NAMES:
            return _error("REAL_TASK_PATH_UNSAFE", "Windows reserved device names are not allowed.")
        if part in policy.forbiddenSourceRoots:
            return _error("REAL_TASK_PATH_UNSAFE", "Path enters a forbidden source root.")
    root = parts[0]
    if source_path and root not in policy.allowedSourceRoots:
        return _error("REAL_TASK_SOURCE_OUTSIDE_ALLOWED_ROOT", "Source path must be under an allowed source root.")
    if not source_path and root not in policy.allowedSourceRoots:
        return _error("REAL_TASK_SOURCE_OUTSIDE_ALLOWED_ROOT", "Task paths must stay under allowed source roots.")
    return None


def _resolve_repo_path(root: Path, raw_path: str, policy: RealTaskPolicy, require_existing: bool) -> dict[str, Any]:
    path_error = validate_manifest_path(raw_path, policy, source_path=True)
    if path_error:
        return {"path": None, "error": RealTaskError(path_error.code, path_error.message, path=raw_path)}
    repo_root = root.resolve(strict=True)
    current = repo_root
    parts = raw_path.split("/")
    for index, part in enumerate(parts):
        candidate = current / part
        if not candidate.exists() and not candidate.is_symlink():
            if require_existing:
                return {"path": None, "error": _error("REAL_TASK_SOURCE_MISSING", "Source path does not exist.", path=raw_path)}
            return {"path": current.joinpath(*parts[index:]), "error": None}
        if _is_symlink_or_reparse(candidate):
            return {"path": None, "error": _error("REAL_TASK_SOURCE_REPARSE", "Source path contains a symlink, junction, or reparse point.", path=raw_path)}
        try:
            resolved = candidate.resolve(strict=True)
            resolved.relative_to(repo_root)
        except (OSError, ValueError):
            return {"path": None, "error": _error("REAL_TASK_PATH_UNSAFE", "Source path resolves outside the repository.", path=raw_path)}
        if index == 0 and part not in policy.allowedSourceRoots:
            return {"path": None, "error": _error("REAL_TASK_SOURCE_OUTSIDE_ALLOWED_ROOT", "Source path is outside allowed roots.", path=raw_path)}
        if part in policy.forbiddenSourceRoots:
            return {"path": None, "error": _error("REAL_TASK_PATH_UNSAFE", "Source path enters forbidden root.", path=raw_path)}
        current = resolved
    return {"path": current, "error": None}


def _inspect_single_source(root: Path, path: Path, raw_path: str, policy: RealTaskPolicy, seen_files: set[str]) -> dict[str, Any]:
    if not path.exists() and not path.is_symlink():
        return {"path": raw_path, "type": "missing", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_MISSING", "errorMessage": "Source path does not exist."}
    stat_result, stat_error = _lstat_path(path)
    if stat_error:
        return {"path": raw_path, "type": "unknown", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": stat_error}
    assert stat_result is not None
    if path.is_symlink() or _stat_is_reparse(stat_result):
        return {"path": raw_path, "type": "reparse", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_REPARSE", "errorMessage": "Source path is a symlink, junction, or reparse point."}
    if stat.S_ISREG(stat_result.st_mode):
        return _inspect_regular_file(root, path, raw_path, seen_files)
    if not stat.S_ISDIR(stat_result.st_mode):
        return {"path": raw_path, "type": "other", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": "Source path is not a regular file or directory."}
    file_count = 0
    total_bytes = 0
    stack = [path]
    while stack:
        current_path = stack.pop()
        try:
            entries = sorted(os.scandir(current_path), key=lambda item: item.name)
        except OSError as exc:
            return {"path": raw_path, "type": "directory", "fileCount": file_count, "totalBytes": total_bytes, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": f"Source directory could not be enumerated: {type(exc).__name__}."}
        for entry in entries:
            child = Path(entry.path)
            try:
                child_stat = entry.stat(follow_symlinks=False)
            except OSError as exc:
                return {"path": raw_path, "type": "directory", "fileCount": file_count, "totalBytes": total_bytes, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": f"Source entry could not be inspected: {type(exc).__name__}."}
            if entry.is_symlink() or _stat_is_reparse(child_stat):
                return {"path": raw_path, "type": "directory", "fileCount": file_count, "totalBytes": total_bytes, "errorCode": "REAL_TASK_SOURCE_REPARSE", "errorMessage": "Source directory contains a symlink, junction, or reparse point."}
            if stat.S_ISDIR(child_stat.st_mode):
                stack.append(child)
                continue
            if not stat.S_ISREG(child_stat.st_mode):
                return {"path": raw_path, "type": "directory", "fileCount": file_count, "totalBytes": total_bytes, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": "Source directory contains a non-regular filesystem entry."}
            file_entry = _inspect_regular_file(root, child, raw_path, seen_files, child_stat)
            if file_entry.get("errorCode"):
                return {"path": raw_path, "type": "directory", "fileCount": file_count, "totalBytes": total_bytes, "errorCode": file_entry["errorCode"], "errorMessage": file_entry["errorMessage"]}
            file_count += int(file_entry.get("fileCount", 0))
            total_bytes += int(file_entry.get("totalBytes", 0))
            if file_count > policy.maxSourceFiles or total_bytes > policy.maxSourceBytes:
                return {"path": raw_path, "type": "directory", "fileCount": file_count, "totalBytes": total_bytes, "errorCode": "REAL_TASK_SOURCE_LIMIT_EXCEEDED", "errorMessage": "Source inventory exceeds host limits."}
    return {"path": raw_path, "type": "directory", "fileCount": file_count, "totalBytes": total_bytes, "underAllowedRoot": True}


def _inspect_regular_file(root: Path, path: Path, raw_path: str, seen_files: set[str], stat_result: os.stat_result | None = None) -> dict[str, Any]:
    if stat_result is None:
        stat_result, stat_error = _lstat_path(path)
        if stat_error:
            return {"path": raw_path, "type": "file", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": stat_error}
        assert stat_result is not None
    if not stat.S_ISREG(stat_result.st_mode):
        return {"path": raw_path, "type": "file", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": "Source entry is not a regular file."}
    if stat_result.st_size < 0:
        return {"path": raw_path, "type": "file", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": "Source file reported an invalid size."}
    try:
        identity = path.resolve(strict=True).relative_to(root.resolve(strict=True)).as_posix()
    except (OSError, ValueError):
        return {"path": raw_path, "type": "file", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_PATH_UNSAFE", "errorMessage": "Source file resolves outside repository."}
    if identity in seen_files:
        return {"path": raw_path, "type": "file", "fileCount": 0, "totalBytes": 0, "underAllowedRoot": True, "deduplicated": True}
    seen_files.add(identity)
    return {"path": raw_path, "type": "file", "fileCount": 1, "totalBytes": stat_result.st_size, "underAllowedRoot": True}


def _validate_expected_outputs(manifest: dict[str, Any], write_paths: list[str], policy: RealTaskPolicy) -> list[RealTaskError]:
    errors: list[RealTaskError] = []
    outputs = manifest.get("expectedOutputs")
    if not isinstance(outputs, list) or not outputs:
        return [_error("REAL_TASK_MANIFEST_INVALID", "expectedOutputs must be a non-empty array.", "expectedOutputs")]
    seen: set[str] = set()
    for index, output in enumerate(outputs):
        field = f"expectedOutputs[{index}]"
        if not isinstance(output, dict) or set(output) != {"path", "kind", "required"}:
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Expected output must contain only path, kind, and required.", field))
            continue
        path = output.get("path")
        path_error = validate_manifest_path(path, policy)
        if path_error:
            errors.append(RealTaskError(path_error.code, path_error.message, field, str(path)))
            continue
        assert isinstance(path, str)
        if path in seen:
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Duplicate expected output path.", field, path))
        seen.add(path)
        if output.get("kind") not in {"file", "directory"}:
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Expected output kind must be file or directory.", f"{field}.kind"))
        if not isinstance(output.get("required"), bool):
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Expected output required must be an exact bool.", f"{field}.required"))
        if not _is_contained_by_any(path, write_paths):
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Expected output must be inside allowedWritePaths.", field, path))
        if output.get("kind") == "file" and not _extension_allowed_for_text(path):
            errors.append(_error("REAL_TASK_POLICY_VIOLATION", "Binary output extensions are forbidden in 03B-2A.", field, path))
    return errors


def _validate_completion_criteria(manifest: dict[str, Any]) -> list[RealTaskError]:
    criteria = manifest.get("completionCriteria")
    if not isinstance(criteria, list) or not criteria:
        return [_error("REAL_TASK_MANIFEST_INVALID", "completionCriteria must be a non-empty array.", "completionCriteria")]
    errors: list[RealTaskError] = []
    if len(criteria) > 30:
        errors.append(_error("REAL_TASK_MANIFEST_INVALID", "completionCriteria cannot contain more than 30 entries.", "completionCriteria"))
    for index, item in enumerate(criteria):
        if not isinstance(item, str) or not item.strip() or len(item) > 1000 or _has_control(item):
            errors.append(_error("REAL_TASK_MANIFEST_INVALID", "Completion criterion must be non-empty text up to 1000 characters.", f"completionCriteria[{index}]"))
    return errors


def _validate_budget(manifest: dict[str, Any], policy: RealTaskPolicy) -> list[RealTaskError]:
    errors: list[RealTaskError] = []
    role_budget = manifest.get("roleBudget", {})
    if role_budget is None:
        role_budget = {}
    if not isinstance(role_budget, dict):
        return [_error("REAL_TASK_LIMIT_INVALID", "roleBudget must be an object.", "roleBudget")]
    if set(role_budget) - {"maxInvocations"}:
        errors.append(_error("REAL_TASK_POLICY_VIOLATION", "roleBudget contains unsupported fields.", "roleBudget"))
    if "maxInvocations" in role_budget and (not _is_int(role_budget["maxInvocations"]) or role_budget["maxInvocations"] < 1 or role_budget["maxInvocations"] > policy.maxInvocations):
        errors.append(_error("REAL_TASK_LIMIT_INVALID", "roleBudget.maxInvocations must be integer 1-3 and cannot exceed host policy.", "roleBudget.maxInvocations"))
    repair = manifest.get("repairPolicy", {})
    if repair is None:
        repair = {}
    if not isinstance(repair, dict):
        return errors + [_error("REAL_TASK_LIMIT_INVALID", "repairPolicy must be an object.", "repairPolicy")]
    if set(repair) - {"maxAttempts"}:
        errors.append(_error("REAL_TASK_POLICY_VIOLATION", "repairPolicy contains unsupported fields.", "repairPolicy"))
    if "maxAttempts" in repair and (not _is_int(repair["maxAttempts"]) or repair["maxAttempts"] < 0 or repair["maxAttempts"] > policy.maxRepairAttempts):
        errors.append(_error("REAL_TASK_LIMIT_INVALID", "repairPolicy.maxAttempts must be integer 0-1 and cannot exceed host policy.", "repairPolicy.maxAttempts"))
    return errors


def _effective_limits(manifest: dict[str, Any], policy: RealTaskPolicy, errors: list[RealTaskError]) -> dict[str, int]:
    limits = manifest.get("limits", {})
    if limits is None:
        limits = {}
    effective = {
        "maxChangedFiles": policy.maxChangedFiles,
        "maxChangedBytes": policy.maxChangedBytes,
        "maxSingleChangedFileBytes": policy.maxSingleChangedFileBytes,
    }
    if not isinstance(limits, dict):
        errors.append(_error("REAL_TASK_LIMIT_INVALID", "limits must be an object.", "limits"))
        return effective
    unknown = sorted(set(limits) - set(effective))
    for field in unknown:
        errors.append(_error("REAL_TASK_LIMIT_INVALID", "Unknown limits field.", f"limits.{field}"))
    for field, host_value in effective.items():
        if field not in limits:
            continue
        value = limits[field]
        if not _is_int(value) or value < 1:
            errors.append(_error("REAL_TASK_LIMIT_INVALID", "Limit must be a positive integer.", f"limits.{field}"))
        elif value > host_value:
            errors.append(_error("REAL_TASK_LIMIT_INVALID", "Manifest limit cannot exceed host cap.", f"limits.{field}"))
        else:
            effective[field] = value
    if effective["maxSingleChangedFileBytes"] > effective["maxChangedBytes"]:
        errors.append(_error("REAL_TASK_LIMIT_INVALID", "maxSingleChangedFileBytes cannot exceed maxChangedBytes.", "limits.maxSingleChangedFileBytes"))
    return effective


def _validate_validators(
    manifest: dict[str, Any],
    policy: RealTaskPolicy,
    source_paths: list[str],
    write_paths: list[str],
    delete_paths: list[str],
    limits: dict[str, int],
) -> list[RealTaskError]:
    validators = manifest.get("validationPlan")
    if not isinstance(validators, list) or not validators:
        return [_error("REAL_TASK_VALIDATOR_INVALID", "validationPlan must be a non-empty array.", "validationPlan")]
    errors: list[RealTaskError] = []
    if len(validators) > 50:
        errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "validationPlan cannot contain more than 50 validators.", "validationPlan"))
    ids: set[str] = set()
    scope = sorted(set(source_paths + write_paths + delete_paths + [output.get("path") for output in manifest.get("expectedOutputs", []) if isinstance(output, dict) and isinstance(output.get("path"), str)]))
    for index, validator in enumerate(validators):
        field = f"validationPlan[{index}]"
        if not isinstance(validator, dict):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Validator entry must be an object.", field))
            continue
        validator_type = validator.get("type")
        validator_id = validator.get("id")
        if not isinstance(validator_id, str) or not validator_id.strip() or _has_control(validator_id):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Validator id must be non-empty text.", f"{field}.id"))
        elif validator_id in ids:
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Validator id must be unique.", f"{field}.id"))
        else:
            ids.add(validator_id)
        if validator_type not in policy.allowedValidatorTypes:
            errors.append(_error("REAL_TASK_VALIDATOR_UNSUPPORTED", "Validator type is not host-approved.", f"{field}.type"))
            continue
        allowed_keys = VALIDATOR_REQUIRED[str(validator_type)]
        if validator_type == "no_conflict_markers":
            allowed_keys = allowed_keys | {"paths"}
        unknown = sorted(set(validator) - allowed_keys)
        for name in unknown:
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Validator contains forbidden parameter.", f"{field}.{name}"))
        for name in sorted(VALIDATOR_REQUIRED[str(validator_type)] - set(validator)):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Validator is missing a required parameter.", f"{field}.{name}"))
        for forbidden in ("command", "executable", "shell", "environment", "network"):
            if forbidden in validator:
                errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Command execution fields are forbidden.", f"{field}.{forbidden}"))
        errors.extend(_validate_validator_payload(field, validator, str(validator_type), policy, scope, limits))
    return errors


def _validate_validator_payload(field: str, validator: dict[str, Any], validator_type: str, policy: RealTaskPolicy, scope: list[str], limits: dict[str, int]) -> list[RealTaskError]:
    errors: list[RealTaskError] = []
    path_value = validator.get("path")
    if "path" in VALIDATOR_REQUIRED.get(validator_type, set()):
        errors.extend(_validate_scoped_path(path_value, policy, scope, f"{field}.path"))
    if validator_type in {"changed_paths_exact", "changed_paths_subset"}:
        paths = validator.get("paths")
        if not isinstance(paths, list) or not paths or not all(isinstance(item, str) for item in paths):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Validator paths must be a non-empty array of strings.", f"{field}.paths"))
        else:
            if len(set(paths)) != len(paths):
                errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Validator paths must be unique.", f"{field}.paths"))
            for path in paths:
                errors.extend(_validate_scoped_path(path, policy, scope, f"{field}.paths"))
    if validator_type == "no_conflict_markers" and "paths" in validator:
        paths = validator.get("paths")
        if not isinstance(paths, list) or not paths or not all(isinstance(item, str) for item in paths):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "no_conflict_markers paths must be a non-empty array of strings.", f"{field}.paths"))
        else:
            for path in paths:
                errors.extend(_validate_scoped_path(path, policy, scope, f"{field}.paths"))
    if validator_type == "json_schema":
        schema_name = validator.get("schemaName")
        if not isinstance(schema_name, str) or not re.match(r"^[A-Za-z0-9_.-]{1,80}$", schema_name) or "/" in schema_name or "\\" in schema_name or schema_name.endswith(".json"):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "schemaName must reference a host registry name, not a file path.", f"{field}.schemaName"))
    if validator_type == "json_field_equals":
        pointer = validator.get("jsonPointer")
        if not isinstance(pointer, str) or not pointer.startswith("/") or _has_control(pointer):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "jsonPointer must be an absolute JSON pointer string.", f"{field}.jsonPointer"))
    if validator_type in {"text_contains", "text_not_contains"}:
        text = validator.get("text")
        if not isinstance(text, str) or not text or _has_control(text):
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "text must be non-empty safe text.", f"{field}.text"))
    if validator_type == "max_file_size":
        value = validator.get("maxBytes")
        if not _is_int(value) or value < 1 or value > limits["maxSingleChangedFileBytes"]:
            errors.append(_error("REAL_TASK_LIMIT_INVALID", "maxBytes cannot exceed effective maxSingleChangedFileBytes.", f"{field}.maxBytes"))
    if validator_type == "max_changed_files":
        value = validator.get("maxFiles")
        if not _is_int(value) or value < 1 or value > limits["maxChangedFiles"]:
            errors.append(_error("REAL_TASK_LIMIT_INVALID", "maxFiles cannot exceed effective maxChangedFiles.", f"{field}.maxFiles"))
    if validator_type == "extension_allowlist":
        extensions = validator.get("extensions")
        if not isinstance(extensions, list) or not extensions:
            errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "extensions must be a non-empty array.", f"{field}.extensions"))
        else:
            seen: set[str] = set()
            for extension in extensions:
                if not isinstance(extension, str) or not re.match(r"^\.[a-z0-9]+$", extension):
                    errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Extensions must be lowercase and begin with dot.", f"{field}.extensions"))
                elif extension in seen:
                    errors.append(_error("REAL_TASK_VALIDATOR_INVALID", "Extensions must be unique.", f"{field}.extensions"))
                elif extension not in TEXT_EXTENSIONS:
                    errors.append(_error("REAL_TASK_POLICY_VIOLATION", "Binary output extensions are forbidden in 03B-2A.", f"{field}.extensions"))
                seen.add(str(extension))
    return errors


def _validate_scoped_path(path: Any, policy: RealTaskPolicy, scope: list[str], field: str) -> list[RealTaskError]:
    path_error = validate_manifest_path(path, policy)
    if path_error:
        return [RealTaskError(path_error.code, path_error.message, field, str(path))]
    assert isinstance(path, str)
    if not _is_contained_by_any(path, scope):
        return [_error("REAL_TASK_VALIDATOR_INVALID", "Validator path is outside declared task scope.", field, path)]
    return []


def _build_effective_plan(
    root: Path,
    manifest: dict[str, Any],
    policy: RealTaskPolicy,
    manifest_hash: str | None,
    source_inspection: list[dict[str, Any]],
    parent_snapshot: dict[str, Any] | None,
    inspected_sources: bool,
    errors: list[RealTaskError],
    warnings: list[dict[str, Any]],
) -> dict[str, Any]:
    created = _utc_now()
    task_id = str(manifest.get("taskId") or "invalid-task")
    plan_id = f"real_task_plan_{task_id}_{created.replace(':', '').replace('-', '').replace('.', '')}"
    effective_limits = _effective_limits(manifest, policy, [])
    role_budget = manifest.get("roleBudget") if isinstance(manifest.get("roleBudget"), dict) else {}
    repair_policy = manifest.get("repairPolicy") if isinstance(manifest.get("repairPolicy"), dict) else {}
    effective_policy = policy.to_dict()
    effective_policy.update(
        {
            "effectiveMaxInvocations": min(_safe_int(role_budget.get("maxInvocations"), policy.maxInvocations), policy.maxInvocations),
            "effectiveMaxRepairAttempts": min(_safe_int(repair_policy.get("maxAttempts"), policy.maxRepairAttempts), policy.maxRepairAttempts),
            "effectiveLimits": effective_limits,
            "auditorRequired": True,
            "parentWrite": False,
            "network": False,
            "packageInstall": False,
            "unityLaunch": False,
            "binaryOutputs": False,
            "automaticApply": False,
            "automaticRetry": False,
            "automaticModelDowngrade": False,
            "automaticCreditUsage": False,
        }
    )
    final_verdict = "PASS" if not errors and (not inspected_sources or source_inspection) else "FAILED"
    return {
        "planId": plan_id,
        "createdAt": created,
        "stage": REAL_TASK_STAGE,
        "taskId": task_id,
        "taskManifestVersion": manifest.get("schemaVersion"),
        "manifestSha256": manifest_hash,
        "manifestDisplayName": manifest.get("title"),
        "manifestValid": not errors,
        "effectivePolicy": effective_policy,
        "sourcePaths": list(manifest.get("sourcePaths", [])) if isinstance(manifest.get("sourcePaths"), list) else [],
        "allowedWritePaths": list(manifest.get("allowedWritePaths", [])) if isinstance(manifest.get("allowedWritePaths"), list) else [],
        "allowedDeletePaths": list(manifest.get("allowedDeletePaths", [])) if isinstance(manifest.get("allowedDeletePaths", []), list) else [],
        "expectedOutputs": manifest.get("expectedOutputs", []),
        "validationPlan": manifest.get("validationPlan", []),
        "completionCriteria": manifest.get("completionCriteria", []),
        "sourceInspection": {
            "inspected": inspected_sources,
            "entries": source_inspection,
            "totalFiles": sum(int(item.get("fileCount", 0)) for item in source_inspection),
            "totalBytes": sum(int(item.get("totalBytes", 0)) for item in source_inspection),
            "maxSourceFiles": policy.maxSourceFiles,
            "maxSourceBytes": policy.maxSourceBytes,
        },
        "parentGitSnapshot": parent_snapshot,
        "dirtyParentWorktree": parent_snapshot.get("dirty") if parent_snapshot else None,
        "modelInvocationPlanned": False,
        "modelInvocationStarted": False,
        "codexInvocationCount": 0,
        "sandboxStarted": False,
        "workspaceCreationPlanned": False,
        "workspaceCreated": False,
        "sourceCopyPlanned": False,
        "sourceCopied": False,
        "unityStarted": False,
        "networkUsed": False,
        "automaticRetry": False,
        "automaticModelDowngrade": False,
        "automaticCreditUsage": False,
        "automaticApply": False,
        "finalVerdict": final_verdict,
        "errorCode": errors[0].code if errors else None,
        "errorMessage": errors[0].message if errors else None,
        "errors": [error.to_dict() for error in errors],
        "warnings": warnings,
    }


def _parent_git_snapshot(root: Path) -> tuple[dict[str, Any], list[RealTaskError]]:
    return parent_git_snapshot(root)


def _run_git_command(root: Path, command: list[str]) -> dict[str, Any]:
    try:
        completed = subprocess.run(
            command,
            cwd=str(root),
            capture_output=True,
            text=True,
            check=False,
            timeout=GIT_COMMAND_TIMEOUT_SECONDS,
        )
    except OSError as exc:
        return {
            "command": command,
            "exitCode": None,
            "stdout": "",
            "stderr": _bounded_text(type(exc).__name__),
            "success": False,
            "failure": "process_start_failed",
        }
    except subprocess.SubprocessError as exc:
        return {
            "command": command,
            "exitCode": None,
            "stdout": "",
            "stderr": _bounded_text(type(exc).__name__),
            "success": False,
            "failure": "process_failed",
        }
    stdout = _bounded_text(completed.stdout)
    stderr = _bounded_text(completed.stderr)
    too_many_lines = len(stdout.splitlines()) > GIT_MAX_LINES
    too_large = len(completed.stdout or "") > GIT_MAX_OUTPUT_CHARS or len(completed.stderr or "") > GIT_MAX_OUTPUT_CHARS
    return {
        "command": command,
        "exitCode": completed.returncode,
        "stdout": stdout,
        "stderr": stderr,
        "success": completed.returncode == 0 and not too_many_lines and not too_large,
        "failure": "output_limit_exceeded" if completed.returncode == 0 and (too_many_lines or too_large) else None,
    }


def _public_git_result(result: dict[str, Any]) -> dict[str, Any]:
    return {
        "command": list(result["command"]),
        "exitCode": result["exitCode"],
        "success": result["success"],
        "stderr": result["stderr"],
        "failure": result["failure"],
    }


def _git_lines_from_result(result: dict[str, Any]) -> list[str]:
    if not result["success"]:
        return []
    return result["stdout"].splitlines()


def _git_single_line(result: dict[str, Any]) -> str | None:
    lines = _git_lines_from_result(result)
    if not lines:
        return ""
    if len(lines) > 1:
        return None
    return lines[0].strip()


def _bounded_text(value: str | None) -> str:
    text = value or ""
    if len(text) > GIT_MAX_OUTPUT_CHARS:
        return text[:GIT_MAX_OUTPUT_CHARS] + "...[truncated]"
    return text


def _lstat_path(path: Path) -> tuple[os.stat_result | None, str | None]:
    try:
        result = path.stat(follow_symlinks=False)
    except OSError as exc:
        return None, f"Source entry could not be inspected: {type(exc).__name__}."
    return result, None


def _stat_is_reparse(stat_result: os.stat_result) -> bool:
    return bool(getattr(stat_result, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _is_symlink_or_reparse(path: Path) -> bool:
    if path.is_symlink():
        return True
    try:
        attrs = path.stat(follow_symlinks=False).st_file_attributes
    except (AttributeError, OSError):
        return False
    return bool(attrs & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _is_expected_output_path(path: str, manifest: dict[str, Any]) -> bool:
    return any(isinstance(item, dict) and item.get("path") == path for item in manifest.get("expectedOutputs", []))


def _is_contained_by_any(path: str, containers: list[str]) -> bool:
    return any(path == container or _path_contains(container, path) for container in containers)


def _path_contains(container: str, child: str) -> bool:
    container_parts = _path_parts(container)
    child_parts = _path_parts(child)
    return len(container_parts) < len(child_parts) and child_parts[: len(container_parts)] == container_parts


def _path_parts(path: str) -> tuple[str, ...]:
    return tuple(part for part in path.split("/") if part)


def _is_service_path(path: str) -> bool:
    return path in SERVICE_PATHS or any(path.startswith(f"{service}/") for service in SERVICE_PATHS)


def _extension_allowed_for_text(path: str) -> bool:
    suffix = Path(path).suffix.lower()
    return suffix in TEXT_EXTENSIONS


def _string_field(manifest: dict[str, Any], field: str, limit: int, errors: list[RealTaskError]) -> None:
    value = manifest.get(field)
    if not isinstance(value, str) or not value.strip() or len(value) > limit or _has_control(value):
        errors.append(_error("REAL_TASK_MANIFEST_INVALID", f"{field} must be non-empty safe text up to {limit} characters.", field))


def _validate_metadata(manifest: dict[str, Any]) -> list[RealTaskError]:
    if "metadata" not in manifest:
        return []
    metadata = manifest["metadata"]
    if not isinstance(metadata, dict):
        return [_error("REAL_TASK_MANIFEST_INVALID", "metadata must be an object.", "metadata")]
    try:
        encoded = json.dumps(metadata, sort_keys=True, separators=(",", ":")).encode("utf-8")
    except (TypeError, ValueError):
        return [_error("REAL_TASK_MANIFEST_INVALID", "metadata must be JSON serializable.", "metadata")]
    if len(encoded) > 16 * 1024:
        return [_error("REAL_TASK_MANIFEST_INVALID", "metadata serialized size must not exceed 16 KiB.", "metadata")]
    return []


def _has_control(value: str) -> bool:
    return any(ord(char) < 32 for char in value)


def _is_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool)


def _safe_int(value: Any, default: int) -> int:
    return value if _is_int(value) else default


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _error(code: str, message: str, field: str | None = None, path: str | None = None) -> RealTaskError:
    return RealTaskError(code, message, field, path)
