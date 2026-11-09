# Handoff inbox contract

`inbox.openapi.yaml` is the whole contract: three HTTP calls an outside system implements so Sindri can pull work from it. The guide for implementers is `docs/sindri/handoff.md`.

| Call | Meaning |
|---|---|
| `GET /offers?owner=&pageToken=` | The unclaimed offers made to one owner, oldest first, in pages |
| `POST /offers/{id}/claim` | Claim one; exactly one caller wins (`409` for the loser, `404` unknown, `410` expired) |
| `POST /offers/{id}/result` | Submit the result of a claimed offer; idempotent on `claimId` |

Offer fields: `id`, `title`, `repo`, `branch` (nullable), `prompt` (nullable), `links[]` (`label`, `url`), `labels[]`. Auth is `Authorization: Bearer <key>`, the key of the Sindri connection.

Conformance examples live in `tests/fixtures/handoff/fake-inbox/` (listed in its `manifest.json` with the verdict each must get). `node scripts/check-handoff-schema.mjs` validates the file, its examples and those fixtures; Sindri's own tests run the same files through `HttpInboxClient`.
