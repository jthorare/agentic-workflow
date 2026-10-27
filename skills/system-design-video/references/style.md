# Style: whiteboard

## Board and ink
- The board is a dark surface; neutral strokes are an off-white "chalk". Both, and every marker color, are **inks** the storyboard binds to design tokens (`inks`: ink name -> token path such as `color.surface.board`, or a literal `#rrggbb` only when the project has no token for it).
- A marker color means one concept and is used for that concept only: pick the project's accent tokens for the workflows, stages or services the design has, and keep the same ink for the same thing in every cut. Check contrast: a mid-tone accent on a dark board needs to stay readable at 720p; if it is not, ask for a lighter token instead of inventing a hex.
- Labels are hand-lettered (system fonts: Marker Felt, Chalkboard SE, Bradley Hand, Segoe Print, Comic Sans MS, then `cursive`; override with `font` in the storyboard). Docs and slides stay in the project's own type; this is the one place a handwriting face is right.
- Strokes are 6 world units wide, wobbled with a fixed seed (identical on every render), with a light chalk edge.

## Motion
- Everything is drawn. Boxes and arrows draw on with `cubic-bezier(.45, 0, .2, 1)`; text writes on left to right behind a pen dot; a value that changes is struck through and dimmed, and the new value is written beside it; a result worth noticing gets a ring.
- The camera pans and zooms between beats on a minimum-jerk curve (1.1 s, 1.5 s for long pans). Earlier drawings stay on the board and new ideas get new space. Never cut.
- Energy has a source: the pen. Nothing lights up, flashes or moves without it. An arrow leaves the thing that produces the number and lands on the thing that reads it.
- Every frame is a pure function of time, so any frame renders identically in any order.

## Legibility at 720p in chat
- Thick strokes; labels 48 px or more after camera zoom (at zoom 0.8 a 48-unit label is 38 px, the floor); big numbers 100 units and up.
- At most one new idea per beat, about 8 items at most.
- One spoken sentence per beat, bottom of frame. Keep drawings out of the bottom 130 px.
- Three-digit numbers are about twice as wide as one digit: leave room beside a struck-through value for its replacement, or shrink both.

## Wording
Sentence case; the audience's vocabulary. Ratios read "X requests to Y workers", never arrows. Arithmetic is shown as the sum ("7 - 1 = 6"). Every figure traces to a source.

## Audio
Not built in: the captions carry the narration. Options to offer, none added without approval: (1) the host's text-to-speech command per `say` line (free, local, robotic), (2) the user's own recording per beat, (3) a hosted text-to-speech service (paid), (4) a compositing engine for sound design, if the user has one. Voiceover timings come from `layout.json` (each beat's start, land and end).

## Review checklist (every sheet)
1. The beat's one idea is drawn, and nothing is half-drawn at its land.
2. No label overlaps another label, a value, or the caption; nothing is clipped by the frame or runs past its box.
3. Each ink means only its concept; every number matches the source.
4. The caption matches the beat's `say` and the narration script.
5. Old content left in view at the edge is not distracting: move it farther away or raise the zoom.
6. The board color is uniform to the corners.
