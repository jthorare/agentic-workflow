---
name: new-package
description: Scaffold a new C# package (Core + App, or Core, Features, Infrastructure, Host) that passes every check on its first commit and leaves exactly one red test — the TDD start for a port or a new tier-1..3 package.
argument-hint: "<Name> [tier] [errorPrefix] [shape] [host] [firstSlice] [behavior]"
allowed-tools: Bash(node *), Bash(git *), Bash(dotnet *), Read, Grep, Glob, AskUserQuestion
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

# /new-package

Creates a C# package in this repo's `src/` and `tests/` exactly as `docs/csharp/scaffolds/new-package.md` specifies
(that file is the contract; this skill drives its generator, `scripts/scaffold/new-package.mjs`). The package builds
with zero warnings, is declared in `deps.yaml`, is registered with `tests/Aw.Architecture.Tests`, and has one red test
named for the first use case, so your first move is the TDD step.

Run it from the repo root of a checkout of this repo (the C# build lives here). Rules the output satisfies:
`.agents/rules/csharp.md` and `.agents/rules/csharp-testing.md`.

## 1. Collect the inputs

Take what the arguments give, then **Ask the user** for every input still missing. Never guess a tier or an error
prefix: read the tier from `deps.yaml` when the package is declared there, and ask for the prefix.

| Input | Flag | Rule |
|---|---|---|
| name | `--name` | PascalCase, `^[A-Z][A-Za-z0-9]{2,30}$`, not Common/Shared/Utils/Helpers, no `src/<Name>/` yet |
| tier | `--tier` | 1–3, and equal to the `deps.yaml` tier when the package is declared (tier 0 is reserved) |
| error prefix | `--error-prefix` | `^[A-Z0-9]{2,16}$`, not used by any `src/**/*Errors.cs`; codes are `AW-<prefix>-NNN` |
| note | `--note` | one line; required only when the package isn't in `deps.yaml` yet |
| shape | `--shape` | `small` (Core + App) unless the package is expected over ~1,500 lines, then `full` |
| host | `--host` | `cli` (NativeAOT console app), `http` (minimal API; needs `full`) or `none` (library; needs `full`) |
| binary | `--binary` | the executable name, default `<name lowercase>` (e.g. `aw-readiness`) |
| depends | `--depends` | comma list of lower-tier, non-tier-0 packages whose `Core` the features reference |
| first slice | `--first-slice`, `--behavior` | the first use case (PascalCase, verb first) and its behavior (snake_case) |
| first slice surface | `--command` (cli) or `--route` (http), `--ids`, `--ports` | as `/new-slice` takes them (`docs/csharp/scaffolds/new-slice.md`) |

## 2. Run the generator

```bash
node scripts/scaffold/new-package.mjs --name <Name> --tier <n> --error-prefix <PREFIX> --shape <small|full> \
  --host <cli|http|none> --first-slice <Slice> --behavior <behavior> [--note "<one line>"] [--binary <exe>] \
  [--depends A,B] [--command <cmd> | --route "<METHOD> /path/{fooId}"] [--ids FooId:string] [--ports IFooStore]
```

It takes a slot of the heavy-job lock itself (or runs inside one, under `AW_HEAVY_LOCK_HELD=1`), writes the files, then runs the
spec's commands in order: restore of the new projects and the architecture tests, the locked whole-solution restore,
the public API lines from each library project's build (`RS0016`), `dotnet build -warnaserror` and the unit lane (exactly one failure: the
slice's red test) of the package's test project and the architecture tests, `dotnet format --verify-no-changes` limited to the files it wrote, and `node scripts/check-deps.mjs`. Any other failure reverts
every file it wrote and prints the rule id, the measured value and the fix.

Refusals (exit 1, nothing written): `NP-INPUT` (a missing or malformed input), `NP-NAME`, `NP-EXISTS`, `NP-TIER`,
`NP-DEPS-MISMATCH`, `NP-PREFIX`, `NP-DEPENDS`, `NP-FANOUT`, `NP-SMALL-HTTP`, `NP-SMALL-NONE`, `NP-PINS`, and the first slice's
`NS-*` ids. On `NP-PINS`, make the pin commit it prints first (pins in `Directory.Packages.props` plus the lock
files an unlocked restore changes), then run the generator again. On any other refusal, fix the input (ask the user
when it's theirs to decide) and re-run.

## 3. Commit and hand over

Check `git status` shows only the files the generator listed, then commit them as the message it prints
(`test: <name> package scaffold with a red <FirstSlice> test`). Report the files, the deps.yaml edges it made current,
and the next step it printed: make the slice test pass (`/new-slice` describes the order).

## Next steps

- `/new-slice` — add the next use case to the package, red first
- `/specToProvenPR` — when the package is a port packet, take it to a proven, review-clean PR
