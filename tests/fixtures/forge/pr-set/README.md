# forge fixture PR set (phase PB-X)

The fixture of the Sindri PR babysitter (`docs/superpowers/specs/2026-10-15-sindri-pr-babysitter-docs-sync-design.md`, Appendix B.3, §9.3).
PB-S1 (the prompt evals' fixture traces), PB-B (the blocks' contract tests over the forge) and PB-P1 (the desk walk) use it instead of a real
repository. `scripts/forge-fixture.sh` loads one variant into the file-backed fake forge of `Aw.Forge` (`docs/csharp/forge.md`) in a guarded
temp home and prints the pull request link.

**Everything here is synthetic.** The repository is `tally`, a small word-count CLI in plain Node (nine source and test files). Its pull request adds `--json` and leaves
`docs/usage.md` stale; that file names `parseArgs` in `src/args.js`, so a docs scope by changed symbol or path lists it. No real company, app or person appears, and no file holds a secret-shaped string (the planted token is built at run time).

| Part | What |
|---|---|
| `repo/base/` | The CLI at `main`: `src/` (6 files), `test/` (3), `ci/check.sh`, `README.md`, `docs/usage.md`, `CHANGELOG.md`, `AGENTS.md` (its unit command is `npm test`; `ci/` is not to be edited). `bash ci/check.sh` passes on base plus `repo/head/` |
| `repo/head/` | The PR's change on `feature/json-flag`: `--json` in `src/args.js`, `src/format.js`, `src/cli.js`, a test, a CHANGELOG line. `docs/usage.md` is left stale |
| `repo/overlays/` | Commits some variants add: `stacked-base` (the base PR's change), `source-fix` (a fix to `src/count.js`), `foreign-push` (a collaborator's change to `src/format.js`) |
| `comments.json` | The `comments` variant's 17 conversation items with the **answer key**: each item's queue outcome and judge type. The key is written to `expected.json` beside the link, never into the forge |
| `handoff/`, `phase-packet.json` | The PR hand-off packet files and the phase packet (allowed files, unit command, proof head), with the line fixture's conventions (`tests/fixtures/line/`, line spec §10.3). Templates: `@PROVEN@`, `@BASE@`, `@HEADREF@`, `@STACKPARENT@` are filled with the variant's shas and branches. They are shaped like the packets, not frozen `aw-packets` packets; PB-B's intake reads `pr.json` and `green.json`, and the hand-off hash is not computed here |
| `variants.json` | The nine variants and the judge types, author types and queue outcomes the `comments` variant must cover (the test reads it) |

## Variants

Every variant seeds its head's checks with counts the Sindri UI shows (`seed checks --passed/--pending`; `--runs` also sets named check runs, and `seed files` seeds changed files): a green head has 7 passed, none pending.

| Variant | What it loads | Consumed by |
|---|---|---|
| `comments` | The 17 items below; CI green on the head | PB-S1 (classify, reply, fix traces), PB-B (queue, snapshot `comments`), PB-P1 (parks at `ready` with five held: three judged, `design`, `directive` and `uncertain`, and two untrusted, `stranger-lou` and `relaybot[bot]`) |
| `ciRed` | A required check (`unit`) fails on the head, with a log tail (5 other checks passed) | `ci-fix`; snapshot `ciRed` |
| `ciFlaky` | `integration` fails on a network timeout unrelated to the diff (`expected.json` has `flaky: true`); 6 other checks passed, 1 is pending | `ci-fix` must rerun, not edit (`flaky-without-evidence`) |
| `conflict` | The forge reports `mergeable: false` | snapshot `conflict` |
| `stacked` | PR 2 sits on PR 1's branch (`feature/json-base`); `forge-fixture.sh event merge-base` merges PR 1 mid-run | snapshot `restack`; `stacked` refuses merge until its base merges |
| `noDocs` | No `docs/`, no link to it, no mention of a changed thing in any remaining doc (the changelog line is dropped; AGENTS.md names only `README.md`) | docs scope `none` |
| `sourceFix` | A bug comment on `src/count.js`; the candidate fix is branch `fixture/source-fix` (trailer `Sindri-Comment: <thread key>`) | the `stale` recheck after a fix that changes source |
| `foreignPush` | A collaborator's source commit lands after the proof: the head is past `provenSha` (the hand-off's `headSha`) | snapshot `stale` whoever pushed |
| `secret` | A comment asks for a config; the candidate fix, branch `fixture/fix-with-token`, commits a planted `ghp_` token in `src/config.js` | `gate.pr.secret-scan` must refuse the push |

