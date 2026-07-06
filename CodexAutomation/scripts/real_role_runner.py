from __future__ import annotations

import json
import hashlib
import os
import shutil
import subprocess
import stat
import time
import traceback
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from codex_event_parser import classify_schema_invalid_text, parse_jsonl_events
from codex_runner import run_process_with_file_logs_and_timeout, run_process_with_timeout
from file_utils import _fs_path, read_json, read_text_long_safe, write_json_atomic, write_text_long_safe
from isolated_workspace import LOCAL_AGENTS_TEXT, create_isolated_workspace, relative_to_root, validate_initial_service_baseline, validate_service_directory
from pipeline_models import ExecutionStatus, PipelineState
from pipeline_result_validator import (
    validate_audit_result,
    validate_implementation_result,
    validate_repair_result,
    validate_role_execution,
)
from pipeline_validator import WORKSPACE_ERROR_ENTRY, _read_json_file, changed_paths, run_validations, snapshot_error, snapshot_workspace
from usage_limit_models import utc_now


REAL_TASK_ID = "REAL-PIPELINE-TEST-001"
REQUIRED_FLAGS = {
    "--sandbox",
    "--cd",
    "--json",
    "--output-schema",
    "--output-last-message",
    "--color",
    "--ignore-user-config",
}


@dataclass(frozen=True)
class RealRoleSettings:
    isolatedWorkspaceRoot: str
    realRoleReportRoot: str
    maxRealCodexInvocationsPerTest: int
    implementerTimeoutSeconds: int
    repairerTimeoutSeconds: int
    auditorTimeoutSeconds: int
    sandboxProbeTimeoutSeconds: int
    sandboxProbeTaskkillTimeoutSeconds: int
    sandboxProbePostKillWaitSeconds: int
    approvalPolicy: str
    windowsSandboxImplementation: str
    requireSandboxWriteProbe: bool
    allowAutomaticUnelevatedFallback: bool


@dataclass(frozen=True)
class ProbeGateResult:
    ok: bool
    errorCode: str | None
    errorMessage: str | None
    report: dict[str, Any] | None


@dataclass(frozen=True)
class ProbeRunContext:
    probe_run_id: str
    configured_probe_root: Path
    run_directory: Path
    workspace: Path
    report_path: Path
    started_at: str


@dataclass(frozen=True)
class ProbeExecutionOutcome:
    context: ProbeRunContext
    report_summary: dict[str, Any]


def parse_real_role_settings(config: dict[str, Any]) -> tuple[RealRoleSettings | None, list[str]]:
    errors: list[str] = []
    roles = config.get("realRoles")
    if not isinstance(roles, dict):
        return None, ["REAL_ROLE_CONFIG_INVALID: realRoles must be an object."]
    _exact_bool(roles, "enabled", True, errors)
    _exact_bool(roles, "allowRealProjectWrite", False, errors)
    _exact_bool(roles, "allowIsolatedWorkspaceWriteTest", True, errors)
    _exact_bool(roles, "useJsonEvents", True, errors)
    _exact_bool(roles, "useOutputSchema", True, errors)
    _exact_bool(roles, "useOutputLastMessage", True, errors)
    _exact_bool(roles, "automaticRetryAfterRateLimit", False, errors)
    isolated = _string(roles, "isolatedWorkspaceRoot", errors)
    report_root = _string(roles, "realRoleReportRoot", errors)
    max_invocations = _int_range(roles, "maxRealCodexInvocationsPerTest", 1, 3, errors)
    implementer_timeout = _positive_int(roles, "implementerTimeoutSeconds", errors)
    repairer_timeout = _positive_int(roles, "repairerTimeoutSeconds", errors)
    auditor_timeout = _positive_int(roles, "auditorTimeoutSeconds", errors)
    sandbox_probe_timeout = _int_range(roles, "sandboxProbeTimeoutSeconds", 5, 60, errors)
    sandbox_probe_taskkill_timeout = _int_range(roles, "sandboxProbeTaskkillTimeoutSeconds", 1, 15, errors)
    sandbox_probe_post_kill_timeout = _int_range(roles, "sandboxProbePostKillWaitSeconds", 1, 10, errors)
    approval = _string(roles, "approvalPolicy", errors)
    if approval != "never":
        errors.append("REAL_ROLE_CONFIG_INVALID: approvalPolicy must be never.")
    windows_sandbox = _string(roles, "windowsSandboxImplementation", errors)
    if windows_sandbox not in {"elevated", "unelevated"}:
        errors.append("REAL_ROLE_CONFIG_INVALID: windowsSandboxImplementation must be elevated or unelevated.")
    if windows_sandbox != "elevated":
        errors.append("REAL_ROLE_CONFIG_INVALID: windowsSandboxImplementation must be elevated for BOOTSTRAP-03B-1.")
    _exact_bool(roles, "requireSandboxWriteProbe", True, errors)
    _exact_bool(roles, "allowAutomaticUnelevatedFallback", False, errors)
    if errors:
        return None, errors
    return RealRoleSettings(
        isolated,
        report_root,
        max_invocations,
        implementer_timeout,
        repairer_timeout,
        auditor_timeout,
        sandbox_probe_timeout,
        sandbox_probe_taskkill_timeout,
        sandbox_probe_post_kill_timeout,
        approval,
        windows_sandbox,
        True,
        False,
    ), []


def run_real_role_plan(root: Path, automation_root: Path, config: dict[str, Any]) -> dict[str, Any]:
    settings, errors = parse_real_role_settings(config)
    run_id = f"real_plan_{utc_now().replace(':', '').replace('-', '').replace('.', '')}"
    if errors or settings is None:
        return {"ok": False, "errorCode": "REAL_ROLE_CONFIG_INVALID", "errors": errors}
    workspace = root / settings.isolatedWorkspaceRoot / run_id / "workspace"
    commands = [
        sanitize_command(build_codex_command("implementer", "workspace-write", workspace, automation_root / "schemas" / "implementation_result.schema.json", root / settings.realRoleReportRoot / run_id / "implementer_last_message.json", settings)),
        sanitize_command(build_codex_command("repairer", "workspace-write", workspace, automation_root / "schemas" / "repair_result.schema.json", root / settings.realRoleReportRoot / run_id / "repairer_last_message_attempt_1.json", settings)),
        sanitize_command(build_codex_command("auditor", "read-only", workspace, automation_root / "schemas" / "audit_result.schema.json", root / settings.realRoleReportRoot / run_id / "auditor_last_message.json", settings)),
    ]
    return {
        "ok": True,
        "runId": run_id,
        "workspace": relative_to_root(root, workspace),
        "maxRealCodexInvocations": settings.maxRealCodexInvocationsPerTest,
        "commands": commands,
        "realCodexStarted": False,
    }


def _create_probe_run_context(root: Path, run_id_suffix: str, started_at: str) -> ProbeRunContext:
    safe_started = started_at.replace(":", "").replace("-", "").replace(".", "")
    probe_run_id = f"probe_{run_id_suffix}_{safe_started}"
    configured_probe_root = root / "CodexAutomation" / "runtime" / "real_role_sandbox_probes"
    run_directory = configured_probe_root / probe_run_id
    workspace = run_directory / "workspace"
    report_path = run_directory / "SANDBOX_WRITE_PROBE_REPORT.json"
    return ProbeRunContext(probe_run_id, configured_probe_root, run_directory, workspace, report_path, started_at)


def _probe_context_report(root: Path, context: ProbeRunContext) -> dict[str, Any]:
    return {
        "probeRunId": context.probe_run_id,
        "probeRunDirectory": relative_to_root(root, context.run_directory),
        "workspacePath": relative_to_root(root, context.workspace),
        "reportPath": relative_to_root(root, context.report_path),
    }


def run_real_role_sandbox_probe(root: Path, automation_root: Path, config: dict[str, Any], run_id_suffix: str = "manual") -> dict[str, Any]:
    context = _create_probe_run_context(root, run_id_suffix, utc_now())
    return run_real_role_sandbox_probe_outcome(root, automation_root, config, context).report_summary


def run_real_role_sandbox_probe_outcome(root: Path, automation_root: Path, config: dict[str, Any], context: ProbeRunContext) -> ProbeExecutionOutcome:
    return ProbeExecutionOutcome(context, _run_real_role_sandbox_probe_with_context(root, automation_root, config, context))


