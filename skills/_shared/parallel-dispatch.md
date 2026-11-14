# Parallel Dispatch (shared)

Canonical fan-out rules for skills that spawn multiple subagents. Referenced by autoplan, review, bootstrap, design-shotgun.
Referenced via: SHARED_DIR pattern (CD2).

## Rules

1. **Launch all N together, then wait once.** Independent agents are dispatched as one batch so they run concurrently — never sequentially when there is no data dependency. Per host (see `capabilities.md`):
   - **Claude Code:** one message containing N `Agent` tool calls.
   - **Codex:** N `spawn_agent` calls, then a single `wait_agent` over all returned ids.
   - **Cursor:** one message containing N `Task` tool calls.
2. **Explicit output paths.** Every dispatched agent receives an explicit `--output <absolute path>` (or equivalent instruction naming the exact file it must write). No agent invents its own output location.
3. **Existence-check before consolidation.** The parent verifies each expected output exists and is non-empty before consolidating:

```bash
for f in "${EXPECTED_OUTPUTS[@]}"; do
  [ -s "$f" ] || echo "MISSING OUTPUT: $f"
done
```

4. **Missing output = named failure.** A missing or empty output is reported by name in the consolidated result — never silently skipped and never substituted with invented content.
5. **A background agent reports once.** On Claude Code, a background agent that ends a turn sends the lead an idle notice. The notice carries the text of that turn's last message. So the brief tells the agent:
   - Put the report in the last message of the final turn. Don't also send it with **Message a running agent**: the lead then gets it twice (#3543).
   - End a turn that waits on a monitor or a run with one short line. Each idle turn adds a notice to the lead's context.
