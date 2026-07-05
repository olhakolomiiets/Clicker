from __future__ import annotations

import hashlib
import json
import secrets
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any, Callable


REAL_TASK_VALIDATION_STAGE = "BOOTSTRAP-03B-2B-C"
VALIDATION_REPORT_VERSION = 1
VALIDATOR_RESULT_VERSION = 1
VALIDATION_HANDLE_VERSION = 1

VALIDATOR_TYPES = (
    "file_exists",
    "file_absent",
    "json_valid",
    "json_schema",
    "json_field_equals",
    "text_contains",
    "text_not_contains",
    "changed_paths_exact",
    "changed_paths_subset",
    "no_unexpected_files",
    "no_conflict_markers",
    "max_file_size",
    "max_changed_files",
    "extension_allowlist",
)

DANGEROUS_VALIDATOR_FIELDS = {
    "command",
    "executable",
    "script",
    "shell",
    "environment",
    "network",
    "endpoint",
    "schemaPath",
    "plugin",
    "module",
    "function",
    "class",
    "env",
    "argv",
    "args",
    "cwd",
    "stdin",
    "stdout",
    "stderr",
}

VALIDATOR_FAIL_CODES_BY_TYPE = {
    "file_exists": frozenset({"VALIDATOR_FILE_MISSING", "VALIDATOR_FILE_TYPE_MISMATCH"}),
    "file_absent": frozenset({"VALIDATOR_FILE_PRESENT"}),
    "json_valid": frozenset({"VALIDATOR_JSON_INVALID"}),
    "json_schema": frozenset({"VALIDATOR_JSON_SCHEMA_MISMATCH"}),
    "json_field_equals": frozenset({"VALIDATOR_JSON_FIELD_MISSING", "VALIDATOR_JSON_FIELD_MISMATCH"}),
    "text_contains": frozenset({"VALIDATOR_TEXT_MISSING"}),
    "text_not_contains": frozenset({"VALIDATOR_TEXT_FORBIDDEN"}),
    "changed_paths_exact": frozenset({"VALIDATOR_CHANGED_PATHS_MISMATCH"}),
    "changed_paths_subset": frozenset({"VALIDATOR_CHANGED_PATHS_EXTRA"}),
    "no_unexpected_files": frozenset({"VALIDATOR_UNEXPECTED_FILE"}),
    "no_conflict_markers": frozenset({"VALIDATOR_CONFLICT_MARKER"}),
    "max_file_size": frozenset({"VALIDATOR_FILE_TYPE_MISMATCH", "VALIDATOR_FILE_TOO_LARGE"}),
    "max_changed_files": frozenset({"VALIDATOR_TOO_MANY_CHANGED_FILES"}),
    "extension_allowlist": frozenset({"VALIDATOR_EXTENSION_FORBIDDEN"}),
}

VALIDATOR_BLOCKED_CODES = frozenset({
    "REAL_TASK_VALIDATOR_EXECUTION_FAILED",
    "REAL_TASK_VALIDATOR_PATH_UNSAFE",
    "REAL_TASK_VALIDATOR_READ_FAILED",
    "REAL_TASK_VALIDATOR_RESULT_INVALID",
    "REAL_TASK_VALIDATOR_WORKSPACE_MUTATION",
    "REAL_TASK_VALIDATION_BB_INVALID",
    "REAL_TASK_VALIDATION_CONFIG_INVALID",
    "REAL_TASK_VALIDATION_CONTEXT_MISMATCH",
    "REAL_TASK_VALIDATION_INTEGRITY_INVALID",
    "REAL_TASK_VALIDATION_PLAN_INVALID",
    "REAL_TASK_VALIDATION_REPORT_INVALID",
    "REAL_TASK_VALIDATION_REPORT_WRITE_FAILED",
    "REAL_TASK_VALIDATOR_REGISTRY_INVALID",
    "REAL_TASK_VALIDATOR_SCHEMA_INVALID",
    "REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID",
    "REAL_TASK_VALIDATOR_SCHEMA_UNKNOWN",
    "REAL_TASK_PATH_UNSAFE",
    "REAL_TASK_SERVICE_FILE_INVALID",
    "VALIDATOR_INVENTORY_DISK_MISMATCH",
})


class ValidationState(str, Enum):
    PENDING = "PENDING"
    BB_REVALIDATING = "BB_REVALIDATING"
    REGISTRY_LOADING = "REGISTRY_LOADING"
    PLAN_REVALIDATING = "PLAN_REVALIDATING"
    VALIDATORS_EXECUTING = "VALIDATORS_EXECUTING"
    INTEGRITY_RECHECKING = "INTEGRITY_RECHECKING"
    REPORTING = "REPORTING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    BLOCKED = "BLOCKED"


