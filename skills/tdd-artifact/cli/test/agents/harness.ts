// Shared helpers for the agents-lane parity tests: find packets, run the Python and the TypeScript, hash trees.
import { createHash } from "node:crypto";
import { existsSync, mkdtempSync, readFileSync, readdirSync, statSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

export const SKILL = fileURLToPath(new URL("../../../", import.meta.url));
export const CLI = join(SKILL, "cli");
export const PY_AGENTS = join(SKILL, "scripts", "write_agents.py");
export const PY_STRIP = join(SKILL, "scripts", "strip_page.py");

export const HAVE_PYTHON = Bun.spawnSync(["python3", "-I", "-c", "pass"]).exitCode === 0 && existsSync(PY_AGENTS);

/** Built packets to compare on: TDD_PARITY_BUILDS (":"-separated), else every ~/.system-design/<project>/system-design/<slug>/build with a content.json. */
export function builds(): string[] {
  const env = process.env.TDD_PARITY_BUILDS;
  if (env) return env.split(":").filter((b) => b && existsSync(join(b, "content.json")));
  const root = join(homedir(), ".system-design");
  const out: string[] = [];
  if (!existsSync(root)) return out;
  for (const proj of readdirSync(root).sort()) {
    const sd = join(root, proj, "system-design");
    if (!existsSync(sd) || !statSync(sd).isDirectory()) continue;
    for (const slug of readdirSync(sd).sort()) {
      const b = join(sd, slug, "build");
      if (existsSync(join(b, "content.json"))) out.push(b);
    }
  }
  return out;
}

/** Built pages to strip: each build's tdd.html plus the packet's reference/*.html exports. */
export function pages(): string[] {
  const out: string[] = [];
  for (const b of builds()) {
    if (existsSync(join(b, "tdd.html"))) out.push(join(b, "tdd.html"));
    const ref = join(b, "..", "reference");
    if (existsSync(ref)) for (const f of readdirSync(ref).sort()) if (f.endsWith(".html")) out.push(join(ref, f));
  }
  return out;
}

export function label(p: string): string {
  return relative(join(homedir(), ".system-design"), p).split("/").slice(-3).join("/");
}

export function tmp(tag: string): string {
  return mkdtempSync(join(tmpdir(), `tdd-agents-${tag}-`));
}

export interface Run { code: number; out: string; err: string }

function run(cmd: string[]): Run {
  const r = Bun.spawnSync(cmd, { cwd: SKILL, stdout: "pipe", stderr: "pipe" });
  return { code: r.exitCode ?? -1, out: r.stdout.toString(), err: r.stderr.toString() };
}
/** Both run from the skill directory: write_agents.py resolves relative paths against the cwd (os.path.relpath). */
export function py(script: string, args: string[]): Run {
  return run(["python3", "-I", "-B", script, ...args]);
}
export function ts(cmd: "agents" | "strip", args: string[]): Run {
  return run(["bun", join(CLI, "bin", "tdd.ts"), cmd, ...args]);
}

/** relative path -> sha256 for every file under dir (sorted by path). */
export function tree(dir: string): Map<string, string> {
  const out = new Map<string, string>();
  const walk = (d: string) => {
    for (const n of readdirSync(d).sort()) {
      const p = join(d, n);
      if (statSync(p).isDirectory()) walk(p);
      else out.set(relative(dir, p), createHash("sha256").update(readFileSync(p)).digest("hex"));
    }
  };
  walk(dir);
  return new Map([...out].sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
}

/** The paths that differ between two trees (missing on either side or a different sha256). */
export function treeDiff(a: Map<string, string>, b: Map<string, string>): string[] {
  const keys = [...new Set([...a.keys(), ...b.keys()])].sort();
  return keys.filter((k) => a.get(k) !== b.get(k)).map((k) => `${k}: ${a.has(k) ? "py" : "-"}/${b.has(k) ? "ts" : "-"}`);
}
