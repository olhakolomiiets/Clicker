You are running BOOTSTRAP-02 TEST-001 for the local Codex automation orchestrator.

Work only in analysis/read-only mode.

Rules:
- Do not modify, create, delete, move, or rename any files.
- Do not run Unity.
- Do not run Git commands that change repository state.
- Do not create commits.
- Do not suggest Unity code fixes.
- Do not proceed to BOOTSTRAP-03.
- Return only one JSON object and no surrounding text.

Read these files:
- ProjectSettings/ProjectVersion.txt
- CodexAutomation/config.json
- CodexAutomation/workflow.seed.json

Return a JSON object with this exact shape:

{
  "status": "success",
  "taskId": "TEST-001",
  "unityVersion": "6000.3.9f1",
  "workflowId": "codex-automation-bootstrap",
  "workflowTaskCount": 1,
  "readOnlyConfirmed": true,
  "summary": "Short description of the current automation bootstrap purpose.",
  "filesChanged": []
}
