from __future__ import annotations

from copy import deepcopy
from dataclasses import replace
from types import MappingProxyType
import time
from pathlib import Path
from typing import Any

from file_utils import read_json, write_json_atomic
from parent_git_snapshot import parent_git_snapshot, snapshots_equal
from real_task_models import RealTaskError
from real_task_workspace import git_fingerprint, validate_service_files
from schema_validator import validate
from task_manifest_validator import parse_real_task_policy, validate_task_manifest_file
from validator_models import (
    DANGEROUS_VALIDATOR_FIELDS,
    REAL_TASK_VALIDATION_STAGE,
    VALIDATION_HANDLE_VERSION,
    VALIDATION_REPORT_VERSION,
    VALIDATOR_TYPES,
    ValidationPrerequisiteHandle,
    ValidationExecutionResult,
    ValidationFailure,
    ValidationState,
    ValidatorContext,
    ValidatorOutcome,
    ValidatorPolicy,
    ValidatorReadableScope,
    ValidatorScopeKind,
    ValidatorStatus,
    VALIDATOR_BLOCKED_CODES,
    VALIDATOR_FAIL_CODES_BY_TYPE,
    canonical_json_bytes,
    canonical_sha256,
    new_validation_capability,
)
from validator_registry import load_validator_registry
from validator_safe_io import changed_paths, resolve_workspace_path
from validator_schema_registry import load_schema_registry
from workspace_change_inventory import build_change_inventory, compare_inventory_hash, entries_by_path
from workspace_change_models import file_sha256
from real_task_change_runner import (
    validate_change_inventory_report,
    validate_change_policy_report,
    validate_change_tracking_report,
    validate_final_change_analysis_report,
    validate_source_inventory_report,
    validate_workspace_diff_report,
    _parent_source_changed,
)
from workspace_change_policy import parse_change_policy
from workspace_inventory import InventoryError, is_symlink_or_reparse


_VALIDATION_PREREQUISITE_REGISTRY: dict[str, dict[str, Any]] = {}


def parse_validation_policy(config: dict[str, Any]) -> tuple[ValidatorPolicy | None, list[RealTaskError]]:
    raw = config.get("realTaskValidationPolicy")
    errors: list[RealTaskError] = []
    required = {
        "enabled",
        "allowedValidatorTypes",
        "maxValidatorsPerTask",
        "maxFindings",
        "maxTextReadBytes",
        "maxJsonReadBytes",
        "maxSchemaBytes",
        "maxReportBytes",
        "failFast",
        "allowCommandValidators",
        "allowRegexValidators",
        "allowExternalSchemaPaths",
        "allowDynamicPlugins",
        "allowNetwork",
        "allowUnity",
    }
    if not isinstance(raw, dict):
        return None, [_error("REAL_TASK_VALIDATION_CONFIG_INVALID", "realTaskValidationPolicy must be an object.", "realTaskValidationPolicy")]
    for field in sorted(set(raw) - required):
        errors.append(_error("REAL_TASK_VALIDATION_CONFIG_INVALID", "Unknown realTaskValidationPolicy field.", f"realTaskValidationPolicy.{field}"))
    for field in sorted(required - set(raw)):
        errors.append(_error("REAL_TASK_VALIDATION_CONFIG_INVALID", "Missing realTaskValidationPolicy field.", f"realTaskValidationPolicy.{field}"))
    exact_bools = {
        "enabled": True,
        "failFast": False,
        "allowCommandValidators": False,
        "allowRegexValidators": False,
        "allowExternalSchemaPaths": False,
        "allowDynamicPlugins": False,
        "allowNetwork": False,
        "allowUnity": False,
    }
    for field, expected in exact_bools.items():
        if raw.get(field) is not expected:
            errors.append(_error("REAL_TASK_VALIDATION_CONFIG_INVALID", f"{field} must be exact {expected}.", f"realTaskValidationPolicy.{field}"))
    allowed = raw.get("allowedValidatorTypes")
    if not isinstance(allowed, list) or tuple(allowed) != VALIDATOR_TYPES:
        errors.append(_error("REAL_TASK_VALIDATION_CONFIG_INVALID", "allowedValidatorTypes must exactly match the committed supported set in order.", "realTaskValidationPolicy.allowedValidatorTypes"))
        allowed_tuple = ()
    else:
        allowed_tuple = tuple(allowed)
    caps = {
        "maxValidatorsPerTask": (1, 64),
        "maxFindings": (1, 256),
        "maxTextReadBytes": (1, 2097152),
        "maxJsonReadBytes": (1, 2097152),
        "maxSchemaBytes": (1, 1048576),
        "maxReportBytes": (1024, 4194304),
    }
    parsed: dict[str, int] = {}
    for field, (minimum, maximum) in caps.items():
        value = raw.get(field)
        if not isinstance(value, int) or isinstance(value, bool) or value < minimum or value > maximum:
            errors.append(_error("REAL_TASK_VALIDATION_CONFIG_INVALID", f"{field} must be an integer from {minimum} to {maximum}.", f"realTaskValidationPolicy.{field}"))
            parsed[field] = minimum
        else:
            parsed[field] = value
    if errors:
        return None, errors
    return ValidatorPolicy(
        enabled=True,
        allowedValidatorTypes=allowed_tuple,
        maxValidatorsPerTask=parsed["maxValidatorsPerTask"],
        maxFindings=parsed["maxFindings"],
        maxTextReadBytes=parsed["maxTextReadBytes"],
        maxJsonReadBytes=parsed["maxJsonReadBytes"],
        maxSchemaBytes=parsed["maxSchemaBytes"],
        maxReportBytes=parsed["maxReportBytes"],
        failFast=False,
        allowCommandValidators=False,
        allowRegexValidators=False,
        allowExternalSchemaPaths=False,
        allowDynamicPlugins=False,
        allowNetwork=False,
        allowUnity=False,
    ), []


