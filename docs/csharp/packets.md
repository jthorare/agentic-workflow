# Packets — `aw-packets validate | freeze | hash | verify`

`Aw.Packets` (`src/Packets`, tier 1, small shape: `Aw.Packets.Core` + `Aw.Packets.App`) checks and freezes handoff
packets (factory addendum §17.4). Before the switch it is the standalone NativeAOT CLI `aw-packets`, which writes only
under `${AW_STATE_DIR:-~/.agentic-workflow}/packets/` (spec §4c); nothing installs or schedules it. After the switch,
sindri offers them as `sindri packet …` by running `aw-packets` (a `cli` edge in `deps.yaml`; spec §4c, addendum §17.4).

It was generated with `/new-package` (the invocation is in the PR that added it) and grown slice by slice with
`/new-slice`.

## A packet directory

| File | Record | Holds |
|---|---|---|
| `packet.json` | `PacketManifest` | `schemaVersion`, `id` (`PacketId`: lower-case letters, digits, `-`, ≤ 64), `repo` (`RepoSlug`: `owner/name`, each side letters, digits, `.`, `_`, `-`, not ending in `.`, not a Windows device name), `baseSha` (text, so a short sha is a PKT-BASE-001 failure rather than a malformed packet), `status` (`draft`, `frozen`), `hash` (`PacketHash`: `sha256:` + 64 hex, or null), `links` |
| `links.decisions` | `DecisionLog` | `questions[]`: `id`, `text`, `asker`, and either `answer` or `deferred: { owner, buildMayAssume }`; `affects[]` |
| `links.outOfScope` | `OutOfScope` | `items[]` |
| `links.tasks` | `TaskPlan` | `workstreams[]` (`id`, `title`) and `tasks[]`: `id`, `workstream`, `title`, `deps[]`, `size` (`XS`…`XL`), `writeScope[]` (globs, or `new: <path>`), `readSeeds[]`, `acceptance[]` (`id`, `text` — none for a held-out example —, `evidence[]`, `holdout`), `rules[]`, `budget` (`tokens`, `turns`, `wallMinutes`) |
| `links.design[]`, `links.plans[]`, `links.workflow` | — | Any format; part of the hash by content |

