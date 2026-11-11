// tdd new vs new_packet.py: each scenario runs the Python into a temp root, snapshots the tree (every path and the
// sha256 of every file), removes it, runs the TS into the same root and compares stdout, stderr, exit code and tree.
// TDD_TODAY pins the date (one scenario leaves it unset). Repos: GitHub ssh, GitLab https with a user, Bitbucket
// (no repo_url), a clone whose origin/HEAD is behind HEAD, no origin and no user.name, no commit, not a repo.
// Then the refusals and the argparse surface.
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { py, rmrf, snapshot, tempDir, ts, type Run } from "./_harness";

let T = "";
let env: Record<string, string | undefined> = {};
const R = () => join(T, "root");

function git(cwd: string, ...args: string[]) {
  const r = Bun.spawnSync({ cmd: ["git", ...args], cwd, env: { ...process.env, ...env, GIT_AUTHOR_NAME: "C", GIT_AUTHOR_EMAIL: "c@example.com",
    GIT_COMMITTER_NAME: "C", GIT_COMMITTER_EMAIL: "c@example.com", GIT_AUTHOR_DATE: "2026-01-02T03:04:05Z", GIT_COMMITTER_DATE: "2026-01-02T03:04:05Z" } });
  if (r.exitCode !== 0) throw new Error(`git ${args.join(" ")}: ${r.stderr}`);
}
function repo(name: string, opts: { origin?: string; user?: string; commit?: boolean } = {}): string {
  const d = join(T, name);
  mkdirSync(d, { recursive: true });
  git(d, "init", "-q", "-b", "main");
  if (opts.user) git(d, "config", "user.name", opts.user);
  if (opts.origin) git(d, "remote", "add", "origin", opts.origin);
  if (opts.commit !== false) { writeFileSync(join(d, "f"), name); git(d, "add", "f"); git(d, "commit", "-qm", "init"); }
  return d;
}

beforeAll(() => {
  T = tempDir("new");
  const home = join(T, "home");
  mkdirSync(home);
  env = { HOME: home, XDG_CONFIG_HOME: join(home, ".config"), GIT_CONFIG_NOSYSTEM: "1", TDD_TODAY: "2026-11-08", SYSTEM_DESIGN_OUT: "",
    // argparse wraps usage and help to COLUMNS (else the terminal, else 80); the TS renders the 80-column form.
    COLUMNS: undefined, LINES: undefined };
  repo("gh", { origin: "git@github.com:owner/some-repo.git", user: "Test Author" });
  repo("gl", { origin: "https://user@gitlab.com/group/sub/proj.git", user: "Ann O'Neil" });
  repo("bb", { origin: "ssh://git@bitbucket.org/team/x.git", user: "B" });
  const bare = join(T, "remote.git");
  mkdirSync(bare);
  git(bare, "init", "-q", "--bare", "-b", "main");
  const seedRepo = repo("seed", { origin: bare, user: "S" });
  git(seedRepo, "push", "-q", "origin", "main");
  git(T, "clone", "-q", bare, "clone");
  git(join(T, "clone"), "config", "user.name", "Cloner");
  writeFileSync(join(T, "clone", "g"), "ahead");
  git(join(T, "clone"), "add", "g");
  git(join(T, "clone"), "commit", "-qm", "ahead");
  repo("noorigin");
  repo("empty", { commit: false, user: "E" });
  mkdirSync(join(T, "plain"));
});
afterAll(() => rmrf(T));

/** Run each argv in order with the Python, snapshot, clear the root, then the same with the TS; compare everything. */
function parity(argvs: string[][], opts: { cwd?: string; env?: Record<string, string | undefined>; setup?: () => void } = {}): Run[] {
  const cwd = opts.cwd ?? T, e = { ...env, ...(opts.env ?? {}) };
  rmrf(R()); opts.setup?.();
  const p = argvs.map((a) => py("new_packet.py", a, cwd, e));
  const pt = snapshot(R());
  rmrf(R()); opts.setup?.();
  const t = argvs.map((a) => ts("new", a, cwd, e));
  expect(t).toEqual(p);
  expect(snapshot(R())).toEqual(pt);
  return p;
}

