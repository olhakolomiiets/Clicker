from __future__ import annotations

import inspect
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
import real_task_execution_context as execution_context_registry  # noqa: E402
import real_task_runner  # noqa: E402
from real_task_diagnostic_runner import repair_decision  # noqa: E402
from real_task_execution_context import create_execution_handle, mark_execution_handle_terminal, transition_execution_handle, validate_execution_handle  # noqa: E402
from real_task_bundle import create_result_bundle  # noqa: E402
from real_task_execution_models import RealTaskExecutionContext, RoleInvocationResult, canonical_sha256  # noqa: E402
from real_task_execution_policy import apply_manifest_role_budget, parse_execution_policy  # noqa: E402
from real_task_role_adapter import CodexRealTaskRoleAdapter, FakeRealTaskRoleAdapter  # noqa: E402
from real_task_role_prompts import build_auditor_prompt, build_implementer_prompt, build_repair_prompt, prompt_hash  # noqa: E402
from real_task_runner import _execute_real_task_with_test_adapter, execute_real_task  # noqa: E402
from schema_validator import validate as validate_schema_instance  # noqa: E402


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class RealTaskExecutionPolicyTests(unittest.TestCase):
    def test_valid_execution_policy_and_rejections(self) -> None:
        policy, errors = parse_execution_policy(CONFIG)
        self.assertFalse(errors, [error.to_dict() for error in errors])
        self.assertIsNotNone(policy)
        self.assertEqual(policy.maxRoleInvocations, 3)
        self.assertFalse(policy.publicRunCliEnabled)
        cases = [
            ("unknown", "x", True),
            ("missing", "enabled", None),
            ("bool_as_int", "maxRoleInvocations", True),
            ("zero_cap", "maxBundleFiles", 0),
            ("too_many_invocations", "maxRoleInvocations", 4),
            ("too_many_repairs", "maxRepairAttempts", 2),
            ("public_cli", "publicRunCliEnabled", True),
            ("self_test", "controlledRealModelSelfTestEnabled", True),
            ("danger", "allowNetwork", True),
        ]
        for _name, field, value in cases:
            bad = json.loads(json.dumps(CONFIG))
            if field == "enabled" and value is None:
                bad["realTaskExecutionPolicy"].pop(field)
            else:
                bad["realTaskExecutionPolicy"][field] = value
            with self.subTest(field=field):
                self.assertTrue(parse_execution_policy(bad)[1])

    def test_manifest_budget_can_only_lower_caps(self) -> None:
        policy = parse_execution_policy(CONFIG)[0]
        self.assertIsNotNone(policy)
        budget, errors = apply_manifest_role_budget(policy, {"roleBudget": {"maxInvocations": 2, "maxRepairAttempts": 0}})
        self.assertFalse(errors)
        self.assertEqual(budget["maxRoleInvocations"], 2)
        self.assertEqual(budget["maxRepairAttempts"], 0)
        self.assertTrue(apply_manifest_role_budget(policy, {"roleBudget": {"maxInvocations": 4}})[1])


