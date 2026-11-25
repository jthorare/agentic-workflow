# Sindri's Messages page: clear the chat inbox without opening Slack

Status: autoplan rounds 1, 2 and 3 folded (ideas lane, 2026-11-12). Serves #2979 (Messages, a Slack replacement with full parity) under #2978 (one app that replaces the others). It builds the Messages rows of the prior-art ledger (`docs/sindri/prior-art/messages.md`, #2989) and the child issues #3020 (snooze and follow up) and #3021 (links as live rows). It sits beside the Home spec (`docs/superpowers/specs/2026-11-12-sindri-home-inbox-design.md`) and reuses its row / peek / full grammar, keys, snooze store and write-through pipeline.
Reviews: 3 /autoplan rounds done; frames drawn; meta's UX pass (#3188) folded in decision 22 (no owner read). Next: design redraws the frames the UX pass names.

"Ledger N" means problem N in the prior-art file. "Take N" means item N of its closing list "What we take". "Parity row N" means row N of §3 below.

A guess is marked **(guess)** and names the P0 probe line that proves it. P0 is the owner-run probe of what a Slack user token can do (§5.5). No guess below has been run against Slack.

**Source tags.** A rule or number with no tag is `agent default`: the ideas lane picked it and the owner has not answered. `owner decision (#2979)` marks what the owner's words on that issue fix: Slack stays the system of record; the four smart lists; huddles dropped; one app for everything; full parity as the ledger target; agents never auto-send without a rule the owner wrote. `agent default (api, #2979)` (or engine, meta) marks a lane plan on that issue. `agent default (lead, 2026-11-12)` marks the calls the lead made after autoplan rounds 1, 2 and 3 (the Decisions list says which; calls 12 to 20 are the round 2 ones, 21 to 26 the round 3 ones; decision 22 is meta's UX pass). `agent default (question N)` marks a recommended pick the body already uses and §16 still asks. Row heights and the peek width are `agent default (Home spec)`.

## 0. Why

The owner's words on #2979: "imagine that i wanted to add a slack replacement experience here. how would that work / look without losing any slack functionality." Today the owner opens Slack for every message.

What exists on `origin/main` (cb6cacd146; read in the code, issues linked; the app was not driven for this spec):

- **No Messages page.** Slack appears in four places, and none is a reader.
- **A REST connection.** The `slack` OAuth preset (`config/connections/presets/slack.json`) is refused because Slack accepts only https redirects. The path is `sindri connection add slack:work --kind rest --key-env SLACK_TOKEN` (`docs/sindri/connections.md:66`). The token lives in one keychain entry (`IConnectionSecrets`; an owner-only file where the box has no keychain).
- **One poller and one write.** `SlackEventPoller` (`Infrastructure/Connections/Polling/`) polls `conversations.history` of ONE channel, 100 per page, and raises `slack.message` and `slack.mention` (`Core/Connections/PollableEvents.cs`). `RestActions.cs` has one Slack write, `post_message`.
- **A context reader.** `Aw.Connectors.Core.Slack` (`ISlackReader`, `SlackCursor`, `SlackPermalink`, `SlackTs`) reads public and private channels for the code-and-docs index with a BOT token. `SlackChannelIds` refuses direct messages on purpose. Messages cannot reuse that reader as it stands.
- **Nothing else.** No Socket Mode, no DM or thread read, no read marks, no search, no reactions, no edit, no delete, no file, no draft.

The job, from #2979: **a Slack client on the owner's own token that shows what needs a reply first, lets the owner answer, react, snooze and turn a thread into work in place, and shows the rest of Slack one key away. Slack stays the system of record. Colleagues see ordinary Slack messages.**

Two facts shape everything (both from the ledger's header, read from Slack's docs on 2026-11-12):

1. **History reads may be 1 per minute.** For apps created after 2025-05-29 that are not in the Marketplace, `conversations.history` and `.replies` allow 1 request per minute with at most 15 messages. Older internal apps keep Tier 3 (50 or more per minute). Which one the owner's app gets is unknown **(guess; P0 line 3)**. The sync design below works in both cases: live events fill the store, and history only fills gaps. A one-time cold-start fill from `search.messages` gives the first days a sample (§5.2).
2. **Slack has no API for Later, drafts, the Activity feed or channel notification settings.** Those four are Sindri-local. The parity ledger says "differs" for them, never "done".

## 1. The bar

| Step of the daily loop | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Clear what needs me to zero | 1 | Superhuman keys, Zulip Inbox | Done / Snooze / stay on one key each; only threads whose last word is not mine | §6, §7 |
| Know which threads wait on me | 2 | Zulip "Participated", Slack Activity | mine = I wrote in it, was mentioned in it, or follow it; sorted by who waits | §7.2 |
| One topic per conversation | 3 | Zulip topics, wee-slack threads | a thread is a row; a gist is its title | §7.3, §9.1 |
| Read a busy channel fast | 4 | wee-slack, slack-term (MIT) | one line per short message, grouped by author, threads collapsed | §6.3 |
| Jump with the keyboard | 5 | Slack Ctrl+K, Zulip N / P | one palette; `n` next unread; read marks stay local | §6.5 |
| Reply, edit, react, send later | 6 | Slack keys, Zulip drafts | local drafts; optimistic send; Slack-only message keys (Release 2 and 3) | §6.4, §8 |
| Snooze and follow up | 7 | Superhuman `H`, Zulip reminders | one snooze store; "unless replied" | §9.4 |
| Search with operators | 8 | Slack modifiers, Zulip operators | Slack's words verbatim; local first | §9.8 |
| Catch up after time away | 9 | Slack AI Recap | per channel 3 lines, each citing its source | §9.6 |
| Thread to task | 10 | Linear's Slack app | `m` make, `a` attach, both ways visible | §9.5 |
| Live row for a link | 11 | GitHub and Linear unfurls | a 32 px row from our own store | §9.3 |
| Mute and notify | 12 | Zulip mute and follow rules | mention breaks through; followed overrides muted | §9.9 |
| The connection | 13 | wee-slack OAuth route | official user token only; honest gap status | §5 |

Nobody has a thread row that is also the PR's row and the issue's row, a snooze that waits on CI, or an agent draft that waits for approval in the same inbox. Those three are ours and have no prior art to lean on.

**Release 1 is a slice that writes nothing to Slack** *(agent default (lead, 2026-11-12); widened by decision 22)*. It has one workspace; DMs, mentions and starred channels; Needs reply and Unread; the dense conversation; the thread in the peek; link rows; local Done, plain snooze and a local Mark all read; hand to agent (`t`); make issue (`m`); polling only. It sends nothing, reacts to nothing and writes nothing to Slack. To answer, the owner presses "Open in Slack" (a permalink; `r` does the same, so the key keeps its meaning when Release 2 adds the composer). It has no Socket Mode, no gist and no brief. *Why:* the question that decides everything is whether reading in Sindri beats reading in Slack. The read side answers it for a fraction of the write stack.

**What Release 1 does, plainly.** The owner named six jobs: read, reply, done, snooze, hand to agent, make issue. Release 1 does five in the app. Read works in Needs reply, Unread, the conversation and the peek. Done is `e`, snooze is `h`, hand to agent is `t` and make issue is `m`. Reply opens the thread in Slack (`r`), because a reply is a Slack write. Three parts stay out of Release 1:

1. The agent draft (`messages_draft`, the Drafts list, "Ask an agent to draft"). It needs the drafts store and the composer. Release 2.
2. The box "Reply in the thread with the issue link" on the make-issue sheet. It posts to Slack. Release 2.
3. Attach (`a`) and Save as a note. Release 3.

*Why `t` and `m` moved in:* neither writes to Slack, both reuse a piece that exists (Home's hand-to-agent picker, Linear's create-issue write), and without them Release 1 proves two of the six jobs. They add two phases, M1o and M1p (§14).

**Release 2 is writes:** send, reply, react, drafts, agent drafts, the reply-with-link box on make issue, follow up if no reply, and the opt-in read-mark sync. It starts only after P0 passes and the owner has used Release 1 for a week and says it replaces Slack for reading (gate G, §14). **Release 3 is demand-pulled:** a row of §3 gets built when a Slack open in the dogfood issues names it.

**Kill rule.** The owner decides each stop. Ideas writes the decision here the same day.

