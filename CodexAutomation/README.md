# Codex Automation Bootstrap

This is BOOTSTRAP-03B-2D for the local Codex Automation system.

The current stage is still safe for the Unity project. It keeps BOOTSTRAP-01 preflight, BOOTSTRAP-02 read-only Codex smoke test behavior, BOOTSTRAP-03A fake pipeline behavior, BOOTSTRAP-03B-1 real-role self-test behavior, BOOTSTRAP-03B-2A generic real-task manifest validation/plan behavior, BOOTSTRAP-03B-2B-A foundation preparation behavior, BOOTSTRAP-03B-2B-B change analysis behavior, BOOTSTRAP-03B-2B-C validator execution behavior, and BOOTSTRAP-03B-2C internal orchestration. BOOTSTRAP-03B-2D adds exactly one controlled fixed real-model real-task execution self-test. It does not add a public generic task-run CLI, user-supplied real-task execution, apply, resume, merge, commit, push, PR creation, Unity launch, package installation, or task sandbox network access.

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
- `CodexAutomation/runtime/real_task_foundation_runs/`
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
- Generic real-task manifest validation is started only by `--validate-task-manifest`.
- Generic real-task planning is started only by `--real-task-plan`.
- BOOTSTRAP-03B-2D has no `--real-task-run` mode.
- BOOTSTRAP-03B-2D adds only `--real-task-execution-self-test`; it accepts no manifest path, workspace path, prompt, model, sandbox, approval policy, or positional arguments.
- BOOTSTRAP-03B-2C has no production `--real-task-prepare`, public change-analysis CLI mode, generic validation-run CLI, or public generic execution CLI. The internal production API is `execute_real_task(validated_manifest_path)`, and test code may use the private `_execute_real_task_with_test_adapter(...)` fake-adapter entry only for no-model integration tests.
- The generic real-task validate/plan modes use a no-Codex preflight profile; they do not run `codex.cmd --version`, `codex --version`, `codex exec`, or `codex sandbox`.
- No Unity code is changed.
- No real implementer, repairer, or auditor may write to the Unity project.
- No generic real-task implementer, repairer, or auditor is launched in BOOTSTRAP-03B-2B-A.
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

## BOOTSTRAP-03B-2A Generic Real-Task Manifest and Plan

BOOTSTRAP-03B-2A is a planning-only step for the future generic isolated real-task pipeline. It adds a strict manifest contract, host-only policy merge, source path safety checks, and a no-model plan report. It does not execute a task.

The two BOOTSTRAP-03B-2A CLI modes use a no-Codex preflight profile. They may inspect Python, Git, repository state, Unity running state, manifest syntax, and source filesystem metadata, but they do not launch any Codex executable even for version inspection.

Validate a manifest without requiring source files to exist:

```bat
python CodexAutomation/scripts/orchestrator.py --validate-task-manifest CodexAutomation/tests/fixtures/real_tasks/valid_minimal.json
```

Create a no-model plan report after validating source existence and source inventory:

```bat
python CodexAutomation/scripts/orchestrator.py --real-task-plan CodexAutomation/tests/fixtures/real_tasks/valid_minimal.json
start_codex_pipeline.bat --real-task-plan CodexAutomation/tests/fixtures/real_tasks/valid_minimal.json
```

The plan report is written under:

```text
CodexAutomation/runtime/real_task_plans/<plan-id>/REAL_TASK_PLAN_REPORT.json
```

The report is diagnostic evidence only. A future real task run must recreate and revalidate its own trusted context; it must not trust an old plan report.

### Real-Task Host Policy

`config.json` contains a host-only `realTasks` section. In BOOTSTRAP-03B-2A, all dangerous behavior is disabled:

- `allowExecution`: `false`
- `allowParentProjectWrite`: `false`
- `allowAutomaticApply`: `false`
- `allowNetwork`: `false`
- `allowPackageInstall`: `false`
- `allowUnityLaunch`: `false`
- `allowAutomaticRetry`: `false`
- `allowAutomaticModelDowngrade`: `false`
- `allowAutomaticCreditUsage`: `false`
- `allowBinaryOutputs`: `false`

