# TEST-001: Read-only Codex Automation Smoke Test

This task verifies that BOOTSTRAP-02 can invoke Codex CLI non-interactively without changing the Unity project.

Instructions:

1. Work only in analysis/read-only mode.
2. Do not modify any files.
3. Read:
   - `ProjectSettings/ProjectVersion.txt`
   - `CodexAutomation/config.json`
   - `CodexAutomation/workflow.seed.json`
4. Return:
   - the Unity version found in `ProjectSettings/ProjectVersion.txt`
   - the workflow id found in `CodexAutomation/workflow.seed.json`
   - the number of tasks in the workflow
   - confirmation that no changes were made
   - a short summary of the current automation bootstrap purpose
5. Do not launch Unity.
6. Do not run Git commands that change state.
7. Do not create files.
8. Do not propose Unity code fixes.
9. Do not proceed to BOOTSTRAP-03.

The final response must be only the JSON object requested by the system prompt.
