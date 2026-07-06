from __future__ import annotations

import hashlib
import secrets
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from real_task_execution_models import REAL_TASK_EXECUTION_SELF_TEST_STAGE, canonical_sha256, utc_now


SELF_TEST_TASK_ID = "bootstrap-03b-2d-controlled-real-model-self-test"
SELF_TEST_REQUIRED_MESSAGE = "BOOTSTRAP-03B-2D CONTROLLED REAL MODEL SELF TEST PASSED"
SELF_TEST_MODE = "controlled-real-model-self-test"
SELF_TEST_REPORT_NAME = "REAL_TASK_EXECUTION_SELF_TEST_REPORT.json"


@dataclass(frozen=True)
class RealTaskExecutionSelfTestHandle:
    selfTestRunId: str
    taskId: str
    manifestSha256: str
    syntheticParentIdentityHash: str
    selfTestRoot: str
    capability: str


def new_self_test_run_id() -> str:
    stamp = utc_now().replace(":", "").replace("-", "").replace(".", "")
    return f"rtes_{stamp}_{secrets.token_hex(4)}"


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def snapshot_hash(snapshot: Any) -> str:
    return canonical_sha256(snapshot)


def stage() -> str:
    return REAL_TASK_EXECUTION_SELF_TEST_STAGE
