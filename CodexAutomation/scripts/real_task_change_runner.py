from __future__ import annotations

import time
import unicodedata
from pathlib import Path
from typing import Any

from file_utils import read_json, write_json_atomic
from parent_git_snapshot import parent_git_snapshot, snapshots_equal
from real_task_models import RealTaskError
from real_task_workspace import git_fingerprint, validate_service_files
from schema_validator import validate
from task_manifest_validator import parse_real_task_policy
from workspace_change_inventory import build_change_inventory, compare_inventory_hash, entries_by_path
from workspace_change_models import (
    CHANGE_HANDLE_VERSION,
    CHANGE_REPORT_VERSION,
    REAL_TASK_CHANGE_STAGE,
    ChangeAnalysisResult,
    ChangeFailure,
    ChangeState,
    ChangeTrackingHandle,
    canonical_json_bytes,
    canonical_sha256,
    new_handle_capability,
    new_tracking_id,
    utc_now,
)
from workspace_change_policy import evaluate_change_policy, parse_change_policy
from workspace_diff import build_workspace_diff
from workspace_inventory import InventoryError, build_source_inventory, compare_source_inventories


_TRACKING_REGISTRY: dict[str, dict[str, Any]] = {}


def begin_change_tracking(prepared_run_handle: dict[str, Any], root: Path, automation_root: Path, config: dict[str, Any]) -> ChangeTrackingHandle:
    state_history = [ChangeState.PENDING.value]
    _transition(state_history, ChangeState.CONTEXT_REVALIDATING)
    context, final_prep, baseline, source_post, run_dir, workspace = _revalidate_prepared_run(prepared_run_handle, root, automation_root, config)
    real_policy, real_errors = parse_real_task_policy(config)
    change_policy, change_errors = parse_change_policy(config, real_policy)
    if real_errors or change_errors or change_policy is None:
        first = (real_errors + change_errors)[0]
        raise ChangeFailure(first.code, first.message)
    _transition(state_history, ChangeState.BASELINE_REVALIDATING)
    _validate_workspace_containment(root, run_dir, workspace)
    _transition(state_history, ChangeState.CHANGE_BASELINE_INVENTORY)
    service_registry = list(baseline.get("serviceRegistry", []))
    service_valid, service_error = validate_service_files(workspace, service_registry, int(config["realTaskFoundation"]["copyChunkBytes"]))
    if not service_valid:
        raise ChangeFailure(service_error or "REAL_TASK_SERVICE_FILE_INVALID", "Service files are invalid.")
    isolated_git = git_fingerprint(workspace)
    max_entries = int(config["realTaskFoundation"]["maxInventoryEntries"])
    change_baseline = build_change_inventory(workspace, context["runId"], context["taskId"], final_prep["trustedContextHash"], "change_baseline", service_registry, change_policy, max_entries)
    baseline_path = run_dir / "CHANGE_BASELINE_INVENTORY.json"
    baseline_hash = _write_trusted_report(root, automation_root, baseline_path, change_baseline, config, _semantic_context())
    tracking_id = new_tracking_id(context["runId"])
    capability = new_handle_capability()
    tracking_path = run_dir / "CHANGE_TRACKING_REPORT.json"
    tracking_report = {
        "reportVersion": CHANGE_REPORT_VERSION,
        "trackingId": tracking_id,
        "runId": context["runId"],
        "taskId": context["taskId"],
        "stage": REAL_TASK_CHANGE_STAGE,
        "createdAt": utc_now(),
        "trustedContextHash": final_prep["trustedContextHash"],
        "manifestSha256": context["manifestSha256"],
        "workspaceDirectory": str(workspace.resolve(strict=True)),
        "baselineInventoryPath": str(baseline_path.resolve(strict=True)),
        "baselineInventoryHash": baseline_hash,
        "serviceRegistryHash": canonical_sha256(service_registry),
        "isolatedGitFingerprintHash": canonical_sha256(isolated_git),
        "sourcePostInventoryHash": source_post["inventorySha256"],
        "parentGitTrackingStartSnapshot": context["parentGitInitialSnapshot"],
        "noModel": True,
        "noCodex": True,
        "noSandbox": True,
        "noUnity": True,
        "finalized": False,
        "errorCode": None,
        "errorMessage": None,
    }
    tracking_hash = _write_trusted_report(root, automation_root, tracking_path, tracking_report, config, _semantic_context())
    _transition(state_history, ChangeState.TRACKING_READY)
    handle = ChangeTrackingHandle(
        handleVersion=CHANGE_HANDLE_VERSION,
        trackingId=tracking_id,
        capability=capability,
        runId=context["runId"],
        taskId=context["taskId"],
        stage=REAL_TASK_CHANGE_STAGE,
        trustedContextHash=final_prep["trustedContextHash"],
        manifestSha256=context["manifestSha256"],
        workspaceDirectory=str(workspace.resolve(strict=True)),
        runDirectory=str(run_dir.resolve(strict=True)),
        trackingReportPath=str(tracking_path.resolve(strict=True)),
        trackingReportHash=tracking_hash,
        baselineInventoryPath=str(baseline_path.resolve(strict=True)),
        baselineInventoryHash=baseline_hash,
        serviceRegistryHash=canonical_sha256(service_registry),
        isolatedGitFingerprintHash=canonical_sha256(isolated_git),
        sourcePostInventoryHash=source_post["inventorySha256"],
        parentGitTrackingStartSnapshot=context["parentGitInitialSnapshot"],
        noModel=True,
        noCodex=True,
        noSandbox=True,
        noUnity=True,
    )
    _TRACKING_REGISTRY[tracking_id] = {
        "capability": capability,
        "handleObjectId": id(handle),
        "trackingReportHash": tracking_hash,
        "baselineInventoryHash": baseline_hash,
        "finalized": False,
    }
    return handle


