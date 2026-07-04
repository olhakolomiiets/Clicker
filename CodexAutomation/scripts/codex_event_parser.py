from __future__ import annotations

import json
from collections.abc import Iterator
from typing import Any


RATE_LIMIT_CODES = {
    "CODEX_USAGE_LIMIT_REACHED",
    "CODEX_WEEKLY_LIMIT_REACHED",
    "CODEX_CREDITS_EXHAUSTED",
    "rate_limit_exceeded",
    "usage_limit_reached",
    "credits_exhausted",
}
KNOWN_EVENT_TYPES = {
    "turn.started",
    "turn_started",
    "turn-started",
    "turn.completed",
    "turn_completed",
    "turn-completed",
    "turn.failed",
    "turn_failed",
    "turn-failed",
    "response.failed",
    "item.failed",
    "error",
}


def parse_jsonl_events(text: str) -> dict[str, Any]:
    summary: dict[str, Any] = {
        "eventCount": 0,
        "eventTypes": [],
        "threadId": None,
        "turnStarted": False,
        "turnCompleted": False,
        "turnFailed": False,
        "errorEventCount": 0,
        "usage": [],
        "unknownEventTypes": [],
        "parseErrors": [],
        "rateLimit": None,
        "schemaInvalid": False,
        "schemaErrorCode": None,
        "schemaErrorMessage": None,
        "unclassifiedError": None,
    }
    event_types: set[str] = set()
    unknown_types: set[str] = set()
    for line_number, line in enumerate(text.splitlines(), start=1):
        if not line.strip():
            continue
        try:
            event = json.loads(line)
        except json.JSONDecodeError as exc:
            summary["parseErrors"].append({"code": "CODEX_JSONL_INVALID_LINE", "line": line_number, "message": str(exc)})
            continue
        if not isinstance(event, dict):
            summary["parseErrors"].append({"code": "CODEX_JSONL_INVALID_LINE", "line": line_number, "message": "line is not a JSON object"})
            continue
        summary["eventCount"] += 1
        event_type = str(event.get("type") or event.get("event") or event.get("kind") or "unknown")
        event_types.add(event_type)
        if event_type not in KNOWN_EVENT_TYPES:
            unknown_types.add(event_type)
        if summary["threadId"] is None:
            summary["threadId"] = event.get("thread_id") or event.get("threadId") or event.get("conversation_id")
        if event_type in {"turn.started", "turn_started", "turn-started"}:
            summary["turnStarted"] = True
        if event_type in {"turn.completed", "turn_completed", "turn-completed"}:
            summary["turnCompleted"] = True
        if event_type in {"turn.failed", "turn_failed", "turn-failed", "response.failed", "item.failed", "error"} or "error" in event:
            summary["turnFailed"] = event_type != "error" or True
            summary["errorEventCount"] += 1
            rate_limit = classify_rate_limit_event(event)
            if rate_limit is not None:
                summary["rateLimit"] = rate_limit
            else:
                schema_invalid = classify_schema_invalid_event(event)
                if schema_invalid is not None:
                    summary["schemaInvalid"] = True
                    summary["schemaErrorCode"] = schema_invalid["schemaErrorCode"]
                    summary["schemaErrorMessage"] = schema_invalid["schemaErrorMessage"]
                elif summary["unclassifiedError"] is None:
                    summary["unclassifiedError"] = _safe_error_summary(event)
        usage = event.get("usage")
        if isinstance(usage, dict):
            summary["usage"].append(usage)
    summary["eventTypes"] = sorted(event_types)
    summary["unknownEventTypes"] = sorted(unknown_types)
    return summary


def classify_schema_invalid_text(text: str) -> dict[str, Any] | None:
    stripped = text.strip()
    if not stripped:
        return None
    parsed = _parse_json_object_text(stripped)
    if parsed is not None:
        return classify_schema_invalid_event(parsed)
    return _schema_invalid_from_fields(None, stripped)


def classify_schema_invalid_event(event: dict[str, Any]) -> dict[str, Any] | None:
    for candidate in _iter_error_candidates(event):
        code = _string_value(candidate.get("code")) or _string_value(candidate.get("errorCode"))
        message = _string_value(candidate.get("message")) or _string_value(candidate.get("detail"))
        detected = _schema_invalid_from_fields(code, message)
        if detected is not None:
            return detected
    return None


