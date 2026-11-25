# Prior art: Messages (a Slack replacement that triages to zero)

This file is the prior-art ledger for the Messages surface of Sindri (rule: #2989). It lists the small daily jobs of a chat inbox, who solves each best today, what we copy, and what Sindri does on origin/main.
It was written 2026-11-12 from primary docs and READMEs read that day. A claim from a vendor's own page is marked "vendor claim". A thing I could not confirm is marked "unconfirmed".
Surface job: let the owner clear every message that needs them (reply, react, snooze, make a task) in minutes, from a local store, while Slack stays the system of record.
Direction issue: #2979. Related: #2978 (row/peek/full grammar, Home as the one inbox), #2989 (ledger rule), #2960 (engine plan for message triggers and draft replies).

Licence note for builders: slack-term, wee-slack: MIT, we may copy ideas and code with notice. gomuks and mautrix/slack: AGPL-3.0, ideas only. Zulip's web app is Apache-2.0 (unconfirmed this pass; check before copying code). Slack, Discord, Superhuman, Missive, Beeper, Linear: closed, UI ideas only.

Hard facts that shape every row (read first):
- Socket Mode needs an app-level token (`xapp-`), works only for apps with granular permissions, and apps using it are not allowed in the Slack Marketplace. Examples on the page use a bot token; user-token use is not shown (https://docs.slack.dev/apis/events-api/using-socket-mode). So P0 must prove what a user token receives live. If not enough: poll.
- Rate limit: since 2025-05-29, `conversations.history` and `conversations.replies` are 1 request per minute with `limit` max/default 15 for non-Marketplace apps created after that date and for new installs. Internal customer-built apps created before that date keep their old limits. Dates for existing installs differ between Slack pages (2025-09-02 vs 2026-03-03). https://docs.slack.dev/changelog/2025/05/29/rate-limit-changes-for-non-marketplace-apps and https://api.slack.com/changelog/2025-05-terms-rate-limit-update-and-faq . The Sindri sync design cannot assume history pulls; live events must fill the store, and backfill is slow. This is the biggest risk in #2979 and belongs in the P0 probe.
- Slack's saved items ("Later") have no API: `stars.list` still lists, but the page says stars "can no longer be viewed or interacted with by end-users" and Later APIs are "not currently available" (https://docs.slack.dev/reference/methods/stars.list). No drafts API found on the methods index (https://docs.slack.dev/reference/methods). `chat.scheduledMessages.list` returns only messages scheduled through the API with the same token, not ones scheduled in the Slack UI (https://docs.slack.dev/reference/methods/chat.scheduledmessages.list). `conversations.info` documents `unread_count` for DMs only (https://docs.slack.dev/reference/methods/conversations.info). There is no activity-feed method. So Sindri's Later, drafts and "needs reply" lists are Sindri-local state, not synced with Slack. Write that into the parity ledger as "differs", not "done".

---

## 1. Clear everything that needs me, to zero, in two minutes

**Best today**
- Superhuman (email, same job). Triage rule from its help: leave in inbox if it needs action today, Remind Me if another day, Done if none. E = Done, H = Remind Me, Cmd/Ctrl+K opens the command menu. https://help.superhuman.com/article/582-achieving-inbox-zero and https://help.superhuman.com/article/478-superhuman-command . Vendor claims: "twice as fast", "save 4 hours every week" (https://superhuman.com/mail). Not verified. source: docs
- Zulip Inbox: lists only conversations with unread messages; Shift+I opens it; filter All topics / Standard / Followed. https://zulip.com/help/inbox source: docs
- Beeper / Texts (now one product): Texts' page lists "Archive conversations ... to hit inbox zero", "Snooze Messages", "Mark as unread" (vendor claims). https://texts.com/ . Beeper's own pages: reminders and Send Later are in the paid Plus tier ($9.99/month per FAQ). https://www.beeper.com/faq source: docs
- Slack Activity view: one place for mentions, threads you are in, and reactions to your messages; Ctrl+Shift+M on Windows/Linux, desktop app only. No filters or retention on the page I read. https://slack.com/help/articles/360059928654 (served the quick-start guide; the Activity article itself, 19693583638803, was linked and not read). source: none, unconfirmed

**What we take**
- Superhuman's three-way decision per row: Done (E) / Remind (H) / stays. One key each, the row leaves the list at once, the next row takes focus (no mouse, no confirm).
- Zulip's rule that the inbox holds only things with something unread. Our "Needs reply" list holds only threads where the last word is not the owner's.
- Row = 32 px: avatar glyph, channel, who, one-line gist, age. 25-30 rows per screen (#2978).
- Keys: `j/k` move, `e` done, `h` snooze (menu: later today, tomorrow, next week, pick), `r` reply in peek, `Space` peek, `m` make task, `u` mark unread, `z` undo. Undo for 5 s after Done is our add; none of the three source tools was confirmed to have it.

**Where one app wins**: Done on a thread can also close the Linear issue or approve the PR it links to, because the row knows its linked objects. Superhuman and Slack can't.

**Desktop gain**: key to row removed and next row focused in < 50 ms, no network wait (optimistic). Measure: key event to repaint in the real app, 1,000 rows loaded. Cold open to first row painted < 100 ms from the local store (#2978 budget).

**Sindri today**: missing. No Messages page on origin/main. Slack appears only as a polled trigger (`slack.message`, `slack.mention`) in `src/Sindri/Aw.Sindri.Core/Connections/PollableEvents.cs:15` and a REST connection with a user token (`docs/sindri/connections.md:66`). Direction #2979.

**Verdict**: take and change (Superhuman's three keys on Zulip's "only unread" rule).

---

## 2. See which threads wait on me

**Best today**
- Slack Activity view (mentions, threads you are in, reactions) and Threads view (Ctrl+Shift+T, desktop only). https://slack.com/help/articles/201374536-Slack-keyboard-shortcuts-for-Windows and the Activity page above. source: docs
- Zulip Recent conversations: shortcut T; toggles Include DMs, Unread, Participated; participants column shows newest senders on the left. https://zulip.com/help/recent-conversations . Followed topics auto-follow when you start, take part in, or are mentioned in them (https://zulip.com/help/follow-a-topic); `Shift+N` jumps to the next unread followed topic (https://zulip.com/help/keyboard-shortcuts). source: docs
- Discord Inbox: third-party cheat sheets give `Ctrl+I` for an inbox of mentions and threads; the official article returned 403 and the key is unconfirmed. https://support.discord.com/hc/en-us/articles/1500000056121 (official shortcuts article; Ctrl+K confirmed, inbox key not in the text I got). source: none, unconfirmed

**What we take**
- Zulip's "Participated" toggle as the definition: a thread is mine if I wrote in it, was mentioned in it, or follow it. Auto-follow on mention.
- Slack's single list mixing mentions, thread replies and reactions, but sorted by "waits on me" (direct question > review ask > FYI, #2979) instead of time.
- Column "waits on": me / them / nobody. A thread where I replied last leaves the list.

**Where one app wins**: "waits on me" also counts the PR that asks me for a review and the issue assigned to me, in the same list (Home, #2978).

**Desktop gain**: ranking runs on the local store: list rebuild after a new event < 50 ms for 5,000 threads. Measure: event arrival to repaint.

**Sindri today**: missing. The Needs-you list exists (`src/Sindri/Aw.Sindri.Ui/Api/NeedsYouList.cs`) but has no message rows.

**Verdict**: take and change.

---

## 3. Keep a conversation on one topic without losing the channel

**Best today**
- Zulip topics: every message in a channel has a topic; `C` starts one; move messages to another topic via message menu > Move messages. https://zulip.com/help/introduction-to-topics and https://zulip.com/help/move-content-to-another-topic . Muting a topic hides it from the combined feed, `Shift+M` toggles. https://zulip.com/help/mute-a-topic source: docs
- Slack threads: reply with `T` or Right arrow on a focused message; Threads view lists the ones you are in. Keys page above. source: docs
- wee-slack: a thread opens as its own buffer, `/thread af8` where the id is printed in brackets on each threaded message; `/reply 1 <text>` starts one. https://github.com/wee-slack/wee-slack (MIT). Thread read state is local only when you use OAuth, per the README. source: docs
- Element/Matrix threads panel: not confirmed this pass (the blog index had nothing). source: none, unconfirmed

**What we take**
- Zulip's one-line topic title as the row title, because a thread row needs a name. For a Slack thread there is none, so the agent writes a gist (#2979) and the owner can rename it locally.
- Thread = row, same as Home's rows. Peek (`Space`) shows the thread; Enter opens the thread full-width.
- Mute at thread level (`Shift+M`, same key as Zulip), local only; never writes to Slack unless Slack mutes the same thing.

**Where one app wins**: the thread's title can be the issue or PR it is about.

**Desktop gain**: opening a 200-message thread from local store: first message painted < 100 ms, scroll 10k lines at 60 fps. Measure with the perf harness on a seeded store.

**Sindri today**: missing.

**Verdict**: take and change (Zulip's topics as a display idea; we can't impose topics on Slack).

---

## 4. Read a busy channel fast (density)

**Best today**
- slack-term: panes for channels, messages, input; vim-like keys (`i` insert, `/` search, `j/k` channel cursor, `J/K` thread, `g/G` top/bottom, `,` next notification). MIT, 6.6k stars. https://github.com/jpbruinsslot/slack-term . Auth path unconfirmed (README points to a "without legacy tokens" wiki page). source: none, unconfirmed
- wee-slack: history replay at startup, edits shown "(edited)", regex edit `s/old/new/`, reactions, read-marker sync. https://github.com/wee-slack/wee-slack source: docs
- Discord compact mode: unconfirmed this pass (page 403; no source read). source: none, unconfirmed
- Zulip: one message line per sender block; details unconfirmed beyond the help pages above. source: none, unconfirmed

**What we take**
- One line per short message, grouped by author, time on hover, from IRC/terminal clients (wee-slack, slack-term show one line per message by default). Code blocks and diffs rendered; threads collapsed with a count (#2979).
- `g/G` to top/bottom and `,`-style "next notification" as `n` = next unread row.

**Where one app wins**: a link to a PR/issue/run renders as a live row (state, CI, owner) instead of a URL card. Slack's own PR card (title, description, assignees, reviewers, labels, checks; https://github.com/integrations/slack) shows data from one provider and is suppressed if the same link was posted in the channel within 30 minutes or 3+ links are in one message; ours is not.

**Desktop gain**: 10k messages scrolling at 60 fps with virtualised rows; target frame time < 16 ms in the Avalonia list. Measure with the perf harness.

**Sindri today**: missing.

**Verdict**: take and change.

---

## 5. Jump anywhere with the keyboard

**Best today**
- Slack: Ctrl+K jump to a conversation; Ctrl+J latest unread in conversation; Alt+Shift+Up/Down previous/next unread channel; Esc marks the current conversation read, Shift+Esc marks all read; Alt+Click or `U` marks unread. https://slack.com/help/articles/201374536-Slack-keyboard-shortcuts-for-Windows source: docs
- Zulip: `N` next unread topic, `P` next unread DM, `S` go to topic, `Q` filter sidebar, `/` or Ctrl+K search, `End` or `Shift+G` jumps to the last message and marks the view read. https://zulip.com/help/keyboard-shortcuts source: docs
- Discord: Ctrl+K quick switcher (confirmed in the official article above); Alt+Shift+Up/Down for unread channels is from third-party lists, unconfirmed. source: none, unconfirmed
- Superhuman: Cmd/Ctrl+K command menu is the master entry point and lists every shortcut. https://help.superhuman.com/article/478-superhuman-command source: docs

**What we take**
- One palette (Ctrl+K) for people, channels, threads, issues, PRs, notes, actions (the #2978 grammar). Fuzzy on the last 100 touched things first.
- `n` next unread/needs-reply (Zulip N), `Esc` marks the open thread read (Slack), `Shift+G` ends and marks read (Zulip).
- Show the key next to each action in the palette (Superhuman).

**Where one app wins**: the palette finds a PR, a thread and a note in one box.

**Desktop gain**: palette opens and the first 20 results paint < 50 ms from keypress, with no network. Measure keypress to paint.

**Sindri today**: palette (Ctrl+K) is in the #2978 grammar; message targets missing.

**Verdict**: take as is (keys), take and change (targets).

---

## 6. Reply, edit, react, and send later without friction

**Best today**
- Slack: `E` edits a focused message, Up from the empty composer edits the last one, `R` reacts, `F` forwards, `M` reminder, `A` save, `P` pin. Keys page above. Scheduled send and drafts exist in the UI; I could not fetch the help page (unconfirmed URL); API has `chat.scheduleMessage`, but the list endpoint only sees API-scheduled messages (above). source: none, unconfirmed
- Zulip: drafts save on Esc after 3+ characters, are local to the client and don't sync; `D` opens the list. Scheduled message from the send menu, listed in the sidebar. https://zulip.com/help/view-and-edit-your-message-drafts and https://zulip.com/help/schedule-a-message source: docs
- wee-slack: edit with `s/old/new/`, reactions, "(edited)" marker. https://github.com/wee-slack/wee-slack source: docs

**What we take**
- Slack's message keys exactly (`E`, `R`, `T`/Right, Up-to-edit), so muscle memory carries over.
- Zulip's draft rule: autosave on leaving the composer, 3+ characters, a Drafts list with Enter to restore and Backspace to delete. Ours is local-only like Zulip's, because Slack has no drafts API.
- Optimistic send: the line appears at once, greyed; a failed send stays in place with a red "retry" (#2978 rule 2).

**Where one app wins**: "ask an agent to draft" from the thread's context, shown as a Draft row that waits for approve / edit / drop (engine plan on #2979: `draft_message`).

**Desktop gain**: send to visible in the list < 50 ms (optimistic); Slack confirmation within 2 s (#2978). Measure keypress to paint, and send to API ack.

**Sindri today**: missing; `draft_message` and `send_message` blocks are planned (engine plan, #2979).

**Verdict**: take as is (keys, optimistic send), take and change (drafts local-only).

---

## 7. Snooze, remind, save for later

**Best today**
- Superhuman Remind Me (`H`, or Cmd+K > Remind Me) brings a message to the top of the inbox at a chosen time; also "remind if no reply" in days (vendor page). https://help.superhuman.com/article/582-achieving-inbox-zero and https://superhuman.com/mail source: docs
- Zulip: "Remind me about this" on a message, with a note and a suggested or custom time; arrives as a DM from Notification Bot; Reminders view in the sidebar. https://zulip.com/help/schedule-a-reminder source: docs
- Slack Saved items: Home > Saved; `A` to save; "Open in Home"; clean-up moves items to To-dos. https://slack.com/help/articles/360042650274-Save-messages-and-files-for-later . No API (above). source: docs
- Missive snooze: removes a conversation from the inbox until a later time (via search snippet from https://missiveapp.com/features; page itself unread). source: docs

**What we take**
- Snooze returns the row to the top of Needs reply at the time, with a "snoozed until" ghost nowhere else. Presets: later today (+3 h), tomorrow 9:00, next Monday 9:00, pick.
- Superhuman's "if no reply in N days" as a second option: `H` then `f` = follow up if they don't answer.
- Saved = a flag on the row, not a separate product; shows in a Later smart list (#2979).

**Where one app wins**: a snooze can be "until CI is green" or "until the PR merges", because the row knows the linked object.

**Desktop gain**: snooze fires on time offline and after sleep (local timer); first paint of Later < 100 ms. Measure: wake-from-sleep to correct list.

**Sindri today**: missing.

**Verdict**: take and change.

---

## 8. Search with operators

**Best today**
- Slack search modifiers: `in:`, `from:`, `has:`, `before:`, `after:`, `on:`, `during:`, `is:saved`, `is:thread`, `has:pin`, `with:`, quotes, `-` to exclude, `rep*` wildcard from 3 chars. https://slack.com/help/articles/202528808-Search-in-Slack source: docs
- Zulip operators: `channel:`, `topic:`, `sender:`, `is:unread`, `is:mentioned`, `is:resolved`, `has:link`, `has:attachment`, `near:`, `date:`; negate with `-`; stemmed keywords. https://zulip.com/help/search-for-messages source: docs
- API side: `search.messages` is legacy, user token with `search:read`, Tier 2, and Slack recommends `assistant.search.context` instead; user-token results follow the UI's search filters. https://docs.slack.dev/reference/methods/search.messages source: docs
- Discord search filters: unconfirmed this pass. source: none, unconfirmed

**What we take**
- Slack's `in: from: has: before: after: is:saved` verbatim, plus Zulip's `is:unread`, `is:resolved`, `near:`. Same words so no relearning.
- Search the local store first (instant), then Slack's `search.messages` for what the store lacks; results merged and tagged "from Slack".
- Add `pr:`, `issue:`, `note:` operators for the cross-source box (#2979 item 5).

**Where one app wins**: one box over messages, issues, PRs and notes.

**Desktop gain**: local full-text index (SQLite FTS5): first result < 100 ms on 1M messages. Measure keypress to first row. Slack's API search can't match it because of the network and its Tier 2 limit.

**Sindri today**: missing for messages. The Context search exists for code (see `docs/sindri/prior-art/context-map.md`).

**Verdict**: take and change.

---

## 9. Catch up after time away

**Best today**
- Slack AI: summarise a channel or DM for unread messages, last 7 days, or a custom range (desktop); Recap is a daily digest of chosen channels that cites its source messages. Summaries on Pro and up, Recap on Business+ and Enterprise+. https://slack.com/help/articles/31739993134867 and https://slack.com/help/articles/25076892548883 (vendor docs). source: docs
- Zulip Recent conversations with the Unread toggle: shows ongoing topics, newest senders first. https://zulip.com/help/recent-conversations source: docs
- Zulip `End`/`Shift+G` marks the view read, so catching up ends with one key. Keys page above. source: docs

**What we take**
- Recap's shape: per channel, 3 lines, each line tappable to its source message (peek). Slack cites sources; ours must too.
- Zulip's "mark all read in this view" on one key.
- Brief is on-demand per channel and daily on Home ("Since you last looked", #2978), written by an agent from the local store, with links.

**Where one app wins**: the brief mixes the channel with what changed in the PRs and issues the channel talks about.

**Desktop gain**: the brief reads from the local store, so no history pull (the 1/min limit makes history pulls unusable). Time to first brief line < 2 s after a model call starts streaming; the store read itself < 100 ms.

**Sindri today**: missing.

**Verdict**: take and change.

---

## 10. Turn a thread into a task (and back)

**Best today**
- Linear Slack integration: message > More actions > Connect to apps > Create new issue, with an optional synced comment thread (Linear comments appear in Slack; Slack thread updates when the issue closes). Link an existing issue (no sync) or `Control L` in Linear. `@Linear` agent can "file a bug, assign me" from a thread; `/ask` for non-Linear users on Business and Enterprise. https://linear.app/docs/slack source: docs
- GitHub's Slack app: "Create an Issue" from the message menu or `/github open`; cards have comment, edit, close/reopen buttons. https://github.com/integrations/slack source: docs
- Missive: assign a conversation to a person or team; assigned items show in the assignee's inbox and the team Tasks view; `Cmd+D` self-assign; tasks can be created from internal chat. https://learn.missiveapp.com/assignment and https://missiveapp.com/docs/core-features/triage-and-assignment (search snippets; pages not opened). source: none, unconfirmed

**What we take**
- Linear's two modes, named for what they do: "make issue" (new, synced) and "attach to issue" (link only). One key each: `m` make, `a` attach.
- Linear's synced thread, but both directions are ours to show on one screen: the thread in the issue's peek, the issue in the thread's peek.
- Missive's assign-moves-it-out rule: once a thread has an issue with an owner, it leaves Needs reply unless a new message asks me something.

**Where one app wins**: Linear can't show the Slack thread's CI; we show issue, thread and PR in one row set.

**Desktop gain**: `m` to the new issue visible in the row < 100 ms optimistic; Linear write within 2 s (#2978).

**Sindri today**: missing. Linear reads exist (`docs/sindri/linear.md`); Projects is #2961.

**Verdict**: take and change.

---

## 11. Show the work a link points to (instead of a URL card)

**Best today**
- Linear unfurl: issue shows title, description, status, assignee, creation date, with actions to change assignee, comment, subscribe. Private-team URLs never unfurl. https://linear.app/docs/slack source: docs
- GitHub unfurl: issues, PRs, comments, code blobs with line numbers; parent card shows latest status, title, description, assignees, reviewers, labels and checks; suppressed in some repeat cases. https://github.com/integrations/slack source: docs
- Slack's unfurl API: `link_shared` event and `chat.unfurl`; up to five registered domains per app; the event omits message text. Without a handler Slack falls back to OpenGraph. https://docs.slack.dev/messaging/unfurling-links-in-messages/ source: docs

**What we take**
- A link to something Sindri knows renders as its own row (glyph, state, CI, owner, age), 32 px, clickable to peek. Unknown links stay plain.
- GitHub's field set (title, assignees, reviewers, labels, checks) as the PR row's columns.
- Never unfurl something the owner can't see (Linear's private-team rule).

**Where one app wins**: all three of Slack, Linear and GitHub show one provider's data, and only to the channel. Ours is the owner's view with live state from the ledger.

**Desktop gain**: a message with 3 links renders its rows with no per-link network call; < 16 ms to lay out. Measure with the perf harness.

**Sindri today**: missing for messages. PR and Linear rows exist elsewhere in the app.

**Verdict**: ours wins by design once built (needs proof: side-by-side frame).

---

## 12. Mute noise and stay in control of notifications

**Best today**
- Zulip: muted topics don't notify (except mentions), are hidden from the combined feed, and don't count toward channel unread counts; followed topics in a muted channel count as unmuted. https://zulip.com/help/mute-a-topic and https://zulip.com/help/follow-a-topic source: docs
- Slack: Esc / Shift+Esc to mark read; per-channel notification settings (not read this pass: unconfirmed). source: none, unconfirmed
- Beeper: reminders and unified inbox for 15+ networks (vendor claim); free for 5 accounts. https://www.beeper.com/faq source: docs

**What we take**
- Zulip's exact mute rule: mute hides and silences, a mention still breaks through, a followed thread overrides a muted channel.
- The owner's rules are written once (a Sindri rule), applied to the local store, and shown in a "why is this here" tooltip on each row (our add).

**Where one app wins**: the same rule can silence a channel on Home and in the app tray together.

**Desktop gain**: a new event decides notify / silent in < 20 ms locally; notification shown before the next frame.

**Sindri today**: missing; engine triggers `slack.mention` exist (`PollableEvents.cs:15`).

**Verdict**: take as is (Zulip's rules).

---

## 13. Use Slack without being able to use the official app's tokens (the connection itself)

**Best today**
- wee-slack: OAuth (`xoxp-`) or a session token plus `d` cookie from the web client; README warns session tokens "can't be revoked" and are "not officially supported". https://github.com/wee-slack/wee-slack source: docs
- slk (Go, Bubble Tea): `xoxc` token and `d` cookie from DevTools; maintainer calls it unofficial. https://github.com/sendbird/slk (from search results; page not opened) source: none, unconfirmed
- slack_rust (Rust, ratatui): user or bot token plus app-level token over Socket Mode. https://github.com/oovets/slack_rust (search result only; unconfirmed behaviour) source: none, unconfirmed
- slackdump (export with a user token) was not read this pass: unconfirmed. source: none, unconfirmed

**What we take**
- Only the official route: a user-token OAuth app the company has allowed, or ask the workspace admin. Session-cookie tokens are out (Slack calls them unsupported and they cannot be revoked).
- Honest status in the UI: "Slack gave us N of the last 15 messages per call" when rate-limited, never a silent gap.

**Where one app wins**: n/a (this is the risk row).

**Desktop gain**: store survives restarts and offline: reopen with no network shows the last synced state in < 100 ms.

**Sindri today**: `slack` preset is refused (Slack accepts only https redirect URLs); use a REST connection with a user token: `connection add slack:work --kind rest --key-env SLACK_TOKEN` (`docs/sindri/connections.md:66`). Context sources read Slack with a bot token (`docs/sindri/sources.md:61`).

**Verdict**: take and change (official tokens only). This row decides #2979 P0.

---

## Closing table

| Problem | Best tool | Verdict | Sindri today (issue #) |
|---|---|---|---|
| 1 Clear needs-reply to zero | Superhuman keys + Zulip Inbox | take and change | missing (#2979) |
| 2 Which threads wait on me | Zulip Recent conversations (Participated) + Slack Activity | take and change | missing; Needs-you list has no message rows (#2979, #2978) |
| 3 One topic per conversation | Zulip topics, wee-slack thread buffers | take and change | missing (#2979) |
| 4 Read a busy channel fast | slack-term, wee-slack (MIT) | take and change | missing (#2979) |
| 5 Jump with the keyboard | Slack Ctrl+K, Zulip N/P, Superhuman Cmd+K | take as is (keys) | palette in grammar (#2978); no message targets |
| 6 Reply, edit, react, send later | Slack keys, Zulip drafts | take as is / change | missing; `draft_message` planned (#2979) |
| 7 Snooze, remind, later | Superhuman H, Zulip reminders | take and change | missing (#2979) |
| 8 Search with operators | Slack modifiers, Zulip operators | take and change | missing for messages |
| 9 Catch up after time away | Slack AI Recap, Zulip Recent | take and change | missing (#2979, #2978 brief) |
| 10 Thread to task | Linear Slack integration | take and change | missing (#2979, #2961) |
| 11 Live row for a link | GitHub and Linear unfurls | ours wins by design (unproven) | missing |
| 12 Mute and notification rules | Zulip mute/follow rules | take as is | missing; triggers exist (`PollableEvents.cs:15`) |
| 13 The Slack connection | wee-slack OAuth route | take and change | REST connection only (`connections.md:66`) |

---

## What we take

1. Done / Remind / stay on one key each, undo for 5 s, only threads that wait on me (problems 1, 2, 7): #2979 P3 (triage); #3020 for the snooze presets and "follow up if no reply".
2. Slack's message keys (`E`, `R`, `T`, Up-to-edit), optimistic send, local-only drafts as Zulip does (problem 6): #2979 P2.
3. One Ctrl+K palette with `n` next unread, `Esc` marks read, `Shift+G` ends and marks read (problem 5): #2978 (palette), message targets in #2979 P1.
4. Slack's search operators plus Zulip's `is:unread`, `is:resolved`, local FTS first, Slack search second (problem 8): #2979 P1.
5. Links to PRs, issues and runs render as 32 px live rows, not URL cards (problems 4, 11): #3021.
6. `m` make issue / `a` attach to issue, thread and issue peek each other, Done can close the linked issue (problem 10): #2979 P3, Linear side #2961.
7. Official user-token OAuth only, honest "N of 15 messages per call" status, live events fill the store because history pulls are 1/min (problems 9, 13): #2979 P0 probe.

