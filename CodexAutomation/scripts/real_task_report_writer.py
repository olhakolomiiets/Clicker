from __future__ import annotations

import json
import os
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from file_utils import read_json
from real_task_execution_models import RealTaskExecutionFailure, canonical_json_bytes, canonical_sha256
from schema_validator import validate


@dataclass(frozen=True)
class TrustedReportReceipt:
    relativePath: str
    size: int
    sha256: str
    schemaName: str
    reportVersion: int

    def to_dict(self) -> dict[str, Any]:
        return {
            "relativePath": self.relativePath,
            "size": self.size,
            "sha256": self.sha256,
            "schemaName": self.schemaName,
            "reportVersion": self.reportVersion,
        }


def write_trusted_report(
    automation_root: Path,
    runtime_root: Path,
    path: Path,
    payload: dict[str, Any],
    schema_name: str,
    semantic_validator: Callable[[dict[str, Any]], list[str]] | None = None,
    max_bytes: int = 4_194_304,
) -> TrustedReportReceipt:
    final_path = path.resolve(strict=False)
    runtime = runtime_root.resolve(strict=False)
    try:
        final_path.relative_to(runtime)
    except ValueError as exc:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_WRITE_FAILED", "Report path must stay under runtime root.") from exc
    if _has_reparse_parent(final_path):
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_WRITE_FAILED", "Report path contains a symlink or reparse component.")
    if final_path.exists():
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_WRITE_FAILED", "Trusted report already exists.")
    _validate_payload(automation_root, payload, schema_name, semantic_validator)
    encoded = canonical_json_bytes(payload)
    if len(encoded) > max_bytes:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_WRITE_FAILED", "Trusted report exceeds size cap.")
    expected_hash = canonical_sha256(payload)
    final_path.parent.mkdir(parents=True, exist_ok=True)
    fd, temp_name = tempfile.mkstemp(prefix=f".{final_path.name}.", suffix=".tmp", dir=str(final_path.parent), text=False)
    temp_path = Path(temp_name)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(encoded)
            handle.write(b"\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temp_path, final_path)
    except Exception:
        try:
            temp_path.unlink(missing_ok=True)
        finally:
            raise
    reread = _read_json_no_duplicates(final_path)
    _validate_payload(automation_root, reread, schema_name, semantic_validator)
    if canonical_sha256(reread) != expected_hash:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_WRITE_FAILED", "Trusted report reread hash mismatch.")
    try:
        size = final_path.stat().st_size
    except OSError as exc:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", f"Trusted report stat failed: {exc}") from exc
    if size > max_bytes:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_WRITE_FAILED", "Trusted report reread exceeds size cap.")
    return TrustedReportReceipt(
        relativePath=final_path.relative_to(runtime).as_posix(),
        size=size,
        sha256=expected_hash,
        schemaName=schema_name,
        reportVersion=int(reread.get("reportVersion", 0)),
    )


def read_trusted_report(
    automation_root: Path,
    runtime_root: Path,
    path: Path,
    receipt: TrustedReportReceipt,
    schema_name: str,
    semantic_validator: Callable[[dict[str, Any]], list[str]] | None = None,
    max_bytes: int = 4_194_304,
) -> dict[str, Any]:
    final_path = path.resolve(strict=False)
    runtime = runtime_root.resolve(strict=False)
    try:
        relative_path = final_path.relative_to(runtime).as_posix()
    except ValueError as exc:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", "Trusted report path must stay under runtime root.") from exc
    if relative_path != receipt.relativePath or schema_name != receipt.schemaName:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", "Trusted report receipt path or schema mismatch.")
    if _has_reparse_parent(final_path):
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", "Trusted report path contains a symlink or reparse component.")
    size = final_path.stat().st_size
    if size > max_bytes or size != receipt.size:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", "Trusted report size mismatch.")
    payload = _read_json_no_duplicates(final_path)
    _validate_payload(automation_root, payload, schema_name, semantic_validator)
    if canonical_sha256(payload) != receipt.sha256:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", "Trusted report artifact hash mismatch.")
    if int(payload.get("reportVersion", 0)) != receipt.reportVersion:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", "Trusted report version mismatch.")
    return payload


def _validate_payload(
    automation_root: Path,
    payload: dict[str, Any],
    schema_name: str,
    semantic_validator: Callable[[dict[str, Any]], list[str]] | None,
) -> None:
    schema_errors = validate(payload, read_json(automation_root / "schemas" / schema_name))
    if schema_errors:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", schema_errors[0])
    if semantic_validator is not None:
        semantic_errors = semantic_validator(payload)
        if semantic_errors:
            raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", semantic_errors[0])


def _read_json_no_duplicates(path: Path) -> dict[str, Any]:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", f"Duplicate JSON key in trusted report: {key}")
            result[key] = value
        return result

    try:
        loaded = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=reject_duplicates)
    except RealTaskExecutionFailure:
        raise
    except Exception as exc:
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", f"Trusted report JSON reread failed: {exc}") from exc
    if not isinstance(loaded, dict):
        raise RealTaskExecutionFailure("REAL_TASK_FINAL_REPORT_INVALID", "Trusted report root must be an object.")
    return loaded


def _has_reparse_parent(path: Path) -> bool:
    current = Path(path.anchor)
    for part in path.parts[1:-1]:
        current = current / part
        if not current.exists():
            continue
        try:
            if current.is_symlink():
                return True
            attrs = getattr(os.stat(current, follow_symlinks=False), "st_file_attributes", 0)
            if attrs & 0x400:
                return True
        except OSError:
            return True
    return False
