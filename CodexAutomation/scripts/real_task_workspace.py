from __future__ import annotations

import hashlib
import json
import os
import shutil
import stat
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from file_utils import write_json_atomic
from real_task_foundation_models import FoundationPolicy, TrustedFoundationContext, canonical_sha256
from workspace_inventory import (
    InventoryError,
    build_workspace_inventory,
    compare_source_inventories,
    file_sha256,
    inventory_entries_by_path,
    is_symlink_or_reparse,
)


HOST_AGENTS_TEXT = """# Host-Controlled Foundation Workspace Rules

- Work only inside this isolated workspace.
- Never modify the parent repository.
- Service files are immutable.
- `.git`, `.agents`, `AGENTS.md`, `task.json`, and `effective_policy.json` are immutable.
- Host policy overrides project instructions.
- Network, package installation, Unity, Codex execution, and parent apply are forbidden in this foundation stage.
- Allowed source, write, and delete paths come from the trusted host context.
- Reports outside this workspace are not role-writable.
"""


@dataclass(frozen=True)
class WorkspacePaths:
    repository_root: Path
    runtime_root: Path
    run_directory: Path
    staging: Path
    workspace: Path
    evidence: Path
    logs: Path


def relative_to_root(root: Path, path: Path) -> str:
    return path.resolve(strict=False).relative_to(root.resolve(strict=False)).as_posix()


def create_run_paths(root: Path, policy: FoundationPolicy, run_id: str) -> WorkspacePaths:
    runtime_root = root / policy.runtimeRoot
    _validate_runtime_path(root, runtime_root)
    run_directory = runtime_root / run_id
    if run_directory.exists() or run_directory.is_symlink():
        raise InventoryError("REAL_TASK_RUN_DIRECTORY_EXISTS", "Foundation run directory already exists.", run_id)
    run_directory.mkdir(parents=True)
    _validate_runtime_path(root, run_directory, runtime_root)
    evidence = run_directory / "evidence"
    logs = run_directory / "logs"
    staging = run_directory / "staging"
    workspace = run_directory / "workspace"
    evidence.mkdir()
    logs.mkdir()
    staging.mkdir()
    for path in (evidence, logs, staging):
        _validate_runtime_path(root, path, run_directory)
    return WorkspacePaths(root, runtime_root, run_directory, staging, workspace, evidence, logs)


def create_service_files(
    root: Path,
    staging: Path,
    evidence: Path,
    manifest: dict[str, Any],
    effective_policy: dict[str, Any],
    context: TrustedFoundationContext,
    chunk_bytes: int,
) -> tuple[list[dict[str, Any]], list[str]]:
    service_paths: list[str] = []
    _write_text(staging / "AGENTS.md", HOST_AGENTS_TEXT)
    service_paths.append("AGENTS.md")
    (staging / ".agents").mkdir()
    service_paths.append(".agents")
    write_json_atomic(staging / "task.json", manifest)
    service_paths.append("task.json")
    write_json_atomic(staging / "effective_policy.json", {"hostPolicy": effective_policy, "foundationPolicy": context.effectiveFoundationPolicy})
    service_paths.append("effective_policy.json")
    parent_agents = root / "AGENTS.md"
    evidence_entry: dict[str, Any] = {"path": "evidence/PARENT_ROOT_AGENTS.md", "present": False, "sha256": None, "evidencePath": None}
    if parent_agents.exists() or parent_agents.is_symlink():
        if is_symlink_or_reparse(parent_agents) or not parent_agents.is_file():
            raise InventoryError("REAL_TASK_SERVICE_FILE_INVALID", "Parent root AGENTS.md evidence is not a regular file.", "AGENTS.md")
        evidence_target = evidence / "PARENT_ROOT_AGENTS.md"
        _copy_regular_file_bytes(parent_agents, evidence_target, chunk_bytes)
        evidence_entry = {
            "path": "evidence/PARENT_ROOT_AGENTS.md",
            "present": True,
            "sha256": file_sha256(parent_agents, chunk_bytes),
            "evidencePath": "evidence/PARENT_ROOT_AGENTS.md",
        }
    registry = _service_registry(staging, set(service_paths), "host_generated", chunk_bytes)
    registry.append(
        {
            "path": evidence_entry["path"],
            "kind": "file",
            "expectedType": "file" if evidence_entry["present"] else "missing",
            "expectedSha256": evidence_entry["sha256"],
            "mutability": "immutable",
            "origin": "parent_evidence",
            "workspacePath": None,
            "evidencePath": evidence_entry["evidencePath"],
        }
    )
    return registry, service_paths


