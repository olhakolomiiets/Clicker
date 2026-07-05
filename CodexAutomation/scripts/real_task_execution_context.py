from __future__ import annotations

import os
import hashlib
import secrets
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from file_utils import read_json
from parent_git_snapshot import parent_git_snapshot
from real_task_execution_models import (
    EXECUTION_CONTEXT_VERSION,
    EXECUTION_HANDLE_VERSION,
    REAL_TASK_EXECUTION_STAGE,
    RealTaskExecutionContext,
    RealTaskExecutionFailure,
    RealTaskExecutionHandle,
    canonical_sha256,
    new_capability,
    utc_now,
)
from real_task_report_writer import TrustedReportReceipt, write_trusted_report
from schema_validator import validate


_HANDLE_REGISTRY: dict[str, dict[str, Any]] = {}
ROLE_SEQUENCE = ("IMPLEMENTER", "REPAIRER", "AUDITOR")


@dataclass(frozen=True)
class ResolvedExecutionAuthority:
    handle: RealTaskExecutionHandle
    context: dict[str, Any]
    contextReceipt: TrustedReportReceipt
    registry: dict[str, Any]


def create_execution_context(
    root: Path,
    automation_root: Path,
    run_dir: Path,
    orchestration_run_id: str,
    manifest_path: Path,
    manifest_sha: str,
    effective_policy: dict[str, Any],
    execution_policy: dict[str, Any],
    foundation_report: dict[str, Any],
    invocation_budget: dict[str, int],
) -> tuple[RealTaskExecutionContext, str]:
    foundation_run_dir = root / "CodexAutomation" / "runtime" / "real_task_foundation_runs" / str(foundation_report["runId"])
    trusted_context = read_json(foundation_run_dir / "TRUSTED_RUN_CONTEXT.json")
    baseline = read_json(foundation_run_dir / "WORKSPACE_BASELINE_INVENTORY.json")
    source_post = read_json(foundation_run_dir / "SOURCE_POST_INVENTORY.json")
    parent_snapshot, git_errors = parent_git_snapshot(root)
    if git_errors:
        first = git_errors[0]
        raise RealTaskExecutionFailure(first.code, first.message)
    workspace = Path(str(trusted_context["workspaceDirectory"]))
    context = RealTaskExecutionContext(
        contextVersion=EXECUTION_CONTEXT_VERSION,
        orchestrationRunId=orchestration_run_id,
        taskId=str(trusted_context["taskId"]),
        stage=REAL_TASK_EXECUTION_STAGE,
        createdAt=utc_now(),
        runDirectory=str(run_dir.resolve(strict=False)),
        manifestPath=str(manifest_path.resolve(strict=True)),
        manifestSha256=manifest_sha,
        effectivePolicyHash=canonical_sha256(effective_policy),
        foundationRunId=str(foundation_report["runId"]),
        foundationRunDirectory=str(foundation_run_dir.resolve(strict=True)),
        foundationFinalReportHash=canonical_sha256(foundation_report),
        trustedContextHash=str(foundation_report["trustedContextHash"]),
        workspaceIdentity={
            "workspaceDirectory": str(workspace.resolve(strict=True)),
            "workspaceBaselineInventoryHash": baseline["inventorySha256"],
            "isolatedGitValid": foundation_report.get("isolatedGitValid") is True,
        },
        parentRepositoryIdentity={
            "repositoryRoot": str(root.resolve(strict=True)),
            "parentHead": _head_from_snapshot(parent_snapshot),
        },
        initialParentGitSnapshotHash=canonical_sha256(parent_snapshot),
        initialParentSourceInventoryHash=str(source_post["inventorySha256"]),
        baselineInventoryHash=str(baseline["inventorySha256"]),
        executionPolicyHash=canonical_sha256(execution_policy),
        invocationBudget={"maxRoleInvocations": invocation_budget["maxRoleInvocations"], "invocationsUsed": 0},
        repairBudget={"maxRepairAttempts": invocation_budget["maxRepairAttempts"], "repairsUsed": 0},
        roleSequence=[],
        noParentWrite=True,
        noAutomaticApply=True,
        noNetwork=True,
        noPackageInstall=True,
        noUnity=True,
    )
    context_hash = write_execution_context(automation_root, run_dir / "REAL_TASK_EXECUTION_CONTEXT.json", context)
    return context, context_hash


