from __future__ import annotations

import json
import subprocess
import sys
import unittest
from dataclasses import replace
from pathlib import Path
from unittest.mock import Mock, patch


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from file_utils import read_json  # noqa: E402
from real_task_validation_runner import _final_verdict, _integrity_pass, _readable_scopes, _validate_outcome, _validate_raw_outcome_numbers, _validate_service_snapshot, _validate_validation_report_semantics, create_validation_prerequisite_handle, execute_validation_plan, parse_validation_policy  # noqa: E402
from schema_validator import validate as validate_schema_instance  # noqa: E402
from validator_safe_io import resolve_workspace_path  # noqa: E402
from validator_models import ValidationFailure, blocked_outcome, fail_outcome, pass_outcome  # noqa: E402
from validator_models import VALIDATOR_TYPES, ValidatorContext, ValidatorOutcome, ValidatorReadableScope, ValidatorScopeKind, ValidatorStatus, canonical_sha256  # noqa: E402
from validator_registry import load_validator_registry  # noqa: E402
from validator_schema_registry import load_schema_registry  # noqa: E402
from validator_self_test_runner import _git, _validate_synthetic_git_cwd  # noqa: E402
from validator_self_test_runner import run_validator_self_test  # noqa: E402
from workspace_change_models import file_sha256  # noqa: E402
import real_task_validation_runner as validation_runner  # noqa: E402


CONFIG = read_json(ROOT / "CodexAutomation" / "config.json")


