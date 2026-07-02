from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any


class PipelineState(str, Enum):
    PENDING = "PENDING"
    IMPLEMENTING = "IMPLEMENTING"
    VALIDATING = "VALIDATING"
    AUDITING = "AUDITING"
    REPAIRING = "REPAIRING"
    PAUSED_RATE_LIMIT = "PAUSED_RATE_LIMIT"
    COMPLETED = "COMPLETED"
    BLOCKED = "BLOCKED"
    FAILED = "FAILED"
    FAILED_MAX_REPAIRS = "FAILED_MAX_REPAIRS"
    FAILED_INVOCATION_BUDGET = "FAILED_INVOCATION_BUDGET"


class ExecutionStatus(str, Enum):
    SUCCESS = "SUCCESS"
    RATE_LIMITED = "RATE_LIMITED"
    TECHNICAL_ERROR = "TECHNICAL_ERROR"


ERROR_CODES = {
    "INVALID_TASK",
    "INVALID_STATE_TRANSITION",
    "IMPLEMENTATION_RESULT_INVALID",
    "VALIDATION_FAILED",
    "AUDIT_RESULT_INVALID",
    "REPAIR_RESULT_INVALID",
    "FAILED_MAX_REPAIRS",
    "FAILED_WRITE_DETECTED",
    "REAL_WORKSPACE_WRITE_DISABLED",
    "PIPELINE_PREFLIGHT_FAILED",
    "PIPELINE_INTERNAL_ERROR",
    "FAILED_INVOCATION_BUDGET",
    "FAILED_MAX_AUDITS",
    "INVALID_PIPELINE_CONFIG",
    "ROLE_EXECUTION_RESULT_INVALID",
    "UNSAFE_ABSOLUTE_PATH",
    "UNSAFE_PARENT_TRAVERSAL",
    "UNSAFE_PATH_OUTSIDE_WORKSPACE",
    "UNSAFE_SYMLINK_OR_REPARSE_POINT",
    "INVALID_VALIDATION_PATH",
    "WORKSPACE_ROOT_NOT_FOUND",
    "WORKSPACE_ROOT_NOT_DIRECTORY",
    "CODEX_USAGE_LIMIT_REACHED",
    "CODEX_WEEKLY_LIMIT_REACHED",
    "CODEX_CREDITS_EXHAUSTED",
    "CODEX_RATE_LIMIT_RESET_UNKNOWN",
    "CODEX_RATE_LIMIT_RETRY_EXHAUSTED",
    "CODEX_RATE_LIMIT_DATA_INVALID",
    "PIPELINE_PAUSED_RATE_LIMIT",
}


ALLOWED_TRANSITIONS = {
    PipelineState.PENDING: {PipelineState.IMPLEMENTING},
    PipelineState.IMPLEMENTING: {PipelineState.VALIDATING, PipelineState.PAUSED_RATE_LIMIT, PipelineState.FAILED},
    PipelineState.VALIDATING: {PipelineState.AUDITING, PipelineState.REPAIRING, PipelineState.BLOCKED},
    PipelineState.AUDITING: {PipelineState.COMPLETED, PipelineState.REPAIRING, PipelineState.BLOCKED, PipelineState.PAUSED_RATE_LIMIT, PipelineState.FAILED},
    PipelineState.REPAIRING: {PipelineState.VALIDATING, PipelineState.PAUSED_RATE_LIMIT, PipelineState.FAILED, PipelineState.FAILED_MAX_REPAIRS},
    PipelineState.PAUSED_RATE_LIMIT: {PipelineState.IMPLEMENTING, PipelineState.AUDITING, PipelineState.REPAIRING},
}


@dataclass
class PipelineSettings:
    maxRepairAttemptsDefault: int = 3
    pipelineSelfTestEnabled: bool = True
    runtimeWorkspaceRoot: str = "CodexAutomation/runtime/workspaces"
    pipelineReportRoot: str = "CodexAutomation/runtime/pipeline_runs"
    allowRealWorkspaceWrite: bool = False
    maxCodexInvocationsPerPipelineRun: int = 20
    reserveInvocationsForFinalAudit: int = 2
    maxAuditsPerTask: int = 4
    maxRateLimitRetriesPerInvocation: int = 1


@dataclass
class PipelineContext:
    root: Path
    automation_root: Path
    run_id: str
    task: dict[str, Any]
    workspace: Path
    run_dir: Path
    settings: PipelineSettings
    state: PipelineState = PipelineState.PENDING
    state_history: list[str] = field(default_factory=lambda: [PipelineState.PENDING.value])
    codexInvocationCount: int = 0
    repairAttempt: int = 0
    rateLimitRetryCount: int = 0
    activeFindings: list[dict[str, Any]] = field(default_factory=list)
    validationAttempts: list[dict[str, Any]] = field(default_factory=list)
    auditAttempts: list[dict[str, Any]] = field(default_factory=list)
    repairAttempts: list[dict[str, Any]] = field(default_factory=list)
    roleExecutionAttempts: list[dict[str, Any]] = field(default_factory=list)
    rateLimitPauses: list[dict[str, Any]] = field(default_factory=list)
    currentValidationAttempt: int = 0
    currentAuditAttempt: int = 0
    currentRepairContext: dict[str, Any] = field(default_factory=dict)
    auditInvocationCount: int = 0
    invocationBudgetEvents: list[dict[str, Any]] = field(default_factory=list)
    resultValidationErrors: list[dict[str, Any]] = field(default_factory=list)
    errorCode: str | None = None
    errorMessage: str | None = None
    startedAt: str = ""


def transition(context: PipelineContext, next_state: PipelineState) -> None:
    allowed = ALLOWED_TRANSITIONS.get(context.state, set())
    if next_state not in allowed:
        context.errorCode = "INVALID_STATE_TRANSITION"
        context.errorMessage = f"{context.state.value} -> {next_state.value}"
        context.state = PipelineState.FAILED
        context.state_history.append(context.state.value)
        return
    context.state = next_state
    context.state_history.append(next_state.value)


def role_execution_result(
    execution_status: str,
    role: str,
    task_id: str,
    started_at: str,
    finished_at: str,
    duration_seconds: float,
    result: dict[str, Any] | None,
    error_code: str | None = None,
    error_message: str | None = None,
    usage_limit: dict[str, Any] | None = None,
) -> dict[str, Any]:
    return {
        "executionStatus": execution_status,
        "role": role,
        "taskId": task_id,
        "startedAt": started_at,
        "finishedAt": finished_at,
        "durationSeconds": duration_seconds,
        "errorCode": error_code,
        "errorMessage": error_message,
        "usageLimit": usage_limit,
        "result": result,
    }
