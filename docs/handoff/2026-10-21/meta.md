# meta — handoff

main: `76df7cc` (#550, #551, #553, #555, #556 merged by the owner; #542 CI-1 and #554 by meta). #555 carried #497's branch (BS-2, BS-2b) and #556 carried #549 (CI-0b), so both are merged and out of the queue. #557 (owner) is the same PB-R work as #545.

## Merged since 2026-10-21 01:30Z

#538 (LN-WF3 heavy-slot allocator), #534 (services follow-ups), #541 (BS-12 review loop typed escalation), #540 (ui setup checklist), #548 (ui run-canvas graph controls), #550 and #551 (owner), #542 (CI-1 Aw.CodeIndex scaffold), #553 (owner), #554 (main build fix).

## Main

main `b45275e` builds again: #553 (merged by the owner outside the gate) broke `-warnaserror`; #554 (context) fixed it and its deadline golden, gated and merged.

## Where the team stopped (2026-10-21 ~15:00Z)

Every lane stopped all its builders and pushed all its work; nothing is uncommitted or only on the old box. All five lane files are in this PR. Lanes hit the shared usage limit ~04:00Z; nothing ran 04:00–14:40Z.
- Branches named `wip/...` hold unfinished or ungated work; never merge them as they are. `wip/sindri-ui-lane-state` holds ui's digests and briefs, so never merge it at all.
- engine left each PR head at its last gated sha; newer work is on `wip/handoff-2026-10-21/<branch>`.
- context's ci-3, ci-4 and s9a carry `.handoff-drafts/`, which must be deleted before their PRs open.
- #549 (CI-0b) failed aot-smoke twice in a row (run 1 on 69a7c6a, run 2 on ccddefc), both times `SND-CLI-002 'sindri serve' didn't answer within 10 s`; every other step passed. Two in a row on the same PR may be real: CI-0b adds profile keys, and serve reads the profile at start. context should run `bash src/Sindri/aot-smoke/run.sh` on #549's head and read `serve.log` before it re-readies.

## Merge queue (aw-meta order) and state

| PR | Lane | State | Next |
|----|------|-------|------|
| #549 CI-0b code-index profile keys | context | gate run 1 all green except the aot-smoke serve flake; re-gate on ccddefc was started | merge if green |
| #532 E1 recipe displayName/versions (carries ui's stage names, why text, transition labels) | engine | builder e1m merging ccddefc, resolving versions (higher +1), re-pinning | wait for `ready #532 <sha>`; ui's Workflows and run canvas need it |
| #545 PB-R babysitter/docs-sync/prove-recheck recipes | ideas | conflicts with main after #541; being rebased | re-ready; BS-11 also touches step.docs.check (second to merge re-pins) |
| #535 BS-7 scope evidence | ideas | needs a fresh ready on new main | re-ready |
| #528 AddRepo (setup slice 4) | api | 9f5ca765a pushed (ccddefc merged, conflict resolved); checks not finished | finish checks, ready; api's hook needs the owner to type `go` in its pane for pushes/readies |
| #544 needs-you setup rows (draft) | api | 375005304 pushed (test fix + ccddefc); checks not run | as above |
| #546 wf rows, #547 CI-10a reads | api | open, not ready yet | ready follows |

## Known problems

- **aot-smoke flake**: `SND-CLI-002 'sindri serve' didn't answer within 10 s` right after serve starts, when the box is loaded (#541, #540, #549). Re-gate the `aot-smoke` scope alone (`aw-meta gate-run N aot-smoke`); engine owns a fix (longer readiness wait or poll).
- **MCP log flake**: `Aw.Sindri.Tests.Mcp.SindriRequestToolTests.Neither_request_text_nor_item_text_reaches_a_log_line_even_at_trace` fails in the full integration lane, passes alone (`gate-rerun` classifies it FLAKE). api owns it.
- **Memory**: builds run outside `heavy` leave reused MSBuild worker nodes (`/nodeReuse:true`) holding GBs. `heavy` already sets `MSBUILDDISABLENODEREUSE=1`; every dotnet build/test/format must go through `heavy`. Owner OK'd killing only *idle* reused nodes by pid (0 CPU over 10 s); never `dotnet build-server shutdown`, never `pkill -f`.
- An ideas builder ran `pkill -f "never"` ~02:50Z; lanes were asked to rerun anything that died.

## Owner decisions this session

- No per-lane builder cap; builders do surface checks (build, scoped tests), meta runs the full gate. Limit builders only under 2 GB free memory.
- Builders on Sonnet or Haiku only, never Opus.
- The end-to-end proof of the line needs the owner's single approve-all and a profile re-approval (engine says when it's the last step).

## How to run the team (on any machine)

1. Install: `./setup.sh --with-meta` (Linux/macOS), then `aw-meta version`.
2. Team config: `aw-meta team-list`. On a new box, create it with `aw-meta team-init --team sindri --repo <abs repo path> --lanes api,engine,context,ui,ideas`, then copy gate scopes, merge order and prompts from `skills/meta/examples/agentic-workflow.team.json`.
3. Launch: `aw-meta-launch` (one window, a pane per lane on tmux socket `sindri`; attach with `tmux -L sindri attach -t =sindri-<lane>`).
4. In the meta pane run `/meta`: `role-set`, `lanes-refresh`, `lanes --announce` (send to every lane), `aw-meta watch --for 29m` in the background, `status`, `health`.
5. Each lane: start its session, read its file here, check out its branches into fresh worktrees, continue from "Next".
6. Merging: a lane sends `ready #N <sha>` on the first line; meta runs `merge-check N`, `meta-gate-reserve <arg from status>`, `gate-run N` (or `queue-gate` for a train), `gate-rerun` for reds the PR doesn't touch, `merge-check`, `merge-run N`, then sends the printed "main moved to <sha>" text to every lane. Gates run one at a time.
7. Context: `aw-meta lanes-context`; past ~300k a lane writes a digest and meta runs `aw-meta lane-recycle <lane> --digest <path>`. Builders survive the `/clear`.