def expand_ancestor_agents(root: Path, source_paths: list[str], allowed_roots: tuple[str, ...], chunk_bytes: int) -> list[dict[str, Any]]:
    expanded: dict[str, dict[str, Any]] = {}
    for raw in source_paths:
        parts = raw.split("/")
        if not parts or parts[0] not in allowed_roots:
            continue
        if (root / raw).is_dir():
            ancestor_parts = parts
        else:
            ancestor_parts = parts[:-1]
        for depth in range(1, len(ancestor_parts) + 1):
            candidate_rel = "/".join(ancestor_parts[:depth] + ["AGENTS.md"])
            if candidate_rel == "AGENTS.md":
                continue
            candidate = root / candidate_rel
            if not candidate.exists() and not candidate.is_symlink():
                continue
            if is_symlink_or_reparse(candidate) or not candidate.is_file():
                raise InventoryError("REAL_TASK_SERVICE_FILE_INVALID", "Applicable AGENTS.md is unsafe.", candidate_rel)
            expanded[candidate_rel] = {
                "path": candidate_rel,
                "sha256": file_sha256(candidate, chunk_bytes),
                "origin": "ancestor_project_instruction",
            }
    return [expanded[path] for path in sorted(expanded)]


def copy_sources(
    root: Path,
    staging: Path,
    source_inventory: dict[str, Any],
    chunk_bytes: int,
    service_paths: set[str],
    allowed_source_roots: tuple[str, ...] = ("Assets", "Packages", "ProjectSettings"),
) -> dict[str, Any]:
    copied_files: list[dict[str, Any]] = []
    copied_directories: list[str] = []
    root_resolved = root.resolve(strict=True)
    staging_resolved = staging.resolve(strict=True)
    for entry in source_inventory.get("entries", []):
        relative = str(entry["path"])
        source = root_resolved / relative
        destination = staging_resolved / relative
        _validate_destination(staging_resolved, destination)
        if entry["type"] == "directory":
            destination.mkdir(parents=True, exist_ok=True)
            copied_directories.append(relative)
            continue
        if entry["type"] != "file":
            raise InventoryError("REAL_TASK_UNSUPPORTED_FILE_TYPE", "Only files and directories can be copied.", relative)
        result = _copy_verified_file(root_resolved, source, destination, chunk_bytes, allowed_source_roots)
        result["path"] = relative
        result["serviceClassification"] = "immutable_service" if relative in service_paths else entry.get("serviceClassification", "task_content")
        copied_files.append(result)
    return {
        "copiedFiles": copied_files,
        "copiedDirectories": sorted(copied_directories),
        "copiedBytes": sum(int(item["bytes"]) for item in copied_files),
    }


def verify_destination_matches_source(staging: Path, source_inventory: dict[str, Any], max_entries: int, chunk_bytes: int, service_paths: set[str]) -> dict[str, Any]:
    destination_inventory = build_workspace_inventory(staging, max_entries, chunk_bytes, service_paths)
    source_entries = inventory_entries_by_path(source_inventory)
    destination_entries = inventory_entries_by_path(destination_inventory)
    allowed_generated_directories = _required_ancestor_directories(set(source_entries) | set(service_paths))
    projected_destination = {path: destination_entries.get(path) for path in source_entries}
    missing = sorted(path for path, entry in projected_destination.items() if entry is None)
    mismatched: list[str] = []
    for path, source_entry in source_entries.items():
        destination_entry = projected_destination.get(path)
        if destination_entry is None:
            continue
        for key in ("type", "size", "sha256"):
            if source_entry.get(key) != destination_entry.get(key):
                mismatched.append(path)
                break
    extras = sorted(
        path
        for path in destination_entries
        if path not in source_entries
        and not _is_registered_service_path(path, service_paths)
        and path not in allowed_generated_directories
    )
    if missing or mismatched or extras:
        raise InventoryError("REAL_TASK_SOURCE_HASH_MISMATCH", "Destination inventory does not match verified source inventory.", ",".join(missing + mismatched + extras))
    return destination_inventory


