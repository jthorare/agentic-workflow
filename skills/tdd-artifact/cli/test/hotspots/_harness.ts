// Shared helpers for the hotspot parity tests: run the Python script (python3 -I) and the TS command (bun bin/tdd.ts)
// the same way, copy packets, snapshot trees, find the two real packets without naming their owners' folders.
import { createHash } from "node:crypto";
import { cpSync, existsSync, lstatSync, mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { join, relative } from "node:path";

export const CLI = join(import.meta.dir, "..", "..");
export const SKILL = join(CLI, "..");
export const SCRIPTS = join(SKILL, "scripts");

export interface Run { code: number; out: string; err: string }
const dec = new TextDecoder();

function spawn(cmd: string[], cwd: string, env: Record<string, string | undefined>, input?: string): Run {
  const full: Record<string, string> = {};
  for (const [k, v] of Object.entries({ ...process.env, ...env })) if (v !== undefined) full[k] = v;
  const r = Bun.spawnSync({ cmd, cwd, env: full,
    stdin: input === undefined ? "ignore" : new TextEncoder().encode(input), stdout: "pipe", stderr: "pipe" });
  return { code: r.exitCode ?? -1, out: dec.decode(r.stdout), err: dec.decode(r.stderr) };
}
/** python3 -I scripts/<script> args, in cwd. */
export const py = (script: string, args: string[], cwd: string, env: Record<string, string | undefined> = {}): Run =>
  spawn(["python3", "-I", join(SCRIPTS, script), ...args], cwd, env);
/** bun bin/tdd.ts <command> args, in cwd. */
export const ts = (command: string, args: string[], cwd: string, env: Record<string, string | undefined> = {}): Run =>
  spawn(["bun", join(CLI, "bin", "tdd.ts"), command, ...args], cwd, env);
/** python3 -I -c code with a JSON payload on stdin; returns the parsed JSON it prints. */
export function pyJson<T>(code: string, payload: unknown, cwd: string = tmpdir()): T {
  const r = spawn(["python3", "-I", "-c", code], cwd, {}, JSON.stringify(payload));
  if (r.code !== 0) throw new Error(`python3 failed (${r.code}): ${r.err}`);
  return JSON.parse(r.out) as T;
}

export const tempDir = (tag: string): string => mkdtempSync(join(tmpdir(), `tdd-hotspots-${tag}-`));
export const rmrf = (p: string): void => rmSync(p, { recursive: true, force: true });
export const copy = (src: string, dst: string): void => cpSync(src, dst, { recursive: true, preserveTimestamps: true });

/** Every path under root (sorted), with its kind and the sha256 of each file's bytes. */
export function snapshot(root: string): string[] {
  const out: string[] = [];
  const walk = (d: string) => {
    for (const name of readdirSync(d).sort()) {
      const p = join(d, name), st = lstatSync(p), rel = relative(root, p);
      if (st.isDirectory()) { out.push(`d ${rel}`); walk(p); }
      else out.push(`f ${rel} ${createHash("sha256").update(readFileSync(p)).digest("hex")}`);
    }
  };
  if (existsSync(root)) walk(root);
  return out;
}
export const readUtf8 = (p: string): string => readFileSync(p, "utf8");

/** The packets the parity tests run on: $TDD_HOTSPOTS_PACKETS (colon-separated), else the open-shift-ranking reference
 * packet and the tdd-system architecture packet under $SYSTEM_DESIGN_OUT or ~/.system-design, wherever their
 * repo-slug folder is. A packet needs diagrams/ and build/content.json. */
export function packets(): string[] {
  const env = process.env.TDD_HOTSPOTS_PACKETS;
  let found: string[] = [];
  if (env) found = env.split(":").filter(Boolean);
  else {
    const root = process.env.SYSTEM_DESIGN_OUT || join(homedir(), ".system-design");
    let repos: string[] = [];
    try { repos = readdirSync(root).sort(); } catch { return []; }
    for (const slug of ["open-shift-ranking", "tdd-system"]) {
      for (const r of repos) {
        const p = join(root, r, "system-design", slug);
        if (existsSync(join(p, "diagrams")) && existsSync(join(p, "build", "content.json"))) { found.push(p); break; }
      }
    }
  }
  return found.filter((p) => existsSync(join(p, "diagrams")) && existsSync(join(p, "build", "content.json")));
}
/** Copy what the hotspot tools read (diagrams/, build/content.json, the top-level markdown) into dst. */
export function copyPacket(src: string, dst: string): void {
  copy(join(src, "diagrams"), join(dst, "diagrams"));
  copy(join(src, "build", "content.json"), join(dst, "build", "content.json"));
  for (const n of readdirSync(src)) if (n.endsWith(".md")) copy(join(src, n), join(dst, n));
}

/** Python that loads scripts/<script> as a module and runs main(argv) for each argv on stdin, capturing stdout,
 * stderr and the exit status the interpreter would report (SystemExit with a message: the message on stderr, 1; any
 * other exception: "EXC <type>" on stderr, 1). Prints [[code, out, err], ...]. */
export const pyMainLoop = (script: string): string => String.raw`
import contextlib, importlib.util, io, json, sys
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("m", ${JSON.stringify(join(SCRIPTS, script))})
m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
res = []
for argv in json.load(sys.stdin):
    o, e, code = io.StringIO(), io.StringIO(), 0
    with contextlib.redirect_stdout(o), contextlib.redirect_stderr(e):
        try:
            rc = m.main(argv)
            code = rc if isinstance(rc, int) else 0
        except SystemExit as x:
            if isinstance(x.code, str): e.write(x.code + "\n"); code = 1
            else: code = x.code or 0
        except Exception as x:
            code = 1; e.write("EXC " + type(x).__name__)
    res.append([code, o.getvalue(), e.getvalue()])
print(json.dumps(res))
`;
/** Compare a TS run with a pyMainLoop row; a Python exception only needs the same type in the TS traceback. */
export function sameRun(got: Run, want: [number, string, string]): boolean {
  if (got.code !== want[0] || got.out !== want[1]) return false;
  if (want[2].startsWith("EXC ")) return new RegExp(`\\n${want[2].slice(4)}(: |\\n)`).test(got.err);
  return got.err === want[2];
}

/** A small seeded PRNG (mulberry32) so every run draws the same cases. */
export function rng(seed: number) {
  let a = seed >>> 0;
  const next = () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  const int = (lo: number, hi: number) => lo + Math.floor(next() * (hi - lo + 1));
  const pick = <T>(xs: readonly T[]): T => xs[int(0, xs.length - 1)]!;
  return { next, int, pick };
}
