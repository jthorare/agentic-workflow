# tdd-cli

The tdd skill's command line under bun (TypeScript 7, Effect 4 where a program is effectful). It is the target of the
bun port designed in the architecture packet's `research/bun-port-2026.md` (D19) and the home of the system-database
loader (D17, D23, D24) and the revision runner.

- `bin/tdd.ts` dispatches `tdd <command>`; each command is a `bin/<name>.ts` with a `main(argv): Promise<number>`.
  A command that is not ported yet says so and names the Python script to run instead.
- `src/py/` is the Python-semantics kernel (lossless JSON, float repr, `re`, `str`, `html`, `os.path`) that byte-identity
  with the Python scripts depends on; `src/sd/` is the TypeScript copy of the system-design surface the compiler uses,
  pinned by `UPSTREAM.json`; `src/compile`, `src/lint`, `src/agents`, `src/hotspots`, `src/eval`, `src/rev`, `src/db`
  are the ports and the new programs.
- `parity/` holds the Python oracle and `gate.ts`, which runs both implementations into the same path and diffs.
- `sql/schema.sql` is the system database (eight tables); `tdd load-db <packet>` fills it after lint passes.

Setup once: `bun install` here. Check: `bun run check`. Tests: `bun test`. During the overlap period the Python scripts
stay the documented path (SKILL.md); nothing here is required to build a TDD until the cutover PR.

## The Python-semantics kernel (`src/py`, PR 9)

Plain TypeScript, no Effect. CPython 3.13 is the specification: every module has golden tests against a live
`python3` (`parity/oracle.py` prints the golden JSON; `test/py/*.test.ts` compare). Import it as
`import * as py from "../py"`.

- **Values.** A Python int is a JS `number` when integral and safe, else a `bigint`; a float is a `PyFloat` (wrap every
  float the code makes, so `0.0` prints `0.0`); a dict is a `Map` (`PyDict`), never a plain object, because plain objects
  move integer-like keys to the front.
