# Codex Automation Bootstrap

This is BOOTSTRAP-02 for the local Codex Automation system.

The current stage is intentionally read-only for the Unity project. It creates a safe local scaffold, runs a preflight report, and can run one explicit non-interactive Codex CLI smoke test. It does not implement tasks, does not audit or repair code, does not launch Unity batchmode, and does not change files inside `Assets`, `Packages`, or `ProjectSettings`.

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
- No Unity code is changed.
- No implementer is available.
- No auditor is available.
- No repair loop is available.
- No task generator is available.
- No Unity Editor or Unity batchmode process is started.
- No Git commits are created.
- No resume pipeline is available.
- No two-hour autonomous mode is available.
- No Python packages are installed.
- Dirty worktree and running Unity are reported but do not stop preflight.
- The seed workflow contains only the `TEST-001` smoke-test task.

## Later Stages

Future stages can add implementation, audit/report flows, generated task handling, Unity validation, Git integration, resume support, and long-running automation. Those features are intentionally out of scope for BOOTSTRAP-02.
