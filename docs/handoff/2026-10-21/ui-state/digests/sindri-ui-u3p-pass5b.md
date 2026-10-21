# #224 U3' pass 5b handoff

Pushed head 553d5492 on feat/sindri-ui-u3. origin/main (f5bc3ea5, with #253's final goldens) merged; add/add golden conflicts (needs-you fixture/large/session-repo, requests/invalid-kind) took main's files; two comment-only conflicts in ItemSheetViewModelTests and QueueViewModelTests took main's. No test needed a value fix after the merge.

## Commits (red then green, each pushed)
1b1ebd79 chore: format the strip tests
dbe49ec9 docs: the Workflows strip in the UI notes (T14b)
27bb782d docs: the screens spec's strip drawing follows the fixture (T14b)
21dd37aa feat: the Workflows strip on Home, its row, focus and the fold (T19)
cd3eb2f9 test: the screens spec's strip drawing matches the fixture (T14b, red)
eb2f73a7 test: meta-launch late-file test reaches the timeout path (tmux env); docs: grace window and .failed marker
ec07c673 feat: the fixture's workflows and incident verbs (T20)
8383e34b test: the fixture's workflows and incident verbs (T20, red)
2a10d660 docs: sessions also see repo-less requests and decisions
aaeb56b0 docs: the decision note is left out of the stream and a session's ledger, and served on the repo's views
19f5e506 test: drop an unneeded null-forgiving operator (IDE0370)
a926d8b7 chore: format; the query helper is UrlQuery (QueryString clashes with a property)
8f47ddfd test: the strip's key frames, names, order and fold (T19, red)
f0839097 feat: every event re-reads the Workflows strip (T18)
df8f076a test: the strip's re-reads (T18, red)
f1f2d3ba feat: the strip's keys, links and inline answer (T17)
f679f2a0 fix: meta-lane snapshot via grep -Fxv, late file kept by its lane, refused lane reported at once; drop the macOS work
dce2b3c9 test: the strip's keys, links and inline answer (T17, red)
6ba91c1e docs: RecordedText's summary gives the hide-then-scrub order; db-run's code list adds 001, 022 and a missing --config
fbd7aeaf test: rebuilding-or-stalled theories for requests and needs-you; multi-line text cases; projection-level test names end in _in_the_view
b4c55e04 refactor: one QueryString helper for the item, request, decision and context verbs
96256218 fix: indent every line of a request's text and a decision's note in the text lists
3b68dfb0 fix: a decision.recorded for a decision that is no longer pending changes nothing; why RequestsProjection sits in Items; voided-row docs
059caf7f Revert "ci: install tmux in the bash-tests job so meta-launch.test.sh runs"
b9b5e8fa fix: hide secret JSON members before the value scrub so a learned numeric secret cannot invalidate the JSON and switch hiding off; docs: the order, the letter-to-digit and count-word rules, db-run codes 009, 011, 015
0a2b232b test: the scheduler's recorded runs clear a failure row through NeedsYouProjection
46c0cbe1 test: red tests for the macOS CI failures and the run-2 launcher findings; portable wc and open stubs
d2d66dcb test: the view store contract covers every view and column ViewColumns maps; ViewSql derives its columns from it
920480c9 test: red cases for a numeric secret value breaking member hiding, exact recorded output, an escaped key, an object-valued secret and a failed pointer into a renamed repeated column
2ec82ed2 fix: item.state-changed and profile.approved invalidate the needs-you views they change
6c0eea3e docs: the needs-you profile row's primary action is ApproveProfile
5e7746de fix: a failure row's cli is sindri status until RunService exists
e528d816 fix: hide secret members of JSON with repeated keys, split secret names at letter-to-digit, count words only when adjacent, wider word list, renamed repeated columns keep hiding, a non-database SQLite file is 018, nestedComments required
74b2d99a fix: an unknown request kind on POST /requests answers SND-REQ-001
4307cff9 fix: decision.recorded carries the decision's repo and its public form drops the note
cef8f7e4 docs: lane variables are exported by meta-lane.sh (L-12, SL-1, section 10.2); start lock and .settled in meta.md
14e92311 ci: install tmux in the bash-tests job so meta-launch.test.sh runs
3287fdd3 fix: meta-lane refuses a bad prompt before starting, ids from new files only, self-serialised reported lanes
b4831d71 test: red cases for repeated JSON keys hiding secret members, digits ending a secret word, count-word adjacency, the wider word list, renamed repeated columns and a non-database SQLite file
57453899 fix: unique result keys, ISO dates and times, quoted-value tails hidden, SQLite copy verified (journal, stat retry, hot-journal rollback, quick_check), symlink-safe run-dir guard, missing source is 010, non-nesting SQLite comments, secret-name rule matches whole tokens
4a4ad485 fix: CLI request tests follow the body's member order
b676ddd9 docs: launcher SL-1 docs, env vars, deps edge Meta -> Launcher is current
eb3937c6 feat: aw-meta-launch reads launcher.json (viewers, catalog lane agents, --init-launcher)
80489f39 fix: a request body may leave out item; a session's repo must be a repo of the approved profile
cc0b4636 test: golden transcript of aw-meta-launch with no launcher.json (pre-SL-1 scripts)
dce39879 test: compiling stubs for the new seams so the red cases fail on behavior
ea59e666 test: red cases for duplicate result columns, quoted-value tails, SQLite copy locks, ISO dates and times, the widened secret-name rule, the symlinked run dir and non-nesting SQLite comments
8e5da48f chore: start launcher SL-1 (aw-meta-launch reads launcher.json)
30102d43 feat: sindri decisions verb (--pending, --decided, --item, --json) (plan gap G11)
5e0bf90a test: red tests for the decisions verb (plan gap G11)
1ac8d931 fix: schema enforces the engine enum and step target cap, DataRunFiles registered by Core, evidence.md matches the db-run behavior
ada3da01 fix: learn bytes and array cells of secret columns, hash the seed's bytes, redact quoted literals in server errors, read unmappable Postgres cells, report 42501 on a read as a failed query
56f42740 test: red cases for secret learning of bytes and arrays, seed hash bytes, Postgres error text, unmappable cells and 42501; golden, 018, 022 and shifted-ordinal coverage
f110328b fix: read a SQLite source by file copy, never through a connection (WAL sidecars, page sizes, source inside the run dir)
41665a30 docs: the request, decide and needs-you verbs and routes; API spec 3.6 takes the profile subject and RunService; smoke gains request and decide steps (P4 unit I)
c8020075 test: red cases for the SQLite copy (WAL source untouched, page sizes, source inside the run dir)
2f4fdad3 feat: sindri request, requests, decide and needs-you verbs (text and --json) (P4 unit H)
e32b39e4 test: red tests for the request, requests, decide and needs-you verbs; RoutedApi shared (P4 unit H)
b0d0c18e test: CloseItem voids pending decisions, item reads needs-decision and back; queue/fixture, items-id/needs-decision and after-decide goldens (P4 unit G)
90ebc50e chore: re-restore the architecture tests lock file after merging main
9386cd32 test: Decide and the P4 routes in the UI lane's catalog pins and golden reads; format
f33baf20 docs: db-run redaction model, seed guard, effective role privileges and the targets config read for every run
bf8e87f0 test: P4 goldens for needs-you, decisions, decisions-id and requests; fixture order decision, failure, profile (P4 units D-F)
731b3327 fix: db-run seed confined to the run copy, secret column values hidden by value, inherited write grants refused, targets config read for sqlite
f5d74a06 feat: ListRequests, ListDecisions and GetNeedsYou with session scope, ordering, cleared line and counts sentence (P4 unit F)
3a595e9e test: red tests for db-run hand-test bugs B1-B4 (seed escape, secret by value, group-inherited writes, sqlite targets config)
ae8670e2 test: red ListRequests, ListDecisions and GetNeedsYou (order, scope, cleared, cli, empty sentence) (P4 unit F)
9de07189 feat: Decide records decision.recorded by the human, 409 when decided elsewhere; SQLite race test (P4 unit E)
8c437af7 test: red Decide (options, races, voided, scope, scrubbed note) with the service shape (P4 unit E)
33c1c58d feat: FileRequest appends request.filed with item scope, caps and scrubbed text; request text stays out of public reads (P4 unit D)
e116a7d7 test: red FileRequest (cap, scope, scrub, closed item) with the service shape (P4 unit D)
ff06d94b docs: db-run redaction section follows api-run's wording (free text scrubbed, fixed fields as-is, both placeholders)
9cecb82e fix: db-run follows api-run's scrubbing (free-text-only value scrub, per-kind placeholders, scrubbed refusal and error); public API and a fixed-field test
8692b90c refactor: place the request projection and decision rules where the namespace graph stays acyclic (P4 unit C)
1edb9434 chore: merge origin/feat/dt-e1-api-run (credential forms, per-kind placeholders, free-text-only scrubbing) into db-run, ported to Core/Data
97eb9a12 chore: format the db-run files (DT-E2)
7ceea527 feat: request, decision and needs-you projections; needs-decision item state (P4 unit C)
a8a1936d chore: deps.yaml Evidence -> KernelData edge, lock files, the db-run command in AGENTS.md (DT-E2)
fc90d21d feat: db-run - the service, seeds into the run copy, the command and the generated schemas (DT-E2 S9, S10)
c2a301e3 test: red - db-run service, seeds, summary shape, schemas and command, with the stubs they need (DT-E2 S9, S10)
47903466 feat: views.db tables for requests, decisions and needs-you; schema version 3 (P4 unit B)
9e03ddac feat: Postgres reader over Npgsql with identity and role preflight, typed database targets (DT-E2 S8)
54db85f0 feat: pure rules for requests, decisions and needs-you rows (P4 unit A)
83637ec9 test: red - Postgres preflight, connection string, typed database targets and the heavy container contract (DT-E2 S8)
ac5477d4 feat: Rows JSON and the SQLite reader over a read-only copy, with the bypass guard (DT-E2 S7)
9aabcec6 test: red - the database reader port, Rows JSON, the read-only contract and the SQLite reader (DT-E2 S7)
9267ffc2 feat: DbScript reader, SQL text scanner and the pre-run write refusal (DT-E2 S6)
58047c5f test: red FileRequest, Decide, ListRequests, ListDecisions and GetNeedsYou slices; FileRequest and Decide rows ready (P4 contract)
09fbb3f2 test: red - DbScript, SqlText and the pre-run write refusal (DT-E2 S6)
b2d1cb5e docs: the db-run contract (script, summary, database guards, targets, ports) (DT-E2)
cb7baa47 chore: merge main (P5) into feat/sindri-api-p4-needs-you
3a66c01b feat: request, decision and needs-you vocabulary, events, catalog rows, errors and empty sentences
939e1f15 feat: RequestId, DecisionId and DecisionOption

## Tasks
- T16 format: clean (dotnet format).
- T17 keys/links/answer: StripSelectionTests, StripKeysTests; ShellViewModel.Strip.cs; J/K order Needs you -> strip -> Running -> Queued.
- T18 re-reads: ReadStrip via Health.Follow; StripRequeryTests (404 marks only the strip, other errors keep rows). FoldTests -> StripFoldTests.
- T19 views: Views/Workflows/StripRow (one line; detail drops under the line on narrow pages), HomePage panel, ShellWindow.Strip.cs focus; WorkflowsStripFramesTests (all 8 frames named u3-u3p-*), names test, order test.
- T20 fixture: verbs workflows [--large] and incident start|stop (+ bash tests). recipes/dev/incident-hold.yaml is already on this branch (U3'-ENG-1 met).
- T14b: Screens_spec_strip_drawing_matches_the_fixture (spec now says 8 open, ... 3 more); docs/csharp/sindri-ui.md strip paragraph.

## Park-row rule (guess, flagged)
[Answer]/A on a desk-answer row (never protected) opens the Needs-you row of kind park for the same instance: a row with chip PARKED or ESCALATED whose Subject.Id equals the instance address or id, else whose ItemId equals the instance's ItemId; none -> opens the workflow deep link. Source: #253's needs-you goldens (fixture/large/stalled/...) contain NO park rows (only decision, failure, profile), and NeedsYouKind.Park is never produced yet (P11); the plan says P11 serves desk tasks as park rows. So the rule rests on the test helper shape (subject kind item, row.Item) and the plan text. Re-check when P11 lands.

## Gate (on 553d5492)
- build tests/Aw.Sindri.Ui.Tests -warnaserror: exit 0
- Ui unit lane: 1298 total, 0 failed, 1298 succeeded
- Ui integration lane: 469 total, 0 failed, 459 succeeded, 10 skipped (pre-existing macOS-only); no error, no exit 134
- Aw.Architecture.Tests: 117/117
- dotnet format --verify-no-changes: exit 0
- check-deps ok; sync-rules --check up to date; sindri-ui-fixture.test.sh 0 failures

## Findings / existing tests changed
- Fold: at 1280x800 five Needs-you rows are ~643 px (decision rows ~120 px each); the column tops then sit at y~980. Screens 4.1's "column tops above the fold with 5 + 3 rows" does NOT hold; the strip is one line per row (190 px for 3). Tests assert counts (5,3 and 4,2 at 1.3), one-line rows, and column tops above the fold for the fixture list only. Needs lead/owner decision (compact the Needs-you rows, or relax the spec).
- Pre-existing tests now serve an empty strip (WorkflowAnswer = workflow-instances/empty) so they keep their subject: ShellItemsTests, ShellNeedsYouTests, QueueFocusTests, FocusRingTests (the last also uses a 960 px window). ResponsivePageTests passes thanks to the narrow-tier wrap.
- Text scale is never fed to NeedsYou.TextScale or Strip.TextScale in the product (only tests set them), so the 1.3 cap does not take effect in the app.

## Still open in T1-T20
- Fold gap above; text-scale wiring (live from Settings) for both lists.
- Screens_spec_states_the_strip_cap_at_scale_1_3 (T14b second test): not written, needs the owner's OK of the 1.3 cap (skips are an analyzer error).
- From pass 4: CliText backstop (type absent), T7 if still open, T14 spec text edits, u3-file-request frame (no filing form), live P4 run of fixture verbs (also the new workflows/incident verbs: recipe ids and `item add` printing an ITM id are unverified against a live control plane), WindowsDockBadge on real Windows, `fail` verb assumption.
- Real-app proof of the strip needs engine P11 (list route) and #250's re-recorded goldens.
