# Handoff: gh-2089-optional-tmpfs-key

status: diagnosed
report: /home/thor/repos/aw-wt/api-2089/investigations/gh-2089-optional-tmpfs-key/handoff.md
boundary: src/Sindri/Aw.Sindri.Core/Sandbox/Plan
repro: no failing command (latent defect). Read-only check: grep -n "Optional" src/Sindri/Aw.Sindri.Core/Sandbox/Plan/PlanMounts.cs src/Sindri/Aw.Sindri.Core/Sandbox/Plan/SeatbeltPlan.cs; a test adding a second optional Tmpfs default shows it planned as a writable real path on Seatbelt

## Root Cause

Two places in the plan builder identify the .NET corefx scratch entry by a proxy (kind Tmpfs, or kind Tmpfs plus Optional) instead of by what it is. `PlanMounts.AddHomeAllow` (PlanMounts.cs:50) gives every `_homeDefaults` row of kind Tmpfs `Size = CorefxTmpfsBytes` (4 MiB), and `SeatbeltPlan.Rewriter.Add` (SeatbeltPlan.cs:89) rewrites every Tmpfs with `Optional == true` to ReadWrite on its real host path. `PlanMount` (PlanMount.cs:10) carries no field saying "private runtime scratch", so the proxy is the only handle. Today it is exact because `.dotnet/corefx` (PlanMounts.cs:14) is the only entry that is both Tmpfs and Optional. A second optional Tmpfs default (one meant to hide a host dir) would get the 4 MiB corefx size on Bubblewrap and, on Seatbelt, become a writable window onto the real host path instead of being denied (the non-optional branch at SeatbeltPlan.cs:93 is the one that would hide it). Latent: no input today reaches it.

## Hypotheses

| # | Hypothesis | Cause-site files | Likelihood | Result |
|---|-----------|------------------|------------|--------|
| 1 | Seatbelt rewrite (SeatbeltPlan.cs:89) and the default sizing (PlanMounts.cs:50) key on "any (optional) Tmpfs", not the corefx entry | `src/Sindri/Aw.Sindri.Core/Sandbox/Plan/SeatbeltPlan.cs`, `src/Sindri/Aw.Sindri.Core/Sandbox/Plan/PlanMounts.cs` | High | confirmed |
| 2 | A profile input (homeAllow, cachePaths) can already create a second optional Tmpfs today | `src/Sindri/Aw.Sindri.Core/Sandbox/Plan/PlanMounts.cs` | Low | ruled-out |
| 3 | The other two Optional+Tmpfs consumers (BubblewrapArgv.cs:64, RunAreaPreparer.cs:79) are also wrongly corefx-specific and need the same fix | `src/Sindri/Aw.Sindri.Infrastructure/Sandbox/BubblewrapArgv.cs`, `src/Sindri/Aw.Sindri.Infrastructure/Sandbox/RunAreaPreparer.cs` | Medium | ruled-out |

Row notes: Row 1 confirmed by reading: SeatbeltPlan.cs:89 tests only `Kind == Tmpfs && Optional`, its comment says "the only optional one is the .NET runtime's lock directory" (an assumption, not a check); PlanMounts.cs:50 tests only `d.Kind == Tmpfs`; the only `_homeDefaults` Tmpfs row is `.dotnet/corefx` (PlanMounts.cs:14). No test pins that (SandboxPlanSeatbeltTests.cs:67 only asserts the Seatbelt plan has no Tmpfs left, which would pass vacuously for a wrongly rewritten second entry). Row 2: PlanMounts.cs:51-52 maps homeAllow to ReadWrite or ReadOnly only; :53 maps cachePaths to ReadWrite; login mounts (:65) are ReadOnly/ReadWrite; Inventory (:162) is ReadOnly/ReadWrite/PerLaunchDir. The remaining Tmpfs entries (/run :32, home :34, Sindri home :142-143) are non-Optional. So only a code edit to `_homeDefaults` (or a new Add* in PlanMounts.cs) can add one. Row 3: those consumers implement generic mount-point mechanics. BubblewrapArgv.cs:64 skips an optional tmpfs whose host mount point is missing (bubblewrap cannot create a mount point in a read-only bind); RunAreaPreparer.cs:79 creates a missing mount point (0700) under an existing parent. Both are correct for any optional tmpfs, so they stay as they are. The Seatbelt branch and the size are the only corefx-specific meanings.