def _run_real_role_sandbox_probe_with_context(root: Path, automation_root: Path, config: dict[str, Any], context: ProbeRunContext) -> dict[str, Any]:
    started = time.monotonic()
    started_at = context.started_at
    probe_run_id = context.probe_run_id
    settings, config_errors = parse_real_role_settings(config)
    probe_root = context.run_directory
    workspace = context.workspace
    probe_root.mkdir(parents=True, exist_ok=True)
    parent_initial = _git_snapshot(root)
    stdout_path = probe_root / "probe_stdout.log"
    stderr_path = probe_root / "probe_stderr.log"
    report: dict[str, Any] = {
        "probeRunId": probe_run_id,
        "startedAt": started_at,
        "finishedAt": None,
        "durationSeconds": None,
        "codexCliVersion": None,
        "windowsSandboxImplementation": settings.windowsSandboxImplementation if settings else None,
        "permissionsProfile": ":workspace",
        "workspacePath": relative_to_root(root, workspace),
        "reportPath": relative_to_root(root, context.report_path),
        "sanitizedCommand": [],
        "processStarted": False,
        "processId": None,
        "timedOut": False,
        "terminationAttempted": False,
        "taskkillStarted": False,
        "taskkillCompleted": False,
        "fallbackKillAttempted": False,
        "terminationIncomplete": False,
        "exitCode": None,
        "stdoutSummary": [],
        "stderrSummary": [],
        "probeFileCreated": False,
        "probeFileReadBack": False,
        "probeFileRemoved": False,
        "unexpectedPaths": [],
        "parentGitChanged": False,
        "finalVerdict": "FAILED",
        "errorCode": None,
        "errorMessage": None,
        "realCodexStarted": False,
        "unityStarted": False,
        "networkUsed": False,
        "reportWarnings": [],
    }
    report["finalVerdict"] = "RUNNING"
    _write_probe_report(report, probe_root)
    write_json_atomic(probe_root / "probe_command.json", {"sanitizedCommand": [], "workspacePath": report["workspacePath"]})
    write_json_atomic(probe_root / "probe_process.json", {"processStarted": False, "processId": None, "timedOut": False})
    _initialize_probe_text_log(stdout_path)
    _initialize_probe_text_log(stderr_path)
    final_verdict = "FAILED"
    error_code: str | None = None
    error_message: str | None = None
    try:
        if config_errors or settings is None:
            error_code = "REAL_ROLE_CONFIG_INVALID"
            error_message = "; ".join(config_errors)
            return report
        cli_version, cli_error = _codex_version(root)
        report["codexCliVersion"] = cli_version
        if cli_error:
            error_code = "BLOCKED_UNSUPPORTED_CODEX_SANDBOX_HELPER"
            error_message = cli_error
            return report
        sandbox_help = _codex_sandbox_help(root)
        for required in ("--permissions-profile", "--cd", "--config"):
            if required not in sandbox_help:
                error_code = "BLOCKED_UNSUPPORTED_CODEX_SANDBOX_HELPER"
                error_message = f"codex sandbox help is missing {required}."
                return report
        workspace_error = _create_probe_workspace(root, workspace)
        if workspace_error:
            error_code = workspace_error
            error_message = "Probe workspace is outside the allowed runtime boundary or unsafe."
            return report
        initial_snapshot = _snapshot_probe_workspace(workspace)
        write_json_atomic(probe_root / "probe_workspace_initial_snapshot.json", initial_snapshot)
        command = build_sandbox_probe_command(workspace, settings)
        report["sanitizedCommand"] = sanitize_command(command)
        write_json_atomic(probe_root / "probe_command.json", {"sanitizedCommand": report["sanitizedCommand"], "workspacePath": report["workspacePath"]})
        write_json_atomic(probe_root / "probe_started.json", {"timestamp": utc_now(), "workspacePath": report["workspacePath"]})
        _write_probe_report(report, probe_root)

        def on_process_event(event: str, process_result: dict[str, Any]) -> None:
            _update_probe_process_report(report, probe_root, process_result)
            if event == "timeout":
                write_json_atomic(probe_root / "probe_timeout.json", {"timestamp": utc_now(), "processId": process_result.get("pid")})
            if event in {"taskkill_done", "fallback_kill", "termination_incomplete"}:
                _write_probe_termination_marker(probe_root, report, process_result)
            _write_probe_report(report, probe_root)

        process = run_process_with_file_logs_and_timeout(
            command,
            workspace,
            stdout_path,
            stderr_path,
            settings.sandboxProbeTimeoutSeconds,
            settings.sandboxProbeTaskkillTimeoutSeconds,
            settings.sandboxProbePostKillWaitSeconds,
            on_process_event,
        )
        _update_probe_process_report(report, probe_root, process)
        stdout_text = _read_text_if_exists(stdout_path)
        stderr_text = _read_text_if_exists(stderr_path)
        report["stdoutSummary"] = _safe_output_summary(stdout_text)
        report["stderrSummary"] = _safe_output_summary(stderr_text)
        if process.get("process_start_error"):
            error_code = "SANDBOX_PROBE_PROCESS_START_FAILED"
            error_message = process["process_start_error"]
            return report
        if process["timed_out"]:
            error_code = "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT"
            error_message = "Sandbox write probe timed out."
            if process.get("termination", {}).get("incomplete"):
                report["reportWarnings"].append({"code": "PROCESS_TERMINATION_INCOMPLETE", "message": "Probe process termination did not fully complete."})
            elif process.get("termination", {}).get("attempted") and not process.get("termination", {}).get("succeeded"):
                report["reportWarnings"].append({"code": "WINDOWS_SANDBOX_TASKKILL_FAILED", "message": process.get("termination", {}).get("stderr") or "taskkill did not report success."})
            return report
        report["exitCode"] = process["exit_code"]
        report["probeFileCreated"] = "PROBE_FILE_CREATED" in stdout_text
        report["probeFileReadBack"] = "PROBE_FILE_READ_BACK" in stdout_text
        report["probeFileRemoved"] = "PROBE_FILE_REMOVED" in stdout_text
        final_snapshot = _snapshot_probe_workspace(workspace)
        snapshot_problem = snapshot_error(final_snapshot)
        if snapshot_problem:
            error_code = snapshot_problem["errorCode"]
            error_message = snapshot_problem["message"]
            return report
        report["unexpectedPaths"] = sorted(final_snapshot.keys())
        if process["exit_code"] != 0:
            error_code = "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"
            error_message = "Sandbox write probe command failed."
            return report
        if not report["probeFileCreated"]:
            error_code = "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"
            error_message = "Sandbox probe did not confirm file creation."
            return report
        if not report["probeFileReadBack"]:
            error_code = "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"
            error_message = "Sandbox probe did not confirm read-back verification."
            return report
        if not report["probeFileRemoved"]:
            error_code = "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"
            error_message = "Sandbox probe did not confirm file removal."
            return report
        if report["unexpectedPaths"]:
            error_code = "WINDOWS_SANDBOX_PROBE_UNEXPECTED_FILE"
            error_message = "Sandbox probe left unexpected files in workspace."
            return report
        final_verdict = "PASS"
        return report
    except Exception as exc:
        error_code = "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"
        error_message = _sanitize_error_message(exc)
        return report
    finally:
        _finish_probe(report, probe_root, started, parent_initial, root, final_verdict, error_code, error_message)


