import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { awStateDir, stateDir, type Deps } from "../src/deps.js";
import { LEDGER_SCHEMA_VERSION } from "../src/ledger/db.js";
import { runChecks } from "../src/doctor/doctor.js";
import { makeGraphifyProvider } from "../src/index/graph.js";
import { makeIndexCommand } from "../src/index/commands.js";
import { indexPath, openIndex } from "../src/index/db.js";
import { heavyLockDir, heavyLockDirs } from "../src/index/heavy-lock.js";
import type { IndexProbes } from "../src/index/io.js";
import { GRAPHIFY_PIN } from "../src/index/pins.js";
import { runCli } from "../src/main.js";
import { preCommitHook } from "../src/scrub/commands.js";
import { fakeSystem, makeDeps, tempDir } from "./helpers.js";
import { approvedIndexDeps, BODY, fakeIndexIo, OFF as OFF_INDEX, ring0Name, ring0Repo, seedIndex } from "./index-fixtures.js";

// Plan 2's tests never touch the machine's Ollama or graphify: nothing is installed, nothing answers.
const offline: IndexProbes = { has: () => false, run: async () => ({ code: 127, stdout: "", stderr: "" }), getJson: async () => null };
const byName = async (deps: Deps, node = "22.10.0") => Object.fromEntries((await runChecks(deps, node, offline)).map((c) => [c.name, c]));

async function ring0Deps(): Promise<Deps> {
  const root = tempDir("sindri-doc-");
  fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
  execFileSync("git", ["init", "-q"], { cwd: root });
  const d = makeDeps({ cwd: root });
  await runCli(["profile", "init", "--ring0"], d);
  fs.appendFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "index:\n  embeddings:\n    enabled: false\n  graph: none\n");
  return d;
}

describe("sindri doctor", () => {
  it("on a fresh machine: warns about the missing state dir and profile, nothing fails", async () => {
    const d = makeDeps();
    const c = await byName(d);
    expect(c.node.status).toBe("ok");
    expect(c["state-dir"]).toMatchObject({ status: "warn", detail: "not created yet" });
    expect(c.ledger).toMatchObject({ status: "ok", detail: "no ledger yet" });
    expect(c.lock).toMatchObject({ status: "ok", detail: "free" });
    expect(c.profile).toMatchObject({ status: "warn", fix: "sindri profile init --ring0 (or sindri profile init)" });
    expect(c["profile-approved"]).toBeUndefined();
    const r = await runCli(["doctor"], d);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toMatch(/^ok {3}node/m);
    expect(r.stdout).toMatch(/^warn state-dir/m);
  });

  it("is all ok after ring-0 init, approve and pre-commit install (spec §13.3 evidence)", async () => {
    const d = await ring0Deps();
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
    await runCli(["scrub", "--install-pre-commit"], d);
    await makeIndexCommand(fakeIndexIo())(["build"], d);
    const r = await runCli(["doctor"], d);
    expect(r.stdout).not.toMatch(/^(warn|fail)/m);
    expect(r.exitCode).toBe(0);
    const c = await byName(d);
    expect(c.budget.detail).toBe("unset (not enforced before rollout step 3a)");
    expect(Object.keys(c).some((k) => k.startsWith("pre-commit:"))).toBe(true);
  });

  it("warns on an unapproved profile, a missing hook and another active host; a set budget is ok", async () => {
    const d = await ring0Deps();
    const file = path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    fs.appendFileSync(file, "budget:\n  perItem: 1000\n  perDay: 5000\n");
    const c = await byName({ ...d, system: fakeSystem({ hostname: () => "other" }) });
    expect(c["profile-approved"].status).toBe("warn");
    expect(c["active-host"]).toMatchObject({ status: "warn", detail: "this host is other; hosts.active is test-host" });
    expect(c.budget).toMatchObject({ status: "ok", detail: "perItem 1000, perDay 5000 tokens" });
    const hook = Object.entries(c).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    expect(hook?.status).toBe("warn");
  });

  it("warns when the installed hook points at a CLI that is gone", async () => {
    const d = await ring0Deps();
    await runCli(["scrub", "--install-pre-commit"], { ...d, env: { ...d.env, SINDRI_BIN: "/nonexistent/sindri" } });
    const hook = Object.entries(await byName(d)).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    expect(hook).toMatchObject({ status: "warn" });
    expect(hook?.detail).toContain("/nonexistent/sindri, which is missing");
  });

  it("fails on an old node, a network state dir, an invalid profile and a newer ledger", async () => {
    const d = makeDeps({ system: fakeSystem({ isLocalDisk: () => false, bootId: () => null }) });
    fs.mkdirSync(stateDir(d), { recursive: true });
    const raw = new Database(path.join(stateDir(d), "ledger.db"));
    raw.pragma("user_version = 99");
    raw.close();
    fs.mkdirSync(path.join(d.env.AW_STATE_DIR as string, "profile"), { recursive: true });
    fs.writeFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "bogus: 1\n");
    const c = await byName(d, "18.19.0");
    expect(c.node.status).toBe("fail");
    expect(c["state-dir"]).toMatchObject({ status: "fail", detail: "on a network filesystem" });
    expect(c["boot-id"]).toMatchObject({ status: "ok", detail: "unreadable; stale-lock checks use pids only" });
    expect(c.ledger.status).toBe("fail");
    expect(c.ledger.detail).toContain("SND-LEDGER-001");
    expect(c.profile.status).toBe("fail");
    expect((await runCli(["doctor", "--json"], d)).exitCode).toBe(2);
  });

  it("warns on loose permissions, an unknown disk type, a stale lock and leftovers", async () => {
    const d = makeDeps({ system: fakeSystem({ isLocalDisk: () => null, pidAlive: () => false }) });
    const dir = stateDir(d);
    fs.mkdirSync(path.join(dir, "sindri.lock"), { recursive: true });
    fs.chmodSync(dir, 0o755);
    fs.writeFileSync(path.join(dir, "sindri.lock", "owner.json"), JSON.stringify({ pid: 9, pidStartTime: null, host: "test-host", bootId: "boot-1", startedAt: "t", epoch: 1 }));
    fs.mkdirSync(path.join(dir, "sindri.lock.stale-x"));
    const c = await byName(d);
    expect(c["state-dir"]).toMatchObject({ status: "warn", detail: "mode 755 (want 700); can't tell if it is on local disk" });
    expect(c.lock.status).toBe("warn");
    expect(c.lock.detail).toContain("stale lock from test-host/9");
    expect(c.lock.detail).toContain("leftovers: sindri.lock.stale-x");
  });

  it("reports a held lock, and a stale lock without leftovers, as ok", async () => {
    const d = makeDeps();
    const dir = stateDir(d);
    fs.mkdirSync(path.join(dir, "sindri.lock"), { recursive: true });
    fs.chmodSync(dir, 0o700);
    fs.writeFileSync(path.join(dir, "sindri.lock", "owner.json"), JSON.stringify({ pid: 9, pidStartTime: "start-9", host: "test-host", bootId: "boot-1", startedAt: "t", epoch: 1 }));
    expect((await byName(d)).lock).toMatchObject({ status: "ok", detail: "held by test-host/9 since t" });
    const stale = await byName({ ...d, system: fakeSystem({ pidAlive: () => false }) });
    expect(stale.lock).toMatchObject({ status: "ok", detail: "stale lock from test-host/9; the next run takes it over" });
  });
});

