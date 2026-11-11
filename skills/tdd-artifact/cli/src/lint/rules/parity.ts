// PARITY, reversed (research/bun-port-2026.md sections 5.2 and 5.3). lint_packet.py's PARITY rule runs the TypeScript
// compiler as a subprocess (bun cli/bin/build-tdd.ts) and compares it with the build; here the TypeScript compiler runs
// in-process and python3 (build_tdd.py) is the external one. The checks, their order and every finding and note text
// are the Python rule's (Lint.r_parity), so lint.json stays identical on both linters; only the skip note differs
// (python3 or build_tdd.py absent, where the Python says bun, cli/ or node_modules).
//
// The build is the first reference. Whenever it differs from the TypeScript compile (or the TypeScript compile fails),
// a fresh build_tdd.py compile, spelled the same way, decides whether the difference is the TypeScript's. On a compiled
// lint lint_packet.py compared its own build_tdd.py compile with the TypeScript one; this linter compiled with the
// TypeScript compiler, so the reference is that fresh build_tdd.py compile (TDD_GENERATED pinned to the compile's
// stamp), and a build_tdd.py failure there is reported in the Python rule's words for a failing fresh compile.
import { Context, Effect, Layer } from "effect";
import * as py from "../../py";
import { Packet } from "../packet";
import { type CompileFailure, SCRIPTS, pythonCompileOnce, tempDir, tsCompileOnce } from "../services";
import { type PyValue, dget, eq, get, isDict, isOsOrValueError, iter, keys, or, repr } from "../pyops";

const { join, relpath } = py.path;

export interface TsCompilerApi {
  /** Compile `src` (relative to cwd) into outDir with --no-copy and TDD_GENERATED=generated. */
  readonly compile: (src: string, cwd: string, outDir: string, generated: string) => CompileFailure;
}
export class TsCompiler extends Context.Service<TsCompiler, TsCompilerApi>()("tdd/lint/TsCompiler") {}
export const TsCompilerLive = Layer.succeed(TsCompiler, { compile: (src: string, cwd: string, out: string, gen: string) => tsCompileOnce(src, cwd, out, gen) });

const PYTHON_TIMEOUT_S = 300;

/** How Lint._compile_to words a failed run: "exit N: <last line>" (or "exit N" when it printed nothing). */
const failure = (f: readonly [number, string]): string => `exit ${f[0]}: ${f[1] || `exit ${f[0]}`}`;