Every JSON file carries `schemaVersion` (1). The JSON Schemas for editors are generated from the records and committed
in `schemas/packets/` (`PacketSchemaTests` fails when a record changes and the schema isn't regenerated). Links are
relative paths with `/` inside the packet directory; `..`, a root or a `:` is refused, and so is a symbolic link anywhere between the packet directory and the file (a linked file or a linked sub-directory). Every JSON file is UTF-8 without a byte order mark, with no duplicate keys, no unknown members (an optional `$schema` editor hint is allowed at the top of each file) and no null in a list; a packet holds at most 2,000 tasks (`PacketReader.MaxTasks`). Anything else is AW-PACKETS-003.

Write-scope globs are repo-relative: `*` and `?` stay inside one path segment, `**` crosses segments (`src/**`,
`**/guide.md`; `**` only as a whole segment, inside one it acts as `*`); entries are at most 1,024 characters, and matching is a two-pointer walk with no regex (bounded by the entry length, no backtracking blow-up); each distinct entry is matched against the base commit's files once. `new: src/c.cs` declares a file the task creates.

## Checks (`aw-packets validate <packet-dir> [--repo <dir>] [--json]`)

`--repo` is a directory inside the repository the packet builds (default: the packet directory). The base commit's
file list comes from git.

| Rule | Check |
|---|---|
| `PKT-ID-001` | Task, workstream, question and example ids are unique |
| `PKT-WS-001` | Every task is in a declared workstream |
| `PKT-GRAPH-001` | Every dependency names a task in the packet |
| `PKT-GRAPH-002` | The task graph is acyclic: each strongly connected group is reported once, naming every task in it and one cycle through it (`tasks t1, t2, t3 form a cycle: t1 -> t3 -> t2 -> t1`) |
| `PKT-BASE-001` | `baseSha` is a full commit sha that exists in the repository |
| `PKT-SCOPE-001` | Every write scope is a relative glob that matches a file at the base commit, or `new:` a path that can be created there (not an existing file or directory, not under an existing file, and not in conflict with another declared new path); a glob with a leading `/` or a `.`/`..` segment is named as not relative |
| `PKT-SCOPE-002` | Two tasks no dependency chain orders don't write the same path (a base file both match, or a declared new path) |
| `PKT-ACC-001` | Every task has a visible acceptance example, and every visible one has text |
| `PKT-EVID-001` | Every acceptance example names at least one kebab-case evidence kind (addendum §17.11) |
| `PKT-DEC-001` | Every question is answered, or deferred with an owner and a `buildMayAssume` default |
| `PKT-HOLD-001` | Every task holds at least one acceptance example out (addendum §17.4: a share held out, per task); a task with no examples fails |
| `PKT-HOLD-002` | No held-out example keeps its text in the packet: held-out text lives in sindri-private state, mounted only into evaluator Steps and never committed (addendum §17.19); the packet keeps its id and evidence kinds |
| `PKT-SIZE-001` | Every task fits its size class's limits from a sizes file (`validate --sizes <file>`, below): `unchecked` without one |
| `PKT-FLOW-001` | **Unchecked**: workflow coverage (addendum §17.7) needs the lens catalog and workflow generation |

The text output lists every rule (`pass`, `FAIL` with the subject, what was found and the fix, or `unchecked` with the
reason). A rule reports at most 50 failures, then one `omitted` record whose `omitted` field counts the rest (the summary line's failure count includes them). `--json` writes one document, `schemaVersion` 1, with `packet`, `passed` and `checks[]` (one record per
failure, one per passing or unchecked rule; each failure has `rule`, `subject`, `message` and `fix`).

## Sizes (`PKT-SIZE-001`, `validate --sizes <file>`, LN-PKT3)

PKT owns the shape of the limits, the Sindri profile the values: Sindri writes the sizes file from `workflow.plan.sizes`
and passes it, so tier-1 `aw-packets` never reads the profile. Without `--sizes`, the rule is `unchecked`; with it,
each task is held to its own size class (`validate` of a build packet, and of a plan graph with `--kind plan`; no other
command takes it, and the other line kinds have no tasks). The file (`schemas/packets/sizes.schema.json`, generated from `SizesFile`):

```json
{ "schemaVersion": 1,
  "sizes": { "XS": { "files": 3, "items": 2, "budget": { "tokens": 50000, "turns": 20, "wallMinutes": 30 } },
             "S": {...}, "M": {...}, "L": {...}, "XL": {...} } }
```

All five sizes are present. A limit is zero or more; a `budget` field left out is not checked. A task fails the rule when:

- the files its write scope covers exceed `files`: the base commit's files its globs match plus the paths it declares
  `new:`, each file once however many entries match it;
- its acceptance examples (held-out ones included) exceed `items`;
- its `budget.tokens`, `turns` or `wallMinutes` exceed the limit (a field the task leaves out is not compared).

The failure is one record per task, naming each limit it is over, with the fix (split the task or raise its size). It is
`unchecked` when the base commit can't be read (the files can't be counted). A sizes file that is missing (or a directory), unreadable,
larger than 64 KB, malformed, not `schemaVersion` 1 or with a negative limit is `AW-PACKETS-009` (exit 2), never a check
result. `freeze` takes no `--sizes` (Sindri validates first).

## Freeze (`aw-packets freeze <packet-dir> [--repo <dir>] [--by owner|machine] [--json]`)

Runs the same checks. A packet with a failed check isn't frozen and nothing is written. Otherwise its canonical hash
is recorded at `packets/<owner>/<name>/<id>/frozen/<hex>.json` (per repository: a packet id is unique only within one) (`FreezeRecord`: `schemaVersion`, `repo`, packet, hash,
`digestVersion`, `kind` (`build` unless the record says otherwise), `by` (`machine` or `owner`, below), `baseSha` (left out for a kind that has none), `frozenAt`, the files the hash covers). Freezing the same hash again writes nothing and says `already frozen`. The
packet directory itself is never changed; the record under `packets/` is authoritative (the ledger of the C# Sindri
application takes over once it exists, spec §4c; Sindri is built new, not ported, factory addendum §17.29).

