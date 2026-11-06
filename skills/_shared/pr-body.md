# PR Body Template (shared)

Literal PR-body template with evidence embedding rules (CD13). Referenced by shipRelease and specToProvenPR.
Referenced via: SHARED_DIR pattern (CD2).

```markdown
## Summary
<what changed + why>

Covers: <the doc items this PR builds, comma-separated (harbor-booking-tdd#Ph2, harbor-booking-tdd#REQ-E3), or the literal `none (<reason>)`>

## Key decisions
| decision | why |
|---|---|
<≥1 row, or the literal line `None.`>

## Evidence
<embedded evidence.md text: verdict line, journey table, mockup diff %, cross-check raw output>
<artifact paths; with --attach-images: relative links to docs/evidence/<branch>/*.png>

## Tests
<runner used, pass/fail counts>

## Coverage
<overall % or "not available"; list files below 80%>

## Review resolution
<findings found/fixed, number of review runs>
```

## Rules (CD13)

- **`Covers:`** is one line under Summary naming the design-doc items this PR builds, e.g. `Covers: harbor-booking-tdd#Ph2, harbor-booking-tdd#REQ-E3`: a phase, requirement, test row or section anchor of a doc written in the design-doc convention (`_shared/design-doc-front-matter.md`). Use the full ref (`doc:<root>[/<slug>]/<doc id>#<item>`) when the doc is in another repo or the short form could match two docs. Nothing after the last ref. A PR that builds no design doc says `Covers: none (<reason>)`. Never a second `Covers:` line.
- Before `gh pr create`, check the body: `sindri library validate --pr-body <body.md>` when `sindri library` exists, else print "Skipped the design-doc check: sindri library isn't installed" (`_shared/design-doc-front-matter.md`, "The check"). Fix what it names.

- The `## Evidence` section always embeds the `evidence.md` text (verdict, journey table, cross-check raw output) plus artifact paths — never just a pointer.
- Gate before `gh pr create`: `test -s evidence.md` — an empty/missing evidence file blocks PR creation for user-facing diffs.
- `--attach-images` (specToProvenPR default **on** for user-facing stages; shipRelease default **off**): copy the PNGs to `docs/evidence/<branch>/` on the PR branch, commit them, and reference with relative links.
- A user-facing diff with missing or FAIL evidence ⇒ **no ready PR** — open as draft with a warning checkbox only.
