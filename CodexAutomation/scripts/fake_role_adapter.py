from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from pipeline_models import ExecutionStatus, role_execution_result
from usage_limit_models import fake_usage_limit, utc_now


class FakeRoleAdapter:
    def __init__(self, scenario: str = "normal") -> None:
        self.scenario = scenario
        self.calls: dict[str, int] = {"implementer": 0, "auditor": 0, "repairer": 0}
        self.audit_fix_required_sent = False

    def run_implementer(self, task: dict[str, Any], workspace: Path) -> dict[str, Any]:
        return self._run("implementer", task, workspace)

    def run_repairer(self, task: dict[str, Any], workspace: Path, repair_context: dict[str, Any]) -> dict[str, Any]:
        return self._run("repairer", task, workspace, repair_context)

    def run_auditor(self, task: dict[str, Any], workspace: Path, validation_result: dict[str, Any], repair_context: dict[str, Any]) -> dict[str, Any]:
        return self._run("auditor", task, workspace, validation_result, repair_context)

    def _run(self, role: str, task: dict[str, Any], workspace: Path, *args: Any) -> dict[str, Any]:
        self.calls[role] += 1
        started = utc_now()
        rate_limited = self._rate_limited(role)
        if rate_limited:
            usage = fake_usage_limit(rate_limited["limitType"])
            return role_execution_result(
                ExecutionStatus.RATE_LIMITED.value,
                role,
                task["id"],
                started,
                utc_now(),
                0,
                None,
                usage["errorCode"],
                usage["limit"]["message"],
                usage["limit"],
            )
        if role == "implementer":
            result = self._implement(task, workspace)
        elif role == "repairer":
            result = self._repair(task, workspace, args[0])
        else:
            result = self._audit(task, workspace, args[0])
        result = self._mutate_domain_result(role, result)
        execution = role_execution_result(ExecutionStatus.SUCCESS.value, role, task["id"], started, utc_now(), 0, result)
        return self._mutate_execution(role, task, execution)

    def _rate_limited(self, role: str) -> dict[str, str] | None:
        scenario_role = {
            "rate_limit_implementer": "implementer",
            "rate_limit_auditor": "auditor",
            "rate_limit_repairer": "repairer",
            "retry_exhausted": "implementer",
            "unknown_reset": "implementer",
            "credits_exhausted": "implementer",
        }.get(self.scenario)
        if scenario_role != role:
            return None
        if self.scenario == "retry_exhausted":
            return {"limitType": "five_hour_window"}
        if self.calls[role] == 1:
            if self.scenario == "unknown_reset":
                return {"limitType": "unknown"}
            if self.scenario == "credits_exhausted":
                return {"limitType": "credits"}
            return {"limitType": "five_hour_window"}
        return None

    def _implement(self, task: dict[str, Any], workspace: Path) -> dict[str, Any]:
        if self.scenario in {"already_valid", "audit_fix_required", "audit_blocked", "audit_always_fix_required"}:
            artifact = {"taskId": task["id"], "value": 42, "repairApplied": True}
        else:
            artifact = {"taskId": task["id"], "value": 41, "repairApplied": False}
        (workspace / "artifact.json").write_text(json.dumps(artifact, indent=2) + "\n", encoding="utf-8")
        return {
            "status": "implemented",
            "taskId": task["id"],
            "summary": "Created fake artifact with intentional validation failures.",
            "filesChanged": ["artifact.json"],
            "testsAttempted": [],
            "knownIssues": ["value and repairApplied intentionally require repair"],
        }

    def _repair(self, task: dict[str, Any], workspace: Path, repair_context: dict[str, Any]) -> dict[str, Any]:
        artifact = {"taskId": task["id"], "value": 42, "repairApplied": True}
        (workspace / "artifact.json").write_text(json.dumps(artifact, indent=2) + "\n", encoding="utf-8")
        return {
            "status": "repaired",
            "taskId": task["id"],
            "summary": "Updated fake artifact to satisfy validation and audit findings.",
            "filesChanged": ["artifact.json"],
            "addressedFindingCodes": [finding["code"] for finding in repair_context["validationFindings"] + repair_context["auditFindings"]],
            "remainingIssues": [],
        }

    def _audit(self, task: dict[str, Any], workspace: Path, validation_result: dict[str, Any]) -> dict[str, Any]:
        if self.scenario == "audit_always_fix_required":
            return _audit_result(task["id"], "FIX_REQUIRED", [_finding("AUDIT_REPAIR_REQUIRED", "high")], None)
        if self.scenario == "audit_blocked":
            return _audit_result(task["id"], "BLOCKED", [_finding("AUDIT_BLOCKED", "blocking")], "Audit blocked by fake scenario.")
        if self.scenario == "audit_fix_required" and not self.audit_fix_required_sent:
            self.audit_fix_required_sent = True
            return _audit_result(task["id"], "FIX_REQUIRED", [_finding("AUDIT_REPAIR_REQUIRED", "high")], None)
        artifact = json.loads((workspace / "artifact.json").read_text(encoding="utf-8"))
        if artifact.get("value") == 42 and artifact.get("repairApplied") is True:
            return _audit_result(task["id"], "PASS", [], None)
        return _audit_result(task["id"], "FIX_REQUIRED", [_finding("AUDIT_ARTIFACT_INVALID", "high")], None)

    def _mutate_execution(self, role: str, task: dict[str, Any], execution: dict[str, Any]) -> dict[str, Any]:
        if self.scenario == "invalid_success_missing_result" and role == "implementer":
            execution["result"] = None
        elif self.scenario == "invalid_rate_limited_with_result" and role == "implementer":
            usage = fake_usage_limit()
            execution["executionStatus"] = ExecutionStatus.RATE_LIMITED.value
            execution["errorCode"] = usage["errorCode"]
            execution["errorMessage"] = usage["limit"]["message"]
            execution["usageLimit"] = usage["limit"]
        elif self.scenario == "invalid_technical_error_missing_code" and role == "implementer":
            execution["executionStatus"] = ExecutionStatus.TECHNICAL_ERROR.value
            execution["result"] = None
            execution["errorCode"] = None
        elif self.scenario == "invalid_role" and role == "implementer":
            execution["role"] = "auditor"
        elif self.scenario == "invalid_task_id" and role == "implementer":
            execution["taskId"] = "WRONG-TASK"
        elif self.scenario == "invalid_usage_timestamp" and role == "implementer":
            execution["executionStatus"] = ExecutionStatus.RATE_LIMITED.value
            execution["result"] = None
            execution["errorCode"] = "CODEX_USAGE_LIMIT_REACHED"
            execution["errorMessage"] = "Fake usage limit"
            execution["usageLimit"] = {
                "limitType": "five_hour_window",
                "detectedAtUtc": "2026-07-02T14:00:00",
                "resetAtUtc": None,
                "retryAfterSeconds": None,
                "message": "Fake usage limit",
                "source": "fake_adapter",
            }
        return execution

    def _mutate_domain_result(self, role: str, result: dict[str, Any]) -> Any:
        non_dict_values = {
            "text": "text",
            "number": 123,
            "boolean": True,
            "array": [],
            "null": None,
        }
        for suffix, value in non_dict_values.items():
            if self.scenario == f"invalid_{role}_result_{suffix}":
                return value
        if role == "auditor" and self.scenario == "invalid_audit_pass_blocking":
            result["findings"] = [_finding("BAD_PASS", "blocking")]
        elif role == "auditor" and self.scenario == "invalid_audit_fix_without_findings":
            result["verdict"] = "FIX_REQUIRED"
            result["findings"] = []
        elif role == "auditor" and self.scenario == "invalid_audit_blocked_without_reason":
            result["verdict"] = "BLOCKED"
            result["findings"] = []
            result["blockedReason"] = None
        elif role == "repairer" and self.scenario == "invalid_repair_unknown_code":
            result["addressedFindingCodes"].append("UNKNOWN_FINDING")
        return result


def _finding(code: str, severity: str) -> dict[str, Any]:
    return {
        "code": code,
        "severity": severity,
        "file": "artifact.json",
        "symbol": "",
        "problem": "Fake audit finding.",
        "evidence": "Fake adapter scenario.",
        "requiredFix": "Repair artifact within current task scope.",
    }


def _audit_result(task_id: str, verdict: str, findings: list[dict[str, Any]], blocked_reason: str | None) -> dict[str, Any]:
    return {
        "verdict": verdict,
        "taskId": task_id,
        "summary": f"Fake audit {verdict}.",
        "findings": findings,
        "filesReviewed": ["artifact.json"],
        "scopeViolations": [],
        "blockedReason": blocked_reason,
    }
