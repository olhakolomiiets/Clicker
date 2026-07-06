from __future__ import annotations

import time
from pathlib import Path
from typing import Any

from file_utils import read_json
from parent_git_snapshot import parent_git_snapshot, snapshots_equal
from real_task_execution_models import RealTaskExecutionFailure, canonical_sha256, new_capability, utc_now
from real_task_execution_policy import parse_execution_policy, parse_execution_self_test_policy
from real_task_execution_self_test_fixture import create_self_test_fixture, expected_changed_paths, self_test_config
from real_task_execution_self_test_models import (
    SELF_TEST_REPORT_NAME,
    SELF_TEST_TASK_ID,
    RealTaskExecutionSelfTestHandle,
    file_sha256,
    new_self_test_run_id,
    snapshot_hash,
    stage,
)
from real_task_report_writer import write_trusted_report
from real_task_role_adapter import CodexRealTaskRoleAdapter
from real_task_runner import _execute_real_task_with_test_adapter
from task_manifest_validator import validate_task_manifest_file


_SELF_TEST_REGISTRY: dict[str, dict[str, Any]] = {}


def run_real_task_execution_self_test(root: Path, automation_root: Path, config: dict[str, Any]) -> dict[str, Any]:
    started = time.monotonic()
    started_at = utc_now()
    self_test_run_id = new_self_test_run_id()
    report: dict[str, Any] | None = None
    fixture: dict[str, Any] | None = None
    production_result = None
    root_before: dict[str, Any] = {}
    root_after: dict[str, Any] = {}
    parent_before: dict[str, Any] = {}
    parent_after: dict[str, Any] = {}
    error_code: str | None = None
    error_message: str | None = None
    final_host_validation_passed = False
    try:
        execution_policy, execution_errors = parse_execution_policy(config)
        self_policy, self_errors = parse_execution_self_test_policy(config)
        errors = execution_errors + self_errors
        if errors or execution_policy is None or self_policy is None:
            first = errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        if execution_policy.publicRunCliEnabled is not False or execution_policy.controlledRealModelSelfTestEnabled is not True:
            raise RealTaskExecutionFailure("REAL_TASK_SELF_TEST_POLICY_INVALID", "Only controlled self-test execution may be enabled.")
        root_before, root_errors = parent_git_snapshot(root)
        if root_errors:
            first = root_errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        fixture = create_self_test_fixture(root, automation_root, self_test_run_id)
        controlled_config = self_test_config(config)
        manifest_path = Path(fixture["manifestPath"])
        validation = validate_task_manifest_file(Path(fixture["parent"]), automation_root, controlled_config, manifest_path, inspect_sources=True)
        if not validation.ok or validation.manifestSha256 is None:
            first = validation.errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        parent_before, parent_errors = parent_git_snapshot(Path(fixture["parent"]))
        if parent_errors:
            first = parent_errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        parent_identity_hash = snapshot_hash(parent_before)
        handle = _create_self_test_handle(self_test_run_id, fixture, str(validation.manifestSha256), parent_identity_hash)
        _consume_self_test_handle(handle, self_test_run_id, str(validation.manifestSha256), parent_identity_hash)
        adapter = CodexRealTaskRoleAdapter(
            root=Path(fixture["parent"]),
            automation_root=automation_root,
            config=controlled_config,
            run_dir=Path(fixture["reports"]),
            allow_real_execution=True,
        )
        production_result = _execute_real_task_with_test_adapter(manifest_path, Path(fixture["parent"]), automation_root, controlled_config, adapter)
        final_host_validation_passed = _final_host_validation(Path(fixture["parent"]), production_result.report)
    except RealTaskExecutionFailure as exc:
        error_code = exc.code
        error_message = exc.message
    except Exception as exc:
        error_code = "REAL_TASK_SELF_TEST_BLOCKED"
        error_message = f"{type(exc).__name__}: {exc}"
    finally:
        try:
            root_after, _ = parent_git_snapshot(root)
        except Exception:
            root_after = {}
        if fixture is not None:
            try:
                parent_after, _ = parent_git_snapshot(Path(fixture["parent"]))
            except Exception:
                parent_after = {}
        root_unchanged = bool(root_before and root_after and snapshots_equal(root_before, root_after))
        parent_unchanged = bool(parent_before and parent_after and snapshots_equal(parent_before, parent_after))
        report = _build_report(
            root,
            automation_root,
            fixture,
            self_test_run_id,
            started_at,
            round((time.monotonic() - started) * 1000),
            production_result,
            root_before,
            root_after,
            root_unchanged,
            parent_before,
            parent_after,
            parent_unchanged,
            final_host_validation_passed,
            error_code,
            error_message,
        )
        if fixture is not None:
            path = Path(fixture["reports"]) / SELF_TEST_REPORT_NAME
            max_bytes = int(config.get("realTaskExecutionSelfTestPolicy", {}).get("maxMetaReportBytes", 2_097_152))
            write_trusted_report(automation_root, root / "CodexAutomation" / "runtime", path, report, "real_task_execution_self_test.schema.json", _validate_self_test_report, max_bytes)
            report = read_json(path)
            report["reportPath"] = str(path)
    return report


