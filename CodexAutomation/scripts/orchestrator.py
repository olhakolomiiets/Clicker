from __future__ import annotations

import argparse
from pathlib import Path

from codex_runner import PASS_VERDICT, run_smoke_test
from file_utils import read_json
from models import RepoContext, find_task, validate_smoke_task, validate_workflow
from preflight import find_repo_root, format_summary, run_preflight


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Codex Automation BOOTSTRAP orchestrator.")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Run safe preflight only. This is the default mode.",
    )
    parser.add_argument(
        "--smoke-test",
        action="store_true",
        help="Run BOOTSTRAP-02 read-only Codex CLI smoke test after preflight.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    script_path = Path(__file__).resolve()
    repo_root = find_repo_root(script_path.parent)
    context = RepoContext(
        root=repo_root,
        automation_root=repo_root / "CodexAutomation",
        dry_run=not args.smoke_test,
    )
    report = run_preflight(context)
    print(format_summary(report))
    if not args.smoke_test:
        return 0 if report["ok"] else 1

    if not report["ok"]:
        print("Smoke test skipped: preflight failed.")
        return 1

    config = read_json(context.automation_root / "config.json")
    workflow = read_json(context.automation_root / "workflow.seed.json")
    workflow_errors = validate_workflow(workflow)
    if workflow_errors:
        print("Smoke test skipped: workflow invalid.")
        for error in workflow_errors:
            print(f"  - {error}")
        return 1

    runner_config = config.get("codex_runner", {}) if isinstance(config, dict) else {}
    task_id = str(runner_config.get("test_task_id", "TEST-001"))
    task = find_task(workflow, task_id)
    if task is None:
        print(f"Smoke test skipped: task {task_id} was not found.")
        return 1

    task_errors = validate_smoke_task(task, task_id)
    if task_errors:
        print("Smoke test skipped: smoke task invalid.")
        for error in task_errors:
            print(f"  - {error}")
        return 1

    smoke_report = run_smoke_test(repo_root, context.automation_root, config, task)
    print("Codex Smoke Test")
    print(f"Task: {smoke_report['task_id']}")
    print(f"Verdict: {smoke_report['verdict']}")
    print(f"Error code: {smoke_report['error_code'] or 'none'}")
    print(f"Report: {smoke_report['paths']['run_report']}")
    if smoke_report["errors"]:
        print("Errors:")
        for error in smoke_report["errors"]:
            print(f"  - {error}")
    return 0 if smoke_report["verdict"] == PASS_VERDICT else 1


if __name__ == "__main__":
    raise SystemExit(main())