def create_validation_prerequisite_handle(change_analysis_result: Any, root: Path, config: dict[str, Any]) -> ValidationPrerequisiteHandle:
    report = getattr(change_analysis_result, "report", None)
    report_path = getattr(change_analysis_result, "reportPath", None)
    if not isinstance(report, dict) or not isinstance(report_path, str):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "B-C prerequisite requires a B-B ChangeAnalysisResult with report path.")
    if report.get("stage") != "BOOTSTRAP-03B-2B-B" or report.get("finalState") != "COMPLETED" or report.get("finalVerdict") != "PASS":
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "B-B result did not PASS and cannot authorize validation.")
    run_dir = Path(report_path).resolve(strict=False).parent
    workspace = run_dir / "workspace"
    tracking = read_json(run_dir / "CHANGE_TRACKING_REPORT.json")
    baseline = read_json(run_dir / "CHANGE_BASELINE_INVENTORY.json")
    final_inventory = read_json(run_dir / "CHANGE_FINAL_INVENTORY.json")
    capability = new_validation_capability()
    prerequisite_id = f"validation_prereq_{report['runId']}_{capability[:12]}"
    handle = ValidationPrerequisiteHandle(
        handleVersion=VALIDATION_HANDLE_VERSION,
        validationPrerequisiteId=prerequisite_id,
        capability=capability,
        runId=str(report["runId"]),
        taskId=str(report["taskId"]),
        trackingId=str(tracking.get("trackingId")),
        stage="BOOTSTRAP-03B-2B-B",
        trustedContextHash=str(report["trustedContextHash"]),
        manifestSha256=str(report["manifestSha256"]),
        workspaceDirectory=str(workspace.resolve(strict=True)),
        runDirectory=str(run_dir.resolve(strict=True)),
        finalChangeReportPath=str(Path(report_path).resolve(strict=True)),
        finalChangeReportHash=canonical_sha256(report),
        baselineInventoryHash=str(report["changeBaselineInventoryHash"]),
        finalInventoryHash=str(report["changeFinalInventoryHash"]),
        diffReportHash=str(report["diffReportHash"]),
        policyReportHash=str(report["policyReportHash"]),
        serviceRegistryHash=canonical_sha256(baseline.get("serviceRegistry", [])),
        isolatedGitFingerprintHash=canonical_sha256(final_inventory.get("isolatedGitFingerprint")),
        sourcePostInventoryHash=str(tracking.get("sourcePostInventoryHash")),
        noModel=True,
        noCodex=True,
        noSandbox=True,
        noUnity=True,
    )
    _VALIDATION_PREREQUISITE_REGISTRY[prerequisite_id] = {
        "capability": capability,
        "handleObjectId": id(handle),
        "finalChangeReportHash": handle.finalChangeReportHash,
        "consumed": False,
    }
    return handle


def execute_validation_plan(validation_prerequisite_handle: ValidationPrerequisiteHandle, root: Path, automation_root: Path, config: dict[str, Any]) -> ValidationExecutionResult:
    started = time.monotonic()
    state_history = [ValidationState.PENDING.value]
    report_path: Path | None = None
    warnings: list[dict[str, Any]] = []
    try:
        _transition(state_history, ValidationState.BB_REVALIDATING)
        handle = _validate_prerequisite_handle(validation_prerequisite_handle)
        run_dir, context, final_change, artifacts = _revalidate_bb_result(handle, root, automation_root, config)
        _consume_prerequisite_handle(handle)
        task = read_json(Path(context["workspaceDirectory"]) / "task.json")
        validation_policy, policy_errors = parse_validation_policy(config)
        if policy_errors or validation_policy is None:
            first = policy_errors[0]
            raise ValidationFailure(first.code, first.message)
        _transition(state_history, ValidationState.REGISTRY_LOADING)
        registry, registry_snapshot = load_validator_registry(validation_policy)
        schema_registry = load_schema_registry(root, automation_root, validation_policy)
        schema_snapshot = schema_registry.snapshot()
        _transition(state_history, ValidationState.PLAN_REVALIDATING)
        baseline = artifacts["baseline"]
        final_inventory = artifacts["finalInventory"]
        diff = artifacts["diff"]
        policy_report = artifacts["policyReport"]
        plan = _validated_plan(root, automation_root, config, context, task, validation_policy, baseline, final_inventory, diff)
        service_paths = tuple(str(item["workspacePath"]) for item in baseline.get("serviceRegistry", []) if item.get("workspacePath"))
        readable_scopes = _readable_scopes(task, diff, baseline, final_inventory)
        validator_context = ValidatorContext(
            repositoryRoot=root,
            automationRoot=automation_root,
            runDirectory=run_dir,
            workspaceDirectory=Path(context["workspaceDirectory"]),
            task=_freeze(task),
            effectivePolicy=_freeze(context["effectiveHostPolicy"]),
            validationPolicy=validation_policy,
            baselineInventory=_freeze(baseline),
            finalInventory=_freeze(final_inventory),
            diffReport=_freeze(diff),
            policyReport=_freeze(policy_report),
            finalChangeReport=_freeze(final_change),
            serviceRegistry=service_paths,
            readableScopes=readable_scopes,
            schemaRegistry=schema_registry,
        )
        _transition(state_history, ValidationState.VALIDATORS_EXECUTING)
        pre_integrity = _fresh_integrity(root, automation_root, run_dir, context, final_inventory, config)
        if not _integrity_pass(pre_integrity):
            raise ValidationFailure("REAL_TASK_VALIDATION_INTEGRITY_INVALID", "Pre-validation integrity does not match trusted B-B final state.")
        outcomes: list[ValidatorOutcome] = []
        for index, entry in enumerate(plan):
            definition = registry[entry["type"]]
            entry_context = replace(validator_context, validatorIndex=index)
            try:
                validator_started = time.monotonic()
                before_hash = _workspace_integrity_hash(entry_context, config)
                outcome = definition.implementation(entry_context, entry)
                after_hash = _workspace_integrity_hash(entry_context, config)
                if before_hash != after_hash:
                    raise ValidationFailure("REAL_TASK_VALIDATOR_WORKSPACE_MUTATION", "Validator unexpectedly changed workspace or service state.")
                _validate_raw_outcome_numbers(index, outcome)
                outcome = _normalize_outcome(index, definition.version, outcome, round((time.monotonic() - validator_started) * 1000))
                _validate_outcome(entry, index, definition.version, outcome, validation_policy)
            except ValidationFailure as exc:
                outcome = ValidatorOutcome(entry["id"], index, entry["type"], definition.version, ValidatorStatus.BLOCKED.value, exc.code, _safe_message(exc.message))
            except Exception as exc:
                outcome = ValidatorOutcome(entry["id"], index, entry["type"], definition.version, ValidatorStatus.BLOCKED.value, "REAL_TASK_VALIDATOR_EXECUTION_FAILED", type(exc).__name__)
            outcomes.append(outcome)
            if outcome.status == ValidatorStatus.BLOCKED.value:
                break
        _transition(state_history, ValidationState.INTEGRITY_RECHECKING)
        integrity = _fresh_integrity(root, automation_root, run_dir, context, final_inventory, config)
        final_verdict = _final_verdict(outcomes, integrity)
        final_state = ValidationState.COMPLETED.value if final_verdict == "PASS" else (ValidationState.BLOCKED.value if final_verdict == "BLOCKED" else ValidationState.FAILED.value)
        _transition(state_history, ValidationState.REPORTING)
        _transition(state_history, ValidationState.COMPLETED if final_verdict == "PASS" else (ValidationState.BLOCKED if final_verdict == "BLOCKED" else ValidationState.FAILED))
        reports = _build_reports(
            state_history,
            context,
            final_change,
            registry_snapshot,
            schema_snapshot,
            plan,
            outcomes,
            integrity,
            round(time.monotonic() - started, 3),
            final_state,
            final_verdict,
            None,
            None,
            warnings,
        )
        _write_validation_report(run_dir / "VALIDATOR_REGISTRY_SNAPSHOT.json", reports["registry"], validation_policy, automation_root)
        _write_validation_report(run_dir / "VALIDATION_PLAN_REPORT.json", reports["plan"], validation_policy, automation_root)
        _write_validation_report(run_dir / "VALIDATOR_RESULTS_REPORT.json", reports["results"], validation_policy, automation_root)
        _write_validation_report(run_dir / "VALIDATION_INTEGRITY_REPORT.json", reports["integrity"], validation_policy, automation_root)
        report = reports["final"]
        report_path = run_dir / "FINAL_VALIDATION_REPORT.json"
        _write_validation_report(report_path, report, validation_policy, automation_root)
        return ValidationExecutionResult(final_verdict, final_state, report, str(report_path))
    except ValidationFailure as exc:
        final_state = ValidationState.BLOCKED.value
        fallback = _fallback_report(state_history, validation_prerequisite_handle, round(time.monotonic() - started, 3), exc.code, exc.message, warnings)
        try:
            policy, errors = parse_validation_policy(config)
            if policy is not None and not errors and isinstance(validation_prerequisite_handle, ValidationPrerequisiteHandle):
                run_dir = _best_run_dir(validation_prerequisite_handle)
                if run_dir is not None:
                    report_path = run_dir / "FINAL_VALIDATION_REPORT.json"
                    _write_validation_report(report_path, fallback, policy, automation_root)
        except Exception:
            warnings.append({"code": "REAL_TASK_VALIDATION_REPORT_WRITE_FAILED", "message": "Fallback validation report could not be written."})
        return ValidationExecutionResult("BLOCKED", final_state, fallback, str(report_path) if report_path else None)


