from __future__ import annotations

import hashlib
import json
import secrets
from dataclasses import dataclass
from datetime import datetime, timezone
from enum import Enum
from pathlib import Path
from typing import Any

from real_task_models import RealTaskError


REAL_TASK_EXECUTION_STAGE = "BOOTSTRAP-03B-2C"
EXECUTION_CONTEXT_VERSION = 1
EXECUTION_REPORT_VERSION = 1
EXECUTION_HANDLE_VERSION = 1


class ExecutionState(str, Enum):
    PENDING = "PENDING"
    MANIFEST_REVALIDATING = "MANIFEST_REVALIDATING"
    FOUNDATION_PREPARING = "FOUNDATION_PREPARING"
    CONTEXT_CREATING = "CONTEXT_CREATING"
    SANDBOX_PROBING = "SANDBOX_PROBING"
    IMPLEMENTING = "IMPLEMENTING"
    DIAGNOSTIC_ANALYZING = "DIAGNOSTIC_ANALYZING"
    REPAIR_DECIDING = "REPAIR_DECIDING"
    REPAIRING = "REPAIRING"
    FINAL_CHANGE_ANALYZING = "FINAL_CHANGE_ANALYZING"
    FINAL_VALIDATING = "FINAL_VALIDATING"
    AUDITING = "AUDITING"
    BUNDLING = "BUNDLING"
    REPORTING = "REPORTING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    BLOCKED = "BLOCKED"
    RATE_LIMITED = "RATE_LIMITED"


@dataclass(frozen=True)
class RealTaskExecutionPolicy:
    enabled: bool
    publicRunCliEnabled: bool
    controlledRealModelSelfTestEnabled: bool
    maxRoleInvocations: int
    maxRepairAttempts: int
    implementerEnabled: bool
    repairerEnabled: bool
    auditorEnabled: bool
    requireFreshSandboxProbe: bool
    requireFinalHostValidationPass: bool
    requireAuditorAfterHostPass: bool
    allowParentWrite: bool
    allowAutomaticApply: bool
    allowGitCommit: bool
    allowGitPush: bool
    allowPullRequest: bool
    allowNetwork: bool
    allowPackageInstall: bool
    allowUnity: bool
    allowAutomaticRetry: bool
    allowRateLimitRetry: bool
    allowModelDowngrade: bool
    allowCreditUse: bool
    maxRoleReportBytes: int
    maxDiagnosticReportBytes: int
    maxBundleBytes: int
    maxBundleFiles: int
    maxPromptBytes: int
    maxRoleMessageBytes: int
    maxEventLogBytes: int
    maxAuditFindings: int

    def to_dict(self) -> dict[str, Any]:
        return self.__dict__.copy()


@dataclass(frozen=True)
class InvocationBudget:
    maxRoleInvocations: int
    maxRepairAttempts: int
    invocationsUsed: int = 0
    repairsUsed: int = 0

    def to_dict(self) -> dict[str, Any]:
        return self.__dict__.copy()


@dataclass(frozen=True)
class RealTaskExecutionContext:
    contextVersion: int
    orchestrationRunId: str
    taskId: str
    stage: str
    createdAt: str
    runDirectory: str
    manifestPath: str
    manifestSha256: str
    effectivePolicyHash: str
    foundationRunId: str
    foundationRunDirectory: str
    foundationFinalReportHash: str
    trustedContextHash: str
    workspaceIdentity: dict[str, Any]
    parentRepositoryIdentity: dict[str, Any]
    initialParentGitSnapshotHash: str
    initialParentSourceInventoryHash: str
    baselineInventoryHash: str
    executionPolicyHash: str
    invocationBudget: dict[str, Any]
    repairBudget: dict[str, Any]
    roleSequence: list[str]
    noParentWrite: bool
    noAutomaticApply: bool
    noNetwork: bool
    noPackageInstall: bool
    noUnity: bool

    def to_dict(self) -> dict[str, Any]:
        return {
            "contextVersion": self.contextVersion,
            "orchestrationRunId": self.orchestrationRunId,
            "taskId": self.taskId,
            "stage": self.stage,
            "createdAt": self.createdAt,
            "runDirectory": self.runDirectory,
            "manifestPath": self.manifestPath,
            "manifestSha256": self.manifestSha256,
            "effectivePolicyHash": self.effectivePolicyHash,
            "foundationRunId": self.foundationRunId,
            "foundationRunDirectory": self.foundationRunDirectory,
            "foundationFinalReportHash": self.foundationFinalReportHash,
            "trustedContextHash": self.trustedContextHash,
            "workspaceIdentity": self.workspaceIdentity,
            "parentRepositoryIdentity": self.parentRepositoryIdentity,
            "initialParentGitSnapshotHash": self.initialParentGitSnapshotHash,
            "initialParentSourceInventoryHash": self.initialParentSourceInventoryHash,
            "baselineInventoryHash": self.baselineInventoryHash,
            "executionPolicyHash": self.executionPolicyHash,
            "invocationBudget": self.invocationBudget,
            "repairBudget": self.repairBudget,
            "roleSequence": list(self.roleSequence),
            "noParentWrite": self.noParentWrite,
            "noAutomaticApply": self.noAutomaticApply,
            "noNetwork": self.noNetwork,
            "noPackageInstall": self.noPackageInstall,
            "noUnity": self.noUnity,
        }


@dataclass(frozen=True)
class RealTaskExecutionHandle:
    handleVersion: int
    orchestrationRunId: str
    taskId: str
    stage: str
    capability: str
    executionContextHash: str
    foundationRunId: str
    trustedContextHash: str
    workspaceDirectory: str
    runDirectory: str
    state: str
    consumed: bool
    terminal: bool


@dataclass(frozen=True)
class RoleInvocationResult:
    role: str
    invocationId: str
    status: str
    verdict: str
    report: dict[str, Any]
    changedWorkspace: bool = False
    errorCode: str | None = None
    errorMessage: str | None = None


@dataclass(frozen=True)
class RealTaskExecutionResult:
    finalVerdict: str
    finalState: str
    report: dict[str, Any]
    reportPath: str | None
    bundleManifestPath: str | None = None


class RealTaskExecutionFailure(Exception):
    def __init__(self, code: str, message: str, verdict: str = "BLOCKED") -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.verdict = verdict


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def new_orchestration_run_id() -> str:
    stamp = utc_now().replace(":", "").replace("-", "").replace(".", "")
    return f"real_task_{stamp}_{secrets.token_hex(4)}"


def new_capability() -> str:
    return secrets.token_hex(32)


def canonical_json_bytes(data: Any) -> bytes:
    return json.dumps(data, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def canonical_sha256(data: Any) -> str:
    return hashlib.sha256(canonical_json_bytes(data)).hexdigest()


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def error(code: str, message: str, field: str | None = None) -> RealTaskError:
    return RealTaskError(code, message, field)
