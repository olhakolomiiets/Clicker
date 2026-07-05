from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import PureWindowsPath
from typing import Any

from real_task_models import RealTaskError, RealTaskPolicy


@dataclass(frozen=True)
class ChangePolicy:
    renameDetection: bool
    allowExtensionlessOutputs: bool
    allowUtf8Bom: bool
    allowUtf16: bool
    allowBinaryChanges: bool
    allowBinaryDeletes: bool
    maxPathLength: int
    maxTextReadBytes: int
    maxDiffReportBytes: int
    allowedChangedTextExtensions: tuple[str, ...]
    forbiddenChangedExtensions: tuple[str, ...]

    def to_dict(self) -> dict[str, Any]:
        return {
            "renameDetection": self.renameDetection,
            "allowExtensionlessOutputs": self.allowExtensionlessOutputs,
            "allowUtf8Bom": self.allowUtf8Bom,
            "allowUtf16": self.allowUtf16,
            "allowBinaryChanges": self.allowBinaryChanges,
            "allowBinaryDeletes": self.allowBinaryDeletes,
            "maxPathLength": self.maxPathLength,
            "maxTextReadBytes": self.maxTextReadBytes,
            "maxDiffReportBytes": self.maxDiffReportBytes,
            "allowedChangedTextExtensions": list(self.allowedChangedTextExtensions),
            "forbiddenChangedExtensions": list(self.forbiddenChangedExtensions),
        }


def parse_change_policy(config: dict[str, Any], real_task_policy: RealTaskPolicy | None = None) -> tuple[ChangePolicy | None, list[RealTaskError]]:
    raw = config.get("realTaskChangePolicy")
    errors: list[RealTaskError] = []
    required = {
        "renameDetection",
        "allowExtensionlessOutputs",
        "allowUtf8Bom",
        "allowUtf16",
        "allowBinaryChanges",
        "allowBinaryDeletes",
        "maxPathLength",
        "maxTextReadBytes",
        "maxDiffReportBytes",
        "allowedChangedTextExtensions",
        "forbiddenChangedExtensions",
    }
    if not isinstance(raw, dict):
        return None, [_error("REAL_TASK_CONFIG_INVALID", "realTaskChangePolicy must be an object.", "realTaskChangePolicy")]
    for field in sorted(set(raw) - required):
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Unknown realTaskChangePolicy field.", f"realTaskChangePolicy.{field}"))
    for field in sorted(required - set(raw)):
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Missing realTaskChangePolicy field.", f"realTaskChangePolicy.{field}"))
    exact_bools = {
        "renameDetection": False,
        "allowExtensionlessOutputs": False,
        "allowUtf8Bom": True,
        "allowUtf16": False,
        "allowBinaryChanges": False,
        "allowBinaryDeletes": False,
    }
    for field, expected in exact_bools.items():
        if raw.get(field) is not expected:
            errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be exact {expected}.", f"realTaskChangePolicy.{field}"))
    max_path = _bounded_int(raw, "maxPathLength", 1, 4096, errors)
    max_text = _bounded_int(raw, "maxTextReadBytes", 1, 64 * 1024 * 1024, errors)
    max_diff = _bounded_int(raw, "maxDiffReportBytes", 1024, 64 * 1024 * 1024, errors)
    if real_task_policy is not None and max_text != real_task_policy.maxSingleChangedFileBytes:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "maxTextReadBytes must match host maxSingleChangedFileBytes.", "realTaskChangePolicy.maxTextReadBytes"))
    allowed = _extension_list(raw.get("allowedChangedTextExtensions"), "allowedChangedTextExtensions", errors)
    forbidden = _extension_list(raw.get("forbiddenChangedExtensions"), "forbiddenChangedExtensions", errors)
    overlap = set(allowed) & set(forbidden)
    if overlap:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Allowed and forbidden changed extensions must not overlap.", "realTaskChangePolicy"))
    if errors:
        return None, errors
    return ChangePolicy(False, False, True, False, False, False, max_path, max_text, max_diff, allowed, forbidden), []


def finding(code: str, category: str, path: str | None, blocking: bool, message: str, expected: Any = None, actual: Any = None) -> dict[str, Any]:
    return {
        "code": code,
        "category": category,
        "path": path,
        "blocking": blocking,
        "message": message,
        "expected": expected,
        "actual": actual,
    }


def is_contained_by(path: str, container: str) -> bool:
    if path == container:
        return True
    container_parts = tuple(part for part in container.split("/") if part)
    path_parts = tuple(part for part in path.split("/") if part)
    return len(container_parts) < len(path_parts) and path_parts[: len(container_parts)] == container_parts


def is_in_scope(path: str, scopes: list[str]) -> bool:
    return any(is_contained_by(path, scope) for scope in scopes)