def run_real_role_self_test(root: Path, automation_root: Path, config: dict[str, Any], task_path: Path) -> dict[str, Any]:
    started = time.monotonic()
    run_id = f"real_role_{utc_now().replace(':', '').replace('-', '').replace('.', '')}"
    started_at = utc_now()
    settings, config_errors = parse_real_role_settings(config)
    report_root = root / (settings.realRoleReportRoot if settings else "CodexAutomation/runtime/real_role_runs") / run_id
    report_root.mkdir(parents=True, exist_ok=True)
    task: dict[str, Any] = {}
    report = _base_report(run_id, task, started_at, settings)
    try:
        task = read_json(task_path)
        report["taskId"] = task.get("id")
        parent_initial = _git_snapshot(root)
        report["initialParentGitSnapshot"] = parent_initial
        write_json_atomic(report_root / "task.json", task)
        if config_errors or settings is None:
            return _finish(report, report_root, started, "FAILED", "REAL_ROLE_CONFIG_INVALID", "; ".join(config_errors), root)
        forbidden_task_overrides = (
            "maxRealCodexInvocationsPerTest",
            "allowRealProjectWrite",
            "windowsSandboxImplementation",
            "requireSandboxWriteProbe",
            "allowAutomaticUnelevatedFallback",
            "sandboxProbeTimeoutSeconds",
            "sandboxProbeTaskkillTimeoutSeconds",
            "sandboxProbePostKillWaitSeconds",
        )
        if any(task.get(name) is not None for name in forbidden_task_overrides):
            return _finish(report, report_root, started, "FAILED", "REAL_ROLE_CONFIG_INVALID", "Task cannot override real-role safety settings.", root)

        cli_version, cli_error = _codex_version(root)
        report["codexCliVersion"] = cli_version
        if cli_error:
            return _finish(report, report_root, started, "FAILED", "BLOCKED_UNSUPPORTED_CODEX_CLI", cli_error, root)
        help_text = _codex_help(root)
        missing_flags = sorted(flag for flag in REQUIRED_FLAGS if flag not in help_text)
        if missing_flags:
            return _finish(report, report_root, started, "FAILED", "BLOCKED_UNSUPPORTED_CODEX_CLI", f"Missing flags: {missing_flags}", root)

        isolated_root = root / settings.isolatedWorkspaceRoot
        workspace, workspace_errors = create_isolated_workspace(root, run_id, isolated_root, task)
        if workspace_errors or workspace is None:
            return _finish(report, report_root, started, "FAILED", "ISOLATED_WORKSPACE_INVALID", "; ".join(workspace_errors), root)
        report["workspacePath"] = relative_to_root(root, workspace)
        _write_text(report_root / "run_config.json", json.dumps({"runId": run_id, "settings": settings.__dict__}, indent=2))
        _write_text(report_root / "initial_parent_git_snapshot.txt", "\n".join(parent_initial))
        initial_workspace_snapshot = snapshot_workspace(workspace)
        initial_service_error = validate_initial_service_baseline(workspace, initial_workspace_snapshot)
        if initial_service_error:
            return _finish(report, report_root, started, "FAILED", initial_service_error["errorCode"], initial_service_error["errorMessage"], root)
        report["workspaceInitialSnapshot"] = initial_workspace_snapshot
        write_json_atomic(report_root / "workspace_initial_snapshot.json", initial_workspace_snapshot)
        initial_workspace_git_state = _workspace_git_state(workspace)
        report["workspaceInitialGitState"] = initial_workspace_git_state
        probe_context = _create_probe_run_context(root, "self_test", utc_now())
        probe_outcome = run_real_role_sandbox_probe_outcome(root, automation_root, config, probe_context)
        probe_report = probe_outcome.report_summary
        report["sandboxWriteProbe"] = probe_report
        report["sandboxWriteProbeContext"] = _probe_context_report(root, probe_context)
        if probe_outcome.context != probe_context:
            report["sandboxWriteProbeOutcomeContextMismatch"] = True
            try:
                report["untrustedReturnedProbeContext"] = _probe_context_report(root, probe_outcome.context)
            except Exception:
                report["untrustedReturnedProbeContext"] = {"errorCode": "SANDBOX_WRITE_PROBE_CONTEXT_MISMATCH"}
            return _finish(
                report,
                report_root,
                started,
                "BLOCKED",
                "SANDBOX_WRITE_PROBE_CONTEXT_MISMATCH",
                "Sandbox probe returned a context that does not match the host-created current probe context.",
                root,
            )
        probe_gate = _validate_sandbox_probe_report_for_real_roles(root, probe_context, settings)
        if not probe_gate.ok:
            if probe_gate.report is not None:
                report["sandboxWriteProbe"] = probe_gate.report
            return _finish(report, report_root, started, "BLOCKED", probe_gate.errorCode or "WINDOWS_SANDBOX_WRITE_PROBE_FAILED", probe_gate.errorMessage, root)

        context = _RealContext(root, automation_root, workspace, report_root, settings, task, report)
        _transition(report, "IMPLEMENTING")
        implementation = _run_role(context, "implementer", "workspace-write", settings.implementerTimeoutSeconds, build_implementer_prompt(task), 1)
        if implementation != "success":
            return _finish_real_context(context, started)
        after_impl = snapshot_workspace(workspace)
        write_json_atomic(report_root / "workspace_after_implementer.json", after_impl)
        after_impl_git_state = _workspace_git_state(workspace)
        report["workspaceAfterImplementerGitState"] = after_impl_git_state
        _transition(report, "VALIDATING")
        pre_repair_guard = _validate_controlled_pre_repair_artifact(task, workspace, initial_workspace_snapshot, after_impl, initial_workspace_git_state, after_impl_git_state)
        report["validationAttempts"].append(pre_repair_guard)
        write_json_atomic(report_root / "validation_after_implementer.json", pre_repair_guard)
        if pre_repair_guard["verdict"] != "FIX_REQUIRED":
            return _finish(report, report_root, started, "FAILED", pre_repair_guard["errorCode"], pre_repair_guard["errorMessage"], root)

        service_before_repair = _service_validation_result(workspace, after_impl, initial_workspace_snapshot, changed_paths(initial_workspace_snapshot, after_impl))
        if service_before_repair:
            report["validationAttempts"].append(service_before_repair)
            return _finish(report, report_root, started, "FAILED", service_before_repair["errorCode"], service_before_repair["errorMessage"], root)
        _transition(report, "REPAIRING")
        repair_prompt = build_repair_prompt(task, pre_repair_guard["findings"])
        repair = _run_role(context, "repairer", "workspace-write", settings.repairerTimeoutSeconds, repair_prompt, 1)
        if repair != "success":
            return _finish_real_context(context, started)
        after_repair = snapshot_workspace(workspace)
        write_json_atomic(report_root / "workspace_after_repairer.json", after_repair)
        _transition(report, "VALIDATING")
        service_after_repair = _service_validation_result(workspace, after_repair, initial_workspace_snapshot, changed_paths(initial_workspace_snapshot, after_repair))
        if service_after_repair:
            report["validationAttempts"].append(service_after_repair)
            write_json_atomic(report_root / "validation_after_repairer_service_directory.json", service_after_repair)
            return _finish(report, report_root, started, "FAILED", service_after_repair["errorCode"], service_after_repair["errorMessage"], root)
        validation_repair = run_validations(task, workspace, initial_workspace_snapshot, after_repair)
        report["validationAttempts"].append(validation_repair)
        write_json_atomic(report_root / "validation_after_repairer.json", validation_repair)
        if validation_repair["verdict"] != "PASS":
            return _finish(report, report_root, started, "FAILED", "REAL_ROLE_EXECUTION_FAILED", "Host validation after repair did not pass.", root)
        artifact_errors = _validate_final_artifact(workspace)
        if artifact_errors:
            return _finish(report, report_root, started, "FAILED", "REAL_ROLE_EXECUTION_FAILED", "; ".join(artifact_errors), root)

        _transition(report, "AUDITING")
        before_auditor = snapshot_workspace(workspace)
        write_json_atomic(report_root / "workspace_before_auditor.json", before_auditor)
        service_before_auditor = _service_validation_result(workspace, before_auditor, initial_workspace_snapshot, changed_paths(initial_workspace_snapshot, before_auditor))
        if service_before_auditor:
            report["validationAttempts"].append(service_before_auditor)
            return _finish(report, report_root, started, "FAILED", service_before_auditor["errorCode"], service_before_auditor["errorMessage"], root)
        auditor = _run_role(context, "auditor", "read-only", settings.auditorTimeoutSeconds, build_auditor_prompt(task, validation_repair), 1)
        after_auditor = snapshot_workspace(workspace)
        write_json_atomic(report_root / "workspace_after_auditor.json", after_auditor)
        service_after_auditor = _service_validation_result(workspace, after_auditor, initial_workspace_snapshot, changed_paths(initial_workspace_snapshot, after_auditor))
        if service_after_auditor:
            report["validationAttempts"].append(service_after_auditor)
            return _finish(report, report_root, started, "FAILED", service_after_auditor["errorCode"], service_after_auditor["errorMessage"], root)
        if before_auditor != after_auditor:
            return _finish(report, report_root, started, "FAILED", "REAL_TEST_AUDITOR_CHANGED_WORKSPACE", "Auditor changed workspace snapshot.", root)
        if auditor != "success":
            return _finish_real_context(context, started)
        _transition(report, "COMPLETED")
        return _finish(report, report_root, started, "COMPLETED", None, None, root)
    except Exception as exc:
        _write_internal_diagnostic(report_root, exc)
        return _finish(report, report_root, started, "FAILED", "REAL_ROLE_INTERNAL_ERROR", _sanitize_error_message(exc), root)


class _RealContext:
    def __init__(self, root: Path, automation_root: Path, workspace: Path, report_root: Path, settings: RealRoleSettings, task: dict[str, Any], report: dict[str, Any]) -> None:
        self.root = root
        self.automation_root = automation_root
        self.workspace = workspace
        self.report_root = report_root
        self.settings = settings
        self.task = task
        self.report = report


