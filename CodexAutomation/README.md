# Codex Automation Bootstrap

This is BOOTSTRAP-01 for the local Codex Automation system.

The current stage is intentionally read-only for the Unity project. It creates a safe local scaffold and runs a preflight report. It does not run Codex tasks, does not launch Unity batchmode, and does not change files inside `Assets`, `Packages`, or `ProjectSettings`.

## Created Files

- `config.json`
- `workflow.seed.json`
- `workflow.runtime.json`
- `state.json`
- `start_codex_pipeline.bat`
- `CodexAutomation/scripts/orchestrator.py`
- `CodexAutomation/scripts/preflight.py`
- `CodexAutomation/scripts/models.py`
- `CodexAutomation/scripts/file_utils.py`
- `CodexAutomation/runtime/logs/`
- `CodexAutomation/runtime/reports/`
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

`--dry-run` is the default mode at this stage.

The report is written to:

```text
CodexAutomation/runtime/reports/PREFLIGHT_REPORT.json
```

Runtime files are created automatically when missing and are not stored in Git:

- `CodexAutomation/workflow.runtime.json`
- `CodexAutomation/state.json`
- `CodexAutomation/runtime/`

## Current Limits

- No `codex exec` is started.
- No Unity Editor or Unity batchmode process is started.
- No Git commits are created.
- No Python packages are installed.
- Dirty worktree and running Unity are reported but do not stop preflight.
- The seed workflow contains only the `TEST-001` smoke-test task.

## Later Stages

Future stages can add the Codex runner, audit/report flows, generated task handling, Unity validation, and Git integration. Those features are intentionally out of scope for BOOTSTRAP-01.
