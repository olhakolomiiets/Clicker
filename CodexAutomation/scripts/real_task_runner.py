from __future__ import annotations

import time
from pathlib import Path
from typing import Any, Callable

from file_utils import read_json
from parent_git_snapshot import parent_git_snapshot, snapshots_equal
from real_task_audit_runner import capture_auditor_integrity, write_full_audit_report
from real_task_bundle import create_result_bundle
from real_task_change_runner import begin_change_tracking, finalize_change_analysis
from real_task_diagnostic_runner import repair_decision, run_diagnostic_pass
from real_task_execution_context import complete_role_invocation, create_execution_context, create_execution_handle, mark_execution_handle_terminal, reserve_role_invocation, resolve_execution_authority, transition_execution_handle, validate_execution_handle
from real_task_execution_models import (
    REAL_TASK_EXECUTION_STAGE,
    ExecutionState,
    RealTaskExecutionFailure,
    RealTaskExecutionResult,
    RoleInvocationResult,
    canonical_sha256,
    new_orchestration_run_id,
    utc_now,
)
from real_task_execution_policy import apply_manifest_role_budget, parse_execution_policy
from real_task_foundation_runner import prepare_foundation_workspace
from real_task_integrity import capture_integrity_snapshot, trust_integrity_pass
from real_task_report_writer import write_trusted_report
from real_task_role_adapter import CodexRealTaskRoleAdapter, RealTaskRoleAdapter
from real_task_role_prompts import build_auditor_prompt, build_implementer_prompt, build_repair_prompt, prompt_hash
from real_task_validation_runner import create_validation_prerequisite_handle, execute_validation_plan
from task_manifest_validator import validate_task_manifest_file


def execute_real_task(validated_manifest_path: str | Path) -> RealTaskExecutionResult:
    root = Path(__file__).resolve().parents[2]
    automation_root = root / "CodexAutomation"
    config = read_json(automation_root / "config.json")
    return _execute_real_task_with_test_adapter(Path(validated_manifest_path), root, automation_root, config, CodexRealTaskRoleAdapter())


def _execute_real_task_with_production_adapter(
    validated_manifest_path: str | Path,
    root: Path,
    automation_root: Path,
    config: dict[str, Any],
    parent_integrity_checkpoint: Callable[[str], None] | None = None,
) -> RealTaskExecutionResult:
    adapter = CodexRealTaskRoleAdapter(root=root, automation_root=automation_root, config=config, allow_real_execution=True)
    return _shared_execute_real_task_core(
        Path(validated_manifest_path),
        root,
        automation_root,
        config,
        adapter,
        allow_public_execution_policy=True,
        parent_integrity_checkpoint=parent_integrity_checkpoint,
    )


def _execute_real_task_with_test_adapter(
    validated_manifest_path: str | Path,
    root: Path,
    automation_root: Path,
    config: dict[str, Any],
    adapter: RealTaskRoleAdapter,
    allow_public_execution_policy: bool = False,
) -> RealTaskExecutionResult:
    return _shared_execute_real_task_core(
        validated_manifest_path,
        root,
        automation_root,
        config,
        adapter,
        allow_public_execution_policy=allow_public_execution_policy,
    )


