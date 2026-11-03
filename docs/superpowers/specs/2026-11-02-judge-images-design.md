# The judge sends images to Jev: visual-critique first, then any question, Sindri's choose Steps and the hooks

Status: draft for the owner's read (not marked ready). Issue: #1961. Owner answers (2026-11-02, through meta): 1 - build on Jev's
experimental image API (`TypeSafe-Danger-Zone-Images: true`, `assets` + `state`); 2 - an image question goes to Jev first, with the
agent CLIs as the fallback; 3 - there are no model options: one Jev model, so no model list, no model config and no per-model
thresholds; 4 - every hook decision may attach images, as an optional input, and an old caller's call stays byte-identical.
Written against main `414f647bf`.

Related: `docs/csharp/judge.md` (providers, chain, decision rows), `docs/superpowers/specs/2026-10-29-context-model-routing-design.md`
(the other Jev caller, `JevChoicePort`), Jev's image docs at https://docs.typesafe.ai/images (read 2026-11-02).

## 0. Why

Jev is the judge's cheapest and fastest provider, and today it never sees a picture:

1. `JevProvider.Classes` is `ContentClasses.Text` (`src/Judge/Aw.Judge.Infrastructure/Providers/JevProvider.cs:47`), and
   `ChainSpec.Build` routes the image class to the agent CLIs then rules (`src/Judge/Aw.Judge.Core/Providers/ChainSpec.cs:21-23`).
   So every `visual-critique` call starts `claude`, `codex` or `cursor-agent` with a screenshot, which takes tens of seconds and a
   CLI's usage.
