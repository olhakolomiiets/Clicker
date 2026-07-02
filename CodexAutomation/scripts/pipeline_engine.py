from __future__ import annotations

import json
import time
from pathlib import Path
from typing import Any

from fake_role_adapter import FakeRoleAdapter
from file_utils import read_json, write_json_atomic
from pipeline_config import parse_pipeline_settings
from pipeline_models import ExecutionStatus, PipelineContext, PipelineSettings, PipelineState, transition
from pipeline_result_validator import (
    validate_audit_result,
    validate_implementation_result,
    validate_repair_result,
    validate_role_execution,
)
from pipeline_validator import changed_paths, run_validations, snapshot_error, snapshot_workspace, validate_pipeline_task
from prompt_builder import build_audit_prompt, build_implementation_prompt, build_repair_prompt
from usage_limit_models import utc_now


def run_pipeline_self_test(root: Path, automation_root: Path, config: dict[str, Any], task_path: Path, scenario: str = "normal") -> dict[str, Any]:
    started = time.monotonic()
    started_at = utc_now()
    run_id = f"pipeline_{utc_now().replace(':', '').replace('-', '').replace('.', '')}"
    settings, config_errors = _settings(config)
    run_dir = root / settings.pipelineReportRoot / run_id
    workspace = root / settings.runtimeWorkspaceRoot / run_id
    run_dir.mkdir(parents=True, exist_ok=True)
    workspace.mkdir(parents=True, exist_ok=True)
    task = read_json(task_path)
    write_json_atomic(run_dir / "task.json", task)
    initial_git = _git_snapshot(root)
    context = PipelineContext(root, automation_root, run_id, task, workspace, run_dir, settings)
    context.startedAt = started_at
    if config_errors:
        context.state = PipelineState.FAILED
        context.state_history.append(context.state.value)
        context.errorCode = "INVALID_PIPELINE_CONFIG"
        context.errorMessage = "; ".join(config_errors)
        return _final_report(context, started, initial_git, _git_snapshot(root))
    adapter = FakeRoleAdapter(scenario)
    task_errors = validate_pipeline_task(task, automation_root / "schemas" / "pipeline_task.schema.json")
    if task_errors:
        context.state = PipelineState.FAILED
        context.errorCode = "INVALID_TASK" if "REAL_WORKSPACE_WRITE_DISABLED" not in task_errors else "REAL_WORKSPACE_WRITE_DISABLED"
        context.errorMessage = "; ".join(task_errors)
        return _final_report(context, started, initial_git, _git_snapshot(root))
    _run_pipeline(context, adapter, started, initial_git)
    return _final_report(context, started, initial_git, _git_snapshot(root))


def run_rate_limit_self_test(root: Path, automation_root: Path, config: dict[str, Any], task_path: Path) -> dict[str, Any]:
    scenarios = [
        "rate_limit_implementer",
        "rate_limit_auditor",
        "rate_limit_repairer",
        "retry_exhausted",
        "unknown_reset",
        "credits_exhausted",
        "invalid_usage_timestamp",
    ]
    started = time.monotonic()
    run_id = f"rate_limit_{utc_now().replace(':', '').replace('-', '').replace('.', '')}"
    settings, _ = _settings(config)
    run_dir = root / settings.pipelineReportRoot / run_id
    run_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for scenario in scenarios:
        report = run_pipeline_self_test(root, automation_root, config, task_path, scenario)
        results.append(_scenario_summary(scenario, report))
    final = {
        "pipelineRunId": run_id,
        "startedAt": utc_now(),
        "finishedAt": utc_now(),
        "durationSeconds": round(time.monotonic() - started, 3),
        "scenarios": results,
        "allPassed": all(result["passed"] for result in results),
        "realCodexStarted": False,
        "sleepPerformed": False,
        "automaticCreditUsage": False,
        "automaticModelDowngrade": False,
    }
    write_json_atomic(run_dir / "rate_limit_scenarios.json", final)
    write_json_atomic(run_dir / "FINAL_PIPELINE_REPORT.json", final)
    return final


def resume_after_rate_limit(context: PipelineContext) -> str | None:
    if context.state != PipelineState.PAUSED_RATE_LIMIT:
        context.errorCode = "INVALID_STATE_TRANSITION"
        return context.errorCode
    pause = context.rateLimitPauses[-1]
    if pause["rateLimitRetryCount"] >= pause["maxRateLimitRetriesPerInvocation"]:
        context.errorCode = "CODEX_RATE_LIMIT_RETRY_EXHAUSTED"
        return context.errorCode
    context.rateLimitRetryCount += 1
    transition(context, PipelineState(pause["resumeState"]))
    return None


