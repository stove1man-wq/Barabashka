---
name: Reliable Tool Execution
description: Rules for truthful tool use and safe development in this Unity Android project
alwaysApply: true
---

# Reliable tool execution

- Use Agent mode when a task requires terminal commands or file changes.
- Invoke the actual available tool. Do not print JSON, XML, or pseudocode as a substitute for a tool call.
- For terminal work, use the built-in Run terminal command tool.
- Wait for the actual tool result before reporting success.
- Never infer successful execution from a proposed command or generated text.
- If execution requires user approval, request it and wait.
- If a tool call fails, report the exact failure and do not repeat the same test indefinitely.
- After two failed attempts with the same approach, reassess the cause.
- Start with read-only inspection commands where possible.
- Ask before destructive operations, deleting files, resetting Git, or changing major architecture.
- Follow AGENTS.md and the project's architecture and progress documentation.
- Unity and C# remain the primary application stack. Do not migrate the project to native Android without approval.
- Preserve existing user changes and inspect git status before editing project files.