**Who froze it (`--by owner|machine`, LN-PKT5, line spec §2).** `freeze` takes `--by` for every kind (default `machine`); it is
stored as `by` in the `FreezeRecord` and printed by `verify`. `step.build.amend` and the stop blocks pass `owner`, every other
freeze `machine`. A freeze is write-once: freezing a hash that is already frozen writes nothing and keeps the first record, `by` and `frozenAt` included, there is no upgrade from `machine` to `owner`. The response and the text report the **stored** record's `by` and `frozenAt`, and when the stored `by` is not the one this call asked for (an `--by owner` freeze of a hash a machine froze first) they add `alreadyFrozenBy: <stored by>` (JSON) and a `note: alreadyFrozenBy …` line (text): the exit stays 0 (it is frozen), but the owner's freeze was not recorded. So Sindri must freeze as `owner` first when the owner's freeze has to be provable, and check `alreadyFrozenBy`/`verify`'s `by` otherwise. A record from before `by` existed has no `by` and reads as `machine` (every such freeze was a machine
freeze); `verify` prints it so. The record is not part of the digest, so `by` never changes a hash. Freezing a hash that is
already frozen writes nothing and keeps the first record's `by` (above).

**The hash (`digestVersion` 1, spec §4c Digests):** SHA-256 over the canonical JSON of
`{"digestVersion":1,"files":{<path>:<entry>}}`. A JSON packet file's entry is the file itself without its `$schema` editor hint (and `packet.json` without
`status` and `hash`); any other linked file's entry is `sha256:<hex of its bytes>`. Canonical JSON sorts object keys by
ordinal, drops whitespace, keeps numbers as written, and writes strings as System.Text.Json's relaxed encoder does (the Kernel profile): `"` and `\` escaped, control characters in their short or `\u00XX` form, U+2028/U+2029 and characters outside the Basic Multilingual Plane as `\u` escapes (surrogate pairs), everything else as UTF-8. `CanonicalJsonTests` pins examples and one packet's hash; changing these bytes needs a new `digestVersion`.

## Verify (`aw-packets verify <packet-dir> --hash <h> [--candidate] [--kind K] [--json]`, LN-PKT2)

Recomputes the packet's hash with the one canonicalization `hash` prints and `freeze` records (any of the eight kinds:
`--kind`, `build` by default), compares it with `--hash` (`sha256:` and 64 lower-case hex digits), and finds the freeze
record where `freeze` wrote it, `packets/<owner>/<name>/<id>/frozen/<hex>.json`, with `<owner>/<name>` and `<id>` read from
`packet.json` (build and the line kinds). A scope packet has no `packet.json`: give the repository and instance it was frozen
under, `--repo-slug <owner/name> --instance <slug>`, as `freeze --kind scoping` takes them (only a scope packet takes them).
With `--candidate` it only recomputes and compares, looks for no record, and needs neither. It reads only: nothing is written.

| Exit | When |
|---|---|
| 0 | The hash matches and (unless `--candidate`) a freeze record of that hash exists |
| 1 | The packet hashes to something else (`MISMATCH`), the hash was never frozen for that packet (`NOT FROZEN`), or the packet is malformed (`AW-PACKETS-003`, `-004`) |
| 2 | A usage error (a missing or malformed `--hash`, an unknown or repeated flag), or the packet or the record can't be read (`AW-PACKETS-002`, `-008`) |

Text: `aw-packets verify: <kind> packet <id> — verified <hash>, frozen by owner at 2026-10-17T09:00:00Z (packets/…)`, or
`… — MISMATCH: the packet hashes to <a>, not <b>`, or `… — NOT FROZEN: no freeze record for <hash> (looked at packets/…)`, or
`… — hash matches <hash> (candidate: no freeze record looked up)`. `--json` writes `{schemaVersion, kind, packet, verified,
candidate, expected, computed, digestVersion, frozen, by, frozenAt, location, reason}` (`packet`, `frozen`, `by`, `frozenAt`,
`location` and `reason` left out when unknown or not applicable). A record whose own `hash` differs from its location is not
verified. A phase packet's hash covers its `packet.json` links, so verifying the newest amendment is verifying by its own hash:
which packet is in force under a phase is Sindri's to say.

## Packet kinds (`--kind build|scoping|design|plan|phase|proof|review|handoff`)

`PacketKinds` (`Aw.Packets.Core.Kinds`) is the registry: a kind is a name and the fixed files its schema lists. Every
command takes `--kind` (anywhere in its arguments; `build`, the packet above, when it isn't given; an unknown kind is a
usage error, exit 2). The `build` kind has no fixed list (`packet.json` links its files); the `scoping` kind (scoping
station spec §8.3, P1) lists twelve; the six **line kinds** (below, LN-PKT1) list their files too, `packet.json` and `index.json` among them.

