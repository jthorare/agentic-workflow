#!/usr/bin/env bun
// The parity gate (research/bun-port-2026.md sections 3.2, 5.2 PR 9 and PR 11): compile one packet twice into the
// same out path, one compiler after the other, and compare everything the compile produces: content.json,
// content.files.json and every copied file by sha256, the stdout summary line, the stderr warnings, the exit code.
// `generated` is pinned with TDD_GENERATED and TZ is fixed, so two runs of one compiler must be byte-identical.
// A is the Python compiler and B the TypeScript one (bin/build-tdd.ts) unless --a / --b say otherwise.
// One CPython interpreter warning is left out of the stderr comparison and reported as a note: the DeprecationWarning
// for build_tdd.py's positional re.split maxsplit (box_decision), exactly its two lines, on Python runs only. The line is
// found by content in the current build_tdd.py, so its number follows the file; every other stderr line is compared.
//
// usage: bun run parity/gate.ts <packet-dir> [--src <file.md>] [--a python|ts|<exe>] [--b python|ts|<exe>]   (A python, B ts)
//        [--out <dir>] [--keep] [--generated <stamp>] [--expect-line <summary>] [--json <report.json>] [-- <flag>...]
// <exe> is any program taking the compiler's own arguments (<source.md> <out-dir>); flags after `--` (--no-copy,
// --strict, --meta f ...) are passed to both compilers after those two. The default out path is a new
// temporary folder, removed afterwards unless --keep. The gate never deletes a file it did not write: an --out folder
// must be new, empty, or hold exactly the gate's own earlier output (listed in a ledger under the system temp folder);
// between the two compiles it removes exactly the paths compiler A wrote, and it leaves compiler B's output in place.
// Exit 0 when green, 1 when anything differs or a compile fails, 2 on bad usage.
import { spawnSync } from "bun";
import { createHash } from "node:crypto";
import { existsSync, lstatSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmdirSync, rmSync, statSync, unlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join, relative, resolve } from "node:path";

const CLI = resolve(dirname(new URL(import.meta.url).pathname), "..");
const TDD = resolve(CLI, "..");

export interface Snapshot {
  compiler: string;
  exit: number;
  stdout: string;
  stderr: string;
  files: Map<string, { sha256: string; bytes: number }>;
  content: Buffer | null;
  manifest: Buffer | null;
  seconds: number;
}

export interface GateResult {
  green: boolean;
  packet: string;
  out: string;
  summary: string;
  problems: string[];
  notes: string[];
  a: Omit<Snapshot, "files" | "content" | "manifest"> & { files: number };
  b: Omit<Snapshot, "files" | "content" | "manifest"> & { files: number };
}

function command(which: string, src: string, out: string, args: string[] = []): string[] {
  if (which === "python") return ["python3", "-I", "-B", BUILD_TDD, src, out, ...args];
  if (which === "ts") {
    const bin = join(CLI, "bin", "build-tdd.ts");
    if (!existsSync(bin)) throw new Error(`ts: ${relative(process.cwd(), bin)} does not exist`);
    return [process.execPath, "--no-install", bin, src, out, ...args];
  }
  return [which, src, out, ...args];
}

function walk(root: string, dir = root, acc = new Map<string, { sha256: string; bytes: number }>()) {
  for (const name of readdirSync(dir).sort()) {
    const p = join(dir, name);
    const st = statSync(p);
    if (st.isDirectory()) walk(root, p, acc);
    else acc.set(relative(root, p), { sha256: createHash("sha256").update(readFileSync(p)).digest("hex"), bytes: st.size });
  }
  return acc;
}

/** Every path under root (files and symlinks; folders), relative, without following symlinks. */
export function tree(root: string, dir = root, acc = { files: [] as string[], dirs: [] as string[] }) {
  for (const name of readdirSync(dir).sort()) {
    const p = join(dir, name);
    if (lstatSync(p).isDirectory()) {
      acc.dirs.push(relative(root, p));
      tree(root, p, acc);
    } else acc.files.push(relative(root, p));
  }
  return acc;
}

