# Sindri app demo tour (2026-10-19), FINAL on main f7ac779, updated for bb26d9a

Launch: `scripts/sindri-app.sh` (or bare `sindri` opens the app). Start it from a folder that stays put. Click into a text box before you type.

## Pages and what each shows (everything below is on main f7ac779)
| Page / screen | Keys | What it shows |
|---|---|---|
| Home | G H (or G N); G D jumps to its Decisions | Needs-you strip (count, desk/answer rows, the next sentence), open workflows strip, health line |
| Workflows | G W | Instances grouped by recipe; head counts; one cell per state; NOW text. J/K walk rows, Enter opens the frame |
| Instance frame | Enter on a row | Header + state, states band in served order (visited states read done), expanded state (step, next sentence, commands as CLI + Copy), facts panel, children panel, tab counts; Back/Esc |
| Facts | View on a fact | Scope map as a styled table (graph chips, counts; Show raw); other facts as one aligned tree |
| Children | frame's Children panel | Child rows by workstream with their state; a needs-you child links to its frame; "unreachable" as a warning |
| Recipes / recipe sheet | G R; "Recipe ▸" in the frame or a group head | States, inputs, outputs of the pinned version |
| Start form | N on Workflows, ⌘N / Ctrl+N from any page | Start an instance from the app: pick a recipe, fill inputs, send |
| Agents | G A | Live sessions by provider/lane, ASKING rows, filters, session sheet (Enter), ended fold (E), copy resume (Y); the tab count; tray line |
| Context | G C | Type an anchor (or two), Enter: head "map vN · K anchors", intent/product/code bands with edges, coverage strip, searched-source chips; "not found" and suggested anchors |
| Work (queue) | G Q | Items with sizes; item sheet; Create (C) and Close item |
| System / Health | G . (or G S) | Control plane, services, freshness |

Every page: ⌘K / Ctrl+K palette, ? shortcuts sheet, / filter, J/K rows, Enter/Esc, Y copy the row's CLI, ⌘, / Ctrl+, Settings.

## Demo-path recipe (desk-driven)
1. Approve `workflow.idea-to-brief` FIRST, then the scoping recipe (else RCP-CHILD-001).
2. On a build from main bb26d9a (batch #400: #394 S-LB2), the SHIPPED `workflow.scoping.yaml` approves; no variant is needed. Only the older context demo home on port 7481 still needs ~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-demo-check/raw/scoping-variant.yaml.
3. Steps are descriptor-only: complete each state at the desk (`sindri workflow complete … --outcome …`). The app follows every transition live.
4. Children appear only on instances started after P7 (#371, merged).

## Known rough edges (not UI bugs; the app draws what's served)
- "1 done today" can show with nothing finished (engine counts.done).
- Times are local (by design); the CLI prints UTC.
- Focus send on Agents is pending (CO-4a); the live preview is pending (CO-7b/c).
- The Context page doesn't open a scope yet (R8-14, U9e).
