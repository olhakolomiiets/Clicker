# Codex Automation Bootstrap

This is BOOTSTRAP-03B-1 for the local Codex Automation system.

The current stage is still safe for the Unity project. It keeps BOOTSTRAP-01 preflight, BOOTSTRAP-02 read-only Codex smoke test behavior, and BOOTSTRAP-03A fake pipeline behavior. BOOTSTRAP-03B-1 adds one explicit real-role self-test that may use `codex exec --sandbox workspace-write` only inside a newly created isolated runtime workspace under `CodexAutomation/runtime/real_role_workspaces/<run-id>/workspace/`. It does not launch Unity batchmode and does not change files inside `Assets`, `Packages`, or `ProjectSettings`.

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
- `CodexAutomation/runtime/real_role_workspaces/`
- `CodexAutomation/runtime/real_role_runs/`
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

- Read-only `codex exec` is started only by the explicit `--smoke-test` mode.
- Fake pipeline self-tests use a local fake role adapter and do not start Codex.
- Real-role self-test is started only by the explicit `--real-role-self-test` mode and is limited to an isolated runtime workspace.
- No Unity code is changed.
- No real implementer, repairer, or auditor may write to the Unity project.
- No automatic retry after rate limit is available.
- No process-restart resume is available.
- No task generator is available; BOOTSTRAP-04 will own task generation.
- No Unity Editor or Unity batchmode process is started.
- No Git commits are created.
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

## BOOTSTRAP-03B-1 Real-Role Self-Test

BOOTSTRAP-03B-1 connects real Codex roles only for a controlled local test task, `REAL-PIPELINE-TEST-001`. The real roles run in a separate local Git repository inside:

```text
CodexAutomation/runtime/real_role_workspaces/<run-id>/workspace/
```

For the installed Codex CLI 0.142.5, the real-role self-test has observed compatibility behavior where the CLI/runtime may create or expect an exact empty `.agents` directory inside the role workspace. Host code now pre-creates that exact service directory before the initial workspace snapshot and before any Codex invocation:

```text
CodexAutomation/runtime/real_role_workspaces/<run-id>/workspace/.agents/
```

`.agents` is a strictly controlled empty service directory, not a wildcard ignore. The initial service baseline contains only `.git`, `AGENTS.md`, `task.json`, and `.agents` at the workspace top level. `AGENTS.md` and `task.json` must keep their original hashes, the isolated Git fingerprint must remain stable, and `.agents` must remain a normal empty directory. No `.agents/**` contents are allowed. Other dotfiles and runtime paths are not ignored; unexpected paths such as `.agents.json`, `.agents.tmp`, `.agents-old`, `.agent`, `.codex`, or any other automatically created path still fail validation.

The service directory is validated before and after every real role. Validation checks the exact relative path `.agents`, existence, ordinary directory type, non-symlink status, non-reparse/junction status, containment inside the current isolated workspace, and an empty recursive listing. Missing, non-empty, invalid, or replaced service directories use stable error codes: `REAL_TEST_SERVICE_DIRECTORY_MISSING`, `REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY`, `REAL_TEST_SERVICE_DIRECTORY_INVALID`, and `REAL_TEST_SERVICE_DIRECTORY_REPLACED`. Historical failed runs, including evidence where `.agents` first appeared after implementer, are kept under runtime and are not reused as a baseline for later runs.

The Unity project root, `Assets`, `Packages`, `ProjectSettings`, `CodexAutomation`, and parent runtime directories are never used as the workspace-write root. The configuration must keep `allowRealProjectWrite` exactly `false` and `allowIsolatedWorkspaceWriteTest` exactly `true`; task JSON cannot override those safety settings.

Plan the real-role command set without starting Codex:

```bat
python CodexAutomation/scripts/orchestrator.py --real-role-self-test-plan
start_codex_pipeline.bat --real-role-self-test-plan
```

Check the local Windows sandbox write helper without a model invocation:

```bat
python CodexAutomation/scripts/orchestrator.py --real-role-sandbox-probe
```

The sandbox probe uses `codex sandbox`, not `codex exec`. It creates a unique ignored workspace under
`CodexAutomation/runtime/real_role_sandbox_probes/`, writes and reads back a temporary probe file inside
that workspace, removes it, and writes `SANDBOX_WRITE_PROBE_REPORT.json`.

