# Sindri profile reference

The profile reference is [`docs/csharp/sindri-profile.md`](../csharp/sindri-profile.md): every key of `profile.yaml` and
`repos/<repo>.yaml`, its type, default and meaning. `scripts/tests/check-profile-doc.test.sh` fails when a key in the
schema is missing from it. The context engine's section is described in [context.md](context.md).

Edits apply on save: the running Sindri reads the live file, so a valid change takes effect at once. There is no approval step; `sindri profile approve` only prints `removed: edits apply on save`.