def _run_pipeline(context: PipelineContext, adapter: FakeRoleAdapter, started: float, initial_git: list[str]) -> None:
    transition(context, PipelineState.IMPLEMENTING)
    before = snapshot_workspace(context.workspace)
    before_error = snapshot_error(before)
    if before_error:
        transition(context, PipelineState.FAILED)
        context.errorCode = before_error["errorCode"]
        context.errorMessage = before_error["message"]
        return
    _write_prompt(context.run_dir / "implementation_prompt.txt", build_implementation_prompt(context.task))
    while True:
        if not _reserve_invocation(context, required=True):
            return
        implementation_execution = adapter.run_implementer(context.task, context.workspace)
        context.roleExecutionAttempts.append(implementation_execution)
        write_json_atomic(context.run_dir / "implementation_execution_result.json", implementation_execution)
        if not _validate_execution(context, implementation_execution, "implementer"):
            return
        status = _handle_role_status(context, implementation_execution, "implementer", PipelineState.IMPLEMENTING)
        if status == "stop":
            return
        if status == "success":
            break
    if not _validate_domain_result(context, "implementer", implementation_execution["result"]):
        return
    write_json_atomic(context.run_dir / "implementation_result.json", implementation_execution["result"])
    transition(context, PipelineState.VALIDATING)
    _validate_repair_audit_loop(context, adapter, before)


def _validate_repair_audit_loop(context: PipelineContext, adapter: FakeRoleAdapter, initial_snapshot: dict[str, Any]) -> None:
    while context.state in {PipelineState.VALIDATING, PipelineState.AUDITING, PipelineState.REPAIRING}:
        if context.state == PipelineState.VALIDATING:
            validation = run_validations(context.task, context.workspace, initial_snapshot, snapshot_workspace(context.workspace))
            context.validationAttempts.append(validation)
            context.activeFindings = validation["findings"]
            write_json_atomic(context.run_dir / f"validation_attempt_{len(context.validationAttempts) - 1}.json", validation)
            if validation["verdict"] == "PASS":
                transition(context, PipelineState.AUDITING)
            elif validation["verdict"] == "BLOCKED":
                transition(context, PipelineState.BLOCKED)
                context.errorCode = "VALIDATION_FAILED"
                return
            else:
                if not _start_repair(context):
                    return
        elif context.state == PipelineState.AUDITING:
            current_snapshot = snapshot_workspace(context.workspace)
            audit_prompt = build_audit_prompt(
                context.task,
                {"files": list(current_snapshot.keys()), "changedPaths": changed_paths(initial_snapshot, current_snapshot)},
                context.validationAttempts,
                context.roleExecutionAttempts[-1],
            )
            _write_prompt(context.run_dir / f"audit_prompt_attempt_{len(context.auditAttempts) + 1}.txt", audit_prompt)
            while True:
                if not _reserve_audit_invocation(context):
                    return
                audit_execution = adapter.run_auditor(context.task, context.workspace, context.validationAttempts[-1], context.currentRepairContext)
                context.roleExecutionAttempts.append(audit_execution)
                write_json_atomic(context.run_dir / f"audit_execution_result_attempt_{context.auditInvocationCount}.json", audit_execution)
                if not _validate_execution(context, audit_execution, "auditor"):
                    return
                status = _handle_role_status(context, audit_execution, "auditor", PipelineState.AUDITING)
                if status == "stop":
                    return
                if status == "success":
                    break
            audit_result = audit_execution["result"]
            if not _validate_domain_result(context, "auditor", audit_result):
                return
            context.auditAttempts.append(audit_result)
            write_json_atomic(context.run_dir / f"audit_result_attempt_{len(context.auditAttempts)}.json", audit_result)
            if audit_result["verdict"] == "PASS":
                transition(context, PipelineState.COMPLETED)
                return
            if audit_result["verdict"] == "BLOCKED":
                context.activeFindings = audit_result["findings"]
                transition(context, PipelineState.BLOCKED)
                return
            context.activeFindings = audit_result["findings"]
            if not _start_repair(context):
                return
        elif context.state == PipelineState.REPAIRING:
            _write_prompt(context.run_dir / f"repair_prompt_attempt_{context.repairAttempt}.txt", build_repair_prompt(context.task, context.currentRepairContext))
            while True:
                if not _reserve_invocation(context, required=False):
                    return
                repair_execution = adapter.run_repairer(context.task, context.workspace, context.currentRepairContext)
                context.roleExecutionAttempts.append(repair_execution)
                write_json_atomic(context.run_dir / f"repair_execution_result_attempt_{context.repairAttempt}.json", repair_execution)
                if not _validate_execution(context, repair_execution, "repairer"):
                    return
                status = _handle_role_status(context, repair_execution, "repairer", PipelineState.REPAIRING)
                if status == "stop":
                    return
                if status == "success":
                    break
            repair_result = repair_execution["result"]
            if not _validate_domain_result(context, "repairer", repair_result):
                return
            context.repairAttempts.append(repair_result)
            write_json_atomic(context.run_dir / f"repair_result_attempt_{context.repairAttempt}.json", repair_result)
            transition(context, PipelineState.VALIDATING)


