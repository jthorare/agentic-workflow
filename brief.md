# Brief: workflow revision diff on the canvas (#2977, part of #2960)

Static HTML frames, 1440x900, dark, in the #2978 grammar. No app code, no git, no gh, no dotnet, no posting.
Write only inside ~/.agentic-workflow/joi-fairshare-agentic-workflow/design/diff-2977/.

Read first:
- ../grammar-2978/grammar.md + grammar.css (link `../grammar-2978/grammar.css`; new classes in a local diff.css)
- today-canvas-1280.png in this folder: today's canvas (node cards ~200x64: title 13/600 + type 12 ink-subtle; edges with an
  output label pill like `true`). Keep that node shape; restyle into grammar tokens.

What api/meta decided (#2977 comments):
- A revision = nodes[] (id, type, title, config, onFailure) + edges[] (from, output, to). The diff between two saved revisions is
  node added / removed / changed, edge added / removed, and per changed node the fields that differ old -> new in plain captions
  ("Schedule: weekdays 7:00 -> weekdays 8:30", never raw keys like config.cron). No rename guessing: a rename is remove + add.
- The diff carries validate findings: "what changed and is it still valid" in one view.
- An agent's draft is a pending revision in the same revision list (state: pending), and needs Accept. A person's own edit doesn't.

Example workflow (use it in every frame): "PR review loop": trigger `PR opened on ledgerly/web` -> `Pre-review` (prompt) ->
`Needs me?` (condition) -true-> `Tell me` (notify), -false-> `Label ready` (github). The agent's draft (rev 4 pending, by agent
`r-0412`, 3 min ago) does: adds `Run tests` (command) between trigger and Pre-review; changes Pre-review's model + prompt;
removes `Label ready` and its edge; changes Tell me's channel `#eng-web -> dm`. One validate finding: `Run tests` has no repo set.

Frames (one .html + .png each):
1. `diff.html` - Workflows / PR review loop, header shows `rev 3 live · rev 4 pending by r-0412 · 3m`. Canvas left (1000 px), a
   440 px change list right (the peek pane shape). On canvas: added node = ok-soft fill, ok 1px border, `+` corner badge;
   removed = 50% opacity, dashed err border, title struck; changed = att 1px border + small att dot badge with count of fields;
   unchanged = as today, dimmed slightly (ink-subtle titles) so change pops. Added edge solid ok, removed edge dashed err.
   Change list: grouped Added 1 / Changed 2 / Removed 1, one 32 px row each (glyph, node title, `2 fields`); then the finding as a
   .state-line (att) "Run tests has no repo. [Set repo]". Cursor on the first change; canvas shows that node with the cursor ring.
   Bottom of the pane: `Discard` ghost, `Accept rev 4 [Ctrl+Enter]` primary - disabled while the finding is open, with the reason
   under it in ink-subtle ("Fix 1 finding to accept").
2. `diff-field.html` - same, cursor on Changed > Pre-review, the row expanded below it: field rows `Model  sonnet -> opus`,
   `Prompt` with a 3-line before/after (removed line err-soft bg + `-`, added ok-soft + `+`, mono 12).
3. `diff-clean.html` - after Set repo: finding gone, Accept enabled.
4. `accepted.html` - after Accept: header `rev 4 live`, canvas back to normal, toast `Accepted rev 4 · Undo [Ctrl+Z]`.

Keys in the footer: grammar keys exactly, `.keys-div`, then `n/p next/prev change`. Accept is Ctrl+Enter (a write is never a bare
letter). Sentence case, ids by kind (r-0412 run), no all caps. Colour only for change state; nothing else on the canvas is coloured.

Screenshot each: google-chrome --headless=new --virtual-time-budget=3000 --window-size=1440,900 --screenshot=<name>.png file://<abs>
Look at every PNG yourself (Inter loaded, nothing clipped). Report file list + numbered guesses.