describe("sindri doctor fix hints clear their warning", () => {
  it("state-dir: the suggested `sindri profile init` creates the state dir (mode 700)", async () => {
    const d = makeDeps();
    const before = await byName(d);
    expect(before["state-dir"]).toMatchObject({ status: "warn", detail: "not created yet", fix: "sindri profile init --ring0 (or sindri profile init)" });
    await runCli(["profile", "init"], d);
    expect((await byName(d))["state-dir"]).toMatchObject({ status: "ok", detail: stateDir(d) });
    expect(fs.statSync(stateDir(d)).mode & 0o777).toBe(0o700);
  });
});

describe("sindri doctor coverage paths", () => {
  it("reports a template-variant hook as skipping the scan (never as refusing every commit)", async () => {
    const d = await ring0Deps();
    const file = path.join(d.cwd, ".git", "hooks", "pre-commit");
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const hookCheck = async () => Object.entries(await byName(d)).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    const repo = fs.realpathSync(d.cwd);
    fs.writeFileSync(file, preCommitHook("/nonexistent/sindri", { template: true }));
    const gone = await hookCheck();
    expect(gone).toMatchObject({ status: "warn", detail: "template hook calls /nonexistent/sindri, which is missing, so the secret scan is skipped", fix: `scripts/install-sindri.sh, then sindri repo onboard ${repo}` });
    expect(gone?.detail).not.toContain("every commit is refused");
    fs.writeFileSync(file, preCommitHook(process.execPath, { template: true }));
    expect(await hookCheck()).toMatchObject({ status: "warn", detail: "template copy: it skips the secret scan if sindri goes missing or repo status fails", fix: `sindri repo onboard ${repo}` });
  });

  it("never tells a core.hooksPath repo to run the installer (final review I1)", async () => {
    const d = await ring0Deps();
    execFileSync("git", ["config", "core.hooksPath", ".githooks"], { cwd: d.cwd });
    const hook = Object.entries(await byName({ ...d, env: { ...d.env, SINDRI_BIN: "/opt/aw bin/sindri" } })).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    expect(hook).toMatchObject({ status: "warn", detail: "secret-scan hook not installed" });
    expect(hook?.fix).not.toContain("--install-pre-commit");
    expect(hook?.fix).toContain(`add these two lines to ${path.join(fs.realpathSync(d.cwd), ".githooks", "pre-commit")} yourself`);
    expect(hook?.fix).toContain("'/opt/aw bin/sindri' scrub --staged || exit 1");
  });

  it("reports an executable hook binary as ok", async () => {
    const d = await ring0Deps();
    await runCli(["scrub", "--install-pre-commit"], { ...d, env: { ...d.env, SINDRI_BIN: process.execPath } });
    const hook = Object.entries(await byName(d)).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    expect(hook?.status).toBe("ok");
    expect(hook?.detail).toContain(process.execPath);
  });

  it("warns on a v1 hook (secret scan only) and a v2 hook without the shape step, while shape.record is on", async () => {
    const d = await ring0Deps();
    await runCli(["scrub", "--install-pre-commit"], { ...d, env: { ...d.env, SINDRI_BIN: process.execPath } });
    const file = path.join(d.cwd, ".git", "hooks", "pre-commit");
    const v2 = fs.readFileSync(file, "utf8");
    const hookCheck = async (deps: Deps) => Object.entries(await byName(deps)).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    fs.writeFileSync(file, v2.replace("# sindri-pre-commit v2", "# sindri-scrub-pre-commit v1").replace(/^"\$SINDRI" shape.*$/m, ""));
    expect(await hookCheck(d)).toMatchObject({ status: "warn", detail: "hook is v1: secret scan only, no shape recording", fix: `sindri scrub --install-pre-commit --repo ${fs.realpathSync(d.cwd)}` });
    fs.writeFileSync(file, v2.replace(/^"\$SINDRI" shape.*$/m, ""));
    expect(await hookCheck(d)).toMatchObject({ status: "warn", detail: "hook doesn't run sindri shape --record (shape.record is on)" });
    fs.appendFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "shape:\n  record: false\n");
    expect((await hookCheck(d))?.status).toBe("ok");
  });

  it("warns when shape runs wait in the spool's quarantine", async () => {
    const d = await ring0Deps();
    expect((await byName(d))["shape-spool"]).toBeUndefined();
    const q = path.join(stateDir(d), "spool", "quarantine");
    fs.mkdirSync(q, { recursive: true });
    expect((await byName(d))["shape-spool"]).toBeUndefined();
    fs.writeFileSync(path.join(q, "shape-x.json"), "{");
    expect((await byName(d))["shape-spool"]).toMatchObject({ status: "warn", detail: `1 quarantined shape run(s) in ${q}`, fix: `inspect, then remove ${q}` });
  });

  it("fails on an unreadable ledger with no error code, and does not touch it", async () => {
    const d = await ring0Deps();
    const file = path.join(stateDir(d), "ledger.db");
    fs.mkdirSync(stateDir(d), { recursive: true });
    fs.writeFileSync(file, "not a db");
    const c = await byName(d);
    expect(c.ledger.status).toBe("fail");
    expect(c.ledger.detail).not.toMatch(/^SND-/);
    expect(c["profile-approved"].status).toBe("warn");
    expect(fs.readFileSync(file, "utf8")).toBe("not a db");
  });

  it("prints unset for the missing half of a partial budget", async () => {
    const d = await ring0Deps();
    const file = path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    const base = fs.readFileSync(file, "utf8");
    fs.writeFileSync(file, `${base}budget:\n  perItem: 1000\n`);
    expect((await byName(d)).budget.detail).toBe("perItem 1000, perDay unset tokens");
    fs.writeFileSync(file, `${base}budget:\n  perDay: 5000\n`);
    expect((await byName(d)).budget.detail).toBe("perItem unset, perDay 5000 tokens");
  });

  it("reports an unknown flag as SND-CLI-002", async () => {
    const r = await runCli(["doctor", "--nope"], makeDeps());
    expect(r.exitCode).toBe(2);
    expect(r.stderr + r.stdout).toContain("SND-CLI-002");
  });

  it("holds node to the engines floor: 21.x fails with the fix, 22.0 is fine", async () => {
    const d = makeDeps();
    const old = (await byName(d, "21.7.3")).node;
    expect(old.status).toBe("fail");
    expect(old.detail).toBe("21.7.3 (need >= 22, which better-sqlite3 13 requires)");
    expect(old.fix).toBe("install Node 22 or newer (see .nvmrc)");
    expect((await byName(d, "20.11.0")).node.status).toBe("fail");
    expect((await byName(d, "22.0.0")).node.status).toBe("ok");
  });
});

