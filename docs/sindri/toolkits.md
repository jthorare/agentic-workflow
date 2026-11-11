# Toolkits

A toolkit is a small .NET program that Sindri builds, starts in a sandbox and calls through MCP. Agent sessions reach its tools as `<toolkit>__<tool>`.

## Build

`toolkit_build` (a job) takes a heavy-job slot, then runs these steps in the toolkit's folder:

1. `dotnet build -warnaserror`.
2. `dotnet test`.
3. `<built binary> --manifest`. The binary prints its manifest on stdout and Sindri parses it. Nothing is written into the folder, so the source hash stays the same.

`dotnet build`, `dotnet test` and `--manifest` run unsandboxed, as Sindri's user. An agent session can start them through `toolkit_build`.

A good build stores the source hash and the manifest. A start is refused when the source on disk no longer matches that hash (SND-TK-001).

## Run

`toolkit_start` launches the binary in the process sandbox. `toolkit_stop` ends it and removes its run area.

When the process crashes, Sindri restarts it: 1 s after the first crash, 2 s after the second. The third crash inside ten minutes marks the toolkit `failed` and stops the restarts; there is no fourth wait. `toolkit_start` launches a failed toolkit again.

After a serve restart every toolkit that was `running` shows as `built` until it is started again.