The hard caps are `maxInvocations=3`, `maxRepairAttempts=1`, `maxChangedFiles=50`, `maxChangedBytes=10485760`, `maxSingleChangedFileBytes=2097152`, `maxSourceFiles=2000`, and `maxSourceBytes=104857600`. Manifest values may only lower applicable caps. The manifest cannot enable parent writes, network, package installation, Unity launch, automatic apply, automatic retry, model downgrade, credit usage, extra writable roots, sandbox policy, model/provider selection, or approval policy.

### Manifest Fields

Manifest version `1` supports `schemaVersion`, `taskId`, `title`, `objective`, `taskType`, `sourcePaths`, `allowedWritePaths`, `allowedDeletePaths`, `expectedOutputs`, `validationPlan`, `completionCriteria`, `roleBudget`, `repairPolicy`, `limits`, and `metadata`.

User manifests must not include host-only policy fields such as `networkPolicy`, `packagePolicy`, `gitPolicy`, `unityPolicy`, `applyPolicy`, `sandboxPolicy`, `approvalPolicy`, `writableRoots`, `model`, `provider`, `credits`, or `retryPolicy`.

### Path Restrictions

Manifest paths are exact repository-relative paths. Wildcards and globs are forbidden. Paths may only live under `Assets`, `Packages`, or `ProjectSettings`. Forbidden source roots are `.git`, `.codex`, `CodexAutomation`, `Library`, `Temp`, `Logs`, `obj`, `Build`, `Builds`, and `UserSettings`.

The validator rejects absolute paths, UNC paths, drive-relative paths, backslashes, colons or NTFS ADS syntax, `..`, dot components, empty components, control characters, trailing dot or space, Windows reserved device names, symlinks, junctions, and reparse points.

`sourcePaths` must not overlap. Exact duplicates and parent/child pairs such as `Assets/Foo` plus `Assets/Foo/Bar.cs` are rejected with `REAL_TASK_SOURCE_OVERLAP`; prefix collisions such as `Assets/Foo` and `Assets/Foobar` are not treated as containment.

Plan-mode source inventory is fail-closed. The host does not follow symlinks, junctions, or reparse points, counts each canonical repository-relative regular file at most once, and fails with `REAL_TASK_SOURCE_INVENTORY_FAILED` if a required stat, directory enumeration, or child entry inspection cannot be completed. Missing top-level source paths still use `REAL_TASK_SOURCE_MISSING`, reparse paths use `REAL_TASK_SOURCE_REPARSE`, and source cap violations use `REAL_TASK_SOURCE_LIMIT_EXCEEDED`.

### Validators

The manifest may request only host-approved validator types: `file_exists`, `file_absent`, `json_valid`, `json_schema`, `json_field_equals`, `text_contains`, `text_not_contains`, `changed_paths_exact`, `changed_paths_subset`, `no_unexpected_files`, `no_conflict_markers`, `max_file_size`, `max_changed_files`, and `extension_allowlist`.

Command strings, executables, shell fields, environment fields, and network fields are forbidden in validator entries. `json_schema.schemaName` is a future host registry name, not a file path.

### Dirty Parent Worktree

Dirty parent worktree is allowed for validation and plan mode only after a successful Git snapshot. Plan mode records a structured `parentGitSnapshot` with status, HEAD, branch or detached-head state, staged paths, full porcelain status, dirty state, and bounded command diagnostics. The persisted plan schema and semantic report validation distinguish successful and failed Git snapshots: a successful snapshot requires a non-empty HEAD, explicit branch or detached-head state, boolean dirty state, and matching `dirtyParentWorktree`; a failed snapshot keeps dirty state unknown/null and cannot produce a PASS report. If any required Git command fails or the parent cannot be confirmed as a Git work tree, the plan fails with `REAL_TASK_GIT_SNAPSHOT_FAILED`; dirty state is reported as unknown/null rather than clean. Detached HEAD is represented explicitly with `detachedHead=true` and `branch=null`. Plan mode never cleans, restores, resets, stashes, or modifies parent Git state. A future real task run must block if the parent snapshot changes after its trusted run snapshot.

### Deferred Stages

BOOTSTRAP-03B-2C and BOOTSTRAP-03B-2D are not implemented here. Generic real-task execution, implementer/repairer/auditor execution, patch/result bundle production, and controlled real-task execution self-test remain future work. Automatic apply, merge, commit, push, PR creation, Unity launch, and Unity batchmode validation are still out of scope.