describe("tdd new", () => {
  test("GitHub origin, em-dash title, ticket: same tree, same output", () => {
    const [r] = parity([["my-packet", "--title", "Hold ranking — the queue is the finding", "--ticket", "P-ABC-123", "--repo", join(T, "gh"), "--root", R()]]);
    expect(r!.code).toBe(0);
    expect(snapshot(R()).length).toBeGreaterThan(0);
  });
  test("GitLab https origin with a user, --opt=value, long title with a generic tail", () => {
    const [r] = parity([["other", `--title=A Very Long Title About Open Shift Ranking Technical Design Document`, `--repo=${join(T, "gl")}`, `--root=${R()}`]]);
    expect(r!.out).toContain("repo_url https://gitlab.com/group/sub/proj");
  });
  test("Bitbucket origin (no repo_url), abbreviated options, padded ticket, ': ' split", () => {
    parity([["p3", "--tit", "Ledger notes: the queue is the finding", "--tic", "  X-1 ", "--re", join(T, "bb"), "--ro", R()]]);
    parity([["p4", "--title", "Payments For The New Ledger System Design", "--repo", join(T, "bb"), "--root", R()]]);
    parity([["p5", "--title", "One two three four five Technical Spec", "--repo", join(T, "bb"), "--root", R()]]);
  });
  test("clone with origin/HEAD behind HEAD, default root under HOME, no title", () => {
    const [r] = parity([["a-b-c-d"]], { cwd: join(T, "clone"), env: { SYSTEM_DESIGN_OUT: "" }, setup: () => rmrf(join(env.HOME!, ".system-design")) });
    expect(r!.out).toContain("origin/HEAD");
    expect(r!.out).toContain("~/.system-design/");
  });
  test("no origin and no user.name, root from SYSTEM_DESIGN_OUT, placeholders inside the title", () => {
    parity([["np", "--title", "Uses {{slug}} and {{date}} and {{sha}}", "--repo", join(T, "noorigin")]], { env: { SYSTEM_DESIGN_OUT: R() } });
  });
  test("repo without a commit, and a folder that is not a repo (cwd default)", () => {
    parity([["e1", "--repo", join(T, "empty"), "--root", R()]]);
    const [r] = parity([["plain-one", "--root", R()]], { cwd: join(T, "plain") });
    expect(r!.out).toContain("none recorded");
  });
  test("TDD_TODAY unset: both use today's local date", () => {
    parity([["today", "--repo", join(T, "gh"), "--root", R()]], { env: { TDD_TODAY: "" } });
  });
  test("refusals: existing packet, a file in the way, bad slug, empty ticket, missing repo", () => {
    const again = parity([["dup", "--repo", join(T, "gh"), "--root", R()], ["dup", "--repo", join(T, "gh"), "--root", R()]]);
    expect(again[1]!.code).toBe(1);
    parity([["f", "--repo", join(T, "gh"), "--root", R()]], {
      setup: () => { mkdirSync(join(R(), "owner-some-repo", "system-design"), { recursive: true }); writeFileSync(join(R(), "owner-some-repo", "system-design", "f"), "x"); },
    });
    for (const argv of [["Bad_Slug"], ["-1"], ["s", "--ticket", "   "], ["s", "--repo", join(T, "nope"), "--root", R()]]) parity([argv]);
  });
  test("argparse surface: help, missing and extra arguments, unknown and ambiguous options, --", () => {
    for (const argv of [[], ["-h"], ["--help"], ["--he"], ["s", "-h", "--bogus"], ["--title"], ["s", "--title", "--ticket", "x"],
      ["--t", "x", "s"], ["--r", "x", "s"], ["s", "x"], ["s", "--bogus"], ["--bogus"], ["s", "-x"], ["s", "--", "--title"],
      ["--help=x"], ["--title", "-- x", "s", "--root", R(), "--repo", join(T, "gh")], ["--title=", "s2", "--root", R(), "--repo", join(T, "gh")],
      ["--", "s3", "--root", R()], ["s", "--root", R(), "--repo", join(T, "gh"), "--title", "--", "x"]]) {
      parity([argv]);
    }
  });
});
