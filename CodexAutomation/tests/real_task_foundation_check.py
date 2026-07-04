from __future__ import annotations

import json
import os
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
from real_task_foundation_models import canonical_sha256, new_foundation_run_id, parse_foundation_policy  # noqa: E402
from real_task_foundation_runner import prepare_foundation_workspace, write_validated_trusted_context  # noqa: E402
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
        self.temp.cleanup()


if __name__ == "__main__":
    unittest.main()