def implicit_ancestor_allowed(path: str, exact_file_scopes: list[str]) -> bool:
    return any(is_contained_by(scope, path) for scope in exact_file_scopes)


def is_service_path(path: str, service_paths: set[str]) -> bool:
    return path in service_paths or any(path.startswith(f"{service}/") for service in service_paths)


def evaluate_change_policy(
    baseline: dict[str, Any],
    final: dict[str, Any],
    diff: dict[str, Any],
    context: dict[str, Any],
    change_policy: ChangePolicy,
    parent_git_changed: bool | None,
    parent_source_changed: bool | None,
    isolated_git_valid: bool,
) -> dict[str, Any]:
    findings: list[dict[str, Any]] = []
    service_paths = {str(item["workspacePath"]) for item in baseline.get("serviceRegistry", []) if item.get("workspacePath")}
    _service_guard_findings(diff, service_paths, findings)
    _scope_findings(baseline, final, diff, list(context.get("allowedWritePaths", [])), list(context.get("allowedDeletePaths", [])), findings)
    _limit_findings(diff, context, change_policy, findings)
    _binary_text_findings(diff, change_policy, findings)
    _expected_output_findings(final, context, service_paths, change_policy, findings)
    if not isolated_git_valid:
        findings.append(finding("REAL_TASK_ISOLATED_GIT_INVALID", "isolated_git", ".git", True, "Isolated .git fingerprint changed.", "unchanged", "changed"))
    if parent_git_changed:
        findings.append(finding("REAL_TASK_PARENT_GIT_CHANGED", "parent_git", None, True, "Parent Git snapshot changed.", False, True))
    if parent_source_changed:
        findings.append(finding("REAL_TASK_PARENT_SOURCE_CHANGED", "parent_source", None, True, "Parent source inventory changed.", False, True))
    return {
        "reportVersion": 1,
        "runId": final["runId"],
        "taskId": final["taskId"],
        "stage": "BOOTSTRAP-03B-2B-B",
        "trustedContextHash": final["trustedContextHash"],
        "serviceGuardStatus": _status(findings, "service"),
        "isolatedGitStatus": "PASS" if isolated_git_valid else "BLOCKED",
        "writeScopeStatus": _status(findings, "write_scope"),
        "deleteScopeStatus": _status(findings, "delete_scope"),
        "limitsStatus": _status(findings, "limits"),
        "binaryTextStatus": _status(findings, "binary_text"),
        "expectedOutputsStatus": _status(findings, "expected_outputs"),
        "parentGitStatus": "PASS" if parent_git_changed is False else "BLOCKED",
        "parentSourceStatus": "PASS" if parent_source_changed is False else "BLOCKED",
        "findings": findings,
        "finalPolicyVerdict": _verdict(findings),
    }


def _service_guard_findings(diff: dict[str, Any], service_paths: set[str], findings: list[dict[str, Any]]) -> None:
    for category, item in _changed_items(diff):
        path = item["path"]
        if is_service_path(path, service_paths):
            findings.append(finding("REAL_TASK_SERVICE_FILE_INVALID", "service", path, True, "Service path changed.", "unchanged", category))
        elif path.endswith("AGENTS.md"):
            findings.append(finding("REAL_TASK_SERVICE_FILE_INVALID", "service", path, True, "New or changed AGENTS.md injection is forbidden.", "no AGENTS mutation", category))
        elif path == ".agents" or path.startswith(".agents/"):
            findings.append(finding("REAL_TASK_SERVICE_FILE_INVALID", "service", path, True, ".agents must remain an exact empty service directory.", "empty immutable directory", category))
        elif path == ".git" or path.startswith(".git/"):
            findings.append(finding("REAL_TASK_ISOLATED_GIT_INVALID", "service", path, True, ".git content mutation is forbidden.", "fingerprint stable", category))


def _scope_findings(baseline: dict[str, Any], final: dict[str, Any], diff: dict[str, Any], write_paths: list[str], delete_paths: list[str], findings: list[dict[str, Any]]) -> None:
    baseline_entries = {entry["path"]: entry for entry in baseline.get("entries", [])}
    final_entries = {entry["path"]: entry for entry in final.get("entries", [])}
    write_grants = _permission_grants(write_paths, baseline_entries, final_entries)
    delete_grants = _permission_grants(delete_paths, baseline_entries, final_entries)
    for category, item in _changed_items(diff):
        path = item["path"]
        before = item.get("before")
        after = item.get("after")
        needs_write = category in {"added", "modified", "typeChanged"}
        needs_delete = category in {"deleted", "typeChanged"}
        if after and after.get("type") == "directory" and _implicit_write_ancestor_allowed(path, write_grants):
            needs_write = False
        if before and before.get("type") == "directory" and _deleted_directory_allowed(path, baseline_entries, diff, delete_grants):
            needs_delete = False
        if needs_write and not _path_allowed(path, write_grants):
            findings.append(finding("REAL_TASK_FORBIDDEN_CHANGE", "write_scope", path, False, "Changed path is outside allowedWritePaths.", write_paths, path))
        if needs_delete and not _path_allowed(path, delete_grants):
            findings.append(finding("REAL_TASK_FORBIDDEN_DELETE", "delete_scope", path, False, "Deleted path is outside allowedDeletePaths.", delete_paths, path))