def _shared_execute_real_task_core(
    validated_manifest_path: str | Path,
    root: Path,
    automation_root: Path,
    config: dict[str, Any],
    adapter: RealTaskRoleAdapter,
    allow_public_execution_policy: bool = False,
    parent_integrity_checkpoint: Callable[[str], None] | None = None,
) -> RealTaskExecutionResult:
    started = time.monotonic()
    state_history = [ExecutionState.PENDING.value]
    run_id = new_orchestration_run_id()
    run_dir = root / "CodexAutomation" / "runtime" / "real_task_runs" / run_id
    final_report_path = run_dir / "FINAL_REAL_TASK_REPORT.json"
    run_dir.mkdir(parents=True, exist_ok=False)
    if isinstance(adapter, CodexRealTaskRoleAdapter):
        if adapter.root is None:
            adapter.root = root
        if adapter.automation_root is None:
            adapter.automation_root = automation_root
        if not adapter.config:
            adapter.config = config
        if adapter.run_dir is None:
            adapter.run_dir = run_dir
    warnings: list[dict[str, Any]] = []
    invocations: list[RoleInvocationResult] = []
    repairs_used = 0
    report_hashes: dict[str, str | None] = {
        "sandboxProbeReportHash": None,
        "implementerInvocationReportHash": None,
        "diagnosticReportHash": None,
        "repairDecisionReportHash": None,
        "repairerInvocationReportHash": None,
        "finalChangeAnalysisReportHash": None,
        "finalValidationReportHash": None,
        "auditorInvocationReportHash": None,
        "auditReportHash": None,
        "resultBundleManifestHash": None,
    }
    context_dict: dict[str, Any] = {}
    foundation_report: dict[str, Any] | None = None
    final_change = None
    final_validation = None
    audit_report: dict[str, Any] | None = None
    bundle_manifest: dict[str, Any] | None = None
    final_verdict = "BLOCKED"
    final_state = ExecutionState.BLOCKED.value
    error_code: str | None = None
    error_message: str | None = None
    parent_initial, parent_final = {}, {}
    try:
        _transition(state_history, ExecutionState.MANIFEST_REVALIDATING)
        manifest_path = Path(validated_manifest_path)
        manifest_result = validate_task_manifest_file(root, automation_root, config, manifest_path, inspect_sources=True, allow_execution_enabled=allow_public_execution_policy)
        if not manifest_result.ok or manifest_result.manifest is None or manifest_result.effectivePlan is None:
            first = manifest_result.errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        execution_policy, policy_errors = parse_execution_policy(config, allow_public_generic_real_task_run_enabled=allow_public_execution_policy)
        if policy_errors or execution_policy is None:
            first = policy_errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        budget, budget_errors = apply_manifest_role_budget(execution_policy, manifest_result.manifest)
        if budget_errors:
            first = budget_errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        parent_initial, git_errors = parent_git_snapshot(root)
        if git_errors:
            first = git_errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        _parent_checkpoint(parent_integrity_checkpoint, "after_lock_manifest_before_foundation")
        _transition(state_history, ExecutionState.FOUNDATION_PREPARING)
        foundation_report = prepare_foundation_workspace(root, automation_root, config, manifest_path)
        if foundation_report.get("finalVerdict") != "PASS":
            raise RealTaskExecutionFailure(foundation_report.get("errorCode") or "REAL_TASK_EXECUTION_CONTEXT_INVALID", foundation_report.get("errorMessage") or "B-A foundation did not PASS.")
        _parent_checkpoint(parent_integrity_checkpoint, "after_foundation_before_sandbox_probe")
        _transition(state_history, ExecutionState.CONTEXT_CREATING)
        context, context_hash = create_execution_context(
            root,
            automation_root,
            run_dir,
            run_id,
            manifest_path,
            str(manifest_result.manifestSha256),
            manifest_result.effectivePlan["effectivePolicy"],
            execution_policy.to_dict(),
            foundation_report,
            budget,
        )
        context_dict = context.to_dict()
        handle = create_execution_handle(context, context_hash)
        validate_execution_handle(handle, ("CONTEXT_READY",))
        change_handle = begin_change_tracking(foundation_report, root, automation_root, config)
        transition_execution_handle(handle, ("CONTEXT_READY",), "BASELINE_READY")
        _transition(state_history, ExecutionState.SANDBOX_PROBING)
        validate_execution_handle(handle, ("BASELINE_READY",))
        context_dict = resolve_execution_authority(handle, automation_root, ("BASELINE_READY",)).context
        probe = adapter.run_sandbox_probe(context_dict)
        report_hashes["sandboxProbeReportHash"] = _write_report(root, automation_root, run_dir / "SANDBOX_PROBE_REPORT.json", probe, "real_task_sandbox_probe.schema.json", execution_policy.maxRoleReportBytes)
        if probe.get("finalVerdict") != "PASS":
            raise RealTaskExecutionFailure("REAL_TASK_SANDBOX_PROBE_FAILED", "Fresh sandbox probe failed.")
        transition_execution_handle(handle, ("BASELINE_READY",), "PROBE_PASS")
        _parent_checkpoint(parent_integrity_checkpoint, "after_sandbox_probe_before_implementer_reservation")
        _transition(state_history, ExecutionState.IMPLEMENTING)
        before_implementer_integrity = capture_integrity_snapshot(handle, root, automation_root, config, "before_implementer", ("PROBE_PASS",))
        if not trust_integrity_pass(before_implementer_integrity):
            raise RealTaskExecutionFailure("REAL_TASK_EXECUTION_HANDLE_INVALID", "Pre-implementer integrity failed.")
        impl_reservation = reserve_role_invocation(handle, "IMPLEMENTER", ("PROBE_PASS",))
        context_dict = resolve_execution_authority(handle, automation_root, ("IMPLEMENTER_RESERVED",)).context
        implementer_prompt = build_implementer_prompt(manifest_result.manifest, context_dict)
        _enforce_prompt_cap(implementer_prompt, execution_policy.maxPromptBytes)
        implementer = adapter.run_implementer(context_dict, implementer_prompt)
        _validate_role_result_identity(implementer, impl_reservation)
        invocations.append(implementer)
        report_hashes["implementerInvocationReportHash"] = _write_role_report(root, automation_root, run_dir, implementer, implementer_prompt, impl_reservation, context_hash, execution_policy.maxRoleReportBytes)
        complete_role_invocation(handle, impl_reservation, _reservation_status(implementer), implementer.invocationId)
        _fail_for_role(implementer, "REAL_TASK_IMPLEMENTER_FAILED")
        post_implementer_integrity = capture_integrity_snapshot(handle, root, automation_root, config, "after_implementer", ("IMPLEMENTER_SUCCESS",))
        if not trust_integrity_pass(post_implementer_integrity):
            raise RealTaskExecutionFailure("REAL_TASK_SERVICE_FILE_INVALID", "Post-implementer trust integrity failed.")
        _transition(state_history, ExecutionState.DIAGNOSTIC_ANALYZING)
        transition_execution_handle(handle, ("IMPLEMENTER_SUCCESS",), "DIAGNOSTIC_READY")
        diagnostic, report_hashes["diagnosticReportHash"] = run_diagnostic_pass(handle, root, automation_root, config, change_handle)
        transition_execution_handle(handle, ("DIAGNOSTIC_READY",), "DIAGNOSTIC_COMPLETE")
        _transition(state_history, ExecutionState.REPAIR_DECIDING)
        decision, report_hashes["repairDecisionReportHash"] = repair_decision(handle, root, automation_root, config, 1, repairs_used, budget["maxRepairAttempts"])
        if decision["shouldRepair"]:
            _transition(state_history, ExecutionState.REPAIRING)
            before_repair = capture_integrity_snapshot(handle, root, automation_root, config, "before_repair", ("DIAGNOSTIC_COMPLETE",))
            _parent_checkpoint(parent_integrity_checkpoint, "before_repairer_reservation")
            repair_reservation = reserve_role_invocation(handle, "REPAIRER", ("DIAGNOSTIC_COMPLETE",))
            context_dict = resolve_execution_authority(handle, automation_root, ("REPAIRER_RESERVED",)).context
            repair_prompt = build_repair_prompt(manifest_result.manifest, context_dict, diagnostic)
            _enforce_prompt_cap(repair_prompt, execution_policy.maxPromptBytes)
            repairer = adapter.run_repairer(context_dict, repair_prompt)
            _validate_role_result_identity(repairer, repair_reservation)
            repairs_used += 1
            invocations.append(repairer)
            report_hashes["repairerInvocationReportHash"] = _write_role_report(root, automation_root, run_dir, repairer, repair_prompt, repair_reservation, context_hash, execution_policy.maxRoleReportBytes)
            complete_role_invocation(handle, repair_reservation, _reservation_status(repairer), repairer.invocationId)
            _fail_for_role(repairer, "REAL_TASK_ROLE_EXECUTION_FAILED")
            after_repair = capture_integrity_snapshot(handle, root, automation_root, config, "after_repair", ("REPAIRER_SUCCESS",))
            if not trust_integrity_pass(after_repair):
                raise RealTaskExecutionFailure("REAL_TASK_SERVICE_FILE_INVALID", "Post-repair trust integrity failed.")
            if before_repair["workspaceContentHash"] == after_repair["workspaceContentHash"]:
                raise RealTaskExecutionFailure("REAL_TASK_REPAIR_NO_PROGRESS", "Repairer made no workspace progress.", "FAIL")
        elif decision.get("diagnosticVerdict") != "PASS":
            verdict = "BLOCKED" if decision.get("decision") == "BLOCKED" else "FAIL"
            raise RealTaskExecutionFailure(decision.get("errorCode") or "REAL_TASK_REPAIR_EXHAUSTED", "Diagnostic failed and no repair is available.", verdict)
        _transition(state_history, ExecutionState.FINAL_CHANGE_ANALYZING)
        transition_execution_handle(handle, ("DIAGNOSTIC_COMPLETE", "REPAIRER_SUCCESS"), "FINAL_BB_READY")
        final_change = finalize_change_analysis(change_handle, root, automation_root, config)
        report_hashes["finalChangeAnalysisReportHash"] = canonical_sha256(final_change.report)
        if final_change.finalVerdict != "PASS":
            raise RealTaskExecutionFailure("REAL_TASK_FINAL_VALIDATION_FAILED", "Final B-B change analysis did not PASS.", final_change.finalVerdict)
        transition_execution_handle(handle, ("FINAL_BB_READY",), "FINAL_BB_PASS")
        _transition(state_history, ExecutionState.FINAL_VALIDATING)
        validation_handle = create_validation_prerequisite_handle(final_change, root, config)
        final_validation = execute_validation_plan(validation_handle, root, automation_root, config)
        report_hashes["finalValidationReportHash"] = canonical_sha256(final_validation.report)
        if final_validation.finalVerdict != "PASS":
            raise RealTaskExecutionFailure("REAL_TASK_FINAL_VALIDATION_FAILED", "Final B-C validation did not PASS.", final_validation.finalVerdict)
        transition_execution_handle(handle, ("FINAL_BB_PASS",), "FINAL_BC_PASS")
        _transition(state_history, ExecutionState.AUDITING)
        before_audit = capture_auditor_integrity(handle, root, automation_root, config, "before_auditor", ("FINAL_BC_PASS",))
        _parent_checkpoint(parent_integrity_checkpoint, "before_auditor_reservation")
        auditor_reservation = reserve_role_invocation(handle, "AUDITOR", ("FINAL_BC_PASS",))
        context_dict = resolve_execution_authority(handle, automation_root, ("AUDITOR_RESERVED",)).context
        auditor_prompt = build_auditor_prompt(manifest_result.manifest, context_dict, final_validation.report)
        _enforce_prompt_cap(auditor_prompt, execution_policy.maxPromptBytes)
        auditor = adapter.run_auditor(context_dict, auditor_prompt)
        _validate_role_result_identity(auditor, auditor_reservation)
        invocations.append(auditor)
        report_hashes["auditorInvocationReportHash"] = _write_role_report(root, automation_root, run_dir, auditor, auditor_prompt, auditor_reservation, context_hash, execution_policy.maxRoleReportBytes)
        complete_role_invocation(handle, auditor_reservation, _reservation_status(auditor), auditor.invocationId)
        after_audit = capture_auditor_integrity(handle, root, automation_root, config, "after_auditor", ("AUDITOR_SUCCESS", "AUDITOR_FAIL"))
        audit_report, report_hashes["auditReportHash"] = write_full_audit_report(handle, root, automation_root, config, auditor, before_audit, after_audit)
        _parent_checkpoint(parent_integrity_checkpoint, "after_auditor")
        if not audit_report["approved"]:
            raise RealTaskExecutionFailure(audit_report["errorCode"] or "REAL_TASK_AUDIT_REJECTED", audit_report["errorMessage"] or "Audit rejected.", "FAIL" if audit_report["errorCode"] == "REAL_TASK_AUDIT_REJECTED" else "BLOCKED")
        transition_execution_handle(handle, ("AUDITOR_SUCCESS",), "AUDIT_APPROVED")
        _parent_checkpoint(parent_integrity_checkpoint, "before_bundle_finalization")
        _transition(state_history, ExecutionState.BUNDLING)
        bundle_manifest, report_hashes["resultBundleManifestHash"] = create_result_bundle(handle, root, automation_root, final_change.report, final_validation.report, audit_report, execution_policy.to_dict())
        transition_execution_handle(handle, ("AUDIT_APPROVED",), "BUNDLE_COMPLETE")
        final_verdict = derive_final_real_task_verdict(context_dict, final_change.report, final_validation.report, audit_report, bundle_manifest, parent_git_changed=False, error_code=None)
        _parent_checkpoint(parent_integrity_checkpoint, "before_public_terminal_pass")
        final_state = ExecutionState.COMPLETED.value
    except RealTaskExecutionFailure as exc:
        error_code = exc.code
        error_message = exc.message
        final_verdict = exc.verdict
        final_state = ExecutionState.RATE_LIMITED.value if exc.code == "REAL_TASK_RATE_LIMITED" else (ExecutionState.BLOCKED.value if exc.verdict == "BLOCKED" else ExecutionState.FAILED.value)
    except Exception as exc:
        error_code = "REAL_TASK_FINAL_REPORT_INVALID"
        error_message = f"{type(exc).__name__}: {exc}"
        final_verdict = "BLOCKED"
        final_state = ExecutionState.BLOCKED.value
    try:
        parent_final, git_errors = parent_git_snapshot(root)
        if git_errors:
            parent_final = {}
    except Exception:
        parent_final = {}
    parent_git_changed = bool(parent_initial and parent_final and not snapshots_equal(parent_initial, parent_final))
    if final_verdict == "PASS" and parent_git_changed:
        final_verdict = "BLOCKED"
        final_state = ExecutionState.BLOCKED.value
        error_code = error_code or "REAL_TASK_PARENT_GIT_CHANGED"
        error_message = error_message or "Parent Git changed before final report."
    if state_history[-1] != final_state:
        _transition(state_history, ExecutionState.REPORTING)
        _transition(state_history, ExecutionState(final_state))
    final_report = _final_report(
        run_id,
        context_dict,
        foundation_report,
        report_hashes,
        adapter,
        state_history,
        invocations,
        repairs_used,
        parent_git_changed,
        final_change.report if final_change else None,
        final_validation.report if final_validation else None,
        audit_report,
        bundle_manifest,
        round(time.monotonic() - started, 3),
        final_state,
        final_verdict,
        error_code,
        error_message,
        warnings,
    )
    _write_report(root, automation_root, final_report_path, final_report, "real_task_final.schema.json", int(config.get("realTaskExecutionPolicy", {}).get("maxDiagnosticReportBytes", 4_194_304)))
    if context_dict:
        try:
            mark_execution_handle_terminal(handle)
        except Exception:
            pass
    return RealTaskExecutionResult(final_verdict, final_state, final_report, str(final_report_path), str(run_dir / "result_bundle" / "RESULT_MANIFEST.json") if bundle_manifest else None)