def classify_rate_limit_event(event: dict[str, Any]) -> dict[str, Any] | None:
    candidates = [event]
    for key in ("error", "data", "details"):
        value = event.get(key)
        if isinstance(value, dict):
            candidates.append(value)
    for data in candidates:
        code = data.get("code") or data.get("errorCode") or data.get("type") or data.get("error_type")
        retry = data.get("retry_after_seconds", data.get("retryAfterSeconds", data.get("retry_after")))
        reset = data.get("reset_at", data.get("resetAt"))
        if code in RATE_LIMIT_CODES or retry is not None or reset is not None:
            return {
                "limitType": _limit_type_from_code(str(code or "unknown")),
                "errorCode": _error_code_from_code(str(code or "CODEX_USAGE_LIMIT_REACHED")),
                "message": str(data.get("message") or event.get("message") or "Structured Codex usage limit event."),
                "retryAfterSeconds": retry if isinstance(retry, (int, float)) and not isinstance(retry, bool) else None,
                "resetAtUtc": reset if isinstance(reset, str) and reset.strip() else None,
            }
    return None


def _iter_error_candidates(event: dict[str, Any]) -> Iterator[dict[str, Any]]:
    stack: list[Any] = [event]
    seen: set[int] = set()
    while stack:
        value = stack.pop()
        if not isinstance(value, dict):
            continue
        marker = id(value)
        if marker in seen:
            continue
        seen.add(marker)
        yield value
        for key in ("error", "data", "details", "item", "response"):
            child = value.get(key)
            if isinstance(child, dict):
                stack.append(child)
        message = value.get("message")
        if isinstance(message, str):
            parsed = _parse_json_object_text(message)
            if parsed is not None:
                stack.append(parsed)


def _parse_json_object_text(text: str) -> dict[str, Any] | None:
    stripped = text.strip()
    if not stripped.startswith("{"):
        return None
    try:
        parsed = json.loads(stripped)
    except json.JSONDecodeError:
        return None
    return parsed if isinstance(parsed, dict) else None


def _schema_invalid_from_fields(code: str | None, message: str | None) -> dict[str, Any] | None:
    haystack = " ".join(part for part in (code, message) if part).lower()
    if not haystack:
        return None
    schema_invalid = (
        "invalid_json_schema" in haystack
        or "invalid schema for response_format" in haystack
        or "schema must have a type key" in haystack
        or "schema must have a 'type' key" in haystack
    )
    if not schema_invalid:
        return None
    return {
        "errorCode": "CODEX_EXEC_SCHEMA_INVALID",
        "schemaErrorCode": code or "invalid_json_schema",
        "schemaErrorMessage": _short_message(message or code or "Invalid schema for response_format."),
    }


def _short_message(message: str, limit: int = 240) -> str:
    collapsed = " ".join(message.split())
    if len(collapsed) <= limit:
        return collapsed
    return collapsed[: limit - 3].rstrip() + "..."


def _string_value(value: Any) -> str | None:
    return value if isinstance(value, str) and value.strip() else None


def _error_code_from_code(code: str) -> str:
    lowered = code.lower()
    if "credit" in lowered:
        return "CODEX_CREDITS_EXHAUSTED"
    if "weekly" in lowered:
        return "CODEX_WEEKLY_LIMIT_REACHED"
    return "CODEX_USAGE_LIMIT_REACHED"


def _limit_type_from_code(code: str) -> str:
    lowered = code.lower()
    if "credit" in lowered:
        return "credits"
    if "weekly" in lowered:
        return "weekly"
    if "five" in lowered or "usage" in lowered or "rate" in lowered:
        return "five_hour_window"
    return "unknown"


def _safe_error_summary(event: dict[str, Any]) -> dict[str, Any]:
    error = event.get("error")
    if isinstance(error, dict):
        return {
            "type": error.get("type") or error.get("code"),
            "code": error.get("code") or error.get("errorCode"),
            "message": error.get("message"),
        }
    return {"type": event.get("type"), "code": event.get("code"), "message": event.get("message")}
