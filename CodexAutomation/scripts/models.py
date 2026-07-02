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

    workflow_id = data.get("id")
    if not isinstance(workflow_id, str) or not workflow_id.strip():
        errors.append("Workflow id must be a non-empty string.")

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
        elif isinstance(workflow_id, str) and task_id == workflow_id:
            errors.append(f"{label} id must be distinct from workflow id.")
        elif task_id in seen_ids:
            errors.append(f"Duplicate task id: {task_id}.")
        else:
            seen_ids.add(task_id)

        if not isinstance(title, str) or not title.strip():
            errors.append(f"{label} must have a non-empty string title.")

        if status not in {"pending", "running", "complete", "blocked"}:
            errors.append(f"{label} has unsupported status: {status!r}.")

    return errors


def validate_smoke_task(task: dict[str, Any], expected_task_id: str) -> list[str]:
    errors: list[str] = []
    if task.get("id") != expected_task_id:
        errors.append(f"Smoke task id must be {expected_task_id}.")
    if task.get("mode") != "read_only":
        errors.append("Smoke task mode must be read_only.")
    for field_name in ("taskFile", "promptFile", "schemaFile"):
        if not isinstance(task.get(field_name), str) or not task[field_name].strip():
            errors.append(f"Smoke task {field_name} must be a non-empty string.")
    timeout_seconds = task.get("timeoutSeconds")
    if not isinstance(timeout_seconds, int) or timeout_seconds <= 0:
        errors.append("Smoke task timeoutSeconds must be a positive integer.")
    if task.get("allowedWritePaths") != []:
        errors.append("Smoke task allowedWritePaths must be an empty list.")
    forbidden_write_paths = task.get("forbiddenWritePaths")
    if not isinstance(forbidden_write_paths, list) or not all(isinstance(item, str) for item in forbidden_write_paths):
        errors.append("Smoke task forbiddenWritePaths must be a list of strings.")
    return errors


def find_task(workflow: dict[str, Any], task_id: str) -> dict[str, Any] | None:
    tasks = workflow.get("tasks")
    if not isinstance(tasks, list):
        return None
    for task in tasks:
        if isinstance(task, dict) and task.get("id") == task_id:
            return task
    return None