def _parent_checkpoint(parent_integrity_checkpoint: Callable[[str], None] | None, checkpoint: str) -> None:
    if parent_integrity_checkpoint is not None:
        parent_integrity_checkpoint(checkpoint)


def _transition(history: list[str], state: ExecutionState) -> None:
    history.append(state.value)


def _write_report(root: Path, automation_root: Path, path: Path, report: dict[str, Any], schema_name: str, max_bytes: int) -> str:
    write_trusted_report(automation_root, root / "CodexAutomation" / "runtime", path, report, schema_name, _semantic_validator_for(schema_name), max_bytes)
    return canonical_sha256(read_json(path))


def _write_role_report(root: Path, automation_root: Path, run_dir: Path, result: RoleInvocationResult, prompt: str, reservation: dict[str, Any], context_hash: str, max_bytes: int) -> str:
    report = {
        "reportVersion": 1,
        "role": reservation["role"],
        "invocationId": reservation["invocationId"],
        "reservationId": reservation["reservationId"],
        "sequence": reservation["sequence"],
        "executionContextHash": context_hash,
        "status": result.status,
        "verdict": result.verdict,
        "promptHash": prompt_hash(prompt),
        "result": result.report,
        "errorCode": result.errorCode,
        "errorMessage": result.errorMessage,
    }
    return _write_report(root, automation_root, run_dir / f"{result.role.lower()}_INVOCATION_REPORT.json", report, "real_task_role_invocation.schema.json", max_bytes)


