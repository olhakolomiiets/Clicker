from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path
from typing import Any

from file_utils import write_json_atomic
from real_task_execution_self_test_models import SELF_TEST_MODE, SELF_TEST_REQUIRED_MESSAGE, SELF_TEST_TASK_ID
from workspace_inventory import is_symlink_or_reparse


GIT_TIMEOUT_SECONDS = 10


def create_self_test_fixture(root: Path, automation_root: Path, self_test_run_id: str) -> dict[str, Any]:
    runtime_root = (automation_root / "runtime" / "real_task_execution_self_tests").resolve(strict=False)
    self_root = runtime_root / self_test_run_id
    parent = self_root / "parent"
    fixture = self_root / "fixture"
    reports = self_root / "reports"
    for path in (runtime_root, self_root, parent, fixture, reports):
        _reject_reparse_parent(path)
    self_root.mkdir(parents=True, exist_ok=False)
    parent.mkdir()
    fixture.mkdir()
    reports.mkdir()
    _write_seed(parent)
    _init_git(parent)
    manifest = _manifest(parent)
    manifest_path = fixture / "controlled_task_manifest.json"
    write_json_atomic(manifest_path, manifest)
    return {
        "runtimeRoot": runtime_root,
        "selfTestRoot": self_root,
        "parent": parent,
        "fixture": fixture,
        "reports": reports,
        "manifestPath": manifest_path,
        "manifest": manifest,
    }


def self_test_config(base: dict[str, Any]) -> dict[str, Any]:
    copied = json.loads(json.dumps(base))
    real_tasks = copied["realTasks"]
    if "TaskData" not in real_tasks["allowedSourceRoots"]:
        real_tasks["allowedSourceRoots"] = list(real_tasks["allowedSourceRoots"]) + ["TaskData"]
    return copied


def expected_changed_paths() -> list[str]:
    return ["TaskData/message.txt", "TaskData/result.json"]


def _write_seed(parent: Path) -> None:
    task_data = parent / "TaskData"
    task_data.mkdir()
    (task_data / "message.txt").write_text("Replace this controlled self-test placeholder.\n", encoding="utf-8", newline="\n")
    (task_data / "reference.txt").write_text(
        "Required stage: BOOTSTRAP-03B-2D\nRequired mode: controlled-real-model-self-test\n",
        encoding="utf-8",
        newline="\n",
    )
    (parent / ".gitignore").write_text("CodexAutomation/runtime/\n", encoding="utf-8", newline="\n")


def _manifest(parent: Path) -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "taskId": SELF_TEST_TASK_ID,
        "title": "Controlled real-model real-task execution self-test",
        "objective": (
            "Update the controlled self-test files. Replace TaskData/message.txt with the exact line "
            f"{SELF_TEST_REQUIRED_MESSAGE}. Create TaskData/result.json as valid JSON with status=PASS, "
            f"stage=BOOTSTRAP-03B-2D, mode={SELF_TEST_MODE}, and message={SELF_TEST_REQUIRED_MESSAGE}. "
            "Do not change any other task file. Do not use network access, install packages, run Unity, or modify Git metadata/service files."
        ),
        "taskType": "isolated_code_change",
        "sourcePaths": ["TaskData"],
        "allowedWritePaths": ["TaskData/message.txt", "TaskData/result.json"],
        "allowedDeletePaths": [],
        "expectedOutputs": [
            {"path": "TaskData/message.txt", "kind": "file", "required": True},
            {"path": "TaskData/result.json", "kind": "file", "required": True},
        ],
        "validationPlan": [
            {"id": "result-exists", "type": "file_exists", "path": "TaskData/result.json"},
            {"id": "forbidden-absent", "type": "file_absent", "path": "TaskData/forbidden.tmp"},
            {"id": "result-json-valid", "type": "json_valid", "path": "TaskData/result.json"},
            {"id": "status-pass", "type": "json_field_equals", "path": "TaskData/result.json", "jsonPointer": "/status", "expected": "PASS"},
            {"id": "stage-2d", "type": "json_field_equals", "path": "TaskData/result.json", "jsonPointer": "/stage", "expected": "BOOTSTRAP-03B-2D"},
            {"id": "mode-fixed", "type": "json_field_equals", "path": "TaskData/result.json", "jsonPointer": "/mode", "expected": SELF_TEST_MODE},
            {"id": "message-fixed-json", "type": "json_field_equals", "path": "TaskData/result.json", "jsonPointer": "/message", "expected": SELF_TEST_REQUIRED_MESSAGE},
            {"id": "message-text", "type": "text_contains", "path": "TaskData/message.txt", "text": SELF_TEST_REQUIRED_MESSAGE},
            {"id": "placeholder-gone", "type": "text_not_contains", "path": "TaskData/message.txt", "text": "Replace this controlled self-test placeholder."},
            {"id": "changed-exact", "type": "changed_paths_exact", "paths": ["TaskData/message.txt", "TaskData/result.json"]},
            {"id": "no-unexpected", "type": "no_unexpected_files"},
            {"id": "no-conflicts", "type": "no_conflict_markers", "paths": ["TaskData/message.txt", "TaskData/result.json"]},
            {"id": "message-size", "type": "max_file_size", "path": "TaskData/message.txt", "maxBytes": 1024},
            {"id": "changed-count", "type": "max_changed_files", "maxFiles": 2},
            {"id": "extensions", "type": "extension_allowlist", "extensions": [".txt", ".json"]},
        ],
        "completionCriteria": [
            f"TaskData/message.txt contains exactly {SELF_TEST_REQUIRED_MESSAGE}.",
            "TaskData/result.json contains the fixed PASS fields.",
            "Only TaskData/message.txt and TaskData/result.json changed.",
        ],
        "roleBudget": {"maxInvocations": 3},
        "repairPolicy": {"maxAttempts": 1},
        "limits": {"maxChangedFiles": 2, "maxChangedBytes": 8192, "maxSingleChangedFileBytes": 4096},
        "metadata": {"selfTest": True, "parentRepository": str(parent.resolve(strict=False))},
    }


def _init_git(parent: Path) -> None:
    _git(parent, ["git", "init"])
    _git(parent, ["git", "config", "--local", "user.name", "Controlled Self Test"])
    _git(parent, ["git", "config", "--local", "user.email", "controlled-self-test@example.invalid"])
    _git(parent, ["git", "add", "--", ".gitignore", "TaskData/message.txt", "TaskData/reference.txt"])
    _git(parent, ["git", "commit", "-m", "seed controlled self-test task"])


def _git(cwd: Path, argv: list[str]) -> None:
    allowed = {
        ("git", "init"),
        ("git", "config", "--local", "user.name", "Controlled Self Test"),
        ("git", "config", "--local", "user.email", "controlled-self-test@example.invalid"),
        ("git", "add", "--", ".gitignore", "TaskData/message.txt", "TaskData/reference.txt"),
        ("git", "commit", "-m", "seed controlled self-test task"),
    }
    if tuple(argv) not in allowed:
        raise RuntimeError("unsupported synthetic git command")
    completed = subprocess.run(argv, cwd=str(cwd), capture_output=True, text=True, check=False, timeout=GIT_TIMEOUT_SECONDS)
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr.strip() or "synthetic git command failed")


def _reject_reparse_parent(path: Path) -> None:
    current = Path(path.anchor)
    for part in path.parts[1:]:
        current = current / part
        if current.exists() and is_symlink_or_reparse(current):
            raise RuntimeError("self-test runtime path contains a symlink or reparse component")
