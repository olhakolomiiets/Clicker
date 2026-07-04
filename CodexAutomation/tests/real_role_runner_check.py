from __future__ import annotations

import json
import os
import sys
import tempfile
import unittest
import uuid
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
from codex_runner import run_process_with_file_logs_and_timeout, run_process_with_timeout  # noqa: E402
import isolated_workspace  # noqa: E402
from isolated_workspace import LOCAL_AGENTS_TEXT, create_isolated_workspace, validate_initial_service_baseline, validate_service_directory  # noqa: E402
from pipeline_result_validator import validate_audit_result  # noqa: E402
from pipeline_validator import changed_paths, snapshot_workspace  # noqa: E402
import real_role_runner  # noqa: E402
from real_role_runner import (  # noqa: E402
    _RealContext,
    _create_probe_run_context,
    _finish,
    _finish_probe,
    _run_role,
    _service_validation_result,
    _validate_controlled_pre_repair_artifact,
    _validate_sandbox_probe_report_for_real_roles,
    ProbeExecutionOutcome,
    ProbeRunContext,
    RealRoleSettings,
    build_codex_command,
    build_implementer_prompt,
    build_sandbox_probe_command,
    parse_real_role_settings,
    run_real_role_plan,
    run_real_role_sandbox_probe,
    run_real_role_sandbox_probe_outcome,
    run_real_role_self_test,
)


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")
TASK = read_json(ROOT / "CodexAutomation" / "tests" / "fixtures" / "REAL-PIPELINE-TEST-001.json")


