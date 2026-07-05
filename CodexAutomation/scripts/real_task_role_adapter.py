from __future__ import annotations

import shutil
from pathlib import Path
from typing import Any, Protocol

from real_task_execution_models import RoleInvocationResult


class RealTaskRoleAdapter(Protocol):
    def run_sandbox_probe(self, context: dict[str, Any]) -> dict[str, Any]:
        ...

    def run_implementer(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        ...

    def run_repairer(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        ...

    def run_auditor(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        ...


class CodexRealTaskRoleAdapter:
    """Production boundary for future real model roles.

    The public execute_real_task entry constructs this fixed adapter and does not accept
    adapter/model/sandbox/approval overrides. BOOTSTRAP-03B-2C keeps controlled
    real-model execution disabled, so calls return a blocked diagnostic instead of
    starting Codex.
    """

    fixed_registration = True
    explicit_argv_only = True
    shell = False
    network_allowed = False
    package_install_allowed = False
    unity_allowed = False
    automatic_retry = False
    model_downgrade = False

    def run_sandbox_probe(self, context: dict[str, Any]) -> dict[str, Any]:
        return {
            "probeRunId": f"probe_{context['orchestrationRunId']}",
            "finalVerdict": "PASS",
            "processStarted": False,
            "modelInvocationStarted": False,
            "sandboxStarted": False,
            "networkUsed": False,
            "errorCode": None,
            "errorMessage": None,
        }

    def run_implementer(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        return self._disabled("IMPLEMENTER")

    def run_repairer(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        return self._disabled("REPAIRER")

    def run_auditor(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        return self._disabled("AUDITOR")

    def _disabled(self, role: str) -> RoleInvocationResult:
        return RoleInvocationResult(
            role=role,
            invocationId=f"{role.lower()}_disabled",
            status="TECHNICAL_ERROR",
            verdict="BLOCKED",
            report={"role": role, "status": "TECHNICAL_ERROR", "finalVerdict": "BLOCKED", "errorCode": "REAL_TASK_ROLE_EXECUTION_FAILED"},
            errorCode="REAL_TASK_ROLE_EXECUTION_FAILED",
            errorMessage="Controlled real-model task execution is disabled in BOOTSTRAP-03B-2C.",
        )


class FakeRealTaskRoleAdapter:
    def __init__(self, scenario: str = "direct_success") -> None:
        self.scenario = scenario
        self.invocations: list[str] = []

    def run_sandbox_probe(self, context: dict[str, Any]) -> dict[str, Any]:
        return {
            "probeRunId": f"fake_probe_{context['orchestrationRunId']}",
            "finalVerdict": "PASS",
            "processStarted": False,
            "modelInvocationStarted": False,
            "sandboxStarted": False,
            "networkUsed": False,
            "errorCode": None,
            "errorMessage": None,
        }

    def run_implementer(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        self.invocations.append("IMPLEMENTER")
        if self.scenario == "rate_limited":
            return self._result("IMPLEMENTER", "RATE_LIMITED", "RATE_LIMITED", "REAL_TASK_RATE_LIMITED")
        if self.scenario == "timeout":
            return self._result("IMPLEMENTER", "TIMEOUT", "BLOCKED", "REAL_TASK_TIMEOUT")
        if self.scenario == "technical_failure":
            return self._result("IMPLEMENTER", "TECHNICAL_ERROR", "BLOCKED", "REAL_TASK_ROLE_EXECUTION_FAILED")
        self._write(context, "Assets/Generated/planet_task_output.txt", "needs repair\n" if self.scenario in {"repair_success", "repair_exhausted", "repair_no_progress"} else "complete\n")
        if self.scenario == "host_blocked":
            self._write(context, "task.json", "{}\n")
        return self._result("IMPLEMENTER", "SUCCESS", "PASS")

    def run_repairer(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        self.invocations.append("REPAIRER")
        if self.scenario == "repair_timeout":
            return self._result("REPAIRER", "TIMEOUT", "BLOCKED", "REAL_TASK_TIMEOUT")
        if self.scenario == "repair_rate_limited":
            return self._result("REPAIRER", "RATE_LIMITED", "RATE_LIMITED", "REAL_TASK_RATE_LIMITED")
        if self.scenario == "repair_no_progress":
            return self._result("REPAIRER", "SUCCESS", "PASS")
        if self.scenario == "repair_parent_mutation":
            self._write(context, "task.json", "{}\n")
            return self._result("REPAIRER", "SUCCESS", "PASS")
        self._write(context, "Assets/Generated/planet_task_output.txt", "complete\n")
        return self._result("REPAIRER", "SUCCESS", "PASS")

    def run_auditor(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        self.invocations.append("AUDITOR")
        if self.scenario == "auditor_reject":
            return self._result("AUDITOR", "SUCCESS", "FAIL", "REAL_TASK_AUDIT_REJECTED")
        if self.scenario == "auditor_schema_invalid":
            return self._result("AUDITOR", "SUCCESS", "BLOCKED", "REAL_TASK_AUDIT_RESULT_INVALID")
        if self.scenario == "auditor_mutation":
            self._write(context, "Assets/Generated/auditor_note.txt", "changed\n")
            return self._result("AUDITOR", "SUCCESS", "PASS", changed=True)
        if self.scenario == "auditor_agents_mutation":
            self._write(context, ".agents/auditor_note.txt", "changed\n")
            return self._result("AUDITOR", "SUCCESS", "PASS", changed=True)
        return self._result("AUDITOR", "SUCCESS", "PASS")

    def _write(self, context: dict[str, Any], relative_path: str, text: str) -> None:
        path = Path(context["workspaceIdentity"]["workspaceDirectory"]) / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")

    def _result(self, role: str, status: str, verdict: str, code: str | None = None, changed: bool = False) -> RoleInvocationResult:
        return RoleInvocationResult(
            role=role,
            invocationId=f"{role.lower()}_{len(self.invocations)}",
            status=status,
            verdict=verdict,
            report={"role": role, "status": status, "finalVerdict": verdict, "errorCode": code},
            changedWorkspace=changed,
            errorCode=code,
            errorMessage=code,
        )


def workspace_copy(src: Path, dst: Path) -> None:
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(src, dst)
