from __future__ import annotations

from typing import Any


def build_implementation_prompt(task: dict[str, Any]) -> str:
    return "\n".join(
        [
            "ROLE: implementer",
            f"TASK: {task['id']} - {task['title']}",
            task["description"],
            "ACCEPTANCE CRITERIA:",
            *[f"- {item}" for item in task["acceptanceCriteria"]],
            f"ALLOWED WRITE PATHS: {task['allowedWritePaths']}",
            f"FORBIDDEN WRITE PATHS: {task['forbiddenWritePaths']}",
            "NON-GOALS:",
            *[f"- {item}" for item in task["nonGoals"]],
            "Stay within scope and return only the structured implementation result.",
        ]
    )


def build_audit_prompt(task: dict[str, Any], snapshot_summary: dict[str, Any], validation_results: list[dict[str, Any]], role_report: dict[str, Any]) -> str:
    return "\n".join(
        [
            "ROLE: auditor",
            f"TASK: {task['id']} - {task['title']}",
            f"DESCRIPTION: {task['description']}",
            "ACCEPTANCE CRITERIA:",
            *[f"- {item}" for item in task["acceptanceCriteria"]],
            f"ALLOWED WRITE PATHS: {task['allowedWritePaths']}",
            f"FORBIDDEN WRITE PATHS: {task['forbiddenWritePaths']}",
            "NON-GOALS:",
            *[f"- {item}" for item in task["nonGoals"]],
            f"ACTUAL CHANGED PATHS: {snapshot_summary.get('changedPaths', [])}",
            f"SNAPSHOT SUMMARY: {snapshot_summary}",
            f"ROLE REPORT: {role_report}",
            f"VALIDATION RESULTS: {validation_results}",
            "Do not modify files. Do not create new tasks.",
            "Return only the structured audit result.",
            "Verdict contract: PASS, FIX_REQUIRED, or BLOCKED.",
        ]
    )


def build_repair_prompt(task: dict[str, Any], repair_context: dict[str, Any]) -> str:
    return "\n".join(
        [
            "ROLE: repairer",
            f"TASK: {task['id']} - {task['title']}",
            f"ACTIVE FINDINGS: {repair_context['validationFindings'] + repair_context['auditFindings']}",
            f"ACCEPTANCE CRITERIA: {task['acceptanceCriteria']}",
            f"ALLOWED WRITE PATHS: {task['allowedWritePaths']}",
            f"FORBIDDEN WRITE PATHS: {task['forbiddenWritePaths']}",
            f"REPAIR ATTEMPT: {repair_context['repairAttempt']} of {repair_context['maxRepairAttempts']}",
            f"INVOCATION BUDGET REMAINING: {repair_context['remainingInvocationBudget']}",
            "Do not expand scope. Fix only the listed findings.",
            "Return only the structured repair result.",
        ]
    )
