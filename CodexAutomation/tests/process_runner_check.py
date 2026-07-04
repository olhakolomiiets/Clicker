from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from codex_runner import run_process_with_file_logs_and_timeout, run_process_with_timeout  # noqa: E402


class FakeCompleted:
    def __init__(self, returncode: int = 0, stderr: str = "") -> None:
        self.returncode = returncode
        self.stderr = stderr


class FakeFileLogProcess:
    instances: list["FakeFileLogProcess"] = []

    def __init__(self, command: list[str], cwd: str, stdin: object, stdout: object, stderr: object, shell: bool, text: bool) -> None:
        self.command = command
        self.cwd = cwd
        self.stdin_arg = stdin
        self.stdout_arg = stdout
        self.stderr_arg = stderr
        self.shell = shell
        self.text = text
        self.pid = 4321
        self.returncode: int | None = None
        self.wait_results: list[object] = [0]
        self.kill_called = False
        self.communicate_called = False
        FakeFileLogProcess.instances.append(self)

    def wait(self, timeout: int | None = None) -> int:
        result = self.wait_results.pop(0)
        if result == "timeout":
            raise subprocess.TimeoutExpired(self.command, timeout)
        self.returncode = int(result)
        return self.returncode

    def kill(self) -> None:
        self.kill_called = True

    def communicate(self, *args: object, **kwargs: object) -> tuple[str, str]:
        self.communicate_called = True
        raise AssertionError("file-log probe path must not call communicate")


