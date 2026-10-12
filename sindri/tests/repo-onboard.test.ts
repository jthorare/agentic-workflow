import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { stateDir, type Deps } from "../src/deps.js";
import type { GitRunner } from "../src/git.js";
import { withHeavyLock } from "../src/index/heavy-lock.js";
import { indexPath } from "../src/index/db.js";
import { templateDir } from "../src/index/onboard.js";
import { ERRORS, SindriError } from "../src/errors.js";
import { ledgerPath } from "../src/ledger/db.js";
import { runCli } from "../src/main.js";
import { PRE_COMMIT_MARKER, TEMPLATE_MARKER, preCommitHook } from "../src/scrub/commands.js";
import { fakeGit, git, gitRepo, makeDeps, tempDir } from "./helpers.js";
import { approvedIndexDeps, ring0Repo } from "./index-fixtures.js";

async function approve(d: Deps): Promise<void> {
  const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
  await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
}
const hookOf = (repo: string): string => path.join(repo, ".git", "hooks", "pre-commit");
type StepJson = { name: string; status: string; detail: string; fix?: string };
const stepList = (stdout: string): StepJson[] => (JSON.parse(stdout) as { steps: StepJson[] }).steps;
const steps = (stdout: string): Record<string, string> => Object.fromEntries(stepList(stdout).map((s) => [s.name, s.status]));

