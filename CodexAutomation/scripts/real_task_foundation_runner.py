from __future__ import annotations

import json
import os
import time
from pathlib import Path
from typing import Any

from file_utils import read_json, write_json_atomic
from parent_git_snapshot import parent_git_snapshot, snapshots_equal
from real_task_execution_models import RealTaskExecutionFailure
from real_task_foundation_models import (
    FOUNDATION_CONTEXT_VERSION,
    FOUNDATION_REPORT_VERSION,
    FoundationState,
    REAL_TASK_FOUNDATION_STAGE,
    TrustedFoundationContext,
    canonical_json_bytes,
    canonical_sha256,
    new_foundation_run_id,
    parse_foundation_policy,
    utc_now,
)
from real_task_models import RealTaskError
from real_task_report_writer import TrustedReportReceipt, read_trusted_report, write_trusted_report
from real_task_workspace import (
    add_project_instruction_services,
    copy_sources,
    create_run_paths,
    create_service_files,
    create_standalone_git,
    expand_ancestor_agents,
    git_fingerprint,
    promote_staging_to_workspace,
    relative_to_root,
    validate_service_files,
    verify_destination_matches_source,
)
from schema_validator import validate
from task_manifest_validator import parse_real_task_policy, validate_task_manifest_file
from workspace_inventory import InventoryError, build_source_inventory, build_workspace_inventory, compare_source_inventories


FINAL_FOUNDATION_REPORT_NAME = "FINAL_WORKSPACE_PREPARATION_REPORT.json"
FINAL_FOUNDATION_REPORT_SCHEMA = "real_task_workspace_final.schema.json"
FINAL_FOUNDATION_REPORT_ERROR = "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID"


