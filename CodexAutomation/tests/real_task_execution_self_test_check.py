from __future__ import annotations

import json
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
from real_task_execution_policy import parse_execution_policy, parse_execution_self_test_policy  # noqa: E402
import real_task_execution_self_test as self_test_runner  # noqa: E402
from real_task_execution_self_test import _consume_self_test_handle, _create_self_test_handle, _derive_verdict  # noqa: E402
from real_task_execution_self_test_fixture import create_self_test_fixture, expected_changed_paths, self_test_config  # noqa: E402
from real_task_execution_self_test_models import SELF_TEST_REQUIRED_MESSAGE, SELF_TEST_TASK_ID, file_sha256, new_self_test_run_id  # noqa: E402
from real_task_foundation_runner import prepare_foundation_workspace  # noqa: E402
from real_task_role_adapter import CodexRealTaskRoleAdapter, FakeRealTaskRoleAdapter  # noqa: E402
from schema_validator import validate as validate_schema_instance  # noqa: E402
from task_manifest_validator import validate_task_manifest_file  # noqa: E402


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class RealTaskExecutionSelfTestPolicyTests(unittest.TestCase):
    def test_valid_self_test_policy_and_generic_public_run_disabled(self) -> None:
        execution_policy, execution_errors = parse_execution_policy(CONFIG)
        self.assertFalse(execution_errors, [error.to_dict() for error in execution_errors])
        self.assertTrue(execution_policy.controlledRealModelSelfTestEnabled)
        self.assertFalse(execution_policy.publicGenericRealTaskRunEnabled)
        policy, errors = parse_execution_self_test_policy(CONFIG)
        self.assertFalse(errors, [error.to_dict() for error in errors])
        self.assertEqual(policy.cliFlag, "--real-task-execution-self-test")
        self.assertEqual(policy.maxRunsPerInvocation, 1)
        self.assertEqual(policy.maxRoleInvocations, 3)
        self.assertEqual(policy.maxRepairAttempts, 1)

    def test_self_test_policy_rejects_strict_mutations(self) -> None:
        cases = [
            ("unknown", "unknownField", True),
            ("missing", "enabled", None),
            ("bool_as_int", "maxRunsPerInvocation", True),
            ("max_runs", "maxRunsPerInvocation", 2),
            ("max_roles", "maxRoleInvocations", 2),
            ("max_repairs", "maxRepairAttempts", 0),
            ("user_manifest", "allowUserManifest", True),
            ("user_workspace", "allowUserWorkspace", True),
            ("user_prompt", "allowUserPrompt", True),
            ("user_model", "allowUserModel", True),
            ("task_network", "allowTaskNetwork", True),
            ("retry", "allowAutomaticRetry", True),
            ("downgrade", "allowModelDowngrade", True),
            ("credit", "allowCreditUse", True),
            ("unity", "allowUnity", True),
            ("package", "allowPackageInstall", True),
            ("apply", "allowAutomaticApply", True),
        ]
        for name, field, value in cases:
            bad = json.loads(json.dumps(CONFIG))
            if value is None:
                bad["realTaskExecutionSelfTestPolicy"].pop(field)
            else:
                bad["realTaskExecutionSelfTestPolicy"][field] = value
            with self.subTest(name=name):
                self.assertTrue(parse_execution_self_test_policy(bad)[1])


class RealTaskExecutionSelfTestCliTests(unittest.TestCase):
    def test_cli_shape_and_absent_generic_run_flags(self) -> None:
        help_run = subprocess.run([sys.executable, "CodexAutomation/scripts/orchestrator.py", "--help"], cwd=str(ROOT), capture_output=True, text=True, check=False, timeout=30)
        self.assertEqual(help_run.returncode, 0)
        self.assertIn("--real-task-execution-self-test", help_run.stdout)
        for forbidden in ("--execute-real-task", "--run-task-manifest", "--resume-real-task", "--apply-task-result"):
            self.assertNotIn(forbidden, help_run.stdout)
        extra = subprocess.run([sys.executable, "CodexAutomation/scripts/orchestrator.py", "--real-task-execution-self-test", "manifest.json"], cwd=str(ROOT), capture_output=True, text=True, check=False, timeout=30)
        self.assertNotEqual(extra.returncode, 0)
        duplicate = subprocess.run([sys.executable, "CodexAutomation/scripts/orchestrator.py", "--real-task-execution-self-test", "--real-task-execution-self-test"], cwd=str(ROOT), capture_output=True, text=True, check=False, timeout=30)
        self.assertNotEqual(duplicate.returncode, 0)
        model = subprocess.run([sys.executable, "CodexAutomation/scripts/orchestrator.py", "--real-task-execution-self-test", "--model", "x"], cwd=str(ROOT), capture_output=True, text=True, check=False, timeout=30)
        self.assertNotEqual(model.returncode, 0)


