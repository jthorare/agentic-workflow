# Setup (the rail's Settings): notes for the ui lane

Frame, tokens and the Annotations toggle are copied from `workflows.html`. Files: `setup.html`, `questions.html`, `not-mocked.html`.

## Regions (1440 wide; content column 760 max, centred, 24 px side padding)
- Top bar 56 px, title "Settings". Lead (20/28 600), one sub line, progress strip: six 4 px segments (done = `--done-dot`, working = `--run-dot`) plus "N of 6 done".
- "You've run ./setup.sh --with-sindri" line: 40 px, `--card-warm`, 1 px `--border`, radius 8.
- Checklist: one flat panel, radius 8, six rows separated by hairlines. Row min 64 px: 24 px number circle (check when done), title 14/20 500, why line 12/18 `--muted-fg`, status badge (word plus glyph), primary action at the right, chevron. A row expands (click or Enter) to a body indented 56 px.
- Status words: To do, Working, Done, Failed, Needs you. "Needs you" uses the attention amber only. Done rows show the detail line instead of the why line.
- Footnote: terminal alternative for Onyx (`sindri-app onyx up`, copy chip).
- Done state: quiet green panel with a button to Workflows.

## States (hash)
`#fresh`, `#profile` (typing: button stays disabled until the field equals `approve 3f9a1c`), `#approveall` (hold at 62%; press and hold the real button to run the 1.5 s fill, release cancels, completion marks the row Done), `#done`, `#failed` (map failed, Retry; Onyx login form open). `?annot=1` turns annotations on.

## Interactions
Row click or Enter toggles; the action button opens the row and focuses its first field. Typed field enables the Approve button on exact match (trimmed). Hold button: pointerdown or Space/Enter starts, pointerup, leave or keyup cancels.

## Data gaps (outlined with the Annotations toggle)
- `GET /setup` `SetupView`: the six steps with `state, why, detail, error, action, progress {done,total}`. Titles used here ("Add your repository", ...) are plain titles; the spec's `title` for `repo` is "Add your repo". Ask for the longer ones, or let the UI own the titles.
- Profile change summary in plain words (repos, services, allowance, connections, human-only stops): `GET /profile` today returns changed key paths only.
- Typed challenge: `POST /sindri/workflow/challenges` with `confirm: "typed"`, then `POST /profile/approve {hash, answer}`. Phrase shown here is the short form `approve <hash6>` (see needs-you question 10).
- Approve-all: `GET /workflow/recipes/approve-all` needs per-workflow `displayName`, a one-line capability summary and the eval result ("passed 9 of 9"); `confirm: "held"`, `minHoldMs: 1500`, then `POST ... {challenge, heldMs}`. The server refuses a hold under 1.5 s or an answer under 2 s after the challenge. List shown is the 11 workflows in sample.json (not the 26 in the spec).
- Per-coding-agent result: `mcp.registered {providers:[{provider,outcome}]}`; "will ask you to trust this folder" is a fixed line for Codex and Cursor.
- Map progress: `progress {done,total}` of files (the spec says repos done of repos total, so a files figure is a new field). The map has no promote button (owner rule).
- Onyx: `POST /setup/onyx {adminEmail, adminPassword}` (never logged; used once).
- Errors in plain words (codes only in the grey row): SND-SETUP-001 "That folder isn't a git checkout, or can't be read", 002 "That repository is already added", 003 "Hold the button for the full time", 004 "Add the repository and approve your settings first", 005 "Enter your Onyx email and password".

## Avalonia
Checklist = ItemsRepeater with an expander row template; the plan list is a plain ItemsControl (no inner scroll). The hold button is a custom Button: PointerPressed starts a 1.5 s DispatcherTimer or animation on a Border width, PointerReleased and PointerCaptureLost cancel.