/** build_tdd.py into outDir, TDD_GENERATED pinned, 300 s: null or the failure as the Python rule words it. */
export function pythonCompileTo(python: string, src: string, cwd: string, outDir: string, generated: string): string | null {
  const t0 = Date.now();
  const f = pythonCompileOnce(python, src, cwd, outDir, { TDD_GENERATED: generated }, PYTHON_TIMEOUT_S);
  if (f !== null && Date.now() - t0 >= PYTHON_TIMEOUT_S * 1000) {
    const cmd = [python, "-I", join(SCRIPTS(), "build_tdd.py"), src, outDir, "--no-copy"];
    return `TimeoutExpired: Command '${repr(cmd)}' timed out after ${PYTHON_TIMEOUT_S} seconds`;
  }
  return f === null ? null : failure(f);
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

/** Lint._manifest_diffs(ma, mb): the manifest fields PARITY compares (files paths, missing, warnings) that differ,
 * described. ma and mb must be dicts (AttributeError otherwise, as in the Python). */
export function manifestDiffs(ma: PyValue, mb: PyValue): string[] {
  const diffs: string[] = [];
  const fields: readonly (readonly [string, (m: PyValue) => PyValue])[] = [
    ["files", (m) => keys(or(dget(m, "files"), new Map()))],
    ["missing", (m) => dget(m, "missing")],
    ["warnings", (m) => dget(m, "warnings")],
  ];
  for (const [k, f] of fields) {
    const x = f(ma), y = f(mb);
    if (!eq(x, y)) {
      const xs = iter(or(x, [])), ys = iter(or(y, []));
      const n = Math.min(xs.length, ys.length);
      let j = 0;
      while (j < n && eq(xs[j], ys[j])) j++;
      diffs.push(`${k} (${xs.length} vs ${ys.length}; first at ${j}: ${repr(j < xs.length ? xs[j] : null)} vs ${repr(j < ys.length ? ys[j] : null)})`);
    }
  }
  return diffs;
}

/** The rule; its temporary folders are removed when it ends (the Python rule's finally). */
export const parity = Effect.scoped(Effect.gen(function* () {
  const p = yield* Packet;
  const ts = yield* TsCompiler;
  const python = Bun.which("python3");
  const bt = join(SCRIPTS(), "build_tdd.py");
  if (!python || !p.fs.isfile(bt)) {
    p.note(`PARITY: ${!python ? "python3 is not on PATH" : p.rel(bt) + " is absent"}; build_tdd.py was not compared`);
    return;
  }
  const own = (p.compiled ? p.tmp : p.build) ?? "";
  const generated = isDict(p.content) ? get(p.content, "generated") : null;
  if (!p.src || !p.fs.isfile(join(own, "content.json")) || typeof generated !== "string" || !generated) {
    p.note("PARITY: no source, no content.json or no generated stamp to compare with; skipped");
    return;
  }
  const where = p.compiled ? "(compiled) content.json" : p.rel(join(own, "content.json"))!;
  const mfile = p.compiled ? "(compiled) content.files.json" : p.rel(join(own, "content.files.json"))!;
  const src = p.rel(p.src)!;
  const srcRel = relpath(p.src, p.root);
  // python_fresh(): one build_tdd.py compile of the source, made when first needed
  let fresh: readonly [string, string | null] | null = null;
  const pythonFresh = Effect.gen(function* () {
    if (fresh === null) {
      const out = yield* tempDir("lint-tdd-parity-");
      fresh = [out, pythonCompileTo(python, srcRel, p.root, out, generated)] as const;
    }
    return fresh;
  });
  // read_manifest(path): the JSON, or None when it is missing or unreadable
  const readManifest = (path: string): PyValue => {
    try {
      return py.json.loads(p.fs.readText(path));
    } catch (e) {
      if (!isOsOrValueError(e)) throw e;
      return null;
    }
  };
  const tout = yield* tempDir("lint-tdd-parity-");
  const terr = ts.compile(srcRel, p.root, tout, generated);
  if (terr !== null) {
    const [, perr] = yield* pythonFresh;
    if (perr !== null) {
      p.note(`PARITY: build_tdd.py fails on the current source too (${perr}); the compilers were not compared`);
      return;
    }
    p.add("PARITY", "content", "error", `the TypeScript compiler failed on ${src} where build_tdd.py did not (${failure(terr)})`, src);
    return;
  }
  const mine = p.fs.readBytes(join(tout, "content.json"));
  const mineM = readManifest(join(tout, "content.files.json"));
  // the reference: the build, or (compiled) build_tdd.py's compile of the source, which lint_packet.py's run linted
  let ref = own;
  if (p.compiled) {
    const [pout, perr] = yield* pythonFresh;
    if (perr !== null) {
      p.add("PARITY", "content", "error", `build_tdd.py fails on ${src} (${perr}) where the TypeScript compiler succeeded`, src);
      return;
    }
    ref = pout;
  }
  const refM = readManifest(join(ref, "content.files.json"));
  const sameC = sameBytes(p.fs.readBytes(join(ref, "content.json")), mine);
  const mDiffs = refM !== null && mineM !== null ? manifestDiffs(refM, mineM) : null;
  if (sameC && mDiffs !== null && mDiffs.length === 0) {
    p.note(`PARITY: the TypeScript compiler (cli/bin/build-tdd.ts) reproduced ${where} byte for byte with TDD_GENERATED=${generated}, ` +
      `and the manifest's files, missing and warnings`);
    return;
  }
  // the build differs from the TypeScript compile: a fresh build_tdd.py compile decides whose difference it is
  const [pout, perr] = yield* pythonFresh;
  if (perr !== null) {
    p.add("PARITY", "content", "error", `build_tdd.py fails on ${src} (${perr}) where the TypeScript compiler succeeded`, src);
    return;
  }
  const freshC = p.fs.readBytes(join(pout, "content.json"));
  const freshM = readManifest(join(pout, "content.files.json"));
  if (!sameBytes(freshC, mine)) {
    p.add("PARITY", "content", "error", `the TypeScript compiler's content.json differs from build_tdd.py's on ${src}: ` +
      `${firstDiff(freshC, mine)}; fix cli/src/compile (build_tdd.py stays the reference until the cutover)`, src);
    return;
  }
  const d = manifestDiffs(or(freshM, new Map()), or(mineM, new Map()));
  if (d.length) {
    p.add("PARITY", "manifest", "warn", `the TypeScript compiler's manifest differs from build_tdd.py's on ${src}: ${d.join("; ")}`, src);
    return;
  }
  const why = !sameC ? `${where} differs from a fresh compile of the source (a stale build: rebuild)`
    : mDiffs === null ? `${mfile} is missing or unreadable`
    : `${mfile} differs from a fresh compile's (${mDiffs.join("; ")}): a stale build, files found only under the build folder, ` +
      `or another spelling of the packet path`;
  p.note(`PARITY: ${why}; the TypeScript compiler (cli/bin/build-tdd.ts) reproduced a fresh build_tdd.py compile byte for byte ` +
    `with TDD_GENERATED=${generated}, and its manifest's files, missing and warnings`);
}));
