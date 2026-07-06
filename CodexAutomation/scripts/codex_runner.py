from __future__ import annotations

import json
import platform
import shutil
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from file_utils import open_text_long_safe, read_json, write_json_atomic
from schema_validator import validate


PASS_VERDICT = "PASS"
EXPECTED_WORKFLOW_ID = "codex-automation-bootstrap"


class RunnerErrorCodes:
    CODEX_CLI_NOT_FOUND = "CODEX_CLI_NOT_FOUND"
    CODEX_CLI_UNAVAILABLE = "CODEX_CLI_UNAVAILABLE"
    CODEX_EXEC_FAILED = "CODEX_EXEC_FAILED"
    CONFIG_INVALID = "CONFIG_INVALID"
    TIMEOUT = "TIMEOUT"
    EMPTY_STDOUT = "EMPTY_STDOUT"
    RESULT_MISSING = "RESULT_MISSING"
    CODEX_LAST_MESSAGE_MISSING = "CODEX_LAST_MESSAGE_MISSING"
    CODEX_LAST_MESSAGE_EMPTY = "CODEX_LAST_MESSAGE_EMPTY"
    CODEX_RESULT_INVALID_JSON = "CODEX_RESULT_INVALID_JSON"
    PROCESS_TREE_TERMINATION_FAILED = "PROCESS_TREE_TERMINATION_FAILED"
    JSON_EXTRA_TEXT = "JSON_EXTRA_TEXT"
    INVALID_JSON = "INVALID_JSON"
    SCHEMA_VALIDATION_FAILED = "SCHEMA_VALIDATION_FAILED"
    TASK_ID_MISMATCH = "TASK_ID_MISMATCH"
    WORKFLOW_ID_MISMATCH = "WORKFLOW_ID_MISMATCH"
    READ_ONLY_NOT_CONFIRMED = "READ_ONLY_NOT_CONFIRMED"
    FILES_CHANGED_REPORTED = "FILES_CHANGED_REPORTED"
    FAILED_WRITE_DETECTED = "FAILED_WRITE_DETECTED"
    PROMPT_MISSING = "PROMPT_MISSING"
    SCHEMA_MISSING = "SCHEMA_MISSING"
    TASK_FILE_MISSING = "TASK_FILE_MISSING"
    WORKFLOW_INVALID = "WORKFLOW_INVALID"
    GIT_STATUS_FAILED = "GIT_STATUS_FAILED"


