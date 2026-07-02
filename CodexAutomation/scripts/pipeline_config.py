from __future__ import annotations

from typing import Any

from pipeline_models import PipelineSettings


def parse_pipeline_settings(config: dict[str, Any]) -> tuple[PipelineSettings | None, list[str]]:
    errors: list[str] = []
    pipeline = config.get("pipeline")
    usage = config.get("usageLimits")
    if not isinstance(pipeline, dict):
        return None, ["INVALID_PIPELINE_CONFIG: pipeline must be an object."]
    if not isinstance(usage, dict):
        return None, ["INVALID_PIPELINE_CONFIG: usageLimits must be an object."]

    max_repairs = _required_int(pipeline, "maxRepairAttemptsDefault", 0, errors)
    self_test = _required_bool(pipeline, "pipelineSelfTestEnabled", errors)
    workspace_root = _required_string(pipeline, "runtimeWorkspaceRoot", errors)
    report_root = _required_string(pipeline, "pipelineReportRoot", errors)
    allow_write = _required_bool(pipeline, "allowRealWorkspaceWrite", errors)
    max_invocations = _required_int(pipeline, "maxCodexInvocationsPerPipelineRun", 1, errors)
    reserve = _required_int(pipeline, "reserveInvocationsForFinalAudit", 0, errors)
    max_audits = _required_int(pipeline, "maxAuditsPerTask", 1, errors)
    usage_enabled = _required_bool(usage, "enabled", errors)
    max_rate_retries = _required_int(usage, "maxRateLimitRetriesPerInvocation", 0, errors)
    _required_int(usage, "resetSafetyBufferSeconds", 0, errors)
    _required_int(usage, "maximumWaitSeconds", 0, errors)
    _required_bool(usage, "stopWhenResetTimeUnknown", errors)
    allow_credits = _required_bool(usage, "allowAutomaticCreditUsage", errors)
    allow_model = _required_bool(usage, "allowAutomaticModelDowngrade", errors)

    if allow_write is not False:
        errors.append("INVALID_PIPELINE_CONFIG: allowRealWorkspaceWrite must be false in BOOTSTRAP-03A.")
    if allow_credits is not False:
        errors.append("INVALID_PIPELINE_CONFIG: allowAutomaticCreditUsage must be false.")
    if allow_model is not False:
        errors.append("INVALID_PIPELINE_CONFIG: allowAutomaticModelDowngrade must be false.")
    if usage_enabled is not True:
        errors.append("INVALID_PIPELINE_CONFIG: usageLimits.enabled must be true.")
    if max_invocations is not None and reserve is not None and reserve >= max_invocations:
        errors.append("INVALID_PIPELINE_CONFIG: reserveInvocationsForFinalAudit must be less than maxCodexInvocationsPerPipelineRun.")

    if errors:
        return None, errors
    return (
        PipelineSettings(
            maxRepairAttemptsDefault=max_repairs,
            pipelineSelfTestEnabled=self_test,
            runtimeWorkspaceRoot=workspace_root,
            pipelineReportRoot=report_root,
            allowRealWorkspaceWrite=allow_write,
            maxCodexInvocationsPerPipelineRun=max_invocations,
            reserveInvocationsForFinalAudit=reserve,
            maxAuditsPerTask=max_audits,
            maxRateLimitRetriesPerInvocation=max_rate_retries,
        ),
        [],
    )


def _required_bool(source: dict[str, Any], name: str, errors: list[str]) -> bool | None:
    value = source.get(name)
    if not isinstance(value, bool):
        errors.append(f"INVALID_PIPELINE_CONFIG: {name} must be a boolean.")
        return None
    return value


def _required_int(source: dict[str, Any], name: str, minimum: int, errors: list[str]) -> int | None:
    value = source.get(name)
    if not isinstance(value, int) or isinstance(value, bool):
        errors.append(f"INVALID_PIPELINE_CONFIG: {name} must be an integer.")
        return None
    if value < minimum:
        errors.append(f"INVALID_PIPELINE_CONFIG: {name} must be >= {minimum}.")
        return None
    return value


def _required_string(source: dict[str, Any], name: str, errors: list[str]) -> str | None:
    value = source.get(name)
    if not isinstance(value, str) or not value.strip():
        errors.append(f"INVALID_PIPELINE_CONFIG: {name} must be a non-empty string.")
        return None
    return value
