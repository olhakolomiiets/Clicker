from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from codex_event_parser import classify_rate_limit_event, classify_schema_invalid_text, parse_jsonl_events  # noqa: E402


class CodexEventParserCheck(unittest.TestCase):
    def test_valid_jsonl_events_summary(self) -> None:
        text = "\n".join(
            [
                json.dumps({"type": "turn.started", "thread_id": "thread-1"}),
                json.dumps({"type": "turn.completed", "usage": {"input_tokens": 1}}),
            ]
        )
        summary = parse_jsonl_events(text)
        self.assertEqual(summary["eventCount"], 2)
        self.assertEqual(summary["threadId"], "thread-1")
        self.assertTrue(summary["turnStarted"])
        self.assertTrue(summary["turnCompleted"])
        self.assertFalse(summary["parseErrors"])

    def test_invalid_jsonl_line(self) -> None:
        summary = parse_jsonl_events("{bad json")
        self.assertEqual(summary["parseErrors"][0]["code"], "CODEX_JSONL_INVALID_LINE")

    def test_unknown_event_type_is_recorded(self) -> None:
        summary = parse_jsonl_events(json.dumps({"type": "new.event"}))
        self.assertIn("new.event", summary["unknownEventTypes"])
        self.assertFalse(summary["parseErrors"])

    def test_error_event_not_ignored(self) -> None:
        summary = parse_jsonl_events(json.dumps({"type": "turn.failed", "error": {"code": "SOME_ERROR"}}))
        self.assertTrue(summary["turnFailed"])
        self.assertEqual(summary["errorEventCount"], 1)
        self.assertIsNotNone(summary["unclassifiedError"])

    def test_structured_rate_limit_is_classified(self) -> None:
        event = {"type": "turn.failed", "error": {"code": "rate_limit_exceeded", "retry_after_seconds": 30}}
        rate_limit = classify_rate_limit_event(event)
        self.assertIsNotNone(rate_limit)
        self.assertEqual(rate_limit["errorCode"], "CODEX_USAGE_LIMIT_REACHED")
        self.assertEqual(rate_limit["retryAfterSeconds"], 30)

    def test_plain_text_limit_word_is_not_rate_limit(self) -> None:
        event = {"type": "turn.failed", "message": "the word limit appears in ordinary text"}
        self.assertIsNone(classify_rate_limit_event(event))

    def test_invalid_response_schema_error_is_classified(self) -> None:
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
        text = "\n".join(
            [
                json.dumps({"type": "thread.started", "thread_id": "thread-1"}),
                json.dumps({"type": "turn.started"}),
                json.dumps({"type": "error", "message": json.dumps(nested_error)}),
                json.dumps({"type": "turn.failed", "error": {"message": json.dumps(nested_error)}}),
            ]
        )
        summary = parse_jsonl_events(text)
        self.assertTrue(summary["turnFailed"])
        self.assertTrue(summary["schemaInvalid"])
        self.assertEqual(summary["schemaErrorCode"], "invalid_json_schema")
        self.assertIn("blockedReason", summary["schemaErrorMessage"])
        self.assertIsNone(summary["rateLimit"])
        self.assertIsNone(summary["unclassifiedError"])

    def test_invalid_response_schema_can_be_classified_from_stderr(self) -> None:
        error = classify_schema_invalid_text(
            "invalid_json_schema: Invalid schema for response_format 'codex_output_schema': schema must have a type key."
        )
        self.assertIsNotNone(error)
        self.assertEqual(error["errorCode"], "CODEX_EXEC_SCHEMA_INVALID")
        self.assertEqual(error["schemaErrorCode"], "invalid_json_schema")

    def test_unknown_turn_failure_remains_unclassified(self) -> None:
        summary = parse_jsonl_events(json.dumps({"type": "turn.failed", "error": {"code": "SOME_NEW_ERROR", "message": "unknown"}}))
        self.assertTrue(summary["turnFailed"])
        self.assertFalse(summary["schemaInvalid"])
        self.assertIsNotNone(summary["unclassifiedError"])


if __name__ == "__main__":
    unittest.main()
