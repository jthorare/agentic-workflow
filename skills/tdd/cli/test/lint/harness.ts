// The lint parity harness: run lint_packet.py (python3 -I -B) and `bun bin/tdd.ts lint` on the same packet, from the
// same cwd, with the same environment (TZ pinned: META/stale-build prints a local time), and compare the exit code,
// stdout, stderr and the --json file byte for byte. The corpus builds the base packet once and copies it per case.
import { chmodSync, cpSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, symlinkSync, utimesSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, relative } from "node:path";

export const CLI = join(import.meta.dir, "..", "..");
export const SKILL = join(CLI, "..");
export const SCRIPTS = join(SKILL, "scripts");
export const FIXTURES = join(import.meta.dir, "fixtures");
/** A zone with a 45-minute offset, so a wrong local-time conversion cannot pass by accident. */
export const TZ = "Pacific/Chatham";
/** Every packet file's mtime after a case is set up (2026-01-05T00:00:00Z), so only the cases about time see time. */
export const T0 = Date.UTC(2026, 0, 5) / 1000;
/** content.json's stamp for every compile in a run (a compiled lint's PARITY note prints it). */
export const GENERATED = "2026-01-05T00:00:00Z";

export const HAVE_PYTHON = Bun.spawnSync(["python3", "-I", "-c", "pass"]).exitCode === 0 && existsSync(join(SCRIPTS, "lint_packet.py"));

export interface Run { code: number; out: string; err: string; json: string | null }
const dec = new TextDecoder();

function spawn(cmd: string[], cwd: string, env: Record<string, string | undefined> = {}): { code: number; out: string; err: string } {
  const full: Record<string, string> = {};
  for (const [k, v] of Object.entries({ ...process.env, COLUMNS: undefined, LINES: undefined, TZ, TDD_GENERATED: GENERATED, ...env })) if (v !== undefined) full[k] = v;
  const r = Bun.spawnSync({ cmd, cwd, env: full, stdin: "ignore", stdout: "pipe", stderr: "pipe" });
  return { code: r.exitCode ?? -1, out: dec.decode(r.stdout), err: dec.decode(r.stderr) };
}

function withJson(run: (json: string) => { code: number; out: string; err: string }, scratch: string, tag: string): Run {
  const json = join(scratch, `${tag}.lint.json`);
  rmSync(json, { force: true });
  const r = run(json);
  return { ...r, json: existsSync(json) ? readFileSync(json, "utf8") : null };
}

/** lint_packet.py <args> --json <scratch>/py.lint.json (the json flag is added only when `json` is true). */
export function pyLint(args: string[], cwd: string, scratch: string, opts: { json?: boolean; script?: string; env?: Record<string, string | undefined> } = {}): Run {
  const script = opts.script ?? join(SCRIPTS, "lint_packet.py");
  return withJson((j) => spawn(["python3", "-I", "-B", script, ...args, ...(opts.json === false ? [] : ["--json", j])], cwd, opts.env), scratch, "py");
}

/** bun bin/tdd.ts lint <args> --json <scratch>/ts.lint.json */
export function tsLint(args: string[], cwd: string, scratch: string, opts: { json?: boolean; env?: Record<string, string | undefined> } = {}): Run {
  return withJson((j) => spawn(["bun", join(CLI, "bin", "tdd.ts"), "lint", ...args, ...(opts.json === false ? [] : ["--json", j])], cwd, opts.env), scratch, "ts");
}

export const tempDir = (tag: string): string => mkdtempSync(join(tmpdir(), `tdd-lint-${tag}-`));
export const rmrf = (p: string): void => {
  // a case may leave a file unreadable (the AGENTS check-failed case): make it removable first
  if (existsSync(p)) walkAll(p, (f) => { try { chmodSync(f, 0o755); } catch { /* gone */ } });
  rmSync(p, { recursive: true, force: true });
};
export const copy = (src: string, dst: string): void => cpSync(src, dst, { recursive: true, preserveTimestamps: true });

/** Every file and folder under p, p included; a symbolic link is neither visited nor followed (a case may link the real
 * skill, which a chmod or an mtime reset must never reach). */
function walkAll(p: string, fn: (f: string) => void): void {
  let st;
  try { st = lstatSync(p); } catch { return; }
  if (st.isSymbolicLink()) return;
  fn(p);
  if (st.isDirectory()) for (const n of readdirSync(p)) walkAll(join(p, n), fn);
}

/** Set every file and folder under root (root included) to mtime t. */
export function touchAll(root: string, t: number): void {
  walkAll(root, (f) => { try { utimesSync(f, t, t); } catch { /* unreadable: skip */ } });
}

