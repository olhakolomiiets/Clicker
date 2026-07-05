from __future__ import annotations

from typing import Any

import local_validators
from validator_models import VALIDATOR_FAIL_CODES_BY_TYPE, VALIDATOR_TYPES, ValidatorDefinition, ValidatorPolicy, ValidationFailure, canonical_sha256


_REGISTRY = {
    "file_exists": ("validator.file_exists.v1", "file_path_v1", True, False, False, 0, local_validators.file_exists),
    "file_absent": ("validator.file_absent.v1", "file_path_v1", True, False, False, 0, local_validators.file_absent),
    "json_valid": ("validator.json_valid.v1", "file_path_v1", True, False, False, 1, local_validators.json_valid),
    "json_schema": ("validator.json_schema.v1", "json_schema_name_v1", True, False, False, 1, local_validators.json_schema),
    "json_field_equals": ("validator.json_field_equals.v1", "json_pointer_equals_v1", True, False, False, 1, local_validators.json_field_equals),
    "text_contains": ("validator.text_contains.v1", "text_literal_v1", True, False, False, 1, local_validators.text_contains),
    "text_not_contains": ("validator.text_not_contains.v1", "text_literal_v1", True, False, False, 1, local_validators.text_not_contains),
    "changed_paths_exact": ("validator.changed_paths_exact.v1", "path_set_v1", False, True, False, 0, local_validators.changed_paths_exact),
    "changed_paths_subset": ("validator.changed_paths_subset.v1", "path_set_v1", False, True, False, 0, local_validators.changed_paths_subset),
    "no_unexpected_files": ("validator.no_unexpected_files.v1", "expected_outputs_v1", False, True, True, 0, local_validators.no_unexpected_files),
    "no_conflict_markers": ("validator.no_conflict_markers.v1", "optional_path_set_v1", True, True, False, 64, local_validators.no_conflict_markers),
    "max_file_size": ("validator.max_file_size.v1", "file_size_v1", True, False, False, 0, local_validators.max_file_size),
    "max_changed_files": ("validator.max_changed_files.v1", "changed_file_count_v1", False, True, False, 0, local_validators.max_changed_files),
    "extension_allowlist": ("validator.extension_allowlist.v1", "extension_allowlist_v1", False, True, False, 0, local_validators.extension_allowlist),
}


def load_validator_registry(policy: ValidatorPolicy) -> tuple[dict[str, ValidatorDefinition], dict[str, Any]]:
    definitions: dict[str, ValidatorDefinition] = {}
    if tuple(_REGISTRY) != VALIDATOR_TYPES:
        raise ValidationFailure("REAL_TASK_VALIDATOR_REGISTRY_INVALID", "Validator registry order or set differs from the approved set.")
    seen_impl: set[str] = set()
    for validator_type in VALIDATOR_TYPES:
        version, contract, needs_read, needs_diff, needs_outputs, max_files, implementation = _REGISTRY[validator_type]
        if not callable(implementation):
            raise ValidationFailure("REAL_TASK_VALIDATOR_REGISTRY_INVALID", "Validator implementation is not callable.")
        if version in seen_impl:
            raise ValidationFailure("REAL_TASK_VALIDATOR_REGISTRY_INVALID", "Duplicate validator implementation id.")
        seen_impl.add(version)
        definitions[validator_type] = ValidatorDefinition(
            type=validator_type,
            version="1",
            implementationId=version,
            argumentContract=contract,
            needsWorkspaceRead=needs_read,
            needsDiff=needs_diff,
            needsExpectedOutputs=needs_outputs,
            maxFilesRead=max_files,
            allowedFailCodes=tuple(sorted(VALIDATOR_FAIL_CODES_BY_TYPE[validator_type])),
            noSideEffects=True,
            implementation=implementation,
        )
    if tuple(policy.allowedValidatorTypes) != VALIDATOR_TYPES:
        raise ValidationFailure("REAL_TASK_VALIDATOR_REGISTRY_INVALID", "Configured validator types must exactly match the approved set in deterministic order.")
    snapshot = {
        "registryVersion": 1,
        "stage": "BOOTSTRAP-03B-2B-C",
        "validatorTypes": [definitions[name].snapshot() for name in VALIDATOR_TYPES],
    }
    snapshot["registrySha256"] = canonical_sha256(snapshot)
    return definitions, snapshot