class RealTaskExecutionSelfTestFixtureTests(unittest.TestCase):
    def test_fixture_seed_manifest_and_git_are_fixed(self) -> None:
        run_id = new_self_test_run_id()
        fixture = create_self_test_fixture(ROOT, ROOT / "CodexAutomation", run_id)
        parent = Path(fixture["parent"])
        self.assertTrue(str(Path(fixture["selfTestRoot"])).startswith(str(ROOT / "CodexAutomation" / "runtime" / "real_task_execution_self_tests")))
        self.assertNotEqual(parent.resolve(), ROOT.resolve())
        self.assertEqual((parent / "TaskData" / "message.txt").read_text(encoding="utf-8").strip(), "Replace this controlled self-test placeholder.")
        self.assertIn("Required stage: BOOTSTRAP-03B-2D", (parent / "TaskData" / "reference.txt").read_text(encoding="utf-8"))
        manifest = read_json(Path(fixture["manifestPath"]))
        self.assertEqual(manifest["taskId"], SELF_TEST_TASK_ID)
        self.assertIn(SELF_TEST_REQUIRED_MESSAGE, manifest["objective"])
        self.assertEqual(manifest["allowedWritePaths"], expected_changed_paths())
        self.assertEqual(manifest["allowedDeletePaths"], [])
        self.assertNotIn("model", manifest)
        self.assertNotIn("sandboxPolicy", manifest)
        self.assertNotIn("approvalPolicy", manifest)
        validators = [entry["type"] for entry in manifest["validationPlan"]]
        for required in ("file_exists", "file_absent", "json_valid", "json_field_equals", "text_contains", "text_not_contains", "changed_paths_exact", "no_unexpected_files", "no_conflict_markers", "max_file_size", "max_changed_files", "extension_allowlist"):
            self.assertIn(required, validators)
        validation = validate_task_manifest_file(parent, ROOT / "CodexAutomation", self_test_config(CONFIG), Path(fixture["manifestPath"]), inspect_sources=True)
        self.assertTrue(validation.ok, [error.to_dict() for error in validation.errors])
        head = subprocess.run(["git", "log", "-1", "--pretty=%s"], cwd=str(parent), capture_output=True, text=True, check=False, timeout=10)
        self.assertEqual(head.stdout.strip(), "seed controlled self-test task")

    def test_controlled_fixture_uses_production_foundation_final_report_receipt(self) -> None:
        run_id = new_self_test_run_id()
        fixture = create_self_test_fixture(ROOT, ROOT / "CodexAutomation", run_id)
        controlled_config = self_test_config(CONFIG)
        foundation = prepare_foundation_workspace(
            Path(fixture["parent"]),
            ROOT / "CodexAutomation",
            controlled_config,
            Path(fixture["manifestPath"]),
            "foundation_2d_receipt_12345678",
        )
        self.assertEqual(foundation["finalVerdict"], "PASS", foundation)
        self.assertTrue(foundation["complete"])
        self.assertIsInstance(foundation["finalReportReceipt"], dict)
        final_path = Path(fixture["parent"]) / "CodexAutomation" / "runtime" / "real_task_foundation_runs" / foundation["runId"] / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
        self.assertTrue(final_path.exists())
        self.assertEqual(foundation["finalReportReceipt"]["relativePath"], f"real_task_foundation_runs/{foundation['runId']}/FINAL_WORKSPACE_PREPARATION_REPORT.json")
        self.assertEqual(foundation["finalReportReceipt"]["size"], final_path.stat().st_size)
        self.assertFalse(foundation["modelInvocationStarted"])
        self.assertFalse(foundation["sandboxStarted"])