| File | Format | Checked here |
|---|---|---|
| `scope-map.json`, `acceptance.json`, `coverage.json`, `size.json`, `questions.json`, `challenge.json`, `runs.json`, `sources.json` | one JSON object | well formed (`PKT-SCP-003`) |
| `lenses.json` | one JSON object, `LensesFile` (`schemas/packets/lenses.schema.json`) | well formed, and the schema (`PKT-SCP-004`, `-005`) |
| `answers.jsonl`, `rounds.jsonl` | one JSON object per line (blank lines allowed) | well formed |
| `brief.md` | UTF-8 text | well formed |

**Rules (schema only).** What the files mean (citations, coverage, trust, and every check across files) stays in
Sindri's `ScopePacketChecks`, which `aw-packets` (tier 1) must not reference (tier 3); Sindri calls `aw-packets` through
its `cli` edge. That includes `SCP-LENS-001`, the check that `lenses.json` agrees with `coverage.json`: it reads two
files, so it lives in Sindri, not here. The ids are `PKT-SCP-*`, never `PKT-SCOPE-001/002` (the build kind's write-scope
rules).

| Rule | Holds when |
|---|---|
| `PKT-SCP-001` | all twelve files are present |
| `PKT-SCP-002` | the directory holds no other entry (a sub-directory counts, and is named with a trailing `/`) |
| `PKT-SCP-003` | each JSON file is one strict JSON object (UTF-8, no byte order mark, no duplicate key, no null in a list), each JSON-lines line is one object, `brief.md` is valid UTF-8 |
| `PKT-SCP-004` | `lenses.json` has `schemaVersion` 1 and the four lenses (`code`, `product`, `related`, `history`); each has `anchors`, `depth` (0 to 32) and `hops` (the hop kinds walked, each of `state`, `flow`, `dependency`, `component` at most once, in any order, empty at depth 0), `mapVersion` (a non-empty string, as the context engine writes it, for example `v12@a41b07d`), `foundCount` (at least the nodes and items listed, more when a band is `truncated`), `nodes[]`, `edges[]`, `items[]` and `truncated[]` (bands `code`, `product`, `intent`, each once); ids match `^L-(code\|product\|related\|history)-[1-9][0-9]*$`; `band` and `trust` (`untrusted`, `owner`) are from their enums; no unknown field |
| `PKT-SCP-005` | the lens item ids are unique across the file, and each names the lens it sits in |

`PKT-SCP-004` and `-005` are `unchecked` when `lenses.json` is missing or not well formed.

**`validate --kind scoping <packet-dir> [--json]`** runs those five rules; the output is the build kind's (`kind` is
`scoping`, and there is no `packet`). Exit 0 passed, 1 a check failed, 2 the directory is missing. It takes no `--repo`
and writes nothing.

**`hash <packet-dir> [--kind build|scoping] [--json]`** prints the canonical hash and runs no check; with `--json` it
writes `{schemaVersion, kind, hash, digestVersion, files}` (`files` sorted). It is the one canonicalization:
Sindri's `evidence` and `freeze` Steps call it, and `freeze` records the same value. A scope packet with a file
missing, or a JSON file with a duplicate key, has no hash (`AW-PACKETS-003`, exit 1). Entries the kind doesn't list are
not hashed (`validate` fails them).

**`freeze --kind scoping <packet-dir> --repo-slug <owner/name> --instance <slug> [--json]`** validates, then records the
hash at `packets/<owner>/<name>/<instance>/frozen/<hex>.json` (`instance` is a `PacketId`; the record's `kind` is
`scoping` and it has no `baseSha`). A failed check freezes nothing (exit 1); the same hash again says `already frozen`.

**The scoping hash** is the build kind's canonical form over the twelve files, with `"kind":"scoping"` added to the hashed
document so no build packet can collide with it: a JSON file's entry is the file without its `$schema` hint, a
JSON-lines or text file's entry is `sha256:<hex of its bytes>`. `ScopingDigestTests` pins one packet's hash, computed
independently of the C# code.

## Line kinds: `design`, `plan`, `phase`, `proof`, `review`, `handoff` (LN-PKT1)

