---
name: system-design-video
description: Turn a system design (data model, how a value flows through functions, rollout states, PR order) into a short whiteboard explainer video drawn live, stroke by stroke, on a dark board. One cut per audience (engineering, product), captions, a narration script, and 1080p + 720p MP4s. Use when asked for a system design video, a design walkthrough film, or a whiteboard explainer of a design doc or TDD.
argument-hint: "[design doc, packet dir, or topic] [--persona engineering|product]"
allowed-tools: Bash(ls *), Bash(mkdir *), Bash(bun *), Bash(ffmpeg *), Bash(ffprobe *), Bash(git *), Bash(SHARED_DIR=*), Bash(source *), Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `$HOME/.agentic-workflow/toolkit/skills/_design-preamble.md` and follow it before continuing (the ink colors come from the project's `design-tokens.json`).

---

# System design video

Explain a design the way an engineer whiteboards it in an interview: a dark board, the explanation drawn live, stroke by stroke. Boxes, arrows, tables and numbers appear as they are drawn; earlier drawings stay on the board; the camera pans to new space. Nothing fades in as a finished diagram. Each beat is one spoken sentence and the pen draws what that sentence says.

Same four gates as a product demo film (scope, brief, storyboard, produce), but the footage is a drawing, not a screen recording. Works for any project; everything project-specific comes from the project (see Inputs).

**The default film is the engineering system design, not the concept.** It is built from the design doc's sections, and a run walks them in this order:

1. **The model:** the old tables against the new ones, each table and key drawn, and how old rows map onto new ones (the backfill arrows).
2. **The write and read paths:** who writes, what each write touches and in which transaction, which readers run, what picks the one that serves, where mismatches go.
3. **The rollout and its gates:** every state a unit passes through, the check that lets it move on, the way back at each step, and the point of no return.
4. **The work breakdown:** every PR or milestone, with its dependencies and a few words on what it changes. Mark which dependencies the doc states and which you inferred.

A concept or product cut (the idea, the worked example, what changes for the people using it) is a secondary persona and is never the only film a run produces. Each beat names the design-doc section it comes from (`src` in the storyboard, shown top-left on screen); a beat with no section behind it does not belong.

Read before starting: `references/style.md` (look, motion, legibility), `references/storyboard-format.md` (the storyboard file), `references/personas.md`. Read on trouble: `references/gotchas.md`. A runnable worked example, with generic content, is in `examples/base-limit/`.

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"          # sets REPO_SLUG, AW_DIR
SKILL="$HOME/.agentic-workflow/toolkit/skills/system-design-video"
D="$AW_DIR/videos/<design>/<persona>"      # brief.md, storyboard.json, narration.md, take/, deliverables/
mkdir -p "$D"
```

Output goes under `$AW_DIR/videos/` (never into the code repo): `brief.md`, `storyboard.json`, `narration.md`, `take/`, `deliverables/`.

**Prerequisites:** `ffmpeg` and `ffprobe` with libx264, `bun`, and `playwright-core` with its Chromium (Step 0 checks all four). The renderer needs a desktop-capable machine for headless Chromium and a few minutes of CPU per film.

**Exit codes:** `doctor.sh` 0 ready, 1 something missing; `deliver.sh` and `playback-check.sh` 0 ok, 1 usage error or a failed check (`playback-check.sh` and `deliver.sh` fail on a decode error, codec other than h264, level above 4.1 or B-frames; `review.sh` exits 1 on usage error only and prints a warning when its own playback check fails); `render.ts` 0 done, 1 bad arguments, any other non-zero is a thrown error (bad storyboard, unknown token, no playwright-core).

## Inputs (all from the project, nothing baked in)

| Need | Source |
| --- | --- |
| The design: model, flows, decisions, a worked example with real numbers | a **packet dir** from `/system-design` (read `packet.json`, see below); otherwise the doc the user names, or the plan, spec and TDD files in the repo |
| Code and surfaces named on the board | Sindri context: `mcp: sindri/context_lenses`, then `mcp: sindri/context_query` for the symbols and files a beat names; the repo's `AGENTS.md` for the vocabulary. If Sindri is not available, read the code directly and say so in the brief |
| Look: board color, chalk, the meaning-colors | `design-tokens.json` (W3C DTCG) and `.impeccable.md`, from **Invoke skill `design-language`** if the project has neither. Ask the user which tokens are the board, the neutral ink and each concept's color |
| Ticket context | whatever tracker the project uses, through its own tool; read-only |

Every number on the board comes from a source line you can cite. Trace the worked example's arithmetic against the source before drawing it.

## The packet (preferred input)

`/system-design <topic>` ends with `/system-design-video <packet dir>`. The dir holds `packet.json` (slug, title, personas -> pages, `diagrams` -> `{mmd, svg}`, `sections` in TDD order with `id`, `title`, `anchor`, and the diagram keys each embeds), `tdd.html`, `diagrams/<key>.mmd` (+ `colors.json`), `status.md` and `prs/`. Read `packet.json` first, then the sections the film walks.

- **Boards come from the `.mmd`, not from prose.** Each scene's board is laid out from its diagram's mermaid source, so the boxes, labels, arrows and subgraphs are the ones in the doc. Redraw from prose only when a scene has no `.mmd` in the manifest, and say so in the brief.
- **Beats cite the manifest.** A beat's `src` is `TDD <section anchor>` of the section that embeds its diagram (the script fills it); a beat for a diagram no section embeds does not belong.
- Node colors come from the packet's `diagrams/colors.json` (blue = primary ink, violet = secondary, green = tertiary, red = danger, grey = chalk), the same file the doc's diagrams were styled with.

```bash
bun $SKILL/scripts/packet-to-storyboard.ts <packet dir> --keys model,paths,rollout,pr-order --out $D/storyboard.json
```
It parses flowchart (nested subgraphs) and stateDiagram-v2 sources, lays each out in layers (arrows that would cross a box bend around it), and writes one beat per subgraph (or per layer of loose nodes), each scene in its own region of the world with the camera framed on the beat. Every `say` it writes is a scaffold (the group title): rewrite each into one spoken sentence from the doc's own words and numbers, add the worked-example beats the diagram does not carry, and keep each `src`. Check the stills for an arrow that still crosses a box; move that node or add `via` waypoints by hand.

## Step 0: Check the machine

```bash
$SKILL/scripts/doctor.sh
```
It checks ffmpeg, bun, and playwright-core with its Chromium. It prints the install command for anything missing; it never installs. If the browser stack is missing, **Ask the user** whether to run the printed command.

## Step 1: Scope (gate)

One video per audience, each answering one person's question (`references/personas.md`). Defaults: the engineering cut first (model, write and read paths, rollout and gates, work breakdown: the four parts above) and, second, a product cut (one thing to set, a rule per workflow, what changes for the people using it). An engineering film of 90 to 150 seconds is normal; a product film is shorter. Merge cuts that would draw the same board; drop an audience with nothing to see. Write the film table (audience, question, story from start to payoff, the shared worked example). **Ask the user** to approve the list before continuing.

## Step 2: Brief (gate)

`$D/brief.md`: the promise in one sentence, facts with source refs, the worked example traced step by step, 3 to 8 beats, and `$D/narration.md` (one spoken sentence per beat, in the audience's words). A ratio reads "X requests to Y workers", never with an arrow. **Ask the user** to approve.

## Step 3: Storyboard (gate)

`$D/storyboard.json` per `references/storyboard-format.md` (from a packet, start from `packet-to-storyboard.ts` output above): one scene per design-doc section (model, paths, rollout, work), one beat per sentence, each beat's `src` naming its section, items in the order the pen draws them, one new idea per beat, 90 to 150 seconds for the engineering cut and 30 to 60 for a concept cut. `inks` bind each ink name to a token path. **Ask the user** to review.

## Step 4: Produce

```bash
TOKENS=design-tokens.json        # the project's tokens
bun $SKILL/whiteboard/render.ts $D/storyboard.json $D/take --tokens $TOKENS --still 5,12,20   # stills in seconds
bun $SKILL/whiteboard/render.ts $D/storyboard.json $D/take --tokens $TOKENS                   # footage.mp4 + layout.json
$SKILL/scripts/review.sh $D/take                                                              # one still per beat, in sheets
```

Read every `take/check/sheet-*.jpg` against the storyboard and run the checklist in `references/style.md`. Fix overlaps and clipping in the storyboard, re-render, read the sheets again. Then encode:

```bash
$SKILL/scripts/deliver.sh $D/take/footage.mp4 $D/deliverables/v1 <name> <board-hex>   # board hex is in take/resolved-inks.json
```

This writes the 1080p and 720p MP4s (BT.709 tagged, no audio) and prints ffprobe and a corner-pixel check against the board color. Extract 3 to 6 frames from the 1080p during motion and one from the 720p cut (`ffmpeg -ss <t> -i <mp4> -frames:v 1`), and read them.

`deliver.sh` ends with `playback-check.sh` (full decode, level at most 4.1, no B-frames). Then tell the user to play each MP4 once, start to end, in the system player before delivery; stills are not playback.

Sound and voiceover are not built in. The captions carry the narration. Offer the user the options in `references/style.md` (audio) and add none unasked.

## Done when

- Every beat's still was read against the storyboard, and the style checklist holds.
- The deliverables exist at 1920x1080 and 1280x720, h264, BT.709 tags, with the corner pixel check passing.
- The report names which frames were read; say "reviewed stills", never "watched", unless the video was played in a real player. `playback-check.sh` passes on both files.
- Deliverable paths and sizes are listed.

Posting to chat or a tracker is a separate step: **Ask the user** first, and use the project's own tool for it.

## Next steps

- `/design-language`: when the project has no tokens, define them first.
- `/officeHours` or `/archReview`: when drawing the design exposed a gap in the design itself.
