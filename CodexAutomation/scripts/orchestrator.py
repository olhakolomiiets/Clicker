from __future__ import annotations

import argparse
from pathlib import Path

from codex_runner import PASS_VERDICT, run_smoke_test
from file_utils import read_json
from models import RepoContext, find_task, validate_smoke_task, validate_workflow
from pipeline_engine import run_pipeline_self_test, run_rate_limit_self_test
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
    parser.add_argument(
        "--pipeline-self-test",
        action="store_true",
        help="Run BOOTSTRAP-03A local fake pipeline self-test without Codex.",
    )
    parser.add_argument(
        "--pipeline-rate-limit-self-test",
        action="store_true",
        help="Run BOOTSTRAP-03A local fake rate-limit scenarios without Codex.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    script_path = Path(__file__).resolve()
    repo_root = find_repo_root(script_path.parent)
    context = RepoContext(
        root=repo_root,
        automation_root=repo_root / "CodexAutomation",
        dry_run=not (args.smoke_test or args.pipeline_self_test or args.pipeline_rate_limit_self_test),
    )
    report = run_preflight(context)
    print(format_summary(report))
    if not (args.smoke_test or args.pipeline_self_test or args.pipeline_rate_limit_self_test):
        return 0 if report["ok"] else 1

    if not report["ok"]:
        print("Requested run skipped: preflight failed.")
        return 1

    config = read_json(context.automation_root / "config.json")
    if args.pipeline_self_test:
        task_path = context.automation_root / "tests" / "fixtures" / "PIPELINE-TEST-001.json"
        pipeline_report = run_pipeline_self_test(repo_root, context.automation_root, config, task_path)
        print("Pipeline Self Test")
        print(f"Final state: {pipeline_report['finalState']}")
        print(f"Final verdict: {pipeline_report['finalVerdict']}")
        print(f"Report: {pipeline_report['pipelineRunId']}")
        return 0 if pipeline_report["finalState"] == "COMPLETED" and pipeline_report["finalVerdict"] == "PASS" else 1

    if args.pipeline_rate_limit_self_test:
        task_path = context.automation_root / "tests" / "fixtures" / "PIPELINE-TEST-001.json"
        rate_report = run_rate_limit_self_test(repo_root, context.automation_root, config, task_path)
        print("Pipeline Rate-limit Self Test")
        print(f"All scenarios passed: {rate_report['allPassed']}")
        print(f"Real Codex started: {rate_report['realCodexStarted']}")
        print(f"Sleep performed: {rate_report['sleepPerformed']}")
        print(f"Report: {rate_report['pipelineRunId']}")
        return 0 if rate_report["allPassed"] else 1

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
