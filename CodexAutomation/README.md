# Codex Automation Bootstrap

This is BOOTSTRAP-03A for the local Codex Automation system.

The current stage is still safe for the Unity project. It keeps BOOTSTRAP-01 preflight and BOOTSTRAP-02 read-only Codex smoke test behavior, and adds a local fake IMPLEMENT VALIDATE AUDIT REPAIR pipeline core. BOOTSTRAP-03A does not run real workspace-write Codex roles, does not launch Unity batchmode, and does not change files inside `Assets`, `Packages`, or `ProjectSettings`.

## Created Files

- `config.json`
- `workflow.seed.json`
- `workflow.runtime.json`
- `state.json`
- `start_codex_pipeline.bat`
- `CodexAutomation/.gitignore`
- `CodexAutomation/scripts/orchestrator.py`
- `CodexAutomation/scripts/preflight.py`
- `CodexAutomation/scripts/codex_runner.py`
- `CodexAutomation/scripts/schema_validator.py`
- `CodexAutomation/scripts/models.py`
- `CodexAutomation/scripts/file_utils.py`
- `CodexAutomation/schemas/codex_smoke_result.schema.json`
- `CodexAutomation/prompts/read_only_smoke_test.md`
- `CodexAutomation/tasks/TEST-001.md`
- `CodexAutomation/runtime/logs/`
- `CodexAutomation/runtime/reports/`
- `CodexAutomation/runtime/results/`
- `CodexAutomation/runtime/workspaces/`
- `CodexAutomation/runtime/pipeline_runs/`
- `CodexAutomation/runtime/audits/`
- `CodexAutomation/runtime/generated_tasks/`

## Run Preflight

From the repository root:

```bat
python CodexAutomation\scripts\orchestrator.py --dry-run
```

On Windows, the launcher script supports `python` and the Windows `py` launcher. It checks these commands in order: `python`, `py -3.13`, `py -3`, then `py`.

Manual command for the current Windows workstation:

```bat
py -3.13 CodexAutomation\scripts\orchestrator.py --dry-run
```

Or:

```bat
start_codex_pipeline.bat
```

`--dry-run` is the default mode. `start_codex_pipeline.bat` still runs only preflight unless an explicit argument is passed.

The report is written to:

```text
CodexAutomation/runtime/reports/PREFLIGHT_REPORT.json
```

Runtime files are created automatically when missing and are not stored in Git:

- `CodexAutomation/workflow.runtime.json`
- `CodexAutomation/state.json`
- `CodexAutomation/runtime/`

## Run Read-only Smoke Test

BOOTSTRAP-02 can run one explicit smoke test task, `TEST-001`. The task invokes Codex CLI with a read-only sandbox and asks Codex to inspect only:

- `ProjectSettings/ProjectVersion.txt`
- `CodexAutomation/config.json`
- `CodexAutomation/workflow.seed.json`

The smoke test requires structured JSON output and validates it locally against `CodexAutomation/schemas/codex_smoke_result.schema.json`.
The structured result is read only from the Codex CLI `--output-last-message` file. stdout and stderr are saved only as diagnostic logs.

The stable workflow id is `codex-automation-bootstrap`. The smoke test task id is `TEST-001`.

On Windows, if the Codex smoke test times out, the runner attempts to terminate the full process tree with `taskkill /PID <pid> /T /F` and records the termination diagnostics in the run report.

From the repository root:

```bat
start_codex_pipeline.bat --smoke-test
```

Manual command:

```bat
py -3.13 CodexAutomation/scripts/orchestrator.py --smoke-test
```

Smoke test runtime output is written to ignored runtime paths:

- stdout: `CodexAutomation/runtime/logs/TEST-001_*_stdout.log`
- stderr: `CodexAutomation/runtime/logs/TEST-001_*_stderr.log`
- structured result: `CodexAutomation/runtime/results/TEST-001_*_result.json`
- run report: `CodexAutomation/runtime/reports/TEST-001_*_RUN_REPORT.json`

## Current Limits

- `codex exec` is started only by the explicit `--smoke-test` mode.
- Pipeline self-tests use a local fake role adapter and do not start Codex.
- No Unity code is changed.
- No real implementer is available.
- No real auditor is available.
- No real repairer is available.
- No `codex exec --json` parser is available.
- No task generator is available; BOOTSTRAP-04 will own task generation.
- No Unity Editor or Unity batchmode process is started.
- No Git commits are created.
- No resume pipeline is available.
- No two-hour autonomous mode is available.
- No Python packages are installed.
- Dirty worktree and running Unity are reported but do not stop preflight.
- The seed workflow contains only the `TEST-001` smoke-test task.

## Later Stages

## BOOTSTRAP-03A Pipeline Core

The local pipeline state machine supports:

```text
PENDING -> IMPLEMENTING -> VALIDATING -> REPAIRING -> VALIDATING -> AUDITING -> COMPLETED
```

