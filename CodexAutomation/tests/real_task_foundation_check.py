from __future__ import annotations

import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
from parent_git_snapshot import parent_git_snapshot  # noqa: E402
import real_task_foundation_runner  # noqa: E402
import real_task_report_writer  # noqa: E402
from real_task_execution_context import create_execution_context  # noqa: E402
from real_task_execution_models import RealTaskExecutionFailure  # noqa: E402
from real_task_foundation_models import canonical_sha256, new_foundation_run_id, parse_foundation_policy  # noqa: E402
from real_task_foundation_runner import prepare_foundation_workspace, write_validated_trusted_context  # noqa: E402
from real_task_report_writer import read_trusted_report  # noqa: E402
from real_task_workspace import (  # noqa: E402
    WorkspacePaths,
    copy_sources,
    create_run_paths,
    git_fingerprint,
    validate_existing_source_file_component_chain,
    verify_destination_matches_source,
)
from schema_validator import validate as validate_schema_instance  # noqa: E402
from task_manifest_validator import parse_real_task_policy  # noqa: E402
from workspace_inventory import InventoryError, build_source_inventory, file_sha256  # noqa: E402


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class RealTaskFoundationCheck(unittest.TestCase):
    def test_valid_foundation_config_accepted(self) -> None:
        real_policy, real_errors = parse_real_task_policy(CONFIG)
        self.assertFalse(real_errors)
        policy, errors = parse_foundation_policy(ROOT, CONFIG, real_policy)
        self.assertFalse(errors, [error.to_dict() for error in errors])
        self.assertIsNotNone(policy)
        self.assertTrue(policy.retainFailedStaging)
        self.assertTrue(policy.preserveEmptyDirectories)
        self.assertFalse(policy.autoPairUnityMeta)
        self.assertTrue(policy.createStandaloneGit)
        self.assertFalse(policy.createBaselineCommit)

    def test_foundation_config_rejects_missing_unknown_string_bool_bool_int_and_baseline_commit(self) -> None:
        cases = []
        config = json.loads(json.dumps(CONFIG))
        config["realTaskFoundation"].pop("runtimeRoot")
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTaskFoundation"]["unknown"] = True
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTaskFoundation"]["retainFailedStaging"] = "true"
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTaskFoundation"]["copyChunkBytes"] = True
        cases.append(config)
        config = json.loads(json.dumps(CONFIG))
        config["realTaskFoundation"]["createBaselineCommit"] = True
        cases.append(config)
        for item in cases:
            with self.subTest(item=item["realTaskFoundation"]):
                policy, errors = parse_foundation_policy(ROOT, item)
                self.assertIsNone(policy)
                self.assertTrue(errors)

    def test_foundation_config_rejects_unsafe_runtime_root_and_small_inventory_limit(self) -> None:
        real_policy, errors = parse_real_task_policy(CONFIG)
        self.assertFalse(errors)
        for runtime_root in [".", "CodexAutomation", "Assets/runtime", "../outside", "C:/outside"]:
            config = json.loads(json.dumps(CONFIG))
            config["realTaskFoundation"]["runtimeRoot"] = runtime_root
            with self.subTest(runtime_root=runtime_root):
                self.assertTrue(parse_foundation_policy(ROOT, config, real_policy)[1])
        config = json.loads(json.dumps(CONFIG))
        config["realTaskFoundation"]["maxInventoryEntries"] = 1
        self.assertTrue(parse_foundation_policy(ROOT, config, real_policy)[1])

    def test_fresh_run_ids_unique(self) -> None:
        ids = {new_foundation_run_id() for _ in range(20)}
        self.assertEqual(len(ids), 20)
        self.assertTrue(all(item.startswith("foundation_") for item in ids))

    def test_existing_run_directory_rejected(self) -> None:
        with self.synthetic_parent() as parent:
            policy = self.foundation_policy()
            run_id = "foundation_existing_12345678"
            existing = parent / policy.runtimeRoot / run_id
            existing.mkdir(parents=True)
            with self.assertRaises(InventoryError) as raised:
                create_run_paths(parent, policy, run_id)
            self.assertEqual(raised.exception.code, "REAL_TASK_RUN_DIRECTORY_EXISTS")

    def test_context_hash_stable_and_old_plan_report_not_authorization(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            old_plan = parent / "CodexAutomation" / "runtime" / "real_task_plans" / "old" / "REAL_TASK_PLAN_REPORT.json"
            old_plan.parent.mkdir(parents=True)
            old_plan.write_text(json.dumps({"finalVerdict": "PASS", "sourceCopied": True}), encoding="utf-8")
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_context_12345678")
            self.assertEqual(report["finalVerdict"], "PASS")
            context_path = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "TRUSTED_RUN_CONTEXT.json"
            context = read_json(context_path)
            self.assertEqual(canonical_sha256(context), report["trustedContextHash"])
            self.assertNotEqual(context["runId"], "old")

    def test_trusted_context_writer_validates_writes_rereads_and_rejects_overwrite(self) -> None:
        with self.synthetic_parent() as parent:
            manifest_path = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest_path, "foundation_contextwrite_12345678")
            args = self.context_writer_args(parent, report["runId"], manifest_path)
            context = read_json(args["context_path"])
            copy_path = args["run_paths"].run_directory / "TRUSTED_RUN_CONTEXT_COPY.json"
            context_hash = write_validated_trusted_context(path=copy_path, payload=context, **args["writer_kwargs"])
            self.assertEqual(context_hash, canonical_sha256(read_json(copy_path)))
            with self.assertRaises(Exception):
                write_validated_trusted_context(path=copy_path, payload=context, **args["writer_kwargs"])

    def test_trusted_context_rejects_structure_semantics_size_and_reread_mismatch(self) -> None:
        with self.synthetic_parent() as parent:
            manifest_path = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest_path, "foundation_contextbad_12345678")
            args = self.context_writer_args(parent, report["runId"], manifest_path)
            context = read_json(args["context_path"])
            cases = [
                ("unknown_root", {"extra": True}),
                ("unknown_host", {"effectiveHostPolicy": {**context["effectiveHostPolicy"], "extra": True}}),
                ("unknown_foundation", {"effectiveFoundationPolicy": {**context["effectiveFoundationPolicy"], "extra": True}}),
                ("wrong_run", {"runId": "foundation_wrong_12345678"}),
                ("wrong_task", {"taskId": "TASK-WRONG"}),
                ("wrong_stage", {"stage": "BOOTSTRAP-03B-2B-B"}),
                ("wrong_hash", {"manifestSha256": "0" * 64}),
                ("no_model_false", {"noModel": False}),
                ("failed_git", {"parentGitInitialSnapshot": {**context["parentGitInitialSnapshot"], "status": "failed"}}),
                ("bool_limit", {"sourceLimits": {**context["sourceLimits"], "maxSourceFiles": True}}),
            ]
            for suffix, update in cases:
                mutated = json.loads(json.dumps(context))
                mutated.update(update)
                path = args["run_paths"].run_directory / f"TRUSTED_RUN_CONTEXT_{suffix}.json"
                with self.subTest(suffix=suffix):
                    with self.assertRaises(Exception):
                        write_validated_trusted_context(path=path, payload=mutated, **args["writer_kwargs"])
                    self.assertFalse(path.exists())
            small_config = json.loads(json.dumps(CONFIG))
            small_config["realTaskFoundation"]["maxReportBytes"] = 10
            small_args = dict(args["writer_kwargs"])
            small_args["config"] = small_config
            with self.assertRaises(Exception):
                write_validated_trusted_context(path=args["run_paths"].run_directory / "TRUSTED_RUN_CONTEXT_TOO_BIG.json", payload=context, **small_args)

            def mismatching_read(path: Path):
                if path.name == "TRUSTED_RUN_CONTEXT_REREAD.json":
                    mutated = json.loads(json.dumps(context))
                    mutated["taskId"] = "TASK-MISMATCH"
                    return mutated
                return read_json(path)

            with mock.patch("real_task_foundation_runner.read_json", side_effect=mismatching_read):
                with self.assertRaises(Exception):
                    write_validated_trusted_context(path=args["run_paths"].run_directory / "TRUSTED_RUN_CONTEXT_REREAD.json", payload=context, **args["writer_kwargs"])

    def test_trusted_context_rejects_unsafe_runtime_paths_before_write(self) -> None:
        with self.synthetic_parent() as parent:
            manifest_path = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest_path, "foundation_contextpaths_12345678")
            args = self.context_writer_args(parent, report["runId"], manifest_path)
            context = read_json(args["context_path"])
            run_paths = args["run_paths"]
            sibling_run = run_paths.runtime_root / "foundation_contextpaths_sibling"
            sibling_run.mkdir()
            outside_staging = sibling_run / "staging"
            outside_staging.mkdir()
            outside_workspace = sibling_run / "workspace"
            outside_evidence = sibling_run / "evidence"
            outside_logs = sibling_run / "logs"
            outside_evidence.mkdir()
            outside_logs.mkdir()
            runtime_mismatch = parent / "CodexAutomation" / "runtime" / "different_root"
            runtime_mismatch.mkdir()
            cases = [
                ("staging_escape", "stagingDirectory", outside_staging, {"REAL_TASK_CONTEXT_MISMATCH", "REAL_TASK_RUNTIME_PATH_UNSAFE"}),
                ("workspace_mismatch", "workspaceDirectory", outside_workspace, {"REAL_TASK_CONTEXT_MISMATCH", "REAL_TASK_RUNTIME_PATH_UNSAFE"}),
                ("evidence_mismatch", "evidenceDirectory", outside_evidence, {"REAL_TASK_CONTEXT_MISMATCH", "REAL_TASK_RUNTIME_PATH_UNSAFE"}),
                ("logs_mismatch", "logsDirectory", outside_logs, {"REAL_TASK_CONTEXT_MISMATCH", "REAL_TASK_RUNTIME_PATH_UNSAFE"}),
                ("runtime_root_mismatch", "runtimeRoot", runtime_mismatch, {"REAL_TASK_CONTEXT_MISMATCH", "REAL_TASK_RUNTIME_PATH_UNSAFE"}),
            ]
            for suffix, field, value, expected_codes in cases:
                mutated = json.loads(json.dumps(context))
                mutated[field] = str(value.resolve(strict=False))
                path = run_paths.run_directory / f"TRUSTED_RUN_CONTEXT_{suffix}.json"
                with self.subTest(suffix=suffix):
                    with self.assertRaises(Exception) as raised:
                        write_validated_trusted_context(path=path, payload=mutated, **args["writer_kwargs"])
                    self.assertIn(raised.exception.code, expected_codes)
                    self.assertFalse(path.exists())

    def test_source_inventory_file_directory_hidden_empty_meta_and_binary(self) -> None:
        with self.synthetic_parent() as parent:
            fixture = parent / "Assets" / "TestFixture"
            (fixture / ".hidden").write_text("hidden", encoding="utf-8")
            (fixture / "empty").mkdir()
            (fixture / "thing.prefab.meta").write_text("guid: abc\n", encoding="utf-8")
            (fixture / "binary.bin").write_bytes(b"\x00\xff\x10")
            inventory = build_source_inventory(parent, ["Assets/TestFixture"], 100, 1024)
            paths = {entry["path"]: entry for entry in inventory["entries"]}
            self.assertIn("Assets/TestFixture/.hidden", paths)
            self.assertIn("Assets/TestFixture/empty", paths)
            self.assertEqual(paths["Assets/TestFixture/empty"]["type"], "directory")
            self.assertIn("Assets/TestFixture/thing.prefab.meta", paths)
            self.assertIn("Assets/TestFixture/binary.bin", paths)
            self.assertEqual(paths["Assets/TestFixture/binary.bin"]["sha256"], file_sha256(fixture / "binary.bin", 1024))

    def test_adjacent_meta_not_auto_added_for_exact_file_source(self) -> None:
        with self.synthetic_parent() as parent:
            fixture = parent / "Assets" / "TestFixture"
            (fixture / "input.txt.meta").write_text("guid: input\n", encoding="utf-8")
            inventory = build_source_inventory(parent, ["Assets/TestFixture/input.txt"], 100, 1024)
            self.assertEqual([entry["path"] for entry in inventory["entries"]], ["Assets/TestFixture/input.txt"])

    def test_inventory_rejects_case_collision_and_limit(self) -> None:
        with self.synthetic_parent() as parent:
            fixture = parent / "Assets" / "TestFixture"
            (fixture / "Case.txt").write_text("a", encoding="utf-8")
            (fixture / "case.txt").write_text("b", encoding="utf-8")
            if len({item.name for item in fixture.iterdir() if item.name.lower() == "case.txt"}) < 2:
                self.skipTest("Case collision cannot be represented on this filesystem.")
            with self.assertRaises(InventoryError) as raised:
                build_source_inventory(parent, ["Assets/TestFixture"], 100, 1024)
            self.assertEqual(raised.exception.code, "REAL_TASK_PATH_COLLISION")
            with self.assertRaises(InventoryError) as limit_raised:
                build_source_inventory(parent, ["Assets/TestFixture/input.txt"], 0, 1024)
            self.assertEqual(limit_raised.exception.code, "REAL_TASK_SOURCE_LIMIT_EXCEEDED")

    def test_inventory_rejects_symlink_when_supported(self) -> None:
        with self.synthetic_parent() as parent:
            source = parent / "Assets" / "TestFixture" / "input.txt"
            link = parent / "Assets" / "TestFixture" / "link.txt"
            try:
                link.symlink_to(source)
            except OSError:
                self.skipTest("Symlink creation is unavailable in this Windows session.")
            with self.assertRaises(InventoryError):
                build_source_inventory(parent, ["Assets/TestFixture"], 100, 1024)

    def test_exact_file_copy_preserves_bytes_and_line_endings(self) -> None:
        with self.synthetic_parent() as parent:
            fixture = parent / "Assets" / "TestFixture"
            (fixture / "input.txt").write_bytes(b"a\r\nb\n")
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_copy_12345678")
            workspace_file = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "workspace" / "Assets" / "TestFixture" / "input.txt"
            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertEqual(workspace_file.read_bytes(), b"a\r\nb\n")
            self.assertFalse((workspace_file.parent / "input.txt.meta").exists())

    def test_directory_copy_preserves_empty_directories_hidden_and_meta(self) -> None:
        with self.synthetic_parent() as parent:
            fixture = parent / "Assets" / "TestFixture"
            (fixture / ".hidden").write_text("hidden", encoding="utf-8")
            (fixture / "empty").mkdir()
            (fixture / "input.txt.meta").write_text("guid: input\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_dir_12345678")
            workspace = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "workspace"
            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertTrue((workspace / "Assets" / "TestFixture" / ".hidden").exists())
            self.assertTrue((workspace / "Assets" / "TestFixture" / "empty").is_dir())
            self.assertTrue((workspace / "Assets" / "TestFixture" / "input.txt.meta").exists())

    def test_destination_verification_rejects_extra_files_and_directories(self) -> None:
        with self.synthetic_parent() as parent:
            inventory = build_source_inventory(parent, ["Assets/TestFixture"], 100, 1024)
            staging = parent / "CodexAutomation" / "runtime" / "verify_extra" / "staging"
            staging.mkdir(parents=True)
            copy_sources(parent, staging, inventory, 1024, set())
            verify_destination_matches_source(staging, inventory, 100, 1024, set())
            (staging / "Assets" / "TestFixture" / "extra.txt").write_text("extra", encoding="utf-8")
            with self.assertRaises(InventoryError) as extra_file:
                verify_destination_matches_source(staging, inventory, 100, 1024, set())
            self.assertEqual(extra_file.exception.code, "REAL_TASK_SOURCE_HASH_MISMATCH")
            (staging / "Assets" / "TestFixture" / "extra.txt").unlink()
            (staging / "Assets" / "TestFixture" / "extra_empty").mkdir()
            with self.assertRaises(InventoryError):
                verify_destination_matches_source(staging, inventory, 100, 1024, set())
            (staging / "Assets" / "TestFixture" / "extra_empty").rmdir()
            (staging / "Assets" / "TestFixture" / "extra_nonempty").mkdir()
            (staging / "Assets" / "TestFixture" / "extra_nonempty" / "child.txt").write_text("child", encoding="utf-8")
            with self.assertRaises(InventoryError):
                verify_destination_matches_source(staging, inventory, 100, 1024, set())

    def test_destination_verification_rejects_missing_and_type_mismatched_expected_paths(self) -> None:
        with self.synthetic_parent() as parent:
            (parent / "Assets" / "TestFixture" / "empty").mkdir()
            inventory = build_source_inventory(parent, ["Assets/TestFixture"], 100, 1024)
            staging = parent / "CodexAutomation" / "runtime" / "verify_missing" / "staging"
            staging.mkdir(parents=True)
            copy_sources(parent, staging, inventory, 1024, set())
            (staging / "Assets" / "TestFixture" / "empty").rmdir()
            with self.assertRaises(InventoryError):
                verify_destination_matches_source(staging, inventory, 100, 1024, set())
            copy_sources(parent, staging, inventory, 1024, set())
            (staging / "Assets" / "TestFixture" / "empty").rmdir()
            (staging / "Assets" / "TestFixture" / "empty").write_text("not dir", encoding="utf-8")
            with self.assertRaises(InventoryError):
                verify_destination_matches_source(staging, inventory, 100, 1024, set())
            (staging / "Assets" / "TestFixture" / "input.txt").unlink()
            (staging / "Assets" / "TestFixture" / "input.txt").mkdir()
            with self.assertRaises(InventoryError):
                verify_destination_matches_source(staging, inventory, 100, 1024, set())

    def test_destination_verification_allows_registered_services_only(self) -> None:
        with self.synthetic_parent() as parent:
            inventory = build_source_inventory(parent, ["Assets/TestFixture/input.txt"], 100, 1024)
            staging = parent / "CodexAutomation" / "runtime" / "verify_services" / "staging"
            staging.mkdir(parents=True)
            copy_sources(parent, staging, inventory, 1024, set())
            (staging / ".agents").mkdir()
            (staging / ".git" / "objects").mkdir(parents=True)
            verify_destination_matches_source(staging, inventory, 100, 1024, {".agents", ".git"})
            (staging / ".codex").mkdir()
            with self.assertRaises(InventoryError):
                verify_destination_matches_source(staging, inventory, 100, 1024, {".agents", ".git"})

    def test_source_component_chain_validation_blocks_unsafe_components(self) -> None:
        with self.synthetic_parent() as parent:
            source = parent / "Assets" / "TestFixture" / "input.txt"
            validate_existing_source_file_component_chain(parent, source, ("Assets", "Packages", "ProjectSettings"))
            with self.assertRaises(InventoryError):
                validate_existing_source_file_component_chain(parent, parent / "CodexAutomation" / ".gitignore", ("Assets",))
            with mock.patch("real_task_workspace.is_symlink_or_reparse", side_effect=lambda path: path.name == "TestFixture"):
                with self.assertRaises(InventoryError) as raised:
                    validate_existing_source_file_component_chain(parent, source, ("Assets", "Packages", "ProjectSettings"))
                self.assertEqual(raised.exception.code, "REAL_TASK_SOURCE_REPARSE")
            missing = parent / "Assets" / "Missing" / "input.txt"
            with self.assertRaises(InventoryError):
                validate_existing_source_file_component_chain(parent, missing, ("Assets", "Packages", "ProjectSettings"))

    def test_source_chain_rechecked_after_copy_and_blocks_promotion(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            calls = {"count": 0}

            def chain_check(root: Path, source: Path, allowed_source_roots: tuple[str, ...]) -> None:
                calls["count"] += 1
                if calls["count"] >= 2:
                    raise InventoryError("REAL_TASK_SOURCE_CHANGED", "component changed")

            with mock.patch("real_task_workspace.validate_existing_source_file_component_chain", side_effect=chain_check):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_chain_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            self.assertEqual(report["errorCode"], "REAL_TASK_SOURCE_CHANGED")
            self.assertFalse((run_dir / "workspace").exists())
            self.assertFalse(report["workspaceReady"])

    def test_source_leaf_symlink_replacement_at_copy_recheck_blocks_promotion(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            source = parent / "Assets" / "TestFixture" / "input.txt"
            leaf_checks = {"count": 0}

            def symlink_after_first_leaf_validation(path: Path) -> bool:
                if path == source:
                    leaf_checks["count"] += 1
                    return leaf_checks["count"] >= 2
                return False

            with mock.patch("real_task_workspace.is_symlink_or_reparse", side_effect=symlink_after_first_leaf_validation):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_leafsymlink_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            destination = run_dir / "staging" / "Assets" / "TestFixture" / "input.txt"
            self.assertGreaterEqual(leaf_checks["count"], 2)
            self.assertEqual(report["errorCode"], "REAL_TASK_SOURCE_REPARSE")
            self.assertEqual(report["finalVerdict"], "FAILED")
            self.assertTrue((run_dir / "staging").exists())
            self.assertFalse((run_dir / "workspace").exists())
            self.assertFalse(destination.exists())
            self.assertFalse(report["workspaceReady"])
            self.assertFalse(report["sourceCopyVerified"])

    def test_failed_copy_retains_staging_and_no_workspace(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            with mock.patch("real_task_workspace._copy_verified_file", side_effect=InventoryError("REAL_TASK_SOURCE_COPY_FAILED", "copy failed")):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_failcopy_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            self.assertEqual(report["finalVerdict"], "FAILED")
            self.assertEqual(report["errorCode"], "REAL_TASK_SOURCE_COPY_FAILED")
            self.assertTrue((run_dir / "staging").exists())
            self.assertFalse((run_dir / "workspace").exists())
            self.assertFalse(report["workspaceReady"])

    def test_source_file_changed_during_copy_rejected(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            original = file_sha256

            def changing_hash(path: Path, chunk_bytes: int = 1024 * 1024) -> str:
                if path.name == "input.txt" and path.exists():
                    path.write_text("changed", encoding="utf-8")
                return original(path, chunk_bytes)

            with mock.patch("real_task_workspace.file_sha256", side_effect=changing_hash):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_race_12345678")
            self.assertEqual(report["finalVerdict"], "FAILED")
            self.assertIn(report["errorCode"], {"REAL_TASK_SOURCE_CHANGED", "REAL_TASK_SOURCE_HASH_MISMATCH"})

    def test_source_directory_gains_child_between_pre_and_post_rejected(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture"], ["Assets/TestFixture/input.txt"])
            original = build_source_inventory
            calls = {"count": 0}

            def inventory_with_added_child(root: Path, source_paths: list[str], max_entries: int, chunk_bytes: int, service_paths: set[str] | None = None):
                calls["count"] += 1
                if calls["count"] == 2:
                    (root / "Assets" / "TestFixture" / "new_child.txt").write_text("new", encoding="utf-8")
                return original(root, source_paths, max_entries, chunk_bytes, service_paths)

            with mock.patch("real_task_foundation_runner.build_source_inventory", side_effect=inventory_with_added_child):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_gain_12345678")
            self.assertEqual(report["errorCode"], "REAL_TASK_SOURCE_CHANGED")
            self.assertFalse(report["workspaceReady"])

    def test_service_files_and_parent_agents_policy(self) -> None:
        with self.synthetic_parent() as parent:
            (parent / "Assets" / "AGENTS.md").write_text("# Asset rules\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_service_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            workspace = run_dir / "workspace"
            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertTrue((workspace / "AGENTS.md").read_text(encoding="utf-8").startswith("# Host-Controlled"))
            self.assertTrue((workspace / ".agents").is_dir())
            self.assertEqual(list((workspace / ".agents").iterdir()), [])
            self.assertTrue((workspace / "task.json").exists())
            self.assertTrue((workspace / "effective_policy.json").exists())
            self.assertTrue((run_dir / "evidence" / "PARENT_ROOT_AGENTS.md").exists())
            self.assertFalse((workspace / "PARENT_ROOT_AGENTS.md").exists())
            self.assertTrue((workspace / "Assets" / "AGENTS.md").exists())
            baseline = read_json(run_dir / "WORKSPACE_BASELINE_INVENTORY.json")
            registry = {item["path"]: item for item in baseline["serviceRegistry"]}
            self.assertEqual(registry["Assets/AGENTS.md"]["mutability"], "immutable")
            self.assertEqual(registry["Assets/AGENTS.md"]["origin"], "ancestor_project_instruction")
            self.assertEqual(registry[".agents"]["origin"], "empty_agents_directory")

    def test_agents_origin_separates_ancestor_and_naturally_copied_sources(self) -> None:
        with self.synthetic_parent() as parent:
            (parent / "Assets" / "TestFixture" / "AGENTS.md").write_text("# Nested\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_agentsnested_12345678")
            baseline = read_json(parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "WORKSPACE_BASELINE_INVENTORY.json")
            registry = {item["path"]: item for item in baseline["serviceRegistry"]}
            self.assertEqual(registry["AGENTS.md"]["origin"], "host_generated")
            self.assertEqual(registry["Assets/TestFixture/AGENTS.md"]["origin"], "copied_project_instruction")
            self.assertNotIn("PARENT_ROOT_AGENTS.md", registry)

        with self.synthetic_parent() as parent:
            (parent / "Assets" / "TestFixture" / "AGENTS.md").write_text("# Exact\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture/AGENTS.md"], ["Assets/TestFixture/AGENTS.md"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_agentsexact_12345678")
            baseline = read_json(parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "WORKSPACE_BASELINE_INVENTORY.json")
            registry = {item["path"]: item for item in baseline["serviceRegistry"]}
            self.assertEqual(registry["Assets/TestFixture/AGENTS.md"]["origin"], "copied_project_instruction")

    def test_agents_natural_copy_and_ancestor_overlap_dedupes_to_copied_origin(self) -> None:
        with self.synthetic_parent() as parent:
            agents = parent / "Assets" / "TestFixture" / "AGENTS.md"
            agents.write_text("# Overlap\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_agentsoverlap_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            baseline = read_json(run_dir / "WORKSPACE_BASELINE_INVENTORY.json")
            entries = [item for item in baseline["serviceRegistry"] if item["path"] == "Assets/TestFixture/AGENTS.md"]
            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertEqual(len(entries), 1)
            self.assertEqual(entries[0]["origin"], "copied_project_instruction")
            self.assertEqual(entries[0]["expectedSha256"], file_sha256(agents, CONFIG["realTaskFoundation"]["copyChunkBytes"]))
            self.assertEqual(entries[0]["workspacePath"], "Assets/TestFixture/AGENTS.md")

    def test_agents_same_hash_duplicate_path_keeps_single_immutable_entry(self) -> None:
        with self.synthetic_parent() as parent:
            agents = parent / "Assets" / "TestFixture" / "AGENTS.md"
            agents.write_text("# Same hash\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture/AGENTS.md"], ["Assets/TestFixture/AGENTS.md"])

            def duplicate_ancestor_agents(root: Path, source_paths: list[str], allowed_roots: tuple[str, ...], chunk_bytes: int):
                return [
                    {"path": "Assets/TestFixture/AGENTS.md", "sha256": file_sha256(agents, chunk_bytes), "origin": "ancestor_project_instruction"},
                    {"path": "Assets/TestFixture/AGENTS.md", "sha256": file_sha256(agents, chunk_bytes), "origin": "ancestor_project_instruction"},
                ]

            with mock.patch("real_task_foundation_runner.expand_ancestor_agents", side_effect=duplicate_ancestor_agents):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_agentssamehash_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            baseline = read_json(run_dir / "WORKSPACE_BASELINE_INVENTORY.json")
            entries = [item for item in baseline["serviceRegistry"] if item["path"] == "Assets/TestFixture/AGENTS.md"]
            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertEqual(len(entries), 1)
            self.assertEqual(entries[0]["origin"], "copied_project_instruction")
            self.assertEqual(entries[0]["mutability"], "immutable")
            self.assertEqual(entries[0]["expectedSha256"], file_sha256(agents, CONFIG["realTaskFoundation"]["copyChunkBytes"]))

    def test_agents_duplicate_path_different_hash_fails_closed(self) -> None:
        with self.synthetic_parent() as parent:
            agents = parent / "Assets" / "TestFixture" / "AGENTS.md"
            agents.write_text("# Original\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture/AGENTS.md"], ["Assets/TestFixture/AGENTS.md"])
            original_add = real_task_foundation_runner.add_project_instruction_services
            calls = {"count": 0}

            def mutate_before_second_registration(staging: Path, paths: list[str], origin: str, service_registry: list[dict[str, object]], chunk_bytes: int):
                calls["count"] += 1
                if calls["count"] == 2:
                    (staging / "Assets" / "TestFixture" / "AGENTS.md").write_text("# Mutated\n", encoding="utf-8")
                    paths = ["Assets/TestFixture/AGENTS.md"]
                return original_add(staging, paths, origin, service_registry, chunk_bytes)

            with mock.patch("real_task_foundation_runner.add_project_instruction_services", side_effect=mutate_before_second_registration):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_agentsconflict_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            self.assertEqual(report["errorCode"], "REAL_TASK_SERVICE_FILE_INVALID")
            self.assertEqual(report["finalVerdict"], "FAILED")
            self.assertTrue((run_dir / "staging").exists())
            self.assertFalse((run_dir / "workspace").exists())
            self.assertFalse(report["workspaceReady"])

    def test_root_agents_excluded_and_host_generated_workspace_root_preserved(self) -> None:
        with self.synthetic_parent() as parent:
            (parent / "Assets" / "AGENTS.md").write_text("# Asset\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["Assets/TestFixture"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_agentsroot_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            workspace = run_dir / "workspace"
            baseline = read_json(run_dir / "WORKSPACE_BASELINE_INVENTORY.json")
            registry = {item["path"]: item for item in baseline["serviceRegistry"]}
            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertEqual(registry["AGENTS.md"]["origin"], "host_generated")
            self.assertTrue((workspace / "AGENTS.md").read_text(encoding="utf-8").startswith("# Host-Controlled"))
            self.assertNotIn("PARENT_ROOT_AGENTS.md", registry)
            self.assertNotIn("PARENT_ROOT_AGENTS.md", {entry["path"] for entry in baseline["entries"]})
            self.assertEqual(registry["Assets/AGENTS.md"]["origin"], "ancestor_project_instruction")

    def test_standalone_git_has_no_baseline_commit_remotes_staged_or_active_hooks(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_git_12345678")
            workspace = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "workspace"
            fingerprint = git_fingerprint(workspace)
            self.assertIsNone(fingerprint["head"])
            self.assertEqual(fingerprint["remotes"], [])
            self.assertEqual(fingerprint["stagedPaths"], [])
            self.assertEqual(fingerprint["activeHooks"], [])
            self.assertFalse(fingerprint["baselineCommitCreated"])
            self.assertFalse((workspace / ".git").is_file())
            baseline = read_json(parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "WORKSPACE_BASELINE_INVENTORY.json")
            self.assertTrue(baseline["isolatedGitFingerprint"]["postPromotionVerified"])
            self.assertEqual(baseline["isolatedGitFingerprint"]["stagingFingerprintSha256"], baseline["isolatedGitFingerprint"]["workspaceFingerprintSha256"])

    def test_post_promotion_git_mismatch_or_failure_blocks_pass(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            good = {
                "gitDirectoryValid": True,
                "gitFilePointerPresent": False,
                "head": None,
                "stagedPaths": [],
                "remotes": [],
                "refs": [],
                "activeHooks": [],
                "configSha256": "a" * 64,
                "baselineCommitCreated": False,
            }
            bad = dict(good)
            bad["configSha256"] = "b" * 64
            with mock.patch("real_task_foundation_runner.git_fingerprint", side_effect=[good, bad]):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_gitmismatch_12345678")
            self.assertEqual(report["errorCode"], "REAL_TASK_ISOLATED_GIT_INVALID")
            self.assertEqual(report["finalVerdict"], "FAILED")
            self.assertFalse(report["isolatedGitValid"])

        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            with mock.patch("real_task_foundation_runner.git_fingerprint", side_effect=[good, InventoryError("REAL_TASK_ISOLATED_GIT_INVALID", "git failed")]):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_gitfailpost_12345678")
            self.assertEqual(report["errorCode"], "REAL_TASK_ISOLATED_GIT_INVALID")
            self.assertEqual(report["finalVerdict"], "FAILED")

    def test_post_promotion_specific_git_mutations_block_pass(self) -> None:
        good = self.clean_git_fingerprint()
        cases = [
            ("remote", {"remotes": ["origin"]}),
            ("staged", {"stagedPaths": ["Assets/TestFixture/input.txt"]}),
            ("hook", {"activeHooks": ["pre-commit"]}),
            ("head_ref", {"head": "f" * 40, "refs": ["refs/heads/main:" + "f" * 40]}),
            ("config", {"configSha256": "b" * 64}),
        ]
        for suffix, mutation in cases:
            with self.subTest(suffix=suffix):
                with self.synthetic_parent() as parent:
                    manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
                    final = dict(good)
                    final.update(mutation)
                    with mock.patch("real_task_foundation_runner.git_fingerprint", side_effect=[good, final]):
                        report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, f"foundation_git_{suffix}_12345678")
                    run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
                    self.assertEqual(report["errorCode"], "REAL_TASK_ISOLATED_GIT_INVALID")
                    self.assertEqual(report["finalVerdict"], "FAILED")
                    self.assertFalse(report["isolatedGitValid"])
                    self.assertFalse(report["workspaceReady"])
                    self.assertTrue((run_dir / "workspace").exists())
                    self.assertFalse((run_dir / "WORKSPACE_BASELINE_INVENTORY.json").exists())
                    self.assert_no_execution_flags(report)

    def test_parent_git_status_change_blocks_promotion(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            original = parent_git_snapshot
            calls = {"count": 0}

            def changing_snapshot(root: Path):
                calls["count"] += 1
                snapshot, errors = original(root)
                if calls["count"] >= 2 and snapshot.get("status") == "success":
                    snapshot = json.loads(json.dumps(snapshot))
                    snapshot["porcelainStatus"] = [" M Assets/TestFixture/input.txt"]
                    snapshot["dirty"] = True
                return snapshot, errors

            with mock.patch("real_task_foundation_runner.parent_git_snapshot", side_effect=changing_snapshot):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_parent_12345678")
            self.assertEqual(report["errorCode"], "REAL_TASK_PARENT_GIT_CHANGED")
            self.assertFalse(report["workspaceReady"])

    def test_parent_git_snapshot_failure_blocks(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            with mock.patch("real_task_foundation_runner.parent_git_snapshot", return_value=({"status": "failed"}, [type("E", (), {"code": "REAL_TASK_GIT_SNAPSHOT_FAILED", "message": "git failed"})()])):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_gitfail_12345678")
            self.assertEqual(report["errorCode"], "REAL_TASK_GIT_SNAPSHOT_FAILED")

    def test_successful_promotion_and_reports(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_pass_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            self.assertEqual(report["finalVerdict"], "PASS")
            self.assertTrue((run_dir / "workspace").exists())
            self.assertFalse((run_dir / "staging").exists())
            self.assertTrue(report["workspaceReady"])
            self.assertFalse(report["stagingRetained"])
            for name in [
                "TRUSTED_RUN_CONTEXT.json",
                "SOURCE_PRE_INVENTORY.json",
                "SOURCE_POST_INVENTORY.json",
                "WORKSPACE_PREPARATION_REPORT.json",
                "SOURCE_COPY_REPORT.json",
                "WORKSPACE_BASELINE_INVENTORY.json",
                "FINAL_WORKSPACE_PREPARATION_REPORT.json",
            ]:
                self.assertTrue((run_dir / name).exists(), name)
            self.assertEqual(report["codexInvocationCount"], 0)
            self.assertFalse(report["modelInvocationStarted"])
            self.assertFalse(report["sandboxStarted"])
            self.assertFalse(report["unityStarted"])
            self.assertFalse(report["networkUsed"])

    def test_report_schemas_accept_success_outputs_and_have_no_empty_property_schemas(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_schema_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            schemas = {
                "TRUSTED_RUN_CONTEXT.json": "real_task_trusted_context.schema.json",
                "SOURCE_PRE_INVENTORY.json": "real_task_source_inventory.schema.json",
                "SOURCE_POST_INVENTORY.json": "real_task_source_inventory.schema.json",
                "WORKSPACE_BASELINE_INVENTORY.json": "real_task_source_inventory.schema.json",
                "WORKSPACE_PREPARATION_REPORT.json": "real_task_workspace_preparation.schema.json",
                "SOURCE_COPY_REPORT.json": "real_task_source_copy_report.schema.json",
                "FINAL_WORKSPACE_PREPARATION_REPORT.json": "real_task_workspace_final.schema.json",
            }
            for report_name, schema_name in schemas.items():
                schema = read_json(ROOT / "CodexAutomation" / "schemas" / schema_name)
                self.assertFalse(self.contains_empty_schema(schema), schema_name)
                self.assertFalse(validate_schema_instance(read_json(run_dir / report_name), schema), report_name)

    def test_strict_inventory_and_trusted_context_schemas_reject_unknown_nested_fields(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent, ["Assets/TestFixture/input.txt"], ["Assets/TestFixture/input.txt"])
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_strictschema_12345678")
            run_dir = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            inventory_schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_source_inventory.schema.json")
            context_schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_trusted_context.schema.json")
            inventory = read_json(run_dir / "SOURCE_PRE_INVENTORY.json")
            context = read_json(run_dir / "TRUSTED_RUN_CONTEXT.json")
            mutated_inventory = json.loads(json.dumps(inventory))
            mutated_inventory["unknown"] = True
            self.assertTrue(validate_schema_instance(mutated_inventory, inventory_schema))
            mutated_inventory = json.loads(json.dumps(inventory))
            mutated_inventory["entries"][0]["unknown"] = True
            self.assertTrue(validate_schema_instance(mutated_inventory, inventory_schema))
            mutated_context = json.loads(json.dumps(context))
            mutated_context["unknown"] = True
            self.assertTrue(validate_schema_instance(mutated_context, context_schema))
            mutated_context = json.loads(json.dumps(context))
            mutated_context["effectiveHostPolicy"]["unknown"] = True
            self.assertTrue(validate_schema_instance(mutated_context, context_schema))
            mutated_context = json.loads(json.dumps(context))
            mutated_context["effectiveFoundationPolicy"]["unknown"] = True
            self.assertTrue(validate_schema_instance(mutated_context, context_schema))
            mutated_context = json.loads(json.dumps(context))
            mutated_context["parentGitInitialSnapshot"]["unknown"] = True
            self.assertTrue(validate_schema_instance(mutated_context, context_schema))
            mutated_context = json.loads(json.dumps(context))
            del mutated_context["sourceLimits"]["maxSourceFiles"]
            self.assertTrue(validate_schema_instance(mutated_context, context_schema))

    def foundation_policy(self):
        real_policy, real_errors = parse_real_task_policy(CONFIG)
        self.assertFalse(real_errors)
        policy, errors = parse_foundation_policy(ROOT, CONFIG, real_policy)
        self.assertFalse(errors)
        self.assertIsNotNone(policy)
        return policy

    def context_writer_args(self, parent: Path, run_id: str, manifest_path: Path) -> dict[str, object]:
        runtime_root = parent / CONFIG["realTaskFoundation"]["runtimeRoot"]
        run_directory = runtime_root / run_id
        run_paths = WorkspacePaths(
            repository_root=parent,
            runtime_root=runtime_root,
            run_directory=run_directory,
            staging=run_directory / "staging",
            workspace=run_directory / "workspace",
            evidence=run_directory / "evidence",
            logs=run_directory / "logs",
        )
        context_path = run_directory / "TRUSTED_RUN_CONTEXT.json"
        context = read_json(context_path)
        manifest = read_json(manifest_path)
        return {
            "context_path": context_path,
            "run_paths": run_paths,
            "writer_kwargs": {
                "root": parent,
                "automation_root": ROOT / "CodexAutomation",
                "config": CONFIG,
                "run_paths": run_paths,
                "run_id": run_id,
                "task_id": context["taskId"],
                "manifest_path": manifest_path,
                "manifest_sha": context["manifestSha256"],
                "manifest": manifest,
                "effective_policy": context["effectiveHostPolicy"],
                "foundation_policy": context["effectiveFoundationPolicy"],
                "parent_initial": context["parentGitInitialSnapshot"],
            },
        }

    def synthetic_parent(self):
        return _SyntheticParent()

    def clean_git_fingerprint(self) -> dict[str, object]:
        return {
            "gitDirectoryValid": True,
            "gitFilePointerPresent": False,
            "head": None,
            "stagedPaths": [],
            "remotes": [],
            "refs": [],
            "activeHooks": [],
            "configSha256": "a" * 64,
            "baselineCommitCreated": False,
        }

    def assert_no_execution_flags(self, report: dict[str, object]) -> None:
        self.assertEqual(report["codexInvocationCount"], 0)
        self.assertFalse(report["modelInvocationStarted"])
        self.assertFalse(report["sandboxStarted"])
        self.assertFalse(report["unityStarted"])
        self.assertFalse(report["networkUsed"])

    def write_manifest(self, parent: Path, source_paths: list[str], write_paths: list[str]) -> Path:
        manifest = {
            "schemaVersion": 1,
            "taskId": "TASK-FOUNDATION-001",
            "title": "Foundation Preparation Test",
            "objective": "Prepare a no-model foundation workspace for tests.",
            "taskType": "isolated_code_change",
            "sourcePaths": source_paths,
            "allowedWritePaths": write_paths,
            "allowedDeletePaths": [],
            "expectedOutputs": [{"path": write_paths[0], "kind": "file", "required": True}],
            "validationPlan": [{"id": "changed", "type": "changed_paths_subset", "paths": write_paths}],
            "completionCriteria": ["Workspace is prepared."],
            "roleBudget": {"maxInvocations": 3},
            "repairPolicy": {"maxAttempts": 1},
            "limits": {"maxChangedFiles": 50, "maxChangedBytes": 10485760, "maxSingleChangedFileBytes": 2097152},
            "metadata": {},
        }
        path = parent / "task_manifest.json"
        path.write_text(json.dumps(manifest), encoding="utf-8")
        return path

    def contains_empty_schema(self, value: object) -> bool:
        if value == {}:
            return True
        if isinstance(value, dict):
            return any(self.contains_empty_schema(item) for item in value.values())
        if isinstance(value, list):
            return any(self.contains_empty_schema(item) for item in value)
        return False


class RealTaskFoundationTrustedFinalReportTests(unittest.TestCase):
    def test_deep_production_foundation_path_writes_trusted_final_report(self) -> None:
        with self.synthetic_parent() as parent:
            config = json.loads(json.dumps(CONFIG))
            config["realTasks"]["allowedSourceRoots"] = list(config["realTasks"]["allowedSourceRoots"]) + ["TaskData"]
            task_data = parent / "TaskData"
            task_data.mkdir()
            (task_data / "i.txt").write_text("line one\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["TaskData/i.txt"], ["TaskData/i.txt"])
            run_id = "foundation_deep_" + ("s" * 101)
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", config, manifest, run_id)
            run_dir = parent / config["realTaskFoundation"]["runtimeRoot"] / report["runId"]
            final_path = run_dir / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            receipt = report["finalReportReceipt"]
            self.assertEqual(report["finalVerdict"], "PASS", report)
            self.assertTrue(report["complete"])
            self.assertTrue(os.path.isfile(self.fs_path(final_path)))
            self.assertEqual(final_path.name, "FINAL_WORKSPACE_PREPARATION_REPORT.json")
            self.assertGreater(len(str(final_path.resolve(strict=False))), 260)
            self.assertEqual(report["finalReportRelativePath"], f"{report['runId']}/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            self.assertEqual(receipt["relativePath"], f"real_task_foundation_runs/{report['runId']}/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            self.assertEqual(receipt["size"], os.stat(self.fs_path(final_path)).st_size)
            trusted = read_trusted_report(
                ROOT / "CodexAutomation",
                parent / "CodexAutomation" / "runtime",
                final_path,
                real_task_foundation_runner.TrustedReportReceipt(**receipt),
                "real_task_workspace_final.schema.json",
                lambda item: real_task_foundation_runner._validate_final_foundation_report(item, self.run_paths(parent, report["runId"]), report["finalReportRelativePath"]),
                config["realTaskFoundation"]["maxReportBytes"],
            )
            self.assertEqual(trusted["finalVerdict"], "PASS")
            self.assertFalse(list(final_path.parent.glob(".FINAL_WORKSPACE_PREPARATION_REPORT.json.*.tmp")))

    def test_existing_final_report_blocks_without_overwrite(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            original_create_run_paths = real_task_foundation_runner.create_run_paths
            existing_text = "pre-existing authority placeholder"

            def create_with_existing(root, policy, run_id):
                paths = original_create_run_paths(root, policy, run_id)
                paths.run_directory.mkdir(parents=True, exist_ok=True)
                (paths.run_directory / "FINAL_WORKSPACE_PREPARATION_REPORT.json").write_text(existing_text, encoding="utf-8")
                return paths

            with mock.patch("real_task_foundation_runner.create_run_paths", side_effect=create_with_existing):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_existing_final_12345678")
            final_path = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            self.assertEqual(report["finalVerdict"], "BLOCKED")
            self.assertEqual(report["errorCode"], "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")
            self.assertEqual(final_path.read_text(encoding="utf-8"), existing_text)
            self.assertIsNone(report["finalReportReceipt"])

    def test_deep_existing_final_report_blocks_without_overwrite(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            original_create_run_paths = real_task_foundation_runner.create_run_paths
            existing_text = "deep pre-existing authority placeholder"
            run_id = "foundation_deep_existing_" + ("d" * 96)

            def create_with_existing(root, policy, requested_run_id):
                paths = original_create_run_paths(root, policy, requested_run_id)
                paths.run_directory.mkdir(parents=True, exist_ok=True)
                final_path = paths.run_directory / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
                with open(self.fs_path(final_path), "w", encoding="utf-8") as handle:
                    handle.write(existing_text)
                return paths

            with mock.patch("real_task_foundation_runner.create_run_paths", side_effect=create_with_existing):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, run_id)
            final_path = parent / CONFIG["realTaskFoundation"]["runtimeRoot"] / report["runId"] / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            self.assertGreater(len(str(final_path.resolve(strict=False))), 260)
            self.assertEqual(report["finalVerdict"], "BLOCKED")
            self.assertEqual(report["errorCode"], "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")
            with open(self.fs_path(final_path), "r", encoding="utf-8") as handle:
                self.assertEqual(handle.read(), existing_text)
            self.assertIsNone(report["finalReportReceipt"])
            self.assertFalse(list(final_path.parent.glob(".FINAL_WORKSPACE_PREPARATION_REPORT.json.*.tmp")))

    def test_existing_directory_at_final_report_path_is_rejected(self) -> None:
        with self.synthetic_parent() as parent:
            paths = self.run_paths(parent, "foundation_existing_directory_12345678")
            paths.run_directory.mkdir(parents=True, exist_ok=True)
            final_path = paths.run_directory / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            final_path.mkdir()
            report = self.minimal_final_report(paths.run_directory.name, "foundation_existing_directory_12345678/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            with self.assertRaises(RealTaskExecutionFailure) as raised:
                real_task_report_writer.write_trusted_report(ROOT / "CodexAutomation", parent / "CodexAutomation" / "runtime", final_path, report, "real_task_workspace_final.schema.json", None, CONFIG["realTaskFoundation"]["maxReportBytes"])
            self.assertEqual(raised.exception.code, "REAL_TASK_FINAL_REPORT_WRITE_FAILED")
            self.assertTrue(final_path.is_dir())

    def test_existing_broken_symlink_at_final_report_path_is_rejected(self) -> None:
        with self.synthetic_parent() as parent:
            paths = self.run_paths(parent, "foundation_existing_broken_link_12345678")
            paths.run_directory.mkdir(parents=True, exist_ok=True)
            final_path = paths.run_directory / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            try:
                final_path.symlink_to(paths.run_directory / "missing.json")
            except (OSError, NotImplementedError):
                self.skipTest("Symlink creation is unavailable in this session.")
            report = self.minimal_final_report(paths.run_directory.name, "foundation_existing_broken_link_12345678/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            with self.assertRaises(RealTaskExecutionFailure) as raised:
                real_task_report_writer.write_trusted_report(ROOT / "CodexAutomation", parent / "CodexAutomation" / "runtime", final_path, report, "real_task_workspace_final.schema.json", None, CONFIG["realTaskFoundation"]["maxReportBytes"])
            self.assertEqual(raised.exception.code, "REAL_TASK_FINAL_REPORT_WRITE_FAILED")
            self.assertTrue(final_path.is_symlink())

    def test_existing_symlink_or_reparse_target_metadata_is_rejected(self) -> None:
        with self.synthetic_parent() as parent:
            runtime = parent / "CodexAutomation" / "runtime"
            run_id = "foundation_existing_reparse_target_12345678"
            final_path = runtime / "real_task_foundation_runs" / run_id / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            report = self.minimal_final_report(run_id, f"real_task_foundation_runs/{run_id}/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            original_stat = real_task_report_writer._stat_path_long_safe

            class ReparseTargetStatus:
                st_mode = stat.S_IFLNK
                st_size = 0
                st_file_attributes = 0x400

            def fake_stat(path, *, follow_symlinks, error_code, message):
                if path.name == "FINAL_WORKSPACE_PREPARATION_REPORT.json":
                    return ReparseTargetStatus()
                return original_stat(path, follow_symlinks=follow_symlinks, error_code=error_code, message=message)

            with mock.patch("real_task_report_writer._stat_path_long_safe", side_effect=fake_stat), mock.patch("real_task_report_writer.os.replace") as replace_mock:
                with self.assertRaises(RealTaskExecutionFailure) as raised:
                    real_task_report_writer.write_trusted_report(ROOT / "CodexAutomation", runtime, final_path, report, "real_task_workspace_final.schema.json", None, CONFIG["realTaskFoundation"]["maxReportBytes"])
            self.assertEqual(raised.exception.code, "REAL_TASK_FINAL_REPORT_WRITE_FAILED")
            self.assertFalse(replace_mock.called)

    def test_atomic_replace_failure_blocks_without_receipt(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            with mock.patch("real_task_report_writer.os.replace", side_effect=OSError("replace failed")):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_replace_fail_12345678")
            self.assertEqual(report["finalVerdict"], "BLOCKED")
            self.assertEqual(report["errorCode"], "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")
            self.assertIsNone(report["finalReportReceipt"])

    def test_final_report_path_escape_is_rejected_before_write(self) -> None:
        with self.synthetic_parent() as parent:
            policy = parse_foundation_policy(parent, CONFIG, parse_real_task_policy(CONFIG)[0])[0]
            paths = create_run_paths(parent, policy, "foundation_escape_12345678")
            report = self.minimal_final_report("foundation_escape_12345678", "../outside.json")
            with self.assertRaises(Exception) as raised:
                real_task_foundation_runner._write_trusted_final_foundation_report(parent, ROOT / "CodexAutomation", paths, report, CONFIG)
            self.assertEqual(raised.exception.code, "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")
            self.assertFalse((paths.run_directory.parent / "outside.json").exists())

    def test_reparse_ancestor_blocks_foundation_authority(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            with mock.patch("real_task_report_writer._has_reparse_parent", return_value=True):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_reparse_12345678")
            self.assertEqual(report["finalVerdict"], "BLOCKED")
            self.assertEqual(report["errorCode"], "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")
            self.assertIsNone(report["finalReportReceipt"])

    def test_deep_reparse_ancestor_metadata_blocks_before_replace(self) -> None:
        with self.synthetic_parent() as parent:
            runtime = parent / "CodexAutomation" / "runtime"
            run_id = "foundation_reparse_" + ("r" * 96)
            final_path = runtime / "real_task_foundation_runs" / run_id / "reparse_marker" / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            report = self.minimal_final_report(run_id, f"real_task_foundation_runs/{run_id}/reparse_marker/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            original_stat = real_task_report_writer._stat_path_long_safe
            seen_paths: list[Path] = []

            class ReparseStatus:
                st_mode = stat.S_IFDIR
                st_size = 0
                st_file_attributes = 0x400

            def fake_stat(path, *, follow_symlinks, error_code, message):
                seen_paths.append(path)
                if path.name == "reparse_marker":
                    return ReparseStatus()
                return original_stat(path, follow_symlinks=follow_symlinks, error_code=error_code, message=message)

            with mock.patch("real_task_report_writer._stat_path_long_safe", side_effect=fake_stat), mock.patch("real_task_report_writer.os.replace") as replace_mock:
                with self.assertRaises(RealTaskExecutionFailure) as raised:
                    real_task_report_writer.write_trusted_report(ROOT / "CodexAutomation", runtime, final_path, report, "real_task_workspace_final.schema.json", None, CONFIG["realTaskFoundation"]["maxReportBytes"])
            self.assertEqual(raised.exception.code, "REAL_TASK_FINAL_REPORT_WRITE_FAILED")
            self.assertFalse(replace_mock.called)
            self.assertTrue(any(path.name == "reparse_marker" for path in seen_paths))
            self.assertGreater(len(str(final_path.resolve(strict=False))), 260)

    def test_metadata_inspection_error_fails_closed_before_write(self) -> None:
        with self.synthetic_parent() as parent:
            runtime = parent / "CodexAutomation" / "runtime"
            run_id = "foundation_metadata_error_12345678"
            final_path = runtime / "real_task_foundation_runs" / run_id / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            report = self.minimal_final_report(run_id, f"real_task_foundation_runs/{run_id}/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            original_stat = real_task_report_writer.os.stat

            def failing_stat(path, *args, **kwargs):
                if str(path).endswith("FINAL_WORKSPACE_PREPARATION_REPORT.json"):
                    raise PermissionError("metadata denied")
                return original_stat(path, *args, **kwargs)

            with mock.patch("real_task_report_writer.os.stat", side_effect=failing_stat), mock.patch("real_task_report_writer.os.replace") as replace_mock:
                with self.assertRaises(RealTaskExecutionFailure) as raised:
                    real_task_report_writer.write_trusted_report(ROOT / "CodexAutomation", runtime, final_path, report, "real_task_workspace_final.schema.json", None, CONFIG["realTaskFoundation"]["maxReportBytes"])
            self.assertEqual(raised.exception.code, "REAL_TASK_FINAL_REPORT_WRITE_FAILED")
            self.assertFalse(replace_mock.called)

    def test_late_target_appearance_blocks_without_replace(self) -> None:
        with self.synthetic_parent() as parent:
            runtime = parent / "CodexAutomation" / "runtime"
            run_id = "foundation_late_target_12345678"
            final_path = runtime / "real_task_foundation_runs" / run_id / "FINAL_WORKSPACE_PREPARATION_REPORT.json"
            report = self.minimal_final_report(run_id, f"real_task_foundation_runs/{run_id}/FINAL_WORKSPACE_PREPARATION_REPORT.json")
            original_reject = real_task_report_writer._reject_existing_target
            checks = {"count": 0}
            existing_text = "late authority placeholder"

            def reject_with_late_target(path, error_code):
                checks["count"] += 1
                if checks["count"] == 3:
                    with open(self.fs_path(path), "w", encoding="utf-8") as handle:
                        handle.write(existing_text)
                return original_reject(path, error_code)

            with mock.patch("real_task_report_writer._reject_existing_target", side_effect=reject_with_late_target), mock.patch("real_task_report_writer.os.replace") as replace_mock:
                with self.assertRaises(RealTaskExecutionFailure) as raised:
                    real_task_report_writer.write_trusted_report(ROOT / "CodexAutomation", runtime, final_path, report, "real_task_workspace_final.schema.json", None, CONFIG["realTaskFoundation"]["maxReportBytes"])
            self.assertEqual(raised.exception.code, "REAL_TASK_FINAL_REPORT_WRITE_FAILED")
            self.assertFalse(replace_mock.called)
            with open(self.fs_path(final_path), "r", encoding="utf-8") as handle:
                self.assertEqual(handle.read(), existing_text)
            self.assertFalse(list(final_path.parent.glob(".FINAL_WORKSPACE_PREPARATION_REPORT.json.*.tmp")))

    def test_tampered_reread_hash_blocks_foundation_authority(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            original_read = real_task_foundation_runner.read_trusted_report
            calls = {"count": 0}

            def tampered_read(*args, **kwargs):
                loaded = original_read(*args, **kwargs)
                calls["count"] += 1
                if calls["count"] == 1:
                    loaded = dict(loaded)
                    loaded["runId"] = "tampered"
                return loaded

            with mock.patch("real_task_foundation_runner.read_trusted_report", side_effect=tampered_read):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_tamper_12345678")
            self.assertEqual(report["finalVerdict"], "BLOCKED")
            self.assertEqual(report["errorCode"], "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")

    def test_consumer_rejects_missing_foundation_receipt(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_consumer_missing_12345678")
            self.assertEqual(report["finalVerdict"], "PASS")
            untrusted = dict(report)
            untrusted["finalReportReceipt"] = None
            run_dir = parent / "CodexAutomation" / "runtime" / "real_task_runs" / "consumer_missing"
            run_dir.mkdir(parents=True)
            with self.assertRaises(RealTaskExecutionFailure) as raised:
                create_execution_context(parent, ROOT / "CodexAutomation", run_dir, "consumer_missing", manifest, "a" * 64, {}, CONFIG["realTaskExecutionPolicy"], untrusted, {"maxRoleInvocations": 3, "maxRepairAttempts": 1})
            self.assertEqual(raised.exception.code, "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")

    def test_deep_consumer_accepts_trusted_workspace_directory(self) -> None:
        with self.synthetic_parent() as parent:
            config = json.loads(json.dumps(CONFIG))
            config["realTasks"]["allowedSourceRoots"] = list(config["realTasks"]["allowedSourceRoots"]) + ["TaskData"]
            task_data = parent / "TaskData"
            task_data.mkdir()
            (task_data / "i.txt").write_text("line one\n", encoding="utf-8")
            manifest = self.write_manifest(parent, ["TaskData/i.txt"], ["TaskData/i.txt"])
            run_id = "foundation_consumer_deep_" + ("w" * 92)
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", config, manifest, run_id)
            self.assertEqual(report["finalVerdict"], "PASS", report)
            run_dir = parent / "CodexAutomation" / "runtime" / "real_task_runs" / "consumer_deep"
            run_dir.mkdir(parents=True)
            with mock.patch("real_task_execution_context.ensure_trusted_directory", wraps=real_task_report_writer.ensure_trusted_directory) as directory_check:
                context, context_hash = create_execution_context(parent, ROOT / "CodexAutomation", run_dir, "consumer_deep", manifest, "a" * 64, {}, config["realTaskExecutionPolicy"], report, {"maxRoleInvocations": 3, "maxRepairAttempts": 1})
            self.assertEqual(context.foundationRunId, report["runId"])
            self.assertEqual(len(context_hash), 64)
            self.assertTrue(any(call.args[0].name == "workspace" for call in directory_check.call_args_list))

    def test_consumer_rejects_workspace_file_instead_of_directory(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_consumer_file_12345678")
            self.assertEqual(report["finalVerdict"], "PASS", report)
            workspace = parent / "CodexAutomation" / "runtime" / "real_task_foundation_runs" / report["runId"] / "workspace"
            shutil.rmtree(self.fs_path(workspace))
            workspace.write_text("not a directory", encoding="utf-8")
            run_dir = parent / "CodexAutomation" / "runtime" / "real_task_runs" / "consumer_workspace_file"
            run_dir.mkdir(parents=True)
            with self.assertRaises(RealTaskExecutionFailure) as raised:
                create_execution_context(parent, ROOT / "CodexAutomation", run_dir, "consumer_workspace_file", manifest, "a" * 64, {}, CONFIG["realTaskExecutionPolicy"], report, {"maxRoleInvocations": 3, "maxRepairAttempts": 1})
            self.assertEqual(raised.exception.code, "REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID")

    def test_ba_cannot_pass_without_final_report_artifact(self) -> None:
        with self.synthetic_parent() as parent:
            manifest = self.write_manifest(parent)
            with mock.patch("real_task_foundation_runner._write_trusted_final_foundation_report", side_effect=real_task_foundation_runner._FoundationFailure("REAL_TASK_FOUNDATION_FINAL_REPORT_INVALID", "forced missing final report")):
                report = prepare_foundation_workspace(parent, ROOT / "CodexAutomation", CONFIG, manifest, "foundation_missing_final_12345678")
            self.assertEqual(report["finalVerdict"], "BLOCKED")
            self.assertFalse(report["complete"])
            self.assertIsNone(report["finalReportReceipt"])

    def synthetic_parent(self):
        return _SyntheticParent()

    def write_manifest(self, parent: Path, source_paths: list[str] | None = None, write_paths: list[str] | None = None) -> Path:
        return RealTaskFoundationCheck().write_manifest(parent, source_paths or ["Assets/TestFixture/input.txt"], write_paths or ["Assets/TestFixture/input.txt"])

    def run_paths(self, parent: Path, run_id: str):
        runtime_root = parent / CONFIG["realTaskFoundation"]["runtimeRoot"]
        run_dir = runtime_root / run_id
        return WorkspacePaths(parent, runtime_root, run_dir, run_dir / "staging", run_dir / "workspace", run_dir / "evidence", run_dir / "logs")

    def minimal_final_report(self, run_id: str, relative_path: str) -> dict[str, object]:
        return {
            "reportVersion": 1,
            "runId": run_id,
            "taskId": "TASK-FOUNDATION-001",
            "stage": "BOOTSTRAP-03B-2B-A",
            "stateHistory": ["PENDING", "REPORTING", "COMPLETED"],
            "manifestSha256": "a" * 64,
            "trustedContextHash": "b" * 64,
            "parentGitInitialSnapshot": {"status": "success"},
            "parentGitFinalSnapshot": {"status": "success"},
            "parentGitChanged": False,
            "sourcePrePostMatch": True,
            "sourceCopyVerified": True,
            "workspaceCreated": True,
            "stagingRetained": False,
            "workspaceReady": True,
            "serviceFilesValid": True,
            "isolatedGitValid": True,
            "sourceInventoryHash": "c" * 64,
            "workspaceBaselineInventoryHash": "d" * 64,
            "codexInvocationCount": 0,
            "modelInvocationStarted": False,
            "sandboxStarted": False,
            "unityStarted": False,
            "networkUsed": False,
            "sourceCopied": True,
            "complete": True,
            "finalReportRelativePath": relative_path,
            "finalReportReceipt": None,
            "durationSeconds": 0.0,
            "finalState": "COMPLETED",
            "finalVerdict": "PASS",
            "errorCode": None,
            "errorMessage": None,
            "warnings": [],
        }

    def fs_path(self, path: Path) -> str:
        text = str(path.resolve(strict=False))
        if os.name == "nt" and not text.startswith("\\\\?\\"):
            return "\\\\?\\" + text
        return text


class _SyntheticParent:
    def __enter__(self) -> Path:
        self.temp = tempfile.TemporaryDirectory(dir=ROOT / "CodexAutomation" / "runtime")
        self.root = Path(self.temp.name)
        (self.root / "Assets" / "TestFixture").mkdir(parents=True)
        (self.root / "Assets" / "TestFixture" / "input.txt").write_text("line one\n", encoding="utf-8")
        (self.root / "AGENTS.md").write_text("# Parent Rules\n", encoding="utf-8")
        (self.root / "CodexAutomation").mkdir()
        (self.root / "CodexAutomation" / ".gitignore").write_text("runtime/\n", encoding="utf-8")
        subprocess.run(["git", "init"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "config", "user.email", "foundation@example.invalid"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "config", "user.name", "Foundation Test"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "add", "AGENTS.md", "Assets", "CodexAutomation/.gitignore"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        subprocess.run(["git", "commit", "-m", "synthetic parent baseline"], cwd=str(self.root), capture_output=True, text=True, check=False, timeout=10)
        return self.root

    def __exit__(self, exc_type, exc, tb) -> None:
        try:
            self.temp.cleanup()
        except OSError:
            shutil.rmtree(_fs_path(Path(self.temp.name)), ignore_errors=True)


def _fs_path(path: Path) -> str:
    text = str(path.resolve(strict=False))
    if os.name == "nt" and not text.startswith("\\\\?\\"):
        return "\\\\?\\" + text
    return text


if __name__ == "__main__":
    unittest.main()