The spec-to-proven-PR line (`docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md` §3.2) is a chain
of packets, each an `aw-packets` kind. Unlike `scoping`, every line kind has a **`packet.json`** whose `links` name each of
the kind's files and its `index.json`, so the canonical hash covers all of them, and **PKT-BASE-001** and the freeze
checks apply to every kind. The kinds, their files and the `links` key that names each:

| Kind (rule prefix) | Files (link key) |
|---|---|
| `design` (`PKT-DSN`) | `spec.md` (spec), `sections.json` (sections), `coverage.json` (coverage), `decisions.json` (decisions), `acceptance.json` (acceptance), `signals.json` (signals), `contracts.json` (contracts), `lens-rounds.jsonl` (lensRounds), `style.json` (style), `escapes.json` (escapes), `runs.json` (runs), `index.json` (index) |
| `plan` (`PKT-PLN`) | `tasks.json` (**tasks**: PKT's own `links.tasks`, LN10), `graph.json` (graph), `exec-review.json` (execReview), `holdout.json` (holdout), `answers.jsonl` (answers), `index.json` (index) |
| `phase` (`PKT-PHS`) | `phase.json` (phase), `acceptance.json` (acceptance), `signals.json` (signals), `holdout.json` (holdout), `design.md` (design), `index.json` (index) |
| `proof` (`PKT-PRF`) | `proof.json` (proof), `red.json` (red), `before-after.json` (beforeAfter), `holdout.json` (holdout), `not-proven.md` (notProven), `acceptance.json` (acceptance), `index.json` (index), and every file under `app-run/` (appRun) and `evidence/` (evidence), listed one by one |
| `review` (`PKT-REV`) | `review.json` (review), `index.json` (index) |
| `handoff` (`PKT-HND`) | `pr.json` (pr), `body.md` (body), `green.json` (green), `index.json` (index) |

**`packet.json`** (`schemas/packets/<kind>-packet.schema.json`, one per kind): `schemaVersion` 1, `kind`, `id` (a
`PacketId`), `repo`, `baseSha` (text, so a short sha is a PKT-BASE-001 failure), `status`, `hash` and `links`. The links
of a file are strings equal to the file's name; a proof pack's `appRun` and `evidence` are lists of paths under
`app-run/` and `evidence/` (any format; hashed by content). No unknown field. The plan graph's `tasks.json` is PKT's
`TaskPlan` (the build kind's, `schemas/packets/tasks.schema.json`): one task per phase, stacks as workstreams.

**`index.json`** (`schemas/packets/index.schema.json`): `schemaVersion` 1, `kind` (the packet's), `parents` (the hashes of
the packets it builds on, each once) and `acceptance` (`self`: the hash of its own `acceptance.json`, null when it has
none; `extends`: the hash of the `acceptance.json` it extends, null at the root of the chain).

**Rules (schema only).** One engine, one prefix per kind, numbered from 001. What the files *mean* (coverage, signals,
the executability review, hidden scenarios, the PR) stays in Sindri's `DesignPacketChecks`, `PlanChecks` and
`ProofChecks`, which `aw-packets` (tier 1) must not reference.

| Rule (`P` = the kind's prefix) | Holds when |
|---|---|
| `P-001` | every file the kind lists is present |
| `P-002` | the directory holds no other entry (a sub-directory counts, named with a trailing `/`; `app-run/` and `evidence/` are the proof pack's own) |
| `P-003` | each JSON file is one strict JSON object (UTF-8, no byte order mark, no duplicate key, no null in a list), each JSON-lines line is one object, each text file is valid UTF-8 |
| `P-004` | `packet.json` matches the kind's schema, says `"kind": "<this kind>"`, and has `schemaVersion` 1 |
| `P-005` | `links` name each of the kind's files and nothing else; a linked directory's list equals the files found under it (an unlisted file would escape the hash), each once |
| `P-006` | `index.json` matches its schema, says the kind, lists no parent twice, and ties to `acceptance.json`: `acceptance.self` is the `sha256:` of that file's bytes (null when the kind has none) and `acceptance.extends` equals its `parent` |
| `PKT-BASE-001` | `baseSha` is a full commit sha that exists in the repository (`--repo`, default the packet directory) |
| `PKT-DSN-007`, `PKT-PRF-007`, `PKT-PHS-010` | `acceptance.json` matches its schema: `schemaVersion` 1, `parent` (the hash of the previous `acceptance.json`, line spec LC-2), `items[]` with a non-blank `id` and `text`, a `status` of `failing`, `passing` or `superseded`, `evidence[]` of `{kind, path, sha256, commit, at}`, and `supersededBy` (the id of an item in the file) on a superseded item and on no other (provisional, pending the line spec's amendment); the other item fields (`form`, `surfaces`, `citations`, `trust`, the item's own `parent`) pass through, since the scoping spec and Sindri's `LC-ACC-001` own them |
| `PKT-DSN-008`, `PKT-PHS-011` | item ids are unique and no item is `passing`: only the proof pack flips an item (LC-2) |
| `PKT-PRF-008` | item ids are unique and every `passing` item has evidence |
| `PKT-PLN-007` | `tasks.json` matches the task-plan schema; then **the build kind's task rules run on it** (LN10): `PKT-ID-001`, `PKT-WS-001`, `PKT-GRAPH-001/002`, `PKT-BASE-001`, `PKT-SCOPE-001/002`, `PKT-ACC-001`, `PKT-EVID-001`, `PKT-DEC-001` (an empty decision log: it passes), `PKT-HOLD-001/002`; `PKT-SIZE-001` is checked when `--sizes` is given (LN-PKT3). They are unchecked when `tasks.json` can't be read |
| `PKT-PHS-007` | the packet id is `<line>-<phase>` or `<line>-<phase>-a<n>`: lower-case letters, digits and `-`, at least two segments, an amendment number from 1 |
| `PKT-PHS-008` | `phase.json` matches its schema: `goal`, `kind` (`feature`, `fix`, `refactor`, `docs`, `config`), `deps`, `size`, `base` (`rule` `default-branch` or `dependency-head`, and its `ref`, which for `dependency-head` is one of `deps`), `allowedFiles` (non-empty, each a relative glob or `new: <path>`, the write-scope syntax), `testChange[]` (`path` and `reason`), `readSeeds`, `budget`; an optional `amends`; no unknown field |
| `PKT-PHS-009` | `phase.json` has `amends` exactly when the id ends `-a<n>`; an amendment's `paths` are non-empty write-scope entries and its `reason` is not blank |
| `PKT-PHS-012` | `holdout.json` is `{schemaVersion, setHash, count}`: the hash and size of the hidden set, never its text |
| `PKT-HOLD-003` | A phase packet's `holdout.json` `setHash` is the one its seal names (LN-PKT4, line spec §9.1): `validate --kind phase --seal <file>`, where the file is the `seal.json` the holdout store writes, `{schemaVersion, phase, setHash, planHash}`, of which only `schemaVersion` (1) and `setHash` are read. `unchecked` without `--seal` or when `holdout.json` can't be read; only the phase kind has the rule, and `validate` reads the seal file and writes nothing |
| `PKT-HND-007` | `pr.json` matches its schema: `base`, `headBranch`, `headSha` (a full sha), `stackParent` (a branch or null), `title`, `labels` (each once) |

`PKT-xxx-007` and above that name a file check are `unchecked` when the file is missing or not well formed; `P-005` and
`PKT-BASE-001` are `unchecked` when `packet.json` can't be read.

**Errors name the file and the field** (`index.json: the property 'id' is not a field of this file.`), never a CLR type.

**Re-freezing an id.** `freeze` does not refuse a packet whose id was frozen before with other content: it records
the new hash beside the old one (the record is per hash), and `verify` (LN-PKT2) matches by hash, not by id. Which
packet is "the newest in force" under a phase, and that ids are unique per line, are Sindri's to enforce.

**Phase-packet ids are unique per line** (line spec LN-PKT1): that is a fact about *all* of a line's packets, which one
packet's schema-only `validate` cannot see, so Sindri checks it (as it does `SCP-LENS-001` for the scope packet). An
amendment is a new packet with the next number whose `index.json` parent is the packet it amends, so the newest id
under a phase is the packet in force.

**`validate --kind <line kind> <packet-dir> [--repo <dir>] [--json]`** runs the rules above; output as the build kind's
(`kind` is the kind and `packet` the id, null when `packet.json` can't be read). Exit 0 passed, 1 a check failed, 2 the
directory is missing or the repository can't be read. It writes nothing.

**`hash <packet-dir> --kind <line kind> [--json]`** prints the canonical hash, no check run; `files` lists the files it
covers, the linked directories' files included. **The line hash** is the build kind's canonical form over the kind's files
(`packet.json` without `status` and `hash`, a JSON file without `$schema`, any other file `sha256:<hex of its bytes>`) and the
files under the linked directories, with `"kind":"<kind>"` in the hashed document so no packet of another kind can collide
with it. Entries the kind doesn't list are not hashed (`validate` fails them). `LineDigestTests` pins one packet's hash,
computed independently of the C# code.

**`freeze --kind <line kind> <packet-dir> [--repo <dir>] [--json]`** validates, then records the hash at
`packets/<owner>/<name>/<id>/frozen/<hex>.json` from `packet.json`'s `repo` and `id` (the record's `kind` is the kind, with
its `baseSha`). A failed check freezes nothing (exit 1); the same hash again says `already frozen`; a packet whose
`packet.json` records `status: frozen` and its `hash` freezes to the same hash, since neither is hashed.

## Exit codes and errors

| Exit | When |
|---|---|
| 0 | Every implemented check passed (and, for `freeze`, the hash is recorded; for `verify`, the hash is verified) |
| 1 | A check failed, or the packet is malformed (`AW-PACKETS-003`, `-004`) |
| 2 | A usage error, or the tool couldn't do its job: no packet (`AW-PACKETS-002`), an unreadable repository (`-006`) or packet file (`-008`), an unwritable freeze log (`-007`), an unusable sizes file (`-009`) or seal file (`-010`) |

Errors go to stderr as `aw-packets: <code> <message>` (with `--json`, the `Error` record).

## Ports and adapters

| Port (`Aw.Packets.Core`) | Adapter (`Aw.Packets.App.Infrastructure`) | Fake and contract (`tests/Aw.Packets.Testing`) |
|---|---|---|
| `IPacketSource` | `FileSystemPacketSource` | `InMemoryPacketSource`, `PacketSourceContract` |
| `IScopingPacketSource` | `FileSystemScopingPacketSource` (the twelve listed files, a symbolic link refused; the other top-level names, never their content) | `InMemoryScopingPacketSource`, `ScopingPacketSourceContract` |
| `ILinePacketSource` | `FileSystemLinePacketSource` (a line kind's listed files and every file under its linked directories, at most 2,000 of them; a symbolic link, linked file or directory, is refused; the other top-level names, never their content) | `InMemoryLinePacketSource`, `LinePacketSourceContract` |
| `ISealSource` | `FileSystemSealSource` (a regular file of at most 16 KiB; `SealReader` parses it) | `InMemorySealSource`, `SealSourceContract` |
| `IRepoSnapshot` | `GitRepoSnapshot` (`git ls-tree -r -z`, 30 s limit per run) | `InMemoryRepoSnapshot`, `RepoSnapshotContract` |
| `IFreezeLog` | `FileFreezeLog` (temp file, then move) | `InMemoryFreezeLog`, `FreezeLogContract` |
| `TimeProvider` (BCL) | `TimeProvider.System` | `FakeTimeProvider` |

Every `IPacketSource` adapter reads through `PacketReader` (in `Aw.Packets.Core`), so parsing, link safety and the
errors are the same whatever supplies the bytes; `PacketSourceContract` checks them on each adapter.

## Open questions

- **Size classes (`PKT-SIZE-001`)** are answered by LN-PKT3 (above): the values live in the profile's `workflow.plan.sizes`,
  Sindri writes the sizes file, and a task over a limit is a failure. Not decided here: which lens a packet-time budget
  uses (the file has one budget per size).
- **Holdout store (`PKT-HOLD-002` checks the packet side; `PKT-HOLD-003` compares a phase's set hash with a seal file it is given):** addendum §17.19 puts held-out examples in
  sindri-private state with a canary per set. What is that store's path and format, and how does a packet name a set
  (by example id, as now)? The store side and the canary are checked once the store exists.
- **Workflow coverage (`PKT-FLOW-001`)** is not an open question: §17.4's Workflow part is the frozen output of
  workflow generation (§17.7), which the rollout puts in F3; the rule stays `unchecked` until then.
- **`packet.yaml` vs `packet.json`:** the addendum names `packet.yaml`; this build reads `packet.json` (typed,
  source-generated JSON under NativeAOT with no YAML library). Keep JSON, or add a YAML reader?