1. P0 line 1 or line 8 fails: there is no token. Messages stops. Only link rows (#3021) and Home rows for existing Slack events go on.
2. P0 line 3 says 1 request per minute, and lines 4 and 5 both fail: there is no live path. The owner re-prices bet 2 before M1e is ready. Messages then ships read-late or stops. On a 1-per-minute workspace the thread-reply check (§5.2) is also capped, so a reply in an old thread without a mention shows only when the thread is opened (parity row 5 says `partial`).
3. Gate G fails (§14): writes do not start.

**The bar in one line:** Needs reply names the thread that waits on the owner first, the owner answers it without opening Slack, the answer reaches Slack within 2 s, and the lists never lie about a gap.

Three bets decide whether this is worth it. Each has a number.

1. **The inbox beats Slack's Activity view.** The owner's Slack **opens to read** (an open that did not come from a Sindri permalink) for "what needs me" halve between days 1 to 2 and days 3 to 7 of the Release 1 week (X2, gate G). An open from a Sindri permalink is a reply, not a read: in Release 1 "Open in Slack" is the only way to answer, so it is not counted against the bet. After Release 2, opens of any kind fall to 5 or fewer a week. ux counts them in a tally in the dogfood issue (X1, X2). W14's 0 opens is the end state of full parity, not this bet.
2. **Live is live enough.** A new DM shows in Needs reply within 30 s on the polled hot set **(guess; P0 line 3)**. Socket Mode (Release 3) aims at 5 s **(guess; P0 line 5)**. If P0 says polling cannot hold 30 s, the page ships read-late with a visible "last read" time and the bet is re-priced (kill rule 2).
3. **The token is allowed.** The owner's workspace lets the owner install a Slack app with user scopes, and the company allows chat text on the laptop. If an admin blocks the app, there is no token and no Messages page. Questions 1 and 8 (§16) are one day-0 gate. The owner answers both before M1e is ready and before X2 starts. M1d builds against the fake and the documented scopes until then (lead call 20).

### The W-rows

A W-row is one done line. Each is a test or a measured run in the real app (aw-ui-driver) and names its owner lane in §14. "Lane" is the test lane: `unit`, `integration`, `perf` (run by hand or by the perf lane, M3f, never on a loaded box), `bash`, `architecture` (an `Aw.Architecture.Tests` rule) or `ux sheet` (a run by ux, not a test project). Wall-clock numbers are asserted only in `perf`. Unit and integration rows assert work done (rows read, calls made), never milliseconds. "Rel" is the release that must pass it.

| # | Done line | Measured as | Lane | Rel |
|---|---|---|---|---|
| W1 | First paint from the store. | Needs reply list, 5,000 threads in `messages.db`, serve not answering, p95 over 20 runs: under 100 ms. The integration proxy (50,000 messages) asserts the paint reads the store once and makes 0 network calls. | perf; integration | 1 |
| W2 | Key to paint, write to Slack. | Keydown to repaint under 50 ms. Send request to Slack confirming within 2 s, p95 over 50 writes on the fake Slack. | perf | 2 |
| W3 | A failed write is visible and retryable. | For each write kind: the fake refuses, the line shows the reason and Retry within 1 frame, and Retry lands the write exactly once (§8.3). Offline, every write kind refuses at once with SND-MSG-028 and the composer keeps the text as a draft (§8.3 step 7). A 429 hold ends in a failed line after 2 minutes. | integration | 2 |
| W4 | Density. | 1440x900, peek closed, compact: at least 25 list rows, or at least 40 conversation lines of short messages. | perf | 1 |
| W5 | Scroll. | 10,000 messages in one conversation scroll with a frame time under 16 ms (virtualized rows). The integration proxy checks that only visible rows are built. | perf; integration | 1 |
| W6 | The ledger is green. | Every Slack function in §3 is `done`, `partial` with a named limit, `missing #N`, or `dropped: why`. `scripts/check-parity.sh docs/parity/slack.md` fails on a bare row. | bash | 1 |
| W7 | Needs reply is exact. | A property test over 10,000 random event streams: a thread is in the list if and only if it meets the one definition in §7.1. The §7.2 example is a golden test. One event rebuilds only the touched thread's row (asserted as rows touched; the 50 ms at 5,000 threads is measured in `perf`). | unit; perf | 1 |
| W8 | Links are live rows. | A message with 3 links to a PR, an issue and a run lays out with 0 network calls (16 ms in `perf`). A link to something Sindri does not know stays plain text. | integration | 1 |
| W9 | Snooze. | The row returns within 1 s of the due time with the app in the tray. "Unless replied" drops the snooze when someone answered after my last message. Undo within 5 s. | unit | 1 (plain), 2 (unless replied) |
| W10 | No silent gap. | With the fake Slack at 1 request per minute and 15 per page, the conversation shows a gap marker with the count and "Load older (1 per minute)". A test fails if a gap shows no marker. A reply in an old thread that the thread-reply check (§5.2) could not read shows the thread as `partial`, never as caught up. | unit | 1 |
| W11 | Catch-up brief. | At most 3 lines per channel; every line cites message ids that exist in the store; plain-fact lines paint under 100 ms; a model failure costs only the sentence. | unit | 3 |
| W12 | Agents never send alone. | One choke point: only `MessageOutbox` holds the write port (`ISlackSender`), `internal`. `BannedSymbols.txt` bans the port's type everywhere else. BannedSymbols bans symbols, not string literals, so an `Aw.Architecture.Tests` source scan bans every Slack write method name as a string: `chat.postMessage`, `chat.postEphemeral`, `chat.update`, `chat.delete`, `chat.scheduleMessage`, `reactions.add`, `reactions.remove`, `conversations.mark`, `pins.add`, `pins.remove`, `files.getUploadURLExternal`, `files.completeUploadExternal`, `users.profile.set`, `dnd.setSnooze` and `dnd.endSnooze`. `Infrastructure/Connections/RestActions.cs` (the workflow `post_message`, preset `slack`) is the one allow-listed file. Every caller of `MessageOutbox.Enqueue` passes an `Origin` of `Human` or `HumanSavedRevision`; no constructor makes one from a session or a test run. A session's `messages_draft` writes a draft and nothing else. An agent draft plus `Enter` sends nothing. | unit; architecture | 2 |
| W13 | Search. | Local first result under 100 ms on 1,000,000 messages (SQLite FTS5) in `perf`; a 50,000-message proxy in integration. Every operator in §9.8 has a golden parse test. | perf; integration | 1 (local) |
| W14 | **A day without Slack.** | ux runs a scripted day with Slack closed. Each time the owner opens Slack is an issue, `Part of #2979`, that names the parity row or function and never quotes message text (§11). Count: 0 for shipped rows. Huddles are the one named exception (§3, row 26). | ux sheet | 2 |
| W15 | Untrusted text. | A test posts a message holding markup, a bidi override and a prompt-injection line. The page shows plain text, no remote image loads, and every MCP and CLI result fences it. | unit; integration | 1 |
| W16 | Arrival. | A message posted on the fake Slack appears in Needs reply within 30 s on the poll path, measured from the fake's post to the row painted. This covers a new top-level message. A reply in an older thread is bound by the reply check's rotation (§5.2, up to 3 minutes on Tier 3). | perf | 1 |
| W17 | A session reads no text. | For every GET under `/sindri/messages` and every `messages_*` read tool, a session caller gets no `text`, `blocks`, file name, topic or display name. A golden per route with the caller class as a parameter, in the ApiGoldens classes. | integration | 1 |
| W18 | Text stays in one file. | After a run on the fake with fixture messages, a scan of every `ledger.db` table (events, `workflow_run`, `workflow_node_run`, job records), `views.db`, `temporal.db`, the logs, the spans and the metric labels finds no byte of a fixture message body. Names and ids may appear; bodies may not (§11). A case runs a workflow whose generic `prompt` node references a Messages trigger field and checks that the stored node input holds ids and no body (the field is an id, so the prompt cannot expand to text; lead call 13). A case fails a Messages node on purpose and checks the stored error is a code plus a fixed title, never message text or a token (`Failed_messages_node_stores_code_and_title_only`). The run-store part of this row runs with E0 in Release 3; before that no workflow has a Messages trigger. | integration | 1 |

*Source:* every number in this table is `agent default`. W6's "full parity" and W14's "a day without Slack" follow `owner decision (#2979)`.

## 2. Decisions

1. **Slack is the system of record. Sindri keeps a cache in `messages.db`.** The file holds channels, DMs, users, messages, reactions, file metadata and read state. It is a SQLite file in `$AW_SINDRI_HOME`, mode 0600, separate from the ledger. *Why:* the ledger is append-only, and a Slack delete or edit must change local text. Putting DM text in the ledger would keep it forever and leak it into `GET /sindri/ledger`.
2. **The ledger holds ids and counts, never message text.** Events: `messages.act.sent`, `.landed`, `.failed`, `messages.synced`, `messages.thread.linked`, `messages.draft.agent`, `messages.send.workflow`, `messages.class.missed` (the `x` key: row id and class only). `messages.synced` is written at most once per 15 minutes or on a change of path or state (Home's `.changed` rule), carrying counts. A Home row raised for a message has a title of sender and place only ("Sam, DM"), never the text. *Why:* the same reason, and `Aw.Redaction` cannot unwrite an event.
3. **One official user token, `xoxp-`, in a Slack app the owner installs.** No session cookie, no `xoxc-`. The token lives under its own connection, `slack:messages` (§5.1). *Why:* Slack calls cookie tokens unsupported and they cannot be revoked (ledger 13). A bot token cannot read the owner's DMs, so it cannot replace Slack.
4. **Release 1 polls. Socket Mode comes in Release 3, only if P0 line 5 proves it. Events API is out.** Events API needs a public https URL; Sindri listens on loopback only. *Why:* P0 line 5 decides, and the first Slack app token (`xapp-`) is a second manual copy the first run does not need. The code ships the poll path and an `ISlackLive` port that Socket Mode fills later.
5. **History fills gaps, not the store.** The store fills from live events and from reads the owner triggers (open a conversation, press `Load older`). A background backfill runs only on a Tier 3 workspace (§5.3). *Why:* 1 request per minute cannot backfill anything.
6. **The left side is four smart lists, not Slack's channel tree.** Needs reply, Unread, Later, Drafts; then starred channels; all channels behind `Ctrl+K`. *Why:* Slack's sidebar is where its noise comes from (#2979).
7. **Mine means participant.** A thread is mine when I wrote in it, was @mentioned in it, or follow it. A DM is always mine. `@here` and `@channel` never make a thread mine. *Why:* Zulip's "Participated" (ledger 2), with the broadcast case cut so a lunch ping does not rank.
8. **Later, Drafts, snooze, mute and Done are Sindri-local.** None writes to Slack. *Why:* Slack has no API for the first and last of them (§0). The ledger marks each "differs".
9. **Writes follow Home's pipeline** (Home §5.2): paint in under 50 ms, write to Slack, confirm within 2 s, visible failure with Retry, Undo only for local state. Message sends carry an opaque random `actId` in Slack's `metadata` (it names no run, draft or workflow) so a retry never posts twice **(guess; P0 line 6)**. Writes are Release 2.
10. **No bare key writes to Slack. Read marks stay local.** `r` opens the composer. The composer sends on `Ctrl+Enter`, like Home's reply composer; `Enter` is a newline and never sends during IME composition. `messages.sendKey: enter` is an opt-in for Slack's habit. Delete asks for a confirm that defaults to Cancel. A read mark is a local pointer. The opt-in `messages.syncReadToSlack` (default off) or the `.` panel action "Mark read in Slack" writes it to Slack; both are the owner's explicit act. `Esc` has one job: close or back out one layer (§6.5). *Why:* Home's rule is that a bare key never writes to a source; `conversations.mark` also clears the owner's phone. *(agent default (lead, 2026-11-12))*
11. **One key set, with Slack-only message keys added on letters Home does not use.** List mode (the cursor is on a list row) uses Home's keys. Message mode (the cursor is on a message in a conversation or thread) adds `i` edit, `o` react, `s` share (forward), `p` pin and `Del`. No letter has two meanings, and no key differs from another only by case, except Home's own `r` and `R` (reply and re-rank), which Messages inherits as they are. The footer names the live set. *Why:* Zulip's and Reviews' rule, and a wrong-mode key stays harmless. The cost: Slack's `e` (edit) and `r` (react) move to `i` and `o`. Question 3 asks the owner. *(agent default (lead, 2026-11-12))*
12. **A link to something Sindri knows renders as a live row** from the local row store, never as a Slack unfurl (#3021). It needs no Slack data, so it ships first.
13. **Snooze and Done are one store with Home: Home's ledger events.** Home's merged spec ("Events and migration") defines `home.row.snoozed`, `.woke`, `.done`, `.revived` and `.muted`, projected into `home_rows`, and says "One store serves Messages (#3020)". Messages writes those events for its rows and adds the field `unlessReplied` (#3020). There is no Messages-local `row_state` table and no later replay (lead call 17). *Why:* api's plan on #2979: one record on a row. `agent default (api, #2979)`
14. **Agents draft; the owner sends.** An agent draft is never sent without the owner's send action. `draft_message` waits for the owner. `send_message` runs only owner-written templates, and only on a revision a human saved (engine plan on #2979). Model-written text goes only through `draft_message`. There is no other auto-send. *Why:* meta's rule on #2979 (`owner decision (#2979)`: no auto-send without a rule the owner wrote), tested as W12. `agent default (lead, 2026-11-12)` for the template-only rule.
15. **Triage rank is deterministic. Work chat never goes to a hosted model by default.** The class of a row (direct question, review ask, FYI) comes from fixed rules (§7.2). The gist and the catch-up brief are off by default and never run on DMs, group DMs or Slack Connect. A later opt-in per starred channel is allowed (`messages.model.allow`). They are not in Release 1. *Why:* DM text and other orgs' text would leave the laptop on the first sync. The order must not change between two runs on the same store. *(agent default (lead, 2026-11-12))*
16. **Search is local first, Slack second.** Local FTS5 answers in 100 ms. A second pass calls `search.messages` and tags results "from Slack". *Why:* Tier 2 is 20 calls a minute and the API search follows workspace limits (ledger 8).
17. **Huddles and calls are dropped on purpose.** A huddle link shows as a row with "Open in Slack". *Why:* #2979. A user token cannot join one.
18. **Messages raises Home rows; Home ranks them.** Messages owns the list; Home owns the one inbox. A `dm` or `mention` row exists in both with one id (§9.2). *Why:* Home §3.2 already defines `dm` and `mention`.
19. **Message text is untrusted everywhere.** Pages show plain text. Agents get it fenced (§11).
20. **Message text lives in `messages.db` and its file cache, and nowhere else.** Workflow triggers, `workflow_run.trigger`, node run input and output, and Temporal history hold ids only (§9.7). Retention and `purge` cover every store that keeps text (§11 table). *(agent default (lead, 2026-11-12))*
21. **Slack-only function lives in Release 2 and 3.** Release 1 writes nothing to Slack (§1). *(agent default (lead, 2026-11-12))*
22. **Meta's UX pass on the frames (#3188, 2026-11-12) is folded.** *(agent default (lead, 2026-11-12))* Twelve MUSTs and fourteen nits; the calls:
    1. Hand to agent (`t`) and make issue (`m`) move to Release 1, because neither writes to Slack. The agent draft and the reply-with-link box stay in Release 2 (§1, §9.5, §9.7).
    2. First run has no dead end: "Open my Slack apps" on steps 2 and 3, one plain line and one button for each failure (SND-MSG-002, 003, 024, 028, 030, Slack 5xx, a 429 on the first sync), and a first sync that shows "N threads found" and a next action (§6.8).
    3. `e` in Unread moves the local read pointer, with Undo. "Mark all read" is local and ships in Release 1. Neither writes to Slack or to Home (§6.2, §6.5).
    4. The peek shows the thread with its replies, or "Replies not loaded". Done, snooze, wrong class, read, hand-off and make issue each have a fixed result line (§6.1, §6.6).
    5. The footer shows `x` and `n`. The cursor starts on row 1 and returns to the last opened row (§6.2, §6.5).
    6. UI copy never names a release. Frames carry no Release badge; the release lives in the file name (§6.1 Look).
    7. Release 1 search is local only, with an empty and a limited state (§9.8).
    8. Hover and pressed are two looks, and the conversation states have fixed copy (§6.1, §6.6).
    9. Thirteen nits are text-only and taken; one is not (Review notes).

**Lead calls after autoplan round 1 (2026-11-12, lead).** These eleven are decided (1 to 7 from round 1, 8 to 11 from the lead's pass on the round 1 reviews). Each sits in the body under the decision or section named.

1. Work chat never goes to a hosted model by default. The gist and the brief are off, and they never run on DMs, group DMs or Slack Connect. A later opt-in per starred channel is allowed (decision 15, §9.1, §11, question 10).
2. Read marks stay local; `messages.syncReadToSlack` is the opt-in. No bare key writes to Slack. The composer sends on `Ctrl+Enter`. `Esc` has one job. The key sets have no duplicate letters and no case-only pairs (decisions 10 and 11, §6.5, question 11).
3. Triggers, run records and Temporal history hold ids, never text. `send_message` takes owner-written templates only. Model-written text goes through `draft_message`. An agent draft is never sent without the owner's send action. Retention and `purge` cover every store that keeps text (decisions 14 and 20, §9.7, §11).
4. The `xapp-` token lives in `TokenSet.ClientSecret` of the same entry. Messages has its own `slack:messages` connection, and workflow `post_message` refuses it. The Redaction rule extends in M1d (§5.1, §11).
5. Release 1 is read-only with polling only, no gist and no brief. Writes come after P0 and a week of real use. A kill rule and gate G guard it. The phases are re-cut, red and stubs come first, there is a `messages` setup step, `FakeSlackServer` is reused, the tools lane owns the shared files, and there is one credential path (§1, §5.1, §8.1, §14).
6. `home_links` replaces `thread_links`. `workspaces`, `sync_cursors` and the profile keys `messages.workspace` and `messages.starred` are gone. Home's id-only rows are views.db tables; text is in `messages.db` (§8.1, §8.5).
7. A TLA+ model of the outbox gates M2a (`specs/tla/MessageOutbox/`, M2t in §14).
8. **Store.** Home's merged spec (`2026-11-12-sindri-home-inbox-design.md`, "Events and migration") says Home rows are ledger events (`home.row.raised`, `.changed`, `.cleared` and the rest) that fill a `home_rows` table in `views.db`; `sindri views rebuild` replays them. Messages follows that: its Home rows are those events and that table, with ids and sender and place titles only. Message text stays in `messages.db`. There is no `rows.db`, and meta has nothing left to confirm (§8.1).
9. **Engine node I/O.** *(Corrected by call 13: the generic `prompt` node stores its filled-in prompt, so the text-fetch step is a Messages node, not `prompt`.)* On main, `NodeRunLimits.Cap` stores a node run's input and output as text up to 64 KB (`Core/Graph/Runs/NodeRunLimits.cs`), and `IWorkflowRunStore` writes that text. No hash-and-length mode exists, so this spec does not wait for one. The rule is: a Messages trigger and every Messages node emit ids only (conversation, `ts`, `thread_ts`, kind, `external`). A step that needs the text (`prompt`, `draft_message`) fetches it from `messages.db` in process at run time. It never writes the text to `workflow_run`, `workflow_node_run` or Temporal history, and its output carries ids plus a hash and a length at most. If the engine later stores a hash and a length for any node, that is a bonus, not a need. Test: `Run_record_holds_no_message_text` (W18), which runs a `messages.dm` workflow with a `draft_message` node on the fake and scans `workflow_run`, `workflow_node_run` and `temporal.db` for the fixture body (§9.7 item 2).
10. **Names are not content (answers round 1 S14).** A Home row title keeps the sender and the place ("Sam, DM", "Ada, #harbor") and never the message text. *Why:* a name is not message content, and a Home row with no name is not useful: "DM" alone tells the owner nothing about what to open. The cost is that a name reaches the ledger and so a session's Home reads. That is accepted: Messages' own session verbs (`list`, `conversations`, `thread`) still return ids and counts only (§8.4, §11), and W18 checks bodies, not names.
11. **One fake.** `FakeSlackServer` (`tests/Aw.Connectors.Testing/Slack/`) serves the unit and integration tests and the UI proofs. For a proof run, `scripts/slack-fixture.sh up|down` starts it on loopback with seeded data and points `SlackWebClient` at it. `FileSlack` and `AW_MESSAGES_ADAPTER` are dropped (§8.1, §14). *Round 2 found the mechanism missing; call 14 supplies it.*

**Lead calls after autoplan round 2 (2026-11-12, lead).** Calls 12 to 20 follow on from 11. Each sits in the body under the section named.

12. **Offline never queues a Slack write.** The composer refuses at once with SND-MSG-028 and keeps the text as a draft. A read mark syncs to Slack only while online. A 429 hold is capped at 2 minutes; then the send fails with its code and the draft stays. "Queued, sends within 2 minutes" is gone (§6.6, §8.3 steps 6 and 7, SND-MSG-005, 007 and 028).
13. **Call 9 correction.** The generic `prompt` node stores its filled-in prompt (`GraphBlockActivities.cs:62-87` on main), so the `slack-question-draft` template does not use `prompt`. It uses a Messages model node, `messages_classify`, that fetches the text in process and records ids, a hash and a length only (§9.7 items 2 to 4, W18, E0).
14. **Call 11 correction.** M1a adds a small loopback host, `Aw.Connectors.Testing.Server`, around `FakeSlackServer` for proof runs. The Slack base-URL override is accepted for loopback only, through the env var `AW_SLACK_BASE_URL` plus a marker file in a guarded scratch home. §8.1 defines the marker. The var goes in `.env.example` in M1d (moved by call 22). The "new test edge" claim is dropped. The host is named `Aw.Connectors.Testing.Server` (call 22).
15. **Reply check.** Polling also checks replies on threads from the last 3 days, capped at 20% of the one Slack rate budget per method (§5.2, §5.3). Round 3 adds rotation and a cap of 25 threads (§5.2).
16. **Retention.** It never deletes a thread that is in Needs reply or snoozed. Purge also deletes the derived columns (gist, its cache key, `my_last_ts`) (§8.1). Call 26 changes the skip rule to per thread, with a 180-day cap.
17. **Done and snooze.** Home's merged spec on origin/main defines the events (`home.row.done`, `.snoozed`, `.woke`, `.revived`) and the projected store, so Messages uses them. No `row_state` table, no replay (decision 13, §9.4, M1h).
18. **Keys.** Messages' four lists use Home's `[` and `]` splits (and `1` to `4`). `t` only means hand-to-agent. `p` opens a Pin / Cancel sheet. `s` opens the composer prefilled with the link. A bare `u` never writes. In the composer, `Ctrl+Enter` sends (call 2 stands). On a list row, `Ctrl+Enter` opens the row menu, as Home does. §6.4 says both, so no Home change is needed.
19. **Gate G.** It counts opens to read. `r` opens the permalink in Release 1. A cold-start fill from `search.messages` gives day-one rows (§1, §5.2, §14); call 25 changes the gate G sample.
20. **Plan.** `slack:messages` is stored with `Preset = null`, and the poller refuses it too. M1d stops waiting on the day-0 gate. M0a ships a probe manifest and its own `.env.example` lines. T1 adds the pinned doctor line, and the `messages` setup step stays Skipped until opt-in (§5.1, §14).

**Lead calls after autoplan round 3 (2026-11-12, lead).** These six are decided; 21 to 26 follow on from 20, each taking the reviewer's pick. Each sits in the body under the section named. All are `agent default (lead, 2026-11-12)`.

21. **Class 1 stays out of Home in Release 1.** Release 1 Needs reply and the Home collector carry class 3 and 2 only. A class 1 thread ("thanks!", a quiet thread reply, a keyword hit) shows in Unread until Home adds the kinds `thread-reply` and `keyword` (a Home follow-up, not yet in the Home spec). So Pat's "thanks!" raises no Home row, no toast and no tray count, and every Needs reply row has a Home row that Done can act on (§6.2, §7.2, §9.2, §9.9, M1h, M1i).
22. **Fixture and override placement.** The `AW_SLACK_BASE_URL` override, its tests and its `.env.example` line move from M1a to M1d, where `SlackWebClient` is built. `slack-fixture.sh` lists its own variables in `.env.example` in M1a. The marker home forces the owner-only file secret store, so a fixture never touches the real keychain entry; `down` deletes the marker and that file. A bash suite covers `slack-fixture.sh`, listed in `docs/testing/bash-suites.md` and the dotnet-builds list of `scripts/ci/bash-tests.sh` (§8.1, §14).
23. **Sync-fed triggers need an engine change.** On main a trigger fires only through an `IEventPoller` whose preset matches the connection, and `slack:messages` has `Preset = null`. Release 3 gets an engine phase row, M3d0: a store-fed event source keyed per connection, a catalog check per connection, and new kind names `messages.mention`, `messages.dm`, `messages.thread.reply`, `messages.keyword` (`slack.mention` already means "text" on `slack:work`). "No engine change needed" is dropped for the trigger half (§9.7, §14).
24. **Broadcast check matches Slack's real syntax.** On the rendered text, `send_message` and the composer chip refuse `<!channel>`, `<!here>`, `<!everyone>` and `<!subteam^…>` as well as the words, and any `<@…>` or `<#…>` the template did not name. Slot values escape `&`, `<` and `>` before they enter the template (§6.4, §9.7 item 6).
25. **Gate G is sized to a real week.** (a) is 30 marked rows seen over the week plus a recall check on 10 threads the owner answered in Slack. The week splits: days 1 and 2 beside Slack, days 3 to 7 with Slack closed except Open in Slack. M1i (collector and tray count) and M1h block X2 (§1, §14).
26. **Retention is per thread, capped, and reads the ledger.** It skips the thread, not the whole conversation. Snooze state comes from the ledger events, not `views.db`, so a rebuild or a stalled projector cannot delete a snoozed thread. Any skip ends after 180 days. The outbox gets `origin` and `created_at` for the daily cap, and landed rows keep their text only until retention (§8.1, §9.7 item 6, §11).

## 3. The parity ledger

Statuses and limits here are `agent default` unless a row names #2979. This table is the first draft of `docs/parity/slack.md` (M0b copies it there in the five-column shape of `docs/parity/linear.md`). Status words: `planned Mn` (a phase in §14), `partial` (with the limit), `differs` (works, but is Sindri-local), `blocked` (P0 must prove it), `dropped: why`. When a phase ships, the cell becomes `done` with an evidence path. Gap issues are filed at the start of each phase, `Part of #2979`.

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
| 9 | Edit own message | planned M4 (Release 3) | `chat.update`; own messages only |
| 10 | Delete own message | planned M4 (Release 3) | `chat.delete`; confirm in the UI |
| 11 | Rich text: bold, italic, strike, lists, quotes | planned M1 (render), M2 (compose) | `Mrkdwn` parser in Core; golden tests |
| 12 | Code blocks and inline code | planned M1 | rendered mono; diffs get +/- colour |
| 13 | Emoji and custom emoji | planned M1 | `emoji.list` cached; Unicode names built in |
| 14 | Files and images: view | planned M4 | fetched with the token to `files.slack.com`; disk cache capped |
| 15 | Files: upload | planned M4 | `files.getUploadURLExternal` + `completeUploadExternal`, `files:write` |
| 16 | Search with Slack's operators | planned M1 (local), M3 (Slack second pass, cross-source) | §9.8; Slack search is the second pass |
| 17 | Unread counts | partial, planned M1 | Slack gives `unread_count` for DMs only; channels are counted from `last_read` and the store **(guess; P0 line 2)** |
| 18 | Mark read, mark unread | differs, planned M1 (local), M2 (opt-in sync) | read marks are a local pointer; `conversations.mark` and the `*:write` scopes are used only with `messages.syncReadToSlack` or the `.` panel action, and only while online; "mark unread" sets the local pointer to the message before and never writes. The owner's phone keeps its own state until then |
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
| 30 | Slack Connect channels | partial, planned M1 | Release 1: a title-only row (sender and place) with "Open in Slack"; the text is not stored (`messages.connect.store: off`) and is read live and held in memory only while the conversation is open; inviting an outside org is dropped: admin-only |
| 31 | Channel: create, join, leave | planned M4 | `conversations.create`, `.join`, `.leave`; scopes `channels:write`, `groups:write` |
| 32 | Channel: mute | differs, planned M4 | no API; local mute (§9.9) |
| 33 | Channel: archive, rename, set topic | dropped: admin or owner rights, rare; open in Slack | |
| 34 | User profiles | planned M1 | `users.info`, `users.profile.get`, cached; the peek shows name, title, local time, status |
| 35 | Jump-to (`Ctrl+K`) | planned M1 | the #2978 palette with channel and person targets |
| 36 | Keyboard set | planned M1, M2 | §6.5 |
| 37 | Pins | planned M4 | `p` opens a Pin / Cancel sheet; `pins.add`, `.remove`, `.list` |
| 38 | Forward / share a message | planned M4 | `s` opens the composer prefilled with the permalink; the send is the write |
| 39 | Remind me about this | differs, planned M1 | a Sindri snooze on the thread (§9.4); `reminders.add` is not used so no Slackbot DM appears **(guess: owner may prefer both; question 7)** |
| 40 | Copy link to message | planned M1 | `SlackPermalink` pattern from the workspace name |
| 41 | Bookmarks in a channel header | dropped: read-only in Slack's header; shown as plain links in the channel peek if `bookmarks.list` works | low value |
| 42 | Canvases and Lists | dropped: a link opens in Slack | no stable user API in the probe plan |
| 43 | Slack's own workflows (Workflow Builder) | dropped: Sindri workflows replace them | |
| 44 | Message formatting preview, link unfurl previews | partial, planned M1 | links to known objects render as live rows; other links show their title only |
| 45 | Multiple workspaces | dropped for Release 1; one workspace first | question 2 |
| 46 | Also send to channel (thread reply) | planned M4 | `reply_broadcast` on `chat.postMessage`; a broadcast, so the submit menu guards it |
| 47 | Channel details: members, topic, description, files list | partial, planned M4 | `conversations.info`, `.members`; read-only; topic and description set are dropped (row 33) |
| 48 | User groups (`@team` mentions) | partial, planned M4 | `usergroups.list`; render and pick; `usergroups:read` |
| 49 | Typing indicators | dropped: needs Socket Mode user events, shows nothing a reply does not | low value |
| 50 | Mark all read; jump to first unread; jump to date | planned M1 (jump to first unread `n`; local Mark all read; `e` in Unread), M2 (opt-in sync to Slack), M4 (jump to date) | local pointer moves (row 18); Mark all read is a footer chip and the palette entry `messages.mark-all-read`, not a key |
| 51 | Emoji skin tone; custom-emoji search | planned M4 | `emoji.list` cache (row 13) |
| 52 | Copy message text | planned M1 | local clipboard; no Slack call |
| 53 | Slack DND silences Sindri's toasts | planned M4 | reads `dnd.info`; Sindri obeys it (row 24 sets it) |
| 54 | Sidebar sections and sorting | dropped: the four smart lists and starred channels replace them | decision 6 |
| 55 | Slack Connect invite acceptance | dropped: admin-only | row 30 |
| 56 | Mute thread (turn off notifications for one thread) | differs, planned M4 | local mute on `thread_state`; no API |

Row 29 and row 27 are the two where this ledger may contradict #2979's text. The draft says `blocked` until the owner runs P0. A `blocked` row needs an owner-accepted drop before W6 can pass.

## 4. What changes from today

| Today | After |
|---|---|
| `slack:work` REST connection, one write | stays for workflows. Messages has its own connection, `slack:messages`, and workflow `post_message` refuses it (§5.1) |
| `SlackEventPoller` polls one channel for workflows | stays until store triggers ship (M3d1). It and the sync service draw from one per-app, per-method rate budget (§5.3). Once store triggers ship, the poller is off for a workspace where Messages is connected, and a test proves two readers never run for one connection |
| `Aw.Connectors.Core.Slack` (bot token, channels only) | stays; the code index keeps its own narrow reader. Messages never feeds the index, embeddings or Ollama (`messages.db` is not a `context.sources` kind; an architecture test says so) |
| no Messages tab | `Messages` joins the nav next to Home |
| Slack events are workflow triggers only | a message becomes a store row first, then a trigger (ids only) and a Home row (sender and place only) |
| `sindri setup` has a Sources step that offers Slack as the bot index token | a new `messages` step: `Skipped` until the owner presses Connect Slack, then `NeedsYou`, then done when the `messages:token` doctor row is ok; the Sources step text says which Slack token it takes |

## 5. The Slack connection

### 5.1 Scopes and the token

The app is a Slack app the owner creates in the workspace from a manifest that Sindri prints (`sindri messages connect --manifest`) and opens as a prefilled "create app from manifest" link **(guess: Slack's `apps?new_app=1&manifest_yaml=` link; P0 line 8 checks it; the by-hand steps in §6.8 are the fallback)**. Before `connect --manifest` exists, the probe app is made by hand from the probe manifest (M0a). The manifest sets `token_rotation_enabled: false`. It asks for **user** scopes only. Scope names come from api's plan on #2979 and Slack's method pages; P0 line 1 reads the granted list back.

| Need | User scopes | Release |
|---|---|---|
| List and read channels, DMs, group DMs | `channels:read`, `groups:read`, `im:read`, `mpim:read`, `channels:history`, `groups:history`, `im:history`, `mpim:history` | 1 |
| People and emoji | `users:read`, `emoji:read` | 1 |
| Search | `search:read` | 1 |
| Send, edit, delete | `chat:write` | 2 |
| React | `reactions:read`, `reactions:write` | 2 |
| Mark read (only with the opt-in) | `channels:write`, `groups:write`, `im:write`, `mpim:write` | 2 |
| Files | `files:read`, `files:write` | 3 |
| Status, DND | `users.profile:write`, `dnd:write`, `dnd:read` | 3 |
| Pins | `pins:read`, `pins:write` | 3 |

Release 1 asks only for the first three rows, so an admin approves a read-only app. The probe (§5.5) uses a second manifest with every scope in the table, `docs/sindri/slack-probe-manifest.yaml`, because the probe's line 6 posts, reacts and marks. A later phase that needs more scopes shows "Needs scope X: reconnect" on the feature and keeps everything else working (SND-MSG-003).

**One credential path.** `sindri messages connect` and the Connections tab's paste field are the only ways in. Both read the token at a no-echo prompt (the `ISecretPrompts` pattern of `IndexConnectVerbs.cs`); there is no `--key-env` for Messages, so the token never passes through a shell environment, history or `ps`. The command checks the prefix before any call (SND-MSG-030): `xoxb-` (bot), `xapp-` (app-level) and `xoxc-` (cookie) are refused on this path with the sentence "Messages needs the User OAuth Token (`xoxp-`) from OAuth & Permissions, User Token section". It then reads `auth.test`, stores the token under the connection `slack:messages` (a legal name: `<service>:<label>`, lowercase letters, digits and `-`), records the workspace id and the user id on that connection row, and refuses a token whose workspace differs from the one it was connected to (SND-MSG-024). The token lives in one `IConnectionSecrets` entry, so `connection remove` deletes it, and `sindri messages disconnect` and `purge --all` do the same. Where the box has no keychain the entry is an owner-only file; `sindri doctor` says which (row `messages:token`). The later app-level token `xapp-` (Release 3) is stored by its own verb, `sindri messages connect --app-token`, with the same no-echo prompt; that verb refuses every prefix except `xapp-`, and it writes `TokenSet.ClientSecret` of the same entry: one entry, one delete. Neither token appears in an argument list, a log, an error, a ledger row or a response.

`slack:messages` is stored as a hand-made connection with `Preset = null`, the workspace id and user id in its `Config`, and a `probeUrl` for `auth.test`, so `sindri connection test slack:messages` and the Connections tab's Test button call Slack and show red for a revoked token (without it, a null preset reports "connected" with no call). Test: `Connection_test_calls_auth_test`. The name `slack:messages` belongs to the Messages verb: the generic `connection add slack:messages` is refused with a pointer to `sindri messages connect` (test `Generic_connection_add_refuses_the_messages_name`, E1). Messages assumes a non-rotating `xoxp-` token (the manifest turns rotation off) and shows Reconnect on `invalid_auth`. `RestActions.For(null)` is empty, so a workflow `post_message` naming it is refused today with no new column, migration or SND-CONN code. `SlackEventPoller` and every trigger path refuse it too (preset `slack` only). Tests: `Post_message_refuses_the_messages_connection` and `Poller_refuses_the_messages_connection`. *Why:* `RestActions.cs` already has `post_message` to `chat.postMessage`, and that path has no named-conversation list, no daily cap and no broadcast ban (lead call 20).

| Slack token | Where it comes from | Used by |
|---|---|---|
| `xoxb-` bot token | the index app (`sindri index connect slack`) | the code index, channels only |
| `xoxp-` or `xoxb-` token | `sindri connection add slack:work` | workflows (`post_message`, the one-channel poll); not for Messages |
| `xoxp-` user token | OAuth & Permissions, User Token section | Messages (`slack:messages`) |
| `xapp-` app-level token | Basic Information, App-Level Tokens | Messages Socket Mode, Release 3 |

**Target time to first success: 10 minutes, with no admin gate** *(agent default (devex round 1))*: print the link (1 min), Create and Install on Slack (3), copy and paste the token (2), first sync (2 to 4). With an admin gate the owner waits for approval and SND-MSG-019 says so. The "Connect Slack" doc is timed against this number.

### 5.2 Live: Socket Mode or poll

Release 1 uses the two poll rows. The Socket Mode row is Release 3 (decision 4).

| Path | How it works | Needs | Status |
|---|---|---|---|
| Socket Mode (Release 3) | `apps.connections.open` with the `xapp-` token returns a WebSocket URL; Slack pushes events; Sindri acks each envelope. Sindri accepts only a `wss://*.slack.com` URL, follows no redirect and logs the host only. Slack refreshes sockets often: Sindri opens the new socket before it closes the old one and reads history only if the overlap failed | the app subscribes to user events (DMs and channels the owner is in) | **(guess)** Slack's page shows `bot_events` only. P0 line 5 tests a user-event subscription |
| Poll, hot set | `conversations.history` with `oldest=<cursor>` for conversations with unread, mentions, an open view or a star | Tier 3 history (50 or more per minute) | **(guess)** P0 line 3 reads the real limit |
| Poll, search | `search.messages` for `to:me` and `has:...` as a change detector for DMs and mentions: 1 call per 20 s uses 3 of Tier 2's 20 calls a minute | `search:read` | **(guess)** fallback when history is at 1 per minute; P0 line 4 |
| Poll, cold set | the rest, rarely | Tier 3 | only on Tier 3 |
| Poll, thread replies | for threads I am in with activity in the last 3 days (set capped at 25 threads, newest owner activity first, round-robin): `conversations.replies` of the root with `oldest=<last seen reply ts>` so the answer is small, at most 20% of the bucket per method (10 calls a minute on Tier 3, so 5 per 30 s cycle and a full rotation of 25 threads in up to 3 minutes). A 429 mid-rotation skips and keeps the position. Backfill and open-thread reads draw from the same 20%, open-thread reads first. The 3 days, 20% and 25 are `agent default (lead, 2026-11-12)`. `conversations.history` returns top-level messages only, so without this a reply in an older channel thread is invisible. The status line and gate G (b) state the bound: "replies in older threads: up to 3 minutes" | Tier 3 | on a 1-per-minute workspace it runs only for the thread the owner opens, and the thread shows `partial` (parity row 5, W10) **(guess; P0 line 3)** |
| Cold-start fill | once, after the first connect: `search.messages` for `to:me` and `from:me` over the last 30 days (`messages.retain.days`) at no more than 5 of the 20 Tier 2 calls a minute, so the first-sync line shows "N threads found" while it runs and when it ends, even when N is 0 (§6.8), and the first day has a sample for gate G (a) | `search:read` | **(guess; P0 line 4)**. The hits are stored as `partial` messages (§9.8) |

Order of preference in Release 1: hot-set polling, then search polling. Sindri picks the best path P0 proved, writes the choice to `messages.sync` (`auto`, `poll`; Release 3 adds `socket`), and shows it in the status line and in `sindri doctor` (SND-MSG-020 is an `Ok` row with detail text when Socket Mode is not available). The poll cursor per (connection, kind) is the ledger's `poll_cursor` table; the per-conversation newest `ts` and gap list are columns on `conversations` (§8.1). A disconnect makes one history read per hot conversation to fill the gap. On a 1-per-minute workspace that read is queued by hotness and the rest show a gap marker (W10).

### 5.3 Rate limits and the honest status

The sync service keeps one token bucket per Slack method and app (the app id `auth.test` returns), seeded from the `Retry-After` and tier headers P0 records. A 429 pauses that method only. `SlackEventPoller` draws from the same bucket only when its connection has the same app id; two different apps never share one. Slack counts the app, the workspace and the method together. Messages has the higher priority, so a workflow poll can never starve the sync. Test: `Two_readers_draw_from_one_bucket`. The page header shows the state in words:

- `Live (socket)`, `Live (polling every 30 s)`, `Behind: history is limited to 15 messages per minute`, `Offline: last read 09:41`.
- A conversation with a hole shows a **gap marker** line: "12 older messages not loaded. Load older (1 per minute)". The marker is a conversation row, so it scrolls with the messages. A test fails if a gap has no marker (W10).
- Backfill: on Tier 3, `messages.backfill.days` (default 14 for DMs, starred channels and threads in Needs reply; 3 for the rest) runs at 20% of the bucket. On a limited workspace it is off. Backfill never goes beyond `messages.retain.days`.

### 5.4 Write-through methods

| Action | Slack method | Scope | Notes |
|---|---|---|---|
| Send, reply | `chat.postMessage` (`thread_ts` for a reply) | `chat:write` | the `actId` goes in `metadata.event_payload`; a retry first looks for it **(guess; P0 line 6)** |
| Edit | `chat.update` | `chat:write` | own messages only |
| Delete | `chat.delete` | `chat:write` | own messages only |
| React | `reactions.add`, `reactions.remove` | `reactions:write` | already-reacted is a no-op |
| Mark read, unread | `conversations.mark` | `*:write` | only with `messages.syncReadToSlack` or the `.` panel action "Mark read in Slack" |
| Schedule | `chat.scheduleMessage` | `chat:write` | up to 120 days **(guess)** |
| Upload | `files.getUploadURLExternal`, `files.completeUploadExternal` | `files:write` | |
| Status, DND | `users.profile.set`, `dnd.setSnooze`, `dnd.endSnooze` | as named | |

These are the Release 2 and 3 writes. Each is one row in the outbox table (§8.3). A 429 holds a write for at most 2 minutes (`agent default (lead, 2026-11-12)`), with a countdown ("waiting, Slack asked us to wait 12 s"); after that the line fails with SND-MSG-005 and Retry. Offline, no write is held at all (§8.3 step 7).

### 5.5 The P0 probe

api's plan on #2979 gives `scripts/slack-probe.sh`: owner-run, read-only by default, same shape as `scripts/linear-probe.sh` (the key reaches curl through a 0600 config file, the host must be `slack.com` or loopback, output is counts, booleans, error codes and header numbers; no message text, names or channel names). This spec adds the lines below to that plan. Each guess in this spec points at a line, and each result becomes a ledger cell. `--write` needs `--channel <id>` of a private scratch channel the owner made, deletes what it posted in the exit trap, and refuses without the flag (exit 2), as `linear-probe.sh` does with its team and label. Both new scripts get a bash suite (`scripts/tests/slack-probe.test.sh`), listed in `scripts/ci/bash-tests.sh` and `docs/testing/bash-suites.md`. The probe's variable is `AW_SLACK_PROBE_KEY`; its name holds KEY, so `.env.example` lists it as `# AW_SLACK_PROBE_KEY=` with no value (ENV-EXAMPLE-004), in M0a itself, because `check-env-example.sh` reads bash too. `--write` reuses the same key: the probe app is the owner's own.

| Line | Question | Result changes |
|---|---|---|
| 1 | Token kind and granted scopes (`auth.test`, `x-oauth-scopes`) | §5.1 scopes; rows 2, 3, 14 to 24 |
| 2 | Can the token read DMs, group DMs, threads? Do `conversations.info` and the `last_read` field give a channel unread count? | rows 2, 3, 4, 17 |
| 3 | History and replies tier: `Retry-After` after a burst, the `limit` ceiling, the `x-rate-limit` tier | decision 5, §5.3, W16 |
| 4 | `search.messages` ok? Error code? Tier? Does `to:me` work as a change detector? | §5.2 third row, rows 16, 6 |
| 5 | `apps.connections.open` with a user token and with an `xapp-` token; can the app subscribe to user events? | decision 4, §5.2, bet 2 |
| 6 | With `--write` in a scratch private channel: post with `metadata`, read it back, update, react, mark, delete, schedule, upload | decision 9, rows 9, 10, 18, 21 |
| 7 | Slash commands: does `chat.command` exist and accept a user token? Can a user token trigger a block action? | rows 27, 29 |
| 8 | Is the app blocked by workspace approval for any scope? Does the prefilled manifest link work? | question 1 (day-0 gate with question 8) |

## 6. The page

### 6.1 Layout

Three columns on the #2978 grammar: a left rail, the list or conversation, and the peek. 32 px rows (compact, the default) and 40 px (comfortable), set by `home.density` *(agent default (Home spec))*. The conversation takes the middle column instead of a Full surface: it is a third shape of "full", on purpose, because chat needs the rail and the peek beside it.

```
┌ top bar 40 ───────────────────────────────────────────────────────────────────────────────────────────────┐
│ Messages   3 need reply                    Ctrl+K search or jump    Live (polling every 30 s) · joi        │ header 48
├ rail 220 ────────────────┬ list or conversation ─────────────────────────────┬ peek 440 ────────────────────┤
│ ▸ Needs reply        3   │ ■ Sam    can you ship the fare table today?  DM  │ Thread · #harbor              │
│   Unread            14   │ ◆ Ada    #harbor  review #3120 when you can  2h  │ Ada 09:02                     │
│   Later              2   │ ● Kim    #harbor  thread: seat holds  new   3h   │  @joi review #3120 when you   │
│   Drafts             1   │                                                  │  can ┌─────────────────────┐ │
│ ─ Starred ───────────────│                                                  │      │ ◎ #3120 Seat holds  │ │
│   # harbor           2   │                                                  │      │ CI green · asked of │ │
│   # ferry-ops            │                                                  │      │ you · ada      32px │ │
│   Sam                    │                                                  │      └─────────────────────┘ │
│   Kim, Lee               │                                                  │ ┌ Open in Slack (r) ────────┐ │
│ Ctrl+K all channels      │                                                  │ │ Opens the thread in     │ │ 
│                          │                                                  │ │ Slack.                   │ │ 
│                          │                                                  │ └───────────────────────────┘ │
├──────────────────────────┴──────────────────────────────────────────────────┴───────────────────────────────┤
│ j/k · Space peek · Enter open · r Slack · e done · h snooze · t agent · m issue · x wrong · n next · ? keys│ footer 28
└────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

The footer shows only keys that work in this release: in Release 1 `r` reads "open in Slack", `a` and `l` are absent, and `t agent`, `m issue`, `x wrong class` and `n next` are on it (the footer per mode is in §6.5; the mock above shortens the labels to fit). The rail is collapsible from its header button and the palette id `messages.rail` (not `[`, which is Home's previous split). Below 1,100 px the rail collapses to 48 px first; below 900 px the peek replaces the conversation. A collapsed rail item shows its first two letters (or a glyph for a list) and a count dot, so two channels never share an icon. The top bar shows the Needs reply count. The OS notification and the tray count are Home's, not Messages' (§9.9). The rail shows every list's count, and a DM can count in both Needs reply and Unread.

**Width budget.** At 1,000 px with the rail at 48 and the peek at 440, the list gets 512 px. A Needs reply row keeps its fixed part (glyph, who, age, the place) at 200 px, the title takes the rest, and the facts drop in this order when the title would be under 120 px: linked-object chips, then the reason chip, then the place. On the cursor row the buttons drop their labels (icon only) before the title shrinks under 120 px. Text scale 1.3 scales text and row heights only. Conversation lines grow with text scale, and 20 px is the minimum.

**Needs reply row.** Glyph (class: direct question a filled square, review ask a diamond, FYI a circle), who, the place (`DM` or `#channel`), the one-line title (Release 1: the first 80 characters of the newest message not mine; the model gist is Release 3, §9.1), age, and on the cursor row three buttons (Reply, Snooze, Done) and a More button that lists Hand to agent (`t`) and Make issue (`m`) with their keys. In Release 1 Reply opens the thread in Slack; its tooltip says "Opens the thread in Slack." Facts are fixed columns, never colour alone. The reason chip shows only when it adds what the place column does not say ("mention", "review ask", "keyword"); a DM row has no "DM" chip.

**Conversation.** Open with `Enter` from a list or `Ctrl+K`. It fills the middle column. See §6.3.

**Peek.** `Space` on a list row shows the thread, or on a conversation line shows that message's thread, or the linked object (a PR's summary, an issue's fields) when the cursor is on a link row. The header reads "Thread · #harbor" with a "Copy link" button; it shows no internal id. Under it the peek lists the root and every reply the store holds, each with name and time ("Kim 08:20", "Ada 08:41"). Three states of the replies: loaded (the list); never read, which says "Replies not loaded. Load replies (1 per minute)" with a button that shows a countdown ("next in 41 s") while it waits and is off when offline ("Offline"); and none ("No replies yet."). In Release 1 the peek's one button is "Open in Slack (r)", under the line "Opens the thread in Slack." UI copy never names a release; in Release 2 the composer takes that place. Home's peek of a `dm` or `mention` row shows the same button (Home's `r` Reply composer must be hidden for those kinds until M2c; that is an open edit to the Home spec, §9.2). The Home peek of a Messages row gets only Home's keys.

**Look.** The design lane draws it in M1j from `Theme/Tokens.axaml` and `Typography.axaml`; the roles are fixed here. Name, time, body and code each take a type role from `Typography.axaml`. An avatar is a 20 px disc with initials and a colour hashed from the user id; no remote image loads. Unread is a bold name plus a dot. A mention is an accent bar on the left and the word "you". The three classes are three shapes (a filled square for 3, a diamond for 2, a circle for 1) and the peek names the class in words. Row, chip and button have three different looks: rest, hover and pressed. Hover lifts the fill one token step above rest. Pressed drops the fill one step below rest and moves the content down 1 px. A frame shows all three side by side for a row, a chip and a button. The cursor ring stays inside the list gutter (20 px in) and never runs to the window edge. A code such as SND-MSG-028 stays on one line (no wrap) or sits under "Details". A search row has no radio circle; the cursor row is a plain highlight. A number in a frame, such as "38 ms", is a placeholder: timings are asserted only in `perf` (W13). At 1,000 px the footer never clips mid-word: chips drop in the order of §6.5 and `? keys` stays. **UI copy never names a release.** No frame carries a Release badge; the release stays in the frame's file name. Motion is 120 ms, and none under reduced motion. Frame 12 shows hover and pressed.

### 6.2 The four lists

| List | Holds | Order | Leaves when |
|---|---|---|---|
| Needs reply | threads that meet the one definition in §7.1. Release 1 shows class 3 and 2 only (call 21) | class, then blocks, then oldest | the §7.1 exclusions: I reply, press Done, snooze, or the thread links an issue with an owner and nothing new asks me. A read mark never removes a thread |
| Unread | conversations with messages after my local read pointer, not muted. In Release 1 it is also where a class 1 thread shows | most recent first | the pointer passes them: lines scroll into view, `End`, `e` on the row, or Mark all read |
| Later | threads and messages I flagged (Release 2; the rail entry is hidden while no flag exists, so it hides again when the last flag goes; an open Later list shows its empty state until the owner leaves it) | newest flag first | I remove the flag |
| Drafts | my unsent drafts, owner-written and agent-written (Release 2) | oldest first | I send or delete |

Counts on the rail are computed from the store. Done and snoozed rows are not counted. Unread and Needs reply overlap: a DM I have not read is in both.

**Where the cursor starts.** On open of a list the cursor is on the first row (the New row when one exists), and an open peek shows that row: in `messages-needs.png` and its width frames (`-1000`, `-1200`, `-rail-collapsed`) that is Lee's "got a sec?" in the New band. The other Needs frames (`messages-needs-more-menu.png`, `messages-needs-return.png`, `messages-result-*.png`, `messages-search-*.png`) are drawn after a move or an action, so their cursor sits where that move left it, not on row 1. On return from a conversation (`Esc`) the cursor is on the last row opened. An empty list has no cursor and shows its empty state. Test: `Cursor_starts_on_row_one_and_returns_to_the_last_opened`.

**Clearing Unread.** A conversation leaves Unread by three local acts: read it, press `e` on its row (the pointer goes to its newest line), or press "Mark all read" in the footer (every listed pointer goes to its newest line). None writes to Slack. None writes a `home.row.done` event, because Home never raised these rows. With 14 unread conversations the owner reaches 0 in one press, not 14 opens. Test: `E_in_unread_moves_the_pointer_and_writes_no_home_event`.

### 6.3 The dense conversation

- **One line per short message**, grouped by author: the name and time show on the first line of a run; later lines show only the time, on hover. A run breaks after 5 minutes of silence or a different author. Line height 20 px; rows are virtualized.
- **Threads collapsed inline**: "Ada, Sam and 2 more · 6 replies · last 09:02" as one 28 px line directly under its own root, with the same count the peek shows. `Enter` opens the thread in the peek (`t` is hand to agent only).
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
- **Ask an agent to draft** (button, palette id `messages.draft-agent`): opens the hand-off dialog first (§9.7 item 8: it names the provider, the size and that the session can run shell), and pressing Send in that dialog is the owner's explicit act. Only then it starts a `draft_message` run with the thread as fenced input and a note field. The result is a separate draft row (origin `agent:<run>`, versioned). It never replaces text the owner typed. It is never sent by that act (§9.7).
- **The target is always named** in the composer header: "#harbor, 412 people" or "Ada (DM)".
- **Send** is `Ctrl+Enter` *in the composer* (lead call 18; call 2 stands): a plain text reply has one action, so a menu would be a pointless second press. This differs from a Home *row*, where `Ctrl+Enter` opens the row's submit menu with the primary preselected and `Enter` commits it. On a Messages list row `Ctrl+Enter` does what Home does (opens the row menu). No Home change is needed. `Enter` is a newline in the composer (and never sends while an IME is composing; `Esc` during IME composition belongs to the IME). The submit menu (Send primary, Send and mark done, Schedule shown disabled with "arrives with scheduled send" until M4; `Enter` commits it) opens first, instead of sending, when the text holds `@channel`, `@here` or `@everyone`, when the channel has more than `messages.confirmAboveMembers` members (default 25), or when it is a Slack Connect channel. The broadcast test is Slack's own syntax on the text as it will be sent (call 24): the words, or `<!channel>`, `<!here>`, `<!everyone>`, `<!subteam^…>`. The primary then reads "Notify 412 people".
- **Agent drafts.** One rule: an agent draft is never sent without the owner's send action (`Ctrl+Enter` or the button). A workflow, a session, a timer or a row key is not that action. The button reads "Review and send" until the owner edits or opens the menu, and the draft shows its label, the run link and one chip per risk (a broadcast by the same detector, a link, an attachment). The menu opens first when the draft holds any of the three. Opened from the Drafts list or a Home Needs-you row, approve is a button, never a row key.
- Autosave after 3 characters on leaving the composer (Zulip's rule). Drafts are local (parity row 20). In Release 1 the composer does not exist.
- **Offline** the text box stays editable and keeps focus; only Send and `Ctrl+Enter` are off. The line above the box reads "Offline: your text is kept as a draft. Send when you're back." (SND-MSG-028) and is announced politely when it appears. `Ctrl+Enter` shows that line and no toast. Autosave runs as usual, so a person mid-sentence loses nothing. The draft is the only thing stored. Nothing is queued (lead call 12).
- **Two words for two things.** A *toast* is Home's (an OS or in-app notification counted by the tray). Messages' one-line "Sent." line with its **Follow up if no reply** button is a *confirmation line*: in-app only, not an OS notification, not counted by the tray.
- A focused text field keeps its own `Ctrl+Z`; the 5 s Undo of `Ctrl+Z` applies when no text field has focus.

### 6.5 Keys

Keys follow the #2978 grammar. `?` shows the live set. The rule is decision 11: one set, with Slack-only message keys added.

**List mode** (cursor on a list row; Home's keys):

| Key | Does | Writes to Slack? |
|---|---|---|
| `j` `k` | move | no |
| `Space` | peek | no |
| `Enter` | open the conversation / thread full | no |
| `r` | Release 1: opens the thread's permalink in Slack (a reply by hand; gate G counts opens to read, not these). Release 2: opens the composer in the peek | no (sending does) |
| `e` | In Needs reply: Done (leaves Needs reply); Undo `Ctrl+Z` for 5 s. In Unread: mark read, which moves the local pointer to the newest line of that conversation; Undo `Ctrl+Z` for 5 s; no Slack write and no `home.row.done` event | no |
| `h` | snooze picker (§9.4), opened beside the cursor row; `f` inside it toggles "unless replied" (Release 2; Release 1 uses Home's picker as it is). Offered on Needs reply rows only: an Unread row has no Home row to snooze | no |
| `m` | make issue from the thread (§9.5, Release 1) | no (Linear) |
| `a` | attach the thread to an issue (Release 3) | no |
| `t` | hand to agent (Home §5.1 picker); the only meaning of `t`. Release 1: the session reads the thread and writes no draft (§9.7 item 8) | no |
| `u` | mark unread: moves the local pointer only; `Ctrl+Z` undoes it for 5 s. It never writes to Slack, with or without the opt-in | no |
| `l` | flag for Later (Release 2); follow and star are in the `.` panel; `Ctrl+Z` undoes it for 5 s | no |
| `n` | next unread / needs-reply row | no |
| (no key) | Mark all read: a footer chip in Unread and the palette entry `messages.mark-all-read`. It moves every listed conversation's pointer to its newest line (muted ones are skipped); one 5 s Undo restores them all; local | no |
| `x` | wrong class: logs this row's class as a miss (the `messages.class.missed` event, ids only), shows "Marked wrong" on the row; `Ctrl+Z` undoes it for 5 s (dogfood, Release 1; gate G (a) reads it) | no |
| `Esc` | back one layer (below) | no |
| `/` | filter this list. Search and jump is `Ctrl+K` (header, rail and palette all say `Ctrl+K`) | no |
| `[` `]` `1` to `4` | previous / next list, jump to a list, as Home's splits; the lists are Needs reply, Unread, Later, Drafts | no |
| `R` | re-rank now (Home's key, a settle point) | no |
| `.` | actions panel with keys shown; holds "Mark read in Slack", "Mark unread in Slack", follow, star and the catch-up brief (palette id `messages.catch-up`) | the panel's action does, on commit |
| `c` | new message (pick person or channel, Release 2) | no (sending does) |

Starred channels and "all channels" are reached with `Tab` into the rail (one tab stop; up and down move, `Enter` opens).

**Message mode** (cursor on a message line) has every list key above that fits the thread (`e`, `h`, `m`, `a`, `t`, `l`, `r`, `u`, `n`), plus the Slack-only keys:

| Key | Does | Release |
|---|---|---|
| `i` | edit own message (up-arrow from an empty composer does the same) | 3 |
| `o` | react: opens the emoji picker (arrows move in two dimensions, typing filters, `Enter` picks); the pick commits one chip, reversible by the same pick | 2 |
| `s` | share (forward): pick a conversation, then the composer opens in the peek prefilled with the permalink; `Ctrl+Enter` sends with the guards of §6.4. A pick writes nothing | 3 |
| `p` | pin: opens a one-line sheet "Pin to #harbor" with Pin and Cancel buttons, default Cancel (the shape of the Del confirm). Nothing is written until Pin | 3 |
| `Del` | delete own message; the confirm sheet defaults to Cancel | 3 |
| `Enter` | open the thread in the peek | 1 |
| `Home` `End` | top, bottom (the lines shown count as seen). Like scrolling, they have no Undo of their own; `u` moves the pointer back | 1 |
| `[` `]` | no effect in message mode; `Esc` backs out to the list first | 1 |

`→` does nothing on a message line, as on a Home row. `Tab` stays focus traversal. `g` is not a key. Every key has a palette entry with its key shown (Superhuman's rule). The footer shows a chip, "List keys", "Message keys" or "Composer", and a screen reader announces a change of mode ("Message keys: reading #harbor"). Pickers, menus, sheets and the brief show their own footer keys and no mode chip. A bare letter fires only when no text field has focus. The search scope switch has no key: the scope chip is a button, and the palette has `messages.search-scope`. `B` (Release 3) runs the catch-up brief (§9.6).

**Footer per mode (Release 1).** It shows only keys that work. At 1,000 px chips drop from the right in this order: `t`, `m`, `u`, `Esc`. `x`, `n` and `? keys` stay, because gate G reads `x` and `n` is the walk through the list.

| Mode chip | Footer, left to right |
|---|---|
| List keys (Needs reply) | j/k move · Space peek · Enter open · r open in Slack · e done · h snooze · t hand to agent · m make issue · x wrong class · n next · ? keys |
| List keys (Unread) | j/k move · Space peek · Enter open · r open in Slack · e mark read · t hand to agent · m make issue · n next · Mark all read · ? keys |
| Message keys | j/k move · Enter thread · r open in Slack · e done · h snooze · t hand to agent · m make issue · u mark unread · n next · Esc back · ? keys |


**Read marks are local.** A line is read after it has been on screen for 1 s with the window focused and an input in the last 2 minutes (for a screen reader, the line under the cursor, not the viewport). The pointer moves to the newest line seen; no key moves it except `u`, `End`, `e` in Unread and the palette entries (Mark all read among them). The pointer is the later of the local pointer and Slack's `last_read_ts`, so reading on the phone clears Unread here, except in a conversation where the owner pressed `u`: there the pointer stays until the owner reads past it. Nothing here writes to Slack by a bare key. With `messages.syncReadToSlack` on, the owner's own opt-in, a forward move from viewing is sent as `conversations.mark` while online, one mark per changed conversation, and the page header says so ("Read marks sync to Slack"). `u` and `End` never sync. Offline nothing is queued: the header says "Read marks not synced: offline", and on reconnect the pointer is compared with Slack's `last_read_ts` and one mark per changed conversation is sent. That is a fresh compare, not a queue: no act row is stored.

**`Esc` has one job: close or back out one layer.** The order is: picker or menu, then the composer (unsaved text stays as a draft and the composer blurs), then the peek, then the conversation, then the list. When Messages was opened from a Home row (`Enter` on a `dm` or `mention` row), the last `Esc` returns to Home with the cursor on that row. `Esc` never marks anything read. Test: `Esc_out_of_conversation_does_not_mark_unseen_lines_read`.

### 6.6 States, per surface

Every surface also shows the one status line of §5.3. Banners name the cause and the fix. "Offline" (no network) is a different state from "Token expired" (revoked): offline keeps everything readable and queues nothing, ever (lead call 12); revoked says "Reconnect". Rows that name a write belong to Release 2.

| Surface | Loading | Empty | Stale or behind | Disconnected or revoked | Rate limited | Failed write |
|---|---|---|---|---|---|---|
| Needs reply | 6 skeleton rows from the cache | "You're caught up." with the next snooze time and, when Unread holds rows, an "Open Unread (14)" button | rows stay; header says "Behind: 12 min" | "Slack token expired. Reconnect" (SND-MSG-002) | header "Behind" + reason | row keeps its place; "failed: reason" + Retry |
| Unread | skeleton | "Nothing unread." | gap markers in conversations | as above | as above | n/a |
| Conversation | cached lines paint; new ones stream in | three states. (a) Slack returned zero: "No messages yet. Say hi." (b) the store is empty and history is not read: a gap marker, "History not loaded. Load older (1 per minute)", never "Say hi". (c) Slack Connect with `messages.connect.store: off`: lines are fetched live and held in memory; offline or limited it says "Text from other organisations is not kept here. Open in Slack." | gap marker | read-only with a banner | gap marker with Load older | the line stays, greyed, red "failed: reason", Retry and Discard |
| Peek thread | root first, replies stream | "No replies yet." (Slack returned none) | "replies loaded 09:12"; never read: "Replies not loaded. Load replies (1 per minute)" | as above | "Replies not loaded. Load replies (next in 41 s)" | as above |
| Composer | n/a | placeholder with the target | n/a | Send off, "Reconnect to send"; the box stays editable; offline: "Offline: your text is kept as a draft. Send when you're back." (SND-MSG-028) | "waiting, 12 s" (up to 2 minutes, then failed with Retry) | the draft is kept and the failure shows once, as the red line on the thread (no second banner above the box). On success the line turns normal and a 5 s confirmation line offers "Sent." with a **Follow up if no reply** button |
| Search, local only (Release 1) | local results paint first | "Nothing in the store yet. Open a conversation to load it." | "Local results only: Sindri has not read older messages." | "Local results only" | n/a | n/a |
| Search with the Slack second pass (Release 3) | local results paint first, "Searching Slack..." | "Nothing in the store. Search Slack?" | "Slack search unavailable: limit" | "Local results only" | "Local results only, Slack limit" | n/a |
| Catch-up brief | plain facts paint, sentence streams | "Nothing changed." | "Written from the store as of 09:12" | n/a | n/a | "Model off: facts only" |
| Scope missing | the feature is disabled, not hidden | n/a | n/a | n/a | n/a | "Needs scope `files:write`. Reconnect" (SND-MSG-003) |

**Offline and success, for the first table's surfaces.** Offline: every list and conversation stays readable from the store with the header "Offline: last read 09:41"; every write is refused at once with SND-MSG-028. Success: Done leaves the row in place with a 5 s Undo; a landed send turns the line normal and shows the 5 s "Sent." confirmation line with its Follow up button; a search shows its source tags. Load older is a button with its own row: the countdown "next in 41 s", disabled while a call runs and while offline ("Offline"). Offline, Retry on a failed line, react, pin, delete and share are disabled with the reason "Offline", not clickable and then refused with SND-MSG-028; Retry comes back as soon as the header says Live. The emoji picker offline shows "built-in names; picking is off until you're back". First run offline: step 3 stores nothing and says "Offline: connect when you're back"; the token field keeps its text for the session (frame 8 has the variant).

**Result lines.** Each lasts 5 s, pauses while hovered or focused, and takes `Ctrl+Z` for Undo where it names one. The line replaces the row's content in place, so nothing around it moves; at 5 s the list closes the gap and the cursor lands on the next row.

| Action | The line | The row afterward |
|---|---|---|
| Done (`e`, Needs reply) | "Done. Undo" | gone at 5 s; the cursor is on the next row |
| Mark read (`e`, Unread) | "Marked read. Undo" | gone at 5 s |
| Mark all read | "14 conversations marked read. Undo" | the list then shows "Nothing unread." |
| Snooze (`h`) | "Snoozed until Mon 9:00. Undo" | gone at 5 s; under Snoozed as "back Mon 9:00" |
| Wrong class (`x`) | "Marked wrong. Undo", and a "Marked wrong" chip stays on the row | the row stays in place |
| Hand to agent (`t`) | "Handed to Claude Code. Open session" (the provider the owner picked) | the row stays; no Undo, because a session started |
| Make issue (`m`) | "Created ENG-412. Open issue"; when the issue has an owner, also "Moved out of Needs reply" | the row shows the key chip; no Undo |

**More surfaces.** Copy is fixed here; the cells that say "as above" follow the table above. Offline means no network.

| Surface | Loading | Empty | Error | Offline | Success or write-fail |
|---|---|---|---|---|---|
| Later | skeleton | "Nothing flagged. Press `l` on a message." | "Could not read the store" (SND-MSG-025) | local, fine | flag toggles at once; no Slack call |
| Drafts | skeleton | "No drafts. Anything you start typing and leave is kept here." | SND-MSG-015 shows "changed in another window" | local, fine; the list says drafts are local | send turns the draft into a line; a failed send keeps the draft |
| Rail | counts paint from the store | "No starred channels yet. Star it from the `.` panel, or find it with `Ctrl+K`." | a count that cannot load shows a dash | counts from the store | n/a |
| Link row | the row paints from the store with no spinner | n/a | `none`: plain text, never an error chip | local, fine | n/a |
| Files and thumbnails (Release 3) | grey box 96 px high | n/a | "Could not load file" chip with the name and Retry; for SND-MSG-010 (too large) no Retry, only "Open in Slack" | cached thumbnails stay | n/a |
| Emoji picker | recent and cached emoji paint first | "No match" | "Emoji list unavailable: showing built-in names" | built-in names and the cache | a failed react removes the chip's own border and shows one line, "couldn't react: reason", with Retry |
| Snooze picker | opens beside the cursor row | n/a | SND-MSG-013 and 014 show in the picker | local, fine | "back Mon 9:00" on the row |
| Hand to agent (Home's picker, Release 1) | the dialog paints at once | n/a | "No agent provider is set up." with a Settings button | local, fine | the result line above |
| Make-issue sheet (Release 1) and attach picker (Release 3) | team and project lists paint from the cache | picker: "No issue matches" | SND-MSG-017 and 018 show in the sheet | disabled with the reason | Create shows the full body first; the result line above; the row then shows the issue key |
| Read mark (opt-in sync) | n/a | n/a | silent in the list | the header says "Read marks not synced: offline"; nothing is queued | n/a |
| Toast and tray (Home's) | n/a | n/a | n/a | n/a | sender and place only: "Sam, DM" (the preview text arrives with Release 2, §9.9) |
| First run | §6.8 | §6.8 | §6.8 | §6.8 | §6.8 |

### 6.7 Accessibility

- Lists are `role=listbox` with one tab stop. The cursor row is the single active descendant. `Space` peek never steals focus; `Enter` moves focus to the conversation. The rail is one more tab stop (up and down move, `Enter` opens); its items read "Needs reply, 3, selected" and "Starred, 4 channels".
- Message lines are `role=listitem` inside a `role=log`. A polite live region announces a new message in the OPEN conversation only ("Ada: review 3120 when you can"). Other conversations announce through the rail count, not a message.
- State is never colour alone: class glyphs differ in shape (square, diamond, circle), "failed" is text, "new" is the word.
- Every action has a palette entry and a button. Reactions have a button, not hover only. A reaction chip's name reads "thumbs up, 3: Ada, Sam".
- Focus after Done: the next row. After send: the composer stays focused, and `Esc` leaves it for the conversation, where `h` works again. After a failed write: focus stays on the failed line; Retry is the next tab stop. A failed send also fires one assertive announcement: "Send failed: reason. Retry is next."
- "Load older" keeps the focused line where it is and announces "12 older messages loaded". Entering or leaving message mode moves no focus.
- Contrast: 4.5:1 for body and muted text, 3:1 for chips and glyphs, checked on the dark frames.
- The emoji picker, the format toolbar and the `[[` picker are listboxes with names; a screen reader reads each emoji by name.
- Reduced motion turns off the new-message chip animation and row removal slides.
- A 5 s toast or confirmation line pauses while hovered or focused, its text is focusable, and Done and Undo announce in the polite region ("Done. Undo with Ctrl+Z, 5 seconds"). The post-send follow-up is also in the `.` panel, so a screen-reader or switch user can reach it.
- Offline, the composer announces "Offline: your text is kept as a draft" and keeps focus. The Pin and Del sheets have screen-reader names ("Pin to #harbor, dialog", "Delete message, dialog").
- The focus ring is 3:1 against its background, as chips and glyphs are.
- The screen-reader name of a Needs reply row: "Direct question from Sam in a direct message, 8 minutes ago: can you ship the fare table today". A row reads its text because the row shows it on screen. Sender and place only is the toast's rule (§9.9), since a toast shows over other apps.

### 6.8 First run

The page cannot exist without a token, so an empty page is the connect card. It has three steps; each has a Done check and its own failure line. Sindri cannot know the app's id before the owner creates the app, so every button that needs the app's page opens Slack's app list (`https://api.slack.com/apps`) and the owner picks the app. That button reads "Open my Slack apps".

1. **Create the app.** The primary button "Open Slack's create-app page" opens the prefilled manifest link **(guess; P0 line 8)**. The manifest text sits beside it with a "Copy manifest" button. Failure: the link does not work. The card then shows the manifest and the steps by hand; its primary button is "Open my Slack apps" and "Copy manifest" is second.
2. **Install it.** The line: "In your app's page, choose Install to Workspace." Buttons: "Open my Slack apps" (primary) and "I installed it". Failure: the workspace blocks the app (SND-MSG-019). The card shows copyable request text for the admin, says no Messages page can exist until the admin approves, and its button is "Copy request text".
3. **Paste the token.** The line: "In your app's page, open OAuth & Permissions and copy the User OAuth Token. It starts with xoxp-." Buttons: "Open my Slack apps" and "Connect". The field is write-only and never echoed; Sindri clears the clipboard after the paste, if it can. The card detects the token kind (SND-MSG-030) and checks the scopes (SND-MSG-003).

**Step 3 failures.** Each has one plain line and one button, so the owner never lands on a card with no way forward.

| Cause | Code | The line | The button |
|---|---|---|---|
| A bot, app or cookie token | 030 | "That is a bot token (xoxb-). Messages needs the User OAuth Token (xoxp-), under User Token." The line names the prefix that was pasted | Paste another token |
| A mistyped or revoked token | 002 | "Slack does not accept this token. It may be mistyped or revoked. Copy it again from OAuth & Permissions." | Paste another token |
| A token from another workspace (seen on reconnect; the first connect fixes the workspace) | 024 | "This token is for another workspace. This connection is for ferry-labs. Paste the right one." | Paste another token |
| A missing scope | 003 | "The token is missing the scope im:history. Add it in your Slack app, reinstall the app, then check again." | Open my Slack apps (primary), Check again (second) |
| Slack answers 5xx | none | "Slack is not answering. Your token is fine. Sindri tries again in 30 s." | Try now |
| Slack answers 429 during the first sync | none | "Slack asked Sindri to wait. Reading continues in 41 s. Nothing is lost." | Pick a conversation to load |
| No network | 028 | "Offline: connect when you're back." The field keeps its text | Try again |
| The admin blocks the app | 019 | the request text above | Copy request text |

A 5xx and a 429 carry no SND-MSG code: the header shows the rate-limit words of §5.3.

After connect, the first sync shows progress: "Reading the last 30 days: 12 threads found" with a progress bar, then "Found 14 threads in the last 30 days." (the cold-start fill, §5.2). It states the count even when it is 0: "Found 0 threads in the last 30 days. New messages appear here." Under it sits the line "40 conversations". An empty store on a poll path then shows "Listening. New messages appear here." with one primary button, "Pick a conversation to load", which opens the `Ctrl+K` jump list; the picked conversation loads at 1 request per minute where Slack limits history. The `messages` step in `sindri setup` is `Skipped` until the owner presses Connect Slack (or the profile names `messages`), then `NeedsYou`, then done when the `messages:token` doctor row is ok. An owner who never connects Slack keeps the doctor `setup` row clean.

## 7. Triage: what waits on me, and in what order

### 7.1 Which threads are mine

The store marks a conversation row **mine** when one of these holds. `me` is the user id `auth.test` returned.

1. It is a direct message or group DM (always mine).
2. I wrote at least one message in the thread.
3. A message in it names me (`<@me>`), not `@here`, `@channel` or `@everyone`.
4. I follow it (the `l` flag on a thread, or "follow" in the `.` panel; Release 2, so in Release 1 a thread is mine by items 1 to 3).

**The one definition of Needs reply** *(agent default (lead, 2026-11-12))*. A thread is in Needs reply if and only if all of these hold. Every other place in this spec (§6.2, §7.2, §9.4, §9.5, W7) points here.

1. It is mine (the list above).
2. Its newest message is not mine.
3. I have written no reply after that message.
4. It is not Done, not snoozed, and not linked to an issue that has an owner. A newer class 3 message returns a linked thread (§9.5).

A read mark affects Unread only. Reading a thread in Slack on a phone, or here, never drops an unanswered thread. That is Zulip's "Participated", cut by who has the last word (ledger 2, take 2).

### 7.2 Class, order and a worked example

Class comes from fixed rules, checked in this order. A model never sets it (decision 15).

| Class | Glyph | Rule |
|---|---|---|
| 3, direct question | filled square | the newest message is from another person and is in a DM or group DM of 3 or fewer people, or names me, and it holds `?` or a request phrase (`can you`, `could you`, `please`, `need you`, `when can`; the list is the profile key `messages.requestPhrases`) |
| 2, review ask | diamond | not class 3, and the thread links a PR (or issue) that the row store says waits on me (a review requested of me, an issue assigned to me) |
| 1, FYI | circle | any other mine thread with a new message from someone else; a keyword hit; a mention with no question. Not a Needs reply row in Release 1 (call 21) |

Sort key, in this order: class high first, then `blocks` (Home §4.1, counted over the thread's `about` links, §9.5), then the time of the newest message, oldest first, then the conversation id. Order holds still between settle points, with the New band, exactly as Home §4.3 says.

Messages and Home rank differently on purpose. Home ranks by what a row unblocks across every source. Messages ranks by "who is waiting on a person's answer". A DM from Sam can sit under a review ask on Home and above it here.

**Worked example.** All names and numbers are made up. It is the list at 09:10, and its rows are drawn in `messages-needs.png`, from Kim's thread down to Sam's DM.

- Kim, in Ada's `#harbor` thread, 08:55: "@joi can you check the group booking path uses 90 s too?" Ada's root (07:30) is "@joi review #3120 when you can", and the thread has 3 replies (Kim 08:20, Ada 08:41, Kim 08:55). The newest message is Kim's, names me and holds a `?`, so the thread is class 3. It links PR #3120, which closes issue #3055 and has PR #3125 stacked on it: blocks 2.
- Sam, DM, 09:02: "can you ship the fare table today?" Class 3 (a `?` in a DM). Blocks 0.
- Ada, `#harbor`, 01:10: "also #3125 is stacked on 3120, can you look at both". It names no one and is not a DM, so it is not class 3. It links PR #3125, which waits on me: class 2, blocks 0.
- Pat, DM, 06:00: "thanks!" The newest message is Pat's, no `?`. Class 1, blocks 0.
- Lee, `#general`, 08:55: "@here lunch Friday?" Not mine (`@here`). It shows in Unread only.

| Row | Class | Blocks | Newest |
|---|---|---|---|
| Kim, Ada's `#harbor` thread | 3 | 2 | 08:55 |
| Sam, DM | 3 | 0 | 09:02 |
| Ada, `#harbor` | 2 | 0 | 01:10 |
| Pat, DM | 1 | 0 | 06:00 |

In Release 1, Kim's thread, Sam and Ada are in Needs reply; Pat (class 1) shows in Unread (call 21). The golden test ranks all four rows, and `Release_1_needs_reply_holds_class_3_and_2_only` checks the list. Full order: Kim before Sam (same class, Kim's thread blocks 2), then Ada, then Pat. At 09:12 Lee's DM "got a sec?" arrives. It lands in the New band, not in the list. The owner replies to Sam at 09:14: Sam's row leaves in place and no other row moves. Until the next settle point the row stays, dimmed, with "replied 09:14" in place of its reason chip. At 09:16 the owner presses `R` (Home's re-rank key, listed in §6.5) and the New band takes its ranked place: class 3, blocks 0, 09:12, so second, under Kim's thread. This is the golden test `MessageRank_orders_the_example`.

**Done and return.** Done (`e`) leaves Needs reply and writes nothing to Slack. A done thread returns on its own when a new message is addressed to me: a mention, a DM, a reply in a thread I wrote in. The row says "back: new reply" (Home §5.4). A reaction to my message does not return it.

### 7.3 The thread row

A Needs reply row is one thread (or one DM). Its title is the first 80 characters of the newest message not mine in Release 1 and the gist (§9.1) from Release 3, and the owner can rename it; the rename is local (`thread_state.title_override`, Release 2). The facts are: who wrote last, the place, the age, and the linked object chips (a PR number, an issue key). The row id is `slack:thread:<workspace>/<conversation>/<thread ts>` and a DM uses `slack:dm:<workspace>/<conversation>` (Home §3.1 `<source>:<kind>:<source id>`).

## 8. Data, writes, routes and verbs

### 8.1 The store

`messages.db` has its own migrations under `Infrastructure/Messages/Migrations/`. It is a cache plus owner state and a work queue, not a view of the ledger, so `sindri views rebuild` does not touch it. *Why a separate file:* the ledger is append-only and message text must change on a Slack edit or delete (decision 1).

**Where rows live.** Home's merged spec says Home rows are ledger events (`home.row.raised`, `.changed`, `.cleared`, `.seen`, `.snoozed`, `.done` and the rest) that fill the `home_rows` table, and edges fill `home_links`, both in `views.db`; `sindri views rebuild` replays them. Messages follows that and uses no other row store. Its collector raises and clears rows through those events (§9.2), with ids and sender-and-place titles. Message text lives only in `messages.db`. `rows.db` appears nowhere on main; the api plan on #2979 used it as a working name for #2978's row model, and Home's `home_rows` is what that became (lead call 8, §2). This spec keeps its own routes and outbox, because text must stay out of ledger-fed stores.

| Table | Holds | Why a new table and not an existing one |
|---|---|---|
| `conversations` | id, kind (`channel`, `private`, `dm`, `mpim`), name, ext-shared flag, starred, local mute, `last_read_ts` (Slack's value), `read_ts` (the local pointer), `latest_ts`, `newest_ts`, `gaps` (JSON) | no main table holds channels or DMs. The poll position per (connection, kind) stays in the ledger's `poll_cursor` (migration 0013); only the per-conversation newest ts and gap list live here, as two columns |
| `users` | id, names, time zone, title, status, presence, bot flag, deleted flag | no main table holds Slack people. The workspace id and my user id come from `auth.test` and sit on the `slack:messages` connection row, so there is no `workspaces` table |
| `messages` | workspace, conversation, `ts`, `thread_ts`, user, text, blocks JSON, `reactions` JSON, subtype, `edited_ts`, reply count, `latest_reply_ts`, a `partial` flag (known only from a search hit), file ids | the text must stay out of the ledger. Slack returns reactions as an array on the message and the only query is `has:reaction`, so a JSON column replaces a `reactions` table |
| `files` (Release 3) | file metadata and a thumbnail path | message rows hold file ids, and messages that share a file share its metadata |
| `thread_state` | owner columns: follow, Later flag, title override. Derived columns: my last ts (`my_last_ts`), gist and its cache key | a channel thread whose root was never fetched (a `partial` search hit) has no `messages` row to hold a flag; a DM uses the conversation as its key. `my_last_ts` is `max(ts)` of my rows and could be a query: M1c measures it and drops the column if the query is fast enough. Done, snooze and mute are Home's per-row state (Home's ledger events, keyed by row id; decision 13), so they are not here; Later and follow are not in Home. The split decides what `purge` deletes (below) |
| `drafts` (Release 2) | id, conversation, thread, text, origin (`owner` or `agent:<run>`), version | unsent text the ledger must not hold |
| `outbox` (Release 2) | `actId`, `origin` (the workflow id or `human`), `created_at`, kind, arguments, state, tries, reason, version (§8.3). `origin` and `created_at` serve the daily cap (§9.7 item 6). A landed row keeps its text only until retention, and retention and `purge` cover it | unsent text the ledger must not hold. The job engine (`view_jobs`) and Home's `home.act.*` events keep ids only, so neither can carry the text of a send that is waiting to retry |
| `messages_fts` | FTS5 over message text (unicode61, diacritics removed) | a virtual table, no equivalent exists |

Thread edges are rows in Home's `home_links`, kind `about` (made or attached) or `mentions` (a link in the text), written through Home's collector. There is no `thread_links` table. They hold ids only.

**Two groups of tables in one file.** Cache tables (`messages`, `conversations`, `users`, `files`, `messages_fts`) can be rebuilt by a re-sync, and so can the derived columns of `thread_state` (`my_last_ts`, gist, its cache key). Owner state (`drafts`, the owner columns of `thread_state`, `outbox` rows not yet sent) cannot. `sindri messages purge` clears the cache tables and the derived columns, so no gist outlives its messages. `purge --all --yes` also takes owner state. The page warns before either, and SND-MSG-025's fix clears the cache tables only.

**Retention and storage defaults** *(agent default (lead, 2026-11-12); the owner may loosen them, question 6 and 8)*: `messages.retain.days` is 30. Slack Connect text is not stored (metadata only, `messages.connect.store: off`). The file cache is off until Release 3. The nightly delete removes older messages, their files and their FTS rows, but **skips a thread, not the whole conversation**: any thread in Needs reply, any thread with a live snooze, and any thread with an owner-state row (a Later or follow flag), a draft or an unsent outbox row. It keeps that thread's messages plus a bounded context (the 20 messages around it). So a 35-day-old unanswered DM stays in Needs reply and a snoozed thread keeps its messages until it returns, but the rest of a busy channel still expires. **Snooze state is read from the ledger events** (`home.row.snoozed` and `.woke`), not from `home_rows` in `views.db`, so `sindri views rebuild` or a stalled projector cannot make a snoozed thread look unsnoozed; the job skips every thread it cannot classify. **Any skip ends after 180 days**: the text goes and the row shows "text expired". A Later flag keeps its thread's text until the owner removes the flag or 180 days pass, and the page shows "kept because: Later" in the purge preview. The 3-day reply window, the 30-day default and the 180-day cap are `agent default (lead, 2026-11-12)`. The nightly job is the service `messages-retention`; the sync is `messages-sync` (the names the aot-smoke services array pins). Snooze wake is Home's. `sindri doctor` warns when `$AW_SINDRI_HOME` sits inside a cloud-sync or backup folder. A Slack delete or edit event changes the local row at once. Retention and `purge` cover every store that keeps text (§11 table).

Types, all new (no `Aw.Sindri.Core.Messages` exists on main):

- `Aw.Sindri.Core.Messages` (pure, no I/O): `MessageTs`, `Mrkdwn` (parse, render), `MessageClass`, `MessageRank`, `GapMarker`, `ThreadRow`, `SnoozeIntent`.
- `Aw.Sindri.Infrastructure.Messages`: `MessagesStore`, `ISlackClient` with `SlackWebClient` (one `HttpClient` that sends the token only to `slack.com` hosts and follows no redirect) (the real adapter), `ISlackLive` with `PollLive` (Release 1) and `SocketModeLive` (Release 3), `MessagesSyncService`, and in Release 2 `MessageOutbox`, the only holder of the write port `ISlackSender`.
- **The fake reuses `FakeSlackServer`** (`tests/Aw.Connectors.Testing/Slack/FakeSlackServer.cs`, an `HttpMessageHandler` on main that serves `conversations.history` and `.replies` with 429 and `ok:false` scripting). M1a extends it with the calls Messages reads: `auth.test`, `users.conversations`, `conversations.info`, `users.info`, `search.messages`, DMs and group DMs, and in Release 2 `chat.postMessage` with `metadata` echo. `SlackWebClient` takes an `HttpMessageHandler`, so unit and integration tests pass the fake and no second class of that name exists. A loopback WebSocket host, `FakeSocketModeHost`, comes with Socket Mode in Release 3.
- **Running the real app on fake data** (W2, W14, W16 and the aw-ui-driver proofs) uses the same `FakeSlackServer` code, so there is one fake. `sindri serve` is another process and cannot load a handler from a test assembly, so M1a adds a small loopback host, `Aw.Connectors.Testing.Server` (a Kestrel wrapper around the handler, like Forge's `aw-forge-fake`), under `tests/`, so `deps.yaml` needs no entry (M1a runs `node scripts/check-deps.mjs` and records the result) and the lock and `AgenticWorkflow.slnx` do. The name avoids `.Host`, which the layer rules read as a production layer (guess; not run). The existing test-support rule already bans production references to a `*.Testing` project; M1a adds only a test that nothing under `src/` references it. `scripts/slack-fixture.sh up|down` builds the host into `<home>/bin` like forge's fixture (the lead builds; builders do not), finds it, starts it on a loopback port with seeded DMs, threads, a gap and a 429 storm, makes a guarded temp `AW_SINDRI_HOME`, seeds the `slack:messages` connection straight into that home with a fake token that starts with `xoxp-fixture-` (no flag, no shell variable), and prints the answer key. **The marker home forces the owner-only file secret store** (no keychain call), because the keychain entry is per connection name, not per home, and a fixture run must never overwrite or delete the owner's real `slack:messages` token. `down` deletes the marker and the file secret. Test: `Fixture_seed_never_touches_the_keychain`. The script's own variables (a home directory, `SLACK_FIXTURE_HOME`, and a command override for the host, `AW_SLACK_FAKE`) go in `.env.example` in M1a, since `check-env-example.sh` counts bash. A bash suite, `scripts/tests/slack-fixture.test.sh`, runs the script over the real host (dotnet build once); it is listed in `docs/testing/bash-suites.md` and in the dotnet-builds list of `scripts/ci/bash-tests.sh`.
- **The base-URL override (production code, loopback only).** `SlackWebClient` reads the env var `AW_SLACK_BASE_URL` (code, tests and its `.env.example` line all land in M1d, call 22). It sets the API base only: files and Socket Mode URLs keep the real host rules, and `SlackEventPoller` (which hard-codes `slack.com`) ignores it, so the fixture covers Messages only. The four conditions and the marker text are `agent default (lead, 2026-11-12)`. It accepts the value only when all of these hold, else it ignores the variable and uses `https://slack.com`: (1) the host is `127.0.0.1`, `::1` or `localhost`; (2) the Sindri home holds the **marker file** `.slack-fixture-home`, which only `slack-fixture.sh up` writes, with the first line `slack-fixture v1` and the second line the loopback port the host listens on; (3) the URL's port equals the marker's port; (4) the connection's token starts with `xoxp-fixture-`. A real-looking token is refused on the override. So a stray variable cannot send a real `xoxp-` token off `slack.com`. Tests: `Base_url_override_is_ignored_without_the_marker`, `Base_url_override_refuses_a_non_loopback_host` and `Base_url_override_refuses_a_real_token` and `Fixture_token_is_never_sent_to_a_non_loopback_files_host`, with the same cases as the copied host rule. Contract tests run on the fake, and on real Slack only by the owner's hand (like the forge's GitHub check, with its env vars in `.env.example`).
- **Dependencies.** `deps.yaml` already has Sindri -> Connectors (Host only), and `Host/IndexSources/SlackConnect.cs` uses `Aw.Connectors.Core.Slack`. Messages adds no production edge: its code stays in `Core`, `Infrastructure` and `Features`. The Sindri test projects already reference `Aw.Connectors.Testing` (`tests/Aw.Sindri.Tests/Aw.Sindri.Tests.csproj`), so the fake is no new edge; the loopback host is one new project under `tests/`. Messages tests go into the existing `Aw.Sindri.Tests` and `Aw.Sindri.Testing`; no other test project is added. `SlackWebClient` does not reuse the internal `SlackHostPolicy`; it copies its rule (slack.com only, https, no redirects) with the same test cases. Services live in `Features/Messages/*`.

### 8.2 Routes

All under `/sindri/messages`, named by M1f (§14). A session read returns ids, kinds and counts only, never text, names or topics (W17). Every POST and DELETE needs the per-start human token (SND-AUTH-002 for a session) and rejects a browser `Origin`. The write routes arrive with Release 2.

| Route | Does |
|---|---|
| `GET /status` | sync path, state, last read, gap counts, scopes granted, the rate-limit words |
| `GET /lists/{needs-reply\|unread\|later\|drafts}?after=&limit=` | the rows of a list, current order, with counts |
| `GET /conversations?starred=&q=` | the conversation list and the jump list |
| `GET /conversations/{id}/messages?before=&after=&limit=` | a window of lines with gap markers |
| `GET /threads/{conversation}/{ts}` | the root, replies, linked rows, `thread_state` |
| `GET /search?q=&scope=local\|slack\|everything` | §9.8 |
| `POST /send` | `{conversation, threadTs?, text, actId, draftId?}`. A POST whose `actId` already has an outbox row is the retry: it takes no `text` (the row holds it) and runs the lookup first (§8.3 step 5). Offline it is refused (SND-MSG-028) |
| `POST /lists/unread/read`, `POST /lists/unread/unread` | Mark all read and its Undo (Release 1, local): `{conversations: [{id, ts}]}` moves the local pointers in one transaction. Sends nothing to Slack and writes no Home event |
| `POST /messages/{conversation}/{ts}/edit`, `/delete`, `/react`, `/unreact` | own-message writes and reactions |
| `POST /conversations/{id}/read`, `/unread` | `{ts}`: moves the local pointer (Release 1). With `messages.syncReadToSlack` on, or from the `.` panel's "Mark read in Slack", it also sends `conversations.mark` while online (Release 2); offline it sends nothing and queues nothing |
| `PUT /drafts/{id}`, `DELETE /drafts/{id}` | local drafts, with `version` |
| `POST /threads/{conversation}/{ts}/make-issue`, `/attach` (Release 3), `/later`, `/follow` (Release 2) | §9.5 |
| `POST /schedule`, `POST /status`, `POST /dnd` | M4 |
| `POST /brief` | starts the `MessagesBrief` job (Release 3; refuses a source that is not on `messages.model.allow`, SND-MSG-031) |
| `POST /connect`, `POST /disconnect`, `POST /purge` | owner only, loopback only; `connect` takes the token in the request body from the no-echo prompt, never from a flag, and the body is never logged |
| `POST /sindri/links/resolve` | `{urls[]}` (at most 20) to a row id and its fields, or `none` per url (#3021; the route name is api's to confirm) |

Snooze uses Home's `POST /sindri/home/rows/{id}/snooze`, with one added body field, `unlessReplied` (Home §8 lists `{until}` or `{preset}` only; the Home spec needs that line).

### 8.3 Write-through and the outbox

Every Slack write goes through `MessageOutbox`, on Home's pipeline (Home §5.2; the 50 ms, 2 s and 15 s are `agent default (Home spec)`):

1. **Paint in under 50 ms.** The line appears greyed in the conversation with a local key `~<actId>`. For a reaction, the chip count moves. The text is already in `outbox` on disk.
2. **Send.** The service takes the outbox row in FIFO order per conversation, so a reply never passes the message before it. It calls Slack and writes `messages.act.sent`.
3. **Echo.** Slack's event for the same message can arrive before the HTTP answer. The line matches by `metadata.actId`, then by `ts`, and never shows twice. Test: `Echo_before_response_does_not_duplicate_the_line`. A TLA+ model, `specs/tla/MessageOutbox/`, checks steps 2 to 8 before M2a (§14). Its bounds drop the offline-expiry clock: offline is a refusal, not a state. It does add "a 429 expiry never sends later".
4. **Within 2 s** the line turns normal and takes Slack's `ts`. After 2 s it says "still sending". It never rolls back by itself.
5. **Failure** (Slack refuses, or 15 s pass): the line stays where it is, greyed, with "failed: <reason>", **Retry** and **Discard**. Retry reuses the `actId`. Before it re-sends, the service looks for a message carrying that `actId` in the thread's history. This costs one history call, and on a 1-per-minute workspace it waits in the bucket **(guess; P0 line 6 proves the metadata round trip)**. A row never re-sends without a lookup that proves the message is absent. If the lookup cannot run, or the metadata is not kept, the row stays `unknown` and says: "Slack may have delivered this message. Check #harbor, then: It's there (drop it) / Send again" (SND-MSG-032). Test: `Restart_with_unknown_row_never_resends_unproven`.
6. **Rate limit.** A 429 keeps the row `pending` with "waiting, Slack asked us to wait 12 s" and a countdown. The hold is capped at 2 minutes. After that the line fails with SND-MSG-005 and Retry, and it never fires on its own later. A failed row does not block later rows of the same conversation, and Retry does not reorder them.
7. **Offline.** No Slack write is ever queued. A send, reply, react, edit, delete, create-channel or read mark is refused at once with SND-MSG-028, and the composer keeps the text as a draft ("Offline: your text is kept as a draft"). No outbox row exists for a write that never started. A send that started online and then lost the network fails by step 5 (15 s) and keeps Retry; Retry is refused while offline. Read marks sync only while online (§6.5).
8. **Restart.** A row left `sending` is `unknown` on start. It is looked up by `actId` before any re-send (step 5's rule).
9. **Undo** exists only for local state (Done, snooze, mute, a read mark). A sent message has no Undo; the delete action is separate and asks to confirm.

A second action on a line that is still sending is refused: "wait for the first to land" (SND-MSG-006). A failed `send` prints `act <id>`. `sindri messages send --retry <actId>` (MCP `messages_send {retryOf}`) runs the lookup first. A `send` with the same text and conversation inside 10 minutes of an `unknown` row asks for `--yes` (`agent default (lead, 2026-11-12)`).

### 8.4 CLI and MCP

`sindri messages <verb>` on the CLI, `messages_<verb>` over MCP at `/sindri/mcp`. Every route has a verb, or an owner-only row in a test (`Every_route_has_a_verb_or_an_owner_only_row`, like Reviews). Message text goes in with `--text-file <path|->`, never `--text`, so it stays out of argv. `--json` is the route's body plus `retryable`.

| Verb | Does | A session may |
|---|---|---|
| `connect [--manifest]`, `connect --app-token` (Release 3) | prints the app manifest, or stores the token typed at a no-echo prompt; `--app-token` stores the `xapp-` token the same way and refuses every other prefix | no |
| `disconnect` | removes the connection and its secret, then offers `purge` (default yes for DM text) so `messages.db` does not outlive the token | no |
| `status [--json]` | the status route | yes |
| `doctor` | the `messages:*` doctor rows | yes |
| `list <list> [--max N] [--json]` | rows of a list | ids and counts only |
| `conversations`, `thread <conversation> <ts>` | reads | ids and counts only |
| `show <conversation> [--thread TS] [--before TS]` | lines | no (§11), except what the owner handed to this session with "Hand to agent": a grant keyed by (conversation, thread ts) that ends with the session. A DM has no thread root, so its grant covers the Needs reply row's own messages, the run after my last message (at most 20). Under a grant a `show` with no `--thread`, a second thread, or a `--before` past the grant is refused. Tests: `Show_grant_covers_only_the_handed_thread`, `Show_without_thread_is_refused_under_a_grant` |
| `search <query> [--scope S]` | search | no |
| `send --text-file F`, `send --retry <actId>`, `edit`, `delete`, `react`, `read`, `unread` | writes (Release 2 and 3) | no |
| `draft <conversation> --text-file F [--thread TS]` | writes a draft, sends nothing | yes: the one write a session has (Release 2) |
| `later`, `follow` | local flags | no |
| `snooze <row> --until T [--unless-replied]`, `done <row>` | Home's verbs | no |
| `make-issue`, `attach` | §9.5 (Release 3) | no |
| `schedule`, `dnd`, `status set`, `links resolve` | M4 verbs | no |
| `brief [--refresh]` | the catch-up brief (Release 3) | owner only |
| `purge [--conversation C] [--all --yes]` | deletes local data (cache only unless `--all`) | no |

Exit codes follow the context family: 0 ok, 1 attention (a refusal in §10 the owner can fix or retry, or Sindri not running), 2 tool or usage error. §10 lists the codes. A failed `send` JSON carries `retryable: true` for 005, 007, 021.

### 8.5 Profile keys

`messages.sync` (`auto`, `poll`; Release 3 adds `socket`; default `auto`), `messages.backfill.days`, `messages.keywords`, `messages.requestPhrases`, `messages.sendKey` (`ctrl-enter`, `enter`; default `ctrl-enter`), `messages.syncReadToSlack` (default off), `messages.retain.days` (default 30), `messages.connect.store` (default off), `messages.gist` (`plain`, `model`; default `plain`), `messages.model.allow` (starred channel ids; default empty), `messages.notify.rules`, `messages.notify.preview` (default off; arrives with Release 2, §9.9), `messages.confirmAboveMembers` (default 25), `messages.away.hours` (default 4), `messages.send.dailyCap` (default 20, per workflow), `messages.send.dailyCapWorkspace` (default 40), `messages.files.cacheMb` (default 500, used from Release 3). There is no `messages.workspace` (read from `auth.test`) and no `messages.starred` (the store owns it; starring is a store write with Undo). An unknown or out-of-range value is SND-MSG-029 with the key path.

The profile work belongs to M1d. `SindriProfile` is a typed record, so a `messages:` block needs the record, the canonical-hash rule (an absent block keeps the hash), validate rules and a Settings card. `sindri profile set` refuses list and map targets, so the list keys (`keywords`, `requestPhrases`, `notify.rules`, `model.allow`) are edited in the profile file and checked with `sindri profile validate`. They take rows in `docs/csharp/sindri-profile.md`.

## 9. The better-than-Slack set

### 9.1 Gist

Not in Release 1. The row title is `plain`: the first 80 characters of the newest message not mine, in the same line, so nothing shifts. The model gist is off by default and never runs on a DM, a group DM or a Slack Connect conversation. The owner may turn it on per starred channel (`messages.gist: model` plus the channel id in `messages.model.allow`). For an allowed channel, a model writes one line, at most 80 characters, for each thread row in the first 30 of Needs reply. The input is the last 10 messages of the thread, fenced, and goes to the agent provider the owner picked for `context.models`; the status line names that provider and shows "model: on" (doctor row `messages:model`). The job record carries conversation id and `ts`, never text. The cache key is `(conversation, thread ts, newest ts, edited ts)`, so an edit or a new reply makes a new gist, and a gist cache is deleted by `purge`. The job reports its token spend like other jobs. The class never depends on the gist. Test: `Gist_default_sends_nothing_to_a_model`.

### 9.2 Home rows

A collector in the Sindri host (Home §3.3 pattern) raises one Home row per Needs reply thread of class 3 or 2 (call 21): kind `dm` or `mention` (both defined in Home §3.2), id as in §7.3, `blocks` from `thread-about` edges in `home_links`. The title is sender plus place only ("Sam, DM", "Ada, #harbor"), never the gist or any text, because `home.row.raised` and `.changed` carry the title into the ledger. The Home peek reads the text from `/sindri/messages` at paint time. A row's why chip holds a reason word ("mention", "DM", "keyword") and never the matched text. Home's `dm` and `mention` rows have a Reply composer in the peek (Home spec §7.4, the `r` row of the key table); Release 1 has no send route, so Home must hide Reply for these kinds and show "Open in Slack" until M2c. That line is an open edit to the Home spec; it is not in the Home spec on main yet. When M2c turns the composer on, Home's peek reply box is the Messages composer, with the Messages rule (`Ctrl+Enter` sends); that is one more line in Home's section 7.4 table. Test: no `home.*` or `messages.*` event payload holds a byte of a fixture message body (W18). The collector is Release 1 work that starts when Home's S0 routes merge; it does not block the dogfood test. It clears the row when the thread leaves Needs reply. Class 1 gets no Home row: Pat's "thanks!" is a DM with no question, so it is not `dm` by Home's own definition and raises no toast and no tray count (test `Class_1_thread_raises_no_home_row`). The Home follow-up: Home adds the kinds `thread-reply` and `keyword`, with reason words "reply" and "keyword", and a non-notifying flag; only then do class 1 threads get rows. The M1i collector blocks X2 (call 25). Done, snooze and mute are Home's records, so the two pages never disagree.

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
- **Follow up if no reply.** For a thread where I spoke last. The record stores `until` and `myLastTs`. If anyone else posts in the thread after `myLastTs` before `until`, the record is dropped without a toast (Messages writes `home.row.woke` with reason `replied`, the event Home's collectors use when addressed activity arrives). The reply puts the thread in Needs reply by the normal rule, so nothing is lost. If nobody posts, the thread returns to Needs reply at `until` with "no reply since Tue" in the age column.
- **Check before it returns.** If the sync state is "Behind" at the due time, the service reads that thread's replies once, so a thread someone answered does not come back.
- **Offered after you send.** A sent message shows a confirmation line for 5 s: "Sent." with a **Follow up if no reply** button (it pauses while hovered or focused; the same action is in the `.` panel). The saved filter "Waiting on them" (`/`, or palette `messages.waiting`) lists these threads.
- **Local.** Slack's Later and reminders have no API. The snooze never writes to Slack. Undo is a reverse op within 5 s. Longest snooze is 365 days.

The store and the timer are Home's: the ledger events `home.row.snoozed`, `.woke`, `.done` and `.revived`, projected into `home_rows` (Home's merged spec, "Events and migration"; #3030). Messages writes those events for its rows, sends the `unlessReplied` field and reads the replied state from its own store. There is no Messages-local record and no replay (lead call 17). "Follow up if no reply" is Release 2.

### 9.5 Make an issue, attach a thread

`m` (make issue) is Release 1: it writes to Linear and to Home's `home_links`, never to Slack. `a` (attach) and Save as a note are Release 3. The reply-in-thread box is a Slack write, so it is Release 2; the Release 1 sheet does not show it.

- `m` opens a sheet: title (the gist or the first 80 characters, editable), team, project (the project whose PR the thread links, else the last used), assignee (me), body, label. **The body is the Slack permalink plus text the owner types.** Sindri never copies Slack text into the body. The sheet shows the full body before Create. The owner may paste a quote; that is the owner's edit. Make issue is the owner's act only: no agent and no workflow runs it. Create calls the existing Linear create-issue write (the New issue sheet's route), then writes an `about` edge to Home's `home_links` and the event `messages.thread.linked`.
- `a` opens a picker over issues and PRs (the palette's search) and records the same edge without creating anything.
- **Both ways.** The thread row shows the issue key. The issue's peek and the PR's peek show the thread (Projects and Reviews read `home_links`). A thread with an `about` edge counts in `blocks`.
- **Leaves Needs reply.** A thread with an `about` edge whose issue has an owner leaves Needs reply (exclusion 4, §7.1), unless a newer message is class 3 (Missive's assign-moves-it-out rule, ledger 10).
- **A link in the text is only `mentions`.** A `mentions` edge shows "Mentioned in 2 threads" on the PR and never ranks, so a stranger's link cannot lift a row.
- **No Slack write by default.** "Reply in the thread with the issue link" is a checkbox, off (Release 2; Release 1 writes nothing to Slack, so the sheet hides it). Question 4.
- **Save a decision as a note** (`messages.save-note`, with the quote, the author, the time and the permalink) waits on the Notes page (#2981) and shows disabled with that reason until it ships.

### 9.6 Catch-up brief

Release 3. Off by default (decision 15): facts lines only until the owner turns the model on, and written lines only for starred channels in `messages.model.allow`. A DM, a group DM or a Slack Connect conversation gets facts lines only. After `messages.away.hours` (default 4) without the app in focus, a banner offers "Catch up: 9 h away". `B` and the palette id `messages.catch-up` run it any time, and Home's "Since you last looked" can include it.

- **Channels.** Starred channels, DMs, and channels where I was mentioned since I left; at most 12. The status line names the provider when the model is on.
- **Per channel.** A facts line from the store with no model ("14 new, 2 mention you, 1 thread with 9 replies"), then at most 3 written lines. Each written line carries chips for its source messages; a chip peeks the message.
- **Link check.** The job drops any written line whose cited message is not in the store. A brief of 40 lines at most.
- **Honesty.** If the store has gaps for a channel, the line says "12 messages missing" and does not summarize what it never read. No history call is made for a brief (the 1-per-minute limit, ledger 9).
- **Failure.** With the model off or limited, the facts lines remain and the page says why.

The job is `MessagesBrief`, one of the Home-brief family: facts first, a model for the sentence only.

### 9.7 Agents on your side

Items 1 to 7 (workflow triggers, nodes and test runs) are Release 3, from the engine plan on #2979 (accepted by meta). Item 8 splits. The hand-off (the owner presses `t` or Home's "Hand to agent"; the session reads the thread through the per-thread `show` grant) is Release 1: it writes nothing to Slack and needs no composer. The agent draft (§6.4's "Ask an agent to draft", `messages_draft`, the Drafts list) stays Release 2, because it needs the drafts store and the composer; there the owner presses the button and the owner sends, so no workflow is needed. All of it waits on the Slack sync (M1e). The rules below are `agent default (lead, 2026-11-12)`.

1. **Triggers.** New `connection_event` kinds fed by the sync service from the store: `messages.mention`, `messages.dm`, `messages.thread.reply`, `messages.keyword` (new names: `slack.mention` already exists for `slack:work` with a `text` field, and the same key must not mean "ids only" on `slack:messages`). They replace the one-channel poll for these uses. **This needs an engine change (call 23, M3d0).** On main `PollGroupSource` dispatches a `connection_event` trigger only through `PollCatalog` and an `IEventPoller` whose preset equals the connection's; `slack:messages` has `Preset = null`, so no poller matches and a trigger on it would save and never fire. M3d0 adds a store-fed event source (not an `IEventPoller`) that the sync service calls, keyed per connection, that starts runs through the same occurrence and dedupe path with `deliveryId` = the message `ts`, and a catalog check per connection kind, so `messages.dm` on `slack:work` and `slack.message` on `slack:messages` are refused at save. Tests: `Trigger_on_messages_connection_fires_once_per_ts`, `Messages_kind_on_a_work_connection_is_refused`. **The payload is ids only:** `{conversation, ts, thread_ts, permalink, kind, external}`. `external` is true for a Slack Connect or other-org author. A trigger ignores the owner's own messages and any message a Sindri workflow posted (matched by `actId`), so two workflows cannot loop. An external trigger fires a node only if the node opts in. The catalog check (#2987) covers a typo.
2. **No message text in any run store.** `workflow_run.trigger`, `workflow_node_run.input` and `.output` (ledger.db), and the `GraphWorkflow` history in temporal.db hold ids, never text. On main a node run's input and output are stored as text up to 64 KB (`NodeRunLimits`), and the generic `prompt` node stores its filled-in prompt (`GraphBlockActivities.cs:62-87`), so the generic `prompt` node is never used over Slack text. The rule is built on ids: a Messages trigger and Messages nodes emit ids only, and a Messages node that needs text (`messages_classify`, `draft_message`) fetches it from `messages.db` in process at run time and never writes it to the run, node or Temporal records. Its stored input is ids and its stored output is ids, plus a hash and a length at most. The run-store rule needs no engine change (lead calls 9 and 13, §2); the trigger source does (item 1, call 23). A failed Messages node stores an error of a code plus a fixed title only, as `TestConnectionService` does, because a Slack or model error can repeat message text or the token. Tests: `Run_record_holds_no_message_text`, `Failed_messages_node_stores_code_and_title_only` (W18). So `wf run-show` and MCP `workflow_run_get`, which a session may read, return no text.
3. **A model sees only allowed text.** A `messages_classify` node refuses a source that is a DM, a group DM, a Slack Connect conversation, or a channel not on `messages.model.allow` (SND-MSG-031). So the `slack-question-draft` template works only for allow-listed channels. A `prompt` node cannot expand to Slack text: the trigger field it can reference is an id, and the test in W18 proves the stored node input holds no body.
4. **"A question about X."** One new Messages node, `messages_classify` (text fetched in process, answer typed as a boolean or an enum). The template `slack-question-draft` is a trigger, a `messages_classify` node (`is this a question to me about X?` to a boolean), a `condition`, and a `draft_message` node. It ships off.
5. **`draft_message`.** Writes a draft (origin `agent:<run>`, a separate row), listed in Drafts and as a Needs you row on Home, with approve, edit and drop. The node waits for the owner, like `ask_owner`. Outputs: `sent`, `edited` or `dropped`, and the hash and length of the final text.
6. **`send_message`.** Sends without asking, from **owner-written templates only**: text the owner typed in the workflow, plus typed slots filled from the store (ids, numbers, links, a display name). Model output is not a slot. At activation, a `send_message` whose text has an upstream path through a `prompt` node over Slack text is refused (SND-MSG-016). Activation is also refused unless the revision was saved by a human (`saved by human:*`). Every send is recorded on the run and as `messages.send.workflow`. Limits: the node names its conversations and sends to no other; at most `messages.send.dailyCap` (20) sends a day per workflow and `messages.send.dailyCapWorkspace` (40) per workspace (both `agent default (lead, 2026-11-12)`); text with `@channel`, `@here` or `@everyone` is refused (SND-MSG-016). A typed slot (such as a display name) is colleague-controlled text. Every slot value has `&`, `<` and `>` escaped before it enters the template (Slack's own escaping). The broadcast check then runs on the **rendered** text and refuses the words `@channel`, `@here` and `@everyone`, Slack's special forms `<!channel>`, `<!here>`, `<!everyone>` and `<!subteam^…>`, and any `<@…>` or `<#…>` the template did not name (call 24). The composer's risk chip uses the same detector. Bidi and control characters are stripped as in §11. Tests: `Display_name_with_bang_channel_is_refused`, `Slot_values_are_escaped`. The counter is the count of outbox rows by `origin` workflow id with `created_at` since local midnight in the profile's time zone (a zone change moves the next midnight; today's count is not reset), read and inserted in one `messages.db` transaction (no new table, two new outbox columns, §8.1), so it survives a restart and concurrent runs; the `messages.send.workflow` ledger event is written after it (the ledger is another SQLite file, so it is not part of the transaction). `send_message` ships after `draft_message` has run for two weeks (the phase split M3d1 / M3d2 in §14 carries this; M3d2 needs a date field "`draft_message` in use since").
7. **Test runs** (#2969) record "would have drafted" and "would have sent". No test run posts.
8. **Sessions.** From Release 2 a launched agent session may write a draft (`messages_draft`) and nothing else (SND-AUTH-002 blocks every other Messages write); in Release 1 it writes nothing in Messages. It reads no message text unless the owner hands it a thread with "Hand to agent" (Home §5.1 picker); that act opens the per-thread `show` grant of §8.4 for that thread only. The dialog names the provider and what goes along ("thread of 6 messages, 2 linked rows, about 3k tokens") and says the session is a normal session that can run shell and `gh`. The thread goes as a file the session reads through a fenced `messages_show` result, never pasted into the system prompt. The hand-off is the owner's explicit act, so it is the one way DM or Slack Connect text reaches a model.

An agent draft is untrusted output. Decision 14 and §6.4 give the one rule: it is never sent without the owner's send action, and the submit menu guards `@channel`, `@here`, links and attachments.

### 9.8 Search across everything

`Ctrl+K` then a query. `/` stays the list filter (§6.5). Two scopes. **Release 1 ships the Messages scope, local only:** no "Search Slack too" row, no "from Slack" tag and no Everything scope, and the scope chip stays hidden until a second scope exists. Its empty state reads "Nothing in the store yet. Open a conversation to load it." Its limited state reads "Local results only: Sindri has not read older messages." (§6.6).

| Scope | Source | Speed |
|---|---|---|
| Messages | local FTS5 first (Release 1); `Enter` on "Search Slack too" adds `search.messages` results tagged "from Slack" (Release 3) | local under 100 ms; Slack Tier 2, at most 3 calls a minute **(guess; P0 line 4)** |
| Everything | Messages, issues, PRs, notes and code scope, in groups of 5 | each group as fast as its own store |

Operators are Slack's words so nothing is relearned: `in:`, `from:`, `to:`, `with:`, `has:link`, `has:file`, `has:reaction`, `before:`, `after:`, `on:`, `during:`, `is:saved`, `is:thread`, quotes, `-` to exclude, `*` wildcard from 3 characters. Zulip's add `is:unread` and `near:<ts>`. `is:done` replaces Zulip's `is:resolved` (Slack has no resolved state). Cross-source adds `pr:`, `issue:`, `note:`, `code:`. A bad operator is SND-MSG-012 with its position. Everything shows groups, not one fused score: a fused rank would hide why a note outranks a message.

A Slack hit that the store lacks is cached as a `partial` message so it opens in the peek. Search only returns conversations the owner is in.

### 9.9 Notifications, mute and "why is this here"

- **Home owns the toast and the tray count; Messages raises none of its own** (lead call 18; Home's D10). Messages rows reach the owner through Home's pipeline: in Release 1 only class 3 and class 2 threads become Home `dm` / `mention` rows (call 21), and they notify by Home's rule (every `dm` or `mention` row that enters Needs you, once per row per episode; the tray count is notifying rows not yet seen). Class 1 threads have no Home row until Home adds `thread-reply` and `keyword`, so they never toast or count. So in Release 1 Home's Messages tab count and the Messages top bar count are the same number; class 1 is counted in Unread only. M1i (collector and tray count) blocks X2 (call 25), so the tray count exists in the gate week. A toast and the screen-reader label show sender and place only ("Sam, DM"). `messages.notify.preview: on` is not built in Release 1: Home builds a toast from the row, and the row holds no text, so the preview needs Home's toast function to take an optional body from the owner kind's provider (the Messages store, read in process on the Home host). It arrives with Release 2. Sindri obeys Slack's DND (row 53). A `messages.notify.rules` entry sets a conversation to `all`, `mentions` or `none`, or adds keywords.
- **Mute** (Zulip's rule, ledger 12): it hides the conversation from Unread and silences toasts; a DM or a mention still breaks through; a followed thread overrides a muted channel.
- Every row has a reason chip that names the rule ("mention", "your thread", "keyword: fare", "DM"). The tooltip says which rule put it there.
- Slack's own phone and desktop pushes are untouched. The ledger row says "differs".

## 10. Errors

Reserved now: SND-MSG-001 to 040 (no `SND-MSG-` code exists on main). They go in `Core/SindriErrors.Messages.cs`. The app shows each sentence first and the code in small text. The CLI exits 1 for 001, 002, 003, 004, 005, 006, 007, 008, 019, 021, 023, 024, 028 and 032 (the owner can fix or retry) and 2 for the rest. The reason text in 004, 007 and 019 comes from Slack and is fenced.

| Code | Meaning | HTTP | fix: |
|---|---|---|---|
| 001 | no Slack connection for this workspace | 409 | `sindri messages connect` |
| 002 | the token is revoked or invalid (`invalid_auth`, `token_revoked`) | 401 | make a new token, `sindri messages connect` |
| 003 | the token lacks a scope; carries the scope name; the feature is off | 403 | add the scope in the Slack app, reinstall, reconnect |
| 004 | Slack refused a write for a permission or rule (`cant_update_message`, `not_in_channel`, `restricted_action`); carries Slack's reason | 403 | read the reason; join the channel or ask an admin |
| 005 | Slack rate-limited the call; carries seconds to wait; the write is held up to 2 minutes, then it fails with this code and Retry | 429 | wait; Retry if it failed |
| 006 | a second action on a line still sending | 409 | wait for the first to land |
| 007 | a write failed after 15 s; Retry and Discard; carries `act <id>` | 502 | `sindri messages send --retry <actId>` (it looks the message up first); a plain second `send` posts twice and is refused while the first is unknown |
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
| 020 | Socket Mode is not available for user events; polling is in use. An `Ok` doctor row with detail text, not an error | n/a | none |
| 021 | history is limited; a gap stays; carries the count | 200 | Load older, one per minute |
| 022 | `purge` needs `--yes` | 400 | repeat with `--yes` |
| 023 | the CLI could not reach Sindri; nothing was sent | CLI only | `sindri serve`, repeat |
| 024 | the token belongs to a different workspace than the connection | 409 | connect the right token |
| 025 | `messages.db` is newer than this build, or damaged | 500 | `sindri messages doctor`; `purge` rebuilds the cache |
| 026 | a schedule time is past, or over Slack's limit | 400 | pick another time |
| 027 | a channel name breaks Slack's rules | 400 | use lowercase letters, digits, `-` and `_` |
| 028 | the action writes to Slack and Sindri is offline; nothing is queued, and a send's text stays as a draft | 503 | reconnect, repeat |
| 029 | a `messages.*` profile key is unknown or out of range; carries the key path | n/a | edit the profile file, then `sindri profile validate` |
| 030 | the pasted token is a bot, app-level or cookie token (`xoxb-`, `xapp-`, `xoxc-`), checked by prefix before any call | 400 | paste the User OAuth Token (`xoxp-`) from OAuth & Permissions, User Token section |
| 031 | a model use is refused: the source is a DM, a group DM, a Slack Connect conversation, or a channel not on `messages.model.allow` | 403 | none by default; the owner may allow a starred channel |
| 032 | a send is `unknown` and Sindri cannot prove Slack has no copy | 409 | check the conversation in Slack, then Retry or Discard |

## 11. Security

- **The token.** Stored through `IConnectionSecrets` in one entry under `slack:messages`: the keychain, or an owner-only (0600) file where the box has no keychain or secret-tool. `sindri doctor` says which (row `messages:token`). It is never in `messages.db`, an argument list, a log, an error, a ledger row or a response. `Aw.Redaction` must be extended before Messages stores a token. `BuiltinRules.cs:57` today is `xox[abprs]-[A-Za-z0-9-]{10,}`: it matches neither `xoxc-` nor `xapp-`, and `xoxe.xoxp-` only in part. **M1d** changes that rule to `xox[abcprs]-`, adds `xapp-[A-Za-z0-9-]{10,}` and the `xoxe.` forms, and adds a `BuiltinRules` test per prefix. The manifest sets `token_rotation_enabled: false`. `sindri doctor` warns when `SLACK_TOKEN` (the `slack:work` connection's variable) or `AW_MESSAGES_TOKEN` is set in a session's environment; a Messages token never uses either, but a leftover one is a Slack token in the environment. The Step sandbox empties both. The `POST /connect` body is never logged.
- **Hosts.** `SlackWebClient` sends the token only to `slack.com` and its subdomains and `files.slack.com`, over https, with redirects off. A Socket Mode URL must be `wss://*.slack.com`. Emoji and thumbnails load only from an exact allow list of hosts (a constant in `SlackWebClient`, filled from what P0 line 6 records for emoji and thumbnails), over https, with redirects off and the token never sent to a CDN host, with a size cap of 10 MB each, through Sindri, never from a message's own `<img>` or remote URL.
- **Text is untrusted.** Names, channel names, messages, topics, status text and file names show as plain text or the `Mrkdwn` subset: no HTML, no remote images, links shown as text with the host visible, bidi and control characters stripped. Sindri opens a message's URL only on the owner's click, and the live-row resolver only reads the local store.
- **Prompt injection and model use.** Work chat never goes to a hosted model by default (decision 15). The gist and the brief are off, and they never run on a DM, a group DM or a Slack Connect conversation, whatever the owner turns on. A model may read a starred channel the owner put on `messages.model.allow`. Any text that reaches a model goes through `FencedPrompt` as data. A model's output is plain text and becomes a Slack message only by the owner's send action (decision 14). Slack Connect text and DM text reach a model only through the owner's own "Hand to agent", which names the provider and the size (§9.7 item 8). This sentence and §9.1 say the same thing.
- **Sessions see counts.** All Messages data is repo-less. A session gets ids, kinds and counts for the lists, and no text, topic, file name, channel name or display name unless the owner hands the row over (W17). `GET /sindri/ledger` and `/sindri/events` carry ids and counts only (decision 2).
- **Writes are human-only.** Every POST needs the per-start human token. The one exception is a `send_message` node on a human-saved revision, with owner-written template text, bounded by §9.7. There is one write port, held only by `MessageOutbox` (W12).
- **Logs, spans and `actId`.** No message body, channel name or user name appears in a log line, a span or a metric label; ids only (W18 runs a scan over the fake server's run). `actId` is an opaque random id; it names no run, draft or workflow, because other apps in the channel can read Slack's `metadata`.
- **Issues, PRs and evidence.** An issue about a gap names the parity row or function and never quotes or paraphrases message text, channel names or people. Every PR shot, evidence pack and aw-ui-driver run uses the fake Slack's made-up data. A dogfood run against real Slack is never recorded.

**Where message text lives, and what covers it.**

| Store | Holds message text? | Purge and retention |
|---|---|---|
| `messages.db`: messages, FTS, drafts, outbox (landed rows keep sent text until retention), the gist and brief caches, `thread_state` | yes | the nightly delete (`messages.retain.days`, which skips a thread in Needs reply or snoozed for up to 180 days, §8.1) and `purge` (cache tables and derived columns); `purge --all --yes` takes owner state too |
| the file cache | yes | the same retention; `purge` |
| `ledger.db` events, `workflow_run`, `workflow_node_run`, job records | no: ids (Home events carry sender and place names) | W18 scans them |
| `views.db` (Home rows, `home_links`) | no: titles are sender and place | W18 scans it |
| `temporal.db` | no: ids only | W18 scans it |
| logs, spans, metrics | no | W18 scans them |
| toasts, OS notifications, the tray | no, unless `messages.notify.preview: on` | n/a |
| GitHub issues, PRs, evidence | no | the rule above |
| a model provider | only a channel on `messages.model.allow`, or the owner's hand-off | n/a |

- **Local data.** `messages.db`, its WAL and the file cache are 0600 under `$AW_SINDRI_HOME`. `sindri messages purge` deletes by conversation or everything in the cache. The company's rule on storing chat on the work laptop is the day-0 gate (question 8); until it is answered the defaults are 30 days and no Slack Connect text (§8.1).
- **The app-level token** `xapp-` can open a Socket for the whole app. It arrives with Release 3 through `sindri messages connect --app-token`, sits in `TokenSet.ClientSecret` of the same entry, and is used only by `SocketModeLive`.
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
- Feeding the code index, embeddings or Ollama. `messages.db` is not a `context.sources` kind, and search is FTS5 only (an architecture test).
- Sending work chat to a hosted model by default (decision 15).

## 13. Gaps found (the guesses to prove)

These are in this spec as guesses. The five riskiest come first. Each names the probe line that proves it and what changes if it is wrong.

| # | Guess | If wrong | Probe |
|---|---|---|---|
| 1 | A user token can open Socket Mode and receive the owner's DM and channel events. Slack's page shows `bot_events` only. | Polling only: bet 2 moves to 30 s on Tier 3, and to search polling on a limited workspace. | line 5 |
| 2 | The owner's Slack app keeps Tier 3 history (50 or more per minute). Slack's 2025 change gives new non-Marketplace apps 1 per minute and 15 messages. | No backfill, no hot-set polling, no thread-reply check; live events or search polling only; gap markers everywhere (W10). | line 3 |
| 3 | The workspace lets the owner install a user-scope app, including `im:history` and the `*:write` scopes. | There is no token and no Messages page. | lines 1, 8 |
| 4 | `search.messages` with `to:me` works as a cheap change detector for DMs and mentions (3 calls a minute). | No live path on a limited workspace without Socket Mode. | line 4 |
| 5 | Interactive buttons and slash commands cannot be sent with a user token (rows 27, 29). #2979 says buttons work. | If they work, rows move to M4 and W6 is stricter; if not, they stay `blocked` and need an owner-accepted drop. | line 7 |
| 6 | `chat.postMessage` keeps `metadata.event_payload` for user tokens and returns it in history and events, so a retry never posts twice. | A retry may post twice. The row stays `unknown` and the owner checks Slack first (§8.3 step 5). Release 2 does not ship without this proof or that rule. | line 6 |
| 7 | Channel unread counts can be computed from `last_read` plus the store. Slack documents `unread_count` for DMs only. | Unread counts for channels are wrong until opened. | line 2 |
| 8 | Slack's attachment and thumbnail URLs load with the token header. | Images need a different fetch; M4 grows. | line 6 |

Other findings from reading `origin/main`:

- `PollableEvents.cs` lists only `slack.message` and `slack.mention`, and `SlackEventPoller` reads ONE channel. The four triggers in §9.7 need the store, not a per-channel poller, and a store-fed event source in the engine (call 23).
- `SlackChannelIds` and the context reader refuse DMs on purpose, and the reader uses a bot token. Messages is a second Slack client, not an extension.
- Home's row kinds have no class-1 thread reply or keyword (§9.2); the follow-up to add them is Home's. Home's snooze body has no `unlessReplied` (§8.2).
- The row store that #3021's lookup reads is #2978's model. On main, `rows.db` does not exist; Home's accepted spec uses views.db tables (§8.1). M1g may start on a stub store and read the real one when it lands.
- `scripts/slack-probe.sh` does not exist on main. api's plan names it. The probe needs an app before `connect --manifest` exists, so M0a ships `docs/sindri/slack-probe-manifest.yaml`.
- Main already has three Slack paths: the index reader (bot token), the `slack:work` REST connection with `post_message`, and `SlackEventPoller`. Messages is a fourth client with its own connection; §4 and §5.1 say how the first three stay apart.

## 14. Phases and parallel plan (for /specToProvenPR)

The whole table, its sizes, lanes and release marks are `agent default`. Size is a rough guess: S under 1 day, M 1 to 3 days, L over 3, with the PR count. Each phase names its first red test. "Rel" is the release (§1): 1 is the read-only slice, 2 is writes, 3 is demand-pulled. "ledger" in the last words of a phase means its parity rows turn from `planned` to `done` with an evidence path.

| Phase | What | Lane | Needs | First red test (proves) | Size | Rel |
|---|---|---|---|---|---|---|
| M0a | `scripts/slack-probe.sh` with lines 1 to 8 (§5.5); `--write` needs `--channel`; a bash suite; a fake answer set; the probe manifest `docs/sindri/slack-probe-manifest.yaml` (every scope of §5.1, with the by-hand steps); its own `.env.example` line for `AW_SLACK_PROBE_KEY` (a stated exception to the tools-lane rule, because `check-env-example.sh` counts bash) | api | none | `Probe_prints_counts_and_never_a_name` | S, 1 | 1 |
| M0b | `docs/parity/slack.md` from §3 (rows 1 to 56); `scripts/check-parity.sh` (W6) and its bash suite | ideas | none | `Parity_check_fails_on_a_bare_row` | S, 1 | 1 |
| M0c | The owner creates the probe app from the probe manifest, runs the probe and answers the day-0 gate (questions 1 and 8); ideas writes the results into §3, §5 and §13; the owner applies the kill rule (§1) | owner, ideas | M0a | none: the go/no-go | S | 1 |
| M1s | **Red and stubs first.** For M1b to M1h: request and response records, service stubs that throw `NotSupportedException`, route-table rows, `Aw.Architecture.Tests` entries for `Aw.Sindri.Core.Messages`, `PublicAPI` lines, event-catalog rows, ApiGoldens scenario rows. Every named test below exists and fails red | api | none | `Messages_stubs_compile_and_every_named_test_fails` | M, 1 | 1 |
| M1a | Extend `FakeSlackServer` (§8.1); the loopback host `Aw.Connectors.Testing.Server` (lock and slnx entries; the `deps.yaml` result recorded); `scripts/slack-fixture.sh` (builds and starts the host, writes the marker `.slack-fixture-home`, forces the file secret store, seeds the connection; `down` removes both); its `.env.example` lines (`SLACK_FIXTURE_HOME`, `AW_SLACK_FAKE`; M1a's own, as the exception above); `scripts/tests/slack-fixture.test.sh` with its `docs/testing/bash-suites.md` line and dotnet-builds entry. The `AW_SLACK_BASE_URL` override is not here: it moves to M1d | api | M1s | `Fake_slack_serves_a_dm_and_a_429`; `Fixture_seed_never_touches_the_keychain` | M, 2 | 1 |
| M1b | Core, pure: `Mrkdwn`, `MessageTs`, `MessageClass`, `MessageRank`, `GapMarker`, `ThreadRow` | api | M1s | `MessageRank_orders_the_example`; `Needs_reply_matches_the_one_definition` (W7); `Mrkdwn_round_trips_the_golden_set` | M, 2 | 1 |
| M1c | `messages.db`, migrations, FTS5, retention (30 days; skips a thread in Needs reply, snoozed or owner-state, with the 180-day cap; snooze read from ledger events, tested with seeded events; purge of derived columns), the conversation columns, the `outbox` columns `origin` and `created_at` | api | M1s | `Needs_reply_rebuild_touches_only_the_changed_thread` (W7); `Retention_keeps_a_thread_in_needs_reply`; `Retention_keeps_a_snoozed_thread_after_views_rebuild`; `Retention_ends_a_skip_after_180_days` | M, 1 | 1 |
| M1d | `ISlackClient`, `SlackWebClient`, the one rate budget, `sindri messages connect` (no-echo prompt) and `--manifest`, the `slack:messages` connection stored with `Preset = null` (§5.1), the Redaction rule (§11), the profile record (§8.5), the `AW_SLACK_BASE_URL` override with its marker rule, its three tests and its `.env.example` line (moved from M1a, call 22), the `probeUrl` for the connection test, SND-MSG-001 to 003, 005, 019, 024, 030 | api | M1a, M1c (the day-0 gate blocks M1e's ready and X2's start, not M1d) | `Rate_limit_429_pauses_only_that_method`; `Base_url_override_is_ignored_without_the_marker`; `Connection_test_calls_auth_test`; `Redaction_scrubs_xapp_and_xoxc`; `Wrong_token_prefix_is_refused_before_any_call` | M, 2 | 1 |
| M1e | Sync (service `messages-sync`): `PollLive` only, the hot set, the search detector, the thread-reply check (20% of the bucket), the cold-start fill, gap markers, status words, `messages.synced` at its rate | api | M1c, M1d | `Gap_without_marker_fails` (W10); `Message_appears_within_30s_on_the_poll_path` (W16); `Reply_in_an_old_thread_is_seen`; `Cold_start_fill_gives_a_sample` | L, 3 | 1 |
| M1f | Read routes, lists, conversation, thread, users, local search, `status`, `doctor`; the read verbs; the session filter | api | M1c (it reads the store; M1e only adds the status words) | `ApiGoldens_messages_lists`; `Session_read_returns_no_text` (W17) | M, 2 | 1 |
| M1g | `POST /sindri/links/resolve` | api (second builder) | M1s (stub row store) | `Resolve_github_pr_url_to_row_key` | M, 1 | 1 |
| M1h | Done and plain snooze through Home's ledger events (`home.row.done`, `.snoozed`, `.woke`, `.revived`; decision 13), appended directly until Home's A2 routes land, with the same event shapes so nothing is replayed; the local read-pointer routes (`/read`, `/unread`, `/lists/unread/read`) for `e` in Unread, `u` and Mark all read | api | M1c; M1i (the Home row must exist); Home S0 (event catalog rows, migration number) | `Done_leaves_needs_reply_and_returns_on_a_new_dm` (W9); `E_in_unread_moves_the_pointer_and_writes_no_home_event` | S, 1 | 1 |
| M1i | Home collector `messages`: `dm` and `mention` rows for class 3 and 2 only (sender and place titles) and the tray count; the `thread-about` edges in `home_links` can follow X2; Home must hide Reply for these kinds until M2c (an open edit to the Home spec, §9.2) | api | M1f; Home S0 routes | `Collector_raises_dm_row_once_per_episode`; `Home_row_title_holds_no_text`; `Class_1_thread_raises_no_home_row` | M, 1 | 1 (blocks X2) |
| M1o | Make issue (`m`): the sheet, the Create call to Linear's create-issue write, the `about` edge in `home_links` and `messages.thread.linked`, the Done-leaves rule (§7.1 exclusion 4), the result line. No reply-in-thread box (Release 2) | api, ui | M1i, M1f, M1j; Linear create (#2961) | `Make_issue_body_holds_no_slack_text_unless_typed`; `Make_issue_with_an_owned_issue_moves_the_thread_out_of_needs_reply` | M, 2 | 1 |
| M1p | Hand to agent (`t`) for Messages rows: Home's picker opened from a Messages row (an Unread row too), the dialog of §9.7 item 8, the per-thread `show` grant (§8.4), the fenced `messages_show` result, the result line. No `messages_draft` (Release 2) | api, ui | M1f, M1j; Home's picker (Home §5.1) | `Show_grant_covers_only_the_handed_thread`; `Show_without_thread_is_refused_under_a_grant`; `Hand_off_writes_nothing_to_slack` | M, 2 | 1 |
| E1 | The poller draws from the one budget (same app id only); `post_message` and the poller refuse `slack:messages` (`Preset = null`) | engine | M1d | `Two_readers_draw_from_one_bucket`; `Post_message_refuses_the_messages_connection`; `Poller_refuses_the_messages_connection`; `Generic_connection_add_refuses_the_messages_name` | S, 1 | 1 |
| E0 | The run-store residency harness: a fixture trigger through `workflow_run`, `workflow_node_run` and Temporal history, scanned for text bytes; includes a `prompt` node over a Messages trigger field (W18). Only M3d needs it, so it waits for Release 3 | engine | none | `Run_record_holds_no_message_text` | S, 1 | 3 |
| M1j | Frames (§15) | design | Home's tokens | the numbers in §6 | M, 1 | 1 |
| M1k | Rail, Needs reply and Unread on `RowList`, `Row`, `PeekPane`; counts; `Ctrl+K` with local search and its empty and limited states; `n`, `x`; the footer per mode; the result lines | ui | M1j; the M1s stubs | `Needs_reply_paints_200_rows_with_one_store_read` (W1); `Cursor_starts_on_row_one_and_returns_to_the_last_opened` | M, 2 | 1 |
| M1l | Conversation: dense lines, virtualized, gap markers, collapsed threads, reaction chips, code and diff | ui | M1j, M1b, M1f | `Short_messages_group_by_author_in_one_line_each` (W4, W5) | L, 3 | 1 |
| M1m | Peek: thread, inline link rows, the linked object, "Open in Slack" | ui | M1l, M1g | `Link_row_paints_with_no_network_call` (W8) | M, 2 | 1 |
| M1n | First run: the connect card and its states (§6.8) | ui | M1d, M1j | `First_run_shows_admin_blocked_request_text` | M, 1 | 1 |
| T1 | **tools lane, Release 1.** The `aot-smoke/expected.txt` lines (services `messages-sync` and `messages-retention`, `tools/list`, doctor rows `messages:token`, `messages:sync`, `messages:store`, `messages:model`, and the four `messages:*` doctor rows when Messages is not connected, in the shape of `ok    context:index-slack not configured (...)`, i.e. `ok    messages:token not configured (...)`; `messages-sync` and `messages-retention` are listed even when idle, between `memory-expire` and `setup`; the doctor `setup` line 33 and its twin at line 259 stay as they are while the step is `Skipped`, because `SetupDoctor` leaves a skipped step out and the smoke profile does not opt in), the `messages` step in `SetupSteps.cs` (`Skipped` until opt-in, then `NeedsYou`, done when `messages:token` is ok), `Usage.cs`, `docs/sindri/errors.md`, `docs/sindri/messages.md`, the AGENTS.md Commands block. `.env.example` is not T1's for Messages: M0a, M1a and M1d carry their own lines (`AW_SLACK_PROBE_KEY`, the fixture script's variables, `AW_SLACK_BASE_URL`), and `AW_MESSAGES_ADAPTER` is gone (call 11) | tools | M1s (the names); grows with M1d to M1f | `Aot_smoke_expected_lists_the_messages_services_and_tools` | M, 2 | 1 |
| M3f | perf: W1, W5 and W7 timings on the seeded fake; runs after gate G, because the gate reads the owner's numbers, not these | perf | M1a (a seed generator is day-one work) | `Scroll_p95_under_16ms_at_10000_lines` | M, 1 | 1 |
| Q1 | qa (after gate G; it feeds none of the gate numbers): hostile fixtures (injection, bidi), a 429 storm, a revoked token, a gap, the residency scan (W15, W18) | qa | M1a; fixture files are day-one work | `Hostile_fixture_set_stays_plain_text`; `Text_residency_scan_finds_no_body` | M, 2 | 1 |
| X1 | ux: the baseline: a week of Slack-open counts (opens to read) and the timing of 10 fixed jobs in Slack, now, before X2 | ux | none | the timing sheet | M, 1 | 1 |
| X2 | The Release 1 dogfood week, split. Days 1 and 2: the owner reads real Slack beside Sindri; the owner marks each wrong class with `x` and files each gap as an issue that names a parity row and quotes nothing (feeds gate G (a), (b)). Days 3 to 7: Slack stays closed except Open in Slack; ux tallies the opens to read (feeds (c)). The owner sees Home's tray count | ux, owner | M1k, M1l, M1m, M1i, M1h, M1o, M1p | the dogfood sheet | M, 1 | 1 |
| G | **Gate G.** The owner decides. Continue to Release 2 only if: (a) Needs reply precision is 80% or more on 30 rows the owner marks over the week (rows seen, not at once; `x` is wrong, a seen row without `x` is right; cold-start `partial` rows are counted apart and do not count toward the 30), plus a recall check: the owner names 10 threads they answered in Slack that week, and at least 9 were in Needs reply or Unread before the answer, (b) a new DM shows within 30 s and a reply in an older thread within the bound of §5.2, and (c) the owner's Slack **opens to read** for "what needs me" in days 3 to 7 are at most half of the X1 baseline (an open from a Sindri permalink is a reply, not a read, and is not counted); ux tallies them in the dogfood issue. Otherwise kill rule 3 (§1). The 30 rows, 10 threads and 9 of 10 are `agent default (lead, 2026-11-12)`, and 9 of 10 went to the owner with the §16 batch (*owner can flip*); a real Needs reply list holds 5 to 15 rows at a time | owner | X2 | the three numbers | none | 1 to 2 |
| M2t | TLA+ model of the outbox, `specs/tla/MessageOutbox/` (`MessageOutbox.tla`, `.cfg`, a `gaps/` witness). It covers send, the echo arriving before the response, a crash, a retry, and a 429 hold that expires. Bounds: 1 conversation, 2 sends, 1 crash. Checks: at most one Slack message per `actId`; no line shown twice; an `unknown` row is never re-sent unproven; an expired 429 hold never sends later. Runs with `scripts/tlc-check.sh` | api (formal-verify) | none: it needs only §8.3, so it starts on day one | the witness config fails on the invariant its line 1 names | M, 1 | 2 (gates M2a) |
| M2a | `MessageOutbox` and `ISlackSender` (the one write port; `BannedSymbols.txt` and the architecture rule of W12), `send`, reply, react, the read-mark routes with the opt-in sync (online only), SND-MSG-004 to 009, 028, 032 | api | M1d, M1e, M2t, gate G | `Failed_send_keeps_the_line_and_retry_lands_once` (W3); `Echo_before_response_does_not_duplicate_the_line`; `Restart_with_unknown_row_never_resends_unproven` | L, 3 | 2 |
| M2b | Drafts routes and autosave | api | M1c, gate G | `Draft_saves_after_3_characters` | S, 1 | 2 |
| M2c | Composer: `Ctrl+Enter` send, the guards, agent drafts, optimistic line, retry, formatting, `@ # :`, `[[`; the make-issue box "Reply in the thread with the issue link" | ui | M2a, M2b, M1b | `Ctrl_enter_sends_and_enter_adds_a_line`; `Agent_draft_plus_enter_sends_nothing`; `Broadcast_opens_the_submit_menu` | L, 3 | 2 |
| M2d | The react picker (`o`), the Later and Drafts rail lists | ui | M2a, M1l | `React_pick_commits_one_write` | M, 1 | 2 |
| M2e | The "follow up if no reply" checkbox on Home's snooze picker, the "back at" line, the post-send toast (#3020) | ui | M1h (Home's store, #3030), M1k | `Follow_up_toggle_stores_my_last_ts` | M, 1 | 2 |
| M2f | The write verbs, `messages_draft`, the MCP tools, `send --retry` | api | M2a, M1f | `Every_route_has_a_verb_or_an_owner_only_row`; `Cli_send_again_without_retry_of_is_refused_while_the_first_is_unknown` | M, 1 | 2 |
| T2 | tools lane, Release 2: the R2 lines in `expected.txt`, errors, docs, `Usage.cs` | tools | M2a, M2f | the aot-smoke diff | S, 1 | 2 |
| Q2 | qa: echo-before-response, offline refuses at once and keeps the draft, restart without re-send (W3) | qa | M2a | `Hostile_send_set_posts_once`; `Offline_send_refuses_and_keeps_the_draft` | M, 1 | 2 |
| X3 | ux: the day without Slack (W14), after M2e | ux | M2e | the scripted-day sheet | M, 1 | 2 |
| T3 | tools lane, Release 3: one tools slice per Release 3 phase pulled (jobs, services, MCP tools, doctor rows that change `expected.txt`); a phase that adds them sends its lines to tools before ready | tools | the pulling phase | the aot-smoke diff | S, 1 each | 3 |
| M3a | `MessageGist` job, cache, plain fallback, the allow list | engine | M1c | `Gist_default_sends_nothing_to_a_model` | M, 1 | 3 |
| M3b | Attach (`a`) and its picker; the sheet's attach tab | api, ui | M1o | `Attach_records_an_about_edge_and_creates_nothing` | M, 1 | 3 |
| M3c | `MessagesBrief` job and UI | engine, ui | M1f, M3a | `Brief_line_citing_a_missing_ts_is_dropped` (W11) | M, 2 | 3 |
| M3d0 | **Engine: the store-fed event source** (call 23): a source the sync service calls, keyed per connection (not an `IEventPoller`), starting runs through the occurrence and dedupe path with `deliveryId` = the message `ts`; the catalog check per connection kind; the kinds `messages.mention`, `.dm`, `.thread.reply`, `.keyword` | engine | M1e | `Trigger_on_messages_connection_fires_once_per_ts`; `Messages_kind_on_a_work_connection_is_refused` | M, 2 | 3 |
| M3d1 | Engine triggers from the store (ids only, on the M3d0 source), the `messages_classify` node, `draft_message`, the `slack-question-draft` template | engine | M3d0, M1e, M2b, E0; #2960's test run | `Messages_classify_stores_ids_only` (W18); `Prompt_node_over_a_trigger_field_stores_no_body` | M, 2 | 3 |
| M3d2 | `send_message` (owner-written templates only; the date field "`draft_message` in use since" gates it to two weeks after M3d1) | engine | M3d1 | `Send_message_activation_is_refused_without_a_human_revision`; `Send_message_refuses_text_from_a_prompt_node`; `Send_message_slot_with_at_channel_is_refused` (W12) | M, 2 | 3 |
| M3e | Unified search: Slack second pass, the Everything scope | api, ui, context | M1f | `Operator_parse_golden` (W13) | M, 2 | 3 |
| M3g | `SocketModeLive` behind `ISlackLive`, `FakeSocketModeHost`, the `xapp-` token in `TokenSet.ClientSecret`, overlap reconnect | api | M1e, P0 line 5 | `Socket_event_appears_within_5s` | L, 3 | 3 |
| M4a to M4f | Files; scheduled send; notification rules and toasts; app messages; status, DND, presence; channel create, join, leave, mute, pins, forward, edit, delete | api, ui | M2a (M1i for M4c) | one red test per row, named when the row is pulled | M to L | 3 |
| M5 | Save a decision as a note | ui | Notes (#2981) | `Save_note_carries_quote_and_permalink` | S, 1 | 3 |

**Phase graph.**

1. M1s first (hours, not days). Everything in Release 1 starts from its stubs.
2. M0a -> M0c. The owner's run, kill rules 1 and 2, and the day-0 gate decide whether M1e is ready and X2 starts. M1i and M1h also block X2 (call 25). M1a, M1b, M1c, M1d, M1g and M2t need no probe.
3. M1a + M1c -> M1d -> M1e -> M1f -> M1i. M1d builds against the documented scope table and the fake, and does not wait for M0c; M0c can only add or remove a scope.
4. M1b + M1j + M1f -> M1k, M1l -> M1m. M1k starts on the M1s stubs the day it merges, with a golden of the list.
5. M1d -> E1, M1n. M1c -> M1h. M1i + M1f + M1j -> M1o. M1f + M1j -> M1p.
6. M1k + M1l + M1m + M1o + M1p -> X2 -> gate G. M2t has been finished since early in Release 1.
7. Gate G -> M2a -> M2c, M2d; M1c -> M2b -> M2c; M1h + M1k -> M2e; M1f + M2a -> M2f -> T2; M2a -> Q2; M2e -> X3.
8. Release 3 rows run in parallel, one lane each, only when an X2 or X3 issue names the row (disjoint files).

**Every lane has work on day one.** api: M0a, then M1s, then M1a, M1b, M1c, M1g (a second builder takes M1g and M1b) and M2t. ideas: M0b. design: M1j. ui: M1k the day M1s merges. tools: T1 on the M1s names. engine: E0. perf: the M3f seed generator. qa: the Q1 fixture files. ux: X1. owner: M0c.

**Hot files** that serialize PRs: `EndpointTable.cs`, `Usage.cs`, `EventCatalog.cs`, `SindriErrors.*`, `PublicAPI.Unshipped.txt` (Core's is 19,000 lines), `Composition.cs` and `src/Sindri/aot-smoke/expected.txt`. Serialize M1f, M2a and M3b on `EndpointTable.cs`. **One owner for the shared files:** the tools lane makes every change to `expected.txt`, `docs/sindri/errors.md` and `Usage.cs` in T1, T2 and T3; other lanes send it the lines they need. One stated exception: a phase that adds an environment variable (M0a, M1a, M1d) carries its own `.env.example` line, because `check-env-example.sh` counts bash and an unlisted variable breaks main. Early dogfood: when M1k and M1l merge, the owner reads real Slack read-only beside the Slack app (X2). Nothing waits on a source that does not exist: links return `none` before M1g; the composer does not exist before M2c.

**Gates per phase.** Every phase: the `-warnaserror` build, the touched projects' lanes and scoped format (per project). `Aw.Architecture.Tests` from M1s (the new namespace and the loopback host), and `node scripts/check-deps.mjs`. M1c to M2f also run the ApiGoldens classes and `EndpointCqrsTests`. `check-env-example.sh` runs in M0a, M1a, M1c to M2f (so M1d is in) and in any phase that adds a variable. A new route adds its golden scenario row and the Ui `GoldenContract` slug; run `AW_GOLDEN_UPDATE=1`, then rebuild. A new POST runs the full Sindri integration lane. Every phase that adds an event runs `EventCatalogTests` (M1e, M1i, M2a, M3b, M3d0, M3d1). Any new service, MCP tool, doctor row or `Usage.cs` change runs aot-smoke and the whole `Aw.Sindri.Tests` unit lane. M1i and M3d1 add Needs-you or decision rows, so they run `Aw.Sindri.Ui.Controls.Tests` integration. M1j to M1n use type and tokens, so they run `Aw.Sindri.Ui.Rendering.Tests`. M1k to M2e that add views run `Aw.Sindri.Ui.Frames.Tests` integration. Any change to a fake under `tests/Aw.Connectors.Testing` runs `Aw.Connectors.Tests` (the project that exercises `FakeSlackServer`; `Aw.Connectors.Testing.Tests` does not exist), and a change under `tests/Aw.Sindri.Testing` runs that fake's own tests. M2t runs `scripts/tlc-check.sh`. W13 and W5 at full size, and the wall-clock lines of W1, W2, W4, W7 and W16, run in the perf lane (M3f), never in the unit or integration lane.

**Docs to update per phase:** new `docs/sindri/messages.md` (with a "Connect Slack" section: the manifest, the token steps, the scopes, the table of Slack tokens, the 10-minute target); `docs/parity/slack.md`; `docs/csharp/sindri.md` (CLI table, errors); `docs/csharp/sindri-ui.md` (the page, keys); `docs/csharp/sindri-profile.md` (the `messages.*` keys); `docs/sindri/connections.md` (the Slack row stops saying "REST only"); `docs/testing/bash-suites.md` (the probe, parity and fixture suites).

## 15. Frames for design (M1j)

1. Needs reply with the peek open at 1,440 px: 25 rows, the three class glyphs, a reason chip, buttons on the cursor row, the New band.
2. The dense conversation: a busy channel, a DM, a thread in the peek with an inline link row, a code block, a diff block, reaction chips, the New divider, a gap marker.
3. The composer (Release 2): empty, typing with a `[[` picker open, an agent draft with its label, run link and risk chips, the submit menu with "Notify 412 people", a failed send with the "Slack may have delivered this" lines, the offline composer with text being typed and the draft kept, a 429 wait with its countdown.
4. The snooze picker beside the cursor row and the "back Mon 9:00" row line (Release 1); its "follow up if no reply" checkbox is Release 2. The agent hand-off dialog (§9.7 item 8, Release 1).
5. The make-issue sheet (Release 1) and the attach picker (Release 3).
6. (Release 3) The catch-up brief with source chips, and its model-off state.
7. (Release 3) Search: Messages scope with "from Slack" tags; the Everything scope in groups.
8. First run (§6.8): the connect card in three steps (create the app, install it, paste the token), each with its failure line, the admin-blocked state with the copyable request text, then the first sync ("40 conversations, 0 messages so far") and "Listening".
9. Header states: Live, Behind with the rate-limit words, Offline, Token expired, Scope missing.
10. A user peek (Release 2); a file chip and thumbnail and an app message with blocks (Release 3), "Open in Slack" for a huddle.
11. Dark frames of 1 and 2; the focus ring on a row, a message line and the composer.
12. Widths 1,440, 1,200 and 1,000 px; the rail collapsed; hover and pressed states for a row, a chip and a button.
13. The state rows of §6.6: Later and Drafts empty, a link row with `none`, an offline header, the footer mode chip ("List keys" and "Message keys"), the emoji picker with no match, a toast showing sender and place only.
14. The result lines of §6.6: Done, mark read, Mark all read, snooze, wrong class, hand to agent, make issue.
15. The peek with the root and three replies; the peek with "Replies not loaded"; the conversation with the collapsed thread line under its own root.
16. Release 1 search: local rows only, the empty state and the limited state.
17. Conversation states: "No messages yet. Say hi.", "History not loaded", Slack Connect "not kept here", Load older running and offline.
18. The footer per mode (§6.5), at 1,440 and 1,000 px.
19. Unread after `e` and after Mark all read; the caught-up Needs reply with "Open Unread (14)".
20. A strip of rest, hover and pressed for a row, a chip and a button.
No frame carries a Release badge; the release stays in the file name.

## 16. Questions for the owner

Each has a recommended pick. Questions 1 and 8 are one day-0 gate, and the owner answers both before M1e is ready and X2 starts (Release 1 builds against the fake until then, M1d included). Questions 2 to 11 do not block; ask them in one batch of click-throughs. A pick the body already uses is tagged `agent default (question N)`. Questions 10 and 11 are written to the lead's pick (2026-11-12). Questions 2 to 7 and 9, and the gate G recall bar (9 of 10, row G of §14), went to the owner as one click-through batch on 2026-11-12. The owner is away, so the body is written to each pick below; each is marked *owner can flip*, and an answer that differs is folded in when it arrives.

1. **Can you install a Slack app with user scopes on the work workspace** (Release 1 needs `im:history`, `mpim:history`, `channels:history`, `groups:history`, the `*:read` scopes, `users:read`, `emoji:read` and `search:read`; the write scopes come in Release 2)? An admin may need to approve it. Pick: run the P0 probe's line 1 and line 8 today. If an admin blocks it, Messages cannot ship (kill rule 1). Tell me which scopes it allows and §5.1 adapts.
2. **One workspace or several?** Pick: one for Release 1. The workspace comes from `auth.test`, not a profile key. *Owner can flip.*
3. **Which keys inside a conversation?** Pick: one key set (Home's) with Slack-only message keys on letters Home does not use (`i` edit, `o` react, `s` share, `p` pin, `Del`), so no letter has two meanings. The cost: Slack's `e` and `r` move. The alternative is two sets with Slack's letters, which Reviews' rule forbids. *Owner can flip.*
4. **Should "make issue" also reply in the Slack thread with the link?** Pick: no by default, a checkbox per use. A reply shows colleagues that you changed tools. *Owner can flip.*
5. **Agent send limits.** Is "named conversations, 20 a day per workflow, 40 per workspace, no broadcasts, owner-written templates only" (§9.7) the right fence for `send_message`? Pick: yes, and `slack-question-draft` ships off. You write the rule by saving the workflow. *Owner can flip.*
6. **How much history to keep and load?** Pick: 30 days of text, 14 days of backfill for DMs, starred channels and your threads and 3 days for the rest, only when the probe shows Tier 3. On a limited workspace there is no backfill. *Owner can flip.*
7. **Also create a Slack reminder when you snooze?** `reminders.add` would DM you from Slackbot. Pick: no. Sindri's snooze is the only reminder, so there is one inbox. *Owner can flip.*
8. **Is storing Slack messages on the work laptop allowed by your company?** `messages.db` holds DMs. Pick: ask. Until you answer, Messages stores 30 days and no Slack Connect text. Part of the day-0 gate.
9. **Should Done on a thread also close the Linear issue it links?** Pick: no. Done is local; closing the issue is its own action in the `.` panel. *Owner can flip.*
10. **May Slack text go to a hosted model?** Written to the lead's pick: no by default. The gist and the catch-up brief are off, and they never run on DMs, group DMs or Slack Connect. A later opt-in per starred channel is allowed. Hand to agent stays your own act and names the provider.
11. **Do read marks write to Slack?** Written to the lead's pick: no. Read marks stay in Sindri, and your phone keeps its own unread state. `messages.syncReadToSlack` or the `.` panel's "Mark read in Slack" writes them when you choose.

### Review notes

**First draft (ideas, 2026-11-12).** No review round had run.

**Autoplan round 1 (ideas, 2026-11-12).** Five lenses (product, architecture, security, devex, design) returned NEEDS_WORK. Folded, with the lead's seven calls recorded in the Decisions list (tagged `agent default (lead, 2026-11-12)`):

- Release 1 is a read-only slice with a kill rule and gate G; writes are Release 2; the rest is demand-pulled (§1, §14).
- Work chat never goes to a hosted model by default (decision 15, §9.1, §9.6, §11, question 10).
- No bare key writes to Slack; read marks stay local; the composer sends on `Ctrl+Enter`; `Esc` has one job; one key set (decisions 10 and 11, §6.5, question 11).
- Run stores, run stores and logs hold ids, never text; Home titles hold sender and place names (decision 20, lead call 10, §9.7, §11, W18).
- `send_message` takes owner-written templates only; one rule for agent drafts (decision 14, §6.4, §9.7).
- The `xapp-` token lives in `TokenSet.ClientSecret`; Messages has the `slack:messages` connection; the Redaction rule extends in M1d (§5.1, §11).
- `home_links` replaces `thread_links`; `workspaces` and `sync_cursors` are gone; every table has a reason (§8.1).
- A TLA+ outbox model gates M2a; one fake, `FakeSlackServer`, serves tests and proofs; the tools lane owns the shared files (§14).
- One definition of Needs reply (§7.1); one rate budget for the poller and the sync (§5.3); make-issue carries a permalink and owner-typed text (§9.5).
- The state rows, the first-run card, the Look block and the parity rows 46 to 56 (§3, §6).

Settled after round 1 (lead calls 8 to 11): the store is Home's `home_rows`; node I/O is ids only with no engine change; Home titles keep names; one fake.

**Autoplan round 2 (ideas, 2026-11-12).** Five lenses returned NEEDS_WORK with no CRITICAL. Round 1 MUSTs fixed in text: 45 of 52 (product 5/5, security 7/8, devex 10/13, arch 13/15, design 10/11). Folded, with the lead's calls 12 to 20 recorded in §2:

- Offline never queues a Slack write; a 429 hold is capped at 2 minutes (call 12, §6.4, §6.6, §8.3, SND-MSG-005, 007, 028).
- The generic `prompt` node stores text, so `slack-question-draft` uses a Messages node, `messages_classify` (call 13, §9.7, W18).
- The fake gets a loopback host and a marker-guarded, loopback-only base-URL override (call 14, §8.1, M1a).
- The poll path reads replies in older threads and does a cold-start fill (calls 15 and 19, §5.2).
- Retention skips Needs reply and snoozed threads; purge takes derived columns (call 16, §8.1).
- Done and snooze use Home's ledger events (call 17, decision 13, §9.4).
- Keys follow Home: splits for the four lists, `t` for hand-off only, a Pin / Cancel sheet, a prefilled share, `u` never writes; Home owns toasts and the tray (call 18, §6.4, §6.5, §9.9).
- Gate G counts opens to read; `r` opens the permalink in Release 1 (call 19).
- The plan: `Preset = null`, M1d off the day-0 gate, a probe manifest, `.env.example` lines carried by the phase that adds the variable, the pinned doctor line, T3 (call 20, §14).
- The three smaller MUSTs: a per-thread grant for `show` (§8.4), the `@channel` check on rendered `send_message` text (§9.7 item 6), and `connect --app-token` for `xapp-` (§5.1).
- SHOULDs and NITs folded where cheap: Slack Connect shows a title-only row; the empty conversation has three states; Home hides Reply for `dm` and `mention` rows until M2c; the `x` miss has an event; the daily cap counts outbox rows; the bucket is keyed by app; width budget, accessibility lines, exact CDN allow list, E0 moved to Release 3, M3d split, test project names, service names, exit codes.

**Autoplan round 3 (ideas, 2026-11-12).** Five lenses returned NEEDS_WORK with no CRITICAL; every round 2 MUST of each lens was fixed in the text. Folded, with the lead's calls 21 to 26 recorded in §2:

- Class 1 stays out of Home in Release 1: Needs reply and the collector carry class 3 and 2 (call 21).
- The base-URL override moves to M1d; the fixture forces the file secret store and has its own bash suite; the host is `Aw.Connectors.Testing.Server`; T1's doctor-line claim is corrected (call 22).
- Sync-fed triggers get an engine phase, M3d0, and new `messages.*` kind names (call 23).
- The broadcast check uses Slack's `<!channel>` forms on rendered text, and slot values are escaped (call 24).
- Gate G is 30 rows plus a recall check, over a split week; M1i and M1h block X2 (call 25).
- Retention is per thread with a 180-day cap and reads the ledger; the outbox has `origin` and `created_at` (call 26).
- Smaller fixes: an editable offline composer, reply-check rotation and a stated latency, a probe for the connection test, error text without message text, the `show` grant for a DM, offline states for Retry, react, pin, delete, first run and Load older, the Release 1 mock, stale `/` and `l` text, one meaning of "toast", the Home Reply line as an open edit, `agent default` tags on the new numbers, M3f and Q1 after the gate, and the NITs.

**Meta's UX pass (ideas, 2026-11-12).** Meta opened 40 of the 55 frames and sent 12 MUSTs, 14 nits and 5 picks. Folded, recorded as decision 22:

- MUST 1 to 3, 11: first-run dead ends, step 3 failures, day-one progress, conversation states (§6.6, §6.8).
- MUST 4: `t` and `m` in Release 1; the agent draft and the reply-with-link box stay in Release 2 (§1, §9.5, §9.7, M1o, M1p).
- MUST 5 to 7, 9: `e` in Unread, Mark all read, result lines, `x` and `n` in the footer, the cursor rule (§6.2, §6.5, §6.6).
- MUST 6, 8, 10, 12: the peek's replies, no release names in UI copy, Release 1 search, hover and pressed (§6.1, §9.8).
- Nits taken (text only): 1, 2, 4 to 14. Meta's picks were taken as written.
- Nit 3 not taken: the cursor-row button, the peek button and the footer chip name one action in three places on purpose, as Home's row / peek / footer grammar does; merging them is a design change, not a text fix.

3 /autoplan rounds done; frames drawn; meta's UX pass folded; next: design redraws the frames in `messages-ux-redraw.md`.
