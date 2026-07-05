from __future__ import annotations

from pathlib import Path
from typing import Any

from parent_git_snapshot import parent_git_snapshot, snapshots_equal
from real_task_change_runner import _parent_source_changed
from real_task_execution_context import resolve_execution_authority
from real_task_execution_models import RealTaskExecutionFailure, canonical_sha256
from real_task_workspace import git_fingerprint, validate_service_files
from task_manifest_validator import parse_real_task_policy
from workspace_change_inventory import build_change_inventory
from workspace_change_policy import parse_change_policy
from workspace_inventory import InventoryError
from file_utils import read_json


def capture_integrity_snapshot(handle: Any, root: Path, automation_root: Path, config: dict[str, Any], label: str, expected_states: tuple[str, ...] | None = None) -> dict[str, Any]:
    try:
        context = resolve_execution_authority(handle, automation_root, expected_states).context
        workspace = Path(context["workspaceIdentity"]["workspaceDirectory"])
        foundation_dir = Path(context["foundationRunDirectory"])
        trusted_context = read_json(foundation_dir / "TRUSTED_RUN_CONTEXT.json")
        baseline = read_json(foundation_dir / "WORKSPACE_BASELINE_INVENTORY.json")
        service_registry = list(baseline.get("serviceRegistry", []))
        real_policy, real_errors = parse_real_task_policy(config)
        change_policy, change_errors = parse_change_policy(config, real_policy)
        if real_errors or change_errors or change_policy is None:
            first = (real_errors + change_errors)[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        final_inventory = build_change_inventory(
            workspace,
            str(trusted_context["runId"]),
            str(trusted_context["taskId"]),
            str(context["trustedContextHash"]),
            f"execution_{label}",
            service_registry,
            change_policy,
            int(config["realTaskFoundation"]["maxInventoryEntries"]),
        )
        service_files_valid, service_error = validate_service_files(workspace, service_registry, int(config["realTaskFoundation"]["copyChunkBytes"]))
        agents_valid = _agents_valid(workspace)
        isolated_git = git_fingerprint(workspace)
        isolated_git_hash = canonical_sha256(isolated_git)
        baseline_git = baseline.get("isolatedGitFingerprint", {})
        expected_git_hash = baseline_git.get("workspaceFingerprintSha256") or canonical_sha256(baseline_git)
        parent_git, git_errors = parent_git_snapshot(root)
        if git_errors:
            first = git_errors[0]
            raise RealTaskExecutionFailure(first.code, first.message)
        source_schema = read_json(automation_root / "schemas" / "real_task_source_inventory.schema.json")
        parent_source_changed = _parent_source_changed(
            root,
            foundation_dir,
            trusted_context,
            str(context["initialParentSourceInventoryHash"]),
            int(config["realTaskFoundation"]["maxInventoryEntries"]),
            int(config["realTaskFoundation"]["copyChunkBytes"]),
            source_schema,
        )
        parent_git_changed = not snapshots_equal(trusted_context["parentGitInitialSnapshot"], parent_git)
        return {
            "label": label,
            "orchestrationRunId": context["orchestrationRunId"],
            "taskId": context["taskId"],
            "workspaceInventoryHash": final_inventory["inventorySha256"],
            "workspaceContentHash": _workspace_content_hash(final_inventory),
            "workspaceIdentity": context["workspaceIdentity"],
            "serviceFilesHash": canonical_sha256(service_registry),
            "serviceFilesValid": service_files_valid,
            "serviceError": service_error,
            "agentsDirectoryHash": canonical_sha256(_agents_listing(workspace)),
            "agentsDirectoryValid": agents_valid,
            "isolatedGitFingerprintHash": isolated_git_hash,
            "isolatedGitValid": isolated_git_hash == expected_git_hash,
            "parentGitSnapshotHash": canonical_sha256(parent_git),
            "parentGitChanged": parent_git_changed,
            "parentSourceInventoryHash": context["initialParentSourceInventoryHash"],
            "parentSourceChanged": parent_source_changed,
            "runtimeContainmentValid": _contained(workspace, foundation_dir),
            "finalInventory": final_inventory,
        }
    except InventoryError as exc:
        raise RealTaskExecutionFailure(exc.code, exc.message) from exc


def trust_integrity_pass(snapshot: dict[str, Any]) -> bool:
    return (
        snapshot.get("serviceFilesValid") is True
        and snapshot.get("agentsDirectoryValid") is True
        and snapshot.get("isolatedGitValid") is True
        and snapshot.get("parentGitChanged") is False
        and snapshot.get("parentSourceChanged") is False
        and snapshot.get("runtimeContainmentValid") is True
    )


def compare_full_workspace_unchanged(before: dict[str, Any], after: dict[str, Any]) -> bool:
    keys = (
        "workspaceContentHash",
        "serviceFilesHash",
        "agentsDirectoryHash",
        "isolatedGitFingerprintHash",
        "parentGitSnapshotHash",
        "parentSourceInventoryHash",
    )
    return all(before.get(key) == after.get(key) for key in keys) and trust_integrity_pass(after)


def _agents_valid(workspace: Path) -> bool:
    agents = workspace / ".agents"
    return agents.exists() and agents.is_dir() and not agents.is_symlink() and _agents_listing(workspace) == []


def _agents_listing(workspace: Path) -> list[str]:
    agents = workspace / ".agents"
    if not agents.exists() or not agents.is_dir():
        return ["<missing-or-not-directory>"]
    return sorted(path.relative_to(agents).as_posix() for path in agents.rglob("*"))


def _workspace_content_hash(inventory: dict[str, Any]) -> str:
    return canonical_sha256(
        {
            "entries": inventory.get("entries", []),
            "serviceRegistry": inventory.get("serviceRegistry", []),
            "isolatedGitFingerprint": inventory.get("isolatedGitFingerprint"),
            "fileCount": inventory.get("fileCount"),
            "directoryCount": inventory.get("directoryCount"),
            "totalBytes": inventory.get("totalBytes"),
        }
    )


def _contained(path: Path, container: Path) -> bool:
    try:
        path.resolve(strict=True).relative_to(container.resolve(strict=True))
        return True
    except (OSError, ValueError):
        return False
