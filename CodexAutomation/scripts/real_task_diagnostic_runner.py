from __future__ import annotations

import time
from dataclasses import replace
from pathlib import Path
from typing import Any

from file_utils import read_json
from real_task_change_runner import _parent_source_changed
from real_task_execution_context import consume_diagnostic_receipt, diagnostic_receipt_for_repair_decision, register_diagnostic_receipt, resolve_execution_authority
from real_task_execution_models import RealTaskExecutionFailure, RealTaskExecutionHandle, canonical_sha256
from real_task_report_writer import read_trusted_report, write_trusted_report
from real_task_validation_runner import _readable_scopes, _validated_plan, parse_validation_policy
from task_manifest_validator import parse_real_task_policy
from validator_models import ValidatorContext, ValidatorStatus
from validator_registry import load_validator_registry
from validator_schema_registry import load_schema_registry
from workspace_change_inventory import build_change_inventory
from workspace_change_policy import evaluate_change_policy, parse_change_policy
from workspace_diff import build_workspace_diff
from real_task_workspace import git_fingerprint, validate_service_files
from parent_git_snapshot import parent_git_snapshot, snapshots_equal


REPAIRABLE_DIAGNOSTIC_CODES = {
    "VALIDATION_FAILED",
    "TEXT_CONTAINS_MISSING",
    "FILE_MISSING",
    "EXPECTED_OUTPUT_MISSING",
    "CHANGED_PATHS_EXACT_MISMATCH",
    "NO_CONFLICT_MARKERS_FAILED",
    "MAX_FILE_SIZE_EXCEEDED",
    "EXTENSION_ALLOWLIST_FAILED",
}


