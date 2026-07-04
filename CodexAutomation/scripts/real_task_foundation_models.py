from __future__ import annotations

import hashlib
import json
import secrets
from dataclasses import dataclass
from datetime import datetime, timezone
from enum import Enum
from pathlib import Path, PureWindowsPath
from typing import Any

from real_task_models import RealTaskError, RealTaskPolicy


REAL_TASK_FOUNDATION_STAGE = "BOOTSTRAP-03B-2B-A"
FOUNDATION_CONTEXT_VERSION = 1
FOUNDATION_REPORT_VERSION = 1

FOUNDATION_ERROR_CODES = {
    "REAL_TASK_CONTEXT_MISMATCH",
    "REAL_TASK_RUNTIME_PATH_UNSAFE",
    "REAL_TASK_RUN_DIRECTORY_EXISTS",
    "REAL_TASK_TRUSTED_CONTEXT_INVALID",
    "REAL_TASK_WORKSPACE_CREATE_FAILED",
    "REAL_TASK_WORKSPACE_STAGING_INVALID",
    "REAL_TASK_WORKSPACE_PROMOTION_FAILED",
    "REAL_TASK_SOURCE_CHANGED",
    "REAL_TASK_SOURCE_COPY_FAILED",
    "REAL_TASK_SOURCE_HASH_MISMATCH",
    "REAL_TASK_DESTINATION_ESCAPE",
    "REAL_TASK_DESTINATION_REPARSE",
    "REAL_TASK_UNSUPPORTED_FILE_TYPE",
    "REAL_TASK_SERVICE_FILE_INVALID",
    "REAL_TASK_ISOLATED_GIT_INVALID",
    "REAL_TASK_PARENT_GIT_CHANGED",
    "REAL_TASK_INVENTORY_FAILED",
    "REAL_TASK_PATH_COLLISION",
    "REAL_TASK_FOUNDATION_REPORT_INVALID",
    "REAL_TASK_FOUNDATION_REPORT_WRITE_FAILED",
}


class FoundationState(str, Enum):
    PENDING = "PENDING"
    MANIFEST_VALIDATING = "MANIFEST_VALIDATING"
    CONTEXT_CREATING = "CONTEXT_CREATING"
    SOURCE_SNAPSHOTTING = "SOURCE_SNAPSHOTTING"
    WORKSPACE_STAGING = "WORKSPACE_STAGING"
    SERVICE_FILES_CREATING = "SERVICE_FILES_CREATING"
    SOURCE_COPYING = "SOURCE_COPYING"
    SOURCE_POST_VERIFYING = "SOURCE_POST_VERIFYING"
    DESTINATION_VERIFYING = "DESTINATION_VERIFYING"
    PARENT_GIT_VERIFYING = "PARENT_GIT_VERIFYING"
    WORKSPACE_PROMOTING = "WORKSPACE_PROMOTING"
    WORKSPACE_BASELINE_VERIFYING = "WORKSPACE_BASELINE_VERIFYING"
    REPORTING = "REPORTING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    BLOCKED = "BLOCKED"


@dataclass(frozen=True)
class FoundationPolicy:
    runtimeRoot: str
    retainFailedStaging: bool
    preserveEmptyDirectories: bool
    autoPairUnityMeta: bool
    createStandaloneGit: bool
    createBaselineCommit: bool
    copyChunkBytes: int
    maxInventoryEntries: int
    maxReportBytes: int

    def to_dict(self) -> dict[str, Any]:
        return {
            "runtimeRoot": self.runtimeRoot,
            "retainFailedStaging": self.retainFailedStaging,
            "preserveEmptyDirectories": self.preserveEmptyDirectories,
            "autoPairUnityMeta": self.autoPairUnityMeta,
            "createStandaloneGit": self.createStandaloneGit,
            "createBaselineCommit": self.createBaselineCommit,
            "copyChunkBytes": self.copyChunkBytes,
            "maxInventoryEntries": self.maxInventoryEntries,
            "maxReportBytes": self.maxReportBytes,
            "textEncodings": ["utf-8", "utf-8-bom"],
            "utf16OutputsAllowed": False,
            "extensionlessOutputsAllowed": False,
            "binaryDeletionAllowed": False,
            "renameRepresentation": "delete_add",
        }


@dataclass(frozen=True)
class TrustedFoundationContext:
    contextVersion: int
    runId: str
    taskId: str
    stage: str
    createdAt: str
    repositoryRoot: str
    runtimeRoot: str
    runDirectory: str
    stagingDirectory: str
    workspaceDirectory: str
    evidenceDirectory: str
    logsDirectory: str
    manifestDisplayName: str | None
    manifestPath: str
    manifestSha256: str
    effectiveHostPolicy: dict[str, Any]
    effectiveFoundationPolicy: dict[str, Any]
    sourcePaths: tuple[str, ...]
    allowedWritePaths: tuple[str, ...]
    allowedDeletePaths: tuple[str, ...]
    sourceLimits: dict[str, int]
    outputLimits: dict[str, int]
    parentGitInitialSnapshot: dict[str, Any]
    noModel: bool
    noCodex: bool
    noSandbox: bool
    noUnity: bool

    def to_dict(self) -> dict[str, Any]:
        return {
            "contextVersion": self.contextVersion,
            "runId": self.runId,
            "taskId": self.taskId,
            "stage": self.stage,
            "createdAt": self.createdAt,
            "repositoryRoot": self.repositoryRoot,
            "runtimeRoot": self.runtimeRoot,
            "runDirectory": self.runDirectory,
            "stagingDirectory": self.stagingDirectory,
            "workspaceDirectory": self.workspaceDirectory,
            "evidenceDirectory": self.evidenceDirectory,
            "logsDirectory": self.logsDirectory,
            "manifestDisplayName": self.manifestDisplayName,
            "manifestPath": self.manifestPath,
            "manifestSha256": self.manifestSha256,
            "effectiveHostPolicy": self.effectiveHostPolicy,
            "effectiveFoundationPolicy": self.effectiveFoundationPolicy,
            "sourcePaths": list(self.sourcePaths),
            "allowedWritePaths": list(self.allowedWritePaths),
            "allowedDeletePaths": list(self.allowedDeletePaths),
            "sourceLimits": self.sourceLimits,
            "outputLimits": self.outputLimits,
            "parentGitInitialSnapshot": self.parentGitInitialSnapshot,
            "noModel": self.noModel,
            "noCodex": self.noCodex,
            "noSandbox": self.noSandbox,
            "noUnity": self.noUnity,
        }


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def new_foundation_run_id() -> str:
    stamp = utc_now().replace(":", "").replace("-", "").replace(".", "")
    return f"foundation_{stamp}_{secrets.token_hex(4)}"