def prepare_foundation_workspace(root: Path, automation_root: Path, config: dict[str, Any], manifest_path: Path, run_id: str | None = None) -> dict[str, Any]:
    started = time.monotonic()
    state_history: list[str] = [FoundationState.PENDING.value]
    warnings: list[dict[str, Any]] = []
    trusted_context: TrustedFoundationContext | None = None
    trusted_context_hash: str | None = None
    run_paths = None
    manifest: dict[str, Any] | None = None
    manifest_sha: str | None = None
    task_id = "invalid-task"
    source_pre: dict[str, Any] | None = None
    source_post: dict[str, Any] | None = None
    workspace_baseline: dict[str, Any] | None = None
    parent_initial: dict[str, Any] | None = None
    parent_final: dict[str, Any] | None = None
    parent_changed: bool | None = None
    source_pre_post_match = False
    source_copy_verified = False
    source_copied = False
    service_files_valid = False
    isolated_git_valid = False
    staging_git_fingerprint: dict[str, Any] | None = None
    staging_retained = False
    workspace_ready = False
    service_registry: list[dict[str, Any]] = []
    copied_summary: dict[str, Any] = {"copiedFiles": [], "copiedDirectories": [], "copiedBytes": 0}
    error_code: str | None = None
    error_message: str | None = None
    final_state = FoundationState.FAILED.value
    run_id = run_id or new_foundation_run_id()
    try:
        _transition(state_history, FoundationState.MANIFEST_VALIDATING)
        real_policy, real_policy_errors = parse_real_task_policy(config)
        foundation_policy, foundation_errors = parse_foundation_policy(root, config, real_policy)
        if real_policy_errors or foundation_errors or real_policy is None or foundation_policy is None:
            first = (real_policy_errors + foundation_errors)[0]
            raise _FoundationFailure(first.code, first.message)
        validation = validate_task_manifest_file(root, automation_root, config, manifest_path, inspect_sources=True)
        if not validation.ok or validation.manifest is None or validation.manifestSha256 is None or validation.effectivePlan is None:
            first = validation.errors[0] if validation.errors else RealTaskError("REAL_TASK_MANIFEST_INVALID", "Manifest validation failed.")
            raise _FoundationFailure(first.code, first.message)
        manifest = validation.manifest
        manifest_sha = validation.manifestSha256
        task_id = str(manifest["taskId"])
        _transition(state_history, FoundationState.CONTEXT_CREATING)
        parent_initial, parent_errors = parent_git_snapshot(root)
        if parent_errors:
            first = parent_errors[0]
            raise _FoundationFailure(first.code, first.message)
        run_paths = create_run_paths(root, foundation_policy, run_id)
        source_limits = {
            "maxSourceFiles": real_policy.maxSourceFiles,
            "maxSourceBytes": real_policy.maxSourceBytes,
            "maxInventoryEntries": foundation_policy.maxInventoryEntries,
        }
        effective_plan_policy = validation.effectivePlan["effectivePolicy"]
        output_limits = dict(effective_plan_policy.get("effectiveLimits", {}))
        trusted_context = TrustedFoundationContext(
            contextVersion=FOUNDATION_CONTEXT_VERSION,
            runId=run_id,
            taskId=task_id,
            stage="BOOTSTRAP-03B-2B-A",
            createdAt=utc_now(),
            repositoryRoot=str(root.resolve()),
            runtimeRoot=str(run_paths.runtime_root.resolve()),
            runDirectory=str(run_paths.run_directory.resolve()),
            stagingDirectory=str(run_paths.staging.resolve()),
            workspaceDirectory=str(run_paths.workspace.resolve(strict=False)),
            evidenceDirectory=str(run_paths.evidence.resolve()),
            logsDirectory=str(run_paths.logs.resolve()),
            manifestDisplayName=manifest.get("title"),
            manifestPath=str(manifest_path.resolve()),
            manifestSha256=manifest_sha,
            effectiveHostPolicy=effective_plan_policy,
            effectiveFoundationPolicy=foundation_policy.to_dict(),
            sourcePaths=tuple(manifest["sourcePaths"]),
            allowedWritePaths=tuple(manifest["allowedWritePaths"]),
            allowedDeletePaths=tuple(manifest.get("allowedDeletePaths", [])),
            sourceLimits=source_limits,
            outputLimits=output_limits,
            parentGitInitialSnapshot=parent_initial,
            noModel=True,
            noCodex=True,
            noSandbox=True,
            noUnity=True,
        )
        trusted_data = trusted_context.to_dict()
        trusted_context_hash = write_validated_trusted_context(
            root=root,
            automation_root=automation_root,
            path=run_paths.run_directory / "TRUSTED_RUN_CONTEXT.json",
            payload=trusted_data,
            config=config,
            run_paths=run_paths,
            run_id=run_id,
            task_id=task_id,
            manifest_path=manifest_path,
            manifest_sha=manifest_sha,
            manifest=manifest,
            effective_policy=effective_plan_policy,
            foundation_policy=foundation_policy.to_dict(),
            parent_initial=parent_initial,
        )
        _write_report(root, automation_root, run_paths.run_directory / "WORKSPACE_PREPARATION_REPORT.json", _preparation_report(run_id, task_id, state_history, trusted_context_hash, run_paths, "RUNNING", None, None), config)

        _transition(state_history, FoundationState.SOURCE_SNAPSHOTTING)
        ancestor_agents = expand_ancestor_agents(root, list(trusted_context.sourcePaths), real_policy.allowedSourceRoots, foundation_policy.copyChunkBytes)
        effective_source_paths = sorted(set(trusted_context.sourcePaths) | {item["path"] for item in ancestor_agents})
        service_source_paths = {item["path"] for item in ancestor_agents}
        source_pre = build_source_inventory(root, effective_source_paths, foundation_policy.maxInventoryEntries, foundation_policy.copyChunkBytes, service_source_paths)
        _attach_report_identity(source_pre, run_id, task_id)
        _write_report(root, automation_root, run_paths.run_directory / "SOURCE_PRE_INVENTORY.json", source_pre, config)

        _transition(state_history, FoundationState.WORKSPACE_STAGING)
        _transition(state_history, FoundationState.SERVICE_FILES_CREATING)
        service_registry, root_service_paths = create_service_files(root, run_paths.staging, run_paths.evidence, manifest, effective_plan_policy, trusted_context, foundation_policy.copyChunkBytes)
        staging_git_fingerprint = create_standalone_git(run_paths.staging, run_paths.run_directory)
        service_registry.append({"path": ".git", "kind": "directory", "expectedType": "directory", "expectedSha256": None, "mutability": "immutable", "origin": "isolated_git", "workspacePath": ".git", "evidencePath": None})

        _transition(state_history, FoundationState.SOURCE_COPYING)
        copied_summary = copy_sources(root, run_paths.staging, source_pre, foundation_policy.copyChunkBytes, service_source_paths, real_policy.allowedSourceRoots)
        source_copied = True
        source_agents_in_copy = [entry["path"] for entry in source_pre["entries"] if str(entry["path"]).endswith("AGENTS.md") and entry["path"] != "AGENTS.md"]
        copied_agent_paths = _naturally_copied_agent_paths(root, trusted_context.sourcePaths, source_agents_in_copy)
        ancestor_agent_paths = set(source_agents_in_copy) - copied_agent_paths
        all_service_paths = add_project_instruction_services(run_paths.staging, sorted(copied_agent_paths), "copied_project_instruction", service_registry, foundation_policy.copyChunkBytes)
        all_service_paths.update(add_project_instruction_services(run_paths.staging, sorted(ancestor_agent_paths), "ancestor_project_instruction", service_registry, foundation_policy.copyChunkBytes))
        all_service_paths.update(root_service_paths)
        _transition(state_history, FoundationState.SOURCE_POST_VERIFYING)
        source_post = build_source_inventory(root, effective_source_paths, foundation_policy.maxInventoryEntries, foundation_policy.copyChunkBytes, service_source_paths)
        _attach_report_identity(source_post, run_id, task_id)
        source_pre_post_match = compare_source_inventories(source_pre, source_post)
        if not source_pre_post_match:
            raise _FoundationFailure("REAL_TASK_SOURCE_CHANGED", "Source pre/post inventories differ.")
        _write_report(root, automation_root, run_paths.run_directory / "SOURCE_POST_INVENTORY.json", source_post, config)

        _transition(state_history, FoundationState.DESTINATION_VERIFYING)
        staging_inventory = verify_destination_matches_source(run_paths.staging, source_post, foundation_policy.maxInventoryEntries, foundation_policy.copyChunkBytes, all_service_paths)
        source_copy_verified = True
        service_files_valid, service_error = validate_service_files(run_paths.staging, service_registry, foundation_policy.copyChunkBytes)
        if not service_files_valid:
            raise _FoundationFailure(service_error or "REAL_TASK_SERVICE_FILE_INVALID", "Service files are invalid.")
        staging_git_fingerprint = git_fingerprint(run_paths.staging)
        _write_report(root, automation_root, run_paths.run_directory / "SOURCE_COPY_REPORT.json", _source_copy_report(run_id, task_id, trusted_context_hash, source_pre_post_match, source_copy_verified, copied_summary, ancestor_agents, service_registry, None, None), config)

        _transition(state_history, FoundationState.PARENT_GIT_VERIFYING)
        parent_final, parent_errors = parent_git_snapshot(root)
        if parent_errors:
            first = parent_errors[0]
            raise _FoundationFailure(first.code, first.message)
        parent_changed = not snapshots_equal(parent_initial, parent_final)
        if parent_changed:
            raise _FoundationFailure("REAL_TASK_PARENT_GIT_CHANGED", "Parent Git snapshot changed during foundation preparation.")

        _transition(state_history, FoundationState.WORKSPACE_PROMOTING)
        promote_staging_to_workspace(run_paths)
        _transition(state_history, FoundationState.WORKSPACE_BASELINE_VERIFYING)
        workspace_git_fingerprint = git_fingerprint(run_paths.workspace)
        if canonical_sha256(staging_git_fingerprint) != canonical_sha256(workspace_git_fingerprint):
            raise _FoundationFailure("REAL_TASK_ISOLATED_GIT_INVALID", "Post-promotion .git fingerprint differs from staging fingerprint.")
        git_state = dict(workspace_git_fingerprint)
        git_state["postPromotionVerified"] = True
        git_state["stagingFingerprintSha256"] = canonical_sha256(staging_git_fingerprint)
        git_state["workspaceFingerprintSha256"] = canonical_sha256(workspace_git_fingerprint)
        isolated_git_valid = True
        workspace_baseline = build_workspace_inventory(run_paths.workspace, foundation_policy.maxInventoryEntries, foundation_policy.copyChunkBytes, all_service_paths)
        _attach_report_identity(workspace_baseline, run_id, task_id)
        workspace_baseline["serviceRegistry"] = service_registry
        workspace_baseline["isolatedGitFingerprint"] = git_state
        _write_report(root, automation_root, run_paths.run_directory / "WORKSPACE_BASELINE_INVENTORY.json", workspace_baseline, config)
        workspace_ready = True
        final_state = FoundationState.COMPLETED.value
        _transition(state_history, FoundationState.REPORTING)
        _transition(state_history, FoundationState.COMPLETED)
    except _FoundationFailure as exc:
        error_code = exc.code
        error_message = exc.message
        final_state = FoundationState.FAILED.value
        _transition(state_history, FoundationState.REPORTING)
        _transition(state_history, FoundationState.FAILED)
    except InventoryError as exc:
        error_code = exc.code
        error_message = exc.message
        final_state = FoundationState.FAILED.value
        _transition(state_history, FoundationState.REPORTING)
        _transition(state_history, FoundationState.FAILED)
    except Exception as exc:
        error_code = "REAL_TASK_INTERNAL_ERROR"
        error_message = f"{type(exc).__name__}: {exc}"
        final_state = FoundationState.FAILED.value
        _transition(state_history, FoundationState.REPORTING)
        _transition(state_history, FoundationState.FAILED)
    finally:
        if run_paths is None:
            return _final_report(
                run_id=run_id,
                task_id=task_id,
                state_history=state_history,
                manifest_sha=manifest_sha,
                trusted_context_hash=trusted_context_hash,
                parent_initial=parent_initial,
                parent_final=parent_final,
                parent_changed=parent_changed,
                source_pre_post_match=source_pre_post_match,
                source_copy_verified=source_copy_verified,
                workspace_created=False,
                staging_retained=False,
                workspace_ready=False,
                service_files_valid=service_files_valid,
                isolated_git_valid=isolated_git_valid,
                source_inventory_hash=None,
                workspace_baseline_hash=None,
                source_copied=source_copied,
                final_state=final_state,
                error_code=error_code,
                error_message=error_message,
                warnings=warnings,
                duration_seconds=round(time.monotonic() - started, 3),
            )
        if run_paths is not None:
            staging_retained = run_paths.staging.exists()
            if source_pre is not None and not (run_paths.run_directory / "SOURCE_PRE_INVENTORY.json").exists():
                _safe_write(root, automation_root, run_paths.run_directory / "SOURCE_PRE_INVENTORY.json", source_pre, config, warnings)
            if source_post is not None and not (run_paths.run_directory / "SOURCE_POST_INVENTORY.json").exists():
                _safe_write(root, automation_root, run_paths.run_directory / "SOURCE_POST_INVENTORY.json", source_post, config, warnings)
            if trusted_context_hash is not None and not (run_paths.run_directory / "SOURCE_COPY_REPORT.json").exists():
                _safe_write(root, automation_root, run_paths.run_directory / "SOURCE_COPY_REPORT.json", _source_copy_report(run_id, task_id, trusted_context_hash, source_pre_post_match, source_copy_verified, copied_summary, [], service_registry, error_code, error_message), config, warnings)
            if parent_initial is not None and parent_final is None:
                parent_final, parent_errors = parent_git_snapshot(root)
                if parent_errors:
                    parent_final = {"status": "failed"}
                    if error_code is None:
                        error_code = parent_errors[0].code
                        error_message = parent_errors[0].message
                parent_changed = None if parent_final.get("status") == "failed" else not snapshots_equal(parent_initial, parent_final)
            final_report = _final_report(
                run_id=run_id,
                task_id=task_id,
                state_history=state_history,
                manifest_sha=manifest_sha,
                trusted_context_hash=trusted_context_hash,
                parent_initial=parent_initial,
                parent_final=parent_final,
                parent_changed=parent_changed,
                source_pre_post_match=source_pre_post_match,
                source_copy_verified=source_copy_verified,
                workspace_created=bool(run_paths and run_paths.workspace.exists()),
                staging_retained=staging_retained,
                workspace_ready=workspace_ready,
                service_files_valid=service_files_valid,
                isolated_git_valid=isolated_git_valid,
                source_inventory_hash=source_post.get("inventorySha256") if source_post else (source_pre.get("inventorySha256") if source_pre else None),
                workspace_baseline_hash=workspace_baseline.get("inventorySha256") if workspace_baseline else None,
                source_copied=source_copied,
                final_state=final_state,
                error_code=error_code,
                error_message=error_message,
                warnings=warnings,
                duration_seconds=round(time.monotonic() - started, 3),
                final_report_relative_path=(run_paths.run_directory / FINAL_FOUNDATION_REPORT_NAME).resolve(strict=False).relative_to(run_paths.runtime_root.resolve(strict=False)).as_posix(),
            )
            try:
                receipt = _write_trusted_final_foundation_report(root, automation_root, run_paths, final_report, config)
                final_report = dict(final_report)
                final_report["finalReportReceipt"] = receipt.to_dict()
                if final_report.get("finalVerdict") == "PASS":
                    _verify_final_foundation_authority(root, automation_root, run_paths, final_report, receipt, config)
                return final_report
            except _FoundationFailure as exc:
                return _final_report(
                    run_id=run_id,
                    task_id=task_id,
                    state_history=_terminal_history(state_history, FoundationState.BLOCKED),
                    manifest_sha=manifest_sha,
                    trusted_context_hash=trusted_context_hash,
                    parent_initial=parent_initial,
                    parent_final=parent_final,
                    parent_changed=parent_changed,
                    source_pre_post_match=source_pre_post_match,
                    source_copy_verified=source_copy_verified,
                    workspace_created=bool(run_paths and run_paths.workspace.exists()),
                    staging_retained=staging_retained,
                    workspace_ready=False,
                    service_files_valid=service_files_valid,
                    isolated_git_valid=isolated_git_valid,
                    source_inventory_hash=source_post.get("inventorySha256") if source_post else (source_pre.get("inventorySha256") if source_pre else None),
                    workspace_baseline_hash=workspace_baseline.get("inventorySha256") if workspace_baseline else None,
                    source_copied=source_copied,
                    final_state=FoundationState.BLOCKED.value,
                    error_code=exc.code,
                    error_message=exc.message,
                    warnings=warnings,
                    duration_seconds=round(time.monotonic() - started, 3),
                    final_report_relative_path=(run_paths.run_directory / FINAL_FOUNDATION_REPORT_NAME).resolve(strict=False).relative_to(run_paths.runtime_root.resolve(strict=False)).as_posix(),
                )
    raise RuntimeError("unreachable foundation preparation state")


