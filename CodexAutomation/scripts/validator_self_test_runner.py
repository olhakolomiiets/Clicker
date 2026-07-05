from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from file_utils import write_json_atomic
from real_task_change_runner import begin_change_tracking, finalize_change_analysis
from real_task_foundation_runner import prepare_foundation_workspace
from real_task_validation_runner import create_validation_prerequisite_handle, execute_validation_plan
from schema_validator import validate
from validator_models import REAL_TASK_VALIDATION_STAGE, canonical_sha256
from workspace_inventory import is_symlink_or_reparse


def run_validator_self_test(root: Path, automation_root: Path, config: dict[str, Any]) -> dict[str, Any]:
    run_id = "vst_" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    runtime_root = automation_root / "runtime" / "real_task_validator_self_tests" / run_id
    runtime_root.mkdir(parents=True)
    cases = [
        _run_case(root, automation_root, config, runtime_root, "pass", "PASS"),
        _run_case(root, automation_root, config, runtime_root, "fail", "FAIL"),
    ]
    all_matched = all(case["matched"] for case in cases)
    report = {
        "reportVersion": 1,
        "selfTestRunId": run_id,
        "stage": REAL_TASK_VALIDATION_STAGE,
        "cases": cases,
        "expectedCaseCount": 2,
        "actualCaseCount": len(cases),
        "allExpectedOutcomesMatched": all_matched,
        "codexInspected": False,
        "codexInvocationCount": 0,
        "modelInvocationStarted": False,
        "sandboxStarted": False,
        "unityStarted": False,
        "networkUsed": False,
        "finalVerdict": "PASS" if all_matched else "FAIL",
        "errorCode": None if all_matched else "VALIDATOR_SELF_TEST_MISMATCH",
        "errorMessage": None if all_matched else "One or more validator self-test cases did not match expected results.",
    }
    report_path = runtime_root / "VALIDATOR_SELF_TEST_REPORT.json"
    errors = validate(report, json.loads((automation_root / "schemas" / "validator_self_test_report.schema.json").read_text(encoding="utf-8")))
    if errors:
        raise RuntimeError(f"self-test report schema invalid: {errors[0]}")
    write_json_atomic(report_path, report)
    report["reportPath"] = str(report_path)
    return report


def _run_case(root: Path, automation_root: Path, config: dict[str, Any], runtime_root: Path, case_id: str, expected_verdict: str) -> dict[str, Any]:
    parent = runtime_root / case_id / "parent"
    _create_parent(parent, case_id)
    manifest = _write_manifest(parent, case_id)
    prep = prepare_foundation_workspace(parent, automation_root, config, manifest, f"foundation_validator_{case_id}_12345678")
    handle = begin_change_tracking(prep, parent, automation_root, config)
    workspace = Path(handle.workspaceDirectory)
    if case_id == "pass":
        (workspace / "Assets" / "VST" / "input.txt").write_text("READY changed\n", encoding="utf-8")
        (workspace / "Assets" / "VST" / "result.json").write_text('{"status":"ok","count":1}\n', encoding="utf-8")
    else:
        (workspace / "Assets" / "VST" / "input.txt").write_text("READY\n<<<<<<< conflict\nFORBIDDEN\n", encoding="utf-8")
        (workspace / "Assets" / "VST" / "result.json").write_text('{"status":"ok","count":1}\n', encoding="utf-8")
    change = finalize_change_analysis(handle, parent, automation_root, config)
    prerequisite = create_validation_prerequisite_handle(change, parent, config)
    validation = execute_validation_plan(prerequisite, parent, automation_root, config)
    report = validation.report
    actual_failed_types = [item["validatorType"] for item in report["results"] if item["status"] == "FAIL"]
    expected_failed_types = [] if expected_verdict == "PASS" else [
        "json_field_equals",
        "text_not_contains",
        "changed_paths_exact",
        "no_conflict_markers",
        "max_file_size",
        "extension_allowlist",
    ]
    matched = (
        validation.finalVerdict == expected_verdict
        and sorted(actual_failed_types) == sorted(expected_failed_types)
        and report["blockedCount"] == 0
    )
    return {
        "caseId": case_id,
        "expectedVerdict": expected_verdict,
        "actualVerdict": validation.finalVerdict,
        "expectedValidatorCount": 14,
        "actualValidatorCount": len(report["results"]),
        "expectedPassCount": 14 if expected_verdict == "PASS" else 8,
        "actualPassCount": report["passCount"],
        "expectedFailCount": 0 if expected_verdict == "PASS" else 6,
        "actualFailCount": report["failCount"],
        "expectedBlockedCount": 0,
        "actualBlockedCount": report["blockedCount"],
        "expectedFailedTypes": expected_failed_types,
        "actualFailedTypes": actual_failed_types,
        "finalValidationReportPath": validation.reportPath,
        "finalValidationReportHash": canonical_sha256(report),
        "matched": matched,
    }