class RealTaskExecutionSelfTestCapabilityTests(unittest.TestCase):
    def test_capability_identity_and_single_use(self) -> None:
        run_id = new_self_test_run_id()
        fixture = create_self_test_fixture(ROOT, ROOT / "CodexAutomation", run_id)
        manifest_sha = file_sha256(Path(fixture["manifestPath"]))
        parent_identity = "a" * 64
        handle = _create_self_test_handle(run_id, fixture, manifest_sha, parent_identity)
        _consume_self_test_handle(handle, run_id, manifest_sha, parent_identity)
        with self.assertRaises(Exception):
            _consume_self_test_handle(handle, run_id, manifest_sha, parent_identity)
        copied = type(handle)(**handle.__dict__)
        with self.assertRaises(Exception):
            _consume_self_test_handle(copied, run_id, manifest_sha, parent_identity)


class RealTaskExecutionSelfTestControllerTests(unittest.TestCase):
    def test_controller_uses_production_adapter_and_writes_pass_report_with_fake_result(self) -> None:
        class FakeResult:
            finalVerdict = "PASS"
            finalState = "COMPLETED"
            bundleManifestPath: str | None = None

            def __init__(self, fixture_parent: Path) -> None:
                run_dir = fixture_parent / "CodexAutomation" / "runtime" / "real_task_runs" / "fake_orchestration"
                bundle = run_dir / "result_bundle"
                workspace = fixture_parent / "CodexAutomation" / "runtime" / "real_task_foundation_runs" / "fake_foundation" / "workspace" / "TaskData"
                workspace.mkdir(parents=True, exist_ok=True)
                (workspace / "message.txt").write_text(SELF_TEST_REQUIRED_MESSAGE + "\n", encoding="utf-8")
                (workspace / "result.json").write_text(json.dumps({"status": "PASS", "stage": "BOOTSTRAP-03B-2D", "mode": "controlled-real-model-self-test", "message": SELF_TEST_REQUIRED_MESSAGE}), encoding="utf-8")
                bundle.mkdir(parents=True, exist_ok=True)
                (bundle / "RESULT_MANIFEST.json").write_text(json.dumps({"eligibleForApply": False}), encoding="utf-8")
                self.bundleManifestPath = str(bundle / "RESULT_MANIFEST.json")
                self.reportPath = str(run_dir / "FINAL_REAL_TASK_REPORT.json")
                self.report = {
                    "orchestrationRunId": "fake_orchestration",
                    "foundationRunId": "fake_foundation",
                    "finalState": "COMPLETED",
                    "finalVerdict": "PASS",
                    "invocationsUsed": 2,
                    "repairsUsed": 0,
                    "codexInvocationCount": 2,
                    "modelInvocationStarted": True,
                    "sandboxStarted": True,
                    "networkUsed": False,
                    "unityStarted": False,
                    "packageInstallUsed": False,
                    "automaticRetryUsed": False,
                    "modelDowngradeUsed": False,
                    "creditUsageTriggered": False,
                    "auditorRan": True,
                    "auditorApproved": True,
                    "workspaceUnchangedAfterAuditor": True,
                    "bundleIntegrityValid": True,
                }
                run_dir.mkdir(parents=True, exist_ok=True)
                (run_dir / "FINAL_REAL_TASK_REPORT.json").write_text(json.dumps(self.report), encoding="utf-8")

        def fake_execute(manifest_path, root, automation_root, config, adapter):
            self.assertIsInstance(adapter, CodexRealTaskRoleAdapter)
            self.assertTrue(adapter.allow_real_execution)
            self.assertFalse(isinstance(adapter, FakeRealTaskRoleAdapter))
            self.assertEqual(Path(manifest_path).name, "controlled_task_manifest.json")
            return FakeResult(Path(root))

        with patch.object(self_test_runner, "_execute_real_task_with_test_adapter", side_effect=fake_execute):
            report = self_test_runner.run_real_task_execution_self_test(ROOT, ROOT / "CodexAutomation", CONFIG)
        self.assertEqual(report["selfTestVerdict"], "PASS", report)
        self.assertTrue(report["rootRepositoryUnchanged"])
        self.assertTrue(report["syntheticParentUnchanged"])
        self.assertFalse(report["eligibleForApply"])

    def test_verdict_precedence(self) -> None:
        pass_report = {
            "finalVerdict": "PASS",
            "auditorRan": True,
            "auditorApproved": True,
            "workspaceUnchangedAfterAuditor": True,
            "bundleIntegrityValid": True,
            "modelInvocationStarted": True,
            "sandboxStarted": True,
            "networkUsed": False,
            "unityStarted": False,
            "packageInstallUsed": False,
            "automaticRetryUsed": False,
            "modelDowngradeUsed": False,
            "creditUsageTriggered": False,
            "invocationsUsed": 2,
            "repairsUsed": 0,
            "codexInvocationCount": 2,
        }
        self.assertEqual(_derive_verdict(pass_report, True, True, True, None), "PASS")
        self.assertEqual(_derive_verdict(pass_report, False, True, True, None), "BLOCKED")
        self.assertEqual(_derive_verdict({"finalVerdict": "RATE_LIMITED"}, True, True, False, None), "RATE_LIMITED")
        self.assertEqual(_derive_verdict({"finalVerdict": "FAIL"}, True, True, False, None), "FAIL")


