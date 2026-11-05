models: scripts/heavy.sh config/lib/locks.sh src/Sindri/Aw.Sindri.Infrastructure/Locks/DirectoryHeavySlots.cs src/Sindri/Aw.Sindri.Infrastructure/Locks/HeavySlotLease.cs src/Sindri/Aw.Sindri.Infrastructure/Locks/HolderRecords.cs src/Sindri/Aw.Sindri.Infrastructure/Locks/HeavyLockSettings.cs
# HeavySlots

- **Models:** the box-wide heavy-job slots: `<locks>/heavy-job.lock` (slot 1) and `heavy-job.lock.k` (slot k), mkdir mutexes shared by three takers.
  - The `heavy` wrapper, `scripts/heavy.sh`. `acquire box-wide` reads the cap file once per job and the meta reserve on every pass. It does mkdir, then the owner write. To reclaim a slot it moves `owner` aside to `owner.reclaim.<its pid>`, checks it is the dead line it read, then removes it and the folder (the same rename-aside as locks.sh; before #2058 it unlinked and rmdired with no mutex).
  - `config/lib/locks.sh` `acquire_any_lock`: uses `AW_HEAVY_SLOTS` (default 2) and never reads the cap file or the reserve. Its record is `<dir>/pid`. To reclaim, it moves the record aside first (`_lock_reclaim_dead`).
  - `DirectoryHeavySlots.cs`: reads the cap once at serve start (the `HeavyLockSettings` singleton) and never reads the reserve. Its record is `<dir>/owner`, and it reclaims by renaming the record aside (`Claim`, to `owner.reclaim.<its pid>.<random>`) and then a non-recursive delete. `HeavySlotLease` leaves alone a slot whose owner names another process.

  All three free a slot left holding only a moved-aside record (`*.reclaim.<pid>…`) whose mover is dead: a reclaimer SIGKILLed between its move and its delete (#2065).

  Each syscall is one step; the move aside and the re-read are two. Simpler than the code: a release checks the job (`who`), where Sindri's `IsOurs` / `HeavySlotLease` compare only the pid, which every job in one serve shares (matters with 2+ Sindri jobs). A process can be SIGKILLed at any step: its folder and record stay, and its pid is never reused. Only SIGKILL is modelled: a reclaimer whose read or delete fails in process (Sindri's `Claim` puts the record back), a reused pid, and the 6 h bound every taker puts on a leftover whose reclaimer looks alive are covered by tests, not TLC. Not modelled: the per-session `heavy-session/<pid>.lock` pool, the memory guard (it only skips slots), timeouts, the 6 h no-record grace and the TS `holder.json`.
- **Checks:** `OneHolderPerSlot` (no two live processes hold one slot folder) and `SomeoneRuns` (the box never locks up among the live jobs). `TakeWithinCap` (nobody takes a slot above the cap file as it reads at that moment) and `ReserveHonored` (a non-meta job never takes a slot meta reserved) are in `HeavySlots.cfg` too, but there they can't fail: its cap never changes and no job is meta. They only bite in `gaps/StaleCap.cfg` and `gaps/IgnoredReserve.cfg`. `LeftoverCleared` (a record a dead reclaimer moved aside is always removed again) is in `LeftoverReclaim.cfg`. Also, checked only as witnesses on the gap cfgs: `NoStarvation` (every waiter gets a slot) and `OrphanReclaimed` (a slot whose holder died is freed).
- **Bounds:** `HeavySlots.cfg`: one wrapper job, one locks.sh holder, one Sindri job, 2 slots, every cap 2, no reserve, no SIGKILL, weak fairness; about 1k states. `HeavySlotsReclaim.cfg`: two locks.sh holders and one Sindri job on 1 slot, 1 SIGKILL anywhere. Their rename-aside reclaims never let two processes hold one slot (about 1k states). `WrapperReclaimRace.cfg` (three wrapper jobs, 1 slot, 1 SIGKILL) and `MixedReclaimRace.cfg` (two wrapper jobs and a locks.sh holder) check the same for the wrapper (#2058). `LeftoverReclaim.cfg`: one of each taker on 1 slot, 2 SIGKILLs (#2065).

## Known gaps (`gaps/*.cfg`, run in CI as witnesses: `tlc-check.sh --witnesses` expects each to fail on the invariant its line 1 `\* witness: <Name>` names)

| Config | Fails | What TLC shows | Issue |
|---|---|---|---|
| `gaps/DeadHolderKept.cfg` | `OrphanReclaimed` | A locks.sh holder SIGKILLed: its record is `pid`, and the wrapper reads only `owner`, so wrapper jobs wait on a dead slot forever. The same happens to any holder killed between its mkdir and its record write (an empty folder nobody reclaims) | #2059 |
| `gaps/PollStarvation.cfg` | `NoStarvation` (strong fairness) | No queue: a job that polls once a second can find the slot busy on every look, so no fairness forces its mkdir | #2060 |
| `gaps/FullReserve.cfg` | `NoStarvation` | `heavy-meta-reserve` equal to the slot count, held there, starves every lane job | #2060 |
| `gaps/StaleCap.cfg` | `TakeWithinCap` | Lowering `heavy-slots-cap` from 2 to 1: a wrapper job already waiting, Sindri and locks.sh all still take slot 2 | #2061 |
| `gaps/IgnoredReserve.cfg` | `ReserveHonored` | Only the wrapper reads `heavy-meta-reserve`: locks.sh (bugfix-state's `locked`) and Sindri jobs take reserved slots | #2061 |

Run: `scripts/tlc-check.sh --witnesses specs/tla/HeavySlots` (the gaps print WITNESS-CAUGHT). For one gap, run `bash skills/formal-verify/scripts/tlc.sh check "$PWD/specs/tla/HeavySlots/HeavySlots.tla" --config "$PWD/specs/tla/HeavySlots/gaps/<Name>.cfg"`. Use absolute paths: `tlc.sh` changes into the spec's folder.

When `scripts/heavy.sh`, locks.sh or `DirectoryHeavySlots.cs` change how they take or reclaim a slot, update the spec.
