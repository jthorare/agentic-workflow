# The judge sends images to Jev: visual-critique first, then any question, Sindri's choose Steps and the hooks

Status: draft for the owner's read (not marked ready). Issue: #1961. Owner answers (2026-11-02, through meta): 1 - build on Jev's
experimental image API (`TypeSafe-Danger-Zone-Images: true`, `assets` + `state`); 2 - an image question goes to Jev first, with the
agent CLIs as the fallback; 3 - there are no model options: one Jev model, so no model list, no model config and no per-model
thresholds; 4 - every hook decision may attach images, as an optional input, and an old caller's call stays byte-identical.
Written against main `414f647bf`; the goldens in 6 are captured on each PR's own base, in its red commit.

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
- Images in Sindri's batched Jev picks (`JevChoicePort`'s label and surface picks). They have no image to send. The two hook
  callers of the batch classifier, `prompt-sort` and `trait-check`, do get images (3.6).
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
  input that doesn't validate, and no provider runs. The judge never cuts the list itself: a caller with more than 4 picks which 4
  (the Step and the hooks keep the first 4 and say how many they dropped, 5.1 and 5.2), so one rule holds everywhere. The reason code stays one (`invalid-images`, so the hooks' contract is
  untouched); stderr names the cause in words, e.g. `judge: images[2] /home/u/r/a.webp is not a PNG or JPEG`.