class _FoundationFailure(Exception):
    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


def _transition(history: list[str], state: FoundationState) -> None:
    value = state.value
    if not history or history[-1] != value:
        history.append(value)


def _terminal_history(history: list[str], terminal: FoundationState) -> list[str]:
    copied = list(history)
    if not copied or copied[-1] != FoundationState.REPORTING.value:
        copied.append(FoundationState.REPORTING.value)
    if copied[-1] != terminal.value:
        copied.append(terminal.value)
    return copied


def _attach_report_identity(report: dict[str, Any], run_id: str, task_id: str) -> None:
    report["runId"] = run_id
    report["taskId"] = task_id


def write_validated_trusted_context(
    root: Path,
    automation_root: Path,
    path: Path,
    payload: dict[str, Any],
    config: dict[str, Any],
    run_paths: Any,
    run_id: str,
    task_id: str,
    manifest_path: Path,
    manifest_sha: str,
    manifest: dict[str, Any],
    effective_policy: dict[str, Any],
    foundation_policy: dict[str, Any],
    parent_initial: dict[str, Any],
) -> str:
    if path.exists() or path.is_symlink():
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context file already exists.")
    _validate_trusted_context_payload(root, automation_root, payload, config, run_paths, run_id, task_id, manifest_path, manifest_sha, manifest, effective_policy, foundation_policy, parent_initial)
    max_bytes = int(config["realTaskFoundation"]["maxReportBytes"])
    if len(canonical_json_bytes(payload)) > max_bytes:
        raise _FoundationFailure("REAL_TASK_TRUSTED_CONTEXT_INVALID", "Trusted context exceeds maxReportBytes.")
    expected_hash = canonical_sha256(payload)
    write_json_atomic(path, payload)
    reread = read_json(path)
    _validate_trusted_context_payload(root, automation_root, reread, config, run_paths, run_id, task_id, manifest_path, manifest_sha, manifest, effective_policy, foundation_policy, parent_initial)
    reread_hash = canonical_sha256(reread)
    if reread_hash != expected_hash:
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context hash changed after reread.")
    return reread_hash