describe("sindri doctor lock and read-only guarantees", () => {
  it("names an unreadable lock owner without a since clause", async () => {
    const d = makeDeps();
    const dir = stateDir(d);
    fs.mkdirSync(path.join(dir, "sindri.lock"), { recursive: true });
    fs.chmodSync(dir, 0o700);
    fs.writeFileSync(path.join(dir, "sindri.lock", "owner.json"), "{not json");
    const lock = (await byName(d)).lock;
    expect(lock.status).toBe("ok");
    expect(lock.detail).toMatch(/unreadable owner/);
    expect(lock.detail).not.toContain("since");
    expect(lock.detail).not.toContain("undefined");
  });

  it("does not migrate, back up, chmod or leave files beside a valid older WAL ledger (m19)", async () => {
    const d = await ring0Deps();
    const dir = stateDir(d);
    fs.mkdirSync(dir, { recursive: true });
    fs.chmodSync(dir, 0o700);
    const file = path.join(dir, "ledger.db");
    // Like a real ledger: WAL mode, last writer closed cleanly (no -wal or -shm left).
    const raw = new Database(file);
    raw.pragma("journal_mode = WAL");
    raw.exec("CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)");
    raw.exec("CREATE TABLE profile_approvals (hash TEXT PRIMARY KEY, approved_at TEXT NOT NULL, approved_by TEXT NOT NULL)");
    raw.pragma("user_version = 1");
    raw.close();
    fs.chmodSync(file, 0o644);
    const before = fs.readdirSync(dir).sort();
    expect(before.filter((n) => n.startsWith("ledger.db"))).toEqual(["ledger.db"]);
    const bytes = fs.readFileSync(file);
    const r = await runCli(["doctor"], d);
    expect(r.stdout).toMatch(/^ok {3}ledger/m);
    const check = Object.fromEntries((await runChecks(d, "22.10.0")).map((c) => [c.name, c]));
    expect(check.ledger.detail).toBe(`schema v1 of ${LEDGER_SCHEMA_VERSION}`);
    expect(check["profile-approved"].detail).toContain("has never been approved");
    expect(fs.statSync(file).mode & 0o777).toBe(0o644);
    expect(fs.readdirSync(dir).sort()).toEqual(before);
    expect(fs.readFileSync(file).equals(bytes)).toBe(true);
  });
});

