# References: list grammar and code context map (issue 2978)

Label key: [DOC] = stated in the vendor's page. [MEAS] = measured from a public image of known scale (none done here: I could not open images). [GUESS] = my estimate from memory of the product. Row heights below are all [GUESS] unless marked.

## Part A: list rows and peek panes

### 1. Linear: issue list + peek
- URL: https://linear.app/docs/peek (search result text confirmed it; my direct fetch returned 404, so re-check in a browser). Properties shown in rows: https://linear.app/docs/display-options [DOC, fetched]. Peek history: https://linear.app/changelog/2019-09-23-improved-peek
- Row height: about 36 px at default density [GUESS].
- State glyph: small circle-family icon, one per status type (backlog dashed, todo empty, in progress half, done check) [GUESS from memory]. Priority icon sits left of the id.
- Columns: ID, status, title, then right-aligned chips (labels, project, assignee, date). User picks which properties show [DOC].
- Peek: Space opens it, Space again closes, holding Space keeps it open until release [DOC via search]. Shows description, assignee, status, priority, cycle, labels, estimate, dates. Width about 40% of the window [GUESS]. Key was moved from Alt to Space in 2019 [DOC].
- Take: Space = peek, hold-to-peek, one glyph family per state, user-chosen property columns.

### 2. Superhuman: inbox rows + split inbox
- URL: https://help.superhuman.com/hc/en-us/articles/38458392810643 (Default Split Inbox) and https://help.superhuman.com/article/441-splits. Both returned 403/404 to my fetch; the search snippet confirms they exist. Not verified by reading.
- Row height: about 56 px, two lines (sender + subject/snippet) [GUESS].
- Glyph: none per row; unread is bold text and a left dot [GUESS].
- Columns: sender | subject + snippet | relative time. It is a sentence row, not a table.
- Splits: tabs across the top, each a saved filter; docs advise at most 7 [DOC via search]. Command palette Cmd+K lists every shortcut with its key [DOC via search].
- Keys: j/k move, Enter opens, e marks done [from memory, unverified here].
- Take: split tabs as saved filters (our sections), Cmd+K shows the key next to each action. Their row is two-line; ours stays one-line.

### 3. Raycast: list + detail
- URL: https://developers.raycast.com/api-reference/user-interface/list [DOC, fetched].
- Row height: about 40 px [GUESS].
- Layout: `isShowingDetail` puts a detail area on the right for the selected item [DOC]. Title + subtitle on the left, `accessories` on the right. The docs say to drop accessories while detail shows and move that info into the detail pane [DOC].
- Detail width: about half the window [GUESS]. Detail holds markdown, with a metadata table at the bottom [DOC].
- Keys: arrows move, Enter runs the primary action, Cmd+K opens the action panel [from memory].
- Take: when the peek is open, the fact columns collapse and their values move into the pane. That is the cleanest rule in this set, and it is documented.

### 4. Graphite: PR inbox
- URL: https://graphite.com/docs/use-pr-inbox (fetched via the .md variant). Product page: https://graphite.dev/features/inbox
- Sections [DOC]: Needs your review, Approved, Returned to you, Merging and recently merged, Drafts, Waiting for review. Sections are custom filters, reorderable, shareable by link.
- Row contents, height and glyph: the docs do not say. Skip numbers; row about 40 px [GUESS].
- Keys [DOC]: Cmd+K searches the inbox. S shows the stack, F toggles the file tree, both on the PR page. Inbox j/k is not documented in what I read.
- Take: sections named by "who owns the next move", which matches our "who it waits on" column. Six sections is a good upper bound.

### 5. Height
- Not verified. Search found no peek feature and no shortcut list. Found: custom shortcuts under Personal settings > Shortcuts (https://height.app/blog/january-14th-2021) and ctrl+J/K/H/L navigation (https://height.app/blog/jan-8th-2020, page text garbled).
- Take: only that user-set shortcuts exist. Do not cite Height for row or peek design.

### 6. GitHub issues list (new)
- Not verified. I could not find the 2025 changelog post (the URL I guessed returned 404). Verified instead: sub-issue shortcuts Option+Shift+C / A / P at https://docs.github.com/en/enterprise-cloud@latest/get-started/accessibility/keyboard-shortcuts, and `?` on a list shows shortcuts (2014 post, https://github.blog/2014-07-28-the-new-github-issues/).
- Take: `?` opens the key list on every list. Nothing on row geometry.

Not researched: Missive, Things, Front, Notion Mail. Out of time budget.

## Part B: the map

1. Answer-first list + map: Sourcegraph. https://sourcegraph.com/docs/code-search/code-navigation [fetched]. Hover gives the signature; "Find references" lists references, definitions and implementations at the bottom, precise and search-based mixed. Take: ranked list below, symbol detail above; mark each hit precise or guessed.
2. Scope as an editable workspace: Nx project graph. https://nx.dev/docs/features/explore-graph [fetched]. `nx graph --focus <project>` shows one project with ancestors and descendants. You can search, show or hide nodes, set proximity, group by folder. `nx graph --affected` marks what a change touches. Take: the scope is a node set the user adds to and removes from, and "affected" is computed from it.
3. Map-led bands, entry -> core -> data: Sentry trace waterfall. https://docs.sentry.io/concepts/key-terms/tracing/trace-view/ and https://sentry.io/changelog/trace-waterfall-time-compression-pinned-attributes-and-web-vitals/ [changelog fetched, docs from search]. Spans nest in a tree, colored by operation. Time compression shrinks empty gaps. Pinned attributes become columns. Take: rows grouped by band with colour per band, collapse empty gaps, pin a fact as a column.
4. Drill-down columns: macOS Finder column view. No URL verified. Take: selecting a row in column N fills column N+1; keyboard arrows left/right step between columns. Needs a source link before it goes in the spec.
5. Layered lanes: Backstage catalog or Honeycomb. Neither fetched, no URL verified. Pick one and verify before citing. Figma layers also unchecked.

## What our grammar takes
1. Row height: 32 px, one line (Linear and Raycast sit near 36-40 [GUESS]; we go tighter because there is no subtitle).
2. Glyph set: one 14 px circle family, 5 states (backlog, active, waiting, blocked, done); no per-row colour except blocked.
3. Peek width: 40% of the window, min 360 px (Linear about 40%, Raycast about 50% [GUESS]); fact columns collapse into the pane when it opens (Raycast, DOC).
4. Key map: j/k or arrows move, Space peek (hold = temporary, Linear DOC), Enter full view, Esc closes, Cmd+K lists every key, `?` shows keys for this list.
5. Columns: 3-5 fixed-width facts plus age and "waits on"; sections named by who owns the next move (Graphite DOC).