- **Strict for explicit callers, best effort for hooks.** A hook can't check what the judge checks (magic bytes, size, a file
  deleted in between), and a bad attachment must never cost a hook the answer it gets today. So a hook's input also sets
  `"imagesBestEffort": true`: the judge drops each image that fails a check, with a trace line and a judge stderr line naming it (done-gate, scope-gate and send-gate
  send the judge's stderr to `/dev/null`, so their own stderr doesn't change; trait-check relays the judge's stderr when it blocks,
  but it and prompt-sort are batch calls, which record a drop on their own row and never on stderr, 3.6), and the call
  goes on with the rest (as text when none are left). Without it (Sindri's Step, a person at the CLI) a bad image escalates
  `invalid-images` as above.
- **Best effort covers Jev's limits too.** For a hook call, an image Jev can't take (3.3: outside 280-1568 px, over the pixel
  count or 16 MiB, `jevImages` off) is dropped before the call, never a reason to leave Jev: the call goes to Jev with the images
  that fit, or as text when none do, which is today's call. If Jev answers `413`, or `400` on an image call, the judge retries
  Jev once without the images when at least half the hook's budget is left; else Jev counts as errored, as on a Jev timeout today.
  Once a hook call has dropped its images for Jev, the rest of its chain runs as text too, so a hook never pays for a CLI call it
  doesn't pay for today. Example: on a Mac the agent Reads a retina screenshot (3024x1964) inside the checkout and stops; done-gate's
  `ask-check` drops it (over 1568 px) and Jev answers as text in about a second, as today, not `claude-cli` on a 10 s budget. Each
  drop is a trace line and a `jev images dropped:` line in `judge why` (4).
- **An older judge ignores the field.** Question inputs drop unknown fields (`QuestionDefinition` docs), so a box that installs the
  new hooks before the new judge sends `images` to a judge that reads past it: today's call, no error. No hook gets a new argument
  either: trait-check already passes every edited file as `--file`, PNGs included, and today's judge skips a binary one (5.2), so a new
  hook with an old judge sends the same argv as today. A new flag would have been usage (exit 64) on an old judge, which
  trait-check.sh fails open as `judge-exit-64`, losing the whole trait check.
- **Format by magic bytes**, not by extension: PNG (`89 50 4E 47`) or JPEG (`FF D8 FF`). Anything else is `invalid-images`.
- **Read once.** The evaluator reads each file once, hashes it (sha256) and reads its width and height from the header (PNG `IHDR`,
  JPEG's first `SOFn`; no image library). Every provider gets those bytes, not the path, so a file that changes mid-call can't give
  two providers two different pictures.
- **Absent or `[]` is today's call.** The evaluator removes `images` before it deserializes the question's own input record, so no
  question record changes. With no images nothing else changes either: same chain, same request bytes, same decision row, same stdout.
- `visual-critique` keeps its fields. Its `afterScreenshot` and `baselineScreenshot` (relative to `evidenceDir`) become its images,
  in that order, through the same path. Today `ImageEvidence` and `VisualCritiqueQuestion.InputProblem` keep them inside
  `evidenceDir`; `InputProblem` stays, and the reader also resolves symlinks and refuses a file whose real path leaves `evidenceDir`
  (red test in PR 1a: `VisualCritique_symlink_out_of_evidence_dir_is_refused`).

Where it lives. Core does no I/O, so the read is a port:

- `Aw.Judge.Core/Questions/ImageAttachment.cs`: path, sha256, bytes (`ReadOnlyMemory<byte>`), width, height, format. The limit checks
  (the judge's 4 / 24 MiB, Jev's 280-1568 px) are pure functions on it, and the PNG/JPEG header parse is pure too, on the bytes.
- `Aw.Judge.Core/Ports/IImageReader.cs`: `ValueTask<ImageRead> Read(IReadOnlyList<string> paths, ct)`, implemented in
  Infrastructure (`FileImageReader`: open, read once, hash). The evaluator calls it after it strips `images` from the raw input and
  before `Evaluate<TInput>` runs.
- `QuestionRef` gains `Images` (`IReadOnlyList<ImageAttachment>`, empty by default). `IModelProvider.Decide` keeps its signature:
  the bytes ride on the question, so the rules provider and `ModelPickRules` don't change, and only the providers that send images
  read the field (`JevProvider`, `JevInputCapProvider` passes it through, the three CLI providers).
- An escalated `invalid-images` row stores the paths as the caller gave them (nothing was read); a decided or below-threshold row
  stores the read form (4). Both shapes are in the row golden.

## 3. Routing and the Jev call

### 3.1 The chain

The question's `ContentClass` never changes: the decision row's class, `judge health`'s per-class counts and the adjudicator's
filter all stay as they are. Routing gets one more input instead: a call whose `QuestionRef.Images` is non-empty takes its provider
list from the image entry of the chain, whatever its class. `ChainRouting.Candidates` takes `hasImages`; a call without images routes
exactly as today. The adjudicator (`AdjudicateService.cs:104`, which skips image-class rows because its child gets no Read tool) also
skips rows that stored images.

`ChainSpec.Build` changes one line: the image class becomes `[jev (when on), ..agent CLIs, rules]`, the same order as the text
classes. `JevProvider.Classes` becomes `ContentClasses.All`. `--providers` and a question's own provider list still narrow it.

Before: `judge visual-critique` with Jev's key set asks `claude-cli`; `"provider": "claude-cli"` on the decision row.
After: the same call asks Jev; `"provider": "jev"` when Jev says `looks-right`, and `claude-cli` when Jev is unavailable, errored,
below threshold, or says `looks-off` / `sloppy` (next section).

**Jev can't give reasons, and visual-critique wants them.** Today `JevProvider.Refused` refuses any question that asks for `reasons`
(`JevProvider.cs:92-94`), and visual-critique does (`VisualCritiqueQuestion.cs:39`). The judge already has the mechanism for "some
answers need reasons": `ReasonedOutputs` (resolution-check sets `partial`/`unresolved`), and an answer in that set without reasons is
skipped like a failed one, so the next provider runs (`QuestionDefinition.LacksReasons`). So:

- visual-critique sets `ReasonedOutputs = {looks-off, sloppy}`. `looks-right` needs no reasons (callers act only on the problems).
- `Refused` stays as it is for every other question. visual-critique opts in with a new definition flag, `JevMaySettle` (carried
  on `QuestionRef`), and `Refused` lets Jev through only when it's set. It has to be opt-in: resolution-check also asks for reasons
  and its `resolved` needs none, so loosening `Refused` for everyone would move every `/bugFixOrchestrator` resolution check, with
  no images at all, onto Jev. With the flag, resolution-check's chain is today's (golden in PR 1b).
- A reasonless `looks-off` from Jev is stored as a skip with the reason `no-reasons`, not as a failure, so `judge health` doesn't
  count Jev as failing every time it spots a problem. That's a new branch: today a reasonless answer goes through `Step` as
  `NoReasons` (`QuestionEvaluator.cs:200-210`), writes a failure row and sets `HadRealFailure`, so a chain that runs out ends
  `Failed`. For a `JevMaySettle` question, Jev's reasonless answer writes no failure row, skips with a non-failing reason, and a
  chain that runs out after it ends `Escalated` as for any other skip.
- `SkippedProvider` (the trace entry) gains a nullable reason code. Today it holds only an enum, which can't say "images off" or
  "413"; every `jev skipped:` line in 4 and every count in 4a reads from that code.
- Net: Jev settles the common case, a page that looks right, in one fast call. A page with problems goes to the CLI, which says what
  the problems are. Jev's skipped answer is in the trace, so `judge why` shows `jev skipped: jev said looks-off with no reasons; asked claude-cli`.

Before / after on a 10-frame ui-evidence run where 8 frames look right: today 10 CLI calls; after, 10 Jev calls and 2 CLI calls.

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
| `providers.jevImages: false` in config.json | `ProviderUnavailable("images-off")` | the CLIs |
| Jev answers `413` | `ProviderUnavailable("image-too-large")` | the CLIs |
| Jev answers `400` on an image call | `ProviderErrored("http-400")` as today | the CLIs |

That table is for strict callers (Sindri's Step, a person, ui-evidence). A hook call (`imagesBestEffort`) never moves on for these:
it drops the image and stays on Jev (2).

The common case this hits: ui-evidence's default viewport is 1512x982 (`src/Evidence/Aw.Evidence.Core/Design/DesignManifest.cs:10`),
which fits; the same page at device pixel ratio 2 is 3024x1964, which doesn't, and goes to the CLIs as it does today. Downscaling
those is the owner question in 9.

### 3.4 Images leave the machine

Jev is a third party. Text gets redacted before it goes; a screenshot can't be. So:

- Only files the caller named in `images` (or visual-critique's two fields) are ever sent. The judge never looks for images itself.
- config.json `providers.jevImages` (default `true`, owner answer 1, unless PR 1b ships it `false` for one of three reasons: no Jev
  key on the lead's box to run the eval, Jev missing the bar in 8, or TypeSafe's terms allowing training or unclear) turns image sending off without turning Jev off: Jev keeps
  answering text calls, and image calls skip it (`images-off`). It's a sibling of `providers.jev`, which is already a boolean
  (`ProvidersConfig.cs:11`); `jev: false` drops Jev from every chain, images included. **This is the rollback** for the visual-critique
  move: `judge config set providers jev-images false` puts every image call back on the CLIs, no deploy.
- **Hook-sourced images stay inside the repo.** A hook attaches a path only when it resolves (symlinks followed) under the checkout
  the session runs in (`git rev-parse --show-toplevel` of the hook's cwd). A screenshot the agent opened from `~/Downloads` or `/tmp`
  is never sent. Hook calls also cap the total at 4 MiB: most hook budgets are 10 s per provider (`AskCheckQuestion.cs:34`,
  `WakeGateQuestion.cs:40`, `BriefScopeQuestion.cs:42`), and 4 MiB is about 2 s up at 20 Mbit/s. PR 4 measures one ask-check with
  4 MiB attached and puts the seconds in its body. Every path a hook leaves out (outside the repo, over 4, over 4 MiB) is counted
  on the hook's `probe-log` line, so nothing is filtered silently.
- **Jev's data terms decide the default.** PR 1b records in `docs/csharp/judge.md` what TypeSafe's terms say about keeping or
  training on request content, with the link, read on the day it merges. If they allow training on it, or say nothing findable,
  `jevImages` ships `false` and the owner decides with the terms in front of them.
- **Off means off for hooks.** Turning Jev images off (config or env) moves an explicit caller's images to the CLIs, as today. A
  hook call (`imagesBestEffort`) instead drops its images and goes as text: hooks never sent images before this work, so a repo that
  turns images off gets exactly today's hook calls and no screenshot goes to any vendor.
- **Off (or on) for one repo, or one shell.** `AW_JUDGE_JEV_IMAGES=0` or `=1` in the environment wins over config.json (hooks and Sindri's judge
  child inherit it), so a repo whose screenshots show customer data turns it off in its own env (direnv, the lane's launch env)
  without touching the box's config. Listed in `.env.example`.
- **Step images stay in known places.** `step.judge.choose` images must resolve (symlinks followed) under the item's checkout or
  `$AW_STATE_DIR`'s ui-evidence output; anything else fails the Step before the call, with the reason
  `images: /…/a.png (from fact frames) is outside the checkout and the ui-evidence output`.
- **Nothing logs an image.** Debug logs name images by reference (`aw-image-1-…`) and size, never bytes or source paths; the
  multipart parts' `filename` is the reference.
- **Fixtures are synthetic**: the 20 labeled screenshots are rendered from small HTML pages in the fixture directory, never captures
  of a real app.
- `judge eval` and the label replays send images only when `jevImages` is on, like a live call.
- Today `judge config set` takes only `<question> enabled|threshold`. PR 1 adds one form, `judge config set providers jev-images
  true|false`, so turning it off is one pasted command, not a hand edit. It prints the saved config like the other forms; a value
  other than `true`/`false` is usage, exit 64. `judge config get` already prints the whole config.

### 3.5 The agent CLIs

`AgentCliProvider` takes its image path today when the question's class is Image and reads `afterScreenshot`/`evidenceDir` from
the input (`AgentCliProvider.cs:36-41`). It changes to: take the image path when `QuestionRef.Images` is non-empty, and build its
evidence from the attachments, not from the input's fields. visual-critique's attachments are its two screenshots, so its CLI calls
see the same files in the same order as today, read from the per-call copies below (golden: the argv differs only in the directory).

They take images today through `ImageEvidence` (`src/Judge/Aw.Judge.Infrastructure/Providers/ImageEvidence.cs`), which needs every
screenshot inside one `evidenceDir`: Claude's Read tool is scoped to it, Cursor's workspace is it. Generic `images` can be anywhere,
so for every call with images (visual-critique included) an Infrastructure `ImageWorkspace` (PR 1a), created lazily by the first
CLI provider that needs it (Jev settles most calls, so most calls never write one) and disposed by the evaluator after the last
provider, writes the read bytes into a fresh per-call directory (`$TMPDIR/aw-judge-images-<random>/`,
mode 0700, visual-critique's files under their own relative names (`home.png`, `base/home.png`, so the prompt's "Look at the screenshot
home.png" still names a real file), generic images as `image-1.png`, …; a workspace that can't be created errors that CLI provider
(`image-workspace`) and the chain moves on; removed in a `finally` after the call, and any `aw-judge-images-*` directory older than an hour and
owned by the current user swept at the next call's start (symlinks not followed), which
catches a judge killed by a hook's timeout) and hands the CLIs that directory as their `evidenceDir`. Same bytes Jev saw, same scoping.
Because visual-critique's CLI calls now read these copies too, Jev and the CLI always judge the same bytes. Its CLI prompt and
argv differ from today only in the directory (golden). For a generic call the CLI prompt gets one more line naming the files in
order ("The images are image-1.png, image-2.png in this directory, in the order the question lists them"), since only
visual-critique's own prompt names its screenshots.

### 3.6 The batch classifier (prompt-sort, trait-check)

`prompt-sort` and `trait-check` don't go through the evaluator: they ask several Jev questions in one call through `IBatchClassifier`
(`SortPromptService.cs:18`, `JevTraitJudge.cs:13`). `BatchRequest` gains `Images` the same way `QuestionRef` does, read by the same
`IImageReader`, and `JevProvider.Batch` sends the same multipart request (one `assets` map for the whole batch, the state's `images`
holding the references once). A batch has no CLI fallback today, so an image the batch can't send (out of limits, `jevImages` off,
`413`) is dropped from that batch with a trace line and the batch goes as text: the hook still gets its answer, just without the
picture. A size limit Jev only reports after the upload (`413`, or `400` on an image batch) gets one retry without the images when
at least half the hook's budget is left, inside `JevProvider.Batch`'s own deadline, else the batch counts as unanswered and the hook fails open as it does today on a Jev
timeout. The drop is recorded on the batch's own record (the sort or trait-check row), never on stderr, which the hook contract
freezes. For `trait-check`, a question that was about an image file (an edited PNG) is not asked as text when its image was dropped:
that file goes in trait-check's existing skipped list with the reason `image-not-sent` (no new stdout field), never judged on its name alone. `prompt-sort` asks about the prompt, so its answer on
text alone is still an answer. With no images, the batch request is byte-identical (golden).

## 4. The decision row

The stored input holds images as `{ "path", "sha256", "bytes", "width", "height" }` in place of each path, never the bytes. Example:

```json
{ "transcriptTail": "…", "images": [{ "path": "/home/u/r/ui-evidence/home.png", "sha256": "3fa9c1d2…", "bytes": 412337, "width": 1512, "height": 982 }] }
```

The row's `provider` and `reason` work as today (`jev` / `model`, or the fallback with Jev's reason in the trace). `judge why <id>`
adds one line per image and, when Jev was skipped for its images, one line saying why in words:

```
images: 2
  1. /home/u/r/ui-evidence/home.png  1512x982  403 KB  sha256 3fa9c1d2e8b0
  2. /home/u/r/ui-evidence/home@2x.png  3024x1964  1.2 MB  sha256 77c01e9a4b12
jev skipped: image 2 is 3024x1964, over Jev's 1568 px limit; asked claude-cli
```

This is the one list of `jev skipped:` lines (each ends `; asked <next provider>`):

| When | Line |
|---|---|
| An image outside Jev's limits | `image N is WxH, over Jev's 1568 px limit` / `under Jev's 280 px minimum` / `image N is WxH, over Jev's 2,458,624-pixel limit` / `over Jev's 16 MiB limit` |
| Images off | `image sending to Jev is off (judge config set providers jev-images true)` |
| No key | `no Jev key (TYPESAFE_API_KEY or the keychain)` |
| Jev answered a problem | `jev said looks-off with no reasons` (or `sloppy`) |
| Jev below threshold | `jev said looks-right at 0.41, under 0.5` |
| `413` | `Jev said the images were too large (413)` |
| `400` | `Jev refused the request (400); AW_LOG_LEVEL=debug shows its body` |

A hook call that dropped an image for Jev (2) prints a `jev images dropped:` line instead, since Jev still answered:
`jev images dropped: image 1 is 3024x1964, over Jev's 1568 px limit; asked jev as text` (or `...; asked jev with images 2, 3`).
After a `413` / `400` retry it reads `jev images dropped: Jev said the images were too large (413); asked jev again as text`.

A call without images prints exactly today's `why`. ui-evidence caches visual-critique verdicts by image hashes, prompt version and model (`VerdictCacheKey`); it can't know the
provider before the call, so PR 1b bumps its `CurrentPromptVersion` to `visual-critique/v2`: no CLI-era verdict is reused as a
Jev one. The key also gets whether Jev images are on (ui-evidence reads it once per run from `judge config get`, env included), so a
`jev-images false` rollback misses every Jev-era entry instead of replaying it. `judge eval` and the label replays read `path` from each stored image object and re-read the file: a file that is gone or whose sha256 changed makes that
case `skipped: image-changed`, not a failure.

## 4a. Health

`judge health` gets one line once any call had images, counting every `jev skipped:` reason from 4:
`images: 14 calls, jev 9, clis 5 (looks-off 2, out-of-limits 1, too-large 1, http-400 1), dropped for jev 3` (the last count is hook
calls that went to Jev with fewer images, 2); with sending off it reads
`images: 14 calls, jev 0, clis 14 (images-off 14)`. A sudden run of `http-400` is how a change on Jev's experimental API shows. Nothing
when there were none, so its output on a box that never sends images is today's. That's the number that answers whether retina
frames make the Jev path rare (9).

## 5. Callers

### 5.1 `choose` and Sindri's `step.judge.choose`

- `choose` takes `images` like every question (2).
- `ChooseRequest` (`src/Sindri/Aw.Sindri.Core/Judge/ChooseRequest.cs:9`) gains `IReadOnlyList<string> Images` (default empty), and
  `JudgeCliChoice` writes it into the `choose` input only when non-empty, so every recipe today sends the same bytes.
- A recipe's `step.judge.choose` state gets an optional `images: <fact key>`: a fact whose value is a list of absolute paths (what
  `ui-evidence run` already reports for its frames). Recipe validation: the key must be one of the state's readable facts
  (`RCP-CHOICE-007`, "`images` names fact `<key>`, which this state can't read"). The Step resolves the list, keeps the first 4,
  and fails the Step with `step_failed` when a path is missing, before any judge call, with the reason
  `images: /…/home.png (from fact frames) does not exist`. When it dropped some, `fact:decision` gets `imagesDropped: <n>`.
- No shipped recipe uses it in this work. The proof is an eval case on a fixture recipe plus the integration test in 8.
- The Step's `fact:decision` gets `imageCount: <n>` (only when n > 0, so today's facts don't change). The app's Jobs view shows it on
  the Step's decision line: "decided with 1 image" / "decided with 2 images", nothing at 0, and "decided with 4 images
  (2 left out)" when `imagesDropped` is set. A batch's dropped images are on its own row only; batches have no `judge why`. That's in PR 3, with its UI goldens.

### 5.2 The hooks (owner answer 4)

Every hook question takes `images`. What each hook attaches, at most 4, newest first:

| Hook | Question | Images it attaches |
|---|---|---|
| `done-gate.sh` | `ask-check` | PNG/JPEG files the last assistant turn opened with `Read` (from `transcript_path`, the same tail it reads now). Codex and Cursor send no `transcript_path`, so none there |
| `scope-gate.sh` | `brief-scope` | absolute paths ending `.png`/`.jpg`/`.jpeg` named in the brief's goal or acceptance criteria that exist |
| `trait-check.sh` | `trait-check` | nothing new in the hook: it already passes every edited file as `--file`, and today the judge skips a PNG/JPEG one as binary (`ITraitFiles`). The new judge reads an edited PNG/JPEG `--file` as an image instead, under the same rules as the helper below (inside the checkout of its cwd, 4 files, 4 MiB, best effort), and records what it left out on its own row (3.6) |
| `send-gate.sh` | `wake-gate` | absolute PNG/JPEG paths named in the message text that exist (a lead sending "see /…/home.png") |
| `prompt-sort.sh` | `prompt-sort` | absolute PNG/JPEG paths named in the prompt that exist. An image pasted into Claude Code reaches the hook as `[Image #1]` with no path, so it can't be attached |

So all five hooks that ask the judge can attach images (owner answer 4), and none gets a new argument (2). The three text sources share one helper,
`config/lib/image-paths.sh` (`aw_image_paths <text>`: absolute paths ending `.png`/`.jpg`/`.jpeg`, existing files only, deduped,
newest first, keeping paths under the session's checkout only, up to 4 files and 4 MiB, then counting what it left out for
`probe-log`, along with the count it sent; sizes by `wc -c`, which reads the same on macOS and Linux), so the rule is written once. done-gate's newest is the last `Read` in the turn; for text sources it's the order they
appear, last first. Every hook input with images also sets `imagesBestEffort: true` (2).

The hook contract stays frozen (AGENTS.md "hook contract"): no new exit codes, stderr prefixes or stdout fields. A hook adds `images`
to its `jq -nc` input only when it found at least one (more than 4: the newest 4, and a `probe-log` line with the dropped count), so `ASK_INPUT` for a turn with no image reads is exactly today's string.

## 6. Byte-identical for old callers

The rule: a call with no `images` (absent or `[]`) is today's call, end to end. The checks, each in the PR that could break it:

1. **Jev wire golden** (PR 1): `JevProvider` against a recording `HttpMessageHandler`, one case per question, no images: the request
   body bytes and headers equal a golden captured on PR 1's base commit before any change (the red commit captures it). With images: the multipart golden (parts, names,
   header, references in the state).
2. **Decision row golden** (PR 2): the stored input for every question without images equals today's.
3. **Hook surface** (PR 4): `config/lib/tests/judge-cs-hook-surface.test.sh` passes unchanged against the new judge, plus new cases
   with an image (done-gate after a `Read` of a PNG fixture: the input carries `images`, the hook's exit and stderr are unchanged).
4. **Sindri** (PR 3): `JudgeCliChoice`'s argv and stdin for a request with no images equal today's (golden).

## 7. PRs

Red first in each; ideas reviews each; "Part of #1961" except the last, which says "Fixes #1961".

| PR | What | Red test (fails before, names the behavior) | Gate |
|---|---|---|---|
| 1a | The plumbing, no behavior change: `ImageAttachment` + `IImageReader` + limits, `QuestionRef.Images`, `hasImages` routing, the CLIs building evidence from attachments, visual-critique's `afterScreenshot`/`baselineScreenshot` read into attachments, `ImageWorkspace` (generic `images` waits for PR 2; visual-critique still on the CLIs) | `VisualCritique_cli_sees_the_same_screenshots_in_the_same_order`; `ImageReader_reads_png_and_jpeg_sizes_from_headers` | Judge unit + integration, Arch, PublicAPI |
| 1b | Jev takes images: `JevProvider` multipart + header + references, `JevMaySettle` (set by visual-critique only; `Refused` unchanged otherwise), visual-critique's `ReasonedOutputs`, the `SkippedProvider` reason code, `Classes = All`, image chain `jev, CLIs, rules`, `providers.jevImages` + `AW_JUDGE_JEV_IMAGES`, the 20 labeled cases (embedded), `judge check images` | `VisualCritique_looks_right_is_settled_by_jev_from_multipart_screenshots`; `CheckImages_prints_both_scores_and_the_flip_command_when_the_bar_holds`; `VisualCritique_looks_off_from_jev_goes_to_the_cli_for_reasons`; `ResolutionCheck_still_refuses_jev`; `Image_over_1568px_skips_jev_for_the_clis`; the no-image Jev wire golden | Judge unit + integration, `judge-cs-hook-surface`, judge aot-smoke, Arch (new type in Core), PublicAPI |
| 2 | Any question takes `images` (and `imagesBestEffort`, both stripped before the question's record): the evaluator reads and strips it, routes on the image class, CLIs get the per-call directory, the decision row's image lines, `judge why`, eval's `image-changed` | `AskCheck_with_an_image_routes_to_jev_and_stores_its_hash_not_its_bytes`; `Input_without_images_stores_todays_row`; `Best_effort_drops_a_bad_image_and_answers_as_text`; `Strict_bad_image_escalates_invalid_images`; `Best_effort_image_over_jev_limits_is_dropped_and_jev_answers_as_text`; `Best_effort_413_retries_jev_once_as_text` | Judge unit + integration, hook surface, aot-smoke |
| 3 | Sindri: `ChooseRequest.Images`, `step.judge.choose` `images:`, `RCP-CHOICE-007`, `imageCount` in `fact:decision`, the Jobs view line | `JudgeChoose_passes_the_named_fact_as_images`; `JudgeCliChoice_without_images_sends_todays_input`; `Jobs_view_says_decided_with_2_images` | Sindri unit + integration (ApiGoldens for the fact), Ui unit + integration, recipe eval, Arch |
| 4 | Batch images (`BatchRequest.Images`, `JevProvider.Batch` multipart, drop-and-trace) and the hooks: all five sources (done-gate, scope-gate, send-gate, prompt-sort, and trait-check reading an edited PNG `--file` as an image), `config/lib/image-paths.sh`, docs (`docs/csharp/judge.md`, `config/hooks` README) | `done-gate` bash case: a turn that read a PNG sends `images`; `done-gate` bash case: a turn that read a 3024x1964 PNG gets its answer from `jev` with a dropped-image note; `TraitCheck_edited_png_file_is_sent_as_an_image`; trait-check.sh's argv for an edited PNG is byte-identical to today's | every hook bash test, hook surface, sync-rules |

Each PR updates `docs/csharp/judge.md` (and `docs/csharp/sindri-workflow.md` for PR 3) for what it ships, so no PR lands an
undocumented field.

PR 1 is split in two so the plumbing (1a) can land and be rolled back apart from the behavior change (1b). Every PR that changes usage text (`config set providers jev-images` and `check images` in 1b) runs the whole Judge unit lane and judge aot-smoke, and updates its `expected.txt`.

PR 1b's gate adds `scripts/check-env-example.sh` (`AW_JUDGE_JEV_IMAGES`). The evaluator strips `imagesBestEffort` with `images`
(PR 2). PR 4's red tests include a `.png` that isn't a PNG from a hook: dropped, the call answers as text. PR 4 also runs one hook
input with `images` against the judge from PR 2's base (the pre-change build) to prove an older judge ignores it.

PR 4's red tests include the helper's refusals: a path outside the checkout, a symlink that escapes it, five images (keeps 4),
and 6 MiB of images (keeps what fits 4 MiB), each counted on the `probe-log` line.

**Parallel plan.** PR 1a, then 1b (everything rides on the provider and chain). Then PR 2. Then PR 3 and PR 4 at once: PR 3 is
Sindri-only; PR 4 is the batch classifier (Judge Infrastructure, which PR 3 doesn't touch) plus the hooks. Builders: one per PR (Sonnet), the lane lead runs every gate.

## 8. Proof

- **Jev against the CLI on the same screenshots (PR 1b).** Today Jev never judges a picture, so moving visual-critique to Jev
  first needs evidence. PR 1b adds 20 labeled visual-critique cases under `tests/fixtures/judge/images/` (10 that look right,
  10 with a visible break: overlap, clipped text, missing element, wrong color, cut-off content). The error that costs is Jev saying
  `looks-right` on a broken page, because nothing after it looks again; a wrong `looks-off` only costs a CLI call. So the bar is:
  **zero `looks-right` on the 10 broken cases**, plus seconds per call for both providers.
  - Order: the lead runs `judge check images` (below) before ready when this box has the Jev key (`judge health` shows
    `jev: available`), and its output goes in the PR body. If the bar holds, `jevImages` ships `true`; if not, `false`, and the
    numbers go to the owner.
  - No key on the box: PR 1b ships `jevImages` default `false`, and the owner runs the same one command; a one-line PR then moves
    the default if the bar held.

- **`judge check images` (PR 1b): the whole check in one command.** It runs from any directory, writes nothing to config.json, and
  does, in order:
  1. Reads the Jev key the way a live call does. None: prints `no Jev key: judge check images needs one` and exits 1.
  2. Unpacks the 20 cases, which are embedded in the binary (so the installed `judge` doesn't need a checkout), into a temp
     directory and fills each case's `evidenceDir` with it (the field must be absolute). Budget: the 20 PNGs are rendered from small HTML
     pages, about 50 KB each, so they add about 1 MB to the NativeAOT judge; PR 1b's body states the real total, and over 2 MB is
     a finding.
  3. Runs one live `visual-critique` on a looks-right case with images forced on for this run only (as `AW_JUDGE_JEV_IMAGES=1`
     does), and prints its decision, provider and decision id; a provider other than `jev` prints the `jev skipped:` reason from 4.
  4. Runs `judge eval visual-critique` on the 20 cases twice, `--provider jev` (images forced on) and `--provider claude-cli`, one
     line per case as it goes.
  5. Prints the result and removes the temp directory.

  What the owner sees, with the bar held and sending off (exit 0):

  ```
  live call:  looks-right  provider jev  (judge why dec_8f2c…)
  jev:        broken judged looks-right 0/10 · right pages judged looks-right 9/10 · 1.8 s per call
  claude-cli: broken judged looks-right 0/10 · right pages judged looks-right 10/10 · 21.4 s per call
  bar (0 broken judged looks-right): held
  images to Jev: off
  to turn it on: judge config set providers jev-images true
  ```

  Bar missed: `bar ...: missed (2/10)`, exit 1, and with sending on the last line is the `false` command instead. Bar held with
  sending already on: no command line. Exit 0 bar held, 1 bar missed or no Jev key, 64 usage. It spends 41 model calls (one live,
  20 per eval); the CLI eval runs on the owner's own `claude` login, so it takes minutes, not seconds.

- In tests: the goldens in 6 and the red tests in 7, on a recording handler (no network in CI). `judge check images` gets its own
  red test on a recording Jev and a stub CLI, `CheckImages_prints_both_scores_and_the_flip_command_when_the_bar_holds`, and its
  usage line goes in judge aot-smoke's `expected.txt`.
- On the owner's box, once, after PR 1b (one item on the owner checklist, needs the Jev key): `judge check images` → you should see
  `bar (0 broken judged looks-right): held` and, if sending is off, the exact command to turn it on.
- PR 3 adds no owner step. Its proof is an integration test that runs a fixture recipe whose `step.judge.choose` names two frames
  through a real control plane on a temp home with a recording Jev: the Jobs view shows "decided with 2 images" and the ledger's
  `step.finished` carries `provider: jev`. Jev itself reading pictures is what `judge check images` already proved; PR 3 only
  carries them there.

## 9. Owner question

1. **Big screenshots.** A retina screenshot (3024x1964) is over Jev's 1568 px limit, and Jev won't resize. My pick: send it to the
   CLIs, as happens today (3.3); no new dependency. The other way: the judge downscales to fit, which adds SkiaSharp (MIT) to the
   NativeAOT judge (~8 MB of native library per platform) and changes the picture Jev judges. Go with sending it to the CLIs?