## BOOTSTRAP-03B-2B-A Foundation Preparation

BOOTSTRAP-03B-2B-A adds reusable host-controlled foundation APIs for future generic real-task runs. These APIs are internal production-path functions, not a production workspace preparation CLI.

The foundation layer always recreates trust from current inputs:

- Manifest and host policy are validated again.
- Existing `REAL_TASK_PLAN_REPORT.json` files are never read as authorization.
- A new trusted context is schema/semantic validated before write, written atomically as canonical JSON, reread, validated again, and later reports carry the reread canonical SHA-256.
- Runtime output stays under `CodexAutomation/runtime/real_task_foundation_runs/<run-id>/`.

The runtime layout for a foundation run is:

```text
CodexAutomation/runtime/real_task_foundation_runs/<run-id>/
  TRUSTED_RUN_CONTEXT.json
  SOURCE_PRE_INVENTORY.json
  SOURCE_POST_INVENTORY.json
  WORKSPACE_BASELINE_INVENTORY.json
  WORKSPACE_PREPARATION_REPORT.json
  SOURCE_COPY_REPORT.json
  FINAL_WORKSPACE_PREPARATION_REPORT.json
  evidence/
  logs/
  staging/
  workspace/
```

`workspace/` is absent until staging is fully verified. On failure, `staging/` is retained as evidence, `workspaceReady=false` is reported, and no cleanup reuses or removes the failed staging directory.

Source copy rules:

- Only manifest source paths plus host-expanded applicable nested/ancestor `AGENTS.md` service inputs are copied.
- Source pre-copy and post-copy inventories hash every regular file with SHA-256.
- Empty directories are preserved and inventoried.
- Bytes are copied exactly; line endings and encodings are not normalized.
- Hidden files are included when they are inside an explicitly copied source directory.
- No automatic Unity `.meta` pairing is performed. Explicit `.meta` files and `.meta` files inside copied directories are copied naturally.
- Each source file component chain is revalidated immediately before opening the file and again before trusting the post-copy source hash.
- Destination verification compares the full file and directory path set against verified source content, with only registered service paths and required ancestor directories allowed.
- Symlinks, junctions, reparse points, special files, destination escapes, extra destination content, and hash mismatches fail closed.

Service-file rules:

- Workspace root `AGENTS.md` is generated by host code and contains immutable automation safety rules.
- Parent root `AGENTS.md` is stored only as `evidence/PARENT_ROOT_AGENTS.md` with a hash; it is not active workspace instructions.
- `.agents/` is an exact empty immutable directory.
- `task.json` and `effective_policy.json` are immutable host snapshots.
- Applicable ancestor and nested project `AGENTS.md` files are copied to their repo-relative paths and classified immutable.
- Service metadata distinguishes host-expanded ancestor instructions from project instructions naturally copied by a declared source file or directory.
- Host policy always overrides project instructions.

Standalone isolated Git rules:

- The workspace has a standalone `.git`.
- Parent `.git` is not used.
- No worktree links, remotes, staged paths, active hooks, submodule operations, `git add`, `git commit`, or baseline commit are created.
- The isolated Git fingerprint is checked in staging and again after promotion to `workspace/`; mismatch blocks PASS.
- Host inventories remain the source of truth; Git state is only protected service evidence.

Foundation reports are strict, machine-readable JSON files validated by local schemas. Critical trusted context and inventory schemas reject unknown root and nested policy/Git fields. PASS requires source pre/post match, verified destination hashes and path sets, valid service files, post-promotion verified isolated Git, unchanged parent Git before/after snapshots, zero Codex invocations, no model start, no sandbox start, no Unity start, and no network use.

## BOOTSTRAP-03B-2B-B Change Analysis

BOOTSTRAP-03B-2B-B adds internal production-path APIs only:

```text
begin_change_tracking(prepared_run_handle) -> ChangeTrackingHandle
finalize_change_analysis(change_tracking_handle) -> ChangeAnalysisResult
```

There is no public prepare/analyze/run CLI in this stage. Test code may mutate only synthetic isolated workspaces between the two APIs. The host never runs Codex, a model, sandbox probing, Unity, package installation, network access, patch apply, or production task execution.

