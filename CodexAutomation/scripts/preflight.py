from __future__ import annotations

import os
import platform
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from file_utils import read_json, write_json_atomic
from models import CommandResult, RepoContext, validate_workflow


REPORT_NAME = "PREFLIGHT_REPORT.json"
STATE_FORMAT_VERSION = 1
IDLE_STATE = "idle"


def find_repo_root(start: Path) -> Path:
    current = start.resolve()
    for candidate in (current, *current.parents):
        if (candidate / ".git").exists():
            return candidate
    return current


def run_command(command: list[str], cwd: Path, timeout_seconds: int = 10) -> subprocess.CompletedProcess[str] | None:
    try:
        return subprocess.run(
            command,
            cwd=str(cwd),
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
        )
    except (OSError, subprocess.SubprocessError):
        return None


def inspect_command(name: str, commands: list[list[str]], cwd: Path) -> CommandResult:
    last_error: str | None = None
    for command in commands:
        executable = shutil.which(command[0])
        if executable is None:
            last_error = f"Command not found: {command[0]}"
            continue

        completed = run_command(command, cwd)
        if completed is None:
            last_error = f"Command failed to start: {' '.join(command)}"
            continue

        output = (completed.stdout or completed.stderr).strip()
        if completed.returncode == 0:
            return CommandResult(
                name=name,
                command=command,
                found=True,
                version=output.splitlines()[0] if output else None,
                path=executable,
                error=None,
            )

        last_error = output or f"Command returned exit code {completed.returncode}"

    return CommandResult(
        name=name,
        command=commands[0],
        found=False,
        version=None,
        path=shutil.which(commands[0][0]),
        error=last_error,
    )


def read_unity_version(project_version_path: Path) -> dict[str, str | None]:
    result: dict[str, str | None] = {
        "editor_version": None,
        "editor_version_with_revision": None,
    }
    if not project_version_path.exists():
        return result

    for line in project_version_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("m_EditorVersion:"):
            result["editor_version"] = line.split(":", 1)[1].strip()
        elif line.startswith("m_EditorVersionWithRevision:"):
            result["editor_version_with_revision"] = line.split(":", 1)[1].strip()
    return result


def find_unity_editor(root: Path, config: dict[str, Any], editor_version: str | None) -> str | None:
    if not editor_version:
        return None

    configured_root = config.get("unity", {}).get("hub_editor_root")
    candidates: list[Path] = []
    if isinstance(configured_root, str) and configured_root:
        candidates.append(Path(configured_root) / editor_version / "Editor" / "Unity.exe")

    program_files = os.environ.get("ProgramFiles")
    if program_files:
        candidates.append(Path(program_files) / "Unity" / "Hub" / "Editor" / editor_version / "Editor" / "Unity.exe")

    for candidate in candidates:
        if candidate.exists():
            return str(candidate)
    return None


def is_git_repository(root: Path) -> bool:
    completed = run_command(["git", "rev-parse", "--is-inside-work-tree"], root)
    return completed is not None and completed.returncode == 0 and completed.stdout.strip() == "true"


def current_branch(root: Path) -> str | None:
    completed = run_command(["git", "branch", "--show-current"], root)
    if completed is None or completed.returncode != 0:
        return None
    return completed.stdout.strip() or None


def git_status(root: Path) -> dict[str, Any]:
    completed = run_command(["git", "status", "--short"], root)
    if completed is None:
        return {"available": False, "dirty": None, "entries": [], "error": "Unable to run git status."}
    entries = [line for line in completed.stdout.splitlines() if line.strip()]
    return {
        "available": completed.returncode == 0,
        "dirty": bool(entries),
        "entries": entries,
        "error": None if completed.returncode == 0 else (completed.stderr.strip() or "git status failed."),
    }


def unity_processes() -> list[dict[str, Any]]:
    if platform.system().lower() != "windows":
        return []

    completed = run_command(
        ["powershell", "-NoProfile", "-Command", "Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,Path | ConvertTo-Json -Compress"],
        Path.cwd(),
    )
    if completed is None or completed.returncode != 0 or not completed.stdout.strip():
        return []

    import json

    try:
        data = json.loads(completed.stdout)
    except json.JSONDecodeError:
        return []

    if isinstance(data, dict):
        data = [data]
    if not isinstance(data, list):
        return []
    return [item for item in data if isinstance(item, dict)]