def _start_repair(context: PipelineContext) -> bool:
    max_repairs = context.settings.maxRepairAttemptsDefault
    if context.repairAttempt >= max_repairs:
        context.state = PipelineState.FAILED_MAX_REPAIRS
        context.state_history.append(context.state.value)
        context.errorCode = "FAILED_MAX_REPAIRS"
        return False
    context.repairAttempt += 1
    context.currentRepairContext = {
        "task": context.task,
        "acceptanceCriteria": context.task["acceptanceCriteria"],
        "repairAttempt": context.repairAttempt,
        "maxRepairAttempts": max_repairs,
        "validationFindings": context.validationAttempts[-1]["findings"] if context.validationAttempts else [],
        "auditFindings": context.auditAttempts[-1]["findings"] if context.auditAttempts else [],
        "changedPaths": changed_paths({}, snapshot_workspace(context.workspace)),
        "workspaceSnapshotSummary": {"paths": list(snapshot_workspace(context.workspace).keys())},
        "codexInvocationCount": context.codexInvocationCount,
        "remainingInvocationBudget": context.settings.maxCodexInvocationsPerPipelineRun - context.codexInvocationCount,
    }
    transition(context, PipelineState.REPAIRING)
    return True


def _handle_role_status(context: PipelineContext, execution: dict[str, Any], role: str, from_state: PipelineState) -> str:
    if execution["executionStatus"] == ExecutionStatus.SUCCESS.value:
        context.rateLimitRetryCount = 0
        if context.errorCode in {"CODEX_USAGE_LIMIT_REACHED", "CODEX_WEEKLY_LIMIT_REACHED", "PIPELINE_PAUSED_RATE_LIMIT"}:
            context.errorCode = None
        return "success"
    if execution["executionStatus"] == ExecutionStatus.RATE_LIMITED.value:
        pause = _pause_context(context, execution, role, from_state)
        context.rateLimitPauses.append(pause)
        transition(context, PipelineState.PAUSED_RATE_LIMIT)
        context.errorCode = execution["errorCode"]
        if execution["errorCode"] in {"CODEX_RATE_LIMIT_RESET_UNKNOWN", "CODEX_CREDITS_EXHAUSTED"}:
            return "stop"
        retry_error = resume_after_rate_limit(context)
        if retry_error:
            return "stop"
        return "retry"
    transition(context, PipelineState.FAILED)
    context.errorCode = execution.get("errorCode") or "PIPELINE_INTERNAL_ERROR"
    context.errorMessage = execution.get("errorMessage")
    return "stop"


def _pause_context(context: PipelineContext, execution: dict[str, Any], role: str, from_state: PipelineState) -> dict[str, Any]:
    usage = execution["usageLimit"]
    return {
        "taskId": context.task["id"],
        "pausedRole": role,
        "pausedFromState": from_state.value,
        "resumeState": from_state.value,
        "detectedAtUtc": usage.get("detectedAtUtc"),
        "resetAtUtc": usage.get("resetAtUtc"),
        "retryAfterSeconds": usage.get("retryAfterSeconds"),
        "limitType": usage.get("limitType"),
        "rateLimitErrorCode": execution["errorCode"],
        "rateLimitMessage": execution.get("errorMessage"),
        "rateLimitRetryCount": context.rateLimitRetryCount,
        "maxRateLimitRetriesPerInvocation": context.settings.maxRateLimitRetriesPerInvocation,
        "codexInvocationCount": context.codexInvocationCount,
        "repairAttempt": context.repairAttempt,
        "activeFindings": context.activeFindings,
        "currentValidationAttempt": len(context.validationAttempts),
        "currentAuditAttempt": len(context.auditAttempts),
        "currentRepairContext": context.currentRepairContext,
    }


