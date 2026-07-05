from __future__ import annotations

from pathlib import Path, PureWindowsPath
from typing import Any

from file_utils import read_json
from schema_validator import validate, validate_schema_keywords
from validator_models import ValidationFailure, ValidatorPolicy, canonical_sha256
from workspace_inventory import is_symlink_or_reparse


class ValidatorSchemaRegistry:
    def __init__(self, root: Path, automation_root: Path, policy: ValidatorPolicy) -> None:
        self.root = root
        self.automation_root = automation_root
        self.policy = policy
        self.registry_root = automation_root / "validator_schemas"
        self._entries = self._load_registry()

    def snapshot(self) -> dict[str, Any]:
        data = {
            "registryVersion": 1,
            "schemas": [
                {
                    "schemaName": item["schemaName"],
                    "schemaVersion": item["schemaVersion"],
                    "relativePath": item["relativePath"],
                    "canonicalSchemaSha256": item["canonicalSchemaSha256"],
                    "maxBytes": item["maxBytes"],
                }
                for item in self._entries.values()
            ],
        }
        data["registrySha256"] = canonical_sha256(data)
        return data

    def load_schema(self, schema_name: str) -> dict[str, Any]:
        if schema_name not in self._entries:
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_UNKNOWN", "schemaName is not registered.")
        entry = self._entries[schema_name]
        path = self._resolve_schema_path(entry["relativePath"])
        if path.stat(follow_symlinks=False).st_size > min(int(entry["maxBytes"]), self.policy.maxSchemaBytes):
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_INVALID", "Registered schema exceeds maxBytes.")
        schema = read_json(path)
        if _contains_remote_ref(schema):
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_INVALID", "Remote schema references are forbidden.")
        errors = validate_schema_keywords(schema)
        if errors:
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_INVALID", errors[0])
        if canonical_sha256(schema) != entry["canonicalSchemaSha256"]:
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_INVALID", "Registered schema canonical hash mismatch.")
        return schema

    def _load_registry(self) -> dict[str, dict[str, Any]]:
        registry_path = self.registry_root / "registry.json"
        if is_symlink_or_reparse(registry_path):
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema registry path is unsafe.")
        raw = read_json(registry_path)
        schema_path = self.automation_root / "schemas" / "validator_schema_registry.schema.json"
        if schema_path.exists():
            schema_errors = validate(raw, read_json(schema_path))
            if schema_errors:
                raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", schema_errors[0])
        if not isinstance(raw, dict) or set(raw) != {"registryVersion", "schemas"} or raw.get("registryVersion") != 1:
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema registry root contract is invalid.")
        schemas = raw.get("schemas")
        if not isinstance(schemas, list):
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema registry schemas must be an array.")
        entries: dict[str, dict[str, Any]] = {}
        for item in schemas:
            if not isinstance(item, dict):
                raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema registry entry must be an object.")
            required = {"schemaName", "schemaVersion", "relativePath", "canonicalSchemaSha256", "maxBytes"}
            if set(item) != required:
                raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema registry entry fields are invalid.")
            name = item["schemaName"]
            if not isinstance(name, str) or not name or "/" in name or "\\" in name or ":" in name or name in entries:
                raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema registry name is invalid or duplicated.")
            if not isinstance(item["schemaVersion"], int) or isinstance(item["schemaVersion"], bool) or item["schemaVersion"] < 1:
                raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema version must be a positive integer.")
            if not isinstance(item["maxBytes"], int) or isinstance(item["maxBytes"], bool) or item["maxBytes"] < 1 or item["maxBytes"] > self.policy.maxSchemaBytes:
                raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema maxBytes is invalid.")
            self._resolve_schema_path(item["relativePath"])
            entries[name] = dict(item)
        return dict(sorted(entries.items()))

    def _resolve_schema_path(self, relative_path: Any) -> Path:
        if not isinstance(relative_path, str) or not relative_path or "\\" in relative_path or ":" in relative_path:
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema relativePath is invalid.")
        windows = PureWindowsPath(relative_path)
        if Path(relative_path).is_absolute() or windows.drive or windows.root or ".." in relative_path.split("/"):
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema relativePath escapes registry root.")
        path = (self.registry_root / relative_path).resolve(strict=True)
        try:
            path.relative_to(self.registry_root.resolve(strict=True))
        except ValueError as exc:
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema path escapes registry root.") from exc
        if is_symlink_or_reparse(path):
            raise ValidationFailure("REAL_TASK_VALIDATOR_SCHEMA_REGISTRY_INVALID", "Schema path is symlink or reparse point.")
        return path


def load_schema_registry(root: Path, automation_root: Path, policy: ValidatorPolicy) -> ValidatorSchemaRegistry:
    return ValidatorSchemaRegistry(root, automation_root, policy)


def _contains_remote_ref(value: Any) -> bool:
    if isinstance(value, dict):
        for key, item in value.items():
            if key == "$ref":
                return True
            if _contains_remote_ref(item):
                return True
    if isinstance(value, list):
        return any(_contains_remote_ref(item) for item in value)
    return False
