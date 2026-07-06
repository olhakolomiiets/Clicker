from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

import codex_runner  # noqa: E402
import file_utils  # noqa: E402
import real_role_runner  # noqa: E402
from file_utils import read_json, read_text_long_safe, write_text_long_safe  # noqa: E402
from real_role_runner import (  # noqa: E402
    _create_probe_run_context,
    _initialize_probe_text_log,
    _read_text_if_exists,
    _run_real_role_sandbox_probe_with_context,
    _sandbox_probe_powershell_script,
    _snapshot_probe_workspace,
)


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class SandboxProbeLongPathTests(unittest.TestCase):
    def test_write_text_long_safe_handles_deep_empty_and_unicode_text(self) -> None:
        with _runtime_root() as root:
            final_path = _deep_text_path(root, "direct", "probe_stdout.log")
            self.assertFalse(final_path.parent.exists())

            write_text_long_safe(final_path, "")
            self.assertEqual(read_text_long_safe(final_path), "")

            write_text_long_safe(final_path, "planet\nunicode ok\n")
            if os.name == "nt":
                self.assertGreater(len(str(final_path.resolve(strict=False))), 260)
            self.assertEqual(read_text_long_safe(final_path), "planet\nunicode ok\n")
            self.assertFalse((root / "probe_stdout.log").exists())

    def test_write_text_long_safe_propagates_open_errors(self) -> None:
        with _runtime_root() as root:
            final_path = _deep_text_path(root, "error", "probe_stderr.log")
            with mock.patch("file_utils.open", side_effect=OSError("open denied"), create=True):
                with self.assertRaisesRegex(OSError, "open denied"):
                    write_text_long_safe(final_path, "")

    def test_production_probe_log_initialization_handles_deep_paths(self) -> None:
        with _runtime_root() as root:
            probe_root = _deep_probe_root(root)
            stdout_path = probe_root / "probe_stdout.log"
            stderr_path = probe_root / "probe_stderr.log"

            _initialize_probe_text_log(stdout_path)
            _initialize_probe_text_log(stderr_path)

            if os.name == "nt":
                self.assertGreater(len(str(stdout_path.resolve(strict=False))), 260)
                self.assertGreater(len(str(stderr_path.resolve(strict=False))), 260)
            self.assertEqual(read_text_long_safe(stdout_path), "")
            self.assertEqual(read_text_long_safe(stderr_path), "")

    def test_sandbox_probe_script_uses_long_safe_dotnet_marker_operations(self) -> None:
        script = _sandbox_probe_powershell_script()

        self.assertIn("Convert-ToExtendedPath", script)
        self.assertIn("sandbox_write_probe.txt", script)
        self.assertIn("CODEX_SANDBOX_WRITE_OK", script)
        self.assertIn("[System.IO.File]::WriteAllText($markerFilesystemPath,$expected,$utf8NoBom)", script)
        self.assertIn("[System.IO.File]::Exists($markerFilesystemPath)", script)
        self.assertIn("[System.IO.File]::ReadAllText($markerFilesystemPath,$utf8NoBom)", script)
        self.assertIn("[System.IO.File]::Delete($markerFilesystemPath)", script)
        self.assertIn("PROBE_FILE_CREATED", script)
        self.assertIn("PROBE_FILE_READ_BACK", script)
        self.assertIn("PROBE_FILE_REMOVED", script)
        self.assertNotIn("Test-Path", script)
        self.assertNotIn("Remove-Item", script)
        self.assertNotIn("Get-Content", script)
        self.assertNotIn("Set-Content", script)
        self.assertNotIn("Out-File", script)
        self.assertNotIn("'\\\\?\\\\' +", script)

    @unittest.skipUnless(os.name == "nt" and shutil.which("powershell.exe"), "Windows PowerShell is required")
    def test_generated_powershell_probe_creates_reads_and_removes_deep_marker(self) -> None:
        with _runtime_root() as root:
            context = _deep_probe_context(root)
            os.makedirs(file_utils._fs_path(context.workspace), exist_ok=True)
            marker_path = context.workspace / "sandbox_write_probe.txt"
            self.assertGreater(len(str(marker_path.resolve(strict=False))), 260)

            completed = subprocess.run(
                ["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", _sandbox_probe_powershell_script()],
                cwd=str(context.workspace),
                capture_output=True,
                text=True,
                check=False,
                timeout=30,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn("PROBE_FILE_CREATED", completed.stdout)
            self.assertIn("PROBE_FILE_READ_BACK", completed.stdout)
            self.assertIn("PROBE_FILE_REMOVED", completed.stdout)
            self.assertNotIn("WriteAllText", completed.stderr)
            self.assertFalse(os.path.exists(file_utils._fs_path(marker_path)))
            self.assertEqual(_snapshot_probe_workspace(context.workspace), {})

    @unittest.skipUnless(os.name == "nt" and shutil.which("powershell.exe"), "Windows PowerShell is required")
    def test_generated_powershell_probe_failure_is_nonzero_without_success_tokens(self) -> None:
        with _runtime_root() as root:
            context = _deep_probe_context(root)
            os.makedirs(file_utils._fs_path(context.workspace), exist_ok=True)
            marker_path = context.workspace / "sandbox_write_probe.txt"
            script = _sandbox_probe_powershell_script().replace(
                "Write-Output 'PROBE_FILE_CREATED'; ",
                "Write-Output 'PROBE_FILE_CREATED'; [System.IO.File]::WriteAllText($markerFilesystemPath,'BAD',$utf8NoBom); ",
            )

            completed = subprocess.run(
                ["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", script],
                cwd=str(context.workspace),
                capture_output=True,
                text=True,
                check=False,
                timeout=30,
            )

            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("PROBE_FILE_CREATED", completed.stdout)
            self.assertNotIn("PROBE_FILE_READ_BACK", completed.stdout)
            self.assertNotIn("PROBE_FILE_REMOVED", completed.stdout)
            self.assertFalse({"PROBE_FILE_CREATED", "PROBE_FILE_READ_BACK", "PROBE_FILE_REMOVED"}.issubset(set(completed.stdout.split())))
            try:
                os.unlink(file_utils._fs_path(marker_path))
            except FileNotFoundError:
                pass
            self.assertFalse(os.path.exists(file_utils._fs_path(marker_path)))
            self.assertEqual(_snapshot_probe_workspace(context.workspace), {})

    def test_production_probe_reaches_fake_process_boundary_with_deep_logs(self) -> None:
        with _runtime_root() as root:
            context = _deep_probe_context(root)
            calls: list[tuple[list[str], Path, Path, Path]] = []

            def fake_process(command, cwd, stdout_path, stderr_path, *_args, **_kwargs):
                calls.append((command, cwd, stdout_path, stderr_path))
                write_text_long_safe(stdout_path, "PROBE_FILE_CREATED\nPROBE_FILE_READ_BACK\nPROBE_FILE_REMOVED\n")
                write_text_long_safe(stderr_path, "fake stderr\n")
                return {
                    "pid": 1234,
                    "process_started": True,
                    "process_start_error": None,
                    "timed_out": False,
                    "exit_code": 0,
                    "termination": {
                        "attempted": False,
                        "succeeded": False,
                        "command": [],
                        "exit_code": None,
                        "stderr": "",
                        "fallback_kill_attempted": False,
                        "incomplete": False,
                    },
                }

            with _patched_probe_dependencies(fake_process):
                report = _run_real_role_sandbox_probe_with_context(root, ROOT / "CodexAutomation", CONFIG, context)

            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertEqual(len(calls), 1)
            command, cwd, stdout_path, stderr_path = calls[0]
            self.assertIn("sandbox", command)
            self.assertEqual(cwd, context.workspace)
            self.assertEqual(stdout_path.name, "probe_stdout.log")
            self.assertEqual(stderr_path.name, "probe_stderr.log")
            self.assertEqual(read_text_long_safe(stdout_path), "PROBE_FILE_CREATED\nPROBE_FILE_READ_BACK\nPROBE_FILE_REMOVED\n")
            self.assertEqual(read_text_long_safe(stderr_path), "fake stderr\n")
            self.assertTrue(os.path.exists(file_utils._fs_path(context.run_directory / "SANDBOX_WRITE_PROBE_REPORT.json")))
            self.assertTrue(os.path.exists(file_utils._fs_path(context.run_directory / "probe_command.json")))
            self.assertTrue(os.path.exists(file_utils._fs_path(context.run_directory / "probe_process.json")))

    @unittest.skipUnless(os.name == "nt" and shutil.which("powershell.exe"), "Windows PowerShell is required")
    def test_production_probe_evaluates_generated_powershell_script_on_deep_workspace(self) -> None:
        with _runtime_root() as root:
            context = _deep_probe_context(root)
            marker_path = context.workspace / "sandbox_write_probe.txt"
            calls: list[list[str]] = []

            def fake_process(command, cwd, stdout_path, stderr_path, *_args, **_kwargs):
                calls.append(command)
                separator = command.index("--")
                child_command = command[separator + 1 :]
                self.assertEqual(child_command[:3], ["powershell.exe", "-NoProfile", "-NonInteractive"])
                self.assertEqual(child_command[3], "-Command")
                self.assertEqual(child_command[4], _sandbox_probe_powershell_script())
                completed = subprocess.run(child_command, cwd=str(cwd), capture_output=True, check=False, timeout=30)
                with open(file_utils._fs_path(stdout_path), "wb") as handle:
                    handle.write(completed.stdout)
                with open(file_utils._fs_path(stderr_path), "wb") as handle:
                    handle.write(completed.stderr)
                return {
                    "pid": 1234,
                    "process_started": True,
                    "process_start_error": None,
                    "timed_out": False,
                    "exit_code": completed.returncode,
                    "termination": {
                        "attempted": False,
                        "succeeded": False,
                        "command": [],
                        "exit_code": None,
                        "stderr": "",
                        "fallback_kill_attempted": False,
                        "incomplete": False,
                    },
                }

            with _patched_probe_dependencies(fake_process):
                report = _run_real_role_sandbox_probe_with_context(root, ROOT / "CodexAutomation", CONFIG, context)

            self.assertEqual(report["finalVerdict"], "PASS", report.get("errorMessage"))
            self.assertEqual(len(calls), 1)
            self.assertGreater(len(str(marker_path.resolve(strict=False))), 260)
            self.assertTrue(report["probeFileCreated"])
            self.assertTrue(report["probeFileReadBack"])
            self.assertTrue(report["probeFileRemoved"])
            self.assertEqual(report["unexpectedPaths"], [])
            self.assertFalse(os.path.exists(file_utils._fs_path(marker_path)))
            self.assertEqual(_snapshot_probe_workspace(context.workspace), {})

    def test_probe_stderr_readback_tolerates_invalid_utf8_without_masking_failure(self) -> None:
        with _runtime_root() as root:
            context = _deep_probe_context(root)
            stderr_path = context.run_directory / "probe_stderr.log"
            os.makedirs(file_utils._fs_path(stderr_path.parent), exist_ok=True)
            with open(file_utils._fs_path(stderr_path), "wb") as handle:
                handle.write(b"before \x88 after")

            text = _read_text_if_exists(stderr_path)
            self.assertIn("\ufffd", text)

            def fake_process(_command, _cwd, stdout_path, stderr_path, *_args, **_kwargs):
                write_text_long_safe(stdout_path, "")
                with open(file_utils._fs_path(stderr_path), "wb") as handle:
                    handle.write(b"fatal \x88 stderr")
                return {
                    "pid": 1234,
                    "process_started": True,
                    "process_start_error": None,
                    "timed_out": False,
                    "exit_code": 1,
                    "termination": {
                        "attempted": False,
                        "succeeded": False,
                        "command": [],
                        "exit_code": None,
                        "stderr": "",
                        "fallback_kill_attempted": False,
                        "incomplete": False,
                    },
                }

            with _patched_probe_dependencies(fake_process):
                report = _run_real_role_sandbox_probe_with_context(root, ROOT / "CodexAutomation", CONFIG, context)

            self.assertEqual(report["finalVerdict"], "FAILED")
            self.assertEqual(report["errorCode"], "WINDOWS_SANDBOX_WRITE_PROBE_FAILED")
            self.assertEqual(report["errorMessage"], "Sandbox write probe command failed.")
            self.assertNotIn("UnicodeDecodeError", report["errorMessage"])
            self.assertIn("fatal \ufffd stderr", report["stderrSummary"])

    def test_probe_log_initialization_failure_does_not_start_process(self) -> None:
        with _runtime_root() as root:
            context = _deep_probe_context(root)
            fake_process = mock.Mock()

            with mock.patch("real_role_runner._initialize_probe_text_log", side_effect=OSError("log denied")), _patched_probe_dependencies(fake_process):
                with self.assertRaisesRegex(OSError, "log denied"):
                    _run_real_role_sandbox_probe_with_context(root, ROOT / "CodexAutomation", CONFIG, context)

            fake_process.assert_not_called()
            process_report = read_json(context.run_directory / "probe_process.json")
            self.assertFalse(process_report["processStarted"])
            initial_report = read_json(context.run_directory / "SANDBOX_WRITE_PROBE_REPORT.json")
            self.assertFalse(initial_report["processStarted"])
            self.assertEqual(initial_report["finalVerdict"], "RUNNING")

    def test_process_file_log_runner_uses_long_safe_file_handles_and_reads_back(self) -> None:
        with _runtime_root() as root:
            stdout_path = _deep_text_path(root, "process", "probe_stdout.log")
            stderr_path = stdout_path.with_name("probe_stderr.log")
            opened_paths: list[str] = []

            class FakeProcess:
                pid = 4321
                returncode = 0

                def __init__(self, *args, **kwargs):
                    kwargs["stdout"].write("out from fake\n")
                    kwargs["stderr"].write("err from fake\n")

                def wait(self, timeout=None):
                    return 0

            def spy_open(path: Path, mode: str, **kwargs):
                opened_paths.append(file_utils._fs_path(path))
                return file_utils.open_text_long_safe(path, mode, **kwargs)

            with mock.patch("codex_runner.open_text_long_safe", side_effect=spy_open), mock.patch("codex_runner.subprocess.Popen", side_effect=FakeProcess):
                result = codex_runner.run_process_with_file_logs_and_timeout(["fake"], root, stdout_path, stderr_path, 5, 1, 1)

            self.assertTrue(result["process_started"])
            self.assertEqual(result["exit_code"], 0)
            self.assertEqual(read_text_long_safe(stdout_path), "out from fake\n")
            self.assertEqual(read_text_long_safe(stderr_path), "err from fake\n")
            if os.name == "nt":
                self.assertTrue(all(path.startswith("\\\\?\\") for path in opened_paths))


def _patched_probe_dependencies(fake_process):
    return mock.patch.multiple(
        real_role_runner,
        _codex_version=mock.Mock(return_value=("codex 0.142.5-test", None)),
        _codex_sandbox_help=mock.Mock(return_value="--permissions-profile --cd --config"),
        _git_snapshot=mock.Mock(return_value=[]),
        run_process_with_file_logs_and_timeout=mock.Mock(side_effect=fake_process),
    )


def _deep_probe_context(root: Path):
    return _create_probe_run_context(root, "real_task_real_task_20260706T123037153493Z_b5e8dd91", "20260706T123038329976Z")


def _deep_probe_root(root: Path) -> Path:
    return _deep_probe_context(root).run_directory


def _deep_text_path(root: Path, name: str, filename: str) -> Path:
    return root / ("deep_" + name) / ("segment_" + "x" * 80) / ("segment_" + "y" * 80) / ("segment_" + "z" * 80) / filename


class _runtime_root:
    def __enter__(self) -> Path:
        temp_parent = ROOT / "CodexAutomation" / "runtime" / "real_task_execution_self_tests"
        temp_parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="rtes_20260706T123036409647Z_", dir=temp_parent)
        self.root = Path(self.temp.name) / "parent"
        self.root.mkdir()
        return self.root

    def __exit__(self, exc_type, exc, tb) -> None:
        try:
            self.temp.cleanup()
        except OSError:
            shutil.rmtree(file_utils._fs_path(Path(self.temp.name)), ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
