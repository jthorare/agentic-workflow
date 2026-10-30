# Live check: the Sindri-owned index (ConnectAI replacement)

For an agent (Claude Code, Codex, Cursor) setting this up on a new box. Follow the steps in order, fill in the checklist in section 4, and report with the block in section 6. Tracking issues: #1394 (the owned index), #1597 (independent pulls, concepts over the whole stored dataset; this run is its proof 8), #1281 (the whole map's Why row).

## 1. What this checks

Sindri keeps its own index of records, text chunks, meaning vectors and the product map in one Sindri-owned store. Onyx is gone. The index is fed by three connectors (GitHub, Linear, Slack) plus the repo docs of the repos in your profile. "Working" means all of this, for each source:

- a sync ran and stored more than 0 records;
- a word query finds a record, and the hit is tagged as coming from the owned index (`rrf:records#n`);
- a meaning-only query (no shared words with the record) finds it too, tagged `rrf:records-dense#n`;
- a feature lens lists records, each with its `at` time;
- `sindri doctor` shows the `context:index-*` rows as `ok`;
- it pulls on its own: a slow or long source never holds another source's records back, and `index sync <source>` runs only that source.

And for the whole box:

- the `index-decisions` run groups records from every source by meaning and names each group once, so at least one concept cites records from 2 or more sources;
- a rerun with nothing new sends nothing to the model again;
- after a map refresh, the whole map's Why row holds records and decisions.

Proof that a hit came from the owned index is the `rrf:records` / `rrf:records-dense#n` tag on the result item. A citation's source or ref is not proof: it only says `records`.

## 2. Before you start

0. Step 0 on a new box: run `./setup.sh` from a current `main` checkout. Older installs have no `index connect`, `index embed` or `context:index-*` doctor rows. Check with `sindri index connect slack` later: `SND-CLI-003` means the install is too old.
1. Serve running. `sindri` (or `sindri serve`) starts the control plane. `sindri status` should answer.
2. Ollama with the embedding model. The record embedder is pinned to `http://127.0.0.1:11434` and `nomic-embed-text:latest` (pin in `context/code-index.lock`). The profile's `index.embeddings.*` settings only reach the code index, not this one. So:
   ```
   ollama pull nomic-embed-text:latest
   ```
3. At least one repo in the profile's `repos:`. With no repo in scope the feed catalog returns no feeds.
4. Credentials, all by pointer. Never paste a token into the profile, a command line, an issue or a log:
   - GitHub: a fine-grained read-only token covering exactly the listed repos (Issues, Pull requests, Metadata, all read). Entered at the `connect` prompt.
   - Linear: a read-only API key, entered at the `sindri index connect linear` prompt like the others. A pointer you write yourself (`env:NAME`, `file:/abs/path` mode 0600, `op:vault/item/field`, or `keychain:` on macOS) works too.
   - Slack: a bot token. Scopes the prompt names: `channels:history`, `channels:read`, plus `groups:history` for private channels. The bot must be invited to each listed channel. Direct messages are never read. Entered at the `connect` prompt.
5. `connect` and `profile approve` are the human's verbs. They refuse agent sessions (`SND-AUTH-002`) and need a real terminal. If you are an agent, ask the person at the keyboard to run them, or run them in the terminal they gave you.

## 3. Steps

Profile file: `$AW_SINDRI_HOME/profile/profile.yaml` (default `~/.agentic-workflow/sindri-app/profile/profile.yaml`).

### 3.1 Profile block

Add under the existing profile. Placeholders only; fill in your own values:

```yaml
context:
  sources:
    github:
      repos: [<owner/repo>, <owner/repo>]     # owner/name, no duplicates
      # token: written by `sindri index connect github`; do not hand-write
    linear:
      teams: [<TEAMKEY>]                      # empty reads every team the key can see
      # token: written by `sindri index connect linear` (or your own env:/file:/op:/keychain: pointer)
    slack:
      channels:
        - { id: <C0123ABC>, name: <channel-name> }
      # token: written by `sindri index connect slack`
```

Linear teams are keys like `ENG` (a capital, then capitals or digits). Slack channel ids look like `C0123ABC` (or `G...` for private). If you wrote the Linear key as an `env:` pointer yourself, export the variable in the shell that starts serve, then restart serve (`sindri stop`, then `sindri serve`).

### 3.2 Validate

```
sindri profile validate
```

Expected: a line `hash <64 hex>, not approved`, exit 1 (valid but not approved). Fail: `SND-CONTEXT-001` names the bad key path. `SND-CONTEXT-002` means a token value was pasted instead of a pointer: rotate that token and use a pointer.

### 3.3 Approve (the owner or the person at the terminal)

```
sindri profile approve            # preview: hash and changed key paths
sindri profile approve <hash>
```

