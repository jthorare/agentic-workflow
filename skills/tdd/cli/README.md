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

## Parity harness (`parity/`)

- `oracle.py` (test only): `python3 -I -B parity/oracle.py <suite>` prints golden JSON for a suite (`list` names
  them). It loads `build_doc.py` / `build_prs.py` unchanged from the sibling `skills/system-design` (or
  `$SYSTEM_DESIGN_SKILL`) and the tdd scripts by path, and never writes bytecode beside them.
- `gate.ts`: `bun run parity/gate.ts <packet-dir> [--a python|ts|exe] [--b python|ts|exe] [--expect-line "<summary>"]
  [-- <compiler flags>]` compiles the packet twice into the same temporary out path with `TDD_GENERATED` and `TZ`
  pinned, `build_tdd.py` as A and `bin/build-tdd.ts` as B by default, and compares `content.json`,
  `content.files.json`, every file by sha256 (the dashed SVGs included), stdout and stderr. Flags after `--`
  (`--no-copy`, `--strict`) go to both compilers. CPython's own interpreter warnings on stderr (today one
  `DeprecationWarning` for `build_tdd.py`'s positional `maxsplit`) are printed as notes and not compared. Exit 0
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
surface from `src/sd` (`import * as sd from "../sd"`, the Python names) and the vocabulary from `src/vocab.ts`, the one
copy of `TOKEN`, `token_id`, `CITE`, `LEGEND_TONES`, `LINK_KEYS`, `SKIP_TAGS`, the tdd `MARK` and `SQL_KW` (checked
against `build_tdd.py` and `eval/ev_common.py` in `test/compile`). `BUILD_TDD_SHA256` pins the Python file the port
reproduces: a change to `build_tdd.py` lands with the same change here and a new pin (the twin rule).

During the overlap the linter's `PARITY` rule (`scripts/lint_packet.py`, `references/validators.md`) compiles every
linted source with this compiler too. `test/compile` runs the gate on an edge-case fixture (`test/compile/fixtures/edge`:
every container, hotspot variants, PNG / JPEG / SVG sizing, a CRLF SVG that the dash rewrites, sub-PRs, aliases, the
sanitizer), with `--no-copy` and `--strict`, on `$TDD_PARITY_PACKETS`, and on argv errors.

## Effect smoke

`test/smoke/effect.test.ts` (a `Context.Service` provided by a `Layer.effect` whose scoped resource is released when the
program ends, a `Schema.Class` decoded with `Schema.decodeUnknownEffect`, `Effect.forEach` with `concurrency: 3`)
type-checks with typescript 7.0.2 (`bun run check`) and passes under bun 1.4.4 on effect 4.1.2, checked 2026-11-08.
The research's gate (section 4.4) is met: the linter, the eval and the revision runner may use Effect 4.