class RealTaskExecutionContextTests(unittest.TestCase):
    def test_context_schema_hash_and_handle_registry(self) -> None:
        context = RealTaskExecutionContext(
            contextVersion=1,
            orchestrationRunId="real_task_unit_1234",
            taskId="TASK-UNIT",
            stage="BOOTSTRAP-03B-2C",
            createdAt="2026-07-05T00:00:00Z",
            runDirectory=str((ROOT / "CodexAutomation" / "runtime" / "real_task_runs" / "unit").resolve()),
            manifestPath=str((ROOT / "CodexAutomation" / "tests" / "fixtures" / "real_tasks" / "valid_minimal.json").resolve()),
            manifestSha256="a" * 64,
            effectivePolicyHash="b" * 64,
            foundationRunId="foundation_unit",
            foundationRunDirectory=str((ROOT / "CodexAutomation" / "runtime" / "real_task_foundation_runs" / "foundation_unit").resolve()),
            foundationFinalReportHash="c" * 64,
            trustedContextHash="d" * 64,
            workspaceIdentity={"workspaceDirectory": str(ROOT / "CodexAutomation" / "runtime" / "unit" / "workspace"), "workspaceBaselineInventoryHash": "e" * 64, "isolatedGitValid": True},
            parentRepositoryIdentity={"repositoryRoot": str(ROOT), "parentHead": "1527389ce"},
            initialParentGitSnapshotHash="f" * 64,
            initialParentSourceInventoryHash="1" * 64,
            baselineInventoryHash="2" * 64,
            executionPolicyHash="3" * 64,
            invocationBudget={"maxRoleInvocations": 3, "invocationsUsed": 0},
            repairBudget={"maxRepairAttempts": 1, "repairsUsed": 0},
            roleSequence=[],
            noParentWrite=True,
            noAutomaticApply=True,
            noNetwork=True,
            noPackageInstall=True,
            noUnity=True,
        )
        schema_errors = validate_schema_instance(context.to_dict(), read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_execution_context.schema.json"))
        self.assertFalse(schema_errors, schema_errors)
        self.assertEqual(canonical_sha256(context.to_dict()), canonical_sha256(context.to_dict()))
        handle = create_execution_handle(context, canonical_sha256(context.to_dict()))
        self.assertIs(validate_execution_handle(handle), handle)
        with self.assertRaises(Exception):
            transition_execution_handle(handle, ("CONTEXT_READY",), "DIAGNOSTIC_COMPLETE")
        forged = type(handle)(**{**handle.__dict__})
        with self.assertRaises(Exception):
            validate_execution_handle(forged)
        mark_execution_handle_terminal(handle)
        with self.assertRaises(Exception):
            validate_execution_handle(handle)


class RealTaskRoleAdapterTests(unittest.TestCase):
    def test_production_adapter_boundary_is_fixed_and_disabled_for_2c(self) -> None:
        adapter = CodexRealTaskRoleAdapter()
        self.assertTrue(adapter.fixed_registration)
        self.assertFalse(adapter.shell)
        self.assertFalse(adapter.network_allowed)
        result = adapter.run_implementer({"orchestrationRunId": "x"}, "prompt")
        self.assertEqual(result.verdict, "BLOCKED")
        self.assertEqual(result.errorCode, "REAL_TASK_ROLE_EXECUTION_FAILED")


class RealTaskPromptTests(unittest.TestCase):
    def test_prompts_are_deterministic_and_omit_dangerous_permissions(self) -> None:
        task = {"taskId": "TASK-001"}
        context = {"orchestrationRunId": "run-1"}
        prompts = [
            build_implementer_prompt(task, context),
            build_repair_prompt(task, context, {"findings": []}),
            build_auditor_prompt(task, context, {"finalVerdict": "PASS"}),
        ]
        self.assertEqual([prompt_hash(item) for item in prompts], [prompt_hash(item) for item in prompts])
        joined = "\n".join(prompts)
        self.assertNotIn("danger-full-access", joined)
        self.assertNotIn("model override", joined.lower())
        self.assertIn("Parent repository is not writable.", joined)


class RealTaskOrchestrationTests(unittest.TestCase):
    def test_direct_success_scenario_passes_without_model_sandbox_unity_or_network(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("direct_success"))
            self.assertEqual(result.finalVerdict, "PASS", result.report)
            self.assertEqual(result.report["invocationsUsed"], 2)
            self.assertTrue(result.report["finalHostValidationPassed"])
            self.assertTrue(result.report["auditorApproved"])
            self.assertFalse(result.report["modelInvocationStarted"])
            self.assertFalse(result.report["sandboxStarted"])
            self.assertFalse(result.report["unityStarted"])
            self.assertFalse(result.report["networkUsed"])
            self.assertIsNotNone(result.bundleManifestPath)
            bundle = read_json(Path(result.bundleManifestPath))
            self.assertFalse(bundle["eligibleForApply"])
            sums = read_json(Path(result.bundleManifestPath).parent / "SHA256SUMS.json")
            self.assertEqual(bundle["sha256SumsExcludedPaths"], ["RESULT_MANIFEST.json", "SHA256SUMS.json"])
            self.assertEqual(bundle["sha256SumsSha256"], canonical_sha256(sums))
            self.assertEqual(bundle["sha256SumsSize"], (Path(result.bundleManifestPath).parent / "SHA256SUMS.json").stat().st_size)
            self.assertEqual(bundle["coveredFileCount"], sums["totalFiles"])
            self.assertEqual(bundle["coveredTotalBytes"], sums["totalBytes"])
            covered = {entry["relativePath"] for entry in sums["entries"]}
            self.assertNotIn("RESULT_MANIFEST.json", covered)
            self.assertNotIn("SHA256SUMS.json", covered)
            run_dir = Path(result.reportPath).parent
            context_report = read_json(run_dir / "REAL_TASK_EXECUTION_CONTEXT.json")
            self.assertEqual(canonical_sha256(context_report), result.report["executionContextHash"])
            role_report = read_json(run_dir / "implementer_INVOCATION_REPORT.json")
            self.assertEqual(role_report["invocationId"], "implementer_1")
            self.assertEqual(role_report["sequence"], 1)
            decision = read_json(run_dir / "REPAIR_DECISION_REPORT.json")
            diagnostic = read_json(run_dir / "DIAGNOSTIC_VALIDATION_ATTEMPT_001.json")
            self.assertEqual(decision["decision"], "NOT_NEEDED")
            self.assertFalse(decision["shouldRepair"])
            self.assertTrue(decision["diagnosticReceiptConsumed"])
            self.assertEqual(decision["diagnosticPayloadHash"], diagnostic["diagnosticReportHash"])
            self.assertEqual(decision["diagnosticArtifactHash"], canonical_sha256(diagnostic))
            self.assertNotEqual(decision["diagnosticPayloadHash"], decision["diagnosticArtifactHash"])
            self.assertEqual(decision["diagnosticReportRelativePath"], f"real_task_runs/{run_dir.name}/DIAGNOSTIC_VALIDATION_ATTEMPT_001.json")

    def test_manifest_lowered_invocation_budget_blocks_auditor(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete", max_invocations=1)
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("direct_success"))
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_INVOCATION_BUDGET_EXCEEDED")
            self.assertEqual(result.report["invocationsUsed"], 1)
            self.assertFalse(result.report["auditorRan"])

    def test_manifest_budget_two_repair_blocks_mandatory_auditor(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete", max_invocations=2)
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("repair_success"))
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_INVOCATION_BUDGET_EXCEEDED")
            self.assertEqual(result.report["invocationsUsed"], 2)
            self.assertTrue(result.report["repairAttempted"])
            self.assertFalse(result.report["auditorRan"])

    def test_one_repair_success_uses_original_baseline_and_three_invocations(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("repair_success"))
            self.assertEqual(result.finalVerdict, "PASS", result.report)
            self.assertEqual(result.report["invocationsUsed"], 3)
            self.assertEqual(result.report["repairsUsed"], 1)
            decision = read_json(Path(result.reportPath).parent / "REPAIR_DECISION_REPORT.json")
            self.assertEqual(decision["decision"], "RUN_REPAIR")
            self.assertTrue(decision["shouldRepair"])
            self.assertTrue(decision["diagnosticReceiptConsumed"])

    def test_repair_decision_api_does_not_accept_diagnostic_dict(self) -> None:
        self.assertNotIn("diagnostic", inspect.signature(repair_decision).parameters)

    def test_modified_persisted_diagnostic_blocks_repair_without_fallback(self) -> None:
        def tamper(path: Path, _handle) -> None:
            payload = read_json(path)
            payload["durationSeconds"] = payload["durationSeconds"] + 1
            path.write_text(json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)
        self.assertFalse(result.report["finalHostValidationPassed"])
        self.assertFalse(result.report["auditorRan"])
        self.assertIsNone(result.bundleManifestPath)

    def test_wrong_diagnostic_artifact_hash_blocks_repair(self) -> None:
        def tamper(_path: Path, handle) -> None:
            execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]["trustedCanonicalSha256"] = "0" * 64

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_wrong_diagnostic_artifact_size_blocks_repair(self) -> None:
        def tamper(_path: Path, handle) -> None:
            record = execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]
            record["trustedSize"] = int(record["trustedSize"]) + 1

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_substituted_diagnostic_identity_blocks_repair(self) -> None:
        def tamper(path: Path, handle) -> None:
            payload = read_json(path)
            payload["orchestrationRunId"] = "other_run"
            payload["taskId"] = "OTHER-TASK"
            payload["executionContextHash"] = "1" * 64
            payload["originalBaselineHash"] = "2" * 64
            payload["diagnosticReportHash"] = canonical_sha256({key: value for key, value in payload.items() if key != "diagnosticReportHash"})
            path.write_text(json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
            record = execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]
            record["trustedSize"] = path.stat().st_size
            record["trustedCanonicalSha256"] = canonical_sha256(payload)

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_consumed_diagnostic_receipt_blocks_repair(self) -> None:
        def tamper(_path: Path, handle) -> None:
            execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]["consumedForRepairDecision"] = True

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_wrong_internal_diagnostic_payload_hash_blocks_repair(self) -> None:
        def tamper(path: Path, handle) -> None:
            payload = read_json(path)
            payload["diagnosticReportHash"] = "0" * 64
            path.write_text(json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
            execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]["trustedSize"] = path.stat().st_size

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_authorizing_diagnostic_report_blocks_repair(self) -> None:
        def tamper(path: Path, handle) -> None:
            payload = read_json(path)
            payload["authorization"] = True
            path.write_text(json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
            execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]["trustedSize"] = path.stat().st_size

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_duplicate_key_diagnostic_report_blocks_repair(self) -> None:
        def tamper(path: Path, handle) -> None:
            original = path.read_text(encoding="utf-8")
            path.write_text(original[:-2] + ',"repairEligible":true}\n', encoding="utf-8", newline="\n")
            execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]["trustedSize"] = path.stat().st_size

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_semantic_contradiction_diagnostic_report_blocks_repair(self) -> None:
        def tamper(path: Path, handle) -> None:
            payload = read_json(path)
            payload["finalVerdict"] = "PASS"
            payload["repairEligible"] = True
            payload["diagnosticReportHash"] = canonical_sha256({key: value for key, value in payload.items() if key != "diagnosticReportHash"})
            path.write_text(json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
            execution_context_registry._HANDLE_REGISTRY[handle.orchestrationRunId]["diagnosticReceipts"]["1"]["trustedSize"] = path.stat().st_size

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_role_or_in_memory_repair_claim_has_no_effect_after_persisted_tamper(self) -> None:
        def tamper(path: Path, _handle) -> None:
            payload = read_json(path)
            payload["repairEligible"] = False
            payload["diagnosticReportHash"] = canonical_sha256({key: value for key, value in payload.items() if key != "diagnosticReportHash"})
            path.write_text(json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")

        result, adapter = run_with_diagnostic_tamper(tamper)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_DIAGNOSTIC_FAILED")
        self.assertNotIn("REPAIRER", adapter.invocations)

    def test_repair_no_progress_fails_without_auditor(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("repair_no_progress"))
            self.assertEqual(result.finalVerdict, "FAIL")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_REPAIR_NO_PROGRESS")
            self.assertFalse(result.report["auditorRan"])

    def test_host_blocked_service_mutation_stops_before_auditor(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("host_blocked"))
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertFalse(result.report["auditorRan"])

    def test_rate_limit_has_no_retry_repair_or_auditor(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("rate_limited"))
            self.assertEqual(result.finalState, "RATE_LIMITED")
            self.assertEqual(result.report["invocationsUsed"], 1)
            self.assertFalse(result.report["repairAttempted"])
            self.assertFalse(result.report["auditorRan"])

    def test_timeout_has_no_retry_repair_or_auditor(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("timeout"))
            self.assertEqual(result.finalState, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_TIMEOUT")
            self.assertEqual(result.report["invocationsUsed"], 1)
            self.assertFalse(result.report["repairAttempted"])
            self.assertFalse(result.report["auditorRan"])

    def test_auditor_reject_is_fail_without_bundle(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("auditor_reject"))
            self.assertEqual(result.finalVerdict, "FAIL")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_AUDIT_REJECTED")
            self.assertIsNone(result.bundleManifestPath)

    def test_malformed_auditor_blocks_without_bundle(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("auditor_schema_invalid"))
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_AUDIT_RESULT_INVALID")
            self.assertIsNone(result.bundleManifestPath)

    def test_adapter_wrong_invocation_id_blocks_before_diagnostic(self) -> None:
        class WrongInvocationAdapter(FakeRealTaskRoleAdapter):
            def run_implementer(self, context: dict, prompt: str) -> RoleInvocationResult:
                result = super().run_implementer(context, prompt)
                return RoleInvocationResult(
                    role=result.role,
                    invocationId="forged_invocation",
                    status=result.status,
                    verdict=result.verdict,
                    report=result.report,
                    changedWorkspace=result.changedWorkspace,
                    errorCode=result.errorCode,
                    errorMessage=result.errorMessage,
                )

        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), WrongInvocationAdapter("direct_success"))
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_ROLE_RESULT_INVALID")
            self.assertIsNone(result.report["diagnosticReportHash"])

    def test_auditor_mutation_blocks_bundle(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("auditor_mutation"))
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_AUDITOR_CHANGED_WORKSPACE")
            self.assertIsNone(result.bundleManifestPath)

    def test_auditor_agents_mutation_blocks_bundle(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("auditor_agents_mutation"))
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_AUDITOR_CHANGED_WORKSPACE")
            self.assertIsNone(result.bundleManifestPath)

    def test_bundle_rejects_raw_context_without_registered_handle(self) -> None:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), FakeRealTaskRoleAdapter("direct_success"))
            self.assertEqual(result.finalVerdict, "PASS", result.report)
            with self.assertRaises(Exception):
                create_result_bundle({}, parent_path, ROOT / "CodexAutomation", result.report, {}, {}, {}, CONFIG["realTaskExecutionPolicy"])  # type: ignore[arg-type]

    def test_public_production_entry_does_not_accept_adapter_injection(self) -> None:
        self.assertEqual(execute_real_task.__code__.co_argcount, 1)