def _run_role(context: _RealContext, role: str, sandbox: str, timeout_seconds: int, prompt: str, attempt: int) -> str:
    report = context.report
    if report["codexInvocationCount"] >= context.settings.maxRealCodexInvocationsPerTest:
        report["errorCode"] = "REAL_ROLE_INVOCATION_LIMIT"
        report["errorMessage"] = "Real role invocation limit reached."
        report["finalState"] = "FAILED"
        return "stop"
    report["codexInvocationCount"] += 1
    report["realCodexStarted"] = True
    schema = context.automation_root / "schemas" / _schema_for_role(role)
    suffix = f"_attempt_{attempt}" if role == "repairer" else ""
    last_message = context.report_root / f"{role}_last_message{suffix}.json"
    stdout_path = context.report_root / f"{role}_stdout{suffix}.jsonl"
    stderr_path = context.report_root / f"{role}_stderr{suffix}.log"
    prompt_path = context.report_root / (f"{role}_prompt_attempt_{attempt}.txt" if role == "repairer" else f"{role}_prompt.txt")
    _write_text(prompt_path, prompt)
    command = build_codex_command(role, sandbox, context.workspace, schema, last_message, context.settings)
    report["actualCommands"].append(sanitize_command(command))
    process = run_process_with_timeout(command, context.workspace, prompt, timeout_seconds)
    _write_text(stdout_path, process["stdout"])
    _write_text(stderr_path, process["stderr"])
    event_summary = parse_jsonl_events(process["stdout"])
    if not event_summary["schemaInvalid"]:
        stderr_schema_error = classify_schema_invalid_text(process["stderr"])
        if stderr_schema_error is not None:
            event_summary["schemaInvalid"] = True
            event_summary["schemaErrorCode"] = stderr_schema_error["schemaErrorCode"]
            event_summary["schemaErrorMessage"] = stderr_schema_error["schemaErrorMessage"]
    report["eventSummaries"][role] = event_summary

    result_data = None
    errors: list[str] = []
    if process["timed_out"]:
        errors.append("TIMEOUT")
    if process["exit_code"] != 0:
        errors.append("CODEX_EXEC_FAILED")
    if event_summary["parseErrors"]:
        errors.append("CODEX_JSONL_INVALID_LINE")
    if event_summary["schemaInvalid"]:
        message = event_summary.get("schemaErrorMessage") or "Invalid response_format schema."
        errors.append(f"CODEX_EXEC_SCHEMA_INVALID: {message}")
    elif event_summary["turnFailed"] and event_summary["rateLimit"] is None:
        errors.append("CODEX_EXEC_ERROR_UNCLASSIFIED")
    if event_summary["rateLimit"] is not None:
        usage = event_summary["rateLimit"]
        report["rateLimitPauses"].append({"pausedRole": role, "pausedFromState": report["stateHistory"][-1], "usageLimit": usage})
        report["finalState"] = "PAUSED_RATE_LIMIT"
        report["errorCode"] = usage["errorCode"]
        report["errorMessage"] = usage["message"]
        return "stop"
    if not last_message.exists():
        errors.append("CODEX_LAST_MESSAGE_MISSING")
    elif last_message.stat().st_size == 0:
        errors.append("CODEX_LAST_MESSAGE_EMPTY")
    else:
        raw = last_message.read_text(encoding="utf-8").strip()
        try:
            result_data = json.loads(raw)
        except json.JSONDecodeError as exc:
            errors.append(f"CODEX_RESULT_INVALID_JSON: {exc}")
    if result_data is not None:
        write_json_atomic(context.report_root / f"{role}_last_message{suffix}.json", result_data)
    execution = {
        "executionStatus": ExecutionStatus.SUCCESS.value if not errors else ExecutionStatus.TECHNICAL_ERROR.value,
        "role": role,
        "taskId": context.task["id"],
        "startedAt": utc_now(),
        "finishedAt": utc_now(),
        "durationSeconds": 0,
        "errorCode": None if not errors else "REAL_ROLE_EXECUTION_FAILED",
        "errorMessage": None if not errors else "; ".join(errors),
        "usageLimit": None,
        "result": result_data if not errors else None,
    }
    wrapper_errors = validate_role_execution(execution, context.automation_root / "schemas", role, context.task["id"])
    domain_errors: list[str] = []
    if not wrapper_errors and result_data is not None:
        if role == "implementer":
            domain_errors = validate_implementation_result(result_data, context.automation_root / "schemas", context.task["id"])
        elif role == "repairer":
            domain_errors = validate_repair_result(result_data, context.automation_root / "schemas", context.task["id"], {"validationFindings": context.report["validationAttempts"][-1]["findings"] if context.report["validationAttempts"] else [], "auditFindings": []})
        else:
            domain_errors = validate_audit_result(result_data, context.automation_root / "schemas", context.task["id"])
    execution["exitCode"] = process["exit_code"]
    execution["timedOut"] = process["timed_out"]
    execution["termination"] = process["termination"]
    if event_summary["schemaInvalid"]:
        execution["schemaErrorCode"] = event_summary["schemaErrorCode"]
        execution["schemaErrorMessage"] = event_summary["schemaErrorMessage"]
    execution["validationErrors"] = wrapper_errors + domain_errors
    write_json_atomic(context.report_root / (f"{role}_execution_result_attempt_{attempt}.json" if role == "repairer" else f"{role}_execution_result.json"), execution)
    if role == "implementer":
        report["implementationExecution"] = execution
    elif role == "repairer":
        report["repairExecutionAttempts"].append(execution)
    else:
        report["auditExecutionAttempts"].append(execution)
    if execution["validationErrors"] or errors:
        report["finalState"] = "FAILED"
        report["errorCode"] = _role_error_code(role, domain_errors, wrapper_errors, errors)
        report["errorMessage"] = "; ".join(execution["validationErrors"] or errors)
        return "stop"
    return "success"


def build_codex_command(role: str, sandbox: str, workspace: Path, schema: Path, last_message: Path, settings: RealRoleSettings) -> list[str]:
    executable = shutil.which("codex.cmd") or shutil.which("codex") or "codex.cmd"
    return [
        executable,
        "exec",
        "--ignore-user-config",
        "-c",
        f'windows.sandbox="{settings.windowsSandboxImplementation}"',
        "-c",
        f'approval_policy="{settings.approvalPolicy}"',
        "--sandbox",
        sandbox,
        "--cd",
        str(workspace),
        "--json",
        "--output-schema",
        str(schema),
        "--output-last-message",
        str(last_message),
        "--color",
        "never",
        "-",
    ]


def build_sandbox_probe_command(workspace: Path, settings: RealRoleSettings) -> list[str]:
    executable = shutil.which("codex.cmd") or shutil.which("codex") or "codex.cmd"
    script = _sandbox_probe_powershell_script()
    return [
        executable,
        "sandbox",
        "-P",
        ":workspace",
        "-C",
        str(workspace),
        "-c",
        f'windows.sandbox="{settings.windowsSandboxImplementation}"',
        "--",
        "powershell.exe",
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        script,
    ]


def sanitize_command(command: list[str]) -> list[str]:
    return [str(part) for part in command]


def build_implementer_prompt(task: dict[str, Any]) -> str:
    return "\n".join(
        [
            "ROLE: real implementer for controlled isolated pipeline self-test.",
            "Work only in this isolated workspace.",
            "You must actually create artifact.json on disk.",
            "Creating artifact.json is explicitly allowed and required.",
            "artifact.json is the only file you may create or modify.",
            "Do not modify AGENTS.md, task.json, or .git.",
            "Do not merely return a structured report describing the file.",
            "Create the controlled pre-repair state exactly:",
            '{"taskId":"REAL-PIPELINE-TEST-001","value":41,"repairApplied":false}',
            "Do not repair it. This is intentional and not a task error.",
            "Do not create any other files.",
            "Before returning the structured implementation result:",
            "1. verify artifact.json exists;",
            "2. read it back from disk;",
            "3. parse it as JSON;",
            "4. verify the exact object equals {\"taskId\":\"REAL-PIPELINE-TEST-001\",\"value\":41,\"repairApplied\":false};",
            "5. verify no additional files were created or modified.",
            "If file creation or verification fails:",
            "- do not claim filesChanged contains artifact.json;",
            "- clearly report the failure in summary and knownIssues;",
            "- do not claim successful filesystem implementation.",
            "Structured output never replaces the filesystem operation.",
            f"TASK: {json.dumps(task, indent=2)}",
            "Return only the structured implementation_result JSON.",
        ]
    )


