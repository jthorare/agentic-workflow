# Example run (generic sketch)

A change that replaces three per-workflow rows with one shared record, touching readers, writers and a few screens.

- **Status doc sections:** 1 surfaces (Sindri found 5 readers, the hand map found 7: two were scripts), 2 counts (rows, tenants, cells affected, with the query text), 3 decisions ("D4: caps follow the target. Owner: 'if I raise the target to 8 the cap goes to 7'"), 4 scope in EARS ("When the target changes, the system shall recompute the cap"), 5 TDD notes, 6 tracker (empty until approval).
- **Engineering page:** headings state the finding ("Both readers run for every tenant; a tenant flips when clean"), not the topic. One diagram per mechanism (data model, rollout states, PR order, read/write paths). PR cards: each lists its way back.
- **Product + design page:** a "decided" list in plain sentences with real numbers, then questions as cards (`Question N`, context, `Example:`, numbered options), then mockups beside the real screens they change.
- **Diagram spec:** `pr-order.mmd` as `flowchart TB` with `subgraph` per milestone; `colors.json` marks new work blue and flagged work violet.
- **Review before sharing:** open each rendered page at desktop and phone width; check the diagrams are legible at 390px; confirm every mockup has its compare image.
