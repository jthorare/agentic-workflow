# Dependency Graph

## Runtime Environment

| Requirement | Value |
|-------------|-------|
| Node.js | >= 22 |
| TypeScript target | ES2022 |
| Module system | Node16 (ESM — `"type": "module"` in package.json) |
| Module resolution | Node16 |
| Strict mode | Enabled |

## Package Dependencies

Each TypeScript package (`scorer`, `judge`, `sindri`, `skills/ui-evidence`, `skills/bugFixOrchestrator`) declares its own runtime and dev dependencies in its `package.json`. The declared package graph (tiers and every project, compose, CLI, database and file edge) lives in `deps.yaml` and is checked by `node scripts/check-deps.mjs`.

## External System Dependencies

| System | Binding | Required By |
|--------|---------|-------------|
| GitHub CLI (`gh`) | Shell command | Skills (review, postReview, addressReview) |
| Claude Code / Codex / Cursor | CLI tools (any subset) | Skills execution, MCP server registration, judge model calls (`claude -p`, `codex exec`, `cursor-agent -p`) |
