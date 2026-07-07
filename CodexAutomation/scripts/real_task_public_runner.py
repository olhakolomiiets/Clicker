from __future__ import annotations

import hashlib
import json
import os
import stat
import subprocess
import sys
import ctypes
from dataclasses import dataclass
from pathlib import Path, PureWindowsPath
from typing import Any
from ctypes import wintypes

from file_utils import read_json
from parent_git_snapshot import parent_git_snapshot, snapshots_equal
from real_task_execution_models import RealTaskExecutionFailure, RealTaskExecutionResult, canonical_json_bytes, canonical_sha256, file_sha256, new_orchestration_run_id, utc_now
from real_task_execution_policy import parse_execution_policy
from real_task_report_writer import TrustedReportReceipt, read_trusted_report, write_trusted_report, _read_json_no_duplicates
from real_task_runner import _execute_real_task_with_production_adapter
from schema_validator import validate as validate_schema_instance
from task_manifest_validator import parse_real_task_policy, validate_task_manifest_file


PUBLIC_REAL_TASK_PASS = 0
PUBLIC_REAL_TASK_INTERNAL_ERROR = 1
PUBLIC_REAL_TASK_FAIL = 2
PUBLIC_REAL_TASK_BLOCKED = 3
PUBLIC_REAL_TASK_RATE_LIMITED = 4
PUBLIC_REAL_TASK_INVALID_CLI = 64
PUBLIC_REAL_TASK_INVALID_INPUT = 65

PUBLIC_REAL_TASK_STAGE = "BOOTSTRAP-03B-3A"
PUBLIC_MANIFEST_ROOT = Path("CodexAutomation/tasks/real_tasks")
PUBLIC_RUN_ROOT = Path("CodexAutomation/runtime/public_real_task_runs")
PUBLIC_LOCK_PATH = Path("CodexAutomation/runtime/locks/public_generic_real_task_run.lock")
PUBLIC_REPORT_NAME = "PUBLIC_REAL_TASK_RUN_REPORT.json"
PUBLIC_REPORT_SCHEMA = "public_real_task_run.schema.json"
PUBLIC_RECEIPT_NAME = "PUBLIC_REAL_TASK_RUN_REPORT.receipt.json"
PUBLIC_RECEIPT_SCHEMA = "public_real_task_run_receipt.schema.json"
MAX_PUBLIC_MANIFEST_BYTES = 262_144
MAX_PUBLIC_REPORT_BYTES = 4_194_304
WILDCARD_CHARS = {"*", "?", "[", "]"}
GIT_TIMEOUT_SECONDS = 10
INVALID_WIN32_HANDLE_VALUE = ctypes.c_void_p(-1).value


class BY_HANDLE_FILE_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("dwFileAttributes", wintypes.DWORD),
        ("ftCreationTime", wintypes.FILETIME),
        ("ftLastAccessTime", wintypes.FILETIME),
        ("ftLastWriteTime", wintypes.FILETIME),
        ("dwVolumeSerialNumber", wintypes.DWORD),
        ("nFileSizeHigh", wintypes.DWORD),
        ("nFileSizeLow", wintypes.DWORD),
        ("nNumberOfLinks", wintypes.DWORD),
        ("nFileIndexHigh", wintypes.DWORD),
        ("nFileIndexLow", wintypes.DWORD),
    ]


class FILE_DISPOSITION_INFO(ctypes.Structure):
    _fields_ = [("DeleteFile", ctypes.c_byte)]


@dataclass(frozen=True)
class _Win32KernelApi:
    kernel32: object
    CreateFileW: object
    WriteFile: object
    ReadFile: object
    SetFilePointerEx: object
    FlushFileBuffers: object
    GetFileSizeEx: object
    GetFileInformationByHandle: object
    SetFileInformationByHandle: object
    CloseHandle: object


_WIN32_KERNEL_API: _Win32KernelApi | None = None


@dataclass(frozen=True)
class PublicFileIdentity:
    platform: str
    device: int
    file_id: int
    mode: int
    attributes: int
    size: int
    ctime_ns: int
    is_reparse: bool

    def to_dict(self) -> dict[str, Any]:
        return {
            "platform": self.platform,
            "device": self.device,
            "fileId": self.file_id,
            "mode": self.mode,
            "attributes": self.attributes,
            "size": self.size,
            "ctimeNs": self.ctime_ns,
            "isReparse": self.is_reparse,
        }


@dataclass(frozen=True)
class PublicManifestAuthority:
    path: Path
    relative_path: str
    size: int
    sha256: str
    head_sha256: str
    bytes_value: bytes
    file_identity: PublicFileIdentity


@dataclass(frozen=True)
class PublicProductionEvidence:
    report: dict[str, Any]
    bundle_manifest: dict[str, Any]
    report_path: Path
    bundle_manifest_path: Path
    report_relative_path: str
    bundle_relative_path: str
    report_sha256: str
    bundle_manifest_sha256: str


@dataclass(frozen=True)
class PublicRealTaskRunResult:
    exitCode: int
    finalState: str
    finalVerdict: str
    report: dict[str, Any]
    reportPath: str | None = None
    bundleManifestPath: str | None = None


