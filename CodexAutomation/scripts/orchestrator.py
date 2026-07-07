from __future__ import annotations

import argparse
import sys
from pathlib import Path

from codex_runner import PASS_VERDICT, run_smoke_test
from file_utils import read_json
from models import RepoContext, find_task, validate_smoke_task, validate_workflow
from pipeline_engine import run_pipeline_self_test, run_rate_limit_self_test
from preflight import find_repo_root, format_summary, run_preflight
from real_role_runner import run_real_role_plan, run_real_role_sandbox_probe, run_real_role_self_test
from real_task_execution_self_test import exit_code_for_self_test_verdict, run_real_task_execution_self_test
from real_task_public_runner import PUBLIC_REAL_TASK_INVALID_CLI, run_public_real_task
from task_manifest_validator import validate_task_manifest_file, write_real_task_plan
from validator_self_test_runner import run_validator_self_test


class PublicAwareArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        if _argv_has_public_real_task_intent(sys.argv[1:]):
            self.print_usage(sys.stderr)
            self.exit(PUBLIC_REAL_TASK_INVALID_CLI, f"{self.prog}: error: {message}\n")
        super().error(message)


def _argv_has_public_real_task_intent(argv: list[str]) -> bool:
    public_prefixes = ("--real-task-r", "--public-real-task", "--public-run")
    return any(arg == "--real-task-run" or arg.startswith(public_prefixes) for arg in argv)