Expected: `Approved <hash>`. Run `sindri doctor`; before this point every `context:index-*` row says `not configured (no profile is approved)`, which is fine.

### 3.4 Connect GitHub

```
sindri index connect github
```

Prompts once for the token (not echoed). It first reads one issue page from every listed repo and stores the token only if all pass. Expected: `github connected: the token reads all N listed repos and is stored at <pointer>; the profile points to it.` then `Run sindri profile approve to start using it.` Run `sindri profile approve` again (preview, then with the hash).

Failures: `SND-INDEX-009` the token was refused (nothing saved; make a new one); `SND-INDEX-010` the token can't read some listed repos (they are named); `SND-INDEX-011` the profile lists no repo; `SND-AUTH-002` you are in an agent session, or stdin is not a terminal.

### 3.5 Connect Linear

```
sindri index connect linear
```

Same flow as GitHub: one hidden prompt, checked against every team `context.sources.linear.teams` lists (when it lists none, the key must read at least one team, and the teams it reads are written in, sorted), then stored and pointed to from the profile. Then `sindri profile approve` again. Failures: `SND-INDEX-009` the key was refused, `SND-INDEX-010` it can't read a listed team; nothing is saved either way. If you wrote your own pointer in 3.1 instead, export the variable before serve starts (or make the file), and skip the connect.

Check:

```
sindri doctor
```

Expected: `context:index-linear` is `ok <pointer> resolves (not probed: ...)`. If the profile lists teams but no token, `context:linear` warns `Linear needs a token: the approved profile lists teams <TEAMKEY> but no context.sources.linear.token` with the fix `sindri index connect linear`, and the app's Projects page says `Linear needs a token` too: run the connect. Doctor never calls Linear; the first sync proves the key. A refused key shows up at sync as `SND-CONTEXT-071`, an unreachable or rate-limited Linear as `SND-CONTEXT-010`.

### 3.6 Connect Slack

```
sindri index connect slack
```

Same flow as GitHub: one hidden prompt, checked against every listed channel, then stored and pointed to from the profile. Then `sindri profile approve` again. Failure `SND-INDEX-009` is a refused token.

Who sees Slack: a CLI `sindri context query` is a human and reads the profile's channels. An agent session sees a Slack record only when it is linked to its repo's graph.

### 3.7 Sync

```
sindri index sync
```

Expected: `job JOB-... started`, `run index-sync (manual)`, `succeeded: ok: <source> N fetched, ...`, `ran index-sync (all sources)`, exit 0. Write down the job id. Notes:

- "fetched" is not "records". The recent-window pass and the older-history pass can fetch the same item twice.
- `sindri index sync <source>` runs only that source, as its own job (`job JOB-... started`). An unknown name lists the feeds and starts nothing. A named sync pulls even while that source is in backoff.
- Bare `sindri index sync` starts every source's pull; none waits for another.
- `'<name>' isn't a record source of this profile` means that feed is not active (usually a missing or unresolvable token). Go back to 3.4 to 3.6.
- A source that is down shows as a status row with a code, not as a failed job. Serve also syncs every 5 minutes on its own.

### 3.8 Status until usable

```
sindri index status
```

One line per source: `<source>  <state> · last sync <time>`. Expected sources: `github`, `linear`, `slack`, `repo-docs` (a source with no stored token has no row yet, so github and slack show up only after connect). State words: `not started`, `syncing recent window`, `usable, filling older history`, `complete`, or `last sync failed: <code> (<title>)`. Pass: every source says `usable...` or `complete`. Exit 1 while any last sync failed or serve is down. `SND-CONTEXT-059` as a trailing code only means the sync hit its time cap; the state word stays true.

After the state, a row may add how its pull stands: ` · pulling now`, ` · backoff, 2 failures, next try 14:05Z` (plus the last error), ` · failed, 5 failures`, and ` · last commit 13:58Z`, the last time that source wrote records. Below the sources sits the concepts line (3.13).

Re-run `index sync` and `index status` until usable. `--json` has `phase`, `usable`, `complete`, `lastSync`, `lastCode`, and per source `pull`, `failures`, `nextTry`, `lastCommit`, `lastErrorText`, but no record counts; take counts from the sync output and from `sindri index purge <source>` preview if you must (answer anything but `yes`, it then says `nothing was deleted`).

### 3.9 Embed

```
sindri index embed
```

Expected: `succeeded: ok: embedded N chunks (M stored), none left`. Ollama down is not a failed job; it is the "meaning search is off" state. Start Ollama and re-run. Serve also embeds every minute, newest records first, 4,096 chunks per run, so a big first sync needs several runs: repeat until `none left`.

### 3.10 Queries

Use `--json` and pull the tags:

