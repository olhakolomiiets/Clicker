from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import math
from typing import Any


RATE_LIMIT_CODES = {
    "CODEX_USAGE_LIMIT_REACHED",
    "CODEX_WEEKLY_LIMIT_REACHED",
    "CODEX_CREDITS_EXHAUSTED",
    "CODEX_RATE_LIMIT_RESET_UNKNOWN",
    "CODEX_RATE_LIMIT_RETRY_EXHAUSTED",
    "CODEX_RATE_LIMIT_DATA_INVALID",
    "PIPELINE_PAUSED_RATE_LIMIT",
}

LIMIT_TYPES = {"five_hour_window", "weekly", "credits", "unknown"}


@dataclass(frozen=True)
class UsageLimit:
    limitType: str
    detectedAtUtc: str
    resetAtUtc: str | None
    retryAfterSeconds: int | float | None
    message: str
    source: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "limitType": self.limitType,
            "detectedAtUtc": self.detectedAtUtc,
            "resetAtUtc": self.resetAtUtc,
            "retryAfterSeconds": self.retryAfterSeconds,
            "message": self.message,
            "source": self.source,
        }


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def validate_usage_limit(data: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if not isinstance(data, dict):
        return ["CODEX_RATE_LIMIT_DATA_INVALID: usageLimit must be an object."]
    if data.get("limitType") not in LIMIT_TYPES:
        errors.append("CODEX_RATE_LIMIT_DATA_INVALID: limitType is invalid.")
    for name in ("message", "source"):
        if not isinstance(data.get(name), str) or not data[name].strip():
            errors.append(f"CODEX_RATE_LIMIT_DATA_INVALID: {name} must be a non-empty string.")
    detected_at = _parse_utc_timestamp(data.get("detectedAtUtc"))
    if detected_at is None:
        errors.append("CODEX_RATE_LIMIT_DATA_INVALID: detectedAtUtc must be a UTC ISO timestamp.")
    reset_at = data.get("resetAtUtc")
    parsed_reset = None
    if reset_at is not None:
        parsed_reset = _parse_utc_timestamp(reset_at)
        if parsed_reset is None:
            errors.append("CODEX_RATE_LIMIT_DATA_INVALID: resetAtUtc must be null or a UTC ISO timestamp.")
    if detected_at is not None and parsed_reset is not None and parsed_reset < detected_at:
        errors.append("CODEX_RATE_LIMIT_DATA_INVALID: resetAtUtc cannot be earlier than detectedAtUtc.")
    retry_after = data.get("retryAfterSeconds")
    if retry_after is not None:
        if (
            not isinstance(retry_after, (int, float))
            or isinstance(retry_after, bool)
            or not math.isfinite(float(retry_after))
            or retry_after < 0
        ):
            errors.append("CODEX_RATE_LIMIT_DATA_INVALID: retryAfterSeconds must be null or a non-negative number.")
    return errors


def _parse_utc_timestamp(value: Any) -> datetime | None:
    if not isinstance(value, str) or not value.strip():
        return None
    text = value.strip()
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(text)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    if parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        return None
    return parsed.astimezone(timezone.utc)


def fake_usage_limit(limit_type: str = "five_hour_window", reset_at: str | None = None) -> dict[str, Any]:
    code = {
        "five_hour_window": "CODEX_USAGE_LIMIT_REACHED",
        "weekly": "CODEX_WEEKLY_LIMIT_REACHED",
        "credits": "CODEX_CREDITS_EXHAUSTED",
        "unknown": "CODEX_RATE_LIMIT_RESET_UNKNOWN",
    }.get(limit_type, "CODEX_RATE_LIMIT_DATA_INVALID")
    return {
        "limit": UsageLimit(
            limitType=limit_type,
            detectedAtUtc=utc_now(),
            resetAtUtc=reset_at,
            retryAfterSeconds=None,
            message="Fake usage limit",
            source="fake_adapter",
        ).to_dict(),
        "errorCode": code,
    }