def _permission_grants(paths: list[str], baseline_entries: dict[str, dict[str, Any]], final_entries: dict[str, dict[str, Any]]) -> list[dict[str, str]]:
    grants: list[dict[str, str]] = []
    for path in paths:
        entry = final_entries.get(path) or baseline_entries.get(path)
        if entry and entry.get("type") == "directory":
            kind = "directory_tree"
        elif any(other != path and is_contained_by(other, path) for other in paths):
            kind = "directory_tree"
        else:
            kind = "exact_file"
        grants.append({"path": path, "kind": kind})
    return grants


def _path_allowed(path: str, grants: list[dict[str, str]]) -> bool:
    for grant in grants:
        grant_path = grant["path"]
        if grant["kind"] == "exact_file" and path == grant_path:
            return True
        if grant["kind"] == "directory_tree" and is_contained_by(path, grant_path):
            return True
    return False


def _implicit_write_ancestor_allowed(path: str, grants: list[dict[str, str]]) -> bool:
    return any(grant["kind"] == "exact_file" and is_contained_by(grant["path"], path) for grant in grants)


def _deleted_directory_allowed(path: str, baseline_entries: dict[str, dict[str, Any]], diff: dict[str, Any], grants: list[dict[str, str]]) -> bool:
    if any(grant["kind"] == "directory_tree" and is_contained_by(path, grant["path"]) for grant in grants):
        return True
    baseline_descendants = [candidate for candidate in baseline_entries if candidate != path and is_contained_by(candidate, path)]
    deleted_paths = {item["path"] for category, item in _changed_items(diff) if category in {"deleted", "typeChanged"}}
    for descendant in baseline_descendants:
        if descendant not in deleted_paths:
            return False
        if not _path_allowed(descendant, grants):
            return False
    return bool(baseline_descendants)


def _limit_findings(diff: dict[str, Any], context: dict[str, Any], policy: ChangePolicy, findings: list[dict[str, Any]]) -> None:
    limits = context.get("outputLimits", {})
    summary = diff["summary"]
    checks = (
        ("maxChangedFiles", "totalChangedFiles"),
        ("maxChangedBytes", "totalChangedBytes"),
    )
    for limit_name, summary_name in checks:
        limit = limits.get(limit_name)
        actual = summary.get(summary_name)
        if isinstance(limit, int) and isinstance(actual, int) and actual > limit:
            findings.append(finding("REAL_TASK_DIFF_LIMIT_EXCEEDED", "limits", None, False, f"{summary_name} exceeds {limit_name}.", limit, actual))
    single_limit = limits.get("maxSingleChangedFileBytes")
    if isinstance(single_limit, int):
        for _, item in _changed_items(diff):
            sizes = [entry["size"] for entry in (item.get("before"), item.get("after")) if entry and entry.get("type") == "file"]
            if sizes and max(sizes) > single_limit:
                findings.append(finding("REAL_TASK_DIFF_LIMIT_EXCEEDED", "limits", item["path"], False, "Single changed file exceeds maxSingleChangedFileBytes.", single_limit, max(sizes)))
    if policy.maxDiffReportBytes <= 0:
        findings.append(finding("REAL_TASK_DIFF_LIMIT_EXCEEDED", "limits", None, False, "maxDiffReportBytes must be positive.", ">0", policy.maxDiffReportBytes))


def _binary_text_findings(diff: dict[str, Any], policy: ChangePolicy, findings: list[dict[str, Any]]) -> None:
    for category, item in _changed_items(diff):
        for side in ("before", "after"):
            entry = item.get(side)
            if not entry or entry.get("type") != "file":
                continue
            extension = entry.get("extension") or ""
            if extension in policy.forbiddenChangedExtensions:
                code = "REAL_TASK_BINARY_CHANGE_FORBIDDEN" if entry.get("contentKind") == "binary" else "REAL_TASK_TEXT_CHANGE_INVALID"
                findings.append(finding(code, "binary_text", item["path"], False, "Changed extension is forbidden.", sorted(policy.forbiddenChangedExtensions), extension))
            if entry.get("contentKind") == "binary":
                findings.append(finding("REAL_TASK_BINARY_CHANGE_FORBIDDEN", "binary_text", item["path"], False, "Binary file changes are forbidden.", "text", "binary"))
            elif entry.get("contentKind") == "text":
                if not extension and not policy.allowExtensionlessOutputs:
                    findings.append(finding("REAL_TASK_TEXT_CHANGE_INVALID", "binary_text", item["path"], False, "Extensionless outputs are forbidden.", "extension", ""))
                elif extension not in policy.allowedChangedTextExtensions:
                    findings.append(finding("REAL_TASK_TEXT_CHANGE_INVALID", "binary_text", item["path"], False, "Changed text extension is not allowed.", sorted(policy.allowedChangedTextExtensions), extension))
                if entry.get("textEncoding") == "utf-8-bom" and not policy.allowUtf8Bom:
                    findings.append(finding("REAL_TASK_TEXT_CHANGE_INVALID", "binary_text", item["path"], False, "UTF-8 BOM is not allowed.", "utf-8", "utf-8-bom"))


