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

## Files

`aw-eval-lib.sh` (the walk-up and the real-binary lookup) and `aw-eval-match.pl` (the matcher) are sourced or called by the stubs.
