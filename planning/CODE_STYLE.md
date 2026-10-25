# Code Style Guide

The toolkit is C# (`src/`, `tests/`). Its conventions of record are `.agents/rules/csharp.md` and
`.agents/rules/csharp-testing.md`, enforced by analyzers, `dotnet format` and the architecture tests (spec
`docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4a–§4b). This guide doesn't repeat them.

The shell scripts (`setup.sh`, `scripts/`, `config/hooks/`, `providers/`) are bash: `set -euo pipefail` unless a hook
must fail open, quoted expansions, no GNU-only flags (macOS ships bash 3.2 and BSD tools), and a `*.test.sh` beside every
non-trivial script (`.agents/rules/testing.md`).
