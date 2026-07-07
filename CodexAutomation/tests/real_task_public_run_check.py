from __future__ import annotations

import inspect
import json
import os
import subprocess
import sys
import tempfile
import unittest
import ctypes
from pathlib import Path
from unittest import mock
from ctypes import wintypes


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
import real_task_public_runner as public_runner  # noqa: E402
from real_task_execution_models import RealTaskExecutionResult, canonical_sha256, file_sha256  # noqa: E402
from real_task_public_runner import (  # noqa: E402
    MAX_PUBLIC_MANIFEST_BYTES,
    PUBLIC_LOCK_PATH,
    PUBLIC_REAL_TASK_BLOCKED,
    PUBLIC_REAL_TASK_INVALID_INPUT,
    _PublicRunLock,
    load_public_manifest_authority,
    resolve_public_manifest_path,
    run_public_real_task,
)
from real_task_runner import _execute_real_task_with_production_adapter  # noqa: E402
from schema_validator import validate as validate_schema_instance  # noqa: E402


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class FakeWin32Function:
    def __init__(self, name: str, impl=None) -> None:
        self.name = name
        self.impl = impl or (lambda *args: True)
        self.argtypes = None
        self.restype = None
        self.calls: list[tuple] = []

    def __call__(self, *args):
        self.calls.append(args)
        return self.impl(*args)


class FakeKernel32:
    def __init__(self, handle: int = 0x1234567887654321) -> None:
        self.handle = handle
        self.metadata = b'{"lockVersion":1,"manifestRelativePath":"CodexAutomation/tasks/real_tasks/task.json","manifestSha256":"' + (b"a" * 64) + b'","ownerTokenHash":"' + (b"b" * 64) + b'","processId":1,"publicRunId":"public-1","startedAt":"2026-01-01T00:00:00Z"}\n'
        self.CreateFileW = FakeWin32Function("CreateFileW", lambda *_args: self.handle)
        self.WriteFile = FakeWin32Function("WriteFile", self._write_file)
        self.ReadFile = FakeWin32Function("ReadFile", self._read_file)
        self.SetFilePointerEx = FakeWin32Function("SetFilePointerEx")
        self.FlushFileBuffers = FakeWin32Function("FlushFileBuffers")
        self.GetFileSizeEx = FakeWin32Function("GetFileSizeEx")
        self.GetFileInformationByHandle = FakeWin32Function("GetFileInformationByHandle", self._file_info)
        self.SetFileInformationByHandle = FakeWin32Function("SetFileInformationByHandle")
        self.CloseHandle = FakeWin32Function("CloseHandle")

    def _write_file(self, _handle, _buffer, length, written, _overlapped):
        written._obj.value = int(length.value if hasattr(length, "value") else length)
        return True

    def _read_file(self, _handle, buffer, length, read, _overlapped):
        size = min(len(self.metadata), int(length.value if hasattr(length, "value") else length))
        ctypes.memmove(buffer, self.metadata, size)
        read._obj.value = size
        return True

    def _file_info(self, _handle, info_ptr):
        info = info_ptr._obj
        info.dwFileAttributes = 0x80
        info.dwVolumeSerialNumber = 7
        info.nFileSizeHigh = 0
        info.nFileSizeLow = len(self.metadata)
        info.nFileIndexHigh = 0x12
        info.nFileIndexLow = 0x34
        return True


def _handle_value(value) -> int | None:
    return value.value if hasattr(value, "value") else value


def _configured_fake_win_lock(fake_kernel: FakeKernel32):
    api = public_runner._configure_win32_kernel_api(fake_kernel)
    lock = _PublicRunLock(Path("C:/lock/public.lock"))
    lock.public_run_id = "public-1"
    lock.manifest_sha256 = "a" * 64
    lock.owner_token_hash = "b" * 64
    lock._win_handle = fake_kernel.handle
    with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api):
        lock.identity = public_runner._win_file_identity(fake_kernel.handle)
    fake_kernel.GetFileInformationByHandle.calls.clear()
    return lock, api


