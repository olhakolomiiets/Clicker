from __future__ import annotations

import subprocess
import stat
from pathlib import Path
from typing import Any

from file_utils import write_json_atomic
from pipeline_validator import snapshot_error, snapshot_workspace

SERVICE_DIRECTORY = ".agents"
INITIAL_SERVICE_TOP_LEVEL_PATHS = {".git", "AGENTS.md", "task.json", SERVICE_DIRECTORY}


LOCAL_AGENTS_TEXT = """# Isolated Codex Real Role Test Rules

- Work only inside this workspace directory.
- Do not read parent directories.
- Do not read the Unity project.
- Do not use the network.
- Do not install packages.
- Do not run destructive Git commands.
- Creating artifact.json is explicitly allowed and required.
- artifact.json is the only file you may create or modify.
- Do not modify AGENTS.md, task.json, or .git.
- Do not merely report that artifact.json was created.
- Before returning your structured result, read artifact.json back from disk.
- Verify that its parsed JSON exactly matches the requested controlled state.
- If the sandbox prevents writing or verification, do not claim the task was implemented.
- Report the failure accurately in summary and knownIssues.
- Return the requested structured result.
- Do not create additional files.
"""


def create_isolated_workspace(root: Path, run_id: str, isolated_root: Path, task: dict[str, Any]) -> tuple[Path | None, list[str]]:
    errors: list[str] = []
    resolved_root = root.resolve()
    runtime_root = (root / "CodexAutomation" / "runtime").resolve()
    isolated_root.mkdir(parents=True, exist_ok=True)
    try:
        isolated_root.resolve(strict=True).relative_to(runtime_root)
    except ValueError:
        return None, ["ISOLATED_WORKSPACE_OUTSIDE_RUNTIME"]
    workspace = isolated_root / run_id / "workspace"
    if workspace.exists():
        return None, ["ISOLATED_WORKSPACE_INVALID: workspace already exists."]
    workspace.mkdir(parents=True)
    try:
        workspace.resolve(strict=True).relative_to(isolated_root.resolve(strict=True))
    except ValueError:
        return None, ["ISOLATED_WORKSPACE_OUTSIDE_RUNTIME"]
    if workspace.resolve(strict=True) in {resolved_root, (resolved_root / "CodexAutomation").resolve()}:
        return None, ["REAL_PROJECT_WRITE_FORBIDDEN"]
    workspace_error = snapshot_error(snapshot_workspace(workspace))
    if workspace_error:
        return None, [workspace_error["errorCode"]]

    git_init = subprocess.run(["git", "init"], cwd=str(workspace), capture_output=True, text=True, check=False)
    if git_init.returncode != 0:
        return None, [f"ISOLATED_WORKSPACE_INVALID: git init failed: {git_init.stderr.strip()}"]
    (workspace / "AGENTS.md").write_text(LOCAL_AGENTS_TEXT, encoding="utf-8", newline="\n")
    write_json_atomic(workspace / "task.json", task)
    (workspace / SERVICE_DIRECTORY).mkdir()
    service_error = validate_initial_service_baseline(workspace, snapshot_workspace(workspace))
    if service_error:
        return None, [service_error["errorCode"]]
    return workspace, errors


