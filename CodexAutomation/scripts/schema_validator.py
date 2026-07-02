from __future__ import annotations

from typing import Any


SUPPORTED_KEYWORDS = {
    "type",
    "required",
    "properties",
    "additionalProperties",
    "enum",
    "const",
    "items",
    "minItems",
    "maxItems",
}


def validate_schema_keywords(schema: Any, path: str = "$") -> list[str]:
    errors: list[str] = []
    if not isinstance(schema, dict):
        return [f"{path}: schema must be an object."]

    for key, value in schema.items():
        if key not in SUPPORTED_KEYWORDS:
            errors.append(f"{path}: unsupported schema keyword {key!r}.")
            continue
        if key == "properties":
            if not isinstance(value, dict):
                errors.append(f"{path}.properties: must be an object.")
            else:
                for property_name, property_schema in value.items():
                    errors.extend(validate_schema_keywords(property_schema, f"{path}.properties.{property_name}"))
        elif key == "items":
            errors.extend(validate_schema_keywords(value, f"{path}.items"))

    return errors


def validate_instance(instance: Any, schema: dict[str, Any], path: str = "$") -> list[str]:
    errors: list[str] = []
    expected_type = schema.get("type")
    if expected_type is not None and not _matches_type(instance, expected_type):
        errors.append(f"{path}: expected type {expected_type!r}.")
        return errors

    if "const" in schema and instance != schema["const"]:
        errors.append(f"{path}: expected const value {schema['const']!r}.")

    if "enum" in schema:
        enum_values = schema["enum"]
        if not isinstance(enum_values, list):
            errors.append(f"{path}.enum: must be an array.")
        elif instance not in enum_values:
            errors.append(f"{path}: value {instance!r} is not in enum {enum_values!r}.")

    if isinstance(instance, dict):
        errors.extend(_validate_object(instance, schema, path))
    elif isinstance(instance, list):
        errors.extend(_validate_array(instance, schema, path))

    return errors


def validate(instance: Any, schema: Any) -> list[str]:
    keyword_errors = validate_schema_keywords(schema)
    if keyword_errors:
        return keyword_errors
    return validate_instance(instance, schema)


def _matches_type(instance: Any, expected_type: Any) -> bool:
    if expected_type == "object":
        return isinstance(instance, dict)
    if expected_type == "array":
        return isinstance(instance, list)
    if expected_type == "string":
        return isinstance(instance, str)
    if expected_type == "boolean":
        return isinstance(instance, bool)
    if expected_type == "integer":
        return isinstance(instance, int) and not isinstance(instance, bool)
    if expected_type == "number":
        return (isinstance(instance, int) or isinstance(instance, float)) and not isinstance(instance, bool)
    if expected_type == "null":
        return instance is None
    return False


def _validate_object(instance: dict[str, Any], schema: dict[str, Any], path: str) -> list[str]:
    errors: list[str] = []
    required = schema.get("required", [])
    properties = schema.get("properties", {})

    if not isinstance(required, list) or not all(isinstance(item, str) for item in required):
        errors.append(f"{path}.required: must be an array of strings.")
        required = []

    if not isinstance(properties, dict):
        errors.append(f"{path}.properties: must be an object.")
        properties = {}

    for required_name in required:
        if required_name not in instance:
            errors.append(f"{path}: missing required property {required_name!r}.")

    additional_properties = schema.get("additionalProperties", True)
    if additional_properties is not True and additional_properties is not False:
        errors.append(f"{path}.additionalProperties: only boolean values are supported.")

    if additional_properties is False:
        for property_name in instance:
            if property_name not in properties:
                errors.append(f"{path}: unexpected property {property_name!r}.")

    for property_name, property_schema in properties.items():
        if property_name in instance:
            errors.extend(validate_instance(instance[property_name], property_schema, f"{path}.{property_name}"))

    return errors


def _validate_array(instance: list[Any], schema: dict[str, Any], path: str) -> list[str]:
    errors: list[str] = []
    min_items = schema.get("minItems")
    max_items = schema.get("maxItems")

    if min_items is not None:
        if not isinstance(min_items, int):
            errors.append(f"{path}.minItems: must be an integer.")
        elif len(instance) < min_items:
            errors.append(f"{path}: expected at least {min_items} items.")

    if max_items is not None:
        if not isinstance(max_items, int):
            errors.append(f"{path}.maxItems: must be an integer.")
        elif len(instance) > max_items:
            errors.append(f"{path}: expected at most {max_items} items.")

    item_schema = schema.get("items")
    if item_schema is not None:
        for index, item in enumerate(instance):
            errors.extend(validate_instance(item, item_schema, f"{path}[{index}]"))

    return errors
