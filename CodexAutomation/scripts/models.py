from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class CommandResult:
    name: str
    command: list[str]
    found: bool
    version: str | None
    path: str | None
    error: str | None


@dataclass(frozen=True)
class RepoContext:
    root: Path
    automation_root: Path
    dry_run: bool


def validate_workflow(data: Any) -> list[str]:
    errors: list[str] = []
    if not isinstance(data, dict):
        return ["Workflow root must be an object."]

    if data.get("format_version") != 1:
        errors.append("Workflow format_version must be 1.")

    tasks = data.get("tasks")
    if not isinstance(tasks, list):
        errors.append("Workflow tasks must be a list.")
        return errors

    seen_ids: set[str] = set()
    for index, task in enumerate(tasks):
        label = f"Task at index {index}"
        if not isinstance(task, dict):
            errors.append(f"{label} must be an object.")
            continue

        task_id = task.get("id")
        title = task.get("title")
        status = task.get("status")

        if not isinstance(task_id, str) or not task_id.strip():
            errors.append(f"{label} must have a non-empty string id.")
        elif task_id in seen_ids:
            errors.append(f"Duplicate task id: {task_id}.")
        else:
            seen_ids.add(task_id)

        if not isinstance(title, str) or not title.strip():
            errors.append(f"{label} must have a non-empty string title.")

        if status not in {"pending", "running", "complete", "blocked"}:
            errors.append(f"{label} has unsupported status: {status!r}.")

    return errors
