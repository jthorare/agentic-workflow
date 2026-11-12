## Root Cause
`src/Sindri/Aw.Sindri.Host/Commands/Probe/HarnessDoctor.cs`: `Checks` returns Warn for a pending probe, `Reprobed` returns Warn for a version that differs even with every marker set, and `Installed` returns Warn for a differing version. `Probe()` builds the golden-file fix line (`tests/Aw.Sindri.Tests/Golden/Probe/<file>`) for all of them. A user's box has no such folder, and refreshing the pin is a toolkit PR. Result: `sindri doctor` shows 4 warns on a fresh home (#3184).

## Check
`Aw.Sindri.Tests.Commands.Probe.HarnessProbeTests` in `tests/Aw.Sindri.Tests/Aw.Sindri.Tests.csproj` (Lane=unit).

## Fix
1. `HarnessDoctor.Checks` takes `bool dev`; `sindri doctor --dev` sets it (Usage.cs, docs/csharp/sindri.md 'The harness probe').
2. Without dev: Reprobed missing marker stays Warn with a user fix (update the toolkit, ./setup.sh, or report it); Reprobed differing version with all markers set -> Ok `claude 2.1.322 (probe pinned at 2.1.299; the markers are all set)`; Pending -> Ok `pending owner probe: provisional markers`; Installed differing version -> Ok `codex 0.9 installed (probe pinned at 0.8)`. No fix line in the Ok cases.
3. With dev: old behaviour, Probe() fix included.
4. Update src/Sindri/aot-smoke/expected.txt only if its doctor lines change.