def run_diagnostic_pass(handle: RealTaskExecutionHandle, root: Path, automation_root: Path, config: dict[str, Any], change_handle: Any, attempt_number: int = 1) -> tuple[dict[str, Any], str]:
    authority = resolve_execution_authority(handle, automation_root, ("DIAGNOSTIC_READY",))
    context = authority.context
    started = time.monotonic()
    workspace = Path(context["workspaceIdentity"]["workspaceDirectory"])
    task = read_json(workspace / "task.json")
    run_dir = Path(handle.runDirectory)
    foundation_dir = Path(context["foundationRunDirectory"])
    trusted_context = read_json(foundation_dir / "TRUSTED_RUN_CONTEXT.json")
    baseline = read_json(Path(change_handle.baselineInventoryPath))
    service_registry = list(baseline.get("serviceRegistry", []))
    real_policy, real_errors = parse_real_task_policy(config)
    change_policy, change_errors = parse_change_policy(config, real_policy)
    validation_policy, validation_errors = parse_validation_policy(config)
    errors = real_errors + change_errors + validation_errors
    if errors or change_policy is None or validation_policy is None:
        first = errors[0]
        raise RealTaskExecutionFailure(first.code, first.message)
    final_inventory = build_change_inventory(
        workspace,
        str(trusted_context["runId"]),
        str(trusted_context["taskId"]),
        str(context["trustedContextHash"]),
        "diagnostic_final",
        service_registry,
        change_policy,
        int(config["realTaskFoundation"]["maxInventoryEntries"]),
    )
    service_valid, _ = validate_service_files(workspace, service_registry, int(config["realTaskFoundation"]["copyChunkBytes"]))
    isolated_git_valid = canonical_sha256(git_fingerprint(workspace)) == change_handle.isolatedGitFingerprintHash
    parent_final, git_errors = parent_git_snapshot(root)
    if git_errors:
        first = git_errors[0]
        raise RealTaskExecutionFailure(first.code, first.message)
    parent_git_changed = not snapshots_equal(change_handle.parentGitTrackingStartSnapshot, parent_final)
    source_schema = read_json(automation_root / "schemas" / "real_task_source_inventory.schema.json")
    parent_source_changed = _parent_source_changed(
        root,
        foundation_dir,
        trusted_context,
        change_handle.sourcePostInventoryHash,
        int(config["realTaskFoundation"]["maxInventoryEntries"]),
        int(config["realTaskFoundation"]["copyChunkBytes"]),
        source_schema,
    )
    diff = build_workspace_diff(baseline, final_inventory)
    policy_context = dict(trusted_context)
    policy_context["expectedOutputs"] = task.get("expectedOutputs", [])
    policy_report = evaluate_change_policy(baseline, final_inventory, diff, policy_context, change_policy, parent_git_changed, parent_source_changed, service_valid and isolated_git_valid)
    registry, _registry_snapshot = load_validator_registry(validation_policy)
    schema_registry = load_schema_registry(root, automation_root, validation_policy)
    plan = _validated_plan(root, automation_root, config, trusted_context, task, validation_policy, baseline, final_inventory, diff)
    validator_context = ValidatorContext(
        repositoryRoot=root,
        automationRoot=automation_root,
        runDirectory=foundation_dir,
        workspaceDirectory=workspace,
        task=task,
        effectivePolicy=trusted_context["effectiveHostPolicy"],
        validationPolicy=validation_policy,
        baselineInventory=baseline,
        finalInventory=final_inventory,
        diffReport=diff,
        policyReport=policy_report,
        finalChangeReport={"finalVerdict": policy_report["finalPolicyVerdict"], "finalState": "DIAGNOSTIC"},
        serviceRegistry=tuple(str(item["workspacePath"]) for item in service_registry if item.get("workspacePath")),
        readableScopes=_readable_scopes(task, diff, baseline, final_inventory),
        schemaRegistry=schema_registry,
    )
    validator_results: list[dict[str, Any]] = []
    for index, entry in enumerate(plan):
        definition = registry[entry["type"]]
        try:
            outcome = definition.implementation(replace(validator_context, validatorIndex=index), entry)
            validator_results.append(outcome.to_dict())
        except Exception as exc:
            validator_results.append(
                {
                    "validatorId": entry.get("id", f"validator_{index}"),
                    "type": entry.get("type", "unknown"),
                    "status": ValidatorStatus.BLOCKED.value,
                    "code": "REAL_TASK_DIAGNOSTIC_VALIDATOR_ERROR",
                    "message": f"{type(exc).__name__}: {exc}",
                    "primaryPath": None,
                    "details": {},
                }
            )
    blocked = [item for item in validator_results if item["status"] == ValidatorStatus.BLOCKED.value]
    failed = [item for item in validator_results if item["status"] == ValidatorStatus.FAIL.value]
    findings: list[dict[str, Any]] = []
    for item in policy_report.get("findings", []):
        findings.append({"code": str(item.get("code")), "path": item.get("path"), "message": str(item.get("message"))})
    for item in failed + blocked:
        findings.append({"code": str(item.get("code")), "path": item.get("primaryPath"), "message": str(item.get("message"))})
    integrity_passed = service_valid and isolated_git_valid and not parent_git_changed and not parent_source_changed
    blocked_count = len(blocked) + (1 if policy_report["finalPolicyVerdict"] == "BLOCKED" else 0) + (0 if integrity_passed else 1)
    fail_count = len(failed) + (1 if policy_report["finalPolicyVerdict"] == "FAIL" else 0)
    pass_count = len(validator_results) - len(blocked) - len(failed)
    repairable_count = sum(1 for item in findings if _is_repairable_finding(str(item.get("code"))))
    nonrepairable_count = len(findings) - repairable_count
    final_verdict = "BLOCKED" if blocked_count > 0 else ("FAIL" if fail_count > 0 else "PASS")
    repair_eligible = final_verdict == "FAIL" and blocked_count == 0 and integrity_passed and repairable_count > 0 and nonrepairable_count == 0
    eligibility_code = "REPAIR_ELIGIBLE" if repair_eligible else ("DIAGNOSTIC_PASS" if final_verdict == "PASS" else ("DIAGNOSTIC_BLOCKED" if final_verdict == "BLOCKED" else "NON_REPAIRABLE_FINDINGS"))
    report = {
        "reportVersion": 1,
        "orchestrationRunId": context["orchestrationRunId"],
        "taskId": context["taskId"],
        "stage": "BOOTSTRAP-03B-2C",
        "authorization": False,
        "attemptNumber": attempt_number,
        "executionContextHash": handle.executionContextHash,
        "originalBaselineHash": context["baselineInventoryHash"],
        "changeBaselineInventoryHash": baseline["inventorySha256"],
        "diagnosticFinalInventoryHash": final_inventory["inventorySha256"],
        "diagnosticDiffHash": canonical_sha256(diff),
        "diagnosticPolicyHash": canonical_sha256(policy_report),
        "validatorResultHash": canonical_sha256(validator_results),
        "changePolicyVerdict": policy_report["finalPolicyVerdict"],
        "validationVerdict": "BLOCKED" if blocked else ("FAIL" if failed else "PASS"),
        "passCount": pass_count,
        "failCount": fail_count,
        "blockedCount": blocked_count,
        "integrityPassed": integrity_passed,
        "repairableFindingCount": repairable_count,
        "nonRepairableFindingCount": nonrepairable_count,
        "repairEligible": repair_eligible,
        "repairEligibilityCode": eligibility_code,
        "findingsHash": canonical_sha256(findings),
        "parentGitChanged": parent_git_changed,
        "parentSourceChanged": parent_source_changed,
        "serviceFilesValid": service_valid,
        "isolatedGitValid": isolated_git_valid,
        "finalVerdict": final_verdict,
        "findings": findings,
        "errorCode": None,
        "errorMessage": None,
        "durationSeconds": round(time.monotonic() - started, 3),
    }
    report["diagnosticReportHash"] = canonical_sha256({key: value for key, value in report.items() if key != "diagnosticReportHash"})
    path = _diagnostic_report_path(handle, attempt_number)
    receipt = write_trusted_report(automation_root, root / "CodexAutomation" / "runtime", path, report, "real_task_diagnostic_validation.schema.json", _validate_diagnostic_report, int(config["realTaskExecutionPolicy"]["maxDiagnosticReportBytes"]))
    register_diagnostic_receipt(handle, receipt, report, attempt_number)
    return report, canonical_sha256(report)