def ensure_runtime_workflow(runtime_path: Path, seed_data: dict[str, Any]) -> bool:
    if runtime_path.exists():
        try:
            runtime_data = read_json(runtime_path)
        except Exception:
            return False
        if isinstance(runtime_data, dict) and "id" not in runtime_data and "id" in seed_data:
            write_json_atomic(runtime_path, seed_data)
            return True
        return False
    write_json_atomic(runtime_path, seed_data)
    return True


def ensure_state(state_path: Path) -> bool:
    if state_path.exists():
        return False
    write_json_atomic(
        state_path,
        {
            "format_version": STATE_FORMAT_VERSION,
            "state": IDLE_STATE,
        },
    )
    return True


def ensure_runtime_directories(automation_root: Path) -> list[str]:
    created: list[str] = []
    for relative_path in (
        Path("runtime/logs"),
        Path("runtime/reports"),
        Path("runtime/results"),
        Path("runtime/workspaces"),
        Path("runtime/pipeline_runs"),
        Path("runtime/audits"),
        Path("runtime/generated_tasks"),
    ):
        directory = automation_root / relative_path
        if not directory.exists():
            directory.mkdir(parents=True, exist_ok=True)
            created.append(str(relative_path).replace("\\", "/"))
    return created


def normalize_report_path(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def serialize_command_result(result: CommandResult, inspected: bool = True) -> dict[str, Any]:
    data = result.__dict__.copy()
    data["inspected"] = inspected
    if not inspected:
        data["found"] = None
    return data


def run_preflight(context: RepoContext, inspect_codex: bool = True) -> dict[str, Any]:
    root = context.root
    config_path = context.automation_root / "config.json"
    seed_path = context.automation_root / "workflow.seed.json"
    runtime_path = context.automation_root / "workflow.runtime.json"
    state_path = context.automation_root / "state.json"
    report_path = context.automation_root / "runtime" / "reports" / REPORT_NAME
    runtime_directories_created = ensure_runtime_directories(context.automation_root)

    config: dict[str, Any] = {}
    config_error: str | None = None
    try:
        config = read_json(config_path)
    except Exception as exc:
        config_error = str(exc)

    tools_config = config.get("tools", {}) if isinstance(config, dict) else {}
    python_command = tools_config.get("python_command", "python")
    git_command = tools_config.get("git_command", "git")
    command_results = [
        inspect_command("python", [[str(python_command), "--version"]], root),
        inspect_command("git", [[str(git_command), "--version"]], root),
    ]
    if inspect_codex:
        codex_commands = tools_config.get("codex_commands", ["codex.cmd", "codex"])
        if not isinstance(codex_commands, list) or not codex_commands:
            codex_commands = ["codex.cmd", "codex"]

        codex_version_commands = [[str(command), "--version"] for command in codex_commands if isinstance(command, str)]
        if not codex_version_commands:
            codex_version_commands = [["codex.cmd", "--version"], ["codex", "--version"]]
        command_results.append(inspect_command("codex", codex_version_commands, root))
    else:
        command_results.append(
            CommandResult(
                name="codex",
                command=[],
                found=False,
                version=None,
                path=None,
                error="not_inspected",
            )
        )

    project_version_file = config.get("unity", {}).get("project_version_file", "ProjectSettings/ProjectVersion.txt") if isinstance(config, dict) else "ProjectSettings/ProjectVersion.txt"
    unity_version = read_unity_version(root / str(project_version_file))
    unity_editor_path = find_unity_editor(root, config, unity_version["editor_version"])
    unity_running = unity_processes()

    seed_data: dict[str, Any] | None = None
    seed_error: str | None = None
    runtime_created = False
    try:
        loaded_seed = read_json(seed_path)
        if isinstance(loaded_seed, dict):
            seed_data = loaded_seed
            runtime_created = ensure_runtime_workflow(runtime_path, seed_data)
        else:
            seed_error = "Seed workflow root must be an object."
    except Exception as exc:
        seed_error = str(exc)

    runtime_error: str | None = None
    runtime_data: Any = None
    try:
        runtime_data = read_json(runtime_path)
    except Exception as exc:
        runtime_error = str(exc)

    seed_validation = validate_workflow(seed_data) if seed_data is not None else [seed_error or "Seed workflow could not be loaded."]
    runtime_validation = validate_workflow(runtime_data) if runtime_error is None else [runtime_error]

    state_error: str | None = None
    state_created = False
    try:
        state_created = ensure_state(state_path)
        read_json(state_path)
    except Exception as exc:
        state_error = str(exc)

    status = git_status(root)
    stage = config.get("automation_stage", "BOOTSTRAP-01") if isinstance(config, dict) else "BOOTSTRAP-01"
    report: dict[str, Any] = {
        "format_version": 1,
        "stage": stage,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "dry_run": context.dry_run,
        "repository": {
            "root": str(root),
            "is_git_repository": is_git_repository(root),
            "branch": current_branch(root),
            "dirty": status["dirty"],
            "status_entries": status["entries"],
            "status_error": status["error"],
            "agents_md_found": (root / "AGENTS.md").exists(),
        },
        "config": {
            "path": normalize_report_path(root, config_path),
            "loaded": config_error is None,
            "error": config_error,
        },
        "tools": [serialize_command_result(result, inspected=(result.error != "not_inspected")) for result in command_results],
        "python_runtime": {
            "executable": sys.executable,
            "version": sys.version.split()[0],
        },
        "unity": {
            "project_version": unity_version,
            "editor_path": unity_editor_path,
            "editor_found": unity_editor_path is not None,
            "running": bool(unity_running),
            "running_processes": unity_running,
        },
        "workflow": {
            "seed_path": normalize_report_path(root, seed_path),
            "runtime_path": normalize_report_path(root, runtime_path),
            "runtime_created": runtime_created,
            "runtime_directories_created": runtime_directories_created,
            "seed_validation_errors": seed_validation,
            "runtime_validation_errors": runtime_validation,
        },
        "state": {
            "path": normalize_report_path(root, state_path),
            "loaded": state_error is None,
            "created": state_created,
            "error": state_error,
        },
        "notes": [
            "Dirty worktree is reported but does not fail preflight.",
            "Running Unity Editor is reported but does not fail preflight.",
            "No Codex exec or Unity batchmode command is launched by dry-run preflight.",
            "Codex executable version inspection is skipped for BOOTSTRAP-03B-2A real-task validate/plan modes.",
        ],
    }

    report["ok"] = (
        config_error is None
        and not seed_validation
        and not runtime_validation
        and state_error is None
    )

    write_json_atomic(report_path, report)
    return report


def format_summary(report: dict[str, Any]) -> str:
    lines = [
        "Codex Automation Preflight",
        f"Stage: {report['stage']}",
        f"Dry run: {report['dry_run']}",
        f"Repository: {report['repository']['root']}",
        f"Git repository: {report['repository']['is_git_repository']}",
        f"Branch: {report['repository']['branch'] or 'unknown'}",
        f"Dirty worktree: {report['repository']['dirty']}",
        f"AGENTS.md found: {report['repository']['agents_md_found']}",
        f"Unity version: {report['unity']['project_version'].get('editor_version') or 'unknown'}",
        f"Unity editor path: {report['unity']['editor_path'] or 'not found'}",
        f"Unity running: {report['unity']['running']}",
        "Tools:",
    ]
    for tool in report["tools"]:
        if tool.get("inspected") is False:
            status = "not inspected"
        else:
            status = "found" if tool["found"] else "missing"
        detail = tool["version"] or tool["error"] or "no version output"
        lines.append(f"  - {tool['name']}: {status} ({detail})")
    lines.extend(
        [
            f"Seed workflow errors: {len(report['workflow']['seed_validation_errors'])}",
            f"Runtime workflow errors: {len(report['workflow']['runtime_validation_errors'])}",
            f"Report: CodexAutomation/runtime/reports/{REPORT_NAME}",
            f"Overall ok: {report['ok']}",
        ]
    )
    return "\n".join(lines)