def write_execution_context(automation_root: Path, path: Path, context: RealTaskExecutionContext) -> str:
    payload = context.to_dict()
    if path.exists():
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_CONTEXT_INVALID", "Execution context already exists.")
    receipt = write_trusted_report(
        automation_root,
        path.resolve(strict=False).parents[2],
        path,
        payload,
        "real_task_execution_context.schema.json",
        lambda item: _validate_context_errors(automation_root, item),
        4_194_304,
    )
    reread = read_json(path)
    _validate_context(automation_root, reread)
    digest = canonical_sha256(reread)
    if digest != canonical_sha256(payload) or digest != receipt.sha256:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_CONTEXT_INVALID", "Execution context canonical hash mismatch.")
    return digest


def create_execution_handle(context: RealTaskExecutionContext, context_hash: str) -> RealTaskExecutionHandle:
    capability = new_capability()
    handle = RealTaskExecutionHandle(
        handleVersion=EXECUTION_HANDLE_VERSION,
        orchestrationRunId=context.orchestrationRunId,
        taskId=context.taskId,
        stage=REAL_TASK_EXECUTION_STAGE,
        capability=capability,
        executionContextHash=context_hash,
        foundationRunId=context.foundationRunId,
        trustedContextHash=context.trustedContextHash,
        workspaceDirectory=context.workspaceIdentity["workspaceDirectory"],
        runDirectory=context.runDirectory,
        state="ACTIVE",
        consumed=False,
        terminal=False,
    )
    context_path = Path(context.runDirectory).joinpath("REAL_TASK_EXECUTION_CONTEXT.json").resolve(strict=False)
    runtime_root = context_path.parents[2]
    _HANDLE_REGISTRY[context.orchestrationRunId] = {
        "capabilityDigest": _capability_digest(capability),
        "handleObjectId": id(handle),
        "orchestrationRunId": context.orchestrationRunId,
        "taskId": context.taskId,
        "executionContextHash": context_hash,
        "executionContextRelativePath": str(context_path.relative_to(runtime_root).as_posix()) if context_path.exists() else "real_task_runs/unpersisted/REAL_TASK_EXECUTION_CONTEXT.json",
        "executionContextPath": str(context_path),
        "executionContextSchemaVersion": context.contextVersion,
        "executionContextSize": context_path.stat().st_size if context_path.exists() else 0,
        "foundationRunId": context.foundationRunId,
        "foundationFinalReportHash": context.foundationFinalReportHash,
        "originalBaselineIdentity": context.baselineInventoryHash,
        "workspaceIdentity": context.workspaceIdentity,
        "currentState": "CONTEXT_READY",
        "invocationBudgetEffective": int(context.invocationBudget["maxRoleInvocations"]),
        "invocationsReserved": 0,
        "invocationsCompleted": 0,
        "invocationRoles": [],
        "repairBudgetEffective": int(context.repairBudget["maxRepairAttempts"]),
        "repairsReserved": 0,
        "repairsCompleted": 0,
        "activeReservation": None,
        "diagnosticReceipts": {},
        "terminal": False,
        "consumed": False,
        "owner": "runner",
        "ownerThreadId": threading.get_ident(),
    }
    return handle


def resolve_execution_authority(
    handle: RealTaskExecutionHandle,
    automation_root: Path,
    expected_states: tuple[str, ...] | None = None,
) -> ResolvedExecutionAuthority:
    validate_execution_handle(handle, expected_states)
    entry = _HANDLE_REGISTRY[handle.orchestrationRunId]
    context_path = Path(str(entry.get("executionContextPath", ""))).resolve(strict=False)
    run_dir = Path(handle.runDirectory).resolve(strict=False)
    runtime = context_path.parents[2].resolve(strict=False)
    try:
        context_path.relative_to(run_dir)
        context_path.relative_to(runtime)
    except ValueError as exc:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context path is not bound to this run.") from exc
    if context_path.name != "REAL_TASK_EXECUTION_CONTEXT.json" or _has_reparse_component(context_path):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context path is unsafe.")
    context = read_json(context_path)
    _validate_context(automation_root, context)
    context_hash = canonical_sha256(context)
    if context_hash != entry.get("executionContextHash") or context_hash != handle.executionContextHash:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context report hash mismatch.")
    if context.get("orchestrationRunId") != handle.orchestrationRunId or context.get("taskId") != handle.taskId:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context run/task mismatch.")
    if context.get("foundationRunId") != handle.foundationRunId or context.get("foundationFinalReportHash") != entry.get("foundationFinalReportHash"):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context foundation mismatch.")
    if context.get("workspaceIdentity", {}).get("workspaceDirectory") != handle.workspaceDirectory:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context workspace mismatch.")
    if context.get("baselineInventoryHash") != entry.get("originalBaselineIdentity"):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context baseline mismatch.")
    size = context_path.stat().st_size
    if size != int(entry.get("executionContextSize", -1)):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context size mismatch.")
    receipt = TrustedReportReceipt(
        relativePath=context_path.relative_to(runtime).as_posix(),
        size=size,
        sha256=context_hash,
        schemaName="real_task_execution_context.schema.json",
        reportVersion=int(context.get("contextVersion", 0)),
    )
    return ResolvedExecutionAuthority(handle=handle, context=context, contextReceipt=receipt, registry=dict(entry))