describe("sindri repo onboard", () => {
  it("adds the repo, stops at approval with the exact command, and does nothing else until approved", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "export const b = 1;\n" });
    const r = await runCli(["repo", "onboard", target, "--name", "web"], d);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toMatch(/done\s+repo-add/);
    expect(r.stdout).toMatch(/warn\s+approval/);
    expect(r.stdout).toMatch(/sindri profile approve [0-9a-f]{12}/);
    expect(r.stdout).toMatch(/skip\s+pre-commit/);
    expect(fs.existsSync(hookOf(target))).toBe(false);
    expect(fs.existsSync(indexPath(d, "web"))).toBe(false);
    // Idempotent: a rerun before approval adds nothing and still stops.
    const again = await runCli(["repo", "onboard", target, "--name", "web", "--json"], d);
    expect(again.exitCode).toBe(1);
    expect(steps(again.stdout)).toEqual({ "repo-add": "ok", approval: "warn", "pre-commit": "skip", "index-build": "skip" });
  });

  it("after approval installs the full hook (replacing a template copy) and builds the index; a rerun is all ok", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "export const b = 1;\n" });
    await runCli(["repo", "onboard", target, "--name", "web"], d);
    await approve(d);
    fs.mkdirSync(path.dirname(hookOf(target)), { recursive: true });
    fs.writeFileSync(hookOf(target), preCommitHook("/x/sindri", { template: true }));
    const r = await runCli(["repo", "onboard", target, "--name", "web", "--json"], d);
    expect(r.exitCode).toBe(0);
    expect(steps(r.stdout)).toEqual({ "repo-add": "ok", approval: "ok", "pre-commit": "done", "index-build": "done" });
    const hook = fs.readFileSync(hookOf(target), "utf8");
    expect(hook).toContain(PRE_COMMIT_MARKER);
    expect(hook).not.toContain(TEMPLATE_MARKER);
    expect(fs.existsSync(indexPath(d, "web"))).toBe(true);
    const again = await runCli(["repo", "onboard", target, "--name", "web", "--json"], d);
    expect(steps(again.stdout)).toEqual({ "repo-add": "ok", approval: "ok", "pre-commit": "ok", "index-build": "ok" });
  });

  it("a busy heavy lock is a warn (never a wait); --no-build skips; a foreign hook fails its step (exit 2) and is left alone", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "1" });
    await runCli(["repo", "onboard", target, "--name", "web"], d);
    await approve(d);
    // Same-process nesting: buildIndex's single try at the lock fails (see heavy-lock.test.ts). If the lock
    // ever treats the same pid as reentrant, hold it from a child process with config/lib/locks.sh instead.
    // Both slots of the default two are held.
    const busy = await withHeavyLock(d, "other-job", 0, () => withHeavyLock(d, "another-job", 0, () => runCli(["repo", "onboard", target, "--name", "web", "--json"], d)));
    expect(busy.exitCode).toBe(1);
    expect(steps(busy.stdout)).toEqual({ "repo-add": "ok", approval: "ok", "pre-commit": "done", "index-build": "warn" });
    expect(steps((await runCli(["repo", "onboard", target, "--name", "web", "--no-build", "--json"], d)).stdout)["index-build"]).toBe("skip");
    fs.writeFileSync(hookOf(target), "#!/bin/sh\necho theirs\n");
    const r = await runCli(["repo", "onboard", target, "--name", "web", "--no-build", "--json"], d);
    expect(r.exitCode).toBe(2);
    expect(r.stdout).toContain("SND-SCRUB-003");
    expect(fs.readFileSync(hookOf(target), "utf8")).toBe("#!/bin/sh\necho theirs\n");
    expect(fs.existsSync(indexPath(d, "web"))).toBe(false);
  });

  it("refuses what repo add refuses (not a repo, bad name) and bad usage", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    expect((await runCli(["repo", "onboard", "/"], d)).stderr).toContain("SND-PROFILE-009");
    expect((await runCli(["repo", "onboard", gitRepo({ "a": "1" }), "--name", "Bad Name"], d)).stderr).toContain("SND-PROFILE-014");
    expect((await runCli(["repo", "onboard", "--template", "x"], d)).stderr).toContain("SND-CLI-002");
    expect((await runCli(["repo", "onboard", "--template", "--name", "x"], d)).stderr).toContain("SND-CLI-002");
    expect((await runCli(["repo", "frob"], d)).stderr).toContain("use add, onboard or status");
  });

  it("an approved entry with the same name but another path is approval pending, not onboarded", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const first = gitRepo({ "b.ts": "1" });
    const second = gitRepo({ "c.ts": "1" });
    await runCli(["repo", "onboard", first, "--name", "web", "--no-build"], d);
    await approve(d);
    // The live profile now points web at another checkout; the approved snapshot still has the first.
    const repoFile = path.join(d.env.AW_STATE_DIR as string, "profile", "repos", "web.yaml");
    fs.writeFileSync(repoFile, fs.readFileSync(repoFile, "utf8").replace(fs.realpathSync(first), fs.realpathSync(second)));
    const r = await runCli(["repo", "onboard", second, "--name", "web", "--json"], d);
    expect(r.exitCode).toBe(1);
    expect(steps(r.stdout)).toEqual({ "repo-add": "ok", approval: "warn", "pre-commit": "skip", "index-build": "skip" });
    expect(fs.existsSync(hookOf(second))).toBe(false);
  });

  it("an error that is not sindri's (a pre-commit that is a directory, a failed mirror) is not swallowed into a step", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "1" });
    await runCli(["repo", "onboard", target, "--name", "web"], d);
    await approve(d);
    fs.mkdirSync(hookOf(target), { recursive: true });
    const r = await runCli(["repo", "onboard", target, "--name", "web", "--no-build"], d);
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain("SND-CLI-900");
    fs.rmdirSync(hookOf(target));
    // A mirror that can't be cloned is SND-INDEX-002: a real failure, not a busy lock.
    const real = d.git;
    const noClone: GitRunner = { run: (args, cwd, o) => (args[0] === "clone" ? Promise.resolve({ ok: false, stderr: "fatal: no clone" }) : real.run(args, cwd, o)) };
    const b = await runCli(["repo", "onboard", target, "--name", "web"], { ...d, git: noClone });
    expect(b.exitCode).toBe(2);
    expect(b.stderr).toContain("SND-INDEX-002");
  });

  it("a hooksPath that leads to another tool's hook (husky) is refused, with the local-only init.sh route in the fix", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "1" });
    git(target, "config", "core.hooksPath", ".husky/_");
    fs.mkdirSync(path.join(target, ".husky", "_"), { recursive: true });
    const foreign = path.join(target, ".husky", "_", "pre-commit");
    fs.writeFileSync(foreign, "#!/bin/sh\n. \"$(dirname \"$0\")/h\"\n");
    await runCli(["repo", "onboard", target, "--name", "web", "--no-build"], d);
    await approve(d);
    // The lines carry the installed binary, quoted, like the real hook (GUI git clients have no ~/.local/bin).
    const r = await runCli(["repo", "onboard", target, "--name", "web", "--no-build", "--json"], { ...d, env: { ...d.env, SINDRI_BIN: "/opt/aw bin/sindri" } });
    expect(r.exitCode).toBe(2);
    const step = stepList(r.stdout).find((s) => s.name === "pre-commit");
    expect(step?.status).toBe("fail");
    expect(step?.detail).toContain("SND-SCRUB-003");
    const common = git(target, "rev-parse", "--path-format=absolute", "--git-common-dir").trim();
    expect(step?.fix).toContain("${XDG_CONFIG_HOME:-~/.config}/husky/init.sh");
    expect(step?.fix).toContain("'/opt/aw bin/sindri' scrub --staged || exit 1");
    expect(step?.fix).toContain("'/opt/aw bin/sindri' shape --record --staged || true");
    expect(step?.fix).toContain("git rev-parse --path-format=absolute --git-common-dir");
    expect(step?.fix).toContain(common);
    expect(step?.fix).toContain("never sets core.hooksPath");
    expect(fs.readFileSync(foreign, "utf8")).toBe("#!/bin/sh\n. \"$(dirname \"$0\")/h\"\n");
    expect(git(target, "config", "--get", "core.hooksPath").trim()).toBe(".husky/_");
    // A plain foreign hook (no hooksPath) keeps the generic fix.
    const plain = gitRepo({ "c.ts": "1" });
    fs.writeFileSync(hookOf(plain), "#!/bin/sh\necho theirs\n");
    await runCli(["repo", "onboard", plain, "--name", "lib", "--no-build"], d);
    await approve(d);
    const p = stepList((await runCli(["repo", "onboard", plain, "--name", "lib", "--no-build", "--json"], d)).stdout).find((s) => s.name === "pre-commit");
    expect(p?.fix).not.toContain("husky");
    // A core.hooksPath that isn't husky's gets the two lines to add by hand; husky's shim text elsewhere still counts.
    const other = gitRepo({ "d.ts": "1" });
    git(other, "config", "core.hooksPath", ".githooks");
    fs.mkdirSync(path.join(other, ".githooks"));
    fs.writeFileSync(path.join(other, ".githooks", "pre-commit"), "#!/bin/sh\necho theirs\n");
    await runCli(["repo", "onboard", other, "--name", "other", "--no-build"], d);
    await approve(d);
    const o = stepList((await runCli(["repo", "onboard", other, "--name", "other", "--no-build", "--json"], d)).stdout).find((s) => s.name === "pre-commit");
    expect(o?.status).toBe("fail");
    expect(o?.fix).toContain(`add these two lines to ${path.join(fs.realpathSync(other), ".githooks", "pre-commit")} yourself`);
    fs.writeFileSync(path.join(other, ".githooks", "pre-commit"), "#!/usr/bin/env sh\n. \"$(dirname -- \"$0\")/_/husky.sh\"\n");
    const h = stepList((await runCli(["repo", "onboard", other, "--name", "other", "--no-build", "--json"], d)).stdout).find((s) => s.name === "pre-commit");
    expect(h?.fix).toContain("husky/init.sh");
    // husky v4 writes its shim into .git/hooks (no core.hooksPath): nothing to route, the generic fix.
    const v4 = gitRepo({ "e.ts": "1" });
    fs.writeFileSync(hookOf(v4), "#!/bin/sh\n# husky\n. \"$(dirname \"$0\")/husky.sh\"\n");
    await runCli(["repo", "onboard", v4, "--name", "v4", "--no-build"], d);
    await approve(d);
    const f = stepList((await runCli(["repo", "onboard", v4, "--name", "v4", "--no-build", "--json"], d)).stdout).find((s) => s.name === "pre-commit");
    expect(f?.fix).toBe(ERRORS["SND-SCRUB-003"].fix);
  });
});

