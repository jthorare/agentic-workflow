# The Agents page: rows and reasons

Spec: `docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md` (issue 3018). This page says what exists so far. Phase A0 (issue 3144) added the wire shape only: no route, command or page serves it yet.

## The row

One Agents row is one live session, one waiting job or one lane with no session. The Core type is `AgentRow` in `Aw.Sindri.Core.Sessions`; the list is `AgentRowList` (`rows` in page order, `degraded` for sources that could not be read). The JSON schema is `schemas/agent-rows.schema.json`. The API goldens `tests/Aw.Sindri.Tests/Golden/Api/agents-rows/needs-you.json` and `every-reason.json` are examples, and `needs-you` is the spec's section 4.3 walk (the fixture is `tests/fixtures/agents/needs-you.json`).

`AgentRow` holds the fields the page adds to today's `SessionRow`: `id`, `version`, `state`, `stateSource`, `stateReadAt`, `estimated`, `title`, `facts`, `reasons`, `parent`, `since`, `lastActivity`, `blocks`, `actions` and `links`. The list route (phase A1) joins them to `SessionRow`. Until it ships, no route returns `reasons[]` on `GET /sindri/sessions`.

## Reasons

`AgentReasons.Evaluate` is a pure function. It takes an `AgentReasonInputs` for one row and returns the reasons that hold, lowest class first. The first is the primary one. It reads nothing: the collector (phase A1) fills the inputs from the session state, `aw-meta lanes-context`, `usage`, `status` and `health`.

| Class | Kind | Holds when | Group |
|---|---|---|---|
| 1 | `permission-prompt`, `owner-question`, `login` | the session asks (approval, input, auth) | Needs you |
| 2 | `usage-limit` | `aw-meta usage` lists the lane as Limited | Waiting |
| 3 | `lane-no-session` | a lane in the team map has no live session and the owner did not stop it from the page | Needs you |
| 4 | `recycle-mark` | the lane's context is at the recycle mark, or the meta lane has an open RecycleSelf obligation | Needs you |
| 5 | `idle-owed` | a lane session is idle for `agents.idleOwedMinutes` (default 10) and work is owed: a PR that failed its gate or conflicts, the meta lane's open obligations, or mail a non-Claude lane was not given | Needs you |
| 6 | `slot-wait` | a heavy waiter waited `agents.slotWaitMinutes` (default 30) or more | Waiting |
| 7 | `pr-ready` | a PR the lane sent is in the merge queue with its gate passed or not run, and Meta has not queued it on GitHub | Needs you |

Each reason carries `word` (the chip), `text` (the words on the row), `since` (when the row first held it, from the source) and `source`. The text golden `tests/Aw.Sindri.Tests/Golden/Agents/walk-reasons.txt` pins the walk.

## Profile keys

`agents.idleOwedMinutes`, `agents.slotWaitMinutes` (1 to 1440) and `agents.page` (`rows` or `legacy`, default `legacy`) are documented in `docs/csharp/sindri-profile.md`. A bad value is `SND-AGENT-010`. The `SND-AGENT-001` to `011` codes are in `docs/csharp/sindri.md`.
