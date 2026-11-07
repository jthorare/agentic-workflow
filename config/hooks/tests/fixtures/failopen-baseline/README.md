# Fail-open baseline

Verbatim copies of the fail-open hooks, the Codex/Cursor adapters and `lib/locks.sh` as they were
before HOOK-FAILOPEN logging was added (commit `2004bdc`). `config/hooks/tests/failopen.test.sh`
runs every case against these and against the current hooks, and requires the same stdout,
stderr (with paths and bash's `line N:` normalized) and exit code. That is how it proves the
logging is logging only.

If a hook's **decision** changes on purpose later, copy the new hook here in the same PR, so the
baseline keeps tracking "the hook without its fail-open logging".

`meta-stop-gate.sh` and `meta-outbox.sh` were written with fail-open logging from the start; their baseline copies are
the same hooks with `failopen()` reduced to a no-op.

`sindri-tasks.sh` has no fail-open logging at all; its baseline copy is the hook itself.
