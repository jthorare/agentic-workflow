# Sindri's Messages page: clear the chat inbox without opening Slack

Status: first draft (ideas lane, 2026-11-12). Serves #2979 (Messages, a Slack replacement with full parity) under #2978 (one app that replaces the others). It builds the Messages rows of the prior-art ledger (`docs/sindri/prior-art/messages.md`, #2989) and the child issues #3020 (snooze and follow up) and #3021 (links as live rows). It sits beside the Home spec (`docs/superpowers/specs/2026-11-12-sindri-home-inbox-design.md`) and reuses its row / peek / full grammar, keys, snooze store and write-through pipeline.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

"Ledger N" means problem N in the prior-art file. "Take N" means item N of its closing list "What we take". "Parity row N" means row N of §3 below.

A guess is marked **(guess)** and names the P0 probe line that proves it. P0 is the owner-run probe of what a Slack user token can do (§5.5). No guess below has been run against Slack.

## 0. Why

The owner's words on #2979: "imagine that i wanted to add a slack replacement experience here. how would that work / look without losing any slack functionality." Today the owner opens Slack for every message.

What exists on `origin/main` (cb6cacd146; read in the code, issues linked; the app was not driven for this spec):

- **No Messages page.** Slack appears in four places, and none is a reader.
- **A REST connection.** The `slack` OAuth preset (`config/connections/presets/slack.json`) is refused because Slack accepts only https redirects. The path is `sindri connection add slack:work --kind rest --key-env SLACK_TOKEN` (`docs/sindri/connections.md:66`). The token lives in one keychain entry (`IConnectionSecrets`).
- **One poller and one write.** `SlackEventPoller` (`Infrastructure/Connections/Polling/`) polls `conversations.history` of ONE channel, 100 per page, and raises `slack.message` and `slack.mention` (`Core/Connections/PollableEvents.cs`). `RestActions.cs` has one Slack write, `post_message`.
- **A context reader.** `Aw.Connectors.Core.Slack` (`ISlackReader`, `SlackCursor`, `SlackPermalink`, `SlackTs`) reads public and private channels for the code-and-docs index with a BOT token. `SlackChannelIds` refuses direct messages on purpose. Messages cannot reuse that reader as it stands.
- **Nothing else.** No Socket Mode, no DM or thread read, no read marks, no search, no reactions, no edit, no delete, no file, no draft.

The job, from #2979: **a Slack client on the owner's own token that shows what needs a reply first, lets the owner answer, react, snooze and turn a thread into work in place, and shows the rest of Slack one key away. Slack stays the system of record. Colleagues see ordinary Slack messages.**

Two facts shape everything (both from the ledger's header, read from Slack's docs on 2026-11-12):

1. **History reads may be 1 per minute.** For apps created after 2025-05-29 that are not in the Marketplace, `conversations.history` and `.replies` allow 1 request per minute with at most 15 messages. Older internal apps keep Tier 3 (50 or more per minute). Which one the owner's app gets is unknown **(guess; P0 line 3)**. The sync design below works in both cases: live events fill the store, and history only fills gaps.
2. **Slack has no API for Later, drafts, the Activity feed or channel notification settings.** Those four are Sindri-local. The parity ledger says "differs" for them, never "done".

## 1. The bar

| Step of the daily loop | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Clear what needs me to zero | 1 | Superhuman keys, Zulip Inbox | Done / Snooze / stay on one key each; only threads whose last word is not mine | §6, §7 |
| Know which threads wait on me | 2 | Zulip "Participated", Slack Activity | mine = I wrote in it, was mentioned in it, or follow it; sorted by who waits | §7.2 |
| One topic per conversation | 3 | Zulip topics, wee-slack threads | a thread is a row; a gist is its title | §7.3, §9.1 |
| Read a busy channel fast | 4 | wee-slack, slack-term (MIT) | one line per short message, grouped by author, threads collapsed | §6.3 |
| Jump with the keyboard | 5 | Slack Ctrl+K, Zulip N / P | one palette; `n` next unread; `Esc` marks read | §6.5 |
| Reply, edit, react, send later | 6 | Slack keys, Zulip drafts | Slack's message keys; local drafts; optimistic send | §6.4, §8 |
| Snooze and follow up | 7 | Superhuman `H`, Zulip reminders | one snooze store; "unless replied" | §9.4 |
| Search with operators | 8 | Slack modifiers, Zulip operators | Slack's words verbatim; local first | §9.8 |
| Catch up after time away | 9 | Slack AI Recap | per channel 3 lines, each citing its source | §9.6 |
| Thread to task | 10 | Linear's Slack app | `m` make, `a` attach, both ways visible | §9.5 |
| Live row for a link | 11 | GitHub and Linear unfurls | a 32 px row from our own store | §9.3 |
| Mute and notify | 12 | Zulip mute and follow rules | mention breaks through; followed overrides muted | §9.9 |
| The connection | 13 | wee-slack OAuth route | official user token only; honest gap status | §5 |

Nobody has a thread row that is also the PR's row and the issue's row, a snooze that waits on CI, or an agent draft that waits for approval in the same inbox. Those three are ours and have no prior art to lean on.

**Release 1** is the smallest thing that can replace Slack for one reply: one workspace; DMs, mentions and starred channels; the four smart lists; the dense conversation; the thread in the peek; send, reply, react and mark-read as write-through; local drafts; snooze; link rows. It leaves out files, scheduled send, notification rules, app blocks and status. Those are P4, and the "day without Slack" cannot pass until they ship.

**The bar in one line:** Needs reply names the thread that waits on the owner first, the owner answers it without opening Slack, the answer reaches Slack within 2 s, and the lists never lie about a gap.

Three bets decide whether this is worth it. Each has a number.

1. **The inbox beats Slack's Activity view.** During dogfood weeks, the owner's Slack opens drop to 5 or fewer a week. ux counts them (W14).
2. **Live is live enough.** A new DM shows in Needs reply within 5 s with Socket Mode, or within 30 s on the polled hot set **(guess; P0 lines 3 and 5)**. If P0 says neither holds, the page ships read-late with a visible "last read" time and the bet is re-priced.
3. **The token is allowed.** The owner's workspace lets the owner install a Slack app with user scopes. If an admin blocks it, there is no token and no Messages page. That is question 1 (§16) and a day-0 gate.

### The W-rows

A W-row is one done line. Each is a test or a measured run in the real app (aw-ui-driver) and names its owner lane in §14.

| # | Done line | Measured as |
|---|---|---|
| W1 | First paint from the store. | Needs reply list, 5,000 threads in `messages.db`, serve not answering, p95 over 20 runs: under 100 ms. |
| W2 | Key to paint, write to Slack. | Keydown to repaint under 50 ms. Send request to Slack confirming within 2 s, p95 over 50 writes on the fake Slack. |
| W3 | A failed write is visible and retryable. | For each write kind: the fake refuses, the line shows the reason and Retry within 1 frame, and Retry lands the write exactly once (§8.3). |
| W4 | Density. | 1440x900, peek closed, compact: at least 25 list rows, or at least 40 conversation lines of short messages. |
| W5 | Scroll. | 10,000 messages in one conversation scroll with a frame time under 16 ms (virtualized rows). |
| W6 | The ledger is green. | Every Slack function in §3 is `done`, `partial` with a named limit, `missing #N`, or `dropped: why`. `scripts/check-parity.sh docs/parity/slack.md` fails on a bare row. |
| W7 | Needs reply is exact. | A property test over 10,000 random event streams: a thread is in the list if and only if the last word is not mine and I am a participant. The §7.2 example is a golden test. Rebuild after one event under 50 ms at 5,000 threads. |
| W8 | Links are live rows. | A message with 3 links to a PR, an issue and a run lays out in under 16 ms with 0 network calls. A link to something Sindri does not know stays plain text. |
| W9 | Snooze. | The row returns within 1 s of the due time with the app in the tray. "Unless replied" drops the snooze when someone answered after my last message. Undo within 5 s. |
| W10 | No silent gap. | With the fake Slack at 1 request per minute and 15 per page, the conversation shows a gap marker with the count and "Load older (1 per minute)". A test fails if a gap shows no marker. |
| W11 | Catch-up brief. | At most 3 lines per channel; every line cites message ids that exist in the store; plain-fact lines paint under 100 ms; a model failure costs only the sentence. |
| W12 | Agents never send alone. | A test enumerates every code path that calls `chat.postMessage`: each one starts from a human act or a workflow revision saved by a human. A session's `messages_draft` writes a draft and nothing else. |
| W13 | Search. | Local first result under 100 ms on 1,000,000 messages (SQLite FTS5). Every operator in §9.8 has a golden parse test. |
| W14 | **A day without Slack.** | ux runs a scripted day with Slack closed. Each time the owner opens Slack is an issue, `Part of #2979`. Count: 0 for shipped rows. Huddles are the one named exception (§3, row 31). |
| W15 | Untrusted text. | A test posts a message holding markup, a bidi override and a prompt-injection line. The page shows plain text, no remote image loads, and every MCP and CLI result fences it. |
| W16 | Arrival. | A message posted on the fake Slack appears in Needs reply within 5 s (socket) or 30 s (poll), measured from the fake's post to the row painted. |

## 2. Decisions