def _validate_trusted_context_payload(
    root: Path,
    automation_root: Path,
    payload: dict[str, Any],
    config: dict[str, Any],
    run_paths: Any,
    run_id: str,
    task_id: str,
    manifest_path: Path,
    manifest_sha: str,
    manifest: dict[str, Any],
    effective_policy: dict[str, Any],
    foundation_policy: dict[str, Any],
    parent_initial: dict[str, Any],
) -> None:
    schema_errors = validate(payload, read_json(automation_root / "schemas" / "real_task_trusted_context.schema.json"))
    if schema_errors:
        raise _FoundationFailure("REAL_TASK_TRUSTED_CONTEXT_INVALID", schema_errors[0])
    expected_paths = {
        "repositoryRoot": root.resolve(),
        "runtimeRoot": run_paths.runtime_root.resolve(),
        "runDirectory": run_paths.run_directory.resolve(),
        "stagingDirectory": run_paths.staging.resolve(),
        "workspaceDirectory": run_paths.workspace.resolve(strict=False),
        "evidenceDirectory": run_paths.evidence.resolve(),
        "logsDirectory": run_paths.logs.resolve(),
        "manifestPath": manifest_path.resolve(),
    }
    if payload.get("contextVersion") != FOUNDATION_CONTEXT_VERSION or payload.get("stage") != REAL_TASK_FOUNDATION_STAGE:
        raise _FoundationFailure("REAL_TASK_TRUSTED_CONTEXT_INVALID", "Trusted context version or stage is invalid.")
    if payload.get("runId") != run_id or payload.get("taskId") != task_id:
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context run/task identity mismatch.")
    if not isinstance(payload.get("createdAt"), str) or not payload["createdAt"].endswith("Z"):
        raise _FoundationFailure("REAL_TASK_TRUSTED_CONTEXT_INVALID", "Trusted context timestamp is invalid.")
    if payload.get("manifestSha256") != manifest_sha:
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context manifest hash mismatch.")
    if payload.get("sourcePaths") != list(manifest["sourcePaths"]):
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context source paths mismatch.")
    if payload.get("allowedWritePaths") != list(manifest["allowedWritePaths"]):
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context allowed write paths mismatch.")
    if payload.get("allowedDeletePaths") != list(manifest.get("allowedDeletePaths", [])):
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context allowed delete paths mismatch.")
    if payload.get("effectiveHostPolicy") != effective_policy or payload.get("effectiveFoundationPolicy") != foundation_policy:
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context policy mismatch.")
    if payload.get("parentGitInitialSnapshot") != parent_initial or parent_initial.get("status") != "success":
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context parent Git snapshot mismatch.")
    if any(payload.get(field) is not True for field in ("noModel", "noCodex", "noSandbox", "noUnity")):
        raise _FoundationFailure("REAL_TASK_TRUSTED_CONTEXT_INVALID", "Trusted context fixed no-execution flags are invalid.")
    for field, expected in expected_paths.items():
        if str(expected) != str(Path(str(payload[field])).resolve(strict=False)):
            raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", f"Trusted context path mismatch: {field}.")
    for field in ("runDirectory", "stagingDirectory", "workspaceDirectory", "evidenceDirectory", "logsDirectory"):
        try:
            Path(str(payload[field])).resolve(strict=False).relative_to(run_paths.run_directory.resolve(strict=False))
        except ValueError as exc:
            raise _FoundationFailure("REAL_TASK_RUNTIME_PATH_UNSAFE", f"Trusted context path escapes run directory: {field}.") from exc
    try:
        Path(str(payload["runtimeRoot"])).resolve(strict=False).relative_to((root / "CodexAutomation" / "runtime").resolve(strict=False))
    except ValueError as exc:
        raise _FoundationFailure("REAL_TASK_RUNTIME_PATH_UNSAFE", "Trusted context runtime root is unsafe.") from exc
    source_limits = payload.get("sourceLimits")
    if source_limits.get("maxInventoryEntries") != config["realTaskFoundation"]["maxInventoryEntries"]:
        raise _FoundationFailure("REAL_TASK_CONTEXT_MISMATCH", "Trusted context source limits mismatch.")
    for limits in (source_limits, payload.get("outputLimits")):
        if not all(isinstance(value, int) and not isinstance(value, bool) for value in limits.values()):
            raise _FoundationFailure("REAL_TASK_TRUSTED_CONTEXT_INVALID", "Trusted context limits must be integers.")


