from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
from pipeline_engine import run_pipeline_self_test  # noqa: E402
from pipeline_models import PipelineContext, PipelineSettings, PipelineState, transition  # noqa: E402
from pipeline_result_validator import validate_audit_result, validate_implementation_result, validate_repair_result  # noqa: E402
from prompt_builder import build_audit_prompt, build_implementation_prompt, build_repair_prompt  # noqa: E402


TASK = ROOT / "CodexAutomation" / "tests" / "fixtures" / "PIPELINE-TEST-001.json"
CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class PipelineStateMachineCheck(unittest.TestCase):
    def test_successful_full_fake_cycle(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK)
        self.assertEqual(report["finalState"], "COMPLETED")
        self.assertEqual(report["finalVerdict"], "PASS")
        self.assertEqual(report["stateHistory"], ["PENDING", "IMPLEMENTING", "VALIDATING", "REPAIRING", "VALIDATING", "AUDITING", "COMPLETED"])
        self.assertEqual(len(report["repairAttempts"]), 1)
        self.assertEqual(report["actualChangedPaths"], ["artifact.json"])

    def test_audit_driven_repair_transition(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "audit_fix_required")
        self.assertEqual(report["finalState"], "COMPLETED")
        self.assertGreaterEqual(report["stateHistory"].count("AUDITING"), 2)
        self.assertGreaterEqual(report["stateHistory"].count("REPAIRING"), 1)

    def test_blocked_audit_stops_pipeline(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "audit_blocked")
        self.assertEqual(report["finalState"], "BLOCKED")

    def test_invalid_transition_fails(self) -> None:
        context = PipelineContext(ROOT, ROOT / "CodexAutomation", "invalid", {"id": "x"}, ROOT, ROOT, PipelineSettings())
        transition(context, PipelineState.COMPLETED)
        self.assertEqual(context.errorCode, "INVALID_STATE_TRANSITION")
        self.assertEqual(context.state, PipelineState.FAILED)

    def test_prompt_builders_contain_required_context(self) -> None:
        task = read_json(TASK)
        implementation = build_implementation_prompt(task)
        audit = build_audit_prompt(task, {"artifact.json": {}}, [], {})
        repair = build_repair_prompt(task, {"validationFindings": [], "auditFindings": [], "repairAttempt": 1, "maxRepairAttempts": 3, "remainingInvocationBudget": 10})
        self.assertIn("ACCEPTANCE CRITERIA", implementation)
        self.assertIn("Do not modify files", audit)
        self.assertIn("ACTIVE FINDINGS", repair)
        for criterion in task["acceptanceCriteria"]:
            self.assertIn(criterion, audit)
        for item in task["nonGoals"]:
            self.assertIn(item, audit)
        self.assertIn("ALLOWED WRITE PATHS", audit)
        self.assertIn("FORBIDDEN WRITE PATHS", audit)
        self.assertIn("Verdict contract", audit)

    def test_invalid_role_execution_wrappers_fail_before_transition(self) -> None:
        for scenario in [
            "invalid_success_missing_result",
            "invalid_rate_limited_with_result",
            "invalid_technical_error_missing_code",
            "invalid_role",
            "invalid_task_id",
        ]:
            report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, scenario)
            self.assertEqual(report["finalState"], "FAILED", scenario)
            self.assertEqual(report["errorCode"], "ROLE_EXECUTION_RESULT_INVALID", scenario)
            self.assertTrue(report["resultValidationErrors"], scenario)
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "invalid_usage_timestamp")
        self.assertEqual(report["finalState"], "FAILED")
        self.assertEqual(report["errorCode"], "CODEX_RATE_LIMIT_DATA_INVALID")
        self.assertTrue(report["resultValidationErrors"])

    def test_invalid_domain_results_fail(self) -> None:
        expected = {
            "invalid_audit_pass_blocking": "AUDIT_RESULT_INVALID",
            "invalid_audit_fix_without_findings": "AUDIT_RESULT_INVALID",
            "invalid_audit_blocked_without_reason": "AUDIT_RESULT_INVALID",
            "invalid_repair_unknown_code": "REPAIR_RESULT_INVALID",
        }
        for scenario, code in expected.items():
            report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, scenario)
            self.assertEqual(report["finalState"], "FAILED", scenario)
            self.assertEqual(report["errorCode"], code, scenario)
            self.assertTrue(report["resultValidationErrors"], scenario)

    def test_non_dict_domain_results_fail_with_role_specific_codes(self) -> None:
        cases = {
            "implementer": "IMPLEMENTATION_RESULT_INVALID",
            "auditor": "AUDIT_RESULT_INVALID",
            "repairer": "REPAIR_RESULT_INVALID",
        }
        suffixes = ["text", "number", "boolean", "array"]
        for role, code in cases.items():
            for suffix in suffixes:
                scenario = f"invalid_{role}_result_{suffix}"
                report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, scenario)
                self.assertEqual(report["finalState"], "FAILED", scenario)
                self.assertEqual(report["errorCode"], code, scenario)
                self.assertTrue(report["resultValidationErrors"], scenario)
        null_report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "invalid_implementer_result_null")
        self.assertEqual(null_report["finalState"], "FAILED")
        self.assertEqual(null_report["errorCode"], "ROLE_EXECUTION_RESULT_INVALID")

    def test_domain_validators_reject_null_with_role_specific_codes(self) -> None:
        schema_root = ROOT / "CodexAutomation" / "schemas"
        self.assertTrue(validate_implementation_result(None, schema_root, "PIPELINE-TEST-001")[0].startswith("IMPLEMENTATION_RESULT_INVALID"))
        self.assertTrue(validate_audit_result(None, schema_root, "PIPELINE-TEST-001")[0].startswith("AUDIT_RESULT_INVALID"))
        self.assertTrue(validate_repair_result(None, schema_root, "PIPELINE-TEST-001", {"validationFindings": [], "auditFindings": []})[0].startswith("REPAIR_RESULT_INVALID"))


if __name__ == "__main__":
    unittest.main()