def build_repair_prompt(task: dict[str, Any], findings: list[dict[str, Any]]) -> str:
    return "\n".join(
        [
            "ROLE: real repairer for controlled isolated pipeline self-test.",
            "Work only in this isolated workspace. Modify only artifact.json.",
            f"Findings to fix: {json.dumps(findings, indent=2)}",
            f"Acceptance criteria: {json.dumps(task['acceptanceCriteria'], indent=2)}",
            "Set artifact.json exactly to taskId REAL-PIPELINE-TEST-001, value 42, repairApplied true.",
            "Do not create any other files.",
            "Return only the structured repair_result JSON.",
        ]
    )


def build_auditor_prompt(task: dict[str, Any], validation: dict[str, Any]) -> str:
    return "\n".join(
        [
            "ROLE: real read-only auditor for controlled isolated pipeline self-test.",
            "Read artifact.json only. Do not modify files. Do not create tasks.",
            f"Acceptance criteria: {json.dumps(task['acceptanceCriteria'], indent=2)}",
            f"Host validation: {json.dumps(validation, indent=2)}",
            "Always include blockedReason: null for PASS or FIX_REQUIRED; use a short non-empty string only for BLOCKED.",
            "Return only the structured audit_result JSON.",
        ]
    )


def _schema_for_role(role: str) -> str:
    return {"implementer": "implementation_result.schema.json", "repairer": "repair_result.schema.json", "auditor": "audit_result.schema.json"}[role]


def _validate_final_artifact(workspace: Path) -> list[str]:
    artifact = workspace / "artifact.json"
    data, error = _read_json_file(artifact)
    if error:
        return [f"artifact.json invalid: {error}"]
    expected = {"taskId": REAL_TASK_ID, "value": 42, "repairApplied": True}
    if data != expected:
        return [f"artifact.json does not exactly match expected final object: {data!r}"]
    return []


def _validate_controlled_pre_repair_artifact(
    task: dict[str, Any],
    workspace: Path,
    initial_snapshot: dict[str, Any],
    after_snapshot: dict[str, Any],
    initial_git_state: dict[str, Any] | None = None,
    after_git_state: dict[str, Any] | None = None,
) -> dict[str, Any]:
    actual_changed = changed_paths(initial_snapshot, after_snapshot)
    findings: list[dict[str, Any]] = []
    snapshot_problem = snapshot_error(after_snapshot)
    if snapshot_problem:
        findings.append(_real_finding(snapshot_problem["errorCode"], snapshot_problem["message"]))
        return _real_validation("BLOCKED", snapshot_problem["errorCode"], snapshot_problem["message"], actual_changed, findings)
    service_error = validate_service_directory(workspace, after_snapshot, initial_snapshot)
    if service_error:
        code = service_error["errorCode"]
        findings.append(_real_finding(code, service_error["errorMessage"], ".agents"))
        return _real_validation("FAILED", code, service_error["errorMessage"], actual_changed, findings, service_error.get("unexpectedPaths", []))
    if initial_git_state is not None and after_git_state is not None and initial_git_state != after_git_state:
        code = "REAL_TEST_GIT_STATE_CHANGED"
        message = "Implementer changed isolated workspace Git state."
        findings.append(_real_finding(code, message, ".git"))
        return _real_validation("FAILED", code, message, actual_changed, findings, _unexpected_changed_paths(actual_changed))
    data, error = _read_json_file(workspace / "artifact.json")
    if error == "JSON_FILE_MISSING":
        code = "REAL_TEST_REQUIRED_ARTIFACT_MISSING"
        message = "artifact.json was not created by implementer."
        findings.append(_real_finding(code, message, "artifact.json"))
        return _real_validation("FAILED", code, message, actual_changed, findings)
    if actual_changed != ["artifact.json"]:
        unexpected = _unexpected_changed_paths(actual_changed)
        code = "REAL_TEST_UNEXPECTED_FILE"
        message = f"Implementer changed paths outside artifact.json: {unexpected}"
        findings.append(_real_finding(code, message))
        return _real_validation("FAILED", code, message, actual_changed, findings, unexpected)
    if error:
        code = error
        message = f"artifact.json is not a valid controlled pre-repair artifact: {error}"
        findings.append(_real_finding(error, message, "artifact.json"))
        return _real_validation("FAILED", code, message, actual_changed, findings)
    expected_pre_repair = {"taskId": task.get("id"), "value": 41, "repairApplied": False}
    expected_final = {"taskId": task.get("id"), "value": 42, "repairApplied": True}
    if data == expected_final:
        code = "REAL_TEST_IMPLEMENTER_SKIPPED_REQUIRED_PRE_REPAIR_STATE"
        message = "Implementer created the final artifact instead of the controlled pre-repair state."
        findings.append(_real_finding(code, message, "artifact.json"))
        return _real_validation("FAILED", code, message, actual_changed, findings)
    if data != expected_pre_repair:
        code = "REAL_ROLE_EXECUTION_FAILED"
        message = f"Implementer did not create the controlled pre-repair artifact: {data!r}"
        findings.append(_real_finding(code, message, "artifact.json"))
        return _real_validation("FAILED", code, message, actual_changed, findings)
    findings.append(_real_finding("ARTIFACT_VALUE_INCORRECT", "Controlled pre-repair artifact requires repair.", "artifact.json"))
    return _real_validation("FIX_REQUIRED", None, None, actual_changed, findings)


def _service_validation_result(
    workspace: Path,
    snapshot: dict[str, Any],
    initial_snapshot: dict[str, Any],
    actual_changed: list[str],
) -> dict[str, Any] | None:
    service_error = validate_service_directory(workspace, snapshot, initial_snapshot)
    if not service_error:
        return None
    code = service_error["errorCode"]
    findings = [_real_finding(code, service_error["errorMessage"], ".agents")]
    return _real_validation("FAILED", code, service_error["errorMessage"], actual_changed, findings, service_error.get("unexpectedPaths", []))


def _real_validation(
    verdict: str,
    error_code: str | None,
    error_message: str | None,
    actual_changed: list[str],
    findings: list[dict[str, Any]],
    unexpected_paths: list[str] | None = None,
) -> dict[str, Any]:
    return {
        "verdict": verdict,
        "errorCode": error_code,
        "errorMessage": error_message,
        "results": [],
        "findings": findings,
        "actualChangedPaths": actual_changed,
        "unexpectedPaths": unexpected_paths or [],
    }


def _unexpected_changed_paths(actual_changed: list[str]) -> list[str]:
    return sorted(path for path in actual_changed if path != "artifact.json")


def _real_finding(code: str, problem: str, file_name: str = "") -> dict[str, Any]:
    return {
        "code": code,
        "severity": "blocking",
        "file": file_name,
        "symbol": "",
        "problem": problem,
        "evidence": problem,
        "requiredFix": "Implementer must create only the controlled pre-repair artifact before repair.",
    }


def _role_error_code(role: str, domain_errors: list[str], wrapper_errors: list[str], process_errors: list[str]) -> str:
    if wrapper_errors:
        return "ROLE_EXECUTION_RESULT_INVALID"
    if domain_errors:
        return {"implementer": "IMPLEMENTATION_RESULT_INVALID", "repairer": "REPAIR_RESULT_INVALID", "auditor": "AUDIT_RESULT_INVALID"}[role]
    if _has_process_error(process_errors, "CODEX_EXEC_SCHEMA_INVALID"):
        return "CODEX_EXEC_SCHEMA_INVALID"
    if "CODEX_JSONL_INVALID_LINE" in process_errors:
        return "CODEX_JSONL_INVALID_LINE"
    if "CODEX_EXEC_ERROR_UNCLASSIFIED" in process_errors:
        return "CODEX_EXEC_ERROR_UNCLASSIFIED"
    return "REAL_ROLE_EXECUTION_FAILED"


def _has_process_error(process_errors: list[str], code: str) -> bool:
    return any(error == code or error.startswith(f"{code}:") for error in process_errors)


def _base_report(run_id: str, task: dict[str, Any], started_at: str, settings: RealRoleSettings | None) -> dict[str, Any]:
    return {
        "runId": run_id,
        "taskId": task.get("id"),
        "startedAt": started_at,
        "finishedAt": None,
        "durationSeconds": None,
        "codexCliVersion": None,
        "actualCommands": [],
        "workspacePath": None,
        "parentGitChanged": False,
        "initialParentGitSnapshot": [],
        "finalParentGitSnapshot": [],
        "workspaceInitialSnapshot": {},
        "workspaceInitialGitState": None,
        "workspaceAfterImplementerGitState": None,
        "workspaceFinalSnapshot": {},
        "sandboxWriteProbe": None,
        "actualChangedPaths": [],
        "implementationExecution": None,
        "validationAttempts": [],
        "repairExecutionAttempts": [],
        "auditExecutionAttempts": [],
        "eventSummaries": {},
        "codexInvocationCount": 0,
        "maxRealCodexInvocations": settings.maxRealCodexInvocationsPerTest if settings else None,
        "rateLimitPauses": [],
        "stateHistory": ["PENDING"],
        "finalState": "PENDING",
        "finalVerdict": "FAILED",
        "errorCode": None,
        "errorMessage": None,
        "realCodexStarted": False,
        "unityStarted": False,
        "sleepPerformed": False,
        "automaticCreditUsage": False,
        "automaticModelDowngrade": False,
        "warnings": [],
    }