def _naturally_copied_agent_paths(root: Path, declared_source_paths: tuple[str, ...], agent_paths: list[str]) -> set[str]:
    natural: set[str] = set()
    for agent_path in agent_paths:
        for source_path in declared_source_paths:
            source = root / source_path
            if agent_path == source_path:
                natural.add(agent_path)
            elif source.is_dir() and agent_path.startswith(f"{source_path.rstrip('/')}/"):
                natural.add(agent_path)
    return natural


def _preparation_report(run_id: str, task_id: str, state_history: list[str], trusted_context_hash: str, run_paths: Any, final_state: str, error_code: str | None, error_message: str | None) -> dict[str, Any]:
    return {
        "reportVersion": FOUNDATION_REPORT_VERSION,
        "runId": run_id,
        "taskId": task_id,
        "stage": "BOOTSTRAP-03B-2B-A",
        "stateHistory": state_history,
        "trustedContextHash": trusted_context_hash,
        "stagingPath": run_paths.staging.relative_to(run_paths.runtime_root).as_posix(),
        "workspacePath": run_paths.workspace.relative_to(run_paths.runtime_root).as_posix(),
        "evidencePath": run_paths.evidence.relative_to(run_paths.runtime_root).as_posix(),
        "logsPath": run_paths.logs.relative_to(run_paths.runtime_root).as_posix(),
        "workspaceReady": False,
        "finalState": final_state,
        "errorCode": error_code,
        "errorMessage": error_message,
    }


