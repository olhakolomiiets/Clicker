from __future__ import annotations

from pathlib import Path
from typing import Any

from file_utils import read_json
from pipeline_models import ExecutionStatus
from schema_validator import validate
from usage_limit_models import RATE_LIMIT_CODES, validate_usage_limit


def validate_role_execution(
    execution: dict[str, Any],
    schema_root: Path,
    expected_role: str,
    expected_task_id: str,
) -> list[str]:
    errors = _schema_errors("ROLE_EXECUTION_RESULT_INVALID", execution, schema_root / "role_execution_result.schema.json")
    status = execution.get("executionStatus")
    if execution.get("role") != expected_role:
        errors.append("ROLE_EXECUTION_RESULT_INVALID: role mismatch.")
    if execution.get("taskId") != expected_task_id:
        errors.append("ROLE_EXECUTION_RESULT_INVALID: taskId mismatch.")

    if status == ExecutionStatus.SUCCESS.value:
        if execution.get("result") is None:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: SUCCESS requires result.")
        if execution.get("usageLimit") is not None:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: SUCCESS requires null usageLimit.")
        if execution.get("errorCode") is not None:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: SUCCESS requires null errorCode.")
        if execution.get("errorMessage") is not None:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: SUCCESS requires null errorMessage.")
    elif status == ExecutionStatus.RATE_LIMITED.value:
        if execution.get("result") is not None:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: RATE_LIMITED requires null result.")
        if not isinstance(execution.get("usageLimit"), dict):
            errors.append("ROLE_EXECUTION_RESULT_INVALID: RATE_LIMITED requires usageLimit.")
        else:
            errors.extend(validate_usage_limit(execution["usageLimit"]))
        if execution.get("errorCode") not in RATE_LIMIT_CODES - {"CODEX_RATE_LIMIT_DATA_INVALID", "PIPELINE_PAUSED_RATE_LIMIT"}:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: RATE_LIMITED errorCode is invalid.")
    elif status == ExecutionStatus.TECHNICAL_ERROR.value:
        if execution.get("result") is not None:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: TECHNICAL_ERROR requires null result.")
        if not isinstance(execution.get("errorCode"), str) or not execution["errorCode"].strip():
            errors.append("ROLE_EXECUTION_RESULT_INVALID: TECHNICAL_ERROR requires errorCode.")
        if execution.get("usageLimit") is not None:
            errors.append("ROLE_EXECUTION_RESULT_INVALID: TECHNICAL_ERROR requires null usageLimit.")
    else:
        errors.append("ROLE_EXECUTION_RESULT_INVALID: executionStatus is invalid.")
    return errors


def validate_implementation_result(result: dict[str, Any], schema_root: Path, task_id: str) -> list[str]:
    if not isinstance(result, dict):
        return ["IMPLEMENTATION_RESULT_INVALID: result must be an object."]
    errors = _schema_errors("IMPLEMENTATION_RESULT_INVALID", result, schema_root / "implementation_result.schema.json")
    if result.get("taskId") != task_id:
        errors.append("IMPLEMENTATION_RESULT_INVALID: taskId mismatch.")
    if not isinstance(result.get("summary"), str) or not result["summary"].strip():
        errors.append("IMPLEMENTATION_RESULT_INVALID: summary must be non-empty.")
    return errors


def validate_audit_result(result: dict[str, Any], schema_root: Path, task_id: str) -> list[str]:
    if not isinstance(result, dict):
        return ["AUDIT_RESULT_INVALID: result must be an object."]
    errors = _schema_errors("AUDIT_RESULT_INVALID", result, schema_root / "audit_result.schema.json")
    if result.get("taskId") != task_id:
        errors.append("AUDIT_RESULT_INVALID: taskId mismatch.")
    findings = result.get("findings")
    if not isinstance(findings, list):
        findings = []
    for finding in findings:
        if not isinstance(finding, dict):
            errors.append("AUDIT_RESULT_INVALID: finding must be an object.")
            continue
        for name in ("code", "problem", "evidence", "requiredFix"):
            if not isinstance(finding.get(name), str) or not finding[name].strip():
                errors.append(f"AUDIT_RESULT_INVALID: finding {name} must be non-empty.")
    verdict = result.get("verdict")
    blocking = [finding for finding in findings if isinstance(finding, dict) and finding.get("severity") == "blocking"]
    blocked_reason = result.get("blockedReason")
    scope_violations = result.get("scopeViolations")
    if verdict == "PASS":
        if blocking:
            errors.append("AUDIT_RESULT_INVALID: PASS cannot include blocking findings.")
        if scope_violations:
            errors.append("AUDIT_RESULT_INVALID: PASS requires empty scopeViolations.")
        if isinstance(blocked_reason, str) and blocked_reason.strip():
            errors.append("AUDIT_RESULT_INVALID: PASS requires empty blockedReason.")
    elif verdict == "FIX_REQUIRED":
        if not findings:
            errors.append("AUDIT_RESULT_INVALID: FIX_REQUIRED requires at least one finding.")
        if isinstance(blocked_reason, str) and blocked_reason.strip():
            errors.append("AUDIT_RESULT_INVALID: FIX_REQUIRED cannot use blockedReason instead of findings.")
    elif verdict == "BLOCKED":
        if not blocking and not (isinstance(blocked_reason, str) and blocked_reason.strip()):
            errors.append("AUDIT_RESULT_INVALID: BLOCKED requires a blocking finding or blockedReason.")
    return errors


def validate_repair_result(
    result: dict[str, Any],
    schema_root: Path,
    task_id: str,
    repair_context: dict[str, Any],
) -> list[str]:
    if not isinstance(result, dict):
        return ["REPAIR_RESULT_INVALID: result must be an object."]
    errors = _schema_errors("REPAIR_RESULT_INVALID", result, schema_root / "repair_result.schema.json")
    if result.get("taskId") != task_id:
        errors.append("REPAIR_RESULT_INVALID: taskId mismatch.")
    if result.get("status") != "repaired":
        errors.append("REPAIR_RESULT_INVALID: status must be repaired.")
    known_codes = {
        finding["code"]
        for finding in repair_context.get("validationFindings", []) + repair_context.get("auditFindings", [])
        if isinstance(finding, dict) and isinstance(finding.get("code"), str)
    }
    for code in result.get("addressedFindingCodes", []):
        if code not in known_codes:
            errors.append(f"REPAIR_RESULT_INVALID: unknown addressed finding code {code!r}.")
    return errors


def _schema_errors(code: str, instance: Any, schema_path: Path) -> list[str]:
    return [f"{code}: {error}" for error in validate(instance, read_json(schema_path))]
