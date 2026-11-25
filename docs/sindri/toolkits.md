# Toolkits

A toolkit is a small .NET program that Sindri builds, starts in a sandbox and calls through MCP. Agent sessions reach its tools as `<toolkit>__<tool>`.

## Create and adopt

`toolkit_create` (a job; `sindri toolkit create <name> --tool <tool> [--needs <list>]`) runs the generator, `scripts/scaffold/new-toolkit.mjs`, into `$AW_SINDRI_HOME/toolkits/<name>` and records the toolkit as `off`, with the caller as its creator. The name is lowercase words joined by `-` (`github-triage`); a taken name is SND-TK-012 and a generator failure is SND-TK-013. The generator needs Node and the SDK package that `scripts/install-cs.sh` packs.

At serve start, every folder under `$AW_SINDRI_HOME/toolkits/` that holds a `*.Toolkit.csproj` and has no row gets one, `off`, created by `adopted`.

## Build

`toolkit_build` (a job) takes a heavy-job slot, then runs these steps in the toolkit's folder:

1. `dotnet build -warnaserror`.
2. `dotnet test`.
3. `<built binary> --manifest`. The binary prints its manifest on stdout and Sindri parses it. Nothing is written into the folder, so the source hash stays the same.

`dotnet build`, `dotnet test` and `--manifest` run unsandboxed, as Sindri's user. An agent session can start them through `toolkit_build`.

A good build stores the source hash and the manifest. A start is refused when the source on disk no longer matches that hash (SND-TK-001).

## Run

`toolkit_start` launches the binary in the process sandbox. `toolkit_stop` ends it and removes its run area.

`toolkit_pause` keeps the process running but takes the toolkit's tools out of `tools/list` and refuses calls with SND-TK-005. `toolkit_resume` undoes it. A paused toolkit stays paused through a crash restart; a stop or a serve restart ends the pause.

The same verbs exist on the command line: `sindri toolkit list|create|build|start|stop|pause|resume|calls <name>`, each with `--json`.

When the process crashes, Sindri restarts it: 1 s after the first crash, 2 s after the second. The third crash inside ten minutes marks the toolkit `failed` and stops the restarts; there is no fourth wait. `toolkit_start` launches a failed toolkit again.

After a serve restart every toolkit that was `running` shows as `built` until it is started again.