def _validate_prerequisite_handle(handle: ValidationPrerequisiteHandle) -> ValidationPrerequisiteHandle:
    if not isinstance(handle, ValidationPrerequisiteHandle):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "execute_validation_plan requires a registry-bound validation prerequisite handle.")
    if handle.handleVersion != VALIDATION_HANDLE_VERSION or handle.stage != "BOOTSTRAP-03B-2B-B":
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "Validation prerequisite handle version or stage is invalid.")
    if any(getattr(handle, field) is not True for field in ("noModel", "noCodex", "noSandbox", "noUnity")):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "Validation prerequisite no-execution flags are invalid.")
    entry = _VALIDATION_PREREQUISITE_REGISTRY.get(handle.validationPrerequisiteId)
    if not entry:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "Validation prerequisite is not bound to this process registry.")
    if entry.get("consumed"):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "Validation prerequisite was already consumed.")
    if entry.get("capability") != handle.capability or entry.get("handleObjectId") != id(handle):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "Validation prerequisite capability or object identity mismatch.")
    if entry.get("finalChangeReportHash") != handle.finalChangeReportHash:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "Validation prerequisite final report hash mismatch.")
    return handle


def _consume_prerequisite_handle(handle: ValidationPrerequisiteHandle) -> None:
    entry = _VALIDATION_PREREQUISITE_REGISTRY.get(handle.validationPrerequisiteId)
    if entry is not None:
        entry["consumed"] = True


def _revalidate_bb_result(handle: ValidationPrerequisiteHandle, root: Path, automation_root: Path, config: dict[str, Any]) -> tuple[Path, dict[str, Any], dict[str, Any], dict[str, dict[str, Any]]]:
    report = read_json(Path(handle.finalChangeReportPath))
    if report.get("stage") != "BOOTSTRAP-03B-2B-B" or report.get("finalState") != "COMPLETED" or report.get("finalVerdict") != "PASS":
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "B-B final change analysis must be trusted PASS.")
    if any(report.get(field) is not False for field in ("modelInvocationStarted", "sandboxStarted", "unityStarted", "networkUsed")) or report.get("codexInvocationCount") != 0:
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "B-B no-execution fields are invalid.")
    if canonical_sha256(report) != handle.finalChangeReportHash:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "B-B final change report hash mismatch.")
    run_id = str(report["runId"])
    run_dir = Path(handle.runDirectory)
    expected_run_dir = root / config["realTaskFoundation"]["runtimeRoot"] / run_id
    if run_dir.resolve(strict=True) != expected_run_dir.resolve(strict=True):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "B-B run directory does not match configured runtime root.")
    required = [
        "TRUSTED_RUN_CONTEXT.json",
        "task.json",
        "effective_policy.json",
        "CHANGE_TRACKING_REPORT.json",
        "CHANGE_BASELINE_INVENTORY.json",
        "CHANGE_FINAL_INVENTORY.json",
        "WORKSPACE_DIFF_REPORT.json",
        "CHANGE_POLICY_REPORT.json",
        "FINAL_CHANGE_ANALYSIS_REPORT.json",
        "SOURCE_POST_INVENTORY.json",
        "FINAL_WORKSPACE_PREPARATION_REPORT.json",
    ]
    context = _read_schema_report(automation_root, run_dir / "TRUSTED_RUN_CONTEXT.json", "real_task_trusted_context.schema.json")
    workspace = Path(context["workspaceDirectory"])
    if workspace.resolve(strict=True) != Path(handle.workspaceDirectory).resolve(strict=True):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", "Workspace identity mismatch.")
    for name in required:
        path = workspace / name if name in {"task.json", "effective_policy.json"} else run_dir / name
        if not path.exists():
            raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", f"Required B-B artifact is missing: {name}.")
    task_json = read_json(workspace / "task.json")
    effective_policy = read_json(workspace / "effective_policy.json")
    final_prep = _read_schema_report(automation_root, run_dir / "FINAL_WORKSPACE_PREPARATION_REPORT.json", "real_task_workspace_final.schema.json")
    source_post = _read_schema_report(automation_root, run_dir / "SOURCE_POST_INVENTORY.json", "real_task_source_inventory.schema.json")
    tracking = _read_schema_report(automation_root, run_dir / "CHANGE_TRACKING_REPORT.json", "real_task_change_tracking.schema.json")
    baseline = _read_schema_report(automation_root, run_dir / "CHANGE_BASELINE_INVENTORY.json", "real_task_change_inventory.schema.json")
    final_inventory = _read_schema_report(automation_root, run_dir / "CHANGE_FINAL_INVENTORY.json", "real_task_change_inventory.schema.json")
    diff = _read_schema_report(automation_root, run_dir / "WORKSPACE_DIFF_REPORT.json", "real_task_workspace_diff.schema.json")
    policy_report = _read_schema_report(automation_root, run_dir / "CHANGE_POLICY_REPORT.json", "real_task_change_policy.schema.json")
    final_change = _read_schema_report(automation_root, run_dir / "FINAL_CHANGE_ANALYSIS_REPORT.json", "real_task_change_final.schema.json")
    if final_change != report:
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "Persisted B-B final report differs from in-memory result.")
    semantic_errors = validate_final_change_analysis_report(final_change, {"policyReport": policy_report, "diff": diff})
    if semantic_errors:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", semantic_errors[0])
    semantic_checks = (
        validate_source_inventory_report(source_post, context.get("sourceLimits")),
        validate_change_tracking_report(tracking),
        validate_change_inventory_report(baseline),
        validate_change_inventory_report(final_inventory),
        validate_workspace_diff_report(diff, baseline, final_inventory),
        validate_change_policy_report(policy_report),
    )
    for errors in semantic_checks:
        if errors:
            raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", errors[0])
    if final_change.get("trustedContextHash") != canonical_sha256(context):
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "Trusted context hash mismatch.")
    if final_change.get("manifestSha256") != context.get("manifestSha256"):
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "Manifest hash mismatch.")
    if final_change.get("parentGitChanged") is not False or final_change.get("parentSourceChanged") is not False:
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "Parent Git/source changed before validation.")
    pass_fields = ("serviceFilesValid", "isolatedGitValid", "writeScopeValid", "deleteScopeValid", "changeLimitsValid", "binaryTextPolicyValid", "expectedOutputsValid")
    if any(final_change.get(field) is not True for field in pass_fields):
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "B-B PASS contract booleans are invalid.")
    if final_change.get("errorCode") is not None or final_change.get("errorMessage") is not None:
        raise ValidationFailure("REAL_TASK_VALIDATION_BB_INVALID", "B-B PASS cannot contain an error.")
    _validate_bb_hash_graph(handle, context, final_prep, source_post, tracking, baseline, final_inventory, diff, policy_report, final_change)
    _validate_service_snapshot(workspace, baseline, "task.json", task_json, None)
    _validate_service_snapshot(workspace, baseline, "effective_policy.json", effective_policy, None)
    return run_dir, context, final_change, {
        "task": task_json,
        "effectivePolicy": effective_policy,
        "finalPrep": final_prep,
        "sourcePost": source_post,
        "tracking": tracking,
        "baseline": baseline,
        "finalInventory": final_inventory,
        "diff": diff,
        "policyReport": policy_report,
    }


