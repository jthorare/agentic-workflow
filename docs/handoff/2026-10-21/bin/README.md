# Box scripts (not in the repo tree)

Copies of the two scripts the team runs from `~/.agentic-workflow/bin-r1b/` on the old box. Nothing in the repo installs them.

| Script | What it does |
|--------|--------------|
| `heavy` | Runs a build/test/format under the heavy-job locks: one heavy job per Claude session (`AW_SESSION_HEAVY_SLOTS`, default 1), box-wide slots for expensive jobs (cap file), a memory guard for slots above 1 (`AW_HEAVY_MIN_FREE_MB`, default 10240, +4096 per extra slot), the meta gate reservation, and `MSBUILDDISABLENODEREUSE=1`. Every lane's dotnet build/test/format goes through it. |
| `meta-gate-reserve` | `meta-gate-reserve <arg>` / `--show` / `--clear`: writes how many box-wide slots meta gates reserve. `aw-meta status` prints the argument to pass. |

Install on a new box:

```bash
mkdir -p ~/.agentic-workflow/bin-r1b
cp docs/handoff/2026-10-21/bin/{heavy,meta-gate-reserve} ~/.agentic-workflow/bin-r1b/
chmod +x ~/.agentic-workflow/bin-r1b/{heavy,meta-gate-reserve}
echo 4 > ~/.agentic-workflow/heavy-slots-cap          # box-wide slots; the old box (32 GB) used 4
echo 'export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.agentic-workflow/bin-r1b:$PATH"' >> ~/.bashrc
```

Size the cap and `AW_HEAVY_MIN_FREE_MB` to the new box's memory.