function probes(o: { models?: unknown; graphify?: string; graphifyCode?: number; has?: (b: string) => boolean } = {}): IndexProbes {
  return {
    has: o.has ?? (() => true),
    getJson: async () => (o.models === undefined ? { models: [{ name: "nomic-embed-text:latest" }] } : o.models),
    run: async () => ({ code: o.graphifyCode ?? 0, stdout: `graphify ${o.graphify ?? GRAPHIFY_PIN}\n`, stderr: "" }),
  };
}
const checks = async (deps: Deps, p: IndexProbes) => Object.fromEntries((await runChecks(deps, "22.10.0", p)).map((c) => [c.name, c]));

describe("doctor index checks", () => {
  it("asks the probes on Deps, never the machine's, by default and through runCli", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }), { index: "index:\n  utilityGlobs: []\n" });
    const asked: string[] = [];
    const none: IndexProbes = { ...offline, has: (b) => (asked.push(b), false) };
    const byDefault = Object.fromEntries((await runChecks({ ...d, io: fakeIndexIo({ probes: none }) }, "22.10.0")).map((c) => [c.name, c]));
    expect(byDefault.graphify).toMatchObject({ status: "warn", detail: "no network sandbox" });
    expect(asked).toContain("/usr/bin/sandbox-exec");
    const viaCli = JSON.parse((await runCli(["doctor", "--json"], { ...d, io: fakeIndexIo({ probes: probes() }) })).stdout) as { name: string; detail: string }[];
    expect(viaCli.find((c) => c.name === "graphify")?.detail).toBe(`${GRAPHIFY_PIN}, sandboxed`);
  });

  it("warns on a missing index, and checks embeddings, graphify (exact pin, sandboxed) and the proxy", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }), { index: "index:\n  utilityGlobs: []\n" });
    const name = ring0Name(d);
    const good = await checks(d, probes());
    expect(good[`index:${name}`]).toMatchObject({ status: "warn", detail: "no index", fix: `sindri index build --repo ${name}` });
    expect(good.embeddings).toMatchObject({ status: "ok", detail: "nomic-embed-text on http://127.0.0.1:11434" });
    expect(good.graphify).toMatchObject({ status: "ok", detail: `${GRAPHIFY_PIN}, sandboxed` });
    expect(good["embedding-proxy"]).toBeUndefined();
    expect((await checks(d, probes({ models: null }))).embeddings).toMatchObject({ status: "warn", detail: "Ollama not answering on loopback", fix: "sindri index setup" });
    expect((await checks(d, probes({ models: { models: [] } }))).embeddings).toMatchObject({ status: "warn", detail: "model nomic-embed-text not pulled" });
    expect((await checks(d, probes({ has: (b) => b !== "graphify" }))).graphify).toMatchObject({ status: "warn", detail: `not installed at ${GRAPHIFY_PIN}`, fix: "sindri index setup" });
    expect((await checks(d, probes({ graphifyCode: 1 }))).graphify.detail).toBe(`not installed at ${GRAPHIFY_PIN}`);
    expect((await checks(d, probes({ graphify: "0.0.1" }))).graphify.detail).toBe(`not installed at ${GRAPHIFY_PIN} (found 0.0.1)`);
    expect((await checks(d, probes({ graphify: `${GRAPHIFY_PIN}0` }))).graphify.detail).toBe(`not installed at ${GRAPHIFY_PIN} (found ${GRAPHIFY_PIN}0)`);
    const noBox = await checks({ ...d, system: fakeSystem({ platform: "linux" }) }, probes({ has: (b) => !b.endsWith("/bwrap") }));
    expect(noBox.graphify).toMatchObject({ status: "warn", detail: "no network sandbox" });
    const proxied = await checks({ ...d, env: { ...d.env, NODE_USE_ENV_PROXY: "1" } }, probes());
    expect(proxied["embedding-proxy"]).toMatchObject({ status: "warn", fix: "unset NODE_USE_ENV_PROXY for sindri" });
  });

  it("reports a built index as ok, a stale or never-built one, and layers that are unavailable or pending", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
    const name = ring0Name(d);
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", name], d);
    const off = await checks(d, offline);
    expect(off[`index:${name}`]).toMatchObject({ status: "ok", detail: "built 0 h ago" });
    expect(off.embeddings).toMatchObject({ status: "ok", detail: "off (index.embeddings.enabled: false)" });
    expect(off.graphify).toMatchObject({ status: "ok", detail: "off (index.graph: none)" });
    const later = { ...d, now: () => new Date(d.now().getTime() + 48 * 3_600_000) };
    expect((await checks(later, offline))[`index:${name}`]).toMatchObject({ status: "warn", detail: "stale (built 48 h ago)" });
    fs.rmSync(indexPath(d, name));
    openIndex(indexPath(d, name)).close();
    expect((await checks(d, offline))[`index:${name}`]).toMatchObject({ status: "warn", detail: "never built" });

    // A function, so there is a symbol to embed and the unreachable server makes the layer unavailable.
    const on = await approvedIndexDeps(ring0Repo({ "src/a.ts": BODY("a") }), { index: "index:\n  graph: none\n" });
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", ring0Name(on)], on);
    expect((await checks(on, offline))[`index:${ring0Name(on)}`]).toMatchObject({
      status: "warn", detail: "embeddings unavailable: embedding server unreachable: connect ECONNREFUSED", fix: "sindri index setup",
    });
    await makeIndexCommand(fakeIndexIo())(["build", "--quick", "--full", "--repo", ring0Name(on)], on);
    expect((await checks(on, offline))[`index:${ring0Name(on)}`].detail).toBe("embeddings pending: not built yet (sindri index build); graph pending: not built yet (sindri index build)");
  });

  it("reports an index whose layers table is gone as damaged instead of throwing", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
    const name = ring0Name(d);
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", name], d);
    const db = openIndex(indexPath(d, name));
    db.exec("DROP TABLE layers");
    db.close();
    expect((await checks(d, offline))[`index:${name}`]).toMatchObject({
      status: "warn", detail: "damaged (no usable layers table)", fix: `rm ${indexPath(d, name)}, then sindri index build --repo ${name}`,
    });
    // The fix clears the warning.
    fs.rmSync(indexPath(d, name));
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", name], d);
    expect((await checks(d, offline))[`index:${name}`]).toMatchObject({ status: "ok" });
  });

  it("reports an index whose meta table is gone as damaged instead of throwing (M4)", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
    const name = ring0Name(d);
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", name], d);
    const db = openIndex(indexPath(d, name));
    db.exec("DROP TABLE meta");
    db.close();
    expect((await checks(d, offline))[`index:${name}`]).toMatchObject({
      status: "warn", detail: "damaged (no usable meta table)", fix: `rm ${indexPath(d, name)}, then sindri index build --repo ${name}`,
    });
  });

  it("reports an index file that isn't a database, or lacks its symbols table, as damaged instead of aborting", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
    const name = ring0Name(d);
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", name], d);
    const db = openIndex(indexPath(d, name));
    db.exec("DROP TABLE symbols");
    db.close();
    expect((await checks(d, offline))[`index:${name}`]).toMatchObject({ status: "warn", detail: "damaged (no usable symbols table)" });
    fs.writeFileSync(indexPath(d, name), "a text file where the index should be, long enough to fill a page".repeat(5));
    expect((await checks(d, offline))[`index:${name}`]).toMatchObject({
      status: "warn", detail: "damaged (not a database)", fix: `rm ${indexPath(d, name)}, then sindri index build --repo ${name}`,
    });
  });

  it("rethrows an index read error that is not a missing table, rather than advising rm (M4)", async () => {
    for (const table of ["layers", "meta"]) {
      const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
      const name = ring0Name(d);
      await makeIndexCommand(fakeIndexIo())(["build", "--repo", name], d);
      const db = openIndex(indexPath(d, name));
      // A view that fails when read, with an error other than "no such table" (as SQLITE_BUSY or an I/O error would).
      db.exec(`DROP TABLE ${table}; CREATE VIEW ${table} AS SELECT json('{') AS layer, json('{') AS key, 'v' AS value`);
      db.close();
      await expect(checks(d, offline)).rejects.toThrow(/malformed JSON/);
    }
  });

  it("gives each down layer the fix that helps: raise the cap, rebuild, or set up", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
    const name = ring0Name(d);
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", name], d);
    const setLayer = (layer: string, status: string, detail: string) => {
      const db = new Database(indexPath(d, name));
      db.prepare("UPDATE layers SET status = ?, detail = ? WHERE layer = ?").run(status, detail, layer);
      db.close();
    };
    const fixOf = async () => (await checks(d, offline))[`index:${name}`].fix;
    const snap = tempDir();
    fs.mkdirSync(path.join(snap, "graphify-out"));
    fs.writeFileSync(path.join(snap, "graphify-out", "graph.json"), "x".repeat(1024 * 1024 + 1));
    const over = await Promise.resolve(makeGraphifyProvider({
      bin: "graphify", version: "1", runner: { run: async () => ({ code: 0, stdout: "", stderr: "" }) }, platform: "darwin", has: () => true, home: tempDir(), maxGraphMB: 1,
    }).build(snap)).then(() => "", (e: Error) => e.message);
    expect(over).toMatch(/over 1 MB/);
    setLayer("graph", "unavailable", over);
    expect(await fixOf()).toBe(`raise index.graphMaxMB in the profile (max 512), then sindri index build --repo ${name}`);
    setLayer("graph", "pending", "not built yet (the next build continues)");
    expect(await fixOf()).toBe(`sindri index build --repo ${name}`);
    setLayer("graph", "unavailable", "graphify is not installed (sindri index setup)");
    expect(await fixOf()).toBe("sindri index setup");
    setLayer("embeddings", "pending", "not built yet (the next build continues)");
    expect(await fixOf()).toBe(`sindri index build --repo ${name}; sindri index setup`);
  });

  it("reports the heavy-job lock: free, held, and stuck for over 6 hours", async () => {
    const base = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
    // The default lock dir, then $AW_HEAVY_JOB_LOCK (the path config/lib/locks.sh callers share).
    for (const d of [base, { ...base, env: { ...base.env, AW_HEAVY_JOB_LOCK: path.join(tempDir("sindri-heavy-"), "shared.lock") } }]) {
      const dir = heavyLockDir(awStateDir(d), d.env);
      const holderFile = `${dir}.holder.json`;
      const hold = (age: number, holder: object | null): void => {
        fs.rmSync(holderFile, { force: true });
        fs.rmSync(dir, { recursive: true, force: true });
        fs.mkdirSync(dir, { recursive: true });
        const t = new Date(d.now().getTime() - age * 3_600_000);
        fs.utimesSync(dir, t, t);
        if (holder !== null) fs.writeFileSync(holderFile, JSON.stringify(holder));
      };
      const lock = async () => (await checks(d, offline))["heavy-lock"];
      expect(await lock()).toMatchObject({ status: "ok", detail: "free" });
      hold(1, null);
      expect(await lock()).toMatchObject({ status: "ok", detail: "held by an unknown job" });
      hold(1, { kind: "index-build", pid: 7, host: "other", startedAt: "t" });
      expect(await lock()).toMatchObject({ status: "ok", detail: "held by index-build" });
      hold(7, null);
      expect(await lock()).toMatchObject({ status: "warn", detail: "held for over 6 h", fix: `if that process is gone: rm -r ${dir} && rm -f ${holderFile} (a crashed takeover can leave a .reclaim-* mark inside)` });
      hold(7, { kind: "index-build", pid: 7, host: "other", startedAt: "t" });
      expect((await lock()).detail).toBe("held for over 6 h by index-build (pid 7)");
      fs.rmSync(holderFile, { force: true });
      fs.rmdirSync(dir);
    }
    expect(fs.existsSync(heavyLockDir(awStateDir(base), base.env))).toBe(false);
  });

  it("reports every slot of the heavy-job lock: slot 1 as heavy-lock, slot k as heavy-lock.k", async () => {
    const base = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }));
    const d = { ...base, env: { ...base.env, AW_HEAVY_SLOTS: "3" } };
    const [one, two, three] = heavyLockDirs(awStateDir(d), d.env);
    const slots = async () => {
      const c = await checks(d, offline);
      return [c["heavy-lock"], c["heavy-lock.2"], c["heavy-lock.3"]].map((x) => x && { status: x.status, detail: x.detail });
    };
    expect(await slots()).toEqual([{ status: "ok", detail: "free" }, { status: "ok", detail: "free" }, { status: "ok", detail: "free" }]);
    fs.mkdirSync(two, { recursive: true });
    fs.writeFileSync(`${two}.holder.json`, JSON.stringify({ kind: "index-build", pid: 7, host: "other", startedAt: "t" }));
    fs.mkdirSync(three);
    const t = new Date(d.now().getTime() - 7 * 3_600_000);
    fs.utimesSync(three, t, t);
    expect(await slots()).toEqual([{ status: "ok", detail: "free" }, { status: "ok", detail: "held by index-build" }, { status: "warn", detail: "held for over 6 h" }]);
    expect((await checks(d, offline))["heavy-lock.3"].fix).toContain(`rm -r ${three} && rm -f ${three}.holder.json`);
    expect(Object.keys(await checks(base, offline)).filter((k) => k.startsWith("heavy-lock"))).toEqual(["heavy-lock", "heavy-lock.2"]);
    expect(one).toBe(heavyLockDir(awStateDir(d), d.env));
    fs.rmSync(`${two}.holder.json`);
    fs.rmdirSync(two);
    fs.rmdirSync(three);
  });

  it("warns when Node would route loopback embedding traffic through an env proxy", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }), { index: "index:\n  graph: none\n" });
    const proxy = async (env: NodeJS.ProcessEnv) => (await checks({ ...d, env: { ...d.env, ...env } }, probes()))["embedding-proxy"];
    expect(await proxy({ NODE_USE_ENV_PROXY: "1" })).toMatchObject({
      status: "warn", detail: "NODE_USE_ENV_PROXY is set, so Node may send the embedding request through a proxy", fix: "unset NODE_USE_ENV_PROXY for sindri",
    });
    expect(await proxy({ NODE_USE_ENV_PROXY: "1", HTTP_PROXY: "http://proxy.invalid:3128", https_proxy: "http://proxy.invalid:3128" })).toMatchObject({
      status: "warn", detail: "NODE_USE_ENV_PROXY is set, so HTTP_PROXY, https_proxy then apply to loopback embedding traffic",
    });
    expect(await proxy({ NODE_OPTIONS: "--max-old-space-size=4096 --use-env-proxy", HTTPS_PROXY: "http://proxy.invalid:3128" })).toMatchObject({
      status: "warn", detail: "NODE_OPTIONS has --use-env-proxy, so HTTPS_PROXY then apply to loopback embedding traffic", fix: "remove --use-env-proxy from NODE_OPTIONS for sindri",
    });
    expect(await proxy({ NODE_USE_ENV_PROXY: "", NODE_OPTIONS: "--max-old-space-size=4096", HTTP_PROXY: "http://proxy.invalid:3128" })).toBeUndefined();
  });
});

