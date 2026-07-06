from __future__ import annotations

import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

import file_utils  # noqa: E402
from file_utils import read_json, write_json_atomic  # noqa: E402
from real_role_runner import _write_probe_report  # noqa: E402


class AtomicJsonLongPathTests(unittest.TestCase):
    def test_deep_json_write_creates_parent_and_round_trips_payload(self) -> None:
        with _runtime_root() as root:
            final_path = _deep_json_path(root, "direct_writer", "FINAL_GENERIC_REPORT.json")
            payload = {"status": "PASS", "message": "ok", "items": [1, 2, 3], "nonAscii": "planet"}
            self.assertFalse(final_path.parent.exists())

            write_json_atomic(final_path, payload)

            if os.name == "nt":
                self.assertGreater(len(str(final_path.resolve(strict=False))), 260)
            self.assertEqual(read_json(final_path), payload)
            self.assertFalse(list(final_path.parent.glob(f".{final_path.name}.*.tmp")))

    def test_shallow_write_replaces_existing_target_and_preserves_json_shape(self) -> None:
        with _runtime_root() as root:
            final_path = root / "shallow.json"
            write_json_atomic(final_path, {"old": True})

            write_json_atomic(final_path, {"new": True, "items": ["a"]})

            self.assertEqual(read_json(final_path), {"new": True, "items": ["a"]})
            text = final_path.read_text(encoding="utf-8")
            self.assertTrue(text.endswith("\n"))
            self.assertIn("  ", text)

    def test_replace_uses_compatible_filesystem_paths_for_source_and_destination(self) -> None:
        with _runtime_root() as root:
            final_path = _deep_json_path(root, "replace_args", "ARGUMENT_REPORT.json")
            calls: list[tuple[str, str]] = []

            def fail_replace(source: str, destination: str) -> None:
                calls.append((source, destination))
                raise OSError("replace denied")

            with mock.patch("file_utils.os.replace", side_effect=fail_replace):
                with self.assertRaises(OSError):
                    write_json_atomic(final_path, {"status": "replace-fails"})

            self.assertEqual(len(calls), 1)
            source, destination = calls[0]
            self.assertEqual(destination, file_utils._fs_path(final_path))
            if os.name == "nt":
                self.assertTrue(source.startswith("\\\\?\\"))
                self.assertTrue(destination.startswith("\\\\?\\"))
            self.assertFalse(final_path.exists())
            self.assertFalse(list(final_path.parent.glob(f".{final_path.name}.*.tmp")))

    def test_production_sandbox_probe_report_writer_handles_deep_path(self) -> None:
        with _runtime_root() as root:
            probe_root = (
                root
                / "parent"
                / "CodexAutomation"
                / "runtime"
                / "real_role_sandbox_probes"
                / ("probe_real_task_real_task_" + "p" * 96 + "_20260706T112410340862Z")
            )
            final_path = probe_root / "SANDBOX_WRITE_PROBE_REPORT.json"
            report = {
                "probeRunId": probe_root.name,
                "finalVerdict": "PASS",
                "processStarted": False,
                "modelInvocationStarted": False,
                "sandboxStarted": False,
                "networkUsed": False,
                "errorCode": None,
                "errorMessage": None,
            }

            _write_probe_report(report, probe_root)

            if os.name == "nt":
                self.assertGreater(len(str(final_path.resolve(strict=False))), 260)
            self.assertEqual(read_json(final_path), report)
            self.assertFalse(list(probe_root.glob(".SANDBOX_WRITE_PROBE_REPORT.json.*.tmp")))


def _deep_json_path(root: Path, name: str, filename: str) -> Path:
    return root / ("deep_" + name) / ("segment_" + "x" * 80) / ("segment_" + "y" * 80) / ("segment_" + "z" * 80) / filename


class _runtime_root:
    def __enter__(self) -> Path:
        self.temp = tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime")
        self.root = Path(self.temp.name)
        return self.root

    def __exit__(self, exc_type, exc, tb) -> None:
        try:
            self.temp.cleanup()
        except OSError:
            shutil.rmtree(file_utils._fs_path(Path(self.temp.name)), ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
