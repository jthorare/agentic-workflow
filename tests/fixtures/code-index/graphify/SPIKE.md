# CI-0d: the graphify spike

Spec: `docs/superpowers/specs/2026-10-19-sindri-code-index-design.md` §5.5 and §15 Q1. Run on 2026-10-20.

## Setup

- graphify 0.9.67 (`graphify --version`), installed as `context/code-index.lock` describes: a venv from `context/code-index-requirements.txt` with
  `uv pip install --require-hashes --exclude-newer 2026-09-24T00:00:00Z`.
- Command, as the TS index runs it (`sindri/src/index/graph.ts`): `graphify extract <snapshot> --code-only --no-viz`, inside `bwrap --unshare-all --new-session
  --die-with-parent --ro-bind / / --dev /dev --proc /proc --tmpfs /tmp --tmpfs /run`, the credential dirs masked, only the snapshot bound writable, a cleaned
  environment, and `TMPDIR`, `XDG_CACHE_HOME`, `UV_CACHE_DIR`, `PYTHONPYCACHEPREFIX` in a private `.sindri-tmp-*` dir inside the snapshot. No network (`--unshare-all`).
- Fixtures: `typescript/` (3 files: free functions, a class with methods, an import chain) and `python/` (3 files: functions, a class, `self.` calls, a call through a
  local variable). Known calls are in `expected-calls.json`. The recorded outputs are `typescript/graph.json.recorded` and `python/graph.json.recorded`; the table
  below is `node check-expected.mjs`.
- graphify exits 0 and prints progress lines on stderr. A 3-file fixture takes well under a second. `graph.json` is 10 KB and 7.5 KB.

## What graph.json is

NetworkX node-link JSON: `{"directed": false, "multigraph": false, "graph": {}, "hyperedges": [], "nodes": [...], "links": [...]}`. The edge list is `links`, not `edges`.
`hyperedges` was empty in both runs.

**Node fields** (all present on every node unless noted): `id` (string, `<path-without-extension>_<name>` lowercased, underscores), `label`, `norm_label`, `file_type`
(`"code"`), `source_file`, `source_location`, `community` (integer), `_origin` (`"ast"`), `_callable` (true on functions, methods and classes; absent on file nodes),
`_callable_class` (true on classes only).

- `source_file` is **relative to the snapshot root**, with `/` separators (the TS code assumes absolute paths and strips the snapshot prefix; both forms should be accepted).
- `source_location` is the string `"L<n>"`: the **start line only**. There is no end line.
- There is no `kind` field. Kinds are derived: a **file node** has a `label` equal to the file name and no `_callable`; a **class** has `_callable_class`; a **method** is the
  target of a `method` edge (label `.name()`); a **function** is any other `_callable` node (label `name()`).

**Edge fields**: `source`, `target`, `relation`, `confidence`, `confidence_score`, `weight` (1.0), `source_file`, `source_location` (`"L<n>"`), `_origin`, and `context`
(`"call"` on `calls`, `"import"` on `imports` and `imports_from`; absent on `contains` and `method`).

| relation | from -> to | seen |
|---|---|---|
| `calls` | caller -> callee (a function, a method, or a class for `new X()` / `X()`) | TS 9, Py 4 |
| `contains` | file -> its top-level function or class | TS 6, Py 4 |
| `method` | class -> its method | TS 3, Py 4 |
| `imports` | file -> the imported symbol | TS 3, Py 1 |
| `imports_from` | file -> the imported file | TS 2, Py 1 |