describe("sindri repo onboard: hooksPath and linked worktrees (final review I1, I2)", () => {
  it("never writes a pre-commit into an empty core.hooksPath dir; the step fails with the two lines to add", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ ".githooks/commit-msg": "#!/bin/sh\nexit 0\n" });
    git(target, "config", "core.hooksPath", ".githooks");
    await runCli(["repo", "onboard", target, "--name", "web", "--no-build"], d);
    await approve(d);
    const r = await runCli(["repo", "onboard", target, "--name", "web", "--no-build", "--json"], { ...d, env: { ...d.env, SINDRI_BIN: "/opt/aw bin/sindri" } });
    expect(r.exitCode).toBe(2);
    const step = stepList(r.stdout).find((s) => s.name === "pre-commit");
    expect(step?.status).toBe("fail");
    expect(step?.detail).toContain("SND-SCRUB-003");
    expect(step?.fix).toContain("'/opt/aw bin/sindri' scrub --staged || exit 1");
    expect(fs.readdirSync(path.join(target, ".githooks"))).toEqual(["commit-msg"]);
    expect(git(target, "status", "--porcelain")).toBe("");
  });

  it("run from a linked worktree, adds the main checkout, not the worktree", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "1" });
    const wt = path.join(tempDir(), "wt");
    git(target, "worktree", "add", "-q", wt, "-b", "side");
    const r = await runCli(["repo", "onboard", wt, "--name", "web", "--no-build", "--json"], d);
    expect(r.exitCode).toBe(1);
    expect(stepList(r.stdout)[0].detail).toBe(`web (${fs.realpathSync(target)}) is in the live profile`);
    const repoFile = path.join(d.env.AW_STATE_DIR as string, "profile", "repos", "web.yaml");
    expect(fs.readFileSync(repoFile, "utf8")).toContain(`path: ${fs.realpathSync(target)}\n`);
    await approve(d);
    expect((await runCli(["repo", "status", target], d)).exitCode).toBe(0);
    expect((await runCli(["repo", "status", wt], d)).exitCode).toBe(0);
  });
});

