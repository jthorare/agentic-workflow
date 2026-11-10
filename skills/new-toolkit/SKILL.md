---
name: new-toolkit
description: Scaffold a Sindri toolkit, a small C# MCP server that gives agents typed tools and declares the database, disk and connections it needs. Builds with warnings as errors under its own build rails and leaves one red test. Lives outside the repo, in the Sindri home.
argument-hint: "<Name> <tool_name> [needs] [dir]"
allowed-tools: Bash(node *), Bash(dotnet *), Read, Grep, Glob, AskUserQuestion
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

# /new-toolkit

Creates a toolkit project exactly as `docs/csharp/scaffolds/new-toolkit.md` specifies (that file is the contract; this skill
drives its generator, `scripts/scaffold/new-toolkit.mjs`). A toolkit is not a repo package: it lives in the Sindri home
(`$AW_SINDRI_HOME/toolkits/<name>/` by default), references the `Aw.Sindri.Toolkit` SDK from a local folder feed, and carries
its own `Directory.Build.props`, `.editorconfig`, `global.json`, `Directory.Packages.props` and `nuget.config`. It builds with
zero warnings and has one red test named for the first tool, so your first move is the TDD step.

The generator needs the SDK package in the feed. `scripts/install-cs.sh` puts it there; without it the generator refuses with `NT-SDK`.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Git mode

`${AW_GIT_MODE:-full}`: this skill writes no branch, commit or pull request, so the mode changes nothing here. The toolkit folder is outside any repo; whether it becomes a repo is the person's call.

## 1. Collect the inputs

Take what the arguments give, then **Ask the user** for every input still missing. Never guess a name or a tool.

| Input | Flag | Rule |
|---|---|---|
| name | `--name` | PascalCase, `^[A-Z][A-Za-z0-9]{2,30}$`, not Common/Shared/Utils/Helpers |
| first tool | `--tool` | snake_case, e.g. `diff_contracts` |
| needs | `--needs` | optional comma list: `database:<name>`, `disk:<size>` (or `disk:<name>:<size>`), `connection:<name>` (or `connection:<service>:<name>`); names `[a-z0-9-]` |
| folder | `--dir` | optional; default `$AW_SINDRI_HOME/toolkits/<kebab-name>`; must be new or empty |

Ask what the tool should do and what it needs (a place to keep state is a database, big files are a disk, an outside account is a
connection) before choosing needs.

## 2. Run the generator

```bash
node "${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/scripts/scaffold/new-toolkit.mjs" --name <Name> --tool <snake_case> \
  [--needs database:cache,disk:1g,connection:me] [--dir <folder>]
```

It takes a slot of the heavy-job lock itself (or runs inside one, under `AW_HEAVY_LOCK_HELD=1`), writes the files, then restores,
builds with `-warnaserror`, runs the unit lane (exactly one failure: the sample tool's test) and checks format on the files it wrote.
Any other failure reverts every file and folder it made and prints the rule id, the measured value and the fix.

Refusals (exit 1, nothing written): `NT-INPUT`, `NT-NAME`, `NT-TOOL`, `NT-NEEDS`, `NT-EXISTS`, `NT-SDK`, `NT-PINS`. Fix the input (ask the user
when it's theirs to decide) and run it again. On `NT-SDK`, tell the user to run `scripts/install-cs.sh` from the agentic-workflow checkout.

## 3. Hand over

Report the folder, the files the generator listed and the one red test. The next step: replace the stub in `<Name>Tools.cs` so
`<Tool>Tests` passes, then add tools with `.Tool<Req,Res>(...)`. `dotnet run -- --manifest` prints the toolkit's manifest.

## Next steps

- `/new-slice` is for repo packages, not toolkits: add a tool by editing the toolkit's `<Name>Tools.cs` and its tests
- `/new-package` — when the work belongs in the repo as a package instead

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/new-toolkit-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`new-toolkit`), `artifacts` (absolute paths: the toolkit project, its test project and the sample tool file), `summary` (one line),
plus `toolkit` (the toolkit name); `red_test` (the one red test it leaves).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