`confidence` was `EXTRACTED` (score 1.0) or `INFERRED` (score 0.8); no other word appeared. On a `calls` edge, `source_file` and `source_location` are the **call site**
(in the caller's file). The graph is declared undirected but every edge kept its direction: `source` is the caller on all 13 `calls` edges.

## Call edges: expected and found

| Language | Expected call | Found | Edge confidence | Call site | Both ends file+line |
|---|---|---|---|---|---|
| TS | `square` -> `multiply` | yes | EXTRACTED | src/math.ts:L6 | yes |
| TS | `multiply` -> `add` | yes | EXTRACTED | src/math.ts:L11 | yes |
| TS | `Report.push` -> `square` (cross-file) | yes | EXTRACTED | src/report.ts:L7 | yes |
| TS | `Report.total` -> `add` (cross-file, inside a lambda) | yes | EXTRACTED | src/report.ts:L11 | yes |
| TS | `Report.render` -> `Report.total` (`this.`) | yes | EXTRACTED | src/report.ts:L15 | yes |
| TS | `Report.render` -> `format` | yes | EXTRACTED | src/report.ts:L15 | yes |
| TS | `run` -> `Report.push` (through a local `new Report()`) | yes | INFERRED | src/main.ts:L5 | yes |
| TS | `run` -> `Report.render` (through a local) | yes | INFERRED | src/main.ts:L6 | yes |
| Py | `normalize` -> `strip_marks` | yes | EXTRACTED | pkg/textutil.py:L2 | yes |
| Py | `WordCounter.add` -> `normalize` (cross-file import) | yes | EXTRACTED | pkg/counter.py:L9 | yes |
| Py | `WordCounter.add` -> `WordCounter.split` (`self.`) | yes | EXTRACTED | pkg/counter.py:L9 | yes |
| Py | `count_all` -> `WordCounter.add` (through a local) | **no** | | | |
| Py | `count_all` -> `WordCounter.top` (through a local) | **no** | | | |

**Totals: TypeScript 8 of 8, Python 3 of 5.** Beyond the expected set graphify also emitted `run` -> `Report` (the `new Report()`) in TS and `count_all` -> `WordCounter` in
Python: a `calls` edge whose target is a class means construction. Python's two misses are both a method called on a local variable assigned from a constructor
(`counter = WordCounter(); counter.add(...)`): TypeScript resolved the same pattern (as INFERRED), Python did not. Plain functions, imports and `self.` calls resolved.

"Both ends carry a file and line" holds for every found edge, read from the two nodes: each has `source_file` and `source_location`.

## Verdict: PASS

The pinned graphify emits call edges for the TypeScript fixture: all 8 known calls are `calls` edges and both ends carry a file and line. TypeScript callers and callees
therefore stay available from graphify, and **Q1 needs no owner decision**. Caveats that do not change the verdict:

- Two of the eight TS edges are `INFERRED` (0.8): the call goes through a local variable. They are right here; the store should keep the confidence word.
- Python receivers through locals are not resolved (3/5). Python callers/callees from graphify are partial; the parsers do not cover Python (§5.3), so for Python the layer
  answers what graphify found and the answer should carry `basis = graphify` so a reader can see its source.
- The fixture is small (TypeScript: 9 `calls` edges, 8 of them expected; Python: 4 `calls` edges against 5 expected, 3 of them found). It proves the shape and that call edges exist, not their recall on a real repo.

## Findings CI-7 and the spec must absorb

1. **The graphify environment is unreadable inside the TS sandbox.** The TS sandbox masks `~/.agentic-workflow` (its credential list) with a tmpfs. The lock puts the venv
   under `$AW_STATE_DIR/cache/code-index/` (`~/.agentic-workflow/cache/...` by default), so inside that sandbox `bwrap` fails with `execvp .../bin/graphify: No such file or
   directory` (reproduced). §5.5 and §9 need one of: keep the environment outside the masked path, or bind the environment read-only after the mask. The spike ran with
   that one mask removed.
2. **`source_file` is relative**, not absolute as `sindri/src/index/graph.ts` assumed.
3. **The private dir inside the snapshot is scanned** (graphify listed 98 `.pyc` files from `PYTHONPYCACHEPREFIX` as "not classified" and skipped them). Harmless, but it is
   work and stderr noise; a private dir outside the scanned tree, or none for pycache, is cleaner.
4. **No end line, no kind field, no signature.** A graphify node is a start line and a name. Matching a node to a parser symbol must use the symbol's own span; an unmatched
   node becomes a `graph-symbol` with `end_line` = its line, as §5.5 already says.
5. `graph.json` also holds `community` ids per node (stored as §5.5 says) and `hyperedges` (empty; ignore).

## Proposed text for §5.5 (replace the sentence beginning "The spec does not guess graphify's output" through "...confidence words into this section before CI-7 starts")

> The CI-0d spike ran the pinned graphify (0.9.67) on a TypeScript and a Python fixture (`tests/fixtures/code-index/graphify/`, `SPIKE.md`); the recorded `graph.json` files
> are the contract fixtures. `graph.json` is NetworkX node-link JSON: top-level `directed` (false), `multigraph` (false), `graph` (an object), `hyperedges` (an array,
> ignored), `nodes` and `links` (the edge list is `links`). The strict DTO accepts only:
>
> - **node**: `id` (string, required), `label` (string, required), `source_file` (string, required: relative to the snapshot root, `/` separators; a node without one, or naming a
>   path not in the snapshot's file list, is dropped), `source_location` (string `L<n>`, required: the start line; there is no end line), `community` (integer, optional),
>   `file_type` (string, optional), `_callable` and `_callable_class` (booleans, optional). Unknown fields are ignored; a wrong type on a known field is `SND-CODEIDX-008`.
>   There is no kind field: a node with neither `_callable` nor `_callable_class` is a file node and is not stored as a symbol; `_callable_class` is a class; the target of a
>   `method` edge is a method; any other `_callable` node is a function. A label is the name with `()` for functions and methods (`.name()` for a method) and bare for a class
>   or file; the stored name drops the `()` and the leading `.`.
> - **edge**: `source` and `target` (strings naming node ids, required), `relation` (required, one of `calls`, `contains`, `method`, `imports`, `imports_from`; any other value is
>   skipped, not an error), `confidence` (`EXTRACTED` or `INFERRED`; any other word is stored as `INFERRED`), `source_file` and `source_location` (string `L<n>`; for `calls` the
>   call site in the caller's file), `confidence_score` (number, optional). Only `calls` edges become `edges` rows (`basis = graphify`): `source` is the caller and `target` the
>   callee, both ends must be nodes that kept their file and line, and a `calls` edge whose target is a class is a construction (stored with the class as the callee). The
>   graph is declared undirected but every edge keeps its direction (13 of 13 `calls` edges ran caller to callee in the spike).
>
> On the fixtures, TypeScript's 8 known calls are all `calls` edges with a file and line at both ends (6 `EXTRACTED`, 2 `INFERRED` where the call goes through a local variable).
> Python resolved 3 of 5: plain-function, imported and `self.` calls, but not a method called on a local assigned from a constructor. So callers and callees for TypeScript and
> Python come from graphify and are marked `basis = graphify` with the confidence word, and a Python answer can be incomplete; the CI-7 contract test replays the recorded
> files.
>
> **Sandbox and the graphify environment.** The sandbox masks `~/.agentic-workflow` (its credential list) with an empty tmpfs, and the pinned graphify environment lives under
> `$AW_STATE_DIR/cache/code-index/`, which is inside it by default. The spike reproduced the failure: `bwrap: execvp .../bin/graphify: No such file or directory`. So the pinned
> graphify environment is bound **read-only inside the sandbox after the mask** (`--ro-bind <env> <env>` placed after the `--tmpfs` of `~/.agentic-workflow`, with only the
> environment directory exposed, never its parent). The version check of `SND-CODEIDX-009` runs against the binary at that absolute path.