def run_smoke_test(root: Path, automation_root: Path, config: dict[str, Any], task: dict[str, Any]) -> dict[str, Any]:
    start_monotonic = time.monotonic()
    started_at = _utc_now()
    task_id = str(task.get("id", ""))
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")

    paths_config = config.get("paths", {}) if isinstance(config, dict) else {}
    logs_root = _resolve_path(root, paths_config.get("logs_root", "CodexAutomation/runtime/logs"))
    results_root = _resolve_path(root, paths_config.get("results_root", "CodexAutomation/runtime/results"))
    reports_root = _resolve_path(root, paths_config.get("reports_root", "CodexAutomation/runtime/reports"))
    logs_root.mkdir(parents=True, exist_ok=True)
    results_root.mkdir(parents=True, exist_ok=True)
    reports_root.mkdir(parents=True, exist_ok=True)

    stdout_path = logs_root / f"{task_id}_{timestamp}_stdout.log"
    stderr_path = logs_root / f"{task_id}_{timestamp}_stderr.log"
    last_message_path = results_root / f"{task_id}_{timestamp}_last_message.txt"
    result_path = results_root / f"{task_id}_{timestamp}_result.json"
    report_path = reports_root / f"{task_id}_{timestamp}_RUN_REPORT.json"

    report: dict[str, Any] = {
        "format_version": 1,
        "stage": "BOOTSTRAP-02",
        "task_id": task_id,
        "started_at_utc": started_at,
        "finished_at_utc": None,
        "duration_seconds": None,
        "command": [],
        "exit_code": None,
        "timeout": False,
        "timedOut": False,
        "processPid": None,
        "timeoutSeconds": None,
        "processTreeTerminationAttempted": False,
        "processTreeTerminationSucceeded": False,
        "processTreeTerminationCommand": [],
        "processTreeTerminationExitCode": None,
        "processTreeTerminationStderr": "",
        "resultSource": "output-last-message",
        "lastMessagePath": _relative(root, last_message_path),
        "lastMessageExists": False,
        "lastMessageSizeBytes": 0,
        "error_code": None,
        "errors": [],
        "schema_validation": {
            "valid": False,
            "errors": [],
        },
        "paths": {
            "stdout": _relative(root, stdout_path),
            "stderr": _relative(root, stderr_path),
            "result": _relative(root, result_path),
            "run_report": _relative(root, report_path),
        },
        "git": {
            "before": [],
            "after": [],
            "changed": [],
        },
        "verdict": "FAILED",
    }

    before_snapshot, git_error = _git_snapshot(root)
    report["git"]["before"] = before_snapshot
    if git_error:
        _finish_report(report, report_path, start_monotonic, RunnerErrorCodes.GIT_STATUS_FAILED, git_error)
        return report

    prompt_path = _resolve_path(root, task.get("promptFile"))
    task_path = _resolve_path(root, task.get("taskFile"))
    schema_path = _resolve_path(root, task.get("schemaFile"))
    timeout_seconds = int(task.get("timeoutSeconds") or config.get("codex_runner", {}).get("smoke_test_timeout_seconds", 180))
    report["timeoutSeconds"] = timeout_seconds

    config_error = _validate_runner_config(config, root)
    if config_error:
        _write_text(stdout_path, "")
        _write_text(stderr_path, config_error[1])
        _finish_report(report, report_path, start_monotonic, config_error[0], config_error[1])
        return report

    setup_error = _validate_input_files(prompt_path, task_path, schema_path)
    if setup_error:
        _write_text(stdout_path, "")
        _write_text(stderr_path, setup_error[1])
        _finish_report(report, report_path, start_monotonic, setup_error[0], setup_error[1])
        return report

    codex_command, codex_error = _find_codex_command(config, root)
    if codex_error:
        _write_text(stdout_path, "")
        _write_text(stderr_path, codex_error[1])
        _finish_report(report, report_path, start_monotonic, codex_error[0], codex_error[1])
        return report

    command = [
        codex_command,
        "exec",
        "--sandbox",
        "read-only",
        "--cd",
        str(root),
        "--output-schema",
        str(schema_path),
        "--output-last-message",
        str(last_message_path),
        "--color",
        "never",
        "-",
    ]
    report["command"] = command

    prompt = _build_prompt(prompt_path, task_path)
    process_result = run_process_with_timeout(command, root, prompt, timeout_seconds)
    stdout = process_result["stdout"]
    stderr = process_result["stderr"]
    timed_out = process_result["timed_out"]
    exit_code = process_result["exit_code"]

    _write_text(stdout_path, stdout)
    _write_text(stderr_path, stderr)
    report["exit_code"] = exit_code
    report["timeout"] = timed_out
    report["timedOut"] = timed_out
    report["processPid"] = process_result["pid"]
    report["processTreeTerminationAttempted"] = process_result["termination"]["attempted"]
    report["processTreeTerminationSucceeded"] = process_result["termination"]["succeeded"]
    report["processTreeTerminationCommand"] = process_result["termination"]["command"]
    report["processTreeTerminationExitCode"] = process_result["termination"]["exit_code"]
    report["processTreeTerminationStderr"] = process_result["termination"]["stderr"]

    result_data = None
    result_errors: list[str] = []
    if timed_out:
        result_errors.append("Codex exec timed out.")
        if report["processTreeTerminationAttempted"] and not report["processTreeTerminationSucceeded"]:
            result_errors.append(RunnerErrorCodes.PROCESS_TREE_TERMINATION_FAILED)
            result_errors.append(report["processTreeTerminationStderr"] or "No process tree termination stderr.")
    elif exit_code != 0:
        result_errors.append(f"Codex exec returned exit code {exit_code}.")

    last_message_exists = last_message_path.exists()
    last_message_size = last_message_path.stat().st_size if last_message_exists else 0
    report["lastMessageExists"] = last_message_exists
    report["lastMessageSizeBytes"] = last_message_size

    result_text = ""
    if last_message_path.exists():
        result_text = last_message_path.read_text(encoding="utf-8").strip()
    else:
        result_errors.append(RunnerErrorCodes.CODEX_LAST_MESSAGE_MISSING)

    if not result_text:
        if last_message_exists:
            result_errors.append(RunnerErrorCodes.CODEX_LAST_MESSAGE_EMPTY)
    else:
        parsed, parse_errors = _parse_json_object_only(result_text)
        result_errors.extend(parse_errors)
        result_data = parsed

    schema_data: Any = None
    if result_data is not None:
        try:
            schema_data = read_json(schema_path)
            validation_errors = validate(result_data, schema_data)
            report["schema_validation"]["errors"] = validation_errors
            report["schema_validation"]["valid"] = not validation_errors
            result_errors.extend(validation_errors)
        except Exception as exc:
            result_errors.append(f"Schema validation failed to run: {exc}")

    if isinstance(result_data, dict):
        write_json_atomic(result_path, result_data)
        result_errors.extend(_validate_smoke_semantics(task_id, result_data))

    after_snapshot, after_git_error = _git_snapshot(root)
    report["git"]["after"] = after_snapshot
    if after_git_error:
        result_errors.append(after_git_error)
    if before_snapshot != after_snapshot:
        changed = _changed_entries(before_snapshot, after_snapshot)
        report["git"]["changed"] = changed
        result_errors.append("Git working tree snapshot changed during smoke test.")

    error_code = _select_error_code(
        timed_out=timed_out,
        exit_code=exit_code,
        result_text=result_text,
        result_data=result_data,
        result_errors=result_errors,
        schema_valid=report["schema_validation"]["valid"],
        snapshot_changed=before_snapshot != after_snapshot,
    )
    if error_code or result_errors:
        _finish_report(report, report_path, start_monotonic, error_code or RunnerErrorCodes.CODEX_EXEC_FAILED, result_errors)
        return report

    report["verdict"] = PASS_VERDICT
    _finish_report(report, report_path, start_monotonic, None, [])
    return report


