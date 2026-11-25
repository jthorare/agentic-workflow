# Prior art: Reviews

This is the prior-art ledger for the Reviews surface (rule: #2989). It lists 13 daily review jobs, who does each best today, what we copy, and where Sindri stands on origin/main.
Sources are the products' own docs, READMEs and changelogs, fetched 2026-11-12. "Vendor claim" marks marketing numbers. "Unconfirmed" marks anything I could not read in a primary source.
Surface job: the owner reviews every PR that needs them, in an order that makes sense, from one app, with every action landing on GitHub (direction: #2980; grammar: #2978).

Licence notes: difftastic MIT, delta MIT, octorus MIT, tuicr MIT, ghui/phui MIT. Everything else here is closed or hosted, so we copy the interaction, not code.

---

## 1. Know which PRs wait on me, and why

**Best today**
- Gerrit attention set. Per change, Gerrit keeps the list of users whose turn it is, with an arrow icon before the bolded name, and the dashboard's top section "Your turn" lists exactly those changes. It records why and when you were added. Adding a reviewer adds them; replying removes you. Bots never enter it. [docs](https://gerrit-review.googlesource.com/Documentation/user-attention-set.html) source: docs
- Reviewable "Waiting on". A pointing-hand marks who moves the review forward: people in unreplied discussions, people whose last "reviewed" mark is stale on a file, else the requested reviewers. [docs](https://docs.reviewable.io/reviews) source: docs
- Graphite inbox. Six default sections: Needs your review, Approved, Returned to you, Merging and recently merged, Drafts, Waiting for review. Custom sections with filters; sections can be shared as a link. [docs](https://graphite.com/docs/use-pr-inbox.md) source: docs
- CodeRabbit Triage. One cross-repo queue; the card's "next action" says whether you move (`Update PR`, `Respond to feedback`, `Merge`) or someone else does (`Ping in Slack`, `Find reviewer`). [docs](https://docs.coderabbit.ai/triage.md) source: docs

**What we take**
- Gerrit's rule: the set holds the people who must act, with a reason string per person ("new commits since your review", "unreplied thread from @x"). Show the reason in the row, not just the badge.
- Graphite's section list as the starting grouping: Needs your review / Returned to you / Waiting on others / Approved / Drafts. Filters are saved sections you can share by link.
- Bots out of the set (Gerrit): an agent's comment never puts a PR on the owner's list by itself.

**Where one app wins**
GitHub, Gerrit and Graphite only see PR events. Sindri also knows the agent session that wrote the PR is blocked, the Slack thread that asked for review, and the Linear issue's state. "Waiting on you" can include "the babysitter fixed your last 3 comments, ready for a look".

**Desktop gain**
List first paint under 100 ms from the local PR store, no spinner, and a new review request showing within one poll tick. Measure: time from app focus to first row painted with 200 stored PRs.

**Sindri today**
Done in part. `ReviewListBuilder` puts a PR in "Waiting on you" when the owner is a requested reviewer or reviewed an older commit ("reviewed, N new commits", spec §6.1, D21). Missing: a reason for every cause (unreplied thread, CI went red on my PR), team-requested reviews (spec says "Reviews asked of a team don't show here yet"), and saved/shared sections. Open bugs on the list page: #2791, #2555, #2936.

**Verdict**: take and change. Keep our two groups, add Gerrit-style reason text per row.

---

## 2. Rank the queue by what unblocks someone

**Best today**
- CodeRabbit Triage. Priority and next action are separate on purpose: a red trivial dependency bump must not outrank a customer fix waiting for a reviewer. Precedence: manual override, model verdict with a short reason, admin rule, base score (issue severity, linked Jira/Linear priority, more signals). [docs](https://docs.coderabbit.ai/triage/prioritization.md) source: docs
- Graphite inbox sections order work by state, not score. [docs](https://graphite.com/docs/use-pr-inbox.md) source: docs
- Gerrit "Your turn" is a flat list, unranked. [docs](https://gerrit-review.googlesource.com/Documentation/user-attention-set.html) source: docs
- GitHub's own review-requested list (sort by age/updated only). [GitHub docs, filtering PRs](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/filtering-and-searching-issues-and-pull-requests) (page not re-read here; unconfirmed beyond sort options). source: docs

**What we take**
- Two columns, never merged: priority (how much it is worth) and next action (what moves it, and whose move). A manual override key on the row.
- Rank signal list for us: how many people/agents are blocked on the PR, linked issue priority, size, age waiting. Show the top reason in one short phrase.

**Where one app wins**
Priority can use things Triage cannot see: "3 other PRs stack on this one", "an agent session is idle waiting for this merge", "this blocks the workflow run".

**Desktop gain**
Re-rank 500 PRs in under 16 ms after any event, so the order is never stale on screen. Measure: frame time on a 500-row list while events stream in.

**Sindri today**
Missing. The list orders by group, then newest activity (spec §6.1). The direction issue asks for "what unblocks someone" ranking (#2980) and the unified inbox ranking route is on api (#2978).

**Verdict**: take and change (Triage's priority/next-action split, our own signals).

---

## 3. Read a diff in the order the change makes sense, not file order

**Best today**
- CodeRabbit Change Stack. The PR becomes numbered layers ("a contract and the code that implements it and the tests that cover it belong in one layer, not three"), foundations first. Left rail toggles Layers/Files; each layer has Files and Diagrams tabs (Mermaid, kind picked per change); summaries and findings anchor to line ranges. Preview feature, model-generated, can differ between runs. [overview](https://docs.coderabbit.ai/pr-reviews/change-stack.md), [navigation](https://docs.coderabbit.ai/change-stack/navigation.md) source: docs
- Graphite PR page: file tree beside the diff, `F` toggles it and the table of contents on the left; `.gitattributes linguist-generated` hides generated files. Order is the tree, not the story. [docs](https://graphite.com/docs/pr-page-overview.md) source: docs
- octorus: split view, file list 35% and diff 65%, file-tree toggle, tree-sitter symbol outline. [README](https://github.com/ushironoko/octorus) source: docs
- Zed multibuffer review of agent edits: all changed hunks of many files in one scroll tab. [docs](https://zed.dev/docs/ai/agent-panel) source: docs

**What we take**
- Layers as the unit, numbered, with the dependency between layers stored, not inferred from the number. Keys from Change Stack: `J/K` next/previous layer, `L/H` next/previous file, `U` unified/split, `Z` focus mode (hide rails), `[` and `]` collapse left/right panels.
- Provenance rule: say it is generated, pin it to a head sha, keep older orderings (Change Stack keeps earlier snapshots).
- Always offer the Files mode; ordering is a view, not the only view.

**Where one app wins**
CodeRabbit infers layers with a model from the diff alone. We have the context map: concept, band (entry, core, data, tests) and callers from the code index (`diff-scope`, context lane plan on #2980). The order can be derived from real edges and be the same each time, with the model only naming the layers.

**Desktop gain**
Order computed from the local code index: p95 under 2 s for a 50-file diff (the budget set on #2980), then cached per head sha so reopening is under 100 ms.

**Sindri today**
Partial. The diff is grouped and folded by profile rules (`PrReviewGroup`, spec §7.1), keys `j/k` file, `n/p` hunk, `c` fix task, `o` fold (`Labels.ReviewPage.cs:111`). No concept grouping or layer order yet; `diff-scope` is planned on context (#2980) behind #2974.

**Verdict**: take and change. Change Stack's layer rail and keys, built from our graph instead of a model guess.

---

## 4. Mark files reviewed and not lose my place in a big PR

**Best today**
- GitHub "Viewed" checkbox per file, collapses the file, progress bar in the PR header. [docs](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/reviewing-changes-in-pull-requests/reviewing-proposed-changes-in-a-pull-request) source: docs
- Reviewable reviewed-state per file per reviewer per revision. A purple "Show Diffs to Review" button sets diff bounds on all files to the range you still owe; sufficiently reviewed files hide their chip in the file matrix. [docs](https://docs.reviewable.io/files) source: docs
- tuicr: tracking at file or hunk level, saved across sessions as a local review session. [README](https://github.com/agavra/tuicr) source: docs
- CodeRabbit Change Stack: viewed progress is kept per snapshot, and on GitHub it ticks GitHub's own Viewed box, so teammates see it. [docs](https://docs.coderabbit.ai/change-stack/navigation.md) source: docs

**What we take**
- `v` toggles viewed on the focused file, collapses it, header shows "12/41 viewed". Hunk-level marks as an option (tuicr).
- Write the mark through to GitHub's Viewed state so github.com agrees (Change Stack does this).
- When the head moves, a file whose diff did not change since you marked it stays viewed; a changed one flips back to "changed since viewed" (Reviewable's per-revision state).

**Where one app wins**
Viewed marks can also feed Home ("PR #812: 31 of 40 files left") and the agent: the pre-review can skip the files you have already read.

**Desktop gain**
Mark and collapse under one frame (16 ms), persisted to the local store so reopening a 300-file PR lands on the first unviewed file with no network. Measure: key press to repaint; reopen-to-first-unviewed-file time.

**Sindri today**
Missing in code on origin/main (a grep of `src/Sindri/Aw.Sindri.Ui` for viewed state finds nothing). The spec plans "a file list with viewed marks" (§15, Ph6) and ticks for test-plan rows exist in the draft design. GitHub write-through is not built.

**Verdict**: take as is, plus write-through to GitHub.

---

## 5. See only what changed since my last look

**Best today**
- Reviewable. Every push is a revision; the file matrix has a column per revision and you set diff bounds per file, so "revision 2 vs 4" is one click. Marking reviewed is tied to the bounds. [docs](https://docs.reviewable.io/files) source: docs
- Gerrit. The change screen compares any pair of patch sets (Patchset A and B); the diff of two patch sets shows only author changes. [docs](https://gerrit-review.googlesource.com/Documentation/user-review-ui.html) source: docs
- Phorge/Phabricator Differential. Comments port forward to the new diff and show faded; "diff of diffs" marks rebase-only changes with a separate colour so they are not read as author edits. Secondary source only (the Phorge site blocked fetches): [user guide](https://we.phorge.it/book/phorge/article/differential_inlines/) and a search summary. source: docs
- tuicr. On reopening a PR you reviewed, it preselects commits newer than your last submitted review. [README](https://github.com/agavra/tuicr) source: docs
- GitHub: a "since your last review" compare exists in the UI; I could not confirm its exact screen from the docs page. Unconfirmed. source: docs

**What we take**
- Default view on reopening is "since your last review" (your review's commit to head), computed per file, with one key to flip to the whole PR. This is Reviewable's "Show diffs to review" as the default, not a button.
- Rebase-only hunks drawn in a separate muted colour (Differential) so a force-push after a rebase is not a wall of red.
- Comments follow their line to the new revision and show "outdated" when the line is gone (spec §8.1 already says this for drafts).

**Where one app wins**
We can pair it with the thread list: "your 6 comments: 4 changed lines near the thread, 2 untouched" and propose addressed / not addressed, which no server-side tool does with the agent run that fixed it.

**Desktop gain**
Both revisions are in the local store, so flipping between "since last review" and "all" takes under 100 ms for a 50-file PR with no fetch. Measure: toggle-to-repaint.

**Sindri today**
Partial and thin. The list shows "reviewed, N new commits" (`ReviewListBuilder.cs:25-36`, `ReviewedAt` with `NewCommits`); the page does not diff from the reviewed commit. Direction item 3 on #2980 plans it (context lane for scope, api lane for per-thread "addressed?").

**Verdict**: take and change.

---

## 6. Read a structural / semantic diff

**Best today**
- difftastic: compares syntax trees, so reformatting and moved nesting do not show as changes; 30+ languages via tree-sitter; falls back to text diff with word highlighting for unknown extensions; `--graph-limit` falls back when the graph gets too big. MIT. [manual](https://difftastic.wilfred.me.uk/introduction.html) source: docs
- delta: word-level highlight by Levenshtein edit inference, side-by-side with wrapping, `n`/`N` jump between files. MIT. [README](https://github.com/dandavison/delta) source: docs
- GitHub: line diff with whitespace toggle only (`?w=1`; not re-read here, unconfirmed beyond the toggle in the parity list on #2980). source: docs
- Zed/octorus: syntax-highlighted line diffs with tree-sitter highlighting, not structural. [octorus README](https://github.com/ushironoko/octorus) source: docs

**What we take**
- Two layers: (1) word-level intra-line highlight always on (delta); (2) structural mode per language where we have a parser, with a visible "text diff (no parser)" label when it falls back. Keep difftastic's size cutoff and show the reason.
- Moved block shown as moved, with the pair linked, not as delete plus add.
- Licence: MIT, we can read the code; we reimplement in C# (the repo's port rule), tree-sitter grammars are the shared input.

**Where one app wins**
The structural diff can name the changed symbol and link its callers (problem 11); a pager cannot.

**Desktop gain**
Structural diff computed off-thread for the focused file first: first visible file under 100 ms, the rest streaming while you read. Measure: time to first painted structural file for a 50-file PR.

**Sindri today**
Missing. The diff is the patch GitHub returns, parsed by `ReviewPatch.Hunks` (`ReviewDiffFileViewModel.cs:18`). C# has real symbols in the code index (`CSharpParser`); TS has graph edges only (#2983).

**Verdict**: take and change (word-level first, structural per language later).

---

## 7. Comment, suggest, batch and resolve threads

**Best today**
- Graphite. Click a line number, drag for a range, comment on changed and unchanged lines; "Add to review" puts it in a private pending batch; `Cmd+Enter` posts a single comment now. [docs](https://graphite.com/docs/pr-page-overview.md) source: docs
- Reviewable. Each person has a disposition per discussion (e.g. "Discussing" means "I won't resolve, others may"); a discussion resolves by rule from dispositions; drafts, dispositions and review marks all publish together. [discussions](https://docs.reviewable.io/discussions), [blog](https://www.reviewable.io/blog/discussion-semantics-overhaul) source: docs
- octorus. Inline comments and suggestions go to the pending review on GitHub, a header counts drafts, `Shift+Enter` selects a multi-line range, comments can be resolved/reopened; text box has readline keys. [README](https://github.com/ushironoko/octorus) source: docs
- ghui/phui. `enter` opens a commented line or starts a comment, `v` starts a range, `n/p` jumps between threads. [README](https://github.com/kitlangton/ghui) source: docs
- GitHub suggested changes: apply one, or batch many into one commit. [docs](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/reviewing-changes-in-pull-requests/incorporating-feedback-in-your-pull-request) source: docs

**What we take**
- `c` comment on line or range, `s` suggestion block prefilled with the selected lines, Ctrl+Enter to add to the pending batch, a draft counter in the header, `n/p` over threads.
- Pending batch lives on GitHub's side as a real pending review, so closing the app loses nothing (octorus).
- Skip Reviewable's per-person dispositions: GitHub has no such field, and write-through to GitHub is the contract.

**Where one app wins**
"Hand the fix for my own comment to an agent" from the thread, with the PR, file and line already attached; and the thread shows the agent run that answered it.

**Desktop gain**
Composer opens in under one frame, drafts written to disk as you type so a crash loses 0 characters. Measure: key to composer focus; kill -9 test with a half-typed comment.

**Sindri today**
Partial. Fix-task send exists (spec §8.2; `FixThreadPoster.cs` uses `SubmitReview` with a `sindri.fix:` marker). The composer, drafts and suggestion blocks are "release 2" (spec §8.1); `SubmitReview` is already on `IPullRequests`. Resolve/unresolve and reactions: missing.

**Verdict**: take as is (batch + range + suggestion), skip dispositions.

---

## 8. Verify an agent's pre-review findings fast

**Best today**
- CodeRabbit. Each finding carries four separate labels: type (nitpick / potential issue / refactor), severity (critical to trivial), category (6 areas) and effort-versus-reward; "Needs your attention" splits what blocks merge from what does not. Findings follow across review runs. [docs](https://docs.coderabbit.ai/change-stack/findings.md) source: docs
- Greptile. Reviews against the whole repo; inline comments carry P0/P1/P2 badges and the PR gets a 0 to 5 confidence score (vendor claims); a `/greploop` skill iterates to 5/5 with zero unresolved comments. [docs](https://www.greptile.com/docs/code-review/first-pr-review.md) source: docs
- GitHub Copilot review. "Fix with Copilot" on a review comment makes the coding agent open a PR with the fix; suggestions apply in a few clicks, batchable. [docs](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/request-a-code-review/use-code-review) source: docs
- octorus AI Rally. A reviewer agent and a fixer agent loop; `--review-only` makes the fixer produce a written proposal under a read-only tool list (`Read,Glob,Grep`), re-reviewed until approved, posted `final` / `each` / `none`. [README](https://github.com/ushironoko/octorus) source: docs
- Zed. Agent edits open as a multibuffer; keep or reject per hunk or all; checkpoints restore. [docs](https://zed.dev/docs/ai/agent-panel) source: docs

**What we take**
- Every finding is one row with: severity, a line anchor, one line of why, and three keys: accept as pending comment, edit, drop (Zed's keep/reject per item). `Shift+J/K` steps findings in a panel (Change Stack).
- Keep CodeRabbit's separation of severity from effort; skip category as a filter.
- Show how the finding was checked: the evidence line (grep hit, caller list) beside it, so verifying is reading one line, not re-deriving.
- Read-only agent for the pre-review (octorus, and our own sandbox rule). Nothing posts without the owner.

**Where one app wins**
Greptile/CodeRabbit run on their servers and cannot start a fix in your checkout, name the session that wrote the code, or compare with the babysitter's earlier findings. We can mark "this finding repeats one you dismissed on #812".

**Desktop gain**
Jump from a finding to its line and open its evidence in under 100 ms, because diff and index are local. Measure: key press to scrolled, highlighted line. Also: acceptance rate (accepted / shown) as the quality number for the agent, tracked locally.

**Sindri today**
Missing as a product; planned as the `pr-pre-review` template (engine plan on #2980). Blocked by #2988 (no `review_requested` event) and #2985 (graph `run_agent` refused). Agent `suggest` into the page is in spec §8.4 (release 2).

**Verdict**: take and change. Per-item accept/edit/drop, evidence line, owner-submit only.

---

## 9. See CI and checks next to the code they fail on

**Best today**
- Graphite PR page: Checks is an expandable list in the info panel that opens GitHub for logs. Nothing inline. [docs](https://graphite.com/docs/pr-page-overview.md) source: docs
- GitHub: checks box at the bottom of the conversation; annotations from actions can appear on diff lines (annotation behaviour not re-read here, unconfirmed). source: docs
- ghui/phui and octorus: Actions/CI status inside the TUI; octorus lists CI status and Git Ops. [phui](https://github.com/phall1/phui), [octorus](https://github.com/ushironoko/octorus) source: docs
- CodeRabbit: pre-merge checks in the walkthrough, merge readiness on the Overview. [docs](https://docs.coderabbit.ai/change-stack/findings.md) source: docs

**Nothing solid found for the exact job** "the failing test's log line shown under the hunk it blames". Every tool above links out for logs. This is an open lane for us, not a copy.

**What we take**
- Graphite's compact check list in the header; CodeRabbit's "blocks merge / does not block" split for required versus optional checks.

**Where one app wins**
Sindri already stores CI per head sha and the failing file/line from logs; show it as a gutter marker plus an expandable log excerpt on that hunk, and a "send to agent" key on it.

**Desktop gain**
Log excerpt for the failing check read from the local cache: open under 100 ms; full log search over 50 MB under 200 ms. Measure both.

**Sindri today**
Partial. `ci` and `requiredChecks` panels exist (spec §6.2, §7.3). Not inline in the diff. Missing: line-anchored log excerpts.

**Verdict**: ours to design; take Graphite's header list as the baseline.

---

## 10. Review stacked PRs

**Best today**
- Graphite. A Stack section on the PR page lists every PR stacked alongside; `S` opens the stack view. [docs](https://graphite.com/docs/pr-page-overview.md) source: docs
- Gerrit. Related changes (a relation chain) with per-link "outdated patch set" markers when an ancestor got a new patch set. [docs](https://gerrit-review.googlesource.com/Documentation/user-review-ui.html) source: docs
- Reviewable lists "Stacking tools" as a repository setting, so each PR shows only its own diff. [docs index](https://docs.reviewable.io/files) (setting named in the table of contents; behaviour not read, unconfirmed) source: docs
- Copilot: fixes applied "in a stacked pull request" when you mention `@copilot`. [changelog via search](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/request-a-code-review/use-code-review) source: docs

**What we take**
- Stack strip at the top of the PR: numbered chips with state glyph, current one highlighted, `[`/`]` moves up/down the stack. Gerrit's marker when a parent moved and this PR needs a rebase.
- Each PR shows only its own diff, with the parent's diff one key away.

**Where one app wins**
The stack can include agent PRs and the work items they came from, and the babysitter can restack. Merge order shown with CI state per link.

**Desktop gain**
Stack of 8 PRs diffed locally from two refs; switch between PRs under 100 ms when cached.

**Sindri today**
Missing in the review surface (no stack code in `Core/Reviews`). The babysitter fixture has a `stacked` variant (`scripts/forge-fixture.sh`), so the data case exists.

**Verdict**: take as is (Graphite's strip), plus restack action later.

---

## 11. Jump to context from a hunk: callers, the issue, the thread

**Best today**
- octorus. Tree-sitter symbol index with no LSP: `o` file outline, `s` fuzzy symbol search, `gd` go to definition, `Ctrl-o` jump back (stack of 100), and `i` module graph of imports/importers, each answer labelled `exact` or `approximate`. [README](https://github.com/ushironoko/octorus) source: docs
- CodeRabbit. Blast Radius view (dependencies, consumers, tests around the change) and a "Code Peek", all pinned to the snapshot; chat from a layer, file or range. [navigation](https://docs.coderabbit.ai/change-stack/navigation.md) source: docs
- Greptile. Reviews against the full repo graph instead of the diff (vendor claim). [docs](https://www.greptile.com/docs/code-review/first-pr-review.md) source: docs
- Graphite PR page lists related tasks from Linear and a deployment preview link. [docs](https://graphite.com/docs/pr-page-overview.md) source: docs

**What we take**
- The `exact | approximate` mark on every caller list (octorus), which matches the rule already set on #2980 (`complete | partial (barrels) | none (no parser)`).
- `gd`-style key on the identifier under the cursor, `Ctrl-o` back, a stack of positions.
- A "blast radius" tab per layer: callers, consumers, tests, ranked by distance.

**Where one app wins**
From the same hunk: the Linear issue, the Slack thread, the agent run and the context-map node. None of the tools above hold all of those; they stop at code.

**Desktop gain**
Callers of a changed symbol open in under 100 ms from the local code index; the 2 s p95 budget on #2980 covers whole-diff scope.

**Sindri today**
Partial. `context_query --diff` exists (#2980 context plan); `diff-scope` and caller lists are not built. TS edges stop at barrels (#2983). Issue chips from Linear links exist in the header (spec §6.2).

**Verdict**: take and change.

---

## 12. Approve and merge with confidence

**Best today**
- CodeRabbit. Overview shows a merge-readiness assessment next to the provider's own mergeable state; the merge control refuses a stale snapshot instead of merging something you have not seen. [snapshots](https://docs.coderabbit.ai/change-stack/snapshots.md) source: docs
- Triage rules. Merge ready changes, close stale PRs after notice, repair CI or feedback automatically. [docs](https://docs.coderabbit.ai/triage.md) source: docs
- Reviewable. Completion condition is scripted per repo (who must review which file), plus merge method settings. [docs index](https://docs.reviewable.io/files) source: docs
- Graphite review status shows "waiting on reviewers" or "ready to merge" and whether its agent found issues. [docs](https://graphite.com/docs/pr-page-overview.md) source: docs

**What we take**
- Refuse-on-stale: Approve and Merge compare the head you read with the live head and stop with "the PR moved to a1b2c3d; reload" (spec §8.3 `HeadMoved` already does this for posts).
- A "ready" line on the row: required checks green, no open threads, all files viewed, no pending moves. Show which one is missing.

**Where one app wins**
"Approve + merge when green" can run as an action from Home, and waits on the parent PR in a stack.

**Desktop gain**
Readiness recomputed locally on each event: under 50 ms from event to row updated.

**Sindri today**
Release 1 is read-only ("Approve on GitHub" link, spec §6.3/§8). `SubmitReview` with `HeadMoved` exists in `IPullRequests`. Merge methods, queue, auto-merge: missing.

**Verdict**: take as is (refuse-on-stale), build the ready line ourselves.

---

## 13. Review a 300-file PR without the UI choking, and drive it by keyboard

**Best today**
- octorus. States 1,000,000+ diff lines and 6,000+ files handled (vendor claim), tree-sitter highlighting, background indexing that never blocks browsing. [README](https://github.com/ushironoko/octorus) source: docs
- CodeRabbit Change Stack: search across file paths, diff text, source content, summaries and comments in the browser; capped at 200 ranked results; command palette on `Cmd/Ctrl+K` or `?` showing every key. [docs](https://docs.coderabbit.ai/change-stack/navigation.md) source: docs
- Reviewable: `?` lists bindings, bindings are customizable; per-file "single file mode" for huge diffs. [docs](https://docs.reviewable.io/reviews) source: docs
- Graphite: `Cmd+K` fuzzy PR search across title, description, author. [docs](https://graphite.com/docs/use-pr-inbox.md) source: docs
- GitHub's own cap: its API lists at most 3,000 files per PR (our spec says the page shows this), and patches over a size limit are left out. [keyboard shortcuts](https://docs.github.com/en/get-started/accessibility/keyboard-shortcuts) source: docs

**What we take**
- Virtualized diff (only visible lines built), file panels loaded on demand, `Cmd/Ctrl+K` palette that doubles as the key reference, `/` search with source filters (file, diff, comment), remappable keys.
- A "too large to show" file keeps its place in the list with one key to load it (spec §6.2 already holds >1 MiB patches back).

**Where one app wins**
Search can span the diff, the code index, linked issues and threads in one box.

**Desktop gain**
10,000 diff lines scrolling at 60 fps (frame time under 16.6 ms p95); open a 300-file PR to first painted file under 200 ms from cache; palette to result under 50 ms. Measure with the Avalonia frame recorder in the real app.

**Sindri today**
Partial. `j/k`, `n/p`, `o`, `c` exist in the diff; shortcuts sheet "Review page". No palette on the page, no search, no virtualization numbers on record. Open: #2792, #2793 (the page misses the UI bar).

**Verdict**: take as is (palette, `/` search, virtualization), measure ourselves against octorus's claim.

---

## Closing table

| Problem | Best tool | Verdict | Sindri today (issue) |
|---|---|---|---|
| 1 Which PRs wait on me, and why | Gerrit attention set | take and change | partial: waiting group + "N new commits"; no reasons, no team reviews (#2980, #2791) |
| 2 Rank the queue by what unblocks | CodeRabbit Triage | take and change | missing (#2978 ranking route) |
| 3 Read in change order | CodeRabbit Change Stack | take and change | partial: grouped diff, no layers (#2959, #2974, #2980) |
| 4 Viewed marks, keep my place | GitHub Viewed + Reviewable | take as is + write-through | missing in code (spec Ph6 plans it) |
| 5 Only what changed since my last look | Reviewable diff bounds | take and change | partial: list shows "N new commits" only (#2980) |
| 6 Structural / semantic diff | difftastic (MIT) | take and change | missing |
| 7 Comment, suggest, batch, resolve | Graphite / octorus | take as is, skip dispositions | partial: fix-task only; composer is release 2 |
| 8 Verify agent findings | CodeRabbit findings + octorus review-only | take and change | missing; blocked by #2988, #2985 |
| 9 CI at the failing line | nobody solid | ours to design | partial: ci panel, nothing inline |
| 10 Stacked PRs | Graphite stack view | take as is | missing in review surface |
| 11 Jump to callers / issue / thread | octorus symbols + CodeRabbit Blast Radius | take and change | partial: `context_query --diff`; callers missing (#2983) |
| 12 Approve and merge with confidence | CodeRabbit refuse-on-stale | take as is | read-only in release 1; `HeadMoved` exists |
| 13 Big PR, keyboard, search | octorus + Change Stack palette | take as is, measure | partial keys; no palette, no search (#2792, #2793) |

## Gaps in this research

- Phorge/Phabricator pages blocked my fetch (bot wall). Differential claims come from a search summary, marked secondary.
- GitHub's "changes since your last review" screen: unconfirmed. GitHub's check-annotation-on-diff behaviour: unconfirmed.
- Reviewable's stacking and completion-condition behaviour: named in its docs index, not read.
- No screenshots captured; links point at doc pages. The design lane still needs frames for the side-by-side.

## What we take

1. Read the diff in change order: numbered layers with stored dependencies, a Files mode always one key away (#2980).
2. Every row in the review list says why it is waiting on you, in one phrase, not just a badge (#2980).
3. Agent findings as one row each (severity, line, one-line why, evidence) with three keys: accept as pending comment, edit, drop (#2980).
4. Reopening a PR defaults to "since your last review", per file, with one key for the whole PR (#2980).
5. `v` marks a file viewed, written through to GitHub; a file unchanged since you marked it stays viewed after a push (#2980).
6. Comment, suggestion block and batch with `c`, `s`, Ctrl+Enter, kept as a real pending review on GitHub (#2980).
7. Approve and Merge refuse on a moved head, and a "ready" line on the row names what is missing (#2980).
