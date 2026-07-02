from __future__ import annotations

import json
import os
import platform
import subprocess
import sys
import time
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "CodexAutomation" / "scripts"
sys.path.insert(0, str(SCRIPTS))

from codex_runner import run_process_with_timeout  # noqa: E402
from file_utils import write_json_atomic  # noqa: E402


def main() -> int:
    runtime_results = ROOT / "CodexAutomation" / "runtime" / "results"
    runtime_results.mkdir(parents=True, exist_ok=True)
    child_pid_path = runtime_results / "process_tree_timeout_child_pid.json"
    report_path = runtime_results / "process_tree_timeout_check.json"
    child_pid_path.unlink(missing_ok=True)

    child_code = """
import json
import os
import subprocess
import sys
import time
from pathlib import Path

pid_path = Path(sys.argv[1])
child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(30)"])
pid_path.write_text(json.dumps({"parentPid": os.getpid(), "childPid": child.pid}), encoding="utf-8")
time.sleep(30)
"""

    result = run_process_with_timeout(
        [sys.executable, "-c", child_code, str(child_pid_path)],
        ROOT,
        "",
        1,
    )

    child_info: dict[str, Any] = {}
    if child_pid_path.exists():
        child_info = json.loads(child_pid_path.read_text(encoding="utf-8"))

    child_alive = False
    child_pid = child_info.get("childPid")
    if isinstance(child_pid, int):
        child_alive = _is_process_alive(child_pid)

    passed = (
        result["timed_out"] is True
        and result["termination"]["attempted"] is True
        and (platform.system().lower() != "windows" or result["termination"]["command"][:1] == ["taskkill"])
        and not child_alive
    )

    report = {
        "passed": passed,
        "platform": platform.system(),
        "timedOut": result["timed_out"],
        "processPid": result["pid"],
        "processTreeTerminationAttempted": result["termination"]["attempted"],
        "processTreeTerminationSucceeded": result["termination"]["succeeded"],
        "processTreeTerminationCommand": result["termination"]["command"],
        "processTreeTerminationExitCode": result["termination"]["exit_code"],
        "processTreeTerminationStderr": result["termination"]["stderr"],
        "childPid": child_pid,
        "childAliveAfterTimeout": child_alive,
    }
    write_json_atomic(report_path, report)

    print(json.dumps(report, indent=2))
    return 0 if passed else 1


def _is_process_alive(pid: int) -> bool:
    if platform.system().lower() == "windows":
        completed = subprocess.run(
            ["tasklist", "/FI", f"PID eq {pid}"],
            capture_output=True,
            text=True,
            check=False,
        )
        return str(pid) in completed.stdout

    try:
        os.kill(pid, 0)
    except OSError:
        return False
    return True


if __name__ == "__main__":
    raise SystemExit(main())
