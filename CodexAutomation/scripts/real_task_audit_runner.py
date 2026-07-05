from __future__ import annotations

import os
from pathlib import Path
from typing import Any

from file_utils import write_json_atomic
from real_task_execution_context import resolve_execution_authority, validate_execution_handle
from real_task_execution_models import RealTaskExecutionHandle, RoleInvocationResult, canonical_sha256, file_sha256
from real_task_integrity import capture_integrity_snapshot, compare_full_workspace_unchanged
from real_task_report_writer import write_trusted_report


def snapshot_workspace(workspace: Path) -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    for current, dirs, files in os.walk(workspace):
        dirs[:] = [name for name in dirs if name != ".git"]
        rel_dir = Path(current).relative_to(workspace).as_posix()
        if rel_dir == ".":
            rel_dir = ""
        for file_name in sorted(files):
            path = Path(current) / file_name
            rel = (Path(rel_dir) / file_name).as_posix() if rel_dir else file_name
            entries.append({"path": rel, "size": path.stat().st_size, "sha256": file_sha256(path)})
    entries.sort(key=lambda item: item["path"])
    return {"entries": entries, "hash": canonical_sha256(entries)}


def write_audit_report(run_dir: Path, context: dict[str, Any], auditor: RoleInvocationResult, before: dict[str, Any], after: dict[str, Any]) -> tuple[dict[str, Any], str]:
    changed = before["hash"] != after["hash"]
    approved = auditor.verdict == "PASS" and not changed
    report = {
        "reportVersion": 1,
        "orchestrationRunId": context["orchestrationRunId"],
        "taskId": context["taskId"],
        "stage": "BOOTSTRAP-03B-2C",
        "auditorInvocationId": auditor.invocationId,
        "auditorVerdict": auditor.verdict,
        "approved": approved,
        "workspaceUnchangedAfterAuditor": not changed,
        "beforeWorkspaceHash": before["hash"],
        "afterWorkspaceHash": after["hash"],
        "findings": [] if approved else [{"code": auditor.errorCode or ("REAL_TASK_AUDITOR_CHANGED_WORKSPACE" if changed else "REAL_TASK_AUDIT_REJECTED"), "message": auditor.errorMessage or "Auditor did not approve."}],
        "errorCode": None if approved else (auditor.errorCode or ("REAL_TASK_AUDITOR_CHANGED_WORKSPACE" if changed else "REAL_TASK_AUDIT_REJECTED")),
        "errorMessage": None if approved else (auditor.errorMessage or "Auditor did not approve."),
    }
    path = run_dir / "AUDIT_REPORT.json"
    write_json_atomic(path, report)
    return report, canonical_sha256(report)


def capture_auditor_integrity(handle: RealTaskExecutionHandle, root: Path, automation_root: Path, config: dict[str, Any], label: str, expected_states: tuple[str, ...] | None = None) -> dict[str, Any]:
    return capture_integrity_snapshot(handle, root, automation_root, config, label, expected_states)


def write_full_audit_report(
    handle: RealTaskExecutionHandle,
    root: Path,
    automation_root: Path,
    config: dict[str, Any],
    auditor: RoleInvocationResult,
    before: dict[str, Any],
    after: dict[str, Any],
) -> tuple[dict[str, Any], str]:
    authority = resolve_execution_authority(handle, automation_root, ("AUDITOR_SUCCESS", "AUDITOR_FAIL"))
    context = authority.context
    unchanged = compare_full_workspace_unchanged(before, after)
    approved = auditor.verdict == "PASS" and unchanged
    mutation_code = "REAL_TASK_AUDITOR_CHANGED_WORKSPACE"
    report = {
        "reportVersion": 1,
        "orchestrationRunId": context["orchestrationRunId"],
        "taskId": context["taskId"],
        "stage": "BOOTSTRAP-03B-2C",
        "auditorInvocationId": auditor.invocationId,
        "auditorVerdict": auditor.verdict,
        "approved": approved,
        "workspaceUnchangedAfterAuditor": unchanged,
        "beforeWorkspaceHash": before["workspaceInventoryHash"],
        "afterWorkspaceHash": after["workspaceInventoryHash"],
        "serviceFilesValid": after["serviceFilesValid"],
        "agentsDirectoryValid": after["agentsDirectoryValid"],
        "isolatedGitValid": after["isolatedGitValid"],
        "parentGitChanged": after["parentGitChanged"],
        "parentSourceChanged": after["parentSourceChanged"],
        "findings": [] if approved else [{"code": auditor.errorCode or (mutation_code if not unchanged else "REAL_TASK_AUDIT_REJECTED"), "message": auditor.errorMessage or "Auditor did not approve or changed trusted state."}],
        "errorCode": None if approved else (auditor.errorCode or (mutation_code if not unchanged else "REAL_TASK_AUDIT_REJECTED")),
        "errorMessage": None if approved else (auditor.errorMessage or "Auditor did not approve or changed trusted state."),
    }
    path = Path(handle.runDirectory) / "AUDIT_REPORT.json"
    write_trusted_report(automation_root, root / "CodexAutomation" / "runtime", path, report, "real_task_audit.schema.json", _validate_audit_report, int(config["realTaskExecutionPolicy"]["maxAuditFindings"]) * 4096)
    return report, canonical_sha256(report)


def _validate_audit_report(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("approved") is True:
        for field in ("workspaceUnchangedAfterAuditor", "serviceFilesValid", "agentsDirectoryValid", "isolatedGitValid"):
            if report.get(field) is not True:
                errors.append(f"Approved audit requires {field}=true.")
        if report.get("parentGitChanged") is not False or report.get("parentSourceChanged") is not False:
            errors.append("Approved audit requires parent unchanged.")
        if report.get("errorCode") is not None:
            errors.append("Approved audit requires null errorCode.")
    return errors