def finalize_change_analysis(change_tracking_handle: ChangeTrackingHandle, root: Path, automation_root: Path, config: dict[str, Any]) -> ChangeAnalysisResult:
    started = time.monotonic()
    state_history = [ChangeState.PENDING.value]
    error_code: str | None = None
    error_message: str | None = None
    final_state = ChangeState.FAILED.value
    final_verdict = "FAIL"
    report_path: Path | None = None
    warnings: list[dict[str, Any]] = []
    try:
        _transition(state_history, ChangeState.CONTEXT_REVALIDATING)
        handle = _validate_handle(change_tracking_handle)
        run_dir = Path(handle.runDirectory)
        workspace = Path(handle.workspaceDirectory)
        _validate_registry_handle(handle)
        context = read_json(run_dir / "TRUSTED_RUN_CONTEXT.json")
        final_prep = read_json(run_dir / "FINAL_WORKSPACE_PREPARATION_REPORT.json")
        baseline = read_json(Path(handle.baselineInventoryPath))
        tracking_report = read_json(Path(handle.trackingReportPath))
        _validate_tracking_report_for_handle(handle, tracking_report)
        _validate_handle_matches_context(handle, context, final_prep, baseline)
        _consume_registry_handle(handle)
        if (run_dir / "FINAL_CHANGE_ANALYSIS_REPORT.json").exists():
            raise ChangeFailure("REAL_TASK_CHANGE_REPORT_WRITE_FAILED", "Final change analysis report already exists.")
        real_policy, real_errors = parse_real_task_policy(config)
        change_policy, change_errors = parse_change_policy(config, real_policy)
        if real_errors or change_errors or change_policy is None:
            first = (real_errors + change_errors)[0]
            raise ChangeFailure(first.code, first.message)
        service_registry = list(baseline.get("serviceRegistry", []))
        _transition(state_history, ChangeState.FINAL_INVENTORY)
        max_entries = int(config["realTaskFoundation"]["maxInventoryEntries"])
        final_inventory = build_change_inventory(workspace, handle.runId, handle.taskId, handle.trustedContextHash, "change_final", service_registry, change_policy, max_entries)
        semantic_context = _semantic_context(handle=handle, tracking_report=tracking_report)
        final_inventory_hash = _write_trusted_report(root, automation_root, run_dir / "CHANGE_FINAL_INVENTORY.json", final_inventory, config, semantic_context)
        _transition(state_history, ChangeState.SERVICE_GUARDS)
        service_valid, _ = validate_service_files(workspace, service_registry, int(config["realTaskFoundation"]["copyChunkBytes"]))
        isolated_git_valid = canonical_sha256(git_fingerprint(workspace)) == handle.isolatedGitFingerprintHash
        _transition(state_history, ChangeState.DIFFING)
        semantic_context["baselineInventory"] = baseline
        semantic_context["finalInventory"] = final_inventory
        diff = build_workspace_diff(baseline, final_inventory)
        diff_hash = _write_trusted_report(root, automation_root, run_dir / "WORKSPACE_DIFF_REPORT.json", diff, config, semantic_context)
        _transition(state_history, ChangeState.LIMITS_ENFORCING)
        _transition(state_history, ChangeState.SCOPE_ENFORCING)
        _transition(state_history, ChangeState.BINARY_TEXT_ENFORCING)
        _transition(state_history, ChangeState.EXPECTED_OUTPUTS_CHECKING)
        _transition(state_history, ChangeState.PARENT_VERIFYING)
        parent_final, git_errors = parent_git_snapshot(root)
        if git_errors:
            first = git_errors[0]
            raise ChangeFailure(first.code, first.message)
        parent_git_changed = not snapshots_equal(handle.parentGitTrackingStartSnapshot, parent_final)
        source_schema = read_json(automation_root / "schemas" / "real_task_source_inventory.schema.json")
        parent_source_changed = _parent_source_changed(root, run_dir, context, handle.sourcePostInventoryHash, int(config["realTaskFoundation"]["maxInventoryEntries"]), int(config["realTaskFoundation"]["copyChunkBytes"]), source_schema)
        policy_context = dict(context)
        task_json = read_json(workspace / "task.json")
        policy_context["expectedOutputs"] = task_json.get("expectedOutputs", [])
        policy_report = evaluate_change_policy(baseline, final_inventory, diff, policy_context, change_policy, parent_git_changed, parent_source_changed, isolated_git_valid and service_valid)
        semantic_context["diff"] = diff
        policy_hash = _write_trusted_report(root, automation_root, run_dir / "CHANGE_POLICY_REPORT.json", policy_report, config, semantic_context)
        final_verdict = policy_report["finalPolicyVerdict"]
        final_state = ChangeState.COMPLETED.value if final_verdict == "PASS" else (ChangeState.BLOCKED.value if final_verdict == "BLOCKED" else ChangeState.FAILED.value)
        _transition(state_history, ChangeState.REPORTING)
        _transition(state_history, ChangeState.COMPLETED if final_verdict == "PASS" else (ChangeState.BLOCKED if final_verdict == "BLOCKED" else ChangeState.FAILED))
        final_report = _final_report(
            handle,
            state_history,
            baseline["inventorySha256"],
            final_inventory_hash,
            diff_hash,
            policy_hash,
            not parent_source_changed,
            parent_git_changed,
            parent_source_changed,
            service_valid,
            isolated_git_valid,
            policy_report,
            diff["summary"],
            final_state,
            final_verdict,
            None,
            None,
            warnings,
        )
        report_path = run_dir / "FINAL_CHANGE_ANALYSIS_REPORT.json"
        semantic_context["policyReport"] = policy_report
        semantic_context["diffReportHash"] = diff_hash
        semantic_context["policyReportHash"] = policy_hash
        _write_trusted_report(root, automation_root, report_path, final_report, config, semantic_context)
        return ChangeAnalysisResult(final_verdict, final_state, final_report, str(report_path))
    except ChangeFailure as exc:
        error_code = exc.code
        error_message = exc.message
        final_verdict = exc.verdict
        final_state = ChangeState.BLOCKED.value if exc.verdict == "BLOCKED" else ChangeState.FAILED.value
    except InventoryError as exc:
        error_code = exc.code
        error_message = exc.message
        final_verdict = "BLOCKED"
        final_state = ChangeState.BLOCKED.value
    except Exception as exc:
        error_code = "REAL_TASK_INTERNAL_ERROR"
        error_message = f"{type(exc).__name__}: {exc}"
        final_verdict = "BLOCKED"
        final_state = ChangeState.BLOCKED.value
    handle_dict = change_tracking_handle.to_dict() if isinstance(change_tracking_handle, ChangeTrackingHandle) else {}
    fallback = {
        "reportVersion": CHANGE_REPORT_VERSION,
        "runId": handle_dict.get("runId", "invalid-run"),
        "taskId": handle_dict.get("taskId", "invalid-task"),
        "stage": REAL_TASK_CHANGE_STAGE,
        "stateHistory": state_history + [ChangeState.REPORTING.value, final_state],
        "trustedContextHash": handle_dict.get("trustedContextHash"),
        "manifestSha256": handle_dict.get("manifestSha256"),
        "changeBaselineInventoryHash": handle_dict.get("baselineInventoryHash"),
        "changeFinalInventoryHash": None,
        "diffReportHash": None,
        "policyReportHash": None,
        "sourcePrePostMatch": False,
        "parentGitChanged": None,
        "parentSourceChanged": None,
        "serviceFilesValid": False,
        "isolatedGitValid": False,
        "writeScopeValid": False,
        "deleteScopeValid": False,
        "changeLimitsValid": False,
        "binaryTextPolicyValid": False,
        "expectedOutputsValid": False,
        "diffSummary": _empty_summary(),
        "codexInvocationCount": 0,
        "modelInvocationStarted": False,
        "sandboxStarted": False,
        "unityStarted": False,
        "networkUsed": False,
        "durationSeconds": round(time.monotonic() - started, 3),
        "finalState": final_state,
        "finalVerdict": final_verdict,
        "errorCode": error_code,
        "errorMessage": error_message,
        "warnings": warnings,
    }
    try:
        if isinstance(change_tracking_handle, ChangeTrackingHandle):
            report_path = Path(change_tracking_handle.runDirectory) / "FINAL_CHANGE_ANALYSIS_REPORT.json"
            _write_trusted_report(root, automation_root, report_path, fallback, config, _semantic_context())
    except Exception:
        warnings.append({"code": "REAL_TASK_CHANGE_REPORT_WRITE_FAILED", "message": "Final change analysis report could not be written.", "path": str(report_path) if report_path else None})
    return ChangeAnalysisResult(final_verdict, final_state, fallback, str(report_path) if report_path else None)


