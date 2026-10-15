---
name: heavy-lock-two-slots
description: "Heavy-job locks: 1 heavy job per session plus 2 box-wide slots for expensive jobs (whole-solution builds, AOT, browsers) since 2026-10-14"
metadata:
  node_type: memory
  type: project
  originSessionId: b80bcd18-06da-4f1c-9162-d6d5617f67db
  modified: 2026-10-12T16:13:29.036Z
---

Since 2026-10-11 (owner decision) the box-wide heavy-job lock allows **two** concurrent holders by default, not one.
Slot 1 is `heavy-job.lock`, slot k is `heavy-job.lock.k` (k = 2..`AW_HEAVY_SLOTS`, default 2); each is a mkdir mutex.
Holders that only know `heavy-job.lock` still work (they share slot 1), so the total never exceeds the slot count.
The `~/.agentic-workflow/bin-r1b/heavy` wrapper takes any free slot and exports `AW_HEAVY_SLOT`; the previous
one-slot version is kept as `heavy.one-slot.bak`. The QA-stack lock stays one slot (one local app stack on :3000).

**Why:** heavy jobs serialized across parallel sessions (R2 packets) and wasted wall time; the box (10 cores, 16 GB)
fits two, with memory the limit (~5 GB free under load), so expect some slowdown per job but more throughput.

**How to apply:** keep running builds/tests through `heavy`; don't assume a heavy job runs alone (lane budgets and
timing-sensitive tests can see a loaded box). Repo follow-through: config/lib/locks.sh `acquire_any_lock` + sindri
slot-aware lock PR (branch feat/heavy-lock-two-slots), and Evidence's C# `DirectoryLocks` on feat/r2-ev-evidence.
Set `AW_HEAVY_SLOTS=1` to go back to one at a time.

**Update 2026-10-12:** the owner raised it to **3 slots** for the Sindri build week; `~/.zshrc` exports
`AW_HEAVY_SLOTS=3`, but sessions started earlier don't see it, so pass `AW_HEAVY_SLOTS=3` explicitly on build and
gate commands. Disk (worktree build output) is the other limit: remove each worktree once its PR merges.

**Update 2026-10-14 (box crashed at 3 box-wide slots with 4 lanes and many subagents):** two tiers now.
(1) Per session: 1 heavy job (`AW_SESSION_HEAVY_SLOTS=1`, keyed on the nearest `claude` ancestor pid, lock
`locks/heavy-session/<pid>.lock`), so a lane and all of its subagents queue on one slot.
(2) Box-wide 2 slots (`AW_HEAVY_SLOTS=2`) only for expensive jobs: whole-solution build or test, publish, aot-smoke,
browsers (Playwright, ui-evidence, Aw.Evidence), scaffold e2e. They're auto-detected by `heavy`; `--expensive` and
`--light` override. Dead owners' slots are reclaimed. Backup: `heavy.three-slot.bak`. Lanes: at most 2 builder subagents.
**Tightened again the same evening** (swap 6.4/7.2 GB, load 128 with 4 sessions each building): EVERY build is
expensive (dotnet build/publish/format/restore/run, `dotnet test` without `--no-build`, npm ci/test/vitest,
browsers), so at most 2 builds box-wide; only `dotnet test --no-build` and scripts are light. Each heavy .NET job
gets DOTNET_PROCESSOR_COUNT=4 (AW_HEAVY_CPUS) and MSBuild node reuse off. Lane pattern: heavy build, then heavy
test --no-build.