def _source_copy_report(run_id: str, task_id: str, trusted_context_hash: str | None, source_pre_post_match: bool, source_copy_verified: bool, copied: dict[str, Any], ancestor_agents: list[dict[str, Any]], service_registry: list[dict[str, Any]], error_code: str | None, error_message: str | None) -> dict[str, Any]:
    return {
        "reportVersion": FOUNDATION_REPORT_VERSION,
        "runId": run_id,
        "taskId": task_id,
        "stage": "BOOTSTRAP-03B-2B-A",
        "trustedContextHash": trusted_context_hash,
        "sourcePrePostMatch": source_pre_post_match,
        "sourceCopyVerified": source_copy_verified,
        "copiedFiles": copied.get("copiedFiles", []),
        "copiedDirectories": copied.get("copiedDirectories", []),
        "copiedBytes": copied.get("copiedBytes", 0),
        "hostExpandedAncestorAgents": ancestor_agents,
        "serviceRegistry": service_registry,
        "errorCode": error_code,
        "errorMessage": error_message,
    }


def _final_report(
    run_id: str,
    task_id: str,
    state_history: list[str],
    manifest_sha: str | None,
    trusted_context_hash: str | None,
    parent_initial: dict[str, Any] | None,
    parent_final: dict[str, Any] | None,
    parent_changed: bool | None,
    source_pre_post_match: bool,
    source_copy_verified: bool,
    workspace_created: bool,
    staging_retained: bool,
    workspace_ready: bool,
    service_files_valid: bool,
    isolated_git_valid: bool,
    source_inventory_hash: str | None,
    workspace_baseline_hash: str | None,
    source_copied: bool,
    final_state: str,
    error_code: str | None,
    error_message: str | None,
    warnings: list[dict[str, Any]],
    duration_seconds: float,
    final_report_relative_path: str | None = None,
) -> dict[str, Any]:
    pass_ready = (
        workspace_ready
        and source_copied
        and source_copy_verified
        and source_pre_post_match
        and parent_changed is False
        and service_files_valid
        and isolated_git_valid
        and error_code is None
    )
    final_verdict = "PASS" if pass_ready else ("BLOCKED" if final_state == FoundationState.BLOCKED.value or error_code == FINAL_FOUNDATION_REPORT_ERROR else "FAILED")
    return {
        "reportVersion": FOUNDATION_REPORT_VERSION,
        "runId": run_id,
        "taskId": task_id,
        "stage": "BOOTSTRAP-03B-2B-A",
        "stateHistory": state_history,
        "manifestSha256": manifest_sha,
        "trustedContextHash": trusted_context_hash,
        "parentGitInitialSnapshot": parent_initial,
        "parentGitFinalSnapshot": parent_final,
        "parentGitChanged": parent_changed,
        "sourcePrePostMatch": source_pre_post_match,
        "sourceCopyVerified": source_copy_verified,
        "workspaceCreated": workspace_created,
        "stagingRetained": staging_retained,
        "workspaceReady": workspace_ready,
        "serviceFilesValid": service_files_valid,
        "isolatedGitValid": isolated_git_valid,
        "sourceInventoryHash": source_inventory_hash,
        "workspaceBaselineInventoryHash": workspace_baseline_hash,
        "codexInvocationCount": 0,
        "modelInvocationStarted": False,
        "sandboxStarted": False,
        "unityStarted": False,
        "networkUsed": False,
        "sourceCopied": source_copied,
        "complete": pass_ready,
        "finalReportRelativePath": final_report_relative_path,
        "finalReportReceipt": None,
        "durationSeconds": duration_seconds,
        "finalState": FoundationState.COMPLETED.value if pass_ready else final_state,
        "finalVerdict": final_verdict,
        "errorCode": error_code,
        "errorMessage": error_message,
        "warnings": warnings,
    }