/** Remove the listed paths and nothing else: files first, then the (by then empty) folders, deepest first. */
function removeWritten(out: string, t: { files: string[]; dirs: string[] }) {
  for (const f of t.files) unlinkSync(join(out, f));
  for (const d of [...t.dirs].sort((x, y) => y.length - x.length)) rmdirSync(join(out, d));
}

const LEDGERS = join(tmpdir(), "tdd-parity-gate");
const ledger = (out: string) => join(LEDGERS, `${createHash("sha256").update(out).digest("hex").slice(0, 24)}.json`);

/**
 * Ready an --out folder without touching anything the gate did not write: a missing or empty folder is used as is;
 * one holding only paths the gate's ledger says it wrote has those removed; anything else is refused.
 */
export function prepareOut(out: string): void {
  if (!existsSync(out)) return;
  const t = tree(out);
  if (t.files.length + t.dirs.length === 0) return;
  let owned = new Set<string>();
  try {
    const l = JSON.parse(readFileSync(ledger(out), "utf8")) as { out: string; written: string[] };
    if (l.out === out) owned = new Set(l.written);
  } catch {
    // no ledger: nothing in the folder is the gate's
  }
  const foreign = [...t.files, ...t.dirs].filter((p) => !owned.has(p));
  if (foreign.length)
    throw new Error(`${out} holds ${foreign.length} path(s) the gate did not write (${foreign.slice(0, 3).join(", ")}${foreign.length > 3 ? ", ..." : ""}): refusing to touch it (pass a new or empty folder)`);
  removeWritten(out, t);
}

function recordOut(out: string): void {
  if (!existsSync(out)) return;
  const t = tree(out);
  mkdirSync(LEDGERS, { recursive: true });
  writeFileSync(ledger(out), JSON.stringify({ out, written: [...t.files, ...t.dirs] }));
}

/** One compile into `out`, which must be missing or empty (gate() sees to that). */
export function runCompiler(which: string, packet: string, src: string, out: string, env: Record<string, string>, args: string[] = []): Snapshot {
  const t0 = performance.now();
  const r = spawnSync(command(which, src, out, args), { cwd: packet, env: { ...process.env, ...env }, stdout: "pipe", stderr: "pipe" });
  const seconds = (performance.now() - t0) / 1000;
  const has = existsSync(out);
  const read = (f: string) => (has && existsSync(join(out, f)) ? readFileSync(join(out, f)) : null);
  return {
    compiler: which,
    exit: r.exitCode ?? -1,
    stdout: r.stdout.toString(),
    stderr: r.stderr.toString(),
    files: has ? walk(out) : new Map(),
    content: read("content.json"),
    manifest: read("content.files.json"),
    seconds,
  };
}

const BUILD_TDD = join(TDD, "scripts", "build_tdd.py");
const DEPRECATED_SPLIT = 'by = re.split(r"[,·]", status_text, 1)';

/**
 * The two stderr lines CPython prints for build_tdd.py's positional maxsplit, as warnings.formatwarning writes them
 * (`<file>:<line>: DeprecationWarning: <message>` and the stripped source line indented by two spaces); null once the
 * Python no longer has that call.
 */
export function deprecationPair(script = BUILD_TDD): [string, string] | null {
  const lines = readFileSync(script, "utf8").split("\n");
  const k = lines.findIndex((l) => l.includes(DEPRECATED_SPLIT));
  return k < 0 ? null : [`${script}:${k + 1}: DeprecationWarning: 'maxsplit' is passed as positional argument`, `  ${lines[k]!.trim()}`];
}

