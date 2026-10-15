# UI revision 8 assignments (merge orchestrator, 2026-10-14)

UI rev 8 (#210, 4a2389bd) is merged and approved, so the earlier "rev 8: not yet" answer is replaced.
Spec: docs/superpowers/specs/2026-10-13-sindri-ui-screens-design.md, §6 (edits E-20…E-31), §8 (phases), §8.1
(where each edit lands), §9 (waves and contract unit). README rows X62 (the edits) and X63 (map review).
Owner decision X63: single accept, reject, skip, rename, move and the bulk deterministic accept are `desk`
(reversible map_override + Undo); map promote and import stay `protected`.

## API lane
1. FIRST, the contract unit (it unblocks UI wave 1): API P11 extended with E-20 (instance list and view: states[] in
   path order, facts[], children[]) and E-23 (structured status fields). Its integration tests write the golden JSON
   for E-20, E-22 and E-23; the UI deserializes them. Send the golden file list to the UI lane as soon as it's shaped.
2. E-28: agents.<target>.dailyBudget in the profile, today's spend per target, SND-LAUNCH-010 (lands in P10a).
3. P12 stats: E-24, E-25 (recipe stats and the registry list's stats block).
4. P13 engine: E-30's API half (GET /engine, /engine/instances, …/history from the Temporal SDK).
5. E-29: doctor checks declare `consumers`; GET /status returns the dependency graph and impact closure
   (S1's doctor + scripts/check-deps.mjs). The API lane owns /status, so this is yours.
Order: contract unit, then your current P2/P3/P4 path, with P11/P12/P13 slotted per §9's waves.

## Engine lane
1. URGENT: E-21 (`urgency: incident | triage | work`, default work) and E-22 (`outputs`, `inputs[].kind`) in the recipe
   schema. #210 says these must land before P2 pins the hash and approval format, but P2 (#191) already merged.
   Do them now as a small follow-up PR (P2b) off main, test-first: the schema, the validator rules (the seven new
   RCP-* in WF §9.1), and the recipe hash inputs. Say whether any approved recipe hash changes.
2. E-30's engine half: P4's mirror (RecordEvent) writes temporal_event_id (WF §5.6). Add it to #192.
3. E-31's engine side: the scoping recipe (WF §12) writes a run's new surfaces as draft map proposals (R8-11).
   That's after P4/P5.
4. The two dev recipes for the UI fixture: recipes/dev/triage-three-state.yaml and recipes/dev/desk-round-trip.yaml
   (§8.2). Coordinate with the API lane's contract unit, which needs them.

## Context lane
1. X63 in S-3b: CTX §8.7, so the reversible map-review overrides are `desk`, test-first (alongside ImportContextMap's
   switch to the shared challenge before 2026-10-27).
2. E-26 (lenses: GetLenses, HTTP lenses, CLI context lenses, MCP context_lenses), E-27 (filemap, file level; symbols
   in phase 9) and E-31's context half (ProposeFromScope, map/proposals, the propose override, graph status), plus
   codes SND-CONTEXT-031/-032. All in S-4 (CTX phase 4), the same PR (owner: one PR per stage).
3. The fixture map tests/Aw.Context.Tests/Fixtures/map-tabs.json (one conflict, one orphan, one suggested link)
   for U9 (§8.2).

## UI lane
1. Rev 8 is approved. U3 becomes U3' (rev 7 U3 plus the Workflows strip, §4.1). Fill the WorkflowsStripSlot against
   E-20's root list once the API's golden JSON lands.
2. Wave 1: U3' and U8 (the Workflows list and instance frame, §4.2 and §5; U2's item sheet re-bases on the U8 frame).
   Wave 2: U5', U9 (replaces rev 7 U7), U10, U11, U12. Wave 3: U13. Waves follow §9's graph.
3. Finish #184 first (the 5 fixes, the narrow re-check, ready), then #193 and #197.
4. Carry the critique items into U8's contract unit: one Attach component, open counts from one instance table, a
   popover on every lock button. Plus the three open questions in #210's body (Home's six chips vs five counted,
   "Workflows 12 open" includes four done, ⌘K protected confirm). Bring those to me as owner questions when U3'/U8
   reach them.

Everyone: merge main 4a2389bd at your next clean point (the spec files changed). Reply with your lane name and an
ack or objection.
