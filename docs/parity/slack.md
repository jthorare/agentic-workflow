# Slack parity ledger

What this is: every Slack function a staff engineer uses in a normal day, scored against what the Sindri Messages page does. It is the Slack half of the parity contract in #2979, copied from §3 of `docs/superpowers/specs/2026-11-12-sindri-messages-page-design.md` (merged in #3188). Statuses and limits are `agent default` unless a row names #2979.

The contract, in 3 lines:
1. Each row has a status with its detail. A row with a status and no detail is a bare row, and `scripts/check-parity.sh docs/parity/slack.md` fails on it (W6). No row is silently absent: the rows run 1 to 56 with no gap.
2. Writes go through to Slack. Slack stays the system of record; read marks, Later, drafts and mutes are local unless a row says otherwise.
3. Release 1 ships when `scripts/check-parity.sh --release docs/parity/slack.md` passes: every row is `done`, `partial` with its limit, `missing #N` or `dropped: why`.

Status words, and the detail each one needs:

| Status | Means | Needs |
|---|---|---|
| `done` | built and checked | a file path or `#N` in Evidence |
| `partial` | built with a limit | the limit in "How, and the limit" |
| `differs` | works, but Sindri-local | the limit in "How, and the limit" |
| `planned Mn` | a phase in the spec's §14 | the phase id |
| `missing #N` | a gap with its issue | the issue number |
| `blocked` | the P0 probe must prove it | "P0 line N" in "How, and the limit" |
| `dropped: why` | not built | the reason |

Several statuses join with commas: `partial, planned M1`. A `blocked` row needs an owner-accepted drop, or a probe result that moves it to a phase, before `--release` passes. Rows 27 and 29 are the two where this ledger may contradict #2979's text.

How to update a row: when a phase ships, the Status becomes `done` and Evidence gets the file path or issue. Gap issues are filed at the start of each phase, `Part of #2979`. If you could not check it, leave the old status; never guess.

