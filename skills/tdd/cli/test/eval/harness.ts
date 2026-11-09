// Shared helpers for the eval tests: find the parity packets, snapshot one into a temp tree, run the Python eval and
// the TypeScript eval one after the other into the same out path, and compare the written files with the volatile
// fields removed.
import { cpSync, existsSync, mkdtempSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import * as py from "../../src/py";
import { python } from "../../src/eval/artifact";

export { python };

export const SKILL = fileURLToPath(new URL("../../../", import.meta.url)).replace(/\/$/, "");
export const CLI = join(SKILL, "cli");
export const PY_EVAL = join(SKILL, "scripts", "eval_packet.py");
export const TS_EVAL = join(CLI, "bin", "eval-packet.ts");
export const ORACLE = join(CLI, "test", "eval", "oracle_eval.py");
/** The interpreter the eval's rule picks for this environment ($TDD_PYTHON, else the tdd-skill venv, else python3 on
 * PATH): the oracle runs under it, and `both` runs eval_packet.py under python(env) for the env it is given, so the
 * Python's sys.executable and the TypeScript's choice are each derived, never assumed equal. */
export const PYTHON = python();
export const HAVE_PYTHON = Bun.spawnSync([PYTHON, "-I", "-c", "pass"]).exitCode === 0 && existsSync(PY_EVAL);

/** The volatile fields eval_packet.py names in its records: timestamps (captured, generated, at) and durations
 * (seconds, and the flattened `<scorer>.seconds` / Braintrust `<scorer>_seconds` metrics). */
export const VOLATILE = new Set(["captured", "generated", "at", "seconds"]);
const volatile = (k: string): boolean => VOLATILE.has(k) || k.endsWith(".seconds") || k.endsWith("_seconds");

/** Packet folders to compare on: TDD_PARITY_PACKETS (":"-separated), else TDD_REAL_PACKETS; [] skips the tests. */
export function packets(): string[] {
  const env = process.env.TDD_PARITY_PACKETS || process.env.TDD_REAL_PACKETS || "";
  return env.split(":").filter((p) => p && existsSync(join(p, "build", "content.json")));
}

export const label = (p: string): string => basename(p);

/** A copy of the packet at <tmp>/<repo-slug>/system-design/<slug> (the same slug and repo slug), so a rebuild of the
 * live packet by another lane cannot land between the two runs, and nothing is written next to the real packet. */
export function snapshot(packet: string, tmp: string): string {
  const parent = dirname(packet);
  const repoSlug = basename(parent) === "system-design" ? basename(dirname(parent)) : basename(parent);
  const dst = join(tmp, "pk", repoSlug, "system-design", basename(packet));
  cpSync(packet, dst, { recursive: true, verbatimSymlinks: true, filter: (src) => !src.includes(`${basename(packet)}/build/eval`) });
  return dst;
}

const made: string[] = [];
/** A fresh temp folder; each test file removes its own in `afterAll(cleanup)` (TDD_EVAL_KEEP=1 keeps them). */
export const tmp = (tag: string): string => {
  const d = mkdtempSync(join(tmpdir(), `tdd-eval-${tag}-`));
  made.push(d);
  return d;
};
/** Remove the temp folders made so far (only folders this harness created with mkdtemp). */
export function cleanup(): void {
  if (process.env.TDD_EVAL_KEEP) return;
  for (const d of made.splice(0)) rmSync(d, { recursive: true, force: true });
}

export interface Run { code: number; out: string; err: string; dir: string }

/** A clean environment: PATH, HOME, TZ, LANG and TDD_PYTHON when set (no key of any kind), plus `extra`. */
export function cleanEnv(extra: Record<string, string> = {}): Record<string, string> {
  return { PATH: process.env.PATH ?? "", HOME: process.env.HOME ?? "", TZ: "UTC", LANG: "en_US.UTF-8",
    ...(process.env.TDD_PYTHON ? { TDD_PYTHON: process.env.TDD_PYTHON } : {}), ...extra };
}

function run(cmd: string[], env: Record<string, string>): Omit<Run, "dir"> {
  const r = Bun.spawnSync(cmd, { cwd: SKILL, env, stdout: "pipe", stderr: "pipe" });
  return { code: r.exitCode ?? -1, out: r.stdout.toString(), err: r.stderr.toString() };
}

/** Python then TypeScript, each into <tmp>/out, moved to <tmp>/py and <tmp>/ts afterwards. */
export function both(packet: string, base: string, args: string[], env: Record<string, string> = cleanEnv()): { py: Run; ts: Run } {
  const out = join(base, "out");
  const p = run([python(env), "-I", "-B", PY_EVAL, packet, "--out", out, ...args], env);
  if (existsSync(out)) renameSync(out, join(base, "py"));
  const t = run(["bun", TS_EVAL, packet, "--out", out, ...args], env);
  if (existsSync(out)) renameSync(out, join(base, "ts"));
  return { py: { ...p, dir: join(base, "py") }, ts: { ...t, dir: join(base, "ts") } };
}

function strip(v: unknown): unknown {
  if (v instanceof Map) return new Map([...v].filter(([k]) => !volatile(String(k))).map(([k, x]) => [k, strip(x)]));
  if (Array.isArray(v)) return v.map(strip);
  return v;
}

/** A written JSON file, parsed losslessly (key order, int vs float) with the volatile fields removed, dumped back. */
export function canonical(file: string): string {
  return py.json.dumps(strip(py.json.loads(readFileSync(file, "utf8"))), { indent: 1, ensureAscii: false });
}

export const jsonFiles = (dir: string): string[] => (existsSync(dir) ? py.sorted(readdirSync(dir).filter((f) => f.endsWith(".json"))) : []);

/** The first differing line of two canonical dumps, for a readable failure. */
export function firstDiff(a: string, b: string): string | null {
  if (a === b) return null;
  const la = a.split("\n");
  const lb = b.split("\n");
  for (let i = 0; i < Math.max(la.length, lb.length); i++) if (la[i] !== lb[i]) return `line ${i}: python ${la[i]?.slice(0, 200)} | ts ${lb[i]?.slice(0, 200)}`;
  return "length";
}

/** history.jsonl's last line without its time stamp. */
export function lastHistory(dir: string): string {
  const lines = readFileSync(join(dir, "history.jsonl"), "utf8").trim().split("\n");
  return py.json.dumps(strip(py.json.loads(lines[lines.length - 1]!)));
}

/** The golden JSON of one oracle_eval.py suite. */
export function oracle<T = any>(suite: string, args: string[] = [], env: Record<string, string> = cleanEnv(), interp: string = PYTHON): T { // eslint-disable-line @typescript-eslint/no-explicit-any
  const r = Bun.spawnSync([interp, "-I", "-B", ORACLE, suite, ...args], { env, stdout: "pipe", stderr: "pipe" });
  if (r.exitCode !== 0) throw new Error(`oracle ${suite}: ${r.stderr.toString()}`);
  return JSON.parse(r.stdout.toString()) as T;
}

/** Commands still running whose line contains `marker`, polled for up to `waitMs` until none is left (a SIGKILLed
 * group is reaped within milliseconds; the wait only absorbs a slow `ps`). */
export async function leftover(marker: string, waitMs = 2000): Promise<string[]> {
  const t0 = performance.now();
  for (;;) {
    const hits = Bun.spawnSync(["ps", "-axo", "command"]).stdout.toString().split("\n").filter((l) => l.includes(marker));
    if (!hits.length || performance.now() - t0 > waitMs) return hits;
    await Bun.sleep(50);
  }
}

/** Point a snapshot's meta.json at another repo (a scratch repo: never a real one with --run-tests). */
export function withRepo(packet: string, repoPath: string): void {
  const f = join(packet, "meta.json");
  const meta = py.json.loads(readFileSync(f, "utf8")) as Map<string, unknown>;
  meta.set("repo_path", repoPath);
  meta.delete("repo_url");
  writeFileSync(f, py.json.dumps(meta, { indent: 2, ensureAscii: false }) + "\n");
}

/** A scratch git repo on branch main with `files`, one commit. */
export function scratchRepo(dir: string, files: Record<string, string>): string {
  for (const [name, body] of Object.entries(files)) writeFileSync(join(dir, name), body);
  const git = (...a: string[]) => {
    const r = Bun.spawnSync(["git", "-C", dir, ...a], { env: cleanEnv({ GIT_AUTHOR_NAME: "t", GIT_AUTHOR_EMAIL: "t@example.invalid",
      GIT_COMMITTER_NAME: "t", GIT_COMMITTER_EMAIL: "t@example.invalid", GIT_CONFIG_GLOBAL: "/dev/null", GIT_CONFIG_NOSYSTEM: "1" }) });
    if (r.exitCode !== 0) throw new Error(`git ${a.join(" ")}: ${r.stderr.toString()}`);
  };
  git("init", "-q", "-b", "main");
  git("add", "-A");
  git("commit", "-q", "-m", "fixture");
  return dir;
}
