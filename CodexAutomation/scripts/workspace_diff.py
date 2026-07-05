from __future__ import annotations

from typing import Any

from workspace_change_inventory import entries_by_path
from workspace_change_models import canonical_sha256


def build_workspace_diff(baseline: dict[str, Any], final: dict[str, Any]) -> dict[str, Any]:
    base_entries = entries_by_path(baseline)
    final_entries = entries_by_path(final)
    added: list[dict[str, Any]] = []
    modified: list[dict[str, Any]] = []
    deleted: list[dict[str, Any]] = []
    type_changed: list[dict[str, Any]] = []
    unchanged: list[dict[str, Any]] = []
    for path in sorted(set(base_entries) | set(final_entries)):
        before = base_entries.get(path)
        after = final_entries.get(path)
        if before is None and after is not None:
            added.append(_change(path, "added", None, after))
        elif before is not None and after is None:
            deleted.append(_change(path, "deleted", before, None))
        elif before is not None and after is not None:
            if before["type"] != after["type"]:
                type_changed.append(_change(path, "typeChanged", before, after))
            elif _entry_changed(before, after):
                modified.append(_change(path, "modified", before, after))
            else:
                unchanged.append(_change(path, "unchanged", before, after))
    categories = {
        "added": added,
        "modified": modified,
        "deleted": deleted,
        "typeChanged": type_changed,
        "unchanged": unchanged,
    }
    summary = _summary(categories)
    report = {
        "reportVersion": 1,
        "runId": final["runId"],
        "taskId": final["taskId"],
        "stage": "BOOTSTRAP-03B-2B-B",
        "trustedContextHash": final["trustedContextHash"],
        "baselineInventoryHash": baseline["inventorySha256"],
        "finalInventoryHash": final["inventorySha256"],
        "categories": categories,
        "summary": summary,
        "renameDetection": False,
        "deterministic": True,
        "errorCode": None,
        "errorMessage": None,
    }
    report["diffReportSha256"] = canonical_sha256({key: value for key, value in report.items() if key != "diffReportSha256"})
    return report


def changed_file_paths(diff: dict[str, Any]) -> set[str]:
    result: set[str] = set()
    categories = diff.get("categories", {})
    for name in ("added", "modified", "deleted", "typeChanged"):
        for item in categories.get(name, []):
            before = item.get("before")
            after = item.get("after")
            if (before and before.get("type") == "file") or (after and after.get("type") == "file"):
                result.add(item["path"])
    return result


def _entry_changed(before: dict[str, Any], after: dict[str, Any]) -> bool:
    keys = ("size", "sha256", "contentKind", "textEncoding", "expectedMutability")
    return any(before.get(key) != after.get(key) for key in keys)


def _change(path: str, change_type: str, before: dict[str, Any] | None, after: dict[str, Any] | None) -> dict[str, Any]:
    before_projection = _projection(before)
    after_projection = _projection(after)
    return {
        "path": path,
        "canonicalPathKey": (after or before)["canonicalPathKey"],
        "changeType": change_type,
        "baselineType": before["type"] if before else None,
        "finalType": after["type"] if after else None,
        "baselineSize": before["size"] if before else None,
        "finalSize": after["size"] if after else None,
        "baselineSha256": before["sha256"] if before else None,
        "finalSha256": after["sha256"] if after else None,
        "baselineContentKind": before["contentKind"] if before else None,
        "finalContentKind": after["contentKind"] if after else None,
        "service": bool((before and before.get("expectedMutability") == "immutable_service") or (after and after.get("expectedMutability") == "immutable_service")),
        "allowedWrite": None if change_type == "unchanged" else False,
        "allowedDelete": None if change_type == "unchanged" else False,
        "violationCodes": [],
        "before": before_projection,
        "after": after_projection,
    }


def _projection(entry: dict[str, Any] | None) -> dict[str, Any] | None:
    if entry is None:
        return None
    return {
        "path": entry["path"],
        "canonicalPathKey": entry["canonicalPathKey"],
        "type": entry["type"],
        "size": entry["size"],
        "sha256": entry["sha256"],
        "extension": entry["extension"],
        "contentKind": entry["contentKind"],
        "textEncoding": entry["textEncoding"],
        "serviceKind": entry["serviceKind"],
        "serviceOrigin": entry["serviceOrigin"],
        "expectedMutability": entry["expectedMutability"],
        "sourceClassification": entry["sourceClassification"],
        "symlink": entry["symlink"],
        "reparse": entry["reparse"],
    }


def _summary(categories: dict[str, Any]) -> dict[str, int]:
    changed_files: set[str] = set()
    changed_directories: set[str] = set()
    changed_bytes = 0
    deleted_bytes = 0
    for name in ("added", "modified", "deleted", "typeChanged"):
        for item in categories[name]:
            before = item.get("before")
            after = item.get("after")
            path = item["path"]
            before_file = bool(before and before["type"] == "file")
            after_file = bool(after and after["type"] == "file")
            if before_file or after_file:
                changed_files.add(path)
            else:
                changed_directories.add(path)
            if name in {"added", "modified", "typeChanged"} and after_file:
                changed_bytes += int(after["size"])
            if name in {"deleted", "typeChanged"} and before_file:
                deleted_bytes += int(before["size"])
    return {
        "totalChangedFiles": len(changed_files),
        "totalChangedDirectories": len(changed_directories),
        "totalChangedPaths": len(changed_files | changed_directories),
        "totalChangedBytes": changed_bytes,
        "totalDeletedBytes": deleted_bytes,
        "totalTouchedBytes": changed_bytes + deleted_bytes,
    }