def _validated_plan(root: Path, automation_root: Path, config: dict[str, Any], context: dict[str, Any], task: dict[str, Any], policy: ValidatorPolicy, baseline: dict[str, Any], final_inventory: dict[str, Any], diff: dict[str, Any]) -> list[dict[str, Any]]:
    manifest_path = Path(context["manifestPath"])
    validation = validate_task_manifest_file(root, automation_root, config, manifest_path, inspect_sources=False)
    if not validation.ok or validation.manifestSha256 != context.get("manifestSha256"):
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Immutable task manifest no longer validates.")
    if task.get("validationPlan") != validation.manifest.get("validationPlan"):
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Workspace task validationPlan differs from immutable manifest.")
    raw_plan = task.get("validationPlan")
    if not isinstance(raw_plan, list) or len(raw_plan) > policy.maxValidatorsPerTask:
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "validationPlan count exceeds host cap.")
    result: list[dict[str, Any]] = []
    seen_ids: set[str] = set()
    for index, item in enumerate(raw_plan, start=1):
        if not isinstance(item, dict):
            raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Validator entry must be an object.")
        entry = dict(item)
        entry.setdefault("id", f"validation-{index:03d}")
        if entry["id"] in seen_ids:
            raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Validator IDs must be unique.")
        seen_ids.add(entry["id"])
        if entry.get("type") not in policy.allowedValidatorTypes:
            raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Validator type is not registered.")
        if set(entry) & DANGEROUS_VALIDATOR_FIELDS:
            raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Validator contains dangerous command/plugin fields.")
        _validate_plan_arguments(entry, policy)
        _prevalidate_entry_paths(entry, context, task, baseline, final_inventory, diff)
        result.append(entry)
    return result


def _read_schema_report(automation_root: Path, path: Path, schema_name: str) -> dict[str, Any]:
    payload = read_json(path)
    errors = validate(payload, read_json(automation_root / "schemas" / schema_name))
    if errors:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{path.name}: {errors[0]}")
    return payload


def _validate_bb_hash_graph(
    handle: ValidationPrerequisiteHandle,
    context: dict[str, Any],
    final_prep: dict[str, Any],
    source_post: dict[str, Any],
    tracking: dict[str, Any],
    baseline: dict[str, Any],
    final_inventory: dict[str, Any],
    diff: dict[str, Any],
    policy_report: dict[str, Any],
    final_change: dict[str, Any],
) -> None:
    checks = {
        "runId": final_change.get("runId") == handle.runId == context.get("runId") == tracking.get("runId"),
        "taskId": final_change.get("taskId") == handle.taskId == context.get("taskId") == tracking.get("taskId"),
        "trackingId": tracking.get("trackingId") == handle.trackingId,
        "trustedContextHash": final_change.get("trustedContextHash") == handle.trustedContextHash == canonical_sha256(context),
        "manifestSha256": final_change.get("manifestSha256") == handle.manifestSha256 == context.get("manifestSha256"),
        "sourcePostInventoryHash": source_post.get("inventorySha256") == handle.sourcePostInventoryHash == tracking.get("sourcePostInventoryHash"),
        "finalPrepHash": final_prep.get("trustedContextHash") == handle.trustedContextHash,
        "baselineInventoryHash": baseline.get("inventorySha256") == handle.baselineInventoryHash == final_change.get("changeBaselineInventoryHash"),
        "finalInventoryHash": final_inventory.get("inventorySha256") == handle.finalInventoryHash == final_change.get("changeFinalInventoryHash"),
        "diffReportHash": diff.get("diffReportSha256") == handle.diffReportHash == final_change.get("diffReportHash"),
        "policyReportHash": canonical_sha256(policy_report) == handle.policyReportHash == final_change.get("policyReportHash"),
        "serviceRegistryHash": canonical_sha256(baseline.get("serviceRegistry", [])) == handle.serviceRegistryHash == tracking.get("serviceRegistryHash"),
        "isolatedGitFingerprintHash": canonical_sha256(final_inventory.get("isolatedGitFingerprint")) == handle.isolatedGitFingerprintHash,
        "workspaceDirectory": str(Path(context["workspaceDirectory"]).resolve(strict=True)) == handle.workspaceDirectory == tracking.get("workspaceDirectory"),
    }
    for name, ok in checks.items():
        if not ok:
            raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"B-B trust graph mismatch: {name}.")


