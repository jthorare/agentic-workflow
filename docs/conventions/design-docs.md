---
aw-doc: 1
id: design-docs-convention
kind: note
title: "The design-doc convention"
status: approved
repos: [jthorare/agentic-workflow]
---

# The design-doc convention

A design doc that follows this page can be read by Sindri: it is itemized into sections, phases, requirements, acceptance criteria, test-plan rows and tasks, and a PR can claim the items it builds with a `Covers:` line. Its review page then shows which phase a PR is, which requirements nobody built yet, and which test rows prove them.

The source is the PR review page spec, `docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md` §4. The parsed result's shape is `schemas/library/design-doc.schema.json`; `node scripts/check-library-schemas.mjs` checks that schema against its examples.

## Your first doc

Copy this into a new `.md` file, change the id, title and the rest, and keep the first line `---`:

```markdown
---
aw-doc: 1
id: harbor-booking-tdd
kind: tdd
title: "Harbor ferry booking"
status: draft
repos: [acme/harbor-web]
issues: [412]
---

# Harbor ferry booking

## Goals

- **REQ-1** A rider books a seat on a ferry.
- **REQ-E2** When payment fails, the seat is released.
  - AC-1 The seat shows free again within 10 s.

## Phases

### Ph1 Booking form

### Ph2 Payments

## Proof

| Row | Check | Verifies |
|---|---|---|
| TP-1 | Book a seat on the fake ferry; it shows booked. | REQ-1 |
| TP-2 | Fail the payment; the seat shows free within 10 s. | REQ-E2, AC-1 |
```

A PR that builds the payments phase then says, under its Summary:

```text
Covers: harbor-booking-tdd#Ph2, harbor-booking-tdd#REQ-E2
```

Nothing else is needed: Sindri reads the doc where it lives (below), and `sindri library validate <file>` checks it once that verb ships (spec Ph5).

## Which files are read