def repair_decision(handle: RealTaskExecutionHandle, root: Path, automation_root: Path, config: dict[str, Any], attempt_number: int, repairs_used: int, max_repairs: int) -> tuple[dict[str, Any], str]:
    authority = resolve_execution_authority(handle, automation_root, ("DIAGNOSTIC_COMPLETE",))
    context = authority.context
    try:
        receipt, receipt_record = diagnostic_receipt_for_repair_decision(handle, attempt_number)
        path = _diagnostic_report_path(handle, attempt_number)
        if receipt.relativePath != path.relative_to(root / "CodexAutomation" / "runtime").as_posix():
            raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt path mismatch.")
        diagnostic = read_trusted_report(automation_root, root / "CodexAutomation" / "runtime", path, receipt, "real_task_diagnostic_validation.schema.json", _validate_diagnostic_report, int(config["realTaskExecutionPolicy"]["maxDiagnosticReportBytes"]))
        _validate_diagnostic_binding(handle, context, diagnostic, receipt_record, attempt_number)
    except RealTaskExecutionFailure as exc:
        if exc.code == "REAL_TASK_DIAGNOSTIC_FAILED":
            raise
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", exc.message) from exc
    should_repair = (
        diagnostic.get("finalVerdict") == "FAIL"
        and diagnostic.get("blockedCount") == 0
        and diagnostic.get("integrityPassed") is True
        and diagnostic.get("repairEligible") is True
        and diagnostic.get("repairableFindingCount", 0) > 0
        and diagnostic.get("nonRepairableFindingCount") == 0
        and repairs_used < max_repairs
    )
    if should_repair:
        decision_name = "RUN_REPAIR"
        error_code = None
    elif diagnostic.get("finalVerdict") == "PASS":
        decision_name = "NOT_NEEDED"
        error_code = None
    elif diagnostic.get("finalVerdict") == "BLOCKED":
        decision_name = "BLOCKED"
        error_code = "REAL_TASK_DIAGNOSTIC_FAILED"
    elif diagnostic.get("repairEligible") is False:
        decision_name = "CANNOT_REPAIR"
        error_code = "REAL_TASK_REPAIR_EXHAUSTED"
    else:
        decision_name = "CANNOT_REPAIR"
        error_code = "REAL_TASK_REPAIR_EXHAUSTED"
    consume_diagnostic_receipt(handle, attempt_number, decision_name)
    report = {
        "reportVersion": 1,
        "orchestrationRunId": context["orchestrationRunId"],
        "taskId": context["taskId"],
        "stage": "BOOTSTRAP-03B-2C",
        "diagnosticVerdict": diagnostic.get("finalVerdict"),
        "repairAllowed": max_repairs > 0,
        "repairsUsed": repairs_used,
        "maxRepairAttempts": max_repairs,
        "decision": decision_name,
        "shouldRepair": should_repair,
        "authorization": False,
        "diagnosticReportHash": diagnostic["diagnosticReportHash"],
        "diagnosticPayloadHash": diagnostic["diagnosticReportHash"],
        "diagnosticArtifactHash": receipt.sha256,
        "diagnosticArtifactSize": receipt.size,
        "diagnosticAttemptNumber": attempt_number,
        "diagnosticReportRelativePath": receipt.relativePath,
        "diagnosticReceiptConsumed": True,
        "executionContextHash": handle.executionContextHash,
        "originalBaselineInventoryHash": context["baselineInventoryHash"],
        "blockedCount": diagnostic.get("blockedCount"),
        "repairableFindingCount": diagnostic.get("repairableFindingCount"),
        "nonRepairableFindingCount": diagnostic.get("nonRepairableFindingCount"),
        "repairEligible": diagnostic.get("repairEligible"),
        "errorCode": error_code,
        "errorMessage": None,
    }
    path = Path(handle.runDirectory) / "REPAIR_DECISION_REPORT.json"
    write_trusted_report(automation_root, root / "CodexAutomation" / "runtime", path, report, "real_task_repair_decision.schema.json", _validate_repair_decision, int(config["realTaskExecutionPolicy"]["maxDiagnosticReportBytes"]))
    return report, canonical_sha256(report)