def _expected_output_findings(final: dict[str, Any], context: dict[str, Any], service_paths: set[str], policy: ChangePolicy, findings: list[dict[str, Any]]) -> None:
    entries = {entry["path"]: entry for entry in final.get("entries", [])}
    for output in context.get("expectedOutputs", []):
        if not isinstance(output, dict):
            continue
        path = output.get("path")
        expected_kind = output.get("kind")
        required = output.get("required")
        if required is not True or not isinstance(path, str):
            continue
        entry = entries.get(path)
        if entry is None:
            findings.append(finding("REAL_TASK_EXPECTED_OUTPUT_MISSING", "expected_outputs", path, False, "Required expected output is missing.", expected_kind, None))
            continue
        if entry.get("type") != expected_kind:
            findings.append(finding("REAL_TASK_EXPECTED_OUTPUT_MISSING", "expected_outputs", path, False, "Expected output type does not match.", expected_kind, entry.get("type")))
        if is_service_path(path, service_paths):
            findings.append(finding("REAL_TASK_SERVICE_FILE_INVALID", "expected_outputs", path, True, "Expected output cannot be a service path.", "task output", "service"))
        extension = entry.get("extension") or ""
        if expected_kind == "file" and (entry.get("contentKind") == "binary" or extension in policy.forbiddenChangedExtensions):
            findings.append(finding("REAL_TASK_BINARY_CHANGE_FORBIDDEN", "expected_outputs", path, False, "Expected output uses forbidden binary/extension type.", "allowed text", extension or entry.get("contentKind")))


def _changed_items(diff: dict[str, Any]) -> list[tuple[str, dict[str, Any]]]:
    items: list[tuple[str, dict[str, Any]]] = []
    categories = diff.get("categories", {})
    for category in ("added", "modified", "deleted", "typeChanged"):
        for item in categories.get(category, []):
            items.append((category, item))
    return items


def _status(findings: list[dict[str, Any]], category: str) -> str:
    matching = [item for item in findings if item["category"] == category]
    if any(item["blocking"] for item in matching):
        return "BLOCKED"
    if matching:
        return "FAIL"
    return "PASS"


def _verdict(findings: list[dict[str, Any]]) -> str:
    if any(item["blocking"] for item in findings):
        return "BLOCKED"
    if findings:
        return "FAIL"
    return "PASS"


def _extension_list(value: Any, field: str, errors: list[RealTaskError]) -> tuple[str, ...]:
    if not isinstance(value, list) or not value:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be a non-empty array.", f"realTaskChangePolicy.{field}"))
        return ()
    seen: set[str] = set()
    result: list[str] = []
    for extension in value:
        if not isinstance(extension, str) or not re.match(r"^\.[a-z0-9]+$", extension):
            errors.append(_error("REAL_TASK_CONFIG_INVALID", "Extensions must be lowercase and begin with dot.", f"realTaskChangePolicy.{field}"))
            continue
        if PureWindowsPath(extension).parent.as_posix() not in {".", ""} or "/" in extension or "\\" in extension or "*" in extension or "?" in extension:
            errors.append(_error("REAL_TASK_CONFIG_INVALID", "Extensions must not contain separators or wildcards.", f"realTaskChangePolicy.{field}"))
            continue
        if extension in seen:
            errors.append(_error("REAL_TASK_CONFIG_INVALID", "Extensions must be unique.", f"realTaskChangePolicy.{field}"))
            continue
        seen.add(extension)
        result.append(extension)
    return tuple(result)


def _bounded_int(raw: dict[str, Any], field: str, minimum: int, maximum: int, errors: list[RealTaskError]) -> int:
    value = raw.get(field)
    if not isinstance(value, int) or isinstance(value, bool):
        errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be an integer.", f"realTaskChangePolicy.{field}"))
        return minimum
    if value < minimum or value > maximum:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be from {minimum} to {maximum}.", f"realTaskChangePolicy.{field}"))
    return value


def _error(code: str, message: str, field: str | None = None) -> RealTaskError:
    return RealTaskError(code, message, field)
