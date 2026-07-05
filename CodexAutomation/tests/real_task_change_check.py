from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from dataclasses import replace
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
import real_task_change_runner  # noqa: E402
from real_task_change_runner import begin_change_tracking, finalize_change_analysis, validate_final_change_analysis_report, validate_source_inventory_report, validate_workspace_diff_report  # noqa: E402
from real_task_foundation_runner import prepare_foundation_workspace  # noqa: E402
from schema_validator import validate as validate_schema_instance  # noqa: E402
from workspace_change_inventory import build_change_inventory  # noqa: E402
from workspace_change_models import REAL_TASK_CHANGE_STAGE, canonical_json_bytes, canonical_sha256  # noqa: E402
from workspace_change_policy import parse_change_policy  # noqa: E402
from workspace_diff import build_workspace_diff  # noqa: E402
from task_manifest_validator import parse_real_task_policy  # noqa: E402
from workspace_inventory import InventoryError  # noqa: E402


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class RealTaskChangeCheck(unittest.TestCase):
    def test_change_policy_config_is_strict(self) -> None:
        real_policy, real_errors = parse_real_task_policy(CONFIG)
        self.assertFalse(real_errors)
        policy, errors = parse_change_policy(CONFIG, real_policy)
        self.assertFalse(errors, [error.to_dict() for error in errors])
        self.assertIsNotNone(policy)
        cases = []
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskChangePolicy"]["renameDetection"] = True
        cases.append(bad)
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskChangePolicy"]["allowUtf16"] = 1
        cases.append(bad)
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskChangePolicy"]["allowedChangedTextExtensions"].append(".txt")
        cases.append(bad)
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskChangePolicy"]["allowedChangedTextExtensions"].append("TXT")
        cases.append(bad)
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskChangePolicy"]["allowedChangedTextExtensions"].append(".png")
        cases.append(bad)
        for field, value in (("maxDiffReportBytes", True), ("maxTextReadBytes", True), ("maxDiffReportBytes", 0), ("maxTextReadBytes", -1)):
            bad = json.loads(json.dumps(CONFIG))
            bad["realTaskChangePolicy"][field] = value
            cases.append(bad)
        for item in cases:
            with self.subTest(policy=item["realTaskChangePolicy"]):
                parsed, parse_errors = parse_change_policy(item, real_policy)
                self.assertIsNone(parsed)
                self.assertTrue(parse_errors)

    def test_pass_integration_records_exact_diff_and_reports(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(
                parent,
                source_paths=["Assets/TestFixture/input.txt", "Assets/TestFixture/delete_me.txt"],
                write_paths=["Assets/TestFixture/input.txt", "Assets/TestFixture/result.json"],
                delete_paths=["Assets/TestFixture/delete_me.txt"],
                expected_outputs=[{"path": "Assets/TestFixture/result.json", "kind": "file", "required": True}],
            )
            prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_change_pass_12345678")
            handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
            workspace = Path(handle.workspaceDirectory)
            deleted_size = (workspace / "Assets" / "TestFixture" / "delete_me.txt").stat().st_size
            (workspace / "Assets" / "TestFixture" / "input.txt").write_text("changed\n", encoding="utf-8")
            (workspace / "Assets" / "TestFixture" / "result.json").write_text('{"ok": true}\n', encoding="utf-8")
            (workspace / "Assets" / "TestFixture" / "delete_me.txt").unlink()
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "PASS")
            run_dir = Path(handle.runDirectory)
            diff = read_json(run_dir / "WORKSPACE_DIFF_REPORT.json")
            self.assertEqual([item["path"] for item in diff["categories"]["modified"]], ["Assets/TestFixture/input.txt"])
            self.assertEqual([item["path"] for item in diff["categories"]["added"]], ["Assets/TestFixture/result.json"])
            self.assertEqual([item["path"] for item in diff["categories"]["deleted"]], ["Assets/TestFixture/delete_me.txt"])
            self.assertEqual(diff["summary"]["totalChangedFiles"], 3)
            self.assertEqual(diff["summary"]["totalDeletedBytes"], deleted_size)
            self.assertFalse(diff["renameDetection"])
            self.assert_no_execution_flags(result.report)
            for name in [
                "CHANGE_BASELINE_INVENTORY.json",
                "CHANGE_FINAL_INVENTORY.json",
                "WORKSPACE_DIFF_REPORT.json",
                "CHANGE_POLICY_REPORT.json",
                "FINAL_CHANGE_ANALYSIS_REPORT.json",
            ]:
                self.assertTrue((run_dir / name).exists(), name)

    def test_noop_integration_passes_with_empty_diff_when_no_required_new_output(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(
                parent,
                source_paths=["Assets/TestFixture/input.txt"],
                write_paths=["Assets/TestFixture/input.txt"],
                delete_paths=[],
                expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
            )
            prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_change_noop_12345678")
            handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "PASS")
            self.assertEqual(result.report["diffSummary"]["totalChangedFiles"], 0)
            self.assertEqual(result.report["diffSummary"]["totalChangedBytes"], 0)

    def test_fresh_inventory_classification_and_deterministic_hash(self) -> None:
        with self.synthetic_parent() as parent:
            prep, handle = self.prepared_handle(parent)
            workspace = Path(handle.workspaceDirectory)
            (workspace / "Assets" / "TestFixture" / ".hidden").write_text("hidden", encoding="utf-8")
            (workspace / "Assets" / "TestFixture" / "empty").mkdir()
            (workspace / "Assets" / "TestFixture" / "bom.txt").write_bytes(b"\xef\xbb\xbfhello")
            (workspace / "Assets" / "TestFixture" / "has_nul.txt").write_bytes(b"a\x00b")
            policy = parse_change_policy(CONFIG, parse_real_task_policy(CONFIG)[0])[0]
            self.assertIsNotNone(policy)
            service_registry = read_json(Path(handle.baselineInventoryPath))["serviceRegistry"]
            first = build_change_inventory(workspace, handle.runId, handle.taskId, handle.trustedContextHash, "change_final", service_registry, policy, CONFIG["realTaskFoundation"]["maxInventoryEntries"])
            second = build_change_inventory(workspace, handle.runId, handle.taskId, handle.trustedContextHash, "change_final", service_registry, policy, CONFIG["realTaskFoundation"]["maxInventoryEntries"])
            entries = {entry["path"]: entry for entry in first["entries"]}
            self.assertEqual(first["inventorySha256"], second["inventorySha256"])
            self.assertIn("Assets/TestFixture/.hidden", entries)
            self.assertIn("Assets/TestFixture/empty", entries)
            self.assertEqual(entries["Assets/TestFixture/bom.txt"]["textEncoding"], "utf-8-bom")
            self.assertEqual(entries["Assets/TestFixture/has_nul.txt"]["contentKind"], "binary")
            self.assertIn(".git", entries)
            self.assertNotIn(".git/config", entries)
            self.assertEqual(entries[".agents"]["serviceKind"], "empty_agents_directory")
            self.assertEqual(prep["finalVerdict"], "PASS")

    def test_diff_no_rename_inference_and_type_change_counted_once(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent)
            workspace = Path(handle.workspaceDirectory)
            baseline = read_json(Path(handle.baselineInventoryPath))
            (workspace / "Assets" / "TestFixture" / "input.txt").rename(workspace / "Assets" / "TestFixture" / "renamed.txt")
            (workspace / "Assets" / "TestFixture" / "delete_me.txt").unlink()
            (workspace / "Assets" / "TestFixture" / "delete_me.txt").mkdir()
            policy = parse_change_policy(CONFIG, parse_real_task_policy(CONFIG)[0])[0]
            final = build_change_inventory(workspace, handle.runId, handle.taskId, handle.trustedContextHash, "change_final", baseline["serviceRegistry"], policy, CONFIG["realTaskFoundation"]["maxInventoryEntries"])
            diff = build_workspace_diff(baseline, final)
            self.assertIn("Assets/TestFixture/input.txt", [item["path"] for item in diff["categories"]["deleted"]])
            self.assertIn("Assets/TestFixture/renamed.txt", [item["path"] for item in diff["categories"]["added"]])
            self.assertIn("Assets/TestFixture/delete_me.txt", [item["path"] for item in diff["categories"]["typeChanged"]])
            self.assertEqual(diff["summary"]["totalChangedFiles"], 3)

    def test_forbidden_write_delete_binary_service_git_and_limits(self) -> None:
        cases = [
            ("forbidden_write", lambda ws: (ws / "Assets" / "TestFixture" / "forbidden.txt").write_text("x", encoding="utf-8"), "FAIL", "REAL_TASK_FORBIDDEN_CHANGE"),
            ("forbidden_delete", lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").unlink(), "FAIL", "REAL_TASK_FORBIDDEN_DELETE"),
            ("binary", lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes(b"\x00\x01"), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("service", lambda ws: (ws / "AGENTS.md").write_text("changed", encoding="utf-8"), "BLOCKED", "REAL_TASK_SERVICE_FILE_INVALID"),
            ("agents_child", lambda ws: (ws / ".agents" / "child.txt").write_text("x", encoding="utf-8"), "BLOCKED", "REAL_TASK_SERVICE_FILE_INVALID"),
            ("git", lambda ws: (ws / ".git" / "config").write_text("changed", encoding="utf-8"), "BLOCKED", "REAL_TASK_ISOLATED_GIT_INVALID"),
        ]
        for suffix, mutate, verdict, code in cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    _, handle = self.prepared_handle(parent, f"foundation_change_{suffix}_12345678")
                    mutate(Path(handle.workspaceDirectory))
                    result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(result.finalVerdict, verdict)
                    policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
                    self.assertIn(code, {item["code"] for item in policy_report["findings"]})

    def test_stale_substituted_and_reused_handle_block(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_reuse_12345678")
            first = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(first.finalVerdict, "PASS")
            second = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(second.finalVerdict, "BLOCKED")
            self.assertEqual(second.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_substitute_12345678")
            baseline_path = Path(handle.baselineInventoryPath)
            baseline = read_json(baseline_path)
            baseline["entries"] = []
            baseline_path.write_text(json.dumps(baseline), encoding="utf-8")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_BASELINE_INVALID")

    def test_forged_copied_wrong_tracking_and_tracking_report_substitution_block(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_forged_12345678")
            copied = replace(handle)
            result = finalize_change_analysis(copied, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_CONTEXT_MISMATCH")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_wrongtrack_12345678")
            wrong = replace(handle, trackingId="wrong-tracking-id")
            result = finalize_change_analysis(wrong, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_CONTEXT_MISMATCH")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_tracksub_12345678")
            tracking_path = Path(handle.trackingReportPath)
            tracking = read_json(tracking_path)
            tracking["runId"] = "other-run"
            tracking_path.write_text(json.dumps(tracking), encoding="utf-8")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_CONTEXT_MISMATCH")

    def test_persisted_ba_final_semantic_mismatch_blocks_begin(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(
                parent,
                source_paths=["Assets/TestFixture"],
                write_paths=["Assets/TestFixture/input.txt"],
                delete_paths=[],
                expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
            )
            prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_change_babad_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / prep["runId"]
            final_path = run_dir / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            final_report = read_json(final_path)
            final_report["workspaceReady"] = False
            final_path.write_text(json.dumps(final_report), encoding="utf-8")
            with self.assertRaises(Exception) as raised:
                begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(raised.exception.code, "REAL_TASK_CHANGE_CONTEXT_MISMATCH")

    def test_inventory_entry_cap_enforced_during_traversal(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_entrycap_12345678")
            workspace = Path(handle.workspaceDirectory)
            policy = parse_change_policy(CONFIG, parse_real_task_policy(CONFIG)[0])[0]
            service_registry = read_json(Path(handle.baselineInventoryPath))["serviceRegistry"]
            with self.assertRaises(InventoryError) as raised:
                build_change_inventory(workspace, handle.runId, handle.taskId, handle.trustedContextHash, "change_final", service_registry, policy, 1)
            self.assertEqual(raised.exception.code, "REAL_TASK_SOURCE_LIMIT_EXCEEDED")

    def test_second_finalize_after_fail_is_rejected(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_failreuse_12345678")
            workspace = Path(handle.workspaceDirectory)
            (workspace / "Assets" / "TestFixture" / "forbidden.txt").write_text("x", encoding="utf-8")
            first = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(first.finalVerdict, "FAIL")
            second = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(second.finalVerdict, "BLOCKED")
            self.assertEqual(second.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")

    def test_parent_git_and_source_changes_block(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_parentgit_12345678")
            (parent / "parent_untracked.txt").write_text("dirty\n", encoding="utf-8")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
            self.assertIn("REAL_TASK_PARENT_GIT_CHANGED", {item["code"] for item in policy_report["findings"]})
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_parentsource_12345678")
            (parent / "Assets" / "TestFixture" / "input.txt").write_text("parent changed\n", encoding="utf-8")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
            self.assertIn("REAL_TASK_PARENT_SOURCE_CHANGED", {item["code"] for item in policy_report["findings"]})

    def test_parent_source_post_inventory_substitution_blocks(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_sourcepostsub_12345678")
            source_post_path = Path(handle.runDirectory) / "SOURCE_POST_INVENTORY.json"
            source_post = read_json(source_post_path)
            source_post["inventorySha256"] = "0" * 64
            source_post_path.write_text(json.dumps(source_post), encoding="utf-8")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_CONTEXT_MISMATCH")

    def test_source_post_semantic_validator_rejects_malformed_reports(self) -> None:
        cases = [
            ("wrong_file_count", lambda report: report.update({"fileCount": report["fileCount"] + 1})),
            ("wrong_directory_count", lambda report: report.update({"directoryCount": report["directoryCount"] + 1})),
            ("wrong_total_bytes", lambda report: report.update({"totalBytes": report["totalBytes"] + 1})),
            ("wrong_hash", lambda report: report.update({"inventorySha256": "0" * 64})),
            ("duplicate_path", lambda report: report["entries"].append(json.loads(json.dumps(report["entries"][0])))),
            ("unsorted_entries", lambda report: report["entries"].reverse()),
            ("invalid_file_sha", lambda report: self.first_source_file(report).update({"sha256": "bad"})),
            ("directory_with_sha", lambda report: self.first_source_directory(report).update({"sha256": "a" * 64})),
            ("complete_false", lambda report: report.update({"complete": False})),
            ("non_empty_errors", lambda report: report.update({"errors": [{"code": "X", "message": "bad"}]})),
            ("wrong_run", lambda report: report.update({"runId": "other-run"})),
            ("wrong_task", lambda report: report.update({"taskId": "other-task"})),
        ]
        for suffix, mutate in cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    manifest = self.write_manifest(
                        parent,
                        source_paths=["Assets/TestFixture"],
                        write_paths=["Assets/TestFixture/input.txt"],
                        delete_paths=[],
                        expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
                    )
                    prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, f"foundation_change_sp_{suffix}_12345678")
                    run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / prep["runId"]
                    source_post_path = run_dir / "SOURCE_POST_INVENTORY.json"
                    source_post = read_json(source_post_path)
                    mutate(source_post)
                    source_post_path.write_text(json.dumps(source_post), encoding="utf-8")
                    with self.assertRaises(Exception) as raised:
                        begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(raised.exception.code, "REAL_TASK_CHANGE_CONTEXT_MISMATCH")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_sp_valid_12345678")
            source_post = read_json(Path(handle.runDirectory) / "SOURCE_POST_INVENTORY.json")
            self.assertFalse(validate_source_inventory_report(source_post))

    def test_source_post_schema_and_source_limits_are_enforced(self) -> None:
        schema_cases = [
            ("unknown_root_field", lambda report: report.update({"unknown": True})),
            ("missing_entries", lambda report: report.pop("entries")),
            ("unknown_entry_field", lambda report: report["entries"][0].update({"unknown": True})),
            ("wrong_file_count_type", lambda report: report.update({"fileCount": "1"})),
        ]
        for suffix, mutate in schema_cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    manifest = self.write_manifest(
                        parent,
                        source_paths=["Assets/TestFixture"],
                        write_paths=["Assets/TestFixture/input.txt"],
                        delete_paths=[],
                        expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
                    )
                    prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, f"foundation_change_spschema_{suffix}_12345678")
                    source_post_path = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / prep["runId"] / "SOURCE_POST_INVENTORY.json"
                    source_post = read_json(source_post_path)
                    mutate(source_post)
                    source_post_path.write_text(json.dumps(source_post), encoding="utf-8")
                    with self.assertRaises(Exception) as raised:
                        begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(raised.exception.code, "REAL_TASK_CHANGE_CONTEXT_MISMATCH")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_splimits_12345678")
            source_post = read_json(Path(handle.runDirectory) / "SOURCE_POST_INVENTORY.json")
            exact_limits = {
                "maxSourceFiles": source_post["fileCount"],
                "maxSourceBytes": source_post["totalBytes"],
                "maxInventoryEntries": len(source_post["entries"]),
            }
            self.assertFalse(validate_source_inventory_report(source_post, exact_limits))
            for bad_limits in (
                {"maxSourceFiles": True, "maxSourceBytes": source_post["totalBytes"], "maxInventoryEntries": len(source_post["entries"])},
                {"maxSourceFiles": 0, "maxSourceBytes": source_post["totalBytes"], "maxInventoryEntries": len(source_post["entries"])},
                {"maxSourceFiles": source_post["fileCount"], "maxSourceBytes": -1, "maxInventoryEntries": len(source_post["entries"])},
            ):
                self.assertTrue(validate_source_inventory_report(source_post, bad_limits))
            for tight_limits in (
                {"maxSourceFiles": source_post["fileCount"] - 1, "maxSourceBytes": source_post["totalBytes"], "maxInventoryEntries": len(source_post["entries"])},
                {"maxSourceFiles": source_post["fileCount"], "maxSourceBytes": source_post["totalBytes"] - 1, "maxInventoryEntries": len(source_post["entries"])},
                {"maxSourceFiles": source_post["fileCount"], "maxSourceBytes": source_post["totalBytes"], "maxInventoryEntries": len(source_post["entries"]) - 1},
            ):
                self.assertTrue(validate_source_inventory_report(source_post, tight_limits))
            bool_counter = json.loads(json.dumps(source_post))
            bool_counter["fileCount"] = True
            bool_counter["inventorySha256"] = canonical_sha256({key: value for key, value in bool_counter.items() if key not in {"inventorySha256", "runId", "taskId"}})
            self.assertTrue(validate_source_inventory_report(bool_counter, exact_limits))
            bool_counter = json.loads(json.dumps(source_post))
            bool_counter["totalBytes"] = True
            bool_counter["inventorySha256"] = canonical_sha256({key: value for key, value in bool_counter.items() if key not in {"inventorySha256", "runId", "taskId"}})
            self.assertTrue(validate_source_inventory_report(bool_counter, exact_limits))
            path_limits = dict(exact_limits)
            path_limits["maxPathLength"] = max(len(entry["path"]) for entry in source_post["entries"])
            self.assertFalse(validate_source_inventory_report(source_post, path_limits))
            path_limits["maxPathLength"] -= 1
            self.assertTrue(validate_source_inventory_report(source_post, path_limits))
            path_limits["maxPathLength"] = True
            self.assertTrue(validate_source_inventory_report(source_post, path_limits))

    def test_parent_source_fresh_inventory_uses_trusted_source_entry_cap(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(
                parent,
                source_paths=["Assets/TestFixture"],
                write_paths=["Assets/TestFixture/input.txt"],
                delete_paths=[],
                expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
            )
            prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_change_trustedcap_exact_12345678")
            source_post = read_json(parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / prep["runId"] / "SOURCE_POST_INVENTORY.json")
            trusted_cap = len(source_post["entries"])
            prep["trustedContextHash"] = self.rewrite_trusted_source_entry_cap(parent, prep["runId"], trusted_cap)
            handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
            exact_result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(exact_result.finalVerdict, "PASS")

        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(
                parent,
                source_paths=["Assets/TestFixture"],
                write_paths=["Assets/TestFixture/input.txt"],
                delete_paths=[],
                expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
            )
            prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_change_trustedcap_plus_12345678")
            source_post = read_json(parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / prep["runId"] / "SOURCE_POST_INVENTORY.json")
            trusted_cap = len(source_post["entries"])
            larger_config = json.loads(json.dumps(CONFIG))
            larger_config["realTaskFoundation"]["maxInventoryEntries"] = trusted_cap + 50
            prep["trustedContextHash"] = self.rewrite_trusted_source_entry_cap(parent, prep["runId"], trusted_cap)
            handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", larger_config)
            captured: list[int] = []
            original_builder = real_task_change_runner.build_source_inventory

            def recording_builder(root: Path, source_paths: list[str], max_entries: int, chunk_bytes: int, service_paths=None):
                captured.append(max_entries)
                return original_builder(root, source_paths, max_entries, chunk_bytes, service_paths)

            (parent / "Assets" / "TestFixture" / "trusted_cap_extra.txt").write_text("extra\n", encoding="utf-8")
            with mock.patch("real_task_change_runner.build_source_inventory", side_effect=recording_builder):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", larger_config)
            self.assertEqual(prep["finalVerdict"], "PASS")
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_SOURCE_LIMIT_EXCEEDED")
            self.assertNotEqual(result.report["errorCode"], "REAL_TASK_PARENT_SOURCE_CHANGED")
            self.assertIn(trusted_cap, captured)
            self.assertNotIn(larger_config["realTaskFoundation"]["maxInventoryEntries"], captured)
            reused = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", larger_config)
            self.assertEqual(reused.finalVerdict, "BLOCKED")
            self.assertEqual(reused.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")

    def test_parent_source_fresh_inventory_detects_file_added_deleted_type_and_empty_dir_changes(self) -> None:
        cases = [
            ("modified", lambda parent: (parent / "Assets" / "TestFixture" / "input.txt").write_text("parent changed\n", encoding="utf-8")),
            ("added", lambda parent: (parent / "Assets" / "TestFixture" / "added.txt").write_text("added\n", encoding="utf-8")),
            ("deleted", lambda parent: (parent / "Assets" / "TestFixture" / "delete_me.txt").unlink()),
            ("type_changed", lambda parent: ((parent / "Assets" / "TestFixture" / "delete_me.txt").unlink(), (parent / "Assets" / "TestFixture" / "delete_me.txt").mkdir())),
            ("empty_dir", lambda parent: (parent / "Assets" / "TestFixture" / "empty_parent").mkdir()),
        ]
        for suffix, mutate in cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    _, handle = self.prepared_handle(parent, f"foundation_change_parent_{suffix}_12345678")
                    mutate(parent)
                    result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(result.finalVerdict, "BLOCKED")
                    policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
                    self.assertIn("REAL_TASK_PARENT_SOURCE_CHANGED", {item["code"] for item in policy_report["findings"]})

    def test_diff_unchanged_entries_include_full_side_evidence_and_cross_check(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_unchanged_12345678")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "PASS")
            run_dir = Path(handle.runDirectory)
            baseline = read_json(Path(handle.baselineInventoryPath))
            final = read_json(run_dir / "CHANGE_FINAL_INVENTORY.json")
            diff = read_json(run_dir / "WORKSPACE_DIFF_REPORT.json")
            self.assertTrue(diff["categories"]["unchanged"])
            unchanged = diff["categories"]["unchanged"][0]
            self.assertIsInstance(unchanged["before"], dict)
            self.assertIsInstance(unchanged["after"], dict)
            self.assertFalse(validate_workspace_diff_report(diff, baseline, final))
            mutated = json.loads(json.dumps(diff))
            mutated["categories"]["unchanged"][0]["after"]["sha256"] = "0" * 64
            mutated["diffReportSha256"] = canonical_sha256({key: value for key, value in mutated.items() if key != "diffReportSha256"})
            self.assertTrue(validate_workspace_diff_report(mutated, baseline, final))
            mutated = json.loads(json.dumps(diff))
            removed = mutated["categories"]["unchanged"].pop()
            mutated["diffReportSha256"] = canonical_sha256({key: value for key, value in mutated.items() if key != "diffReportSha256"})
            self.assertTrue(validate_workspace_diff_report(mutated, baseline, final), removed["path"])

    def test_binary_text_matrix_focus_cases(self) -> None:
        unity_serialized_cases = (".unity", ".prefab", ".asset", ".controller", ".anim", ".mat")
        cases = [
            ("utf8_txt", lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_text("ok\n", encoding="utf-8"), "PASS", None),
            ("utf8_bom_json", lambda ws: (ws / "Assets" / "TestFixture" / "result.json").write_bytes(b"\xef\xbb\xbf{\"ok\":true}\n"), "PASS", None),
            ("utf16_le", lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes("bad".encode("utf-16-le")), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("utf16_be", lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes("bad".encode("utf-16-be")), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("invalid_utf8", lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes(b"\xff\xfe\xfa"), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("nul_text", lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes(b"a\x00b"), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("extensionless_added", lambda ws: (ws / "Assets" / "TestFixture" / "new_extensionless").write_text("x", encoding="utf-8"), "FAIL", "REAL_TASK_TEXT_CHANGE_INVALID"),
            ("extensionless_modified", lambda ws: (ws / "Assets" / "TestFixture" / "extensionless").write_text("changed", encoding="utf-8"), "FAIL", "REAL_TASK_TEXT_CHANGE_INVALID"),
            ("unknown_extension", lambda ws: (ws / "Assets" / "TestFixture" / "new.unknown").write_text("x", encoding="utf-8"), "FAIL", "REAL_TASK_TEXT_CHANGE_INVALID"),
            ("unknown_extension_modified", lambda ws: (ws / "Assets" / "TestFixture" / "known.unknown").write_text("changed", encoding="utf-8"), "FAIL", "REAL_TASK_TEXT_CHANGE_INVALID"),
            ("meta_modified_allowed_by_current_policy", lambda ws: (ws / "Assets" / "TestFixture" / "serialized.meta").write_text("guid: changed\n", encoding="utf-8"), "PASS", None),
            ("added_binary", lambda ws: (ws / "Assets" / "TestFixture" / "blob.bin").write_bytes(b"\x00\x01"), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("modified_binary", lambda ws: (ws / "Assets" / "TestFixture" / "binary.bin").write_bytes(b"\x00\x02"), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("deleted_binary", lambda ws: (ws / "Assets" / "TestFixture" / "binary.bin").unlink(), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("unchanged_binary_source", lambda ws: None, "PASS", None),
            ("binary_file_to_directory", lambda ws: ((ws / "Assets" / "TestFixture" / "binary.bin").unlink(), (ws / "Assets" / "TestFixture" / "binary.bin").mkdir()), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
            ("directory_to_binary_file", lambda ws: (shutil.rmtree(ws / "Assets" / "TestFixture" / "BinaryDir"), (ws / "Assets" / "TestFixture" / "BinaryDir").write_bytes(b"\x00\x01")), "FAIL", "REAL_TASK_BINARY_CHANGE_FORBIDDEN"),
        ]
        for extension in unity_serialized_cases:
            cases.append((f"unity_serialized_modified_{extension[1:]}", lambda ws, ext=extension: (ws / "Assets" / "TestFixture" / f"serialized{ext}").write_text("%YAML changed\n", encoding="utf-8"), "FAIL", "REAL_TASK_TEXT_CHANGE_INVALID"))
        for suffix, mutate, verdict, code in cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    (parent / "Assets" / "TestFixture" / "binary.bin").write_bytes(b"\x00\x01")
                    (parent / "Assets" / "TestFixture" / "extensionless").write_text("plain\n", encoding="utf-8")
                    (parent / "Assets" / "TestFixture" / "known.unknown").write_text("known\n", encoding="utf-8")
                    (parent / "Assets" / "TestFixture" / "BinaryDir").mkdir()
                    (parent / "Assets" / "TestFixture" / "BinaryDir" / "child.txt").write_text("child\n", encoding="utf-8")
                    for extension in unity_serialized_cases:
                        (parent / "Assets" / "TestFixture" / f"serialized{extension}").write_text("%YAML\n", encoding="utf-8")
                    (parent / "Assets" / "TestFixture" / "serialized.meta").write_text("guid: fixture\n", encoding="utf-8")
                    subprocess.run(["git", "add", "Assets"], cwd=str(parent), capture_output=True, text=True, check=False, timeout=10)
                    subprocess.run(["git", "commit", "-m", "binary fixture"], cwd=str(parent), capture_output=True, text=True, check=False, timeout=10)
                    manifest = self.write_manifest(
                        parent,
                        source_paths=["Assets/TestFixture"],
                        write_paths=["Assets/TestFixture"],
                        delete_paths=["Assets/TestFixture/binary.bin", "Assets/TestFixture/BinaryDir"],
                        expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
                    )
                    prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, f"foundation_change_bt_{suffix}_12345678")
                    handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
                    mutate(Path(handle.workspaceDirectory))
                    result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(result.finalVerdict, verdict)
                    if code:
                        policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
                        self.assertIn(code, {item["code"] for item in policy_report["findings"]})

    def test_max_text_read_bytes_classification_boundaries_use_actual_bytes(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_readlimit_12345678")
            policy = parse_change_policy(CONFIG, parse_real_task_policy(CONFIG)[0])[0]
            self.assertIsNotNone(policy)
            small_policy = replace(policy, maxTextReadBytes=4)
            workspace = Path(handle.workspaceDirectory)
            service_registry = read_json(Path(handle.baselineInventoryPath))["serviceRegistry"]
            fixture = workspace / "Assets" / "TestFixture"
            cases = [
                ("exact_utf8", b"abcd", "text", "utf-8"),
                ("plus_one_utf8", b"abcde", "binary", None),
                ("exact_bom", b"\xef\xbb\xbfa", "text", "utf-8-bom"),
                ("plus_one_bom", b"\xef\xbb\xbfab", "binary", None),
            ]
            for suffix, data, kind, encoding in cases:
                with self.subTest(suffix=suffix):
                    target = fixture / f"read_limit_{suffix}.txt"
                    target.write_bytes(data)
                    inventory = build_change_inventory(
                        workspace,
                        "run-read-limit",
                        "TASK-CHANGE-001",
                        "trusted-context",
                        "change_final",
                        service_registry,
                        small_policy,
                        CONFIG["realTaskFoundation"]["maxInventoryEntries"],
                    )
                    entry = {item["path"]: item for item in inventory["entries"]}[f"Assets/TestFixture/read_limit_{suffix}.txt"]
                    self.assertEqual(entry["size"], len(data))
                    self.assertEqual(entry["contentKind"], kind)
                    self.assertEqual(entry["textEncoding"], encoding)
                    target.unlink()

    def test_numeric_limit_boundaries_use_finalize_path(self) -> None:
        cases = [
            ("changed_files_exact", {"maxChangedFiles": 1, "maxChangedBytes": 100, "maxSingleChangedFileBytes": 100}, lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes(b"12345678\n"), "PASS"),
            ("changed_files_plus_one", {"maxChangedFiles": 1, "maxChangedBytes": 100, "maxSingleChangedFileBytes": 100}, lambda ws: ((ws / "Assets" / "TestFixture" / "input.txt").write_bytes(b"12345678\n"), (ws / "Assets" / "TestFixture" / "result.json").write_bytes(b"{}\n")), "FAIL"),
            ("changed_bytes_exact", {"maxChangedFiles": 2, "maxChangedBytes": 9, "maxSingleChangedFileBytes": 5}, lambda ws: ((ws / "Assets" / "TestFixture" / "a.txt").write_bytes(b"1234"), (ws / "Assets" / "TestFixture" / "b.txt").write_bytes(b"12345")), "PASS"),
            ("changed_bytes_plus_one", {"maxChangedFiles": 2, "maxChangedBytes": 8, "maxSingleChangedFileBytes": 5}, lambda ws: ((ws / "Assets" / "TestFixture" / "a.txt").write_bytes(b"1234"), (ws / "Assets" / "TestFixture" / "b.txt").write_bytes(b"12345")), "FAIL"),
            ("single_exact", {"maxChangedFiles": 2, "maxChangedBytes": 100, "maxSingleChangedFileBytes": 10}, lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes(b"12345678\n"), "PASS"),
            ("single_plus_one", {"maxChangedFiles": 2, "maxChangedBytes": 100, "maxSingleChangedFileBytes": 9}, lambda ws: (ws / "Assets" / "TestFixture" / "input.txt").write_bytes(b"12345678\n"), "FAIL"),
        ]
        for suffix, limits, mutate, verdict in cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    manifest = self.write_manifest(
                        parent,
                        source_paths=["Assets/TestFixture"],
                        write_paths=["Assets/TestFixture"],
                        delete_paths=[],
                        expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
                        limits=limits,
                    )
                    prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, f"foundation_change_limit_{suffix}_12345678")
                    handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
                    mutate(Path(handle.workspaceDirectory))
                    result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(result.finalVerdict, verdict)
                    if verdict == "FAIL":
                        policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
                        self.assertIn("REAL_TASK_DIFF_LIMIT_EXCEEDED", {item["code"] for item in policy_report["findings"]})

    def test_numeric_single_file_boundaries_cover_added_deleted_and_type_changed(self) -> None:
        cases = [
            ("added_single_exact", [], lambda ws: (ws / "Assets" / "TestFixture" / "result.json").write_bytes(b"123456789\n"), 10, "PASS"),
            ("added_single_plus_one", [], lambda ws: (ws / "Assets" / "TestFixture" / "result.json").write_bytes(b"123456789\n"), 9, "FAIL"),
            ("deleted_single_exact", ["Assets/TestFixture/delete_me.txt"], lambda ws: (ws / "Assets" / "TestFixture" / "delete_me.txt").unlink(), 11, "PASS"),
            ("deleted_single_plus_one", ["Assets/TestFixture/delete_me.txt"], lambda ws: (ws / "Assets" / "TestFixture" / "delete_me.txt").unlink(), 10, "FAIL"),
            ("file_to_directory_exact", ["Assets/TestFixture/delete_me.txt"], lambda ws: ((ws / "Assets" / "TestFixture" / "delete_me.txt").unlink(), (ws / "Assets" / "TestFixture" / "delete_me.txt").mkdir()), 11, "PASS"),
            ("file_to_directory_plus_one", ["Assets/TestFixture/delete_me.txt"], lambda ws: ((ws / "Assets" / "TestFixture" / "delete_me.txt").unlink(), (ws / "Assets" / "TestFixture" / "delete_me.txt").mkdir()), 10, "FAIL"),
            ("directory_to_file_exact", ["Assets/TestFixture/DirToFile.txt", "Assets/TestFixture/DirToFile.txt/child.txt"], lambda ws: (shutil.rmtree(ws / "Assets" / "TestFixture" / "DirToFile.txt"), (ws / "Assets" / "TestFixture" / "DirToFile.txt").write_bytes(b"123456789\n")), 10, "PASS"),
            ("directory_to_file_plus_one", ["Assets/TestFixture/DirToFile.txt", "Assets/TestFixture/DirToFile.txt/child.txt"], lambda ws: (shutil.rmtree(ws / "Assets" / "TestFixture" / "DirToFile.txt"), (ws / "Assets" / "TestFixture" / "DirToFile.txt").write_bytes(b"123456789\n")), 9, "FAIL"),
        ]
        for suffix, delete_paths, mutate, single_limit, verdict in cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    (parent / "Assets" / "TestFixture" / "DirToFile.txt").mkdir()
                    (parent / "Assets" / "TestFixture" / "DirToFile.txt" / "child.txt").write_text("child\n", encoding="utf-8")
                    subprocess.run(["git", "add", "Assets"], cwd=str(parent), capture_output=True, text=True, check=False, timeout=10)
                    subprocess.run(["git", "commit", "-m", "numeric fixture"], cwd=str(parent), capture_output=True, text=True, check=False, timeout=10)
                    manifest = self.write_manifest(
                        parent,
                        source_paths=["Assets/TestFixture"],
                        write_paths=["Assets/TestFixture"],
                        delete_paths=delete_paths,
                        expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
                        limits={"maxChangedFiles": 5, "maxChangedBytes": 100, "maxSingleChangedFileBytes": single_limit},
                    )
                    prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, f"foundation_change_single_{suffix}_12345678")
                    handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
                    mutate(Path(handle.workspaceDirectory))
                    result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(result.finalVerdict, verdict)
                    if verdict == "FAIL":
                        policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
                        self.assertIn("REAL_TASK_DIFF_LIMIT_EXCEEDED", {item["code"] for item in policy_report["findings"]})

    def test_oversized_valid_utf8_and_host_limit_overrides_do_not_bypass_policy(self) -> None:
        max_single = CONFIG["realTasks"]["maxSingleChangedFileBytes"]
        for suffix, size, verdict in (("exact", max_single, "PASS"), ("plus_one", max_single + 1, "FAIL")):
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    manifest = self.write_manifest(
                        parent,
                        source_paths=["Assets/TestFixture"],
                        write_paths=["Assets/TestFixture/input.txt"],
                        delete_paths=[],
                        expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
                        limits={"maxChangedFiles": 1, "maxChangedBytes": max_single + 1, "maxSingleChangedFileBytes": max_single},
                    )
                    prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, f"foundation_change_bigutf8_{suffix}_12345678")
                    handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
                    (Path(handle.workspaceDirectory) / "Assets" / "TestFixture" / "input.txt").write_bytes(b"a" * size)
                    result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
                    self.assertEqual(result.finalVerdict, verdict)
                    if verdict == "FAIL":
                        policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
                        self.assertIn("REAL_TASK_DIFF_LIMIT_EXCEEDED", {item["code"] for item in policy_report["findings"]})
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(
                parent,
                source_paths=["Assets/TestFixture"],
                write_paths=["Assets/TestFixture/input.txt"],
                delete_paths=[],
                expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
                limits={"maxChangedFiles": CONFIG["realTasks"]["maxChangedFiles"] + 1, "maxChangedBytes": 100, "maxSingleChangedFileBytes": 100},
            )
            prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_change_hostlimit_12345678")
            self.assertEqual(prep["finalVerdict"], "FAILED")

    def test_scope_prefix_extensionless_dotted_directory_and_ancestor_delete(self) -> None:
        with self.synthetic_parent() as parent:
            dotted = parent / "Assets" / "TestFixture" / "Dir.With.Dot"
            dotted.mkdir()
            (dotted / "child.txt").write_text("child\n", encoding="utf-8")
            mixed = parent / "Assets" / "TestFixture" / "DeleteMe"
            mixed.mkdir()
            (mixed / "allowed.txt").write_text("allowed\n", encoding="utf-8")
            (mixed / "forbidden.txt").write_text("forbidden\n", encoding="utf-8")
            extensionless = parent / "Assets" / "TestFixture" / "extensionless"
            extensionless.write_text("plain\n", encoding="utf-8")
            subprocess.run(["git", "add", "Assets"], cwd=str(parent), capture_output=True, text=True, check=False, timeout=10)
            subprocess.run(["git", "commit", "-m", "extra fixtures"], cwd=str(parent), capture_output=True, text=True, check=False, timeout=10)
            manifest = self.write_manifest(
                parent,
                source_paths=["Assets/TestFixture"],
                write_paths=["Assets/TestFixture/Dir.With.Dot", "Assets/TestFixture/extensionless", "Assets/TestFixture/input.txt"],
                delete_paths=["Assets/TestFixture/DeleteMe/allowed.txt"],
                expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
            )
            prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_change_scopeedge_12345678")
            handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
            workspace = Path(handle.workspaceDirectory)
            (workspace / "Assets" / "TestFixture" / "Dir.With.Dot" / "new.txt").write_text("ok\n", encoding="utf-8")
            (workspace / "Assets" / "TestFixture" / "extensionless").write_text("changed\n", encoding="utf-8")
            (workspace / "Assets" / "TestFixture" / "DeleteMe" / "allowed.txt").unlink()
            (workspace / "Assets" / "TestFixture" / "DeleteMe" / "forbidden.txt").unlink()
            (workspace / "Assets" / "TestFixture" / "DeleteMe").rmdir()
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "FAIL")
            policy_report = read_json(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
            self.assertIn("REAL_TASK_FORBIDDEN_DELETE", {item["code"] for item in policy_report["findings"]})

    def test_report_semantic_validators_reject_wrong_totals_pass_booleans_and_unknown_diff_fields(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_reportsem_12345678")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            run_dir = Path(handle.runDirectory)
            diff = read_json(run_dir / "WORKSPACE_DIFF_REPORT.json")
            diff["summary"]["totalChangedFiles"] = 99
            self.assertTrue(validate_workspace_diff_report(diff))
            final = json.loads(json.dumps(result.report))
            final["serviceFilesValid"] = False
            self.assertTrue(validate_final_change_analysis_report(final, {"policyReport": read_json(run_dir / "CHANGE_POLICY_REPORT.json"), "diff": read_json(run_dir / "WORKSPACE_DIFF_REPORT.json")}))
            diff = read_json(run_dir / "WORKSPACE_DIFF_REPORT.json")
            if diff["categories"]["unchanged"]:
                diff["categories"]["added"].append({
                    "path": "bad.txt",
                    "canonicalPathKey": "bad.txt",
                    "changeType": "added",
                    "baselineType": None,
                    "finalType": "file",
                    "baselineSize": None,
                    "finalSize": 1,
                    "baselineSha256": None,
                    "finalSha256": "a" * 64,
                    "baselineContentKind": None,
                    "finalContentKind": "text",
                    "service": False,
                    "allowedWrite": False,
                    "allowedDelete": False,
                    "violationCodes": [],
                    "before": None,
                    "after": {"type": "file", "size": 1, "sha256": "a" * 64, "extension": ".txt", "contentKind": "text", "textEncoding": "utf-8", "expectedMutability": "mutable_task"},
                    "unknown": True,
                })
                schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_workspace_diff.schema.json")
                self.assertTrue(validate_schema_instance(diff, schema))

    def test_strict_diff_schema_and_semantics_reject_nested_and_side_contract_mutations(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_strictdiff_12345678")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "PASS")
            run_dir = Path(handle.runDirectory)
            schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_workspace_diff.schema.json")
            baseline = read_json(Path(handle.baselineInventoryPath))
            final = read_json(run_dir / "CHANGE_FINAL_INVENTORY.json")
            diff = read_json(run_dir / "WORKSPACE_DIFF_REPORT.json")
            unchanged = diff["categories"]["unchanged"][0]
            self.assertFalse(validate_schema_instance(diff, schema))
            for side in ("before", "after"):
                mutated = json.loads(json.dumps(diff))
                mutated["categories"]["unchanged"][0][side]["unknown"] = True
                self.assertTrue(validate_schema_instance(mutated, schema), side)
            mutated = json.loads(json.dumps(diff))
            del mutated["categories"]["unchanged"][0]["before"]["path"]
            self.assertTrue(validate_schema_instance(mutated, schema))
            mutated = json.loads(json.dumps(diff))
            mutated["categories"]["added"].append(json.loads(json.dumps(unchanged)))
            mutated["categories"]["added"][-1]["changeType"] = "added"
            mutated["diffReportSha256"] = canonical_sha256({key: value for key, value in mutated.items() if key != "diffReportSha256"})
            self.assertTrue(validate_workspace_diff_report(mutated, baseline, final))
            mutated = json.loads(json.dumps(diff))
            mutated["categories"]["unchanged"][0]["changeType"] = "modified"
            mutated["diffReportSha256"] = canonical_sha256({key: value for key, value in mutated.items() if key != "diffReportSha256"})
            self.assertTrue(validate_workspace_diff_report(mutated, baseline, final))
            mutated = json.loads(json.dumps(diff))
            mutated["categories"]["unchanged"][0]["after"]["size"] += 1
            mutated["diffReportSha256"] = canonical_sha256({key: value for key, value in mutated.items() if key != "diffReportSha256"})
            self.assertTrue(validate_workspace_diff_report(mutated, baseline, final))

    def test_trusted_diff_and_final_report_size_caps_are_exact_boundaries(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_reportcaps_12345678")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "PASS")
            run_dir = Path(handle.runDirectory)
            baseline = read_json(Path(handle.baselineInventoryPath))
            final_inventory = read_json(run_dir / "CHANGE_FINAL_INVENTORY.json")
            diff = read_json(run_dir / "WORKSPACE_DIFF_REPORT.json")
            policy_report = read_json(run_dir / "CHANGE_POLICY_REPORT.json")
            final_report = read_json(run_dir / "FINAL_CHANGE_ANALYSIS_REPORT.json")
            diff_config = json.loads(json.dumps(CONFIG))
            diff_config["realTaskChangePolicy"]["maxDiffReportBytes"] = len(canonical_json_bytes(diff))
            diff_hash = real_task_change_runner._write_trusted_report(
                parent,
                ROOT / "CodexAutomation",
                run_dir / "cap_exact_diff" / "WORKSPACE_DIFF_REPORT.json",
                diff,
                diff_config,
                {"baselineInventory": baseline, "finalInventory": final_inventory},
            )
            self.assertEqual(diff_hash, diff["diffReportSha256"])
            diff_config["realTaskChangePolicy"]["maxDiffReportBytes"] = len(canonical_json_bytes(diff)) - 1
            with self.assertRaises(Exception) as raised:
                real_task_change_runner._write_trusted_report(
                    parent,
                    ROOT / "CodexAutomation",
                    run_dir / "cap_plus_one_diff" / "WORKSPACE_DIFF_REPORT.json",
                    diff,
                    diff_config,
                    {"baselineInventory": baseline, "finalInventory": final_inventory},
                )
            self.assertEqual(raised.exception.code, "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            self.assertEqual(raised.exception.verdict, "BLOCKED")
            final_config = json.loads(json.dumps(CONFIG))
            final_config["realTaskFoundation"]["maxReportBytes"] = len(canonical_json_bytes(final_report))
            final_hash = real_task_change_runner._write_trusted_report(
                parent,
                ROOT / "CodexAutomation",
                run_dir / "cap_exact_final" / "FINAL_CHANGE_ANALYSIS_REPORT.json",
                final_report,
                final_config,
                {"policyReport": policy_report, "diff": diff},
            )
            self.assertEqual(final_hash, canonical_sha256(final_report))
            final_config["realTaskFoundation"]["maxReportBytes"] = len(canonical_json_bytes(final_report)) - 1
            with self.assertRaises(Exception) as raised:
                real_task_change_runner._write_trusted_report(
                    parent,
                    ROOT / "CodexAutomation",
                    run_dir / "cap_plus_one_final" / "FINAL_CHANGE_ANALYSIS_REPORT.json",
                    final_report,
                    final_config,
                    {"policyReport": policy_report, "diff": diff},
                )
            self.assertEqual(raised.exception.code, "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            self.assertEqual(raised.exception.verdict, "BLOCKED")

    def test_finalize_oversized_trusted_report_blocks_and_consumes_handle(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_reportcapfinal_12345678")
            capped_config = json.loads(json.dumps(CONFIG))
            capped_config["realTaskFoundation"]["maxReportBytes"] = 1
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", capped_config)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.finalState, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            self.assertFalse((Path(handle.runDirectory) / "CHANGE_FINAL_INVENTORY.json").exists())
            self.assertFalse((Path(handle.runDirectory) / "FINAL_CHANGE_ANALYSIS_REPORT.json").exists())
            reused = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", capped_config)
            self.assertEqual(reused.finalVerdict, "BLOCKED")
            self.assertEqual(reused.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")

    def test_report_persistence_failure_paths_are_blocking(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_reportwrite_12345678")
            with mock.patch("real_task_change_runner.write_json_atomic", side_effect=OSError("write failed")):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            reused = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(reused.finalVerdict, "BLOCKED")
            self.assertEqual(reused.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_replacefail_12345678")
            with mock.patch("pathlib.Path.replace", side_effect=OSError("replace failed")):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_reportreread_12345678")
            original_read = real_task_change_runner.read_json

            def malformed_reread(path: Path):
                data = original_read(path)
                if Path(path).name == "WORKSPACE_DIFF_REPORT.json":
                    data["summary"]["totalChangedFiles"] = 999
                return data

            with mock.patch("real_task_change_runner.read_json", side_effect=malformed_reread):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_INVALID")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_rereadjson_12345678")
            original_read = real_task_change_runner.read_json

            def invalid_json_reread(path: Path):
                if Path(path).name == "WORKSPACE_DIFF_REPORT.json":
                    raise ValueError("invalid json")
                return original_read(path)

            with mock.patch("real_task_change_runner.read_json", side_effect=invalid_json_reread):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_INVALID")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_rereadschema_12345678")
            original_read = real_task_change_runner.read_json

            def schema_invalid_reread(path: Path):
                data = original_read(path)
                if Path(path).name == "WORKSPACE_DIFF_REPORT.json":
                    data.pop("categories")
                return data

            with mock.patch("real_task_change_runner.read_json", side_effect=schema_invalid_reread):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_INVALID")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_rereadhash_12345678")
            original_read = real_task_change_runner.read_json

            def hash_changed_reread(path: Path):
                data = original_read(path)
                if Path(path).name == "FINAL_CHANGE_ANALYSIS_REPORT.json":
                    data["durationSeconds"] = data["durationSeconds"] + 1
                    schema = original_read(ROOT / "CodexAutomation" / "schemas" / "real_task_change_final.schema.json")
                    policy_report = original_read(Path(handle.runDirectory) / "CHANGE_POLICY_REPORT.json")
                    diff = original_read(Path(handle.runDirectory) / "WORKSPACE_DIFF_REPORT.json")
                    self.assertFalse(validate_schema_instance(data, schema))
                    self.assertFalse(validate_final_change_analysis_report(data, {"policyReport": policy_report, "diff": diff}))
                return data

            with mock.patch("real_task_change_runner.read_json", side_effect=hash_changed_reread):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_INVALID")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_reportexisting_12345678")
            (Path(handle.runDirectory) / "WORKSPACE_DIFF_REPORT.json").write_text("{}", encoding="utf-8")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")

    def test_partial_final_temp_and_existing_malformed_reports_are_not_trusted(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_partialfinal_12345678")
            original_write = real_task_change_runner.write_json_atomic

            def partial_final_write(path: Path, data: dict[str, object]):
                if Path(path).name == "FINAL_CHANGE_ANALYSIS_REPORT.json":
                    Path(path).write_text("{", encoding="utf-8")
                    raise OSError("partial final")
                original_write(path, data)

            with mock.patch("real_task_change_runner.write_json_atomic", side_effect=partial_final_write):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            final_path = Path(handle.runDirectory) / "FINAL_CHANGE_ANALYSIS_REPORT.json"
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            self.assertTrue(final_path.exists())
            with self.assertRaises(ValueError):
                read_json(final_path)
            reused = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(reused.finalVerdict, "BLOCKED")
            self.assertEqual(reused.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")

        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_tempartifact_12345678")
            original_write = real_task_change_runner.write_json_atomic
            temp_paths: list[Path] = []

            def temp_artifact_write(path: Path, data: dict[str, object]):
                if Path(path).name == "WORKSPACE_DIFF_REPORT.json":
                    temp_path = Path(path).parent / f".{Path(path).name}.left.tmp"
                    temp_path.write_text(json.dumps(data), encoding="utf-8")
                    temp_paths.append(temp_path)
                    raise OSError("pre-replace")
                original_write(path, data)

            with mock.patch("real_task_change_runner.write_json_atomic", side_effect=temp_artifact_write):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            self.assertTrue(temp_paths)
            self.assertTrue(temp_paths[0].exists())
            self.assertFalse((Path(handle.runDirectory) / "WORKSPACE_DIFF_REPORT.json").exists())
            reused = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(reused.finalVerdict, "BLOCKED")
            self.assertEqual(reused.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")

        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_replaceassert_12345678")
            with mock.patch("pathlib.Path.replace", side_effect=OSError("replace failed")):
                result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            self.assertFalse((Path(handle.runDirectory) / "CHANGE_FINAL_INVENTORY.json").exists())

        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_existingbadfinal_12345678")
            final_path = Path(handle.runDirectory) / "FINAL_CHANGE_ANALYSIS_REPORT.json"
            final_path.write_text("{", encoding="utf-8")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_REPORT_WRITE_FAILED")
            self.assertEqual(final_path.read_text(encoding="utf-8"), "{")
            with self.assertRaises(ValueError):
                read_json(final_path)
            reused = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(reused.finalVerdict, "BLOCKED")
            self.assertEqual(reused.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")

    def test_missing_registry_and_capability_cases_block(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_registrymissing_12345678")
            real_task_change_runner._TRACKING_REGISTRY.pop(handle.trackingId)
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_CONTEXT_MISMATCH")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_capability_12345678")
            wrong = replace(handle, capability="0" * 64)
            result = finalize_change_analysis(wrong, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_CONTEXT_MISMATCH")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_capdigest_12345678")
            real_task_change_runner._TRACKING_REGISTRY[handle.trackingId]["capability"] = "1" * 64
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_CONTEXT_MISMATCH")
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_finalized_12345678")
            real_task_change_runner._TRACKING_REGISTRY[handle.trackingId]["finalized"] = True
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "BLOCKED")
            self.assertEqual(result.report["errorCode"], "REAL_TASK_CHANGE_HANDLE_REUSED")

    def test_report_schemas_are_strict_and_accept_payloads(self) -> None:
        with self.synthetic_parent() as parent:
            _, handle = self.prepared_handle(parent, "foundation_change_schema_12345678")
            result = finalize_change_analysis(handle, parent, ROOT / "CodexAutomation", CONFIG)
            self.assertEqual(result.finalVerdict, "PASS")
            schemas = {
                "CHANGE_TRACKING_REPORT.json": "real_task_change_tracking.schema.json",
                "CHANGE_BASELINE_INVENTORY.json": "real_task_change_inventory.schema.json",
                "CHANGE_FINAL_INVENTORY.json": "real_task_change_inventory.schema.json",
                "WORKSPACE_DIFF_REPORT.json": "real_task_workspace_diff.schema.json",
                "CHANGE_POLICY_REPORT.json": "real_task_change_policy.schema.json",
                "FINAL_CHANGE_ANALYSIS_REPORT.json": "real_task_change_final.schema.json",
            }
            for report_name, schema_name in schemas.items():
                schema = read_json(ROOT / "CodexAutomation" / "schemas" / schema_name)
                payload = read_json(Path(handle.runDirectory) / report_name)
                self.assertFalse(validate_schema_instance(payload, schema), report_name)
                mutated = json.loads(json.dumps(payload))
                mutated["unknown"] = True
                self.assertTrue(validate_schema_instance(mutated, schema), report_name)

    def prepared_handle(self, parent: Path, run_id: str = "foundation_change_base_12345678"):
        manifest = self.write_manifest(
            parent,
            source_paths=["Assets/TestFixture"],
            write_paths=["Assets/TestFixture/input.txt"],
            delete_paths=[],
            expected_outputs=[{"path": "Assets/TestFixture/input.txt", "kind": "file", "required": True}],
        )
        prep = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, run_id)
        self.assertEqual(prep["finalVerdict"], "PASS")
        handle = begin_change_tracking(prep, parent, ROOT / "CodexAutomation", CONFIG)
        self.assertEqual(handle.stage, REAL_TASK_CHANGE_STAGE)
        return prep, handle

    def write_manifest(self, parent: Path, source_paths: list[str], write_paths: list[str], delete_paths: list[str], expected_outputs: list[dict[str, object]], limits: dict[str, int] | None = None) -> Path:
        manifest = {
            "schemaVersion": 1,
            "taskId": "TASK-CHANGE-001",
            "title": "Change Analysis Test",
            "objective": "Analyze isolated workspace changes.",
            "taskType": "isolated_code_change",
            "sourcePaths": source_paths,
            "allowedWritePaths": write_paths,
            "allowedDeletePaths": delete_paths,
            "expectedOutputs": expected_outputs,
            "validationPlan": [{"id": "changed", "type": "changed_paths_subset", "paths": write_paths + delete_paths}],
            "completionCriteria": ["Change analysis completes."],
            "roleBudget": {"maxInvocations": 3},
            "repairPolicy": {"maxAttempts": 1},
            "limits": limits or {"maxChangedFiles": 50, "maxChangedBytes": 10485760, "maxSingleChangedFileBytes": 2097152},
            "metadata": {},
        }
        path = parent / "task_manifest.json"
        path.write_text(json.dumps(manifest), encoding="utf-8")
        return path

    def synthetic_parent(self):
        return _SyntheticParent()

    def assert_no_execution_flags(self, report: dict[str, object]) -> None:
        self.assertEqual(report["codexInvocationCount"], 0)
        self.assertFalse(report["modelInvocationStarted"])
        self.assertFalse(report["sandboxStarted"])
        self.assertFalse(report["unityStarted"])
        self.assertFalse(report["networkUsed"])

    def first_source_file(self, report: dict[str, object]) -> dict[str, object]:
        for entry in report["entries"]:
            if entry["type"] == "file":
                return entry
        raise AssertionError("source inventory has no file entry")

    def first_source_directory(self, report: dict[str, object]) -> dict[str, object]:
        for entry in report["entries"]:
            if entry["type"] == "directory":
                return entry
        raise AssertionError("source inventory has no directory entry")

    def rewrite_trusted_source_entry_cap(self, parent: Path, run_id: str, max_inventory_entries: int) -> str:
        run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / run_id
        context_path = run_dir / "TRUSTED_RUN_CONTEXT.json"
        final_path = run_dir / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
        context = read_json(context_path)
        final_report = read_json(final_path)
        context["sourceLimits"]["maxInventoryEntries"] = max_inventory_entries
        trusted_hash = canonical_sha256(context)
        final_report["trustedContextHash"] = trusted_hash
        context_path.write_text(json.dumps(context), encoding="utf-8")
        final_path.write_text(json.dumps(final_report), encoding="utf-8")
        return trusted_hash


class _SyntheticParent:
    def __enter__(self) -> Path:
        self.temp = tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime")
        self.root = Path(self.temp.name)
        fixture = self.root / "Assets" / "TestFixture"
        fixture.mkdir(parents=True)
        (fixture / "input.txt").write_text("line one\n", encoding="utf-8")
        (fixture / "delete_me.txt").write_text("remove me\n", encoding="utf-8")
        (self.root / "AGENTS.md").write_text("# Parent Rules\n", encoding="utf-8")
        (self.root / "CodexAutomation").mkdir()
        (self.root / "CodexAutomation" / ".gitignore").write_text("runtime/\n", encoding="utf-8")
        subprocess.run(["git", "init"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "config", "user.email", "change@example.invalid"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "config", "user.name", "Change Test"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "add", "AGENTS.md", "Assets", "CodexAutomation/.gitignore"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "commit", "-m", "synthetic parent baseline"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        return self.root

    def __exit__(self, exc_type, exc, tb) -> None:
        self.temp.cleanup()


if __name__ == "__main__":
    unittest.main()