def _transition(report: dict[str, Any], state: str) -> None:
    report["stateHistory"].append(state)
    report["finalState"] = state


def _set_terminal_state(report: dict[str, Any], state: str) -> None:
    if report["stateHistory"][-1] != state:
        report["stateHistory"].append(state)
    report["finalState"] = state


def _finish_real_context(context: _RealContext, started: float) -> dict[str, Any]:
    return _finish(context.report, context.report_root, started, context.report["finalState"], context.report.get("errorCode"), context.report.get("errorMessage"), context.root)


def _path_component_is_symlink_or_reparse(path: Path) -> bool:
    try:
        if not path.exists() and not path.is_symlink():
            return False
        if path.is_symlink():
            return True
        attrs = getattr(path.stat(follow_symlinks=False), "st_file_attributes", 0)
        return bool(attrs & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))
    except OSError:
        return True


def _validate_sandbox_probe_report_for_real_roles(root: Path, context: ProbeRunContext, settings: RealRoleSettings) -> ProbeGateResult:
    context_error = _validate_probe_run_context(root, context)
    if context_error:
        return ProbeGateResult(False, context_error[0], context_error[1], None)
    loaded, load_error, loaded_path = _load_current_probe_report(root, context)
    if load_error:
        return ProbeGateResult(False, load_error[0], load_error[1], None)
    if loaded is None:
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report could not be loaded.", None)
    if loaded.get("probeRunId") != context.probe_run_id or loaded_path != context.report_path.resolve(strict=True):
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_STALE", "Sandbox probe report does not belong to the current probe run.", loaded)
    if loaded.get("workspacePath") != relative_to_root(root, context.workspace):
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_STALE", "Sandbox probe workspace path does not match the current probe run.", loaded)
    contract = _validate_probe_pass_contract(loaded, settings)
    if not contract.ok:
        return ProbeGateResult(False, contract.errorCode, contract.errorMessage, loaded)
    return ProbeGateResult(True, None, None, loaded)


def _validate_probe_run_context(root: Path, context: ProbeRunContext) -> tuple[str, str] | None:
    expected_probe_root = root / "CodexAutomation" / "runtime" / "real_role_sandbox_probes"
    expected_run_directory = context.configured_probe_root / context.probe_run_id
    if not isinstance(context.probe_run_id, str) or not context.probe_run_id.strip():
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe context is missing probeRunId.")
    if any(part in {"", ".", ".."} for part in Path(context.probe_run_id).parts) or Path(context.probe_run_id).name != context.probe_run_id:
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probeRunId is unsafe.")
    if context.configured_probe_root != expected_probe_root:
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe root does not match configured runtime root.")
    if context.run_directory != expected_run_directory:
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe run directory does not match trusted probeRunId.")
    if context.workspace != context.run_directory / "workspace":
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe workspace path is invalid.")
    if context.report_path != context.run_directory / "SANDBOX_WRITE_PROBE_REPORT.json":
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report path is invalid.")
    try:
        runtime_root = (root / "CodexAutomation" / "runtime").resolve(strict=False)
        configured = context.configured_probe_root.resolve(strict=False)
        run_directory = context.run_directory.resolve(strict=False)
        workspace = context.workspace.resolve(strict=False)
        report_parent = context.report_path.parent.resolve(strict=False)
        report_path = context.report_path.resolve(strict=False)
        configured.relative_to(runtime_root)
        run_directory.relative_to(configured)
        workspace.relative_to(run_directory)
        report_parent.relative_to(run_directory)
        report_path.relative_to(run_directory)
    except (OSError, ValueError):
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report path is outside runtime.")
    for path in (context.configured_probe_root, context.run_directory, context.workspace, context.report_path.parent):
        if _path_component_is_symlink_or_reparse(path):
            return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe path contains a symlink or reparse point.")
    if (context.report_path.exists() or context.report_path.is_symlink()) and _path_component_is_symlink_or_reparse(context.report_path):
        return ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report is a symlink or reparse point.")
    return None


def _load_current_probe_report(root: Path, context: ProbeRunContext) -> tuple[dict[str, Any] | None, tuple[str, str] | None, Path | None]:
    context_error = _validate_probe_run_context(root, context)
    if context_error:
        return None, context_error, None
    report_path = context.report_path
    if not report_path.exists() and not report_path.is_symlink():
        return None, ("SANDBOX_WRITE_PROBE_REPORT_MISSING", "Sandbox probe final report is missing."), None
    try:
        if report_path.is_symlink() or not report_path.is_file():
            return None, ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report is not a regular file."), None
        resolved_existing = report_path.resolve(strict=True)
        if resolved_existing != report_path.resolve(strict=False):
            return None, ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report resolves unexpectedly."), None
        text = report_path.read_text(encoding="utf-8")
        data = json.loads(text)
    except UnicodeDecodeError:
        return None, ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report is not valid UTF-8."), None
    except json.JSONDecodeError:
        return None, ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report is malformed JSON."), None
    except OSError:
        return None, ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report could not be read."), None
    if not isinstance(data, dict):
        return None, ("SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report JSON root must be an object."), None
    return data, None, report_path.resolve(strict=True)


