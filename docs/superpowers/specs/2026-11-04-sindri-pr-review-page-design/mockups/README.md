# PR review page mockups (Ph6, for the owner's pick)

Spec: `../../2026-11-04-sindri-pr-review-page-design.md` (§3, §6 the page and its states, §6.4 keyboard, §7 settings, §8 actions,
§15 Ph6). Static mockups, no production code. Colors, sizes and radii are the app's tokens (`src/Sindri/Aw.Sindri.Ui/Theme/Tokens.axaml`);
no new tokens. Made-up sample data (`acme/harbor`, doc `harbor-booking-tdd`).

`bash shots.sh` renders every PNG with headless Chrome (it runs `shots-list.sh`, `shots-review.sh` and `shots-sheets.sh`). Each HTML file
draws its screens from URL parameters listed in its header comment.

## The pick: the review page's layout

| | A: the spec as written | B: diff first, panels as a tabbed rail |
|---|---|---|
| 1440 | `screens/review-a-1440.png` (whole scroll: `review-a-1440-full.png`) | `screens/review-b-1440.png` (`review-b-1440-full.png`, other tabs: `review-b-1440-docmap.png`) |
| 1000 | `screens/review-a-1000.png` (`review-a-1000-full.png`) | `screens/review-b-1000.png` (`review-b-1000-full.png`, `review-b-1000-ci.png`) |

- **A**: the summary strip under the header, the 420 px panel column beside the diff at 1,280 px and wider; below that the panels
  stack above the diff, collapsed except required checks. Checks, doc map and CI read side by side; the visual order matches §6.4's
  focus order (panels, then diff).
- **B**: the diff on the left, the panels as a 380 px right rail whose tab bar replaces the summary strip (counts and state icons on
  each tab); "Covers Ph2, REQ-E3" moves into the header. The diff is wider and below 1,280 px only the open panel sits above it, so
  the scroll is much shorter. Lost: reading checks, doc map and CI together; §6.2's per-panel collapse becomes a remembered tab; what
  you see (diff left) and the keyboard order (panels first) don't match.

## Nine tabs at the minimum width (§3)

Nine full labels fit at every width; nothing shortens. Measured in `list.html` with Inter's widths, counting a number on five tabs
(`screens/list-bars.png`):

| Window | Needs | The bar's existing step-downs taken | Spare |
|---|---|---|---|
| 1440 | 1172 | none | 268 |
| 1100 / 1099 | 1048 | the ⌘K field folds to its magnifier | 52 / 51 |
| 1000 | 954 | also the health pill's age drops | 46 |
| 1000 at 130% text | 977 | also tight tab padding, brand word drops | 23 |

It's a model of the real bar, and the 130% margin is 23 px, so Ph7 adds a UI test that measures the bar at 1,000 px and 130%.
`list.html` draws the app's real segmented bar; `review.html` and `sheets.html` use the flatter Projects-mockup bar (same labels).

## Every other screen

| What | Files |
|---|---|
| Reviews list (Waiting on you first, then Yours; grouped by project, then repo) | `list-main-{1440,1100,1099,1000}.png` |
| List states: loading, empty, no login, error, before requested reviewers are read | `list-state-*.png` |
| Library errors sheet and its texts; the header with no docs | `list-sheet-*.png`, `list-header-empty-1440.png` |
| Settings → Library: folders, loading, empty, adding one | `list-settings-*.png` |
| Home's "3 PRs waiting on you" line | `list-home-1440.png` |
| Every panel loading, empty, errored | `review-a-panels-{loading,empty,error}-1440.png` |
| Diff states: reading, no files, error, offline from cache, the 3,000-file cap | `review-a-diff-*-1440.png` |
| Head moved banner; focus on the summary strip after Reload | `review-a-moved-1440.png`, `review-a-reload-focus-1440.png` |
| Whole page: merged, closed, not found, not served (`SND-REVIEW-004`), offline with no cache | `review-a-page-*-1440.png` |
| Fix-task sheet from a diff line, a check, a requirement | `sheet-fix-line-*.png`, `sheet-fix-check-1440.png`, `sheet-fix-req-1440.png` |
| Sending, the three outcomes, a contributor's PR, half done with Retry | `sheet-fix-{sending,o1,o2,o3,contrib,half}-*.png` |
| Release 2: composer (inline under the line), suggestion, confirm sheet, posting, `SND-REVIEW-010`..`013` | `sheet-composer*.png`, `sheet-confirm-*.png`, `sheet-posting-*.png`, `sheet-e01{0,1,2,3}-*.png` |

With a side sheet open at 1440 the page behind is 920 px, so it stacks (§15: the page's own width decides).

## Found while drawing (for the owner, then folded into the spec)

Wording the spec doesn't give; the mockups show a first draft of each:
1. The three fix-task outcomes (babysitter takes it, a live session takes it, waiting on the branch); §8.2 has only the half-done and
   contributor texts. `sheet-fix-o{1,2,3}`.
2. "PR not found", "offline with no cache", and the page before `review.login` is set (`SND-REVIEW-007`, not drawn).
3. The login prompt ("Set your GitHub login", one field, Save); the library header when a folder can't be read ("Library: 33 docs,
   1 folder can't be read"); renamed / deleted / binary file texts ("Renamed from <old>, 91% the same", "Deleted file: 61 lines
   removed. Contents hidden. Show", "Binary file, not shown here. Open on GitHub").

Decisions:
4. **Fix task from a check or a requirement**: there's no diff line to put the thread on. Drawn as a line comment on the first changed
   line; options: that, the review body only, or no post (item only).
5. **No `review.credential`** (`SND-REVIEW-012`): disable Post and say why when the confirm sheet opens, instead of failing on Post.
6. **Approve on your own PR**: hidden or shown disabled with a reason (§8.3 says only "not offered").
7. **Release 1's head-moved banner**: drop "Kept: your N comments" (no comments until release 2).
8. **Near misses**: §4.2 says only `validate` and the doctor row show them, §6.1 says the errors sheet lists them. Drawn per §6.1.
9. **Home's line**: drawn under the Needs-you rows, outside their count, linking to Reviews.
10. **Settings shape**: the real Settings page is one scroll of titled cards, so the spec's "Settings → Library" is a section;
    repo roots are listed read-only, the `aw` root gets "Turn off", not "Remove".

Spec fixes (no decision needed): the summary strip's own loading / empty / error texts; `risk` has no error state; focus after Reload
lands on the strip's first segment; `SND-REVIEW-011`'s text is the head-moved banner; `SND-REVIEW-006` belongs in §8.2; the head-moved
banner counts outdated comments in release 2; focus when the source row is gone after a reload (the summary strip); §7.1 `groups`
needs a `folded` key; the 3,000-file cap shows "3,000 files" and the list as GitHub gave it; a "claimed in body" check counts as open;
"Send as fix task" only on failed checks and unclaimed requirements; the list's zero state "none open"; project groups before repo
groups; J/K/Enter shown in the list's hint line. The builders' full notes: `docs` in the PR body.