describe("doctor and the prompt overlay", () => {
  it("lists the evolve-overlay check, and a stray overlay file turns it into a warning with exit 1", async () => {
    const d = makeDeps();
    expect((await byName(d))["evolve-overlay"]).toMatchObject({ status: "ok" });
    const dir = path.join(awStateDir(d), "sindri", "overlay", "prompts");
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, "stray.txt"), "x");
    const c = (await byName(d))["evolve-overlay"];
    expect(c.status).toBe("warn");
    expect(c.detail).toContain("stray.txt: ignored");
    const r = await runCli(["doctor", "--json"], d);
    expect((JSON.parse(r.stdout) as { name: string; status: string }[]).find((x) => x.name === "evolve-overlay")?.status).toBe("warn");
    expect(r.exitCode).toBe(1);
  });
});

describe("doctor privacy checks (privacy B1)", () => {
  async function depsWithRepos(repos: Record<string, { private?: boolean }>, env: Record<string, string> = {}): Promise<Deps> {
    const d = await approvedIndexDeps(ring0Repo({ "src/a.ts": "export const a = 1;\n" }), {
      extraRepos: Object.entries(repos).map(([name, o]) => ({ name, path: tempDir("sindri-priv-"), ...o })),
    });
    return { ...d, env: { ...d.env, ...env } };
  }

  it("warns on a non-toolkit repo with private unset, and says nothing about the toolkit repo", async () => {
    const d = await depsWithRepos({ other: {} });
    const c = await byName(d);
    expect(c["privacy:other"]).toMatchObject({ status: "warn", detail: "private: is not set, so sindri evolve publish treats this repo as public", fix: "set private: true (or false) in repos/other.yaml, then sindri profile approve" });
    expect(c[`privacy:${ring0Name(d)}`]).toBeUndefined();
  });

  it("warns on a private repo with no index and on a missing word list; ok once both exist", async () => {
    const deps = await depsWithRepos({ globex: { private: true } }, { SINDRI_WORDS: "/nonexistent" });
    let c = await byName(deps);
    expect(c["privacy:globex"]).toMatchObject({ status: "warn", fix: "sindri index build --repo globex" });
    expect(c["privacy:globex"].detail).toContain("SND-EVOLVE-017");
    expect(c["privacy:words"].detail).toBe("/nonexistent is missing: sindri evolve publish will refuse (SND-EVOLVE-018)");
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["x"] }).close();
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "word\n");
    c = await byName({ ...deps, env: { ...deps.env, SINDRI_WORDS: words } });
    expect(c["privacy:globex"]).toMatchObject({ status: "ok", detail: "private; its identifiers are held at publish" });
    expect(c["privacy:words"]).toBeUndefined();
  });

  it("warns on a private repo whose structure layer is not ok", async () => {
    const deps = await depsWithRepos({ globex: { private: true } });
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["x"], structure: "pending" }).close();
    expect((await byName(deps))["privacy:globex"].detail).toContain("SND-EVOLVE-017");
  });

  it("warns on a private repo whose index is unusable, as publish would refuse it (one shared readiness test)", async () => {
    const deps = await depsWithRepos({ globex: { private: true } });
    const db = seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["x"] });
    db.exec("DROP TABLE files");
    db.close();
    expect((await byName(deps))["privacy:globex"]).toMatchObject({ status: "warn", detail: "private, but no usable code index: sindri evolve publish will refuse (SND-EVOLVE-017)" });
  });

  it("never checks the toolkit repo as private, even when it sets private: true", async () => {
    const d = await depsWithRepos({}, { SINDRI_WORDS: "/nonexistent" });
    const name = ring0Name(d);
    fs.appendFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "repos", `${name}.yaml`), "private: true\n");
    const hash = (JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout) as { hash: string }).hash;
    expect((await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) })).exitCode).toBe(0);
    const c = await byName(d);
    expect(c.profile).toMatchObject({ status: "ok" });
    expect(c[`privacy:${name}`]).toBeUndefined();
    expect(c["privacy:words"]).toBeUndefined();
  });

  it("warns privacy:judge, once publishing is set up, when the rewriter and the judge are the same model; ok when they differ", async () => {
    const repo = (): string => ring0Repo({ "src/a.ts": "export const a = 1;\n" });
    const TERMS = "privacy:\n  denyTerms:\n    - Initech\n";
    // No privacy.denyTerms: publish refuses anyway, so a fresh install stays all ok (spec §13.3).
    expect((await byName(await depsWithRepos({})))["privacy:judge"]).toBeUndefined();
    const same = await byName(await approvedIndexDeps(repo(), { index: `${OFF_INDEX}${TERMS}` }));
    expect(same["privacy:judge"]).toMatchObject({
      status: "warn",
      detail: "models.challenger and models.adjudicator are both opus, so the generalize judge shares the rewriter's blind spots",
      fix: "set models.adjudicator (or models.challenger) to a different model in the private profile, then sindri profile approve",
    });
    const d = await approvedIndexDeps(repo(), { index: `${OFF_INDEX}${TERMS}models:\n  adjudicator: haiku\n` });
    expect((await byName(d))["privacy:judge"]).toMatchObject({ status: "ok", detail: "the generalize rewriter (opus) and judge (haiku) are different models" });
  });

  it("is silent for a repo marked private: false", async () => {
    const c = await byName(await depsWithRepos({ other: { private: false } }));
    expect(c["privacy:other"]).toBeUndefined();
    expect(c["privacy:words"]).toBeUndefined();
  });
});