It also supports `PAUSED_RATE_LIMIT`, `BLOCKED`, `FAILED`, `FAILED_MAX_REPAIRS`, and `FAILED_INVOCATION_BUDGET`.

`PAUSED_RATE_LIMIT` is a safe pause, not a task failure. It stores the paused role, source state, resume state, detected time, optional reset time, optional retry-after seconds, limit type, retry count, current findings, repair attempt, validation/audit attempt indexes, repair context, and internal invocation count. Resume is explicit and returns only to `IMPLEMENTING`, `AUDITING`, or `REPAIRING`.

Repair attempts and rate-limit retries are separate. A repair attempt is reserved before a repair role invocation. If that invocation is rate-limited before producing a domain result, the same repair attempt is resumed without incrementing again.

The invocation budget is an internal safety cap only. It is not the real account quota and does not estimate remaining Codex messages. Optional role invocations must leave the reserved final-audit budget intact. The final audit is mandatory for `COMPLETED` and may use the reserved invocation. `maxAuditsPerTask` is enforced before each auditor invocation; a rate-limited auditor attempt counts as an auditor invocation because the role launch already happened.

BOOTSTRAP-03A validates every role execution wrapper before using it to drive the state machine. `SUCCESS`, `RATE_LIMITED`, and `TECHNICAL_ERROR` each have conditional checks, and successful implementer/auditor/repairer domain results are validated against their JSON schemas plus local task-specific rules. Invalid wrapper or domain data stops the pipeline with a stable error code.

Task validation paths are resolved through a workspace-only safe resolver. Absolute paths, drive-relative paths, UNC paths, parent traversal, paths outside the runtime workspace, and symlink or reparse-point components are blocked. Workspace snapshots use a safe walker and do not follow unsafe links.

Usage-limit timestamps must be timezone-aware UTC ISO values (`Z` or `+00:00`). `resetAtUtc` may be null, but when present it must not be earlier than `detectedAtUtc`. BOOTSTRAP-03A never buys credits, never switches models automatically, never sleeps until reset, and never computes `resetAtUtc` as detected time plus five hours.

Pipeline config is parsed strictly. Boolean fields must be JSON booleans, not strings or numbers. Integer limits must be integers, not booleans. In BOOTSTRAP-03A, `allowRealWorkspaceWrite`, `allowAutomaticCreditUsage`, and `allowAutomaticModelDowngrade` must be present and exactly `false`.

Run the local full fake pipeline:

```bat
py -3.13 CodexAutomation/scripts/orchestrator.py --pipeline-self-test
start_codex_pipeline.bat --pipeline-self-test
```

Run local fake rate-limit scenarios:

```bat
py -3.13 CodexAutomation/scripts/orchestrator.py --pipeline-rate-limit-self-test
start_codex_pipeline.bat --pipeline-rate-limit-self-test
```

Pipeline runtime output is written under:

```text
CodexAutomation/runtime/pipeline_runs/<run-id>/
CodexAutomation/runtime/workspaces/<run-id>/
```

Important BOOTSTRAP-03A error codes include `INVALID_TASK`, `INVALID_STATE_TRANSITION`, `IMPLEMENTATION_RESULT_INVALID`, `VALIDATION_FAILED`, `AUDIT_RESULT_INVALID`, `REPAIR_RESULT_INVALID`, `ROLE_EXECUTION_RESULT_INVALID`, `FAILED_MAX_REPAIRS`, `FAILED_MAX_AUDITS`, `FAILED_WRITE_DETECTED`, `REAL_WORKSPACE_WRITE_DISABLED`, `PIPELINE_PREFLIGHT_FAILED`, `PIPELINE_INTERNAL_ERROR`, `INVALID_PIPELINE_CONFIG`, `FAILED_INVOCATION_BUDGET`, `UNSAFE_ABSOLUTE_PATH`, `UNSAFE_PARENT_TRAVERSAL`, `UNSAFE_PATH_OUTSIDE_WORKSPACE`, `UNSAFE_SYMLINK_OR_REPARSE_POINT`, `INVALID_VALIDATION_PATH`, `CODEX_USAGE_LIMIT_REACHED`, `CODEX_WEEKLY_LIMIT_REACHED`, `CODEX_CREDITS_EXHAUSTED`, `CODEX_RATE_LIMIT_RESET_UNKNOWN`, `CODEX_RATE_LIMIT_RETRY_EXHAUSTED`, `CODEX_RATE_LIMIT_DATA_INVALID`, and `PIPELINE_PAUSED_RATE_LIMIT`.

Future stages can add real implementation, audit, repair, `codex exec --json` parsing, generated task handling, Unity validation, Git integration, process restart resume support, and long-running automation. Those features are intentionally out of scope for BOOTSTRAP-03A.
