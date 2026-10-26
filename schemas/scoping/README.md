# schemas/scoping

JSON Schemas for the scoping station's facts (scoping spec `docs/superpowers/specs/2026-10-15-sindri-scoping-station-design.md`). The spec is the source of truth: each schema's `description` quotes the spec text it follows, and `examples/manifest.json` gives every valid example a `source`. `node scripts/check-scoping-schemas.mjs` checks the schemas, the examples, the eval payloads and `seeds.json`.

## Fact or packet file: one schema, optional envelope

`scope-map`, `acceptance`, `challenge` and `coverage` are both a fact (spec 6.1, 7) and a packet file (spec 8.3, written to `scope-packet/` by `step.scope.evidence`). aw-packets requires every packet JSON file to carry `schemaVersion` (`docs/csharp/packets.md`). Each of these is **one schema** whose envelope fields are optional: the fact omits them, the file adds them.

| Schema | Fact | Packet file adds |
|---|---|---|
| `scope-map.json` | `{ surfaces, implications, workstreams, flagged, excluded, unmapped, unresolved }` | `schemaVersion`, `change`, `baseSha` |
| `acceptance.json` | `{ items }` | `schemaVersion`, `parent` |
| `challenge.json` | `{ rounds }` | `schemaVersion` |
| `coverage.json` | `{ lenses }` | `schemaVersion` |

`questions.json`, `open-questions.json`, `brief.json`, `scope-input.json` and `challenge-round.json` are facts only (the packet's `questions.json` is `{ schemaVersion, questions: <questions.json items> }`, every question asked over the rounds; open questions are not carried in it yet, as no spec text names a key for them).

## Graph references (issue 1005)

A scope-map surface may carry `nodes` (ids of `fact:graph` nodes it cites) and `origin` (`graph` or `search`). `fact:graph` itself has no schema file yet: its shape is `docs/csharp/sindri-scoping.md` "The graph fact", checked by SCP-GRAPH-001 in Sindri and PKT-SCP-006 in aw-packets.

## Field names, and where the spec is silent

- **Question** (`questions.json`): spec 7.3, field for field: `id`, `surfaces`, `dimension`, `impact`, `blocks`, `trust`, `question`, `options[]` (`id`, `answer`, `rationale`, `cites`, `recommended`, `yields`), `other`, `skip`. The 7.3 example is `examples/questions.spec-7-3.valid.json`. Ids: `Q<n>` (or the reserved `Q-revise`), option ids `Q3.a`.
- **Ids:** acceptance items are `AC-<n>` (7.3: `"blocks": ["AC-4"]`); `A<n>` is the answer-log id; surfaces are `S<n>` (7.3: `"surfaces": ["S2", "S5"]`).
- **Citations** are `L-<lens>-<n>`, `R<n>` or `A<n>` (SCP-CITE-001: "a lens item in fact:lenses, a ref in fact:brief.refs, or an answer in fact:answerLog"). A surface id is not a citation. Acceptance and scope-map use `citations` (8.3's field list); question options use `cites` (7.3).
- **Acceptance `form`** is `ears` or `plain` with a `reason` (SCP-ACC-001: "form: ears, or form: plain with a reason"); EARS sub-types are not a field.
- **Challenge round** is the A.4 shape: `walked`, `verdict` (`nothingNew` or `found`), `surfaces`, `behaviours`. A.4: "`walked[]` (lenses and dimensions) and the verdict"; A.4 "Done when": "`walked[]` says so"; B.2: "`walked[]` complete"; SCP-CHAL-001: "lists every lens and dimension it walked". `walked` is therefore an array. The spec does not give the element shape; each entry is `{ kind: lens | dimension, name, surface? }`, a flat list of the lenses and dimensions walked. 8.3 says `challenge.json` holds those rounds ("findings, lenses and dimensions walked").
- **Silent in the spec, chosen here:** workstream dependencies are `deps` (8.3 says only "workstreams with dependencies"; `docs/csharp/packets.md` `TaskPlan` also uses `deps[]`); a flagged or excluded entry is `{ item, reason }` (SCP-CITE-002: "lists it under flagged with its reason"; SCP-COV-003: "listed under excluded with a one-line reason"; findings use `item`); `unmapped` entries are `{ path, reason }`; a surface may carry its map node `key`; the `no-behaviour-change` marker (SCP-ACC-002) has no field yet.
- **`trust`** (SCP-TRUST-001: "models never write it (the output schemas omit it, so a model-written trust is a schema finding at the draft stage)"): `scope-map.json` and `acceptance.json` each hold a `fact` and a `file` definition; a document with `schemaVersion` is checked as the packet file, which carries code-stamped `trust`, and one without it as the fact, which forbids `trust`. The one exception is `questions.json`: spec 7.3's own example carries `trust` on a question, and the example must pass, so it stays optional there.

`seeds.json` is data, not a schema: the empty value of each of the eight facts intake seeds (6.1). Its `fact:acceptance` is `{ "items": [] }`, the fact form above.

## Notes

- `scope-map`'s `change` and `baseSha` envelope fields come from the line gold fixture and the `docs/csharp/packets.md` conventions, not from spec 8.3.
- "`recommended` is set on at most one option" (spec 7.3) is in the schema (`contains` with `maxContains: 1`), so `check-scoping-schemas.mjs` needs no extra code for it; `questions.two-recommended.invalid.json` pins it.
- Citation, `L-<lens>-<n>` and `S<n>` / `AC-<n>` ids start at 1 (`[1-9][0-9]*`), as in aw-packets' `lenses.json`.
- `coverage.json` requires four rows, one per lens (a `contains` for each lens name); `coverage.four-code-rows.invalid.json` pins it. A source is a name or `{ id, state, reason?, code? }` (the shape #309 gives the recorded limits; `not-synced`'s code is assigned by the context lane's S-H, so the eval omits it).