def validate_execution_handle(handle: RealTaskExecutionHandle, expected_states: tuple[str, ...] | None = None) -> RealTaskExecutionHandle:
    if not isinstance(handle, RealTaskExecutionHandle):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Raw context dicts are not execution authority.")
    if handle.handleVersion != EXECUTION_HANDLE_VERSION or handle.stage != REAL_TASK_EXECUTION_STAGE:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution handle version or stage is invalid.")
    entry = _HANDLE_REGISTRY.get(handle.orchestrationRunId)
    if not entry:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution handle is not bound to this process registry.")
    if entry.get("terminal"):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_REUSED", "Execution handle was already terminal.")
    if entry.get("consumed"):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_REUSED", "Execution handle was already consumed.")
    if entry.get("capabilityDigest") != _capability_digest(handle.capability) or entry.get("handleObjectId") != id(handle):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution handle capability or object identity mismatch.")
    if entry.get("executionContextHash") != handle.executionContextHash:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution context hash mismatch.")
    if entry.get("orchestrationRunId") != handle.orchestrationRunId or entry.get("taskId") != handle.taskId:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution handle run/task binding mismatch.")
    if entry.get("foundationRunId") != handle.foundationRunId or entry.get("workspaceIdentity", {}).get("workspaceDirectory") != handle.workspaceDirectory:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution handle foundation/workspace binding mismatch.")
    if entry.get("ownerThreadId") != threading.get_ident():
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution handle owner mismatch.")
    if expected_states is not None and entry.get("currentState") not in expected_states:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Execution handle state mismatch.")
    return handle


def mark_execution_handle_terminal(handle: RealTaskExecutionHandle) -> None:
    validate_execution_handle(handle)
    entry = _HANDLE_REGISTRY.get(handle.orchestrationRunId)
    if entry is not None:
        entry["terminal"] = True
        entry["consumed"] = True
        entry["currentState"] = "TERMINAL"


def transition_execution_handle(handle: RealTaskExecutionHandle, expected_states: tuple[str, ...], next_state: str) -> None:
    validate_execution_handle(handle, expected_states)
    entry = _HANDLE_REGISTRY[handle.orchestrationRunId]
    if next_state == "DIAGNOSTIC_COMPLETE":
        receipts = entry.get("diagnosticReceipts")
        if not isinstance(receipts, dict) or "1" not in receipts:
            raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt is not registered.")
    entry["currentState"] = next_state


def register_diagnostic_receipt(
    handle: RealTaskExecutionHandle,
    receipt: TrustedReportReceipt,
    report: dict[str, Any],
    attempt_number: int,
) -> None:
    validate_execution_handle(handle, ("DIAGNOSTIC_READY",))
    entry = _HANDLE_REGISTRY[handle.orchestrationRunId]
    key = str(attempt_number)
    receipts = entry.setdefault("diagnosticReceipts", {})
    if not isinstance(receipts, dict) or key in receipts:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt already registered.")
    if report.get("orchestrationRunId") != handle.orchestrationRunId or report.get("taskId") != handle.taskId:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic report run/task mismatch.")
    if report.get("originalBaselineHash") != entry.get("originalBaselineIdentity"):
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic report baseline mismatch.")
    receipts[key] = {
        "attemptNumber": attempt_number,
        "diagnosticRelativePath": receipt.relativePath,
        "trustedCanonicalSha256": receipt.sha256,
        "trustedSize": receipt.size,
        "schemaName": receipt.schemaName,
        "schemaVersion": receipt.reportVersion,
        "reportVersion": int(report.get("reportVersion", 0)),
        "orchestrationRunId": handle.orchestrationRunId,
        "taskId": handle.taskId,
        "executionContextHash": handle.executionContextHash,
        "originalBaselineInventoryHash": entry.get("originalBaselineIdentity"),
        "currentInventoryHash": report.get("diagnosticFinalInventoryHash"),
        "finalDiagnosticVerdict": report.get("finalVerdict"),
        "authorization": report.get("authorization"),
        "registeredAt": utc_now(),
        "consumedForRepairDecision": False,
        "decisionOutcome": None,
    }


