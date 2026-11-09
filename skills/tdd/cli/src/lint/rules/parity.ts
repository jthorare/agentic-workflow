// PARITY, reversed (research/bun-port-2026.md sections 5.2 and 5.3). lint_packet.py's PARITY rule (N6, PR 11) runs
// the TypeScript compiler as a subprocess and compares it with the build; here the TypeScript compiler runs in-process
// and python3 (build_tdd.py) is the external one, used for the fresh compile a differing build calls for. The checks,
// their order and every finding and note text are the Python rule's, so lint.json stays identical on both linters;
// only the skip notes differ (python3 or build_tdd.py absent instead of bun or cli/).
//
// Not in RULES yet: it joins when N6 merges (the Python linter gains PARITY then, and src/compile provides the
// in-process compiler as the TsCompiler service's live layer). Tests run it with a stand-in TsCompiler.
import { Context, Effect } from "effect";
import * as py from "../../py";
import { Packet } from "../packet";
import { SCRIPTS, tailLine, tempDir } from "../services";
import { type PyValue, eq, get, items, iter, repr, truthy } from "../pyops";

const { join, relpath } = py.path;

export interface TsCompilerApi {
  /** Compile `src` (relative to cwd) into outDir with --no-copy and TDD_GENERATED=generated: null, or the failure as
   * "exit N: <last line>" (how the Python rule reports a bun run that failed). */
  readonly compile: (src: string, cwd: string, outDir: string, generated: string) => string | null;
}
export class TsCompiler extends Context.Service<TsCompiler, TsCompilerApi>()("tdd/lint/TsCompiler") {}

const PYTHON_TIMEOUT_S = 300;
const dec = (b: Uint8Array): string => py.io.universalNewlines(py.io.decodeUtf8(b, "replace"));

/** build_tdd.py into outDir (the fresh compile): null, or "exit N: <last line>". */
export function pythonCompileTo(python: string, src: string, cwd: string, outDir: string, generated: string): string | null {
  const env: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) if (v !== undefined) env[k] = v;
  env["TDD_GENERATED"] = generated;
  const cmd = [python, "-I", join(SCRIPTS(), "build_tdd.py"), src, outDir, "--no-copy"];
  const r = Bun.spawnSync(cmd, { cwd, env, stdin: "ignore", stdout: "pipe", stderr: "pipe", timeout: PYTHON_TIMEOUT_S * 1000 });
  if (r.signalCode && r.exitCode === null) return `TimeoutExpired: Command '${repr(cmd)}' timed out after ${PYTHON_TIMEOUT_S} seconds`;
  const code = r.exitCode ?? 1;
  if (code !== 0 || !py.io.isfile(join(outDir, "content.json"))) return `exit ${code}: ${tailLine(dec(r.stderr), dec(r.stdout), `exit ${code}`)}`;
  return null;
}

/** Lint._first_diff(a, b): where two byte strings first differ, with 30 bytes of context on each side. */
export function firstDiff(a: Uint8Array, b: Uint8Array): string {
  const n = Math.min(a.length, b.length);
  let i = 0;
  while (i < n && a[i] === b[i]) i++;
  let line = 1;
  for (let k = 0; k < i; k++) if (a[k] === 10) line++;
  const ctx = (x: Uint8Array) => repr(py.io.decodeUtf8(x.subarray(Math.max(0, i - 30), i + 30), "replace"));
  return `first difference at byte ${i} (line ${line}; ${a.length} vs ${b.length} bytes): ${ctx(a)} vs ${ctx(b)}`;
}

const sameBytes = (a: Uint8Array, b: Uint8Array): boolean => a.length === b.length && a.every((x, k) => x === b[k]);