def _validate_service_snapshot(workspace: Path, baseline: dict[str, Any], relative_path: str, payload: Any, expected_payload_hash: str | None = None) -> None:
    service = None
    for item in baseline.get("serviceRegistry", []):
        if item.get("workspacePath") == relative_path or item.get("path") == relative_path:
            service = item
            break
    if not service:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} is not registered as immutable service.")
    if service.get("expectedType") != "file" or service.get("kind") != "file":
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} service registry type mismatch.")
    workspace_root = workspace.resolve(strict=True)
    path = (workspace_root / relative_path).resolve(strict=False)
    try:
        path.relative_to(workspace_root)
    except ValueError as exc:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} service path escapes workspace.") from exc
    if is_symlink_or_reparse(path) or not path.is_file():
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} service file is unsafe or missing.")
    stat_result = path.stat(follow_symlinks=False)
    inventory_entry = entries_by_path(baseline).get(relative_path)
    if inventory_entry and inventory_entry.get("type") != "file":
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} inventory type mismatch.")
    if inventory_entry and isinstance(inventory_entry.get("size"), int) and stat_result.st_size != inventory_entry.get("size"):
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} service size mismatch.")
    expected_sha = service.get("expectedSha256")
    actual_sha = file_sha256(path)
    if expected_sha and actual_sha != expected_sha:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} service hash mismatch.")
    if expected_payload_hash and actual_sha != expected_payload_hash and canonical_sha256(payload) != expected_payload_hash:
        raise ValidationFailure("REAL_TASK_VALIDATION_CONTEXT_MISMATCH", f"{relative_path} canonical payload hash mismatch.")


def _prevalidate_entry_paths(entry: dict[str, Any], context: dict[str, Any], task: dict[str, Any], baseline: dict[str, Any], final_inventory: dict[str, Any], diff: dict[str, Any]) -> None:
    scope = _readable_scopes(task, diff, baseline, final_inventory)
    fake_context = ValidatorContext(
        repositoryRoot=Path(context["repositoryRoot"]),
        automationRoot=Path(context["repositoryRoot"]) / "CodexAutomation",
        runDirectory=Path(context["runDirectory"]),
        workspaceDirectory=Path(context["workspaceDirectory"]),
        task=task,
        effectivePolicy=context["effectiveHostPolicy"],
        validationPolicy=ValidatorPolicy(True, VALIDATOR_TYPES, 64, 256, 2097152, 2097152, 1048576, 4194304, False, False, False, False, False, False, False),
        baselineInventory={},
        finalInventory={},
        diffReport={},
        policyReport={},
        finalChangeReport={},
        serviceRegistry=(),
        readableScopes=scope,
        schemaRegistry=None,
    )
    for field in ("path",):
        if field in entry:
            resolve_workspace_path(fake_context, entry[field], require_existing=False)
    for field in ("paths",):
        if field in entry:
            for path in entry[field]:
                resolve_workspace_path(fake_context, path, require_existing=False)


def _validate_plan_arguments(entry: dict[str, Any], policy: ValidatorPolicy) -> None:
    validator_type = entry["type"]
    required = {
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
    }[validator_type]
    if validator_type == "no_conflict_markers":
        allowed = required | {"paths"}
    else:
        allowed = required
    if set(entry) - allowed or required - set(entry):
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Validator arguments do not match contract.")
    if "text" in entry and (not isinstance(entry["text"], str) or not entry["text"]):
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Text literal must be non-empty.")
    if "jsonPointer" in entry and (not isinstance(entry["jsonPointer"], str) or not entry["jsonPointer"].startswith("/")):
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "JSON pointer is invalid.")
    for field in ("maxBytes", "maxFiles"):
        if field in entry and (not isinstance(entry[field], int) or isinstance(entry[field], bool) or entry[field] < 1):
            raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Numeric validator cap must be a positive integer.")
    if "paths" in entry and (not isinstance(entry["paths"], list) or len(set(entry["paths"])) != len(entry["paths"])):
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Validator paths must be a unique array.")
    if "extensions" in entry and (not isinstance(entry["extensions"], list) or len(set(entry["extensions"])) != len(entry["extensions"])):
        raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "Validator extensions must be a unique array.")
    if "schemaName" in entry:
        name = entry["schemaName"]
        if not isinstance(name, str) or "/" in name or "\\" in name or name.endswith(".json") or ".." in name:
            raise ValidationFailure("REAL_TASK_VALIDATION_PLAN_INVALID", "schemaName must be a registry name.")


def _readable_scopes(task: dict[str, Any], diff: dict[str, Any], baseline: dict[str, Any] | None = None, final_inventory: dict[str, Any] | None = None) -> tuple[ValidatorReadableScope, ...]:
    scopes: dict[tuple[str, str, str], ValidatorReadableScope] = {}
    entries = {}
    if baseline:
        entries.update(entries_by_path(baseline))
    if final_inventory:
        entries.update(entries_by_path(final_inventory))

    def add(path: str, origin: str, kind: str | None = None, recursive: bool | None = None) -> None:
        if _is_scope_service_path(path):
            return
        entry = entries.get(path)
        scope_kind = kind or (ValidatorScopeKind.DIRECTORY.value if entry and entry.get("type") == "directory" else ValidatorScopeKind.FILE.value)
        recurse = bool(recursive) if recursive is not None else scope_kind == ValidatorScopeKind.DIRECTORY.value
        scope = ValidatorReadableScope(path.rstrip("/"), scope_kind, origin, recurse, False)
        scopes[(scope.normalizedPath, scope.scopeKind, scope.origin)] = scope

    for field, origin in (("sourcePaths", "SOURCE_PATH"), ("allowedWritePaths", "ALLOWED_WRITE_PATH"), ("allowedDeletePaths", "ALLOWED_WRITE_PATH")):
        for path in task.get(field, []):
            if isinstance(path, str):
                add(path, origin)
    for output in task.get("expectedOutputs", []):
        if isinstance(output, dict) and isinstance(output.get("path"), str):
            kind = ValidatorScopeKind.DIRECTORY.value if output.get("kind") == "directory" else ValidatorScopeKind.FILE.value
            add(output["path"], "EXPECTED_OUTPUT", kind)
    for category in ("added", "modified", "deleted", "typeChanged"):
        for item in diff.get("categories", {}).get(category, []):
            path = str(item["path"])
            before = item.get("before") or {}
            after = item.get("after") or {}
            entry_type = after.get("type") or before.get("type")
            kind = ValidatorScopeKind.DIRECTORY.value if entry_type == "directory" else ValidatorScopeKind.FILE.value
            add(path, "TRUSTED_CHANGED_PATH", kind, recursive=False)
    return tuple(sorted(scopes.values(), key=lambda item: (item.normalizedPath, item.scopeKind, item.origin)))