class RealTaskExecutionSelfTestReportTests(unittest.TestCase):
    def test_report_schema_rejects_unknown_nested_field(self) -> None:
        schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_execution_self_test.schema.json")
        report = {
            "reportVersion": 1,
            "stage": "BOOTSTRAP-03B-2D",
            "selfTestRunId": "rtes_unit",
            "selfTestTaskId": SELF_TEST_TASK_ID,
            "selfTestRootIdentity": "root",
            "syntheticParentIdentity": "parent",
            "controlledManifestRelativePath": "fixture/controlled_task_manifest.json",
            "controlledManifestSha256": "a" * 64,
            "productionOrchestrationRunId": "run",
            "productionFinalReportRelativePath": "parent/CodexAutomation/runtime/real_task_runs/run/FINAL_REAL_TASK_REPORT.json",
            "productionFinalReportSha256": "b" * 64,
            "resultBundleManifestRelativePath": "parent/CodexAutomation/runtime/real_task_runs/run/result_bundle/RESULT_MANIFEST.json",
            "resultBundleManifestSha256": "c" * 64,
            "startedAt": "2026-07-05T00:00:00Z",
            "finishedAt": "2026-07-05T00:00:01Z",
            "durationMilliseconds": 1,
            "productionFinalState": "COMPLETED",
            "productionFinalVerdict": "PASS",
            "selfTestVerdict": "PASS",
            "invocationsUsed": 2,
            "repairsUsed": 0,
            "codexInvocationCount": 2,
            "modelInvocationStarted": True,
            "sandboxStarted": True,
            "taskNetworkUsed": False,
            "unityStarted": False,
            "packageInstallUsed": False,
            "automaticRetryUsed": False,
            "modelDowngradeUsed": False,
            "creditUsageTriggered": False,
            "rootRepositoryHeadBefore": "h",
            "rootRepositoryHeadAfter": "h",
            "rootRepositorySnapshotBeforeHash": "d" * 64,
            "rootRepositorySnapshotAfterHash": "d" * 64,
            "rootRepositoryUnchanged": True,
            "syntheticParentSnapshotBeforeHash": "e" * 64,
            "syntheticParentSnapshotAfterHash": "e" * 64,
            "syntheticParentUnchanged": True,
            "finalHostValidationPassed": True,
            "auditorRan": True,
            "auditorApproved": True,
            "auditorWorkspaceUnchanged": True,
            "bundleIntegrityValid": True,
            "eligibleForApply": False,
            "complete": True,
            "errorCode": None,
            "errorMessage": None,
            "warnings": [],
        }
        self.assertFalse(validate_schema_instance(report, schema))
        report["warnings"] = [{"code": "W", "message": "M", "extra": True}]
        self.assertTrue(validate_schema_instance(report, schema))


if __name__ == "__main__":
    unittest.main()