| # | Function | How, and the limit | Status | Evidence |
|---|---|---|---|---|
| 1 | Channels: public, private; read, jump | `users.conversations`, `conversations.history`; the list is local | planned M1 | |
| 2 | Direct messages | needs `im:history`; a bot token cannot (§5.1) | planned M1 | |
| 3 | Group DMs | `mpim:history` | planned M1 | |
| 4 | Threads: read, reply | `conversations.replies`; reply writes `chat.postMessage` with `thread_ts` | planned M1, M2 | |
| 5 | Threads view / Activity (threads you are in, mentions) | Slack has no activity API; Needs reply is built from the store (§7.2) | differs, planned M1 | |
| 6 | Mentions | `<@U...>` in text; live event or search poll | planned M1 | |
| 7 | Keyword alerts | local list `messages.keywords`; FYI rows | planned M4 | |
| 8 | Reactions: see, add, remove | `reactions.add` / `.remove`, `reactions:write` | planned M1, M2 | |
| 9 | Edit own message | `chat.update`; own messages only | planned M4 (Release 3) | |
| 10 | Delete own message | `chat.delete`; confirm in the UI | planned M4 (Release 3) | |
| 11 | Rich text: bold, italic, strike, lists, quotes | `Mrkdwn` parser in Core; golden tests | planned M1 (render), M2 (compose) | |
| 12 | Code blocks and inline code | rendered mono; diffs get +/- colour | planned M1 | |
| 13 | Emoji and custom emoji | `emoji.list` cached; Unicode names built in | planned M1 | |
| 14 | Files and images: view | fetched with the token to `files.slack.com`; disk cache capped | planned M4 | |
| 15 | Files: upload | `files.getUploadURLExternal` + `completeUploadExternal`, `files:write` | planned M4 | |
| 16 | Search with Slack's operators | §9.8; Slack search is the second pass | planned M1 (local), M3 (Slack second pass, cross-source) | |
| 17 | Unread counts | Slack gives `unread_count` for DMs only; channels are counted from `last_read` and the store **(guess; P0 line 2)** | partial, planned M1 | |
| 18 | Mark read, mark unread | read marks are a local pointer; `conversations.mark` and the `*:write` scopes are used only with `messages.syncReadToSlack` or the `.` panel action, and only while online; "mark unread" sets the local pointer to the message before and never writes. The owner's phone keeps its own state until then | differs, planned M1 (local), M2 (opt-in sync) | |
| 19 | Later / Saved | Slack has no API (`stars.list` is dead); Later is a local flag; saves made in Slack do not appear | differs, planned M1 | |
| 20 | Drafts | no drafts API; local drafts, autosave after 3 characters; Slack's own drafts stay in Slack | differs, planned M2 | |
| 21 | Scheduled send | `chat.scheduleMessage`; the list shows only messages scheduled from Sindri (`chat.scheduledMessages.list` limit) | partial, planned M4 | |
| 22 | Status | `users.profile.set`, `users.profile:write` | planned M4 | |
| 23 | Presence | read others' presence for DM partners (`users.getPresence`, rate limited); setting own presence **(guess; blocked until P0)** | partial, planned M4 | |
| 24 | Do not disturb | `dnd.setSnooze`, `dnd.endSnooze`, `dnd.info` | planned M4 | |
| 25 | Notification rules per channel | Slack has no API; Sindri rules apply to Sindri's toasts only; Slack's phone pushes are untouched | differs, planned M4 | |
| 26 | Huddles and call links | #2979 names this drop | dropped: a user token cannot join a huddle; the link opens in Slack | |
| 27 | Slash commands | no documented Web API to run a user's slash command **(guess; P0 line 7)**; fallback: "Open in Slack" | blocked | |
| 28 | App messages and blocks: render | Block Kit subset: section, context, divider, image, actions; unknown blocks show as text plus "Open in Slack" | planned M4 | |
| 29 | App messages: interactive buttons | a click is sent by the Slack client to the app; a user token has no documented way to send it **(guess; P0 line 7)**. #2979 says buttons work: this is the biggest claim the probe may kill. Link buttons open the URL | blocked | |
| 30 | Slack Connect channels | Release 1: a title-only row (sender and place) with "Open in Slack"; the text is not stored (`messages.connect.store: off`) and is read live and held in memory only while the conversation is open; inviting an outside org is dropped: admin-only | partial, planned M1 | |
| 31 | Channel: create, join, leave | `conversations.create`, `.join`, `.leave`; scopes `channels:write`, `groups:write` | planned M4 | |
| 32 | Channel: mute | no API; local mute (§9.9) | differs, planned M4 | |
| 33 | Channel: archive, rename, set topic |  | dropped: admin or owner rights, rare; open in Slack | |
| 34 | User profiles | `users.info`, `users.profile.get`, cached; the peek shows name, title, local time, status | planned M1 | |
| 35 | Jump-to (`Ctrl+K`) | the #2978 palette with channel and person targets | planned M1 | |
| 36 | Keyboard set | §6.5 | planned M1, M2 | |
| 37 | Pins | `p` opens a Pin / Cancel sheet; `pins.add`, `.remove`, `.list` | planned M4 | |
| 38 | Forward / share a message | `s` opens the composer prefilled with the permalink; the send is the write | planned M4 | |
| 39 | Remind me about this | a Sindri snooze on the thread (§9.4); `reminders.add` is not used so no Slackbot DM appears **(guess: owner may prefer both; question 7)** | differs, planned M1 | |
| 40 | Copy link to message | `SlackPermalink` pattern from the workspace name | planned M1 | |
| 41 | Bookmarks in a channel header | low value | dropped: read-only in Slack's header; shown as plain links in the channel peek if `bookmarks.list` works | |
| 42 | Canvases and Lists | no stable user API in the probe plan | dropped: a link opens in Slack | |
| 43 | Slack's own workflows (Workflow Builder) |  | dropped: Sindri workflows replace them | |
| 44 | Message formatting preview, link unfurl previews | links to known objects render as live rows; other links show their title only | partial, planned M1 | |
| 45 | Multiple workspaces | question 2 | dropped: one workspace in Release 1 | |
| 46 | Also send to channel (thread reply) | `reply_broadcast` on `chat.postMessage`; a broadcast, so the submit menu guards it | planned M4 | |
| 47 | Channel details: members, topic, description, files list | `conversations.info`, `.members`; read-only; topic and description set are dropped (row 33) | partial, planned M4 | |
| 48 | User groups (`@team` mentions) | `usergroups.list`; render and pick; `usergroups:read` | partial, planned M4 | |
| 49 | Typing indicators | low value | dropped: needs Socket Mode user events, shows nothing a reply does not | |
| 50 | Mark all read; jump to first unread; jump to date | local pointer moves (row 18); Mark all read is a footer chip and the palette entry `messages.mark-all-read`, not a key | planned M1 (jump to first unread `n`; local Mark all read; `e` in Unread), M2 (opt-in sync to Slack), M4 (jump to date) | |
| 51 | Emoji skin tone; custom-emoji search | `emoji.list` cache (row 13) | planned M4 | |
| 52 | Copy message text | local clipboard; no Slack call | planned M1 | |
| 53 | Slack DND silences Sindri's toasts | reads `dnd.info`; Sindri obeys it (row 24 sets it) | planned M4 | |
| 54 | Sidebar sections and sorting | decision 6 | dropped: the four smart lists and starred channels replace them | |
| 55 | Slack Connect invite acceptance | row 30 | dropped: admin-only | |
| 56 | Mute thread (turn off notifications for one thread) | local mute on `thread_state`; no API | differs, planned M4 | |
