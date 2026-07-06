from __future__ import annotations

from typing import Any

from real_task_execution_models import RealTaskExecutionPolicy, RealTaskExecutionSelfTestPolicy, error
from real_task_models import RealTaskError


REQUIRED_FIELDS = {
    "enabled",
    "publicRunCliEnabled",
    "controlledRealModelSelfTestEnabled",
    "maxRoleInvocations",
    "maxRepairAttempts",
    "implementerEnabled",
    "repairerEnabled",
    "auditorEnabled",
    "requireFreshSandboxProbe",
    "requireFinalHostValidationPass",
    "requireAuditorAfterHostPass",
    "allowParentWrite",
    "allowAutomaticApply",
    "allowGitCommit",
    "allowGitPush",
    "allowPullRequest",
    "allowNetwork",
    "allowPackageInstall",
    "allowUnity",
    "allowAutomaticRetry",
    "allowRateLimitRetry",
    "allowModelDowngrade",
    "allowCreditUse",
    "maxRoleReportBytes",
    "maxDiagnosticReportBytes",
    "maxBundleBytes",
    "maxBundleFiles",
    "maxPromptBytes",
    "maxRoleMessageBytes",
    "maxEventLogBytes",
    "maxAuditFindings",
}

DANGEROUS_FALSE = {
    "publicRunCliEnabled",
    "allowParentWrite",
    "allowAutomaticApply",
    "allowGitCommit",
    "allowGitPush",
    "allowPullRequest",
    "allowNetwork",
    "allowPackageInstall",
    "allowUnity",
    "allowAutomaticRetry",
    "allowRateLimitRetry",
    "allowModelDowngrade",
    "allowCreditUse",
}

TRUE_FIELDS = {
    "enabled",
    "implementerEnabled",
    "repairerEnabled",
    "auditorEnabled",
    "requireFreshSandboxProbe",
    "requireFinalHostValidationPass",
    "requireAuditorAfterHostPass",
}

CAPS = {
    "maxRoleReportBytes": (1024, 2_097_152),
    "maxDiagnosticReportBytes": (1024, 4_194_304),
    "maxBundleBytes": (1024, 16_777_216),
    "maxBundleFiles": (1, 256),
    "maxPromptBytes": (1024, 262_144),
    "maxRoleMessageBytes": (1024, 262_144),
    "maxEventLogBytes": (1024, 4_194_304),
    "maxAuditFindings": (1, 256),
}


def parse_execution_policy(config: dict[str, Any]) -> tuple[RealTaskExecutionPolicy | None, list[RealTaskError]]:
    raw = config.get("realTaskExecutionPolicy")
    errors: list[RealTaskError] = []
    if not isinstance(raw, dict):
        return None, [error("REAL_TASK_EXECUTION_POLICY_INVALID", "realTaskExecutionPolicy must be an object.", "realTaskExecutionPolicy")]
    for field in sorted(set(raw) - REQUIRED_FIELDS):
        errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", "Unknown realTaskExecutionPolicy field.", f"realTaskExecutionPolicy.{field}"))
    for field in sorted(REQUIRED_FIELDS - set(raw)):
        errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", "Missing realTaskExecutionPolicy field.", f"realTaskExecutionPolicy.{field}"))
    for field in TRUE_FIELDS:
        if raw.get(field) is not True:
            errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", f"{field} must be exact true.", f"realTaskExecutionPolicy.{field}"))
    for field in DANGEROUS_FALSE:
        if raw.get(field) is not False:
            errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", f"{field} must be exact false.", f"realTaskExecutionPolicy.{field}"))
    if raw.get("controlledRealModelSelfTestEnabled") is not True:
        errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", "controlledRealModelSelfTestEnabled must be exact true for BOOTSTRAP-03B-2D.", "realTaskExecutionPolicy.controlledRealModelSelfTestEnabled"))
    if raw.get("maxRoleInvocations") != 3 or isinstance(raw.get("maxRoleInvocations"), bool):
        errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", "maxRoleInvocations must be exact integer 3.", "realTaskExecutionPolicy.maxRoleInvocations"))
    if not isinstance(raw.get("maxRepairAttempts"), int) or isinstance(raw.get("maxRepairAttempts"), bool) or raw.get("maxRepairAttempts") < 0 or raw.get("maxRepairAttempts") > 1:
        errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", "maxRepairAttempts must be integer 0 or 1.", "realTaskExecutionPolicy.maxRepairAttempts"))
    parsed_caps: dict[str, int] = {}
    for field, (minimum, maximum) in CAPS.items():
        value = raw.get(field)
        if not isinstance(value, int) or isinstance(value, bool) or value < minimum or value > maximum:
            errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", f"{field} must be an integer from {minimum} to {maximum}.", f"realTaskExecutionPolicy.{field}"))
            parsed_caps[field] = minimum
        else:
            parsed_caps[field] = value
    if errors:
        return None, errors
    return RealTaskExecutionPolicy(
        enabled=True,
        publicRunCliEnabled=False,
        controlledRealModelSelfTestEnabled=True,
        maxRoleInvocations=3,
        maxRepairAttempts=int(raw["maxRepairAttempts"]),
        implementerEnabled=True,
        repairerEnabled=True,
        auditorEnabled=True,
        requireFreshSandboxProbe=True,
        requireFinalHostValidationPass=True,
        requireAuditorAfterHostPass=True,
        allowParentWrite=False,
        allowAutomaticApply=False,
        allowGitCommit=False,
        allowGitPush=False,
        allowPullRequest=False,
        allowNetwork=False,
        allowPackageInstall=False,
        allowUnity=False,
        allowAutomaticRetry=False,
        allowRateLimitRetry=False,
        allowModelDowngrade=False,
        allowCreditUse=False,
        maxRoleReportBytes=parsed_caps["maxRoleReportBytes"],
        maxDiagnosticReportBytes=parsed_caps["maxDiagnosticReportBytes"],
        maxBundleBytes=parsed_caps["maxBundleBytes"],
        maxBundleFiles=parsed_caps["maxBundleFiles"],
        maxPromptBytes=parsed_caps["maxPromptBytes"],
        maxRoleMessageBytes=parsed_caps["maxRoleMessageBytes"],
        maxEventLogBytes=parsed_caps["maxEventLogBytes"],
        maxAuditFindings=parsed_caps["maxAuditFindings"],
    ), []