export const parity = Effect.gen(function* () {
  const p = yield* Packet;
  const ts = yield* TsCompiler;
  const python = Bun.which("python3");
  const bt = join(SCRIPTS(), "build_tdd.py");
  if (!python || !p.fs.isfile(bt)) {
    p.note(`PARITY: ${!python ? "python3 is not on PATH" : p.rel(bt) + " is absent"}; build_tdd.py was not compared`);
    return;
  }
  const refDir = p.compiled ? p.tmp : p.build;
  const cpath = join(refDir ?? "", "content.json"), mpath = join(refDir ?? "", "content.files.json");
  const generated = get(p.content, "generated");
  if (!p.src || !p.fs.isfile(cpath) || typeof generated !== "string" || !generated) {
    p.note("PARITY: no source, no content.json or no generated stamp to compare with; skipped");
    return;
  }
  const src = p.src;
  let where = p.compiled ? "(compiled) content.json" : p.rel(cpath)!;
  let mfile = p.compiled ? "(compiled) content.files.json" : p.rel(mpath)!;
  const srcRel = relpath(src, p.root);
  const tout = yield* tempDir("lint-tdd-parity-");
  const err = ts.compile(srcRel, p.root, tout, generated);
  if (err !== null) {
    p.add("PARITY", "content", "error", `the TypeScript compiler failed on ${p.rel(src)} where build_tdd.py did not (${err})`, p.rel(src));
    return;
  }
  const ref = p.fs.readBytes(cpath), mine = p.fs.readBytes(join(tout, "content.json"));
  let refM = mpath;
  if (!sameBytes(ref, mine)) {
    // a stale build (or files only under build/) differs from any fresh compile: compare two fresh compiles
    const pout = yield* tempDir("lint-tdd-parity-");
    const perr = pythonCompileTo(python, srcRel, p.root, pout, generated);
    if (perr !== null) {
      p.add("PARITY", "content", "error", `${where} differs from the TypeScript compile, and a fresh build_tdd.py compile failed (${perr})`, p.rel(src));
      return;
    }
    const fresh = p.fs.readBytes(join(pout, "content.json"));
    if (!sameBytes(fresh, mine)) {
      p.add("PARITY", "content", "error", `the TypeScript compiler's content.json differs from build_tdd.py's on ${p.rel(src)}: ` +
        `${firstDiff(fresh, mine)}; fix cli/src/compile (build_tdd.py stays the reference until the cutover)`, p.rel(src));
      return;
    }
    p.note(`PARITY: ${where} differs from a fresh compile of the source (a stale build: rebuild); the TypeScript and Python compilers ` +
      `agree on the fresh compile`);
    refM = join(pout, "content.files.json");
    where = "a fresh build_tdd.py compile's content.json";
    mfile = "(fresh compile) content.files.json";
  }
  if (!p.fs.isfile(refM)) {
    p.note(`PARITY: the TypeScript compiler reproduced ${where} byte for byte; no ${mfile} to compare manifests with`);
    return;
  }
  let ma: PyValue, mb: PyValue;
  try {
    ma = py.json.loads(p.fs.readText(refM));
    mb = py.json.loads(p.fs.readText(join(tout, "content.files.json")));
  } catch (e) {
    p.add("PARITY", "manifest", "warn", `the manifests could not be compared: ${(e as Error).message}`, mfile);
    return;
  }
  const keys = (m: PyValue): PyValue[] => items(truthy(get(m, "files")) ? get(m, "files") : new Map()).map(([k]) => k);
  const diffs: string[] = [];
  for (const [k, f] of [["files", keys], ["missing", (m: PyValue) => get(m, "missing")], ["warnings", (m: PyValue) => get(m, "warnings")]] as const) {
    const x = f(ma), y = f(mb);
    if (!eq(x, y)) {
      const xs = iter(truthy(x) ? x : []), ys = iter(truthy(y) ? y : []);
      const n = Math.min(xs.length, ys.length);
      let j = 0;
      while (j < n && eq(xs[j], ys[j])) j++;
      diffs.push(`${k} (${xs.length} vs ${ys.length}; first at ${j}: ${repr(j < xs.length ? xs[j] : null)} vs ${repr(j < ys.length ? ys[j] : null)})`);
    }
  }
  if (diffs.length) {
    p.add("PARITY", "manifest", "warn", `the TypeScript compiler's manifest differs from build_tdd.py's: ${diffs.join("; ")}`, mfile);
    return;
  }
  p.note(`PARITY: the TypeScript compiler (cli/bin/build-tdd.ts) reproduced ${where} byte for byte with TDD_GENERATED=${generated}, ` +
    `and the manifest's files, missing and warnings`);
});