def _create_parent(parent: Path, case_id: str) -> None:
    fixture = parent / "Assets" / "VST"
    fixture.mkdir(parents=True)
    text = "READY\n" if case_id == "pass_all" else "READY\n"
    (fixture / "input.txt").write_text(text, encoding="utf-8")
    (fixture / "data.json").write_text('{"seed": true}\n', encoding="utf-8")
    (parent / "AGENTS.md").write_text("# Synthetic Self Test\n", encoding="utf-8")
    (parent / "CodexAutomation").mkdir()
    (parent / "CodexAutomation" / ".gitignore").write_text("runtime/\n", encoding="utf-8")
    _git(parent, ["git", "init"], parent)
    _git(parent, ["git", "config", "--local", "user.email", "validator@example.invalid"], parent)
    _git(parent, ["git", "config", "--local", "user.name", "Validator Self Test"], parent)
    _git(parent, ["git", "add", "--", "AGENTS.md", "Assets", "CodexAutomation/.gitignore"], parent)
    _git(parent, ["git", "commit", "-m", "synthetic validator baseline"], parent)


def _write_manifest(parent: Path, case_id: str) -> Path:
    base = "Assets/VST"
    exact_paths = [f"{base}/input.txt", f"{base}/result.json"] if case_id == "pass" else [f"{base}/input.txt"]
    max_bytes = 128 if case_id == "pass" else 1
    extensions = [".txt", ".json"] if case_id == "pass" else [".json"]
    expected_status = "ok" if case_id == "pass" else "ready"
    manifest = {
        "schemaVersion": 1,
        "taskId": "TASK-VALIDATOR-SELF-TEST",
        "title": "Validator Self Test",
        "objective": "Run the controlled no-model validator self-test.",
        "taskType": "isolated_code_change",
        "sourcePaths": [base],
        "allowedWritePaths": [f"{base}/input.txt", f"{base}/result.json", f"{base}/missing.json"],
        "allowedDeletePaths": [],
        "expectedOutputs": [{"path": f"{base}/result.json", "kind": "file", "required": True}],
        "validationPlan": [
            {"id": "v001", "type": "file_exists", "path": f"{base}/result.json"},
            {"id": "v002", "type": "file_absent", "path": f"{base}/missing.json"},
            {"id": "v003", "type": "json_valid", "path": f"{base}/result.json"},
            {"id": "v004", "type": "json_schema", "path": f"{base}/result.json", "schemaName": "validator_self_test_result_v1"},
            {"id": "v005", "type": "json_field_equals", "path": f"{base}/result.json", "jsonPointer": "/status", "expected": expected_status},
            {"id": "v006", "type": "text_contains", "path": f"{base}/input.txt", "text": "READY"},
            {"id": "v007", "type": "text_not_contains", "path": f"{base}/input.txt", "text": "FORBIDDEN"},
            {"id": "v008", "type": "changed_paths_exact", "paths": exact_paths},
            {"id": "v009", "type": "changed_paths_subset", "paths": [f"{base}/input.txt", f"{base}/result.json"]},
            {"id": "v010", "type": "no_unexpected_files"},
            {"id": "v011", "type": "no_conflict_markers"},
            {"id": "v012", "type": "max_file_size", "path": f"{base}/result.json", "maxBytes": max_bytes},
            {"id": "v013", "type": "max_changed_files", "maxFiles": 2},
            {"id": "v014", "type": "extension_allowlist", "extensions": extensions},
        ],
        "completionCriteria": ["Validator self-test completes."],
        "roleBudget": {"maxInvocations": 3},
        "repairPolicy": {"maxAttempts": 1},
        "limits": {"maxChangedFiles": 50, "maxChangedBytes": 10485760, "maxSingleChangedFileBytes": 2097152},
        "metadata": {},
    }
    path = parent / "task_manifest.json"
    path.write_text(json.dumps(manifest), encoding="utf-8")
    return path