/** Run git in a fixture repo with a pinned identity and dates (the repo is test data, never the user's). */
export function git(repo: string, args: string[]): string {
  const r = Bun.spawnSync({
    cmd: ["git", "-c", "init.defaultBranch=main", "-c", "commit.gpgsign=false", "-c", "user.name=Fixture", "-c", "user.email=fixture@example.com", "-C", repo, ...args],
    env: { ...process.env, GIT_AUTHOR_DATE: "2026-01-02T10:00:00Z", GIT_COMMITTER_DATE: "2026-01-02T10:00:00Z" } as Record<string, string>,
    stdout: "pipe", stderr: "pipe",
  });
  if (r.exitCode !== 0) throw new Error(`git ${args.join(" ")}: ${dec.decode(r.stderr)}`);
  return dec.decode(r.stdout).trim();
}

export interface Repo { path: string; sha: string; tree: string }

/** fixtures/repo as a git repository: one commit dated 2026-01-02, an origin remote, and one untracked file. */
export function makeRepo(root: string, name = "repo"): Repo {
  const path = join(root, name);
  copy(join(FIXTURES, "repo"), path);
  git(path, ["init", "-q"]);
  git(path, ["add", "-A"]);
  git(path, ["commit", "-q", "-m", "fixture"]);
  git(path, ["remote", "add", "origin", "https://example.com/demo/queue.git"]);
  writeFileSync(join(path, "src", "untracked.ts"), "export const x = 1;\nexport const y = 2;\n");
  return { path, sha: git(path, ["rev-parse", "--short=10", "HEAD"]), tree: git(path, ["rev-parse", "HEAD^{tree}"]) };
}

/** Compile a packet's source into <packet>/build with build_tdd.py (copying files), TDD_GENERATED pinned. */
export function buildPacket(packet: string, slug: string, scripts: string = SCRIPTS): void {
  const r = spawn(["python3", "-I", "-B", join(scripts, "build_tdd.py"), `${slug}.md`, "build"], packet);
  if (r.code !== 0) throw new Error(`build_tdd.py failed on ${packet}: ${r.err || r.out}`);
}

/** The template: fixtures/base copied, meta.json pointed at the repo, built, a tdd.html beside the build, every mtime T0. */
export function makeTemplate(root: string, repo: Repo): string {
  const t = join(root, "template");
  mkdirSync(t, { recursive: true });
  copy(join(FIXTURES, "base"), t);
  const meta = join(t, "demo-packet", "meta.json");
  writeFileSync(meta, readFileSync(meta, "utf8").replaceAll("@SHA@", repo.sha).replaceAll("@REPO@", repo.path));
  buildPacket(join(t, "demo-packet"), "demo-packet");
  writeFileSync(join(t, "demo-packet", "build", "tdd.html"), "<!doctype html><title>Demo Queue</title><main id=\"redesign\"></main>\n");
  touchAll(t, T0);
  return t;
}

/** Copy the template into a fresh case folder and repoint the build's absolute paths at the copy. */
export function caseFrom(template: string, root: string, name: string): string {
  const dir = join(root, name);
  copy(template, dir);
  const build = join(dir, "demo-packet", "build");
  for (const f of ["content.json", "content.files.json"]) {
    const p = join(build, f);
    if (existsSync(p)) {
      writeFileSync(p, readFileSync(p, "utf8").replaceAll(template + "/", dir + "/"));
      utimesSync(p, T0, T0);
    }
  }
  return dir;
}

export const rel = (root: string, p: string): string => relative(root, p);

/**
 * A copy of the skill at <base>/tdd for a case that needs a different compiler: scripts/ copied (to be patched), cli/
 * linked to the real one or, with fakeCli, a cli whose bin/build-tdd.ts prints fakeCli to stderr and exits 1, every
 * other entry linked to the real skill, and the system-design skill linked as its sibling (where build_tdd.py looks).
 * Returns <base>/tdd. Links are never followed by touchAll or rmrf.
 */
export function skillCopy(base: string, fakeCli: string | null = null): string {
  const skill = join(base, "tdd");
  mkdirSync(skill, { recursive: true });
  for (const n of readdirSync(SKILL)) if (n !== "scripts" && (n !== "cli" || fakeCli === null)) symlinkSync(join(SKILL, n), join(skill, n));
  symlinkSync(join(SKILL, "..", "system-design"), join(base, "system-design"));
  cpSync(SCRIPTS, join(skill, "scripts"), { recursive: true, filter: (s) => !s.includes("__pycache__") });
  if (fakeCli !== null) {
    mkdirSync(join(skill, "cli", "bin"), { recursive: true });
    mkdirSync(join(skill, "cli", "node_modules"));
    writeFileSync(join(skill, "cli", "bin", "build-tdd.ts"), `console.error(${JSON.stringify(fakeCli)});\nprocess.exit(1);\n`);
  }
  return skill;
}
