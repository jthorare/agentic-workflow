models: scripts/meta-start-lock.sh:40-80
# StartLockTakeover

- **Models:** the waiter protocol of `take_over_dead` in `scripts/meta-start-lock.sh` (#2613): the takeover guard (a directory, `$start_lock.takeover`) as a set of pid files, and takers that mkdir it, announce a pid file, scan every entry and enter the body.
  - Every taker, the mkdir winner included, announces its pid file first, then reads every entry: any other live pid file means it removes its own file and leaves; a dead one it `rm`s (only the taker whose `rm` succeeds enters the body); no other file at all means the winner enters and a waiter leaves (it cannot tell that from a winner that has not announced yet). The mkdir winner must scan too: a waiter that read the guard in the winner's mkdir-to-pid-file window never sees the winner, and a later death of another file let it in beside the winner (found by TLC, 4 takers, 1 death).
  - A taker may die at any step (its file stays), at most `MaxDeaths` times. An empty guard is cleared by `Age` (the real `_start_guard_rmdir_if_old`, a minute old) unless a taker is between mkdir and its pid file.
- **Invariant:** `MutualExclusion`, at most one live taker in the body. `TypeOK` is the shape.
- **Witness:** `gaps/OldLoop.cfg` sets `Old = TRUE`: the loop before #2613 decided from the first entry in glob order (the lowest pid here), so a dead file in front of a live holder's file let a third waiter in.
- **Not modelled:** liveness (a lone waiter eventually takes over). The outer retry loop in `acquire_start_lock` and its deadline are outside the model, so a waiter that backs off is simply done here. The random backoff sleep is not modelled (it only breaks lockstep, never safety).
- **Run:** `scripts/tlc-check.sh --witnesses specs/tla/StartLockTakeover`