def _fail_for_role(result: RoleInvocationResult, default_code: str) -> None:
    if result.status == "RATE_LIMITED":
        raise RealTaskExecutionFailure("REAL_TASK_RATE_LIMITED", "Role invocation was rate limited.", "RATE_LIMITED")
    if result.status == "TIMEOUT":
        raise RealTaskExecutionFailure("REAL_TASK_TIMEOUT", "Role invocation timed out.")
    if result.status != "SUCCESS":
        raise RealTaskExecutionFailure(result.errorCode or default_code, result.errorMessage or "Role invocation failed.")
    if result.verdict not in {"PASS", "FAIL"}:
        raise RealTaskExecutionFailure(result.errorCode or "REAL_TASK_ROLE_RESULT_INVALID", "Role result verdict is invalid.")


def _validate_role_result_identity(result: RoleInvocationResult, reservation: dict[str, Any]) -> None:
    if result.role != reservation.get("role") or result.invocationId != reservation.get("invocationId"):
        raise RealTaskExecutionFailure("REAL_TASK_ROLE_RESULT_INVALID", "Role result identity does not match reserved invocation.")


def _reservation_status(result: RoleInvocationResult) -> str:
    if result.status == "SUCCESS":
        return "SUCCESS"
    if result.status == "RATE_LIMITED":
        return "RATE_LIMITED"
    if result.status == "TIMEOUT":
        return "TIMEOUT"
    return "FAILED"