def parse_args() -> argparse.Namespace:
    parser = PublicAwareArgumentParser(description="Codex Automation BOOTSTRAP orchestrator.", allow_abbrev=False)
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
    parser.add_argument(
        "--real-role-self-test-plan",
        action="store_true",
        help="Plan BOOTSTRAP-03B-1 real-role self-test without launching Codex.",
    )
    parser.add_argument(
        "--real-role-self-test",
        action="store_true",
        help="Run BOOTSTRAP-03B-1 isolated real-role Codex self-test.",
    )
    parser.add_argument(
        "--real-role-sandbox-probe",
        action="store_true",
        help="Run BOOTSTRAP-03B-1 local no-model Windows sandbox write probe.",
    )
    parser.add_argument(
        "--validate-task-manifest",
        metavar="MANIFEST",
        help="Validate a BOOTSTRAP-03B-2A generic real-task manifest without model, sandbox, workspace, or source copy.",
    )
    parser.add_argument(
        "--real-task-plan",
        metavar="MANIFEST",
        help="Create a BOOTSTRAP-03B-2A no-model real-task plan report without model, sandbox, workspace, or source copy.",
    )
    parser.add_argument(
        "--real-task-validator-self-test",
        action="store_true",
        help="Run BOOTSTRAP-03B-2B-C controlled no-model validator self-test.",
    )
    parser.add_argument(
        "--real-task-execution-self-test",
        action="count",
        default=0,
        help="Run BOOTSTRAP-03B-2D controlled fixed real-model real-task execution self-test.",
    )
    parser.add_argument(
        "--real-task-run",
        metavar="MANIFEST",
        help="Run BOOTSTRAP-03B-3A host-gated public generic real-task execution.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    selected_modes = [
        args.dry_run,
        args.smoke_test,
        args.pipeline_self_test,
        args.pipeline_rate_limit_self_test,
        args.real_role_self_test_plan,
        args.real_role_self_test,
        args.real_role_sandbox_probe,
        bool(args.validate_task_manifest),
        bool(args.real_task_plan),
        args.real_task_validator_self_test,
        bool(args.real_task_execution_self_test),
        bool(args.real_task_run),
    ]
    if sum(1 for selected in selected_modes if selected) > 1:
        print("Select exactly one explicit run mode.")
        return PUBLIC_REAL_TASK_INVALID_CLI if args.real_task_run else 2
    if args.real_task_execution_self_test > 1:
        print("--real-task-execution-self-test may be specified only once.")
        return 2
    script_path = Path(__file__).resolve()
    repo_root = find_repo_root(script_path.parent)
    explicit_mode = (
        args.smoke_test
        or args.pipeline_self_test
        or args.pipeline_rate_limit_self_test
        or args.real_role_self_test_plan
        or args.real_role_self_test
        or args.real_role_sandbox_probe
        or args.validate_task_manifest
        or args.real_task_plan
        or args.real_task_validator_self_test
        or args.real_task_execution_self_test
        or args.real_task_run
    )
    context = RepoContext(
        root=repo_root,
        automation_root=repo_root / "CodexAutomation",
        dry_run=not explicit_mode,
    )
    no_codex_preflight = bool(args.validate_task_manifest or args.real_task_plan or args.real_task_validator_self_test or args.real_task_run)
    report = run_preflight(context, inspect_codex=not no_codex_preflight)
    print(format_summary(report))
    if not explicit_mode:
        return 0 if report["ok"] else 1

    if not report["ok"]:
        print("Requested run skipped: preflight failed.")
        return 1

    config = read_json(context.automation_root / "config.json")
    if args.real_task_run:
        result = run_public_real_task(repo_root, context.automation_root, config, args.real_task_run)
        public_report = result.report
        print("Public Real Task Run")
        print(f"Public run: {public_report.get('publicRunId') or 'none'}")
        print(f"Task: {public_report.get('taskId') or 'unavailable'}")
        print(f"Manifest: {public_report.get('manifestRelativePath') or 'none'}")
        print(f"Production run: {public_report.get('productionOrchestrationRunId') or 'none'}")
        print(f"Final verdict: {public_report.get('finalVerdict') or result.finalVerdict}")
        print(f"Invocations: {public_report.get('invocationsUsed', 0)}")
        print(f"Repairs: {public_report.get('repairsUsed', 0)}")
        print(f"Auditor approved: {public_report.get('auditorApproved', False)}")
        print(f"Bundle manifest: {public_report.get('bundleRelativePath') or 'none'}")
        print(f"Eligible for apply: {public_report.get('eligibleForApply', False)}")
        print(f"Report: {Path(result.reportPath).resolve().relative_to(repo_root.resolve()).as_posix() if result.reportPath else 'none'}")
        print(f"Error code: {public_report.get('errorCode') or 'none'}")
        if public_report.get("errorMessage"):
            print(f"Error message: {public_report['errorMessage']}")
        return result.exitCode

    if args.validate_task_manifest:
        result = validate_task_manifest_file(repo_root, context.automation_root, config, Path(args.validate_task_manifest), inspect_sources=False)
        print("Real Task Manifest Validation")
        print(f"Valid: {result.ok}")
        print(f"Manifest SHA-256: {result.manifestSha256 or 'none'}")
        if result.errors:
            for error in result.errors:
                print(f"  - {error.code}: {error.message}")
        return 0 if result.ok else 1

    if args.real_task_plan:
        result = validate_task_manifest_file(repo_root, context.automation_root, config, Path(args.real_task_plan), inspect_sources=True)
        plan = result.effectivePlan or {}
        if plan:
            report_path, write_error = write_real_task_plan(repo_root, context.automation_root, plan)
            if write_error:
                print("Real Task Plan")
                print("Final verdict: FAILED")
                print(f"Error code: {write_error.code}")
                return 1
        else:
            report_path = None
        print("Real Task Plan")
        print(f"Final verdict: {plan.get('finalVerdict', 'FAILED')}")
        print(f"Manifest valid: {result.ok}")
        print(f"Model invocation started: {plan.get('modelInvocationStarted', False)}")
        print(f"Workspace created: {plan.get('workspaceCreated', False)}")
        print(f"Source copied: {plan.get('sourceCopied', False)}")
        print(f"Report: {report_path.resolve().relative_to(repo_root.resolve()).as_posix() if report_path else 'none'}")
        if result.errors:
            for error in result.errors:
                print(f"  - {error.code}: {error.message}")
        return 0 if result.ok and plan.get("finalVerdict") == "PASS" else 1

    if args.pipeline_self_test:
        task_path = context.automation_root / "tests" / "fixtures" / "PIPELINE-TEST-001.json"
        pipeline_report = run_pipeline_self_test(repo_root, context.automation_root, config, task_path)
        print("Pipeline Self Test")
        print(f"Final state: {pipeline_report['finalState']}")
        print(f"Final verdict: {pipeline_report['finalVerdict']}")
        print(f"Report: {pipeline_report['pipelineRunId']}")
        return 0 if pipeline_report["finalState"] == "COMPLETED" and pipeline_report["finalVerdict"] == "PASS" else 1

    if args.real_task_validator_self_test:
        validator_report = run_validator_self_test(repo_root, context.automation_root, config)
        print("Real Task Validator Self Test")
        print(f"Final verdict: {validator_report['finalVerdict']}")
        print(f"Cases: {validator_report['actualCaseCount']}/{validator_report['expectedCaseCount']}")
        print(f"Codex inspected: {validator_report['codexInspected']}")
        report_path = Path(str(validator_report.get("reportPath", "")))
        print(f"Report: {report_path.resolve().relative_to(repo_root.resolve()).as_posix() if report_path else 'none'}")
        return 0 if validator_report["finalVerdict"] == "PASS" else 1

    if args.real_task_execution_self_test:
        self_test_report = run_real_task_execution_self_test(repo_root, context.automation_root, config)
        print("Real Task Execution Self Test")
        print(f"Self-test run: {self_test_report['selfTestRunId']}")
        print(f"Production run: {self_test_report.get('productionOrchestrationRunId') or 'none'}")
        print(f"Final verdict: {self_test_report['selfTestVerdict']}")
        print(f"Invocations: {self_test_report['invocationsUsed']}")
        print(f"Repairs: {self_test_report['repairsUsed']}")
        print(f"Meta report: {Path(self_test_report.get('reportPath', '')).resolve().relative_to(repo_root.resolve()).as_posix() if self_test_report.get('reportPath') else 'none'}")
        print(f"Production report: {self_test_report.get('productionFinalReportRelativePath') or 'none'}")
        print(f"Bundle manifest: {self_test_report.get('resultBundleManifestRelativePath') or 'none'}")
        print(f"Error code: {self_test_report.get('errorCode') or 'none'}")
        return exit_code_for_self_test_verdict(str(self_test_report["selfTestVerdict"]))

    if args.pipeline_rate_limit_self_test:
        task_path = context.automation_root / "tests" / "fixtures" / "PIPELINE-TEST-001.json"
        rate_report = run_rate_limit_self_test(repo_root, context.automation_root, config, task_path)
        print("Pipeline Rate-limit Self Test")
        print(f"All scenarios passed: {rate_report['allPassed']}")
        print(f"Real Codex started: {rate_report['realCodexStarted']}")
        print(f"Sleep performed: {rate_report['sleepPerformed']}")
        print(f"Report: {rate_report['pipelineRunId']}")
        return 0 if rate_report["allPassed"] else 1

    if args.real_role_self_test_plan:
        plan_report = run_real_role_plan(repo_root, context.automation_root, config)
        print("Real Role Self Test Plan")
        print(f"Plan ok: {plan_report['ok']}")
        print(f"Workspace: {plan_report.get('workspace')}")
        print(f"Max real invocations: {plan_report.get('maxRealCodexInvocations')}")
        for command in plan_report.get("commands", []):
            print("Command: " + " ".join(command))
        if plan_report.get("errors"):
            for error in plan_report["errors"]:
                print(f"  - {error}")
        return 0 if plan_report["ok"] else 1

    if args.real_role_sandbox_probe:
        probe_report = run_real_role_sandbox_probe(repo_root, context.automation_root, config)
        print("Real Role Sandbox Probe")
        print(f"Final verdict: {probe_report['finalVerdict']}")
        print(f"Exit code: {probe_report.get('exitCode')}")
        print(f"Report: {probe_report['probeRunId']}")
        if probe_report.get("errorCode"):
            print(f"Error code: {probe_report['errorCode']}")
        return 0 if probe_report["finalVerdict"] == "PASS" else 1

    if args.real_role_self_test:
        task_path = context.automation_root / "tests" / "fixtures" / "REAL-PIPELINE-TEST-001.json"
        real_report = run_real_role_self_test(repo_root, context.automation_root, config, task_path)
        print("Real Role Self Test")
        print(f"Final state: {real_report['finalState']}")
        print(f"Final verdict: {real_report['finalVerdict']}")
        print(f"Codex invocations: {real_report['codexInvocationCount']}")
        print(f"Report: {real_report['runId']}")
        if real_report.get("errorCode"):
            print(f"Error code: {real_report['errorCode']}")
        return 0 if real_report["finalState"] == "COMPLETED" and real_report["finalVerdict"] == "PASS" else 1

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