The probe has its own bounded timeout configuration: `sandboxProbeTimeoutSeconds`,
`sandboxProbeTaskkillTimeoutSeconds`, and `sandboxProbePostKillWaitSeconds`. It creates the probe report,
sanitized command sidecar, process sidecar, initial workspace snapshot, and stdout/stderr log files before
starting the sandbox helper. Probe stdout and stderr are written directly to runtime log files instead of
being collected through pipes. If the helper times out, the runner records a timeout marker before bounded
process-tree termination and still finalizes `SANDBOX_WRITE_PROBE_REPORT.json` when the report path is
available. A timeout never starts model roles, never retries automatically, and never falls back to
`unelevated`; the Windows sandbox environment must be fixed manually before another real-role attempt.
The in-memory probe result never authorizes real roles by itself. The trusted probe identity is created by
host code before the sandbox helper starts: `probeRunId`, run directory, workspace, and report path are not
read from the report payload. That original host-created context remains the authority after the probe
returns; the returned `ProbeExecutionOutcome.context` is only an untrusted diagnostic echo, and a mismatch
blocks model roles. Before any model role can start, the runner reloads exactly that current probe run's
final `SANDBOX_WRITE_PROBE_REPORT.json` from disk and verifies the trusted `probeRunId`, report path,
workspace path, finalization fields, PASS contract, and absence of `reportWriteError`. Missing, malformed,
stale, RUNNING, report-write-failed, or context-mismatched probe reports block model invocation; an older
self-consistent PASS report cannot choose its own `reportPath` or be reused for a later run.

Run the real-role self-test explicitly:

```bat
python CodexAutomation/scripts/orchestrator.py --real-role-self-test
```

The real self-test is capped at three Codex invocations: implementer, repairer, and read-only auditor. Implementer and repairer use `--sandbox workspace-write` only inside the isolated workspace. Auditor uses `--sandbox read-only`. Prompts are passed through stdin, JSONL stdout is diagnostic event data, and the structured role result is read only from `--output-last-message`.

Real-role response schemas must define an explicit schema for every property. Nullable fields are declared explicitly, such as the auditor `blockedReason` field accepting either a string or `null`; empty property schemas are not valid for the response format. API-level structured-output schema rejection, including `invalid_json_schema` or `Invalid schema for response_format`, is classified as `CODEX_EXEC_SCHEMA_INVALID`. If that rejection prevents `--output-last-message` from being written, the missing last-message file is recorded only as a secondary symptom. The observed failed run `real_role_20260704T103951032146Z` is kept as runtime evidence for this compatibility repair.

Native Windows real-role commands keep `--ignore-user-config` and pass required safe config explicitly:

```text
-c windows.sandbox="elevated" -c approval_policy="never"
```

BOOTSTRAP-03B-1 does not use `danger-full-access`, `--full-auto`, `yolo`, additional writable roots, or the Unity project root as a real-role workspace.

If a structured rate-limit event is detected, the run stops in `PAUSED_RATE_LIMIT`. BOOTSTRAP-03B-1 does not retry, sleep, wait for reset, compute `resetAtUtc`, buy credits, switch models, or resume after process restart.

Real-role runtime output is written under:

```text
CodexAutomation/runtime/real_role_runs/<run-id>/
```

Important BOOTSTRAP-03B-1 error codes include `BLOCKED_UNSUPPORTED_CODEX_CLI`, `ISOLATED_WORKSPACE_INVALID`, `ISOLATED_WORKSPACE_OUTSIDE_RUNTIME`, `REAL_PROJECT_WRITE_FORBIDDEN`, `REAL_ROLE_CONFIG_INVALID`, `REAL_ROLE_INVOCATION_LIMIT`, `REAL_ROLE_EXECUTION_FAILED`, `REAL_TEST_IMPLEMENTER_SKIPPED_REQUIRED_PRE_REPAIR_STATE`, `REAL_TEST_UNEXPECTED_FILE`, `REAL_TEST_AUDITOR_CHANGED_WORKSPACE`, `CODEX_JSONL_INVALID_LINE`, `CODEX_EXEC_SCHEMA_INVALID`, and `CODEX_EXEC_ERROR_UNCLASSIFIED`.

BOOTSTRAP-03B-2 is not implemented. Future work can add known Codex rate-limit parsing variants, controlled resume after reset, and process-restart recovery. Future stages can also add generated task handling, Unity validation, Git integration, and long-running automation.