def _enforce_prompt_cap(prompt: str, cap: int) -> None:
    if len(prompt.encode("utf-8")) > cap:
        raise RealTaskExecutionFailure("REAL_TASK_ROLE_RESULT_INVALID", "Role prompt exceeds host cap.")


def _final_report(
    run_id: str,
    context: dict[str, Any],
    foundation_report: dict[str, Any] | None,
    report_hashes: dict[str, str | None],
    adapter: RealTaskRoleAdapter,
    state_history: list[str],
    invocations: list[RoleInvocationResult],
    repairs_used: int,
    parent_git_changed: bool,
    final_change: dict[str, Any] | None,
    final_validation: dict[str, Any] | None,
    audit_report: dict[str, Any] | None,
    bundle_manifest: dict[str, Any] | None,
    duration: float,
    final_state: str,
    final_verdict: str,
    error_code: str | None,
    error_message: str | None,
    warnings: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "reportVersion": 1,
        "orchestrationRunId": run_id,
        "taskId": context.get("taskId", "unknown"),
        "stage": REAL_TASK_EXECUTION_STAGE,
        "stateHistory": state_history,
        "executionContextHash": canonical_sha256(context) if context else None,
        "manifestSha256": context.get("manifestSha256"),
        "effectivePolicyHash": context.get("effectivePolicyHash"),
        "foundationRunId": context.get("foundationRunId") or (foundation_report or {}).get("runId"),
        "foundationFinalReportHash": context.get("foundationFinalReportHash"),
        **report_hashes,
        "invocationBudget": context.get("invocationBudget", {"maxRoleInvocations": 3, "invocationsUsed": len(invocations)}) | {"invocationsUsed": len(invocations)},
        "invocationsUsed": len(invocations),
        "repairBudget": context.get("repairBudget", {"maxRepairAttempts": 1, "repairsUsed": repairs_used}) | {"repairsUsed": repairs_used},
        "repairsUsed": repairs_used,
        "implementerCompleted": any(item.role == "IMPLEMENTER" and item.status == "SUCCESS" for item in invocations),
        "diagnosticVerdict": "PASS" if report_hashes.get("diagnosticReportHash") and repairs_used == 0 and not error_code else None,
        "repairAttempted": repairs_used > 0,
        "finalHostValidationPassed": bool(final_change and final_validation and final_change.get("finalVerdict") == "PASS" and final_validation.get("finalVerdict") == "PASS"),
        "auditorRan": any(item.role == "AUDITOR" for item in invocations),
        "auditorApproved": bool(audit_report and audit_report.get("approved") is True),
        "workspaceUnchangedAfterAuditor": None if audit_report is None else audit_report.get("workspaceUnchangedAfterAuditor"),
        "parentGitChanged": parent_git_changed,
        "parentSourceChanged": None if final_change is None else final_change.get("parentSourceChanged"),
        "serviceFilesValid": None if final_change is None else final_change.get("serviceFilesValid"),
        "agentsDirectoryValid": None if final_validation is None else final_validation.get("agentsDirectoryValid"),
        "isolatedGitValid": None if final_change is None else final_change.get("isolatedGitValid"),
        "bundleIntegrityValid": bundle_manifest is not None and bundle_manifest.get("complete") is True,
        "codexInvocationCount": int(getattr(adapter, "invocation_count", 0) or 0),
        "modelInvocationStarted": bool(getattr(adapter, "allow_real_execution", False) and int(getattr(adapter, "invocation_count", 0) or 0) > 0),
        "sandboxStarted": bool(getattr(adapter, "allow_real_execution", False) and report_hashes.get("sandboxProbeReportHash")),
        "unityStarted": False,
        "networkUsed": False,
        "packageInstallUsed": False,
        "automaticRetryUsed": False,
        "modelDowngradeUsed": False,
        "creditUsageTriggered": False,
        "durationSeconds": duration,
        "finalState": final_state,
        "finalVerdict": final_verdict,
        "errorCode": error_code,
        "errorMessage": error_message,
        "warnings": warnings,
    }


