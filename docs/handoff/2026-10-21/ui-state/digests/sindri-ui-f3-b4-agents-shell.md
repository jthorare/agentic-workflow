# B4 agents-shell digest (sindri-ui F3/F4)
Worktree: /home/thor/repos/aw-wt/sindri-ui-f3-b4-agents-shell, branch feat/sindri-ui-f3-b4-agents-shell.

## Inventory (keep / rename / move into Details)
| Where | Was | Now | Why |
|---|---|---|---|
| Nav | Recipes | Workflow types | recipe is internal |
| Nav | Home, Workflows, Work, Context, Agents, System | kept | already plain |
| Unserved views | "not served yet" / "not in the app yet" | "coming soon" | no spec talk |
| Views banner announce | Rebuilding views / Views are up to date | Refreshing the lists / Lists are up to date | projector/views jargon |
| Shortcuts sheet | resolved target, instance, region, CLI | linked page, run, area, command; G-chord line lists page names | plain |
| Agents chip | RUNNING / ASKING | WORKING / ASKING YOU | who is working, who is asking you |
| Agents head | "N live · M asking · K outside a workflow" | "N agents · M asking you" | one primary thing |
| Agents group | Outside a workflow | Not part of a workflow | |
| Agents filter | All states | Any status | |
| Agents row | "launch LCH-..." / "meta · team/lane" / "on ITM-../step/1 · build" | "Agent session" / "Lane team/lane" / "On a workflow step · build" | no raw ids up front |
| Agents row | Focus button + "arrives in U11a-f · cli" + Copy on every row | removed from the row (still on the sheet) | noise; one primary thing |
| Session sheet | fields, host, history, Focus all open | closed Details disclosure; leads with heading, state, Open the run | |
| Sheet labels | Launch, Instance, State since, State source, State history, Open the Step | Agent session, Run, In this status since, How this was detected, History, Open the run | |
| Focus | "Focus, arrives in CO-4a" | "Watch the agent, coming soon" | phase codes out |
| Context | Anchor / hint / head "map v3@x · 2 anchors" | Look up / "Type what to look up, such as feature:name" / "2 topics" (map version inside disclosure) | |
| Context bands | Intent, Product, Edges, Coverage, Searched | Why, What it covers, Links, How well it is backed, What was searched | |
| Context | Links/coverage/searched open | closed "More about this answer" disclosure | answer first |
| Left alone | Health/Home constants in Labels.cs (CONTROL PLANE, Epoch · lease, pid · epoch, ...), Arrives() (Labels.Actions, B3), Context node/review/sources panels, palette entry names (API-served) | | other builders' pages or served text |

## Follow-up
AgentRunFixtures.Status() now reads the golden workflow-status/agent-run.json. GoldenContractTests: removed from _notRead the routes already read (needs-you, decisions, decisions-id, requests, workflow-recipes, workflow-instances-start, items-id-open, launches-id); the last two added to _read.

## State
Edits done in working tree, first build running. See PR for gate numbers.

## Resumed state (2026-10-20)
Analyzer fixes + stale test words fixed; head 0ae874008 pushed; PR opened. Gate: build ok; unit 2055/1 fail (ContextGoldenTests, not ours); integration 55/55; rendering 21/21; arch 128/1 fail (ARCH-CYCLE-001 Aw.Context.Infrastructure, not ours); format clean. Nothing left.

## Review fixes (round 1) - done
Head 1b27ab896 (main 883b90fc5 merged). R1-1 topic wording, R1-2 palette "coming soon". Gate: Ui unit 2057/2057, Rendering 21/21, Architecture 128/128, format clean. reviews/457.json resolutions set; PR body has a Review fixes section. Nothing left.