def validate_service_files(staging: Path, registry: list[dict[str, Any]], chunk_bytes: int) -> tuple[bool, str | None]:
    root = staging.resolve(strict=True)
    for item in registry:
        workspace_path = item.get("workspacePath")
        if not workspace_path:
            continue
        relative = str(workspace_path)
        path = root / relative
        try:
            path.resolve(strict=False).relative_to(root)
        except ValueError:
            return False, "REAL_TASK_DESTINATION_ESCAPE"
        if relative == ".agents":
            if not path.exists() or is_symlink_or_reparse(path) or not path.is_dir() or any(path.iterdir()):
                return False, "REAL_TASK_SERVICE_FILE_INVALID"
            continue
        if relative == ".git" or relative.startswith(".git/"):
            continue
        if not path.exists() or is_symlink_or_reparse(path) or not path.is_file():
            return False, "REAL_TASK_SERVICE_FILE_INVALID"
        if item.get("expectedSha256") and file_sha256(path, chunk_bytes) != item.get("expectedSha256"):
            return False, "REAL_TASK_SERVICE_FILE_INVALID"
    return True, None


def create_standalone_git(staging: Path, run_directory: Path, timeout_seconds: int = 10) -> dict[str, Any]:
    template = run_directory / "empty_git_template"
    template.mkdir()
    env = dict(os.environ)
    env["GIT_CONFIG_NOSYSTEM"] = "1"
    env["HOME"] = str(run_directory)
    env["XDG_CONFIG_HOME"] = str(run_directory)
    try:
        completed = subprocess.run(
            ["git", "init", "--template", str(template)],
            cwd=str(staging),
            capture_output=True,
            text=True,
            check=False,
            timeout=timeout_seconds,
            env=env,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        raise InventoryError("REAL_TASK_ISOLATED_GIT_INVALID", f"git init failed to start: {type(exc).__name__}.", ".git") from exc
    if completed.returncode != 0:
        raise InventoryError("REAL_TASK_ISOLATED_GIT_INVALID", "git init failed.", ".git")
    return git_fingerprint(staging)


def git_fingerprint(workspace: Path) -> dict[str, Any]:
    git_dir = workspace / ".git"
    if not git_dir.exists() or git_dir.is_symlink() or not git_dir.is_dir():
        raise InventoryError("REAL_TASK_ISOLATED_GIT_INVALID", ".git is missing or invalid.", ".git")
    git_file = workspace / ".git"
    if git_file.is_file():
        raise InventoryError("REAL_TASK_ISOLATED_GIT_INVALID", ".git file links are forbidden.", ".git")
    hooks = git_dir / "hooks"
    active_hooks: list[str] = []
    if hooks.exists():
        for hook in hooks.iterdir():
            if hook.is_file() and not hook.name.endswith(".sample"):
                active_hooks.append(hook.name)
    remotes = _git_output(workspace, ["git", "remote"])
    staged = _git_output(workspace, ["git", "diff", "--cached", "--name-only"])
    refs = _git_output(workspace, ["git", "for-each-ref", "--format=%(refname):%(objectname)"])
    head = _git_output(workspace, ["git", "rev-parse", "--verify", "HEAD"])
    for result in (remotes, staged, refs):
        if not result["ok"]:
            raise InventoryError("REAL_TASK_ISOLATED_GIT_INVALID", "Standalone .git fingerprint command failed.", ".git")
    config_path = git_dir / "config"
    config_hash = file_sha256(config_path) if config_path.exists() and config_path.is_file() else None
    fingerprint = {
        "gitDirectoryValid": True,
        "gitFilePointerPresent": False,
        "head": head["stdout"] if head["ok"] else None,
        "stagedPaths": sorted(staged["stdout"].splitlines()) if staged["ok"] and staged["stdout"] else [],
        "remotes": sorted(remotes["stdout"].splitlines()) if remotes["ok"] and remotes["stdout"] else [],
        "refs": sorted(refs["stdout"].splitlines()) if refs["ok"] and refs["stdout"] else [],
        "activeHooks": sorted(active_hooks),
        "configSha256": config_hash,
        "baselineCommitCreated": False,
    }
    if fingerprint["remotes"] or fingerprint["stagedPaths"] or fingerprint["activeHooks"] or fingerprint["head"] is not None:
        raise InventoryError("REAL_TASK_ISOLATED_GIT_INVALID", "Standalone .git has unsafe state.", ".git")
    return fingerprint


def _git_output(cwd: Path, command: list[str]) -> dict[str, Any]:
    try:
        completed = subprocess.run(command, cwd=str(cwd), capture_output=True, text=True, check=False, timeout=10)
    except (OSError, subprocess.SubprocessError):
        return {"ok": False, "stdout": ""}
    return {"ok": completed.returncode == 0, "stdout": completed.stdout.strip()}


def promote_staging_to_workspace(paths: WorkspacePaths) -> None:
    if paths.workspace.exists() or paths.workspace.is_symlink():
        raise InventoryError("REAL_TASK_WORKSPACE_PROMOTION_FAILED", "Workspace already exists before promotion.", "workspace")
    try:
        paths.staging.replace(paths.workspace)
    except OSError as exc:
        raise InventoryError("REAL_TASK_WORKSPACE_PROMOTION_FAILED", f"Staging promotion failed: {type(exc).__name__}.", "staging") from exc
    _validate_runtime_path(paths.repository_root, paths.workspace, paths.run_directory)


def _copy_verified_file(root: Path, source: Path, destination: Path, chunk_bytes: int, allowed_source_roots: tuple[str, ...] = ("Assets", "Packages", "ProjectSettings")) -> dict[str, Any]:
    validate_existing_source_file_component_chain(root, source, allowed_source_roots)
    try:
        source_pre = source.stat(follow_symlinks=False)
    except OSError as exc:
        raise InventoryError("REAL_TASK_SOURCE_COPY_FAILED", f"Source stat failed: {type(exc).__name__}.", source.as_posix()) from exc
    if not stat.S_ISREG(source_pre.st_mode):
        raise InventoryError("REAL_TASK_UNSUPPORTED_FILE_TYPE", "Source is not a regular file.", source.as_posix())
    destination.parent.mkdir(parents=True, exist_ok=True)
    fd, temp_name = tempfile.mkstemp(prefix=f".{destination.name}.", suffix=".tmp", dir=str(destination.parent))
    temp_path = Path(temp_name)
    digest = hashlib.sha256()
    copied = 0
    try:
        with source.open("rb") as source_handle, os.fdopen(fd, "wb") as dest_handle:
            while True:
                chunk = source_handle.read(chunk_bytes)
                if not chunk:
                    break
                digest.update(chunk)
                dest_handle.write(chunk)
                copied += len(chunk)
            dest_handle.flush()
            os.fsync(dest_handle.fileno())
        source_hash_during = digest.hexdigest()
        if copied != source_pre.st_size:
            raise InventoryError("REAL_TASK_SOURCE_HASH_MISMATCH", "Copied byte count differs from source size.", source.as_posix())
        if is_symlink_or_reparse(temp_path) or not temp_path.is_file():
            raise InventoryError("REAL_TASK_DESTINATION_REPARSE", "Temporary destination is unsafe.", destination.as_posix())
        destination_hash = file_sha256(temp_path, chunk_bytes)
        if destination_hash != source_hash_during:
            raise InventoryError("REAL_TASK_SOURCE_HASH_MISMATCH", "Destination hash mismatch.", destination.as_posix())
        validate_existing_source_file_component_chain(root, source, allowed_source_roots)
        source_post = source.stat(follow_symlinks=False)
        if not stat.S_ISREG(source_post.st_mode) or source_post.st_size != source_pre.st_size or file_sha256(source, chunk_bytes) != source_hash_during:
            raise InventoryError("REAL_TASK_SOURCE_CHANGED", "Source changed during copy.", source.as_posix())
        validate_existing_source_file_component_chain(root, source, allowed_source_roots)
        os.replace(temp_path, destination)
        if is_symlink_or_reparse(destination) or not destination.is_file():
            raise InventoryError("REAL_TASK_DESTINATION_REPARSE", "Final destination is unsafe.", destination.as_posix())
        final_hash = file_sha256(destination, chunk_bytes)
        if final_hash != source_hash_during:
            raise InventoryError("REAL_TASK_SOURCE_HASH_MISMATCH", "Final destination hash mismatch.", destination.as_posix())
        try:
            dest_stat = destination.stat(follow_symlinks=False)
            same_identity = source_post.st_ino and dest_stat.st_ino and source_post.st_dev == dest_stat.st_dev and source_post.st_ino == dest_stat.st_ino
            hardlink_count = getattr(dest_stat, "st_nlink", 1)
            if same_identity:
                raise InventoryError("REAL_TASK_SOURCE_COPY_FAILED", "Destination has same file identity as source.", destination.as_posix())
        except AttributeError:
            hardlink_count = 1
        return {
            "bytes": copied,
            "sourceSha256": source_hash_during,
            "destinationSha256": final_hash,
            "hardlinkCount": hardlink_count,
        }
    except Exception:
        try:
            temp_path.unlink(missing_ok=True)
        finally:
            pass
        raise


def validate_existing_source_file_component_chain(root: Path, source: Path, allowed_source_roots: tuple[str, ...]) -> None:
    root_resolved = root.resolve(strict=True)
    source_absolute = Path(os.path.abspath(source))
    try:
        relative = source_absolute.relative_to(root_resolved)
    except ValueError as exc:
        raise InventoryError("REAL_TASK_PATH_UNSAFE", "Source path escapes repository.", source.as_posix()) from exc
    parts = relative.parts
    if not parts or parts[0] not in allowed_source_roots:
        raise InventoryError("REAL_TASK_PATH_UNSAFE", "Source path is outside allowed source roots.", relative.as_posix())
    current = root_resolved
    for index, part in enumerate(parts):
        current = current / part
        try:
            current_stat = current.stat(follow_symlinks=False)
        except OSError as exc:
            raise InventoryError("REAL_TASK_SOURCE_CHANGED", f"Source component could not be inspected: {type(exc).__name__}.", current.as_posix()) from exc
        if is_symlink_or_reparse(current):
            raise InventoryError("REAL_TASK_SOURCE_REPARSE", "Source component is a symlink or reparse point.", current.as_posix())
        is_leaf = index == len(parts) - 1
        if is_leaf:
            if not stat.S_ISREG(current_stat.st_mode):
                raise InventoryError("REAL_TASK_UNSUPPORTED_FILE_TYPE", "Source leaf is not a regular file.", current.as_posix())
        elif not stat.S_ISDIR(current_stat.st_mode):
            raise InventoryError("REAL_TASK_SOURCE_CHANGED", "Source intermediate component is not a directory.", current.as_posix())


def _copy_regular_file_bytes(source: Path, destination: Path, chunk_bytes: int) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    with source.open("rb") as source_handle, destination.open("wb") as dest_handle:
        shutil.copyfileobj(source_handle, dest_handle, length=chunk_bytes)


def _validate_destination(root: Path, destination: Path) -> None:
    try:
        destination.resolve(strict=False).relative_to(root)
    except ValueError as exc:
        raise InventoryError("REAL_TASK_DESTINATION_ESCAPE", "Destination escapes staging workspace.", destination.as_posix()) from exc
    current = root
    for part in destination.relative_to(root).parts[:-1]:
        current = current / part
        if current.exists() or current.is_symlink():
            if is_symlink_or_reparse(current):
                raise InventoryError("REAL_TASK_DESTINATION_REPARSE", "Destination component is a symlink or reparse point.", current.as_posix())


def _validate_runtime_path(root: Path, path: Path, container: Path | None = None) -> None:
    try:
        resolved = path.resolve(strict=False)
        runtime = (root / "CodexAutomation" / "runtime").resolve(strict=False)
        resolved.relative_to(runtime if container is None else container.resolve(strict=False))
    except (OSError, ValueError) as exc:
        raise InventoryError("REAL_TASK_RUNTIME_PATH_UNSAFE", "Runtime path is outside allowed container.", path.as_posix()) from exc
    current = resolved
    existing = []
    while current != current.parent:
        if current.exists() or current.is_symlink():
            existing.append(current)
        current = current.parent
    for candidate in existing:
        if is_symlink_or_reparse(candidate):
            raise InventoryError("REAL_TASK_RUNTIME_PATH_UNSAFE", "Runtime path contains symlink or reparse component.", candidate.as_posix())


def _service_registry(staging: Path, service_paths: set[str], origin: str, chunk_bytes: int) -> list[dict[str, Any]]:
    registry: list[dict[str, Any]] = []
    for relative in sorted(service_paths):
        path = staging / relative
        if relative == ".agents":
            registry.append({"path": relative, "kind": "directory", "expectedType": "directory", "expectedSha256": None, "mutability": "immutable", "origin": "empty_agents_directory", "workspacePath": relative, "evidencePath": None})
        elif path.is_file():
            registry.append({"path": relative, "kind": "file", "expectedType": "file", "expectedSha256": file_sha256(path, chunk_bytes), "mutability": "immutable", "origin": origin if relative == "AGENTS.md" else ("task_snapshot" if relative == "task.json" else "policy_snapshot"), "workspacePath": relative, "evidencePath": None})
    return registry


def add_project_instruction_services(staging: Path, paths: list[str], origin: str, service_registry: list[dict[str, Any]], chunk_bytes: int) -> set[str]:
    service_paths = {str(item["workspacePath"]) for item in service_registry if item.get("workspacePath")}
    existing = {item["path"]: item for item in service_registry}
    for relative in sorted(paths):
        target = staging / relative
        if not target.exists() or not target.is_file() or is_symlink_or_reparse(target):
            raise InventoryError("REAL_TASK_SERVICE_FILE_INVALID", "Project AGENTS service file was not copied safely.", relative)
        service_paths.add(relative)
        entry = {
            "path": relative,
            "kind": "file",
            "expectedType": "file",
            "expectedSha256": file_sha256(target, chunk_bytes),
            "mutability": "immutable",
            "origin": origin,
            "workspacePath": relative,
            "evidencePath": None,
        }
        if relative in existing and existing[relative].get("expectedSha256") != entry["expectedSha256"]:
            raise InventoryError("REAL_TASK_SERVICE_FILE_INVALID", "Duplicate project AGENTS hashes differ.", relative)
        if relative not in existing:
            service_registry.append(entry)
    return service_paths


def _is_registered_service_path(path: str, service_paths: set[str]) -> bool:
    return path in service_paths or any(path.startswith(f"{service}/") for service in service_paths)


def _required_ancestor_directories(paths: set[str]) -> set[str]:
    ancestors: set[str] = set()
    for path in paths:
        parts = path.split("/")
        for index in range(1, len(parts)):
            ancestors.add("/".join(parts[:index]))
    return ancestors


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def workspace_inventory_hash(inventory: dict[str, Any]) -> str:
    return canonical_sha256(inventory)