def run_public_real_task(root: Path, automation_root: Path, config: dict[str, Any], manifest_arg: str) -> PublicRealTaskRunResult:
    gate_error = _public_gate_error(config)
    if gate_error is not None:
        return _blocked_input(gate_error, "Public generic real-task execution is disabled by host config.")

    manifest_path, manifest_error = resolve_public_manifest_path(root, manifest_arg)
    if manifest_error is not None or manifest_path is None:
        return _blocked_input(manifest_error or "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE", "Public real-task manifest path is unsafe.")

    authority, authority_error = load_public_manifest_authority(root, manifest_path)
    if authority_error is not None or authority is None:
        return _blocked_input(authority_error or "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE", "Public real-task manifest authority failed.")

    validation = validate_task_manifest_file(root, automation_root, config, authority.path, inspect_sources=True, allow_execution_enabled=True)
    if not validation.ok:
        first = validation.errors[0]
        return _blocked_input(first.code, first.message, manifest_sha=authority.sha256, manifest_relative_path=authority.relative_path, task_id=_task_id(validation.manifest))

    public_run_id = new_public_run_id()
    public_run_root = root / PUBLIC_RUN_ROOT / public_run_id
    runtime_root = root / "CodexAutomation" / "runtime"
    try:
        _create_public_run_root(public_run_root, root)
    except RealTaskExecutionFailure as exc:
        return _internal_error(exc.code, exc.message, manifest_sha=authority.sha256, manifest_relative_path=authority.relative_path, task_id=_task_id(validation.manifest))

    started_at = utc_now()
    lock = _PublicRunLock(root / PUBLIC_LOCK_PATH)
    lock_acquired = False
    lock_released = False
    lock_release_error: tuple[str, str] | None = None
    parent_baseline: dict[str, Any] | None = None
    checkpoint = "before_lock"
    execution: RealTaskExecutionResult | None = None
    exit_code = PUBLIC_REAL_TASK_BLOCKED
    final_state = "BLOCKED"
    final_verdict = "BLOCKED"
    error_code: str | None = None
    error_message: str | None = None
    production_evidence: PublicProductionEvidence | None = None
    try:
        lock_error = lock.acquire(public_run_id, authority.relative_path, authority.sha256)
        if lock_error is not None:
            error_code, error_message = "PUBLIC_REAL_TASK_RUN_LOCKED", lock_error
            exit_code = PUBLIC_REAL_TASK_BLOCKED
        else:
            lock_acquired = True

            checkpoint = "after_lock_manifest_before_foundation"
            changed_authority, changed_error = load_public_manifest_authority(root, authority.path)
            if (
                changed_error is not None
                or changed_authority is None
                or changed_authority.sha256 != authority.sha256
                or changed_authority.bytes_value != authority.bytes_value
                or changed_authority.file_identity != authority.file_identity
            ):
                error_code, error_message = "PUBLIC_REAL_TASK_MANIFEST_CHANGED", "Public manifest changed after lock acquisition."
                exit_code = PUBLIC_REAL_TASK_BLOCKED
            else:
                parent_baseline = _require_clean_parent(root)
                post_lock_validation = validate_task_manifest_file(root, automation_root, config, authority.path, inspect_sources=True, allow_execution_enabled=True)
                if not post_lock_validation.ok:
                    first = post_lock_validation.errors[0]
                    error_code, error_message = first.code, first.message
                    exit_code = PUBLIC_REAL_TASK_BLOCKED
                else:
                    def parent_checkpoint(name: str) -> None:
                        nonlocal checkpoint
                        checkpoint = name
                        _require_parent_unchanged(root, parent_baseline)

                    execution = _execute_real_task_with_production_adapter(authority.path, root, automation_root, config, parent_checkpoint)
                    checkpoint = "after_production_before_public_terminal"
                    _require_parent_unchanged(root, parent_baseline)
                    error_code = execution.report.get("errorCode") if isinstance(execution.report.get("errorCode"), str) else None
                    error_message = execution.report.get("errorMessage") if isinstance(execution.report.get("errorMessage"), str) else None
                    final_state = execution.finalState
                    final_verdict = execution.finalVerdict
                    exit_code = public_exit_code_for_verdict(final_verdict, error_code)
                    if final_verdict == "PASS" and exit_code == PUBLIC_REAL_TASK_PASS:
                        production_evidence = _verify_public_pass_production_evidence(root, automation_root, authority, execution)
    except RealTaskExecutionFailure as exc:
        error_code, error_message = exc.code, exc.message
        final_verdict = exc.verdict
        final_state = "RATE_LIMITED" if exc.code == "REAL_TASK_RATE_LIMITED" else ("FAILED" if exc.verdict == "FAIL" else "BLOCKED")
        exit_code = public_exit_code_for_verdict(final_verdict, error_code)
    except Exception as exc:
        error_code, error_message = "PUBLIC_REAL_TASK_INTERNAL_ERROR", f"{type(exc).__name__}: {exc}"
        final_state = "BLOCKED"
        final_verdict = "BLOCKED"
        exit_code = PUBLIC_REAL_TASK_INTERNAL_ERROR
    finally:
        if lock_acquired:
            release_error = lock.release()
            if release_error is None:
                lock_released = True
            else:
                lock_release_error = release_error
    if lock_release_error is not None:
        error_code, error_message = lock_release_error
        final_state = "BLOCKED"
        final_verdict = "BLOCKED"
        exit_code = PUBLIC_REAL_TASK_INTERNAL_ERROR
        checkpoint = "lock_release"
    return _persist_public_result(root, automation_root, runtime_root, public_run_root, public_run_id, started_at, authority, validation.manifest, execution, production_evidence, final_state, final_verdict, exit_code, error_code, error_message, lock_acquired, lock_released, checkpoint)


def resolve_public_manifest_path(root: Path, manifest_arg: str) -> tuple[Path | None, str | None]:
    if not isinstance(manifest_arg, str) or not manifest_arg.strip():
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if any(ord(char) < 32 for char in manifest_arg):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if any(char in manifest_arg for char in WILDCARD_CHARS):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if "\\" in manifest_arg or ":" in manifest_arg:
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if manifest_arg == "." or manifest_arg.startswith("./") or manifest_arg.endswith("/.") or "/./" in manifest_arg:
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    windows = PureWindowsPath(manifest_arg)
    raw = Path(manifest_arg)
    if raw.is_absolute() or windows.drive or windows.root or manifest_arg.startswith(("/", "\\")):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    parts = raw.parts
    if any(part in {"", ".", ".."} for part in parts):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if raw.suffix != ".json":
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    try:
        rel = raw.relative_to(PUBLIC_MANIFEST_ROOT)
    except ValueError:
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if not rel.parts:
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"

    root_resolved = root.resolve(strict=True)
    public_root = (root_resolved / PUBLIC_MANIFEST_ROOT).resolve(strict=False)
    candidate = root_resolved / raw
    if _has_reparse_component(candidate.parent):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    try:
        status = candidate.stat(follow_symlinks=False)
    except OSError:
        return None, "PUBLIC_REAL_TASK_MANIFEST_MISSING"
    if not stat.S_ISREG(status.st_mode) or _stat_is_reparse(status):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if status.st_size <= 0:
        return None, "PUBLIC_REAL_TASK_MANIFEST_TOO_LARGE"
    if status.st_size > MAX_PUBLIC_MANIFEST_BYTES:
        return None, "PUBLIC_REAL_TASK_MANIFEST_TOO_LARGE"
    try:
        resolved = candidate.resolve(strict=True)
        resolved.relative_to(public_root)
        resolved.relative_to(root_resolved)
    except (OSError, ValueError):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if resolved != candidate.resolve(strict=False):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    return candidate, None


def load_public_manifest_authority(root: Path, manifest_path: Path) -> tuple[PublicManifestAuthority | None, str | None]:
    try:
        root_resolved = root.resolve(strict=True)
        relative_path = manifest_path.resolve(strict=True).relative_to(root_resolved).as_posix()
    except (OSError, ValueError):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    if not relative_path.startswith(PUBLIC_MANIFEST_ROOT.as_posix() + "/"):
        return None, "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE"
    resolved_again, path_error = resolve_public_manifest_path(root, relative_path)
    if path_error is not None or resolved_again is None:
        return None, path_error
    try:
        status = resolved_again.stat(follow_symlinks=False)
        identity = _public_file_identity(status)
        data = resolved_again.read_bytes()
    except OSError:
        return None, "PUBLIC_REAL_TASK_MANIFEST_MISSING"
    if not data or len(data) > MAX_PUBLIC_MANIFEST_BYTES:
        return None, "PUBLIC_REAL_TASK_MANIFEST_TOO_LARGE"
    tracked, tracked_error = _verify_manifest_tracked_and_committed(root, relative_path, data)
    if not tracked:
        return None, tracked_error or "PUBLIC_REAL_TASK_MANIFEST_NOT_COMMITTED"
    return PublicManifestAuthority(
        path=resolved_again,
        relative_path=relative_path,
        size=len(data),
        sha256=hashlib.sha256(data).hexdigest(),
        head_sha256=hashlib.sha256(data).hexdigest(),
        bytes_value=data,
        file_identity=identity,
    ), None