def diagnostic_receipt_for_repair_decision(handle: RealTaskExecutionHandle, attempt_number: int) -> tuple[TrustedReportReceipt, dict[str, Any]]:
    validate_execution_handle(handle, ("DIAGNOSTIC_COMPLETE",))
    entry = _HANDLE_REGISTRY[handle.orchestrationRunId]
    receipts = entry.get("diagnosticReceipts")
    if not isinstance(receipts, dict):
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt is missing.")
    record = receipts.get(str(attempt_number))
    if not isinstance(record, dict):
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic attempt receipt is missing.")
    if record.get("consumedForRepairDecision") is True:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt was already consumed.")
    receipt = TrustedReportReceipt(
        relativePath=str(record["diagnosticRelativePath"]),
        size=int(record["trustedSize"]),
        sha256=str(record["trustedCanonicalSha256"]),
        schemaName=str(record["schemaName"]),
        reportVersion=int(record["reportVersion"]),
    )
    return receipt, dict(record)


def consume_diagnostic_receipt(handle: RealTaskExecutionHandle, attempt_number: int, decision_outcome: str) -> None:
    validate_execution_handle(handle, ("DIAGNOSTIC_COMPLETE",))
    record = _HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"][str(attempt_number)]
    if record.get("consumedForRepairDecision") is True:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt was already consumed.")
    record["consumedForRepairDecision"] = True
    record["decisionOutcome"] = decision_outcome


def reserve_role_invocation(handle: RealTaskExecutionHandle, role: str, expected_states: tuple[str, ...]) -> dict[str, Any]:
    validate_execution_handle(handle, expected_states)
    entry = _HANDLE_REGISTRY[handle.orchestrationRunId]
    if entry.get("activeReservation") is not None:
        raise RealTaskExecutionFailure("REAL_TASK_INVOCATION_BUDGET_EXCEEDED", "A role invocation reservation is already active.")
    roles = list(entry.get("invocationRoles", []))
    _validate_role_order(roles, role)
    if int(entry["invocationsReserved"]) >= int(entry["invocationBudgetEffective"]):
        raise RealTaskExecutionFailure("REAL_TASK_INVOCATION_BUDGET_EXCEEDED", "Role invocation budget exhausted.", "BLOCKED")
    if role == "REPAIRER":
        if int(entry["repairsReserved"]) >= int(entry["repairBudgetEffective"]):
            raise RealTaskExecutionFailure("REAL_TASK_REPAIR_EXHAUSTED", "Repair budget exhausted.", "FAIL")
        entry["repairsReserved"] = int(entry["repairsReserved"]) + 1
    sequence = int(entry["invocationsReserved"]) + 1
    reservation = {
        "reservationId": f"reservation_{secrets.token_hex(8)}",
        "invocationId": f"{role.lower()}_{sequence}",
        "sequence": sequence,
        "role": role,
        "stateBefore": entry["currentState"],
        "effectiveBudget": entry["invocationBudgetEffective"],
        "invocationsUsedBefore": entry["invocationsReserved"],
        "reservedAt": utc_now(),
        "completed": False,
    }
    entry["invocationsReserved"] = sequence
    entry["activeReservation"] = reservation
    entry["currentState"] = f"{role}_RESERVED"
    return dict(reservation)


def complete_role_invocation(handle: RealTaskExecutionHandle, reservation: dict[str, Any], terminal_status: str, invocation_id: str) -> None:
    validate_execution_handle(handle)
    entry = _HANDLE_REGISTRY[handle.orchestrationRunId]
    active = entry.get("activeReservation")
    if not isinstance(active, dict) or active.get("reservationId") != reservation.get("reservationId"):
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Role reservation mismatch.")
    if active.get("invocationId") != invocation_id or reservation.get("invocationId") != invocation_id:
        raise RealTaskExecutionFailure("REAL_TASK_ROLE_RESULT_INVALID", "Role invocation id does not match reservation.")
    active["completed"] = True
    active["terminalStatus"] = terminal_status
    active["terminalInvocationId"] = invocation_id
    entry["activeReservation"] = None
    entry["invocationsCompleted"] = int(entry["invocationsCompleted"]) + 1
    roles = list(entry.get("invocationRoles", []))
    roles.append(str(active["role"]))
    entry["invocationRoles"] = roles
    if active["role"] == "REPAIRER":
        entry["repairsCompleted"] = int(entry["repairsCompleted"]) + 1
    entry["currentState"] = f"{active['role']}_{terminal_status}"