class ValidatorStatus(str, Enum):
    PASS = "PASS"
    FAIL = "FAIL"
    BLOCKED = "BLOCKED"


class ValidatorScopeKind(str, Enum):
    FILE = "FILE"
    DIRECTORY = "DIRECTORY"


@dataclass(frozen=True)
class ValidatorReadableScope:
    normalizedPath: str
    scopeKind: str
    origin: str
    recursive: bool
    service: bool = False

    def to_dict(self) -> dict[str, Any]:
        return {
            "normalizedPath": self.normalizedPath,
            "scopeKind": self.scopeKind,
            "origin": self.origin,
            "recursive": self.recursive,
            "service": self.service,
        }


@dataclass(frozen=True)
class ValidatorPolicy:
    enabled: bool
    allowedValidatorTypes: tuple[str, ...]
    maxValidatorsPerTask: int
    maxFindings: int
    maxTextReadBytes: int
    maxJsonReadBytes: int
    maxSchemaBytes: int
    maxReportBytes: int
    failFast: bool
    allowCommandValidators: bool
    allowRegexValidators: bool
    allowExternalSchemaPaths: bool
    allowDynamicPlugins: bool
    allowNetwork: bool
    allowUnity: bool

    def to_dict(self) -> dict[str, Any]:
        return {
            "enabled": self.enabled,
            "allowedValidatorTypes": list(self.allowedValidatorTypes),
            "maxValidatorsPerTask": self.maxValidatorsPerTask,
            "maxFindings": self.maxFindings,
            "maxTextReadBytes": self.maxTextReadBytes,
            "maxJsonReadBytes": self.maxJsonReadBytes,
            "maxSchemaBytes": self.maxSchemaBytes,
            "maxReportBytes": self.maxReportBytes,
            "failFast": self.failFast,
            "allowCommandValidators": self.allowCommandValidators,
            "allowRegexValidators": self.allowRegexValidators,
            "allowExternalSchemaPaths": self.allowExternalSchemaPaths,
            "allowDynamicPlugins": self.allowDynamicPlugins,
            "allowNetwork": self.allowNetwork,
            "allowUnity": self.allowUnity,
        }


@dataclass(frozen=True)
class ValidatorDefinition:
    type: str
    version: str
    implementationId: str
    argumentContract: str
    needsWorkspaceRead: bool
    needsDiff: bool
    needsExpectedOutputs: bool
    maxFilesRead: int
    allowedFailCodes: tuple[str, ...]
    noSideEffects: bool
    implementation: Callable[["ValidatorContext", dict[str, Any]], "ValidatorOutcome"]

    def snapshot(self) -> dict[str, Any]:
        return {
            "type": self.type,
            "version": self.version,
            "implementationId": self.implementationId,
            "argumentContract": self.argumentContract,
            "needsWorkspaceRead": self.needsWorkspaceRead,
            "needsDiff": self.needsDiff,
            "needsExpectedOutputs": self.needsExpectedOutputs,
            "maxFilesRead": self.maxFilesRead,
            "allowedFailCodes": list(self.allowedFailCodes),
            "noSideEffects": self.noSideEffects,
        }


@dataclass(frozen=True)
class ValidatorContext:
    repositoryRoot: Path
    automationRoot: Path
    runDirectory: Path
    workspaceDirectory: Path
    task: dict[str, Any]
    effectivePolicy: dict[str, Any]
    validationPolicy: ValidatorPolicy
    baselineInventory: dict[str, Any]
    finalInventory: dict[str, Any]
    diffReport: dict[str, Any]
    policyReport: dict[str, Any]
    finalChangeReport: dict[str, Any]
    serviceRegistry: tuple[str, ...]
    readableScopes: tuple[ValidatorReadableScope, ...]
    schemaRegistry: Any
    validatorIndex: int = 0