2. Only `visual-critique` can carry an image at all, and only as two named fields (`afterScreenshot`, `baselineScreenshot`,
   `src/Judge/Aw.Judge.Core/Questions/VisualCritiqueInput.cs:7`). `choose` (what Sindri's `step.judge.choose` asks), `ask-check`,
   `brief-scope`, `wake-gate`, `trait-check` and the rest are text only, so a decision about a UI change is made from text that
   describes the screen instead of the screen.

Jev has taken images since 1.13 (our pin is `jev-1.13.0`, `src/Judge/Aw.Judge.Core/Batching/JevModel.cs:6`), behind an opt-in header.

### Goals

1. Image questions go to Jev first, then the agent CLIs, then rules (owner answer 2).
2. Any judge question can carry images through one optional input field, `images`, with the same routing.
3. Sindri's `step.judge.choose` can attach images it already has as facts (ui-evidence frames).
4. The hooks attach images where the turn has them; every hook call without images stays byte for byte what it is today.
5. Nothing about models changes: one Jev model, `JevModel.Id`, and today's thresholds (owner answer 3).

### Non-goals

- Downscaling, cropping or rotating images. Jev rejects images outside its limits instead of resizing them; an image Jev can't take
  goes to the CLIs (3.3).
- Jev's Box questions (bounding boxes). Nothing asks for a location yet.
- Images in the batched Jev calls (`IBatchClassifier`: `JevChoicePort`'s label and surface picks, `prompt-sort`). None of them has an
  image to send.
- Image options (Jev lets a choice option be an image). Options stay text.

## 1. What Jev takes (from its docs, 2026-11-02)

- Header `TypeSafe-Danger-Zone-Images: true` on every request that holds an image. Without it, `400`.
- Images go in a top-level `assets` map, each under a reference string the caller picks. The state names the image by putting that
  reference where the image belongs: a string value equal to a reference is replaced by the image, and a reference inside a longer
  string is replaced where it appears. Every asset must be referenced, or `400`.
- Two encodings. JSON with `{"type":"image","format":"png","data_base64":"..."}`, or **multipart** (recommended: base64 is a third
  bigger and counts toward the body limit): part `request` (`application/json`) holds the body, each image is its own file part, and
  its asset says `{"type":"image","part":"<part name>"}`.
- Limits, rejected not resized: PNG or JPEG stills; 16 MiB per file; width and height each 280 to 1568 px; 78,400 to 2,458,624
  pixels; 32 MiB per body. `400` for a bad image, `413` for a size limit, `400 max_tokens_exceeded` when images plus text overflow.
- Every question type takes images, `choice` included.

## 2. The input: `images`

Every question's input JSON may hold one more top-level field:

```json
{ "transcriptTail": "...", "images": ["/abs/path/home.png", "/abs/path/home-base.png"] }
```

- **Absolute paths to files**, at most **4** per call, total at most **24 MiB** (Jev's 32 MiB body less room for the JSON). More, a
  relative path, a missing file or a directory: the call escalates `invalid-input` with the reason `invalid-images`, like any other
  input that doesn't validate, and no provider runs.
- **Format by magic bytes**, not by extension: PNG (`89 50 4E 47`) or JPEG (`FF D8 FF`). Anything else is `invalid-images`.
- **Read once.** The evaluator reads each file once, hashes it (sha256) and reads its width and height from the header (PNG `IHDR`,
  JPEG's first `SOFn`; no image library). Every provider gets those bytes, not the path, so a file that changes mid-call can't give
  two providers two different pictures.
- **Absent or `[]` is today's call.** The evaluator removes `images` before it deserializes the question's own input record, so no
  question record changes. With no images nothing else changes either: same chain, same request bytes, same decision row, same stdout.
- `visual-critique` keeps its fields. Its `afterScreenshot` and `baselineScreenshot` (relative to `evidenceDir`) become its images,
  in that order, through the same path.

Where it lives: `Aw.Judge.Core/Questions/ImageAttachment.cs` (path, sha256, bytes, width, height, format; the limits check is a pure
function on it), and `QuestionEvaluator` reads and strips the field before `Evaluate<TInput>` runs.

## 3. Routing and the Jev call

### 3.1 The chain

A call **with** images routes on the image class, whatever its question's own class. A call without images routes on its question's
class, as today.

`ChainSpec.Build` changes one line: the image class becomes `[jev (when on), ..agent CLIs, rules]`, the same order as the text
classes. `JevProvider.Classes` becomes `ContentClasses.All`. `--providers` and a question's own provider list still narrow it.

Before: `judge visual-critique` with Jev's key set asks `claude-cli`; `"provider": "claude-cli"` on the decision row.
After: the same call asks Jev; `"provider": "jev"`, and `claude-cli` only when Jev is unavailable, errored or below threshold.

### 3.2 The request

`JevProvider` sends multipart when the call has images, and today's JSON body when it hasn't (byte-identical, 6):

- Header `TypeSafe-Danger-Zone-Images: true`.
- Part `request`: today's body (`model`, `state`, `questions`) plus `assets`. Each image's reference is
  `aw-image-<n>-<first 12 hex of its sha256>`, e.g. `aw-image-1-3fa9c1d2e8b0`: unique per call and never a string a real input holds,
  so Jev's in-string replacement can't fire on the caller's text.
- In the state, `images` holds the references in order (`"images": ["aw-image-1-…", "aw-image-2-…"]`), never the paths. For
  `visual-critique` the reference replaces the path in `afterScreenshot` and `baselineScreenshot`.
- One file part per image, named by its reference, content type `image/png` or `image/jpeg`.
- The instructions get one sentence: "The images are in `images`, in the order the input lists them." (visual-critique's prompt
  already names its two screenshots.)
- Redaction and the input cap run on the state and instructions exactly as today. Images can't be redacted (3.4).

### 3.3 What Jev can't take

Checked before anything is sent, from the header read in 2:

| Case | Result | Next |
|---|---|---|
| Width or height outside 280-1568 px, pixels outside 78,400-2,458,624, file over 16 MiB | `ProviderUnavailable("image-out-of-limits")` | the CLIs |
| `providers.jev.images: false` in config.json | `ProviderUnavailable("images-off")` | the CLIs |
| Jev answers `413` | `ProviderUnavailable("image-too-large")` | the CLIs |
| Jev answers `400` on an image call | `ProviderErrored("http-400")` as today | the CLIs |

The common case this hits: ui-evidence's default viewport is 1512x982 (`src/Evidence/Aw.Evidence.Core/Design/DesignManifest.cs:10`),
which fits; the same page at device pixel ratio 2 is 3024x1964, which doesn't, and goes to the CLIs as it does today. Downscaling
those is the owner question in 9.

### 3.4 Images leave the machine

Jev is a third party. Text gets redacted before it goes; a screenshot can't be. So:

- Only files the caller named in `images` (or visual-critique's two fields) are ever sent. The judge never looks for images itself.
- config.json `providers.jev.images` (default `true`, owner answer 1) turns image sending off without turning Jev off: Jev keeps
  answering text calls, and image calls skip it (`images-off`).
- Today `judge config set` takes only `<question> enabled|threshold`. PR 1 adds one form, `judge config set providers jev-images
  true|false`, so turning it off is one pasted command, not a hand edit; `judge config get` already prints the whole config.

### 3.5 The agent CLIs

They take images today through `ImageEvidence` (`src/Judge/Aw.Judge.Infrastructure/Providers/ImageEvidence.cs`), which needs every
screenshot inside one `evidenceDir`: Claude's Read tool is scoped to it, Cursor's workspace is it. Generic `images` can be anywhere,
so for a call with `images` the judge writes the read bytes into a fresh per-call directory (`$TMPDIR/aw-judge-images-<random>/`,
`image-1.png`, …, removed after the call) and hands the CLIs that directory as their `evidenceDir`. Same bytes Jev saw, same scoping.
visual-critique keeps its own `evidenceDir`, so its CLI calls don't change.

## 4. The decision row

The stored input holds images as `{ "path", "sha256", "bytes", "width", "height" }` in place of each path, never the bytes. Example:

```json
{ "transcriptTail": "…", "images": [{ "path": "/home/u/r/ui-evidence/home.png", "sha256": "3fa9c1d2…", "bytes": 412337, "width": 1512, "height": 982 }] }
```

The row's `provider` and `reason` work as today (`jev` / `model`, or the fallback with Jev's reason in the trace). `judge why <id>`
prints the image lines. `judge eval` and the label replays re-read the paths: a file that is gone or whose sha256 changed makes that
case `skipped: image-changed`, not a failure.

## 5. Callers

### 5.1 `choose` and Sindri's `step.judge.choose`

- `choose` takes `images` like every question (2).
- `ChooseRequest` (`src/Sindri/Aw.Sindri.Core/Judge/ChooseRequest.cs:9`) gains `IReadOnlyList<string> Images` (default empty), and
  `JudgeCliChoice` writes it into the `choose` input only when non-empty, so every recipe today sends the same bytes.
- A recipe's `step.judge.choose` state gets an optional `images: <fact key>`: a fact whose value is a list of absolute paths (what
  `ui-evidence run` already reports for its frames). Recipe validation: the key must be one of the state's readable facts
  (`RCP-CHOICE-007`, "`images` names fact `<key>`, which this state can't read"). The Step resolves the list, keeps the first 4,
  and fails the Step with `step_failed` when a path is missing, before any judge call.
- No shipped recipe uses it in this work. The proof is an eval case on a fixture recipe plus the e2e in 8.
- The Step's `fact:decision` gets `images: <n>` so the Jobs view can show "decided with 2 images".

### 5.2 The hooks (owner answer 4)

Every hook question takes `images`. What each hook attaches, at most 4, newest first:

| Hook | Question | Images it attaches |
|---|---|---|
| `done-gate.sh` | `ask-check` | PNG/JPEG files the last assistant turn opened with `Read` (from `transcript_path`, the same tail it reads now). Codex and Cursor send no `transcript_path`, so none there |
| `scope-gate.sh` | `brief-scope` | absolute paths ending `.png`/`.jpg`/`.jpeg` named in the brief's goal or acceptance criteria that exist |
| `trait-check.sh` | `trait-check` | edited files that are PNG/JPEG, as `--image <path>` (a new repeatable flag; today they're skipped as binary) |
| `send-gate.sh` | `wake-gate` | absolute PNG/JPEG paths named in the message text that exist (a lead sending "see /…/home.png") |
| `prompt-sort.sh` | `prompt-sort` | absolute PNG/JPEG paths named in the prompt that exist. An image pasted into Claude Code reaches the hook as `[Image #1]` with no path, so it can't be attached |

So all five hooks that ask the judge can attach images (owner answer 4). The three text sources share one helper,
`config/lib/image-paths.sh` (`aw_image_paths <text>`: absolute paths ending `.png`/`.jpg`/`.jpeg`, existing files only, deduped,
first 4), so the rule is written once.

The hook contract stays frozen (AGENTS.md "hook contract"): no new exit codes, stderr prefixes or stdout fields. A hook adds `images`
to its `jq -nc` input only when it found at least one, so `ASK_INPUT` for a turn with no image reads is exactly today's string.

## 6. Byte-identical for old callers

The rule: a call with no `images` (absent or `[]`) is today's call, end to end. The checks, each in the PR that could break it:

1. **Jev wire golden** (PR 1): `JevProvider` against a recording `HttpMessageHandler`, one case per question, no images: the request
   body bytes and headers equal a golden captured on `414f647bf` before any change. With images: the multipart golden (parts, names,
   header, references in the state).
2. **Decision row golden** (PR 2): the stored input for every question without images equals today's.
3. **Hook surface** (PR 4): `config/lib/tests/judge-cs-hook-surface.test.sh` passes unchanged against the new judge, plus new cases
   with an image (done-gate after a `Read` of a PNG fixture: the input carries `images`, the hook's exit and stderr are unchanged).
4. **Sindri** (PR 3): `JudgeCliChoice`'s argv and stdin for a request with no images equal today's (golden).

## 7. PRs

Red first in each; ideas reviews each; "Part of #1961" except the last, which says "Fixes #1961".

| PR | What | Red test (fails before, names the behavior) | Gate |
|---|---|---|---|
| 1 | Jev takes images: `ImageAttachment` + limits, `JevProvider` multipart + header + references, `Classes = All`, image chain `jev, CLIs, rules`, `providers.jev.images`, visual-critique on Jev | `VisualCritique_goes_to_jev_first_with_the_screenshots_as_multipart_parts`; `Image_over_1568px_skips_jev_for_the_clis`; the no-image Jev wire golden | Judge unit + integration, `judge-cs-hook-surface`, judge aot-smoke, Arch (new type in Core), PublicAPI |
| 2 | Any question takes `images`: the evaluator reads and strips it, routes on the image class, CLIs get the per-call directory, the decision row's image lines, `judge why`, eval's `image-changed` | `AskCheck_with_an_image_routes_to_jev_and_stores_its_hash_not_its_bytes`; `Input_without_images_stores_todays_row` | Judge unit + integration, hook surface, aot-smoke |
| 3 | Sindri: `ChooseRequest.Images`, `step.judge.choose` `images:`, `RCP-CHOICE-007`, the `fact:decision` count | `JudgeChoose_passes_the_named_fact_as_images`; `JudgeCliChoice_without_images_sends_todays_input` | Sindri unit + integration, recipe eval, Arch |
| 4 | Hooks: all five sources (done-gate, scope-gate, send-gate, prompt-sort, trait-check `--image`), `config/lib/image-paths.sh`, docs (`docs/csharp/judge.md`, `config/hooks` README) | `done-gate` bash case: a turn that read a PNG sends `images` | every hook bash test, hook surface, sync-rules |

**Parallel plan.** PR 1 first (everything rides on the provider and chain). Then PR 2. Then PR 3 and PR 4 at once: PR 3 is
Sindri-only and PR 4 is hooks plus one judge flag, no shared files. Builders: one per PR (Sonnet), the lane lead runs every gate.

## 8. Proof

- In tests: the goldens in 6 and the red tests in 7, on a recording handler (no network in CI).
- On the owner's box, once, after PR 1 (one item on the owner checklist, needs the Jev key):
  `judge visual-critique < tests/fixtures/judge/images/visual-critique.json` → you should see `"decision"` and, in
  `judge why <id>`, `provider: jev`. If Jev answers `400`, the checklist's second line captures the body
  (`AW_LOG_LEVEL=debug`), which is the evidence for a wire fix.
- After PR 3: a fixture recipe whose `step.judge.choose` names two frames, run through `sindri workflow start` on a scratch home;
  the Jobs view shows "decided with 2 images" and the ledger's `step.finished` carries `provider: jev`.

## 9. Owner question

1. **Big screenshots.** A retina screenshot (3024x1964) is over Jev's 1568 px limit, and Jev won't resize. My pick: send it to the
   CLIs, as happens today (3.3); no new dependency. The other way: the judge downscales to fit, which adds SkiaSharp (MIT) to the
   NativeAOT judge (~8 MB of native library per platform) and changes the picture Jev judges. Go with sending it to the CLIs?