def execution_registry_snapshot(handle: RealTaskExecutionHandle) -> dict[str, Any]:
    validate_execution_handle(handle)
    entry = dict(_HANDLE_REGISTRY[handle.orchestrationRunId])
    entry.pop("capabilityDigest", None)
    return entry


def _validate_context(automation_root: Path, payload: dict[str, Any]) -> None:
    errors = _validate_context_errors(automation_root, payload)
    if errors:
        raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_CONTEXT_INVALID", errors[0])


def _validate_context_errors(automation_root: Path, payload: dict[str, Any]) -> list[str]:
    errors = validate(payload, read_json(automation_root / "schemas" / "real_task_execution_context.schema.json"))
    if errors:
        return errors
    semantic_errors: list[str] = []
    if payload.get("stage") != REAL_TASK_EXECUTION_STAGE:
        semantic_errors.append("Execution context stage mismatch.")
    for field in ("noParentWrite", "noAutomaticApply", "noNetwork", "noPackageInstall", "noUnity"):
        if payload.get(field) is not True:
            semantic_errors.append(f"{field} must be true.")
    budget = payload.get("invocationBudget", {})
    repair_budget = payload.get("repairBudget", {})
    if not isinstance(budget.get("maxRoleInvocations"), int) or isinstance(budget.get("maxRoleInvocations"), bool) or budget.get("maxRoleInvocations") < 1 or budget.get("maxRoleInvocations") > 3:
        semantic_errors.append("Invocation budget must be an integer from 1 to 3.")
    if not isinstance(repair_budget.get("maxRepairAttempts"), int) or isinstance(repair_budget.get("maxRepairAttempts"), bool) or repair_budget.get("maxRepairAttempts") < 0 or repair_budget.get("maxRepairAttempts") > 1:
        semantic_errors.append("Repair budget must be an integer from 0 to 1.")
    for path_field in ("runDirectory", "foundationRunDirectory"):
        path = Path(str(payload[path_field])).resolve(strict=False)
        parent_identity = payload.get("parentRepositoryIdentity", {})
        repository_root = Path(str(parent_identity.get("repositoryRoot", automation_root.parent))).resolve(strict=False)
        runtime = (repository_root / "CodexAutomation" / "runtime").resolve(strict=False)
        try:
            path.relative_to(runtime)
        except ValueError as exc:
            semantic_errors.append(f"{path_field} must be under automation runtime.")
            continue
        if _has_reparse_component(path):
            semantic_errors.append(f"{path_field} must not contain reparse components.")
    return semantic_errors


def _has_reparse_component(path: Path) -> bool:
    current = path.anchor
    for part in path.parts[1:]:
        current_path = Path(current) / part
        try:
            if current_path.exists() and current_path.is_symlink():
                return True
            attrs = getattr(os.stat(current_path, follow_symlinks=False), "st_file_attributes", 0)
            if attrs & 0x400:
                return True
        except OSError:
            return False
        current = str(current_path)
    return False


def _head_from_snapshot(snapshot: dict[str, Any]) -> str | None:
    if snapshot.get("status") != "success":
        return None
    return snapshot.get("head")


def _capability_digest(capability: str) -> str:
    return hashlib.sha256(capability.encode("utf-8")).hexdigest()


def _validate_role_order(existing: list[str], role: str) -> None:
    if role not in ROLE_SEQUENCE:
        raise RealTaskExecutionFailure("REAL_TASK_INVOCATION_BUDGET_EXCEEDED", "Unsupported role sequence.")
    if role in existing:
        raise RealTaskExecutionFailure("REAL_TASK_INVOCATION_BUDGET_EXCEEDED", "Duplicate role invocation rejected.")
    if role == "IMPLEMENTER" and existing:
        raise RealTaskExecutionFailure("REAL_TASK_INVOCATION_BUDGET_EXCEEDED", "Implementer must be first.")
    if role == "REPAIRER" and existing != ["IMPLEMENTER"]:
        raise RealTaskExecutionFailure("REAL_TASK_INVOCATION_BUDGET_EXCEEDED", "Repairer must follow implementer.")
    if role == "AUDITOR" and existing not in (["IMPLEMENTER"], ["IMPLEMENTER", "REPAIRER"]):
        raise RealTaskExecutionFailure("REAL_TASK_INVOCATION_BUDGET_EXCEEDED", "Auditor must follow host validation.")
