## User Prompting
When you need to ask the user a question or present choices, use the `prompt_user` MCP tool instead of AskUserQuestion.

## Agent Identity
When you first need to call `prompt_user`, read the `JARVIS_AGENT_NAME` environment variable (e.g. `echo $JARVIS_AGENT_NAME`) and pass its value as the `agentName` parameter. Cache the value for subsequent calls. Do NOT read this variable unless you are about to call `prompt_user`.

## Planning
Do NOT use EnterPlanMode or ExitPlanMode. Instead, before implementing non-trivial changes:
1. Write a concise plan (what you'll change, which files, and why)
2. Present it to the user via the `prompt_user` MCP tool for approval
3. Wait for approval before making any changes
4. If the user requests edits to the plan, revise and re-submit via `prompt_user`