describe("sindri repo status", () => {
  it("nudges once for a repo outside the approved profile, and is silent outside git, before any approval and once onboarded", async () => {
    const fresh = makeDeps();
    const target = gitRepo({ "b.ts": "1" });
    const nudge = (d: Deps, p: string) => runCli(["repo", "status", p, "--nudge"], d);
    expect((await nudge(fresh, target)).stdout).toBe("");
    expect(fs.existsSync(ledgerPath(stateDir(fresh)))).toBe(false); // read-only: never creates the ledger
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    expect((await nudge(d, tempDir())).stdout).toBe("");
    const before = fs.readFileSync(ledgerPath(stateDir(d)));
    const n = await nudge(d, target);
    expect(n.exitCode).toBe(0);
    expect(n.stdout.trim().split("\n")).toHaveLength(1);
    expect(n.stdout).toContain("sindri repo onboard");
    expect(fs.readFileSync(ledgerPath(stateDir(d)))).toEqual(before); // never writes the ledger
    const listing = fs.readdirSync(stateDir(d)).sort();
    expect(listing.filter((f) => f.endsWith("-wal") || f.endsWith("-shm"))).toEqual([]);
    await runCli(["repo", "status", target], d);
    expect(fs.readdirSync(stateDir(d)).sort()).toEqual(listing); // no -wal/-shm left beside the ledger
    expect((await runCli(["repo", "status", target], d)).exitCode).toBe(1);
    await runCli(["repo", "onboard", target, "--name", "web"], d);
    expect((await nudge(d, target)).stdout).toContain("waiting for approval");
    await approve(d);
    expect((await nudge(d, target)).stdout).toBe("");
    expect((await runCli(["repo", "status", target], d)).exitCode).toBe(0);
  });

  it("without --nudge: outside git and before any approval are exit 1 with a reason; --json carries the state", async () => {
    const fresh = makeDeps();
    const target = gitRepo({ "b.ts": "1" });
    const none = await runCli(["repo", "status", target], fresh);
    expect(none.exitCode).toBe(1);
    expect(none.stdout).toContain("No approved profile yet");
    const outside = await runCli(["repo", "status", tempDir()], fresh);
    expect(outside.exitCode).toBe(1);
    expect(outside.stdout).toContain("Not inside a git repo.");
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const j = await runCli(["repo", "status", target, "--json"], d);
    expect(j.exitCode).toBe(1);
    expect(JSON.parse(j.stdout)).toMatchObject({ kind: "not-onboarded", path: fs.realpathSync(target) });
    const ring0 = await runCli(["repo", "status"], d);
    expect(ring0.exitCode).toBe(0);
    expect(ring0.stdout).toMatch(/is onboarded\./);
    // No path means the current directory, for status --nudge and for onboard.
    expect(await runCli(["repo", "status", "--nudge"], d)).toEqual({ exitCode: 0, stdout: "", stderr: "" });
    const here = await runCli(["repo", "onboard", "--no-build", "--json"], d);
    expect(here.exitCode).toBe(0);
    expect(steps(here.stdout)).toEqual({ "repo-add": "ok", approval: "ok", "pre-commit": "done", "index-build": "skip" });
  });

  it("counts a linked worktree of an onboarded repo as onboarded", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "1" });
    await runCli(["repo", "onboard", target, "--name", "web", "--no-build"], d);
    await approve(d);
    const wt = path.join(tempDir(), "wt");
    git(target, "worktree", "add", "-q", wt, "-b", "side");
    expect((await runCli(["repo", "status", wt], d)).exitCode).toBe(0);
  });

  it("a worktree of a bare repo that sits inside an onboarded checkout is not counted as that checkout", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "1" });
    await runCli(["repo", "onboard", target, "--name", "web", "--no-build"], d);
    await approve(d);
    // dirname(<target>/inner.git) is the onboarded checkout, but <target>/.git is not that common dir.
    git(target, "clone", "-q", "--bare", target, "inner.git");
    const wt = path.join(tempDir(), "wt");
    git(path.join(target, "inner.git"), "worktree", "add", "-q", wt, "-b", "side");
    const r = await runCli(["repo", "status", wt, "--json"], d);
    expect(JSON.parse(r.stdout)).toMatchObject({ kind: "not-onboarded" });
  });

  it("reads approval from a ledger at an older schema version (an upgrade never silently skips the gate)", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const raw = new Database(ledgerPath(stateDir(d)));
    raw.pragma("user_version = 1");
    raw.close();
    expect((await runCli(["repo", "status"], d)).exitCode).toBe(0);
  });

  it("an internal error is exit 2 without --nudge (never 0 or 1), and silence with --nudge", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const target = gitRepo({ "b.ts": "1" });
    fs.writeFileSync(ledgerPath(stateDir(d)), "this is not a sqlite file, and long enough to be read as one".repeat(20));
    const r = await runCli(["repo", "status", target], d);
    expect(r.exitCode).toBe(2);
    const n = await runCli(["repo", "status", target, "--nudge"], d);
    expect(n).toEqual({ exitCode: 0, stdout: "", stderr: "" });
  });

  it("a sindri error with exit code 1 is still exit 2 (exit 1 means only: not onboarded)", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    const throwing: GitRunner = { run: async () => { throw new SindriError("SND-PROFILE-015", "planted", { exitCode: 1 }); } };
    const r = await runCli(["repo", "status", "--json"], { ...d, git: throwing });
    expect(r.exitCode).toBe(2);
    expect(r.stdout).toContain("SND-PROFILE-015");
  });

  it("with no live profile left, a repo outside the approved one is not onboarded (no pending name); a vanished repo path is skipped", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }), { extraRepos: [{ name: "gone", path: path.join(tempDir(), "vanished") }] });
    const target = gitRepo({ "b.ts": "1" });
    fs.rmSync(path.join(d.env.AW_STATE_DIR as string, "profile"), { recursive: true, force: true });
    const r = await runCli(["repo", "status", target, "--json"], d);
    expect(r.exitCode).toBe(1);
    expect(JSON.parse(r.stdout)).toEqual({ kind: "not-onboarded", path: fs.realpathSync(target) });
    expect((await runCli(["repo", "status", target, "--nudge"], d)).stdout).toMatch(/is not onboarded/);
  });
});