def canonical_json_bytes(data: Any) -> bytes:
    return json.dumps(data, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def canonical_sha256(data: Any) -> str:
    return hashlib.sha256(canonical_json_bytes(data)).hexdigest()


def parse_foundation_policy(root: Path, raw_config: dict[str, Any], real_task_policy: RealTaskPolicy | None = None) -> tuple[FoundationPolicy | None, list[RealTaskError]]:
    raw = raw_config.get("realTaskFoundation")
    errors: list[RealTaskError] = []
    required = {
        "runtimeRoot",
        "retainFailedStaging",
        "preserveEmptyDirectories",
        "autoPairUnityMeta",
        "createStandaloneGit",
        "createBaselineCommit",
        "copyChunkBytes",
        "maxInventoryEntries",
        "maxReportBytes",
    }
    if not isinstance(raw, dict):
        return None, [_error("REAL_TASK_CONFIG_INVALID", "realTaskFoundation must be an object.", "realTaskFoundation")]
    for field in sorted(set(raw) - required):
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Unknown realTaskFoundation field.", f"realTaskFoundation.{field}"))
    for field in sorted(required - set(raw)):
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "Missing realTaskFoundation field.", f"realTaskFoundation.{field}"))
    runtime_root = raw.get("runtimeRoot")
    if not isinstance(runtime_root, str) or not runtime_root.strip():
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "runtimeRoot must be a non-empty string.", "realTaskFoundation.runtimeRoot"))
        runtime_root = ""
    elif _unsafe_runtime_root(root, runtime_root):
        errors.append(_error("REAL_TASK_RUNTIME_PATH_UNSAFE", "runtimeRoot must be a repo-relative path under CodexAutomation/runtime.", "realTaskFoundation.runtimeRoot"))
    exact_bools = {
        "retainFailedStaging": True,
        "preserveEmptyDirectories": True,
        "autoPairUnityMeta": False,
        "createStandaloneGit": True,
        "createBaselineCommit": False,
    }
    for field, expected in exact_bools.items():
        if raw.get(field) is not expected:
            errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be exact {expected}.", f"realTaskFoundation.{field}"))
    copy_chunk = _bounded_int(raw, "copyChunkBytes", 65536, 8 * 1024 * 1024, errors)
    max_entries = _bounded_int(raw, "maxInventoryEntries", 1, 100000, errors)
    max_report = _bounded_int(raw, "maxReportBytes", 1024, 64 * 1024 * 1024, errors)
    if real_task_policy is not None and max_entries < real_task_policy.maxSourceFiles:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", "maxInventoryEntries must be at least maxSourceFiles.", "realTaskFoundation.maxInventoryEntries"))
    if errors:
        return None, errors
    return FoundationPolicy(
        str(runtime_root),
        True,
        True,
        False,
        True,
        False,
        copy_chunk,
        max_entries,
        max_report,
    ), []


def _bounded_int(raw: dict[str, Any], field: str, minimum: int, maximum: int, errors: list[RealTaskError]) -> int:
    value = raw.get(field)
    if not isinstance(value, int) or isinstance(value, bool):
        errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be an integer.", f"realTaskFoundation.{field}"))
        return minimum
    if value < minimum or value > maximum:
        errors.append(_error("REAL_TASK_CONFIG_INVALID", f"{field} must be from {minimum} to {maximum}.", f"realTaskFoundation.{field}"))
        return value
    return value


def _unsafe_runtime_root(root: Path, value: str) -> bool:
    raw = value.strip().replace("\\", "/")
    windows = PureWindowsPath(raw)
    if Path(raw).is_absolute() or windows.drive or windows.root or raw.startswith(("/", "\\")):
        return True
    parts = [part for part in raw.split("/") if part]
    if parts[:2] != ["CodexAutomation", "runtime"] or len(parts) < 3:
        return True
    if any(part in {"..", "."} or not part.strip() for part in parts):
        return True
    try:
        resolved = (root / raw).resolve(strict=False)
        runtime = (root / "CodexAutomation" / "runtime").resolve(strict=False)
        resolved.relative_to(runtime)
    except (OSError, ValueError):
        return True
    forbidden = {root.resolve(strict=False), (root / "CodexAutomation").resolve(strict=False)}
    return resolved in forbidden


def _error(code: str, message: str, field: str | None = None) -> RealTaskError:
    return RealTaskError(code, message, field)