def exit_code_for_self_test_verdict(verdict: str) -> int:
    return {"PASS": 0, "FAIL": 2, "BLOCKED": 3, "RATE_LIMITED": 4}.get(verdict, 1)


def _create_self_test_handle(self_test_run_id: str, fixture: dict[str, Any], manifest_sha: str, parent_identity_hash: str) -> RealTaskExecutionSelfTestHandle:
    handle = RealTaskExecutionSelfTestHandle(
        selfTestRunId=self_test_run_id,
        taskId=SELF_TEST_TASK_ID,
        manifestSha256=manifest_sha,
        syntheticParentIdentityHash=parent_identity_hash,
        selfTestRoot=str(Path(fixture["selfTestRoot"]).resolve(strict=True)),
        capability=new_capability(),
    )
    _SELF_TEST_REGISTRY[self_test_run_id] = {
        "handleObjectId": id(handle),
        "capability": handle.capability,
        "taskId": SELF_TEST_TASK_ID,
        "manifestSha256": manifest_sha,
        "syntheticParentIdentityHash": parent_identity_hash,
        "selfTestRoot": handle.selfTestRoot,
        "consumed": False,
    }
    return handle


def _consume_self_test_handle(handle: RealTaskExecutionSelfTestHandle, self_test_run_id: str, manifest_sha: str, parent_identity_hash: str) -> None:
    entry = _SELF_TEST_REGISTRY.get(self_test_run_id)
    if not entry or entry.get("consumed"):
        raise RealTaskExecutionFailure("REAL_TASK_SELF_TEST_CAPABILITY_INVALID", "Self-test capability is missing or already consumed.")
    if id(handle) != entry["handleObjectId"] or handle.capability != entry["capability"]:
        raise RealTaskExecutionFailure("REAL_TASK_SELF_TEST_CAPABILITY_INVALID", "Self-test capability identity mismatch.")
    if handle.taskId != SELF_TEST_TASK_ID or handle.manifestSha256 != manifest_sha or handle.syntheticParentIdentityHash != parent_identity_hash:
        raise RealTaskExecutionFailure("REAL_TASK_SELF_TEST_CAPABILITY_INVALID", "Self-test capability binding mismatch.")
    entry["consumed"] = True


def _final_host_validation(parent: Path, production_report: dict[str, Any] | None) -> bool:
    if not isinstance(production_report, dict) or production_report.get("finalVerdict") != "PASS":
        return False
    message_path = parent / "CodexAutomation" / "runtime" / "real_task_foundation_runs" / str(production_report["foundationRunId"]) / "workspace" / "TaskData" / "message.txt"
    result_path = message_path.parent / "result.json"
    if not message_path.exists() or not result_path.exists():
        return False
    if message_path.read_text(encoding="utf-8").strip() != "BOOTSTRAP-03B-2D CONTROLLED REAL MODEL SELF TEST PASSED":
        return False
    result = read_json(result_path)
    return result == {
        "status": "PASS",
        "stage": "BOOTSTRAP-03B-2D",
        "mode": "controlled-real-model-self-test",
        "message": "BOOTSTRAP-03B-2D CONTROLLED REAL MODEL SELF TEST PASSED",
    }