| Root | Where | What is read |
|---|---|---|
| `aw` | `~/.agentic-workflow` (the profile's `context.sources.library.stateDir`) | `*.md` with `aw-doc: 1` under `<repo-slug>/plans/**`, `<repo-slug>/proof/*/` and `<repo-slug>/system-design/*/`. Tool state (`bin*/`, `cache/`, `skill-evals/`, `digests/`, `meta/`, logs) is never opened |
| a vault's name | each `context.sources.library.vaults[].path` | every `*.md` with `aw-doc: 1`, minus `.obsidian/`, `.trash/`, any dot folder and the vault's `deny` globs |
| `repo:<name>` | each profile repo | its doc globs (`docs/**/*.md` covers `docs/superpowers/specs/`); the ones with front matter are itemized |

- **The front matter decides.** Only a file whose first 4 KiB start with front matter holding `aw-doc: 1` is read. A file without it is skipped before any of its text is stored. Older docs without front matter are ignored on purpose.
- **Near misses are counted, not read.** `aw-doc: "1"`, `aw-doc: 1.0`, `aw-doc: yes`, or `aw-doc: 1` after a blank first line: the line as written decides, so `1.0` is a near miss even though YAML reads it as 1. The file is skipped, and its path shows on the `library` doctor row and in `sindri library list --near-misses` (`SND-LIB-019`).
- Symlinks are not followed. A front-matter file over 1 MiB is stored as "too large", with no items.

## Front matter

| Key | Required | Value |
|---|---|---|
| `aw-doc` | yes | the number `1` (the convention's version; it opts the file in) |
| `id` | yes | lowercase letters, digits and `-`, 3 to 64 characters. A skill that makes it from a title cuts it at 64 on a word boundary |
| `kind` | yes | `tdd`, `spec`, `plan`, `stages`, `product`, `engineering`, `design`, `tasks`, `verification` or `note` |
| `title` | yes | any text; quote it when it holds a `:` |
| `status` | yes | `draft`, `review`, `approved`, `building`, `done` or `superseded` |
| `project` | no | a Sindri project id (`prj_…`) or `linear:project/<id>` |
| `repos` | no | `owner/name` list. With exactly one repo, it decides which agent sessions may read the doc |
| `issues` | no | GitHub issue numbers as plain numbers (`412`, not `"412"`, so Obsidian's Properties editor keeps them), or Linear keys (`ENG-12`) |
| `supersedes` | no | the doc this one replaces: a doc id in the same scope, or a full ref for another scope |
| `parent` | no | the doc this one belongs to, by the same rule (a stage map names its repo spec as `doc:repo:<name>/<id>`) |
| `generated` | only on generated files | the tool that wrote the file, always with `source-sha256` |
| `source-sha256` | with `generated` | the sha256 of the file it was generated from; hand-editing the generated file is `SND-LIB-017` |

Keys not in this table (an Obsidian note's `tags`, say) are kept and ignored.

**Doc ids are unique per scope.** In the `aw` root the scope is the root plus the repo-slug (`doc:aw/<slug>/<id>`); in a vault or a repo it is the root. When two docs in one scope share an id, the one at the lowest path keeps it and the others show `SND-LIB-012`.

## Items

Items are read only from:

1. headings `##` to `####`;
2. list items (`-`, `*`, `1.`);
3. a table row's **first cell**.

A bold lead-in (`**REQ-3**`) works inside those, never on a bare paragraph. Nothing inside a ```` ``` ```` or `~~~` fence, or a `>` quote or callout, is read, so examples in fences are safe. An item's text is its line plus the lines under it, up to the next item or heading of the same or higher level.

| Item | Id | Defined in kinds |
|---|---|---|
| Section | a heading with no id; its id is its anchor, by GitHub's slug rule (lowercase, spaces to `-`, punctuation dropped, `-1`, `-2` on repeats) | all |
| Phase | `Ph<n>`, with an optional lowercase letter or `-<n>`: `Ph0`, `Ph3b`, `Ph2-1`. No leading zeros: `Ph01` is read as `Ph1` | `spec`, `tdd`, `stages` |
| Requirement | `REQ-<n>`, or `REQ-` with `U`, `E`, `S`, `O` or `W` and a number (`REQ-E3`) | `spec`, `tdd`, `product` |
| Acceptance criterion | `AC-<n>`. Its requirement is the one a table row pairs it with; with no such row, the nearest requirement above it in the same section | `spec`, `tdd`, `product` |
| Test-plan row | `TP-<n>`. The row's other cells that hold ids become "verifies" links | `spec`, `tdd`, `verification` |
| Task | `TASK-<n>`. List items `- reqs: REQ-U1` and `- depends: TASK-1, TASK-2` under its heading become "relates" and "blocks" links | `tasks` |

- **The first definition in a doc wins.** A later list item or table row whose first cell repeats an id is a reference, and the ids in that row's other cells become links (officeHours' Traceability row `| REQ-U1 | AC-1 | … |` ties `REQ-U1` to `AC-1`). Two headings, or two list items, defining one id is `SND-LIB-013`.
- **An id defined in the wrong kind of doc is a reference.** `REQ-U1` in an `engineering` or `tasks` doc points at the requirement; it doesn't define a second one.
- officeHours' `TR-<n>` and `ADR-<n>` stay plain text: nothing claims or verifies them.

## Refs and `Covers:`

- **The full ref** is `doc:<root>[/<slug>]/<doc id>#<item id>`, e.g. `doc:aw/acme-harbor/harbor-booking-tdd#Ph2`. Inside the same scope, `<doc id>#<item id>` is enough. The item may be a section's anchor (`harbor-booking-tdd#data-model`).
- **A PR claims items** with one line under its Summary: `Covers: <ref>, <ref>…` (the key in any case, refs may sit in backticks, nothing after the last ref), or `Covers: none (<reason>)` for a PR that builds no design doc. A second `Covers:` line is `SND-LIB-021`.
- **Where a short ref looks**: the PR repo's own docs first, then `aw` and vault docs whose `repos:` names the PR's repo. Two matches at the same step is `SND-LIB-022`: write the full ref.
- A ref that resolves nowhere is a broken claim (`SND-LIB-021`). A doc the PR itself adds or edits is checked at the PR's head and shows "in this PR".

## Errors

Each error names the file, the line and the fix. A doc with an error is stored with it and without items, so it shows as broken instead of vanishing. A broken ref (`015`), a shared id (`012`) and an unknown `parent` or `supersedes` (`016`) are decided against the rest of the scope when read, and keep the doc's items.

| Code | Error | Fix |
|---|---|---|
| `SND-LIB-010` | a missing or bad front-matter key (id shape, kind, status, an `issues:` entry), or `aw-doc: 2` ("convention version 2 needs a newer Sindri") | write the key as the table above says |
| `SND-LIB-011` | an item id of a bad shape (`REQ-` with no number) | give it a number; `Ph01` is fine and reads as `Ph1` |
| `SND-LIB-012` | another doc in the scope has this doc id | rename one of them |
| `SND-LIB-013` | two headings, or two list items, define one item id | rename one, or turn the second into a reference |
| `SND-LIB-014` | an `AC` no table row pairs and no requirement precedes in its section | put it under its requirement, or add a Traceability row |
| `SND-LIB-015` | a ref to an item the scope doesn't have | fix the ref, or add the item |
| `SND-LIB-016` | an unknown `parent` or `supersedes` | use an id in the scope, or the full ref |
| `SND-LIB-017` | a hand-edited generated file (its `source-sha256` no longer matches its source) | edit the source and regenerate |
| `SND-LIB-018` | "too large" (over 1 MiB or 2,000 items), or "blocked" (the scrubber hit) | split the doc, or remove the secret |
| `SND-LIB-019` | a near miss: `aw-doc:` present but not the number 1 | write `aw-doc: 1` unquoted in the front matter, and make `---` the file's first line |
| `SND-LIB-021` | a broken `Covers:` claim, or a second `Covers:` line | fix the ref; keep one line |
| `SND-LIB-022` | a short `Covers:` ref matches two docs | write the full ref |

## What the skills write

officeHours, system-design and specToProvenPR write this convention (spec Ph4a): officeHours' `plan.md` and domain docs, system-design's `doc.md` (generated from `packet.json`), specToProvenPR's `stages.md` and verification plan, and the `Covers:` line in PR bodies (`skills/_shared/pr-body.md`). Each runs `sindri library validate` on what it wrote, and prints "Skipped the design-doc check: sindri library isn't installed" when that verb isn't there yet.
