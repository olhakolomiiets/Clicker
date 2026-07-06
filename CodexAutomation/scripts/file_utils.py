from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any, TextIO


def read_json(path: Path) -> Any:
    with open(_fs_path(path), "r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json_atomic(path: Path, data: Any) -> None:
    os.makedirs(_fs_path(path.parent), exist_ok=True)
    fd, temp_name = _mkstemp(path)

    temp_path = Path(temp_name)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(data, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(_fs_path(temp_path), _fs_path(path))
    except Exception:
        try:
            os.unlink(_fs_path(temp_path))
        except FileNotFoundError:
            pass
        except OSError:
            pass
        raise


def write_text_long_safe(
    path: Path,
    text: str,
    *,
    encoding: str = "utf-8",
    newline: str | None = "\n",
    create_parent: bool = True,
    fsync: bool = False,
) -> None:
    if create_parent:
        os.makedirs(_fs_path(path.parent), exist_ok=True)
    with open(_fs_path(path), "w", encoding=encoding, newline=newline) as handle:
        handle.write(text)
        handle.flush()
        if fsync:
            os.fsync(handle.fileno())


def read_text_long_safe(path: Path, *, encoding: str = "utf-8", errors: str = "strict") -> str:
    with open(_fs_path(path), "r", encoding=encoding, errors=errors) as handle:
        return handle.read()


def open_text_long_safe(
    path: Path,
    mode: str,
    *,
    encoding: str = "utf-8",
    newline: str | None = "\n",
    create_parent: bool = True,
) -> TextIO:
    if create_parent and any(flag in mode for flag in ("w", "a", "x", "+")):
        os.makedirs(_fs_path(path.parent), exist_ok=True)
    return open(_fs_path(path), mode, encoding=encoding, newline=newline)


def _fs_path(path: Path) -> str:
    resolved = path.resolve(strict=False)
    text = str(resolved)
    if os.name == "nt" and not text.startswith("\\\\?\\"):
        return "\\\\?\\" + text
    return text


def _mkstemp(path: Path) -> tuple[int, str]:
    kwargs = {
        "prefix": f".{path.name}.",
        "suffix": ".tmp",
        "dir": str(path.parent),
        "text": True,
    }
    try:
        return tempfile.mkstemp(**kwargs)
    except FileNotFoundError:
        if os.name != "nt":
            raise
        kwargs["dir"] = _fs_path(path.parent)
        return tempfile.mkstemp(**kwargs)
