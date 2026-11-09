# Sindri memory

Sindri memory stores are named places to keep short facts that a later run, a workflow or an agent can recall by meaning. Not to be mixed up
with other servers' `memory_search`: the tool descriptions here say "Sindri memory".

## Stores and facts

- A **store** has a unique name (lowercase letters, digits and `-`, 1 to 48 characters, starting with a letter or digit), an optional description and who made it.
- A **fact** is 1 to 2,000 characters of text, tags, an optional expiry and where it came from (workflow, run, item, session).
- Text, tags and the description are **scrubbed** (secrets redacted) before anything stores them or sends them to the embedding model.
- Facts live in `ledger.db` (`memory`, `memory_fact`, migration `0011_memory.sql`). They are not rebuilt from events, and the migration runner backs them up with the rest of the ledger.

## Writing: dedupe

A write embeds the text with the pinned local model (`nomic-embed-text` through Ollama, `context/code-index.lock`) and stores the vector with the model that made it.
When an unexpired fact in the same store has the **same tag set** and a vector at or above the dedupe threshold (cosine 0.9), that fact is **refreshed**
(`refreshedAt`, and the new expiry when given) and no row is added: the answer says `action: refreshed`. Otherwise `action: added`.
Tags are part of a fact's identity: the same text with other tags is a new fact. The read, the decision and the write happen in one store transaction,
so two near-same writes at once add one fact. A vector of another dimension (another model) never counts as a duplicate.

An expiry has to be in the future (`SND-MEM-007`). The `memory-expire` built-in service (hourly, no profile entry) deletes facts whose expiry has passed.

## Recall modes

`sindri memory search` ranks the unexpired facts of one store. Exact search, no index.

| Mode | When | Ranking |
|---|---|---|
| `semantic` | the embedding model answers | cosine similarity of the query vector and each fact vector, at the floor of the match level |
| `keyword` | the model is off, unreachable, or `index.embeddings.enabled: false` in the profile | the share of the query's words found in a fact's text and tags; the answer's `note` says why (`SND-MEM-004`, informational) |

Match levels (provisional, see below): `loose` 0.15, `balanced` 0.5 (default), `strict` 0.75. A fact with no vector yet, or one of another dimension, is
skipped, and the answer's `note` says how many. Facts written while the model was down get their vector in the next `memory-expire` pass (a batch of 20 per hour).
The limit defaults to 10 and is at most 50.

Provenance (workflow, run, item, session) comes back with each fact. Recalled text may have been written by an agent from a connection, so the MCP answers fence
it as untrusted (`sindri:memory`).

## Provisional numbers

The three match-level floors and the 0.9 dedupe threshold were tuned on hand-made 4-dimension vectors (`tests/fixtures/memory/pairs.json`), not on the real
output of `nomic-embed-text`. Treat them as provisional until a heavy-lane row measures them on the pinned model.

## Verbs

```
sindri memory list [--json]
sindri memory create <name> [--description <text>] [--json]
sindri memory search <name> <query> [--level loose|balanced|strict] [--limit <n>] [--json]
sindri memory write <name> <text> [--tag <tag>] [--expires-at <time>] [--json]
sindri memory forget <name> --fact <id> | --tag <tag> | --expired [--json]
```

The same services answer HTTP (`GET /sindri/memories`, `GET /sindri/memories/{name}/search`, `POST /sindri/memories`, `POST /sindri/memories/{name}/facts`,
`DELETE /sindri/memories/{name}/facts`) and the MCP tools `memory_list`, `memory_create`, `memory_search`, `memory_write`, `memory_forget`. Agent sessions may use all of them.
`forget` takes exactly one of a fact id, a tag or `--expired`.

## Errors

| Code | Meaning |
|---|---|
| `SND-MEM-001` | no memory has that name |
| `SND-MEM-002` | a memory with that name exists |
| `SND-MEM-003` | a fact is empty or longer than 2,000 characters |
| `SND-MEM-004` | the embedding model did not answer, so recall used keywords (informational) |
| `SND-MEM-005` | the match level is not loose, balanced or strict |
| `SND-MEM-006` | the memory name is not lowercase letters, digits and `-` |
| `SND-MEM-007` | the fact's expiry is not in the future |

## In the app

The app's Memory page (`G M`, or System's "Memory stores" link) lists the stores with their fact counts and lets you create a store, search (it shows the recall mode), write and forget, the same verbs as above. See `docs/csharp/sindri-ui.md` "PR 18a".

## Not here yet

Workflow blocks (`memory_search`, `memory_write`, `memory_forget`) and the `memory` field on the prompt and run-agent blocks need the block runtime of the graph PRs.
