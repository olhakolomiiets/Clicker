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
        "Objective:",
        str(task.get("objective", "")),
        "Allowed write paths:",
        "\n".join(f"- {path}" for path in task.get("allowedWritePaths", [])),
        "Allowed delete paths:",
        "\n".join(f"- {path}" for path in task.get("allowedDeletePaths", [])) or "- none",
        "Expected outputs:",
        "\n".join(f"- {item.get('path')}" for item in task.get("expectedOutputs", []) if isinstance(item, dict)),
        "Completion criteria:",
        "\n".join(f"- {item}" for item in task.get("completionCriteria", [])),
        "Parent repository is not writable.",
        "Do not request network, package installation, Unity, Git commit, Git push, or apply.",
        "Use only the isolated workspace. Do not modify .git, .agents, AGENTS.md, task.json, or effective_policy.json.",
        instruction,
        f"Return only the configured structured role result with role={role}, status=SUCCESS, finalVerdict=PASS, errorCode=null when complete.",
    ]
    return "\n".join(lines) + "\n"