def _find_codex_command(config: dict[str, Any], root: Path) -> tuple[str | None, tuple[str, str] | None]:
    runner_config = config.get("codex_runner", {}) if isinstance(config, dict) else {}
    configured_commands = runner_config.get("commands", [])
    candidates: list[str] = []
    if isinstance(configured_commands, list):
        candidates.extend(str(command) for command in configured_commands if isinstance(command, str) and command.strip())
    candidates.extend(["codex.cmd", "codex"])

    seen: set[str] = set()
    for candidate in candidates:
        if candidate in seen:
            continue
        seen.add(candidate)
        executable = shutil.which(candidate)
        if executable is None:
            continue
        completed = subprocess.run([executable, "--version"], cwd=str(root), capture_output=True, text=True, check=False)
        if completed.returncode == 0:
            return executable, None

    return None, (RunnerErrorCodes.CODEX_CLI_NOT_FOUND, "Codex CLI was not found or did not pass --version.")


def _validate_runner_config(config: dict[str, Any], root: Path) -> tuple[str, str] | None:
    runner_config = config.get("codex_runner", {}) if isinstance(config, dict) else {}
    if runner_config.get("safe_mode") != "read_only":
        return RunnerErrorCodes.CONFIG_INVALID, "codex_runner.safe_mode must be read_only for BOOTSTRAP-02."

    working_directory = runner_config.get("project_working_directory", ".")
    resolved_working_directory = _resolve_path(root, working_directory).resolve()
    if resolved_working_directory != root.resolve():
        return (
            RunnerErrorCodes.CONFIG_INVALID,
            "codex_runner.project_working_directory must resolve to the repository root for BOOTSTRAP-02.",
        )
    return None


POST_TERMINATION_WAIT_SECONDS = 5


