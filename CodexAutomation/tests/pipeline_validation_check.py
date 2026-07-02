from __future__ import annotations

import json
import math
import stat
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
from pipeline_engine import run_pipeline_self_test  # noqa: E402
from pipeline_config import parse_pipeline_settings  # noqa: E402
import pipeline_validator  # noqa: E402
from pipeline_validator import (  # noqa: E402
    _is_symlink_or_reparse,
    resolve_workspace_path,
    run_validations,
    snapshot_error,
    snapshot_workspace,
    validate_pipeline_task,
    validate_workspace_relative_path_pattern,
)
from usage_limit_models import validate_usage_limit  # noqa: E402


TASK = ROOT / "CodexAutomation" / "tests" / "fixtures" / "PIPELINE-TEST-001.json"
CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class PipelineValidationCheck(unittest.TestCase):
    def test_invalid_task_schema(self) -> None:
        task = read_json(TASK)
        task.pop("title")
        errors = validate_pipeline_task(task, ROOT / "CodexAutomation" / "schemas" / "pipeline_task.schema.json")
        self.assertTrue(errors)

    def test_real_write_mode_blocked(self) -> None:
        task = read_json(TASK)
        task["mode"] = "project_write"
        errors = validate_pipeline_task(task, ROOT / "CodexAutomation" / "schemas" / "pipeline_task.schema.json")
        self.assertTrue(any("REAL_WORKSPACE_WRITE_DISABLED" in error for error in errors))

    def test_scope_violation_unexpected_file(self) -> None:
        task = read_json(TASK)
        task["validations"].append({"type": "forbidden_file", "code": "FORBIDDEN_EXTRA", "file": "extra.txt"})
        temp_task = ROOT / "CodexAutomation" / "runtime" / "results" / "invalid_scope_task.json"
        temp_task.parent.mkdir(parents=True, exist_ok=True)
        temp_task.write_text(json.dumps(task), encoding="utf-8")
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, temp_task)
        self.assertIn("artifact.json", report["actualChangedPaths"])

    def test_max_repair_attempts(self) -> None:
        config = json.loads(json.dumps(CONFIG))
        config["pipeline"]["maxRepairAttemptsDefault"] = 0
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", config, TASK)
        self.assertEqual(report["finalState"], "FAILED_MAX_REPAIRS")
        self.assertEqual(report["errorCode"], "FAILED_MAX_REPAIRS")

    def test_invocation_budget_guard(self) -> None:
        config = json.loads(json.dumps(CONFIG))
        config["pipeline"]["maxCodexInvocationsPerPipelineRun"] = 1
        config["pipeline"]["reserveInvocationsForFinalAudit"] = 0
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", config, TASK)
        self.assertEqual(report["finalState"], "FAILED_INVOCATION_BUDGET")

    def test_reserve_invocations_for_final_audit_guard(self) -> None:
        config = json.loads(json.dumps(CONFIG))
        config["pipeline"]["maxCodexInvocationsPerPipelineRun"] = 3
        config["pipeline"]["reserveInvocationsForFinalAudit"] = 2
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", config, TASK)
        self.assertEqual(report["finalState"], "FAILED_INVOCATION_BUDGET")

    def test_final_audit_allowed_when_remaining_equals_reserve(self) -> None:
        config = json.loads(json.dumps(CONFIG))
        config["pipeline"]["maxCodexInvocationsPerPipelineRun"] = 3
        config["pipeline"]["reserveInvocationsForFinalAudit"] = 2
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", config, TASK, "already_valid")
        self.assertEqual(report["finalState"], "COMPLETED")
        self.assertEqual(report["auditInvocationCount"], 1)

    def test_audit_count_limit_applies(self) -> None:
        config = json.loads(json.dumps(CONFIG))
        config["pipeline"]["maxAuditsPerTask"] = 1
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", config, TASK, "audit_always_fix_required")
        self.assertEqual(report["errorCode"], "FAILED_MAX_AUDITS")
        self.assertEqual(report["auditInvocationCount"], 1)

    def test_rate_limited_audit_counts_as_invocation(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "rate_limit_auditor")
        self.assertEqual(report["rateLimitPauses"][0]["pausedRole"], "auditor")
        self.assertGreaterEqual(report["auditInvocationCount"], 2)
        self.assertGreaterEqual(report["remainingInvocationBudget"], 0)

    def test_task_cannot_raise_global_limit(self) -> None:
        task = read_json(TASK)
        task["maxCodexInvocationsPerPipelineRun"] = 999
        errors = validate_pipeline_task(task, ROOT / "CodexAutomation" / "schemas" / "pipeline_task.schema.json")
        self.assertTrue(errors)

    def test_safe_path_resolver_blocks_unsafe_paths(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            workspace = Path(temp)
            outside = workspace.parent / "outside.json"
            outside.write_text("{}", encoding="utf-8")
            unsafe_paths = [
                "../outside.json",
                "sub/../../outside.json",
                "C:\\outside.json",
                "\\\\server\\share\\outside.json",
                "/root/outside.json",
            ]
            for value in unsafe_paths:
                resolved = resolve_workspace_path(workspace, value)
                self.assertIsNotNone(resolved["errorCode"], value)
            inside = resolve_workspace_path(workspace, "sub/file.json")
            self.assertIsNone(inside["errorCode"])
            self.assertEqual(inside["path"], workspace.resolve() / "sub" / "file.json")

    def test_path_array_patterns_are_safety_checked(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            workspace = Path(temp)
            unsafe = [
                "../outside.json",
                "sub/../../outside.json",
                "C:\\outside.json",
                "C:outside.json",
                "\\\\server\\share\\file.json",
                "/outside.json",
                "sub\\..\\..\\outside.json",
                "",
                "   ",
                123,
                "../**/*.json",
            ]
            for value in unsafe:
                result = validate_workspace_relative_path_pattern(workspace, value)
                self.assertIsNotNone(result["errorCode"], value)
            for value in ["artifact.json", "sub/artifact.json", "*.json", "**/*.json"]:
                result = validate_workspace_relative_path_pattern(workspace, value)
                self.assertIsNone(result["errorCode"], value)

    def test_path_array_validation_blocks_unsafe_entries(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            workspace = Path(temp)
            before = snapshot_workspace(workspace)
            task = read_json(TASK)
            for validator_type in ("exact_allowed_changed_paths", "forbidden_changed_paths", "no_unexpected_files"):
                task["validations"] = [{"type": validator_type, "code": "UNSAFE_PATH_ARRAY", "paths": ["../outside.json"]}]
                result = run_validations(task, workspace, before, snapshot_workspace(workspace))
                self.assertEqual(result["verdict"], "BLOCKED")
                self.assertEqual(result["results"][0]["actual"], "UNSAFE_PARENT_TRAVERSAL")

    def test_workspace_root_snapshot_errors(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            base = Path(temp)
            missing = base / "missing"
            self.assertEqual(snapshot_error(snapshot_workspace(missing))["errorCode"], "WORKSPACE_ROOT_NOT_FOUND")
            root_file = base / "workspace_file"
            root_file.write_text("not a directory", encoding="utf-8")
            self.assertEqual(snapshot_error(snapshot_workspace(root_file))["errorCode"], "WORKSPACE_ROOT_NOT_DIRECTORY")
            normal = base / "workspace"
            normal.mkdir()
            self.assertIsNone(snapshot_error(snapshot_workspace(normal)))

    def test_mock_reparse_attribute_detection(self) -> None:
        path = Path("mocked")
        reparse_stat = SimpleNamespace(st_file_attributes=getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))
        normal_stat = SimpleNamespace(st_file_attributes=0)
        with mock.patch.object(Path, "is_symlink", return_value=False), mock.patch.object(Path, "stat", return_value=reparse_stat):
            self.assertTrue(_is_symlink_or_reparse(path))
        with mock.patch.object(Path, "is_symlink", return_value=False), mock.patch.object(Path, "stat", return_value=normal_stat):
            self.assertFalse(_is_symlink_or_reparse(path))

    def test_mock_reparse_root_and_component_are_blocked(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            workspace = Path(temp)
            with mock.patch.object(pipeline_validator, "_is_symlink_or_reparse", return_value=True):
                self.assertEqual(snapshot_error(snapshot_workspace(workspace))["errorCode"], "UNSAFE_SYMLINK_OR_REPARSE_POINT")
                self.assertEqual(resolve_workspace_path(workspace, "artifact.json")["errorCode"], "UNSAFE_SYMLINK_OR_REPARSE_POINT")

    def test_symlink_paths_are_blocked_when_supported(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            workspace = Path(temp)
            outside = workspace.parent / "outside.json"
            outside.write_text("{}", encoding="utf-8")
            link = workspace / "outside_link.json"
            try:
                link.symlink_to(outside)
            except OSError:
                self.skipTest("Symlink creation is not available in this Windows session.")
            resolved = resolve_workspace_path(workspace, "outside_link.json")
            self.assertEqual(resolved["errorCode"], "UNSAFE_SYMLINK_OR_REPARSE_POINT")

    def test_symlink_directory_paths_are_blocked_when_supported(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            workspace = Path(temp)
            outside_dir = workspace.parent / "outside_dir"
            outside_dir.mkdir(exist_ok=True)
            link = workspace / "outside_dir_link"
            try:
                link.symlink_to(outside_dir, target_is_directory=True)
            except OSError:
                self.skipTest("Directory symlink creation is not available in this Windows session.")
            resolved = resolve_workspace_path(workspace, "outside_dir_link/file.json")
            self.assertEqual(resolved["errorCode"], "UNSAFE_SYMLINK_OR_REPARSE_POINT")

    def test_usage_limit_timestamp_validation(self) -> None:
        valid = {
            "limitType": "five_hour_window",
            "detectedAtUtc": "2026-07-02T14:00:00Z",
            "resetAtUtc": "2026-07-02T15:00:00+00:00",
            "retryAfterSeconds": 0,
            "message": "limit",
            "source": "test",
        }
        self.assertFalse(validate_usage_limit(valid))
        invalid_values = [
            {"detectedAtUtc": "2026-07-02T14:00:00"},
            {"detectedAtUtc": "2026-07-02T14:00:00+02:00"},
            {"detectedAtUtc": "not a time"},
            {"resetAtUtc": "2026-07-02T13:00:00Z"},
            {"retryAfterSeconds": -1},
            {"retryAfterSeconds": True},
            {"retryAfterSeconds": math.nan},
            {"retryAfterSeconds": math.inf},
            {"message": "   "},
            {"source": "   "},
            {"limitType": "monthly"},
        ]
        for patch in invalid_values:
            data = dict(valid)
            data.update(patch)
            self.assertTrue(validate_usage_limit(data), patch)

    def test_invalid_config_values_are_rejected(self) -> None:
        cases = [
            ("allowRealWorkspaceWrite", "false"),
            ("pipelineSelfTestEnabled", "true"),
            ("allowRealWorkspaceWrite", 0),
            ("maxCodexInvocationsPerPipelineRun", True),
            ("maxCodexInvocationsPerPipelineRun", -1),
        ]
        for field, value in cases:
            config = json.loads(json.dumps(CONFIG))
            config["pipeline"][field] = value
            settings, errors = parse_pipeline_settings(config)
            self.assertIsNone(settings)
            self.assertTrue(errors)
        config = json.loads(json.dumps(CONFIG))
        config["pipeline"]["reserveInvocationsForFinalAudit"] = config["pipeline"]["maxCodexInvocationsPerPipelineRun"]
        self.assertTrue(parse_pipeline_settings(config)[1])

    def test_invalid_critical_config_field_missing(self) -> None:
        config = json.loads(json.dumps(CONFIG))
        config["usageLimits"].pop("allowAutomaticCreditUsage")
        self.assertTrue(parse_pipeline_settings(config)[1])


if __name__ == "__main__":
    unittest.main()
