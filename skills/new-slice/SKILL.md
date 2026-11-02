---
name: new-slice
description: Add one use case (a slice) to an existing C# package, red first — the failing test that names the behavior, request and response records, a stub service, validator, registration, CLI command or endpoint, typed ids and ports with their fakes.
argument-hint: "<Package> <Slice> <behavior> [surface] [command|route] [ids] [ports] [--slice-only]"
allowed-tools: Bash(node *), Bash(git *), Bash(dotnet *), Read, Grep, Glob, AskUserQuestion
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

# /new-slice

Adds one use case to a package that `/new-package` created, exactly as `docs/csharp/scaffolds/new-slice.md`
specifies (that file is the contract; this skill drives its generator, `scripts/scaffold/new-slice.mjs`). The recipe
it encodes: **test (with compiling stubs) → service → endpoint or CLI command → registration.** It leaves exactly one
new red test; everything else stays green.

## 1. Collect the inputs

Take what the arguments give, then **Ask the user** for every input still missing.

| Input | Flag | Rule |
|---|---|---|
| package | `--package` | an existing `src/<Package>/` declared in `deps.yaml` |
| slice | `--slice` | PascalCase, verb first (`CheckRepo`, `StartItem`); no existing `Features/<Slice>/` |
| behavior | `--behavior` | snake_case sentence for the first test; the test is `<Slice>_<behavior>` |
| surface | `--surface` | `cli`, `http` or `none`, matching the package's host |
| command | `--command` | cli: kebab-case command name (default: the slice in kebab case) |
| route | `--route` | http: `"<METHOD> /path"`; each `{name}` names a typed id (`{itemId}` for `ItemId`; `{id}` when there is one id) |
| ids | `--ids` | new typed ids the request uses, `Name:string\|Guid\|int`, names ending in `Id`; existing ids are reused |
| ports | `--ports` | `Core` port interfaces the service needs; a missing one is created with its fake and contract base. `ports` + `TimeProvider` ≤ 5 |
| slice-only | `--slice-only` | set when other branches scaffold slices into the same package in parallel (Sindri API spec C7, §12) |

## 2. Run the generator

```bash
node scripts/scaffold/new-slice.mjs --package <Pkg> --slice <Slice> --behavior <behavior> --surface <cli|http|none> \
  [--command <cmd> | --route "<METHOD> /path/{fooId}"] [--ids FooId:string] [--ports IFooStore] [--slice-only]
```

Pass `--slice-only` when other branches scaffold slices into the same package in parallel. It writes the slice's
own files and only its lines in `A.cs`, `IdsJsonContext` and `PublicAPI.Unshipped.txt`, then prints the registration,
table and JSON context lines to add by hand in the branch, and the endpoint or command file to write by hand. A shared
edit or new shared file it would need is refused (`NS-SLICE-ONLY`), including a missing `A.cs`, or a missing `IdsJsonContext` when the slice adds an id:
the phase's contract unit adds those.

It takes a slot of the heavy-job lock itself, writes the files, records the public API lines (every `RS0016` symbol the
build reports for `Core` and `Features`, Vogen's generated id members included), then runs `dotnet build -warnaserror` and the unit lane (exactly one
failure: the new test) on the package's test project and the architecture tests, and `dotnet format --verify-no-changes` limited to the files it wrote. Any other failure reverts every file it wrote.

Refusals (exit 1, nothing written): `NS-PACKAGE`, `NS-NAME`, `NS-EXISTS`, `NS-BEHAVIOR`, `NS-SURFACE`, `NS-COMMAND`,
`NS-ROUTE`, `NS-IDS`, `NS-PORTS`, `NS-DEPS`, `NS-SLICE-ONLY`. Shared logic is never copied from another slice: if the new slice needs
another slice's code, move it into `Core` first (second use → generalize), then re-run.

## 3. Commit and hand over

Commit the listed files as `test: red <Slice> test (<behavior>)` (with `--slice-only`, also pass on the printed
shared lines), then report the next steps the generator printed:
write the assertion → add the ports to the constructor → implement the service (pure decisions on the domain type in
`Core`) → add the integration test if the slice touches SQL → run the lane.

## Next steps

- `/specToProvenPR` — take the slice to a proven, review-clean PR
- `/new-slice` — the next use case