```
sindri context query "<text>" --json | grep -o 'rrf:[a-z-]*#[0-9]*'
```

Run these, picking a real, distinctive item for each source:

1. Word query, GitHub: a phrase from a PR or issue title in a listed repo. Expect a hit whose permalink opens that PR or issue and a tag `rrf:records#n`.
2. Word query, Linear: a phrase from an issue title, or its key. Expect a hit with that key and its Linear link, tagged `rrf:records#n`.
3. Word query, Slack: a phrase from a message in a listed channel. Expect a Slack permalink, tagged `rrf:records#n`.
4. Word query, repo docs: a phrase from a doc in a listed repo.
5. Meaning-only query, one per source: ask for a record without using its words (a PR about "retry storm" asked as "why do requests pile up after a failure"). Expect the record with `rrf:records-dense#n`. On a tiny corpus the word leg still returns everything at a low rank, so read the `records-dense#` rank, not the fused order.

Degraded lines you may see:

- `meaning search is off: Ollama isn't running`: start Ollama.
- `meaning search covers <n>% of your records; the rest are word-search only`: run `sindri index embed` until `none left`, then re-query.
- No dense hits and no line: no vectors yet, do 3.9 first.
- A degraded source `linear` on a query: expected only when the one batched `updatedAt` freshness call for cited Linear records failed. It is not a Linear hit.

Linear results come only from the owned index. A query never searches Linear live.

### 3.11 Feature lens

A feature lens needs a product map. If `sindri context map status` shows none, run `sindri context map refresh` and wait for the job. Pick a feature key from the map (`sindri context map export --json`, or `sindri context show <name>`), then:

```
sindri context lenses feature:<key> --json
```

Expected: records listed under the intent band, each with an `at` time; a `searched` row for each owned-index source (github, linear, repo-docs; slack when configured), `degraded` when that source's last sync failed. There is no "onyx not configured" row any more. Repo docs and tracker records link to features, so a feature with a doc or an issue about it must list at least one record. Plain text form: drop `--json`.

### 3.12 Doctor

```
sindri doctor --json
```

| Row | Pass |
|---|---|
| `context:index-store` | `ok the record store answers (N source feed(s))` |
| `context:index-github`, `-linear`, `-slack` | `ok <pointer> resolves (not probed: ...)` (warn `<key> is not set`: set the variable or run `connect`; fail `SND-SECRET-...`: the pointer is wrong) |
| `context:index-ollama` | `ok Ollama answers at 127.0.0.1:11434 with nomic-embed-text:latest` (warn: start Ollama, pull the model; a wrong digest also warns) |
| `context:index-sync` | `ok record index: <source> synced <time>` for each source (before the first sync: `warn ... not synced yet`, expected) |

Ignore `context:sync` (the old sources sync; its detail starts with `sources sync:`) and `context:linear` for this check. A failing row unrelated to the index (for example `sandbox`) can set the doctor exit code to 2; note it, it is not a failure of this check.

### 3.13 Pulls don't wait for each other

On a first run GitHub's older-history pass is usually the longest. While its row still says `syncing recent window` or `usable, filling older history` with ` · pulling now`, run `sindri index status` a few times. Pass: the Linear, Slack and repo-docs rows each get a ` · last commit` time while GitHub is still pulling. Then run `sindri index sync linear`: it starts one job keyed `index-sync-linear`, and `sindri jobs --json` shows no new `index-sync-github` or `index-sync-slack` job from it.

### 3.14 Decisions and concepts over every source

Concepts are built from the stored records' vectors, so finish 3.9 (`none left`) first. Both `context.labeling.enabled` and `context.conceptSynthesis.enabled` must be on (they are by default). The `index-decisions` service runs every 15 minutes while serve runs; to run it now:

```
sindri services run index-decisions
```

Expected: the job succeeds and its summary names each source it read and a `concepts ...` part. Then:

```
sindri index status
```

The last line reads `concepts: <N> (<M> cite 2+ sources), <Q> queued, <C> of <E> records covered (<P>%), watermark <W>, last run <HH:MM>Z`. Other forms: `concepts: none yet` (no group formed: too few embedded records), `concepts: off (...)` (a switch is off), `concepts: unavailable (<code>)`. `<Q> queued` above 0 means the run hit `context.labeling.maxTokensPerSync`; run it again until `0 queued`.

Pass:

1. `<M>` is above 0 once two or more sources are usable and embedded.
2. Run `sindri services run index-decisions` again with nothing new synced. The summary has no new concepts, `0 queued` stays, and the model call count doesn't move (Health's model calls, or `sindri jobs show <id> --json`).
3. Run `sindri context map refresh`, then `sindri context map export --json` and take a node of kind `concept` whose attributes say `"source": "synthesized"`. `sindri context lenses concept:<slug> --json` lists records from at least two sources (say a GitHub PR and a Linear issue), each with its permalink. In the app, the Context page's Concepts list shows the same concept.