@dataclass(frozen=True)
class ValidatorOutcome:
    validatorId: str
    validatorIndex: int
    validatorType: str
    validatorVersion: str
    status: str
    code: str | None
    message: str
    primaryPath: str | None = None
    relatedPaths: tuple[str, ...] = ()
    expected: str | None = None
    actual: str | None = None
    filesRead: int = 0
    bytesRead: int = 0
    durationMilliseconds: int = 0
    noSideEffects: bool = True
    findings: tuple[dict[str, Any], ...] = ()

    def to_dict(self) -> dict[str, Any]:
        return {
            "resultVersion": VALIDATOR_RESULT_VERSION,
            "validatorId": self.validatorId,
            "validatorIndex": self.validatorIndex,
            "validatorType": self.validatorType,
            "validatorVersion": self.validatorVersion,
            "status": self.status,
            "code": self.code,
            "message": self.message,
            "primaryPath": self.primaryPath,
            "relatedPaths": list(self.relatedPaths),
            "expected": self.expected,
            "actual": self.actual,
            "filesRead": self.filesRead,
            "bytesRead": self.bytesRead,
            "durationMilliseconds": self.durationMilliseconds,
            "noSideEffects": self.noSideEffects,
            "findings": [dict(item) for item in self.findings],
        }


@dataclass(frozen=True)
class ValidationPrerequisiteHandle:
    handleVersion: int
    validationPrerequisiteId: str
    capability: str = field(repr=False)
    runId: str
    taskId: str
    trackingId: str
    stage: str
    trustedContextHash: str
    manifestSha256: str
    workspaceDirectory: str
    runDirectory: str
    finalChangeReportPath: str
    finalChangeReportHash: str
    baselineInventoryHash: str
    finalInventoryHash: str
    diffReportHash: str
    policyReportHash: str
    serviceRegistryHash: str
    isolatedGitFingerprintHash: str
    sourcePostInventoryHash: str
    noModel: bool
    noCodex: bool
    noSandbox: bool
    noUnity: bool

    def public_dict(self) -> dict[str, Any]:
        return {
            "handleVersion": self.handleVersion,
            "validationPrerequisiteId": self.validationPrerequisiteId,
            "runId": self.runId,
            "taskId": self.taskId,
            "trackingId": self.trackingId,
            "stage": self.stage,
            "trustedContextHash": self.trustedContextHash,
            "manifestSha256": self.manifestSha256,
            "workspaceDirectory": self.workspaceDirectory,
            "runDirectory": self.runDirectory,
            "finalChangeReportPath": self.finalChangeReportPath,
            "finalChangeReportHash": self.finalChangeReportHash,
            "baselineInventoryHash": self.baselineInventoryHash,
            "finalInventoryHash": self.finalInventoryHash,
            "diffReportHash": self.diffReportHash,
            "policyReportHash": self.policyReportHash,
            "serviceRegistryHash": self.serviceRegistryHash,
            "isolatedGitFingerprintHash": self.isolatedGitFingerprintHash,
            "sourcePostInventoryHash": self.sourcePostInventoryHash,
            "noModel": self.noModel,
            "noCodex": self.noCodex,
            "noSandbox": self.noSandbox,
            "noUnity": self.noUnity,
        }


@dataclass(frozen=True)
class ValidationExecutionResult:
    finalVerdict: str
    finalState: str
    report: dict[str, Any]
    reportPath: str | None


class ValidationFailure(Exception):
    def __init__(self, code: str, message: str, verdict: str = "BLOCKED") -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.verdict = verdict


def canonical_json_bytes(data: Any) -> bytes:
    return json.dumps(data, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def canonical_sha256(data: Any) -> str:
    return hashlib.sha256(canonical_json_bytes(data)).hexdigest()


def new_validation_capability() -> str:
    return secrets.token_hex(32)


def pass_outcome(validator_id: str, validator_type: str, message: str = "Validator assertion passed.", primary_path: str | None = None, files_read: int = 0, bytes_read: int = 0, *, validator_index: int) -> ValidatorOutcome:
    return ValidatorOutcome(validator_id, validator_index, validator_type, "1", ValidatorStatus.PASS.value, None, message, primaryPath=primary_path, filesRead=files_read, bytesRead=bytes_read)


def fail_outcome(validator_id: str, validator_type: str, code: str, message: str, findings: tuple[dict[str, Any], ...] = (), primary_path: str | None = None, expected: str | None = None, actual: str | None = None, files_read: int = 0, bytes_read: int = 0, *, validator_index: int) -> ValidatorOutcome:
    return ValidatorOutcome(validator_id, validator_index, validator_type, "1", ValidatorStatus.FAIL.value, code, message, primaryPath=primary_path, expected=expected, actual=actual, filesRead=files_read, bytesRead=bytes_read, findings=findings)


def blocked_outcome(validator_id: str, validator_type: str, code: str, message: str, primary_path: str | None = None, *, validator_index: int) -> ValidatorOutcome:
    return ValidatorOutcome(validator_id, validator_index, validator_type, "1", ValidatorStatus.BLOCKED.value, code, message, primaryPath=primary_path)
