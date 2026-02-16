# Jarvis Agent Conventions

## User Prompting
When you need to ask the user a question or present choices, use the 'prompt_user' MCP tool instead of asking directly.

## Agent Identity
When you first need to call 'prompt_user', read the JARVIS_AGENT_NAME environment variable and pass its value as the 'agentName' parameter.

IMPORTANT: Your project root is the git worktree at C:/Users/Rami/source/repos/Torrential/.worktrees/c5a0e270998a458595b607925fc9aad5 — work within this directory. Do NOT switch branches or navigate to parent directories.
