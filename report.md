# Reviews frames, R8 (Part of #3177)

48 PNGs, `reviews-<name>.png`, 1440x900 unless the name says another width (1200, 1000, 1280 are that wide x 900). Spec: `docs/superpowers/specs/2026-11-12-sindri-reviews-page-design.md` at 3063055689; line numbers below are that file's. Built on `../grammar-2978/grammar.css` plus `reviews.css`. Regenerate with `python3 gen.py && ./shoot.sh`. Every frame has a "Screen reader" line in the footer. Text is ink-subtle (never ink-3), wires are #686870, one primary button per frame (a modal frame demotes the header's primary). The app is dark only (Tokens.axaml line 6), so `reviews-queue` and `reviews-a-1440` are the dark frames of the queue and the surface (guess 1). Each PNG was looked at after the last shoot.

| Frame | Spec line | What it shows |
|---|---|---|
| queue | 205-249 (5.1) | Queue sections with reason chips, glyph plus word, header "as of 09:41", filter |
| queue-peek-running / -failed / -absent / -stale | 245, 392 | Peek with the pre-review in each state, cost, checks, Review as the primary |
| queue-peek-ready | 244 | Peek of a ready PR: "Can merge" ready line, Merge menu |
| queue-partial | 382 (5.6 Queue error) | "List is from 09:12: GitHub search failed", Retry, last rows kept |
| queue-serve-down-cache / -nocache | 382 | Cached rows with "as of", and "Sindri is not running" with `sindri serve` in Details |
| queue-filtered-nothing | 382 | "No match for ..." and Clear filter |
| queue-first-run | 382, 474 | "Connect GitHub": token steps, keychain command, `sindri review connect` |
| toast-posted | 285-286 | After Posted: cursor on the next row, "Posted. Next: #3127" and Open (Enter) |
| toast-merged | 352 | "Merged #3109 into main", the row left Ready to merge, cursor on the queue row |
| a-1440, a-1200 | 252-258 | Layout A, the phase-1 build: folder-grouped file list above the diff, 420 px column: seven panels, Threads, Findings, draft counter |
| a-split-diff | 279 | Split diff (`g l`) in Layout A |
| walk-overview-1440 | 283 | Three-pane walk on its Overview step: summary and the seven panels |
| walk-1440 | 252-290 | Three-pane walk, tree 260, diff, threads 360 |
| walk-1200 | 285-291 | 1100 to 1439: tree 240, inline thread under its line, Findings and Draft chips. 1200 sits inside that range, so it is the inline form |
| walk-1280-popover | 291 | The same form with the Findings chip popover open |
| walk-1000 | 291 | Below 1100: tree as a drop-down (`[`), threads inline |
| since-review | 313-319 | "Changes since your review of a1b2c3d (3 commits)", changed files only, "your 6 comments", agent "addressed" proposal |
| tree-failing-50of300 | 387 | A file that couldn't be read, "showing 50 of 300", Load 50 more |
| tree-states | 387 | Failed file, 50 of 300, Order unavailable (Files mode), Ordering skeleton |
| threads-states | 390 | Empty, error, SND-REVIEW-019 inline on a thread |
| findings-states | 392, 365-376 | Stale (SND-REVIEW-016), partial (SND-REVIEW-024), "2 new" chip, empty and failed (SND-REVIEW-017) |
| composer | 280, 407 | Composer under line 41, Add to draft (Ctrl+Enter) |
| composer-suggestion | 408 | `s` composer with a suggestion block, cursor line only |
| submit-normal | 321-352 | Submit sheet, three verdicts, Comment focused |
| submit-empty | 344, 394 | Comment disabled with its reason |
| submit-offline | 348, 394 | All verdicts disabled, SND-REVIEW-021 |
| submit-flagged | 346 | Flagged comments with Keep / Edit / Remove, verdicts off |
| submit-discard-undo | 346 | After Discard draft: "No draft", Undo toast with the 5 s bar, viewed kept |
| submit-head-moved | 350 (5.4 last bullet), 395 | HeadMoved inline in the sheet, Reload, SND-REVIEW-011 |
| head-moved-banner | 284 | Banner on the surface with Reload and the kept/outdated/reset counts |
| merge-picker / merge-merging / merge-conflict | 352 | Picker with the repo default preselected; "Merging..." with buttons off; conflict reason (SND-REVIEW-018), picker stays open |
| palette | 54, 401-402 | Ctrl+K in a review: matching files and hunks |
| palette-no-match | 398 | "No match for ..." and Clear |
| reviewer-picker | 352, 398 | Search field focused, names toggle, own Request button |
| reviewer-states | 398 | Loading, no match, couldn't load, Requesting... |
| focus-order | 352, 410 | Numbered stops for merge picker, reviewer picker, the surface (F6) |
| focus-rings | 280, 410 | Ring on a row, a file, a diff line, buttons and toggles |
| ready-line | 244 | The ready line on a row, in the peek, in the header |
| states-hover-pressed | 667 | Rest, hover, pressed, focus, disabled for every control |
| motion | 667, 417 | 120 ms toast, fold, sheet; scroll to anchor; spinner |
| reduced-motion | 417, 667 | The same with reduced motion: nothing animates |


Round 2 (ideas FIX on bc98aa05b7) changed every Layout A frame (Files mode, real folders, the thread inline under HoldService.cs:41), all error codes (Details), the queue footers, the merge picker (primary Merge, merging dimmed, conflict wording), the sheets (no bare c, no suggestion row, focus rings), the reviewer picker footer, the palette and a new palette no-match frame. The pressed and hover sheet is round 1b's, unchanged.

## UX pass fold-in (meta's 8 MUSTs, spec 8a6d74c200)
New or changed frames, by MUST (see the comment on #3177 for images):
- MUST 1: queue-first-run, first-run-connecting, first-run-refused, first-run-org-approval, first-run-store-failed, watch-repos, watch-repos-ticked, watch-repos-none, queue-serve-down-nocache, start-sindri-starting, start-sindri-failed.
- MUST 2: queue-loading, queue-empty-some, queue-empty-all, queue-empty-stale, queue-dot-menu, queue-dot-menu-running, queue-ctrl-enter-menu, diff-loading, diff-error, diff-partial, offline-surface, offline-surface-uncached, thread-sending, thread-send-failed, thread-expired, since-loading, since-empty, since-error, callers-states, draft-save-failed, submit-failed-010/012/013/014, submit-checking, submit-not-posted, merge-checking, merge-not-merged, read-only, read-only-sheet.
- MUST 3: submit-flagged, submit-flagged-two, submit-empty. MUST 4: toast-posted. MUST 5: queue footers, queue-done-toast, queue-snooze-picker, queue-snoozed-toggle, queue-back.
- MUST 6: every #3120 surface frame (counts.md has the spot list). MUST 7: queue-peek-ready, toast-merged, reviewer-picker, submit-offline. MUST 8: queue, a-1440, walk-1440 and the other opening frames.
- Picks: a-300files, ready-line, thread-focused-footer, Resolve chip on every thread. New mid-walk frames: a-1440-walk, walk-1200-thread.