def public_exit_code_for_verdict(verdict: str, error_code: str | None = None) -> int:
    if error_code in {
        "REAL_TASK_PUBLIC_RUN_DISABLED",
        "PUBLIC_REAL_TASK_MANIFEST_PATH_UNSAFE",
        "PUBLIC_REAL_TASK_MANIFEST_MISSING",
        "PUBLIC_REAL_TASK_MANIFEST_TOO_LARGE",
        "PUBLIC_REAL_TASK_MANIFEST_NOT_TRACKED",
        "PUBLIC_REAL_TASK_MANIFEST_NOT_COMMITTED",
        "REAL_TASK_CONFIG_INVALID",
        "REAL_TASK_EXECUTION_POLICY_INVALID",
    }:
        return PUBLIC_REAL_TASK_INVALID_INPUT
    if error_code in {"PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED", "PUBLIC_REAL_TASK_REPORT_WRITE_FAILED", "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "REAL_TASK_FINAL_REPORT_INVALID", "REAL_TASK_FINAL_REPORT_WRITE_FAILED", "REAL_TASK_BUNDLE_WRITE_FAILED", "REAL_TASK_BUNDLE_INTEGRITY_FAILED"}:
        return PUBLIC_REAL_TASK_INTERNAL_ERROR
    return {
        "PASS": PUBLIC_REAL_TASK_PASS,
        "FAIL": PUBLIC_REAL_TASK_FAIL,
        "BLOCKED": PUBLIC_REAL_TASK_BLOCKED,
        "RATE_LIMITED": PUBLIC_REAL_TASK_RATE_LIMITED,
    }.get(verdict, PUBLIC_REAL_TASK_INTERNAL_ERROR)


def new_public_run_id() -> str:
    return new_orchestration_run_id().replace("real_task_", "public_real_task_", 1)


def _public_gate_error(config: dict[str, Any]) -> str | None:
    real_tasks = config.get("realTasks") if isinstance(config, dict) else None
    execution_policy_raw = config.get("realTaskExecutionPolicy") if isinstance(config, dict) else None
    if not isinstance(real_tasks, dict) or not isinstance(execution_policy_raw, dict):
        return "REAL_TASK_CONFIG_INVALID"
    if real_tasks.get("allowExecution") is not True or execution_policy_raw.get("publicGenericRealTaskRunEnabled") is not True:
        return "REAL_TASK_PUBLIC_RUN_DISABLED"
    _, real_task_errors = parse_real_task_policy(config, allow_execution_enabled=True)
    _, execution_errors = parse_execution_policy(config, allow_public_generic_real_task_run_enabled=True)
    if real_task_errors:
        return real_task_errors[0].code
    if execution_errors:
        return execution_errors[0].code
    return None


def _persist_public_result(
    root: Path,
    automation_root: Path,
    runtime_root: Path,
    public_run_root: Path,
    public_run_id: str,
    started_at: str,
    authority: PublicManifestAuthority,
    manifest: dict[str, Any] | None,
    execution: RealTaskExecutionResult | None,
    production_evidence: PublicProductionEvidence | None,
    final_state: str,
    final_verdict: str,
    exit_code: int,
    error_code: str | None,
    error_message: str | None,
    lock_acquired: bool,
    lock_released: bool,
    checkpoint: str,
) -> PublicRealTaskRunResult:
    if final_verdict == "PASS" and (exit_code != 0 or not lock_acquired or not lock_released):
        final_state = "BLOCKED"
        final_verdict = "BLOCKED"
        exit_code = PUBLIC_REAL_TASK_INTERNAL_ERROR
        error_code = error_code or "PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST"
        error_message = error_message or "PASS requires owner-safe lock release."
    if final_verdict == "PASS" and production_evidence is None:
        final_state = "BLOCKED"
        final_verdict = "BLOCKED"
        exit_code = PUBLIC_REAL_TASK_INTERNAL_ERROR
        error_code = "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID"
        error_message = "PASS requires verified persisted production final report and result bundle."
    pass_evidence = production_evidence if final_verdict == "PASS" else None
    production_report = pass_evidence.report if pass_evidence is not None else (execution.report if execution else {})
    production_report_path = pass_evidence.report_path if pass_evidence is not None else (Path(execution.reportPath) if execution and execution.reportPath else None)
    bundle_path = pass_evidence.bundle_manifest_path if pass_evidence is not None else (Path(execution.bundleManifestPath) if execution and execution.bundleManifestPath else None)
    production_report_relative = pass_evidence.report_relative_path if pass_evidence is not None else _relative_runtime_path(root, production_report_path)
    bundle_relative = pass_evidence.bundle_relative_path if pass_evidence is not None else _relative_runtime_path(root, bundle_path)
    production_run_id = production_report.get("orchestrationRunId") if isinstance(production_report.get("orchestrationRunId"), str) else None
    production_task_id = production_report.get("taskId") if isinstance(production_report.get("taskId"), str) else None
    report = {
        "reportVersion": 1,
        "automationStage": PUBLIC_REAL_TASK_STAGE,
        "publicRunId": public_run_id,
        "startedAt": started_at,
        "finishedAt": utc_now(),
        "manifestRelativePath": authority.relative_path,
        "manifestSha256": authority.sha256,
        "manifestTracked": True,
        "manifestMatchesHead": authority.sha256 == authority.head_sha256,
        "taskId": _task_id(manifest) or production_task_id,
        "productionOrchestrationRunId": production_run_id,
        "productionFinalReportRelativePath": production_report_relative,
        "productionFinalReportSha256": pass_evidence.report_sha256 if pass_evidence is not None else None,
        "finalState": final_state,
        "finalVerdict": final_verdict,
        "complete": True,
        "invocationsUsed": _int_value(production_report.get("invocationsUsed")),
        "repairsUsed": _int_value(production_report.get("repairsUsed")),
        "sandboxProbePassed": bool(production_report.get("sandboxProbeReportHash") and production_report.get("errorCode") != "REAL_TASK_SANDBOX_PROBE_FAILED"),
        "sandboxStarted": bool(production_report.get("sandboxStarted") is True),
        "modelInvocationStarted": bool(production_report.get("modelInvocationStarted") is True),
        "auditorRan": bool(production_report.get("auditorRan") is True),
        "auditorApproved": bool(production_report.get("auditorApproved") is True),
        "auditorWorkspaceUnchanged": production_report.get("workspaceUnchangedAfterAuditor") if production_report else False,
        "bundleRelativePath": bundle_relative,
        "bundleManifestSha256": pass_evidence.bundle_manifest_sha256 if pass_evidence is not None else None,
        "bundleIntegrityValid": bool(production_report.get("bundleIntegrityValid") is True and pass_evidence is not None),
        "eligibleForApply": False,
        "rootRepositoryUnchanged": error_code != "REAL_TASK_PARENT_GIT_CHANGED",
        "parentRepositoryUnchanged": error_code != "REAL_TASK_PARENT_GIT_CHANGED",
        "lockAcquired": lock_acquired,
        "lockReleased": lock_released,
        "parentIntegrityCheckpoint": checkpoint,
        "errorCode": error_code,
        "errorMessage": error_message,
        "exitCode": exit_code,
    }
    try:
        report_path = public_run_root / PUBLIC_REPORT_NAME
        receipt = write_trusted_report(automation_root, runtime_root, report_path, report, PUBLIC_REPORT_SCHEMA, validate_public_real_task_report, MAX_PUBLIC_REPORT_BYTES)
        receipt_payload = _public_receipt_payload(public_run_id, receipt)
        write_trusted_report(automation_root, runtime_root, public_run_root / PUBLIC_RECEIPT_NAME, receipt_payload, PUBLIC_RECEIPT_SCHEMA, validate_public_receipt_report, 4096)
        persisted = read_trusted_public_run_report(automation_root, runtime_root, public_run_root, public_run_id)
    except Exception as exc:
        fallback = dict(report)
        fallback["finalState"] = "BLOCKED"
        fallback["finalVerdict"] = "BLOCKED"
        fallback["errorCode"] = "PUBLIC_REAL_TASK_REPORT_WRITE_FAILED"
        fallback["errorMessage"] = f"{type(exc).__name__}: {exc}"
        fallback["exitCode"] = PUBLIC_REAL_TASK_INTERNAL_ERROR
        return PublicRealTaskRunResult(PUBLIC_REAL_TASK_INTERNAL_ERROR, "BLOCKED", "BLOCKED", fallback, None, str(bundle_path) if bundle_path else None)
    return PublicRealTaskRunResult(int(persisted["exitCode"]), str(persisted["finalState"]), str(persisted["finalVerdict"]), persisted, str(public_run_root / PUBLIC_REPORT_NAME), str(bundle_path) if bundle_path else None)


def validate_public_real_task_report(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("automationStage") != PUBLIC_REAL_TASK_STAGE:
        errors.append("Public report stage mismatch.")
    if report.get("eligibleForApply") is not False:
        errors.append("Public report requires eligibleForApply=false.")
    if report.get("complete") is not True:
        errors.append("Public report requires complete=true.")
    mapping = {"PASS": 0, "FAIL": 2, "BLOCKED": (3, 65, 1), "RATE_LIMITED": 4}
    verdict = report.get("finalVerdict")
    exit_code = report.get("exitCode")
    if verdict in mapping:
        expected = mapping[verdict]
        if isinstance(expected, tuple):
            if exit_code not in expected:
                errors.append("Public report exitCode is inconsistent with finalVerdict.")
        elif exit_code != expected:
            errors.append("Public report exitCode is inconsistent with finalVerdict.")
    if verdict == "PASS":
        required_true = ("manifestTracked", "manifestMatchesHead", "bundleIntegrityValid", "auditorRan", "auditorApproved", "auditorWorkspaceUnchanged", "rootRepositoryUnchanged", "parentRepositoryUnchanged", "lockAcquired", "lockReleased", "sandboxProbePassed", "sandboxStarted", "modelInvocationStarted")
        for field in required_true:
            if report.get(field) is not True:
                errors.append(f"PASS requires {field}=true.")
        if report.get("finalState") != "COMPLETED":
            errors.append("PASS requires finalState=COMPLETED.")
        if report.get("errorCode") is not None or report.get("errorMessage") is not None:
            errors.append("PASS requires null error fields.")
        for field in ("productionOrchestrationRunId", "productionFinalReportRelativePath", "productionFinalReportSha256", "bundleRelativePath", "bundleManifestSha256"):
            if not isinstance(report.get(field), str) or not report.get(field):
                errors.append(f"PASS requires non-empty {field}.")
    else:
        if report.get("errorCode") is None:
            errors.append("Non-PASS public report requires errorCode.")
        for field in ("productionFinalReportSha256", "bundleManifestSha256"):
            if report.get(field) is not None:
                errors.append(f"Non-PASS public report cannot claim {field}.")
    return errors


def _blocked_input(error_code: str, message: str, manifest_sha: str | None = None, manifest_relative_path: str | None = None, task_id: str | None = None) -> PublicRealTaskRunResult:
    report = _minimal_report(error_code, message, PUBLIC_REAL_TASK_INVALID_INPUT, manifest_sha, manifest_relative_path, task_id)
    return PublicRealTaskRunResult(PUBLIC_REAL_TASK_INVALID_INPUT, "BLOCKED", "BLOCKED", report)


def _internal_error(error_code: str, message: str, manifest_sha: str | None = None, manifest_relative_path: str | None = None, task_id: str | None = None) -> PublicRealTaskRunResult:
    report = _minimal_report(error_code, message, PUBLIC_REAL_TASK_INTERNAL_ERROR, manifest_sha, manifest_relative_path, task_id)
    return PublicRealTaskRunResult(PUBLIC_REAL_TASK_INTERNAL_ERROR, "BLOCKED", "BLOCKED", report)


def _minimal_report(error_code: str, message: str, exit_code: int, manifest_sha: str | None, manifest_relative_path: str | None, task_id: str | None) -> dict[str, Any]:
    return {
        "reportVersion": 1,
        "automationStage": PUBLIC_REAL_TASK_STAGE,
        "publicRunId": None,
        "startedAt": None,
        "finishedAt": utc_now(),
        "manifestRelativePath": manifest_relative_path,
        "manifestSha256": manifest_sha,
        "manifestTracked": False,
        "manifestMatchesHead": False,
        "taskId": task_id,
        "productionOrchestrationRunId": None,
        "productionFinalReportRelativePath": None,
        "productionFinalReportSha256": None,
        "finalState": "BLOCKED",
        "finalVerdict": "BLOCKED",
        "complete": True,
        "invocationsUsed": 0,
        "repairsUsed": 0,
        "sandboxProbePassed": False,
        "sandboxStarted": False,
        "modelInvocationStarted": False,
        "auditorRan": False,
        "auditorApproved": False,
        "auditorWorkspaceUnchanged": False,
        "bundleRelativePath": None,
        "bundleManifestSha256": None,
        "bundleIntegrityValid": False,
        "eligibleForApply": False,
        "rootRepositoryUnchanged": True,
        "parentRepositoryUnchanged": True,
        "lockAcquired": False,
        "lockReleased": False,
        "parentIntegrityCheckpoint": None,
        "errorCode": error_code,
        "errorMessage": message,
        "exitCode": exit_code,
    }


def _verify_public_pass_production_evidence(
    root: Path,
    automation_root: Path,
    authority: PublicManifestAuthority,
    execution: RealTaskExecutionResult,
) -> PublicProductionEvidence:
    if not execution.reportPath or not execution.bundleManifestPath:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "PASS requires persisted production final report and bundle manifest paths.")
    report_path = Path(execution.reportPath)
    bundle_path = Path(execution.bundleManifestPath)
    production_run_root = _production_run_root_for_report(root, report_path)
    _require_safe_regular_file_under(report_path, production_run_root, "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")
    expected_bundle_dir = production_run_root / "result_bundle"
    if bundle_path.name != "RESULT_MANIFEST.json":
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle manifest filename is invalid.")
    _require_safe_regular_file_under(bundle_path, expected_bundle_dir, "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")
    report = _read_json_no_duplicates(report_path)
    _validate_schema_or_raise(automation_root, report, "real_task_final.schema.json", "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")
    _validate_production_final_report(report, authority, execution)
    bundle = _read_json_no_duplicates(bundle_path)
    _validate_schema_or_raise(automation_root, bundle, "real_task_bundle_manifest.schema.json", "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")
    _validate_production_bundle_manifest(root, bundle_path, bundle, report, authority)
    return PublicProductionEvidence(
        report=report,
        bundle_manifest=bundle,
        report_path=report_path,
        bundle_manifest_path=bundle_path,
        report_relative_path=_required_runtime_relative(root, report_path),
        bundle_relative_path=_required_runtime_relative(root, bundle_path),
        report_sha256=file_sha256(report_path),
        bundle_manifest_sha256=file_sha256(bundle_path),
    )


def _production_run_root_for_report(root: Path, report_path: Path) -> Path:
    runtime_runs = (root / "CodexAutomation" / "runtime" / "real_task_runs").resolve(strict=False)
    logical = report_path.resolve(strict=False)
    if report_path.name != "FINAL_REAL_TASK_REPORT.json":
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production final report filename is invalid.")
    try:
        relative = logical.relative_to(runtime_runs)
    except ValueError as exc:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production final report is outside real_task_runs runtime root.") from exc
    if len(relative.parts) != 2 or relative.parts[1] != "FINAL_REAL_TASK_REPORT.json":
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production final report is not in an exact run root.")
    return runtime_runs / relative.parts[0]


def _require_safe_regular_file_under(path: Path, parent: Path, error_code: str) -> None:
    try:
        logical = path.resolve(strict=False)
        logical.relative_to(parent.resolve(strict=False))
    except (OSError, ValueError) as exc:
        raise RealTaskExecutionFailure(error_code, "Artifact path escapes its required root.") from exc
    if _has_reparse_component(logical.parent):
        raise RealTaskExecutionFailure(error_code, "Artifact parent contains a symlink or reparse component.")
    try:
        status = logical.stat(follow_symlinks=False)
    except OSError as exc:
        raise RealTaskExecutionFailure(error_code, "Artifact is missing.") from exc
    if not stat.S_ISREG(status.st_mode) or _stat_is_reparse(status):
        raise RealTaskExecutionFailure(error_code, "Artifact target is unsafe.")


def _validate_schema_or_raise(automation_root: Path, payload: dict[str, Any], schema_name: str, error_code: str) -> None:
    errors = validate_schema_instance(payload, read_json(automation_root / "schemas" / schema_name))
    if errors:
        raise RealTaskExecutionFailure(error_code, errors[0])


def _validate_production_final_report(report: dict[str, Any], authority: PublicManifestAuthority, execution: RealTaskExecutionResult) -> None:
    required = {
        "finalState": "COMPLETED",
        "finalVerdict": "PASS",
        "errorCode": None,
        "errorMessage": None,
        "auditorRan": True,
        "auditorApproved": True,
        "workspaceUnchangedAfterAuditor": True,
        "parentGitChanged": False,
        "bundleIntegrityValid": True,
        "sandboxStarted": True,
        "modelInvocationStarted": True,
    }
    for field, expected in required.items():
        if report.get(field) != expected:
            raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", f"Production final report does not satisfy {field}.")
    if report.get("orchestrationRunId") != execution.report.get("orchestrationRunId"):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production report run id mismatches execution result.")
    if report.get("taskId") != execution.report.get("taskId"):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production report task id mismatches execution result.")
    if report.get("manifestSha256") != authority.sha256:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production report manifest hash mismatches public manifest authority.")
    if not isinstance(report.get("resultBundleManifestHash"), str) or not report["resultBundleManifestHash"]:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production report lacks result bundle manifest hash.")


def _validate_production_bundle_manifest(root: Path, bundle_path: Path, bundle: dict[str, Any], report: dict[str, Any], authority: PublicManifestAuthority) -> None:
    if bundle.get("complete") is not True or bundle.get("eligibleForApply") is not False or bundle.get("finalTaskVerdict") != "PASS":
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle manifest is not a completed review-only PASS bundle.")
    for field in ("orchestrationRunId", "taskId"):
        if bundle.get(field) != report.get(field):
            raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle does not match final report identity.")
    if bundle.get("manifestSha256") != authority.sha256:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle manifest hash mismatches public manifest authority.")
    if canonical_sha256(bundle) != report.get("resultBundleManifestHash"):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle manifest canonical hash mismatches final report.")
    _verify_public_bundle_closure(root, bundle_path.parent, bundle)


def _verify_public_bundle_closure(root: Path, bundle_dir: Path, bundle: dict[str, Any]) -> None:
    if _has_reparse_component(bundle_dir):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle directory is unsafe.")
    sums_path = bundle_dir / "SHA256SUMS.json"
    _require_safe_regular_file_under(sums_path, bundle_dir, "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")
    sums = _read_json_no_duplicates(sums_path)
    if canonical_sha256(sums) != bundle.get("sha256SumsSha256") or sums_path.stat().st_size != bundle.get("sha256SumsSize"):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle SHA256SUMS binding mismatch.")
    entries = sums.get("entries")
    if not isinstance(entries, list):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle SHA256SUMS entries are invalid.")
    seen: set[str] = set()
    total_bytes = 0
    for item in entries:
        if not isinstance(item, dict):
            raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle SHA256SUMS entry is invalid.")
        rel = str(item.get("relativePath"))
        if rel in seen or rel in {"RESULT_MANIFEST.json", "SHA256SUMS.json"} or "\\" in rel or ":" in rel or rel.startswith("/") or "/../" in f"/{rel}/":
            raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle contains an unsafe entry path.")
        seen.add(rel)
        artifact = bundle_dir / rel
        _require_safe_regular_file_under(artifact, bundle_dir, "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")
        if artifact.stat().st_size != item.get("size") or file_sha256(artifact) != item.get("sha256"):
            raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle payload hash or size mismatch.")
        total_bytes += int(item.get("size", 0))
    if sums.get("totalFiles") != len(entries) or sums.get("totalBytes") != total_bytes or sums.get("aggregateHash") != canonical_sha256(entries):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle SHA256SUMS totals are inconsistent.")
    covered = set(seen)
    files = {path.resolve(strict=False).relative_to(bundle_dir.resolve(strict=False)).as_posix() for path in bundle_dir.rglob("*") if path.is_file()}
    if files - covered != {"RESULT_MANIFEST.json", "SHA256SUMS.json"}:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Production bundle has unexpected uncovered files.")


def _required_runtime_relative(root: Path, path: Path) -> str:
    relative = _relative_runtime_path(root, path)
    if relative is None:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID", "Artifact is outside runtime root.")
    return relative


def _public_receipt_payload(public_run_id: str, receipt: TrustedReportReceipt) -> dict[str, Any]:
    return {
        "receiptVersion": 1,
        "automationStage": PUBLIC_REAL_TASK_STAGE,
        "publicRunId": public_run_id,
        "reportRelativePath": receipt.relativePath,
        "reportSha256": receipt.sha256,
        "reportByteSize": receipt.size,
        "reportSchemaName": receipt.schemaName,
        "reportVersion": receipt.reportVersion,
        "createdAt": utc_now(),
        "complete": True,
        "trusted": True,
    }


def validate_public_receipt_report(receipt: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if receipt.get("automationStage") != PUBLIC_REAL_TASK_STAGE:
        errors.append("Public receipt stage mismatch.")
    if receipt.get("complete") is not True or receipt.get("trusted") is not True:
        errors.append("Public receipt must be complete and trusted.")
    if receipt.get("reportSchemaName") != PUBLIC_REPORT_SCHEMA:
        errors.append("Public receipt report schema mismatch.")
    for field in ("publicRunId", "reportRelativePath", "reportSha256"):
        if not isinstance(receipt.get(field), str) or not receipt.get(field):
            errors.append(f"Public receipt requires non-empty {field}.")
    if not isinstance(receipt.get("reportByteSize"), int) or receipt.get("reportByteSize") <= 0:
        errors.append("Public receipt requires positive reportByteSize.")
    return errors


def read_trusted_public_run_report(automation_root: Path, runtime_root: Path, public_run_root: Path, public_run_id: str) -> dict[str, Any]:
    receipt_path = public_run_root / PUBLIC_RECEIPT_NAME
    receipt_payload = _read_json_no_duplicates(receipt_path)
    _validate_schema_or_raise(automation_root, receipt_payload, PUBLIC_RECEIPT_SCHEMA, "PUBLIC_REAL_TASK_REPORT_WRITE_FAILED")
    receipt_errors = validate_public_receipt_report(receipt_payload)
    if receipt_errors:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_REPORT_WRITE_FAILED", receipt_errors[0])
    if receipt_payload.get("publicRunId") != public_run_id:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_REPORT_WRITE_FAILED", "Public receipt run id mismatch.")
    report_path = runtime_root / str(receipt_payload["reportRelativePath"])
    try:
        report_path.resolve(strict=False).relative_to(public_run_root.resolve(strict=False))
    except ValueError as exc:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_REPORT_WRITE_FAILED", "Public receipt report path escapes public run root.") from exc
    trusted_receipt = TrustedReportReceipt(
        relativePath=str(receipt_payload["reportRelativePath"]),
        size=int(receipt_payload["reportByteSize"]),
        sha256=str(receipt_payload["reportSha256"]),
        schemaName=str(receipt_payload["reportSchemaName"]),
        reportVersion=int(receipt_payload["reportVersion"]),
    )
    persisted = read_trusted_report(automation_root, runtime_root, report_path, trusted_receipt, PUBLIC_REPORT_SCHEMA, validate_public_real_task_report, MAX_PUBLIC_REPORT_BYTES)
    if persisted.get("publicRunId") != public_run_id or persisted.get("automationStage") != PUBLIC_REAL_TASK_STAGE:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_REPORT_WRITE_FAILED", "Public report identity mismatches receipt.")
    return persisted


def _create_public_run_root(public_run_root: Path, root: Path) -> None:
    runtime_parent = root / PUBLIC_RUN_ROOT
    runtime_parent.mkdir(parents=True, exist_ok=True)
    if _has_reparse_component(runtime_parent):
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_RUNTIME_PATH_UNSAFE", "Public run root parent is unsafe.")
    try:
        public_run_root.mkdir(exist_ok=False)
    except FileExistsError as exc:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_RUNTIME_PATH_UNSAFE", "Public run root already exists.") from exc
    except OSError as exc:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_RUNTIME_PATH_UNSAFE", f"Public run root could not be created: {type(exc).__name__}.") from exc


def _require_clean_parent(root: Path) -> dict[str, Any]:
    snapshot, errors = parent_git_snapshot(root)
    if errors:
        first = errors[0]
        raise RealTaskExecutionFailure(first.code, first.message)
    if snapshot.get("dirty") is not False:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PARENT_NOT_CLEAN", "Public real-task execution requires a fully clean parent repository.")
    state_error = _git_operation_state_error(root)
    if state_error is not None:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PARENT_NOT_CLEAN", state_error)
    return snapshot


def _require_parent_unchanged(root: Path, baseline: dict[str, Any] | None) -> None:
    if baseline is None:
        raise RealTaskExecutionFailure("PUBLIC_REAL_TASK_PARENT_NOT_CLEAN", "Public parent baseline is unavailable.")
    current = _require_clean_parent(root)
    if not snapshots_equal(baseline, current):
        raise RealTaskExecutionFailure("REAL_TASK_PARENT_GIT_CHANGED", "Parent Git snapshot changed during public real-task execution.")


def _git_operation_state_error(root: Path) -> str | None:
    git_dir = _git_dir(root, "--git-dir")
    common_dir = _git_dir(root, "--git-common-dir")
    if git_dir is None or common_dir is None:
        return "Parent Git directory could not be resolved."
    checks = {
        "merge": [common_dir / "MERGE_HEAD"],
        "rebase": [common_dir / "rebase-merge", common_dir / "rebase-apply", git_dir / "rebase-merge", git_dir / "rebase-apply"],
        "cherry-pick": [common_dir / "CHERRY_PICK_HEAD"],
        "revert": [common_dir / "REVERT_HEAD"],
        "bisect": [common_dir / "BISECT_LOG"],
        "sequencer": [common_dir / "sequencer"],
    }
    for name, paths in checks.items():
        for path in paths:
            try:
                if path.exists():
                    return f"Parent Git {name} operation is in progress."
            except OSError:
                return "Parent Git operation state could not be inspected."
    return None


def _git_dir(root: Path, flag: str) -> Path | None:
    result = _run_git(root, ["git", "rev-parse", "--path-format=absolute", flag])
    if not result["success"]:
        return None
    line = result["stdout"].strip().splitlines()
    if len(line) != 1 or not line[0]:
        return None
    return Path(line[0])


def _verify_manifest_tracked_and_committed(root: Path, relative_path: str, data: bytes) -> tuple[bool, str | None]:
    ls_files = _run_git(root, ["git", "ls-files", "--error-unmatch", "--", relative_path])
    if not ls_files["success"]:
        return False, "PUBLIC_REAL_TASK_MANIFEST_NOT_TRACKED"
    cat = _run_git(root, ["git", "cat-file", "-e", f"HEAD:{relative_path}"])
    if not cat["success"]:
        return False, "PUBLIC_REAL_TASK_MANIFEST_NOT_TRACKED"
    head_blob = _run_git_bytes(root, ["git", "show", f"HEAD:{relative_path}"])
    if not head_blob["success"]:
        return False, "PUBLIC_REAL_TASK_MANIFEST_NOT_COMMITTED"
    if hashlib.sha256(head_blob["stdout"]).hexdigest() != hashlib.sha256(data).hexdigest():
        return False, "PUBLIC_REAL_TASK_MANIFEST_NOT_COMMITTED"
    staged = _run_git(root, ["git", "diff", "--cached", "--quiet", "--", relative_path])
    worktree = _run_git(root, ["git", "diff", "--quiet", "--", relative_path])
    if not staged["success"] or not worktree["success"]:
        return False, "PUBLIC_REAL_TASK_MANIFEST_NOT_COMMITTED"
    return True, None


def _run_git(root: Path, command: list[str]) -> dict[str, Any]:
    try:
        completed = subprocess.run(command, cwd=str(root), capture_output=True, text=True, check=False, timeout=GIT_TIMEOUT_SECONDS)
    except (OSError, subprocess.SubprocessError) as exc:
        return {"success": False, "stdout": "", "stderr": type(exc).__name__}
    return {"success": completed.returncode == 0, "stdout": completed.stdout, "stderr": completed.stderr}


def _run_git_bytes(root: Path, command: list[str]) -> dict[str, Any]:
    try:
        completed = subprocess.run(command, cwd=str(root), capture_output=True, check=False, timeout=GIT_TIMEOUT_SECONDS)
    except (OSError, subprocess.SubprocessError) as exc:
        return {"success": False, "stdout": b"", "stderr": type(exc).__name__}
    return {"success": completed.returncode == 0, "stdout": completed.stdout, "stderr": completed.stderr}


class _PublicRunLock:
    def __init__(self, path: Path) -> None:
        self.path = path
        self.public_run_id: str | None = None
        self.manifest_sha256: str | None = None
        self.owner_token: str | None = None
        self.owner_token_hash: str | None = None
        self.identity: PublicFileIdentity | None = None
        self._fd: int | None = None
        self._win_handle: int | None = None
        self._released = False
        self._release_failed = False
        self._win_close_attempted = False
        self._win_close_succeeded = False

    def acquire(self, public_run_id: str, manifest_relative_path: str, manifest_sha256: str) -> str | None:
        self.public_run_id = public_run_id
        self.manifest_sha256 = manifest_sha256
        self.owner_token = os.urandom(32).hex()
        self.owner_token_hash = hashlib.sha256(self.owner_token.encode("ascii")).hexdigest()
        self.path.parent.mkdir(parents=True, exist_ok=True)
        if _has_reparse_component(self.path.parent):
            return "Public real-task lock directory is unsafe."
        metadata = {
            "lockVersion": 1,
            "publicRunId": public_run_id,
            "manifestRelativePath": manifest_relative_path,
            "manifestSha256": manifest_sha256,
            "processId": os.getpid(),
            "startedAt": utc_now(),
            "ownerTokenHash": self.owner_token_hash,
        }
        try:
            if os.name == "nt":
                self._win_handle = _win_create_lock_handle(self.path)
                _win_write_all(self._win_handle, canonical_json_bytes(metadata) + b"\n")
                _win_flush(self._win_handle)
                self.identity = _win_file_identity(self._win_handle)
                reread = _win_read_lock_metadata(self._win_handle)
            else:
                flags = os.O_CREAT | os.O_EXCL | os.O_RDWR
                if hasattr(os, "O_BINARY"):
                    flags |= os.O_BINARY
                self._fd = os.open(_fs_path(self.path), flags, 0o600)
                os.write(self._fd, canonical_json_bytes(metadata) + b"\n")
                os.fsync(self._fd)
                status = os.fstat(self._fd)
                if not stat.S_ISREG(status.st_mode) or _stat_is_reparse(status):
                    self._close_handle()
                    return "Public real-task lock target is unsafe."
                self.identity = _public_file_identity(status)
                reread = _read_lock_metadata_from_fd(self._fd)
        except FileExistsError:
            return "Another public real-task run is already active, or a previous lock requires manual review."
        except OSError as exc:
            self._close_handle()
            return f"Public real-task lock could not be acquired: {type(exc).__name__}."
        except Exception:
            self._close_handle()
            return "Public real-task lock ownership could not be verified."
        if reread != metadata:
            self._close_handle()
            return "Public real-task lock metadata changed after creation."
        return None

    def release(self) -> tuple[str, str] | None:
        if not self.public_run_id or not self.manifest_sha256 or not self.owner_token_hash or self.identity is None:
            return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock was not owned by this process.")
        if self._released:
            return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock was already released.")
        if self._release_failed:
            return ("PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED", "Public real-task lock release previously failed.")
        try:
            if os.name == "nt":
                if self._win_handle is None:
                    return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock handle is missing.")
                if _win_file_identity(self._win_handle) != self.identity:
                    return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock handle identity changed.")
                metadata = _win_read_lock_metadata(self._win_handle)
            else:
                if self._fd is None:
                    return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock fd is missing.")
                fd_status = os.fstat(self._fd)
                if _public_file_identity(fd_status) != self.identity:
                    return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock fd identity changed.")
                path_status = self.path.stat(follow_symlinks=False)
                if _public_file_identity(path_status) != self.identity:
                    return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock path was replaced before release.")
                metadata = _read_lock_metadata_from_fd(self._fd)
        except Exception:
            return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock metadata could not be reread.")
        if (
            metadata.get("publicRunId") != self.public_run_id
            or metadata.get("manifestSha256") != self.manifest_sha256
            or metadata.get("ownerTokenHash") != self.owner_token_hash
        ):
            return ("PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST", "Public real-task lock ownership metadata changed.")
        try:
            if os.name == "nt":
                if self._win_handle is None:
                    return ("PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED", "Public real-task lock could not be released by handle.")
                delete_error: OSError | None = None
                close_error: OSError | None = None
                try:
                    _win_delete_by_handle(self._win_handle)
                except OSError as exc:
                    delete_error = exc
                try:
                    self._close_handle()
                except OSError as exc:
                    close_error = exc
                if delete_error is not None or close_error is not None:
                    self._release_failed = True
                    return ("PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED", _format_win_lock_release_error(delete_error, close_error))
            else:
                if self._fd is None:
                    return ("PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED", "Public real-task lock fd is missing.")
                os.unlink(_fs_path(self.path))
                os.close(self._fd)
                self._fd = None
            self._released = True
        except OSError:
            return ("PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED", "Public real-task lock could not be released.")
        return None

    def _close_handle(self) -> None:
        if self._fd is not None:
            try:
                os.close(self._fd)
            finally:
                self._fd = None
        if self._win_handle is not None:
            handle = self._win_handle
            self._win_handle = None
            self._win_close_attempted = True
            _win_close_handle(handle)
            self._win_close_succeeded = True


def _format_win_lock_release_error(delete_error: OSError | None, close_error: OSError | None) -> str:
    if delete_error is not None:
        message = _bounded_os_error("Public real-task lock delete-by-handle failed", delete_error)
        if close_error is not None:
            message += "; secondary " + _bounded_os_error("CloseHandle failed", close_error)
        return message
    if close_error is not None:
        return _bounded_os_error("Public real-task lock CloseHandle failed", close_error)
    return "Public real-task lock could not be released."


def _bounded_os_error(prefix: str, exc: OSError) -> str:
    code = exc.errno if isinstance(exc.errno, int) else -1
    return f"{prefix}: {code}."


def _read_lock_metadata(path: Path) -> dict[str, Any]:
    raw = path.read_bytes()
    return _parse_lock_metadata(raw)


def _read_lock_metadata_from_fd(fd: int) -> dict[str, Any]:
    os.lseek(fd, 0, os.SEEK_SET)
    raw = os.read(fd, 8192)
    return _parse_lock_metadata(raw)


def _parse_lock_metadata(raw: bytes) -> dict[str, Any]:
    loaded = json.loads(raw.decode("utf-8"))
    if not isinstance(loaded, dict):
        raise ValueError("lock metadata root must be object")
    required = {"lockVersion", "publicRunId", "manifestRelativePath", "manifestSha256", "processId", "startedAt", "ownerTokenHash"}
    if set(loaded) != required:
        raise ValueError("lock metadata fields mismatch")
    if loaded.get("lockVersion") != 1:
        raise ValueError("lock version mismatch")
    return loaded


def _has_reparse_component(path: Path) -> bool:
    current = Path(path.anchor)
    for part in path.parts[1:]:
        current = current / part
        try:
            status = current.stat(follow_symlinks=False)
        except FileNotFoundError:
            continue
        except OSError:
            return True
        if _stat_is_reparse(status):
            return True
    return False


def _stat_is_reparse(status: os.stat_result) -> bool:
    return stat.S_ISLNK(status.st_mode) or bool(getattr(status, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _public_file_identity(status: os.stat_result) -> PublicFileIdentity:
    return PublicFileIdentity(
        platform=sys.platform,
        device=int(getattr(status, "st_dev", 0)),
        file_id=int(getattr(status, "st_ino", 0)),
        mode=int(getattr(status, "st_mode", 0)),
        attributes=int(getattr(status, "st_file_attributes", 0)),
        size=int(getattr(status, "st_size", 0)),
        ctime_ns=int(getattr(status, "st_ctime_ns", 0)),
        is_reparse=_stat_is_reparse(status),
    )


def _win_create_lock_handle(path: Path) -> int:
    GENERIC_READ = 0x80000000
    GENERIC_WRITE = 0x40000000
    DELETE = 0x00010000
    FILE_SHARE_READ = 0x00000001
    CREATE_NEW = 1
    FILE_ATTRIBUTE_NORMAL = 0x00000080
    FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000
    api = _get_win32_kernel_api()
    _set_win32_last_error_zero()
    handle = api.CreateFileW(
        _fs_path(path),
        GENERIC_READ | GENERIC_WRITE | DELETE,
        FILE_SHARE_READ,
        None,
        CREATE_NEW,
        FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT,
        None,
    )
    normalized = _normalize_win_handle(handle)
    if normalized is None or normalized == INVALID_WIN32_HANDLE_VALUE:
        error = _get_win32_last_error()
        if error == 80:
            raise FileExistsError(str(path))
        _raise_win32_error("CreateFileW", error)
    return normalized


def _win_write_all(handle: int, data: bytes) -> None:
    if len(data) > 0xFFFFFFFF:
        raise OSError("WriteFile payload exceeds DWORD length.")
    api = _get_win32_kernel_api()
    buffer = ctypes.create_string_buffer(data)
    written = wintypes.DWORD(0)
    _set_win32_last_error_zero()
    if not api.WriteFile(_win_handle(handle), buffer, wintypes.DWORD(len(data)), ctypes.byref(written), None):
        _raise_win32_error("WriteFile")
    if written.value != len(data):
        raise OSError("short WriteFile")


def _win_flush(handle: int) -> None:
    api = _get_win32_kernel_api()
    _set_win32_last_error_zero()
    if not api.FlushFileBuffers(_win_handle(handle)):
        _raise_win32_error("FlushFileBuffers")


def _win_read_lock_metadata(handle: int) -> dict[str, Any]:
    return _parse_lock_metadata(_win_read_all(handle))


def _win_read_all(handle: int) -> bytes:
    api = _get_win32_kernel_api()
    distance = ctypes.c_longlong(0)
    _set_win32_last_error_zero()
    if not api.SetFilePointerEx(_win_handle(handle), distance, None, 0):
        _raise_win32_error("SetFilePointerEx")
    buffer = ctypes.create_string_buffer(8192)
    read = wintypes.DWORD(0)
    _set_win32_last_error_zero()
    if not api.ReadFile(_win_handle(handle), buffer, wintypes.DWORD(len(buffer)), ctypes.byref(read), None):
        _raise_win32_error("ReadFile")
    return bytes(buffer.raw[: read.value])


def _win_file_identity(handle: int) -> PublicFileIdentity:
    api = _get_win32_kernel_api()
    info = BY_HANDLE_FILE_INFORMATION()
    _set_win32_last_error_zero()
    if not api.GetFileInformationByHandle(_win_handle(handle), ctypes.byref(info)):
        _raise_win32_error("GetFileInformationByHandle")
    size = (int(info.nFileSizeHigh) << 32) | int(info.nFileSizeLow)
    file_id = (int(info.nFileIndexHigh) << 32) | int(info.nFileIndexLow)
    ctime_ticks = (int(info.ftCreationTime.dwHighDateTime) << 32) | int(info.ftCreationTime.dwLowDateTime)
    attrs = int(info.dwFileAttributes)
    return PublicFileIdentity(
        platform=sys.platform,
        device=int(info.dwVolumeSerialNumber),
        file_id=file_id,
        mode=int(stat.S_IFREG),
        attributes=attrs,
        size=size,
        ctime_ns=ctime_ticks * 100,
        is_reparse=bool(attrs & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)),
    )


def _win_delete_by_handle(handle: int) -> None:
    api = _get_win32_kernel_api()
    FileDispositionInfo = 4
    info = FILE_DISPOSITION_INFO(1)
    _set_win32_last_error_zero()
    if not api.SetFileInformationByHandle(_win_handle(handle), FileDispositionInfo, ctypes.byref(info), wintypes.DWORD(ctypes.sizeof(info))):
        _raise_win32_error("SetFileInformationByHandle")


def _win_close_handle(handle: int) -> bool:
    normalized = _normalize_win_handle(handle)
    if normalized is None or normalized == INVALID_WIN32_HANDLE_VALUE:
        return True
    api = _get_win32_kernel_api()
    _set_win32_last_error_zero()
    if not api.CloseHandle(_win_handle(normalized)):
        _raise_win32_error("CloseHandle")
    return True


def _get_win32_kernel_api() -> _Win32KernelApi:
    global _WIN32_KERNEL_API
    if os.name != "nt":
        raise OSError("Win32 Kernel32 API is only available on Windows.")
    if _WIN32_KERNEL_API is None:
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        _WIN32_KERNEL_API = _configure_win32_kernel_api(kernel32)
    return _WIN32_KERNEL_API


def _configure_win32_kernel_api(kernel32: object) -> _Win32KernelApi:
    create_file = kernel32.CreateFileW
    create_file.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        ctypes.c_void_p,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    create_file.restype = wintypes.HANDLE

    write_file = kernel32.WriteFile
    write_file.argtypes = [
        wintypes.HANDLE,
        ctypes.c_void_p,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
        ctypes.c_void_p,
    ]
    write_file.restype = wintypes.BOOL

    read_file = kernel32.ReadFile
    read_file.argtypes = [
        wintypes.HANDLE,
        ctypes.c_void_p,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
        ctypes.c_void_p,
    ]
    read_file.restype = wintypes.BOOL

    set_file_pointer = kernel32.SetFilePointerEx
    set_file_pointer.argtypes = [
        wintypes.HANDLE,
        ctypes.c_longlong,
        ctypes.POINTER(ctypes.c_longlong),
        wintypes.DWORD,
    ]
    set_file_pointer.restype = wintypes.BOOL

    flush_file = kernel32.FlushFileBuffers
    flush_file.argtypes = [wintypes.HANDLE]
    flush_file.restype = wintypes.BOOL

    get_file_size = kernel32.GetFileSizeEx
    get_file_size.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(ctypes.c_longlong),
    ]
    get_file_size.restype = wintypes.BOOL

    get_file_info = kernel32.GetFileInformationByHandle
    get_file_info.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(BY_HANDLE_FILE_INFORMATION),
    ]
    get_file_info.restype = wintypes.BOOL

    set_file_info = kernel32.SetFileInformationByHandle
    set_file_info.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        ctypes.c_void_p,
        wintypes.DWORD,
    ]
    set_file_info.restype = wintypes.BOOL

    close_handle = kernel32.CloseHandle
    close_handle.argtypes = [wintypes.HANDLE]
    close_handle.restype = wintypes.BOOL

    return _Win32KernelApi(
        kernel32=kernel32,
        CreateFileW=create_file,
        WriteFile=write_file,
        ReadFile=read_file,
        SetFilePointerEx=set_file_pointer,
        FlushFileBuffers=flush_file,
        GetFileSizeEx=get_file_size,
        GetFileInformationByHandle=get_file_info,
        SetFileInformationByHandle=set_file_info,
        CloseHandle=close_handle,
    )