class RealTaskReportTests(unittest.TestCase):
    def test_final_schema_rejects_unknown_nested_field(self) -> None:
        schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_final.schema.json")
        report = {
            "reportVersion": 1,
            "orchestrationRunId": "run",
            "taskId": "task",
            "stage": "BOOTSTRAP-03B-2C",
            "stateHistory": ["PENDING"],
            "executionContextHash": None,
            "manifestSha256": None,
            "effectivePolicyHash": None,
            "foundationRunId": None,
            "foundationFinalReportHash": None,
            "sandboxProbeReportHash": None,
            "implementerInvocationReportHash": None,
            "diagnosticReportHash": None,
            "repairDecisionReportHash": None,
            "repairerInvocationReportHash": None,
            "finalChangeAnalysisReportHash": None,
            "finalValidationReportHash": None,
            "auditorInvocationReportHash": None,
            "auditReportHash": None,
            "resultBundleManifestHash": None,
            "invocationBudget": {"maxRoleInvocations": 3, "invocationsUsed": 0},
            "invocationsUsed": 0,
            "repairBudget": {"maxRepairAttempts": 1, "repairsUsed": 0},
            "repairsUsed": 0,
            "implementerCompleted": False,
            "diagnosticVerdict": None,
            "repairAttempted": False,
            "finalHostValidationPassed": False,
            "auditorRan": False,
            "auditorApproved": False,
            "workspaceUnchangedAfterAuditor": None,
            "parentGitChanged": False,
            "parentSourceChanged": None,
            "serviceFilesValid": None,
            "agentsDirectoryValid": None,
            "isolatedGitValid": None,
            "bundleIntegrityValid": False,
            "codexInvocationCount": 0,
            "modelInvocationStarted": False,
            "sandboxStarted": False,
            "unityStarted": False,
            "networkUsed": False,
            "packageInstallUsed": False,
            "automaticRetryUsed": False,
            "modelDowngradeUsed": False,
            "creditUsageTriggered": False,
            "durationSeconds": 0,
            "finalState": "BLOCKED",
            "finalVerdict": "BLOCKED",
            "errorCode": None,
            "errorMessage": None,
            "warnings": [],
        }
        self.assertFalse(validate_schema_instance(report, schema))
        report["extra"] = True
        self.assertTrue(validate_schema_instance(report, schema))


