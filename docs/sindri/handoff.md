# Handoff inbox

A handoff inbox is a small HTTP API that an outside system (a ticket tool, a job queue, a script on another box) runs so Sindri can pull work from it. Sindri asks for the offers made to its owner, claims one, works on it, and sends the result back. The outside system never reaches into the laptop: Sindri always makes the call.

The contract is one OpenAPI file, `schemas/handoff/inbox.openapi.yaml`. This guide says how to implement it, what Sindri does with each call, and what the errors mean. Nothing here names a particular system.

## The three calls

| Call | What it does |
|---|---|
| `GET /offers?owner=<owner>&pageToken=<token>` | Lists the unclaimed offers made to one owner, oldest first. `pageToken` is the `nextPageToken` of the previous page and is left out for the first page. |
| `POST /offers/{id}/claim` | Claims one offer for the owner in the body (`{"owner": "..."}`). Exactly one caller wins. |
| `POST /offers/{id}/result` | Submits the result of a claimed offer. |

Every call carries `Authorization: Bearer <key>`: the key of the Sindri connection that points at the inbox. A missing or wrong key is `401` (or `403` when the key may not act for that owner).

### List

`200` with a page:

```json
{
  "offers": [
    { "id": "o-1", "title": "Fix the login timeout", "repo": "acme/web", "branch": "fix/login-timeout",
      "prompt": "Users are logged out after 5 minutes. Make it 30.",
      "links": [{ "label": "Ticket", "url": "https://tickets.example.test/T-1" }], "labels": ["bug"] }
  ],
  "nextPageToken": null
}
```

`nextPageToken` is a string while more pages exist and `null` on the last page. The field is always there. A claimed or expired offer is not listed.

Offer fields:

| Field | Required | Meaning |
|---|---|---|
| `id` | yes | The offer's id at your system |
| `title` | yes | One line |
| `repo` | yes | The repo the work is for (`owner/name`, or a name the Sindri profile knows) |
| `branch` | no (`null`) | The branch to work on |
| `prompt` | no (`null`) | What to do, up to 100,000 characters |
| `links` | yes (may be empty) | `{label, url}` pairs; `url` is `http` or `https` |
| `labels` | yes (may be empty) | Strings an `inbox` trigger can filter on |

An unknown extra field is not part of the contract: the schema forbids it, so a checker rejects it. Sindri's client ignores one it meets.

### Claim

The body is `{"owner": "<owner>"}`.

| Status | Meaning |
|---|---|
| `200` | The caller won. The body is `{"claimId": "...", "offer": {...}}`. The `claimId` names this claim from now on. |
| `409` | Another caller claimed it first. Nothing starts on Sindri's side (`SND-CONN-202`, informational). |
| `404` | No such offer (`SND-CONN-203`). |
| `410` | The offer expired or was withdrawn (`SND-CONN-203`). |

Sindri never retries a claim. A claim that timed out may have won, and a second try would lose to the first and look like a lost race. Make claiming atomic on your side (one compare-and-set on the offer's state).

### Submit the result

The body:

```json
{ "claimId": "c-1", "succeeded": true, "markdown": "Made the timeout 30 minutes.",
  "links": [{ "label": "PR", "url": "https://github.example.test/acme/web/pull/7" }] }
```

`succeeded` is `false` when the run failed; `markdown` then holds the failure text. The call is idempotent on `claimId`: the same result sent again answers `200` and changes nothing. A `claimId` that is not the one the claim returned is `409`.

`200` answers `{"accepted": true}`. Sindri retries this call up to three times on a transport failure, a timeout, `429` or a `5xx`. If all three fail, the result is `SND-CONN-204`.

## What Sindri does

1. It lists the offers for its owner with the connection's key.
2. For each offer it claims it. A lost race (`409`) starts nothing.
3. A won claim becomes an item that carries the offer's fields, and starts any workflow whose `inbox` trigger matches. The claim id is the occurrence of those runs, so a claim that is delivered twice cannot start a second run.
4. When the run ends, its outputs are submitted with the claim id. A failed run submits `succeeded: false` and the failure text.

Everything an offer carries is text another system wrote. Sindri fences it wherever it reaches a prompt and never treats it as an instruction.

## Limits the client holds to

| What | Value |
|---|---|
| Time per attempt | 30 seconds |
| Tries for list and submit | 3 (claim: 1) |
| Answer size | 4,000,000 characters |
| Redirects | not followed (a redirect is `SND-CONN-205`) |
| URL | `https`; `http` only for a loopback host |
| Host | has to be on the client's allowlist (`SND-CONN-206`) |

## Errors

| Code | When |
|---|---|
| `SND-CONN-002` | The key was refused (`401`, `403`) |
| `SND-CONN-201` | The answer breaks the contract; the message names the field, never its text |
| `SND-CONN-202` | The claim was lost (`409`); informational |
| `SND-CONN-203` | The offer is not there (`404`, `410`) |
| `SND-CONN-204` | The result was not taken |
| `SND-CONN-205` | The inbox is out of reach, too slow, or kept answering `429` or `5xx` |
| `SND-CONN-206` | The host is not allowed, or the URL is plain http for a non-loopback host |

## Checking your implementation

- `tests/fixtures/handoff/fake-inbox/` holds conformance examples: valid and invalid offers, pages, claims and results, each with the verdict it must get (`manifest.json`). Run your own validator over them.
- `tests/Aw.Sindri.Testing/Handoff/FakeInbox.cs` is the reference behaviour in about 150 lines: paging by the last offer key, one winner per offer, one stored result per claim. `FakeInboxServer.cs` serves it over the three routes.
- `node scripts/check-handoff-schema.mjs` validates the OpenAPI file, its examples and those fixtures. `bash scripts/tests/check-handoff-schema.test.sh` runs it with the failures it must catch.
- `InboxContract` (in `tests/Aw.Sindri.Testing/Handoff/`) is the list of behaviours every client and fake has to show; `HttpInboxClient` and `FakeInbox` both pass it.