def _revalidate_prepared_run(prepared_run_handle: dict[str, Any], root: Path, automation_root: Path, config: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any], dict[str, Any], Path, Path]:
    if not isinstance(prepared_run_handle, dict):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Prepared run handle must be the B-A final report.")
    if prepared_run_handle.get("finalVerdict") != "PASS" or prepared_run_handle.get("workspaceReady") is not True:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Prepared run did not PASS with workspaceReady=true.")
    run_id = str(prepared_run_handle.get("runId"))
    run_dir = root / config["realTaskFoundation"]["runtimeRoot"] / run_id
    workspace = run_dir / "workspace"
    context = read_json(run_dir / "TRUSTED_RUN_CONTEXT.json")
    final_prep = read_json(run_dir / "FINAL_WORKSPACE_PREPARATION_REPORT.json")
    baseline = read_json(run_dir / "WORKSPACE_BASELINE_INVENTORY.json")
    source_post = read_json(run_dir / "SOURCE_POST_INVENTORY.json")
    errors = validate(final_prep, read_json(automation_root / "schemas" / "real_task_workspace_final.schema.json"))
    if errors:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", errors[0])
    source_schema = read_json(automation_root / "schemas" / "real_task_source_inventory.schema.json")
    _validate_ba_final_pass_contract(prepared_run_handle, context, final_prep, baseline, source_post, workspace, source_schema)
    if context.get("runId") != run_id or final_prep.get("runId") != run_id:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Run identity mismatch.")
    if final_prep.get("trustedContextHash") != prepared_run_handle.get("trustedContextHash"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Trusted context hash mismatch.")
    if final_prep.get("workspaceBaselineInventoryHash") != baseline.get("inventorySha256"):
        raise ChangeFailure("REAL_TASK_CHANGE_BASELINE_INVALID", "B-A baseline hash mismatch.")
    return context, final_prep, baseline, source_post, run_dir, workspace


def _validate_handle(handle: ChangeTrackingHandle) -> ChangeTrackingHandle:
    if not isinstance(handle, ChangeTrackingHandle):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Finalize requires a ChangeTrackingHandle.")
    if handle.handleVersion != CHANGE_HANDLE_VERSION or handle.stage != REAL_TASK_CHANGE_STAGE:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Change handle version or stage is invalid.")
    if any(getattr(handle, field) is not True for field in ("noModel", "noCodex", "noSandbox", "noUnity")):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Change handle no-execution flags are invalid.")
    return handle


def _validate_registry_handle(handle: ChangeTrackingHandle) -> None:
    entry = _TRACKING_REGISTRY.get(handle.trackingId)
    if not entry:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Change handle is not bound to this process registry.")
    if entry.get("finalized"):
        raise ChangeFailure("REAL_TASK_CHANGE_HANDLE_REUSED", "Change handle was already consumed.")
    if entry.get("capability") != handle.capability or entry.get("handleObjectId") != id(handle):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Change handle capability or object identity mismatch.")
    if entry.get("trackingReportHash") != handle.trackingReportHash or entry.get("baselineInventoryHash") != handle.baselineInventoryHash:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Change handle registry hash mismatch.")


def _consume_registry_handle(handle: ChangeTrackingHandle) -> None:
    entry = _TRACKING_REGISTRY.get(handle.trackingId)
    if entry is not None:
        entry["finalized"] = True


def _validate_handle_matches_context(handle: ChangeTrackingHandle, context: dict[str, Any], final_prep: dict[str, Any], baseline: dict[str, Any]) -> None:
    if handle.runId != context.get("runId") or handle.taskId != context.get("taskId"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Handle run/task identity mismatch.")
    if handle.trustedContextHash != final_prep.get("trustedContextHash"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Handle trusted context hash mismatch.")
    if handle.manifestSha256 != context.get("manifestSha256"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Handle manifest hash mismatch.")
    if handle.baselineInventoryHash != baseline.get("inventorySha256") or not compare_inventory_hash(baseline):
        raise ChangeFailure("REAL_TASK_CHANGE_BASELINE_INVALID", "Change baseline artifact was substituted.")
    if handle.serviceRegistryHash != canonical_sha256(baseline.get("serviceRegistry", [])):
        raise ChangeFailure("REAL_TASK_CHANGE_BASELINE_INVALID", "Service registry hash mismatch.")
    if Path(handle.workspaceDirectory).resolve(strict=True) != Path(context["workspaceDirectory"]).resolve(strict=True):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Workspace path changed.")


def _validate_tracking_report_for_handle(handle: ChangeTrackingHandle, report: dict[str, Any]) -> None:
    if canonical_sha256(report) != handle.trackingReportHash:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Tracking report hash mismatch.")
    expected = {
        "trackingId": handle.trackingId,
        "runId": handle.runId,
        "taskId": handle.taskId,
        "stage": REAL_TASK_CHANGE_STAGE,
        "trustedContextHash": handle.trustedContextHash,
        "manifestSha256": handle.manifestSha256,
        "workspaceDirectory": handle.workspaceDirectory,
        "baselineInventoryPath": handle.baselineInventoryPath,
        "baselineInventoryHash": handle.baselineInventoryHash,
        "serviceRegistryHash": handle.serviceRegistryHash,
        "isolatedGitFingerprintHash": handle.isolatedGitFingerprintHash,
        "sourcePostInventoryHash": handle.sourcePostInventoryHash,
        "finalized": False,
    }
    for key, value in expected.items():
        if report.get(key) != value:
            raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", f"Tracking report field mismatch: {key}.")
    if any(report.get(field) is not True for field in ("noModel", "noCodex", "noSandbox", "noUnity")):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Tracking report no-execution flags are invalid.")


def _parent_source_changed(root: Path, run_dir: Path, context: dict[str, Any], source_post_hash: str, max_entries: int, chunk_bytes: int, source_schema: dict[str, Any]) -> bool:
    source_post = read_json(run_dir / "SOURCE_POST_INVENTORY.json")
    source_limits = context.get("sourceLimits")
    _validate_source_post_report(context, source_post, source_post_hash, source_schema, source_limits)
    source_paths = _top_level_inventory_paths(source_post)
    inventory_max_entries = source_limits["maxInventoryEntries"] if isinstance(source_limits, dict) and _is_positive_integer(source_limits.get("maxInventoryEntries")) else max_entries
    inventory = build_source_inventory(root, source_paths, inventory_max_entries, chunk_bytes)
    inventory["runId"] = context.get("runId")
    inventory["taskId"] = context.get("taskId")
    _validate_source_post_report(context, inventory, inventory.get("inventorySha256"), source_schema, source_limits)
    return not compare_source_inventories(source_post, inventory)


def _top_level_inventory_paths(inventory: dict[str, Any]) -> list[str]:
    paths = sorted(entry["path"] for entry in inventory.get("entries", []) if isinstance(entry, dict))
    result: list[str] = []
    for path in paths:
        if not any(path != existing and path.startswith(f"{existing}/") for existing in result):
            result.append(path)
    return result


def _validate_ba_final_pass_contract(prepared_run_handle: dict[str, Any], context: dict[str, Any], final_prep: dict[str, Any], baseline: dict[str, Any], source_post: dict[str, Any], workspace: Path, source_schema: dict[str, Any]) -> None:
    if prepared_run_handle.get("runId") != final_prep.get("runId") or prepared_run_handle.get("taskId") != final_prep.get("taskId"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Prepared run identity does not match persisted B-A final report.")
    required_true = (
        "workspaceCreated",
        "workspaceReady",
        "sourceCopied",
        "sourceCopyVerified",
        "sourcePrePostMatch",
        "serviceFilesValid",
        "isolatedGitValid",
    )
    for field in required_true:
        if final_prep.get(field) is not True:
            raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", f"Persisted B-A final report does not satisfy PASS contract: {field}.")
    required_false = ("parentGitChanged", "modelInvocationStarted", "sandboxStarted", "unityStarted", "networkUsed")
    for field in required_false:
        if final_prep.get(field) is not False:
            raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", f"Persisted B-A final report does not satisfy PASS contract: {field}.")
    if final_prep.get("codexInvocationCount") != 0 or final_prep.get("finalState") != "COMPLETED" or final_prep.get("finalVerdict") != "PASS":
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Persisted B-A final report is not a completed PASS.")
    if final_prep.get("errorCode") is not None or final_prep.get("errorMessage") is not None:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "Persisted B-A final report contains an error.")
    context_hash = canonical_sha256(context)
    if final_prep.get("trustedContextHash") != context_hash or prepared_run_handle.get("trustedContextHash") != context_hash:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "B-A trusted context hash mismatch.")
    if final_prep.get("manifestSha256") != context.get("manifestSha256"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "B-A manifest hash mismatch.")
    if final_prep.get("workspaceBaselineInventoryHash") != baseline.get("inventorySha256"):
        raise ChangeFailure("REAL_TASK_CHANGE_BASELINE_INVALID", "B-A baseline inventory hash mismatch.")
    if final_prep.get("sourceInventoryHash") != source_post.get("inventorySha256"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "B-A source post inventory hash mismatch.")
    if Path(context["workspaceDirectory"]).resolve(strict=True) != workspace.resolve(strict=True):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "B-A workspace path mismatch.")
    _validate_source_post_report(context, source_post, source_post["inventorySha256"], source_schema, context.get("sourceLimits"))
    _validate_service_registry_semantics(baseline.get("serviceRegistry", []))


def _validate_source_post_report(context: dict[str, Any], source_post: dict[str, Any], expected_hash: str, source_schema: dict[str, Any], source_limits: Any) -> None:
    if not isinstance(source_post, dict):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "B-A source post inventory must be an object.")
    schema_errors = validate(source_post, source_schema)
    if schema_errors:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", f"B-A source post inventory schema is invalid: {schema_errors[0]}")
    if source_post.get("runId") != context.get("runId") or source_post.get("taskId") != context.get("taskId"):
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "B-A source post inventory identity mismatch.")
    if source_post.get("inventorySha256") != expected_hash:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", "B-A source post inventory hash mismatch.")
    errors = validate_source_inventory_report(source_post, source_limits)
    if errors:
        raise ChangeFailure("REAL_TASK_CHANGE_CONTEXT_MISMATCH", f"B-A source post inventory is invalid: {errors[0]}")