def synthetic_parent():
    return tempfile.TemporaryDirectory()


def run_with_diagnostic_tamper(mutator):
    original = real_task_runner.run_diagnostic_pass
    adapter = FakeRealTaskRoleAdapter("repair_success")

    def wrapped(handle, root, automation_root, config, change_handle, attempt_number=1):
        diagnostic, digest = original(handle, root, automation_root, config, change_handle, attempt_number)
        mutator(Path(handle.runDirectory) / "DIAGNOSTIC_VALIDATION_ATTEMPT_001.json", handle)
        return diagnostic, digest

    real_task_runner.run_diagnostic_pass = wrapped
    try:
        with synthetic_parent() as parent:
            parent_path = Path(parent)
            manifest = write_manifest(parent_path, contains_text="complete")
            result = _execute_real_task_with_test_adapter(manifest, parent_path, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), adapter)
            return result, adapter
    finally:
        real_task_runner.run_diagnostic_pass = original


def file_sha256(path: Path) -> str:
    import hashlib

    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_manifest(parent: Path, contains_text: str, max_invocations: int = 3) -> Path:
    subprocess.run(["git", "init"], cwd=parent, capture_output=True, text=True, check=True)
    subprocess.run(["git", "config", "user.email", "test@example.invalid"], cwd=parent, check=True)
    subprocess.run(["git", "config", "user.name", "Test User"], cwd=parent, check=True)
    (parent / ".gitignore").write_text("CodexAutomation/runtime/\n", encoding="utf-8", newline="\n")
    source_dir = parent / "Assets" / "Generated"
    source_dir.mkdir(parents=True)
    (source_dir / "seed.txt").write_text("seed\n", encoding="utf-8", newline="\n")
    subprocess.run(["git", "add", "--", ".gitignore", "Assets/Generated/seed.txt"], cwd=parent, check=True)
    subprocess.run(["git", "commit", "-m", "seed"], cwd=parent, capture_output=True, text=True, check=True)
    manifest = {
        "schemaVersion": 1,
        "taskId": "TASK-EXEC-001",
        "title": "Execution test",
        "objective": "Create a deterministic generated task output.",
        "taskType": "isolated_code_change",
        "sourcePaths": ["Assets/Generated"],
        "allowedWritePaths": ["Assets/Generated/planet_task_output.txt"],
        "allowedDeletePaths": [],
        "expectedOutputs": [{"path": "Assets/Generated/planet_task_output.txt", "kind": "file", "required": True}],
        "validationPlan": [
            {"id": "changed", "type": "changed_paths_exact", "paths": ["Assets/Generated/planet_task_output.txt"]},
            {"id": "contains", "type": "text_contains", "path": "Assets/Generated/planet_task_output.txt", "text": contains_text},
            {"id": "no-conflicts", "type": "no_conflict_markers", "paths": ["Assets/Generated/planet_task_output.txt"]},
        ],
        "completionCriteria": ["Generated output exists."],
        "roleBudget": {"maxInvocations": max_invocations},
        "repairPolicy": {"maxAttempts": 1},
        "limits": {"maxChangedFiles": 10, "maxChangedBytes": 1048576, "maxSingleChangedFileBytes": 1048576},
        "metadata": {},
    }
    path = parent / "task_manifest.json"
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8", newline="\n")
    return path


if __name__ == "__main__":
    unittest.main()