The host-only `realTaskChangePolicy` in `config.json` is strict: rename detection is disabled, extensionless outputs are disabled, UTF-8 and UTF-8 BOM are the only accepted changed text encodings, UTF-16 is rejected, binary changes/deletes are rejected, changed text extensions are allowlisted, Unity serialized assets and common binary extensions are forbidden, and diff/report size limits are bounded. Manifests cannot override this policy.

`begin_change_tracking()` accepts the in-memory B-A final preparation result only as the current run identity hint. Persisted artifacts are the authority. It rereads and semantically revalidates `TRUSTED_RUN_CONTEXT.json`, `FINAL_WORKSPACE_PREPARATION_REPORT.json`, `WORKSPACE_BASELINE_INVENTORY.json`, and `SOURCE_POST_INVENTORY.json`, including the persisted B-A PASS contract, no-execution flags, workspace identity, source-post hash, service registry semantics, workspace containment, service files, and isolated `.git`. It then writes a separate fresh `CHANGE_BASELINE_INVENTORY.json` and a host-created `CHANGE_TRACKING_REPORT.json`.

The returned frozen handle records the tracking id, in-memory-only capability, trusted context hash, manifest hash, workspace directory, tracking report path/hash, baseline inventory path/hash, service registry hash, isolated Git fingerprint hash, source post-inventory hash, parent Git tracking-start snapshot, and fixed no-model/no-Codex/no-sandbox/no-Unity flags. The secret capability is not serialized in JSON reports. Finalization checks the process registry, capability, exact handle object identity, and persisted tracking report hash; manually constructed or copied handles are rejected. Process-restart resume is not implemented.

`finalize_change_analysis()` accepts only the registry-bound handle returned by `begin_change_tracking()`. It rejects stale, substituted, wrong-run, wrong-task, wrong-context, changed-workspace, already-finalized, copied, unregistered, and baseline-substituted handles. It rereads the fresh baseline artifact, builds `CHANGE_FINAL_INVENTORY.json` from disk, recomputes service and isolated Git status, creates `WORKSPACE_DIFF_REPORT.json`, applies policy, rechecks parent Git, compares parent source against the persisted B-A source-post inventory with the same source projection, and writes `CHANGE_POLICY_REPORT.json` plus `FINAL_CHANGE_ANALYSIS_REPORT.json`.

The B-B inventory entry model includes `path`, `canonicalPathKey`, `type`, `size`, `sha256`, `extension`, `contentKind`, `textEncoding`, `serviceKind`, `serviceOrigin`, `expectedMutability`, `sourceClassification`, `symlink=false`, and `reparse=false`. Hidden files and empty directories are included. `realTaskFoundation.maxInventoryEntries` is enforced during recursive traversal. `.git` is represented as a protected service fingerprint rather than task content. `.agents` must remain an exact empty immutable service directory. Path collisions from case-only or Unicode-normalization-only differences fail closed.

The diff is deterministic and does not infer renames. A rename is represented as delete plus add. Categories are `added`, `modified`, `deleted`, `typeChanged`, and `unchanged`. Every category uses the same strict change-entry shape. `before` and `after` are either null or strict side objects matching the inventory side projection: path, canonical key, type, size, SHA-256, extension, content kind, encoding, service metadata, mutability, classification, and fixed symlink/reparse flags. `unchanged` entries now carry both baseline and final side evidence, so unchanged file hash/size equality and unchanged directory metadata can be independently checked. Summary formulas count changed files/directories/paths once per non-unchanged path, `totalChangedBytes` from added/modified/final file sides, `totalDeletedBytes` from deleted/original file sides, and `totalTouchedBytes` as changed plus deleted bytes. Type changes require both write and delete permission.

Write scope uses deterministic permission descriptors derived from baseline/final inventory types and declared path relationships; it does not infer file-vs-directory from filename dots. Exact allowed file paths allow only the exact file and required ancestor directories, not unrelated sibling files. Directory grants allow descendants without prefix collisions. Delete scope is separate. Ancestor directory deletion is allowed only for explicit directory-tree grants or when every deleted descendant is separately allowed and there are no forbidden/unchanged descendants under that ancestor. Missing one side of permission rejects type changes. Temporary, backup, hidden, and extra files are not ignored.

