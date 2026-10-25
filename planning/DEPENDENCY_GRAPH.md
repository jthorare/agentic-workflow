# Dependency Graph

## Runtime Environment

| Requirement | Value |
|-------------|-------|
| .NET | 10 (LTS), SDK pinned in `global.json`; NativeAOT for hook and CLI entry points, `sindri` and `ui-evidence` framework-dependent |
| Node.js | >= 22, setup prerequisite only (`dembrandt`, `prism-mcp`, `xcodebuildmcp`, the schema checkers) |

## Package Dependencies

The C# packages declare their NuGet dependencies in `Directory.Packages.props` (Central Package Management, locked restore, nuget.org only). The declared package graph (tiers and every project, compose, CLI, database and file edge) lives in `deps.yaml` and is checked by `node scripts/check-deps.mjs`.

## External System Dependencies

| System | Binding | Required By |
|--------|---------|-------------|
| GitHub CLI (`gh`) | Shell command | Skills (review, postReview, addressReview) |
| Claude Code / Codex / Cursor | CLI tools (any subset) | Skills execution, MCP server registration, judge model calls (`claude -p`, `codex exec`, `cursor-agent -p`) |