### 3.15 The whole map's Why row (#1281)

After 3.14's map refresh:

```
sindri context map whole --json | jq '.counts.byKind | {record, decision, concept}'
```

Pass: `record` above 0, and `decision` above 0 when the decisions run found any (its summary says how many). In the app: Context, then Map, type a starting point, Enter. The concept row is on top, the Why row under it holds records and decisions, then Product, then Code. Click a concept, then click it again: its own three-band view opens with Why on top. An empty Why row while 3.8 says usable and 3.14 found decisions is the #1281 bug.

## 4. Pass/fail checklist

Copy this, mark each cell `pass`, `fail` or `n/a` (source not configured), and add the evidence for every `fail`.

| Check | GitHub | Linear | Slack | Repo docs |
|---|---|---|---|---|
| `index sync` succeeded, records stored > 0 (count: ) | | | | |
| `index status` says usable or complete | | | | |
| Word query hit, tagged `rrf:records#n` | | | | |
| Meaning-only query hit, tagged `rrf:records-dense#n` | | | | |
| Feature lens lists records with `at` times | | | | |
| Lens has a searched-source row, not degraded | | | | |
| Doctor `context:index-<source>` ok | | | n/a | |
| Got a `last commit` while another source was still pulling (3.13) | | | | |
| `index sync <source>` ran only that source | | | | |

Whole-box rows: `context:index-store` ok, `context:index-ollama` ok, `context:index-sync` ok, no `rrf:linear#n` tag anywhere, no live-Linear result; the concepts line shows `<M>` above 0 and `0 queued`; a second decisions run with nothing new sends nothing to the model; a synthesized concept's lens cites 2+ sources; the whole map has `record` (and `decision`) above 0 and the app's Why row shows them.

## 5. Regressions to report

Report each of these; do not work around them.

- An `rrf:linear#n` tag on any result. The old live Linear source is gone, so this is a regression.
- `index status` saying `usable` (or `complete`) for a source whose last sync failed or stored 0 records.
- A lens with empty records or an empty Why for a feature that has docs or issues, or a record with no `at` time.
- A lens still showing an "onyx not configured" row, or missing a searched-source row for a configured source.
- A word query that cannot find a record that is plainly in `index status`'s usable source.
- A meaning-only query that returns nothing while `context:index-ollama` is ok and `index embed` says `none left`.
- A source with a stored token but no row in `index status`, or `index sync` failing with `SND-CONTEXT-072` on a valid token.
- A secret value showing up in any output, log or doctor row.
- A source whose `last commit` doesn't move while another source is pulling, or `index sync <source>` starting every source.
- `index status` saying a source is syncing when its row has no ` · pulling now` and its `last commit` is old: "a sync job is running" isn't evidence that every source pulls.
- `concepts: ... (0 cite 2+ sources)` with two or more sources usable and `index embed` at `none left`.
- A second decisions run with nothing new that sends records or groups to the model again.
- An empty Why row on the whole map while records are synced and the decisions run found decisions.

Evidence to capture for each:

- the exact command and its full output (redact nothing but tokens, which should never appear anyway);
- `sindri doctor --json`;
- `sindri index status --json`;
- the sync (or embed) job id, from the `job JOB-... started` line;
- for a query: the `--json` output and the `rrf:` tags;
- the date and the `git rev-parse --short HEAD` of the checkout you ran `./setup.sh` from.

Keep work identifiers (company, repo, channel and team names, people) out of the issue: use the same placeholders as in this doc.

## 6. How to report back

Open one issue on `jthorare/agentic-workflow` titled `ConnectAI live check: <pass|fail> on <date>`. Paste:

```
Checkout: <short sha>   Box: <os>   Ollama: <up|down>, model nomic-embed-text:latest <present|missing>
Sources configured: <github, linear, slack, repo-docs>

| Check | GitHub | Linear | Slack | Repo docs |
| (the table from section 4, filled in) |

Doctor rows: index-store <state>, index-ollama <state>, index-sync <state>
Sync job ids: <ids>
Concepts line: <the line from index status>   Multi-source concept: <slug>, cites <sources>
Rerun with nothing new: <no model calls | N model calls>
Whole map byKind: record <n>, decision <n>, concept <n>   Why row in the app: <shows records and decisions | empty>
rrf:linear seen: <yes|no>
Regressions: <none | list, each with command, output and doctor --json attached>
Not tested: <anything skipped and why>
Verdict: <works | does not work>, <one sentence>
```

Attach `sindri doctor --json` and `sindri index status --json`. Never attach tokens, key files or the stored credential.