1. **Slack is the system of record. Sindri keeps a cache in `messages.db`.** The file holds channels, DMs, users, messages, reactions, file metadata and read state. It is a SQLite file in `$AW_SINDRI_HOME`, mode 0600, separate from the ledger. *Why:* the ledger is append-only, and a Slack delete or edit must change local text. Putting DM text in the ledger would keep it forever and leak it into `GET /sindri/ledger`.
2. **The ledger holds ids and counts, never message text.** Events: `messages.act.sent`, `.landed`, `.failed`, `messages.synced`, `messages.thread.linked`, `messages.draft.agent`, `messages.send.workflow`. *Why:* the same reason, and `Aw.Redaction` cannot unwrite an event.
3. **One official user token, `xoxp-`, in a Slack app the owner installs.** No session cookie, no `xoxc-`. *Why:* Slack calls cookie tokens unsupported and they cannot be revoked (ledger 13). A bot token cannot read the owner's DMs, so it cannot replace Slack.
4. **Live from Socket Mode when P0 proves it, otherwise polling. Events API is out.** Events API needs a public https URL; Sindri listens on loopback only. *Why:* P0 line 5 decides. Until it runs, the code ships the poll path and a `ISlackLive` port that Socket Mode fills later.
5. **History fills gaps, not the store.** The store fills from live events and from reads the owner triggers (open a conversation, press `Load older`). A background backfill runs only on a Tier 3 workspace (§5.3). *Why:* 1 request per minute cannot backfill anything.
6. **The left side is four smart lists, not Slack's channel tree.** Needs reply, Unread, Later, Drafts; then starred channels; all channels behind `/` and `Ctrl+K`. *Why:* Slack's sidebar is where its noise comes from (#2979).
7. **Mine means participant.** A thread is mine when I wrote in it, was @mentioned in it, or follow it. A DM is always mine. `@here` and `@channel` never make a thread mine. *Why:* Zulip's "Participated" (ledger 2), with the broadcast case cut so a lunch ping does not rank.
8. **Later, Drafts, snooze, mute and Done are Sindri-local.** None writes to Slack. *Why:* Slack has no API for the first and last of them (§0). The ledger marks each "differs".
9. **Writes follow Home's pipeline** (Home §5.2): paint in under 50 ms, write to Slack, confirm within 2 s, visible failure with Retry, Undo only for local state. Message sends carry an `actId` in Slack's `metadata` so a retry never posts twice **(guess; P0 line 6)**.
10. **A bare letter never writes to Slack.** `r` opens the composer; sending is `Enter` inside the composer (Slack's own key; `messages.sendKey` can change it to `Ctrl+Enter`), or the `Ctrl+Enter` menu on a row. Delete asks for a confirm. *Why:* this is #2978's grammar rule, and `Enter` in a text box is typing, not a list key.
11. **Two key sets, never both live.** List mode (the cursor is on a list row) uses Home's keys. Message mode (the cursor is on a message in a conversation or thread) uses Slack's message keys. The footer names the live set. *Why:* `e` is Done on Home and Edit in Slack, and the ledger says to keep Slack's keys. Question 3 asks the owner.
12. **A link to something Sindri knows renders as a live row** from the local row store, never as a Slack unfurl (#3021). It needs no Slack data, so it ships first.
13. **Snooze is one store with Home.** Messages adds the field `unlessReplied` (#3020). Home builds the store under #3030. *Why:* api's plan on #2979: one record on a row.
14. **Agents draft; only a human-saved workflow revision sends.** `draft_message` waits for the owner. `send_message` activates only on a revision saved by a human (engine plan on #2979). There is no other auto-send. *Why:* meta's rule on #2979, tested as W12.
15. **Triage rank is deterministic; a model only writes the gist.** The class of a row (direct question, review ask, FYI) comes from fixed rules (§7.2). The gist is one model sentence, cached. *Why:* the order must not change between two runs on the same store.
16. **Search is local first, Slack second.** Local FTS5 answers in 100 ms. A second pass calls `search.messages` and tags results "from Slack". *Why:* Tier 2 is 20 calls a minute and the API search follows workspace limits (ledger 8).
17. **Huddles and calls are dropped on purpose.** A huddle link shows as a row with "Open in Slack". *Why:* #2979. A user token cannot join one.
18. **Messages raises Home rows; Home ranks them.** Messages owns the list; Home owns the one inbox. A `dm` or `mention` row exists in both with one id (§9.2). *Why:* Home §3.2 already defines `dm` and `mention`.
19. **Message text is untrusted everywhere.** Pages show plain text. Agents get it fenced (§11).

## 3. The parity ledger

This table is the first draft of `docs/parity/slack.md` (M0b copies it there in the five-column shape of `docs/parity/linear.md`). Status words: `planned Mn` (a phase in §14), `partial` (with the limit), `differs` (works, but is Sindri-local), `blocked` (P0 must prove it), `dropped: why`. When a phase ships, the cell becomes `done` with an evidence path. Gap issues are filed at the start of each phase, `Part of #2979`.

| # | Slack function | Status | How, and the limit |
|---|---|---|---|
| 1 | Channels: public, private; read, jump | planned M1 | `users.conversations`, `conversations.history`; the list is local |
| 2 | Direct messages | planned M1 | needs `im:history`; a bot token cannot (§5.1) |
| 3 | Group DMs | planned M1 | `mpim:history` |
| 4 | Threads: read, reply | planned M1, M2 | `conversations.replies`; reply writes `chat.postMessage` with `thread_ts` |
| 5 | Threads view / Activity (threads you are in, mentions) | differs, planned M1 | Slack has no activity API; Needs reply is built from the store (§7.2) |
| 6 | Mentions | planned M1 | `<@U...>` in text; live event or search poll |
| 7 | Keyword alerts | planned M4 | local list `messages.keywords`; FYI rows |
| 8 | Reactions: see, add, remove | planned M1, M2 | `reactions.add` / `.remove`, `reactions:write` |
| 9 | Edit own message | planned M2 | `chat.update`; own messages only |
| 10 | Delete own message | planned M2 | `chat.delete`; confirm in the UI |
| 11 | Rich text: bold, italic, strike, lists, quotes | planned M1 (render), M2 (compose) | `Mrkdwn` parser in Core; golden tests |
| 12 | Code blocks and inline code | planned M1 | rendered mono; diffs get +/- colour |
| 13 | Emoji and custom emoji | planned M1 | `emoji.list` cached; Unicode names built in |
| 14 | Files and images: view | planned M4 | fetched with the token to `files.slack.com`; disk cache capped |
| 15 | Files: upload | planned M4 | `files.getUploadURLExternal` + `completeUploadExternal`, `files:write` |
| 16 | Search with Slack's operators | planned M1 (local), M3 (cross-source) | §9.8; Slack search is the second pass |
| 17 | Unread counts | partial, planned M1 | Slack gives `unread_count` for DMs only; channels are counted from `last_read` and the store **(guess; P0 line 2)** |
| 18 | Mark read, mark unread | planned M2 | `conversations.mark`; `*:write` scope; "mark unread" sets the mark to the message before |
| 19 | Later / Saved | differs, planned M1 | Slack has no API (`stars.list` is dead); Later is a local flag; saves made in Slack do not appear |
| 20 | Drafts | differs, planned M2 | no drafts API; local drafts, autosave after 3 characters; Slack's own drafts stay in Slack |
| 21 | Scheduled send | partial, planned M4 | `chat.scheduleMessage`; the list shows only messages scheduled from Sindri (`chat.scheduledMessages.list` limit) |
| 22 | Status | planned M4 | `users.profile.set`, `users.profile:write` |
| 23 | Presence | partial, planned M4 | read others' presence for DM partners (`users.getPresence`, rate limited); setting own presence **(guess; blocked until P0)** |
| 24 | Do not disturb | planned M4 | `dnd.setSnooze`, `dnd.endSnooze`, `dnd.info` |
| 25 | Notification rules per channel | differs, planned M4 | Slack has no API; Sindri rules apply to Sindri's toasts only; Slack's phone pushes are untouched |
| 26 | Huddles and call links | dropped: a user token cannot join a huddle; the link opens in Slack | #2979 names this drop |
| 27 | Slash commands | blocked | no documented Web API to run a user's slash command **(guess; P0 line 7)**; fallback: "Open in Slack" |
| 28 | App messages and blocks: render | planned M4 | Block Kit subset: section, context, divider, image, actions; unknown blocks show as text plus "Open in Slack" |
| 29 | App messages: interactive buttons | blocked | a click is sent by the Slack client to the app; a user token has no documented way to send it **(guess; P0 line 7)**. #2979 says buttons work: this is the biggest claim the probe may kill. Link buttons open the URL |
| 30 | Slack Connect channels | partial, planned M1 | read and write like any channel; inviting an outside org is dropped: admin-only |
| 31 | Channel: create, join, leave | planned M4 | `conversations.create`, `.join`, `.leave`; scopes `channels:write`, `groups:write` |
| 32 | Channel: mute | differs, planned M4 | no API; local mute (§9.9) |
| 33 | Channel: archive, rename, set topic | dropped: admin or owner rights, rare; open in Slack | |
| 34 | User profiles | planned M1 | `users.info`, `users.profile.get`, cached; the peek shows name, title, local time, status |
| 35 | Jump-to (`Ctrl+K`) | planned M1 | the #2978 palette with channel and person targets |
| 36 | Keyboard set | planned M1, M2 | §6.5 |
| 37 | Pins | planned M4 | `pins.add`, `.remove`, `.list` |
| 38 | Forward / share a message | planned M4 | posts the permalink into another conversation (a write) |
| 39 | Remind me about this | differs, planned M1 | a Sindri snooze on the thread (§9.4); `reminders.add` is not used so no Slackbot DM appears **(guess: owner may prefer both; question 7)** |
| 40 | Copy link to message | planned M1 | `SlackPermalink` pattern from the workspace name |
| 41 | Bookmarks in a channel header | dropped: read-only in Slack's header; shown as plain links in the channel peek if `bookmarks.list` works | low value |
| 42 | Canvases and Lists | dropped: a link opens in Slack | no stable user API in the probe plan |
| 43 | Slack's own workflows (Workflow Builder) | dropped: Sindri workflows replace them | |
| 44 | Message formatting preview, link unfurl previews | partial, planned M1 | links to known objects render as live rows; other links show their title only |
| 45 | Multiple workspaces | dropped for Release 1; one workspace first | question 2 |

Row 29 and row 27 are the two where this ledger may contradict #2979's text. The draft says `blocked` until the owner runs P0. A `blocked` row needs an owner-accepted drop before W6 can pass.

## 4. What changes from today

| Today | After |
|---|---|
| `slack:work` REST connection, one write | the same connection, plus an app-level token entry, plus the sync service (§5) |
| `SlackEventPoller` polls one channel for workflows | stays as is; workflow triggers read the store later (§9.7) |
| `Aw.Connectors.Core.Slack` (bot token, channels only) | stays; the code index keeps its own narrow reader. Messages never feeds the index |
| no Messages tab | `Messages` joins the nav next to Home |
| Slack events are workflow triggers only | a message becomes a store row first, then a trigger and a Home row |

## 5. The Slack connection

### 5.1 Scopes and the token

The app is a Slack app the owner creates in the workspace from a manifest that Sindri prints (`sindri messages connect --manifest`). It asks for **user** scopes only. Scope names come from api's plan on #2979 and Slack's method pages; P0 line 1 reads the granted list back.

| Need | User scopes |
|---|---|
| List and read channels, DMs, group DMs | `channels:read`, `groups:read`, `im:read`, `mpim:read`, `channels:history`, `groups:history`, `im:history`, `mpim:history` |
| People and emoji | `users:read`, `emoji:read` |
| Send, edit, delete | `chat:write` |
| React | `reactions:read`, `reactions:write` |
| Mark read | `channels:write`, `groups:write`, `im:write`, `mpim:write` |
| Search | `search:read` |
| Files | `files:read`, `files:write` |
| Status, DND | `users.profile:write`, `dnd:write`, `dnd:read` |
| Pins | `pins:read`, `pins:write` |

Release 1 asks only for the first five rows plus `search:read`. A later phase that needs more scopes shows "Needs scope X: reconnect" on the feature and keeps everything else working (SND-MSG-003).

**Where the token lives.** `sindri connection add slack:work --kind rest --key-env SLACK_TOKEN` already stores it in one keychain entry (service `sindri-connection`, `IConnectionSecrets`). `sindri messages connect` wraps that call, reads `auth.test`, records the workspace id and the user id on the connection row, and refuses a token whose workspace differs from the one it was connected to (SND-MSG-024). The app-level token `xapp-` (Socket Mode) is a second keychain entry on the same connection name with the suffix `.app`. Neither token appears in an argument list, a log, an error, a ledger row or a response.

### 5.2 Live: Socket Mode or poll

| Path | How it works | Needs | Status |
|---|---|---|---|
| Socket Mode | `apps.connections.open` with the `xapp-` token returns a WebSocket URL; Slack pushes events; Sindri acks each envelope | the app subscribes to user events (DMs and channels the owner is in) | **(guess)** Slack's page shows `bot_events` only. P0 line 5 tests a user-event subscription |
| Poll, hot set | `conversations.history` with `oldest=<cursor>` for conversations with unread, mentions, an open view or a star | Tier 3 history (50 or more per minute) | **(guess)** P0 line 3 reads the real limit |
| Poll, search | `search.messages` for `to:me` and `has:...` as a change detector for DMs and mentions: 1 call per 20 s uses 3 of Tier 2's 20 calls a minute | `search:read` | **(guess)** fallback when history is at 1 per minute; P0 line 4 |
| Poll, cold set | the rest, rarely | Tier 3 | only on Tier 3 |

Order of preference: Socket Mode, then hot-set polling, then search polling. Sindri picks the best path P0 proved, writes the choice to `messages.sync` (`auto`, `socket`, `poll`), and shows it in the status line and in `sindri doctor` (SND-MSG-020 is the info row when Socket Mode is not available). A disconnect makes one history read per hot conversation to fill the gap. On a 1-per-minute workspace that read is queued by hotness and the rest show a gap marker (W10).

### 5.3 Rate limits and the honest status

The sync service keeps one token bucket per Slack method, seeded from the `Retry-After` and tier headers P0 records. A 429 pauses that method only. The page header shows the state in words:

- `Live (socket)`, `Live (polling every 30 s)`, `Behind: history is limited to 15 messages per minute`, `Offline: last read 09:41`.
- A conversation with a hole shows a **gap marker** line: "12 older messages not loaded. Load older (1 per minute)". The marker is a conversation row, so it scrolls with the messages. A test fails if a gap has no marker (W10).
- Backfill: on Tier 3, `messages.backfill.days` (default 14 for DMs, starred channels and threads in Needs reply; 3 for the rest) runs at 20% of the bucket. On a limited workspace it is off.

### 5.4 Write-through methods

| Action | Slack method | Scope | Notes |
|---|---|---|---|
| Send, reply | `chat.postMessage` (`thread_ts` for a reply) | `chat:write` | the `actId` goes in `metadata.event_payload`; a retry first looks for it **(guess; P0 line 6)** |
| Edit | `chat.update` | `chat:write` | own messages only |
| Delete | `chat.delete` | `chat:write` | own messages only |
| React | `reactions.add`, `reactions.remove` | `reactions:write` | already-reacted is a no-op |
| Mark read, unread | `conversations.mark` | `*:write` | |
| Schedule | `chat.scheduleMessage` | `chat:write` | up to 120 days **(guess)** |
| Upload | `files.getUploadURLExternal`, `files.completeUploadExternal` | `files:write` | |
| Status, DND | `users.profile.set`, `dnd.setSnooze`, `dnd.endSnooze` | as named | |

Each write is one row in the outbox table (§8.3). Rate limits queue writes; a queued write shows "queued, Slack asked us to wait 12 s".

### 5.5 The P0 probe

api's plan on #2979 gives `scripts/slack-probe.sh`: owner-run, read-only by default, same shape as `scripts/linear-probe.sh` (the key reaches curl through a 0600 config file, the host must be `slack.com` or loopback, output is counts, booleans, error codes and header numbers; no message text, names or channel names). This spec adds the lines below to that plan. Each guess in this spec points at a line, and each result becomes a ledger cell.

| Line | Question | Result changes |
|---|---|---|
| 1 | Token kind and granted scopes (`auth.test`, `x-oauth-scopes`) | §5.1 scopes; rows 2, 3, 14 to 24 |
| 2 | Can the token read DMs, group DMs, threads? Do `conversations.info` and the `last_read` field give a channel unread count? | rows 2, 3, 4, 17 |
| 3 | History and replies tier: `Retry-After` after a burst, the `limit` ceiling, the `x-rate-limit` tier | decision 5, §5.3, W16 |
| 4 | `search.messages` ok? Error code? Tier? Does `to:me` work as a change detector? | §5.2 third row, rows 16, 6 |
| 5 | `apps.connections.open` with a user token and with an `xapp-` token; can the app subscribe to user events? | decision 4, §5.2, bet 2 |
| 6 | With `--write` in a scratch private channel: post with `metadata`, read it back, update, react, mark, delete, schedule, upload | decision 9, rows 9, 10, 18, 21 |
| 7 | Slash commands: does `chat.command` exist and accept a user token? Can a user token trigger a block action? | rows 27, 29 |
| 8 | Is the app blocked by workspace approval for any scope? | question 1 |

## 6. The page

### 6.1 Layout

Three columns on the #2978 grammar: a left rail, the list or conversation, and the peek. 32 px rows (compact, the default) and 40 px (comfortable), set by `home.density`.

```
┌ top bar 40 ───────────────────────────────────────────────────────────────────────────────────────────────┐
│ Messages   3 need reply · 1 draft          / search or jump         Live (socket) · status · DND · joi     │ header 48
├ rail 220 ────────────────┬ list or conversation ─────────────────────────────┬ peek 420 ────────────────────┤
│ ▸ Needs reply        3   │ ◉ Sam    can you ship the fare table today?  DM  │ Thread · #harbor              │
│   Unread            14   │ ◎ Ada    #harbor  review #3120 when you can  2h  │ Ada 09:02                     │
│   Later              2   │ ◎ Kim    #harbor  thread: seat holds  new   3h   │  @joi review #3120 when you   │
│   Drafts             1   │                                                  │  can ┌─────────────────────┐ │
│ ─ Starred ───────────────│                                                  │      │ ◎ #3120 Seat holds  │ │
│   # harbor           2   │                                                  │      │ CI green · asked of │ │
│   # ferry-ops            │                                                  │      │ you · ada      32px │ │
│   Sam                    │                                                  │      └─────────────────────┘ │
│   Kim, Lee               │                                                  │ Joi 09:10 (sending)           │
│ / all channels           │                                                  │ ┌ reply to Ada ─────────────┐ │
│                          │                                                  │ │ On it, after lunch        │ │
│                          │                                                  │ └ Enter send · Esc ─────────┘ │
├──────────────────────────┴──────────────────────────────────────────────────┴───────────────────────────────┤
│ j/k move · Space peek · Enter open · r reply · e done · h snooze · m make issue · a attach · ? keys         │ footer 28
└────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

The rail is collapsible (`[`) to a 48 px strip with counts. Below 1,100 px the peek replaces the middle column.

**Needs reply row.** Glyph (class: direct question `◉`, review ask `◎`, FYI `○`), who, the place (`DM` or `#channel`), the one-line gist, age, and on the cursor row two or three buttons (Reply, Snooze, Done). Facts are fixed columns, never colour alone.

**Conversation.** Open with `Enter` from a list or `Ctrl+K`. It fills the middle column. See §6.3.

**Peek.** `Space` on a list row shows the thread, or on a conversation line shows that message's thread, or the linked object (a PR's summary, an issue's fields) when the cursor is on a link row. The composer is in the peek so a reply never leaves the list.

### 6.2 The four lists

| List | Holds | Order | Leaves when |
|---|---|---|---|
| Needs reply | threads where I am a participant and the last word is not mine (§7.2) | class, then blocks, then oldest | I reply, press Done, snooze, or the thread links an issue with an owner and nothing new asks me |
| Unread | conversations with messages after my read mark, not muted | most recent first | I read them (`Esc`, scroll to the end, or `Shift+G`) |
| Later | threads and messages I flagged | newest flag first | I remove the flag |
| Drafts | my unsent drafts, owner-written and agent-written | oldest first | I send or delete |

Counts on the rail are computed from the store. Done and snoozed rows are not counted. Unread and Needs reply overlap: a DM I have not read is in both.

### 6.3 The dense conversation

- **One line per short message**, grouped by author: the name and time show on the first line of a run; later lines show only the time, on hover. A run breaks after 5 minutes of silence or a different author. Line height 20 px; rows are virtualized.
- **Threads collapsed inline**: "Ada, Sam and 2 more · 6 replies · last 09:02" as one 28 px line under the root. `Enter` or `t` opens the thread in the peek.
- **Code and diffs**: fenced code renders mono with a copy key; a block that starts `diff --git` or has +/- lines gets add and remove colours. Inline code is mono.
- **Link rows**: §9.3.
- **Reactions** are chips under the line with counts and the names on hover. The owner's own chip has a filled border.
- **Edits** show "(edited)". **Deleted** messages leave the store and show no tombstone (Slack shows none to the author either).
- **Files and images** show a thumbnail 96 px high, or a file chip (name, size). Click opens the peek.
- **New messages** since the last read show under a "New" divider; the view lands on the divider on open. A new message that arrives while the cursor is above it adds to a "3 new ↓" chip and never moves the view.
- **Gap marker** (§5.3) and **the day divider** are conversation rows.

### 6.4 The composer

- Slack's formatting and shortcuts: `*bold*`, `_italic_`, `~strike~`, backticks, `>` quote, lists, `Ctrl+B / I / Shift+X`, `Ctrl+Shift+C` code block. The preview is what Slack will render (`Mrkdwn` in Core, one parser).
- `@` people, `#` channels, `:` emoji, `/` commands that do not exist as API are shown disabled with the reason (rows 27, 29).
- `[[` opens a picker for a PR, issue, note or scope. It inserts the public URL for a PR or issue. A note or a scope has no URL a colleague can open, so the picker inserts its title as plain text and shows "local only: not a link" on the chip.
- **Ask an agent to draft** (button, palette id `messages.draft-agent`): starts a `draft_message` run with the thread as fenced input and a note field. The result lands in the composer as a draft marked "Drafted by an agent". It is never sent by that act (§9.7).
- **Send** is `Enter`; `Shift+Enter` is a newline; `Ctrl+Enter` opens the row's submit menu with Send, Send and mark done, Schedule. An unedited agent draft needs the menu (decision 14).
- Autosave after 3 characters on leaving the composer (Zulip's rule). Drafts are local (parity row 20).

### 6.5 Keys

Keys follow the #2978 grammar. `?` shows the live set. The rule is decision 11: two sets, one live.

**List mode** (cursor on a list row; Home's keys):

| Key | Does | Writes to Slack? |
|---|---|---|
| `j` `k` | move | no |
| `Space` | peek | no |
| `Enter` | open the conversation / thread full | no |
| `r` | reply: opens the composer in the peek | no (sending does) |
| `e` | Done (leaves Needs reply); Undo `Ctrl+Z` for 5 s | no |
| `h` | snooze picker (§9.4); `f` inside it toggles "unless replied" | no |
| `m` | make issue from the thread (§9.5) | no (Linear) |
| `a` | attach the thread to an issue | no |
| `t` | hand to agent (Home §5.1 picker) | no |
| `u` | mark unread | yes: `conversations.mark` |
| `l` | flag for Later | no |
| `n` | next unread / needs-reply row | no |
| `Esc` | mark the open conversation read, back | yes: `conversations.mark` |
| `/` | filter this list; `Ctrl+K` palette | no |
| `.` | actions panel with keys shown | |
| `c` | new message (pick person or channel) | no (sending does) |

`u` and `Esc` write only a read mark. They are reversible and are the one exception to decision 10; Home's rule allows a bare letter when it opens something cancellable or the write is Undo-able. `Ctrl+Z` undoes the mark within 5 s.

**Message mode** (cursor on a message line; Slack's keys):

| Key | Does |
|---|---|
| `e` | edit own message (up-arrow from an empty composer does the same) |
| `r` | react: emoji picker |
| `t` or `→` | open the thread in the peek |
| `f` | forward: pick a conversation, posts the permalink |
| `m` | remind: opens the snooze picker on this thread |
| `a` | save to Later |
| `p` | pin |
| `Del` | delete own message, with a confirm |
| `Shift+E`, `Shift+H`, `Shift+M`, `Shift+A` | Done, snooze, make issue, attach, for the whole thread |
| `g` `G` | top, bottom (`G` marks the view read) |

`Tab` stays focus traversal. Every key has a palette entry with its key shown (Superhuman's rule).

### 6.6 States, per surface

Every surface also shows the one status line of §5.3. Banners name the cause and the fix.

| Surface | Loading | Empty | Stale or behind | Disconnected or revoked | Rate limited | Failed write |
|---|---|---|---|---|---|---|
| Needs reply | 6 skeleton rows from the cache | "You're caught up." with the next snooze time | rows stay; header says "Behind: 12 min" | "Slack token expired. Reconnect" (SND-MSG-002) | header "Behind" + reason | row keeps its place; "failed: reason" + Retry |
| Unread | skeleton | "Nothing unread." | gap markers in conversations | as above | as above | n/a |
| Conversation | cached lines paint; new ones stream in | "No messages yet. Say hi." | gap marker | read-only with a banner | gap marker with Load older | the line stays, greyed, red "failed: reason", Retry and Discard |
| Peek thread | root first, replies stream | n/a | "replies loaded 09:12" | as above | as above | as above |
| Composer | n/a | placeholder with the target | n/a | disabled, "Reconnect to send" | "queued, wait 12 s" | draft kept, error above it |
| Search | local results paint first, "Searching Slack..." | "Nothing in the store. Search Slack?" | "Slack search unavailable: limit" | "Local results only" | "Local results only, Slack limit" | n/a |
| Catch-up brief | plain facts paint, sentence streams | "Nothing changed." | "Written from the store as of 09:12" | n/a | n/a | "Model off: facts only" |
| Scope missing | the feature is disabled, not hidden | n/a | n/a | n/a | n/a | "Needs scope `files:write`. Reconnect" (SND-MSG-003) |

### 6.7 Accessibility

- Lists are `role=listbox` with one tab stop. The cursor row is the single active descendant. `Space` peek never steals focus; `Enter` moves focus to the conversation.
- Message lines are `role=listitem` inside a `role=log`. A polite live region announces a new message in the OPEN conversation only ("Ada: review 3120 when you can"). Other conversations announce through the rail count, not a message.
- State is never colour alone: class glyphs differ in shape, "failed" is text, "new" is the word.
- Every action has a palette entry and a button. Reactions have a button, not hover only.
- Focus after Done: the next row. After send: the composer stays focused. After a failed write: focus stays on the failed line; Retry is the next tab stop.
- Reduced motion turns off the new-message chip animation and row removal slides.
- The screen-reader name of a Needs reply row: "Direct question from Sam in a direct message, 8 minutes ago: can you ship the fare table today".

## 7. Triage: what waits on me, and in what order

### 7.1 Which threads are mine

The store marks a conversation row **mine** when one of these holds. `me` is the user id `auth.test` returned.

1. It is a direct message or group DM (always mine).
2. I wrote at least one message in the thread.
3. A message in it names me (`<@me>`), not `@here`, `@channel` or `@everyone`.
4. I follow it (the `l` flag on a thread, or "follow" in the `.` panel).

A thread in Needs reply is a mine thread whose newest message is not mine and is newer than my read mark or my last reply, whichever is later. That is Zulip's "Participated", cut by who has the last word (ledger 2, take 2).

### 7.2 Class, order and a worked example

Class comes from fixed rules, checked in this order. A model never sets it (decision 15).

| Class | Glyph | Rule |
|---|---|---|
| 3, direct question | `◉` | the newest message is from another person and is in a DM or group DM of 3 or fewer people, or names me, and it holds `?` or a request phrase (`can you`, `could you`, `please`, `need you`, `when can`; the list is the profile key `messages.requestPhrases`) |
| 2, review ask | `◎` | not class 3, and the thread links a PR (or issue) that the row store says waits on me (a review requested of me, an issue assigned to me) |
| 1, FYI | `○` | any other mine thread with a new message from someone else; a keyword hit; a mention with no question |

Sort key, in this order: class high first, then `blocks` (Home §4.1, counted over the thread's `about` links, §9.5), then the time of the newest message, oldest first, then the conversation id. Order holds still between settle points, with the New band, exactly as Home §4.3 says.

Messages and Home rank differently on purpose. Home ranks by what a row unblocks across every source. Messages ranks by "who is waiting on a person's answer". A DM from Sam can sit under a review ask on Home and above it here.

**Worked example.** All names and numbers are made up. It is the list at 09:10.

- Sam, DM, 09:02: "can you ship the fare table today?" Class 3 (a `?` in a DM). Blocks 0.
- Ada, `#harbor`, 07:30: "@joi review #3120 when you can". The thread links PR #3120, which waits on me. #3120 closes issue #3055 and PR #3125 is stacked on it. Class 2, blocks 2.
- Pat, DM, 06:00: "thanks!" The newest message is Pat's, no `?`. Class 1, blocks 0.
- Kim, `#harbor`, 08:20, in a thread I started: "ok, 90 s holds then". Class 1, blocks 0.
- Lee, `#general`, 08:55: "@here lunch Friday?" Not mine (`@here`). It shows in Unread only.

| Row | Class | Blocks | Newest |
|---|---|---|---|
| Sam, DM | 3 | 0 | 09:02 |
| Ada, `#harbor` | 2 | 2 | 07:30 |
| Pat, DM | 1 | 0 | 06:00 |
| Kim, `#harbor` thread | 1 | 0 | 08:20 |

Order: Sam, Ada, then Pat before Kim (same class and blocks, Pat is older). At 09:12 Lee's DM "got a sec?" arrives. It lands in the New band, not in the list. The owner replies to Sam at 09:14: Sam's row leaves in place and no other row moves. At 09:16 the owner presses `R` and the New band takes its ranked place (class 3, blocks 0, 09:12: first). This is the golden test `MessageRank_orders_the_example`.

**Done and return.** Done (`e`) leaves Needs reply and writes nothing to Slack. A done thread returns on its own when a new message is addressed to me: a mention, a DM, a reply in a thread I wrote in. The row says "back: new reply" (Home §5.4). A reaction to my message does not return it.

### 7.3 The thread row

A Needs reply row is one thread (or one DM). Its title is the gist (§9.1) and the owner can rename it; the rename is local (`thread_state.title_override`). The facts are: who wrote last, the place, the age, and the linked object chips (a PR number, an issue key). The row id is `slack:thread:<workspace>/<conversation>/<thread ts>` and a DM uses `slack:dm:<workspace>/<conversation>` (Home §3.1 `<source>:<kind>:<source id>`).

## 8. Data, writes, routes and verbs

### 8.1 The store

`messages.db` has its own migrations under `Infrastructure/Messages/Migrations/`. It is a cache and a work queue, not a view of the ledger, so `sindri views rebuild` does not touch it. Losing the file costs a re-sync and the drafts and Later flags in it; the page warns before `sindri messages purge`.

| Table | Holds |
|---|---|
| `workspaces` | id, name, url, my user id, the connection name |
| `conversations` | id, kind (`channel`, `private`, `dm`, `mpim`), name, ext-shared flag, starred, local mute, `last_read_ts`, `latest_ts` |
| `users` | id, names, time zone, title, status, presence, bot flag, deleted flag |
| `messages` | workspace, conversation, `ts`, `thread_ts`, user, text, blocks JSON, subtype, `edited_ts`, reply count, `latest_reply_ts`, a `partial` flag (known only from a search hit), file ids |
| `reactions`, `files` | per message; file metadata and a thumbnail path |
| `thread_state` | per thread: my last ts, follow, Later flag, title override, gist and its cache key |
| `thread_links` | thread to object, kind `about` (made or attached) or `mentions` (a link in the text) |
| `drafts` | id, conversation, thread, text, origin (`owner` or `agent:<run>`), version |
| `outbox` | `actId`, kind, arguments, state, tries, reason, version (§8.3) |
| `sync_cursors` | per conversation: newest ts, the gap list, the replies mark (the idea behind `SlackCursor`) |
| `messages_fts` | FTS5 over message text (unicode61, diacritics removed) |

Done, snooze and mute for a thread live in Home's store, keyed by the row id, so Done in Messages is Done on Home. Retention is `messages.retain.days` (default 90); older messages, their files and their FTS rows are deleted nightly. A Slack delete or edit event changes the local row at once.

Types, all new (no `Aw.Sindri.Core.Messages` exists on main):

- `Aw.Sindri.Core.Messages` (pure, no I/O): `MessageTs`, `Mrkdwn` (parse, render), `MessageClass`, `MessageRank`, `GapMarker`, `ThreadRow`, `SnoozeIntent`.
- `Aw.Sindri.Infrastructure.Messages`: `MessagesStore`, `ISlackClient` with `SlackWebClient` (one `HttpClient` that sends the token only to `slack.com` hosts and follows no redirect), `ISlackLive` with `SocketModeLive` and `PollLive`, `MessagesSyncService`, `MessageOutbox`.
- The fake: `FakeSlackServer` in the Sindri testing project, a loopback HTTP and WebSocket host that speaks the subset in §5.4 plus the 429 and `ok:false` shapes. Contract tests run on it, and on the real Slack only by the owner's hand (like the forge's GitHub check). `deps.yaml` gets one new project edge to check at M1c; Sindri never references `Aw.Connectors`.

### 8.2 Routes

All under `/sindri/messages`, named by M1f (§14). Reads are open to a session with the §11 filter. Every POST and DELETE needs the per-start human token (SND-AUTH-002 for a session) and rejects a browser `Origin`.

| Route | Does |
|---|---|
| `GET /status` | sync path, state, last read, gap counts, scopes granted, the rate-limit words |
| `GET /lists/{needs-reply\|unread\|later\|drafts}?after=&limit=` | the rows of a list, current order, with counts |
| `GET /conversations?starred=&q=` | the conversation list and the jump list |
| `GET /conversations/{id}/messages?before=&after=&limit=` | a window of lines with gap markers |
| `GET /threads/{conversation}/{ts}` | the root, replies, linked rows, `thread_state` |
| `GET /search?q=&scope=local\|slack\|everything` | §9.8 |
| `POST /send` | `{conversation, threadTs?, text, actId, draftId?}` |
| `POST /messages/{conversation}/{ts}/edit`, `/delete`, `/react`, `/unreact` | own-message writes and reactions |
| `POST /conversations/{id}/read`, `/unread` | `{ts}` |
| `PUT /drafts/{id}`, `DELETE /drafts/{id}` | local drafts, with `version` |
| `POST /threads/{conversation}/{ts}/make-issue`, `/attach`, `/later`, `/follow` | §9.5 |
| `POST /schedule`, `POST /status`, `POST /dnd` | M4 |
| `POST /brief` | starts the `MessagesBrief` job |
| `POST /connect`, `POST /purge` | owner only |
| `POST /sindri/links/resolve` | `{urls[]}` (at most 20) to a row id and its fields, or `none` per url (#3021; the route name is api's to confirm) |

Snooze uses Home's `POST /sindri/home/rows/{id}/snooze`, with one added body field, `unlessReplied` (Home §8 lists `{until}` or `{preset}` only; the Home spec needs that line).

### 8.3 Write-through and the outbox

Every Slack write goes through `MessageOutbox`, on Home's pipeline (Home §5.2):

1. **Paint in under 50 ms.** The line appears greyed in the conversation with a local key `~<actId>`. For a reaction, the chip count moves. The text is already in `outbox` on disk.
2. **Send.** The service takes the outbox row in FIFO order per conversation, so a reply never passes the message before it. It calls Slack and writes `messages.act.sent`.
3. **Echo.** Slack's event for the same message can arrive before the HTTP answer. The line matches by `metadata.actId`, then by `ts`, and never shows twice. Test: `Echo_before_response_does_not_duplicate_the_line`.
4. **Within 2 s** the line turns normal and takes Slack's `ts`. After 2 s it says "still sending". It never rolls back by itself.
5. **Failure** (Slack refuses, or 15 s pass): the line stays where it is, greyed, with "failed: <reason>", **Retry** and **Discard**. Retry reuses the `actId`. Before it re-sends, the service looks for a message carrying that `actId` in the thread's history. This costs one history call, and on a 1-per-minute workspace it waits in the bucket **(guess; P0 line 6 proves the metadata round trip)**.
6. **Rate limit.** A 429 keeps the row `pending` with "queued, Slack asked us to wait 12 s".
7. **Offline.** A send queues for up to 10 minutes, shows "queued", and expires to the failed state with Retry. Edit, delete and create-channel refuse at once while offline (SND-MSG-028).
8. **Restart.** A row left `sending` is `unknown` on start. It is looked up by `actId` before any re-send.
9. **Undo** exists only for local state (Done, snooze, mute, a read mark). A sent message has no Undo; the delete action is separate and asks to confirm.

A second action on a line that is still sending is refused: "wait for the first to land" (SND-MSG-006).

### 8.4 CLI and MCP

`sindri messages <verb>` on the CLI, `messages_<verb>` over MCP at `/sindri/mcp`. Every route has a verb, or an owner-only row in a test (`Every_route_has_a_verb_or_an_owner_only_row`, like Reviews).

| Verb | Does | A session may |
|---|---|---|
| `connect [--manifest]` | prints the app manifest, or stores the token from `--key-env` | no |
| `status [--json]` | the status route | yes |
| `list <list> [--max N] [--json]` | rows of a list | counts and ids only for DMs and mentions |
| `show <conversation> [--thread TS] [--before TS]` | lines | no (§11) |
| `search <query> [--scope S]` | search | no |
| `send`, `edit`, `delete`, `react`, `read`, `unread` | writes | no |
| `draft <conversation> --text T [--thread TS]` | writes a draft, sends nothing | yes: the one write a session has |
| `snooze <row> --until T [--unless-replied]`, `done <row>` | Home's verbs | no |
| `make-issue`, `attach` | §9.5 | no |
| `brief [--refresh]` | the catch-up brief | owner only |
| `purge [--conversation C]` | deletes local data | no |

Exit codes follow the context family: 0 ok, 1 attention (a refusal in §10, Sindri not running), 2 tool or usage error.

### 8.5 Profile keys

`messages.workspace`, `messages.sync` (`auto`, `socket`, `poll`; default `auto`), `messages.backfill.days`, `messages.starred` (conversation ids), `messages.keywords`, `messages.requestPhrases`, `messages.sendKey` (`enter`, `ctrl-enter`; default `enter`), `messages.retain.days` (default 90), `messages.gist` (`auto`, `plain`, `off`), `messages.notify.rules`, `messages.away.hours` (default 4), `messages.send.dailyCap` (default 20, §9.7), `messages.files.cacheMb` (default 500). An unknown or out-of-range value is SND-MSG-029 with the key path. They take rows in `docs/csharp/sindri-profile.md`.

## 9. The better-than-Slack set

### 9.1 Gist

A model writes one line, at most 80 characters, for each thread row in the first 30 of Needs reply. The input is the last 10 messages of the thread, fenced. The cache key is `(conversation, thread ts, newest ts, edited ts)`, so an edit or a new reply makes a new gist. Until it arrives, and whenever the model is off or limited, the row shows the first 80 characters of the newest message not mine, in the same line, so nothing shifts. The job reports its token spend like other jobs. `messages.gist: plain` turns the model off. The class never depends on the gist.

### 9.2 Home rows

A collector in the Sindri host (Home §3.3 pattern) raises one Home row per Needs reply thread: kind `dm` or `mention` (both defined in Home §3.2), id as in §7.3, `blocks` from `thread-about` links. It clears the row when the thread leaves Needs reply. Home has no kind for a class-1 thread reply or a keyword hit; this spec asks Home to add `thread-reply` and `keyword`, with reason words "reply" and "keyword". Until then those rows show in Messages only. Done, snooze and mute are Home's records, so the two pages never disagree.

### 9.3 Links as live rows (#3021)

1. The composer and the conversation parse every URL in a message. The renderer sends the URLs (at most 20 per window) to `POST /sindri/links/resolve`.
2. The resolver matches URL shapes to row keys. GitHub PR `https://github.com/<o>/<r>/pull/<n>` to `gh:pr:<o>/<r>#<n>`. GitHub issue `.../issues/<n>`. Linear `https://linear.app/<workspace>/issue/<KEY-n>` to `linear:issue:<KEY-n>`. A Sindri run link to the run's row. The shapes are api's to confirm against the #2978 row model.
3. A hit returns the row's title, state, CI state, who it waits on and age, from the local store. The inline row is 32 px, uses the Home row grammar, stays current because it subscribes to the row, and `Space` peeks it.
4. No hit, or an object the owner's connection cannot read, returns `none`: the link stays plain text. Sindri never shows a row for something the owner could not open (Linear's private-team rule, ledger 11).
5. At most 5 rows per message; the rest read "+3 links". A link inside a code block stays text. A link repeated in one message shows once.
6. Colleagues see Slack's normal unfurl. The live row is the owner's view only.

The resolver needs no Slack data, so it ships first (M1g, api's step 2).

### 9.4 Snooze and follow up (#3020)

One picker, Home §5.3: later today, tomorrow 9:00, Monday 9:00, next week, a typed time. Messages adds a checkbox, `f`: **follow up if no reply**.

- **Plain snooze.** The thread leaves Needs reply and shows "back Mon 9:00" under Snoozed. It returns early when a message addressed to me arrives, and the row says "back early: new reply".
- **Follow up if no reply.** For a thread where I spoke last. The record stores `until` and `myLastTs`. If anyone else posts in the thread after `myLastTs` before `until`, the record is dropped without a toast. The reply puts the thread in Needs reply by the normal rule, so nothing is lost. If nobody posts, the thread returns to Needs reply at `until` with "no reply since Tue" in the age column.
- **Check before it returns.** If the sync state is "Behind" at the due time, the service reads that thread's replies once, so a thread someone answered does not come back.
- **Offered after you send.** A sent message shows a toast for 5 s: "Sent. Follow up if no reply? `h`". The saved filter "Waiting on them" (`/`, or palette `messages.waiting`) lists these threads.
- **Local.** Slack's Later and reminders have no API. The snooze never writes to Slack. Undo is a reverse op within 5 s. Longest snooze is 365 days.

The store and the timer are Home's (#3030). Messages sends the `unlessReplied` field and reads the replied state from its own store. A plain time snooze works for any row before Slack sync ships.

### 9.5 Make an issue, attach a thread

- `m` opens a sheet: title (the gist, editable), team, project (the project whose PR the thread links, else the last used), assignee (me), body (a quote of the root and the last three messages, plus the permalink), label. Create calls the existing Linear create-issue write (the New issue sheet's route), then records a `thread_links` row of kind `about` and writes `messages.thread.linked`.
- `a` opens a picker over issues and PRs (the palette's search) and records the same row without creating anything.
- **Both ways.** The thread row shows the issue key. The issue's peek and the PR's peek show the thread (Projects and Reviews read `thread_links`). A thread with an `about` link counts in `blocks`.
- **Leaves Needs reply.** A thread with an `about` link whose issue has an owner leaves Needs reply, unless a newer message is class 3 (Missive's assign-moves-it-out rule, ledger 10).
- **A link in the text is only `mentions`.** A `mentions` edge shows "Mentioned in 2 threads" on the PR and never ranks, so a stranger's link cannot lift a row.
- **No Slack write by default.** "Reply in the thread with the issue link" is a checkbox, off. Question 4.
- **Save a decision as a note** (`messages.save-note`, with the quote, the author, the time and the permalink) waits on the Notes page (#2981) and shows disabled with that reason until it ships.

### 9.6 Catch-up brief

After `messages.away.hours` (default 4) without the app in focus, a banner offers "Catch up: 9 h away". `B` and the palette id `messages.catch-up` run it any time, and Home's "Since you last looked" can include it.

- **Channels.** Starred channels, DMs, and channels where I was mentioned since I left; at most 12.
- **Per channel.** A facts line from the store with no model ("14 new, 2 mention you, 1 thread with 9 replies"), then at most 3 written lines. Each written line carries chips for its source messages; a chip peeks the message.
- **Link check.** The job drops any written line whose cited message is not in the store. A brief of 40 lines at most.
- **Honesty.** If the store has gaps for a channel, the line says "12 messages missing" and does not summarize what it never read. No history call is made for a brief (the 1-per-minute limit, ledger 9).
- **Failure.** With the model off or limited, the facts lines remain and the page says why.

The job is `MessagesBrief`, one of the Home-brief family: facts first, a model for the sentence only.

### 9.7 Agents on your side

From the engine plan on #2979 (accepted by meta). Both blocks wait on the Slack sync (M1e).

1. **Triggers.** New `connection_event` kinds fed by the sync service from the store: `slack.mention`, `slack.dm`, `slack.thread.reply`, `slack.keyword`. They replace the one-channel poll for these uses. Payload: conversation, author, `ts`, `thread_ts`, text (fenced), permalink. The catalog check (#2987) covers a typo.
2. **"A question about X."** No new block. The template `slack-question-draft` is a trigger, a `prompt` node (`is this a question to me about X?` to a boolean), a `condition`, and a `draft_message` node.
3. **`draft_message`.** Writes a draft into the thread's composer (origin `agent:<run>`), listed in Drafts and as a Needs you row on Home, with approve, edit and drop. The node waits for the owner, like `ask_owner`. Outputs: `sent`, `edited` or `dropped`, and the final text.
4. **`send_message`.** Sends without asking. Activation is refused unless the revision was saved by a human (`saved by human:*`). Every send is recorded on the run and as `messages.send.workflow`. This spec adds three limits **(guesses; the owner confirms in question 5)**: the node names its conversations and sends to no other; at most `messages.send.dailyCap` (20) sends a day per workflow; text with `@channel`, `@here` or `@everyone` is refused (SND-MSG-016).
5. **Test runs** (#2969) record "would have drafted" and "would have sent". No test run posts.
6. **Sessions.** A launched agent session may write a draft (`messages_draft`) and nothing else. It reads no message text unless the owner hands it a thread with "Hand to agent" (Home §5.1 picker), which names what goes along ("thread of 6 messages, 2 linked rows, about 3k tokens").

An agent draft is untrusted output. The composer flags an unedited draft that holds `@channel`, a link, or an attachment, and the first `Enter` opens the submit menu instead of sending.

### 9.8 Search across everything

`/` in Messages, or `Ctrl+K` then a query. Two scopes.

| Scope | Source | Speed |
|---|---|---|
| Messages | local FTS5 first; `Enter` on "Search Slack too" adds `search.messages` results tagged "from Slack" | local under 100 ms; Slack Tier 2, at most 3 calls a minute **(guess; P0 line 4)** |
| Everything | Messages, issues, PRs, notes and code scope, in groups of 5 | each group as fast as its own store |

Operators are Slack's words so nothing is relearned: `in:`, `from:`, `to:`, `with:`, `has:link`, `has:file`, `has:reaction`, `before:`, `after:`, `on:`, `during:`, `is:saved`, `is:thread`, quotes, `-` to exclude, `*` wildcard from 3 characters. Zulip's add `is:unread` and `near:<ts>`. `is:done` replaces Zulip's `is:resolved` (Slack has no resolved state). Cross-source adds `pr:`, `issue:`, `note:`, `code:`. A bad operator is SND-MSG-012 with its position. Everything shows groups, not one fused score: a fused rank would hide why a note outranks a message.

A Slack hit that the store lacks is cached as a `partial` message so it opens in the peek. Search only returns conversations the owner is in.

### 9.9 Notifications, mute and "why is this here"

- A toast and the tray count come from class 3 and class 2 rows, once per row per episode. Class 1 never toasts. A `messages.notify.rules` entry sets a conversation to `all`, `mentions` or `none`, or adds keywords.
- **Mute** (Zulip's rule, ledger 12): it hides the conversation from Unread and silences toasts; a DM or a mention still breaks through; a followed thread overrides a muted channel.
- Every row has a reason chip that names the rule ("mention", "your thread", "keyword: fare", "DM"). The tooltip says which rule put it there.
- Slack's own phone and desktop pushes are untouched. The ledger row says "differs".

## 10. Errors

Reserved now: SND-MSG-001 to 040 (no `SND-MSG-` code exists on main). They go in `Core/SindriErrors.Messages.cs`. The app shows each sentence first and the code in small text. The CLI exits 1 for 005, 007, 021 and 023 (a retry can work) and 2 for the rest. The reason text in 004, 007 and 019 comes from Slack and is fenced.

| Code | Meaning | HTTP | fix: |
|---|---|---|---|
| 001 | no Slack connection for this workspace | 409 | `sindri messages connect` |
| 002 | the token is revoked or invalid (`invalid_auth`, `token_revoked`) | 401 | make a new token, `sindri messages connect` |
| 003 | the token lacks a scope; carries the scope name; the feature is off | 403 | add the scope in the Slack app, reinstall, reconnect |
| 004 | Slack refused a write for a permission or rule (`cant_update_message`, `not_in_channel`, `restricted_action`); carries Slack's reason | 403 | read the reason; join the channel or ask an admin |
| 005 | Slack rate-limited the call; carries seconds to wait; the write is queued | 429 | wait; nothing to do |
| 006 | a second action on a line still sending | 409 | wait for the first to land |
| 007 | a write failed after 15 s or expired after 10 minutes; Retry and Discard | 502 | Retry, or `sindri messages send` again |
| 008 | the message changed since it was read (edited, deleted) | 409 | reload; repeat the action |
| 009 | the text is over Slack's limit; carries the limit | 413 | shorten it, or split it |
| 010 | a file is too large or its type is refused | 413 | pick another file |
| 011 | the conversation is not in the store | 404 | `sindri messages status`; open it once to load |
| 012 | a search operator is invalid; carries its position | 400 | fix the operator |
| 013 | snooze in the past, or over 365 days | 400 | pick another time |
| 014 | "follow up if no reply" on a thread where I never wrote | 400 | snooze without `unless-replied` |
| 015 | the draft changed in another window | 409 | reload the draft |
| 016 | a workflow send names a conversation the node did not list, holds a broadcast, or is over the daily cap | 403 | edit the workflow; a human saves the revision |
| 017 | make-issue refused: no Linear write key (SND-LINEAR-002 carried) | 403 | add the Linear write key |
| 018 | attach refused: the object is not one the owner can read | 403 | pick another object |
| 019 | the workspace blocks the Slack app, or a scope needs admin approval; carries Slack's reason | 403 | ask the admin to approve the app |
| 020 | info: Socket Mode is not available for user events; polling is in use | n/a | none (doctor shows it) |
| 021 | history is limited; a gap stays; carries the count | 200 | Load older, one per minute |
| 022 | `purge` needs `--yes` | 400 | repeat with `--yes` |
| 023 | the CLI could not reach Sindri; nothing was sent | CLI only | `sindri serve`, repeat |
| 024 | the token belongs to a different workspace than the connection | 409 | connect the right token |
| 025 | `messages.db` is newer than this build, or damaged | 500 | `sindri messages doctor`; `purge` rebuilds the cache |
| 026 | a schedule time is past, or over Slack's limit | 400 | pick another time |
| 027 | a channel name breaks Slack's rules | 400 | use lowercase letters, digits, `-` and `_` |
| 028 | the action needs the network and Sindri is offline | 503 | reconnect, repeat |
| 029 | a `messages.*` profile key is unknown or out of range; carries the key path | n/a | `sindri profile set` |

## 11. Security

- **The token.** Stored in the keychain through `IConnectionSecrets`, never in `messages.db`, an argument list, a log, an error, a ledger row or a response. `Aw.Redaction` scrubs the patterns `xoxp-`, `xoxb-`, `xoxc-` and `xapp-` from every log and error. `sindri doctor` warns when a token variable is set in a session's environment. The Step sandbox empties it.
- **Hosts.** `SlackWebClient` sends the token only to `slack.com` and its subdomains and `files.slack.com`, over https, with redirects off. Emoji and thumbnails load from Slack's CDN hosts with a size cap of 10 MB each, through Sindri, never from a message's own `<img>` or remote URL.
- **Text is untrusted.** Names, channel names, messages, topics, status text and file names show as plain text or the `Mrkdwn` subset: no HTML, no remote images, links shown as text with the host visible, bidi and control characters stripped. Sindri opens a message's URL only on the owner's click, and the live-row resolver only reads the local store.
- **Prompt injection.** Any message that reaches a model goes through `FencedPrompt` as data. The gist, the brief and the engine's `prompt` node get fenced text. A model's output is plain text and becomes a Slack message only by the owner's click or a revision a human saved (decision 14). Slack Connect messages (another org's) are untrusted by default and reach a model only through the owner's hand-off.
- **Sessions see counts.** All Messages data is repo-less. A session gets ids, kinds and counts for the lists. Bodies go to the owner. `GET /sindri/ledger` and `/sindri/events` carry ids and counts only (decision 2).
- **Writes are human-only.** Every POST needs the per-start human token. The one exception is a `send_message` node on a human-saved revision, bounded by §9.7.
- **Local data.** `messages.db`, `drafts` and the file cache are 0600 under `$AW_SINDRI_HOME`. Retention is `messages.retain.days`. `sindri messages purge` deletes by conversation or all. The owner reads the company's rule on storing chat on the work laptop (question 8).
- **The app-level token** `xapp-` can open a Socket for the whole app. It sits in its own keychain entry and is used only by `SocketModeLive`.
- **Fixtures are made up.** Tests use the fake Slack; the probe prints counts only.

## 12. Goals and non-goals

Goals: everything in §3 that is not `dropped`; the lists and conversation read from the local store in under 100 ms; the owner's Slack opens fall to 5 a week (bet 1).

Non-goals:
- Running a Slack workspace: admin, billing, user management, channel archive.
- Huddles, calls, canvases, Lists and Slack's Workflow Builder (§3, rows 26, 42, 43).
- Several workspaces in Release 1.
- Mobile. The rows stay narrow-safe, as Home's do.
- A Slack bot or app other people use. Colleagues never see Sindri.
- Writing to Slack from a session, a test run, or a workflow with no human-saved send node.

## 13. Gaps found (the guesses to prove)

These are in this spec as guesses. The five riskiest come first. Each names the probe line that proves it and what changes if it is wrong.

| # | Guess | If wrong | Probe |
|---|---|---|---|
| 1 | A user token can open Socket Mode and receive the owner's DM and channel events. Slack's page shows `bot_events` only. | Polling only: bet 2 moves to 30 s on Tier 3, and to search polling on a limited workspace. | line 5 |
| 2 | The owner's Slack app keeps Tier 3 history (50 or more per minute). Slack's 2025 change gives new non-Marketplace apps 1 per minute and 15 messages. | No backfill, no hot-set polling; live events or search polling only; gap markers everywhere (W10). | line 3 |
| 3 | The workspace lets the owner install a user-scope app, including `im:history` and the `*:write` scopes. | There is no token and no Messages page. | lines 1, 8 |
| 4 | `search.messages` with `to:me` works as a cheap change detector for DMs and mentions (3 calls a minute). | No live path on a limited workspace without Socket Mode. | line 4 |
| 5 | Interactive buttons and slash commands cannot be sent with a user token (rows 27, 29). #2979 says buttons work. | If they work, rows move to M4 and W6 is stricter; if not, they stay `blocked` and need an owner-accepted drop. | line 7 |
| 6 | `chat.postMessage` keeps `metadata.event_payload` for user tokens and returns it in history and events, so a retry never posts twice. | A retry may post twice. The fallback is to compare text and time, which can miss. Retry then asks the owner first. | line 6 |
| 7 | Channel unread counts can be computed from `last_read` plus the store. Slack documents `unread_count` for DMs only. | Unread counts for channels are wrong until opened. | line 2 |
| 8 | Slack's attachment and thumbnail URLs load with the token header. | Images need a different fetch; M4 grows. | line 6 |

Other findings from reading `origin/main`:

- `PollableEvents.cs` lists only `slack.message` and `slack.mention`, and `SlackEventPoller` reads ONE channel. The four triggers in §9.7 need the store, not a per-channel poller.
- `SlackChannelIds` and the context reader refuse DMs on purpose, and the reader uses a bot token. Messages is a second Slack client, not an extension.
- Home's row kinds have no class-1 thread reply or keyword (§9.2). Home's snooze body has no `unlessReplied` (§8.2).
- The row store that #3021's lookup reads is #2978's model. On main, `rows.db` does not exist yet. M1g may start on a stub store and read the real one when it lands.
- `scripts/slack-probe.sh` does not exist on main. api's plan names it.

## 14. Phases and parallel plan (for /specToProvenPR)

Size is a rough guess: S under 1 day, M 1 to 3 days, L over 3, with the PR count. Each phase names its first red test. The last column marks Release 1 (§1). "ledger" in the last words of a phase means its parity rows turn from `planned` to `done` with an evidence path.

| Phase | What | Lane | Needs | First red test (proves) | Size | R1 |
|---|---|---|---|---|---|---|
| M0a | `scripts/slack-probe.sh` with lines 1 to 8 (§5.5), a fake answer set, `.env.example` rows | api | none | `Probe_prints_counts_and_never_a_name` | S, 1 | yes |
| M0b | `docs/parity/slack.md` from §3; `scripts/check-parity.sh` (W6) | ideas | none | `Parity_check_fails_on_a_bare_row` | S, 1 | yes |
| M0c | The owner runs the probe; ideas writes the results into §3, §5 and §13 | owner, ideas | M0a | none: the day-0 go/no-go (question 1) | S | yes |
| M1a | `FakeSlackServer`: Web API subset, Socket Mode, 429 and `ok:false`, `metadata` echo | api | none | `Fake_slack_echoes_metadata_on_post` | M, 1 | yes |
| M1b | Core, pure: `Mrkdwn`, `MessageTs`, `MessageClass`, `MessageRank`, `GapMarker`, `ThreadRow` | api | none | `MessageRank_orders_the_example`; `Mrkdwn_round_trips_the_golden_set` | M, 2 | yes |
| M1c | `messages.db`, migrations, FTS5, retention, `deps.yaml` edge check | api | none | `Needs_reply_rebuilds_in_50ms_at_5000_threads` (W7) | M, 1 | yes |
| M1d | `ISlackClient`, `SlackWebClient`, token buckets, `sindri messages connect` and `--manifest`, SND-MSG-001 to 003, 005, 019, 024 | api | M1a; scopes from M0c | `Rate_limit_429_pauses_only_that_method` | M, 2 | yes |
| M1e | Sync: `PollLive`, `SocketModeLive` behind `ISlackLive`, gap markers, status words | api | M1c, M1d; the live path from M0c | `Gap_without_marker_fails` (W10); `Message_appears_within_30s_on_the_poll_path` (W16) | L, 3 | yes |
| M1f | Read routes, lists, conversation, thread, users, local search, `status`; the read verbs | api | M1c, M1e | `ApiGoldens_messages_lists` | M, 2 | yes |
| M1g | `POST /sindri/links/resolve` | api | none (stub row store) | `Resolve_github_pr_url_to_row_key` | M, 1 | yes |
| M1h | Snooze with `unlessReplied`, built under #3030 | api | #3030 | `Unless_replied_snooze_drops_when_answered` (W9) | M, 1 | yes |
| M1i | Home collector `messages`: `dm` and `mention` rows, `thread-about` edges | api | M1f; Home S0 routes | `Collector_raises_dm_row_once_per_episode` | M, 1 | yes |
| M1j | Frames (§15) | design | Home's tokens | the numbers in §6 | M, 1 | yes |
| M1k | Rail, the four lists on `RowList`, `Row`, `PeekPane`; counts; `Ctrl+K` and `n` | ui | M1j; stub routes | `Needs_reply_paints_200_rows_in_100ms` (W1) | M, 2 | yes |
| M1l | Conversation: dense lines, virtualized, gap markers, collapsed threads, reaction chips, code and diff | ui | M1j, M1b, M1f | `Short_messages_group_by_author_in_one_line_each` (W4, W5) | L, 3 | yes |
| M1m | Peek: thread, inline link rows, the linked object | ui | M1l, M1g | `Link_row_paints_with_no_network_call` (W8) | M, 2 | yes |
| M2a | Outbox, `send`, reply, edit, delete, react, read and unread routes; SND-MSG-004 to 009 | api | M1d, M1e | `Failed_send_keeps_the_line_and_retry_lands_once` (W3); `Echo_before_response_does_not_duplicate_the_line` | L, 3 | yes |
| M2b | Drafts routes and autosave | api | M1c | `Draft_saves_after_3_characters` | S, 1 | yes |
| M2c | Composer: send, optimistic line, retry, formatting, `@ # :`, `[[` | ui | M2a, M2b, M1b | `Enter_sends_and_Shift_Enter_adds_a_line` | L, 3 | yes |
| M2d | Message-mode keys: edit, react picker, delete confirm, forward, save | ui | M2a, M1l | `E_edits_only_own_messages` | M, 2 | yes |
| M2e | Snooze picker, "follow up if no reply", the "back at" line, the post-send toast (#3020) | ui | M1h, M1k | `Follow_up_toggle_stores_my_last_ts` | M, 1 | yes |
| M2f | The write verbs and MCP tools | api | M2a, M1f | `Every_route_has_a_verb_or_an_owner_only_row` | M, 1 | no |
| M3a | `MessageGist` job, cache, plain fallback | engine | M1c | `Gist_falls_back_to_80_chars_when_the_model_is_off` | M, 1 | no |
| M3b | Make issue, attach, `thread_links`, the sheets, Done-leaves rule | api, ui | M1i; Linear create (#2961) | `Make_issue_links_the_thread_both_ways` | M, 2 | no |
| M3c | `MessagesBrief` job and UI | engine, ui | M1f, M3a | `Brief_line_citing_a_missing_ts_is_dropped` (W11) | M, 2 | no |
| M3d | Engine triggers from the store, `draft_message`, `send_message` guard | engine | M1e, M2b; #2960's test run | `Send_message_activation_is_refused_without_a_human_revision` (W12) | L, 3 | no |
| M3e | Unified search: operators, Slack second pass, the Everything scope | api, ui, context | M1f | `Operator_parse_golden` (W13) | M, 2 | no |
| M3f | perf: W1 and W5 baseline on the seeded fake now; recheck after M1l | perf | none | `Scroll_p95_under_16ms_at_10000_lines` | M, 1 | yes |
| M4a | Files: view, upload, thumbnail cache | api, ui | M2a | `Upload_two_step_completes_once` | L, 3 | no |
| M4b | Scheduled send | api, ui | M2a | `Scheduled_list_shows_only_sindri_made_items` | M, 1 | no |
| M4c | Notification rules, toasts, tray count, keywords | api, ui | M1i | `Mention_breaks_through_a_muted_channel` | M, 2 | no |
| M4d | App messages: the Block Kit subset | ui | M1l | `Unknown_block_shows_text_and_open_in_slack` | M, 2 | no |
| M4e | Status, DND, presence read, profile peek | api, ui | M2a | `Status_write_reaches_the_fake` | M, 2 | no |
| M4f | Channel create, join, leave; local mute; pins; forward | api, ui | M2a | `Local_mute_never_calls_slack` | M, 2 | no |
| M5 | Save a decision as a note | ui | Notes (#2981) | `Save_note_carries_quote_and_permalink` | S, 1 | no |
| X1 | ux: a week of Slack-open counts and timing of 10 fixed jobs in Slack, now | ux | none | the timing sheet | M, 1 | yes |
| X2 | ux: the day without Slack, after M2e; each Slack open is an issue (W14) | ux | M2e (and M4 for the full pass) | the scripted-day sheet | M, 1 | yes |
| Q1 | qa: injection, bidi, a 429 storm, a revoked token, echo-before-response, offline expiry (W3, W10, W15) | qa | M2a | `Hostile_fixture_set_stays_plain_text` | M, 2 | yes |

**Phase graph.** Start now: M0a, M0b, M1a, M1b, M1c, M1g, M1j, M3f's baseline and X1. Nothing waits on Slack for those.

1. M0a -> M0c. The owner's run picks the live path in M1e and the scopes in M1d. M1a and M1c need no probe.
2. M1a + M1c -> M1d -> M1e -> M1f -> M1i.
3. M1b + M1j + M1f -> M1k, M1l -> M1m. M1k starts on stub routes the day M1j merges, with a golden of the list.
4. M1d + M1e -> M2a -> M2c, M2d. M1c -> M2b -> M2c. M1h + M1k -> M2e.
5. M1f + M2a -> M2f. M1c -> M3a -> M3c.
6. M2a -> M4a to M4f in parallel (disjoint files, one lane each).
7. M2a -> Q1. M2e -> X2.

Serialize M1f, M2a and M3b on `EndpointTable.cs`. Early dogfood: when M1k and M1l merge, the owner reads real Slack read-only beside the Slack app and files each gap as an issue, `Part of #2979`, before X2. Nothing waits on a source that does not exist: the links return `none` before M1g; Make issue shows disabled before M3b; the composer is read-only before M2a.

**Gates per phase.** Every phase: the `-warnaserror` build, the touched projects' lanes and scoped format (per project). M1c to M2f also run the ApiGoldens classes, `EndpointCqrsTests`, `check-env-example.sh` (the probe adds `AW_SLACK_PROBE_KEY` and `AW_SLACK_PROBE_APP_KEY`), `Aw.Architecture.Tests` and `node scripts/check-deps.mjs`. M1e and M2a add `EventCatalogTests`. A doctor row change (`messages-sync`, `messages-token`) updates `src/Sindri/aot-smoke/expected.txt` and runs aot-smoke. M1k to M2e that add views run `Aw.Sindri.Ui.Frames.Tests` integration. Any change to `tests/Aw.Sindri.Testing` fakes runs the fake's own tests.

**Docs to update per phase:** new `docs/sindri/messages.md`; `docs/parity/slack.md`; `docs/csharp/sindri.md` (CLI table, errors); `docs/csharp/sindri-ui.md` (the page, keys); `docs/csharp/sindri-profile.md` (the `messages.*` keys and a "Connect Slack" section: the manifest, the token steps, the scopes); `docs/sindri/connections.md` (the Slack row stops saying "REST only").

## 15. Frames for design (M1j)

1. Needs reply with the peek open at 1,440 px: 25 rows, the three class glyphs, a reason chip, buttons on the cursor row, the New band.
2. The dense conversation: a busy channel, a DM, a thread in the peek with an inline link row, a code block, a diff block, reaction chips, the New divider, a gap marker.
3. The composer: empty, typing with a `[[` picker open, an agent draft with its label and flags, a failed send, a queued send.
4. The snooze picker with the "follow up if no reply" checkbox, and the "back Mon 9:00" row line.
5. The make-issue sheet and the attach picker.
6. The catch-up brief with source chips, and its model-off state.
7. Search: Messages scope with "from Slack" tags; the Everything scope in groups.
8. First run: connect in three steps (create the app from the manifest, paste the token, scope check), then the first sync.
9. Header states: Live, Behind with the rate-limit words, Offline, Token expired, Scope missing.
10. A user peek, a file chip and thumbnail, an app message with blocks, "Open in Slack" for a huddle.
11. Dark frames of 1 and 2; the focus ring on a row, a message line and the composer.
12. Widths 1,440, 1,200 and 1,000 px; the rail collapsed.

## 16. Questions for the owner

Each has my recommended pick. Question 1 blocks everything and is a day-0 gate.

1. **Can you install a Slack app with user scopes on the work workspace** (`im:history`, `mpim:history`, `channels:history`, `chat:write`, the `*:write` read-mark scopes, `search:read`)? An admin may need to approve it. Pick: run the P0 probe's line 1 and line 8 today. If an admin blocks it, Messages cannot ship, and the only fallback is to ask for the approval. Tell me which scopes it allows and §5.1 adapts.
2. **One workspace or several?** Pick: one for Release 1; `messages.workspace` names it.
3. **Which keys inside a conversation?** Home's (`e` is Done) or Slack's (`e` is Edit)? Pick: two sets by mode (decision 11), with Done and snooze on `Shift+` keys in message mode. Muscle memory carries over, and the footer names the live set.
4. **Should "make issue" also reply in the Slack thread with the link?** Linear's Slack app does. Pick: no by default, a checkbox per use. A reply shows colleagues that you changed tools.
5. **Agent send limits.** Is "named conversations, 20 a day, no broadcasts" (§9.7) the right fence for `send_message`? Pick: yes, and `slack-question-draft` ships off. You write the rule by saving the workflow.
6. **How much history to keep and load?** Pick: 14 days of backfill for DMs, starred channels and your threads, 3 days for the rest, only when the probe shows Tier 3. Keep 90 days of text. On a limited workspace there is no backfill.
7. **Also create a Slack reminder when you snooze?** `reminders.add` would DM you from Slackbot. Pick: no. Sindri's snooze is the only reminder, so there is one inbox.
8. **Is storing Slack messages on the work laptop allowed by your company?** `messages.db` holds DMs and other orgs' Slack Connect text. Pick: ask; if unsure, ship with `messages.retain.days: 30` and no Slack Connect text stored.
9. **Should Done on a thread also close the Linear issue it links?** The ledger notes this as a win over Slack. Pick: no for Release 1. Done is local; closing the issue is its own action in the `.` panel.

### Review notes

**First draft (ideas, 2026-11-12).** No review round has run.
