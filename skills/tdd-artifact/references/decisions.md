# Design decisions cited in this skill

Code comments and docs here cite design decisions by number (`D7`, `D26`). This is what each one decided. They were
recorded one at a time while the skill was built, in the owner's words; this file keeps the outcome only. A number
that is not listed was about how the work was organised, not about the skill.

| # | Decision | Where it shows |
|---|---|---|
| D1 | A new skill beside `system-design`, which stays exactly as it is; this skill calls its scripts and never edits them. | `vendor/system-design/`, `cli/src/sd/`, `cli/UPSTREAM.json` |
| D2 | The page background is white, not off-white. | `templates/tdd-system/tokens.json` (`--bg`) |
| D3 | The look is a calm document page with more colour and richer components than a notes tool allows. | `templates/tdd-system/` |
| D4 | Diagrams keep system-design's organisation, language and layout; their boxes become hover hotspots that can hold a note, a link or an image. | `references/diagram-conventions.md`, `app/src/components/Diagram.tsx` |
| D5 | Every reference token and abbreviation links to its definition, with one shared hover card and a panel on click. | `scripts/build_tdd.py` (`link_refs`), `app/src/components/Overlays.tsx`, the `REFS` lint rule |
| D6 | The template is a React app with reusable components, and the build writes files sized for agents. | `app/`, `scripts/write_agents.py`, `references/agents-lane.md` |
| D7 | A schema lane: per table its columns and how they change, sample rows, measured and expected load, in a drawer opened from the schema diagram. | `references/schema-lane.md`, `app/src/components/SchemaDrawer.tsx` |
| D8 | TypeScript 7, and a stripped, plain version of the page for agents. | `app/tsconfig.json`, `scripts/strip_page.py` |
| D10 | An eval lane that scores each revision and can push to Braintrust. | `scripts/eval_packet.py`, `references/eval-loop.md` |
| D12 | The eval also measures implementation, QA, bugs and token use. | `scripts/eval/`, outputs 3 to 6 in `references/eval-loop.md` |
| D14 | The table drawer's data preview looks like a warehouse query UI, with a chat docked beside it; an ops loop answers comments on a published page. | `references/data-view.md`, `references/ops-loop.md` |
| D15 | The ops loop extends the artifact's own "ask Claude" path with more tools. | `references/ops-loop.md`, `templates/ops/` |
| D17 | One system database for all TDDs (not one per TDD), for analysis across packets. | `cli/sql/schema.sql`, `cli/src/db/` |
| D18 | The eval runs as part of the work: every writer and every reviewer runs it and reports the scores that moved. | `app/CONTRACTS.md` (Eval duty), `references/eval-loop.md` |
| D19 | The Python scripts are ported to bun and TypeScript, with Effect 4 where a program has side effects, designed before it was built. | `cli/` |
| D23 | The system database starts as a local Postgres. | `cli/src/db/`, `DATABASE_URL` |
| D24 | A separate loader fills the database after lint; the compiler never writes to it. | `cli/bin/tdd-load-db.ts` |
| D26 | The drawer's head is one row, with an expanded view for wide tables; a table box on the schema diagram is drawn as a card with a sizing strip. | `app/src/components/{SchemaDrawer,TableNode}.tsx` |
| D27 | The port reimplements the system-design surface it needs in TypeScript, uses Effect only in the linter, the eval and the runner, and keeps the Python as the test oracle. | `cli/src/sd/`, `cli/parity/` |
| D28 | The sizing strip is one row of icons with hover text, and a table's hover card is a brief: purpose, a small measures table, trade-offs, a mini diagram. | `app/src/components/TableBrief.tsx` |
| D30 | Column statistics are one badge per column header that opens on hover or click, not a row of bars. | `app/src/components/DataGrid.tsx` |
| D33 | The cutover to the TypeScript commands is deferred: the Python scripts stay the documented path and the TypeScript runs beside them, held equal by the parity gate and the `PARITY` lint rule. | `SKILL.md`, `cli/parity/gate.ts`, `references/validators.md` |