def _git(cwd: Path, command: list[str], expected_parent_root: Path) -> None:
    resolved = _validate_synthetic_git_cwd(cwd, expected_parent_root)
    allowed = {
        ("git", "init"),
        ("git", "config", "--local", "user.email", "validator@example.invalid"),
        ("git", "config", "--local", "user.name", "Validator Self Test"),
        ("git", "add", "--", "AGENTS.md", "Assets", "CodexAutomation/.gitignore"),
        ("git", "commit", "-m", "synthetic validator baseline"),
    }
    if tuple(command) not in allowed:
        raise RuntimeError("unsupported synthetic git command.")
    completed = subprocess.run(command, cwd=str(resolved), capture_output=True, text=True, check=False, timeout=10)
    if completed.returncode != 0:
        raise RuntimeError(f"synthetic git command failed: {' '.join(command)}")


def _validate_synthetic_git_cwd(cwd: Path, expected_parent_root: Path) -> Path:
    fixed_root = _fixed_self_test_root()
    fixed_resolved = fixed_root.resolve(strict=True)
    expected_resolved = expected_parent_root.resolve(strict=True)
    cwd_resolved = cwd.resolve(strict=True)
    try:
        expected_relative = expected_resolved.relative_to(fixed_resolved)
    except ValueError as exc:
        raise RuntimeError("synthetic git cwd is outside fixed validator self-test runtime.") from exc
    if len(expected_relative.parts) != 3 or expected_resolved.name != "parent":
        raise RuntimeError("synthetic git cwd is missing run/case/parent containment.")
    run_root = expected_resolved.parent.parent
    case_root = expected_resolved.parent
    if run_root == fixed_resolved or case_root == run_root or expected_resolved == case_root:
        raise RuntimeError("synthetic git cwd containment is invalid.")
    if run_root.parent != fixed_resolved or case_root.parent != run_root or expected_resolved.parent != case_root:
        raise RuntimeError("synthetic git cwd containment is invalid.")
    if cwd_resolved != expected_resolved:
        raise RuntimeError("synthetic git cwd must be the expected validator self-test parent.")
    _ensure_no_reparse_chain(fixed_root, fixed_root)
    _ensure_no_reparse_chain(expected_parent_root, fixed_root)
    _ensure_no_reparse_chain(cwd, fixed_root)
    return cwd_resolved


def _fixed_self_test_root() -> Path:
    automation_root = Path(__file__).resolve().parents[1]
    return automation_root / "runtime" / "real_task_validator_self_tests"


def _ensure_no_reparse_chain(path: Path, fixed_root: Path) -> None:
    fixed_resolved = fixed_root.resolve(strict=True)
    resolved = path.resolve(strict=True)
    try:
        relative = resolved.relative_to(fixed_resolved)
    except ValueError as exc:
        raise RuntimeError("synthetic git cwd resolved outside fixed validator self-test runtime.") from exc
    if is_symlink_or_reparse(fixed_root):
        raise RuntimeError("synthetic git cwd component is symlink or reparse point.")
    try:
        lexical_relative = path.relative_to(fixed_root)
    except ValueError:
        lexical_relative = ()
    current_lexical = fixed_root
    for part in getattr(lexical_relative, "parts", ()):
        current_lexical = current_lexical / part
        if is_symlink_or_reparse(current_lexical):
            raise RuntimeError("synthetic git cwd component is symlink or reparse point.")
    current = fixed_resolved
    for part in relative.parts:
        current = current / part
        if is_symlink_or_reparse(current):
            raise RuntimeError("synthetic git cwd component is symlink or reparse point.")
