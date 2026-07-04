from __future__ import annotations

import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from typing import Any
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
import task_manifest_validator  # noqa: E402
import orchestrator  # noqa: E402
import preflight  # noqa: E402
from task_manifest_validator import (  # noqa: E402
    inspect_source_paths,
    parse_real_task_policy,
    validate_real_task_plan_report,
    validate_manifest_path,
    validate_task_manifest_file,
)
from schema_validator import validate as validate_schema_instance  # noqa: E402


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")
FIXTURE_ROOT = ROOT / "CodexAutomation" / "tests" / "fixtures" / "real_tasks"
VALID = FIXTURE_ROOT / "valid_minimal.json"


def _completed(command: list[str], returncode: int = 0, stdout: str = "", stderr: str = "") -> subprocess.CompletedProcess[str]:
    return subprocess.CompletedProcess(command, returncode, stdout, stderr)


class RealTaskManifestCheck(unittest.TestCase):
    def test_config_valid_and_exact_policy(self) -> None:
        policy, errors = parse_real_task_policy(CONFIG)
        self.assertFalse(errors)
        self.assertIsNotNone(policy)
        self.assertFalse(policy.allowExecution)
        self.assertFalse(policy.allowParentProjectWrite)
        self.assertFalse(policy.allowAutomaticApply)
        self.assertFalse(policy.allowNetwork)
        self.assertFalse(policy.allowPackageInstall)
        self.assertFalse(policy.allowUnityLaunch)
        self.assertFalse(policy.allowAutomaticRetry)
        self.assertFalse(policy.allowAutomaticModelDowngrade)
        self.assertFalse(policy.allowAutomaticCreditUsage)
        self.assertTrue(policy.allowDirtyParentWorktree)
        self.assertFalse(policy.allowBinaryOutputs)
        self.assertEqual(policy.maxInvocations, 3)
        self.assertEqual(policy.maxRepairAttempts, 1)

    def test_config_rejects_missing_unknown_string_bool_bool_int_dangerous_and_overlap(self) -> None:
        cases = []
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"].pop("allowExecution")
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"]["unknown"] = False
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"]["allowExecution"] = "false"
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"]["maxChangedFiles"] = True
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"]["allowNetwork"] = True
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"]["maxInvocations"] = 2
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"]["maxRepairAttempts"] = 2
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTasks"]["forbiddenSourceRoots"].append("Assets")
        cases.append(config)
        for item in cases:
            with self.subTest(item=item["realTasks"]):
                policy, errors = parse_real_task_policy(item)
                self.assertIsNone(policy)
                self.assertTrue(any(error.code == "REAL_TASK_CONFIG_INVALID" for error in errors))

    def test_valid_minimal_manifest_accepted(self) -> None:
        result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID)
        self.assertTrue(result.ok, result.error_dicts())
        self.assertIsNotNone(result.manifestSha256)
        self.assertIsNotNone(result.effectivePlan)

    def test_manifest_json_schema_and_scalar_errors(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            bad_json = temp_path / "bad.json"
            bad_json.write_text("{bad", encoding="utf-8")
            self.assertFalse(validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, bad_json).ok)
            non_object = temp_path / "non_object.json"
            non_object.write_text("[]", encoding="utf-8")
            self.assertFalse(validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, non_object).ok)
            base = read_json(VALID)
            cases = [
                ("unknown top", {"extra": True}),
                ("missing title", {"__pop__": "title"}),
                ("schema bool", {"schemaVersion": True}),
                ("schema wrong", {"schemaVersion": 2}),
                ("task bad", {"taskId": "-bad"}),
                ("title empty", {"title": ""}),
                ("objective empty", {"objective": ""}),
                ("objective long", {"objective": "x" * 8001}),
                ("task type", {"taskType": "other"}),
            ]
            for name, patch in cases:
                data = json.loads(json.dumps(base))
                if "__pop__" in patch:
                    data.pop(str(patch["__pop__"]))
                else:
                    data.update(patch)
                path = temp_path / f"{name.replace(' ', '_')}.json"
                path.write_text(json.dumps(data), encoding="utf-8")
                with self.subTest(name=name):
                    result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, path)
                    self.assertFalse(result.ok)

    def test_path_safety_rejections(self) -> None:
        policy = self._policy()
        cases = [
            "C:/outside.txt",
            "//server/share/file.txt",
            "C:outside.txt",
            "Assets\\File.cs",
            "Assets/file.txt:ads",
            "Assets/../ProjectSettings/ProjectVersion.txt",
            "Assets/./File.cs",
            "Assets/*.cs",
            "Assets/File.",
            "Assets/File ",
            "Assets/CON.txt",
            "CodexAutomation/config.json",
            "Library/cache.txt",
        ]
        for path in cases:
            with self.subTest(path=path):
                self.assertIsNotNone(validate_manifest_path(path, policy, source_path=True))
        outside = validate_manifest_path("README.md", policy, source_path=True)
        self.assertIsNotNone(outside)
        self.assertEqual(outside.code, "REAL_TASK_SOURCE_OUTSIDE_ALLOWED_ROOT")

    def test_manifest_scope_errors_for_duplicates_service_expected_output_and_delete(self) -> None:
        base = read_json(VALID)
        cases = [
            ("duplicate", {"sourcePaths": ["ProjectSettings/ProjectVersion.txt", "ProjectSettings/ProjectVersion.txt"]}),
            ("service write", {"allowedWritePaths": ["AGENTS.md"]}),
            ("output outside", {"expectedOutputs": [{"path": "Assets/Other.txt", "kind": "file", "required": True}]}),
            ("delete outside", {"allowedDeletePaths": ["Assets/Other.txt"]}),
            ("delete ancestor", {"sourcePaths": ["ProjectSettings/ProjectVersion.txt"], "allowedWritePaths": ["ProjectSettings/ProjectVersion.txt"], "allowedDeletePaths": ["ProjectSettings"]}),
        ]
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            for name, patch in cases:
                data = json.loads(json.dumps(base))
                data.update(patch)
                path = Path(temp) / f"{name}.json"
                path.write_text(json.dumps(data), encoding="utf-8")
                with self.subTest(name=name):
                    self.assertFalse(validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, path).ok)

    def test_limits_lowering_allowed_and_raising_bool_single_gt_total_rejected(self) -> None:
        base = read_json(VALID)
        cases = [
            ("lower", {"limits": {"maxChangedFiles": 1, "maxChangedBytes": 100, "maxSingleChangedFileBytes": 50}}, True),
            ("raise", {"limits": {"maxChangedFiles": 51}}, False),
            ("bool", {"limits": {"maxChangedFiles": True}}, False),
            ("single greater", {"limits": {"maxChangedBytes": 10, "maxSingleChangedFileBytes": 20}}, False),
        ]
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            for name, patch, ok in cases:
                data = json.loads(json.dumps(base))
                data.update(patch)
                path = Path(temp) / f"{name}.json"
                path.write_text(json.dumps(data), encoding="utf-8")
                with self.subTest(name=name):
                    self.assertEqual(validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, path).ok, ok)

    def test_metadata_and_completion_limits(self) -> None:
        base = read_json(VALID)
        cases = [
            ("metadata", {"metadata": {"x": "y" * (17 * 1024)}}),
            ("criteria count", {"completionCriteria": ["x"] * 31}),
            ("criteria long", {"completionCriteria": ["x" * 1001]}),
        ]
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            for name, patch in cases:
                data = json.loads(json.dumps(base))
                data.update(patch)
                path = Path(temp) / f"{name}.json"
                path.write_text(json.dumps(data), encoding="utf-8")
                with self.subTest(name=name):
                    self.assertFalse(validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, path).ok)

    def test_all_supported_validators_accept_valid_payloads(self) -> None:
        validators = [
            {"id": "file-exists", "type": "file_exists", "path": "ProjectSettings/ProjectVersion.txt"},
            {"id": "file-absent", "type": "file_absent", "path": "ProjectSettings/ProjectVersion.txt"},
            {"id": "json-valid", "type": "json_valid", "path": "ProjectSettings/ProjectVersion.txt"},
            {"id": "json-schema", "type": "json_schema", "path": "ProjectSettings/ProjectVersion.txt", "schemaName": "project_version"},
            {"id": "json-field", "type": "json_field_equals", "path": "ProjectSettings/ProjectVersion.txt", "jsonPointer": "/x", "expected": 1},
            {"id": "text-contains", "type": "text_contains", "path": "ProjectSettings/ProjectVersion.txt", "text": "m_EditorVersion"},
            {"id": "text-not", "type": "text_not_contains", "path": "ProjectSettings/ProjectVersion.txt", "text": "conflict"},
            {"id": "changed-exact", "type": "changed_paths_exact", "paths": ["ProjectSettings/ProjectVersion.txt"]},
            {"id": "changed-subset", "type": "changed_paths_subset", "paths": ["ProjectSettings/ProjectVersion.txt"]},
            {"id": "no-unexpected", "type": "no_unexpected_files"},
            {"id": "no-conflicts", "type": "no_conflict_markers", "paths": ["ProjectSettings/ProjectVersion.txt"]},
            {"id": "max-size", "type": "max_file_size", "path": "ProjectSettings/ProjectVersion.txt", "maxBytes": 100},
            {"id": "max-files", "type": "max_changed_files", "maxFiles": 1},
            {"id": "extensions", "type": "extension_allowlist", "extensions": [".txt"]},
        ]
        data = read_json(VALID)
        data["limits"] = {"maxChangedFiles": 1, "maxChangedBytes": 1000, "maxSingleChangedFileBytes": 100}
        data["validationPlan"] = validators
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            path = Path(temp) / "validators.json"
            path.write_text(json.dumps(data), encoding="utf-8")
            self.assertTrue(validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, path).ok)

    def test_validator_rejections(self) -> None:
        base = read_json(VALID)
        cases = [
            ("unsupported", [{"id": "x", "type": "command"}], "REAL_TASK_VALIDATOR_UNSUPPORTED"),
            ("duplicate id", [{"id": "x", "type": "no_unexpected_files"}, {"id": "x", "type": "no_unexpected_files"}], "REAL_TASK_VALIDATOR_INVALID"),
            ("missing param", [{"id": "x", "type": "file_exists"}], "REAL_TASK_VALIDATOR_INVALID"),
            ("forbidden param", [{"id": "x", "type": "no_unexpected_files", "command": "x"}], "REAL_TASK_VALIDATOR_INVALID"),
            ("schema path", [{"id": "x", "type": "json_schema", "path": "ProjectSettings/ProjectVersion.txt", "schemaName": "../schema.json"}], "REAL_TASK_VALIDATOR_INVALID"),
            ("outside scope", [{"id": "x", "type": "file_exists", "path": "Assets/Other.txt"}], "REAL_TASK_VALIDATOR_INVALID"),
            ("numeric cap", [{"id": "x", "type": "max_changed_files", "maxFiles": 51}], "REAL_TASK_LIMIT_INVALID"),
            ("binary extension", [{"id": "x", "type": "extension_allowlist", "extensions": [".png"]}], "REAL_TASK_POLICY_VIOLATION"),
        ]
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            for name, validators, expected in cases:
                data = json.loads(json.dumps(base))
                data["validationPlan"] = validators
                path = Path(temp) / f"{name}.json"
                path.write_text(json.dumps(data), encoding="utf-8")
                result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, path)
                with self.subTest(name=name):
                    self.assertFalse(result.ok)
                    self.assertTrue(any(error.code == expected for error in result.errors), result.error_dicts())

    def test_effective_policy_host_values_cannot_be_overridden(self) -> None:
        data = read_json(VALID)
        data.update(
            {
                "networkPolicy": {"enabled": True},
                "unityPolicy": {"launch": True},
                "gitPolicy": {"parentWrite": True},
                "roleBudget": {"maxInvocations": 99},
                "repairPolicy": {"maxAttempts": 2},
                "applyPolicy": {"automatic": True},
            }
        )
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            path = Path(temp) / "override.json"
            path.write_text(json.dumps(data), encoding="utf-8")
            result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, path)
        self.assertFalse(result.ok)
        plan = result.effectivePlan
        self.assertIsNotNone(plan)
        policy = plan["effectivePolicy"]
        self.assertFalse(policy["network"])
        self.assertFalse(policy["unityLaunch"])
        self.assertFalse(policy["parentWrite"])
        self.assertEqual(policy["effectiveMaxInvocations"], 3)
        self.assertEqual(policy["effectiveMaxRepairAttempts"], 1)
        self.assertTrue(policy["auditorRequired"])
        self.assertFalse(policy["binaryOutputs"])
        self.assertFalse(policy["automaticApply"])

    def test_plan_mode_valid_existing_source_and_report_flags(self) -> None:
        result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID, inspect_sources=True)
        self.assertTrue(result.ok, result.error_dicts())
        plan = result.effectivePlan
        self.assertEqual(plan["finalVerdict"], "PASS")
        self.assertEqual(plan["codexInvocationCount"], 0)
        self.assertFalse(plan["modelInvocationStarted"])
        self.assertFalse(plan["sandboxStarted"])
        self.assertFalse(plan["workspaceCreated"])
        self.assertFalse(plan["sourceCopied"])
        self.assertFalse(plan["unityStarted"])
        self.assertFalse(plan["networkUsed"])
        self.assertIn("parentGitSnapshot", plan)
        self.assertIn("dirtyParentWorktree", plan)

    def test_plan_mode_source_missing_symlink_reparse_and_limits(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_root = Path(temp)
            (temp_root / "Assets").mkdir()
            source = temp_root / "Assets" / "file.txt"
            source.write_text("x", encoding="utf-8")
            policy = self._policy()
            missing = inspect_source_paths(temp_root, ["Assets/missing.txt"], policy)[1]
            self.assertTrue(any(error.code == "REAL_TASK_SOURCE_MISSING" for error in missing))
            link = temp_root / "Assets" / "link.txt"
            try:
                link.symlink_to(source)
                link_errors = inspect_source_paths(temp_root, ["Assets/link.txt"], policy)[1]
                self.assertTrue(any(error.code == "REAL_TASK_SOURCE_REPARSE" for error in link_errors))
            except OSError:
                pass
            with mock.patch("task_manifest_validator._is_symlink_or_reparse", return_value=True):
                reparse_errors = inspect_source_paths(temp_root, ["Assets/file.txt"], policy)[1]
                self.assertTrue(any(error.code == "REAL_TASK_SOURCE_REPARSE" for error in reparse_errors))
            config = json.loads(json.dumps(CONFIG))
            config["realTasks"]["maxSourceFiles"] = 1
            config["realTasks"]["maxSourceBytes"] = 1
            policy_small, policy_errors = parse_real_task_policy(config)
            self.assertFalse(policy_errors)
            (temp_root / "Assets" / "second.txt").write_text("xx", encoding="utf-8")
            limit_errors = inspect_source_paths(temp_root, ["Assets"], policy_small)[1]
            self.assertTrue(any(error.code == "REAL_TASK_SOURCE_LIMIT_EXCEEDED" for error in limit_errors))

    def test_source_paths_reject_duplicates_and_overlaps_but_not_prefix_collisions(self) -> None:
        base = read_json(VALID)
        cases = [
            ("duplicate", ["Assets/Foo/Bar.txt", "Assets/Foo/Bar.txt"], False, "REAL_TASK_SOURCE_OVERLAP"),
            ("parent_child", ["Assets/Foo", "Assets/Foo/Bar.txt"], False, "REAL_TASK_SOURCE_OVERLAP"),
            ("child_parent", ["Assets/Foo/Bar.txt", "Assets/Foo"], False, "REAL_TASK_SOURCE_OVERLAP"),
            ("prefix_collision", ["Assets/Foo", "Assets/Foobar"], True, None),
        ]
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_root = Path(temp)
            (temp_root / "Assets" / "Foo").mkdir(parents=True)
            (temp_root / "Assets" / "Foobar").mkdir(parents=True)
            (temp_root / "Assets" / "Foo" / "Bar.txt").write_text("x", encoding="utf-8")
            for name, source_paths, ok, code in cases:
                data = json.loads(json.dumps(base))
                data["sourcePaths"] = source_paths
                data["allowedWritePaths"] = ["Assets/Foo/Bar.txt"]
                data["expectedOutputs"] = [{"path": "Assets/Foo/Bar.txt", "kind": "file", "required": True}]
                data["validationPlan"] = [{"id": "changed", "type": "changed_paths_exact", "paths": ["Assets/Foo/Bar.txt"]}]
                path = temp_root / f"{name}.json"
                path.write_text(json.dumps(data), encoding="utf-8")
                result = validate_task_manifest_file(temp_root, ROOT / "CodexAutomation", CONFIG, path)
                with self.subTest(name=name):
                    self.assertEqual(result.ok, ok, result.error_dicts())
                    if code:
                        self.assertTrue(any(error.code == code for error in result.errors), result.error_dicts())

    def test_inventory_deduplicates_seen_files_defensively(self) -> None:
        policy = self._policy()
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_root = Path(temp)
            source = temp_root / "Assets" / "Foo"
            source.mkdir(parents=True)
            (source / "Bar.txt").write_text("abc", encoding="utf-8")
            inspections, errors = inspect_source_paths(temp_root, ["Assets/Foo", "Assets/Foo/Bar.txt"], policy)
        self.assertFalse(errors, [error.to_dict() for error in errors])
        self.assertEqual(sum(item["fileCount"] for item in inspections), 1)
        self.assertEqual(sum(item["totalBytes"] for item in inspections), 3)

    def test_inventory_fail_closed_for_stat_scandir_disappearing_and_special_files(self) -> None:
        policy = self._policy()
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_root = Path(temp)
            source_dir = temp_root / "Assets"
            source_dir.mkdir()
            source = source_dir / "file.txt"
            source.write_text("abc", encoding="utf-8")

            for error in (PermissionError("no stat"), OSError("stat failed")):
                with self.subTest(error=type(error).__name__):
                    with mock.patch("task_manifest_validator._lstat_path", return_value=(None, f"Source entry could not be inspected: {type(error).__name__}.")):
                        errors = inspect_source_paths(temp_root, ["Assets/file.txt"], policy)[1]
                    self.assertTrue(any(item.code == "REAL_TASK_SOURCE_INVENTORY_FAILED" for item in errors), [item.to_dict() for item in errors])

            for error in (PermissionError("no scandir"), OSError("walk failed")):
                with self.subTest(scandir=type(error).__name__):
                    with mock.patch("task_manifest_validator.os.scandir", side_effect=error):
                        errors = inspect_source_paths(temp_root, ["Assets"], policy)[1]
                    self.assertTrue(any(item.code == "REAL_TASK_SOURCE_INVENTORY_FAILED" for item in errors), [item.to_dict() for item in errors])

            with mock.patch("task_manifest_validator._inspect_regular_file", return_value={"path": "Assets", "type": "file", "fileCount": 0, "totalBytes": 0, "errorCode": "REAL_TASK_SOURCE_INVENTORY_FAILED", "errorMessage": "Source entry could not be inspected: FileNotFoundError."}):
                disappearing = inspect_source_paths(temp_root, ["Assets"], policy)[1]
            self.assertTrue(any(item.code == "REAL_TASK_SOURCE_INVENTORY_FAILED" for item in disappearing), [item.to_dict() for item in disappearing])

            fake_special = SimpleNamespace(st_mode=stat.S_IFIFO, st_size=0, st_file_attributes=0)
            with mock.patch("task_manifest_validator._lstat_path", return_value=(fake_special, None)):
                special = inspect_source_paths(temp_root, ["Assets/file.txt"], policy)[1]
            self.assertTrue(any(item.code == "REAL_TASK_SOURCE_INVENTORY_FAILED" for item in special), [item.to_dict() for item in special])

    def test_failed_inventory_plan_cannot_pass_and_keeps_no_model_flags(self) -> None:
        with mock.patch("task_manifest_validator._lstat_path", return_value=(None, "Source entry could not be inspected: PermissionError.")):
            result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID, inspect_sources=True)
        plan = result.effectivePlan
        self.assertFalse(result.ok)
        self.assertEqual(plan["finalVerdict"], "FAILED")
        self.assertEqual(plan["errorCode"], "REAL_TASK_SOURCE_INVENTORY_FAILED")
        self.assertEqual(plan["codexInvocationCount"], 0)
        self.assertFalse(plan["workspaceCreated"])
        self.assertFalse(plan["sourceCopied"])
        self.assertFalse(plan["unityStarted"])

    def test_mocked_windows_reparse_attribute_is_rejected(self) -> None:
        policy = self._policy()
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_root = Path(temp)
            (temp_root / "Assets").mkdir()
            (temp_root / "Assets" / "file.txt").write_text("x", encoding="utf-8")
            reparse_stat = SimpleNamespace(st_file_attributes=getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))
            with mock.patch.object(Path, "is_symlink", return_value=False), mock.patch.object(Path, "stat", return_value=reparse_stat):
                errors = inspect_source_paths(temp_root, ["Assets/file.txt"], policy)[1]
            self.assertTrue(any(error.code == "REAL_TASK_SOURCE_REPARSE" for error in errors))

    def test_cli_validate_and_plan_no_model_modes(self) -> None:
        valid = subprocess.run([sys.executable, "CodexAutomation/scripts/orchestrator.py", "--validate-task-manifest", str(VALID)], cwd=str(ROOT), capture_output=True, text=True, check=False)
        self.assertEqual(valid.returncode, 0, valid.stdout + valid.stderr)
        invalid = subprocess.run(
            [sys.executable, "CodexAutomation/scripts/orchestrator.py", "--validate-task-manifest", str(FIXTURE_ROOT / "invalid_absolute_path.json")],
            cwd=str(ROOT),
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertNotEqual(invalid.returncode, 0)
        plan = subprocess.run([sys.executable, "CodexAutomation/scripts/orchestrator.py", "--real-task-plan", str(VALID)], cwd=str(ROOT), capture_output=True, text=True, check=False)
        self.assertEqual(plan.returncode, 0, plan.stdout + plan.stderr)
        self.assertIn("Model invocation started: False", plan.stdout)
        self.assertIn("Workspace created: False", plan.stdout)
        self.assertIn("Source copied: False", plan.stdout)

    def test_no_codex_preflight_profile_skips_codex_command_for_new_modes(self) -> None:
        commands: list[list[str]] = []

        def fake_run(command: list[str], cwd: Path, timeout_seconds: int = 10):
            commands.append(command)
            if command[:2] == ["git", "status"]:
                return _completed(command, 0, "")
            if command[:2] == ["git", "branch"]:
                return _completed(command, 0, "codex/automation-bootstrap\n")
            if command[:2] == ["git", "rev-parse"]:
                return _completed(command, 0, "true\n")
            if command and command[0] == "powershell":
                return _completed(command, 0, "")
            return _completed(command, 0, "ok\n")

        for mode in ("--validate-task-manifest", "--real-task-plan"):
            commands.clear()
            with mock.patch("preflight.run_command", side_effect=fake_run), mock.patch.object(sys, "argv", ["orchestrator.py", mode, str(VALID)]):
                code = orchestrator.main()
            self.assertEqual(code, 0)
            flattened = " ".join(" ".join(command).lower() for command in commands)
            self.assertNotIn("codex", flattened)

    def test_default_preflight_still_inspects_codex(self) -> None:
        inspected: list[str] = []

        def fake_inspect(name: str, commands: list[list[str]], cwd: Path):
            inspected.append(name)
            return SimpleNamespace(name=name, command=commands[0], found=True, version="ok", path=str(commands[0][0]), error=None)

        with mock.patch("preflight.inspect_command", side_effect=fake_inspect):
            report = preflight.run_preflight(self._temp_context(), inspect_codex=True)
        self.assertIn("codex", inspected)
        self.assertTrue(any(tool["name"] == "codex" and tool["version"] == "ok" for tool in report["tools"]))

    def test_no_codex_profile_does_not_call_codex_inspection(self) -> None:
        inspected: list[str] = []

        def fake_inspect(name: str, commands: list[list[str]], cwd: Path):
            inspected.append(name)
            return SimpleNamespace(name=name, command=commands[0], found=True, version="ok", path=str(commands[0][0]), error=None)

        with mock.patch("preflight.inspect_command", side_effect=fake_inspect):
            report = preflight.run_preflight(self._temp_context(), inspect_codex=False)
        self.assertNotIn("codex", inspected)
        codex_tool = next(tool for tool in report["tools"] if tool["name"] == "codex")
        self.assertEqual(codex_tool["error"], "not_inspected")

    def test_git_snapshot_success_dirty_staged_detached_and_failures(self) -> None:
        def run_with(outputs: dict[tuple[str, ...], object]):
            def fake_run(command, cwd, capture_output, text, check, timeout):
                value = outputs.get(tuple(command), _completed(command, 0, ""))
                if isinstance(value, BaseException):
                    raise value
                return value

            with mock.patch("task_manifest_validator.subprocess.run", side_effect=fake_run) as run_mock:
                result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID, inspect_sources=True)
            self.assertTrue(all(call.kwargs.get("shell") is None for call in run_mock.mock_calls if call.kwargs))
            return result

        base_outputs = {
            ("git", "rev-parse", "--is-inside-work-tree"): _completed(["git"], 0, "true\n"),
            ("git", "status", "--porcelain=v1", "--untracked-files=all"): _completed(["git"], 0, ""),
            ("git", "branch", "--show-current"): _completed(["git"], 0, "main\n"),
            ("git", "rev-parse", "HEAD"): _completed(["git"], 0, "abc123\n"),
            ("git", "diff", "--cached", "--name-only"): _completed(["git"], 0, ""),
        }
        clean = run_with(base_outputs)
        self.assertTrue(clean.ok, clean.error_dicts())
        self.assertEqual(clean.effectivePlan["parentGitSnapshot"]["status"], "success")
        self.assertFalse(clean.effectivePlan["parentGitSnapshot"]["dirty"])

        dirty_outputs = dict(base_outputs)
        dirty_outputs[("git", "status", "--porcelain=v1", "--untracked-files=all")] = _completed(["git"], 0, " M CodexAutomation/config.json\n")
        dirty = run_with(dirty_outputs)
        self.assertTrue(dirty.effectivePlan["parentGitSnapshot"]["dirty"])

        staged_outputs = dict(base_outputs)
        staged_outputs[("git", "diff", "--cached", "--name-only")] = _completed(["git"], 0, "CodexAutomation/config.json\n")
        staged = run_with(staged_outputs)
        self.assertEqual(staged.effectivePlan["parentGitSnapshot"]["stagedPaths"], ["CodexAutomation/config.json"])

        detached_outputs = dict(base_outputs)
        detached_outputs[("git", "branch", "--show-current")] = _completed(["git"], 0, "")
        detached = run_with(detached_outputs)
        self.assertTrue(detached.effectivePlan["parentGitSnapshot"]["detachedHead"])
        self.assertIsNone(detached.effectivePlan["parentGitSnapshot"]["branch"])

        failure_cases = [
            ("status", ("git", "status", "--porcelain=v1", "--untracked-files=all"), _completed(["git"], 1, "", "bad")),
            ("head", ("git", "rev-parse", "HEAD"), _completed(["git"], 1, "", "bad")),
            ("branch start", ("git", "branch", "--show-current"), OSError("missing")),
            ("missing git", ("git", "rev-parse", "--is-inside-work-tree"), OSError("missing")),
            ("stderr only", ("git", "status", "--porcelain=v1", "--untracked-files=all"), _completed(["git"], 1, "", "fatal")),
        ]
        for name, command, value in failure_cases:
            outputs = dict(base_outputs)
            outputs[command] = value
            with self.subTest(name=name):
                failed = run_with(outputs)
                plan = failed.effectivePlan
                self.assertFalse(failed.ok)
                self.assertEqual(plan["finalVerdict"], "FAILED")
                self.assertEqual(plan["errorCode"], "REAL_TASK_GIT_SNAPSHOT_FAILED")
                self.assertEqual(plan["parentGitSnapshot"]["status"], "failed")
                self.assertIsNone(plan["parentGitSnapshot"]["dirty"])
                self.assertIsNone(plan["dirtyParentWorktree"])
                self.assertEqual(plan["codexInvocationCount"], 0)
                self.assertFalse(plan["modelInvocationStarted"])
                self.assertFalse(plan["sandboxStarted"])
                self.assertFalse(plan["workspaceCreated"])
                self.assertFalse(plan["sourceCopied"])
                self.assertFalse(plan["unityStarted"])

    def test_old_pass_plan_report_is_not_reused_as_current_truth(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            old_report = temp_path / "REAL_TASK_PLAN_REPORT.json"
            old_report.write_text(json.dumps({"finalVerdict": "PASS"}), encoding="utf-8")
            with mock.patch("task_manifest_validator.inspect_source_paths", wraps=task_manifest_validator.inspect_source_paths) as source_mock, mock.patch(
                "task_manifest_validator._parent_git_snapshot", wraps=task_manifest_validator._parent_git_snapshot
            ) as git_mock:
                first = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID, inspect_sources=True)
                second = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID, inspect_sources=True)
        self.assertTrue(first.ok, first.error_dicts())
        self.assertTrue(second.ok, second.error_dicts())
        self.assertNotEqual(first.effectivePlan["planId"], second.effectivePlan["planId"])
        self.assertEqual(source_mock.call_count, 2)
        self.assertEqual(git_mock.call_count, 2)

    def test_real_task_plan_schema_structure_is_strict_for_git_snapshot(self) -> None:
        schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_plan.schema.json")
        self.assertIsInstance(schema, dict)
        self.assertEqual(schema.get("type"), "object")
        self.assertIs(schema.get("additionalProperties"), False)
        self.assertEqual(schema["properties"]["parentGitSnapshot"]["type"], "object")
        snapshot = schema["properties"]["parentGitSnapshot"]
        for field in ("status", "dirty", "commandResults"):
            self.assertIn(field, snapshot["required"])
        command_results = snapshot["properties"]["commandResults"]
        self.assertIs(command_results.get("additionalProperties"), False)
        for name in ("insideWorkTree", "porcelainStatus", "branch", "head", "stagedPaths"):
            self.assertIn(name, command_results["required"])
            entry = command_results["properties"][name]
            self.assertIs(entry.get("additionalProperties"), False)
            self.assertEqual(entry["properties"]["command"]["minItems"], 1)
        self.assertFalse(self._contains_empty_schema(schema))

    def test_real_task_plan_schema_accepts_latest_pass_report(self) -> None:
        report = self._latest_runtime_plan_report()
        errors = validate_real_task_plan_report(ROOT / "CodexAutomation", report)
        self.assertFalse(errors, errors)

    def test_real_task_plan_report_accepts_success_git_snapshots(self) -> None:
        clean = self._sample_plan()
        self.assertFalse(validate_real_task_plan_report(ROOT / "CodexAutomation", clean))

        dirty = self._sample_plan()
        dirty["parentGitSnapshot"]["porcelainStatus"] = [" M CodexAutomation/config.json"]
        dirty["parentGitSnapshot"]["dirty"] = True
        dirty["dirtyParentWorktree"] = True
        self.assertFalse(validate_real_task_plan_report(ROOT / "CodexAutomation", dirty))

        staged = self._sample_plan()
        staged["parentGitSnapshot"]["stagedPaths"] = ["CodexAutomation/config.json"]
        self.assertFalse(validate_real_task_plan_report(ROOT / "CodexAutomation", staged))

        detached = self._sample_plan()
        detached["parentGitSnapshot"]["branch"] = None
        detached["parentGitSnapshot"]["detachedHead"] = True
        self.assertFalse(validate_real_task_plan_report(ROOT / "CodexAutomation", detached))

    def test_real_task_plan_report_rejects_malformed_success_git_snapshots(self) -> None:
        cases = [
            ("dirty null", ("parentGitSnapshot", "dirty"), None),
            ("dirty parent null", ("dirtyParentWorktree",), None),
            ("head null", ("parentGitSnapshot", "head"), None),
            ("head empty", ("parentGitSnapshot", "head"), ""),
            ("branch null", ("parentGitSnapshot", "branch"), None),
            ("detached branch", ("parentGitSnapshot", "detachedHead"), True),
            ("git repo false", ("parentGitSnapshot", "gitRepository"), False),
        ]
        for name, path, value in cases:
            plan = self._sample_plan()
            self._set_nested(plan, path, value)
            if name == "detached branch":
                plan["parentGitSnapshot"]["branch"] = "main"
            with self.subTest(name=name):
                self.assertTrue(validate_real_task_plan_report(ROOT / "CodexAutomation", plan))

        empty_commands = self._sample_plan()
        empty_commands["parentGitSnapshot"]["commandResults"] = {}
        self.assertTrue(validate_real_task_plan_report(ROOT / "CodexAutomation", empty_commands))

        failed_command = self._sample_plan()
        failed_command["parentGitSnapshot"]["commandResults"]["head"]["success"] = False
        failed_command["parentGitSnapshot"]["commandResults"]["head"]["failure"] = "process_failed"
        self.assertTrue(validate_real_task_plan_report(ROOT / "CodexAutomation", failed_command))

        mismatch = self._sample_plan()
        mismatch["parentGitSnapshot"]["dirty"] = True
        mismatch["dirtyParentWorktree"] = False
        self.assertTrue(validate_real_task_plan_report(ROOT / "CodexAutomation", mismatch))

    def test_real_task_plan_report_accepts_valid_failed_git_snapshot(self) -> None:
        failed = self._sample_failed_git_plan()
        errors = validate_real_task_plan_report(ROOT / "CodexAutomation", failed)
        self.assertFalse(errors, errors)

    def test_real_task_plan_report_rejects_malformed_failed_git_snapshots(self) -> None:
        cases = [
            ("dirty false", ("parentGitSnapshot", "dirty"), False),
            ("dirty parent false", ("dirtyParentWorktree",), False),
            ("pass verdict", ("finalVerdict",), "PASS"),
            ("error null", ("errorCode",), None),
            ("generic error", ("errorCode",), "REAL_TASK_INTERNAL_ERROR"),
            ("empty message", ("errorMessage",), ""),
        ]
        for name, path, value in cases:
            plan = self._sample_failed_git_plan()
            self._set_nested(plan, path, value)
            if name == "pass verdict":
                plan["errorCode"] = None
                plan["errorMessage"] = None
            with self.subTest(name=name):
                self.assertTrue(validate_real_task_plan_report(ROOT / "CodexAutomation", plan))

        all_success = self._sample_failed_git_plan()
        for result in all_success["parentGitSnapshot"]["commandResults"].values():
            result["success"] = True
            result["exitCode"] = 0
            result["failure"] = None
            result["stderr"] = ""
        self.assertTrue(validate_real_task_plan_report(ROOT / "CodexAutomation", all_success))

    def test_real_task_plan_report_rejects_clean_looking_failed_snapshot(self) -> None:
        malformed = self._sample_failed_git_plan()
        malformed["parentGitSnapshot"]["dirty"] = False
        malformed["dirtyParentWorktree"] = False
        malformed["finalVerdict"] = "PASS"
        malformed["errorCode"] = None
        malformed["errorMessage"] = None
        self.assertTrue(validate_real_task_plan_report(ROOT / "CodexAutomation", malformed))

    def test_mocked_git_failure_plan_report_passes_strict_failed_contract(self) -> None:
        def fake_run(command, cwd, capture_output, text, check, timeout):
            if command == ["git", "status", "--porcelain=v1", "--untracked-files=all"]:
                return _completed(command, 1, "", "fatal")
            if command == ["git", "rev-parse", "--is-inside-work-tree"]:
                return _completed(command, 0, "true\n")
            if command == ["git", "branch", "--show-current"]:
                return _completed(command, 0, "main\n")
            if command == ["git", "rev-parse", "HEAD"]:
                return _completed(command, 0, "abc123\n")
            if command == ["git", "diff", "--cached", "--name-only"]:
                return _completed(command, 0, "")
            return _completed(command, 0, "")

        with mock.patch("task_manifest_validator.subprocess.run", side_effect=fake_run):
            result = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID, inspect_sources=True)
        self.assertFalse(result.ok)
        self.assertEqual(result.effectivePlan["errorCode"], "REAL_TASK_GIT_SNAPSHOT_FAILED")
        self.assertFalse(validate_real_task_plan_report(ROOT / "CodexAutomation", result.effectivePlan))

    def test_unknown_mode_combination_rejected(self) -> None:
        result = subprocess.run(
            [sys.executable, "CodexAutomation/scripts/orchestrator.py", "--dry-run", "--real-task-plan", str(VALID)],
            cwd=str(ROOT),
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(result.returncode, 2)

    def test_fixture_invalid_files_are_rejected(self) -> None:
        for name in [
            "invalid_absolute_path.json",
            "invalid_wildcard_path.json",
            "invalid_forbidden_root.json",
            "invalid_limit_raise.json",
            "invalid_unsupported_validator.json",
            "invalid_expected_output_scope.json",
            "invalid_overlapping_sources.json",
        ]:
            with self.subTest(name=name):
                self.assertFalse(validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, FIXTURE_ROOT / name).ok)

    def _policy(self):
        policy, errors = parse_real_task_policy(CONFIG)
        self.assertFalse(errors)
        self.assertIsNotNone(policy)
        return policy

    def _sample_plan(self):
        plan = validate_task_manifest_file(ROOT, ROOT / "CodexAutomation", CONFIG, VALID).effectivePlan
        self.assertIsNotNone(plan)
        plan = json.loads(json.dumps(plan))
        plan["sourceInspection"]["inspected"] = True
        plan["sourceInspection"]["entries"] = []
        plan["parentGitSnapshot"] = {
            "status": "success",
            "gitRepository": True,
            "head": "abc123",
            "branch": "main",
            "detachedHead": False,
            "stagedPaths": [],
            "porcelainStatus": [],
            "dirty": False,
            "commandResults": {
                "insideWorkTree": self._git_result(["git", "rev-parse", "--is-inside-work-tree"]),
                "porcelainStatus": self._git_result(["git", "status", "--porcelain=v1", "--untracked-files=all"]),
                "branch": self._git_result(["git", "branch", "--show-current"]),
                "head": self._git_result(["git", "rev-parse", "HEAD"]),
                "stagedPaths": self._git_result(["git", "diff", "--cached", "--name-only"]),
            },
        }
        plan["dirtyParentWorktree"] = False
        plan["finalVerdict"] = "PASS"
        plan["errorCode"] = None
        plan["errorMessage"] = None
        plan["errors"] = []
        return plan

    def _sample_failed_git_plan(self):
        plan = self._sample_plan()
        plan["parentGitSnapshot"]["status"] = "failed"
        plan["parentGitSnapshot"]["gitRepository"] = None
        plan["parentGitSnapshot"]["dirty"] = None
        plan["parentGitSnapshot"]["commandResults"]["porcelainStatus"]["success"] = False
        plan["parentGitSnapshot"]["commandResults"]["porcelainStatus"]["exitCode"] = 1
        plan["parentGitSnapshot"]["commandResults"]["porcelainStatus"]["stderr"] = "fatal"
        plan["dirtyParentWorktree"] = None
        plan["finalVerdict"] = "FAILED"
        plan["errorCode"] = "REAL_TASK_GIT_SNAPSHOT_FAILED"
        plan["errorMessage"] = "Parent Git snapshot command failed."
        plan["errors"] = [{"code": "REAL_TASK_GIT_SNAPSHOT_FAILED", "message": "Parent Git snapshot command failed.", "field": "parentGitSnapshot"}]
        return plan

    def _git_result(self, command: list[str]):
        return {"command": command, "exitCode": 0, "success": True, "stderr": "", "failure": None}

    def _latest_runtime_plan_report(self):
        reports = sorted((ROOT / "CodexAutomation" / "runtime" / "real_task_plans").glob("*/REAL_TASK_PLAN_REPORT.json"), key=lambda item: item.stat().st_mtime, reverse=True)
        self.assertTrue(reports)
        report = read_json(reports[0])
        self.assertFalse(validate_schema_instance(report, read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_plan.schema.json")))
        return report

    def _contains_empty_schema(self, value) -> bool:
        if value == {}:
            return True
        if isinstance(value, dict):
            return any(self._contains_empty_schema(item) for item in value.values())
        if isinstance(value, list):
            return any(self._contains_empty_schema(item) for item in value)
        return False

    def _set_nested(self, data: dict[str, Any], path: tuple[str, ...], value: Any) -> None:
        current = data
        for part in path[:-1]:
            current = current[part]
        current[path[-1]] = value

    def _temp_context(self):
        from models import RepoContext

        return RepoContext(root=ROOT, automation_root=ROOT / "CodexAutomation", dry_run=False)


if __name__ == "__main__":
    unittest.main()