Service-file guards have priority over manifest permissions. Workspace root `AGENTS.md`, copied/nested `AGENTS.md`, `.agents`, `task.json`, `effective_policy.json`, `.git`, and B-A service registry entries are immutable. New `AGENTS.md` injection is blocked. `.agents/**` children and `.git` mutations are blocked. Parent Git and parent source inputs are rechecked before PASS.

Reports are strict machine-readable artifacts outside the workspace:

```text
CHANGE_BASELINE_INVENTORY.json
CHANGE_FINAL_INVENTORY.json
WORKSPACE_DIFF_REPORT.json
CHANGE_POLICY_REPORT.json
FINAL_CHANGE_ANALYSIS_REPORT.json
```

Authorization-critical reports are schema-validated, semantically checked, size-capped, atomically written, reread, structurally and semantically validated again, and hash-compared. Report persistence failures, including report size-cap failures, existing report collisions, malformed final artifacts, leftover temp artifacts, reread validation failures, and canonical reread hash mismatches, are classified as `BLOCKED` trust failures rather than ordinary task-policy `FAIL`. Existing immutable reports are not overwritten. Diff change entries, nested diff side objects, unchanged entries, and final warnings use strict schemas rather than arbitrary objects. Diff semantic validation cross-checks each entry against the trusted change baseline and final inventories, verifies exact all-path coverage across the baseline/final union, and recomputes summary totals before the report is trusted and again after reread.

Persisted B-A `SOURCE_POST_INVENTORY.json` is independently schema-validated, source-limit-checked, and semantically validated before use. The validator checks source inventory identity, completion, errors, deterministic sorting, path uniqueness, Unicode-normalized canonical uniqueness, file/directory size and hash contracts, exact integer counters, total bytes, symlink/reparse flags, optional source path-length caps, and the B-A canonical inventory hash formula. Parent source verification then rebuilds a fresh source inventory with the trusted context's source inventory cap and the same source projection, then compares the persisted and fresh inventories; the old self-compare path is not used.

The focused B-B regression matrix covers strict source schema and source-limit failures, unchanged binary PASS behavior, extensionless and unknown-extension changes, Unity serialized extension failures, binary add/modify/delete/type-change failures, UTF-8/BOM/UTF-16/invalid UTF-8/NUL classification, `maxTextReadBytes` byte boundaries, numeric change-limit boundaries, report size caps, report persistence failures, and capability handle edge cases.

Verdicts are intentionally distinct. `PASS` requires all safety and policy booleans true and no blocking findings. `FAIL` is used for deterministic task-policy failures such as forbidden writes/deletes, limits, binary/text violations, or missing expected outputs. `BLOCKED` is used for trust and unsafe state such as context mismatch, baseline substitution, service mutation, isolated Git mutation, parent Git/source changes, reparse paths, inventory failure, or report trust failure.

BOOTSTRAP-03B-2C and BOOTSTRAP-03B-2D remain deferred. Generic task execution, implementer/repairer/auditor roles, patch apply, result bundles, automatic apply, merge, commit, push, PR creation, Unity launch, and Unity batchmode validation are not implemented here.

## BOOTSTRAP-03B-2B-C Local Validator Registry

BOOTSTRAP-03B-2B-C adds an internal production-path API:

```text
create_validation_prerequisite_handle(change_analysis_result) -> ValidationPrerequisiteHandle
execute_validation_plan(validation_prerequisite_handle) -> ValidationExecutionResult
```

The API accepts only a process-local prerequisite handle minted from the trusted in-memory B-B result and rereads persisted artifacts from the B-B run directory. It does not accept an arbitrary workspace path, manifest path, registry, function map, command, executable, plugin, or user-supplied validation plan. B-B must have `finalState=COMPLETED`, `finalVerdict=PASS`, unchanged parent Git/source, valid service files and isolated `.git`, valid write/delete/change-limit/text/expected-output checks, and zero Codex/model/sandbox/Unity/network activity. Old plan reports are not authorization.

The host-only `realTaskValidationPolicy` is strict. The allowed validator type list is exact and deterministic, capped at 64 validators and 256 findings. Command validators, regex validators, external schema paths, dynamic plugins, network, and Unity are all disabled. The manifest cannot override this policy.