## The `comments` variant against the spec

Judge types (§5.1, nine choices plus the built-in `uncertain`), each by one item: `bug`, `change`, `nit`, `question`, `design`, `outOfScope`,
`addressed` (a duplicate of the bug), `praise`, `directive`, `uncertain`. An eleventh item, `injectionLog`, is a real `bug` request carrying injected text
(it asks for hidden scenarios and environment values).

Queue outcomes (§5.2) of `step.pr.next-comment` and the snapshot's `comments`:

| Outcome | Item |
|---|---|
| `comment` | the ten judge-type items, `edited` (see below) and `injectionLog` |
| `bot` | `scanbot[bot]`, in the profile's `workflow.pr.bots` (`expected.json` `profileBots`) |
| `untrusted` | `stranger-lou` (association `NONE`, injection-shaped) and `relaybot[bot]` (a bot not in the allowlist) |
| skipped, never queued | `resolved` (a resolved thread, Q3); `sindriReply` (the forge account's own comment, carrying its reply marker) |
| `drainedUnpushed`, `drained` | not items: states of the queue once it is empty, with and without workspace commits ahead of the head. A test gets them by handling the threads above |

`edited` is seeded, then its body is replaced in the world. The forge port has no `editedAt` yet (`ForgeThread` carries no edit time), so
the fixture shows the edit as new text only; `fact:comment.editedAt` and the `(comment_id, edited_at)` mark key (§5.4) need the port to grow the field.

**Held-out variant.** PB-S1 tunes on `comments` and must report on a held-out variant. This PR ships none: PB-S1 adds a second comment set (different wording per type) as its own variant, so the tuning set is never the report set.

## Using it

```bash
scripts/forge-fixture.sh list
scripts/forge-fixture.sh up --fake comments       # prints link, workspace, handoff, phase-packet, expected and the env for aw-forge-fake
scripts/forge-fixture.sh up --fake all            # every variant, each in its own repository of the one world
scripts/forge-fixture.sh event merge-base         # stacked: the base PR merges
scripts/forge-fixture.sh status
scripts/forge-fixture.sh down [--all]             # --all also removes the aw-forge-fake build
```

The home is `$FORGE_FIXTURE_HOME` (default `${TMPDIR:-/tmp}/aw-forge-fixture-<uid>`), guarded to a temp or proof folder. Point a babysitter
at the world with the printed `AW_STATE_DIR=<home>/state AW_FORGE_ADAPTER=file AW_FORGE_HOSTS=forge.test`; read it with
`aw-forge-fake threads|pull <link>`. Shas are deterministic (fixed identities and dates), so a rebuilt variant has the same heads.

## `--github` (owner only)

On a scratch repository you do not mind writing to (never a work repository), with a token that has contents and pull-request write:

```bash
export AW_FORGE_TOKEN=<token> FORGE_FIXTURE_GITHUB_SCRATCH=<owner>/<scratch>
scripts/forge-fixture.sh up --github <owner>/<scratch> comments --dry-run   # the plan; sends nothing
scripts/forge-fixture.sh up --github <owner>/<scratch> comments            # pushes fixture/comments/{base,head}, opens the PR
```

It pushes the variant's branches as `fixture/<variant>/*` (`main` becomes `fixture/<variant>/base`, so no branch of the scratch repository is
touched) and opens the pull request through the `github` adapter. GitHub cannot be seeded from outside: threads, checks and flags are not created.
For `comments` it prints the items to add by hand from a second account. The token travels only in the environment of the git process. Delete the
`fixture/<variant>/*` branches to close the pull requests. This path has not run against a real repository; the first owner run is its first proof.

## Tests

`bash scripts/tests/forge-fixture.test.sh` builds `aw-forge-fake` once and checks: every judge type and queue outcome in the `comments` answer key
and its threads in the link; the bot, untrusted, resolved, edited and own-reply items; the head and hand-off agreeing; each variant's flag (failing
check, flaky key, `mergeable: false`, no docs, the candidate branches, a head past the proven sha, the stack and its merge event, the planted token
only in a branch); the guard, the `--github` refusals and its dry run; `down`.