class RealTaskValidationCheck(unittest.TestCase):
    def test_validation_policy_config_is_strict(self) -> None:
        policy, errors = parse_validation_policy(CONFIG)
        self.assertFalse(errors, [error.to_dict() for error in errors])
        self.assertIsNotNone(policy)
        self.assertEqual(policy.allowedValidatorTypes, VALIDATOR_TYPES)
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskValidationPolicy"]["failFast"] = True
        self.assertTrue(parse_validation_policy(bad)[1])
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskValidationPolicy"]["allowNetwork"] = 1
        self.assertTrue(parse_validation_policy(bad)[1])
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskValidationPolicy"]["maxValidatorsPerTask"] = True
        self.assertTrue(parse_validation_policy(bad)[1])
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskValidationPolicy"]["allowedValidatorTypes"] = list(reversed(VALIDATOR_TYPES))
        self.assertTrue(parse_validation_policy(bad)[1])
        bad = json.loads(json.dumps(CONFIG))
        bad["realTaskValidationPolicy"]["unknown"] = False
        self.assertTrue(parse_validation_policy(bad)[1])

    def test_fixed_validator_registry_loads_and_hash_is_deterministic(self) -> None:
        policy = parse_validation_policy(CONFIG)[0]
        self.assertIsNotNone(policy)
        registry, snapshot = load_validator_registry(policy)
        second_registry, second_snapshot = load_validator_registry(policy)
        self.assertEqual(tuple(registry), VALIDATOR_TYPES)
        self.assertEqual(tuple(second_registry), VALIDATOR_TYPES)
        self.assertEqual(snapshot, second_snapshot)
        self.assertEqual(snapshot["registrySha256"], canonical_sha256({key: value for key, value in snapshot.items() if key != "registrySha256"}))
        self.assertEqual(len({definition.implementationId for definition in registry.values()}), len(VALIDATOR_TYPES))
        self.assertTrue(all(definition.noSideEffects for definition in registry.values()))

    def test_schema_registry_loads_registered_schema_and_blocks_unknown(self) -> None:
        policy = parse_validation_policy(CONFIG)[0]
        self.assertIsNotNone(policy)
        registry = load_schema_registry(ROOT, ROOT / "CodexAutomation", policy)
        schema = registry.load_schema("validator_self_test_result_v1")
        self.assertEqual(schema["type"], "object")
        with self.assertRaises(Exception):
            registry.load_schema("../validator_self_test_result_v1.schema.json")
        snapshot = registry.snapshot()
        self.assertEqual(snapshot, registry.snapshot())

    def test_controlled_self_test_passes_without_codex_model_sandbox_unity_or_network(self) -> None:
        report = run_validator_self_test(ROOT, ROOT / "CodexAutomation", CONFIG)
        self.assertEqual(report["finalVerdict"], "PASS")
        self.assertFalse(report["codexInspected"])
        self.assertEqual(report["codexInvocationCount"], 0)
        self.assertFalse(report["modelInvocationStarted"])
        self.assertFalse(report["sandboxStarted"])
        self.assertFalse(report["unityStarted"])
        self.assertFalse(report["networkUsed"])
        cases = {case["caseId"]: case for case in report["cases"]}
        self.assertEqual(cases["pass"]["actualValidatorCount"], 14)
        self.assertEqual(cases["pass"]["actualVerdict"], "PASS")
        self.assertEqual(cases["fail"]["actualVerdict"], "FAIL")
        self.assertEqual(
            sorted(cases["fail"]["actualFailedTypes"]),
            sorted(["json_field_equals", "text_not_contains", "changed_paths_exact", "no_conflict_markers", "max_file_size", "extension_allowlist"]),
        )
        final_path = Path(cases["pass"]["finalValidationReportPath"])
        run_dir = final_path.parent
        for name in [
            "VALIDATOR_REGISTRY_SNAPSHOT.json",
            "VALIDATION_PLAN_REPORT.json",
            "VALIDATOR_RESULTS_REPORT.json",
            "VALIDATION_INTEGRITY_REPORT.json",
            "FINAL_VALIDATION_REPORT.json",
        ]:
            self.assertTrue((run_dir / name).exists(), name)
        final_report = read_json(final_path)
        first_result = final_report["results"][0]
        for field in ["validatorIndex", "validatorVersion", "primaryPath", "relatedPaths", "filesRead", "bytesRead", "durationMilliseconds", "noSideEffects"]:
            self.assertIn(field, first_result)
        self.assertTrue(final_report["integrity"]["workspaceUnchanged"])

    def test_cli_accepts_only_validator_self_test_without_extra_args(self) -> None:
        ok = subprocess.run(
            [sys.executable, "CodexAutomation/scripts/orchestrator.py", "--real-task-validator-self-test"],
            cwd=str(ROOT),
            capture_output=True,
            text=True,
            check=False,
            timeout=120,
        )
        self.assertEqual(ok.returncode, 0, ok.stdout + ok.stderr)
        self.assertIn("codex: not inspected", ok.stdout)
        extra = subprocess.run(
            [sys.executable, "CodexAutomation/scripts/orchestrator.py", "--real-task-validator-self-test", "manifest.json"],
            cwd=str(ROOT),
            capture_output=True,
            text=True,
            check=False,
            timeout=30,
        )
        self.assertNotEqual(extra.returncode, 0)

    def test_execute_validation_plan_rejects_raw_matching_dict(self) -> None:
        report = run_validator_self_test(ROOT, ROOT / "CodexAutomation", CONFIG)
        case = {case["caseId"]: case for case in report["cases"]}["pass"]
        final_report = read_json(Path(case["finalValidationReportPath"]).parent / "FINAL_CHANGE_ANALYSIS_REPORT.json")
        result = execute_validation_plan(final_report, ROOT, ROOT / "CodexAutomation", CONFIG)
        self.assertEqual(result.finalVerdict, "BLOCKED")
        self.assertEqual(result.report["errorCode"], "REAL_TASK_VALIDATION_CONTEXT_MISMATCH")

    def test_path_scope_does_not_authorize_ancestor(self) -> None:
        report = run_validator_self_test(ROOT, ROOT / "CodexAutomation", CONFIG)
        final_path = Path({case["caseId"]: case for case in report["cases"]}["pass"]["finalValidationReportPath"])
        run_dir = final_path.parent
        from validator_models import ValidatorPolicy, ValidatorContext  # noqa: WPS433

        context = ValidatorContext(
            repositoryRoot=ROOT,
            automationRoot=ROOT / "CodexAutomation",
            runDirectory=run_dir,
            workspaceDirectory=run_dir / "workspace",
            task={},
            effectivePolicy={},
            validationPolicy=parse_validation_policy(CONFIG)[0],
            baselineInventory={},
            finalInventory={},
            diffReport={},
            policyReport={},
            finalChangeReport={},
            serviceRegistry=(),
            readableScopes=("Assets/VST/input.txt",),
            schemaRegistry=None,
        )
        with self.assertRaises(ValidationFailure):
            resolve_workspace_path(context, "Assets", require_existing=False)


class ValidationScopeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.workspace = ROOT / "CodexAutomation" / "runtime" / "validator_scope_unit"
        (self.workspace / "Assets" / "A" / "B").mkdir(parents=True, exist_ok=True)
        (self.workspace / "Assets" / "A" / "B" / "file.txt").write_text("ok\n", encoding="utf-8")
        (self.workspace / "Assets" / "A" / "B" / "sibling.txt").write_text("ok\n", encoding="utf-8")
        self.policy = parse_validation_policy(CONFIG)[0]

    def _context(self, scopes: tuple[ValidatorReadableScope, ...]) -> ValidatorContext:
        return ValidatorContext(
            repositoryRoot=ROOT,
            automationRoot=ROOT / "CodexAutomation",
            runDirectory=self.workspace,
            workspaceDirectory=self.workspace,
            task={},
            effectivePolicy={},
            validationPolicy=self.policy,
            baselineInventory={},
            finalInventory={},
            diffReport={},
            policyReport={},
            finalChangeReport={},
            serviceRegistry=(),
            readableScopes=scopes,
            schemaRegistry=None,
        )

    def test_file_and_directory_scope_semantics(self) -> None:
        file_scope = (ValidatorReadableScope("Assets/A/B/file.txt", ValidatorScopeKind.FILE.value, "UNIT", False),)
        file_context = self._context(file_scope)
        self.assertEqual(resolve_workspace_path(file_context, "Assets/A/B/file.txt").name, "file.txt")
        for path in ("Assets", "Assets/A", "Assets/A/B", "Assets/A/B/file.txt/child", "Assets/A/B/sibling.txt"):
            with self.subTest(path=path):
                with self.assertRaises(ValidationFailure):
                    resolve_workspace_path(file_context, path, require_existing=False)

        dir_scope = (ValidatorReadableScope("Assets/A/B", ValidatorScopeKind.DIRECTORY.value, "UNIT", True),)
        dir_context = self._context(dir_scope)
        self.assertEqual(resolve_workspace_path(dir_context, "Assets/A/B/file.txt").name, "file.txt")
        with self.assertRaises(ValidationFailure):
            resolve_workspace_path(dir_context, "Assets/A/sibling.txt", require_existing=False)

    def test_scope_origin_construction_preserves_file_and_directory_kinds(self) -> None:
        task = {
            "sourcePaths": ["Assets/A"],
            "allowedWritePaths": ["Assets/A/B/file.txt"],
            "allowedDeletePaths": [],
            "expectedOutputs": [
                {"path": "Assets/A/B/file.txt", "kind": "file"},
                {"path": "Assets/A/B", "kind": "directory"},
            ],
        }
        inventory = {"entries": [{"path": "Assets/A", "type": "directory"}, {"path": "Assets/A/B/file.txt", "type": "file"}, {"path": "Assets/A/B", "type": "directory"}]}
        scopes = _readable_scopes(task, {"categories": {}}, inventory, inventory)
        by_key = {(scope.normalizedPath, scope.origin): scope for scope in scopes}
        self.assertEqual(by_key[("Assets/A/B/file.txt", "EXPECTED_OUTPUT")].scopeKind, ValidatorScopeKind.FILE.value)
        self.assertEqual(by_key[("Assets/A/B", "EXPECTED_OUTPUT")].scopeKind, ValidatorScopeKind.DIRECTORY.value)
        self.assertEqual(by_key[("Assets/A", "SOURCE_PATH")].scopeKind, ValidatorScopeKind.DIRECTORY.value)


class ValidationOutcomeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.policy = parse_validation_policy(CONFIG)[0]
        self.entry = {"id": "v001", "type": "file_exists", "path": "Assets/A/B/file.txt"}

    def test_outcome_code_contracts(self) -> None:
        scenarios = [
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, "VALIDATOR_FILE_MISSING", "bad"),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.FAIL.value, None, "bad"),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.BLOCKED.value, None, "bad"),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.FAIL.value, "VALIDATOR_JSON_SCHEMA_MISMATCH", "bad"),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.BLOCKED.value, "VALIDATOR_FILE_MISSING", "bad"),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad", noSideEffects=False),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad", filesRead=True),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad", bytesRead=True),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad", durationMilliseconds=True),
            ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "Traceback bad"),
        ]
        for outcome in scenarios:
            with self.subTest(outcome=outcome):
                with self.assertRaises(ValidationFailure):
                    _validate_outcome(self.entry, 0, "1", outcome, self.policy)

        valid = ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.FAIL.value, "VALIDATOR_FILE_MISSING", "ok")
        _validate_outcome(self.entry, 0, "1", valid, self.policy)

    def test_validator_index_exact_int_contract(self) -> None:
        invalid = [
            (True, 1),
            (False, 0),
            (1.0, 1),
            ("1", 1),
            (-1, 0),
            (-2, 0),
        ]
        for value, expected_index in invalid:
            with self.subTest(value=value, expected_index=expected_index):
                outcome = ValidatorOutcome("v001", value, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad")
                with self.assertRaises(ValidationFailure) as raised:
                    _validate_outcome(self.entry, expected_index, "1", outcome, self.policy)
                self.assertEqual(raised.exception.code, "REAL_TASK_VALIDATOR_RESULT_INVALID")

        for value, expected_index in ((0, 0), (1, 1)):
            with self.subTest(value=value, expected_index=expected_index):
                outcome = ValidatorOutcome("v001", value, "file_exists", "1", ValidatorStatus.PASS.value, None, "ok")
                _validate_outcome(self.entry, expected_index, "1", outcome, self.policy)

        with self.assertRaises(ValidationFailure):
            mismatched = ValidatorOutcome("v001", 1, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad")
            _validate_outcome(self.entry, 0, "1", mismatched, self.policy)

    def test_raw_validator_index_validation_rejects_malformed_values(self) -> None:
        invalid = [-1, -2, True, False, 1.0, "1", None]
        for value in invalid:
            with self.subTest(value=value):
                outcome = ValidatorOutcome("v001", value, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad")
                with self.assertRaises(ValidationFailure) as raised:
                    _validate_raw_outcome_numbers(0, outcome)
                self.assertEqual(raised.exception.code, "REAL_TASK_VALIDATOR_RESULT_INVALID")

        _validate_raw_outcome_numbers(0, ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "ok"))
        _validate_raw_outcome_numbers(1, ValidatorOutcome("v002", 1, "file_absent", "1", ValidatorStatus.PASS.value, None, "ok"))
        with self.assertRaises(ValidationFailure):
            _validate_raw_outcome_numbers(0, ValidatorOutcome("v002", 1, "file_absent", "1", ValidatorStatus.PASS.value, None, "bad"))
        with self.assertRaises(ValidationFailure):
            _validate_raw_outcome_numbers(0, ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad", durationMilliseconds=True))

    def test_outcome_builders_use_context_owned_index(self) -> None:
        self.assertEqual(pass_outcome("v001", "file_exists", validator_index=0).validatorIndex, 0)
        self.assertEqual(fail_outcome("v002", "file_absent", "VALIDATOR_FILE_PRESENT", "bad", validator_index=1).validatorIndex, 1)
        self.assertEqual(blocked_outcome("v003", "json_valid", "REAL_TASK_VALIDATOR_READ_FAILED", "bad", validator_index=2).validatorIndex, 2)

    def test_numeric_outcome_fields_use_exact_non_negative_ints(self) -> None:
        invalid = [
            ("filesRead", True),
            ("bytesRead", False),
            ("durationMilliseconds", True),
            ("filesRead", 1.0),
            ("bytesRead", "1"),
            ("durationMilliseconds", -1),
        ]
        for field_name, value in invalid:
            with self.subTest(field=field_name, value=value):
                outcome = replace(ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "bad"), **{field_name: value})
                with self.assertRaises(ValidationFailure) as raised:
                    _validate_outcome(self.entry, 0, "1", outcome, self.policy)
                self.assertEqual(raised.exception.code, "REAL_TASK_VALIDATOR_RESULT_INVALID")

        valid = ValidatorOutcome("v001", 0, "file_exists", "1", ValidatorStatus.PASS.value, None, "ok", filesRead=1, bytesRead=2, durationMilliseconds=3)
        _validate_outcome(self.entry, 0, "1", valid, self.policy)

    def test_invalid_validator_index_blocks_through_execution_aggregation(self) -> None:
        registry, snapshot = load_validator_registry(self.policy)
        later_validator = Mock(side_effect=AssertionError("later validator should not run"))
        bad_definition = replace(
            registry["file_exists"],
            implementation=lambda context, entry: ValidatorOutcome(entry["id"], -1, entry["type"], registry["file_exists"].version, ValidatorStatus.PASS.value, None, "bad index"),
        )
        patched_registry = dict(registry)
        patched_registry["file_exists"] = bad_definition
        patched_registry["file_absent"] = replace(registry["file_absent"], implementation=later_validator)

        with patch.object(validation_runner, "load_validator_registry", return_value=(patched_registry, snapshot)):
            report = run_validator_self_test(ROOT, ROOT / "CodexAutomation", CONFIG)

        self.assertEqual(report["finalVerdict"], "FAIL")
        pass_case = {case["caseId"]: case for case in report["cases"]}["pass"]
        self.assertEqual(pass_case["actualVerdict"], "BLOCKED")
        final_report = read_json(Path(pass_case["finalValidationReportPath"]))
        self.assertEqual(final_report["finalVerdict"], "BLOCKED")
        self.assertEqual(final_report["blockedCount"], 1)
        self.assertEqual(len(final_report["results"]), 1)
        self.assertEqual(final_report["results"][0]["status"], "BLOCKED")
        self.assertEqual(final_report["results"][0]["code"], "REAL_TASK_VALIDATOR_RESULT_INVALID")
        later_validator.assert_not_called()


class ValidationServiceSnapshotTests(unittest.TestCase):
    def setUp(self) -> None:
        self.workspace = ROOT / "CodexAutomation" / "runtime" / "validator_service_unit"
        self.workspace.mkdir(parents=True, exist_ok=True)

    def _baseline(self, path: str, content: str, expected_type: str = "file", size_delta: int = 0, sha: str | None = None) -> dict[str, object]:
        target = self.workspace / path
        target.write_text(content, encoding="utf-8")
        actual_sha = file_sha256(target)
        return {
            "serviceRegistry": [{"path": path, "kind": expected_type, "expectedType": expected_type, "expectedSha256": sha or actual_sha, "workspacePath": path}],
            "entries": [{"path": path, "type": expected_type, "size": target.stat().st_size + size_delta}],
        }

    def test_task_and_effective_policy_service_snapshot_trust(self) -> None:
        scenarios = [
            ("task.json", '{"taskId":"T"}\n'),
            ("effective_policy.json", '{"hostPolicy":{}}\n'),
        ]
        for path, content in scenarios:
            with self.subTest(path=path, case="valid"):
                _validate_service_snapshot(self.workspace, self._baseline(path, content), path, json.loads(content), canonical_sha256(json.loads(content)))
            for name, baseline in (
                ("wrong_type", self._baseline(path, content, expected_type="directory")),
                ("wrong_size", self._baseline(path, content, size_delta=1)),
                ("wrong_sha", self._baseline(path, content, sha="0" * 64)),
            ):
                with self.subTest(path=path, case=name):
                    with self.assertRaises(ValidationFailure):
                        _validate_service_snapshot(self.workspace, baseline, path, json.loads(content), canonical_sha256(json.loads(content)))


class ValidationSchemaTests(unittest.TestCase):
    def test_nested_schema_rejects_unknown_fields(self) -> None:
        report = run_validator_self_test(ROOT, ROOT / "CodexAutomation", CONFIG)
        pass_case = {case["caseId"]: case for case in report["cases"]}["pass"]
        final_report = read_json(Path(pass_case["finalValidationReportPath"]))
        schema = read_json(ROOT / "CodexAutomation" / "schemas" / "real_task_validation_final.schema.json")
        self.assertFalse(validate_schema_instance(final_report, schema))

        mutations = [
            ("registry", lambda item: item["validatorRegistry"]["validatorTypes"][0].__setitem__("extra", True)),
            ("plan", lambda item: item["validationPlan"][0].__setitem__("extra", True)),
            ("result", lambda item: item["results"][0].__setitem__("extra", True)),
            ("integrity", lambda item: item["integrity"].__setitem__("extra", True)),
            ("warning", lambda item: item.__setitem__("warnings", [{"code": "X", "message": "Y", "extra": True}])),
        ]
        for name, mutate in mutations:
            changed = json.loads(json.dumps(final_report))
            mutate(changed)
            with self.subTest(name=name):
                self.assertTrue(validate_schema_instance(changed, schema))

    def test_self_test_case_schema_rejects_unknown_field(self) -> None:
        report = run_validator_self_test(ROOT, ROOT / "CodexAutomation", CONFIG)
        report.pop("reportPath", None)
        schema = read_json(ROOT / "CodexAutomation" / "schemas" / "validator_self_test_report.schema.json")
        self.assertFalse(validate_schema_instance(report, schema))
        report["cases"][0]["extra"] = True
        self.assertTrue(validate_schema_instance(report, schema))


class ValidationIntegrityTests(unittest.TestCase):
    def test_agents_directory_valid_is_required_for_pass_and_fail(self) -> None:
        integrity = {
            "workspaceUnchanged": True,
            "workspaceMatchesTrustedFinal": True,
            "serviceFilesValid": True,
            "agentsDirectoryValid": True,
            "isolatedGitValid": True,
            "parentGitChanged": False,
            "parentSourceChanged": False,
            "noValidatorWrites": True,
        }
        self.assertTrue(_integrity_pass(integrity))
        outcomes = [ValidatorOutcome("v001", 0, "file_exists", "1", "PASS", None, "ok")]
        self.assertEqual(_final_verdict(outcomes, integrity), "PASS")
        integrity["agentsDirectoryValid"] = False
        self.assertFalse(_integrity_pass(integrity))
        self.assertEqual(_final_verdict(outcomes, integrity), "BLOCKED")
        fail_outcomes = [ValidatorOutcome("v001", 0, "file_exists", "1", "FAIL", "VALIDATOR_FILE_MISSING", "fail")]
        self.assertEqual(_final_verdict(fail_outcomes, integrity), "BLOCKED")


class ValidatorSelfTestContainmentTests(unittest.TestCase):
    def _parent_root(self, run_id: str = "unit_containment", case_id: str = "case") -> Path:
        root = ROOT / "CodexAutomation" / "runtime" / "real_task_validator_self_tests" / run_id / case_id / "parent"
        root.mkdir(parents=True, exist_ok=True)
        return root

    def test_synthetic_git_cwd_containment_and_argv_helpers(self) -> None:
        root = self._parent_root()
        self.assertEqual(_validate_synthetic_git_cwd(root, root), root.resolve(strict=True))
        fixed_root = ROOT / "CodexAutomation" / "runtime" / "real_task_validator_self_tests"
        rejected = (
            ROOT,
            ROOT / "CodexAutomation",
            ROOT / "CodexAutomation" / "runtime",
            fixed_root,
            root.parent.parent,
            root.parent,
        )
        for path in rejected:
            with self.subTest(path=path):
                with self.assertRaises(RuntimeError):
                    _validate_synthetic_git_cwd(path, root)

    def test_synthetic_git_rejects_spoofed_marker_roots(self) -> None:
        root = self._parent_root("unit_spoof_valid", "case")
        spoof_base = ROOT / "CodexAutomation" / "runtime" / "validator_containment_spoofs"
        spoofed = [
            spoof_base / "Temp" / "real_task_validator_self_tests" / "run" / "case" / "parent",
            spoof_base / "OtherProject" / "CodexAutomation" / "runtime" / "real_task_validator_self_tests" / "run" / "case" / "parent",
            spoof_base / "ClickerFake" / "CodexAutomation" / "runtime" / "real_task_validator_self_tests" / "run" / "case" / "parent",
            spoof_base / "SomeOtherRoot" / "real_task_validator_self_tests" / "run" / "case" / "parent",
        ]
        for path in spoofed:
            path.mkdir(parents=True, exist_ok=True)
            with self.subTest(path=path):
                with self.assertRaises(RuntimeError):
                    _validate_synthetic_git_cwd(path, path)

    def test_synthetic_git_requires_exact_expected_parent(self) -> None:
        expected = self._parent_root("unit_exact", "case")
        other_parent = self._parent_root("unit_exact", "other_case")
        child = expected / "child"
        child.mkdir(exist_ok=True)
        for cwd in (other_parent, child):
            with self.subTest(cwd=cwd):
                with self.assertRaises(RuntimeError):
                    _validate_synthetic_git_cwd(cwd, expected)

    def test_synthetic_git_rejects_reparse_components(self) -> None:
        root = self._parent_root("unit_reparse", "case")
        case_root = root.parent.resolve(strict=True)

        def fake_reparse(path: Path) -> bool:
            return path.resolve(strict=False) == case_root

        with patch("validator_self_test_runner.is_symlink_or_reparse", side_effect=fake_reparse):
            with self.assertRaises(RuntimeError):
                _validate_synthetic_git_cwd(root, root)

    def test_synthetic_git_spoofed_path_does_not_start_subprocess(self) -> None:
        expected = self._parent_root("unit_git_expected", "case")
        spoofed = ROOT / "CodexAutomation" / "runtime" / "validator_git_spoof" / "real_task_validator_self_tests" / "run" / "case" / "parent"
        spoofed.mkdir(parents=True, exist_ok=True)
        with patch("validator_self_test_runner.subprocess.run") as run:
            with self.assertRaises(RuntimeError):
                _git(spoofed, ["git", "init"], expected)
            run.assert_not_called()

    def test_synthetic_git_rejects_unsupported_argv_without_subprocess(self) -> None:
        expected = self._parent_root("unit_git_unsupported", "case")
        for command in (["git", "status"], ["git", "remote"], ["git", "push"]):
            with self.subTest(command=command):
                with patch("validator_self_test_runner.subprocess.run") as run:
                    with self.assertRaises(RuntimeError):
                        _git(expected, command, expected)
                    run.assert_not_called()

    def test_synthetic_git_allows_fixed_argv_at_expected_parent(self) -> None:
        expected = self._parent_root("unit_git_allowed", "case")
        completed = subprocess.CompletedProcess(["git", "init"], 0, "", "")
        with patch("validator_self_test_runner.subprocess.run", return_value=completed) as run:
            _git(expected, ["git", "init"], expected)
        run.assert_called_once()


if __name__ == "__main__":
    unittest.main()
