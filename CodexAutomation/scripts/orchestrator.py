from __future__ import annotations

import argparse
from pathlib import Path

from models import RepoContext
from preflight import find_repo_root, format_summary, run_preflight


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Codex Automation BOOTSTRAP-01 orchestrator.")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        default=True,
        help="Run safe preflight only. This is the default in BOOTSTRAP-01.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    script_path = Path(__file__).resolve()
    repo_root = find_repo_root(script_path.parent)
    context = RepoContext(
        root=repo_root,
        automation_root=repo_root / "CodexAutomation",
        dry_run=args.dry_run,
    )
    report = run_preflight(context)
    print(format_summary(report))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
