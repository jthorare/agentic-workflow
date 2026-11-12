# Prior art: Home, the one inbox, and the row / peek / full grammar

This file is the prior-art ledger for the Home surface (rule: #2989). It lists the daily jobs Home has to do, the best existing answer to each, what we copy, and where one app beats a single-source tool.
Facts come from the products' own docs, changelogs and READMEs, read 2026-11-11 or 2026-11-12. Vendor claims are marked "vendor". Anything I could not confirm says "unconfirmed".
Surface job: show everything that needs the owner from every source in one ranked list, act on each row in place, and say "you're done" when the list is empty.
Direction issue: #2978 (grammar, Home as inbox). Ledger rule: #2989. Snapshot of Sindri is origin/main.

Source gaps, so nobody trusts a hole: Superhuman's help-center pages returned 403/404 to my fetcher, so Superhuman keys below come from search snippets of those pages (marked "snippet"). Todoist, aerc and Gmail density pages did not load either. gh-dash's keybinding page 404'd. No screenshots were captured; image URLs are given where a page showed one.

---

## 1. See what needs me from 5 sources in one list

**Best today**
- Universal Inbox (open source, Apache-2.0, Rust, Postgres): one inbox for GitHub, Linear, Google Mail and Slack notifications plus Todoist tasks. Rows show source, type, title, indicators, timestamp. Sync is polling "every few minutes", except Slack, which uses webhooks. https://github.com/universal-inbox/universal-inbox , docs https://universal-inbox-doc.pages.dev/how/collecting_notifications . Screen: https://universal-inbox-doc.pages.dev/quick_start/images/inbox-screen.png (not fetched). source: docs
- Superhuman split inbox (vendor docs): named splits at the top; Tab and Shift+Tab move between splits; the engineering-team page recommends GitHub, Linear, Jira and Slack splits from a "Split Inbox Library" and no more than seven splits (snippet). https://blog.superhuman.com/how-engineering-teams-can-organize-their-inbox/ , https://help.superhuman.com/article/504-setting-up-splits source: docs
- Gitify (MIT, Electron): one tray list over GitHub, Gitea/Forgejo, Bitbucket Cloud, GitLab. https://github.com/gitify-app/gitify source: docs
- Huly (open source): one inbox with filter tabs All / Issues / Messages across its own issues and chat. https://docs.huly.io/communication/inbox/ source: docs

**What we take**
- Splits as tabs on one list, not separate pages: `Tab` / `Shift+Tab` cycle them. Our default splits: All, Reviews, Messages, Work, Agents. Each split is a saved filter (source + reason).
- Every row names its source with a glyph in a fixed first column (Universal Inbox does this with source/type labels).
- Universal Inbox is Apache-2.0, so we can read its code; we copy the shape, not the code.

**Where one app wins**
- These tools all pull from the sources' public APIs. A Linear row cannot show red CI, and a Slack row cannot show the PR it is about. Our row can carry the linked PR's CI state and the thread's last message in the same line, because we already hold all of it in one store.

**Desktop gain**
- Read all five sources from the local store: Home first paint under 100 ms with 5k rows loaded, no network call on the path (perf measures: window open to first row painted). Universal Inbox is a web app polling every few minutes; ours updates from the ledger event stream.

**Sindri today**
- Partial. Home shows a Needs-you list (decisions, failures, setup) and a Todo card side by side: `Views/Shell/HomePage.axaml` (columns `3*,16,2*`), `ViewModels/NeedsYou/NeedsYouFeed.cs`. No Slack/Linear/PR rows yet; the Linear "since you last looked" lives under Work (#2536). Duplicate count bug (#2827). Raw ids lead rows (#2782).

**Verdict:** take and change (splits as tabs, plus linked CI/thread facts on the row).

---

## 2. Rank by what unblocks the most

**Best today**
- GitHub notifications `reason` field: every notification carries why you got it (assign, review-requested, mention, etc.). Default filters are assigned, participating, review requested, mentions. https://docs.github.com/en/subscriptions-and-notifications/how-tos/viewing-and-triaging-notifications/managing-notifications-from-your-inbox source: docs
- Linear Priority tab (2026-09-03): a tab "separates what needs your attention from what can wait"; Linear picks the default contents (example: "a review blocking a release"), you change it by notification source or a filter. https://linear.app/changelog/2026-09-03-priority-inbox , https://linear.app/docs/inbox.md . Linear does not publish the rule it uses; unconfirmed. source: docs
- GitHub mobile "Focused" inbox filter (Oct 2024): important notifications first. https://github.blog/changelog/2024-10-14-whats-new-in-mobile-october-update/ (snippet) source: docs
- Linear "Show unread first" display option. https://linear.app/docs/inbox.md source: docs

**What we take**
- Two tabs: Priority and Other (Linear), with Priority filled by a rule the user can read and edit, not a black box.
- Our rule (new, nobody has it): score = number of other items blocked by this one (a PR that blocks 3 issues, an agent stuck that blocks 2 runs) first, then reason weight (review requested > mention > assigned > FYI), then age. Show the "why" as a one-word chip on the row ("blocks 3").
- Unread-first inside a score band.

**Where one app wins**
- No other tool knows an object graph across Slack, Linear, PRs and agent runs, so none can say "this review request blocks 3 issues and an agent run". We can, because the row model links them (#2978 item 3).

**Desktop gain**
- Re-rank on every incoming event with no server round trip: 10k rows re-scored in under 16 ms (perf measures: event in to reordered list painted).

**Sindri today**
- Missing. Rows group by kind (`NeedsYouGrouping.cs`, queue groups running / withYou / queued in `NeedsYouKinds.cs`); no score, no blocked-count. The ranking route is on api per #2978.

**Verdict:** take and change (Linear's Priority/Other split, our blocked-count score).

---

## 3. Act in place: reply, approve, merge, snooze, hand to an agent

**Best today**
- Raycast Action Panel: `Cmd+K` on the selected item opens its actions; `Enter` runs the first (primary) action; `Cmd+Enter` secondary; `Opt+Enter` tertiary; each action shows its shortcut on the right; typing filters actions by fuzzy match. https://manual.raycast.com/action-panel.md , https://manual.raycast.com/keyboard-shortcuts.md source: docs
- Universal Inbox: per-row single-key actions `d` delete, `u` unsubscribe, `s` snooze, `p` create task, `l` link to task, `Enter` open in source. https://universal-inbox-doc.pages.dev/misc/keyboard_shortcuts.html source: docs
- Octobox (AGPL-3.0, so do not read or copy code): `y`/`e` archive, `m` mute, `d` mark read on GitHub too, `s` star, `x` select. https://github.com/octobox/octobox source: docs
- GitHub mobile: swipe a row to mark done or save. https://github.blog/news-insights/product-news/github-for-mobile-is-now-available (snippet) source: docs

**What we take**
- One primary action per row kind, bound to `Enter`'s sibling: a row's primary action is the one thing most likely right (PR review requested -> Approve is NOT primary; Open review is; CI-green own PR -> Merge is).
- `.` opens the row's action panel (Raycast style), each action shows its single key. Direct keys on the list: `e` done, `h` snooze, `r` reply (inline composer in peek), `a` approve/primary, `g` hand to agent (picks an agent, prefilled with the row's link and context).
- Optimistic write, toast with Undo (we already have this on Needs you: `Seen.UndoOffered`), visible retry on failure (#2978 contract rule 2: lands in the source within 2 s).

**Where one app wins**
- Raycast and Universal Inbox act only inside one source per row. We can offer cross-source actions on one row: "approve PR, then close the Linear issue, then reply in the thread" as one action, and "hand to agent" with the PR, the issue and the thread already attached.

**Desktop gain**
- Key to visible state change under 50 ms (optimistic local write), measured from keydown to repaint. A web inbox waits on its server.

**Sindri today**
- Partial. Needs-you rows have `A` primary action, `X` decline, `Y` copy command, `Enter` sheet, `Esc` back (`ViewModels/NeedsYou/NeedsYouKey.cs`; `NeedsYouActionControl.cs`, `NeedsYouDispatcher.cs`). Todo has check-off with Undo toast (`Labels.TodoKeys.cs`). Missing: reply, merge, snooze, hand-to-agent from a row.

**Verdict:** take and change (Raycast panel + per-row single keys, add cross-source and hand-to-agent actions).

---

## 4. Zero-inbox flow: what "done" means

**Best today**
- GitHub: Done "marks a notification as completed and removes it from your inbox", kept 3 months (`is:done`); Save keeps it indefinitely (`is:saved`). Unsubscribe is a third, separate action. https://docs.github.com/en/subscriptions-and-notifications/how-tos/viewing-and-triaging-notifications/managing-notifications-from-your-inbox . Key `E` = done (docs summary, unverified wording). source: docs
- Octobox archive: a done item comes back on the next sync if there is new activity. https://github.com/octobox/octobox source: docs
- Linear: no done/archive state; "We don't support archiving notifications at this time." You delete (`Backspace`), toggle read (`U`), or snooze (`H`); cap 2,000 open notifications. https://linear.app/docs/inbox.md source: docs
- Superhuman: `E` marks Done and "inbox zero" is the goal (snippet; help page 403). https://help.superhuman.com/article/582-achieving-inbox-zero source: docs

**What we take**
- One verb, `Done` (`e`), the same on every row kind. Done = gone from Home, kept and findable (`is:done`, 90 days like GitHub).
- A done row comes back on its own when the source has new activity addressed to you (Octobox, Linear's snooze rule). A done row never comes back for activity you did not need to see.
- When the Priority list is empty, Home says "You're done" and shows only the since-you-last-looked line. Empty means nothing is waiting on you.
- Linear's missing archive is a known pain; we avoid it.

**Where one app wins**
- Done in one app can close the thing in the source when that is the right meaning (a merged PR's row is done by itself; a replied Slack thread is done when you reply). Single-source inboxes cannot see that you handled it elsewhere.

**Desktop gain**
- Done is a local write: row leaves the list in one frame (< 16 ms), source write follows in background.

**Sindri today**
- Partial. "Mark all seen" with Undo and a "held" pill exist (`HomePage.axaml` `MarkAllSeen`, `NeedsYouHeld`); empty line "Nothing needs you." (`Labels.Home.cs`). No per-row done, no revive-on-activity, no done archive.

**Verdict:** take and change (GitHub's Done/Save split + Octobox revive rule).

---

## 5. Snooze / remind me

**Best today**
- Linear: `H` snoozes a notification (and sets a reminder on an issue); type a date: "Til month/date", "Next quarter", "For X weeks"; the notification returns at the time or earlier on new activity; "Show snoozed" display toggle. Presets mentioned: an hour, tomorrow, next cycle. https://linear.app/docs/inbox.md , https://linear.app/changelog/2021-06-17-inbox-snooze-and-easier-issue-merge source: docs
- Superhuman Remind Me: `H` (or Cmd+K -> Remind Me), type a day/time; `G` then `H` lists reminders; snippet only, help page 403. https://help.superhuman.com/hc/en-us/articles/43822934607251-Remind-Me source: docs
- Universal Inbox: `s` snooze. Duration is unclear: README says "until the next day", docs say "a few hours"; I could not find the exact rule. https://universal-inbox-doc.pages.dev/how/actions/github source: docs
- Things: a to-do with a future start date "hibernates in Upcoming" and moves to Today on its date; Someday hides until you pull it back. https://culturedcode.com/things/support/articles/2803579/ source: docs

**What we take**
- `h` opens a one-line date box with typed natural language (Linear/Superhuman) and 4 presets: 1 hour, tomorrow 9:00, next Monday 9:00, someday.
- Snoozed returns early on new activity addressed to you (Linear), and the row says "back early: new reply".
- A "Snoozed (n)" filter on Home, off by default, same `Show snoozed` idea.
- Snooze also works on a todo and on an agent wait.

**Where one app wins**
- One snooze store for threads, PRs, issues, agent runs and todos. Today a Slack thread snoozed in Slack and a Linear issue snoozed in Linear are two lists in two apps.

**Desktop gain**
- Wake-up at the exact minute with the app closed to the tray (local timer, no server), measured as wake time vs due time, target under 1 s.

**Sindri today**
- Missing. No snooze in `src/Sindri/Aw.Sindri.Ui` (grep on origin/main finds it only in the 2026-10-07 design doc).

**Verdict:** take as is (Linear's `H` + typed date + wake on activity).

---

## 6. Peek the detail without leaving the list

**Best today**
- Linear peek: `Space` toggles a preview, holding `Space` shows it only while held; `J`/`K` moves while it is open. Works on issue views, the Inbox and project lists. Shows description, assignee, status, priority, cycle, labels, estimate, dates. https://linear.app/docs/peek.md source: docs
- Raycast List with detail: `isShowingDetail` puts a markdown detail pane on the right of the list. https://developers.raycast.com/api-reference/user-interface/list source: docs
- gh-dash (MIT): list of PRs/issues sections with a preview pane; "vim-style keyboard hotkeys". Preview-pane keys unconfirmed (docs page 404). https://github.com/dlvhdr/gh-dash source: docs
- Linear Cmd+K "quick look": right arrow adds a preview beside the command menu (snippet, unconfirmed on primary doc). source: docs

**What we take**
- `Space` toggles the side pane (Linear), hold-to-peek too. The pane takes 40% of width, the list narrows to a fixed set of columns.
- Pane shows what lets you decide: for a PR the diff summary + CI + the Slack thread about it; for a thread the last 5 messages with an inline reply box; for an issue the fields + linked PRs.
- `Enter` goes to Full. `Esc` closes the pane and returns the cursor to the same row.

**Where one app wins**
- Linear's peek shows the issue only. Ours shows the issue plus the PR, its CI and the thread, because they are linked objects.

**Desktop gain**
- Peek content from the local store: pane painted within 50 ms of `Space`, measured keydown to paint, no spinner.

**Sindri today**
- Missing as a peek. `Enter` opens a sheet (`Views/NeedsYou/NeedsYouSheet.axaml`, `DecisionSheet.axaml`), which covers the list. Peek is in #2978's grammar as `Space`, not built.

**Verdict:** take as is (Linear's Space-peek), change the pane contents.

---

## 7. One key map across every list

**Best today**
- Linear: `G` then `I` inbox, `G` then `T` triage, `J`/`K` or arrows move, `U` read, `H` snooze, `Backspace` delete, `Cmd/Ctrl+F` list search. https://linear.app/docs/inbox.md source: docs
- Octobox: Gmail-style `j`/`k`, `r`/`.` refresh, `a` select all, `?` help. https://github.com/octobox/octobox source: docs
- Superhuman: `Cmd+K` shows the key next to each action, so users learn keys while using the palette (snippet, vendor). https://help.superhuman.com/article/478-superhuman-command (404 to my fetcher) source: docs
- Universal Inbox: `?` shows shortcuts, `Esc` closes, `Arrow` keys move. https://universal-inbox-doc.pages.dev/misc/keyboard_shortcuts.html source: docs

**What we take**
- Map from #2978: `j/k` move, `/` filter, `Space` peek, `Enter` open, `Esc` back, `c` create, `.` actions, `Ctrl+K` palette, `?` shows the map. Add `g`+letter go-to (Linear) and `e` done, `h` snooze.
- The palette shows each action's key right-aligned (Raycast, Superhuman). Users learn the map from the palette.
- Same keys, same handler: one `RowKeys` component, no page-specific copies.

**Where one app wins**
- Linear's keys only work in Linear; Slack's in Slack. One app gives one `j/k/e/h` everywhere.

**Desktop gain**
- Key to focus change under 16 ms (one frame) on a 10k-row list, measured in the real app.

**Sindri today**
- Partial and split. Needs-you has J/K/A/X/Y/Enter/Esc (`NeedsYouKey.cs`); Agents has its own (`AgentsPageViewModel.Keys.cs`), Linear Work its own (`LinearWorkViewModel.Keys.cs`); a `RowKeys` helper exists but other lists don't use it yet (#2951); Projects has no J/K/Home/End (#2966). `ShortcutsSheet` exists (`Views/Palette/ShortcutsSheet.axaml`).

**Verdict:** take and change (finish `RowKeys` adoption: #2951, #2966).

---

## 8. Density: rows per screen, comfortable vs compact

**Best today**
- Linear: list rows are dense; no density setting is documented for the inbox (display options are Show snoozed, Unread first, grouping). https://linear.app/docs/inbox.md . Image: https://webassets.linear.app/images/ornj730p/production/2decf1f92f20b31e8a917d8e8c8c7e819ea1d8c3-1324x897.png source: docs
- Gmail: three densities, Default / Comfortable / Compact, in Settings (third-party summaries; Google's own page didn't load; which is default is disputed). Compact "stays dense no matter what size your window is" (2011 Gmail blog). https://gmail.googleblog.com/2011/11/changing-information-density-in-gmails.html source: docs
- Superhuman: single dense list, one line per thread (vendor screenshots; no row-height number confirmed). source: docs
- aerc / Mutt: one line per message, keyboard only; aerc binds `j`/`k` for next/previous message (aerc tutorial man page). Archive/delete keys unconfirmed. https://man.archlinux.org/man/aerc-binds.5.en source: docs

**What we take**
- Row 32 px default, 28 px compact (our tokens already carry 28/32/36/40: `Theme/Tokens.axaml:229-233`). Two user steps only: comfortable 36, compact 28 (#2978 says compact default).
- Fixed columns: state glyph, source glyph, title (flex), 3 fact columns, age, who-it-waits-on. 25-30 rows on a 1080p window.
- Setting lives in the palette ("Density: compact / comfortable") and in Settings.
- Nothing in a row wraps. Truncate with ellipsis, full text in peek.

**Where one app wins**
- One density token applies to every list, so Projects, Messages and Reviews match. Gmail's density is Gmail-only.

**Desktop gain**
- Virtualized list: 10k rows scrolling at 60 fps, memory under 200 MB; measured with a seeded 10k-row fixture in the real app.

**Sindri today**
- Missing as a setting. Home today is panel cards with a big count and large padding (`HomePage.axaml`: `NeedsYouNumber` display text, `MinHeight=48` setup line); #2908 empty band, #2780 "boxes before content".

**Verdict:** take and change (row tokens exist; build the two-step setting).

---

## 9. "Since you last looked" brief

**Best today**
- Slack recaps: a daily AI digest for channels you choose, opened from the sidebar "Recap" item; read messages are excluded on refresh; Business+ and Enterprise+ (vendor). Unread pill gets a "Summarize" button (vendor). https://slack.com/help/articles/25076892548883 source: docs
- Linear Pulse / since-you-last-looked: not confirmed from primary source. (Sindri already has its own Linear one: #2536.) source: docs
- Things Today: a daily list of what starts today plus calendar events at the top; "review it quickly at the start of the day". https://culturedcode.com/things/support/articles/4001304/ source: docs
- Superhuman Inbox Reboot: name seen in help center; page 403, content unconfirmed. source: docs

**What we take**
- A 5-10 line brief at the top of Home, written by an agent, each line one fact + one link ("PR #412 merged; CI red on main since 09:10"), covering only changes since the last Home close time.
- Each line is an ordinary row: it can be done, snoozed, peeked.
- Slack's rule "already-read items are dropped on refresh" copied.

**Where one app wins**
- Slack's recap only knows channels. Ours covers PRs, issues, agent runs and CI from one ledger, with exact event ids, so each line links to the object.

**Desktop gain**
- Brief built from the local ledger since a stored cursor (event number), shown at first paint; no waiting on a model for the lines that are plain facts (counts, state changes). Model only writes the summary line. Target: facts visible under 100 ms, summary streamed in.

**Sindri today**
- Missing on Home. Linear page has a since-you-last-looked wall of links (#2536). Ledger has the event stream the brief would read.

**Verdict:** take and change (Slack's recap idea, built from ledger events).

---

## 10. Notifications that don't spam

**Best today**
- Linear: you can't choose which notifications enter the Inbox; delivery to other channels is set in notification settings. Priority tab separates urgent from rest. https://linear.app/docs/inbox.md , https://linear.app/changelog/2026-09-03-priority-inbox source: docs
- GitHub: Unsubscribe removes the item and re-subscribes you only on @mention, team @mention or review request; 15 custom filters. https://docs.github.com/en/subscriptions-and-notifications/how-tos/viewing-and-triaging-notifications/managing-notifications-from-your-inbox , https://docs.github.com/en/enterprise-cloud@latest/subscriptions-and-notifications/reference/inbox-filters source: docs
- Universal Inbox: unsubscribe silences; re-subscribed if directly pinged; Slack only notifies on a chosen emoji reaction or @mention. https://universal-inbox-doc.pages.dev/how/actions/github source: docs
- Huly: choose which update kinds notify and how (inbox, email, push). https://docs.huly.io/communication/notifications/ source: docs

**What we take**
- Home only gets rows that need the owner (reason in: mentioned, assigned, review requested, blocked agent, failed run). Everything else is one key away in its source page.
- Unsubscribe (`u`) on a row, with GitHub's re-entry rule: back only on direct ping.
- OS notification only for Priority rows, one per row, never repeated; badge count = Priority unread only.
- A "mute for this thread/PR" key.

**Where one app wins**
- One mute applies to the thread, the PR and the issue about it together. Per-app muting leaves the other two pinging.

**Desktop gain**
- Native tray badge and OS toast with the app closed, drawn from local state; no push server.

**Sindri today**
- Partial. Tray exists (S1b shell). Needs-you only holds owner-needed items by design (`NeedsYouKinds.cs`). No unsubscribe/mute, no OS toast rule I could find on main.

**Verdict:** take and change (GitHub's re-entry rule, Priority-only toast).

---

## 11. Command palette for every action

**Best today**
- Raycast: one root search for apps, commands and extensions; `Cmd+K` on any item for its actions; fuzzy; shortcuts shown. https://manual.raycast.com/action-panel.md source: docs
- Linear: `Cmd/Ctrl+K` command menu with actions for the current view or selection (2019 changelog: contextual). https://linear.app/changelog/2019-10-07-contextual-command-menu . Per-action key display unconfirmed. source: docs
- Superhuman: `Cmd+K` finds any action and shows its letter (snippet, vendor). source: docs
- Zed: actions via palette; threads sidebar opened with `cmd-alt-j` (docs). https://zed.dev/docs/ai/parallel-agents source: docs

**What we take**
- Palette lists: go-to targets, actions for the selected row (top), global actions, and objects (search by title/id). Keys shown right.
- Context first: with a row selected, its actions come first (Raycast/Linear).
- Every button in the UI has a palette entry. A UI action with no palette entry is a bug.

**Where one app wins**
- One palette covers Slack, Linear, PR and agent actions. Raycast needs one extension per app and each is a thin re-implementation.

**Desktop gain**
- Open and first results under 50 ms, filter per keystroke under 16 ms over 50k entries (local index).

**Sindri today**
- Exists, incomplete. `Views/Palette/CommandPalette.axaml` (up/down/Enter/Esc), `ViewModels/Palette/CommandPaletteViewModel`. Missing entries: workflows (#2957), Projects (#2964).

**Verdict:** take and change (finish coverage; add contextual row actions + keys shown).

---

## 12. Many tools in one shell without losing each one's depth

**Best today**
- Raycast: one shell, extensions render in the same list/detail grammar. https://developers.raycast.com/api-reference/user-interface/list source: docs
- Zed: editor, terminal, agent threads in one window; Threads Sidebar groups threads by project; Terminal Threads show CLI agents (Claude, Codex, Amp) as sidebar entries. https://zed.dev/docs/ai/parallel-agents , https://zed.dev/blog/terminal-threads source: docs
- Huly (open source): issues, chat, docs in one app with one inbox. https://docs.huly.io/communication/inbox/ source: docs
- Height: tried this (tasks + chat); shut down 2025-09-24 (sources agree on the date; secondary). Lesson is in the verdict, not a feature to copy. https://alternativeto.net/news/2025/3/height-project-management-tool-to-shut-down-by-september-2025 source: docs
- Arc: not looked up; unconfirmed. source: docs

**What we take**
- Home is for deciding; sources are for working (#2978). Every source page uses the same row/peek/full grammar so a switch costs nothing.
- Zed's trick: agent sessions appear as ordinary list entries beside everything else, not in a separate tool.

**Where one app wins**
- This is the point: cross-links (thread -> PR -> issue -> run) are first-class. Huly links within its own objects only; it cannot hold your real Slack or GitHub.

**Desktop gain**
- Switch between source pages without reload: under 100 ms to first painted row from warm local store.

**Sindri today**
- Partial. Pages exist for Home, Work/Linear, Agents, Jobs, Context map, Palette. Grammar not shared yet (#2951, #2966).

**Verdict:** take and change (grammar everywhere; skip building a second chat client first, since Height closed with that scope, which is my inference, not a stated cause).

---

## 13. Home with a small view later (mobile)

**Best today**
- GitHub mobile: swipe to done/save, Focused filter, review PR diffs with approve/request changes. https://github.blog/news-insights/product-news/github-for-mobile-is-now-available , https://github.blog/changelog/2024-10-14-whats-new-in-mobile-october-update/ (snippets) source: docs
- Superhuman mobile: Split Inbox Settings from the command menu (snippet). source: docs
- Universal Inbox: web app, responsive (unconfirmed). source: docs

**What we take**
- Mobile = Priority list only, row + peek + 3 quick actions (done, snooze, primary), swipe left done, swipe right snooze (GitHub). Nothing else.

**Where one app wins**
- One mobile inbox for all five sources, not five apps' push notifications.

**Desktop gain**
- Same Avalonia views, same local store design; row cost the same at phone width (no extra column logic).

**Sindri today**
- Missing (iOS/Android/browser heads are "later" in CLAUDE.md tech stack).

**Verdict:** skip for now, keep the row component narrow-safe (the Agents page already switches to three columns under a width: `AgentsPageViewModel.Layout.cs`).

---

## Closing table

| Problem | Best tool | Verdict | Sindri today (issue) |
|---|---|---|---|
| 1 See 5 sources in one list | Universal Inbox, Superhuman splits | take and change | Partial: Needs you + Todo only (#2827, #2782) |
| 2 Rank by what unblocks most | Linear Priority tab, GitHub `reason` | take and change | Missing (api ranking route per #2978) |
| 3 Act in place | Raycast Action Panel, Universal Inbox keys | take and change | Partial: A/X/Y on Needs you; no reply/merge/snooze/agent |
| 4 Done semantics, zero inbox | GitHub Done/Save, Octobox revive | take and change | Partial: Mark all seen only (#2921) |
| 5 Snooze / remind | Linear `H` | take as is | Missing |
| 6 Peek | Linear `Space` peek | take and change | Missing (Enter opens a sheet) |
| 7 One key map | Linear + Raycast shown keys | take and change | Partial (#2951, #2966) |
| 8 Density | Gmail 3 steps, Linear rows | take and change | Missing setting; tokens 28/32/36/40 (#2908, #2780) |
| 9 Since-you-last-looked brief | Slack Recap | take and change | Missing on Home (#2536 on Work) |
| 10 No spam | GitHub unsubscribe re-entry | take and change | Partial; no mute/toast rule |
| 11 Command palette | Raycast, Linear, Superhuman | take and change | Exists; gaps (#2957, #2964) |
| 12 Many tools, one shell | Zed threads, Raycast, Huly | take and change | Partial, grammar not shared |
| 13 Small view later | GitHub mobile | skip for now | Missing |

## Not solid (read before using)
- Superhuman: all key and behaviour claims are from search snippets; help pages blocked my fetcher. Re-check in the app before copying exact keys. The 100 ms rule is a vendor claim (https://blog.superhuman.com/superhuman-is-built-for-speed/); the "50-60 ms internal target" is a third-party claim.
- Ranking (problem 2): Linear and GitHub do not publish their rules. "Blocked-count" score is our own design, no prior art found.
- Density (problem 8): no primary source for Gmail's default or Superhuman's row height; aerc/Mutt keys unverified.
- Todoist overdue/Today: search snippets only (https://www.todoist.com/help/articles/plan-your-day-with-the-today-view-UVUXaiSs); not used above.
- Arc, Notion inbox, Slack's Today view: not researched.
- No screenshots captured; only Linear's image URLs listed in problems 8.

## What we take

1. One Home list with tabs (All, Reviews, Messages, Work, Agents) cycled by Tab, a source glyph on every row, and linked CI / thread facts on the PR row (#2978).
2. Space opens a side pane to peek at the row, Enter goes to full, Esc returns to the same row (#2978).
3. Snooze with `h`: typed date plus four presets, and the row returns early on new activity addressed to you (#3030).
4. One Done verb (`e`) on every row kind; a done row comes back only on new activity addressed to you (#3031).
5. One key map on every list through the shared `RowKeys` component (#2951).
6. Priority tab ranked by how many other items a row blocks, with the rule readable and editable (#2978).
7. A 5-10 line brief at the top of Home of what changed since you last looked, built from ledger events (#2536).
