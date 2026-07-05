from __future__ import annotations

import shutil
from pathlib import Path, PurePosixPath
from typing import Any

from file_utils import read_json
from real_task_execution_context import resolve_execution_authority
from real_task_execution_models import RealTaskExecutionFailure, RealTaskExecutionHandle, canonical_sha256, file_sha256
from real_task_report_writer import write_trusted_report


def create_result_bundle(handle: RealTaskExecutionHandle, root: Path, automation_root: Path, final_change_report: dict[str, Any], final_validation_report: dict[str, Any], audit_report: dict[str, Any], policy: dict[str, Any]) -> tuple[dict[str, Any], str]:
    authority = resolve_execution_authority(handle, automation_root, ("AUDIT_APPROVED",))
    context = authority.context
    run_dir = Path(handle.runDirectory)
    bundle_dir = run_dir / "result_bundle"
    if bundle_dir.exists():
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_WRITE_FAILED", "Result bundle already exists.")
    temp_bundle = run_dir / "result_bundle.tmp"
    if temp_bundle.exists():
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_WRITE_FAILED", "Temporary result bundle already exists.")
    files_dir = temp_bundle / "files"
    files_dir.mkdir(parents=True, exist_ok=False)
    workspace = Path(context["workspaceIdentity"]["workspaceDirectory"])
    diff_path = Path(context["foundationRunDirectory"]) / "WORKSPACE_DIFF_REPORT.json"
    diff = read_json(diff_path) if diff_path.exists() else {"categories": {"added": [], "modified": [], "typeChanged": [], "deleted": []}}
    categories = diff.get("categories", {})
    entries: list[dict[str, Any]] = []
    for change in list(categories.get("added", [])) + list(categories.get("modified", [])) + list(categories.get("typeChanged", [])):
        side = change.get("after")
        if not isinstance(side, dict) or side.get("type") != "file":
            continue
        rel = _validate_bundle_source_path(str(side["path"]))
        if rel.startswith(".git") or rel == ".agents" or rel.startswith(".agents/") or rel.endswith("AGENTS.md") or rel in {"task.json", "effective_policy.json"}:
            continue
        source = workspace / rel
        try:
            source.resolve(strict=False).relative_to(workspace.resolve(strict=True))
        except ValueError as exc:
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Changed file escapes workspace.") from exc
        if not source.exists() or source.is_symlink() or not source.is_file():
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Changed file is unavailable for bundle copy.")
        if side.get("sha256") and file_sha256(source) != side.get("sha256"):
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Changed file hash does not match final inventory.")
        target = files_dir / rel
        try:
            target.resolve(strict=False).relative_to(files_dir.resolve(strict=True))
        except ValueError as exc:
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle destination escapes files directory.") from exc
        if _has_reparse_parent(target):
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle destination parent is unsafe.")
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if file_sha256(target) != file_sha256(source):
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Copied file hash mismatch.")
        entries.append({"relativePath": f"files/{rel}", "artifactType": "changed_file", "sourceTaskPath": rel, "size": target.stat().st_size, "sha256": file_sha256(target)})
    patch_text = _deterministic_patch(diff)
    patch_path = temp_bundle / "changes.patch"
    patch_path.write_text(patch_text, encoding="utf-8", newline="\n")
    entries.append({"relativePath": "changes.patch", "artifactType": "unified_patch", "sourceTaskPath": None, "size": patch_path.stat().st_size, "sha256": file_sha256(patch_path)})
    hashes = _sha256sums(temp_bundle, excluded=("RESULT_MANIFEST.json", "SHA256SUMS.json"))
    sums_receipt = write_trusted_report(
        automation_root,
        root / "CodexAutomation" / "runtime",
        temp_bundle / "SHA256SUMS.json",
        hashes,
        "real_task_bundle_hashes.schema.json",
        lambda item: _validate_sha256sums(item, temp_bundle),
        int(policy["maxBundleBytes"]),
    )
    manifest = {
        "reportVersion": 1,
        "orchestrationRunId": context["orchestrationRunId"],
        "taskId": context["taskId"],
        "stage": "BOOTSTRAP-03B-2C",
        "createdAt": context["createdAt"],
        "finalTaskVerdict": "PASS",
        "eligibleForApply": False,
        "parentRepositoryIdentity": context["parentRepositoryIdentity"],
        "parentHead": context["parentRepositoryIdentity"].get("parentHead"),
        "parentGitSnapshotHash": context["initialParentGitSnapshotHash"],
        "manifestSha256": context["manifestSha256"],
        "effectivePolicyHash": context["effectivePolicyHash"],
        "executionContextHash": canonical_sha256(context),
        "foundationFinalReportHash": context["foundationFinalReportHash"],
        "finalChangeAnalysisReportHash": canonical_sha256(final_change_report),
        "finalValidationReportHash": canonical_sha256(final_validation_report),
        "auditReportHash": canonical_sha256(audit_report),
        "changedPathCount": len(entries) - 1,
        "deletedPathCount": len(categories.get("deleted", [])),
        "copiedFileCount": len(entries) - 1,
        "totalBundleBytes": sum(item["size"] for item in entries),
        "patchSha256": file_sha256(patch_path),
        "entries": entries,
        "sha256SumsSha256": sums_receipt.sha256,
        "sha256SumsSize": sums_receipt.size,
        "sha256SumsExcludedPaths": ["RESULT_MANIFEST.json", "SHA256SUMS.json"],
        "coveredFileCount": hashes["totalFiles"],
        "coveredTotalBytes": hashes["totalBytes"],
        "complete": True,
        "errorCode": None,
        "errorMessage": None,
    }
    if len(entries) + 2 > int(policy["maxBundleFiles"]) or _bundle_bytes(temp_bundle) > int(policy["maxBundleBytes"]):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Result bundle exceeds host caps.")
    write_trusted_report(automation_root, root / "CodexAutomation" / "runtime", temp_bundle / "RESULT_MANIFEST.json", manifest, "real_task_bundle_manifest.schema.json", _validate_bundle_manifest, int(policy["maxBundleBytes"]))
    _verify_bundle(temp_bundle, manifest, hashes, int(policy["maxBundleFiles"]), int(policy["maxBundleBytes"]))
    temp_bundle.rename(bundle_dir)
    _verify_bundle(bundle_dir, manifest, read_json(bundle_dir / "SHA256SUMS.json"), int(policy["maxBundleFiles"]), int(policy["maxBundleBytes"]))
    bundle_report = {
        "reportVersion": 1,
        "orchestrationRunId": context["orchestrationRunId"],
        "taskId": context["taskId"],
        "stage": "BOOTSTRAP-03B-2C",
        "bundleManifestHash": canonical_sha256(manifest),
        "bundleIntegrityValid": True,
        "errorCode": None,
        "errorMessage": None,
    }
    write_trusted_report(automation_root, root / "CodexAutomation" / "runtime", run_dir / "RESULT_BUNDLE_REPORT.json", bundle_report, "real_task_bundle_report.schema.json", None, int(policy["maxBundleBytes"]))
    return manifest, canonical_sha256(manifest)