SELF_TEST_REQUIRED_FIELDS = {
    "enabled",
    "cliFlag",
    "allowArguments",
    "allowUserManifest",
    "allowUserWorkspace",
    "allowUserPrompt",
    "allowUserModel",
    "allowUserSandbox",
    "allowUserApprovalPolicy",
    "requireSyntheticParent",
    "requireFreshFoundation",
    "requireFreshSandboxProbe",
    "requireRootRepositoryUnchanged",
    "requireSyntheticParentUnchanged",
    "maxRunsPerInvocation",
    "maxRoleInvocations",
    "maxRepairAttempts",
    "allowAutomaticRetry",
    "allowRateLimitRetry",
    "allowModelDowngrade",
    "allowCreditUse",
    "allowTaskNetwork",
    "allowPackageInstall",
    "allowUnity",
    "allowAutomaticApply",
    "allowGitCommit",
    "allowGitPush",
    "allowPullRequest",
    "maxMetaReportBytes",
}

SELF_TEST_TRUE_FIELDS = {
    "enabled",
    "requireSyntheticParent",
    "requireFreshFoundation",
    "requireFreshSandboxProbe",
    "requireRootRepositoryUnchanged",
    "requireSyntheticParentUnchanged",
}

SELF_TEST_FALSE_FIELDS = {
    "allowArguments",
    "allowUserManifest",
    "allowUserWorkspace",
    "allowUserPrompt",
    "allowUserModel",
    "allowUserSandbox",
    "allowUserApprovalPolicy",
    "allowAutomaticRetry",
    "allowRateLimitRetry",
    "allowModelDowngrade",
    "allowCreditUse",
    "allowTaskNetwork",
    "allowPackageInstall",
    "allowUnity",
    "allowAutomaticApply",
    "allowGitCommit",
    "allowGitPush",
    "allowPullRequest",
}