def _is_scope_service_path(path: str) -> bool:
    services = {"AGENTS.md", "task.json", "effective_policy.json", ".agents", ".git", ".codex", "CodexAutomation"}
    return path in services or any(path.startswith(f"{service}/") for service in services) or path.endswith("/AGENTS.md")


def _workspace_integrity_hash(context: ValidatorContext, config: dict[str, Any]) -> str:
    real_policy, _ = parse_real_task_policy(config)
    change_policy, errors = parse_change_policy(config, real_policy)
    if errors or change_policy is None:
        raise ValidationFailure("REAL_TASK_VALIDATION_INTEGRITY_INVALID", "Change policy cannot be reparsed.")
    service_registry = _mutable_json(context.baselineInventory.get("serviceRegistry", []))
    inventory = build_change_inventory(context.workspaceDirectory, context.finalChangeReport["runId"], context.finalChangeReport["taskId"], context.finalChangeReport["trustedContextHash"], "validation_integrity", service_registry, change_policy, int(config["realTaskFoundation"]["maxInventoryEntries"]))
    return inventory["inventorySha256"]


def _fresh_integrity(root: Path, automation_root: Path, run_dir: Path, context: dict[str, Any], final_inventory: dict[str, Any], config: dict[str, Any]) -> dict[str, Any]:
    workspace = Path(context["workspaceDirectory"])
    real_policy, _ = parse_real_task_policy(config)
    change_policy, errors = parse_change_policy(config, real_policy)
    if errors or change_policy is None:
        raise ValidationFailure("REAL_TASK_VALIDATION_INTEGRITY_INVALID", "Change policy cannot be reparsed for integrity.")
    try:
        fresh_inventory = build_change_inventory(workspace, context["runId"], context["taskId"], canonical_sha256(context), "validation_post", final_inventory.get("serviceRegistry", []), change_policy, int(config["realTaskFoundation"]["maxInventoryEntries"]))
    except InventoryError as exc:
        raise ValidationFailure(exc.code, exc.message) from exc
    service_valid, _ = validate_service_files(workspace, final_inventory.get("serviceRegistry", []), int(config["realTaskFoundation"]["copyChunkBytes"]))
    agents_valid = _agents_directory_valid(workspace, final_inventory)
    isolated_git_valid = canonical_sha256(git_fingerprint(workspace)) == canonical_sha256(final_inventory.get("isolatedGitFingerprint"))
    parent_final, git_errors = parent_git_snapshot(root)
    parent_changed = True if git_errors else not snapshots_equal(context["parentGitInitialSnapshot"], parent_final)
    source_schema = read_json(automation_root / "schemas" / "real_task_source_inventory.schema.json")
    parent_source_changed = _parent_source_changed(root, run_dir, context, read_json(run_dir / "SOURCE_POST_INVENTORY.json")["inventorySha256"], int(config["realTaskFoundation"]["maxInventoryEntries"]), int(config["realTaskFoundation"]["copyChunkBytes"]), source_schema)
    workspace_matches_trusted_final = fresh_inventory.get("entries") == final_inventory.get("entries") and fresh_inventory.get("fileCount") == final_inventory.get("fileCount") and fresh_inventory.get("directoryCount") == final_inventory.get("directoryCount") and fresh_inventory.get("totalBytes") == final_inventory.get("totalBytes")
    return {
        "reportVersion": VALIDATION_REPORT_VERSION,
        "runId": context["runId"],
        "taskId": context["taskId"],
        "stage": REAL_TASK_VALIDATION_STAGE,
        "trustedFinalInventoryHash": final_inventory.get("inventorySha256"),
        "freshWorkspaceInventoryHash": fresh_inventory.get("inventorySha256"),
        "workspaceUnchanged": workspace_matches_trusted_final,
        "workspaceMatchesTrustedFinal": workspace_matches_trusted_final,
        "serviceFilesValid": service_valid,
        "agentsDirectoryValid": agents_valid,
        "isolatedGitValid": isolated_git_valid,
        "parentGitChanged": parent_changed,
        "parentSourceChanged": parent_source_changed,
        "noValidatorWrites": workspace_matches_trusted_final,
        "parentGitFinalSnapshot": parent_final,
        "finalVerdict": "PASS" if workspace_matches_trusted_final and service_valid and agents_valid and isolated_git_valid and parent_changed is False and parent_source_changed is False else "BLOCKED",
        "errorCode": None,
        "errorMessage": None,
    }


def _normalize_outcome(index: int, version: str, outcome: ValidatorOutcome, duration_ms: int) -> ValidatorOutcome:
    return replace(outcome, durationMilliseconds=max(0, duration_ms))


def _validate_raw_outcome_numbers(index: int, outcome: ValidatorOutcome) -> None:
    if not _is_exact_non_negative_int(outcome.validatorIndex) or outcome.validatorIndex != index:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator result index/version mismatch.")
    for field_name in ("filesRead", "bytesRead", "durationMilliseconds"):
        if not _is_exact_non_negative_int(getattr(outcome, field_name)):
            raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", f"{field_name} must be a non-negative integer.")


def _agents_directory_valid(workspace: Path, final_inventory: dict[str, Any]) -> bool:
    registry = final_inventory.get("serviceRegistry", [])
    service = next((item for item in registry if item.get("workspacePath") == ".agents" or item.get("path") == ".agents"), None)
    if not service or service.get("expectedType") != "directory":
        return False
    path = workspace / ".agents"
    if not path.exists() or is_symlink_or_reparse(path) or not path.is_dir() or any(path.iterdir()):
        return False
    entry = entries_by_path(final_inventory).get(".agents")
    return isinstance(entry, dict) and entry.get("type") == "directory" and entry.get("size") == 0


def _validate_outcome(entry: dict[str, Any], index: int, version: str, outcome: ValidatorOutcome, policy: ValidatorPolicy) -> None:
    if outcome.validatorId != entry["id"] or outcome.validatorType != entry["type"]:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator result identity mismatch.")
    if not _is_exact_non_negative_int(outcome.validatorIndex) or outcome.validatorIndex != index or outcome.validatorVersion != version:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator result index/version mismatch.")
    if outcome.status not in {item.value for item in ValidatorStatus}:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator status is invalid.")
    if outcome.status == ValidatorStatus.PASS.value and outcome.code is not None:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "PASS validator result cannot include a code.")
    if outcome.status == ValidatorStatus.FAIL.value:
        allowed = VALIDATOR_FAIL_CODES_BY_TYPE.get(entry["type"], frozenset())
        if outcome.code not in allowed:
            raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "FAIL validator result code is not allowed for this validator.")
    if outcome.status == ValidatorStatus.BLOCKED.value and outcome.code not in VALIDATOR_BLOCKED_CODES:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "BLOCKED validator result code is not allowed.")
    if len(outcome.findings) > policy.maxFindings:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator finding count exceeds host cap.")
    for field_name in ("filesRead", "bytesRead", "durationMilliseconds"):
        value = getattr(outcome, field_name)
        if not _is_exact_non_negative_int(value):
            raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", f"{field_name} must be a non-negative integer.")
    if outcome.noSideEffects is not True:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator result must declare noSideEffects=true.")
    if outcome.primaryPath is not None and not _safe_report_path(outcome.primaryPath):
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator result primaryPath is unsafe.")
    if len(set(outcome.relatedPaths)) != len(outcome.relatedPaths) or any(not _safe_report_path(path) for path in outcome.relatedPaths):
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator result relatedPaths are invalid.")
    if "Traceback" in outcome.message or len(outcome.message) > 500:
        raise ValidationFailure("REAL_TASK_VALIDATOR_RESULT_INVALID", "Validator result message is unsafe.")