class RealRoleRunnerCheck(unittest.TestCase):
    def settings(self) -> RealRoleSettings:
        settings, errors = parse_real_role_settings(CONFIG)
        self.assertFalse(errors)
        self.assertIsNotNone(settings)
        return settings

    def test_commands_use_expected_sandboxes_and_workspace(self) -> None:
        settings = self.settings()
        workspace = ROOT / "CodexAutomation" / "runtime" / "real_role_workspaces" / "unit" / "workspace"
        schema = ROOT / "CodexAutomation" / "schemas" / "implementation_result.schema.json"
        out = ROOT / "CodexAutomation" / "runtime" / "real_role_runs" / "unit" / "last.json"
        implementer = build_codex_command("implementer", "workspace-write", workspace, schema, out, settings)
        repairer = build_codex_command("repairer", "workspace-write", workspace, schema, out, settings)
        auditor = build_codex_command("auditor", "read-only", workspace, schema, out, settings)
        for command in (implementer, repairer, auditor):
            self.assertIn("--cd", command)
            self.assertEqual(command[command.index("--cd") + 1], str(workspace))
            self.assertNotEqual(command[command.index("--cd") + 1], str(ROOT))
            self.assertIn("--ignore-user-config", command)
            self.assertIn("--json", command)
            self.assertIn("--output-last-message", command)
            self.assertNotIn("yolo", " ".join(command).lower())
            self.assertNotIn("danger-full-access", command)
            self.assertNotIn("--full-auto", command)
            self.assertIn('approval_policy="never"', command)
        self.assertEqual(implementer[implementer.index("--sandbox") + 1], "workspace-write")
        self.assertEqual(repairer[repairer.index("--sandbox") + 1], "workspace-write")
        self.assertEqual(auditor[auditor.index("--sandbox") + 1], "read-only")
        for command in (implementer, repairer, auditor):
            self.assertIn('windows.sandbox="elevated"', command)
            self.assertIn('approval_policy="never"', command)
            self.assertIn("--ignore-user-config", command)
            self.assertEqual(command.count("-c"), 2)

    def test_role_commands_include_explicit_windows_sandbox_for_each_role(self) -> None:
        settings = self.settings()
        workspace = ROOT / "CodexAutomation" / "runtime" / "real_role_workspaces" / "unit" / "workspace"
        schema = ROOT / "CodexAutomation" / "schemas" / "implementation_result.schema.json"
        out = ROOT / "CodexAutomation" / "runtime" / "real_role_runs" / "unit" / "last.json"
        cases = {
            "implementer": "workspace-write",
            "repairer": "workspace-write",
            "auditor": "read-only",
        }
        for role, sandbox in cases.items():
            command = build_codex_command(role, sandbox, workspace, schema, out, settings)
            self.assertIn('windows.sandbox="elevated"', command)
            self.assertEqual(command[command.index("--sandbox") + 1], sandbox)

    def test_config_rejects_unsafe_values(self) -> None:
        self.assertEqual(self.settings().maxRealCodexInvocationsPerTest, 3)
        cases = [
            ("allowRealProjectWrite", True),
            ("allowIsolatedWorkspaceWriteTest", False),
            ("automaticRetryAfterRateLimit", True),
            ("allowAutomaticUnelevatedFallback", True),
            ("requireSandboxWriteProbe", False),
            ("windowsSandboxImplementation", "ELEVATED"),
            ("windowsSandboxImplementation", "unknown"),
            ("windowsSandboxImplementation", "unelevated"),
            ("maxRealCodexInvocationsPerTest", 4),
            ("maxRealCodexInvocationsPerTest", True),
            ("enabled", "true"),
            ("requireSandboxWriteProbe", "true"),
            ("allowAutomaticUnelevatedFallback", "false"),
            ("sandboxProbeTimeoutSeconds", 0),
            ("sandboxProbeTimeoutSeconds", 61),
            ("sandboxProbeTimeoutSeconds", True),
            ("sandboxProbeTimeoutSeconds", "30"),
            ("sandboxProbeTimeoutSeconds", 30.0),
            ("sandboxProbeTaskkillTimeoutSeconds", 0),
            ("sandboxProbeTaskkillTimeoutSeconds", 16),
            ("sandboxProbeTaskkillTimeoutSeconds", "10"),
            ("sandboxProbeTaskkillTimeoutSeconds", 10.0),
            ("sandboxProbePostKillWaitSeconds", 0),
            ("sandboxProbePostKillWaitSeconds", 11),
            ("sandboxProbePostKillWaitSeconds", "5"),
            ("sandboxProbePostKillWaitSeconds", 5.0),
        ]
        for field, value in cases:
            config = json.loads(json.dumps(CONFIG))
            config["realRoles"][field] = value
            settings, errors = parse_real_role_settings(config)
            self.assertIsNone(settings, field)
            self.assertTrue(errors, field)

    def test_config_rejects_missing_probe_timeout_fields(self) -> None:
        for field in ("sandboxProbeTimeoutSeconds", "sandboxProbeTaskkillTimeoutSeconds", "sandboxProbePostKillWaitSeconds"):
            config = json.loads(json.dumps(CONFIG))
            config["realRoles"].pop(field)
            settings, errors = parse_real_role_settings(config)
            self.assertIsNone(settings, field)
            self.assertTrue(errors, field)

    def test_plan_mode_does_not_start_codex(self) -> None:
        plan = run_real_role_plan(ROOT, ROOT / "CodexAutomation", CONFIG)
        self.assertTrue(plan["ok"])
        self.assertFalse(plan["realCodexStarted"])
        self.assertEqual(plan["maxRealCodexInvocations"], 3)
        for command in plan["commands"]:
            self.assertIn('windows.sandbox="elevated"', command)

    def test_sandbox_probe_timeout_settings_are_strict(self) -> None:
        settings = self.settings()
        self.assertEqual(settings.sandboxProbeTimeoutSeconds, 30)
        self.assertEqual(settings.sandboxProbeTaskkillTimeoutSeconds, 10)
        self.assertEqual(settings.sandboxProbePostKillWaitSeconds, 5)

    def test_sandbox_probe_command_is_no_model_workspace_probe(self) -> None:
        settings = self.settings()
        workspace = ROOT / "CodexAutomation" / "runtime" / "real_role_sandbox_probes" / "unit" / "workspace"
        command = build_sandbox_probe_command(workspace, settings)
        self.assertIn("sandbox", command)
        self.assertNotIn("exec", command)
        self.assertNotIn("--json", command)
        self.assertNotIn("--output-schema", command)
        self.assertNotIn("--output-last-message", command)
        self.assertIn("-P", command)
        self.assertEqual(command[command.index("-P") + 1], ":workspace")
        self.assertIn("-C", command)
        self.assertEqual(command[command.index("-C") + 1], str(workspace))
        self.assertIn('windows.sandbox="elevated"', command)

    def test_sandbox_probe_success_creates_reads_and_removes_file(self) -> None:
        report = self._run_mocked_probe("success")
        self.assertEqual(report["finalVerdict"], "PASS")
        self.assertEqual(report["exitCode"], 0)
        self.assertTrue(report["probeFileCreated"])
        self.assertTrue(report["probeFileReadBack"])
        self.assertTrue(report["probeFileRemoved"])
        self.assertEqual(report["unexpectedPaths"], [])
        self.assertFalse(report["parentGitChanged"])
        self.assertFalse(report["realCodexStarted"])
        self.assertTrue(report["workspacePath"].startswith("CodexAutomation/runtime/real_role_sandbox_probes/"))

    def test_sandbox_probe_detects_nonzero_exit(self) -> None:
        report = self._run_mocked_probe("nonzero")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_FAILED")

    def test_sandbox_probe_detects_missing_write(self) -> None:
        report = self._run_mocked_probe("missing_write")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_FAILED")
        self.assertFalse(report["probeFileCreated"])

    def test_sandbox_probe_detects_failed_read_back(self) -> None:
        report = self._run_mocked_probe("failed_readback")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_FAILED")
        self.assertFalse(report["probeFileReadBack"])

    def test_sandbox_probe_detects_leftover_file(self) -> None:
        report = self._run_mocked_probe("leftover")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_PROBE_UNEXPECTED_FILE")
        self.assertIn("sandbox_write_probe.txt", report["unexpectedPaths"])

    def test_sandbox_probe_detects_extra_file(self) -> None:
        report = self._run_mocked_probe("extra")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_PROBE_UNEXPECTED_FILE")
        self.assertIn("extra.txt", report["unexpectedPaths"])

    def test_sandbox_probe_timeout_creates_report_and_markers(self) -> None:
        report = self._run_mocked_probe("timeout")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT")
        self.assertTrue(report["processStarted"])
        self.assertTrue(report["timedOut"])
        self.assertTrue(report["terminationAttempted"])
        probe_root = ROOT / "CodexAutomation" / "runtime" / "real_role_sandbox_probes" / report["probeRunId"]
        self.assertTrue((probe_root / "SANDBOX_WRITE_PROBE_REPORT.json").exists())
        self.assertTrue((probe_root / "probe_timeout.json").exists())
        self.assertTrue((probe_root / "probe_process.json").exists())
        self.assertTrue((probe_root / "probe_stdout.log").exists())
        self.assertTrue((probe_root / "probe_stderr.log").exists())

    def test_sandbox_probe_process_start_failure_creates_report(self) -> None:
        report = self._run_mocked_probe("start_error")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "SANDBOX_PROBE_PROCESS_START_FAILED")
        self.assertFalse(report["processStarted"])
        probe_root = ROOT / "CodexAutomation" / "runtime" / "real_role_sandbox_probes" / report["probeRunId"]
        self.assertTrue((probe_root / "SANDBOX_WRITE_PROBE_REPORT.json").exists())

    def test_sandbox_probe_termination_incomplete_is_reported(self) -> None:
        report = self._run_mocked_probe("termination_incomplete")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT")
        self.assertTrue(report["terminationIncomplete"])
        warning_codes = [warning["code"] for warning in report["reportWarnings"]]
        self.assertIn("PROCESS_TERMINATION_INCOMPLETE", warning_codes)

    def test_disk_probe_gate_allows_valid_current_pass_report(self) -> None:
        report = self._probe_report("PASS", None)
        self._write_probe_disk_report(report)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertTrue(gate.ok)

    def test_disk_probe_gate_blocks_missing_report(self) -> None:
        report = self._probe_report("PASS", None)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertFalse(gate.ok)
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_MISSING")

    def test_in_memory_pass_missing_disk_report_blocks_implementer(self) -> None:
        probe_report = self._probe_report("PASS", None)
        probe_report["writeDiskReport"] = False
        report, calls = self._run_fake_real_self_test(self._implementation_result(), None, probe_report)
        self.assertEqual(report["errorCode"], "SANDBOX_WRITE_PROBE_REPORT_MISSING")
        self.assertEqual(report["codexInvocationCount"], 0)
        self.assertFalse(report["realCodexStarted"])
        self.assertEqual(calls, [])

    def test_final_report_write_exception_turns_successful_probe_into_failure(self) -> None:
        report = self._probe_report("PASS", None)
        probe_root = ROOT / "CodexAutomation" / "runtime" / "real_role_sandbox_probes" / str(report["probeRunId"])
        probe_root.mkdir(parents=True, exist_ok=True)
        with mock.patch("real_role_runner._write_probe_report", side_effect=OSError("write denied")):
            final = _finish_probe(report, probe_root, 0.0, [], ROOT, "PASS", None, None)
        self.assertEqual(final["finalVerdict"], "FAILED")
        self.assertEqual(final["errorCode"], "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED")
        self.assertIn("SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED", final["reportWriteError"])

    def test_disk_probe_gate_blocks_running_report(self) -> None:
        report = self._probe_report("RUNNING", None)
        self._write_probe_disk_report(report)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertFalse(gate.ok)
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_NOT_FINALIZED")

    def test_disk_probe_gate_blocks_null_finished_at(self) -> None:
        report = self._probe_report("PASS", None)
        report["finishedAt"] = None
        self._write_probe_disk_report(report)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_NOT_FINALIZED")

    def test_disk_probe_gate_blocks_malformed_json(self) -> None:
        report = self._probe_report("PASS", None)
        path = ROOT / str(report["reportPath"])
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("{bad", encoding="utf-8")
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_disk_probe_gate_blocks_non_object_json(self) -> None:
        report = self._probe_report("PASS", None)
        path = ROOT / str(report["reportPath"])
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("[]", encoding="utf-8")
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_disk_probe_gate_blocks_wrong_probe_run_id(self) -> None:
        report = self._probe_report("PASS", None)
        disk = dict(report)
        disk["probeRunId"] = "old_probe"
        self._write_probe_disk_report(disk | {"reportPath": report["reportPath"]})
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_STALE")

    def test_disk_probe_gate_blocks_wrong_workspace_path(self) -> None:
        report = self._probe_report("PASS", None)
        disk = dict(report)
        disk["workspacePath"] = "CodexAutomation/runtime/real_role_sandbox_probes/other/workspace"
        self._write_probe_disk_report(disk)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_STALE")

    def test_disk_probe_gate_blocks_previous_pass_report_path(self) -> None:
        report = self._probe_report("PASS", None)
        old = self._probe_report("PASS", None)
        old["probeRunId"] = "old_probe_pass"
        old["reportPath"] = "CodexAutomation/runtime/real_role_sandbox_probes/old_probe_pass/SANDBOX_WRITE_PROBE_REPORT.json"
        old["workspacePath"] = "CodexAutomation/runtime/real_role_sandbox_probes/old_probe_pass/workspace"
        self._write_probe_disk_report(old)
        report["reportPath"] = old["reportPath"]
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_MISSING")

    def test_current_missing_report_ignores_previous_pass_report(self) -> None:
        old = self._probe_report("PASS", None)
        old["probeRunId"] = "old_probe_exists"
        old["reportPath"] = "CodexAutomation/runtime/real_role_sandbox_probes/old_probe_exists/SANDBOX_WRITE_PROBE_REPORT.json"
        old["workspacePath"] = "CodexAutomation/runtime/real_role_sandbox_probes/old_probe_exists/workspace"
        self._write_probe_disk_report(old)
        current = self._probe_report("PASS", None)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(current), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_MISSING")

    def test_old_self_consistent_pass_dict_current_missing_blocks_implementer(self) -> None:
        old = self._probe_report("PASS", None)
        self._write_probe_disk_report(old)
        current = self._probe_report("PASS", None)
        report, calls = self._run_fake_real_self_test(
            self._implementation_result(),
            None,
            probe_report=old,
            probe_context=self._probe_context(old),
            host_probe_context=self._probe_context(current),
        )
        self.assertEqual(report["errorCode"], "SANDBOX_WRITE_PROBE_CONTEXT_MISMATCH")
        self.assertEqual(report["codexInvocationCount"], 0)
        self.assertFalse(report["realCodexStarted"])
        self.assertEqual(calls, [])
        self.assertEqual(report["sandboxWriteProbeContext"]["probeRunId"], current["probeRunId"])
        self.assertEqual(report["untrustedReturnedProbeContext"]["probeRunId"], old["probeRunId"])

    def test_old_self_consistent_pass_dict_current_running_blocks_implementer(self) -> None:
        old = self._probe_report("PASS", None)
        self._write_probe_disk_report(old)
        current = self._probe_report("RUNNING", None)
        report, calls = self._run_fake_real_self_test(
            self._implementation_result(),
            None,
            probe_report=old,
            probe_context=self._probe_context(old),
            host_probe_context=self._probe_context(current),
            disk_report=current,
        )
        self.assertEqual(report["errorCode"], "SANDBOX_WRITE_PROBE_CONTEXT_MISMATCH")
        self.assertEqual(calls, [])

    def test_old_self_consistent_pass_dict_current_malformed_blocks_implementer(self) -> None:
        old = self._probe_report("PASS", None)
        self._write_probe_disk_report(old)
        current = self._probe_report("PASS", None)
        context = self._probe_context(current)
        context.report_path.parent.mkdir(parents=True, exist_ok=True)
        (context.run_directory / "workspace").mkdir(exist_ok=True)
        context.report_path.write_text("{bad", encoding="utf-8")
        report, calls = self._run_fake_real_self_test(
            self._implementation_result(),
            None,
            probe_report=old,
            probe_context=self._probe_context(old),
            host_probe_context=context,
        )
        self.assertEqual(report["errorCode"], "SANDBOX_WRITE_PROBE_CONTEXT_MISMATCH")
        self.assertEqual(calls, [])

    def test_old_context_current_pass_blocks_with_context_mismatch(self) -> None:
        old = self._probe_report("PASS", None)
        self._write_probe_disk_report(old)
        current = self._probe_report("PASS", None)
        report, calls = self._run_fake_real_self_test(
            self._implementation_result(),
            None,
            probe_report=old,
            probe_context=self._probe_context(old),
            host_probe_context=self._probe_context(current),
            disk_report=current,
        )
        self.assertEqual(calls, [])
        self.assertEqual(report["errorCode"], "SANDBOX_WRITE_PROBE_CONTEXT_MISMATCH")
        self.assertEqual(report["sandboxWriteProbeContext"]["probeRunId"], current["probeRunId"])

    def test_old_report_summary_current_context_current_pass_allows_by_current_report(self) -> None:
        old = self._probe_report("PASS", None)
        self._write_probe_disk_report(old)
        current = self._probe_report("PASS", None)
        current_context = self._probe_context(current)
        report, calls = self._run_fake_real_self_test(
            self._implementation_result(),
            None,
            probe_report=old,
            probe_context=current_context,
            host_probe_context=current_context,
            disk_report=current,
        )
        self.assertEqual(calls, ["implementer"])
        self.assertEqual(report["errorCode"], "REAL_TEST_REQUIRED_ARTIFACT_MISSING")
        self.assertEqual(report["sandboxWriteProbeContext"]["probeRunId"], current["probeRunId"])

    def test_multiple_pass_reports_use_only_trusted_current_path(self) -> None:
        first = self._probe_report("PASS", None)
        second = self._probe_report("PASS", None)
        self._write_probe_disk_report(first)
        self._write_probe_disk_report(second)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(second), self.settings())
        self.assertTrue(gate.ok)
        self.assertEqual(gate.report["probeRunId"], second["probeRunId"])

    def test_in_memory_only_sandbox_probe_helper_is_removed(self) -> None:
        self.assertFalse(hasattr(real_role_runner, "_sandbox_probe_allows_real_roles"))

    def test_production_self_test_uses_original_probe_context_for_gate_and_report(self) -> None:
        source = (SCRIPTS / "real_role_runner.py").read_text(encoding="utf-8")
        self.assertIn("_validate_sandbox_probe_report_for_real_roles(root, probe_context, settings)", source)
        self.assertIn('report["sandboxWriteProbeContext"] = _probe_context_report(root, probe_context)', source)
        self.assertNotIn("_validate_sandbox_probe_report_for_real_roles(root, probe_outcome.context", source)
        self.assertNotIn('report["sandboxWriteProbeContext"] = _probe_context_report(root, probe_outcome.context)', source)

    def test_disk_probe_gate_blocks_report_write_error(self) -> None:
        report = self._probe_report("PASS", None)
        report["reportWriteError"] = "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED"
        self._write_probe_disk_report(report)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_WRITE_FAILED")

    def test_disk_probe_gate_blocks_pass_with_error_code(self) -> None:
        report = self._probe_report("PASS", None)
        report["errorCode"] = "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"
        self._write_probe_disk_report(report)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_disk_probe_gate_blocks_timeout_report(self) -> None:
        report = self._probe_report("FAILED", "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT")
        self._write_probe_disk_report(report)
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT")

    def test_disk_probe_gate_blocks_bad_pass_contract_fields(self) -> None:
        cases = [
            ("processStarted", False, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"),
            ("exitCode", 1, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"),
            ("probeFileCreated", False, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"),
            ("probeFileReadBack", False, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"),
            ("probeFileRemoved", False, "WINDOWS_SANDBOX_WRITE_PROBE_FAILED"),
            ("unexpectedPaths", ["extra.txt"], "WINDOWS_SANDBOX_PROBE_UNEXPECTED_FILE"),
            ("parentGitChanged", True, "FAILED_WRITE_DETECTED"),
            ("parentGitChanged", None, "FAILED_WRITE_DETECTED"),
            ("realCodexStarted", True, "SANDBOX_WRITE_PROBE_REPORT_INVALID"),
            ("unityStarted", True, "SANDBOX_WRITE_PROBE_REPORT_INVALID"),
            ("networkUsed", True, "SANDBOX_WRITE_PROBE_REPORT_INVALID"),
        ]
        for field, value, expected_code in cases:
            report = self._probe_report("PASS", None)
            report[field] = value
            self._write_probe_disk_report(report)
            gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
            self.assertEqual(gate.errorCode, expected_code, field)

    def test_disk_probe_gate_blocks_unsafe_report_path(self) -> None:
        report = self._probe_report("PASS", None)
        context = self._probe_context(report)
        unsafe_context = ProbeRunContext(
            context.probe_run_id,
            context.configured_probe_root,
            context.run_directory,
            context.workspace,
            context.run_directory.parent / "old" / "SANDBOX_WRITE_PROBE_REPORT.json",
            context.started_at,
        )
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, unsafe_context, self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_disk_probe_gate_blocks_report_reparse(self) -> None:
        report = self._probe_report("PASS", None)
        self._write_probe_disk_report(report)
        context = self._probe_context(report)
        with mock.patch("real_role_runner._path_component_is_symlink_or_reparse", side_effect=lambda path: Path(path) == context.report_path):
            gate = _validate_sandbox_probe_report_for_real_roles(ROOT, context, self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_disk_probe_gate_blocks_run_directory_reparse(self) -> None:
        report = self._probe_report("PASS", None)
        self._write_probe_disk_report(report)
        context = self._probe_context(report)
        with mock.patch("real_role_runner._path_component_is_symlink_or_reparse", side_effect=lambda path: Path(path) == context.run_directory):
            gate = _validate_sandbox_probe_report_for_real_roles(ROOT, context, self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_disk_probe_gate_blocks_workspace_reparse(self) -> None:
        report = self._probe_report("PASS", None)
        self._write_probe_disk_report(report)
        context = self._probe_context(report)
        with mock.patch("real_role_runner._path_component_is_symlink_or_reparse", side_effect=lambda path: Path(path) == context.workspace):
            gate = _validate_sandbox_probe_report_for_real_roles(ROOT, context, self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_disk_probe_gate_blocks_configured_probe_root_reparse(self) -> None:
        report = self._probe_report("PASS", None)
        self._write_probe_disk_report(report)
        context = self._probe_context(report)
        with mock.patch("real_role_runner._path_component_is_symlink_or_reparse", side_effect=lambda path: Path(path) == context.configured_probe_root):
            gate = _validate_sandbox_probe_report_for_real_roles(ROOT, context, self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_report_from_neighbor_run_directory_blocks(self) -> None:
        report = self._probe_report("PASS", None)
        context = self._probe_context(report)
        neighbor = self._probe_report("PASS", None)
        self._write_probe_disk_report(neighbor)
        malicious_context = ProbeRunContext(
            context.probe_run_id,
            context.configured_probe_root,
            context.run_directory,
            context.workspace,
            self._probe_context(neighbor).report_path,
            context.started_at,
        )
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, malicious_context, self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_INVALID")

    def test_temp_report_without_final_report_blocks(self) -> None:
        report = self._probe_report("PASS", None)
        final_path = ROOT / str(report["reportPath"])
        final_path.parent.mkdir(parents=True, exist_ok=True)
        (final_path.parent / ".SANDBOX_WRITE_PROBE_REPORT.json.tmp").write_text(json.dumps(report), encoding="utf-8")
        gate = _validate_sandbox_probe_report_for_real_roles(ROOT, self._probe_context(report), self.settings())
        self.assertEqual(gate.errorCode, "SANDBOX_WRITE_PROBE_REPORT_MISSING")

    def test_task_fixture_cannot_raise_real_limits_or_project_write(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            task_path = Path(temp) / "task.json"
            task = dict(TASK)
            task["maxRealCodexInvocationsPerTest"] = 99
            task_path.write_text(json.dumps(task), encoding="utf-8")
            report = run_real_role_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, task_path)
            self.assertEqual(report["errorCode"], "REAL_ROLE_CONFIG_INVALID")
            task.pop("maxRealCodexInvocationsPerTest")
            task["allowRealProjectWrite"] = True
            task_path.write_text(json.dumps(task), encoding="utf-8")
            report = run_real_role_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, task_path)
            self.assertEqual(report["errorCode"], "REAL_ROLE_CONFIG_INVALID")
            task.pop("allowRealProjectWrite")
            task["windowsSandboxImplementation"] = "unelevated"
            task_path.write_text(json.dumps(task), encoding="utf-8")
            report = run_real_role_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, task_path)
            self.assertEqual(report["errorCode"], "REAL_ROLE_CONFIG_INVALID")
            task.pop("windowsSandboxImplementation")
            task["sandboxProbeTimeoutSeconds"] = 5
            task_path.write_text(json.dumps(task), encoding="utf-8")
            report = run_real_role_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, task_path)
            self.assertEqual(report["errorCode"], "REAL_ROLE_CONFIG_INVALID")

    def test_isolated_workspace_outside_runtime_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT) as temp:
            workspace, errors = create_isolated_workspace(ROOT, "unit", Path(temp), TASK)
            self.assertIsNone(workspace)
            self.assertIn("ISOLATED_WORKSPACE_OUTSIDE_RUNTIME", errors)

    def test_symlink_or_reparse_workspace_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            with mock.patch("isolated_workspace.snapshot_workspace", return_value={"__workspace_error__": {"errorCode": "UNSAFE_SYMLINK_OR_REPARSE_POINT"}}):
                workspace, errors = create_isolated_workspace(ROOT, "unit", Path(temp), TASK)
                self.assertIsNone(workspace)
                self.assertIn("UNSAFE_SYMLINK_OR_REPARSE_POINT", errors)

    def test_isolated_workspace_precreates_empty_agents_service_directory(self) -> None:
        workspace = self._create_unit_workspace()
        snapshot = snapshot_workspace(workspace)
        self.assertIn(".agents", snapshot)
        self.assertEqual(snapshot[".agents"]["type"], "directory")
        self.assertEqual([path for path in snapshot if path.startswith(".agents/")], [])
        self.assertEqual(validate_service_directory(workspace, snapshot), None)

    def test_agents_service_directory_created_only_inside_current_workspace(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            fake_root = Path(temp) / "fake_project"
            isolated_root = fake_root / "CodexAutomation" / "runtime" / "real_role_workspaces"
            workspace, errors = create_isolated_workspace(fake_root, "unit", isolated_root, TASK)
            self.assertFalse(errors)
            self.assertIsNotNone(workspace)
            self.assertTrue((workspace / ".agents").is_dir())
            self.assertFalse((fake_root / ".agents").exists())
            self.assertFalse((fake_root / "CodexAutomation" / ".agents").exists())
            self.assertFalse((workspace.parent / ".agents").exists())

    def test_initial_service_baseline_allows_exact_empty_agents(self) -> None:
        workspace = self._create_unit_workspace()
        self.assertIsNone(validate_initial_service_baseline(workspace, snapshot_workspace(workspace)))

    def test_missing_agents_service_directory_blocks(self) -> None:
        workspace = self._create_unit_workspace()
        (workspace / ".agents").rmdir()
        error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_MISSING")

    def test_agents_regular_file_blocks(self) -> None:
        workspace = self._create_unit_workspace()
        (workspace / ".agents").rmdir()
        (workspace / ".agents").write_text("bad", encoding="utf-8")
        error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_INVALID")

    def test_agents_symlink_blocks_when_supported(self) -> None:
        workspace = self._create_unit_workspace()
        (workspace / ".agents").rmdir()
        target = workspace / "target_agents"
        target.mkdir()
        try:
            os.symlink(target, workspace / ".agents", target_is_directory=True)
        except (OSError, NotImplementedError) as exc:
            self.skipTest(f"symlink unavailable: {exc}")
        error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_INVALID")

    def test_agents_reparse_point_blocks(self) -> None:
        workspace = self._create_unit_workspace()
        with mock.patch("isolated_workspace._is_reparse_point", return_value=True):
            error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_INVALID")

    def test_agents_outside_workspace_containment_blocks(self) -> None:
        state = {"path": ".agents", "exists": True, "type": "directory", "isSymlink": False, "isReparsePoint": False, "insideWorkspace": False, "entries": []}
        with mock.patch("isolated_workspace.service_directory_state", return_value=state):
            error = validate_service_directory(Path("workspace"), {".agents": {"type": "directory"}})
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_INVALID")

    def test_agents_hidden_file_blocks_and_reports_exact_child(self) -> None:
        workspace = self._create_unit_workspace()
        (workspace / ".agents" / ".hidden").write_text("bad", encoding="utf-8")
        error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(error["unexpectedPaths"], [".agents/.hidden"])

    def test_agents_regular_child_file_blocks(self) -> None:
        workspace = self._create_unit_workspace()
        (workspace / ".agents" / "file").write_text("bad", encoding="utf-8")
        error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(error["unexpectedPaths"], [".agents/file"])

    def test_agents_nested_empty_directory_blocks(self) -> None:
        workspace = self._create_unit_workspace()
        (workspace / ".agents" / "subdirectory").mkdir()
        error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(error["unexpectedPaths"], [".agents/subdirectory"])

    def test_agents_symlink_child_blocks_when_supported(self) -> None:
        workspace = self._create_unit_workspace()
        target = workspace / "target.txt"
        target.write_text("target", encoding="utf-8")
        try:
            os.symlink(target, workspace / ".agents" / "link")
        except (OSError, NotImplementedError) as exc:
            self.skipTest(f"symlink unavailable: {exc}")
        error = validate_service_directory(workspace, snapshot_workspace(workspace))
        self.assertEqual(error["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(error["unexpectedPaths"], [".agents/link"])

    def test_initial_agents_plus_artifact_changed_paths_are_exact_artifact(self) -> None:
        workspace = self._create_unit_workspace()
        before = snapshot_workspace(workspace)
        self._write_pre_repair_artifact(workspace)
        after = snapshot_workspace(workspace)
        self.assertEqual(changed_paths(before, after), ["artifact.json"])
        validation = _validate_controlled_pre_repair_artifact(TASK, workspace, before, after)
        self.assertEqual(validation["verdict"], "FIX_REQUIRED")
        self.assertEqual(validation["actualChangedPaths"], ["artifact.json"])

    def test_agents_created_after_initial_snapshot_still_blocks(self) -> None:
        workspace = self._create_unit_workspace()
        initial = snapshot_workspace(workspace)
        (workspace / ".agents").rmdir()
        initial_without_agents = snapshot_workspace(workspace)
        (workspace / ".agents").mkdir()
        self._write_pre_repair_artifact(workspace)
        after = snapshot_workspace(workspace)
        self.assertNotIn(".agents", initial_without_agents)
        self.assertIn(".agents", changed_paths(initial_without_agents, after))
        validation = _validate_controlled_pre_repair_artifact(TASK, workspace, initial_without_agents, after)
        self.assertEqual(validation["errorCode"], "REAL_TEST_UNEXPECTED_FILE")
        self.assertIn(".agents", validation["actualChangedPaths"])
        self.assertIn(".agents", initial)

    def test_agents_child_after_implementer_blocks(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / ".agents" / "child").write_text("bad", encoding="utf-8")))
        self.assertEqual(report["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(calls, ["implementer"])
        self.assertEqual(report["repairExecutionAttempts"], [])

    def test_agents_removed_after_implementer_blocks(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / ".agents").rmdir()))
        self.assertEqual(report["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_REPLACED")
        self.assertEqual(calls, ["implementer"])

    def test_agents_replaced_by_file_after_implementer_blocks(self) -> None:
        def mutate(workspace: Path) -> None:
            self._write_pre_repair_artifact(workspace)
            (workspace / ".agents").rmdir()
            (workspace / ".agents").write_text("bad", encoding="utf-8")

        report, calls = self._run_fake_real_self_test(self._implementation_result(), mutate)
        self.assertEqual(report["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_REPLACED")
        self.assertEqual(calls, ["implementer"])

    def test_agents_replaced_by_reparse_after_implementer_blocks(self) -> None:
        workspace = self._create_unit_workspace()
        before = snapshot_workspace(workspace)
        self._write_pre_repair_artifact(workspace)
        with mock.patch("isolated_workspace._is_reparse_point", return_value=True):
            after = snapshot_workspace(workspace)
            validation = _validate_controlled_pre_repair_artifact(TASK, workspace, before, after)
        self.assertEqual(validation["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_REPLACED")

    def test_correct_artifact_and_empty_agents_passes_pre_repair_validation(self) -> None:
        workspace = self._create_unit_workspace()
        before = snapshot_workspace(workspace)
        self._write_pre_repair_artifact(workspace)
        validation = _validate_controlled_pre_repair_artifact(TASK, workspace, before, snapshot_workspace(workspace))
        self.assertEqual(validation["verdict"], "FIX_REQUIRED")
        self.assertEqual(validation["unexpectedPaths"], [])

    def test_correct_artifact_and_nonempty_agents_fails(self) -> None:
        workspace = self._create_unit_workspace()
        before = snapshot_workspace(workspace)
        self._write_pre_repair_artifact(workspace)
        (workspace / ".agents" / "file").write_text("bad", encoding="utf-8")
        validation = _validate_controlled_pre_repair_artifact(TASK, workspace, before, snapshot_workspace(workspace))
        self.assertEqual(validation["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(validation["unexpectedPaths"], [".agents/file"])

    def test_other_empty_runtime_directory_blocks_as_unexpected(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / ".other-runtime").mkdir()))
        self._assert_scope_violation(report, calls, ".other-runtime")

    def test_other_dotfile_blocks_as_unexpected(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / ".codex").write_text("bad", encoding="utf-8")))
        self._assert_scope_violation(report, calls, ".codex")

    def test_repairer_passes_only_with_empty_unchanged_agents(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_pre_repair_artifact)
        self.assertEqual(report["finalState"], "COMPLETED")
        self.assertIn("repairer", calls)
        self.assertIn("auditor", calls)

    def test_repairer_creating_agents_file_blocks(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_pre_repair_artifact, repairer_mutation=lambda workspace: (workspace / ".agents" / "file").write_text("bad", encoding="utf-8"))
        self.assertEqual(report["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(calls, ["implementer", "repairer"])
        self.assertEqual(report["auditExecutionAttempts"], [])

    def test_auditor_leaving_agents_empty_passes_service_validation(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_pre_repair_artifact)
        self.assertEqual(report["finalState"], "COMPLETED")
        self.assertEqual(calls, ["implementer", "repairer", "auditor"])

    def test_auditor_creating_agents_file_blocks(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_pre_repair_artifact, auditor_mutation=lambda workspace: (workspace / ".agents" / "file").write_text("bad", encoding="utf-8"))
        self.assertEqual(report["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(calls, ["implementer", "repairer", "auditor"])

    def test_auditor_removing_agents_blocks(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_pre_repair_artifact, auditor_mutation=lambda workspace: (workspace / ".agents").rmdir())
        self.assertEqual(report["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_REPLACED")
        self.assertEqual(calls, ["implementer", "repairer", "auditor"])

    def test_audit_schema_has_explicit_nullable_blocked_reason(self) -> None:
        schema = read_json(ROOT / "CodexAutomation" / "schemas" / "audit_result.schema.json")
        self.assertEqual(schema["type"], "object")
        self.assertFalse(schema["additionalProperties"])
        self.assertIn("blockedReason", schema["required"])
        self.assertTrue(set(schema["required"]).issubset(set(schema["properties"])))
        self.assertEqual(schema["properties"]["blockedReason"], {"type": ["string", "null"]})
        self.assertEqual(self._empty_property_paths(schema), [])

    def test_audit_schema_accepts_null_or_string_blocked_reason_only(self) -> None:
        valid_pass = self._audit_result("PASS", None)
        valid_blocked = self._audit_result("BLOCKED", "manual review required")
        self.assertEqual(validate_audit_result(valid_pass, ROOT / "CodexAutomation" / "schemas", "REAL-PIPELINE-TEST-001"), [])
        self.assertEqual(validate_audit_result(valid_blocked, ROOT / "CodexAutomation" / "schemas", "REAL-PIPELINE-TEST-001"), [])
        object_blocked = self._audit_result("PASS", {"reason": "bad"})
        array_blocked = self._audit_result("PASS", ["bad"])
        missing_blocked = self._audit_result("PASS", None)
        missing_blocked.pop("blockedReason")
        self.assertTrue(validate_audit_result(object_blocked, ROOT / "CodexAutomation" / "schemas", "REAL-PIPELINE-TEST-001"))
        self.assertTrue(validate_audit_result(array_blocked, ROOT / "CodexAutomation" / "schemas", "REAL-PIPELINE-TEST-001"))
        self.assertTrue(validate_audit_result(missing_blocked, ROOT / "CodexAutomation" / "schemas", "REAL-PIPELINE-TEST-001"))

    def test_auditor_schema_invalid_execution_reports_stable_code(self) -> None:
        report, calls = self._run_fake_real_self_test(
            self._implementation_result(),
            self._write_pre_repair_artifact,
            role_process_overrides={
                "auditor": {
                    "stdout": self._invalid_audit_schema_jsonl(),
                    "stderr": "",
                    "timed_out": False,
                    "exit_code": 1,
                    "termination": {},
                }
            },
            skip_last_message_roles={"auditor"},
        )
        self.assertEqual(report["finalState"], "FAILED")
        self.assertEqual(report["errorCode"], "CODEX_EXEC_SCHEMA_INVALID")
        self.assertEqual(report["codexInvocationCount"], 3)
        self.assertEqual(calls, ["implementer", "repairer", "auditor"])
        self.assertEqual(report["repairExecutionAttempts"][0]["result"]["filesChanged"], ["artifact.json"])
        self.assertEqual(report["validationAttempts"][-1]["verdict"], "PASS")
        self.assertEqual(report["validationAttempts"][-1]["actualChangedPaths"], ["artifact.json"])
        self.assertEqual(report["workspaceFinalSnapshot"][".agents"]["type"], "directory")
        self.assertFalse(report["parentGitChanged"])
        self.assertFalse(report["unityStarted"])
        self.assertTrue(report["eventSummaries"]["auditor"]["schemaInvalid"])
        self.assertEqual(report["eventSummaries"]["auditor"]["schemaErrorCode"], "invalid_json_schema")
        self.assertIn("CODEX_LAST_MESSAGE_MISSING", report["errorMessage"])
        self.assertIn("CODEX_EXEC_SCHEMA_INVALID", report["errorMessage"])
        self.assertEqual(report["auditExecutionAttempts"][0]["exitCode"], 1)
        self.assertEqual(report["auditExecutionAttempts"][0]["schemaErrorCode"], "invalid_json_schema")

    def test_auditor_schema_invalid_stays_primary_when_last_message_missing(self) -> None:
        self.assertEqual(
            real_role_runner._role_error_code("auditor", [], [], ["CODEX_EXEC_SCHEMA_INVALID: bad schema", "CODEX_LAST_MESSAGE_MISSING"]),
            "CODEX_EXEC_SCHEMA_INVALID",
        )

    def test_corrected_audit_schema_allows_three_role_pass(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_pre_repair_artifact)
        self.assertEqual(report["finalState"], "COMPLETED")
        self.assertEqual(report["finalVerdict"], "PASS")
        self.assertEqual(calls, ["implementer", "repairer", "auditor"])
        self.assertIsNone(report["auditExecutionAttempts"][0]["result"]["blockedReason"])

    def test_blocked_audit_with_string_blocked_reason_is_domain_result_not_schema_error(self) -> None:
        auditor_result = self._audit_result("BLOCKED", "external state unavailable")
        report, calls = self._run_fake_real_self_test(
            self._implementation_result(),
            self._write_pre_repair_artifact,
            role_results_override={"auditor": auditor_result},
        )
        self.assertEqual(report["finalState"], "COMPLETED")
        self.assertEqual(report["finalVerdict"], "PASS")
        self.assertEqual(calls, ["implementer", "repairer", "auditor"])
        self.assertFalse(report["eventSummaries"]["auditor"]["schemaInvalid"])
        self.assertEqual(report["auditExecutionAttempts"][0]["result"]["blockedReason"], "external state unavailable")

    def test_reproduce_previous_failure_shape(self) -> None:
        initial = {"AGENTS.md": {"type": "file"}, "task.json": {"type": "file"}}
        after = dict(initial)
        after[".agents"] = {"type": "directory"}
        after["artifact.json"] = {"type": "file"}
        self.assertEqual(changed_paths(initial, after), [".agents", "artifact.json"])
        self.assertEqual([path for path in changed_paths(initial, after) if path != "artifact.json"], [".agents"])

    def test_new_snapshot_flow_with_initial_agents_passes(self) -> None:
        workspace = self._create_unit_workspace()
        initial = snapshot_workspace(workspace)
        self._write_pre_repair_artifact(workspace)
        after = snapshot_workspace(workspace)
        self.assertIn(".agents", initial)
        self.assertEqual(changed_paths(initial, after), ["artifact.json"])
        validation = _validate_controlled_pre_repair_artifact(TASK, workspace, initial, after)
        self.assertEqual(validation["verdict"], "FIX_REQUIRED")
        self.assertEqual(read_json(workspace / "artifact.json"), {"taskId": "REAL-PIPELINE-TEST-001", "value": 41, "repairApplied": False})

    def test_no_wildcard_ignore_for_dotfiles_or_agents_children(self) -> None:
        sources = "\n".join(
            [
                (ROOT / "CodexAutomation" / "scripts" / "real_role_runner.py").read_text(encoding="utf-8"),
                (ROOT / "CodexAutomation" / "scripts" / "isolated_workspace.py").read_text(encoding="utf-8"),
            ]
        )
        self.assertNotIn(".agents/**", sources)
        self.assertNotIn(".*", sources)
        self.assertNotIn("dotfile", sources.lower())

    def test_agents_children_are_not_allowlisted_and_reported_exactly(self) -> None:
        workspace = self._create_unit_workspace()
        before = snapshot_workspace(workspace)
        self._write_pre_repair_artifact(workspace)
        (workspace / ".agents" / "nested.tmp").write_text("bad", encoding="utf-8")
        validation = _validate_controlled_pre_repair_artifact(TASK, workspace, before, snapshot_workspace(workspace))
        self.assertEqual(validation["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(validation["unexpectedPaths"], [".agents/nested.tmp"])

    def test_parent_git_fingerprint_protection_remains_active_with_agents(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / ".git" / "config").write_text("changed", encoding="utf-8")))
        self.assertEqual(report["errorCode"], "REAL_TEST_GIT_STATE_CHANGED")
        self.assertEqual(calls, ["implementer"])

    def test_service_gate_failure_keeps_repairer_and_auditor_at_zero(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / ".agents" / "file").write_text("bad", encoding="utf-8")))
        self.assertEqual(report["errorCode"], "REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY")
        self.assertEqual(report["repairExecutionAttempts"], [])
        self.assertEqual(report["auditExecutionAttempts"], [])
        self.assertEqual(calls, ["implementer"])

    def test_timeout_process_result_is_failure_without_real_process(self) -> None:
        settings = self.settings()
        self.assertEqual(settings.maxRealCodexInvocationsPerTest, 3)
        # The production runner delegates timeout termination to codex_runner.run_process_with_timeout;
        # process_timeout_check covers the Windows process tree behavior.
        self.assertTrue(callable(build_codex_command))

    def test_local_agents_template_allows_required_artifact_creation(self) -> None:
        self.assertIn("Creating artifact.json is explicitly allowed and required.", LOCAL_AGENTS_TEXT)
        self.assertIn("artifact.json is the only file you may create or modify.", LOCAL_AGENTS_TEXT)
        self.assertIn("Do not modify AGENTS.md, task.json, or .git.", LOCAL_AGENTS_TEXT)
        self.assertIn("Do not merely report that artifact.json was created.", LOCAL_AGENTS_TEXT)

    def test_implementer_prompt_requires_read_back_verification(self) -> None:
        prompt = build_implementer_prompt(TASK)
        self.assertIn("You must actually create artifact.json on disk.", prompt)
        self.assertIn("Creating artifact.json is explicitly allowed and required.", prompt)
        self.assertIn("Do not merely return a structured report describing the file.", prompt)
        self.assertIn("read it back from disk", prompt)
        self.assertIn("parse it as JSON", prompt)
        self.assertIn("Structured output never replaces the filesystem operation.", prompt)

    def test_fourth_invocation_is_blocked(self) -> None:
        settings = self.settings()
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            report = {"codexInvocationCount": 3, "errorCode": None, "errorMessage": None, "finalState": "IMPLEMENTING", "stateHistory": ["PENDING", "IMPLEMENTING"]}
            context = _RealContext(ROOT, ROOT / "CodexAutomation", temp_path, temp_path, settings, TASK, report)
            status = _run_role(context, "implementer", "workspace-write", 1, "prompt", 1)
            self.assertEqual(status, "stop")
            self.assertEqual(report["errorCode"], "REAL_ROLE_INVOCATION_LIMIT")

    def test_stdout_is_not_structured_result_source(self) -> None:
        settings = self.settings()
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            report = {
                "codexInvocationCount": 0,
                "errorCode": None,
                "errorMessage": None,
                "finalState": "IMPLEMENTING",
                "stateHistory": ["PENDING", "IMPLEMENTING"],
                "actualCommands": [],
                "eventSummaries": {},
                "rateLimitPauses": [],
                "validationAttempts": [],
                "implementationExecution": None,
                "repairExecutionAttempts": [],
                "auditExecutionAttempts": [],
                "realCodexStarted": False,
            }
            context = _RealContext(ROOT, ROOT / "CodexAutomation", temp_path, temp_path, settings, TASK, report)
            fake_process = {"stdout": json.dumps({"status": "implemented"}) + "\n", "stderr": "", "timed_out": False, "exit_code": 0, "termination": {}}
            with mock.patch("real_role_runner.run_process_with_timeout", return_value=fake_process):
                status = _run_role(context, "implementer", "workspace-write", 1, "prompt", 1)
            self.assertEqual(status, "stop")
            self.assertEqual(report["errorCode"], "REAL_ROLE_EXECUTION_FAILED")
            self.assertIn("CODEX_LAST_MESSAGE_MISSING", report["errorMessage"])

    def test_successful_implementer_without_artifact_fails_before_repair_or_audit(self) -> None:
        result = {
            "status": "implemented",
            "taskId": "REAL-PIPELINE-TEST-001",
            "summary": "claimed success",
            "filesChanged": [],
            "testsAttempted": [],
            "knownIssues": [],
        }
        report, calls = self._run_fake_real_self_test(result)
        self.assertEqual(report["finalState"], "FAILED")
        self.assertEqual(report["errorCode"], "REAL_TEST_REQUIRED_ARTIFACT_MISSING")
        self.assertEqual(report["codexInvocationCount"], 1)
        self.assertEqual(calls, ["implementer"])
        self.assertEqual(report["stateHistory"], ["PENDING", "IMPLEMENTING", "VALIDATING", "FAILED"])
        self.assertEqual(report["implementationExecution"]["executionStatus"], "SUCCESS")
        self.assertEqual(report["validationAttempts"][0]["actualChangedPaths"], [])
        self.assertTrue((ROOT / CONFIG["realRoles"]["realRoleReportRoot"] / report["runId"] / "FINAL_REAL_ROLE_REPORT.json").exists())

    def test_sandbox_probe_failure_prevents_implementer_invocation(self) -> None:
        result = self._implementation_result()
        probe_report = self._probe_report("FAILED", "WINDOWS_SANDBOX_WRITE_PROBE_FAILED")
        report, calls = self._run_fake_real_self_test(result, None, probe_report)
        self.assertEqual(report["finalState"], "BLOCKED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_FAILED")
        self.assertEqual(report["codexInvocationCount"], 0)
        self.assertFalse(report["realCodexStarted"])
        self.assertEqual(calls, [])
        report_path = ROOT / CONFIG["realRoles"]["realRoleReportRoot"] / report["runId"] / "FINAL_REAL_ROLE_REPORT.json"
        self.assertTrue(report_path.exists())

    def test_sandbox_probe_timeout_prevents_all_real_roles(self) -> None:
        result = self._implementation_result()
        probe_report = self._probe_report("FAILED", "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT")
        probe_report["timedOut"] = True
        report, calls = self._run_fake_real_self_test(result, None, probe_report)
        self.assertEqual(report["finalState"], "BLOCKED")
        self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT")
        self.assertEqual(report["codexInvocationCount"], 0)
        self.assertFalse(report["realCodexStarted"])
        self.assertEqual(calls, [])
        self.assertEqual(report["repairExecutionAttempts"], [])
        self.assertEqual(report["auditExecutionAttempts"], [])

    def test_implementation_result_claims_artifact_but_filesystem_missing(self) -> None:
        result = {
            "status": "implemented",
            "taskId": "REAL-PIPELINE-TEST-001",
            "summary": "claimed artifact",
            "filesChanged": ["artifact.json"],
            "testsAttempted": [],
            "knownIssues": [],
        }
        report, calls = self._run_fake_real_self_test(result)
        self.assertEqual(report["errorCode"], "REAL_TEST_REQUIRED_ARTIFACT_MISSING")
        self.assertEqual(report["actualChangedPaths"], [])
        self.assertEqual(report["codexInvocationCount"], 1)
        self.assertEqual(calls, ["implementer"])

    def test_correct_pre_repair_artifact_allows_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_pre_repair_artifact)
        self.assertIn("repairer", calls)
        self.assertGreaterEqual(report["codexInvocationCount"], 2)

    def test_artifact_and_agents_change_blocks_repairer_and_auditor(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / "AGENTS.md").write_text("changed", encoding="utf-8")))
        self._assert_scope_violation(report, calls, "AGENTS.md")

    def test_artifact_and_task_change_blocks_repairer_and_auditor(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / "task.json").write_text("{}", encoding="utf-8")))
        self._assert_scope_violation(report, calls, "task.json")

    def test_artifact_and_extra_file_blocks_repairer_and_auditor(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / "extra.txt").write_text("extra", encoding="utf-8")))
        self._assert_scope_violation(report, calls, "extra.txt")

    def test_artifact_and_subdirectory_extra_blocks_repairer_and_auditor(self) -> None:
        def mutate(workspace: Path) -> None:
            self._write_pre_repair_artifact(workspace)
            (workspace / "sub").mkdir()
            (workspace / "sub" / "extra.json").write_text("{}", encoding="utf-8")

        report, calls = self._run_fake_real_self_test(self._implementation_result(), mutate)
        self._assert_scope_violation(report, calls, "sub/extra.json")

    def test_artifact_and_deleted_task_blocks_repairer_and_auditor(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / "task.json").unlink()))
        self._assert_scope_violation(report, calls, "task.json")

    def test_artifact_and_renamed_agents_blocks_repairer_and_auditor(self) -> None:
        def mutate(workspace: Path) -> None:
            self._write_pre_repair_artifact(workspace)
            (workspace / "AGENTS.md").rename(workspace / "AGENTS_RENAMED.md")

        report, calls = self._run_fake_real_self_test(self._implementation_result(), mutate)
        self._assert_scope_violation(report, calls, "AGENTS.md")
        self.assertIn("AGENTS_RENAMED.md", report["validationAttempts"][0]["actualChangedPaths"])

    def test_artifact_and_git_state_change_blocks_before_repairer(self) -> None:
        def mutate(workspace: Path) -> None:
            self._write_pre_repair_artifact(workspace)
            with (workspace / ".git" / "config").open("a", encoding="utf-8") as handle:
                handle.write("\n[unit]\n\tvalue = changed\n")

        report, calls = self._run_fake_real_self_test(self._implementation_result(), mutate)
        self.assertEqual(report["finalState"], "FAILED")
        self.assertEqual(report["errorCode"], "REAL_TEST_GIT_STATE_CHANGED")
        self.assertEqual(calls, ["implementer"])

    def test_artifact_missing_uses_required_artifact_code(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), None)
        self.assertEqual(report["errorCode"], "REAL_TEST_REQUIRED_ARTIFACT_MISSING")
        self.assertEqual(calls, ["implementer"])

    def test_implementer_skipped_to_final_artifact_blocks_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), self._write_final_artifact)
        self.assertEqual(report["errorCode"], "REAL_TEST_IMPLEMENTER_SKIPPED_REQUIRED_PRE_REPAIR_STATE")
        self.assertEqual(calls, ["implementer"])

    def test_wrong_task_id_blocks_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: self._write_artifact(workspace, {"taskId": "WRONG", "value": 41, "repairApplied": False}))
        self.assertEqual(report["errorCode"], "REAL_ROLE_EXECUTION_FAILED")
        self.assertEqual(calls, ["implementer"])

    def test_wrong_pre_repair_value_blocks_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: self._write_artifact(workspace, {"taskId": "REAL-PIPELINE-TEST-001", "value": 40, "repairApplied": False}))
        self.assertEqual(report["errorCode"], "REAL_ROLE_EXECUTION_FAILED")
        self.assertEqual(calls, ["implementer"])

    def test_wrong_repair_applied_blocks_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: self._write_artifact(workspace, {"taskId": "REAL-PIPELINE-TEST-001", "value": 41, "repairApplied": True}))
        self.assertEqual(report["errorCode"], "REAL_ROLE_EXECUTION_FAILED")
        self.assertEqual(calls, ["implementer"])

    def test_extra_artifact_json_field_blocks_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: self._write_artifact(workspace, {"taskId": "REAL-PIPELINE-TEST-001", "value": 41, "repairApplied": False, "extra": True}))
        self.assertEqual(report["errorCode"], "REAL_ROLE_EXECUTION_FAILED")
        self.assertEqual(calls, ["implementer"])

    def test_invalid_artifact_json_blocks_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (workspace / "artifact.json").write_text("{bad", encoding="utf-8"))
        self.assertEqual(report["errorCode"], "JSON_INVALID")
        self.assertEqual(calls, ["implementer"])

    def test_directory_artifact_blocks_repairer(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (workspace / "artifact.json").mkdir())
        self.assertEqual(report["errorCode"], "VALIDATION_PATH_NOT_FILE")
        self.assertEqual(calls, ["implementer"])

    def test_files_changed_claim_cannot_hide_extra_filesystem_change(self) -> None:
        result = self._implementation_result()
        result["filesChanged"] = ["artifact.json"]
        report, calls = self._run_fake_real_self_test(result, lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / "extra.txt").write_text("extra", encoding="utf-8")))
        self._assert_scope_violation(report, calls, "extra.txt")
        self.assertIn("extra.txt", report["actualChangedPaths"])

    def test_scope_violation_final_report_contains_full_changed_paths(self) -> None:
        report, calls = self._run_fake_real_self_test(self._implementation_result(), lambda workspace: (self._write_pre_repair_artifact(workspace), (workspace / "extra.txt").write_text("extra", encoding="utf-8")))
        self._assert_scope_violation(report, calls, "extra.txt")
        report_path = ROOT / CONFIG["realRoles"]["realRoleReportRoot"] / report["runId"] / "FINAL_REAL_ROLE_REPORT.json"
        final_report = read_json(report_path)
        self.assertEqual(final_report["actualChangedPaths"], ["artifact.json", "extra.txt"])

    def test_finalization_parent_git_snapshot_failure_preserves_original_error(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            report_root = Path(temp)
            report = {
                "stateHistory": ["PENDING", "IMPLEMENTING", "VALIDATING"],
                "workspacePath": None,
                "initialParentGitSnapshot": [],
                "parentGitChanged": False,
                "errorCode": None,
                "errorMessage": None,
            }
            with mock.patch("real_role_runner._git_snapshot", side_effect=RuntimeError("snapshot boom")):
                final = _finish(report, report_root, 0.0, "FAILED", "REAL_TEST_UNEXPECTED_FILE", "original", ROOT)
            self.assertEqual(final["errorCode"], "REAL_TEST_UNEXPECTED_FILE")
            self.assertIsNone(final["parentGitChanged"])
            self.assertEqual(final["warnings"][0]["code"], "FINAL_PARENT_GIT_SNAPSHOT_FAILED")
            self.assertTrue((report_root / "FINAL_REAL_ROLE_REPORT.json").exists())

    def test_unexpected_local_validation_exception_still_writes_final_report(self) -> None:
        result = {
            "status": "implemented",
            "taskId": "REAL-PIPELINE-TEST-001",
            "summary": "claimed success",
            "filesChanged": [],
            "testsAttempted": [],
            "knownIssues": [],
        }
        with mock.patch("real_role_runner._validate_controlled_pre_repair_artifact", side_effect=RuntimeError("unit validation boom")):
            report, calls = self._run_fake_real_self_test(result)
        report_path = ROOT / CONFIG["realRoles"]["realRoleReportRoot"] / report["runId"] / "FINAL_REAL_ROLE_REPORT.json"
        self.assertEqual(report["finalState"], "FAILED")
        self.assertEqual(report["errorCode"], "REAL_ROLE_INTERNAL_ERROR")
        self.assertEqual(report["codexInvocationCount"], 1)
        self.assertEqual(calls, ["implementer"])
        self.assertTrue(report_path.exists())
        self.assertIsInstance(report["finalParentGitSnapshot"], list)
        self.assertFalse(report["unityStarted"])

    def _run_fake_real_self_test(
        self,
        implementer_result: dict[str, object],
        implementer_mutation: object | None = None,
        probe_report: dict[str, object] | None = None,
        probe_context: ProbeRunContext | None = None,
        host_probe_context: ProbeRunContext | None = None,
        disk_report: dict[str, object] | None = None,
        repairer_mutation: object | None = None,
        auditor_mutation: object | None = None,
        role_process_overrides: dict[str, dict[str, object]] | None = None,
        skip_last_message_roles: set[str] | None = None,
        role_results_override: dict[str, dict[str, object]] | None = None,
    ) -> tuple[dict[str, object], list[str]]:
        calls: list[str] = []
        role_process_overrides = role_process_overrides or {}
        skip_last_message_roles = skip_last_message_roles or set()
        required_help = " ".join(["--sandbox", "--cd", "--json", "--output-schema", "--output-last-message", "--color", "--ignore-user-config"])
        role_results = {
            "implementer": implementer_result,
            "repairer": {
                "status": "repaired",
                "taskId": "REAL-PIPELINE-TEST-001",
                "summary": "repaired",
                "filesChanged": ["artifact.json"],
                "addressedFindingCodes": ["ARTIFACT_VALUE_INCORRECT"],
                "remainingIssues": [],
            },
            "auditor": {
                "verdict": "PASS",
                "taskId": "REAL-PIPELINE-TEST-001",
                "summary": "pass",
                "findings": [],
                "filesReviewed": ["artifact.json"],
                "scopeViolations": [],
                "blockedReason": None,
            },
        }
        if role_results_override:
            role_results.update(role_results_override)

        def fake_process(command: list[str], cwd: Path, prompt: str, timeout_seconds: int) -> dict[str, object]:
            role = "implementer"
            if "real repairer" in prompt:
                role = "repairer"
            elif "real read-only auditor" in prompt:
                role = "auditor"
            calls.append(role)
            if role == "implementer" and implementer_mutation is not None:
                implementer_mutation(cwd)
            if role == "repairer":
                self._write_final_artifact(cwd)
                if repairer_mutation is not None:
                    repairer_mutation(cwd)
            if role == "auditor" and auditor_mutation is not None:
                auditor_mutation(cwd)
            last_message = Path(command[command.index("--output-last-message") + 1])
            if role not in skip_last_message_roles:
                last_message.parent.mkdir(parents=True, exist_ok=True)
                payload = role_results[role]
                last_message.write_text(json.dumps(payload), encoding="utf-8")
            stdout = "\n".join(
                [
                    json.dumps({"type": "turn.started"}),
                    json.dumps({"type": "turn.completed"}),
                ]
            )
            process = {"stdout": stdout, "stderr": "", "timed_out": False, "exit_code": 0, "termination": {}}
            process.update(role_process_overrides.get(role, {}))
            return process

        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            task_path = Path(temp) / "task.json"
            task_path.write_text(json.dumps(TASK), encoding="utf-8")
            effective_probe_report = probe_report or self._probe_report("PASS", None)
            effective_host_context = host_probe_context or self._probe_context(effective_probe_report)
            effective_probe_context = probe_context or effective_host_context
            if disk_report is not None:
                self._write_probe_disk_report(disk_report)
            elif effective_probe_report.get("writeDiskReport", True):
                self._write_probe_disk_report(effective_probe_report)
            effective_probe_outcome = ProbeExecutionOutcome(effective_probe_context, effective_probe_report)
            with mock.patch("real_role_runner._codex_version", return_value=("codex 0.0.0-test", None)), mock.patch(
                "real_role_runner._codex_help", return_value=required_help
            ), mock.patch("real_role_runner._create_probe_run_context", return_value=effective_host_context), mock.patch(
                "real_role_runner.run_real_role_sandbox_probe_outcome", return_value=effective_probe_outcome
            ), mock.patch(
                "real_role_runner.run_process_with_timeout", side_effect=fake_process
            ):
                report = run_real_role_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, task_path)
        return report, calls

    def _audit_result(self, verdict: str, blocked_reason: object) -> dict[str, object]:
        return {
            "verdict": verdict,
            "taskId": "REAL-PIPELINE-TEST-001",
            "summary": "audit summary",
            "findings": [],
            "filesReviewed": ["artifact.json"],
            "scopeViolations": [],
            "blockedReason": blocked_reason,
        }

    def _empty_property_paths(self, schema: dict[str, object], path: str = "$") -> list[str]:
        empty: list[str] = []
        properties = schema.get("properties")
        if isinstance(properties, dict):
            for name, property_schema in properties.items():
                property_path = f"{path}.properties.{name}"
                if property_schema == {}:
                    empty.append(property_path)
                if isinstance(property_schema, dict):
                    empty.extend(self._empty_property_paths(property_schema, property_path))
        items = schema.get("items")
        if isinstance(items, dict):
            empty.extend(self._empty_property_paths(items, f"{path}.items"))
        return empty

    def _invalid_audit_schema_jsonl(self) -> str:
        nested_error = {
            "type": "error",
            "error": {
                "type": "invalid_request_error",
                "code": "invalid_json_schema",
                "message": "Invalid schema for response_format 'codex_output_schema': In context=('properties', 'blockedReason'), schema must have a 'type' key.",
                "param": "text.format.schema",
            },
            "status": 400,
        }
        return "\n".join(
            [
                json.dumps({"type": "thread.started", "thread_id": "thread-1"}),
                json.dumps({"type": "turn.started"}),
                json.dumps({"type": "error", "message": json.dumps(nested_error)}),
                json.dumps({"type": "turn.failed", "error": {"message": json.dumps(nested_error)}}),
            ]
        )

    def _create_unit_workspace(self) -> Path:
        run_id = f"unit_{uuid.uuid4().hex}"
        isolated_root = ROOT / "CodexAutomation" / "runtime" / "unit_real_role_workspaces"
        workspace, errors = create_isolated_workspace(ROOT, run_id, isolated_root, TASK)
        self.assertFalse(errors)
        self.assertIsNotNone(workspace)
        return workspace

    def _run_mocked_probe(self, mode: str) -> dict[str, object]:
        def fake_process(
            command: list[str],
            cwd: Path,
            stdout_path: Path,
            stderr_path: Path,
            timeout_seconds: int,
            taskkill_timeout_seconds: int,
            post_kill_wait_seconds: int,
            status_callback: object | None = None,
        ) -> dict[str, object]:
            workspace = Path(cwd)
            probe = workspace / "sandbox_write_probe.txt"
            stdout = ""
            stderr = ""
            returncode = 0
            timed_out = False
            process_result: dict[str, object] = {
                "pid": 1234,
                "process_started": True,
                "timed_out": False,
                "exit_code": 0,
                "termination": {"attempted": False, "succeeded": False, "command": [], "exit_code": None, "stderr": "", "fallback_kill_attempted": False, "incomplete": False},
            }
            if mode == "start_error":
                return {"pid": None, "process_started": False, "process_start_error": "cannot start", "stdout": "", "stderr": "", "timed_out": False, "exit_code": None, "termination": process_result["termination"]}
            if status_callback is not None:
                status_callback("started", process_result)
            if mode == "success":
                probe.write_text("CODEX_SANDBOX_WRITE_OK", encoding="utf-8")
                probe.read_text(encoding="utf-8")
                probe.unlink()
                stdout = "PROBE_FILE_CREATED\nPROBE_FILE_READ_BACK\nPROBE_FILE_REMOVED\n"
            elif mode == "nonzero":
                returncode = 1
            elif mode == "missing_write":
                stdout = ""
            elif mode == "failed_readback":
                stdout = "PROBE_FILE_CREATED\nPROBE_FILE_REMOVED\n"
            elif mode == "leftover":
                probe.write_text("CODEX_SANDBOX_WRITE_OK", encoding="utf-8")
                stdout = "PROBE_FILE_CREATED\nPROBE_FILE_READ_BACK\nPROBE_FILE_REMOVED\n"
            elif mode == "extra":
                (workspace / "extra.txt").write_text("extra", encoding="utf-8")
                stdout = "PROBE_FILE_CREATED\nPROBE_FILE_READ_BACK\nPROBE_FILE_REMOVED\n"
            elif mode in {"timeout", "termination_incomplete"}:
                timed_out = True
                returncode = None
                process_result["timed_out"] = True
                process_result["exit_code"] = None
                process_result["termination"] = {
                    "attempted": True,
                    "succeeded": mode != "termination_incomplete",
                    "command": ["taskkill", "/PID", "1234", "/T", "/F"],
                    "exit_code": 0 if mode == "timeout" else None,
                    "stderr": "",
                    "fallback_kill_attempted": mode == "termination_incomplete",
                    "incomplete": mode == "termination_incomplete",
                }
                if status_callback is not None:
                    status_callback("timeout", process_result)
                    status_callback("taskkill_done", process_result)
                    if mode == "termination_incomplete":
                        status_callback("termination_incomplete", process_result)
            stdout_path.write_text(stdout, encoding="utf-8")
            stderr_path.write_text(stderr, encoding="utf-8")
            return {
                "pid": 1234,
                "process_started": True,
                "process_start_error": None,
                "stdout": stdout,
                "stderr": stderr,
                "timed_out": timed_out,
                "exit_code": returncode,
                "termination": process_result["termination"],
            }

        with mock.patch("real_role_runner._codex_version", return_value=("codex 0.0.0-test", None)), mock.patch(
            "real_role_runner._codex_sandbox_help", return_value="--permissions-profile --cd --config"
        ), mock.patch("real_role_runner._git_snapshot", return_value=[]), mock.patch("real_role_runner.run_process_with_file_logs_and_timeout", side_effect=fake_process):
            return run_real_role_sandbox_probe(ROOT, ROOT / "CodexAutomation", CONFIG, "unit")

    def _probe_report(self, verdict: str, error_code: str | None) -> dict[str, object]:
        run_id = f"probe_unit_{uuid.uuid4().hex}_{verdict.lower()}_{error_code or 'pass'}".replace("/", "_")
        report_path = f"CodexAutomation/runtime/real_role_sandbox_probes/{run_id}/SANDBOX_WRITE_PROBE_REPORT.json"
        workspace_path = f"CodexAutomation/runtime/real_role_sandbox_probes/{run_id}/workspace"
        return {
            "probeRunId": run_id,
            "startedAt": "2026-07-03T00:00:00+00:00",
            "finishedAt": "2026-07-03T00:00:01+00:00" if verdict != "RUNNING" else None,
            "durationSeconds": 1.0 if verdict != "RUNNING" else None,
            "windowsSandboxImplementation": "elevated",
            "permissionsProfile": ":workspace",
            "workspacePath": workspace_path,
            "reportPath": report_path,
            "finalVerdict": verdict,
            "errorCode": error_code,
            "errorMessage": None if error_code is None else "probe failed",
            "processStarted": verdict == "PASS",
            "processId": 1234 if verdict == "PASS" else None,
            "timedOut": error_code == "WINDOWS_SANDBOX_WRITE_PROBE_TIMEOUT",
            "exitCode": 0 if verdict == "PASS" else None,
            "probeFileCreated": verdict == "PASS",
            "probeFileReadBack": verdict == "PASS",
            "probeFileRemoved": verdict == "PASS",
            "unexpectedPaths": [],
            "parentGitChanged": False,
            "realCodexStarted": False,
            "unityStarted": False,
            "networkUsed": False,
            "reportWarnings": [],
        }

    def _probe_context(self, report: dict[str, object]) -> ProbeRunContext:
        probe_run_id = str(report["probeRunId"])
        configured_probe_root = ROOT / "CodexAutomation" / "runtime" / "real_role_sandbox_probes"
        run_directory = configured_probe_root / probe_run_id
        return ProbeRunContext(
            probe_run_id,
            configured_probe_root,
            run_directory,
            run_directory / "workspace",
            run_directory / "SANDBOX_WRITE_PROBE_REPORT.json",
            str(report["startedAt"]),
        )

    def _write_probe_disk_report(self, report: dict[str, object]) -> None:
        report_path = ROOT / str(report["reportPath"])
        report_path.parent.mkdir(parents=True, exist_ok=True)
        (report_path.parent / "workspace").mkdir(exist_ok=True)
        report_path.write_text(json.dumps(report), encoding="utf-8")

    def _implementation_result(self) -> dict[str, object]:
        return {
            "status": "implemented",
            "taskId": "REAL-PIPELINE-TEST-001",
            "summary": "implemented",
            "filesChanged": ["artifact.json"],
            "testsAttempted": [],
            "knownIssues": [],
        }

    def _write_artifact(self, workspace: Path, payload: dict[str, object]) -> None:
        (workspace / "artifact.json").write_text(json.dumps(payload), encoding="utf-8")

    def _write_pre_repair_artifact(self, workspace: Path) -> None:
        self._write_artifact(workspace, {"taskId": "REAL-PIPELINE-TEST-001", "value": 41, "repairApplied": False})

    def _write_final_artifact(self, workspace: Path) -> None:
        self._write_artifact(workspace, {"taskId": "REAL-PIPELINE-TEST-001", "value": 42, "repairApplied": True})

    def _assert_scope_violation(self, report: dict[str, object], calls: list[str], expected_path: str) -> None:
        self.assertEqual(report["finalState"], "FAILED")
        self.assertEqual(report["finalVerdict"], "FAILED")
        self.assertEqual(report["errorCode"], "REAL_TEST_UNEXPECTED_FILE")
        self.assertEqual(calls, ["implementer"])
        self.assertEqual(report["codexInvocationCount"], 1)
        self.assertEqual(report["repairExecutionAttempts"], [])
        self.assertEqual(report["auditExecutionAttempts"], [])
        self.assertEqual(report["stateHistory"], ["PENDING", "IMPLEMENTING", "VALIDATING", "FAILED"])
        self.assertIn(expected_path, report["validationAttempts"][0]["actualChangedPaths"])
        self.assertIn(expected_path, report["validationAttempts"][0]["unexpectedPaths"])


if __name__ == "__main__":
    unittest.main()
