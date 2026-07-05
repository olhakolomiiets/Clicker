from __future__ import annotations

import hashlib
import json
import secrets
from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from pathlib import Path
from typing import Any


REAL_TASK_CHANGE_STAGE = "BOOTSTRAP-03B-2B-B"
CHANGE_HANDLE_VERSION = 1
CHANGE_REPORT_VERSION = 1


CHANGE_ERROR_CODES = {
    "REAL_TASK_CHANGE_CONTEXT_MISMATCH",
    "REAL_TASK_CHANGE_BASELINE_INVALID",
    "REAL_TASK_CHANGE_HANDLE_REUSED",
    "REAL_TASK_FINAL_INVENTORY_FAILED",
    "REAL_TASK_DIFF_INVALID",
    "REAL_TASK_DIFF_LIMIT_EXCEEDED",
    "REAL_TASK_FORBIDDEN_CHANGE",
    "REAL_TASK_FORBIDDEN_DELETE",
    "REAL_TASK_BINARY_CHANGE_FORBIDDEN",
    "REAL_TASK_TEXT_CHANGE_INVALID",
    "REAL_TASK_EXPECTED_OUTPUT_MISSING",
    "REAL_TASK_PARENT_SOURCE_CHANGED",
    "REAL_TASK_CHANGE_REPORT_INVALID",
    "REAL_TASK_CHANGE_REPORT_WRITE_FAILED",
    "REAL_TASK_CONTEXT_MISMATCH",
    "REAL_TASK_RUNTIME_PATH_UNSAFE",
    "REAL_TASK_SOURCE_REPARSE",
    "REAL_TASK_INVENTORY_FAILED",
    "REAL_TASK_PATH_COLLISION",
    "REAL_TASK_SERVICE_FILE_INVALID",
    "REAL_TASK_ISOLATED_GIT_INVALID",
    "REAL_TASK_GIT_SNAPSHOT_FAILED",
    "REAL_TASK_PARENT_GIT_CHANGED",
    "REAL_TASK_INTERNAL_ERROR",
}


class ChangeState(str, Enum):
    PENDING = "PENDING"
    CONTEXT_REVALIDATING = "CONTEXT_REVALIDATING"
    BASELINE_REVALIDATING = "BASELINE_REVALIDATING"
    CHANGE_BASELINE_INVENTORY = "CHANGE_BASELINE_INVENTORY"
    TRACKING_READY = "TRACKING_READY"
    FINAL_INVENTORY = "FINAL_INVENTORY"
    SERVICE_GUARDS = "SERVICE_GUARDS"
    DIFFING = "DIFFING"
    LIMITS_ENFORCING = "LIMITS_ENFORCING"
    SCOPE_ENFORCING = "SCOPE_ENFORCING"
    BINARY_TEXT_ENFORCING = "BINARY_TEXT_ENFORCING"
    EXPECTED_OUTPUTS_CHECKING = "EXPECTED_OUTPUTS_CHECKING"
    PARENT_VERIFYING = "PARENT_VERIFYING"
    REPORTING = "REPORTING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    BLOCKED = "BLOCKED"


@dataclass(frozen=True)
class ChangeTrackingHandle:
    handleVersion: int
    trackingId: str
    capability: str = field(repr=False)
    runId: str
    taskId: str
    stage: str
    trustedContextHash: str
    manifestSha256: str
    workspaceDirectory: str
    runDirectory: str
    trackingReportPath: str
    trackingReportHash: str
    baselineInventoryPath: str
    baselineInventoryHash: str
    serviceRegistryHash: str
    isolatedGitFingerprintHash: str
    sourcePostInventoryHash: str
    parentGitTrackingStartSnapshot: dict[str, Any]
    noModel: bool
    noCodex: bool
    noSandbox: bool
    noUnity: bool

    def to_dict(self) -> dict[str, Any]:
        return {
            "handleVersion": self.handleVersion,
            "trackingId": self.trackingId,
            "runId": self.runId,
            "taskId": self.taskId,
            "stage": self.stage,
            "trustedContextHash": self.trustedContextHash,
            "manifestSha256": self.manifestSha256,
            "workspaceDirectory": self.workspaceDirectory,
            "runDirectory": self.runDirectory,
            "trackingReportPath": self.trackingReportPath,
            "trackingReportHash": self.trackingReportHash,
            "baselineInventoryPath": self.baselineInventoryPath,
            "baselineInventoryHash": self.baselineInventoryHash,
            "serviceRegistryHash": self.serviceRegistryHash,
            "isolatedGitFingerprintHash": self.isolatedGitFingerprintHash,
            "sourcePostInventoryHash": self.sourcePostInventoryHash,
            "parentGitTrackingStartSnapshot": self.parentGitTrackingStartSnapshot,
            "noModel": self.noModel,
            "noCodex": self.noCodex,
            "noSandbox": self.noSandbox,
            "noUnity": self.noUnity,
        }


@dataclass(frozen=True)
class ChangeAnalysisResult:
    finalVerdict: str
    finalState: str
    report: dict[str, Any]
    reportPath: str | None


@dataclass(frozen=True)
class ChangeFailure(Exception):
    code: str
    message: str
    verdict: str = "BLOCKED"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def new_tracking_id(run_id: str) -> str:
    return f"change_tracking_{run_id}_{secrets.token_hex(4)}"


def new_handle_capability() -> str:
    return secrets.token_hex(32)


def canonical_json_bytes(data: Any) -> bytes:
    return json.dumps(data, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def canonical_sha256(data: Any) -> str:
    return hashlib.sha256(canonical_json_bytes(data)).hexdigest()


def file_sha256(path: Path, chunk_bytes: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(chunk_bytes)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()