def parse_execution_self_test_policy(config: dict[str, Any]) -> tuple[RealTaskExecutionSelfTestPolicy | None, list[RealTaskError]]:
    raw = config.get("realTaskExecutionSelfTestPolicy")
    errors: list[RealTaskError] = []
    if not isinstance(raw, dict):
        return None, [error("REAL_TASK_SELF_TEST_POLICY_INVALID", "realTaskExecutionSelfTestPolicy must be an object.", "realTaskExecutionSelfTestPolicy")]
    for field in sorted(set(raw) - SELF_TEST_REQUIRED_FIELDS):
        errors.append(error("REAL_TASK_SELF_TEST_POLICY_INVALID", "Unknown realTaskExecutionSelfTestPolicy field.", f"realTaskExecutionSelfTestPolicy.{field}"))
    for field in sorted(SELF_TEST_REQUIRED_FIELDS - set(raw)):
        errors.append(error("REAL_TASK_SELF_TEST_POLICY_INVALID", "Missing realTaskExecutionSelfTestPolicy field.", f"realTaskExecutionSelfTestPolicy.{field}"))
    for field in SELF_TEST_TRUE_FIELDS:
        if raw.get(field) is not True:
            errors.append(error("REAL_TASK_SELF_TEST_POLICY_INVALID", f"{field} must be exact true.", f"realTaskExecutionSelfTestPolicy.{field}"))
    for field in SELF_TEST_FALSE_FIELDS:
        if raw.get(field) is not False:
            errors.append(error("REAL_TASK_SELF_TEST_POLICY_INVALID", f"{field} must be exact false.", f"realTaskExecutionSelfTestPolicy.{field}"))
    if raw.get("cliFlag") != "--real-task-execution-self-test":
        errors.append(error("REAL_TASK_SELF_TEST_POLICY_INVALID", "cliFlag must be --real-task-execution-self-test.", "realTaskExecutionSelfTestPolicy.cliFlag"))
    exact_ints = {
        "maxRunsPerInvocation": 1,
        "maxRoleInvocations": 3,
        "maxRepairAttempts": 1,
    }
    for field, expected in exact_ints.items():
        value = raw.get(field)
        if not isinstance(value, int) or isinstance(value, bool) or value != expected:
            errors.append(error("REAL_TASK_SELF_TEST_POLICY_INVALID", f"{field} must be exact integer {expected}.", f"realTaskExecutionSelfTestPolicy.{field}"))
    max_report = raw.get("maxMetaReportBytes")
    if not isinstance(max_report, int) or isinstance(max_report, bool) or max_report < 1024 or max_report > 2_097_152:
        errors.append(error("REAL_TASK_SELF_TEST_POLICY_INVALID", "maxMetaReportBytes must be an integer from 1024 to 2097152.", "realTaskExecutionSelfTestPolicy.maxMetaReportBytes"))
    if errors:
        return None, errors
    return RealTaskExecutionSelfTestPolicy(
        enabled=True,
        cliFlag="--real-task-execution-self-test",
        allowArguments=False,
        allowUserManifest=False,
        allowUserWorkspace=False,
        allowUserPrompt=False,
        allowUserModel=False,
        allowUserSandbox=False,
        allowUserApprovalPolicy=False,
        requireSyntheticParent=True,
        requireFreshFoundation=True,
        requireFreshSandboxProbe=True,
        requireRootRepositoryUnchanged=True,
        requireSyntheticParentUnchanged=True,
        maxRunsPerInvocation=1,
        maxRoleInvocations=3,
        maxRepairAttempts=1,
        allowAutomaticRetry=False,
        allowRateLimitRetry=False,
        allowModelDowngrade=False,
        allowCreditUse=False,
        allowTaskNetwork=False,
        allowPackageInstall=False,
        allowUnity=False,
        allowAutomaticApply=False,
        allowGitCommit=False,
        allowGitPush=False,
        allowPullRequest=False,
        maxMetaReportBytes=max_report,
    ), []


def apply_manifest_role_budget(policy: RealTaskExecutionPolicy, manifest: dict[str, Any]) -> tuple[dict[str, int], list[RealTaskError]]:
    budget = manifest.get("roleBudget", {}) if isinstance(manifest, dict) else {}
    errors: list[RealTaskError] = []
    max_invocations = policy.maxRoleInvocations
    max_repairs = policy.maxRepairAttempts
    if isinstance(budget, dict):
        if "maxInvocations" in budget:
            value = budget["maxInvocations"]
            if not isinstance(value, int) or isinstance(value, bool) or value < 1 or value > policy.maxRoleInvocations:
                errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", "Manifest maxInvocations can only lower the host cap.", "roleBudget.maxInvocations"))
            else:
                max_invocations = value
        if "maxRepairAttempts" in budget:
            value = budget["maxRepairAttempts"]
            if not isinstance(value, int) or isinstance(value, bool) or value < 0 or value > policy.maxRepairAttempts:
                errors.append(error("REAL_TASK_EXECUTION_POLICY_INVALID", "Manifest maxRepairAttempts can only lower the host cap.", "roleBudget.maxRepairAttempts"))
            else:
                max_repairs = value
    return {"maxRoleInvocations": max_invocations, "maxRepairAttempts": max_repairs}, errors