def _reserve_invocation(context: PipelineContext, required: bool) -> bool:
    max_count = context.settings.maxCodexInvocationsPerPipelineRun
    reserve = context.settings.reserveInvocationsForFinalAudit
    if context.codexInvocationCount + 1 > max_count:
        _record_budget_event(context, required, False, "general limit exhausted")
        context.state = PipelineState.FAILED_INVOCATION_BUDGET
        context.state_history.append(context.state.value)
        context.errorCode = "FAILED_INVOCATION_BUDGET"
        return False
    remaining_after = max_count - (context.codexInvocationCount + 1)
    if remaining_after < 0:
        _record_budget_event(context, required, False, "negative remaining budget")
        context.state = PipelineState.FAILED_INVOCATION_BUDGET
        context.state_history.append(context.state.value)
        context.errorCode = "FAILED_INVOCATION_BUDGET"
        return False
    if not required and remaining_after < reserve:
        _record_budget_event(context, required, False, "reserve protected")
        context.state = PipelineState.FAILED_INVOCATION_BUDGET
        context.state_history.append(context.state.value)
        context.errorCode = "FAILED_INVOCATION_BUDGET"
        return False
    _record_budget_event(context, required, True, None)
    context.codexInvocationCount += 1
    return True


def _reserve_audit_invocation(context: PipelineContext) -> bool:
    if context.auditInvocationCount >= context.settings.maxAuditsPerTask:
        context.state = PipelineState.FAILED
        context.state_history.append(context.state.value)
        context.errorCode = "FAILED_MAX_AUDITS"
        return False
    if not _reserve_invocation(context, required=True):
        return False
    context.auditInvocationCount += 1
    return True


def _settings(config: dict[str, Any]) -> tuple[PipelineSettings, list[str]]:
    settings, errors = parse_pipeline_settings(config)
    if settings is None:
        return PipelineSettings(), errors
    return settings, errors


def _record_budget_event(context: PipelineContext, required: bool, allowed: bool, reason: str | None) -> None:
    max_count = context.settings.maxCodexInvocationsPerPipelineRun
    reserve = context.settings.reserveInvocationsForFinalAudit
    remaining = max(0, max_count - context.codexInvocationCount - (1 if allowed else 0))
    context.invocationBudgetEvents.append(
        {
            "beforeCount": context.codexInvocationCount,
            "required": required,
            "allowed": allowed,
            "reason": reason,
            "remainingInvocationBudget": remaining,
            "reservedInvocationsRemaining": min(reserve, remaining),
        }
    )


def _validate_execution(context: PipelineContext, execution: dict[str, Any], role: str) -> bool:
    errors = validate_role_execution(execution, context.automation_root / "schemas", role, context.task["id"])
    if errors:
        code = "CODEX_RATE_LIMIT_DATA_INVALID" if any(error.startswith("CODEX_RATE_LIMIT_DATA_INVALID") for error in errors) else "ROLE_EXECUTION_RESULT_INVALID"
        _fail_result_validation(context, code, errors)
        return False
    return True


def _validate_domain_result(context: PipelineContext, role: str, result: dict[str, Any]) -> bool:
    schema_root = context.automation_root / "schemas"
    if role == "implementer":
        errors = validate_implementation_result(result, schema_root, context.task["id"])
        code = "IMPLEMENTATION_RESULT_INVALID"
    elif role == "auditor":
        errors = validate_audit_result(result, schema_root, context.task["id"])
        code = "AUDIT_RESULT_INVALID"
    else:
        errors = validate_repair_result(result, schema_root, context.task["id"], context.currentRepairContext)
        code = "REPAIR_RESULT_INVALID"
    if errors:
        _fail_result_validation(context, code, errors)
        return False
    return True


def _fail_result_validation(context: PipelineContext, code: str, errors: list[str]) -> None:
    context.resultValidationErrors.append({"code": code, "errors": errors})
    transition(context, PipelineState.FAILED)
    context.errorCode = code
    context.errorMessage = "; ".join(errors)


