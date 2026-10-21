# meta — handoff

main: `ccddefc` (2026-10-21 14:38Z; #550 and #551 merged by the owner).

## Merged since 2026-10-21 01:30Z

#538 (LN-WF3 heavy-slot allocator), #534 (services follow-ups), #541 (BS-12 review loop typed escalation), #540 (ui setup checklist), #548 (ui run-canvas graph controls), then #550 and #551 (owner).

## Merge queue (aw-meta order) and state

| PR | Lane | State | Next |
|----|------|-------|------|
| #549 CI-0b code-index profile keys | context | gate run 1 all green except the aot-smoke serve flake; re-gate on ccddefc was started | merge if green |
| #532 E1 recipe displayName/versions (carries ui's stage names, why text, transition labels) | engine | builder e1m merging ccddefc, resolving versions (higher +1), re-pinning | wait for `ready #532 <sha>`; ui's Workflows and run canvas need it |
| #545 PB-R babysitter/docs-sync/prove-recheck recipes | ideas | conflicts with main after #541; being rebased | re-ready; BS-11 also touches step.docs.check (second to merge re-pins) |
| #535 BS-7 scope evidence | ideas | needs a fresh ready on new main | re-ready |
| #497 BS-2/2b design and plan steps | engine | conflicted; builder merging main | re-ready |
| #528 AddRepo (setup slice 4) | api | conflict in Ui GoldenContractTests.cs; checks running | owner must type `go` in the api pane before api pushes/readies |
| #544 needs-you setup rows (draft) | api | on 93ad5f6, green | owner `go` as above |
| #542 CI-1 Aw.CodeIndex scaffold | context | review fix in progress | ready follows |
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