def _write_trusted_final_foundation_report(
    root: Path,
    automation_root: Path,
    run_paths: Any,
    report: dict[str, Any],
    config: dict[str, Any],
) -> TrustedReportReceipt:
    final_path = run_paths.run_directory / FINAL_FOUNDATION_REPORT_NAME
    runtime_root = root / "CodexAutomation" / "runtime"
    try:
        expected_relative = final_path.resolve(strict=False).relative_to(run_paths.runtime_root.resolve(strict=False)).as_posix()
    except ValueError as exc:
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Final foundation report path escapes foundation runtime root.") from exc
    if report.get("finalReportRelativePath") != expected_relative:
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Final foundation report relative path mismatch.")
    try:
        receipt = write_trusted_report(
            automation_root,
            runtime_root,
            final_path,
            report,
            FINAL_FOUNDATION_REPORT_SCHEMA,
            lambda item: _validate_final_foundation_report(item, run_paths, expected_relative),
            int(config["realTaskFoundation"]["maxReportBytes"]),
        )
        reread = read_trusted_report(
            automation_root,
            runtime_root,
            final_path,
            receipt,
            FINAL_FOUNDATION_REPORT_SCHEMA,
            lambda item: _validate_final_foundation_report(item, run_paths, expected_relative),
            int(config["realTaskFoundation"]["maxReportBytes"]),
        )
    except RealTaskExecutionFailure as exc:
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, exc.message) from exc
    except Exception as exc:
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, f"{type(exc).__name__}: {exc}") from exc
    if receipt.relativePath != final_path.resolve(strict=False).relative_to(runtime_root.resolve(strict=False)).as_posix():
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Final foundation report receipt path mismatch.")
    if reread.get("runId") != report.get("runId") or reread.get("taskId") != report.get("taskId"):
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Final foundation report reread identity mismatch.")
    return receipt