## Ruled Out
- Profile-driven second optional tmpfs: homeAllow, cachePaths and login mounts never produce kind Tmpfs (PlanMounts.cs:51-53, 65).
- Bubblewrap/RunAreaPreparer need a change: generic mount-point handling, correct for any optional tmpfs (BubblewrapArgv.cs:64, RunAreaPreparer.cs:77-83).

## Side facts
- Real impact is Mac only (SeatbeltPlan.cs:89). On Seatbelt the corefx entry becomes a ReadWrite allowance on the host's real `~/.dotnet/corefx` (golden lines `seatbelt-attended.sb:29`, `seatbelt-writescope.sb:29`, `seatbelt-mac.sb:29`), shared with the host's .NET mutexes. The issue calls that FYI, not a bug; #2088 notes it is untested on a Mac.
- The rewritten ReadWrite keeps Optional=true and is added directly, so it gets no home link (AddHomeLink is skipped by the early branch).
- `PlanMount` is public: PublicAPI.Unshipped.txt:18785-18794 list the ctor, Deconstruct (six out params), Clone, Equals and each property.
- `PlanHasher.cs:41` hashes `Kind`, `Path`, `Source` and `Size` per mount, not `Optional`; a new flag left out of the hash does not move plan hashes.

## Suggested Fix

Candidate shapes:

A. Flag on PlanMount (the issue's suggestion), e.g. `bool PrivateScratch = false` appended last.
   - Files: `PlanMount.cs` (record + param doc), `PlanMounts.cs` (put the flag on the corefx row: widen the `_homeDefaults` tuple and set it in :50; also give the tuple a size so line 50 stops using `Kind == Tmpfs`), `SeatbeltPlan.cs:89` (`m is { Kind: Tmpfs, PrivateScratch: true }`; every other Tmpfs falls through to the AddDeny branch at :93), `PublicAPI.Unshipped.txt` (new ctor/Deconstruct/property lines), plus any `PlanMount` JSON context if the plan file shape is pinned (value-free `sandbox-plan.json` is written from the plan, RunAreaPreparer.cs:73).
   - Tests/goldens: no hash change (PlanHasher ignores it). Goldens: only the plan JSON if a golden serializes mounts with the new field; the Seatbelt `.sb` goldens and `show-*.txt` stay the same. New tests in `tests/Aw.Sindri.Tests/Sandbox/Plan/SandboxPlanSeatbeltTests.cs`: a Tmpfs optional mount without the flag is denied, not allowed; `.dotnet/corefx` is the only mount with the flag. `RunAreaPreparerTests.cs:72` and `BubblewrapArgvTests.cs` construct Optional Tmpfs mounts directly and need no change.
B. New `PlanMountKind` (e.g. `RuntimeScratch`).
   - Files: `PlanMountKind.cs`, `SeatbeltPlan.cs`, `SeatbeltProfile.cs:92,109`, `SandboxPlanRenderer.cs:99,129-140`, `BubblewrapArgv.cs:11,64,81`, `RunAreaPreparer.cs:79`, `PlanMounts.cs`, PublicAPI. Wider than A; every Tmpfs special case in Bubblewrap and the renderer (which labels it "empty", golden `show-*.txt:12`) must learn the new kind. Not recommended.
C. Size column on the `_homeDefaults` table only. Fixes PlanMounts.cs:50 and not SeatbeltPlan.cs:89, so half the bug remains; use it with A or with D.
D. Test only (the issue's fallback): a unit test over `PlanMounts.Build` asserting the sole `Optional && Tmpfs` mount is `<home>/.dotnet/corefx`. Cheap, no API change, but the code still guards nothing; it only fails when someone adds a row.

Recommendation: A, with C folded in (the corefx row carries its own size, so `CorefxTmpfsBytes` is no longer applied by kind), plus the D test as the pin. Keep BubblewrapArgv.cs:64 and RunAreaPreparer.cs:79 unchanged. Module boundary for the fix: `src/Sindri/Aw.Sindri.Core/Sandbox/Plan` plus `PublicAPI.Unshipped.txt` in `Aw.Sindri.Core`.