def relative_to_root(root: Path, path: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def validate_initial_service_baseline(workspace: Path, snapshot: dict[str, Any]) -> dict[str, Any] | None:
    workspace_error = snapshot_error(snapshot)
    if workspace_error:
        return workspace_error
    service_error = validate_service_directory(workspace, snapshot)
    if service_error:
        return service_error
    top_level = {path.split("/", 1)[0] for path in snapshot if path != "__workspace_error__"}
    unexpected = sorted(top_level - INITIAL_SERVICE_TOP_LEVEL_PATHS)
    if unexpected:
        return {
            "errorCode": "REAL_TEST_UNEXPECTED_FILE",
            "errorMessage": f"Initial isolated workspace contains unexpected service paths: {unexpected}",
            "unexpectedPaths": unexpected,
        }
    for required in ("AGENTS.md", "task.json", ".git", SERVICE_DIRECTORY):
        if required not in snapshot:
            return {
                "errorCode": "REAL_TEST_SERVICE_DIRECTORY_MISSING" if required == SERVICE_DIRECTORY else "ISOLATED_WORKSPACE_INVALID",
                "errorMessage": f"Initial isolated workspace is missing {required}.",
                "unexpectedPaths": [],
            }
    return None


def validate_service_directory(workspace: Path, snapshot: dict[str, Any] | None = None, initial_snapshot: dict[str, Any] | None = None) -> dict[str, Any] | None:
    state = service_directory_state(workspace)
    initial_had_directory = bool(initial_snapshot and initial_snapshot.get(SERVICE_DIRECTORY, {}).get("type") == "directory")
    if not state["exists"]:
        return _service_error("REAL_TEST_SERVICE_DIRECTORY_REPLACED" if initial_had_directory else "REAL_TEST_SERVICE_DIRECTORY_MISSING", state)
    if state["type"] != "directory" or state["isSymlink"] or state["isReparsePoint"] or not state["insideWorkspace"]:
        return _service_error("REAL_TEST_SERVICE_DIRECTORY_REPLACED" if initial_had_directory else "REAL_TEST_SERVICE_DIRECTORY_INVALID", state)
    if state["entries"]:
        return _service_error("REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY", state, state["entries"])
    effective_snapshot = snapshot if snapshot is not None else snapshot_workspace(workspace)
    snapshot_entry = effective_snapshot.get(SERVICE_DIRECTORY)
    if not isinstance(snapshot_entry, dict) or snapshot_entry.get("type") != "directory":
        return _service_error("REAL_TEST_SERVICE_DIRECTORY_REPLACED" if initial_had_directory else "REAL_TEST_SERVICE_DIRECTORY_INVALID", state)
    child_paths = sorted(path for path in effective_snapshot if path.startswith(f"{SERVICE_DIRECTORY}/"))
    if child_paths:
        return _service_error("REAL_TEST_SERVICE_DIRECTORY_NOT_EMPTY", state, child_paths)
    return None


def service_directory_state(workspace: Path) -> dict[str, Any]:
    path = workspace / SERVICE_DIRECTORY
    state: dict[str, Any] = {
        "path": SERVICE_DIRECTORY,
        "exists": False,
        "type": "missing",
        "isSymlink": False,
        "isReparsePoint": False,
        "insideWorkspace": False,
        "entries": [],
    }
    try:
        root = workspace.resolve(strict=True)
    except OSError:
        return state
    try:
        exists = path.exists() or path.is_symlink()
        state["exists"] = exists
        if not exists:
            return state
        state["isSymlink"] = path.is_symlink()
        state["isReparsePoint"] = _is_reparse_point(path)
        try:
            path.resolve(strict=True).relative_to(root)
            state["insideWorkspace"] = True
        except (OSError, ValueError):
            state["insideWorkspace"] = False
        if state["isSymlink"] or state["isReparsePoint"]:
            state["type"] = "link"
            return state
        if path.is_dir():
            state["type"] = "directory"
            entries: list[str] = []
            for child in path.iterdir():
                entries.append(child.relative_to(root).as_posix())
            state["entries"] = sorted(entries)
        elif path.is_file():
            state["type"] = "file"
        else:
            state["type"] = "other"
    except OSError:
        state["type"] = "invalid"
    return state


def _is_reparse_point(path: Path) -> bool:
    try:
        attrs = path.stat(follow_symlinks=False).st_file_attributes
    except (AttributeError, OSError):
        return False
    return bool(attrs & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _service_error(error_code: str, state: dict[str, Any], unexpected_paths: list[str] | None = None) -> dict[str, Any]:
    return {
        "errorCode": error_code,
        "errorMessage": f"Invalid {SERVICE_DIRECTORY} service directory state.",
        "serviceDirectory": state,
        "unexpectedPaths": unexpected_paths or [],
    }