- `json.ts` lossless `loads` (key order, int vs float, NaN/Infinity, duplicate keys last-wins at the first position,
  CPython's `JSONDecodeError` text) and `dumps` (indent, separators, ensure_ascii, sort_keys, default, allow_nan) that
  reproduces both packets' `content.json`, `content.files.json` and `lint.json` byte for byte.
- `num.ts` float repr, `round(x, n)` half-even on the exact binary value, `roundInt`, `int(s, base)`; `repr.ts`;
  `str.ts` (whitespace, split/rsplit/partition/strip/splitlines boundaries, code point `cpLen`/`cpSlice`/`cmp`/`sorted`,
  `lower`/`upper`, `replace`); `re.ts` (Python patterns translated onto JS RegExp with the u flag, CPython's
  empty-match iteration); `html.ts` (escape, unescape on the `entities` table with CPython's numeric rules);
  `path.ts` (posixpath, `homeRel`); `io.ts` (strict UTF-8 with CPython's error text, universal newlines, `copy2`,
  sorted `listdir`); `time.ts` (`utcStamp` honours `TDD_GENERATED`).
- `ucd.ts` is generated (`bun run parity/gen-ucd.ts`): CPython's own Unicode 15.1 tables for `\w`, `\d`, `\s`,
  `isprintable`, the `splitlines` boundaries and the case-mapping exceptions. bun's ICU is newer and disagrees on about
  9,000 code points, so the kernel never uses `\p{...}` for these; `test/py/ucd.test.ts` re-checks every table against
  the live python3 over all 1,114,112 code points.

**Traps the port must route through the kernel** (each has golden tests):

1. `s.replace("a", "b")` changes the first occurrence in JS and every occurrence in Python: call `py.replace(s, old,
   new, count?)`, or use a regex with `/g`. `test/py/replace-ban.test.ts` tokenizes every file under `src/` (`src/py`
   included), `bin/` and `parity/` and fails on any `.replace(` whose pattern is not provably a RegExp (a regex
   literal, `new RegExp`, a name bound only to one, or a cast `as RegExp`), wherever the argument sits.
2. `[]`, `{}` and `""` are falsy in Python and `[]`/`{}` truthy in JS (and NaN the other way round): a ported `if x:`
   is `if (py.truthy(x))`.
3. `int("١٢")` is 12 in Python (any Unicode decimal digit, Unicode whitespace around it, `_` between digits) while
   `Number()` gives NaN, and `\d` matches those digits: use `py.int(s)`.
4. Also: `\w \d \s \b $ . ^` differ (use `py.re`), `split(sep, n)` keeps the remainder (`py.str.split`), `trim()` is not
   `strip()`, string length and slicing count UTF-16 units (`cpLen`, `cpSlice`), `<` on strings compares UTF-16 units
   (`py.cmp`), `JSON.stringify` drops `1.0` to `1` and `JSON.parse` reorders integer-like keys (`py.json`).

`re.IGNORECASE` follows sre, not JS's `i` flag: sre lowers with its simple map and adds `re._casefix`, so `i`
matches `İ` and `ı`, `s` matches `ſ` and `k` the Kelvin sign, while under `re.ASCII` only A-Z fold. Each cased
literal and class is compiled to the explicit set sre matches (`src/py/caseless.ts`); `test/py/re.test.ts` checks
77 patterns against CPython over every code point.

Known limits: `re` rejects atomic groups, possessive quantifiers, conditionals, `\N{...}` and backreferences under
`IGNORECASE` (sre compares their lower forms, which JS cannot express); none occur in the scripts.
`expanduser("~name")` resolves only the current user. `json.loads` ignores CPython's 4,300-digit limit on int
literals (`py.int` enforces it). `copy2` keeps mtime to the microsecond.

## The system-design surface (`src/sd`, PR 10)

The 372 lines of system-design's `build_doc.py` (14 names) and `build_prs.py` (4 names) that `build_tdd.py` calls,
reimplemented in plain synchronous TypeScript on the kernel (D1 option A, research section 2). The Python stays the
specification; system-design and the vendor copy are never edited.

- `blocks.ts` (MARK, HEAD, FENCE, LI, HR, TSEP, COMMENT, isBlockStart, splitRow, indentOf, parseList, parseBlocks,
  splitMarker, slug), `inline.ts` (esc, Inline, renderInline, stripTags), `assets.ts` (FILE_LIMIT, svgSize, pngSize,
  imgSize, Assets, copy2, publishedFor, mimeOf), `page.ts` (STATUS_WORDS, GENERIC_TAILS, pageTitle, jsonLoads,
  loadJson, SystemExit), `prs.ts` (BLOCK, front, inline, splitRow, markdown, firstSentence), `errors.ts` (SdError,
  StructError, SameFileError, RecursionError).
- `index.ts` also exports `bd` and `bp` with the Python names, so a port reads like `build_tdd.py`:
  `bd.MARK = TDD_MARK; bd.parse_blocks(run); const inl = bd.Inline(); inl(s, false); bp.markdown(body)`.
- **MARK** is read at call time, as the Python module global is: `bd.MARK = p` (or `setMark(p)`) changes what
  parse_blocks and is_block_start match from then on; each of them also takes the pattern as an optional last argument.
- **Values.** Blocks are plain objects with the Python dicts' key order (`{t, level, text}`, ...); a list's `start` is
  `number | bigint`; tuples are arrays (`splitMarker`, `imgSize`, `front`, `Assets.ref`); Python dicts with open keys are
  Maps (`front`'s meta, `Assets.files`, `Assets.finish()`, `STATUS_WORDS`, `anchor_map`; `.get(k) ?? null` is
  `dict.get(k)`); `loadJson` returns kernel values (`jsonLoads`, on `py.json.loads`).
- **Inline** is a factory: `bd.Inline()` returns a function with an `anchor_map` Map, called as `inl(s, links)`.
- **Errors.** Python's exceptions surface as the kernel's `PyError` (a stray `\0C9\0` placeholder is `IndexError`, a bad
  SVG width `ValueError`, an infinite one `OverflowError`) or an `SdError` whose `type` is the Python name (png_size's
  short PNG `struct.error`; Assets.finish copying a file onto itself through a symlink, a hard link or a case-only name
  `shutil.SameFileError`, as `shutil.copy2` does). load_json's `sys.exit` is `SystemExit(1, "{what} {path}: {e}")` with
  CPython's OSError / UnicodeDecodeError / JSONDecodeError text. `jsonLoads` adds the two json limits the kernel's
  `loads` leaves out: an int literal over 4,300 digits is ValueError (so load_json exits with CPython's "Exceeds the
  limit (4300 digits) ..." text) and a container nested deeper than 9,998 is RecursionError, which load_json does not
  catch; whichever the scanner meets first wins, a JSONDecodeError included.
- `../UPSTREAM.json` pins the sha256 of the vendor `build_doc.py` and `build_prs.py` and of each ported line range;
  `test/sd/upstream.test.ts` fails, naming the ranges that moved, when a file (or the copy `build_tdd.py` would load)
  changes. Port the change, re-run the oracle suites, then update the hashes.
- Proof (`test/sd`, CPython 3.13 via `parity/oracle.py`): `sd_calls` compiles each `$TDD_PARITY_PACKETS` packet with
  `build_tdd.py`, recording every call it makes to the 18 names (arguments, result, and the Inline / Assets state after
  each call), and `replay.test.ts` replays them through src/sd, with and without copying, plus every image under the
  packet through img_size and the data: URL branch; `sd_fuzz` runs 10,000 seeded markdown-shaped documents through
  every text function (`TDD_SD_FUZZ` overrides the count); `sd_files` runs written SVG, PNG and JSON edge cases
  (including the int-digit and nesting limits) through img_size, load_json and Assets; `sd_finish` runs Assets.finish's
  copying on twin trees (symlinked folder, hard link, case-only name, folder destination); `sd_constants` compares the
  patterns and tables. `ban.test.ts` rejects regex literals, `RegExp`, `\p{...}`, `toLowerCase`, `trim`, `JSON`,
  `Number()` and string `.match/.split/.replace` in src/sd.
- Known limits: `mimeOf` (Assets with inline=true only, which build_tdd never uses) knows CPython's built-in table, not
  the host's mime.types, and on macOS that changes common types: CPython reads /etc/apache2/mime.types, so `.ico` is
  image/x-icon there (image/vnd.microsoft.icon here), `.xml` application/xml (text/xml here), and `.woff`, `.woff2`,
  `.ttf`, `.otf`, `.ogg`, `.ogv`, `.m4v` and `.jxl` get a type where this table has none (application/octet-stream
  here). Python's recursion limit in parse_blocks / parse_list (about 1,000 nested quotes or lists) is not reproduced.

## Parity harness (`parity/`)

- `oracle.py` (test only): `python3 -I -B parity/oracle.py <suite>` prints golden JSON for a suite (`list` names
  them). It loads `build_doc.py` / `build_prs.py` unchanged from the sibling `skills/system-design` (or
  `$SYSTEM_DESIGN_SKILL`) and the tdd scripts by path, and never writes bytecode beside them.
- `gate.ts`: `bun run parity/gate.ts <packet-dir> [--a python|ts|exe] [--b python|ts|exe] [--expect-line "<summary>"]
  [-- <compiler flags>]` compiles the packet twice into the same temporary out path with `TDD_GENERATED` and `TZ`
  pinned, `build_tdd.py` as A and `bin/build-tdd.ts` as B by default, and compares `content.json`,
  `content.files.json`, every file by sha256 (the dashed SVGs included), stdout and stderr. Flags after `--`
  (`--no-copy`, `--strict`) go to both compilers. One CPython interpreter warning is printed as a note and not
  compared: the `DeprecationWarning` for `build_tdd.py`'s positional `re.split` maxsplit, exactly its two lines, on
  Python runs only (found by content in the current `build_tdd.py`); every other stderr line is compared. Exit 0
  green, 1 red, 2 usage.
- The packet tests read `$TDD_PARITY_PACKETS` (packet folders separated by `:`) and skip when it is unset:
  `TDD_PARITY_PACKETS=<a>:<b> bun test`.

## The compiler (`src/compile`, `src/vocab.ts`, PR 11)

`bin/build-tdd.ts` (also `tdd build`) is `build_tdd.py` in plain synchronous TypeScript on the kernel: the same argv,
usage text, `content.json` (indent 1) and `content.files.json` (indent 2), copied files and dashed proposed strokes,
stdout line, `warning:` lines and exit codes. Modules: `args.ts` (argv and the usage text, the Python docstring
verbatim), `attrs.ts` (`{attrs}`, `:::` containers, `split_title`), `refs.ts` (reference and glossary linking),
`sanitize.ts` (the HTML guard), `prs.ts` (`prs/pr-NN.md`), `compiler.ts` (the `Compiler`: ids and definitions,
inline and citations, schema, figures and hotspots, blocks, containers, PR cards, the page). It reads the system-design
surface from `src/sd` (`import * as sd from "../sd"`: `parseBlocks(lines, MARK)` with the tdd MARK passed explicitly, never `bd.MARK =`) and the vocabulary from `src/vocab.ts`, the one
copy of `TOKEN`, `token_id`, `CITE`, `LEGEND_TONES`, `LINK_KEYS`, `SKIP_TAGS`, the tdd `MARK` and `SQL_KW` (checked
against `build_tdd.py` and `eval/ev_common.py` in `test/compile`). `BUILD_TDD_SHA256` pins the Python file the port
reproduces: a change to `build_tdd.py` lands with the same change here and a new pin (the twin rule).

During the overlap the linter's `PARITY` rule (`scripts/lint_packet.py`, `references/validators.md`) compiles every
linted source with this compiler too. `test/compile` runs the gate on an edge-case fixture (`test/compile/fixtures/edge`:
every container, hotspot variants, PNG / JPEG / SVG sizing, a CRLF SVG that the dash rewrites, sub-PRs, aliases, the
sanitizer), with `--no-copy` and `--strict`, on `$TDD_PARITY_PACKETS`, and on argv errors.

## The linter (`src/lint`, PR 14, Effect)

`bin/lint-packet.ts` (also `tdd lint`) is `lint_packet.py` in TypeScript with Effect 4 (D27): the same argv and usage
text (the Python docstring verbatim, `src/lint/doc.ts`), report, `lint.json` (indent 1, `ensure_ascii` off) and exit
codes. Modules:
- `finding.ts`: the `Finding` schema (`Schema.Class`: rule, check, severity, file, line, message, in lint.json order).
- `services.ts`: `Fs` (os.listdir order, CPython's `getmtime` float, `posixpath.realpath`, `os.walk`), `Git`
  (read-only `git -C`, 60 s, with the Python's timeout and OSError text), `Clock` (the local time in META/stale-build:
  TZ decides), `Compiler` (src/compile in-process, into a scoped temporary folder) and `Agents` (src/agents `check`).
- `packet.ts`: the `Packet` service (Lint's state and plumbing: source, meta, PR files, build or compile, ids, house
  slots, cached lines, `locate*`, and the findings sink with `--skip`).
- `rules/*.ts`: one Effect per rule family (`struct`, `dnum`, `refs`, `cites`, `diag`, `schema`, `files`, `meta`,
  `agents`, `xdoc`, `parity`), each in Python's order. CITES counts lines with `git show` eight at a time and reads
  the counts back in Python's order. DIAG reads labels through src/hotspots `Shapes`. PARITY is reversed: in-process
  TypeScript against `python3 build_tdd.py`.
- `run.ts`: the rules in `RULES` order, each isolated (a rule that dies is `RULE/check-failed`, the run goes on). The
  compile folder lives in the run's scope. `report.ts`: the text report, `lint.json` and the exit code.
- `pyops.ts`: what the rules need on top of the kernel (dict access, `==`, iteration, `str()`, the content walkers,
  surrogateescape, Python's exception names for a thrown error).

Tests (`test/lint`):
- `corpus.test.ts`: 84 mutations of a neutral base packet (`fixtures/base`, `fixtures/mutations.ts`) with a git
  repository built per run (`fixtures/repo`), and 7 usage cases. Each runs both linters from the same cwd with `TZ` and
  `TDD_GENERATED` pinned. Exit code, stdout, stderr and lint.json must be identical, and every check id that
  `lint_packet.py` declares (read from its source, 69 today) must fire at least once.
  `TDD_LINT_COVERAGE_OUT=<file>` writes, for each check id, the cases that fired it.
- `packets.test.ts`: `$TDD_PARITY_PACKETS`, on the build and with `--compile`.
- `units.test.ts`: the Git, realpath, getmtime, surrogateescape and first_diff texts against CPython; rule isolation;
  services swapped by layer (a Git answering from a table, a raising Agents check, an absent Compiler).

One Python behaviour cannot be matched. DNUM/none sorts the D-numbers by their digits. When two tokens share a number
(`D2` and `D-2`), Python breaks the tie in set order, which depends on the hash seed and so varies from run to run.
The TypeScript keeps the first-seen order. The corpus avoids this tie.

## Effect smoke

`test/smoke/effect.test.ts` (a `Context.Service` provided by a `Layer.effect` whose scoped resource is released when the
program ends, a `Schema.Class` decoded with `Schema.decodeUnknownEffect`, `Effect.forEach` with `concurrency: 3`)
type-checks with typescript 7.0.2 (`bun run check`) and passes under bun 1.4.4 on effect 4.1.2, checked 2026-11-08.
The research's gate (section 4.4) is met: the linter, the eval and the revision runner may use Effect 4.
