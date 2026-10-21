# Sindri UI: plain words and less density (owner feedback F3/F4, 2026-10-19)

Every builder on F3/F4 follows this guide, so the app reads the same everywhere.

## The owner's complaint
- There's too much information. You can't tell what you're looking at or why it matters. It lacks the polish of Linear, HumanLayer and SuperPlane.
- Labels mean nothing to a user. Example: Work > a row > a sidebar list titled "facts".

## Density rules (F3)
1. **One primary thing per view.** Each page and sheet leads with the one thing a person comes for: the run's current stage and what it needs from you, the row's title and status, the session's state. Everything else is secondary.
2. **Progressive disclosure.** Secondary detail sits behind a collapsed "Details" (or a named disclosure like "What this run collected", "Commands", "History"), closed by default. Remember the open/closed choice per page the way the frame remembers panels today (Preferences), if that pattern exists; don't invent storage.
3. **No raw ids up front.** Addresses (ITM-…/step/1), launch ids (LCH-…), hashes and versions move into Details, with Copy. Up front, show the item's title and the workflow's display name.
4. **Fewer words.** Cut duplicate labels, repeated counts and "as served" text. Prefer one short sentence over a table row.
5. **Quiet visuals.** Use the existing theme tokens, one accent for the primary action, and muted secondary text. Don't add colours.
6. **The ideas lane is making SuperPlane-style mockups** (workflows shown like SuperPlane, with our context map inside). When the lead hands you one, build to it. Until then, apply rules 1–5.

## Plain words (F4)
Never show a user an internal term. Use these:

| Internal | Show the user | Why it matters, as a hint where it helps |
|---|---|---|
| recipe / recipe id (workflow.scoping) | the workflow's display name ("Scoping"); the id only in Details. The engine is adding `displayName`: read it as OPTIONAL, falling back to the id with the "workflow." prefix dropped and the rest title-cased | |
| instance | run ("this run", "3 runs") | |
| state | stage | |
| Step (a unit of work an agent or tool does) | step | |
| facts / fact:scopeInput | "What this run collected" (a list heading); each entry by its plain name (scopeInput → "Scope input"), raw key in Details | "Each stage adds what it found; later stages and agents read it." |
| desk | "Waiting for you" / "your turn" | |
| park / parked | "Paused: needs a decision" | |
| takeover / handback | "Take over" / "Hand back" (already plain); hint "Pauses the agent so you can do this step yourself." | |
| launch | "Agent session" | |
| attach | "Watch the agent" (opens its terminal) | |
| SND-…, RCP-…, PKT-… codes | a plain sentence first; the code in Details or after "·" in muted text | |
| counts.done etc., raw field names | never shown | |
| hash12, version (v2) | Details only | |
| epoch, projector, views.db | "Behind: refreshing" style sentences, or Details | |

### Run outcome words (owner approved, 2026-10-19)
A finished run shows exactly one of these, never DONE for all of them:
- **FAILED**, in the fail tone
- **DIDN'T WORK OUT**, in the attention tone
- **CANCELLED**, in the neutral tone
The api lane is fixing StateChip, which today shows DONE for all three. Use these words in any label you write for a finished run.

When in doubt, ask: would someone who has never read the spec understand this word, and know why it's on screen? If not, rename it, add a one-line hint, or move it into Details.

## Rules for the four builders
- Own your pages only (your brief lists them). Add or change labels only in YOUR area's Labels.*.cs files. Shared files (Labels.cs, Labels.Palette.cs, Shell) belong to B4. If you need a shared label changed, report it instead of editing.
- Tests: update every pinned string you change, red first (change the test's expected text, see it fail, then change the label). Accessible names follow the visible text (WCAG 2.5.3: the visible words at the start of the name).
- Keep each PR to your pages, and note any term you left alone and why.