The fixed validator registry contains exactly:

```text
file_exists
file_absent
json_valid
json_schema
json_field_equals
text_contains
text_not_contains
changed_paths_exact
changed_paths_subset
no_unexpected_files
no_conflict_markers
max_file_size
max_changed_files
extension_allowlist
```

Each validator definition records type, version, implementation id, argument contract, workspace/diff/expected-output needs, max files read, and `noSideEffects=true`. Implementations are statically imported in-process Python callables. There is no shell, subprocess, command string, dynamic import, environment mutation, package install, network call, Codex call, or Unity call.

`json_schema` uses only the fixed host schema registry under `CodexAutomation/validator_schemas/`. The manifest passes a case-sensitive `schemaName`, never a filesystem path. Registry entries include `schemaName`, `schemaVersion`, contained relative path, canonical schema SHA-256, and byte cap. Unknown names, path traversal, absolute paths, symlinks/reparse points, malformed JSON, unsupported schema keywords, oversized schemas, and canonical hash mismatches are BLOCKED trust failures.

Validator target paths are repository-relative paths inside the isolated workspace and must be inside an explicit immutable validation-readable scope descriptor. Each descriptor records normalized path, `FILE` or `DIRECTORY` scope kind, origin, recursive behavior, and service=false. `FILE` scopes authorize only the exact path. `DIRECTORY` scopes authorize descendants only when the trusted descriptor is recursive. Service paths such as `.git`, `.agents`, `AGENTS.md`, `task.json`, `effective_policy.json`, `.codex`, and `CodexAutomation` are blocked as validator targets. Text and JSON reads are bounded, UTF-8/UTF-8 BOM only, and strict JSON rejects duplicate keys, trailing data, NaN/Infinity, and NUL bytes.

`task.json` and `effective_policy.json` remain immutable service artifacts. B-C validates their service registry type, workspace containment, regular-file disk type, Windows reparse/junction safety, inventory size where available, and SHA-256 before treating them as trusted snapshots. The current parent manifest/config cannot substitute those persisted workspace files.

Validation integrity now exposes `agentsDirectoryValid` separately from `serviceFilesValid`. PASS and FAIL both require `agentsDirectoryValid=true`; otherwise the final verdict is BLOCKED. The expected B-A state is an immutable empty `.agents` directory with no symlink/reparse component and no children.

Validator results use a fixed status/code contract. PASS requires `code=null`. FAIL codes are allowlisted per validator type in the fixed registry snapshot. BLOCKED codes are restricted to trust, path, read, execution, registry, plan, integrity, and report failures. Invalid result codes are converted to BLOCKED result-invalid failures.

B-C report schemas are strict for nested validator registry entries, schema registry entries, validation-plan entries, validator results, integrity fields, self-test cases, and warnings. Unsupported schema keywords such as `$ref`, `oneOf`, `allOf`, and `anyOf` are not used.

The controlled self-test Git helper resolves and validates the synthetic cwd under the ignored self-test runtime, rejects symlink/reparse components, and allows only fixed local `git init`, `git config --local`, `git add -- <seed paths>`, and `git commit` argv. It still does not run any remote Git command.

Verdicts are intentionally distinct:

- `PASS`: trusted B-B PASS, trusted registries, every validator result PASS, no workspace/service/Git/parent mutation, trusted reports written, and zero Codex/model/sandbox/Unity/network activity.
- `FAIL`: registered validators safely executed and one or more assertions failed.
- `BLOCKED`: trust mismatch, malformed artifacts, registry corruption, unsafe path, service target, symlink/reparse, read failure, invalid validator result, validator workspace mutation, parent Git/source mutation, or report trust/write failure.

The strict final validation report is:

```text
FINAL_VALIDATION_REPORT.json
```

It contains deterministic registry snapshots and hashes, schema registry snapshot and hash, validation-plan hash, ordered per-validator results, recomputed pass/fail/blocked counts, read-only integrity evidence, final state/verdict, and no-execution fields.

Run the controlled no-model validator self-test:

```bat
python CodexAutomation/scripts/orchestrator.py --real-task-validator-self-test
```

This is the only new public B-C CLI mode. It takes no additional arguments and uses a no-Codex preflight profile, so Codex executable/version inspection is skipped. Runtime output is written under:

```text
CodexAutomation/runtime/real_task_validator_self_tests/<run-id>/
```

The self-test creates synthetic parent Git repositories only inside that ignored runtime root, with local-only `git init`, local user config, initial `git add`, and initial `git commit`. It does not touch the actual Clicker parent project. The PASS case executes all 14 validators successfully. The expected FAIL case safely aggregates deterministic failures for `json_field_equals`, `text_not_contains`, `changed_paths_exact`, `no_conflict_markers`, `max_file_size`, and `extension_allowlist`; that case is considered successful when the expected FAIL verdict and failed validator types match.

## BOOTSTRAP-03B-2C Internal Real-Task Orchestration

BOOTSTRAP-03B-2C adds an internal production-path API:

```text
execute_real_task(validated_manifest_path) -> RealTaskExecutionResult
```

The production entry accepts only a manifest path, reloads host config from the fixed automation location, revalidates the manifest from disk, and creates a fresh orchestration run under:

```text
CodexAutomation/runtime/real_task_runs/<run-id>/
```

The orchestration flow is:

```text
fresh B-A foundation -> execution context -> fresh sandbox probe -> implementer -> no-authority diagnostic -> optional one repair -> final B-B -> final B-C -> read-only auditor -> immutable result bundle -> final report
```

Host validation is the source of truth. Diagnostic reports are explicitly non-authorizing and cannot substitute for final B-B/B-C. The original B-B baseline is created before the implementer and is preserved across the optional repair so final cumulative analysis includes implementer plus repairer changes. The invocation budget is fixed at three role invocations: implementer, optional repairer, and auditor. The repair budget is at most one. Rate limits and timeouts are terminal for the current run; there is no automatic retry, sleep-and-retry, model downgrade, or credit fallback.

The effective role budget may only be lowered by the manifest. Role invocations are reserved atomically in the process-local execution registry before the role starts, and each reservation records the expected role sequence, current handle state, invocation count, repair count, host-owned invocation id, and reservation time. Role invocation reports use the host-reserved id/role/sequence; adapter-claimed invocation identities must match or the run blocks before diagnostic/final stages. Exhausting the lowered budget blocks the run before the next role can start; it does not skip the mandatory auditor and still report PASS.

The execution context is written through the trusted report writer as `REAL_TASK_EXECUTION_CONTEXT.json`, schema-validated, semantically checked, reread, and hash-compared before a usable handle is registered. Role, diagnostic, finalization, auditor integrity, and bundle code require a process-local `RealTaskExecutionHandle`; raw context dictionaries, reconstructed handles, copied handles, stale states, wrong run/task/workspace identities, wrong threads, terminal handles, and secret capability mismatches are rejected. Critical 2C APIs resolve their context by rereading the handle-bound execution-context report and comparing it with the registry hash and identity before use. Process-restart resume is not implemented.

Before implementer, after implementer, before repair, after repair, before auditor, and after auditor, the host captures shared integrity snapshots covering the full task workspace content, immutable service files, exact empty `.agents/`, standalone `.git`, parent Git, parent source inventory, and runtime containment. The post-role gates block service, `.agents`, isolated Git, parent Git/source, containment, and no-progress repair failures before later roles or bundling can proceed.

The diagnostic pass reruns the B-B/B-C logic against the original pre-implementer baseline. It rebuilds a final inventory and diff, evaluates the B-B change policy, runs the fixed local B-C validators, records validator result hashes, and writes `authorization=false`. The persisted diagnostic report is written through the trusted writer, registered in the process-local execution handle registry with its full artifact receipt, and then consumed once by repair decision. Repair decision does not accept an in-memory diagnostic as authority: it derives the attempt path from the handle-bound run directory, rereads the persisted report with duplicate-key-safe schema/semantic validation, verifies the diagnostic payload hash separately from the full artifact receipt hash, and checks run/task/context/baseline binding before evaluating repair eligibility. Repair eligibility is host-derived from the trusted diagnostic verdict, pass/fail/blocked counts, integrity status, a fixed repairable finding mapping, repair budget, and invocation budget; string heuristics such as checking for `INVALID` are not authority. A PASS diagnostic is only a repair decision input; the final authority remains the later final B-B and final B-C reports.