def _validate_service_registry_semantics(registry: Any) -> None:
    if not isinstance(registry, list):
        raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Service registry must be an array.")
    seen: set[str] = set()
    canonical_seen: set[str] = set()
    required = {"AGENTS.md", ".agents", "task.json", "effective_policy.json", ".git"}
    for item in registry:
        if not isinstance(item, dict):
            raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Service registry item must be an object.")
        if item.get("mutability") != "immutable":
            raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Service registry item is not immutable.")
        path = item.get("workspacePath")
        if path is None:
            continue
        if not isinstance(path, str) or not path:
            raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Service registry workspacePath is invalid.")
        key = path.casefold()
        if path in seen or key in canonical_seen:
            raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Duplicate service registry path.")
        seen.add(path)
        canonical_seen.add(key)
        expected_type = item.get("expectedType")
        if expected_type == "file" and not isinstance(item.get("expectedSha256"), str):
            raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Service file hash is missing.")
        if expected_type == "directory" and item.get("expectedSha256") is not None:
            raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Service directory hash must be null.")
    if not required.issubset(seen):
        raise ChangeFailure("REAL_TASK_SERVICE_FILE_INVALID", "Required service registry entries are missing.")


def _write_trusted_report(root: Path, automation_root: Path, path: Path, payload: dict[str, Any], config: dict[str, Any], semantic_context: dict[str, Any]) -> str:
    if path.exists() or path.is_symlink():
        raise ChangeFailure("REAL_TASK_CHANGE_REPORT_WRITE_FAILED", f"Report already exists: {path.name}.")
    schema_name = _schema_for_report(path.name)
    if schema_name:
        errors = validate(payload, read_json(automation_root / "schemas" / schema_name))
        if errors:
            raise ChangeFailure("REAL_TASK_CHANGE_REPORT_INVALID", errors[0])
    semantic_errors = _validate_report_semantics(path.name, payload, semantic_context)
    if semantic_errors:
        raise ChangeFailure("REAL_TASK_CHANGE_REPORT_INVALID", semantic_errors[0])
    max_bytes = _max_report_bytes(path.name, config)
    if len(canonical_json_bytes(payload)) > max_bytes:
        raise ChangeFailure("REAL_TASK_CHANGE_REPORT_WRITE_FAILED", f"Report exceeds size cap: {path.name}.")
    expected_hash = canonical_sha256(payload)
    try:
        write_json_atomic(path, payload)
    except OSError as exc:
        raise ChangeFailure("REAL_TASK_CHANGE_REPORT_WRITE_FAILED", f"Report write failed: {path.name}.") from exc
    try:
        reread = read_json(path)
    except Exception as exc:
        raise ChangeFailure("REAL_TASK_CHANGE_REPORT_INVALID", f"Report reread failed: {path.name}.") from exc
    if schema_name:
        errors = validate(reread, read_json(automation_root / "schemas" / schema_name))
        if errors:
            raise ChangeFailure("REAL_TASK_CHANGE_REPORT_INVALID", errors[0])
    semantic_errors = _validate_report_semantics(path.name, reread, semantic_context)
    if semantic_errors:
        raise ChangeFailure("REAL_TASK_CHANGE_REPORT_INVALID", semantic_errors[0])
    if canonical_sha256(reread) != expected_hash:
        raise ChangeFailure("REAL_TASK_CHANGE_REPORT_INVALID", "Report hash changed after reread.")
    return reread.get("inventorySha256") or reread.get("diffReportSha256") or expected_hash