def _verify_final_foundation_authority(
    root: Path,
    automation_root: Path,
    run_paths: Any,
    report: dict[str, Any],
    receipt: TrustedReportReceipt,
    config: dict[str, Any],
) -> None:
    final_path = run_paths.run_directory / FINAL_FOUNDATION_REPORT_NAME
    if report.get("finalVerdict") != "PASS" or report.get("complete") is not True:
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Foundation authority requires PASS and complete=true.")
    if report.get("finalReportReceipt") != receipt.to_dict():
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Final foundation report receipt is not bound to result.")
    persisted = read_trusted_report(
        automation_root,
        root / "CodexAutomation" / "runtime",
        final_path,
        receipt,
        FINAL_FOUNDATION_REPORT_SCHEMA,
        lambda item: _validate_final_foundation_report(item, run_paths, report.get("finalReportRelativePath")),
        int(config["realTaskFoundation"]["maxReportBytes"]),
    )
    if persisted.get("finalVerdict") != "PASS" or persisted.get("complete") is not True:
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Persisted final foundation report is not authoritative PASS.")
    if persisted.get("runId") != report.get("runId"):
        raise _FoundationFailure(FINAL_FOUNDATION_REPORT_ERROR, "Persisted final foundation report run binding mismatch.")


def _validate_final_foundation_report(report: dict[str, Any], run_paths: Any, expected_relative: str | None) -> list[str]:
    errors: list[str] = []
    if report.get("stage") != REAL_TASK_FOUNDATION_STAGE:
        errors.append("Final foundation report stage mismatch.")
    if expected_relative is not None and report.get("finalReportRelativePath") != expected_relative:
        errors.append("Final foundation report path mismatch.")
    if report.get("finalVerdict") == "PASS":
        required_true = ("workspaceCreated", "workspaceReady", "serviceFilesValid", "isolatedGitValid", "sourceCopied", "sourceCopyVerified", "sourcePrePostMatch", "complete")
        for field in required_true:
            if report.get(field) is not True:
                errors.append(f"PASS requires {field}=true.")
        if report.get("parentGitChanged") is not False:
            errors.append("PASS requires parentGitChanged=false.")
        if report.get("errorCode") is not None or report.get("errorMessage") is not None:
            errors.append("PASS requires null error fields.")
        if report.get("finalState") != FoundationState.COMPLETED.value:
            errors.append("PASS requires finalState=COMPLETED.")
    elif report.get("complete") is not False:
        errors.append("Non-PASS requires complete=false.")
    if run_paths is not None and report.get("finalReportRelativePath"):
        try:
            final_path = run_paths.runtime_root / str(report["finalReportRelativePath"])
            final_path.resolve(strict=False).relative_to(run_paths.run_directory.resolve(strict=False))
        except ValueError:
            errors.append("Final report relative path must stay in current foundation run.")
    return errors


def _fs_path(path: Path) -> str:
    resolved = path.resolve(strict=False)
    text = str(resolved)
    if os.name == "nt" and not text.startswith("\\\\?\\"):
        return "\\\\?\\" + text
    return text


def _write_report(root: Path, automation_root: Path, path: Path, report: dict[str, Any], config: dict[str, Any]) -> None:
    _validate_report(root, automation_root, path, report, config)
    path.parent.mkdir(parents=True, exist_ok=True)
    write_json_atomic(path, report)


def _safe_write(root: Path, automation_root: Path, path: Path, report: dict[str, Any], config: dict[str, Any], warnings: list[dict[str, Any]]) -> None:
    try:
        _write_report(root, automation_root, path, report, config)
    except Exception as exc:
        warnings.append({"code": "REAL_TASK_FOUNDATION_REPORT_WRITE_FAILED", "message": f"{path.name}: {type(exc).__name__}"})


def _validate_report(root: Path, automation_root: Path, path: Path, report: dict[str, Any], config: dict[str, Any]) -> None:
    _, foundation_errors = parse_foundation_policy(root, config, None)
    if foundation_errors:
        raise _FoundationFailure(foundation_errors[0].code, foundation_errors[0].message)
    max_bytes = int(config["realTaskFoundation"]["maxReportBytes"])
    if len(canonical_json_bytes(report)) > max_bytes:
        raise _FoundationFailure("REAL_TASK_FOUNDATION_REPORT_INVALID", "Report exceeds maxReportBytes.")
    schema_name = _schema_for_report(path.name)
    if schema_name:
        errors = validate(report, read_json(automation_root / "schemas" / schema_name))
        if errors:
            raise _FoundationFailure("REAL_TASK_FOUNDATION_REPORT_INVALID", errors[0])


def _schema_for_report(name: str) -> str | None:
    return {
        "TRUSTED_RUN_CONTEXT.json": "real_task_trusted_context.schema.json",
        "SOURCE_PRE_INVENTORY.json": "real_task_source_inventory.schema.json",
        "SOURCE_POST_INVENTORY.json": "real_task_source_inventory.schema.json",
        "WORKSPACE_BASELINE_INVENTORY.json": "real_task_source_inventory.schema.json",
        "WORKSPACE_PREPARATION_REPORT.json": "real_task_workspace_preparation.schema.json",
        "SOURCE_COPY_REPORT.json": "real_task_source_copy_report.schema.json",
        "FINAL_WORKSPACE_PREPARATION_REPORT.json": "real_task_workspace_final.schema.json",
    }.get(name)