class PublicRealTaskGateTests(unittest.TestCase):
    def test_public_generic_gate_matrix_and_legacy_field(self) -> None:
        with synthetic_public_parent() as parent:
            manifest_arg = "CodexAutomation/tasks/real_tasks/task.json"
            cases = [
                (False, False, PUBLIC_REAL_TASK_INVALID_INPUT, "REAL_TASK_PUBLIC_RUN_DISABLED"),
                (True, False, PUBLIC_REAL_TASK_INVALID_INPUT, "REAL_TASK_PUBLIC_RUN_DISABLED"),
                (False, True, PUBLIC_REAL_TASK_INVALID_INPUT, "REAL_TASK_PUBLIC_RUN_DISABLED"),
                (True, True, 0, None),
            ]
            for allow_execution, public_enabled, expected_code, expected_error in cases:
                config = enabled_config(allow_execution=allow_execution, public_enabled=public_enabled)
                with self.subTest(allow_execution=allow_execution, public_enabled=public_enabled):
                    with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution(parent)):
                        result = run_public_real_task(parent, ROOT / "CodexAutomation", config, manifest_arg)
                self.assertEqual(result.exitCode, expected_code)
                self.assertEqual(result.report.get("errorCode"), expected_error)

        legacy = enabled_config()
        legacy["realTaskExecutionPolicy"]["publicRunCliEnabled"] = True
        with synthetic_public_parent() as parent:
            result = run_public_real_task(parent, ROOT / "CodexAutomation", legacy, "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, PUBLIC_REAL_TASK_INVALID_INPUT)
        self.assertEqual(result.report["errorCode"], "REAL_TASK_EXECUTION_POLICY_INVALID")

    def test_environment_cannot_enable_public_gate(self) -> None:
        with synthetic_public_parent() as parent, mock.patch.dict(os.environ, {"PUBLIC_GENERIC_REAL_TASK_RUN_ENABLED": "true"}):
            result = run_public_real_task(parent, ROOT / "CodexAutomation", json.loads(json.dumps(CONFIG)), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, PUBLIC_REAL_TASK_INVALID_INPUT)
        self.assertEqual(result.report["errorCode"], "REAL_TASK_PUBLIC_RUN_DISABLED")


class PublicManifestAuthorityTests(unittest.TestCase):
    def test_public_manifest_path_contract(self) -> None:
        with synthetic_public_parent() as parent:
            accepted, error = resolve_public_manifest_path(parent, "CodexAutomation/tasks/real_tasks/task.json")
            self.assertIsNotNone(accepted)
            self.assertIsNone(error)

            oversized = parent / "CodexAutomation" / "tasks" / "real_tasks" / "oversized.json"
            oversized.write_bytes(b" " * (MAX_PUBLIC_MANIFEST_BYTES + 1))
            for item in (
                "CodexAutomation/tasks/real_tasks/task.JSON",
                "CodexAutomation/tasks/real_tasks/task.*.json",
                "CodexAutomation/tasks/real_tasks/task?.json",
                "CodexAutomation/tasks/real_tasks/[task].json",
                "CodexAutomation/tasks/real_tasks/../task.json",
                "CodexAutomation/tasks/real_tasks/./task.json",
                "CodexAutomation\\tasks\\real_tasks\\task.json",
                "C:/outside/task.json",
                "//server/share/task.json",
                "CodexAutomation/runtime/task.json",
                "CodexAutomation/tasks/real_tasks/oversized.json",
            ):
                with self.subTest(item=item):
                    path, path_error = resolve_public_manifest_path(parent, item)
                    self.assertIsNone(path)
                    self.assertIsNotNone(path_error)

    def test_manifest_exact_size_limit_is_accepted(self) -> None:
        with synthetic_public_parent() as parent:
            exact = parent / "CodexAutomation" / "tasks" / "real_tasks" / "exact.json"
            data = (parent / "CodexAutomation" / "tasks" / "real_tasks" / "task.json").read_bytes()
            exact.write_bytes(data + (b" " * (MAX_PUBLIC_MANIFEST_BYTES - len(data))))
            subprocess.run(["git", "add", "--", "CodexAutomation/tasks/real_tasks/exact.json"], cwd=parent, check=True)
            subprocess.run(["git", "commit", "-m", "exact"], cwd=parent, capture_output=True, text=True, check=True)
            accepted, error = resolve_public_manifest_path(parent, "CodexAutomation/tasks/real_tasks/exact.json")
            self.assertIsNotNone(accepted)
            self.assertIsNone(error)
            authority, authority_error = load_public_manifest_authority(parent, accepted)
            self.assertIsNotNone(authority)
            self.assertIsNone(authority_error)
            self.assertEqual(authority.size, MAX_PUBLIC_MANIFEST_BYTES)

    def test_manifest_must_be_tracked_committed_and_match_head(self) -> None:
        with synthetic_public_parent(commit_manifest=False) as parent:
            path, error = resolve_public_manifest_path(parent, "CodexAutomation/tasks/real_tasks/task.json")
            self.assertIsNone(error)
            authority, authority_error = load_public_manifest_authority(parent, path)
            self.assertIsNone(authority)
            self.assertEqual(authority_error, "PUBLIC_REAL_TASK_MANIFEST_NOT_TRACKED")

        with synthetic_public_parent() as parent:
            path, error = resolve_public_manifest_path(parent, "CodexAutomation/tasks/real_tasks/task.json")
            self.assertIsNone(error)
            authority, authority_error = load_public_manifest_authority(parent, path)
            self.assertIsNotNone(authority)
            self.assertIsNone(authority_error)
            path.write_text(path.read_text(encoding="utf-8") + "\n", encoding="utf-8", newline="\n")
            changed, changed_error = load_public_manifest_authority(parent, path)
            self.assertIsNone(changed)
            self.assertEqual(changed_error, "PUBLIC_REAL_TASK_MANIFEST_NOT_COMMITTED")

    def test_manifest_changed_after_lock_blocks_before_production(self) -> None:
        with synthetic_public_parent() as parent:
            original = public_runner.load_public_manifest_authority
            calls = 0

            def wrapped(root: Path, manifest_path: Path):
                nonlocal calls
                calls += 1
                result = original(root, manifest_path)
                if calls == 1:
                    manifest_path.write_text(manifest_path.read_text(encoding="utf-8") + "\n", encoding="utf-8", newline="\n")
                return result

            with mock.patch("real_task_public_runner.load_public_manifest_authority", side_effect=wrapped), mock.patch("real_task_public_runner._execute_real_task_with_production_adapter") as execute_mock:
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, PUBLIC_REAL_TASK_BLOCKED)
        self.assertEqual(result.report["errorCode"], "PUBLIC_REAL_TASK_MANIFEST_CHANGED")
        execute_mock.assert_not_called()

    def test_manifest_same_bytes_replacement_after_lock_blocks_before_production(self) -> None:
        with synthetic_public_parent() as parent:
            original = public_runner.load_public_manifest_authority
            calls = 0

            def wrapped(root: Path, manifest_path: Path):
                nonlocal calls
                calls += 1
                result = original(root, manifest_path)
                if calls == 1:
                    data = manifest_path.read_bytes()
                    replacement = manifest_path.with_name("replacement.tmp")
                    replacement.write_bytes(data)
                    os.replace(replacement, manifest_path)
                return result

            with mock.patch("real_task_public_runner.load_public_manifest_authority", side_effect=wrapped), mock.patch("real_task_public_runner._execute_real_task_with_production_adapter") as execute_mock:
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, PUBLIC_REAL_TASK_BLOCKED)
        self.assertEqual(result.report["errorCode"], "PUBLIC_REAL_TASK_MANIFEST_CHANGED")
        execute_mock.assert_not_called()

    def test_manifest_symlink_or_metadata_failure_after_lock_blocks_before_production(self) -> None:
        with synthetic_public_parent() as parent:
            manifest = parent / "CodexAutomation" / "tasks" / "real_tasks" / "task.json"
            target = manifest.with_name("target.json")
            target.write_bytes(manifest.read_bytes())
            original = public_runner.load_public_manifest_authority
            calls = 0

            def wrapped(root: Path, manifest_path: Path):
                nonlocal calls
                calls += 1
                result = original(root, manifest_path)
                if calls == 1:
                    manifest.unlink()
                    try:
                        manifest.symlink_to(target)
                    except OSError:
                        manifest.write_bytes(target.read_bytes() + b"\n")
                return result

            with mock.patch("real_task_public_runner.load_public_manifest_authority", side_effect=wrapped), mock.patch("real_task_public_runner._execute_real_task_with_production_adapter") as execute_mock:
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, PUBLIC_REAL_TASK_BLOCKED)
        self.assertEqual(result.report["errorCode"], "PUBLIC_REAL_TASK_MANIFEST_CHANGED")
        execute_mock.assert_not_called()

        with synthetic_public_parent() as parent:
            original_identity = public_runner._public_file_identity
            calls = 0

            def failing_identity(status):
                nonlocal calls
                calls += 1
                if calls > 1:
                    raise PermissionError("metadata denied")
                return original_identity(status)

            with mock.patch("real_task_public_runner._public_file_identity", side_effect=failing_identity), mock.patch("real_task_public_runner._execute_real_task_with_production_adapter") as execute_mock:
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertNotEqual(result.exitCode, 0)
        execute_mock.assert_not_called()

    def test_manifest_broken_symlink_is_rejected_before_production(self) -> None:
        with synthetic_public_parent() as parent:
            manifest = parent / "CodexAutomation" / "tasks" / "real_tasks" / "task.json"
            manifest.unlink()
            try:
                manifest.symlink_to(manifest.with_name("missing.json"))
            except OSError:
                self.skipTest("Filesystem does not permit symlink creation.")
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter") as execute_mock:
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, PUBLIC_REAL_TASK_INVALID_INPUT)
        execute_mock.assert_not_called()


