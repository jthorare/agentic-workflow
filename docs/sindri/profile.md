# Sindri profile reference

The profile reference is [`docs/csharp/sindri-profile.md`](../csharp/sindri-profile.md): every key of `profile.yaml` and
`repos/<repo>.yaml`, its type, default and meaning. `scripts/tests/check-profile-doc.test.sh` fails when a key in the
schema is missing from it. The context engine's section is described in [context.md](context.md).

A changed profile takes effect only after `sindri profile approve <hash>`, typed at your own terminal.