def derive_final_real_task_verdict(
    context: dict[str, Any],
    final_change: dict[str, Any] | None,
    final_validation: dict[str, Any] | None,
    audit_report: dict[str, Any] | None,
    bundle_manifest: dict[str, Any] | None,
    parent_git_changed: bool,
    error_code: str | None,
) -> str:
    if error_code and error_code != "REAL_TASK_RATE_LIMITED":
        return "BLOCKED" if "BLOCKED" in error_code or "INVALID" in error_code or "AUDITOR_CHANGED" in error_code or "SERVICE" in error_code or "GIT" in error_code else "FAIL"
    if error_code == "REAL_TASK_RATE_LIMITED":
        return "RATE_LIMITED"
    if parent_git_changed:
        return "BLOCKED"
    if not final_change or final_change.get("finalVerdict") != "PASS":
        return "FAIL" if final_change and final_change.get("finalVerdict") == "FAIL" else "BLOCKED"
    if not final_validation or final_validation.get("finalVerdict") != "PASS":
        return "FAIL" if final_validation and final_validation.get("finalVerdict") == "FAIL" else "BLOCKED"
    if not audit_report or audit_report.get("approved") is not True or audit_report.get("workspaceUnchangedAfterAuditor") is not True:
        return "FAIL" if audit_report and audit_report.get("errorCode") == "REAL_TASK_AUDIT_REJECTED" else "BLOCKED"
    if not bundle_manifest or bundle_manifest.get("complete") is not True or bundle_manifest.get("eligibleForApply") is not False:
        return "BLOCKED"
    return "PASS"