class PublicLockTests(unittest.TestCase):
    def test_win32_kernel_loader_uses_last_error_and_explicit_signatures(self) -> None:
        fake_kernel = FakeKernel32()
        with mock.patch.object(public_runner.os, "name", "nt"), mock.patch.object(public_runner.ctypes, "WinDLL", return_value=fake_kernel, create=True) as win_dll:
            public_runner._WIN32_KERNEL_API = None
            try:
                api = public_runner._get_win32_kernel_api()
            finally:
                public_runner._WIN32_KERNEL_API = None
        win_dll.assert_called_once_with("kernel32", use_last_error=True)
        self.assertIs(api.CreateFileW.restype, wintypes.HANDLE)
        self.assertEqual(api.CreateFileW.argtypes, [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE])
        self.assertIs(api.WriteFile.restype, wintypes.BOOL)
        self.assertEqual(api.WriteFile.argtypes, [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p])
        self.assertIs(api.ReadFile.restype, wintypes.BOOL)
        self.assertEqual(api.ReadFile.argtypes, [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p])
        self.assertIs(api.SetFilePointerEx.restype, wintypes.BOOL)
        self.assertEqual(api.SetFilePointerEx.argtypes, [wintypes.HANDLE, ctypes.c_longlong, ctypes.POINTER(ctypes.c_longlong), wintypes.DWORD])
        self.assertIs(api.FlushFileBuffers.restype, wintypes.BOOL)
        self.assertEqual(api.FlushFileBuffers.argtypes, [wintypes.HANDLE])
        self.assertIs(api.GetFileSizeEx.restype, wintypes.BOOL)
        self.assertEqual(api.GetFileSizeEx.argtypes, [wintypes.HANDLE, ctypes.POINTER(ctypes.c_longlong)])
        self.assertIs(api.GetFileInformationByHandle.restype, wintypes.BOOL)
        self.assertEqual(api.GetFileInformationByHandle.argtypes, [wintypes.HANDLE, ctypes.POINTER(public_runner.BY_HANDLE_FILE_INFORMATION)])
        self.assertIs(api.SetFileInformationByHandle.restype, wintypes.BOOL)
        self.assertEqual(api.SetFileInformationByHandle.argtypes, [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD])
        self.assertIs(api.CloseHandle.restype, wintypes.BOOL)
        self.assertEqual(api.CloseHandle.argtypes, [wintypes.HANDLE])

    def test_win32_high_handle_is_preserved_through_lock_helpers(self) -> None:
        if ctypes.sizeof(ctypes.c_void_p) != 8:
            self.skipTest("64-bit handle preservation requires a 64-bit pointer environment.")
        high_handle = 0x1234567887654321
        fake_kernel = FakeKernel32(high_handle)
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api):
            handle = public_runner._win_create_lock_handle(Path("C:/lock/public.lock"))
            public_runner._win_write_all(handle, b"abc")
            public_runner._win_read_all(handle)
            public_runner._win_file_identity(handle)
            public_runner._win_delete_by_handle(handle)
            public_runner._win_close_handle(handle)
        self.assertEqual(handle, high_handle)
        for function in (fake_kernel.WriteFile, fake_kernel.ReadFile, fake_kernel.GetFileInformationByHandle, fake_kernel.SetFileInformationByHandle, fake_kernel.CloseHandle):
            self.assertEqual(_handle_value(function.calls[0][0]), high_handle, function.name)

    def test_win32_createfile_invalid_handle_fails_without_close(self) -> None:
        fake_kernel = FakeKernel32(public_runner.INVALID_WIN32_HANDLE_VALUE)
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=5):
            with self.assertRaises(OSError) as raised:
                public_runner._win_create_lock_handle(Path("C:/lock/public.lock"))
        self.assertEqual(raised.exception.errno, 5)
        self.assertFalse(fake_kernel.CloseHandle.calls)

    def test_win32_partial_write_fails_before_flush(self) -> None:
        fake_kernel = FakeKernel32()

        def partial_write(_handle, _buffer, _length, written, _overlapped):
            written._obj.value = 1
            return True

        fake_kernel.WriteFile.impl = partial_write
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api):
            with self.assertRaises(OSError):
                public_runner._win_write_all(fake_kernel.handle, b"abcdef")
        self.assertFalse(fake_kernel.FlushFileBuffers.calls)

    def test_win32_delete_by_handle_failure_preserves_error_and_no_path_unlink(self) -> None:
        fake_kernel = FakeKernel32()
        fake_kernel.SetFileInformationByHandle.impl = lambda *_args: False
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=123), mock.patch("real_task_public_runner.os.unlink") as unlink_mock:
            with self.assertRaises(OSError) as raised:
                public_runner._win_delete_by_handle(fake_kernel.handle)
        self.assertEqual(raised.exception.errno, 123)
        unlink_mock.assert_not_called()

    def test_win32_close_once_and_close_failure(self) -> None:
        fake_kernel = FakeKernel32()
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        lock = _PublicRunLock(Path("C:/lock/public.lock"))
        lock._win_handle = fake_kernel.handle
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api):
            lock._close_handle()
            lock._close_handle()
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)

        failing_kernel = FakeKernel32()
        failing_kernel.CloseHandle.impl = lambda *_args: False
        failing_api = public_runner._configure_win32_kernel_api(failing_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=failing_api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=456):
            with self.assertRaises(OSError) as raised:
                public_runner._win_close_handle(failing_kernel.handle)
        self.assertEqual(raised.exception.errno, 456)

    def test_lock_acquire_flush_failure_closes_handle_once_and_returns_no_owner(self) -> None:
        fake_kernel = FakeKernel32()
        fake_kernel.FlushFileBuffers.impl = lambda *_args: False
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            lock = _PublicRunLock(Path(temp) / "public_generic_real_task_run.lock")
            with mock.patch.object(public_runner.os, "name", "nt"), mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=77) as last_error_mock, mock.patch("real_task_public_runner.os.unlink") as unlink_mock:
                acquire_error = lock.acquire("public-1", "CodexAutomation/tasks/real_tasks/task.json", "a" * 64)
        self.assertEqual(acquire_error, "Public real-task lock could not be acquired: OSError.")
        self.assertIsNone(lock._win_handle)
        self.assertIsNone(lock.identity)
        self.assertFalse(lock._released)
        self.assertFalse(lock._release_failed)
        self.assertTrue(lock._win_close_attempted)
        self.assertTrue(lock._win_close_succeeded)
        last_error_mock.assert_called_once()
        self.assertEqual(len(fake_kernel.CreateFileW.calls), 1)
        self.assertEqual(len(fake_kernel.WriteFile.calls), 1)
        self.assertEqual(len(fake_kernel.FlushFileBuffers.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        self.assertEqual(_handle_value(fake_kernel.CloseHandle.calls[0][0]), fake_kernel.handle)
        self.assertFalse(fake_kernel.SetFilePointerEx.calls)
        self.assertFalse(fake_kernel.ReadFile.calls)
        self.assertFalse(fake_kernel.GetFileInformationByHandle.calls)
        self.assertFalse(fake_kernel.SetFileInformationByHandle.calls)
        unlink_mock.assert_not_called()

    def test_win32_release_delete_fails_close_succeeds_closes_once(self) -> None:
        fake_kernel = FakeKernel32()
        fake_kernel.SetFileInformationByHandle.impl = lambda *_args: False
        lock, api = _configured_fake_win_lock(fake_kernel)
        with mock.patch.object(public_runner.os, "name", "nt"), mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=123), mock.patch("real_task_public_runner.os.unlink") as unlink_mock:
            release_error = lock.release()
            second_error = lock.release()
        self.assertIsNotNone(release_error)
        self.assertEqual(release_error[0], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
        self.assertIn("delete-by-handle failed: 123", release_error[1])
        self.assertIsNone(lock._win_handle)
        self.assertFalse(lock._released)
        self.assertTrue(lock._release_failed)
        self.assertTrue(lock._win_close_attempted)
        self.assertTrue(lock._win_close_succeeded)
        self.assertEqual(len(fake_kernel.SetFileInformationByHandle.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        self.assertEqual(second_error[0], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
        self.assertEqual(len(fake_kernel.SetFileInformationByHandle.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        unlink_mock.assert_not_called()

    def test_win32_release_delete_and_close_fail_preserves_delete_primary(self) -> None:
        fake_kernel = FakeKernel32()
        fake_kernel.SetFileInformationByHandle.impl = lambda *_args: False
        fake_kernel.CloseHandle.impl = lambda *_args: False
        lock, api = _configured_fake_win_lock(fake_kernel)

        def last_error() -> int:
            if fake_kernel.CloseHandle.calls:
                return 456
            return 123

        with mock.patch.object(public_runner.os, "name", "nt"), mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", side_effect=last_error), mock.patch("real_task_public_runner.os.unlink") as unlink_mock:
            release_error = lock.release()
            second_error = lock.release()
        self.assertIsNotNone(release_error)
        self.assertEqual(release_error[0], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
        self.assertIn("delete-by-handle failed: 123", release_error[1])
        self.assertIn("secondary CloseHandle failed: 456", release_error[1])
        self.assertIsNone(lock._win_handle)
        self.assertFalse(lock._released)
        self.assertTrue(lock._release_failed)
        self.assertTrue(lock._win_close_attempted)
        self.assertFalse(lock._win_close_succeeded)
        self.assertEqual(len(fake_kernel.SetFileInformationByHandle.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        self.assertEqual(second_error[0], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
        self.assertEqual(len(fake_kernel.SetFileInformationByHandle.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        unlink_mock.assert_not_called()

    def test_win32_release_delete_succeeds_close_fails_blocks_pass(self) -> None:
        fake_kernel = FakeKernel32()
        fake_kernel.CloseHandle.impl = lambda *_args: False
        lock, api = _configured_fake_win_lock(fake_kernel)
        with mock.patch.object(public_runner.os, "name", "nt"), mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=456), mock.patch("real_task_public_runner.os.unlink") as unlink_mock:
            release_error = lock.release()
            second_error = lock.release()
        self.assertIsNotNone(release_error)
        self.assertEqual(release_error[0], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
        self.assertIn("CloseHandle failed: 456", release_error[1])
        self.assertIsNone(lock._win_handle)
        self.assertFalse(lock._released)
        self.assertTrue(lock._release_failed)
        self.assertTrue(lock._win_close_attempted)
        self.assertFalse(lock._win_close_succeeded)
        self.assertEqual(len(fake_kernel.SetFileInformationByHandle.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        self.assertEqual(second_error[0], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
        self.assertEqual(len(fake_kernel.SetFileInformationByHandle.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        unlink_mock.assert_not_called()

    def test_win32_release_success_second_release_does_not_double_close(self) -> None:
        fake_kernel = FakeKernel32()
        lock, api = _configured_fake_win_lock(fake_kernel)
        with mock.patch.object(public_runner.os, "name", "nt"), mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner.os.unlink") as unlink_mock:
            release_error = lock.release()
            second_error = lock.release()
        self.assertIsNone(release_error)
        self.assertIsNotNone(second_error)
        self.assertEqual(second_error[0], "PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST")
        self.assertTrue(lock._released)
        self.assertFalse(lock._release_failed)
        self.assertTrue(lock._win_close_attempted)
        self.assertTrue(lock._win_close_succeeded)
        self.assertEqual(len(fake_kernel.SetFileInformationByHandle.calls), 1)
        self.assertEqual(len(fake_kernel.CloseHandle.calls), 1)
        unlink_mock.assert_not_called()

    def test_win32_flush_set_pointer_and_read_failures_preserve_errors(self) -> None:
        fake_kernel = FakeKernel32()
        fake_kernel.FlushFileBuffers.impl = lambda *_args: False
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=77):
            with self.assertRaises(OSError) as raised:
                public_runner._win_flush(fake_kernel.handle)
        self.assertEqual(raised.exception.errno, 77)

        fake_kernel = FakeKernel32()
        fake_kernel.SetFilePointerEx.impl = lambda *_args: False
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=88):
            with self.assertRaises(OSError) as raised:
                public_runner._win_read_all(fake_kernel.handle)
        self.assertEqual(raised.exception.errno, 88)
        self.assertFalse(fake_kernel.ReadFile.calls)

        fake_kernel = FakeKernel32()
        fake_kernel.ReadFile.impl = lambda *_args: False
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api), mock.patch("real_task_public_runner._get_win32_last_error", return_value=99):
            with self.assertRaises(OSError) as raised:
                public_runner._win_read_all(fake_kernel.handle)
        self.assertEqual(raised.exception.errno, 99)

    def test_file_disposition_info_layout_and_info_class(self) -> None:
        fake_kernel = FakeKernel32()
        api = public_runner._configure_win32_kernel_api(fake_kernel)
        self.assertEqual(ctypes.sizeof(public_runner.FILE_DISPOSITION_INFO), 1)
        self.assertEqual(getattr(public_runner.FILE_DISPOSITION_INFO, "DeleteFile").offset, 0)
        with mock.patch("real_task_public_runner._get_win32_kernel_api", return_value=api):
            public_runner._win_delete_by_handle(fake_kernel.handle)
        call = fake_kernel.SetFileInformationByHandle.calls[0]
        self.assertEqual(call[1], 4)
        self.assertEqual(call[3].value, 1)

    def test_lock_metadata_owner_release_and_foreign_release(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            lock_path = Path(temp) / "public_generic_real_task_run.lock"
            lock = _PublicRunLock(lock_path)
            self.assertIsNone(lock.acquire("public-1", "CodexAutomation/tasks/real_tasks/task.json", "a" * 64))
            if os.name == "nt":
                self.assertIsNotNone(lock._win_handle)
                metadata = public_runner._win_read_lock_metadata(lock._win_handle)
            else:
                metadata = json.loads(lock_path.read_text(encoding="utf-8"))
            self.assertEqual(metadata["lockVersion"], 1)
            self.assertEqual(metadata["publicRunId"], "public-1")
            self.assertEqual(metadata["manifestSha256"], "a" * 64)
            self.assertNotIn("ownerToken", metadata)
            self.assertTrue(lock._win_handle is not None or lock._fd is not None)
            second = _PublicRunLock(lock_path)
            self.assertIsNotNone(second.acquire("public-2", "CodexAutomation/tasks/real_tasks/task.json", "a" * 64))
            metadata["publicRunId"] = "foreign"
            try:
                lock_path.write_text(json.dumps(metadata), encoding="utf-8", newline="\n")
                metadata_replaced = True
            except OSError:
                metadata_replaced = False
            release_error = lock.release()
            if metadata_replaced:
                self.assertIsNotNone(release_error)
                self.assertEqual(release_error[0], "PUBLIC_REAL_TASK_LOCK_OWNERSHIP_LOST")
                self.assertTrue(lock_path.exists())
            else:
                self.assertIsNone(release_error)
                self.assertFalse(lock_path.exists())

    def test_lock_exact_path_constant(self) -> None:
        self.assertEqual(PUBLIC_LOCK_PATH.as_posix(), "CodexAutomation/runtime/locks/public_generic_real_task_run.lock")

    def test_windows_actual_lock_integration_or_explicit_skip(self) -> None:
        if os.name != "nt":
            self.skipTest("Windows Kernel32 lock integration is Windows-only.")
        with tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime") as temp:
            lock_path = Path(temp) / "public_generic_real_task_run.lock"
            lock = _PublicRunLock(lock_path)
            self.assertIsNone(lock.acquire("public-1", "CodexAutomation/tasks/real_tasks/task.json", "a" * 64))
            self.assertIsNotNone(lock._win_handle)
            metadata = public_runner._win_read_lock_metadata(lock._win_handle)
            self.assertEqual(metadata["publicRunId"], "public-1")
            self.assertIsNotNone(lock.identity)
            second = _PublicRunLock(lock_path)
            self.assertIsNotNone(second.acquire("public-2", "CodexAutomation/tasks/real_tasks/task.json", "a" * 64))
            self.assertIsNone(lock.release())
            self.assertIsNone(lock._win_handle)
            self.assertFalse(lock_path.exists())


class PublicReportAndAdapterTests(unittest.TestCase):
    def test_public_report_is_persisted_and_summary_ready(self) -> None:
        with synthetic_public_parent() as parent:
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution(parent)) as execute_mock:
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
            execute_mock.assert_called_once()
            self.assertEqual(result.exitCode, 0, result.report)
            report_path = Path(result.reportPath)
            self.assertTrue(report_path.exists())
            receipt_path = report_path.with_name("PUBLIC_REAL_TASK_RUN_REPORT.receipt.json")
            self.assertTrue(receipt_path.exists())
            schema_errors = validate_schema_instance(read_json(report_path), read_json(ROOT / "CodexAutomation" / "schemas" / "public_real_task_run.schema.json"))
            self.assertFalse(schema_errors, schema_errors)
            receipt = read_json(receipt_path)
            self.assertEqual(receipt["automationStage"], "BOOTSTRAP-03B-3A")
            self.assertEqual(receipt["publicRunId"], result.report["publicRunId"])
            self.assertEqual(receipt["reportRelativePath"], result.reportPath.replace(str(parent / "CodexAutomation" / "runtime") + os.sep, "").replace("\\", "/"))
            self.assertEqual(receipt["reportVersion"], 1)
            self.assertFalse(result.report["eligibleForApply"])
            self.assertTrue(result.report["lockAcquired"])
            self.assertTrue(result.report["lockReleased"])
            self.assertIsNotNone(result.report["productionFinalReportRelativePath"])
            self.assertIsNotNone(result.report["bundleRelativePath"])

    def test_public_pass_is_overridden_when_lock_release_fails(self) -> None:
        original_release = _PublicRunLock.release
        release_calls: list[_PublicRunLock] = []

        def release_fails(lock: _PublicRunLock):
            release_calls.append(lock)
            self.assertIsNone(original_release(lock))
            return ("PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED", "synthetic release failure")

        with synthetic_public_parent() as parent:
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution(parent)) as execute_mock, mock.patch.object(_PublicRunLock, "release", autospec=True, side_effect=release_fails) as release_mock:
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
            execute_mock.assert_called_once()
            self.assertEqual(release_mock.call_count, 1)
            self.assertEqual(len(release_calls), 1)
            self.assertEqual(result.exitCode, 1)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["finalVerdict"], "BLOCKED")
            self.assertEqual(result.report["finalState"], "BLOCKED")
            self.assertTrue(result.report["complete"])
            self.assertTrue(result.report["lockAcquired"])
            self.assertFalse(result.report["lockReleased"])
            self.assertFalse(result.report["eligibleForApply"])
            self.assertEqual(result.report["errorCode"], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
            self.assertIn("synthetic release failure", result.report["errorMessage"])
            self.assertIsNotNone(result.bundleManifestPath)
            self.assertTrue(Path(result.bundleManifestPath).exists())
            self.assertFalse(read_json(Path(result.bundleManifestPath))["eligibleForApply"])
            self.assertEqual(read_json(Path(result.bundleManifestPath))["finalTaskVerdict"], "PASS")

            report_path = Path(result.reportPath)
            self.assertTrue(report_path.exists())
            receipt_path = report_path.with_name("PUBLIC_REAL_TASK_RUN_REPORT.receipt.json")
            self.assertTrue(receipt_path.exists())
            schema_errors = validate_schema_instance(read_json(report_path), read_json(ROOT / "CodexAutomation" / "schemas" / "public_real_task_run.schema.json"))
            self.assertFalse(schema_errors, schema_errors)
            semantic_errors = public_runner.validate_public_real_task_report(read_json(report_path))
            self.assertFalse(semantic_errors, semantic_errors)
            trusted = public_runner.read_trusted_public_run_report(ROOT / "CodexAutomation", parent / "CodexAutomation" / "runtime", report_path.parent, result.report["publicRunId"])
            self.assertEqual(trusted["finalVerdict"], "BLOCKED")
            self.assertEqual(trusted["exitCode"], 1)
            self.assertEqual(trusted["errorCode"], "PUBLIC_REAL_TASK_LOCK_RELEASE_FAILED")
            self.assertFalse(trusted["lockReleased"])
            self.assertNotEqual(trusted["finalVerdict"], "PASS")

    def test_public_receipt_tampering_is_rejected_by_trusted_reader(self) -> None:
        cases = [
            ("wrong_publicRunId", lambda receipt, report: receipt.__setitem__("publicRunId", "other-public-run")),
            ("wrong_stage", lambda receipt, report: receipt.__setitem__("automationStage", "OTHER-STAGE")),
            ("wrong_path", lambda receipt, report: receipt.__setitem__("reportRelativePath", "outside.json")),
            ("absolute_path", lambda receipt, report: receipt.__setitem__("reportRelativePath", str(Path(report).resolve()))),
            ("traversal_path", lambda receipt, report: receipt.__setitem__("reportRelativePath", "../PUBLIC_REAL_TASK_RUN_REPORT.json")),
            ("wrong_hash", lambda receipt, report: receipt.__setitem__("reportSha256", "0" * 64)),
            ("wrong_size", lambda receipt, report: receipt.__setitem__("reportByteSize", int(receipt["reportByteSize"]) + 1)),
            ("wrong_schema", lambda receipt, report: receipt.__setitem__("reportSchemaName", "other.schema.json")),
            ("wrong_report_version", lambda receipt, report: receipt.__setitem__("reportVersion", 2)),
        ]
        for name, mutate in cases:
            with self.subTest(name=name), synthetic_public_parent() as parent:
                with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution(parent)):
                    result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
                self.assertEqual(result.exitCode, 0, result.report)
                report_path = Path(result.reportPath)
                receipt_path = report_path.with_name("PUBLIC_REAL_TASK_RUN_REPORT.receipt.json")
                receipt = read_json(receipt_path)
                mutate(receipt, report_path)
                receipt_path.write_text(json.dumps(receipt, indent=2), encoding="utf-8", newline="\n")
                with self.assertRaises(Exception):
                    public_runner.read_trusted_public_run_report(ROOT / "CodexAutomation", parent / "CodexAutomation" / "runtime", report_path.parent, result.report["publicRunId"])

        with synthetic_public_parent() as parent:
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution(parent)):
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
            report_path = Path(result.reportPath)
            report_path.write_text(report_path.read_text(encoding="utf-8") + "\n", encoding="utf-8", newline="\n")
            with self.assertRaises(Exception):
                public_runner.read_trusted_public_run_report(ROOT / "CodexAutomation", parent / "CodexAutomation" / "runtime", report_path.parent, result.report["publicRunId"])

        with synthetic_public_parent() as parent:
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution(parent)):
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
            receipt_path = Path(result.reportPath).with_name("PUBLIC_REAL_TASK_RUN_REPORT.receipt.json")
            receipt_path.write_text('{"receiptVersion":1,"receiptVersion":1}\n', encoding="utf-8", newline="\n")
            with self.assertRaises(Exception):
                public_runner.read_trusted_public_run_report(ROOT / "CodexAutomation", parent / "CodexAutomation" / "runtime", Path(result.reportPath).parent, result.report["publicRunId"])

        with synthetic_public_parent() as parent:
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution(parent)):
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
            Path(result.reportPath).with_name("PUBLIC_REAL_TASK_RUN_REPORT.receipt.json").unlink()
            with self.assertRaises(Exception):
                public_runner.read_trusted_public_run_report(ROOT / "CodexAutomation", parent / "CodexAutomation" / "runtime", Path(result.reportPath).parent, result.report["publicRunId"])

    def test_public_call_graph_has_no_test_adapter_edge(self) -> None:
        self.assertNotIn("_execute_real_task_with_test_adapter", inspect.getsource(public_runner))
        self.assertNotIn("_execute_real_task_with_test_adapter", inspect.getsource(_execute_real_task_with_production_adapter))
        import real_task_runner

        self.assertFalse(hasattr(real_task_runner, "execute_public_real_task"))

    def test_public_pass_requires_persisted_production_artifacts(self) -> None:
        with synthetic_public_parent() as parent:
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=fake_execution_without_artifacts()):
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, 1)
        self.assertEqual(result.report["finalVerdict"], "BLOCKED")
        self.assertEqual(result.report["errorCode"], "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")

    def test_public_production_authority_negative_matrix(self) -> None:
        def final_report_path(result: RealTaskExecutionResult) -> Path:
            return Path(str(result.reportPath))

        def bundle_path(result: RealTaskExecutionResult) -> Path:
            return Path(str(result.bundleManifestPath))

        cases = [
            ("wrong_production_run_id", lambda result: _json_patch(final_report_path(result), {"orchestrationRunId": "other"})),
            ("wrong_task_id", lambda result: _json_patch(final_report_path(result), {"taskId": "OTHER-TASK"})),
            ("wrong_manifest_hash", lambda result: _json_patch(final_report_path(result), {"manifestSha256": "0" * 64})),
            ("report_outside_root", lambda result: object.__setattr__(result, "reportPath", str(Path(tempfile.gettempdir()) / "FINAL_REAL_TASK_REPORT.json"))),
            ("bundle_outside_result_bundle", lambda result: object.__setattr__(result, "bundleManifestPath", str(bundle_path(result).parent.parent / "RESULT_MANIFEST.json"))),
            ("wrong_bundle_hash", lambda result: _json_patch(final_report_path(result), {"resultBundleManifestHash": "0" * 64})),
            ("payload_missing", lambda result: (bundle_path(result).parent / "files" / "Assets" / "Generated" / "planet_task_output.txt").unlink()),
            ("payload_size_mismatch", lambda result: _json_patch(bundle_path(result).parent / "SHA256SUMS.json", {"totalBytes": 999})),
            ("payload_hash_mismatch", lambda result: (bundle_path(result).parent / "files" / "Assets" / "Generated" / "planet_task_output.txt").write_text("tampered\n", encoding="utf-8", newline="\n")),
            ("duplicate_bundle_entry", lambda result: _duplicate_sums_entry(bundle_path(result).parent / "SHA256SUMS.json")),
            ("bundle_entry_traversal", lambda result: _mutate_first_sums_entry(bundle_path(result).parent / "SHA256SUMS.json", "../evil.txt")),
            ("bundle_entry_absolute", lambda result: _mutate_first_sums_entry(bundle_path(result).parent / "SHA256SUMS.json", "C:/evil.txt")),
            ("eligible_for_apply", lambda result: _json_patch_bundle_and_rebind(bundle_path(result), {"eligibleForApply": True})),
            ("bundle_non_pass", lambda result: _json_patch_bundle_and_rebind(bundle_path(result), {"finalTaskVerdict": "FAIL"})),
            ("auditor_not_approved", lambda result: _json_patch(final_report_path(result), {"auditorApproved": False})),
            ("auditor_workspace_changed", lambda result: _json_patch(final_report_path(result), {"workspaceUnchangedAfterAuditor": False})),
        ]
        for name, mutate in cases:
            with self.subTest(name=name), synthetic_public_parent() as parent:
                execution = fake_execution(parent)
                if name == "report_outside_root":
                    outside = Path(tempfile.gettempdir()) / "FINAL_REAL_TASK_REPORT.json"
                    outside.write_text(json.dumps(execution.report), encoding="utf-8", newline="\n")
                mutate(execution)
                with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=execution):
                    result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
                self.assertEqual(result.exitCode, 1, result.report)
                self.assertEqual(result.report["finalVerdict"], "BLOCKED")
                self.assertEqual(result.report["errorCode"], "PUBLIC_REAL_TASK_PRODUCTION_EVIDENCE_INVALID")
                self.assertFalse(result.report["eligibleForApply"])

    def test_rate_limited_public_result_has_no_retry_exit_four(self) -> None:
        rate_limited = RealTaskExecutionResult(
            "RATE_LIMITED",
            "RATE_LIMITED",
            {
                "orchestrationRunId": "real_task_rate",
                "taskId": "TASK-PUBLIC-001",
                "errorCode": "REAL_TASK_RATE_LIMITED",
                "errorMessage": "rate limited",
                "invocationsUsed": 1,
                "repairsUsed": 0,
                "auditorRan": False,
                "auditorApproved": False,
                "bundleIntegrityValid": False,
                "modelInvocationStarted": True,
                "sandboxStarted": True,
            },
            None,
            None,
        )
        with synthetic_public_parent() as parent:
            with mock.patch("real_task_public_runner._execute_real_task_with_production_adapter", return_value=rate_limited):
                result = run_public_real_task(parent, ROOT / "CodexAutomation", enabled_config(), "CodexAutomation/tasks/real_tasks/task.json")
        self.assertEqual(result.exitCode, 4)
        self.assertEqual(result.report["invocationsUsed"], 1)
        self.assertEqual(result.report["repairsUsed"], 0)


def enabled_config(*, allow_execution: bool = True, public_enabled: bool = True) -> dict:
    config = json.loads(json.dumps(CONFIG))
    config["realTasks"]["allowExecution"] = allow_execution
    config["realTaskExecutionPolicy"]["publicGenericRealTaskRunEnabled"] = public_enabled
    return config


def fake_execution(parent: Path) -> RealTaskExecutionResult:
    run_id = "real_task_public_fake"
    task_id = "TASK-PUBLIC-001"
    run_dir = parent / "CodexAutomation" / "runtime" / "real_task_runs" / run_id
    bundle_dir = run_dir / "result_bundle"
    files_dir = bundle_dir / "files" / "Assets" / "Generated"
    files_dir.mkdir(parents=True, exist_ok=True)
    payload = files_dir / "planet_task_output.txt"
    payload.write_text("complete\n", encoding="utf-8", newline="\n")
    patch = bundle_dir / "changes.patch"
    patch.write_text("--- Assets/Generated/planet_task_output.txt\n+++ Assets/Generated/planet_task_output.txt\n", encoding="utf-8", newline="\n")
    entries = [
        {"relativePath": "changes.patch", "size": patch.stat().st_size, "sha256": file_sha256(patch)},
        {"relativePath": "files/Assets/Generated/planet_task_output.txt", "size": payload.stat().st_size, "sha256": file_sha256(payload)},
    ]
    sums = {
        "reportVersion": 1,
        "entries": entries,
        "totalFiles": len(entries),
        "totalBytes": sum(item["size"] for item in entries),
        "aggregateHash": canonical_sha256(entries),
        "excludedPaths": ["RESULT_MANIFEST.json", "SHA256SUMS.json"],
    }
    write_json(bundle_dir / "SHA256SUMS.json", sums)
    manifest_sha = file_sha256(parent / "CodexAutomation" / "tasks" / "real_tasks" / "task.json")
    bundle_manifest = {
        "reportVersion": 1,
        "orchestrationRunId": run_id,
        "taskId": task_id,
        "stage": "BOOTSTRAP-03B-2C",
        "createdAt": "2026-01-01T00:00:00Z",
        "finalTaskVerdict": "PASS",
        "eligibleForApply": False,
        "parentRepositoryIdentity": {"repositoryRoot": str(parent), "parentHead": "head"},
        "parentHead": "head",
        "parentGitSnapshotHash": "a" * 64,
        "manifestSha256": manifest_sha,
        "effectivePolicyHash": "b" * 64,
        "executionContextHash": "c" * 64,
        "foundationFinalReportHash": "d" * 64,
        "finalChangeAnalysisReportHash": "e" * 64,
        "finalValidationReportHash": "f" * 64,
        "auditReportHash": "1" * 64,
        "changedPathCount": 1,
        "deletedPathCount": 0,
        "copiedFileCount": 1,
        "totalBundleBytes": sum(item["size"] for item in entries),
        "patchSha256": file_sha256(patch),
        "entries": [
            {"relativePath": "files/Assets/Generated/planet_task_output.txt", "artifactType": "changed_file", "sourceTaskPath": "Assets/Generated/planet_task_output.txt", "size": payload.stat().st_size, "sha256": file_sha256(payload)},
            {"relativePath": "changes.patch", "artifactType": "unified_patch", "sourceTaskPath": None, "size": patch.stat().st_size, "sha256": file_sha256(patch)},
        ],
        "sha256SumsSha256": canonical_sha256(sums),
        "sha256SumsSize": (bundle_dir / "SHA256SUMS.json").stat().st_size,
        "sha256SumsExcludedPaths": ["RESULT_MANIFEST.json", "SHA256SUMS.json"],
        "coveredFileCount": sums["totalFiles"],
        "coveredTotalBytes": sums["totalBytes"],
        "complete": True,
        "errorCode": None,
        "errorMessage": None,
    }
    write_json(bundle_dir / "RESULT_MANIFEST.json", bundle_manifest)
    final_report = {
        "reportVersion": 1,
        "orchestrationRunId": run_id,
        "taskId": task_id,
        "stage": "BOOTSTRAP-03B-2C",
        "stateHistory": ["PENDING", "COMPLETED"],
        "executionContextHash": "c" * 64,
        "manifestSha256": manifest_sha,
        "effectivePolicyHash": "b" * 64,
        "foundationRunId": "foundation",
        "foundationFinalReportHash": "d" * 64,
        "sandboxProbeReportHash": "2" * 64,
        "implementerInvocationReportHash": "3" * 64,
        "diagnosticReportHash": "4" * 64,
        "repairDecisionReportHash": "5" * 64,
        "repairerInvocationReportHash": None,
        "finalChangeAnalysisReportHash": "e" * 64,
        "finalValidationReportHash": "f" * 64,
        "auditorInvocationReportHash": "6" * 64,
        "auditReportHash": "1" * 64,
        "resultBundleManifestHash": canonical_sha256(bundle_manifest),
        "invocationBudget": {"maxRoleInvocations": 3, "invocationsUsed": 2},
        "invocationsUsed": 2,
        "repairBudget": {"maxRepairAttempts": 1, "repairsUsed": 0},
        "repairsUsed": 0,
        "implementerCompleted": True,
        "diagnosticVerdict": "PASS",
        "repairAttempted": False,
        "finalHostValidationPassed": True,
        "auditorRan": True,
        "auditorApproved": True,
        "workspaceUnchangedAfterAuditor": True,
        "parentGitChanged": False,
        "parentSourceChanged": False,
        "serviceFilesValid": True,
        "agentsDirectoryValid": True,
        "isolatedGitValid": True,
        "bundleIntegrityValid": True,
        "codexInvocationCount": 2,
        "modelInvocationStarted": True,
        "sandboxStarted": True,
        "unityStarted": False,
        "networkUsed": False,
        "packageInstallUsed": False,
        "automaticRetryUsed": False,
        "modelDowngradeUsed": False,
        "creditUsageTriggered": False,
        "durationSeconds": 1.0,
        "finalState": "COMPLETED",
        "finalVerdict": "PASS",
        "errorCode": None,
        "errorMessage": None,
        "warnings": [],
    }
    write_json(run_dir / "FINAL_REAL_TASK_REPORT.json", final_report)
    return RealTaskExecutionResult(
        "PASS",
        "COMPLETED",
        dict(final_report),
        str(run_dir / "FINAL_REAL_TASK_REPORT.json"),
        str(bundle_dir / "RESULT_MANIFEST.json"),
    )


def fake_execution_without_artifacts() -> RealTaskExecutionResult:
    return RealTaskExecutionResult(
        "PASS",
        "COMPLETED",
        {
            "orchestrationRunId": "real_task_public_fake",
            "taskId": "TASK-PUBLIC-001",
            "errorCode": None,
            "errorMessage": None,
            "invocationsUsed": 2,
            "repairsUsed": 0,
            "sandboxProbeReportHash": "a" * 64,
            "sandboxStarted": True,
            "modelInvocationStarted": True,
            "auditorRan": True,
            "auditorApproved": True,
            "workspaceUnchangedAfterAuditor": True,
            "bundleIntegrityValid": True,
        },
        None,
        None,
    )


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8", newline="\n")


def _json_patch(path: Path, patch: dict) -> None:
    payload = read_json(path)
    payload.update(patch)
    write_json(path, payload)


def _json_patch_bundle_and_rebind(path: Path, patch: dict) -> None:
    _json_patch(path, patch)
    final_report = path.parent.parent / "FINAL_REAL_TASK_REPORT.json"
    report = read_json(final_report)
    report["resultBundleManifestHash"] = canonical_sha256(read_json(path))
    write_json(final_report, report)


def _duplicate_sums_entry(path: Path) -> None:
    payload = read_json(path)
    payload["entries"].append(dict(payload["entries"][0]))
    write_json(path, payload)
    _rebind_bundle_sums(path)


def _mutate_first_sums_entry(path: Path, relative_path: str) -> None:
    payload = read_json(path)
    payload["entries"][0]["relativePath"] = relative_path
    write_json(path, payload)
    _rebind_bundle_sums(path)


def _rebind_bundle_sums(path: Path) -> None:
    bundle = path.with_name("RESULT_MANIFEST.json")
    payload = read_json(bundle)
    payload["sha256SumsSha256"] = canonical_sha256(read_json(path))
    payload["sha256SumsSize"] = path.stat().st_size
    write_json(bundle, payload)
    final_report = bundle.parent.parent / "FINAL_REAL_TASK_REPORT.json"
    if final_report.exists():
        report = read_json(final_report)
        report["resultBundleManifestHash"] = canonical_sha256(read_json(bundle))
        write_json(final_report, report)


class synthetic_public_parent:
    def __init__(self, *, commit_manifest: bool = True) -> None:
        self.commit_manifest = commit_manifest
        self.temp: tempfile.TemporaryDirectory[str] | None = None

    def __enter__(self) -> Path:
        self.temp = tempfile.TemporaryDirectory()
        root = Path(self.temp.name)
        subprocess.run(["git", "init"], cwd=root, capture_output=True, text=True, check=True)
        subprocess.run(["git", "config", "user.email", "test@example.invalid"], cwd=root, check=True)
        subprocess.run(["git", "config", "user.name", "Test User"], cwd=root, check=True)
        (root / ".gitignore").write_text("CodexAutomation/runtime/\n", encoding="utf-8", newline="\n")
        source = root / "Assets" / "Generated"
        source.mkdir(parents=True)
        (source / "seed.txt").write_text("seed\n", encoding="utf-8", newline="\n")
        public_root = root / "CodexAutomation" / "tasks" / "real_tasks"
        public_root.mkdir(parents=True)
        manifest = {
            "schemaVersion": 1,
            "taskId": "TASK-PUBLIC-001",
            "title": "Public task",
            "objective": "Create a deterministic generated task output.",
            "taskType": "isolated_code_change",
            "sourcePaths": ["Assets/Generated"],
            "allowedWritePaths": ["Assets/Generated/planet_task_output.txt"],
            "allowedDeletePaths": [],
            "expectedOutputs": [{"path": "Assets/Generated/planet_task_output.txt", "kind": "file", "required": True}],
            "validationPlan": [
                {"id": "changed", "type": "changed_paths_exact", "paths": ["Assets/Generated/planet_task_output.txt"]},
                {"id": "contains", "type": "text_contains", "path": "Assets/Generated/planet_task_output.txt", "text": "complete"},
                {"id": "no-conflicts", "type": "no_conflict_markers", "paths": ["Assets/Generated/planet_task_output.txt"]},
            ],
            "completionCriteria": ["Generated output exists."],
            "roleBudget": {"maxInvocations": 3},
            "repairPolicy": {"maxAttempts": 1},
            "limits": {"maxChangedFiles": 10, "maxChangedBytes": 1048576, "maxSingleChangedFileBytes": 1048576},
            "metadata": {},
        }
        (public_root / "task.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8", newline="\n")
        subprocess.run(["git", "add", "--", ".gitignore", "Assets/Generated/seed.txt"], cwd=root, check=True)
        if self.commit_manifest:
            subprocess.run(["git", "add", "--", "CodexAutomation/tasks/real_tasks/task.json"], cwd=root, check=True)
        subprocess.run(["git", "commit", "-m", "seed"], cwd=root, capture_output=True, text=True, check=True)
        return root

    def __exit__(self, exc_type, exc, tb) -> None:
        assert self.temp is not None
        self.temp.cleanup()


if __name__ == "__main__":
    unittest.main()