describe("sindri repo onboard --template", () => {
  const withGlobal = (d: Deps, file: string): Deps => ({ ...d, env: { ...d.env, GIT_CONFIG_GLOBAL: file, SINDRI_BIN: "/opt/bin/sindri" } });
  const globalGet = (file: string, key: string): string | null => {
    const r = spawnSync("git", ["config", "--file", file, "--get", key], { encoding: "utf8" });
    return r.status === 0 ? r.stdout.trim() : null;
  };

  it("sets init.templateDir when unset, writes the gated hook, never core.hooksPath, and is idempotent", async () => {
    const cfg = path.join(tempDir(), "gitconfig");
    fs.writeFileSync(cfg, "");
    const d = withGlobal(makeDeps(), cfg);
    const r = await runCli(["repo", "onboard", "--template"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toMatch(/done\s+template/);
    expect(globalGet(cfg, "init.templateDir")).toBe(templateDir(d));
    expect(globalGet(cfg, "core.hooksPath")).toBeNull();
    const hook = path.join(templateDir(d), "hooks", "pre-commit");
    expect(fs.statSync(hook).mode & 0o777).toBe(0o755);
    expect(fs.readFileSync(hook, "utf8")).toContain(TEMPLATE_MARKER);
    expect(fs.readFileSync(hook, "utf8")).toContain("SINDRI='/opt/bin/sindri'");
    expect((await runCli(["repo", "onboard", "--template"], d)).stdout).toMatch(/ok\s+template/);
    // A new binary path refreshes the hook.
    expect((await runCli(["repo", "onboard", "--template"], { ...d, env: { ...d.env, SINDRI_BIN: "/new/sindri" } })).stdout).toMatch(/done\s+template/);
    // git init copies it into a new repo.
    const fresh = tempDir();
    execFileSync("git", ["init", "-q", fresh], { env: { ...process.env, GIT_CONFIG_GLOBAL: cfg } });
    expect(fs.readFileSync(hookOf(fresh), "utf8")).toContain(TEMPLATE_MARKER);
  });

  it("accepts a ~/-relative init.templateDir that already is sindri's (--json)", async () => {
    const cfg = path.join(tempDir(), "gitconfig");
    const d0 = makeDeps();
    fs.writeFileSync(cfg, `[init]\n\ttemplateDir = ~/${path.relative(d0.home, templateDir(d0))}\n`);
    const d = withGlobal(d0, cfg);
    const r = await runCli(["repo", "onboard", "--template", "--json"], d);
    expect(r.exitCode).toBe(0);
    expect(JSON.parse(r.stdout)).toMatchObject({ steps: [{ name: "template", status: "done" }] });
    expect(steps((await runCli(["repo", "onboard", "--template", "--json"], d)).stdout)).toEqual({ template: "ok" });
  });

  it("refuses a templateDir that isn't sindri's, changes nothing in it, and names the file to copy", async () => {
    const cfg = path.join(tempDir(), "gitconfig");
    const theirs = tempDir();
    fs.writeFileSync(cfg, `[init]\n\ttemplateDir = ${theirs}\n`);
    const d = withGlobal(makeDeps(), cfg);
    const r = await runCli(["repo", "onboard", "--template"], d);
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain("SND-SCRUB-006");
    expect(r.stderr).toContain(path.join(templateDir(d), "hooks", "pre-commit"));
    expect(globalGet(cfg, "init.templateDir")).toBe(theirs);
    expect(fs.readdirSync(theirs)).toEqual([]);
  });

  it("reports a git config it can't read or set as SND-SCRUB-006", async () => {
    const d = makeDeps({ git: fakeGit({ "config --global --get init.templateDir": { ok: false, stderr: "fatal: bad config line 1", code: 128 } }) });
    expect((await runCli(["repo", "onboard", "--template"], d)).stderr).toContain("could not read init.templateDir");
    const base = makeDeps();
    const d2 = { ...base, git: fakeGit({ "config --global --get init.templateDir": { ok: false, stderr: "", code: 1 }, [`config --global init.templateDir ${templateDir(base)}`]: { ok: false, stderr: "error: could not lock config file" } }) };
    expect((await runCli(["repo", "onboard", "--template"], d2)).stderr).toContain("SND-SCRUB-006");
  });

  it("the template hook is a no-op until the repo is onboarded, and scans once it is", () => {
    const dir = tempDir();
    const log = path.join(dir, "calls.log");
    const fake = path.join(dir, "sindri");
    fs.writeFileSync(fake, `#!/bin/sh\necho "$*" >> '${log}'\n[ "$1 $2" = "repo status" ] && exit "\${STATUS:-1}"\nexit 0\n`, { mode: 0o755 });
    const hook = path.join(dir, "pre-commit");
    fs.writeFileSync(hook, preCommitHook(fake, { template: true }), { mode: 0o755 });
    const run = (env: Record<string, string>) => spawnSync("sh", [hook], { env: { ...process.env, ...env }, encoding: "utf8" });
    expect(run({ STATUS: "1" }).status).toBe(0);
    expect(fs.readFileSync(log, "utf8")).toBe("repo status\n");
    fs.rmSync(log);
    expect(run({ STATUS: "0" }).status).toBe(0);
    expect(fs.readFileSync(log, "utf8")).toBe("repo status\nscrub --staged\nshape --record --staged\n");
    fs.rmSync(log);
    // repo status failing (exit 2: an internal error) says so in one stderr line and never blocks the commit.
    const failed = run({ STATUS: "2" });
    expect(failed.status).toBe(0);
    expect(failed.stderr).toBe("sindri: repo status failed; secret scan skipped\n");
    expect(fs.readFileSync(log, "utf8")).toBe("repo status\n");
    fs.rmSync(fake);
    expect(run({}).status).toBe(0); // a missing binary is a no-op in the template copy (the full hook fails closed)
  });
});

describe("index commands and a repo that was added but not approved", () => {
  it("--repo names it as not approved yet (SND-PROFILE-015), not as missing; an unknown name is still SND-PROFILE-004", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    await runCli(["repo", "add", gitRepo({ "b.ts": "1" }), "--name", "demo-app"], d);
    for (const args of [["index", "build", "--repo", "demo-app"], ["index", "status", "--repo", "demo-app"], ["index", "query", "x", "--repo", "demo-app"]]) {
      const r = await runCli(args, d);
      expect(r.exitCode).toBe(1);
      expect(r.stderr).toContain("SND-PROFILE-015");
      expect(r.stderr).toContain("demo-app is in the live profile but not approved yet");
      expect(r.stderr).toContain("sindri profile approve");
    }
    expect((await runCli(["index", "build", "--repo", "no-such-repo"], d)).stderr).toContain("SND-PROFILE-004");
  });

  it("index build with no --repo builds the approved repos and lists the unapproved ones it skipped, in one line", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    await runCli(["repo", "add", gitRepo({ "b.ts": "1" }), "--name", "demo-app"], d);
    await runCli(["repo", "add", gitRepo({ "c.ts": "1" }), "--name", "demo-lib"], d);
    const r = await runCli(["index", "build"], d);
    expect(r.exitCode).toBe(0);
    const skipped = r.stdout.split("\n").filter((l) => l.startsWith("skipped (in the live profile, not approved yet)"));
    expect(skipped).toEqual(["skipped (in the live profile, not approved yet): demo-app, demo-lib; run sindri profile approve"]);
    expect(fs.existsSync(indexPath(d, "demo-app"))).toBe(false);
    const j = JSON.parse((await runCli(["index", "build", "--json"], d)).stdout) as { reports: { repo: string }[]; unapproved: string[] };
    expect(j.unapproved).toEqual(["demo-app", "demo-lib"]);
    expect(j.reports).toHaveLength(1);
    // After approval the line goes away and both are built.
    await approve(d);
    const after = await runCli(["index", "build"], d);
    expect(after.stdout).not.toContain("not approved yet");
    expect(fs.existsSync(indexPath(d, "demo-app"))).toBe(true);
  });

  it("an invalid live profile lists nothing as unapproved (the approved snapshot still runs)", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    fs.appendFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "not: [valid\n");
    const r = await runCli(["index", "build"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).not.toContain("not approved yet");
  });

  it("a missing live profile lists nothing as unapproved", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "1" }));
    fs.rmSync(path.join(d.env.AW_STATE_DIR as string, "profile"), { recursive: true, force: true });
    const r = await runCli(["index", "build"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).not.toContain("not approved yet");
  });
});
