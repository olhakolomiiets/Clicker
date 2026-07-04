from __future__ import annotations

import subprocess
from pathlib import Path
from typing import Any

from real_task_models import RealTaskError


GIT_MAX_LINES = 5000
GIT_MAX_OUTPUT_CHARS = 20000
GIT_COMMAND_TIMEOUT_SECONDS = 10
REQUIRED_GIT_SNAPSHOT_COMMANDS = (
    "insideWorkTree",
    "porcelainStatus",
    "branch",
    "head",
    "stagedPaths",
)


def parent_git_snapshot(root: Path) -> tuple[dict[str, Any], list[RealTaskError]]:
    commands = {
        "insideWorkTree": ["git", "rev-parse", "--is-inside-work-tree"],
        "porcelainStatus": ["git", "status", "--porcelain=v1", "--untracked-files=all"],
        "branch": ["git", "branch", "--show-current"],
        "head": ["git", "rev-parse", "HEAD"],
        "stagedPaths": ["git", "diff", "--cached", "--name-only"],
    }
    results = {name: _run_git_command(root, command) for name, command in commands.items()}
    errors: list[RealTaskError] = []
    failed = [name for name, result in results.items() if not result["success"]]
    inside = _git_single_line(results["insideWorkTree"])
    head = _git_single_line(results["head"])
    branch_value = _git_single_line(results["branch"])
    if failed:
        errors.append(_error("REAL_TASK_GIT_SNAPSHOT_FAILED", "Parent Git snapshot command failed.", "parentGitSnapshot"))
    elif inside != "true":
        errors.append(_error("REAL_TASK_GIT_SNAPSHOT_FAILED", "Parent repository is not a Git work tree.", "parentGitSnapshot"))
    elif not head:
        errors.append(_error("REAL_TASK_GIT_SNAPSHOT_FAILED", "Parent Git HEAD could not be determined.", "parentGitSnapshot"))

    status_lines = _git_lines_from_result(results["porcelainStatus"])
    staged_lines = _git_lines_from_result(results["stagedPaths"])
    snapshot_failed = bool(errors)
    return {
        "status": "failed" if snapshot_failed else "success",
        "gitRepository": None if snapshot_failed else True,
        "head": head if head else None,
        "branch": branch_value if branch_value else None,
        "detachedHead": bool(results["branch"]["success"] and not branch_value),
        "stagedPaths": staged_lines if not snapshot_failed else [],
        "porcelainStatus": status_lines if not snapshot_failed else [],
        "dirty": None if snapshot_failed else bool(status_lines),
        "commandResults": {name: _public_git_result(result) for name, result in results.items()},
    }, errors


def snapshots_equal(left: dict[str, Any], right: dict[str, Any]) -> bool:
    keys = ("status", "gitRepository", "head", "branch", "detachedHead", "stagedPaths", "porcelainStatus", "dirty")
    return all(left.get(key) == right.get(key) for key in keys)


def _run_git_command(root: Path, command: list[str]) -> dict[str, Any]:
    try:
        completed = subprocess.run(
            command,
            cwd=str(root),
            capture_output=True,
            text=True,
            check=False,
            timeout=GIT_COMMAND_TIMEOUT_SECONDS,
        )
    except OSError as exc:
        return {
            "command": command,
            "exitCode": None,
            "stdout": "",
            "stderr": _bounded_text(type(exc).__name__),
            "success": False,
            "failure": "process_start_failed",
        }
    except subprocess.SubprocessError as exc:
        return {
            "command": command,
            "exitCode": None,
            "stdout": "",
            "stderr": _bounded_text(type(exc).__name__),
            "success": False,
            "failure": "process_failed",
        }
    stdout = _bounded_text(completed.stdout)
    stderr = _bounded_text(completed.stderr)
    too_many_lines = len(stdout.splitlines()) > GIT_MAX_LINES
    too_large = len(completed.stdout or "") > GIT_MAX_OUTPUT_CHARS or len(completed.stderr or "") > GIT_MAX_OUTPUT_CHARS
    return {
        "command": command,
        "exitCode": completed.returncode,
        "stdout": stdout,
        "stderr": stderr,
        "success": completed.returncode == 0 and not too_many_lines and not too_large,
        "failure": "output_limit_exceeded" if completed.returncode == 0 and (too_many_lines or too_large) else None,
    }


def _public_git_result(result: dict[str, Any]) -> dict[str, Any]:
    return {
        "command": list(result["command"]),
        "exitCode": result["exitCode"],
        "success": result["success"],
        "stderr": result["stderr"],
        "failure": result["failure"],
    }


def _git_lines_from_result(result: dict[str, Any]) -> list[str]:
    if not result["success"]:
        return []
    return result["stdout"].splitlines()


def _git_single_line(result: dict[str, Any]) -> str | None:
    lines = _git_lines_from_result(result)
    if not lines:
        return ""
    if len(lines) > 1:
        return None
    return lines[0].strip()


def _bounded_text(value: str | None) -> str:
    text = value or ""
    if len(text) > GIT_MAX_OUTPUT_CHARS:
        return text[:GIT_MAX_OUTPUT_CHARS] + "...[truncated]"
    return text


def _error(code: str, message: str, field: str | None = None) -> RealTaskError:
    return RealTaskError(code, message, field)