def _validate_diagnostic_report(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("authorization") is not False:
        errors.append("Diagnostic report must be non-authorizing.")
    if report.get("finalVerdict") not in {"PASS", "FAIL", "BLOCKED"}:
        errors.append("Diagnostic finalVerdict is invalid.")
    if report.get("finalVerdict") == "PASS" and report.get("findings"):
        errors.append("PASS diagnostic cannot include findings.")
    if report.get("finalVerdict") == "PASS" and (report.get("failCount") != 0 or report.get("blockedCount") != 0 or report.get("repairEligible") is not False):
        errors.append("PASS diagnostic has inconsistent counts or repair eligibility.")
    if report.get("finalVerdict") == "FAIL" and (report.get("failCount", 0) <= 0 or report.get("blockedCount") != 0 or report.get("integrityPassed") is not True):
        errors.append("FAIL diagnostic has inconsistent counts or integrity.")
    if report.get("finalVerdict") == "BLOCKED" and report.get("repairEligible") is not False:
        errors.append("BLOCKED diagnostic cannot be repair eligible.")
    if report.get("repairEligible") is True and (report.get("finalVerdict") != "FAIL" or report.get("repairableFindingCount", 0) <= 0 or report.get("nonRepairableFindingCount") != 0):
        errors.append("repairEligible diagnostic is inconsistent.")
    expected_hash = canonical_sha256({key: value for key, value in report.items() if key != "diagnosticReportHash"})
    if report.get("diagnosticReportHash") != expected_hash:
        errors.append("Diagnostic report hash mismatch.")
    return errors


def _validate_repair_decision(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("authorization") is not False:
        errors.append("Repair decision must be non-authorizing.")
    if report.get("shouldRepair") is True and report.get("diagnosticVerdict") != "FAIL":
        errors.append("Repair requires diagnostic FAIL.")
    if report.get("shouldRepair") is True and (report.get("blockedCount") != 0 or report.get("repairableFindingCount", 0) <= 0):
        errors.append("Repair decision requires repairable unblocked diagnostic findings.")
    if report.get("shouldRepair") is True and report.get("diagnosticReceiptConsumed") is not True:
        errors.append("Repair requires consumed trusted diagnostic receipt.")
    if report.get("shouldRepair") is True and (report.get("repairEligible") is not True or report.get("nonRepairableFindingCount") != 0):
        errors.append("Repair requires trusted repair eligibility.")
    if report.get("decision") == "RUN_REPAIR" and report.get("shouldRepair") is not True:
        errors.append("RUN_REPAIR must set shouldRepair=true.")
    if report.get("decision") == "BLOCKED" and report.get("errorCode") != "REAL_TASK_DIAGNOSTIC_FAILED":
        errors.append("Blocked diagnostic decision must carry diagnostic failure code.")
    if report.get("diagnosticReportHash") != report.get("diagnosticPayloadHash"):
        errors.append("Diagnostic report hash must match diagnostic payload hash.")
    if report.get("diagnosticPayloadHash") == report.get("diagnosticArtifactHash"):
        errors.append("Diagnostic payload and artifact hashes must have distinct semantics.")
    return errors


def _diagnostic_report_path(handle: RealTaskExecutionHandle, attempt_number: int) -> Path:
    if attempt_number < 1:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic attempt number is invalid.")
    return Path(handle.runDirectory) / f"DIAGNOSTIC_VALIDATION_ATTEMPT_{attempt_number:03d}.json"


def _validate_diagnostic_binding(
    handle: RealTaskExecutionHandle,
    context: dict[str, Any],
    diagnostic: dict[str, Any],
    receipt_record: dict[str, Any],
    attempt_number: int,
) -> None:
    if diagnostic.get("orchestrationRunId") != handle.orchestrationRunId or diagnostic.get("taskId") != handle.taskId:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic report run/task mismatch.")
    if diagnostic.get("executionContextHash") != handle.executionContextHash:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic report context mismatch.")
    if diagnostic.get("originalBaselineHash") != context.get("baselineInventoryHash"):
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic report baseline mismatch.")
    if diagnostic.get("attemptNumber") != attempt_number:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic report attempt mismatch.")
    if diagnostic.get("authorization") is not False or receipt_record.get("authorization") is not False:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic report cannot authorize repair directly.")
    if receipt_record.get("orchestrationRunId") != handle.orchestrationRunId or receipt_record.get("taskId") != handle.taskId:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt run/task mismatch.")
    if receipt_record.get("executionContextHash") != handle.executionContextHash:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt context mismatch.")
    if receipt_record.get("originalBaselineInventoryHash") != context.get("baselineInventoryHash"):
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt baseline mismatch.")
    if receipt_record.get("attemptNumber") != attempt_number:
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt attempt mismatch.")
    if receipt_record.get("finalDiagnosticVerdict") != diagnostic.get("finalVerdict"):
        raise RealTaskExecutionFailure("REAL_TASK_DIAGNOSTIC_FAILED", "Diagnostic receipt verdict mismatch.")


def _is_repairable_finding(code: str) -> bool:
    if code in REPAIRABLE_DIAGNOSTIC_CODES:
        return True
    if code.startswith("VALIDATOR_") or code.endswith("_FAILED") or code.endswith("_MISMATCH"):
        return not code.startswith("REAL_TASK_")
    return False