def _final_report(context: PipelineContext, started: float, initial_git: list[str], final_git: list[str]) -> dict[str, Any]:
    report = {
        "pipelineRunId": context.run_id,
        "taskId": context.task.get("id"),
        "startedAt": context.startedAt,
        "finishedAt": utc_now(),
        "durationSeconds": round(time.monotonic() - started, 3),
        "initialGitSnapshot": initial_git,
        "finalGitSnapshot": final_git,
        "gitChanged": initial_git != final_git,
        "initialState": "PENDING",
        "finalState": context.state.value,
        "stateHistory": context.state_history,
        "implementationResult": _first_result(context, "implementer"),
        "validationAttempts": context.validationAttempts,
        "auditAttempts": context.auditAttempts,
        "repairAttempts": context.repairAttempts,
        "roleExecutionAttempts": context.roleExecutionAttempts,
        "rateLimitPauses": context.rateLimitPauses,
        "codexInvocationCount": context.codexInvocationCount,
        "auditInvocationCount": context.auditInvocationCount,
        "maxCodexInvocationsPerPipelineRun": context.settings.maxCodexInvocationsPerPipelineRun,
        "reserveInvocationsForFinalAudit": context.settings.reserveInvocationsForFinalAudit,
        "maxAuditsPerTask": context.settings.maxAuditsPerTask,
        "remainingInvocationBudget": max(0, context.settings.maxCodexInvocationsPerPipelineRun - context.codexInvocationCount),
        "reservedInvocationsRemaining": min(
            context.settings.reserveInvocationsForFinalAudit,
            max(0, context.settings.maxCodexInvocationsPerPipelineRun - context.codexInvocationCount),
        ),
        "invocationBudgetEvents": context.invocationBudgetEvents,
        "resultValidationErrors": context.resultValidationErrors,
        "actualChangedPaths": changed_paths({}, snapshot_workspace(context.workspace)),
        "maxRepairAttempts": context.settings.maxRepairAttemptsDefault,
        "finalVerdict": "PASS" if context.state == PipelineState.COMPLETED and initial_git == final_git else "FAILED",
        "errorCode": "FAILED_WRITE_DETECTED" if initial_git != final_git else context.errorCode,
        "errorMessage": context.errorMessage,
    }
    write_json_atomic(context.run_dir / "state_history.json", context.state_history)
    write_json_atomic(context.run_dir / "FINAL_PIPELINE_REPORT.json", report)
    return report


def _first_result(context: PipelineContext, role: str) -> dict[str, Any] | None:
    for execution in context.roleExecutionAttempts:
        if execution["role"] == role and execution["result"] is not None:
            return execution["result"]
    return None


def _scenario_summary(scenario: str, report: dict[str, Any]) -> dict[str, Any]:
    expected_pause = {
        "rate_limit_implementer": "implementer",
        "rate_limit_auditor": "auditor",
        "rate_limit_repairer": "repairer",
        "retry_exhausted": "implementer",
        "unknown_reset": "implementer",
        "credits_exhausted": "implementer",
        "invalid_usage_timestamp": "implementer",
    }[scenario]
    pauses = report.get("rateLimitPauses", [])
    first_pause = pauses[0] if pauses else {}
    passed = bool(pauses) and first_pause.get("pausedRole") == expected_pause
    if scenario in {"rate_limit_implementer", "rate_limit_auditor", "rate_limit_repairer"}:
        passed = passed and report.get("finalState") == "COMPLETED"
    if scenario == "retry_exhausted":
        passed = passed and report.get("errorCode") == "CODEX_RATE_LIMIT_RETRY_EXHAUSTED"
    if scenario == "unknown_reset":
        passed = passed and first_pause.get("resetAtUtc") is None and report.get("errorCode") == "CODEX_RATE_LIMIT_RESET_UNKNOWN"
    if scenario == "credits_exhausted":
        passed = passed and first_pause.get("limitType") == "credits" and report.get("errorCode") == "CODEX_CREDITS_EXHAUSTED"
    if scenario == "invalid_usage_timestamp":
        passed = report.get("finalState") == "FAILED" and report.get("errorCode") == "CODEX_RATE_LIMIT_DATA_INVALID"
    expected_terminal = "COMPLETED" if scenario in {"rate_limit_implementer", "rate_limit_auditor", "rate_limit_repairer"} else "PAUSED_RATE_LIMIT"
    if scenario == "invalid_usage_timestamp":
        expected_terminal = "FAILED_INVALID_USAGE_LIMIT"
    pipeline_final_verdict = "PASS" if report.get("finalState") == "COMPLETED" else "PAUSED"
    if report.get("finalState") == "FAILED":
        pipeline_final_verdict = "FAILED"
    return {
        "scenario": scenario,
        "scenarioPassed": passed,
        "passed": passed,
        "pipelineFinalVerdict": pipeline_final_verdict,
        "expectedTerminalCondition": expected_terminal,
        "report": report,
    }


def _git_snapshot(root: Path) -> list[str]:
    import subprocess
    completed = subprocess.run(["git", "status", "--porcelain=v1", "--untracked-files=all"], cwd=str(root), capture_output=True, text=True, check=False)
    return completed.stdout.splitlines() if completed.returncode == 0 else []


def _write_prompt(path: Path, text: str) -> None:
    path.write_text(text, encoding="utf-8", newline="\n")