class ProcessRunnerCheck(unittest.TestCase):
    def test_file_log_runner_normal_exit_uses_files_and_devnull(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            FakeFileLogProcess.instances = []
            with mock.patch("codex_runner.subprocess.Popen", side_effect=FakeFileLogProcess):
                result = run_process_with_file_logs_and_timeout(["cmd"], temp_path, temp_path / "out.log", temp_path / "err.log", 5, 2, 1)
        process = FakeFileLogProcess.instances[0]
        self.assertEqual(result["exit_code"], 0)
        self.assertFalse(result["timed_out"])
        self.assertIs(process.stdin_arg, subprocess.DEVNULL)
        self.assertFalse(process.shell)
        self.assertFalse(process.communicate_called)
        self.assertNotEqual(process.stdout_arg, subprocess.PIPE)
        self.assertNotEqual(process.stderr_arg, subprocess.PIPE)

    def test_file_log_runner_nonzero_exit_returns_code(self) -> None:
        def popen(*args: object, **kwargs: object) -> FakeFileLogProcess:
            process = FakeFileLogProcess(*args, **kwargs)
            process.wait_results = [7]
            return process

        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            with mock.patch("codex_runner.subprocess.Popen", side_effect=popen):
                result = run_process_with_file_logs_and_timeout(["cmd"], temp_path, temp_path / "out.log", temp_path / "err.log", 5, 2, 1)
        self.assertEqual(result["exit_code"], 7)
        self.assertFalse(result["timed_out"])

    def test_file_log_runner_start_oserror_returns_stable_result(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            with mock.patch("codex_runner.subprocess.Popen", side_effect=OSError("no start")):
                result = run_process_with_file_logs_and_timeout(["cmd"], temp_path, temp_path / "out.log", temp_path / "err.log", 5, 2, 1)
        self.assertFalse(result["process_started"])
        self.assertEqual(result["process_start_error"], "no start")

    def test_file_log_runner_timeout_taskkill_and_fallback_are_bounded(self) -> None:
        events: list[str] = []

        def popen(*args: object, **kwargs: object) -> FakeFileLogProcess:
            process = FakeFileLogProcess(*args, **kwargs)
            process.wait_results = ["timeout", "timeout", "timeout"]
            return process

        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            with mock.patch("codex_runner.subprocess.Popen", side_effect=popen), mock.patch(
                "codex_runner.platform.system", return_value="Windows"
            ), mock.patch("codex_runner.subprocess.run", return_value=FakeCompleted(0)):
                result = run_process_with_file_logs_and_timeout(
                    ["cmd"],
                    temp_path,
                    temp_path / "out.log",
                    temp_path / "err.log",
                    5,
                    2,
                    1,
                    lambda event, _result: events.append(event),
                )
        process = FakeFileLogProcess.instances[-1]
        self.assertTrue(result["timed_out"])
        self.assertTrue(result["termination"]["fallback_kill_attempted"])
        self.assertTrue(result["termination"]["incomplete"])
        self.assertTrue(process.kill_called)
        self.assertFalse(process.communicate_called)
        self.assertIn("timeout", events)
        self.assertIn("termination_incomplete", events)

    def test_file_log_runner_taskkill_timeout_returns(self) -> None:
        def popen(*args: object, **kwargs: object) -> FakeFileLogProcess:
            process = FakeFileLogProcess(*args, **kwargs)
            process.wait_results = ["timeout", 1]
            return process

        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            with mock.patch("codex_runner.subprocess.Popen", side_effect=popen), mock.patch(
                "codex_runner.platform.system", return_value="Windows"
            ), mock.patch("codex_runner.subprocess.run", side_effect=subprocess.TimeoutExpired(["taskkill"], 2)):
                result = run_process_with_file_logs_and_timeout(["cmd"], temp_path, temp_path / "out.log", temp_path / "err.log", 5, 2, 1)
        self.assertTrue(result["timed_out"])
        self.assertEqual(result["termination"]["stderr"], "taskkill timed out.")

    def test_file_log_runner_taskkill_nonzero_can_fallback_bounded(self) -> None:
        def popen(*args: object, **kwargs: object) -> FakeFileLogProcess:
            process = FakeFileLogProcess(*args, **kwargs)
            process.wait_results = ["timeout", "timeout", 0]
            return process

        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            temp_path = Path(temp)
            with mock.patch("codex_runner.subprocess.Popen", side_effect=popen), mock.patch(
                "codex_runner.platform.system", return_value="Windows"
            ), mock.patch("codex_runner.subprocess.run", return_value=FakeCompleted(5, "taskkill failed")):
                result = run_process_with_file_logs_and_timeout(["cmd"], temp_path, temp_path / "out.log", temp_path / "err.log", 5, 2, 1)
        process = FakeFileLogProcess.instances[-1]
        self.assertTrue(result["timed_out"])
        self.assertEqual(result["termination"]["exit_code"], 5)
        self.assertFalse(result["termination"]["succeeded"])
        self.assertTrue(result["termination"]["fallback_kill_attempted"])
        self.assertTrue(process.kill_called)

    def test_pipe_runner_second_communicate_timeout_returns_without_unbounded_wait(self) -> None:
        class FakePipeProcess:
            def __init__(self, *args: object, **kwargs: object) -> None:
                self.pid = 99
                self.returncode = None
                self.stdin = mock.Mock()
                self.stdout = mock.Mock()
                self.stderr = mock.Mock()
                self.calls = 0
                self.kill_called = False

            def communicate(self, *args: object, **kwargs: object) -> tuple[str, str]:
                self.calls += 1
                raise subprocess.TimeoutExpired(["cmd"], kwargs.get("timeout"))

            def kill(self) -> None:
                self.kill_called = True

        with mock.patch("codex_runner.subprocess.Popen", return_value=FakePipeProcess()), mock.patch(
            "codex_runner.platform.system", return_value="Windows"
        ), mock.patch("codex_runner.subprocess.run", return_value=FakeCompleted(0)):
            result = run_process_with_timeout(["cmd"], ROOT, "", 1)
        self.assertTrue(result["timed_out"])
        self.assertTrue(result["termination"]["incomplete"])
        self.assertIn("process output collection timed out after kill", result["termination"]["stderr"])


if __name__ == "__main__":
    unittest.main()
