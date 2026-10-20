# Sindri context "explain": the "Why this matters" summary for a map anchor

Status: **approved by the owner on 2026-10-20, every default in §7 as written** (relayed by sindri-meta). Asked by the ideas lane for the F3 context map (the owner approved the
pinned "Why this matters" card; UI spec `~/.agentic-workflow/mockups/f3/specs/context.md` §2 row 2b, §4 "Why this matters
text"). Builds on the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md` §7 trust, §8.9 lenses) and F1
(#494: the map refreshes itself).

## 0. Why

The context map shows what an anchor touches: tickets and decisions above, product in the middle, code below. A person
opening it still has to read the whole canvas to answer the one question they came with: *why does this matter, and what
could go wrong?* The card answers it in at most three plain sentences, each pointing at the nodes it is about, so hovering
a sentence lights up its nodes on the map. Today the card shows "No summary written for this map yet."

## 1. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | A new Context slice, `ExplainAnchor`, writes the summary through `IModelPort` with a new `ModelPurpose.Explain` | the map's cleanup agent runs once per map and decides links; a summary is per anchor and is prose. One purpose, one budget, one switch (as every model use, retrieval spec §3) |
| D2 | **Reads never call the model.** `LensesView` gains an optional `why`, filled only from the stored summary for (anchor, map version); absent when none is stored | the lenses read is stored-graph only and must stay fast and outage-proof (retrieval spec §8.9) |
| D3 | Writing is a **job** (`ExplainAnchor`, key `explain:<sha16>.v<N>`, where `<sha16>` is the first 16 hex of the anchor key's SHA-256: a job key allows only lowercase, digits and `._:/-`, up to 80 characters, and anchor keys can have capitals and run long) started three ways: (a) the app, when the card is shown for one anchor (not a multi-anchor read, not a draft) with no summary at the **current** map version and no recorded failure (D9); (b) `sindri context explain <anchor>`; (c) after an F1 refresh promotes a new version, one follow-up job per anchor that had a summary at the previous version (at most `carryOnRefresh`, most recently written first by `writtenAt`; an anchor missing from the new map is skipped quietly; the carry never spends the last 30% of the day's budget, which is kept for cards a person opens, and stops quietly there) | (a) gives the owner the card without a button; (c) keeps the cards people already looked at current; the cap bounds the spend |
| D4 | The input is the anchor's `LensesView` at that version, built through `MapAccess.View` with the deny cut (so deny-hidden labels never reach the model), rendered as fenced, untrusted text (labels, decision text, conflict lines, counts). Record bodies are never sent; only the stored scrubbed snippets the view already holds | the same trust rule as every model prompt (retrieval spec §7); nothing the map doesn't already hold goes to the model provider, and the prompt goes through `Aw.Redaction` before it is sent. The prompt says the fenced text is data, the call has no tools, and the answer is JSON: `{"sentences":[{"text","nodes"}]}` |
| D5 | The output is validated before it is stored: 1 to 3 sentences, each 20 to 280 characters, each citing at least one node key **present in that view** (the keys are the anchor, every band item, conflict nodes and decision nodes the view holds; the keys are in the prompt); unknown cites are dropped and a sentence left with none is dropped; a sentence with a URL, markup, a control or Unicode format character (category Cf) or a fence marker is dropped; zero sentences left is a failed job (`SND-CONTEXT-180`) | the UI highlights the nodes on hover; a cite to a node not on the canvas would point nowhere, an uncited sentence is an unsupported claim, and ticket text could carry a link a person might click |
| D6 | Stored text is marked `trust: "untrusted"` (the only values are `trusted` and `untrusted`; model text is untrusted by the retrieval spec) plus `writtenBy: "model"`, and is rendered as plain text; the card's sub-line says "Written by AI from this map · {date}" (the date is local, from `writtenAt`; the F3 UI spec's `WhySub` string is amended to match) | the reader must know a model wrote it; plain text keeps a hostile label from becoming UI |
| D7 | Storage: one row per (anchor, map version) in `context.db`, with `REFERENCES map_version (id) ON DELETE CASCADE` (foreign keys on, as the graph tables do) so retention (20 versions) drops them with no extra code; the row also holds the node-key set the summary was written from (a hash and a count), and the text is redacted first and then validated, so redaction cannot leave a too-short sentence behind; the table comes in a named migration (`NNNN_explain_why.sql`) with a test that an old `context.db` opens and gets it | the pinned read (as of an approval) shows the summary written for that version, or none |
| D8 | Who may start it: a human or a service (the app's call is the human's); a session caller is refused (`SND-CONTEXT-020`), as for build and review | it spends the owner's model budget |
| D9 | A job that fails with 180 (no valid sentence) stores a failure marker for (anchor, version): the app never auto-starts again for that pair until the map version changes, because a retry costs money for the same result; the CLI and the card's "Try again" start it anyway. 181 (budget) stores a marker that expires at the next UTC day; model unavailable stores a marker for 10 minutes, and 182 (off) stores none because the app starts nothing while off. The rule is a pure Core decision (like the sync-gap one) | otherwise every open of a card retries and spends again |
| D10 | `why` is all or nothing per reader: a reader gets it only when the node-key hash of its own visible view equals the one stored with the summary (a sentence's text can paraphrase a node it doesn't cite, so dropping single sentences is not enough). A session with a narrower scope therefore usually gets none, and `--dry-run` is for humans and services only | the summary is written from the full view but read by scoped sessions too (`MapAccess`) |

## 2. Shape

`LensesView` (schema 1, single anchor) gains an optional `whyStatus` `{state: none|writing|failed|budget|off|noModel, until?, code?}`, read from stored state only (the app needs it to pick card text and to decide on auto-start; D2 still holds), and:

```json
"why": { "mapVersion": "v13", "writtenAt": "2026-10-20T09:12:40Z", "trust": "untrusted", "writtenBy": "model",
         "sentences": [ { "text": "Tab manager restores panes per window, which ENG-482 and the decision of 3 Oct both depend on.",
                          "nodes": ["feature:tabs.tab-manager", "record:linear:issue/ENG-482"] } ] }
```

Left out when no summary is stored (`JsonIgnore` when null, like `mapCommit`). The multi-anchor read (schema 2), a range read (`LensRange`) and a draft read (`draft=true`) never have `why` or `whyStatus` (the card says "Pick one starting point." for a multi-anchor read and "Summaries are written for approved maps." for a draft, and starts nothing); a pinned read of a retained version has the summary written for that version, or none; for "From the earlier map" the app makes a second, pinned read of the previous version. It is `nodes`, not `cites`, because `LensesView` already has a `Cites` dictionary of record citations. The F3 UI spec (§4) names the shape `why: [{text, cites}]`; X3 amends it to `why.sentences[{text, nodes}]`, and that amendment is a docs-only first commit of X1, so X2's goldens and X3 build on it (the amended lines: row 2b, §4 "Why this matters text" and `WhySub`).

`ExplainAnchorResult(Anchor, MapVersion, Sentences, Dropped, Tokens)`; summary `"why v13: 3 sentences"`. Ledger:
`context.explain.written` (public) with anchor, version, sentence count and the dropped counts by reason, never the text, and a `context.explain.failed` event (code, anchor, version, counts only) for each failure and each carry skip; `doctor` shows the active markers; the lenses read refreshes through the job events, as it does for every context job. A failed job's error carries counts only, and no answer text goes to a log or telemetry.

Routes and verbs (api lane's surfaces, context lane's handlers): `POST /sindri/context/explain` `{anchor}` → 202 job (an anchor not in the current map is refused before a job is queued);
`sindri context explain <anchor> [--detach] [--dry-run] [--json]` (`--dry-run` prints the fenced prompt and its token estimate and calls no model, ignores `--detach`, and with `--json` prints the prompt and estimate as JSON; in this print-only step it also shows the raw answer and each sentence's verdict, which a real job never keeps; `--json` is `ExplainAnchorResult`; exit 1 on any failure, printing the dropped counts by reason; the 182 message names `context.explain.enabled`) (anchor key as `context lenses` takes it; follows the job like `map build` unless `--detach`; Ctrl-C stops following, not the job; exit 0 written, 1 failed, 2 usage, 130 interrupted); a catalog Job row `ExplainAnchor`, label "Write summary". A 409
when the same key is running is quiet for the app. New code per the job pattern: an `EventTypes` constant and payload record, `SindriJsonContext` entries, the event catalog golden, the `ModelPurpose` JSON name, and the `PublicAPI.Unshipped` lines.

## 3. Profile

`context.explain`: `{ enabled: true, maxTokensPerDay: 50000, carryOnRefresh: 10 }` (X2 adds the keys to the profile doc and its check; out-of-range values are `SND-CONTEXT-001`). `enabled: false` turns the job off
(`SND-CONTEXT-182`; 001 means a bad profile value, and the card keeps its fallback). The day is UTC; spend is a counter in `context.db`, reserved from the estimate before the call and settled after, one writer (a reservation left by a crash settles at its estimate on restart), reusing the model port's own budget if it can carry a day window, the counter is new in `context.db` (the model port has only a per-call time budget) and settles from the tokens `ModelAnswer` reports, which the port estimates, so the cap is approximate; a port-level budget error is a model failure, never 181. `doctor` shows the day's remaining budget. One summary costs about 3k input and 300 output tokens, so the default
day is about 15 summaries; over it, the job fails with `SND-CONTEXT-181` and the card says "Summary budget for today is
used up." No model on the box (`ModelAnswer` unavailable) fails the job with the model port's code, whose message talks about labeling, so the card uses its own text for it.

## 4. Errors (this spec owns 180–189; the retrieval spec's code-range row books 170–179 for S-TR, and a scoping test already uses 170, so X1 adds this range to that row (183–189 reserved))

| Code | When | Card says |
|---|---|---|
| `SND-CONTEXT-180` | the answer had no valid sentence after validation | "No summary could be written for this map." |
| `SND-CONTEXT-181` | over `maxTokensPerDay` (the CLI message names `context.explain.maxTokensPerDay`) | "Summary budget for today is used up." |
| `SND-CONTEXT-182` | `context.explain.enabled` is false | "No summary written for this map yet." (no auto-start) |
| existing 020 / model unavailable | a session / no agent CLI | "No summary written for this map yet." for a session (no auto-start); "No AI is available to write a summary." with "Check again"; any other failure (a timeout, a crash): "Could not write a summary." with "Try again" |

Other card states: while the job runs, "Writing summary…" with the card height reserved (the card follows job events, and after 60 seconds without one shows "Taking longer than expected." with "Try again"); after a refresh, until the new job lands, the earlier version's sentences show dimmed under "From the earlier map · {date}"; "Try again" shows on 180 and on a stuck job, never on 181 (the text names the reset time in local time) or 182; a card that is collapsed starts nothing until it is opened; a polite live region announces loading to text or to an error and focus moves to the card header on replace only when it was already inside the card; the highlight has 3:1 contrast in both themes; a pinned (older) version with no summary says "No summary was written for this version." and never auto-starts. Each sentence row is focusable; hover or focus lights its `nodes`, and a row whose nodes are all off the drawn canvas shows no highlight and a muted "not drawn in this view" note. The card has a maximum height with inner scroll and reserves the height of three rows while loading, checked in the 900 px frame test; the dimmed earlier text keeps 4.5:1 contrast (a muted token, not opacity), the group's accessible name is "From the earlier map", and hover on it never lights nodes missing from the current view. The new strings are Labels keys for X3's `LabelsTests` ("Writing summary...", "Taking longer than expected.", "Try again", "Check again", "From the earlier map", "not drawn in this view", "cites N nodes", "Pick one starting point.", "Summaries are written for approved maps.", the 180, 181 and catch-all texts), with the UI spec's "..." style. The interaction is sentence-level (one `nodes` list per sentence), so X3 also amends the UI spec's row 2b, which styled cites as runs inside a sentence. A screen reader hears "cites N nodes: labels".

## 5. Testing

- Unit (Context): the validator (sentence count, length, cites present in the view, uncited dropped, zero → 180, a URL or markup dropped, an injected label never reaches a stored sentence), the
  prompt builder (fenced, untrusted, no record bodies, golden prompt for a fixture view), the budget, the store contract
  (fake and SQLite: write, read by (anchor, version), retention drop), the daily counter (reserve, settle, UTC rollover), the failure-marker decision (180 sticks to the version, 181 expires at the UTC day, model unavailable and off leave none), an unsettled reservation, a start after a failure on the same job key, and a session caller (D10) who gets no `why` and starts no job.
- Unit (Sindri): job start rules (human ok, session 020, running → 409 quiet, unknown anchor refused), the carry after refresh (anchors with a
  summary at v-1, capped, most recently written first, stops at the budget), catalog pin +1, goldens (`LensesView` with and without `why`, the job row, the
  event).
- Integration: lenses read returns `why` after the job ran on a fake model; a pruned version returns none.
- Hand test (scratch home): build a map, open an anchor's lenses, run `context explain`, read `why`, cross-check every
  cite against the view's node keys; refresh the map (F1) and see the summary carried to the new version.

## 6. Phases and parallel plan

| Phase | Unit | Depends on | Observable |
|---|---|---|---|
| X1 | Context: `ModelPurpose.Explain`, settings, `WhyStore` (fake + SQLite + contract), validator, prompt builder, `ExplainAnchor` slice, facade `IContextMap.Explain`, `LensesView.why` read | none | Context goldens with the fake model |
| X2 | Sindri: the job, route, CLI verb, catalog row, label, event, carry after F1's refresh, goldens; Ui JSON context for `why` | X1 (starts on X1's red contracts) and F1 (#494) merged | **In the app:** `sindri context explain <anchor>` then `context lenses <anchor> --json` shows `why` with valid cites |
| X3 (ui lane) | The card renders `why`, hover-to-cite, starts the job when absent | X2's goldens | the card shows the sentences; hovering one lights its nodes |

X1 goes in two steps. First the prompt builder, the validator and a print-only `context explain` (no store, no job); the owner reads about five real anchors with a real model and says whether they pass (at least 3 of 5 tell something the map does not already show). Only then the store, the budget, the job and the rest of X1, and X2 and X3. The input is only the stored view, so the summary could just restate the map. X2 also covers the docs: the Commands and route tables, the card-state table (§4), the 180–182 range, the catalog, the profile doc, and running X1 and X2 against the fake model in a scratch home.

Two builders: X1 Core/Store and X1 slice in parallel on the red contracts, then X2 while the ui lane builds X3 against
X2's goldens.

## 7. Open questions for the owner (plain words; defaults are what we do if you say nothing)

**Owner answers (2026-10-20): all defaults as written.** The card writes its summary by itself on first open; up to 10 summaries carry over after a refresh while under 70% of the ~15-a-day budget; the owner reads 5 real summaries first and at least 3 must be useful, else one prompt rework and a second read, then stop; agent sessions never read summaries; carry-over and failure memory stay in version 1.

- **Q1. Write it automatically when the card is shown?** (A reviewer asked for the button only, since opening a card spends part of the day's allowance.) *Default:* yes, the first time you open a map for something
  that has no summary, Sindri writes one in the background (a few seconds) and the card fills in. The alternative is a
  "Write summary" button.
- **Q2. Daily cap.** *Default:* about 15 summaries a day (50,000 tokens). Raise or lower it in the profile.
- **Q3. Keep summaries current after the map refreshes?** *Default:* yes, for up to 10 of the summaries written most
  recently, each time the map rebuilds itself, as far as the day's cap allows (10 summaries is about what 70% of the default day buys; a second refresh the same day carries almost nothing).
- **Q4. Read real summaries before anything else is built?** *Default:* yes: first only a print-only command; you read five real summaries and say whether they help (at least 3 of 5 should tell you something the map does not) before we build the store, the job and the app side. The alternative is to build everything at once.
- **Q5. Should an agent session ever read the summary?** *Default:* in practice no: a session sees it only when its view is exactly the view the summary was written from, which a narrower scope rarely is.
- **Q6. A smaller first version?** One reviewer asked for just the job, the store, the card and one command, leaving the automatic carry after a refresh, the failure memory and the "earlier map" text for a later spec. *Default:* keep them here, since they stop the card from re-spending money; say "smaller" and they move out.
- **Q7. What if the five real summaries are weak?** *Default:* fewer than 3 of 5 telling you something new ends this work, or allows one rework of the prompt and a second reading of 5 other anchors; you pick the five anchors before reading. Nothing past the print-only command is built until you say so.
