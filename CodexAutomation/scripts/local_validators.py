from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from schema_validator import validate as validate_schema_instance
from validator_models import ValidatorContext, ValidatorOutcome, blocked_outcome, fail_outcome, pass_outcome
from validator_safe_io import added_file_paths, changed_file_paths, changed_paths, final_entry, read_bounded_text, read_strict_json, resolve_workspace_path
from workspace_change_models import file_sha256


def file_exists(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    path = str(entry["path"])
    inventory_entry = final_entry(context, path)
    disk_path = resolve_workspace_path(context, path, require_existing=False)
    disk_exists = disk_path.exists()
    if inventory_entry is None and not disk_exists:
        return fail_outcome(entry["id"], entry["type"], "VALIDATOR_FILE_MISSING", "Expected file is absent.", primary_path=path, validator_index=context.validatorIndex)
    if inventory_entry is None or not disk_exists:
        return blocked_outcome(entry["id"], entry["type"], "VALIDATOR_INVENTORY_DISK_MISMATCH", "Inventory and disk disagree for file_exists.", path, validator_index=context.validatorIndex)
    if inventory_entry.get("type") != "file" or not disk_path.is_file():
        return fail_outcome(entry["id"], entry["type"], "VALIDATOR_FILE_TYPE_MISMATCH", "Expected path is not a file.", primary_path=path, validator_index=context.validatorIndex)
    stat_result = disk_path.stat()
    if stat_result.st_size != inventory_entry.get("size") or file_sha256(disk_path) != inventory_entry.get("sha256"):
        return blocked_outcome(entry["id"], entry["type"], "VALIDATOR_INVENTORY_DISK_MISMATCH", "File inventory size/hash differs from disk.", path, validator_index=context.validatorIndex)
    return pass_outcome(entry["id"], entry["type"], primary_path=path, files_read=0, bytes_read=0, validator_index=context.validatorIndex)


def file_absent(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    path = str(entry["path"])
    inventory_entry = final_entry(context, path)
    disk_path = resolve_workspace_path(context, path, require_existing=False)
    if inventory_entry is None and not disk_path.exists():
        return pass_outcome(entry["id"], entry["type"], primary_path=path, validator_index=context.validatorIndex)
    if (inventory_entry is None) != (not disk_path.exists()):
        return blocked_outcome(entry["id"], entry["type"], "VALIDATOR_INVENTORY_DISK_MISMATCH", "Inventory and disk disagree for absent file check.", validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_FILE_PRESENT", "Expected file to be absent.", primary_path=path, validator_index=context.validatorIndex)


def json_valid(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    try:
        read_strict_json(context, str(entry["path"]))
    except ValueError as exc:
        return fail_outcome(entry["id"], entry["type"], "VALIDATOR_JSON_INVALID", str(exc), primary_path=str(entry["path"]), validator_index=context.validatorIndex)
    return pass_outcome(entry["id"], entry["type"], primary_path=str(entry["path"]), files_read=1, validator_index=context.validatorIndex)


def json_schema(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    data = read_strict_json(context, str(entry["path"]))
    schema = context.schemaRegistry.load_schema(str(entry["schemaName"]))
    errors = validate_schema_instance(data, schema)
    if errors:
        return fail_outcome(entry["id"], entry["type"], "VALIDATOR_JSON_SCHEMA_MISMATCH", errors[0], primary_path=str(entry["path"]), validator_index=context.validatorIndex)
    return pass_outcome(entry["id"], entry["type"], primary_path=str(entry["path"]), files_read=1, validator_index=context.validatorIndex)


def json_field_equals(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    data = read_strict_json(context, str(entry["path"]))
    found, actual = _json_pointer(data, str(entry["jsonPointer"]))
    if not found:
        return fail_outcome(entry["id"], entry["type"], "VALIDATOR_JSON_FIELD_MISSING", "JSON pointer target is missing.", primary_path=str(entry["path"]), validator_index=context.validatorIndex)
    if actual != entry.get("expected"):
        return fail_outcome(entry["id"], entry["type"], "VALIDATOR_JSON_FIELD_MISMATCH", "JSON pointer value does not match expected value.", ({"expected": entry.get("expected"), "actual": actual},), primary_path=str(entry["path"]), expected=str(entry.get("expected")), actual=str(actual), validator_index=context.validatorIndex)
    return pass_outcome(entry["id"], entry["type"], primary_path=str(entry["path"]), files_read=1, validator_index=context.validatorIndex)


def text_contains(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    text = read_bounded_text(context, str(entry["path"]))
    if str(entry["text"]) in text:
        return pass_outcome(entry["id"], entry["type"], primary_path=str(entry["path"]), files_read=1, bytes_read=len(text.encode("utf-8")), validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_TEXT_MISSING", "Expected literal text was not found.", primary_path=str(entry["path"]), expected=str(entry["text"]), actual=None, files_read=1, bytes_read=len(text.encode("utf-8")), validator_index=context.validatorIndex)


def text_not_contains(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    text = read_bounded_text(context, str(entry["path"]))
    if str(entry["text"]) not in text:
        return pass_outcome(entry["id"], entry["type"], primary_path=str(entry["path"]), files_read=1, bytes_read=len(text.encode("utf-8")), validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_TEXT_FORBIDDEN", "Forbidden literal text was found.", primary_path=str(entry["path"]), expected=f"not {entry['text']}", actual=str(entry["text"]), files_read=1, bytes_read=len(text.encode("utf-8")), validator_index=context.validatorIndex)


def changed_paths_exact(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    expected = tuple(sorted(str(path) for path in entry["paths"]))
    actual = changed_paths(context)
    if actual == expected:
        return pass_outcome(entry["id"], entry["type"], validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_CHANGED_PATHS_MISMATCH", "Changed paths do not exactly match.", ({"expected": list(expected), "actual": list(actual)},), expected=",".join(expected), actual=",".join(actual), validator_index=context.validatorIndex)


def changed_paths_subset(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    expected = set(str(path) for path in entry["paths"])
    actual = set(changed_paths(context))
    extra = sorted(actual - expected)
    if not extra:
        return pass_outcome(entry["id"], entry["type"], validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_CHANGED_PATHS_EXTRA", "Changed paths are outside expected subset.", ({"unexpected": extra},), actual=",".join(sorted(actual)), validator_index=context.validatorIndex)


def no_unexpected_files(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    expected_paths: list[str] = []
    expected_dirs: list[str] = []
    for output in context.task.get("expectedOutputs", []):
        if isinstance(output, Mapping) and isinstance(output.get("path"), str):
            if output.get("kind") == "directory":
                expected_dirs.append(output["path"])
            else:
                expected_paths.append(output["path"])
    unexpected = []
    for path in added_file_paths(context):
        if path in expected_paths or any(path.startswith(f"{directory.rstrip('/')}/") for directory in expected_dirs):
            continue
        unexpected.append(path)
    if not unexpected:
        return pass_outcome(entry["id"], entry["type"], validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_UNEXPECTED_FILE", "Unexpected added files exist.", ({"unexpected": unexpected},), validator_index=context.validatorIndex)


def no_conflict_markers(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    markers = ("<<<<<<<", "=======", ">>>>>>>")
    paths = entry.get("paths") or _final_text_changed_paths(context)
    failures: list[dict[str, Any]] = []
    for path in paths:
        inventory_entry = final_entry(context, str(path))
        if not inventory_entry or inventory_entry.get("type") != "file" or inventory_entry.get("contentKind") != "text":
            continue
        text = read_bounded_text(context, str(path))
        found = []
        for line in text.splitlines():
            stripped = line.lstrip(" \t")
            for marker in markers:
                if stripped.startswith(marker):
                    found.append(marker)
        if found:
            failures.append({"path": path, "markers": found})
    if not failures:
        return pass_outcome(entry["id"], entry["type"], validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_CONFLICT_MARKER", "Conflict marker text was found.", tuple(failures), validator_index=context.validatorIndex)


def max_file_size(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    path = str(entry["path"])
    inventory_entry = final_entry(context, path)
    disk_path = resolve_workspace_path(context, path, require_existing=True)
    if not inventory_entry or inventory_entry.get("type") != "file" or not disk_path.is_file():
        return fail_outcome(entry["id"], entry["type"], "VALIDATOR_FILE_TYPE_MISMATCH", "Expected final file for size check.", primary_path=path, validator_index=context.validatorIndex)
    disk_size = disk_path.stat().st_size
    if disk_size != inventory_entry.get("size"):
        return blocked_outcome(entry["id"], entry["type"], "VALIDATOR_INVENTORY_DISK_MISMATCH", "File size differs between inventory and disk.", path, validator_index=context.validatorIndex)
    size = inventory_entry.get("size")
    if isinstance(size, int) and size <= int(entry["maxBytes"]):
        return pass_outcome(entry["id"], entry["type"], primary_path=path, validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_FILE_TOO_LARGE", "File exceeds maxBytes.", ({"actual": size, "maxBytes": entry["maxBytes"]},), primary_path=path, expected=str(entry["maxBytes"]), actual=str(size), validator_index=context.validatorIndex)


def max_changed_files(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    actual = int(context.diffReport.get("summary", {}).get("totalChangedFiles", len(changed_file_paths(context))))
    if actual <= int(entry["maxFiles"]):
        return pass_outcome(entry["id"], entry["type"], validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_TOO_MANY_CHANGED_FILES", "Changed file count exceeds maxFiles.", ({"actual": actual, "maxFiles": entry["maxFiles"]},), validator_index=context.validatorIndex)


def extension_allowlist(context: ValidatorContext, entry: dict[str, Any]) -> ValidatorOutcome:
    allowed = {str(extension).lower() for extension in entry["extensions"]}
    failures: list[dict[str, Any]] = []
    for path in _final_changed_file_paths(context):
        inventory_entry = final_entry(context, path)
        if not inventory_entry:
            continue
        extension = str(inventory_entry.get("extension") or "").lower()
        if extension not in allowed:
            failures.append({"path": path, "extension": extension})
    if not failures:
        return pass_outcome(entry["id"], entry["type"], validator_index=context.validatorIndex)
    return fail_outcome(entry["id"], entry["type"], "VALIDATOR_EXTENSION_FORBIDDEN", "Changed file extension is outside allowlist.", tuple(failures), validator_index=context.validatorIndex)


def _final_changed_file_paths(context: ValidatorContext) -> tuple[str, ...]:
    result: list[str] = []
    categories = context.diffReport.get("categories", {})
    for category in ("added", "modified", "typeChanged"):
        for item in categories.get(category, []):
            after = item.get("after")
            if after and after.get("type") == "file":
                result.append(str(item["path"]))
    return tuple(sorted(set(result)))


def _final_text_changed_paths(context: ValidatorContext) -> tuple[str, ...]:
    result = []
    for path in _final_changed_file_paths(context):
        entry = final_entry(context, path)
        if entry and entry.get("contentKind") == "text":
            result.append(path)
    return tuple(result)


def _json_pointer(data: Any, pointer: str) -> tuple[bool, Any]:
    if pointer == "":
        return True, data
    if not pointer.startswith("/"):
        return False, None
    current = data
    for raw_part in pointer.split("/")[1:]:
        part = raw_part.replace("~1", "/").replace("~0", "~")
        if isinstance(current, dict):
            if part not in current:
                return False, None
            current = current[part]
        elif isinstance(current, list):
            if not part.isdigit():
                return False, None
            index = int(part)
            if index >= len(current):
                return False, None
            current = current[index]
        else:
            return False, None
    return True, current
