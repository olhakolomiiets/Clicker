from __future__ import annotations

from dataclasses import dataclass
from typing import Any


REAL_TASK_STAGE = "BOOTSTRAP-03B-2A"
REAL_TASK_FOUNDATION_STAGE = "BOOTSTRAP-03B-2B-A"


ERROR_CODES = {
    "REAL_TASK_CONFIG_INVALID",
    "REAL_TASK_MANIFEST_JSON_INVALID",
    "REAL_TASK_MANIFEST_SCHEMA_INVALID",
    "REAL_TASK_MANIFEST_INVALID",
    "REAL_TASK_POLICY_VIOLATION",
    "REAL_TASK_PATH_UNSAFE",
    "REAL_TASK_SOURCE_MISSING",
    "REAL_TASK_SOURCE_OUTSIDE_ALLOWED_ROOT",
    "REAL_TASK_SOURCE_REPARSE",
    "REAL_TASK_SOURCE_OVERLAP",
    "REAL_TASK_SOURCE_INVENTORY_FAILED",
    "REAL_TASK_SOURCE_LIMIT_EXCEEDED",
    "REAL_TASK_GIT_SNAPSHOT_FAILED",
    "REAL_TASK_VALIDATOR_UNSUPPORTED",
    "REAL_TASK_VALIDATOR_INVALID",
    "REAL_TASK_LIMIT_INVALID",
    "REAL_TASK_PLAN_WRITE_FAILED",
    "REAL_TASK_INTERNAL_ERROR",
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


@dataclass(frozen=True)
class RealTaskError:
    code: str
    message: str
    field: str | None = None
    path: str | None = None

    def to_dict(self) -> dict[str, Any]:
        data: dict[str, Any] = {"code": self.code, "message": self.message}
        if self.field:
            data["field"] = self.field
        if self.path:
            data["path"] = self.path
        return data


@dataclass(frozen=True)
class RealTaskPolicy:
    allowExecution: bool
    allowParentProjectWrite: bool
    allowAutomaticApply: bool
    allowNetwork: bool
    allowPackageInstall: bool
    allowUnityLaunch: bool
    allowAutomaticRetry: bool
    allowAutomaticModelDowngrade: bool
    allowAutomaticCreditUsage: bool
    allowDirtyParentWorktree: bool
    allowBinaryOutputs: bool
    maxInvocations: int
    maxRepairAttempts: int
    maxChangedFiles: int
    maxChangedBytes: int
    maxSingleChangedFileBytes: int
    maxSourceFiles: int
    maxSourceBytes: int
    allowedSourceRoots: tuple[str, ...]
    forbiddenSourceRoots: tuple[str, ...]
    allowedTaskTypes: tuple[str, ...]
    allowedValidatorTypes: tuple[str, ...]

    def to_dict(self) -> dict[str, Any]:
        return {
            "allowExecution": self.allowExecution,
            "allowParentProjectWrite": self.allowParentProjectWrite,
            "allowAutomaticApply": self.allowAutomaticApply,
            "allowNetwork": self.allowNetwork,
            "allowPackageInstall": self.allowPackageInstall,
            "allowUnityLaunch": self.allowUnityLaunch,
            "allowAutomaticRetry": self.allowAutomaticRetry,
            "allowAutomaticModelDowngrade": self.allowAutomaticModelDowngrade,
            "allowAutomaticCreditUsage": self.allowAutomaticCreditUsage,
            "allowDirtyParentWorktree": self.allowDirtyParentWorktree,
            "allowBinaryOutputs": self.allowBinaryOutputs,
            "maxInvocations": self.maxInvocations,
            "maxRepairAttempts": self.maxRepairAttempts,
            "maxChangedFiles": self.maxChangedFiles,
            "maxChangedBytes": self.maxChangedBytes,
            "maxSingleChangedFileBytes": self.maxSingleChangedFileBytes,
            "maxSourceFiles": self.maxSourceFiles,
            "maxSourceBytes": self.maxSourceBytes,
            "allowedSourceRoots": list(self.allowedSourceRoots),
            "forbiddenSourceRoots": list(self.forbiddenSourceRoots),
            "allowedTaskTypes": list(self.allowedTaskTypes),
            "allowedValidatorTypes": list(self.allowedValidatorTypes),
        }


@dataclass(frozen=True)
class ManifestValidationResult:
    ok: bool
    manifest: dict[str, Any] | None
    manifestSha256: str | None
    effectivePlan: dict[str, Any] | None
    errors: tuple[RealTaskError, ...]
    warnings: tuple[dict[str, Any], ...] = ()

    def error_dicts(self) -> list[dict[str, Any]]:
        return [error.to_dict() for error in self.errors]