def _is_exact_non_negative_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _final_verdict(outcomes: list[ValidatorOutcome], integrity: dict[str, Any]) -> str:
    if any(outcome.status == ValidatorStatus.BLOCKED.value for outcome in outcomes):
        return "BLOCKED"
    if (
        integrity.get("workspaceUnchanged") is not True
        or integrity.get("workspaceMatchesTrustedFinal") is not True
        or integrity.get("noValidatorWrites") is not True
        or integrity.get("serviceFilesValid") is not True
        or integrity.get("agentsDirectoryValid") is not True
        or integrity.get("isolatedGitValid") is not True
        or integrity.get("parentGitChanged") is not False
        or integrity.get("parentSourceChanged") is not False
    ):
        return "BLOCKED"
    if any(outcome.status == ValidatorStatus.FAIL.value for outcome in outcomes):
        return "FAIL"
    return "PASS"


def _build_reports(state_history: list[str], context: dict[str, Any], final_change: dict[str, Any], registry_snapshot: dict[str, Any], schema_snapshot: dict[str, Any], plan: list[dict[str, Any]], outcomes: list[ValidatorOutcome], integrity: dict[str, Any], duration: float, final_state: str, final_verdict: str, error_code: str | None, error_message: str | None, warnings: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    pass_count = sum(1 for item in outcomes if item.status == "PASS")
    fail_count = sum(1 for item in outcomes if item.status == "FAIL")
    blocked_count = sum(1 for item in outcomes if item.status == "BLOCKED")
    plan_report = {
        "reportVersion": VALIDATION_REPORT_VERSION,
        "runId": final_change["runId"],
        "taskId": final_change["taskId"],
        "stage": REAL_TASK_VALIDATION_STAGE,
        "trustedContextHash": final_change["trustedContextHash"],
        "changeFinalReportHash": canonical_sha256(final_change),
        "validatorRegistryHash": registry_snapshot["registrySha256"],
        "validationPlan": plan,
        "validationPlanHash": canonical_sha256(plan),
        "validatorCount": len(plan),
        "argumentValidationStatus": "PASS",
        "errorCode": None,
        "errorMessage": None,
    }
    results_report = {
        "reportVersion": VALIDATION_REPORT_VERSION,
        "runId": final_change["runId"],
        "taskId": final_change["taskId"],
        "stage": REAL_TASK_VALIDATION_STAGE,
        "results": [item.to_dict() for item in outcomes],
        "executedCount": len(outcomes),
        "passCount": pass_count,
        "failCount": fail_count,
        "blockedCount": blocked_count,
        "executionComplete": blocked_count == 0 and len(outcomes) == len(plan),
        "errorCode": None,
        "errorMessage": None,
    }
    final = {
        "reportVersion": VALIDATION_REPORT_VERSION,
        "runId": final_change["runId"],
        "taskId": final_change["taskId"],
        "stage": REAL_TASK_VALIDATION_STAGE,
        "stateHistory": state_history,
        "trustedContextHash": final_change["trustedContextHash"],
        "manifestSha256": final_change["manifestSha256"],
        "changeFinalReportHash": canonical_sha256(final_change),
        "validatorRegistry": registry_snapshot,
        "schemaRegistry": schema_snapshot,
        "validatorRegistryReportHash": registry_snapshot["registrySha256"],
        "validationPlanReportHash": canonical_sha256(plan_report),
        "validatorResultsReportHash": canonical_sha256(results_report),
        "validationIntegrityReportHash": canonical_sha256(integrity),
        "validationPlan": plan,
        "validationPlanHash": plan_report["validationPlanHash"],
        "results": results_report["results"],
        "passCount": pass_count,
        "failCount": fail_count,
        "blockedCount": blocked_count,
        "integrity": integrity,
        "codexInvocationCount": 0,
        "modelInvocationStarted": False,
        "sandboxStarted": False,
        "unityStarted": False,
        "networkUsed": False,
        "durationSeconds": duration,
        "finalState": final_state,
        "finalVerdict": final_verdict,
        "errorCode": error_code,
        "errorMessage": error_message,
        "warnings": warnings,
    }
    return {
        "registry": registry_snapshot,
        "plan": plan_report,
        "results": results_report,
        "integrity": integrity,
        "final": final,
    }


def _fallback_report(state_history: list[str], change_analysis_result: Any, duration: float, error_code: str, error_message: str, warnings: list[dict[str, Any]]) -> dict[str, Any]:
    report = getattr(change_analysis_result, "report", change_analysis_result)
    report = report if isinstance(report, dict) else {}
    return {
        "reportVersion": VALIDATION_REPORT_VERSION,
        "runId": report.get("runId", "invalid-run"),
        "taskId": report.get("taskId", "invalid-task"),
        "stage": REAL_TASK_VALIDATION_STAGE,
        "stateHistory": state_history + [ValidationState.REPORTING.value, ValidationState.BLOCKED.value],
        "trustedContextHash": report.get("trustedContextHash"),
        "manifestSha256": report.get("manifestSha256"),
        "changeFinalReportHash": canonical_sha256(report) if report else None,
        "validatorRegistry": None,
        "schemaRegistry": None,
        "validationPlan": [],
        "validationPlanHash": canonical_sha256([]),
        "results": [],
        "passCount": 0,
        "failCount": 0,
        "blockedCount": 1,
        "integrity": {"workspaceUnchanged": False, "workspaceMatchesTrustedFinal": False, "serviceFilesValid": False, "agentsDirectoryValid": False, "isolatedGitValid": False, "parentGitChanged": None, "parentSourceChanged": None, "noValidatorWrites": False, "parentGitFinalSnapshot": None},
        "codexInvocationCount": 0,
        "modelInvocationStarted": False,
        "sandboxStarted": False,
        "unityStarted": False,
        "networkUsed": False,
        "durationSeconds": duration,
        "finalState": "BLOCKED",
        "finalVerdict": "BLOCKED",
        "errorCode": error_code,
        "errorMessage": error_message,
        "warnings": warnings,
    }


def _write_validation_report(path: Path, report: dict[str, Any], policy: ValidatorPolicy, automation_root: Path) -> str:
    if path.exists() or path.is_symlink():
        raise ValidationFailure("REAL_TASK_VALIDATION_REPORT_WRITE_FAILED", "Validation report already exists.")
    schema_name = _schema_for_validation_report(path.name)
    if schema_name:
        errors = validate(report, read_json(automation_root / "schemas" / schema_name))
        if errors:
            raise ValidationFailure("REAL_TASK_VALIDATION_REPORT_INVALID", errors[0])
    semantic_errors = _validate_validation_report_semantics(path.name, report)
    if semantic_errors:
        raise ValidationFailure("REAL_TASK_VALIDATION_REPORT_INVALID", semantic_errors[0])
    if len(canonical_json_bytes(report)) > policy.maxReportBytes:
        raise ValidationFailure("REAL_TASK_VALIDATION_REPORT_WRITE_FAILED", "Validation report exceeds maxReportBytes.")
    expected = canonical_sha256(report)
    write_json_atomic(path, report)
    reread = read_json(path)
    if schema_name:
        errors = validate(reread, read_json(automation_root / "schemas" / schema_name))
        if errors:
            raise ValidationFailure("REAL_TASK_VALIDATION_REPORT_INVALID", errors[0])
    semantic_errors = _validate_validation_report_semantics(path.name, reread)
    if semantic_errors:
        raise ValidationFailure("REAL_TASK_VALIDATION_REPORT_INVALID", semantic_errors[0])
    if canonical_sha256(reread) != expected:
        raise ValidationFailure("REAL_TASK_VALIDATION_REPORT_WRITE_FAILED", "Validation report changed after reread.")
    return expected


def _best_run_dir(change_analysis_result: Any) -> Path | None:
    if isinstance(change_analysis_result, ValidationPrerequisiteHandle):
        return Path(change_analysis_result.runDirectory)
    return None


def _transition(history: list[str], state: ValidationState) -> None:
    value = state.value
    if not history or history[-1] != value:
        history.append(value)


def _safe_report_path(path: str) -> bool:
    return isinstance(path, str) and path and "\\" not in path and not path.startswith("/") and ":" not in path and ".." not in path.split("/")


def _safe_message(message: str) -> str:
    text = str(message).replace("\n", " ")[:500]
    return "validator error" if "Traceback" in text else text


def _integrity_pass(integrity: dict[str, Any]) -> bool:
    return (
        integrity.get("workspaceUnchanged") is True
        and integrity.get("workspaceMatchesTrustedFinal") is True
        and integrity.get("serviceFilesValid") is True
        and integrity.get("agentsDirectoryValid") is True
        and integrity.get("isolatedGitValid") is True
        and integrity.get("parentGitChanged") is False
        and integrity.get("parentSourceChanged") is False
        and integrity.get("noValidatorWrites") is True
    )


def _schema_for_validation_report(name: str) -> str | None:
    return {
        "VALIDATOR_REGISTRY_SNAPSHOT.json": "real_task_validator_registry.schema.json",
        "VALIDATION_PLAN_REPORT.json": "real_task_validation_plan.schema.json",
        "VALIDATOR_RESULTS_REPORT.json": "real_task_validator_results.schema.json",
        "VALIDATION_INTEGRITY_REPORT.json": "real_task_validation_integrity.schema.json",
        "FINAL_VALIDATION_REPORT.json": "real_task_validation_final.schema.json",
        "VALIDATOR_SELF_TEST_REPORT.json": "validator_self_test_report.schema.json",
    }.get(name)


def _validate_validation_report_semantics(name: str, report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("stage") != REAL_TASK_VALIDATION_STAGE:
        errors.append("validation report stage mismatch.")
    if name == "VALIDATOR_REGISTRY_SNAPSHOT.json":
        types = [item.get("type") for item in report.get("validatorTypes", []) if isinstance(item, dict)]
        if tuple(types) != VALIDATOR_TYPES:
            errors.append("validator registry snapshot set/order mismatch.")
        expected = canonical_sha256({key: value for key, value in report.items() if key != "registrySha256"})
        if report.get("registrySha256") != expected:
            errors.append("validator registry hash mismatch.")
    if name == "VALIDATOR_RESULTS_REPORT.json":
        results = report.get("results", [])
        pass_count = sum(1 for item in results if isinstance(item, dict) and item.get("status") == "PASS")
        fail_count = sum(1 for item in results if isinstance(item, dict) and item.get("status") == "FAIL")
        blocked_count = sum(1 for item in results if isinstance(item, dict) and item.get("status") == "BLOCKED")
        if report.get("passCount") != pass_count or report.get("failCount") != fail_count or report.get("blockedCount") != blocked_count or report.get("executedCount") != len(results):
            errors.append("validator result counts do not match results.")
    if name == "VALIDATION_INTEGRITY_REPORT.json":
        expected = "PASS" if _integrity_pass(report) else "BLOCKED"
        if report.get("finalVerdict") != expected:
            errors.append("integrity report final verdict mismatch.")
    if name == "FINAL_VALIDATION_REPORT.json":
        fail_count = report.get("failCount")
        blocked_count = report.get("blockedCount")
        integrity = report.get("integrity", {})
        if report.get("finalVerdict") == "PASS" and (fail_count != 0 or blocked_count != 0 or not _integrity_pass(integrity)):
            errors.append("PASS final validation report is contradictory.")
        if report.get("finalVerdict") == "FAIL" and (not isinstance(fail_count, int) or fail_count <= 0 or blocked_count != 0 or not _integrity_pass(integrity)):
            errors.append("FAIL final validation report is contradictory.")
        if report.get("finalVerdict") == "BLOCKED" and report.get("finalState") != "BLOCKED":
            errors.append("BLOCKED final validation report requires BLOCKED state.")
        if report.get("codexInvocationCount") != 0 or any(report.get(field) is not False for field in ("modelInvocationStarted", "sandboxStarted", "unityStarted", "networkUsed")):
            errors.append("validation final no-execution fields are invalid.")
    return errors


def _freeze(value: Any) -> Any:
    if isinstance(value, dict):
        return MappingProxyType({key: _freeze(deepcopy(item)) for key, item in value.items()})
    if isinstance(value, list):
        return tuple(_freeze(item) for item in value)
    return value


def _mutable_json(value: Any) -> Any:
    if isinstance(value, MappingProxyType):
        return {key: _mutable_json(item) for key, item in value.items()}
    if isinstance(value, tuple):
        return [_mutable_json(item) for item in value]
    if isinstance(value, list):
        return [_mutable_json(item) for item in value]
    if isinstance(value, dict):
        return {key: _mutable_json(item) for key, item in value.items()}
    return value


def _error(code: str, message: str, field: str | None = None) -> RealTaskError:
    return RealTaskError(code, message, field)