/** stderr without that one interpreter warning (Python runs only): the compiler's own lines, and what was taken out. */
export function splitStderr(stderr: string, compiler = "python", pair: [string, string] | null = compiler === "python" ? deprecationPair() : null): { own: string; interpreter: string[] } {
  const lines = stderr.split("\n");
  if (!pair) return { own: stderr, interpreter: [] };
  const own: string[] = [];
  const interpreter: string[] = [];
  for (let i = 0; i < lines.length; i++) {
    if (lines[i] === pair[0] && lines[i + 1] === pair[1]) {
      interpreter.push(lines[i]!, lines[++i]!);
    } else own.push(lines[i]!);
  }
  return { own: own.join("\n"), interpreter };
}

function firstDiff(a: Buffer, b: Buffer): string {
  const n = Math.min(a.length, b.length);
  let i = 0;
  while (i < n && a[i] === b[i]) i++;
  const line = a.subarray(0, i).toString("utf8").split("\n").length;
  const ctx = (x: Buffer) => JSON.stringify(x.subarray(Math.max(0, i - 40), i + 40).toString("utf8"));
  return `first difference at byte ${i} (line ${line}; ${a.length} vs ${b.length} bytes): A ${ctx(a)} B ${ctx(b)}`;
}

/** The summary after "wrote <path>/content.json: " on stdout. */
export function summaryLine(stdout: string): string {
  const line = stdout.split("\n").find((l) => l.startsWith("wrote ")) ?? "";
  const k = line.indexOf("content.json: ");
  return k < 0 ? "" : line.slice(k + "content.json: ".length);
}

export function compare(a: Snapshot, b: Snapshot): string[] {
  const p: string[] = [];
  if (a.exit !== 0) p.push(`compiler A (${a.compiler}) exited ${a.exit}: ${a.stderr.trim().split("\n").slice(-3).join(" | ")}`);
  if (b.exit !== 0) p.push(`compiler B (${b.compiler}) exited ${b.exit}: ${b.stderr.trim().split("\n").slice(-3).join(" | ")}`);
  if (a.exit !== b.exit) p.push(`exit codes differ: ${a.exit} vs ${b.exit}`);
  if (a.stdout !== b.stdout) p.push(`stdout differs:\n  A ${JSON.stringify(a.stdout)}\n  B ${JSON.stringify(b.stdout)}`);
  const ea = splitStderr(a.stderr, a.compiler).own;
  const eb = splitStderr(b.stderr, b.compiler).own;
  if (ea !== eb) {
    const wa = ea.split("\n");
    const wb = eb.split("\n");
    const k = wa.findIndex((l, i) => l !== wb[i]);
    p.push(`stderr differs at line ${k + 1}:\n  A ${JSON.stringify(wa[k] ?? null)}\n  B ${JSON.stringify(wb[k] ?? null)}`);
  }
  if (a.content && b.content && !a.content.equals(b.content)) p.push(`content.json: ${firstDiff(a.content, b.content)}`);
  if (a.manifest && b.manifest && !a.manifest.equals(b.manifest)) p.push(`content.files.json: ${firstDiff(a.manifest, b.manifest)}`);
  if (!a.content || !b.content) p.push("content.json missing from a run");
  if (!a.manifest || !b.manifest) p.push("content.files.json missing from a run");
  for (const [f, x] of a.files) {
    const y = b.files.get(f);
    if (!y) p.push(`only in A: ${f}`);
    else if (y.sha256 !== x.sha256) p.push(`sha256 differs: ${f} (${x.bytes} vs ${y.bytes} bytes)`);
  }
  for (const f of b.files.keys()) if (!a.files.has(f)) p.push(`only in B: ${f}`);
  return p;
}