All 2C trust reports are written through a bounded trusted writer: schema validation, semantic validation where applicable, canonical JSON serialization, size caps, atomic replace, duplicate-key-safe reread, second schema/semantic validation, and canonical hash comparison. Existing report paths are not overwritten.

The production `CodexRealTaskRoleAdapter` is fixed-registration only. It does not accept manifest-provided executable, shell string, CLI flags, model, sandbox, approval policy, writable roots, dynamic plugin, validator registry, or prompt override. Outside the BOOTSTRAP-03B-2D controlled self-test controller it remains closed. The no-model `FakeRealTaskRoleAdapter` exists only for private tests and does not start Codex, subprocess role execution, Unity, package installation, or network.

Result bundles are immutable review artifacts only:

```text
result_bundle/
  files/<changed task files>
  changes.patch
  RESULT_MANIFEST.json
  SHA256SUMS.json
```

`RESULT_MANIFEST.json` always has `eligibleForApply=false`. `SHA256SUMS.json` is written through trusted persistence and covers copied payload files and `changes.patch`; it intentionally excludes `RESULT_MANIFEST.json` and `SHA256SUMS.json` to avoid circular/stale manifest hashes. The manifest records the trusted canonical SHA-256, size, covered file count, covered byte count, and fixed exclusion list for `SHA256SUMS.json`, and final bundle verification rechecks that binding after bundle promotion. BOOTSTRAP-03B-2C does not apply bundle contents to the parent repository and does not merge, commit, push, create a PR, launch Unity, or run Unity batchmode.

## BOOTSTRAP-03B-2D Controlled Real-Model Real-Task Execution Self-Test

BOOTSTRAP-03B-2D adds one fixed CLI mode:

```bat
python CodexAutomation/scripts/orchestrator.py --real-task-execution-self-test
```

The flag takes no arguments. It does not accept a manifest path, workspace path, prompt, model, sandbox, approval policy, or positional arguments. It is not a generic task runner.

The controller creates a fresh ignored runtime root under:

```text
CodexAutomation/runtime/real_task_execution_self_tests/<selfTestRunId>/
```

Each run contains a synthetic parent Git repository under `parent/`, host-owned fixture inputs under `fixture/`, and the strict meta report under `reports/REAL_TASK_EXECUTION_SELF_TEST_REPORT.json`. The production 2C orchestration writes its own artifacts under the synthetic parent repository's `CodexAutomation/runtime/real_task_runs/<orchestrationRunId>/`, and B-A foundation artifacts under `CodexAutomation/runtime/real_task_foundation_runs/<foundationRunId>/`.

The synthetic task is fixed. It starts with `TaskData/message.txt` containing a placeholder and `TaskData/reference.txt` containing the required stage/mode. The model may only replace `TaskData/message.txt` and create `TaskData/result.json`; allowed deletes are empty. The fixed objective requires the exact message `BOOTSTRAP-03B-2D CONTROLLED REAL MODEL SELF TEST PASSED` and a JSON result with `status=PASS`, `stage=BOOTSTRAP-03B-2D`, `mode=controlled-real-model-self-test`, and the same message.

The controller generates and validates a host-owned manifest. It reuses the production 2C path: fresh B-A foundation, original B-B baseline, fresh sandbox probe, real implementer, host diagnostic, at most one real repairer, final B-B, final B-C, read-only real auditor, immutable result bundle, and final production report. A process-local self-test capability binds the CLI invocation, run id, task id, manifest hash, synthetic parent identity, self-test root, and single-run registry entry before orchestration starts.

The production Codex adapter is selected only by this controller with fixed host config for executable identity, Windows sandbox implementation, and approval policy. It does not accept manifest-controlled model, sandbox, approval, executable, command, environment, network, Unity, package, retry, downgrade, or credit settings. Result bundles remain review-only with `eligibleForApply=false`; no automatic apply is implemented.

Rate limits, timeouts, FAIL, and BLOCKED outcomes are terminal for the current invocation. The controller does not sleep, retry, run a second repair, switch models, use credits, or rerun the self-test automatically. Exit codes are `0=PASS`, `1=controller/internal error`, `2=FAIL`, `3=BLOCKED`, and `4=RATE_LIMITED`.
