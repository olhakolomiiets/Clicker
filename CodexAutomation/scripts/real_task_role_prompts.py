from __future__ import annotations

from typing import Any

from real_task_execution_models import canonical_sha256


def build_implementer_prompt(task: dict[str, Any], context: dict[str, Any]) -> str:
    return _bounded_prompt("IMPLEMENTER", task, context, "Make only manifest-authorized isolated workspace changes.")


def build_repair_prompt(task: dict[str, Any], context: dict[str, Any], diagnostic: dict[str, Any]) -> str:
    return _bounded_prompt("REPAIRER", task, context, f"Repair only these host findings: {diagnostic.get('findings', [])!r}")


def build_auditor_prompt(task: dict[str, Any], context: dict[str, Any], validation: dict[str, Any]) -> str:
    return _bounded_prompt("AUDITOR", task, context, f"Read-only audit after host validation: {validation.get('finalVerdict')}")


def prompt_hash(prompt: str) -> str:
    return canonical_sha256({"prompt": prompt})


def _bounded_prompt(role: str, task: dict[str, Any], context: dict[str, Any], instruction: str) -> str:
    lines = [
        f"Role: {role}",
        f"TaskId: {task.get('taskId')}",
        f"OrchestrationRunId: {context.get('orchestrationRunId')}",
        "Parent repository is not writable.",
        "Do not request network, package installation, Unity, Git commit, Git push, or apply.",
        instruction,
        "Return only the configured structured role result.",
    ]
    return "\n".join(lines) + "\n"