def _deterministic_patch(diff: dict[str, Any]) -> str:
    lines: list[str] = []
    categories = diff.get("categories", {})
    for category in ("added", "modified", "deleted", "typeChanged"):
        for entry in categories.get(category, []):
            before = entry.get("before") or {}
            after = entry.get("after") or {}
            path = (after or before).get("path", "")
            lines.append(f"--- {path}")
            lines.append(f"+++ {path}")
            lines.append(f"@@ {category} @@")
    return "\n".join(lines) + ("\n" if lines else "")


def _sha256sums(bundle_dir: Path, excluded: tuple[str, ...]) -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    excluded_set = set(excluded)
    for path in sorted(item for item in bundle_dir.rglob("*") if item.is_file()):
        rel = path.relative_to(bundle_dir).as_posix()
        if rel in excluded_set:
            continue
        entries.append({"relativePath": rel, "size": path.stat().st_size, "sha256": file_sha256(path)})
    return {
        "reportVersion": 1,
        "entries": entries,
        "totalFiles": len(entries),
        "totalBytes": sum(item["size"] for item in entries),
        "aggregateHash": canonical_sha256(entries),
        "excludedPaths": list(excluded),
    }


def _bundle_bytes(bundle_dir: Path) -> int:
    return sum(path.stat().st_size for path in bundle_dir.rglob("*") if path.is_file())