def run_process_with_timeout(command: list[str], cwd: Path, input_text: str, timeout_seconds: int) -> dict[str, Any]:
    process = subprocess.Popen(
        command,
        cwd=str(cwd),
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    termination = _empty_termination_result()
    timed_out = False
    stdout = ""
    stderr = ""
    try:
        stdout, stderr = process.communicate(input=input_text, timeout=timeout_seconds)
    except subprocess.TimeoutExpired:
        timed_out = True
        termination = terminate_process_tree(process)
        try:
            stdout, stderr = process.communicate(timeout=POST_TERMINATION_WAIT_SECONDS)
        except subprocess.TimeoutExpired:
            termination["fallback_kill_attempted"] = True
            try:
                process.kill()
            except OSError as exc:
                termination["stderr"] = _append_stderr(termination["stderr"], str(exc))
            try:
                stdout, stderr = process.communicate(timeout=POST_TERMINATION_WAIT_SECONDS)
            except subprocess.TimeoutExpired:
                termination["incomplete"] = True
                termination["succeeded"] = False
                termination["stderr"] = _append_stderr(termination["stderr"], "process output collection timed out after kill.")
                _close_process_pipes(process)

    return {
        "pid": process.pid,
        "exit_code": process.returncode,
        "stdout": stdout or "",
        "stderr": stderr or "",
        "timed_out": timed_out,
        "termination": termination,
    }


def run_process_with_file_logs_and_timeout(
    command: list[str],
    cwd: Path,
    stdout_path: Path,
    stderr_path: Path,
    timeout_seconds: int,
    taskkill_timeout_seconds: int,
    post_kill_wait_seconds: int,
    status_callback: Any | None = None,
) -> dict[str, Any]:
    result: dict[str, Any] = {
        "pid": None,
        "exit_code": None,
        "timed_out": False,
        "termination": _empty_termination_result(),
        "process_started": False,
        "process_start_error": None,
    }
    process: subprocess.Popen[Any] | None = None
    with open_text_long_safe(stdout_path, "w", encoding="utf-8", newline="\n") as stdout_file, open_text_long_safe(stderr_path, "w", encoding="utf-8", newline="\n") as stderr_file:
        try:
            process = subprocess.Popen(
                command,
                cwd=str(cwd),
                stdin=subprocess.DEVNULL,
                stdout=stdout_file,
                stderr=stderr_file,
                shell=False,
                text=True,
            )
        except OSError as exc:
            result["process_start_error"] = str(exc)
            if status_callback is not None:
                status_callback("start_failed", result)
            return result
        result["pid"] = process.pid
        result["process_started"] = True
        if status_callback is not None:
            status_callback("started", result)
        try:
            result["exit_code"] = process.wait(timeout=timeout_seconds)
            return result
        except subprocess.TimeoutExpired:
            result["timed_out"] = True
            result["termination"]["attempted"] = True
            if status_callback is not None:
                status_callback("timeout", result)
            result["termination"] = terminate_process_tree(process, taskkill_timeout_seconds)
            result["termination"]["attempted"] = True
            if status_callback is not None:
                status_callback("taskkill_done", result)
            try:
                result["exit_code"] = process.wait(timeout=post_kill_wait_seconds)
            except subprocess.TimeoutExpired:
                result["termination"]["fallback_kill_attempted"] = True
                if status_callback is not None:
                    status_callback("fallback_kill", result)
                try:
                    process.kill()
                except OSError as exc:
                    result["termination"]["stderr"] = _append_stderr(result["termination"]["stderr"], str(exc))
                try:
                    result["exit_code"] = process.wait(timeout=post_kill_wait_seconds)
                except subprocess.TimeoutExpired:
                    result["termination"]["incomplete"] = True
                    result["termination"]["succeeded"] = False
                    result["termination"]["stderr"] = _append_stderr(result["termination"]["stderr"], "process did not exit after fallback kill.")
                    if status_callback is not None:
                        status_callback("termination_incomplete", result)
            return result


def terminate_process_tree(process: subprocess.Popen[str], taskkill_timeout_seconds: int = 10) -> dict[str, Any]:
    result = _empty_termination_result()
    result["attempted"] = True

    if platform.system().lower() == "windows":
        command = ["taskkill", "/PID", str(process.pid), "/T", "/F"]
        result["command"] = command
        try:
            completed = subprocess.run(command, capture_output=True, text=True, check=False, timeout=taskkill_timeout_seconds)
            result["exit_code"] = completed.returncode
            result["stderr"] = (completed.stderr or "").strip()
            result["succeeded"] = completed.returncode == 0
        except subprocess.TimeoutExpired:
            result["exit_code"] = None
            result["stderr"] = "taskkill timed out."
            result["succeeded"] = False
        return result

    try:
        process.terminate()
        result["command"] = ["terminate", str(process.pid)]
        try:
            process.wait(timeout=POST_TERMINATION_WAIT_SECONDS)
        except subprocess.TimeoutExpired:
            process.kill()
            result["fallback_kill_attempted"] = True
            try:
                process.wait(timeout=POST_TERMINATION_WAIT_SECONDS)
            except subprocess.TimeoutExpired:
                result["incomplete"] = True
                result["stderr"] = "process did not exit after fallback kill."
            result["command"] = ["kill", str(process.pid)]
        result["exit_code"] = process.returncode
        result["succeeded"] = process.returncode is not None and not result["incomplete"]
    except OSError as exc:
        result["stderr"] = str(exc)
        result["succeeded"] = False
    return result


def _empty_termination_result() -> dict[str, Any]:
    return {
        "attempted": False,
        "succeeded": False,
        "command": [],
        "exit_code": None,
        "stderr": "",
        "fallback_kill_attempted": False,
        "incomplete": False,
    }


def _append_stderr(current: str, addition: str) -> str:
    if not current:
        return addition
    if not addition:
        return current
    return f"{current}\n{addition}"


def _close_process_pipes(process: subprocess.Popen[Any]) -> None:
    for pipe_name in ("stdin", "stdout", "stderr"):
        pipe = getattr(process, pipe_name, None)
        if pipe is None:
            continue
        try:
            pipe.close()
        except OSError:
            pass


def _build_prompt(prompt_path: Path, task_path: Path) -> str:
    prompt = prompt_path.read_text(encoding="utf-8").strip()
    task = task_path.read_text(encoding="utf-8").strip()
    return f"{prompt}\n\nTask file:\n\n{task}\n"


def _validate_input_files(prompt_path: Path, task_path: Path, schema_path: Path) -> tuple[str, str] | None:
    if not prompt_path.exists():
        return RunnerErrorCodes.PROMPT_MISSING, f"Prompt file is missing: {prompt_path}"
    if not task_path.exists():
        return RunnerErrorCodes.TASK_FILE_MISSING, f"Task file is missing: {task_path}"
    if not schema_path.exists():
        return RunnerErrorCodes.SCHEMA_MISSING, f"Schema file is missing: {schema_path}"
    return None


def _parse_json_object_only(text: str) -> tuple[Any, list[str]]:
    stripped = text.strip()
    if not stripped:
        return None, [RunnerErrorCodes.RESULT_MISSING]
    if not stripped.startswith("{") or not stripped.endswith("}"):
        return None, [RunnerErrorCodes.CODEX_RESULT_INVALID_JSON]
    try:
        decoder = json.JSONDecoder()
        data, end_index = decoder.raw_decode(stripped)
    except json.JSONDecodeError as exc:
        return None, [f"{RunnerErrorCodes.CODEX_RESULT_INVALID_JSON}: {exc}"]
    if end_index != len(stripped):
        return None, [RunnerErrorCodes.CODEX_RESULT_INVALID_JSON]
    if not isinstance(data, dict):
        return None, [RunnerErrorCodes.CODEX_RESULT_INVALID_JSON]
    return data, []


def _validate_smoke_semantics(task_id: str, result_data: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if result_data.get("taskId") != task_id:
        errors.append(RunnerErrorCodes.TASK_ID_MISMATCH)
    if result_data.get("workflowId") != EXPECTED_WORKFLOW_ID:
        errors.append(RunnerErrorCodes.WORKFLOW_ID_MISMATCH)
    if result_data.get("readOnlyConfirmed") is not True:
        errors.append(RunnerErrorCodes.READ_ONLY_NOT_CONFIRMED)
    files_changed = result_data.get("filesChanged")
    if files_changed != []:
        errors.append(RunnerErrorCodes.FILES_CHANGED_REPORTED)
    return errors


def _select_error_code(
    timed_out: bool,
    exit_code: int | None,
    result_text: str,
    result_data: Any,
    result_errors: list[str],
    schema_valid: bool,
    snapshot_changed: bool,
) -> str | None:
    if timed_out:
        return RunnerErrorCodes.TIMEOUT
    if snapshot_changed:
        return RunnerErrorCodes.FAILED_WRITE_DETECTED
    if exit_code != 0:
        return RunnerErrorCodes.CODEX_EXEC_FAILED
    if RunnerErrorCodes.CODEX_LAST_MESSAGE_MISSING in result_errors:
        return RunnerErrorCodes.CODEX_LAST_MESSAGE_MISSING
    if RunnerErrorCodes.CODEX_LAST_MESSAGE_EMPTY in result_errors:
        return RunnerErrorCodes.CODEX_LAST_MESSAGE_EMPTY
    if any(str(error).startswith(RunnerErrorCodes.CODEX_RESULT_INVALID_JSON) for error in result_errors):
        return RunnerErrorCodes.CODEX_RESULT_INVALID_JSON
    if any(str(error).startswith(RunnerErrorCodes.JSON_EXTRA_TEXT) for error in result_errors):
        return RunnerErrorCodes.JSON_EXTRA_TEXT
    if any(str(error).startswith(RunnerErrorCodes.INVALID_JSON) for error in result_errors):
        return RunnerErrorCodes.INVALID_JSON
    if not result_text:
        return RunnerErrorCodes.RESULT_MISSING
    if result_data is None:
        return RunnerErrorCodes.INVALID_JSON
    if not schema_valid:
        return RunnerErrorCodes.SCHEMA_VALIDATION_FAILED
    for code in (
        RunnerErrorCodes.TASK_ID_MISMATCH,
        RunnerErrorCodes.WORKFLOW_ID_MISMATCH,
        RunnerErrorCodes.READ_ONLY_NOT_CONFIRMED,
        RunnerErrorCodes.FILES_CHANGED_REPORTED,
    ):
        if code in result_errors:
            return code
    return None


def _git_snapshot(root: Path) -> tuple[list[str], str | None]:
    completed = subprocess.run(
        ["git", "status", "--porcelain=v1", "--untracked-files=all"],
        cwd=str(root),
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        return [], completed.stderr.strip() or "git status failed."
    return completed.stdout.splitlines(), None


def _changed_entries(before: list[str], after: list[str]) -> list[str]:
    before_set = set(before)
    after_set = set(after)
    return sorted(before_set.symmetric_difference(after_set))


def _finish_report(
    report: dict[str, Any],
    report_path: Path,
    start_monotonic: float,
    error_code: str | None,
    errors: str | list[str],
) -> None:
    report["finished_at_utc"] = _utc_now()
    report["duration_seconds"] = round(time.monotonic() - start_monotonic, 3)
    if error_code:
        report["error_code"] = error_code
        report["verdict"] = "FAILED"
    if isinstance(errors, str):
        report["errors"] = [errors] if errors else []
    else:
        report["errors"] = [str(error) for error in errors if str(error)]
    write_json_atomic(report_path, report)


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _resolve_path(root: Path, value: Any) -> Path:
    if not isinstance(value, str) or not value.strip():
        return root
    path = Path(value)
    if path.is_absolute():
        return path
    return root / path


def _relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve())).replace("\\", "/")
    except ValueError:
        return str(path.resolve())


def _decode_timeout_output(value: str | bytes | None) -> str:
    if value is None:
        return ""
    if isinstance(value, bytes):
        return value.decode("utf-8", errors="replace")
    return value


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()