def _build_report(
    root: Path,
    automation_root: Path,
    fixture: dict[str, Any] | None,
    self_test_run_id: str,
    started_at: str,
    duration_ms: int,
    production_result: Any,
    root_before: dict[str, Any],
    root_after: dict[str, Any],
    root_unchanged: bool,
    parent_before: dict[str, Any],
    parent_after: dict[str, Any],
    parent_unchanged: bool,
    final_host_validation_passed: bool,
    error_code: str | None,
    error_message: str | None,
) -> dict[str, Any]:
    production_report = getattr(production_result, "report", None)
    production_report_path = getattr(production_result, "reportPath", None)
    bundle_path = getattr(production_result, "bundleManifestPath", None)
    production_verdict = production_report.get("finalVerdict") if isinstance(production_report, dict) else None
    self_verdict = _derive_verdict(production_report, root_unchanged, parent_unchanged, final_host_validation_passed, error_code)
    if error_code is None and self_verdict in {"FAIL", "BLOCKED"} and isinstance(production_report, dict):
        production_error = production_report.get("errorCode")
        production_message = production_report.get("errorMessage")
        error_code = production_error if isinstance(production_error, str) and production_error else None
        error_message = production_message if isinstance(production_message, str) and production_message else None
    if self_verdict == "PASS":
        error_code = None
        error_message = None
    report_root = Path(fixture["selfTestRoot"]) if fixture else automation_root / "runtime" / "real_task_execution_self_tests" / self_test_run_id
    return {
        "reportVersion": 1,
        "stage": stage(),
        "selfTestRunId": self_test_run_id,
        "selfTestTaskId": SELF_TEST_TASK_ID,
        "selfTestRootIdentity": str(report_root.resolve(strict=False)),
        "syntheticParentIdentity": str(Path(fixture["parent"]).resolve(strict=False)) if fixture else None,
        "controlledManifestRelativePath": _relative(report_root, Path(fixture["manifestPath"])) if fixture else None,
        "controlledManifestSha256": file_sha256(Path(fixture["manifestPath"])) if fixture else None,
        "productionOrchestrationRunId": production_report.get("orchestrationRunId") if isinstance(production_report, dict) else None,
        "productionFinalReportRelativePath": _relative(report_root, Path(production_report_path)) if production_report_path else None,
        "productionFinalReportSha256": canonical_sha256(read_json(Path(production_report_path))) if production_report_path and Path(production_report_path).exists() else None,
        "resultBundleManifestRelativePath": _relative(report_root, Path(bundle_path)) if bundle_path else None,
        "resultBundleManifestSha256": canonical_sha256(read_json(Path(bundle_path))) if bundle_path and Path(bundle_path).exists() else None,
        "startedAt": started_at,
        "finishedAt": utc_now(),
        "durationMilliseconds": duration_ms,
        "productionFinalState": production_report.get("finalState") if isinstance(production_report, dict) else None,
        "productionFinalVerdict": production_verdict,
        "selfTestVerdict": self_verdict,
        "invocationsUsed": int(production_report.get("invocationsUsed", 0)) if isinstance(production_report, dict) else 0,
        "repairsUsed": int(production_report.get("repairsUsed", 0)) if isinstance(production_report, dict) else 0,
        "codexInvocationCount": int(production_report.get("codexInvocationCount", 0)) if isinstance(production_report, dict) else 0,
        "modelInvocationStarted": bool(production_report.get("modelInvocationStarted")) if isinstance(production_report, dict) else False,
        "sandboxStarted": bool(production_report.get("sandboxStarted")) if isinstance(production_report, dict) else False,
        "taskNetworkUsed": bool(production_report.get("networkUsed")) if isinstance(production_report, dict) else False,
        "unityStarted": bool(production_report.get("unityStarted")) if isinstance(production_report, dict) else False,
        "packageInstallUsed": bool(production_report.get("packageInstallUsed")) if isinstance(production_report, dict) else False,
        "automaticRetryUsed": bool(production_report.get("automaticRetryUsed")) if isinstance(production_report, dict) else False,
        "modelDowngradeUsed": bool(production_report.get("modelDowngradeUsed")) if isinstance(production_report, dict) else False,
        "creditUsageTriggered": bool(production_report.get("creditUsageTriggered")) if isinstance(production_report, dict) else False,
        "rootRepositoryHeadBefore": root_before.get("head"),
        "rootRepositoryHeadAfter": root_after.get("head"),
        "rootRepositorySnapshotBeforeHash": snapshot_hash(root_before) if root_before else None,
        "rootRepositorySnapshotAfterHash": snapshot_hash(root_after) if root_after else None,
        "rootRepositoryUnchanged": root_unchanged,
        "syntheticParentSnapshotBeforeHash": snapshot_hash(parent_before) if parent_before else None,
        "syntheticParentSnapshotAfterHash": snapshot_hash(parent_after) if parent_after else None,
        "syntheticParentUnchanged": parent_unchanged,
        "finalHostValidationPassed": final_host_validation_passed,
        "auditorRan": bool(production_report.get("auditorRan")) if isinstance(production_report, dict) else False,
        "auditorApproved": bool(production_report.get("auditorApproved")) if isinstance(production_report, dict) else False,
        "auditorWorkspaceUnchanged": production_report.get("workspaceUnchangedAfterAuditor") is True if isinstance(production_report, dict) else False,
        "bundleIntegrityValid": bool(production_report.get("bundleIntegrityValid")) if isinstance(production_report, dict) else False,
        "eligibleForApply": read_json(Path(bundle_path)).get("eligibleForApply") if bundle_path and Path(bundle_path).exists() else None,
        "complete": self_verdict == "PASS",
        "errorCode": error_code,
        "errorMessage": error_message,
        "warnings": [],
    }


