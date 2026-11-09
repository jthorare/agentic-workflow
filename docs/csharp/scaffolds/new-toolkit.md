# `/new-toolkit` — scaffold spec

Status: **built** (PR 15 of the agent-first plan, #2258). This file is the contract; the generator is
`scripts/scaffold/new-toolkit.mjs` (templates in `scripts/scaffold/lib/toolkit-templates.mjs`), driven by the `/new-toolkit`
skill (`skills/new-toolkit/SKILL.md`). Its tests: `scripts/tests/scaffold.test.sh` (inputs, refusals, files, revert; a stub
`dotnet`, never a real one) and `scripts/scaffold/e2e.sh` (scaffolds a toolkit, restores, builds and tests it, NativeAOT-publishes it).

## Purpose

A toolkit is a small C# MCP stdio server that gives Sindri's agents typed tools (and declares the database, disk and
connections it needs). It lives **outside the repo** (default `$AW_SINDRI_HOME/toolkits/<kebab-name>/`), so none of the
repo's build rails reach it. The generator makes a toolkit project that carries its own rails, references the
`Aw.Sindri.Toolkit` SDK from a local folder feed, builds with warnings as errors, and leaves exactly one red test.

## Inputs

| Input | Flag | Rule |
|---|---|---|
| name | `--name` | PascalCase, `^[A-Z][A-Za-z0-9]{2,30}$`, not Common/Shared/Utils/Helpers (`NT-NAME`; missing: `NT-INPUT`). The toolkit's manifest name is the kebab form (`ContractDiff` -> `contract-diff`) |
| tool | `--tool` | the first tool, snake_case `^[a-z][a-z0-9_]{2,40}$` (`NT-TOOL`; missing: `NT-INPUT`) |
| dir | `--dir` | default `$AW_SINDRI_HOME/toolkits/<kebab-name>`; must not exist or be empty (`NT-EXISTS`); nothing is ever overwritten |
| needs | `--needs` | comma list: `database:<name>`, `disk:<size>` or `disk:<name>:<size>` (name defaults to `data`), `connection:<service>:<name>`. Names `[a-z0-9-]`, sizes `1g`, `500m`, `2gib`; each database or disk name once (`NT-NEEDS`) |
| feed | `--feed` | the SDK folder feed: `$AW_SINDRI_SDK_FEED`, else `$AW_SINDRI_HOME/sdk/packages`. Must hold an `Aw.Sindri.Toolkit.*.nupkg` (`NT-SDK`: run `scripts/install-cs.sh`) |
| root | `--root` | the agentic-workflow checkout whose `global.json` and `Directory.Packages.props` are copied (default: the checkout the script lives in). A missing pin is `NT-PINS` |
| files only | `--files-only` | write the files, skip the dotnet steps and the feed check (the unit tests; never on a real toolkit) |

Refusals print `<rule>: <message>` and `fix: <what to do>`, write nothing and exit 1; a usage error exits 2.

## Generated files

```
<dir>/
  global.json                 the repo's SDK pin (rollForward disable)
  nuget.config                sources: the local folder feed (only Aw.Sindri.Toolkit) and nuget.org
  Directory.Build.props       net10.0, nullable, warnings as errors, lock files, artifacts/ output
  Directory.Packages.props    central pins copied from the repo: analyzers, xunit.v3; Aw.Sindri.Toolkit 0.1.0-local.*
  .editorconfig  .gitignore
  <Name>.Toolkit.csproj       Exe, IsAotCompatible, PublishAot under a RID, PackageReference Aw.Sindri.Toolkit
  Program.cs                  runs <Name>Tools.Build() as the MCP stdio host
  <Name>Tools.cs              request and response records, Build() with the needs and the tool, the tool stub (throws)
  <Name>JsonContext.cs        the source-generated JSON context for every request and response
  <Name>.Toolkit.Tests/       one test project, one red test: <Tool>Tests.<Tool>_answers_the_subject_it_was_given
  packages.lock.json (x2)     written by the restore
```

Namespaces are `Toolkits.<Name>` and `Toolkits.<Name>.Tests` (a namespace ending in `.Toolkit` would hide the SDK's `Toolkit` type).

## Checks (heavy-job lock, `cwd` = the toolkit folder)

1. `dotnet restore` of both projects (`-p:RestoreLockedMode=false`, so the first restore writes the lock files even under CI).
2. `dotnet build <tests> -warnaserror`.
3. `dotnet test --project <tests> --filter-trait Lane=unit`: exactly one failing test, the sample, and it must fail with the stub's `NotSupportedException`.
4. `dotnet format --verify-no-changes` limited to the files it wrote.

A failed step prints the rule id (`NT-RESTORE`, `SCAFFOLD-BUILD`, `SCAFFOLD-RED`, `SCAFFOLD-FORMAT`), what was measured and the fix, then
**reverts every file and folder the run made**. SIGINT, SIGTERM and SIGHUP revert too. The generator never runs outside the heavy-job lock
and never touches the repo.

## The SDK feed

`scripts/install-cs.sh` packs `src/Toolkit/Aw.Sindri.Toolkit` into the feed (`0.1.0-local.<UTC timestamp>`) when the SDK's sources changed
and keeps the two newest packages. A toolkit references `0.1.0-local.*`, so it picks up the newest; its `packages.lock.json` records the
resolved version. The owner can overturn the folder feed for a repo project reference.

## Next steps the generator prints

Make `<Tool>Tests` pass (replace the stub in `<Name>Tools.cs`), then add tools with `.Tool<Req,Res>(...)`. `dotnet run -- --manifest` prints
the `toolkit.json` Sindri reads (PR 16).