def _validate_probe_pass_contract(report: dict[str, Any], settings: RealRoleSettings) -> ProbeGateResult:
    if report.get("reportWriteError"):
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED", "Sandbox probe report write failed.", report)
    for warning in report.get("reportWarnings") or []:
        if isinstance(warning, dict) and warning.get("code") == "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED":
            return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED", "Sandbox probe report write failed.", report)
    if report.get("finalVerdict") == "RUNNING" or report.get("finishedAt") is None:
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_NOT_FINALIZED", "Sandbox probe report is not finalized.", report)
    if report.get("finalVerdict") != "PASS":
        return ProbeGateResult(False, str(report.get("errorCode") or "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"), str(report.get("errorMessage") or "Sandbox probe did not pass."), report)
    duration = report.get("durationSeconds")
    process_id = report.get("processId")
    if report.get("errorCode") is not None or report.get("errorMessage") is not None:
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Passing sandbox probe report contains an error.", report)
    if not isinstance(report.get("startedAt"), str) or not isinstance(report.get("finishedAt"), str):
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report timestamps are invalid.", report)
    if not isinstance(duration, (int, float)) or isinstance(duration, bool) or duration < 0:
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe duration is invalid.", report)
    if report.get("windowsSandboxImplementation") != settings.windowsSandboxImplementation:
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe implementation does not match config.", report)
    if report.get("permissionsProfile") != ":workspace":
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe permissions profile is invalid.", report)
    if report.get("processStarted") is not True:
        return ProbeGateResult(False, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED", "Sandbox probe process did not start.", report)
    if not isinstance(process_id, int) or isinstance(process_id, bool):
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe processId is invalid.", report)
    if report.get("timedOut") is not False:
        return ProbeGateResult(False, "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT", "Sandbox probe timed out.", report)
    if report.get("exitCode") != 0:
        return ProbeGateResult(False, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED", "Sandbox probe exit code is not zero.", report)
    if report.get("terminationIncomplete") is True:
        return ProbeGateResult(False, "PROCESS_TERMINATION_INCOMPLETE", "Sandbox probe termination was incomplete.", report)
    for flag in ("probeFileCreated", "probeFileReadBack", "probeFileRemoved"):
        if report.get(flag) is not True:
            return ProbeGateResult(False, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED", f"Sandbox probe did not satisfy {flag}.", report)
    if report.get("unexpectedPaths") != []:
        return ProbeGateResult(False, "WINDOWS_SANDBOX_PROBE_UNEXPECTED_FILE", "Sandbox probe left unexpected paths.", report)
    if report.get("parentGitChanged") is not False:
        return ProbeGateResult(False, "FAILED_WRITE_DETECTED", "Sandbox probe changed parent Git state.", report)
    if report.get("realCodexStarted") is not False or report.get("unityStarted") is not False or report.get("networkUsed") is not False:
        return ProbeGateResult(False, "SANDBOX_WRITE_PROBE_REPORT_INVALID", "Sandbox probe report contains forbidden side effects.", report)
    return ProbeGateResult(True, None, None, report)


def _finish(report: dict[str, Any], report_root: Path, started: float, final_state: str, error_code: str | None, error_message: str | None, root: Path) -> dict[str, Any]:
    report["finishedAt"] = utc_now()
    report["durationSeconds"] = round(time.monotonic() - started, 3)
    _set_terminal_state(report, final_state)
    report["errorCode"] = error_code
    report["errorMessage"] = error_message
    final_git: list[str] = []
    try:
        final_git = _git_snapshot(root)
        report["finalParentGitSnapshot"] = final_git
        report["parentGitChanged"] = report.get("initialParentGitSnapshot", []) != final_git
    except Exception as exc:
        report["finalParentGitSnapshot"] = []
        report["parentGitChanged"] = None
        report.setdefault("warnings", []).append({"code": "FINAL_PARENT_GIT_SNAPSHOT_FAILED", "message": _sanitize_error_message(exc)})
        if report.get("errorCode") is None:
            report["errorCode"] = "FINAL_PARENT_GIT_SNAPSHOT_FAILED"
            _set_terminal_state(report, "FAILED")
    workspace_path = report.get("workspacePath")
    if workspace_path:
        workspace = root / workspace_path
        try:
            final_snapshot = snapshot_workspace(workspace)
            report["workspaceFinalSnapshot"] = final_snapshot
            report["actualChangedPaths"] = changed_paths(report.get("workspaceInitialSnapshot", {}), final_snapshot)
        except Exception as exc:
            report.setdefault("warnings", []).append({"code": "FINAL_WORKSPACE_SNAPSHOT_FAILED", "message": _sanitize_error_message(exc)})
    if report["parentGitChanged"] and error_code is None:
        report["errorCode"] = "FAILED_WRITE_DETECTED"
        _set_terminal_state(report, "FAILED")
    report["finalVerdict"] = "PASS" if report["finalState"] == "COMPLETED" and report["parentGitChanged"] is False else "FAILED"
    try:
        _write_text(report_root / "final_parent_git_snapshot.txt", "\n".join(final_git))
        write_json_atomic(report_root / "state_history.json", report["stateHistory"])
        write_json_atomic(report_root / "FINAL_REAL_ROLE_REPORT.json", report)
    except Exception as exc:
        message = f"FINAL_REAL_ROLE_REPORT_WRITE_FAILED: {_sanitize_error_message(exc)}"
        print(message)
        report["finalReportWriteError"] = message
    return report


def _codex_version(root: Path) -> tuple[str | None, str | None]:
    executable = shutil.which("codex.cmd") or shutil.which("codex")
    if not executable:
        return None, "Codex CLI was not found."
    completed = subprocess.run([executable, "--version"], cwd=str(root), capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        return None, completed.stderr.strip() or "codex --version failed."
    return completed.stdout.strip(), None


def _codex_help(root: Path) -> str:
    executable = shutil.which("codex.cmd") or shutil.which("codex") or "codex.cmd"
    completed = subprocess.run([executable, "exec", "--help"], cwd=str(root), capture_output=True, text=True, check=False)
    return completed.stdout + completed.stderr


def _codex_sandbox_help(root: Path) -> str:
    executable = shutil.which("codex.cmd") or shutil.which("codex") or "codex.cmd"
    completed = subprocess.run([executable, "sandbox", "--help"], cwd=str(root), capture_output=True, text=True, check=False)
    return completed.stdout + completed.stderr


def _git_snapshot(root: Path) -> list[str]:
    completed = subprocess.run(["git", "status", "--porcelain=v1", "--untracked-files=all"], cwd=str(root), capture_output=True, text=True, check=False)
    return completed.stdout.splitlines() if completed.returncode == 0 else []


def _workspace_git_state(workspace: Path) -> dict[str, Any]:
    git_dir = workspace / ".git"
    config_hash = None
    config_path = git_dir / "config"
    if config_path.exists() and config_path.is_file():
        try:
            config_hash = hashlib.sha256(config_path.read_bytes()).hexdigest()
        except OSError:
            config_hash = "READ_ERROR"
    head = _git_output(workspace, ["git", "rev-parse", "--verify", "HEAD"])
    staged = _git_output(workspace, ["git", "diff", "--cached", "--name-only"])
    remotes = _git_output(workspace, ["git", "remote"])
    refs = _git_output(workspace, ["git", "for-each-ref", "--format=%(refname):%(objectname)"])
    return {
        "head": head["stdout"] if head["ok"] else None,
        "stagedPaths": sorted(line for line in staged["stdout"].splitlines() if line) if staged["ok"] else [],
        "remotes": sorted(line for line in remotes["stdout"].splitlines() if line) if remotes["ok"] else [],
        "refs": sorted(line for line in refs["stdout"].splitlines() if line) if refs["ok"] else [],
        "configSha256": config_hash,
    }


def _git_output(cwd: Path, command: list[str]) -> dict[str, Any]:
    completed = subprocess.run(command, cwd=str(cwd), capture_output=True, text=True, check=False)
    return {"ok": completed.returncode == 0, "stdout": completed.stdout.strip()}


def _create_probe_workspace(root: Path, workspace: Path) -> str | None:
    runtime_root = (root / "CodexAutomation" / "runtime").resolve()
    try:
        shutil.rmtree(_fs_path(workspace), ignore_errors=True)
        os.makedirs(_fs_path(workspace), exist_ok=False)
        resolved = workspace.resolve(strict=True)
        resolved.relative_to(runtime_root)
    except (OSError, ValueError):
        return "WINDOWS_SANDBOX_PROBE_OUTSIDE_RUNTIME"
    forbidden = {
        root.resolve(),
        (root / "Assets").resolve(),
        (root / "CodexAutomation").resolve(),
    }
    if resolved in forbidden:
        return "WINDOWS_SANDBOX_PROBE_OUTSIDE_RUNTIME"
    workspace_error = snapshot_error(_snapshot_probe_workspace(workspace))
    if workspace_error:
        return workspace_error["errorCode"]
    return None


def _snapshot_probe_workspace(workspace: Path) -> dict[str, dict[str, Any]]:
    root_fs = _fs_path(workspace)
    try:
        if not os.path.exists(root_fs):
            return _probe_workspace_error("WORKSPACE_ROOT_NOT_FOUND", "Workspace root does not exist.")
        if not os.path.isdir(root_fs):
            return _probe_workspace_error("WORKSPACE_ROOT_NOT_DIRECTORY", "Workspace root is not a directory.")
    except OSError:
        return _probe_workspace_error("WORKSPACE_ROOT_NOT_FOUND", "Workspace root does not exist.")

    snapshot: dict[str, dict[str, Any]] = {}
    for directory, dirnames, filenames in os.walk(root_fs):
        for name in list(dirnames):
            path = os.path.join(directory, name)
            relative = os.path.relpath(path, root_fs).replace(os.sep, "/")
            if _is_symlink_or_reparse_fs_path(path):
                snapshot[relative] = {"type": "blocked_link", "size": 0, "sha256": None}
                dirnames.remove(name)
            else:
                snapshot[relative] = {"type": "directory", "size": 0, "sha256": None}
        for name in filenames:
            path = os.path.join(directory, name)
            relative = os.path.relpath(path, root_fs).replace(os.sep, "/")
            if _is_symlink_or_reparse_fs_path(path):
                snapshot[relative] = {"type": "blocked_link", "size": 0, "sha256": None}
                continue
            try:
                with open(path, "rb") as handle:
                    data = handle.read()
                snapshot[relative] = {"type": "file", "size": len(data), "sha256": hashlib.sha256(data).hexdigest()}
            except OSError:
                snapshot[relative] = {"type": "read_error", "size": 0, "sha256": None}
    return snapshot


def _probe_workspace_error(error_code: str, message: str) -> dict[str, dict[str, Any]]:
    return {WORKSPACE_ERROR_ENTRY: {"type": "error", "errorCode": error_code, "message": message}}


def _is_symlink_or_reparse_fs_path(path: str) -> bool:
    try:
        status = os.lstat(path)
    except OSError:
        return True
    if stat.S_ISLNK(status.st_mode):
        return True
    attrs = getattr(status, "st_file_attributes", 0)
    return bool(attrs & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _finish_probe(
    report: dict[str, Any],
    probe_root: Path,
    started: float,
    parent_initial: list[str],
    root: Path,
    verdict: str,
    error_code: str | None,
    error_message: str | None,
) -> dict[str, Any]:
    report["finishedAt"] = utc_now()
    report["durationSeconds"] = round(time.monotonic() - started, 3)
    report["finalVerdict"] = verdict
    report["errorCode"] = error_code
    report["errorMessage"] = error_message
    workspace_path = report.get("workspacePath")
    if workspace_path:
        try:
            final_snapshot = snapshot_workspace(root / workspace_path)
            problem = snapshot_error(final_snapshot)
            if problem:
                report.setdefault("reportWarnings", []).append({"code": problem["errorCode"], "message": problem["message"]})
            else:
                report["workspaceFinalSnapshot"] = final_snapshot
                report["unexpectedPaths"] = sorted(final_snapshot.keys())
        except Exception as exc:
            report.setdefault("reportWarnings", []).append({"code": "FINAL_WORKSPACE_SNAPSHOT_FAILED", "message": _sanitize_error_message(exc)})
    try:
        report["parentGitChanged"] = parent_initial != _git_snapshot(root)
    except Exception as exc:
        report["parentGitChanged"] = None
        report.setdefault("reportWarnings", []).append({"code": "FINAL_PARENT_GIT_SNAPSHOT_FAILED", "message": _sanitize_error_message(exc)})
    try:
        _write_probe_report(report, probe_root)
    except Exception as exc:
        message = f"SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED: {_sanitize_error_message(exc)}"
        print(message)
        report["reportWriteError"] = message
        report.setdefault("reportWarnings", []).append({"code": "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED", "message": message})
        if verdict == "PASS":
            report["finalVerdict"] = "FAILED"
            report["errorCode"] = "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED"
            report["errorMessage"] = "Sandbox probe final report could not be written."
    return report


def _write_probe_report(report: dict[str, Any], probe_root: Path) -> None:
    write_json_atomic(probe_root / "SANDBOX_WRITE_PROBE_REPORT.json", report)


def _initialize_probe_text_log(path: Path) -> None:
    write_text_long_safe(path, "", encoding="utf-8", newline="\n")


def _update_probe_process_report(report: dict[str, Any], probe_root: Path, process_result: dict[str, Any]) -> None:
    termination = process_result.get("termination") or {}
    report["processStarted"] = bool(process_result.get("process_started") or report.get("processStarted"))
    report["processId"] = process_result.get("pid") or report.get("processId")
    report["timedOut"] = bool(process_result.get("timed_out") or report.get("timedOut"))
    report["terminationAttempted"] = bool(termination.get("attempted") or report.get("terminationAttempted"))
    if termination.get("attempted"):
        report["taskkillStarted"] = True
        report["taskkillCompleted"] = termination.get("exit_code") is not None
    report["fallbackKillAttempted"] = bool(termination.get("fallback_kill_attempted") or report.get("fallbackKillAttempted"))
    report["terminationIncomplete"] = bool(termination.get("incomplete") or report.get("terminationIncomplete"))
    if process_result.get("exit_code") is not None:
        report["exitCode"] = process_result["exit_code"]
    write_json_atomic(
        probe_root / "probe_process.json",
        {
            "processStarted": report["processStarted"],
            "processId": report["processId"],
            "timedOut": report["timedOut"],
            "terminationAttempted": report["terminationAttempted"],
            "taskkillStarted": report["taskkillStarted"],
            "taskkillCompleted": report["taskkillCompleted"],
            "fallbackKillAttempted": report["fallbackKillAttempted"],
            "terminationIncomplete": report["terminationIncomplete"],
            "exitCode": report["exitCode"],
        },
    )


def _write_probe_termination_marker(probe_root: Path, report: dict[str, Any], process_result: dict[str, Any]) -> None:
    termination = process_result.get("termination") or {}
    write_json_atomic(
        probe_root / "probe_termination.json",
        {
            "timestamp": utc_now(),
            "processId": report.get("processId"),
            "taskkillAttempted": bool(termination.get("attempted")),
            "taskkillExitCode": termination.get("exit_code"),
            "fallbackKillAttempted": bool(termination.get("fallback_kill_attempted")),
            "terminationComplete": not bool(termination.get("incomplete")),
        },
    )


def _read_text_if_exists(path: Path) -> str:
    try:
        return read_text_long_safe(path, encoding="utf-8", errors="replace")
    except OSError:
        return ""


def _safe_output_summary(text: str) -> list[str]:
    return [line[:500] for line in text.splitlines()[:20]]


def _sandbox_probe_powershell_script() -> str:
    return (
        "$ErrorActionPreference='Stop'; "
        "try { "
        "function Convert-ToExtendedPath([string]$Path) { "
        "$full=[System.IO.Path]::GetFullPath($Path); "
        "if (-not [System.IO.Path]::IsPathRooted($full)) { throw 'PROBE_MARKER_PATH_NOT_ABSOLUTE' }; "
        "if ($full.StartsWith('\\\\?\\')) { return $full }; "
        "if ($full.StartsWith('\\\\')) { return '\\\\?\\UNC\\' + $full.Substring(2) }; "
        "return '\\\\?\\' + $full "
        "}; "
        "$workspaceLogicalPath=[System.IO.Path]::GetFullPath((Get-Location).ProviderPath); "
        "$markerLogicalPath=[System.IO.Path]::GetFullPath([System.IO.Path]::Combine($workspaceLogicalPath,'sandbox_write_probe.txt')); "
        "if ([System.IO.Path]::GetFileName($markerLogicalPath) -ne 'sandbox_write_probe.txt') { throw 'PROBE_MARKER_NAME_INVALID' }; "
        "$markerParent=[System.IO.Path]::GetDirectoryName($markerLogicalPath); "
        "if (-not [string]::Equals($markerParent,$workspaceLogicalPath,[System.StringComparison]::OrdinalIgnoreCase)) { throw 'PROBE_MARKER_OUTSIDE_WORKSPACE' }; "
        "$markerFilesystemPath=Convert-ToExtendedPath $markerLogicalPath; "
        "$expected='CODEX_SANDBOX_WRITE_OK'; "
        "$utf8NoBom=[System.Text.UTF8Encoding]::new($false); "
        "[System.IO.File]::WriteAllText($markerFilesystemPath,$expected,$utf8NoBom); "
        "if (-not [System.IO.File]::Exists($markerFilesystemPath)) { throw 'PROBE_FILE_NOT_CREATED' }; "
        "Write-Output 'PROBE_FILE_CREATED'; "
        "$actual=[System.IO.File]::ReadAllText($markerFilesystemPath,$utf8NoBom); "
        "if ($actual -ne $expected) { throw 'PROBE_READBACK_MISMATCH' }; "
        "Write-Output 'PROBE_FILE_READ_BACK'; "
        "[System.IO.File]::Delete($markerFilesystemPath); "
        "if ([System.IO.File]::Exists($markerFilesystemPath)) { throw 'PROBE_FILE_NOT_REMOVED' }; "
        "Write-Output 'PROBE_FILE_REMOVED'; "
        "exit 0 "
        "} catch { Write-Error $_; exit 1 }"
    )


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _write_internal_diagnostic(report_root: Path, exc: Exception) -> None:
    try:
        _write_text(report_root / "REAL_ROLE_INTERNAL_ERROR.trace.txt", "".join(traceback.format_exception(type(exc), exc, exc.__traceback__)))
    except Exception:
        pass


def _sanitize_error_message(exc: Exception) -> str:
    return f"{type(exc).__name__}: {exc}"


def _exact_bool(source: dict[str, Any], name: str, expected: bool, errors: list[str]) -> None:
    value = source.get(name)
    if not isinstance(value, bool) or value is not expected:
        errors.append(f"REAL_ROLE_CONFIG_INVALID: {name} must be exact {expected}.")


def _positive_int(source: dict[str, Any], name: str, errors: list[str]) -> int:
    value = source.get(name)
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        errors.append(f"REAL_ROLE_CONFIG_INVALID: {name} must be a positive integer.")
        return 1
    return value


def _int_range(source: dict[str, Any], name: str, minimum: int, maximum: int, errors: list[str]) -> int:
    value = source.get(name)
    if not isinstance(value, int) or isinstance(value, bool) or value < minimum or value > maximum:
        errors.append(f"REAL_ROLE_CONFIG_INVALID: {name} must be an integer from {minimum} to {maximum}.")
        return maximum
    return value


def _string(source: dict[str, Any], name: str, errors: list[str]) -> str:
    value = source.get(name)
    if not isinstance(value, str) or not value.strip():
        errors.append(f"REAL_ROLE_CONFIG_INVALID: {name} must be a non-empty string.")
        return ""
    return value
