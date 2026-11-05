# Eval stubs

`evals/stubs/bin/` goes first on PATH for the agent's run only (`scripts/skill-evals.sh` sets it). `fixture.sh` and `--selftest` use
the real tools. The stubs guard against honest mistakes, not a determined agent.

## `git` (engine's 0c) and `gh`

Under `AW_GIT_MODE=readonly` (the staged copy of these files defaults to it) a write is refused: exit 1 and, on stderr,
`aw-eval: <tool> <sub> refused (AW_GIT_MODE=readonly)`.

`gh` refuses `pr create|merge|comment|review|edit|close`, `issue create|comment`, and `api` with `-X`/`--method` POST, PUT, PATCH or
DELETE in any spelling (`-XPOST`, `--method=post`), and `api` with `-f`/`-F`/`--field`/`--raw-field`/`--input` and no method (gh then
POSTs). `api -X GET -f q=x` is a read and goes through. `-R`/`--repo` before or after the subcommand never hides a write.

## Canned answers

The agent has no gh token, so a case's scaffold writes `.aw-eval/gh.json` and/or `.aw-eval/curl.json` at the workspace root (the
injected scaffold always creates `.aw-eval/`). A stub finds the file by walking up from `$PWD` to the first directory holding
`.aw-eval/`, so `cd sub && gh pr view 3` still finds it. The file is a JSON array; the first matching entry wins:

    [{"match": ["pr","view","3","--json","title,state"], "stdout": "{\"title\":\"x\",\"state\":\"OPEN\"}", "stderr": "", "exit": 0}]

`stdout`, `stderr` and `exit` default to `""`, `""` and `0`.

**gh matching.** Order: readonly refusal, then the canned file, then the real `gh`.
1. `-R X`, `--repo X` and `--repo=X` are stripped from the call wherever they sit.
2. The leading items of `match` without a `-` (`"pr","view","3"`) must equal the call's leading words without a `-`, in order.
3. Every flag item in `match` (`"--json"`, `"--state=open"`, or a flag followed by its value as the next item) must appear in the
   call, in any position.
4. Comma-separated values compare as sets: `--json a,b` matches `--json b,a`.
5. Flags in the call that `match` does not name are ignored.

No match while the file exists: stderr `aw-eval: gh <args> has no canned answer`, exit 1. No file: the call goes to the real `gh`.

**curl.** `match` is `["<METHOD>","<url prefix>"]` (a single item is a URL prefix with method GET). The call's method is `-X`/
`--request`, or POST when `-d`/`--data*`/`-F`/`--form` is given, else GET (`-I` is HEAD). The entry's stdout is printed whatever
`-o` or `-w` say. No match, or no file: stderr `aw-eval: curl <METHOD> <url> has no canned answer`, exit 7. curl never touches the network.

## `ui-evidence`

A canned stand-in for the Sindri binary, which an isolated run does not have, so the skill's `command -v ui-evidence` preflight passes.
`doctor` answers a healthy local stack on `http://localhost:3000`, `provenance` answers `seeded`, `install-browser`, `run`, `parity`,
`api-run` and `db-run` answer a passed run with one screenshot, `publish-plan` a text-only plan and anything else `{}`. A `--host` that
does not start with `http://localhost:` is refused like the real guard: exit 1 and, on stderr, `ui-evidence: host refused: <host> is not a
configured local host`. It writes no file and posts nothing, so a case grades the order of calls and the refusals, not the tool.

## system-design-video: `bun`, `ffmpeg`, `ffprobe`

Fakes for the render tool chain, so a run reaches the skill's playback check. They print and write what the real tools would and
never say they are fakes (in a run that read "stub frames" and a "canned stand-in" header, the agent stopped before
`playback-check.sh`: 2 of 3 runs, 2026-11-05). Their docs live here, not in the scripts, for the same reason.

- `bun <dir>/render.ts <storyboard.json> <out-dir> --tokens <tokens.json>` writes `<out-dir>/footage.mp4` (an `ftyp` header and
  2.4 MB of random bytes), `layout.json` (23 s per storyboard beat) and `resolved-inks.json`; `--still t1,t2` writes
  `<out-dir>/stills/t<sec>.png` from `media/<beat id>.png` instead. `bun -e '<script>'` (review.sh's beat index) prints one
  `NN id seconds` line per beat of `./layout.json`. `bun --version` answers; anything else exits 1.
- `ffmpeg` fails on a missing `-i` input. To `-` it answers `-f null` (clean unless the answer file sets `decode_error`) and
  `-f rawvideo` (the answer file's `corner_hex`). To a file it copies `media/`: `NN-<beat>.jpg` gets `<beat>.jpg`, `sheet-N.jpg`
  gets `sheet.jpg`, an `.mp4` gets a header and random bytes (600 kB when scaled to 720p, else 1.6 MB).
- `ffprobe` fails on a missing file and answers the three call shapes the skill's scripts make from the answer file; a file
  named `*720p*` reports 1280x720.

The answer file is the first found of `$AW_STUB_FFPROBE_JSON`, the nearest `ffprobe.json` upward from `$PWD` (a case's fixture
plants its failure there) and `evals/stubs/ffprobe.json`. `media/` holds the pet-sitter case's two beats (`model`, `rollout`) as
960x540 JPEG and PNG on its board color, and a 2x2 contact sheet; an unknown beat gets `model`.

## Files

`aw-eval-lib.sh` (the walk-up and the real-binary lookup) and `aw-eval-match.pl` (the matcher) are sourced or called by the stubs.