def _semantic_context(**values: Any) -> dict[str, Any]:
    return dict(values)


def _validate_report_semantics(name: str, payload: dict[str, Any], context: dict[str, Any]) -> list[str]:
    if name in {"CHANGE_BASELINE_INVENTORY.json", "CHANGE_FINAL_INVENTORY.json"}:
        return validate_change_inventory_report(payload)
    if name == "WORKSPACE_DIFF_REPORT.json":
        return validate_workspace_diff_report(payload, context.get("baselineInventory"), context.get("finalInventory"))
    if name == "CHANGE_POLICY_REPORT.json":
        return validate_change_policy_report(payload)
    if name == "FINAL_CHANGE_ANALYSIS_REPORT.json":
        return validate_final_change_analysis_report(payload, context)
    if name == "CHANGE_TRACKING_REPORT.json":
        return validate_change_tracking_report(payload)
    return []


def validate_change_inventory_report(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    entries = report.get("entries")
    if not isinstance(entries, list):
        return ["entries must be an array."]
    paths = [entry.get("path") for entry in entries if isinstance(entry, dict)]
    if paths != sorted(paths):
        errors.append("inventory entries must be sorted by path.")
    if len(paths) != len(set(paths)):
        errors.append("inventory paths must be unique.")
    canonical: set[str] = set()
    file_count = 0
    directory_count = 0
    total_bytes = 0
    for entry in entries:
        if not isinstance(entry, dict):
            errors.append("inventory entry must be an object.")
            continue
        path = entry.get("path")
        key = entry.get("canonicalPathKey")
        if not isinstance(path, str) or key != _canonical_path_key(path):
            errors.append("canonicalPathKey must match normalized path casefold.")
        if key in canonical:
            errors.append("inventory canonical paths must be unique.")
        canonical.add(str(key))
        entry_type = entry.get("type")
        size = entry.get("size")
        sha = entry.get("sha256")
        if entry_type == "file":
            file_count += 1
            if not isinstance(size, int) or size < 0 or not isinstance(sha, str) or len(sha) != 64:
                errors.append("file entry size/hash contract is invalid.")
            else:
                total_bytes += size
        elif entry_type == "directory":
            directory_count += 1
            if size != 0 or sha is not None:
                errors.append("directory entry size/hash contract is invalid.")
    if report.get("fileCount") != file_count:
        errors.append("fileCount does not match entries.")
    if report.get("directoryCount") != directory_count:
        errors.append("directoryCount does not match entries.")
    if report.get("totalBytes") != total_bytes:
        errors.append("totalBytes does not match entries.")
    if report.get("complete") is not True or report.get("errorCode") is not None or report.get("errorMessage") is not None:
        errors.append("trusted inventory report must be complete without errors.")
    expected_hash = canonical_sha256({key: value for key, value in report.items() if key != "inventorySha256"})
    if report.get("inventorySha256") != expected_hash:
        errors.append("inventorySha256 does not match canonical payload.")
    try:
        _validate_service_registry_semantics(report.get("serviceRegistry", []))
    except ChangeFailure as exc:
        errors.append(exc.message)
    return errors


def validate_source_inventory_report(report: dict[str, Any], source_limits: dict[str, Any] | None = None) -> list[str]:
    errors: list[str] = []
    if report.get("inventoryVersion") != 1 or report.get("rootKind") != "source":
        errors.append("source inventory identity fields are invalid.")
    if report.get("complete") is not True or report.get("errors") != []:
        errors.append("source inventory must be complete without errors.")
    entries = report.get("entries")
    if not isinstance(entries, list):
        return errors + ["source inventory entries must be an array."]
    paths = [entry.get("path") for entry in entries if isinstance(entry, dict)]
    if paths != sorted(paths):
        errors.append("source inventory entries must be sorted by path.")
    if len(paths) != len(set(paths)):
        errors.append("source inventory paths must be unique.")
    canonical_seen: set[str] = set()
    file_count = 0
    directory_count = 0
    total_bytes = 0
    for entry in entries:
        if not isinstance(entry, dict):
            errors.append("source inventory entry must be an object.")
            continue
        path = entry.get("path")
        if not isinstance(path, str) or not path:
            errors.append("source inventory path must be non-empty text.")
            continue
        canonical = _canonical_path_key(path)
        if canonical in canonical_seen:
            errors.append("source inventory canonical paths must be unique.")
        canonical_seen.add(canonical)
        entry_type = entry.get("type")
        size = entry.get("size")
        sha = entry.get("sha256")
        if entry.get("symlink") is not False or entry.get("reparse") is not False:
            errors.append("source inventory cannot contain symlink or reparse entries.")
        if entry_type == "file":
            file_count += 1
            if not isinstance(size, int) or isinstance(size, bool) or size < 0 or not _is_sha256(sha):
                errors.append("source file entry size/hash contract is invalid.")
            else:
                total_bytes += size
        elif entry_type == "directory":
            directory_count += 1
            if size != 0 or sha is not None:
                errors.append("source directory entry size/hash contract is invalid.")
        else:
            errors.append("source entry type is invalid.")
        if not isinstance(entry.get("extension"), str) or not isinstance(entry.get("serviceClassification"), str) or not isinstance(entry.get("sourceRoot"), str):
            errors.append("source entry metadata is invalid.")
    for counter in ("fileCount", "directoryCount", "totalBytes"):
        if not _is_exact_nonnegative_int(report.get(counter)):
            errors.append(f"source {counter} must be a non-negative integer.")
    if report.get("fileCount") != file_count:
        errors.append("source fileCount does not match entries.")
    if report.get("directoryCount") != directory_count:
        errors.append("source directoryCount does not match entries.")
    if report.get("totalBytes") != total_bytes:
        errors.append("source totalBytes does not match entries.")
    if source_limits is not None:
        if not isinstance(source_limits, dict):
            errors.append("source limits must be an object.")
        else:
            limit_fields = ("maxSourceFiles", "maxSourceBytes", "maxInventoryEntries")
            for field in limit_fields:
                if not _is_positive_integer(source_limits.get(field)):
                    errors.append(f"source limit {field} must be a positive integer.")
            if "maxPathLength" in source_limits and not _is_positive_integer(source_limits.get("maxPathLength")):
                errors.append("source limit maxPathLength must be a positive integer.")
            max_files = source_limits.get("maxSourceFiles")
            max_bytes = source_limits.get("maxSourceBytes")
            max_entries = source_limits.get("maxInventoryEntries")
            max_path = source_limits.get("maxPathLength")
            if _is_positive_integer(max_files) and file_count > max_files:
                errors.append("source fileCount exceeds source limits.")
            if _is_positive_integer(max_bytes) and total_bytes > max_bytes:
                errors.append("source totalBytes exceeds source limits.")
            if _is_positive_integer(max_entries) and len(entries) > max_entries:
                errors.append("source entries exceed source limits.")
            if _is_positive_integer(max_path) and any(isinstance(path, str) and len(path) > max_path for path in paths):
                errors.append("source path length exceeds source limits.")
    hash_payload = {key: value for key, value in report.items() if key not in {"inventorySha256", "runId", "taskId"}}
    if report.get("inventorySha256") != canonical_sha256(hash_payload):
        errors.append("source inventorySha256 does not match canonical payload.")
    return errors


def _is_exact_nonnegative_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _is_positive_integer(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


def validate_workspace_diff_report(report: dict[str, Any], baseline_inventory: dict[str, Any] | None = None, final_inventory: dict[str, Any] | None = None) -> list[str]:
    errors: list[str] = []
    categories = report.get("categories", {})
    if report.get("renameDetection") is not False or report.get("deterministic") is not True:
        errors.append("diff report must be deterministic with renameDetection=false.")
    if report.get("errorCode") is not None or report.get("errorMessage") is not None:
        errors.append("diff report must not contain errors.")
    baseline_entries = entries_by_path(baseline_inventory) if isinstance(baseline_inventory, dict) else None
    final_entries = entries_by_path(final_inventory) if isinstance(final_inventory, dict) else None
    seen: set[str] = set()
    for category in ("added", "modified", "deleted", "typeChanged", "unchanged"):
        items = categories.get(category, [])
        paths = [item.get("path") for item in items if isinstance(item, dict)]
        if paths != sorted(paths):
            errors.append(f"{category} entries must be sorted.")
        for item in items:
            if not isinstance(item, dict):
                errors.append(f"{category} entry must be an object.")
                continue
            path = item.get("path")
            if path in seen:
                errors.append("path appears in multiple changed categories.")
            seen.add(str(path))
            if item.get("changeType") != category:
                errors.append("changeType does not match category.")
            before = item.get("before")
            after = item.get("after")
            if before is not None and not _valid_diff_side(before):
                errors.append("baseline side entry contract is invalid.")
            if after is not None and not _valid_diff_side(after):
                errors.append("final side entry contract is invalid.")
            if before and before.get("path") != path:
                errors.append("baseline side path does not match entry path.")
            if after and after.get("path") != path:
                errors.append("final side path does not match entry path.")
            expected_key = (after or before or {}).get("canonicalPathKey")
            if item.get("canonicalPathKey") != expected_key:
                errors.append("entry canonicalPathKey does not match side evidence.")
            if item.get("baselineType") != (before.get("type") if before else None):
                errors.append("baselineType does not match before side.")
            if item.get("finalType") != (after.get("type") if after else None):
                errors.append("finalType does not match after side.")
            if item.get("baselineSize") != (before.get("size") if before else None):
                errors.append("baselineSize does not match before side.")
            if item.get("finalSize") != (after.get("size") if after else None):
                errors.append("finalSize does not match after side.")
            if item.get("baselineSha256") != (before.get("sha256") if before else None):
                errors.append("baselineSha256 does not match before side.")
            if item.get("finalSha256") != (after.get("sha256") if after else None):
                errors.append("finalSha256 does not match after side.")
            if item.get("baselineContentKind") != (before.get("contentKind") if before else None):
                errors.append("baselineContentKind does not match before side.")
            if item.get("finalContentKind") != (after.get("contentKind") if after else None):
                errors.append("finalContentKind does not match after side.")
            if not isinstance(item.get("service"), bool) or not isinstance(item.get("violationCodes"), list):
                errors.append("entry service/violationCodes contract is invalid.")
            if category == "unchanged":
                if item.get("allowedWrite") is not None or item.get("allowedDelete") is not None:
                    errors.append("unchanged entry permissions must be null.")
            elif not isinstance(item.get("allowedWrite"), bool) or not isinstance(item.get("allowedDelete"), bool):
                errors.append("changed entry permissions must be boolean.")
            if category == "added" and before is not None:
                errors.append("added entry must not have baseline side.")
            if category == "deleted" and after is not None:
                errors.append("deleted entry must not have final side.")
            if category == "modified":
                if not before or not after or before.get("type") != after.get("type") or before.get("sha256") == after.get("sha256"):
                    errors.append("modified entry side contract is invalid.")
            if category == "typeChanged" and (not before or not after or before.get("type") == after.get("type")):
                errors.append("typeChanged entry side contract is invalid.")
            if category == "unchanged" and not _unchanged_sides_match(before, after):
                errors.append("unchanged entry side contract is invalid.")
            if baseline_entries is not None and final_entries is not None:
                errors.extend(_validate_diff_entry_against_inventories(category, item, baseline_entries, final_entries))
    if baseline_entries is not None and final_entries is not None:
        expected_paths = set(baseline_entries) | set(final_entries)
        if seen != expected_paths:
            errors.append("diff categories do not cover exactly the baseline/final path union.")
    summary = _recompute_diff_summary(categories)
    if report.get("summary") != summary:
        errors.append("diff summary does not match categories.")
    expected_hash = canonical_sha256({key: value for key, value in report.items() if key != "diffReportSha256"})
    if report.get("diffReportSha256") != expected_hash:
        errors.append("diffReportSha256 does not match canonical payload.")
    return errors


_DIFF_SIDE_FIELDS = (
    "path",
    "canonicalPathKey",
    "type",
    "size",
    "sha256",
    "extension",
    "contentKind",
    "textEncoding",
    "serviceKind",
    "serviceOrigin",
    "expectedMutability",
    "sourceClassification",
    "symlink",
    "reparse",
)


def _inventory_side(entry: dict[str, Any] | None) -> dict[str, Any] | None:
    if entry is None:
        return None
    return {field: entry.get(field) for field in _DIFF_SIDE_FIELDS}


def _valid_diff_side(side: Any) -> bool:
    if not isinstance(side, dict) or set(side) != set(_DIFF_SIDE_FIELDS):
        return False
    path = side.get("path")
    if not isinstance(path, str) or side.get("canonicalPathKey") != _canonical_path_key(path):
        return False
    entry_type = side.get("type")
    size = side.get("size")
    sha = side.get("sha256")
    if side.get("symlink") is not False or side.get("reparse") is not False:
        return False
    if entry_type == "file":
        if not isinstance(size, int) or isinstance(size, bool) or size < 0 or not _is_sha256(sha):
            return False
    elif entry_type == "directory":
        if size != 0 or sha is not None:
            return False
    else:
        return False
    if not isinstance(side.get("extension"), str):
        return False
    if side.get("contentKind") not in {"text", "binary", "directory", "service"}:
        return False
    if side.get("textEncoding") not in {"utf-8", "utf-8-bom", None}:
        return False
    if side.get("serviceKind") is not None and not isinstance(side.get("serviceKind"), str):
        return False
    if side.get("serviceOrigin") is not None and not isinstance(side.get("serviceOrigin"), str):
        return False
    if side.get("expectedMutability") not in {"mutable_task", "immutable_service"}:
        return False
    return isinstance(side.get("sourceClassification"), str)


def _unchanged_sides_match(before: Any, after: Any) -> bool:
    if not isinstance(before, dict) or not isinstance(after, dict):
        return False
    if before.get("path") != after.get("path") or before.get("canonicalPathKey") != after.get("canonicalPathKey"):
        return False
    if before.get("type") != after.get("type"):
        return False
    if before.get("type") == "file":
        if before.get("sha256") != after.get("sha256") or before.get("size") != after.get("size"):
            return False
    elif before.get("type") == "directory":
        if before.get("sha256") is not None or after.get("sha256") is not None:
            return False
    else:
        return False
    metadata = ("extension", "contentKind", "textEncoding", "serviceKind", "serviceOrigin", "expectedMutability", "sourceClassification", "symlink", "reparse")
    return all(before.get(field) == after.get(field) for field in metadata)


def _validate_diff_entry_against_inventories(category: str, item: dict[str, Any], baseline_entries: dict[str, dict[str, Any]], final_entries: dict[str, dict[str, Any]]) -> list[str]:
    errors: list[str] = []
    path = item.get("path")
    baseline_entry = baseline_entries.get(path)
    final_entry = final_entries.get(path)
    before = item.get("before")
    after = item.get("after")
    if before != _inventory_side(baseline_entry):
        errors.append("diff before side does not match baseline inventory.")
    if after != _inventory_side(final_entry):
        errors.append("diff after side does not match final inventory.")
    if category == "added":
        if baseline_entry is not None or final_entry is None:
            errors.append("added entry inventory presence is invalid.")
    elif category == "deleted":
        if baseline_entry is None or final_entry is not None:
            errors.append("deleted entry inventory presence is invalid.")
    elif category == "modified":
        if baseline_entry is None or final_entry is None or baseline_entry.get("type") != final_entry.get("type") or baseline_entry.get("sha256") == final_entry.get("sha256"):
            errors.append("modified entry inventory contract is invalid.")
    elif category == "typeChanged":
        if baseline_entry is None or final_entry is None or baseline_entry.get("type") == final_entry.get("type"):
            errors.append("typeChanged entry inventory contract is invalid.")
    elif category == "unchanged":
        if baseline_entry is None or final_entry is None or not _unchanged_sides_match(before, after):
            errors.append("unchanged entry inventory contract is invalid.")
    return errors


def _canonical_path_key(path: str) -> str:
    return unicodedata.normalize("NFC", path).casefold()


def _is_sha256(value: Any) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(char in "0123456789abcdef" for char in value)


def validate_change_policy_report(report: dict[str, Any]) -> list[str]:
    findings = report.get("findings", [])
    if not isinstance(findings, list):
        return ["findings must be an array."]
    errors: list[str] = []
    for item in findings:
        if not isinstance(item, dict):
            errors.append("finding must be an object.")
            continue
        if not isinstance(item.get("blocking"), bool):
            errors.append("finding blocking must be boolean.")
        category = item.get("category")
        if category in {"service", "isolated_git", "parent_git", "parent_source"} and item.get("blocking") is not True:
            errors.append("trust/safety findings must be blocking.")
    expected = "BLOCKED" if any(item.get("blocking") for item in findings if isinstance(item, dict)) else ("FAIL" if findings else "PASS")
    if report.get("finalPolicyVerdict") != expected:
        errors.append("finalPolicyVerdict does not match findings.")
    return errors


def validate_final_change_analysis_report(report: dict[str, Any], context: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    policy_report = context.get("policyReport")
    diff = context.get("diff")
    if diff and report.get("diffSummary") != diff.get("summary"):
        errors.append("final diffSummary does not match diff report.")
    if policy_report:
        expected_bools = {
            "serviceFilesValid": policy_report.get("serviceGuardStatus") == "PASS",
            "isolatedGitValid": policy_report.get("isolatedGitStatus") == "PASS",
            "writeScopeValid": policy_report.get("writeScopeStatus") == "PASS",
            "deleteScopeValid": policy_report.get("deleteScopeStatus") == "PASS",
            "changeLimitsValid": policy_report.get("limitsStatus") == "PASS",
            "binaryTextPolicyValid": policy_report.get("binaryTextStatus") == "PASS",
            "expectedOutputsValid": policy_report.get("expectedOutputsStatus") == "PASS",
        }
        for field, expected in expected_bools.items():
            if report.get(field) is not expected:
                errors.append(f"{field} does not match policy report.")
    if report.get("finalVerdict") == "PASS":
        pass_fields = ("serviceFilesValid", "isolatedGitValid", "writeScopeValid", "deleteScopeValid", "changeLimitsValid", "binaryTextPolicyValid", "expectedOutputsValid")
        if report.get("finalState") != "COMPLETED" or any(report.get(field) is not True for field in pass_fields):
            errors.append("PASS final report requires completed state and all safety booleans true.")
        if report.get("parentGitChanged") is not False or report.get("parentSourceChanged") is not False:
            errors.append("PASS final report requires unchanged parent Git/source.")
        if report.get("errorCode") is not None or report.get("errorMessage") is not None:
            errors.append("PASS final report cannot contain errors.")
    if report.get("codexInvocationCount") != 0 or any(report.get(field) is not False for field in ("modelInvocationStarted", "sandboxStarted", "unityStarted", "networkUsed")):
        errors.append("final report no-execution fields are invalid.")
    return errors


def validate_change_tracking_report(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("finalized") is not False:
        errors.append("tracking report must be unfinalized when written.")
    if any(report.get(field) is not True for field in ("noModel", "noCodex", "noSandbox", "noUnity")):
        errors.append("tracking report no-execution fields are invalid.")
    for field in ("trackingId", "runId", "taskId", "trustedContextHash", "manifestSha256", "workspaceDirectory", "baselineInventoryPath", "baselineInventoryHash", "serviceRegistryHash", "isolatedGitFingerprintHash", "sourcePostInventoryHash"):
        if not isinstance(report.get(field), str) or not report.get(field):
            errors.append(f"tracking report {field} must be non-empty text.")
    return errors


def _recompute_diff_summary(categories: dict[str, Any]) -> dict[str, int]:
    changed_files: set[str] = set()
    changed_directories: set[str] = set()
    changed_bytes = 0
    deleted_bytes = 0
    for name in ("added", "modified", "deleted", "typeChanged"):
        for item in categories.get(name, []):
            before = item.get("before")
            after = item.get("after")
            path = item["path"]
            before_file = bool(before and before.get("type") == "file")
            after_file = bool(after and after.get("type") == "file")
            if before_file or after_file:
                changed_files.add(path)
            else:
                changed_directories.add(path)
            if name in {"added", "modified", "typeChanged"} and after_file:
                changed_bytes += int(after["size"])
            if name in {"deleted", "typeChanged"} and before_file:
                deleted_bytes += int(before["size"])
    return {
        "totalChangedFiles": len(changed_files),
        "totalChangedDirectories": len(changed_directories),
        "totalChangedPaths": len(changed_files | changed_directories),
        "totalChangedBytes": changed_bytes,
        "totalDeletedBytes": deleted_bytes,
        "totalTouchedBytes": changed_bytes + deleted_bytes,
    }


def _final_report(
    handle: ChangeTrackingHandle,
    state_history: list[str],
    baseline_hash: str,
    final_hash: str,
    diff_hash: str,
    policy_hash: str,
    source_pre_post_match: bool,
    parent_git_changed: bool,
    parent_source_changed: bool,
    service_valid: bool,
    git_valid: bool,
    policy_report: dict[str, Any],
    diff_summary: dict[str, int],
    final_state: str,
    final_verdict: str,
    error_code: str | None,
    error_message: str | None,
    warnings: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "reportVersion": CHANGE_REPORT_VERSION,
        "runId": handle.runId,
        "taskId": handle.taskId,
        "stage": REAL_TASK_CHANGE_STAGE,
        "stateHistory": state_history,
        "trustedContextHash": handle.trustedContextHash,
        "manifestSha256": handle.manifestSha256,
        "changeBaselineInventoryHash": baseline_hash,
        "changeFinalInventoryHash": final_hash,
        "diffReportHash": diff_hash,
        "policyReportHash": policy_hash,
        "sourcePrePostMatch": source_pre_post_match,
        "parentGitChanged": parent_git_changed,
        "parentSourceChanged": parent_source_changed,
        "serviceFilesValid": service_valid and policy_report["serviceGuardStatus"] == "PASS",
        "isolatedGitValid": git_valid and policy_report["isolatedGitStatus"] == "PASS",
        "writeScopeValid": policy_report["writeScopeStatus"] == "PASS",
        "deleteScopeValid": policy_report["deleteScopeStatus"] == "PASS",
        "changeLimitsValid": policy_report["limitsStatus"] == "PASS",
        "binaryTextPolicyValid": policy_report["binaryTextStatus"] == "PASS",
        "expectedOutputsValid": policy_report["expectedOutputsStatus"] == "PASS",
        "diffSummary": diff_summary,
        "codexInvocationCount": 0,
        "modelInvocationStarted": False,
        "sandboxStarted": False,
        "unityStarted": False,
        "networkUsed": False,
        "durationSeconds": 0,
        "finalState": final_state,
        "finalVerdict": final_verdict,
        "errorCode": error_code,
        "errorMessage": error_message,
        "warnings": warnings,
    }


def _validate_workspace_containment(root: Path, run_dir: Path, workspace: Path) -> None:
    try:
        run_dir.resolve(strict=True).relative_to((root / "CodexAutomation" / "runtime").resolve(strict=True))
        workspace.resolve(strict=True).relative_to(run_dir.resolve(strict=True))
    except ValueError as exc:
        raise ChangeFailure("REAL_TASK_RUNTIME_PATH_UNSAFE", "Workspace path escapes trusted run directory.") from exc


def _transition(history: list[str], state: ChangeState) -> None:
    value = state.value
    if not history or history[-1] != value:
        history.append(value)


def _schema_for_report(name: str) -> str | None:
    return {
        "CHANGE_BASELINE_INVENTORY.json": "real_task_change_inventory.schema.json",
        "CHANGE_FINAL_INVENTORY.json": "real_task_change_inventory.schema.json",
        "WORKSPACE_DIFF_REPORT.json": "real_task_workspace_diff.schema.json",
        "CHANGE_POLICY_REPORT.json": "real_task_change_policy.schema.json",
        "FINAL_CHANGE_ANALYSIS_REPORT.json": "real_task_change_final.schema.json",
        "CHANGE_TRACKING_REPORT.json": "real_task_change_tracking.schema.json",
    }.get(name)


def _max_report_bytes(name: str, config: dict[str, Any]) -> int:
    if name == "WORKSPACE_DIFF_REPORT.json":
        return int(config["realTaskChangePolicy"]["maxDiffReportBytes"])
    return int(config["realTaskFoundation"]["maxReportBytes"])


def _empty_summary() -> dict[str, int]:
    return {
        "totalChangedFiles": 0,
        "totalChangedDirectories": 0,
        "totalChangedPaths": 0,
        "totalChangedBytes": 0,
        "totalDeletedBytes": 0,
        "totalTouchedBytes": 0,
    }
