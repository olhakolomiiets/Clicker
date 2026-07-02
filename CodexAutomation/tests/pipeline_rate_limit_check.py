from __future__ import annotations

import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
from pipeline_engine import run_pipeline_self_test, run_rate_limit_self_test  # noqa: E402


TASK = ROOT / "CodexAutomation" / "tests" / "fixtures" / "PIPELINE-TEST-001.json"
CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class PipelineRateLimitCheck(unittest.TestCase):
    def test_all_rate_limit_scenarios(self) -> None:
        report = run_rate_limit_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK)
        self.assertTrue(report["allPassed"])
        self.assertFalse(report["realCodexStarted"])
        self.assertFalse(report["sleepPerformed"])
        self.assertFalse(report["automaticCreditUsage"])
        self.assertFalse(report["automaticModelDowngrade"])

    def test_rate_limit_does_not_increment_repair_attempt(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "rate_limit_repairer")
        pause = report["rateLimitPauses"][0]
        self.assertEqual(pause["pausedRole"], "repairer")
        self.assertEqual(pause["repairAttempt"], 1)
        self.assertEqual(len(report["repairAttempts"]), 1)

    def test_unknown_reset_is_not_five_hours(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "unknown_reset")
        pause = report["rateLimitPauses"][0]
        self.assertIsNone(pause["resetAtUtc"])
        self.assertEqual(report["errorCode"], "CODEX_RATE_LIMIT_RESET_UNKNOWN")

    def test_retry_exhausted_does_not_loop(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "retry_exhausted")
        self.assertEqual(report["errorCode"], "CODEX_RATE_LIMIT_RETRY_EXHAUSTED")
        self.assertLessEqual(len(report["rateLimitPauses"]), 2)

    def test_credits_exhausted_no_credit_purchase_or_model_change(self) -> None:
        report = run_pipeline_self_test(ROOT, ROOT / "CodexAutomation", CONFIG, TASK, "credits_exhausted")
        self.assertEqual(report["errorCode"], "CODEX_CREDITS_EXHAUSTED")
        self.assertEqual(report["rateLimitPauses"][0]["limitType"], "credits")


if __name__ == "__main__":
    unittest.main()
