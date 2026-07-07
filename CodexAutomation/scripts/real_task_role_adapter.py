from __future__ import annotations

import json
import shutil
from pathlib import Path
from typing import Any, Protocol

from codex_event_parser import classify_rate_limit_event, parse_jsonl_events
from codex_runner import run_process_with_timeout
from file_utils import read_json, write_json_atomic
from real_task_execution_models import RoleInvocationResult
from real_role_runner import _create_probe_run_context, run_real_role_sandbox_probe_outcome
from schema_validator import validate


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
    """Production boundary for controlled real model roles.

    The private production wrapper constructs this fixed adapter and does not accept
    adapter/model/sandbox/approval overrides. By default direct construction remains
    disabled; controlled runners enable it only through fixed internal entry points.
    """

    fixed_registration = True
    explicit_argv_only = True
    shell = False
    network_allowed = False
    package_install_allowed = False
    unity_allowed = False
    automatic_retry = False
    model_downgrade = False

    def __init__(
        self,
        root: Path | None = None,
        automation_root: Path | None = None,
        config: dict[str, Any] | None = None,
        run_dir: Path | None = None,
        allow_real_execution: bool = False,
    ) -> None:
        self.root = root
        self.automation_root = automation_root
        self.config = config or {}
        self.run_dir = run_dir
        self.allow_real_execution = allow_real_execution
        self.invocation_count = 0

    def run_sandbox_probe(self, context: dict[str, Any]) -> dict[str, Any]:
        if self.allow_real_execution and self.root is not None and self.automation_root is not None:
            outcome = run_real_role_sandbox_probe_outcome(
                self.root,
                self.automation_root,
                self.config,
                _create_probe_run_context(self.root, f"real_task_{context['orchestrationRunId']}", str(context.get("createdAt") or "selftest")),
            )
            report = dict(outcome.report_summary)
            return {
                "probeRunId": str(report.get("probeRunId")),
                "finalVerdict": report.get("finalVerdict"),
                "processStarted": report.get("processStarted") is True,
                "modelInvocationStarted": False,
                "sandboxStarted": report.get("processStarted") is True,
                "networkUsed": False,
                "errorCode": report.get("errorCode"),
                "errorMessage": report.get("errorMessage"),
            }
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
        if self.allow_real_execution:
            return self._run_role("IMPLEMENTER", "workspace-write", context, prompt, "real_task_implementer_result.schema.json")
        return self._disabled("IMPLEMENTER")

    def run_repairer(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        if self.allow_real_execution:
            return self._run_role("REPAIRER", "workspace-write", context, prompt, "real_task_repairer_result.schema.json")
        return self._disabled("REPAIRER")

    def run_auditor(self, context: dict[str, Any], prompt: str) -> RoleInvocationResult:
        if self.allow_real_execution:
            return self._run_role("AUDITOR", "read-only", context, prompt, "real_task_auditor_result.schema.json")
        return self._disabled("AUDITOR")

    def _run_role(self, role: str, sandbox: str, context: dict[str, Any], prompt: str, schema_name: str) -> RoleInvocationResult:
        self.invocation_count += 1
        invocation_id = f"{role.lower()}_{self.invocation_count}"
        if self.root is None or self.automation_root is None or self.run_dir is None:
            return self._technical(role, invocation_id, "REAL_TASK_ROLE_EXECUTION_FAILED", "Production adapter is missing fixed host bindings.")
        workspace = Path(context["workspaceIdentity"]["workspaceDirectory"]).resolve(strict=True)
        role_dir = self.run_dir / "role_logs"
        role_dir.mkdir(parents=True, exist_ok=True)
        stdout_path = role_dir / f"{invocation_id}_stdout.jsonl"
        stderr_path = role_dir / f"{invocation_id}_stderr.log"
        last_message_path = role_dir / f"{invocation_id}_last_message.json"
        schema_path = self.automation_root / "schemas" / schema_name
        command = self._codex_command(sandbox, workspace, schema_path, last_message_path)
        write_json_atomic(role_dir / f"{invocation_id}_command.json", {"command": _sanitize_command(command), "cwd": str(workspace)})
        timeout = self._timeout_for(role)
        process = run_process_with_timeout(command, workspace, prompt, timeout)
        stdout_path.write_text(process["stdout"], encoding="utf-8", newline="\n")
        stderr_path.write_text(process["stderr"], encoding="utf-8", newline="\n")
        events = parse_jsonl_events(process["stdout"])
        stderr_rate = classify_rate_limit_event({"message": process["stderr"]})
        if process["timed_out"]:
            return self._technical(role, invocation_id, "REAL_TASK_TIMEOUT", "Role invocation timed out.", status="TIMEOUT")
        if events.get("rateLimit") or stderr_rate:
            return self._technical(role, invocation_id, "REAL_TASK_RATE_LIMITED", "Role invocation was rate limited.", status="RATE_LIMITED", verdict="RATE_LIMITED")
        if process["exit_code"] != 0:
            return self._technical(role, invocation_id, "REAL_TASK_ROLE_EXECUTION_FAILED", "Codex role invocation returned nonzero exit code.")
        if not last_message_path.exists():
            return self._technical(role, invocation_id, "REAL_TASK_ROLE_EXECUTION_FAILED", "Codex role result was not written.")
        try:
            payload = json.loads(last_message_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            return self._technical(role, invocation_id, "REAL_TASK_ROLE_RESULT_INVALID", f"{type(exc).__name__}: role JSON result invalid.")
        if not isinstance(payload, dict):
            return self._technical(role, invocation_id, "REAL_TASK_ROLE_RESULT_INVALID", "Role JSON result must be an object.")
        schema_errors = validate(payload, read_json(schema_path))
        if schema_errors:
            return self._technical(role, invocation_id, "REAL_TASK_ROLE_RESULT_INVALID", schema_errors[0])
        status = str(payload.get("status"))
        verdict = str(payload.get("finalVerdict"))
        code = payload.get("errorCode")
        return RoleInvocationResult(
            role=role,
            invocationId=invocation_id,
            status=status,
            verdict=verdict,
            report=payload,
            errorCode=code if isinstance(code, str) else None,
            errorMessage=code if isinstance(code, str) else None,
        )

    def _codex_command(self, sandbox: str, workspace: Path, schema: Path, last_message: Path) -> list[str]:
        executable = shutil.which("codex.cmd") or shutil.which("codex") or "codex.cmd"
        real_roles = self.config.get("realRoles", {}) if isinstance(self.config, dict) else {}
        windows_sandbox = str(real_roles.get("windowsSandboxImplementation", "elevated"))
        approval = str(real_roles.get("approvalPolicy", "never"))
        return [
            executable,
            "exec",
            "--ignore-user-config",
            "-c",
            f'windows.sandbox="{windows_sandbox}"',
            "-c",
            f'approval_policy="{approval}"',
            "--sandbox",
            sandbox,
            "--cd",
            str(workspace),
            "--json",
            "--output-schema",
            str(schema),
            "--output-last-message",
            str(last_message),
            "--color",
            "never",
            "-",
        ]

    def _timeout_for(self, role: str) -> int:
        real_roles = self.config.get("realRoles", {}) if isinstance(self.config, dict) else {}
        key = {"IMPLEMENTER": "implementerTimeoutSeconds", "REPAIRER": "repairerTimeoutSeconds", "AUDITOR": "auditorTimeoutSeconds"}[role]
        value = real_roles.get(key, 600)
        return value if isinstance(value, int) and not isinstance(value, bool) and value > 0 else 600

    def _technical(self, role: str, invocation_id: str, code: str, message: str, status: str = "TECHNICAL_ERROR", verdict: str = "BLOCKED") -> RoleInvocationResult:
        return RoleInvocationResult(
            role=role,
            invocationId=invocation_id,
            status=status,
            verdict=verdict,
            report={"role": role, "status": status, "finalVerdict": verdict, "errorCode": code},
            errorCode=code,
            errorMessage=message,
        )

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


def _sanitize_command(command: list[str]) -> list[str]:
    return ["<schema>" if item.endswith(".schema.json") else item for item in command]