def _verify_bundle(bundle_dir: Path, manifest: dict[str, Any], hashes: dict[str, Any], max_files: int, max_bytes: int) -> None:
    if any(path.is_symlink() for path in bundle_dir.rglob("*")):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle contains symlink.")
    files = sorted(path.relative_to(bundle_dir).as_posix() for path in bundle_dir.rglob("*") if path.is_file())
    if len(files) > max_files or _bundle_bytes(bundle_dir) > max_bytes:
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle exceeds caps.")
    covered = {entry["relativePath"]: entry for entry in hashes.get("entries", [])}
    if canonical_sha256(hashes) != manifest.get("sha256SumsSha256") or (bundle_dir / "SHA256SUMS.json").stat().st_size != manifest.get("sha256SumsSize"):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "SHA256SUMS binding mismatch.")
    if hashes.get("excludedPaths") != manifest.get("sha256SumsExcludedPaths"):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "SHA256SUMS exclusions mismatch.")
    if hashes.get("totalFiles") != manifest.get("coveredFileCount") or hashes.get("totalBytes") != manifest.get("coveredTotalBytes"):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "SHA256SUMS totals mismatch.")
    errors = _validate_sha256sums(hashes, bundle_dir)
    if errors:
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", errors[0])
    for rel, entry in covered.items():
        path = bundle_dir / rel
        if not path.exists() or file_sha256(path) != entry["sha256"] or path.stat().st_size != entry["size"]:
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle payload hash mismatch.")
    expected_uncovered = {"RESULT_MANIFEST.json", "SHA256SUMS.json"}
    if set(files) - set(covered) != expected_uncovered:
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle has unexpected uncovered files.")


def _validate_bundle_manifest(report: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if report.get("eligibleForApply") is not False:
        errors.append("Bundle manifest must keep eligibleForApply=false.")
    if report.get("sha256SumsExcludedPaths") != ["RESULT_MANIFEST.json", "SHA256SUMS.json"]:
        errors.append("Bundle manifest must declare fixed SHA256SUMS exclusions.")
    if report.get("sha256SumsSize", 0) <= 0:
        errors.append("Bundle manifest must bind SHA256SUMS size.")
    return errors


def _validate_sha256sums(report: dict[str, Any], bundle_dir: Path) -> list[str]:
    errors: list[str] = []
    entries = report.get("entries", [])
    if not isinstance(entries, list):
        return ["SHA256SUMS entries must be an array."]
    paths = [str(item.get("relativePath")) for item in entries if isinstance(item, dict)]
    if paths != sorted(paths) or len(paths) != len(set(paths)):
        errors.append("SHA256SUMS entries must be sorted and unique.")
    if report.get("excludedPaths") != ["RESULT_MANIFEST.json", "SHA256SUMS.json"]:
        errors.append("SHA256SUMS exclusions are invalid.")
    total_bytes = 0
    for item in entries:
        if not isinstance(item, dict):
            errors.append("SHA256SUMS entry must be an object.")
            continue
        rel = str(item.get("relativePath"))
        try:
            _validate_bundle_relative_path(rel)
        except RealTaskExecutionFailure as exc:
            errors.append(exc.message)
            continue
        path = bundle_dir / rel
        if rel in {"RESULT_MANIFEST.json", "SHA256SUMS.json"}:
            errors.append("SHA256SUMS cannot cover excluded files.")
        if not path.exists() or path.is_symlink() or not path.is_file():
            errors.append("SHA256SUMS covered file is missing or unsafe.")
            continue
        if path.stat().st_size != item.get("size") or file_sha256(path) != item.get("sha256"):
            errors.append("SHA256SUMS covered file hash or size mismatch.")
        total_bytes += int(item.get("size", 0))
    if report.get("totalFiles") != len(entries) or report.get("totalBytes") != total_bytes:
        errors.append("SHA256SUMS totals are inconsistent.")
    if report.get("aggregateHash") != canonical_sha256(entries):
        errors.append("SHA256SUMS aggregate hash mismatch.")
    return errors


def _validate_bundle_source_path(path: str) -> str:
    _validate_bundle_relative_path(path)
    if path == "RESULT_MANIFEST.json" or path == "SHA256SUMS.json" or path.startswith("files/"):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Changed source path is reserved by bundle.")
    return path


def _validate_bundle_relative_path(path: str) -> None:
    if not path or "\\" in path or ":" in path or path.startswith("/") or path.startswith("//"):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle path is not a safe relative POSIX path.")
    pure = PurePosixPath(path)
    if pure.is_absolute() or any(part in {"", ".", ".."} for part in pure.parts):
        raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle path contains an unsafe component.")
    reserved = {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(1, 10)), *(f"LPT{i}" for i in range(1, 10))}
    for part in pure.parts:
        stem = part.split(".")[0].upper()
        if stem in reserved:
            raise RealTaskExecutionFailure("REAL_TASK_BUNDLE_INTEGRITY_FAILED", "Bundle path contains a reserved Windows name.")


def _has_reparse_parent(path: Path) -> bool:
    current = Path(path.anchor)
    for part in path.parts[1:-1]:
        current = current / part
        if not current.exists():
            continue
        try:
            if current.is_symlink():
                return True
            attrs = getattr(current.stat(follow_symlinks=False), "st_file_attributes", 0)
            if attrs & 0x400:
                return True
        except OSError:
            return True
    return False