def _derive_verdict(production_report: dict[str, Any] | None, root_unchanged: bool, parent_unchanged: bool, final_host_validation_passed: bool, error_code: str | None) -> str:
    if error_code:
        return "BLOCKED"
    if not root_unchanged:
        return "BLOCKED"
    if not parent_unchanged:
        return "BLOCKED"
    if not isinstance(production_report, dict):
        return "BLOCKED"
    if production_report.get("finalVerdict") == "RATE_LIMITED":
        return "RATE_LIMITED"
    if production_report.get("finalVerdict") != "PASS":
        return "FAIL" if production_report.get("finalVerdict") == "FAIL" else "BLOCKED"
    required = (
        final_host_validation_passed,
        production_report.get("auditorRan") is True,
        production_report.get("auditorApproved") is True,
        production_report.get("workspaceUnchangedAfterAuditor") is True,
        production_report.get("bundleIntegrityValid") is True,
        production_report.get("modelInvocationStarted") is True,
        production_report.get("sandboxStarted") is True,
        production_report.get("networkUsed") is False,
        production_report.get("unityStarted") is False,
        production_report.get("packageInstallUsed") is False,
        production_report.get("automaticRetryUsed") is False,
        production_report.get("modelDowngradeUsed") is False,
        production_report.get("creditUsageTriggered") is False,
        production_report.get("invocationsUsed") in {2, 3},
        production_report.get("repairsUsed") in {0, 1},
        production_report.get("codexInvocationCount") == production_report.get("invocationsUsed"),
    )
    return "PASS" if all(required) else "BLOCKED"


def _validate_self_test_report(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    verdict = report.get("selfTestVerdict")
    if verdict not in {"PASS", "FAIL", "BLOCKED", "RATE_LIMITED"}:
        errors.append("selfTestVerdict is invalid.")
    if report.get("stage") != stage():
        errors.append("stage mismatch.")
    if verdict == "PASS":
        for field in (
            "rootRepositoryUnchanged",
            "syntheticParentUnchanged",
            "finalHostValidationPassed",
            "auditorRan",
            "auditorApproved",
            "auditorWorkspaceUnchanged",
            "bundleIntegrityValid",
            "modelInvocationStarted",
            "sandboxStarted",
            "complete",
        ):
            if report.get(field) is not True:
                errors.append(f"PASS requires {field}=true.")
        for field in ("taskNetworkUsed", "unityStarted", "packageInstallUsed", "automaticRetryUsed", "modelDowngradeUsed", "creditUsageTriggered"):
            if report.get(field) is not False:
                errors.append(f"PASS requires {field}=false.")
        if report.get("eligibleForApply") is not False:
            errors.append("PASS requires eligibleForApply=false.")
        if report.get("invocationsUsed") not in {2, 3} or report.get("repairsUsed") not in {0, 1}:
            errors.append("PASS invocation or repair count is invalid.")
        if report.get("codexInvocationCount") != report.get("invocationsUsed"):
            errors.append("PASS requires codexInvocationCount == invocationsUsed.")
        if report.get("errorCode") is not None or report.get("errorMessage") is not None:
            errors.append("PASS cannot contain errors.")
    return errors


def _relative(root: Path, path: Path) -> str:
    try:
        return path.resolve(strict=False).relative_to(root.resolve(strict=False)).as_posix()
    except ValueError:
        return str(path.resolve(strict=False))