export function gate(packetDir: string, o: { src?: string; a?: string; b?: string; out?: string; keep?: boolean; generated?: string; expectLine?: string; args?: string[] } = {}): GateResult {
  const packet = resolve(packetDir);
  const src = o.src ?? `${basename(packet)}.md`;
  if (!existsSync(resolve(packet, src))) throw new Error(`${resolve(packet, src)}: no such source (pass --src)`);
  const created = o.out === undefined;
  const out = o.out ? resolve(o.out) : join(mkdtempSync(join(tmpdir(), `tdd-gate-${basename(packet)}-`)), "build");
  const env = { TDD_GENERATED: o.generated ?? "2000-01-01T00:00:00Z", TZ: "UTC" };
  if (!created) prepareOut(out);
  try {
    const A = runCompiler(o.a ?? "python", packet, src, out, env, o.args);
    if (existsSync(out)) removeWritten(out, tree(out)); // exactly what A wrote: the folder was new or empty before it
    const B = runCompiler(o.b ?? "ts", packet, src, out, env, o.args);
    if (!created) recordOut(out);
    const problems = compare(A, B);
    const notes = [A, B].flatMap((x) => splitStderr(x.stderr, x.compiler).interpreter.filter((l) => !l.startsWith("  ")).map((l) => `${x.compiler} interpreter warning (not compared): ${l}`));
    const summary = summaryLine(B.stdout);
    if (o.expectLine !== undefined && summary !== o.expectLine) problems.push(`summary line changed:\n  expected ${o.expectLine}\n  got      ${summary}`);
    const strip = ({ files, content, manifest, ...rest }: Snapshot) => ({ ...rest, files: files.size });
    return { green: problems.length === 0, packet, out, summary, problems, notes, a: strip(A), b: strip(B) };
  } finally {
    if (created && !o.keep) rmSync(dirname(out), { recursive: true, force: true });
  }
}

function main(argv: string[]): number {
  const o: Parameters<typeof gate>[1] & { json?: string } = {};
  const pos: string[] = [];
  const keys: Record<string, keyof typeof o> = { "--src": "src", "--a": "a", "--b": "b", "--out": "out", "--generated": "generated", "--expect-line": "expectLine", "--json": "json" };
  for (let i = 0; i < argv.length; i++) {
    const x = argv[i]!;
    if (x === "--") {
      o.args = argv.slice(i + 1);
      break;
    }
    if (x === "--keep") o.keep = true;
    else if (x in keys) {
      const v = argv[++i];
      if (v === undefined) return usage(`${x} needs a value`);
      (o as Record<string, unknown>)[keys[x]!] = v;
    } else if (x.startsWith("--")) return usage(`unknown option ${x}`);
    else pos.push(x);
  }
  if (pos.length !== 1) return usage("one packet folder, please");
  let r: GateResult;
  try {
    r = gate(pos[0]!, o);
  } catch (e) {
    console.error(`gate: ${(e as Error).message}`);
    return 2;
  }
  const name = basename(r.packet);
  console.log(`gate ${name}: A=${r.a.compiler} (${r.a.seconds.toFixed(2)} s, exit ${r.a.exit}, ${r.a.files} files)  B=${r.b.compiler} (${r.b.seconds.toFixed(2)} s, exit ${r.b.exit}, ${r.b.files} files)  out=${o.keep ? r.out : "<temp>"}`);
  console.log(`  summary: ${r.summary}`);
  for (const p of r.problems) console.log(`  DIFF ${p}`);
  for (const n of r.notes) console.log(`  note: ${n}`);
  console.log(r.green ? `gate ${name}: GREEN - content.json, content.files.json, ${r.b.files} files by sha256, stdout and stderr identical` : `gate ${name}: RED - ${r.problems.length} difference(s)`);
  if (o.json) Bun.write(o.json, JSON.stringify(r, null, 2) + "\n");
  return r.green ? 0 : 1;
}

function usage(msg: string): number {
  console.error(`gate: ${msg}\nusage: bun run parity/gate.ts <packet-dir> [--src f.md] [--a python|ts|exe] [--b python|ts|exe] [--out dir] [--keep] [--generated stamp] [--expect-line text] [--json report] [-- compiler flags]`);
  return 2;
}

if (import.meta.main) process.exit(main(Bun.argv.slice(2)));