def _semantic_validator_for(schema_name: str):
    if schema_name == "real_task_final.schema.json":
        return _validate_final_report
    if schema_name == "real_task_role_invocation.schema.json":
        return _validate_role_invocation_report
    return None


def _validate_role_invocation_report(report: dict[str, Any]) -> list[str]:
    if report.get("role") != report.get("result", {}).get("role"):
        return ["Role invocation result role mismatch."]
    if report.get("invocationId") == "":
        return ["Role invocation id must be non-empty."]
    return []


def _validate_final_report(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("stateHistory") and report.get("stateHistory")[-1] != report.get("finalState"):
        errors.append("Final state must match terminal state history entry.")
    mapping = {"PASS": "COMPLETED", "FAIL": "FAILED", "BLOCKED": "BLOCKED", "RATE_LIMITED": "RATE_LIMITED"}
    if report.get("finalVerdict") in mapping and report.get("finalState") != mapping[report["finalVerdict"]]:
        errors.append("Final verdict/state mapping is inconsistent.")
    if report.get("finalVerdict") == "PASS":
        required_true = ("implementerCompleted", "finalHostValidationPassed", "auditorRan", "auditorApproved", "workspaceUnchangedAfterAuditor", "bundleIntegrityValid")
        for field in required_true:
            if report.get(field) is not True:
                errors.append(f"PASS requires {field}=true.")
        if report.get("parentGitChanged") is not False:
            errors.append("PASS requires parentGitChanged=false.")
        if report.get("errorCode") is not None:
            errors.append("PASS requires null errorCode.")
    return errors
