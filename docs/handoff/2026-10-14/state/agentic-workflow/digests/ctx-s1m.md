# S-1M handoff (context lane, stage owner)

Goal: /specToProvenPR steps 1–9 for S-1M (spec docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md §3, §8.5, Phase 1b; API spec §8).
Worktree: /Users/thor/repos/agentic-workflow/.claude/worktrees/ctx-s1m, branch feat/ctx-s1m, base feat/ctx-s1a (8123b3db), draft PR #204.
Stage dir: ~/.agentic-workflow/jthorare-agentic-workflow/proof/ctx-s1m (plan.md, verification-plan.md, owner-check.md).

Done (commits): 204f0ddf red MCP tests; 3d22ea36 /sindri/mcp + context_query/context_sources (Host/Mcp); f88ee320 mcp register + doctor context:sources/context:mcp-scope + docs S-1M sections; eecc1b5e exact CQRS exemption + chain test.

Lead decisions: Q1–Q8 in plan.md "Lead answers"; size L ok; Q6 status additions moved to S-3M; red-first note for f88ee320 goes in evidence.md; review findings recorded in evidence.md + PR body, never PR comments; retarget to main + merge main (no rebase) when #194 merges.

Left: step 4 hand test (publish via src/Sindri/aot-smoke/run.sh; expected.txt doctor lines need updating for the 2 new rows; home $STAGE_DIR/home-s1m; fake-claude stub; fakehome HOME; tripwire hashes), manual-test.md, evidence.md (hand-written, skip ui-evidence), check the docs' example citation key format, /review loop (≤5), merge gate (AGENTS.md; npm ci --prefix judge then scorer), denylist-check, gh pr ready, report "ready #204".

Rules: AW_HEAVY_SLOTS=3 heavy wrapper; df -h / before builds, pause <5 GB; no git clean, no rm -rf; never real ~/.claude*, ~/.codex, ~/.cursor, real AW_SINDRI_HOME, setup.sh.