def _normalize_win_handle(handle: object) -> int | None:
    if handle is None:
        return None
    if isinstance(handle, ctypes.c_void_p):
        return None if handle.value is None else int(handle.value)
    return ctypes.c_void_p(handle).value


def _win_handle(handle: int) -> wintypes.HANDLE:
    normalized = _normalize_win_handle(handle)
    if normalized is None:
        raise OSError("Win32 handle is null.")
    return wintypes.HANDLE(normalized)


def _raise_win32_error(operation: str, error_code: int | None = None) -> None:
    code = int(_get_win32_last_error() if error_code is None else error_code)
    if code == 0:
        code = -1
    raise OSError(code, f"{operation} failed: {code}")


def _set_win32_last_error_zero() -> None:
    setter = getattr(ctypes, "set_last_error", None)
    if setter is not None:
        setter(0)


def _get_win32_last_error() -> int:
    getter = getattr(ctypes, "get_last_error", None)
    if getter is None:
        return 0
    return int(getter())


def _fs_path(path: Path) -> str:
    text = str(path.resolve(strict=False))
    if os.name == "nt" and not text.startswith("\\\\?\\"):
        return "\\\\?\\" + text
    return text


def _relative_runtime_path(root: Path, path: Path | None) -> str | None:
    if path is None:
        return None
    try:
        return path.resolve(strict=False).relative_to((root / "CodexAutomation" / "runtime").resolve(strict=False)).as_posix()
    except ValueError:
        return None


def _task_id(manifest: dict[str, Any] | None) -> str | None:
    if isinstance(manifest, dict) and isinstance(manifest.get("taskId"), str):
        return manifest["taskId"]
    return None


def _int_report(execution: RealTaskExecutionResult | None, field: str) -> int:
    if execution is None:
        return 0
    value = execution.report.get(field)
    return _int_value(value)


def _int_value(value: Any) -> int:
    return value if isinstance(value, int) and not isinstance(value, bool) else 0

