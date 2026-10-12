import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import { SindriError } from "../src/errors.js";
import { buildIndex } from "../src/index/build.js";
import { embedderFor, embedderOrUnavailable, graphFor, makeIndexCommand } from "../src/index/commands.js";
import type { Embedder } from "../src/index/embed.js";
import type { GraphProvider } from "../src/index/graph.js";
import { GRAPHIFY_PIN } from "../src/index/pins.js";
import { allSymbols, bandCandidates, depRows, embeddingRows, graphEdges, indexPath, layers, meta, openIndex, openIndexReadOnly, symbolsByAstHash } from "../src/index/db.js";
import { mirrorPath, refreshMirror } from "../src/index/mirror.js";
import { parserId } from "../src/index/parse-ts.js";
import { runCli } from "../src/main.js";
import { approvedIndexDeps, BODY, embedFetch, fakeIndexIo, profileFor, ring0Name, ring0Repo } from "./index-fixtures.js";
import { fakeSystem, git, gitRepo, makeDeps, tempDir } from "./helpers.js";
import { ledgerPath, openLedger } from "../src/ledger/db.js";
import { stateDir } from "../src/deps.js";
import Database from "better-sqlite3";

// Tests run from sindri/ (import.meta trips the vi hoisting transform in this file).
const INDEX_DOC = path.resolve(process.cwd(), "../docs/sindri/index.md");

afterEach(() => vi.restoreAllMocks());

const FILES = {
  "src/a.ts": "export function add(a: number, b: number): number {\n  return a + b;\n}\n",
  "src/b.ts": "export function sum(x: number, y: number): number {\n  return x + y;\n}\n",
  "src/util/strings.ts": "export const trim = (s: string) => s.trim();\n",
  "package.json": JSON.stringify({ dependencies: { dayjs: "^1" } }),
  ".env.local.ts": "export const k = 1;\n",
};

const none = { embedder: null, graph: null };

describe("buildIndex", () => {
  it("indexes structure, clones and deps, and reports disabled layers", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const report = await buildIndex(d, profileFor(root), "r", { full: false }, none);
    expect(report.files).toEqual({ indexed: 4, changed: 3, removed: 0, skipped: 1 });
    expect(report.symbols).toBe(3);
    expect(report.quick).toBe(false);
    expect(report.layers.structure.status).toBe("ok");
    expect(report.layers.embeddings).toEqual({ status: "disabled", detail: "no embedder configured" });
    const db = openIndexReadOnly(indexPath(d, "r"));
    if (db === null) throw new Error("no index");
    const addHash = (db.prepare("SELECT ast_hash FROM symbols WHERE name = 'add'").get() as { ast_hash: string }).ast_hash;
    const add = symbolsByAstHash(db, addHash);
    expect(add.map((s) => s.name).sort()).toEqual(["add", "sum"]);
    expect(add.find((s) => s.name === "add")?.utility).toBe(false);
    const trim = db.prepare("SELECT utility FROM symbols WHERE name = 'trim'").get() as { utility: number };
    expect(trim.utility).toBe(1);
    expect(depRows(db)).toEqual([{ manifest: "package.json", name: "dayjs", version: "^1", kind: "prod", tags: ["date"] }]);
    expect(bandCandidates(db, [])).toEqual([]);
    expect(meta(db).commit).toMatch(/^[0-9a-f]{40}$/);
    expect(layers(db).map((l) => l.layer).sort()).toEqual(["clones", "deps", "embeddings", "graph", "structure"]);
    // A TypeScript upgrade re-parses unchanged files: the structure stamp names the parser.
    expect(layers(db).find((l) => l.layer === "structure")?.stamp).toMatch(new RegExp(`^${parserId().replace(/[.+]/g, "\\$&")}\\+[0-9a-f]{8}$`));
    db.close();
  });

  it("still skips the built-in secret globs when the profile configures its own denyPaths (final review M2)", async () => {
    const root = gitRepo(FILES);
    const report = await buildIndex(makeDeps(), profileFor(root, { yaml: '  denyPaths:\n    - "src/b.ts"\n' }), "r", { full: false }, none);
    // .env.local.ts (a built-in glob) and src/b.ts (the profile's) are both skipped.
    expect(report.files).toEqual({ indexed: 3, changed: 2, removed: 0, skipped: 2 });
  });

  it("rebuilds only changed files, drops removed ones, and --full rebuilds everything", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    await buildIndex(d, p, "r", { full: false }, none);
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function add(a: number): number {\n  return a;\n}\n");
    fs.rmSync(path.join(root, "src/b.ts"));
    git(root, "add", "-A");
    const second = await buildIndex(d, p, "r", { full: false }, none);
    expect(second.files).toMatchObject({ changed: 1, removed: 1 });
    const third = await buildIndex(d, p, "r", { full: true }, none);
    expect(third.files).toMatchObject({ changed: 2, removed: 0 });
  });

  it("a quick build skips the network layers: pending on a fresh index, untouched after", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    const quick = await buildIndex(d, p, "r", { full: false, quick: true }, none);
    expect(quick.quick).toBe(true);
    expect(quick.layers.structure.status).toBe("ok");
    expect(quick.layers.embeddings).toEqual({ status: "pending", detail: "not built yet (sindri index build)" });
    expect(quick.layers.graph.status).toBe("pending");
    const full = await buildIndex(d, p, "r", { full: false }, none);
    expect(full.layers.embeddings.status).toBe("disabled");
    const quickAgain = await buildIndex(d, p, "r", { full: false, quick: true }, none);
    expect(quickAgain.layers.embeddings.status).toBe("disabled");
  });

  it("keeps the previous index when a build fails halfway (Review Focus 2)", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    await buildIndex(d, p, "r", { full: false }, none);
    const before = fs.readFileSync(indexPath(d, "r"));
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function changed() { return 1; }\n");
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(to) === indexPath(d, "r")) throw new Error("disk full");
      real(from, to);
    });
    await expect(buildIndex(d, p, "r", { full: false }, none)).rejects.toThrow("disk full");
    expect(fs.readFileSync(indexPath(d, "r"))).toEqual(before);
    expect(fs.readdirSync(path.dirname(indexPath(d, "r"))).filter((n) => n.includes(".tmp-"))).toEqual([]);
  });

  it("closes the temp copy and removes it when a build step throws before the swap", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const real = d.git;
    const exploding = { run: async (args: string[], cwd: string) => { if (args[0] === "rev-parse") throw new Error("git exploded"); return real.run(args, cwd); } };
    await expect(buildIndex({ ...d, git: exploding }, profileFor(root), "r", { full: false }, none)).rejects.toThrow("git exploded");
    expect(fs.readdirSync(path.dirname(indexPath(d, "r"))).filter((n) => n.includes(".tmp-"))).toEqual([]);
  });

  it("sweeps temp copies a killed build left behind, and keeps fresh ones", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const dir = path.dirname(indexPath(d, "r"));
    fs.mkdirSync(dir, { recursive: true });
    const old = path.join(dir, "r.db.tmp-old");
    const fresh = path.join(dir, "r.db.tmp-fresh");
    const otherRepo = path.join(dir, "x.db.tmp-old");
    for (const f of [old, fresh, otherRepo]) fs.writeFileSync(f, "x");
    fs.utimesSync(old, new Date("2026-10-08T09:00:00Z"), new Date("2026-10-08T09:00:00Z"));
    fs.utimesSync(fresh, new Date("2026-10-08T11:45:00Z"), new Date("2026-10-08T11:45:00Z"));
    fs.utimesSync(otherRepo, new Date("2026-10-08T09:00:00Z"), new Date("2026-10-08T09:00:00Z"));
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    expect([fs.existsSync(old), fs.existsSync(fresh), fs.existsSync(otherRepo)]).toEqual([false, true, true]);
  });

  it("scrubs secret-shaped strings out of stored symbol bodies", async () => {
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const root = gitRepo({ "src/k.ts": `export function key() { return "${secret}"; }\n` });
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const db = openIndexReadOnly(indexPath(d, "r"));
    const row = db?.prepare("SELECT body FROM symbols WHERE name = 'key'").get() as { body: string };
    db?.close();
    expect(row.body).toContain("export function key()");
    expect(row.body).not.toContain(secret);
  });

  it("re-parses every file when index.utilityGlobs changes", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const again = await buildIndex(d, profileFor(root, { utility: [] }), "r", { full: false }, none);
    expect(again.files.changed).toBe(3);
    const db = openIndexReadOnly(indexPath(d, "r"));
    expect(db?.prepare("SELECT utility FROM symbols WHERE name = 'trim'").get()).toEqual({ utility: 0 });
    db?.close();
  });

  it("reads symbols, embeddings and graph edges back (empty for a fresh build)", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const db = openIndexReadOnly(indexPath(d, "r"));
    if (db === null) throw new Error("no index");
    const all = allSymbols(db);
    expect(all.map((s) => s.name).sort()).toEqual(["add", "sum", "trim"]);
    expect(all[0].minhash).toBeInstanceOf(Uint32Array);
    expect(all.find((s) => s.name === "trim")?.exported).toBe(true);
    expect(bandCandidates(db, [])).toEqual([]);
    expect(embeddingRows(db, "m")).toEqual([]);
    expect(graphEdges(db)).toEqual([]);
    const keys = (db.prepare("SELECT key FROM bands WHERE symbol_id = ?").all(all[0].id) as { key: string }[]).map((r) => r.key);
    expect(bandCandidates(db, keys)).toContain(all[0].id);
    db.close();
    const rw = openIndex(indexPath(d, "r"));
    rw.prepare("INSERT INTO embeddings (symbol_id, model, vector) VALUES (?, 'm', ?)").run(all[0].id, Buffer.from([1, 2]));
    rw.prepare("INSERT INTO graph_edges (src, dst, relation, confidence) VALUES ('a', 'b', 'calls', 'EXTRACTED')").run();
    expect(embeddingRows(rw, "m")).toEqual([{ symbolId: all[0].id, vector: Buffer.from([1, 2]) }]);
    expect(embeddingRows(rw, "other")).toEqual([]);
    expect(graphEdges(rw)).toEqual([{ src: "a", dst: "b", relation: "calls", confidence: "EXTRACTED" }]);
    rw.close();
  });

  it("reports no commit for a repo without commits", async () => {
    const root = tempDir("sindri-empty-");
    git(root, "init", "-q", "-b", "main");
    const report = await buildIndex(makeDeps(), profileFor(root), "r", { full: false }, none);
    expect(report.commit).toBeNull();
    expect(report.symbols).toBe(0);
  });

  it("does not band symbols with fewer tokens than one shingle (no tiny-symbol clones)", async () => {
    const root = gitRepo({ "src/t.ts": "function p() {}\nexport function q() {}\n" + BODY("big") });
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const db = openIndexReadOnly(indexPath(d, "r"));
    if (db === null) throw new Error("no index");
    const rows = db.prepare("SELECT name, token_count, (SELECT COUNT(*) FROM bands WHERE symbol_id = symbols.id) AS n FROM symbols ORDER BY name").all() as { name: string; token_count: number; n: number }[];
    db.close();
    const tiny = rows.filter((r) => r.name === "p" || r.name === "q");
    expect(tiny.every((r) => r.token_count < 5 && r.n === 0)).toBe(true);
    expect(rows.find((r) => r.name === "big")?.n).toBeGreaterThan(0);
  });

  it("re-scrubs unchanged files when scrub.extraPatterns changes", async () => {
    const root = gitRepo({ "src/k.ts": 'export function key() { return "internal-token-4242"; }\n' });
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const body = () => {
      const db = openIndexReadOnly(indexPath(d, "r"));
      const row = db?.prepare("SELECT body FROM symbols WHERE name = 'key'").get() as { body: string };
      db?.close();
      return row.body;
    };
    expect(body()).toContain("internal-token-4242");
    const top = "scrub:\n  extraPatterns:\n    - kind: internal-token\n      regex: 'internal-token-[0-9]+'\n";
    const again = await buildIndex(d, profileFor(root, { top }), "r", { full: false }, none);
    expect(again.files.changed).toBe(1);
    expect(body()).not.toContain("internal-token-4242");
  });

  it("mirrors inside the heavy-job lock when asked", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const lock = path.join(d.env.AW_STATE_DIR as string, "locks", "heavy-job.lock");
    const seen: boolean[] = [];
    const git = { run: async (args: string[], cwd: string) => { if (args[0] === "clone") seen.push(fs.existsSync(lock)); return d.git.run(args, cwd); } };
    await buildIndex({ ...d, git }, profileFor(root), "r", { full: false, mirror: true }, none);
    expect(seen).toEqual([true]);
  });

  it("a quick build tries the heavy-job lock once and skips quietly (exit 0) when every slot is held; a full build waits", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES));
    let slept = 0;
    const lock = path.join(d.env.AW_STATE_DIR as string, "locks", "heavy-job.lock");
    fs.mkdirSync(lock, { recursive: true });
    fs.mkdirSync(`${lock}.2`);
    const deps = { ...d, sleep: async () => { slept++; } };
    const quick = await makeIndexCommand(fakeIndexIo())(["build", "--quick"], deps);
    expect(quick.exitCode).toBe(0);
    expect(quick.stdout.trim()).toMatch(new RegExp(`^${ring0Name(d)}: skipped \\(the heavy-job lock is busy \\(all 2 slots: slot 1; held \\d+ (min|h) \\| slot 2; held \\d+ (min|h)\\)\\); the next hourly run retries$`));
    expect(slept).toBe(0);
    expect(fs.existsSync(indexPath(d, ring0Name(d)))).toBe(false);
    const full = await makeIndexCommand(fakeIndexIo())(["build"], deps);
    expect(full.stderr).toContain("SND-INDEX-001");
    expect(slept).toBe(600);
    fs.rmdirSync(lock);
    fs.rmdirSync(`${lock}.2`);
  });

  it("refuses an unknown repo", async () => {
    const root = gitRepo(FILES);
    await expect(buildIndex(makeDeps(), profileFor(root), "nope", { full: false }, none)).rejects.toThrow(/SND-PROFILE-004|no repo named nope/);
  });
});

describe("mirror", () => {
  it("creates a bare mirror, refreshes it, and reports a repo git can't clone", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    expect(await refreshMirror(d, "r", root)).toBe(mirrorPath(d, "r"));
    expect(fs.existsSync(path.join(mirrorPath(d, "r"), "HEAD"))).toBe(true);
    expect(fs.statSync(mirrorPath(d, "r")).mode & 0o777).toBe(0o700);
    expect(fs.statSync(path.dirname(mirrorPath(d, "r"))).mode & 0o777).toBe(0o700);
    fs.writeFileSync(path.join(root, "src/c.ts"), "export const c = 1;\n");
    git(root, "add", "-A");
    git(root, "commit", "-qm", "second");
    await refreshMirror(d, "r", root);
    expect(git(root, "--git-dir", mirrorPath(d, "r"), "log", "--format=%s")).toContain("second");
    const err = await refreshMirror(d, "x", tempDir()).catch((e: unknown) => e);
    expect((err as SindriError).code).toBe("SND-INDEX-002");
  });
});

describe("sindri index build | status", () => {
  const idx = makeIndexCommand(fakeIndexIo());

  it("builds every repo in the approved profile, mirrors it, and reports status", async () => {
    const root = ring0Repo(FILES);
    const logs: string[] = [];
    const d = { ...(await approvedIndexDeps(root)), log: (l: string) => logs.push(l) };
    const build = await idx(["build"], d);
    expect(build.exitCode).toBe(0);
    expect(build.stdout).toMatch(/: 4 files \(3 changed, 0 removed, 1 skipped\), 3 symbols; structure ok, clones ok, deps ok, embeddings disabled, graph disabled/);
    expect(logs).toEqual([expect.stringContaining("; this takes the heavy-job lock")]);
    expect(fs.existsSync(path.join(mirrorPath(d, ring0Name(d)), "HEAD"))).toBe(true);
    const status = await idx(["status"], d);
    expect(status.exitCode).toBe(0);
    expect(status.stdout).toMatch(/built 0 min ago at [0-9a-f]{12}; structure ok/);
    const json = JSON.parse((await idx(["status", "--json"], d)).stdout);
    expect(json[0]).toMatchObject({ missing: false, stale: false, layers: { structure: { status: "ok" } } });
  });

  it("build --json is an object { reports, unapproved }, and docs/sindri/index.md documents that shape", async () => {
    const root = ring0Repo(FILES);
    const d = await approvedIndexDeps(root);
    const j = JSON.parse((await idx(["build", "--json"], d)).stdout) as Record<string, unknown>;
    expect(Object.keys(j).sort()).toEqual(["reports", "unapproved"]);
    expect(j.unapproved).toEqual([]);
    expect((j.reports as { repo: string }[]).map((r) => r.repo)).toEqual([ring0Name(d)]);
    const one = JSON.parse((await idx(["build", "--json", "--repo", ring0Name(d)], d)).stdout) as Record<string, unknown>;
    expect(one).toMatchObject({ unapproved: [], reports: [{ repo: ring0Name(d) }] });
    const doc = fs.readFileSync(INDEX_DOC, "utf8");
    expect(doc).toContain('`index build --json` prints `{ "reports": [...], "unapproved": [...] }`');
  });

  it("mirrors on full builds only", async () => {
    const root = ring0Repo(FILES);
    const d = await approvedIndexDeps(root);
    await idx(["build", "--quick"], d);
    expect(fs.existsSync(mirrorPath(d, ring0Name(d)))).toBe(false);
    await idx(["build"], d);
    expect(fs.existsSync(mirrorPath(d, ring0Name(d)))).toBe(true);
  });

  it("lists every repo, including one with no index, and exits 1 (nothing aborts)", async () => {
    const root = ring0Repo(FILES);
    const d = await approvedIndexDeps(root, { extraRepos: ["ghost"] });
    const name = ring0Name(d);
    await idx(["build", "--repo", name], d);
    const r = await idx(["status"], d);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain(`${name}: built `);
    expect(r.stdout).toContain("ghost: no index (sindri index build --repo ghost)");
    const missing = JSON.parse((await idx(["status", "--json", "--repo", "ghost"], d)).stdout);
    expect(missing).toEqual([{ repo: "ghost", missing: true, commit: "", builtAt: null, ageMs: null, stale: true, layers: {} }]);
  });

  it("says never built for an empty index, and shows the reason for a layer that is not ok", async () => {
    const root = ring0Repo(FILES);
    const d = await approvedIndexDeps(root);
    openIndex(indexPath(d, ring0Name(d))).close();
    const r = await idx(["status"], d);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("stale (never built); structure pending (not built yet), clones pending (not built yet)");
    expect(r.stdout).toContain("  fix: sindri index build --repo");
    expect(r.stdout).not.toContain("NaN");
  });

  it("marks a stale index (older than index.maxAgeHours) with the word stale and a fix line", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES));
    await idx(["build"], d);
    const later = { ...d, now: () => new Date(d.now().getTime() + 48 * 3_600_000) };
    const r = await idx(["status"], later);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toMatch(/stale \(built 48 h ago at [0-9a-f]{12}\)/);
    expect(r.stdout).toContain(`  fix: sindri index build --repo ${ring0Name(d)}`);
  });

  it("says no approved profile when the ledger exists but nothing was approved", async () => {
    const d = makeDeps();
    openLedger(ledgerPath(stateDir(d))).close();
    expect((await idx(["status"], d)).stderr).toContain("SND-PROFILE-012");
  });

  it("never creates or migrates the ledger (status reads it read-only)", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES));
    const file = ledgerPath(stateDir(d));
    const raw = new Database(file);
    raw.pragma("wal_checkpoint(TRUNCATE)");
    raw.pragma("user_version = 1");
    raw.close();
    const before = fs.readFileSync(file);
    expect((await idx(["status"], d)).stderr).not.toContain("SND-");
    expect(fs.readFileSync(file)).toEqual(before);
    const check = new Database(file, { readonly: true });
    expect(check.pragma("user_version", { simple: true })).toBe(1);
    check.close();
    const bare = makeDeps();
    expect((await idx(["status"], bare)).stderr).toContain("SND-PROFILE-012");
    expect(fs.existsSync(ledgerPath(stateDir(bare)))).toBe(false);
  });

  it("says no approved profile, an unknown subcommand, an unknown repo, and shows usage through the registry", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES));
    expect((await idx(["build"], makeDeps())).stderr).toContain("SND-PROFILE-012");
    expect((await idx(["frob"], d)).stderr).toContain("SND-CLI-002");
    expect((await idx([], d)).stderr).toContain("unknown index subcommand: (none)");
    expect((await idx(["build", "--repo", "zzz"], d)).stderr).toContain("SND-PROFILE-004");
    const help = await runCli(["index", "--help"], makeDeps());
    expect(help.stdout).toContain("sindri index build [--repo NAME] [--quick] [--full] [--json]");
    expect(help.stdout).toContain("sindri index setup [--dry-run] [--json]");
  });
});
function fakeEmbedder(model = "m1"): Embedder & { seen: string[] } {
  const seen: string[] = [];
  return {
    model,
    seen,
    embed: async (texts) => {
      seen.push(...texts);
      return texts.map((t) => new Float32Array([t.length, 1]));
    },
  };
}

describe("embeddings layer", () => {
  it("embeds new symbols only, re-embeds on a model change, and degrades on failure", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    const e1 = fakeEmbedder();
    const first = await buildIndex(d, p, "r", { full: false }, { embedder: e1, graph: null });
    expect(first.layers.embeddings).toEqual({ status: "ok", detail: "m1 on loopback" });
    expect(e1.seen).toHaveLength(3);
    fs.writeFileSync(path.join(root, "src/c.ts"), "export function c() { return 3; }\n");
    git(root, "add", "-A");
    const e2 = fakeEmbedder();
    await buildIndex(d, p, "r", { full: false }, { embedder: e2, graph: null });
    expect(e2.seen).toEqual([expect.stringContaining("function c()")]);
    const e3 = fakeEmbedder("m2");
    await buildIndex(d, p, "r", { full: false }, { embedder: e3, graph: null });
    expect(e3.seen).toHaveLength(4);
    const broken: Embedder = { model: "m2", embed: async () => { throw new Error("embedding server unreachable: down"); } };
    fs.writeFileSync(path.join(root, "src/d.ts"), "export function d() { return 4; }\n");
    git(root, "add", "-A");
    const degraded = await buildIndex(d, p, "r", { full: false }, { embedder: broken, graph: null });
    expect(degraded.layers.embeddings).toEqual({ status: "unavailable", detail: "embedding server unreachable: down" });
    expect(degraded.layers.structure.status).toBe("ok");
  });

  it("commits vectors per batch, so a failure keeps what was embedded and the next build continues from there", async () => {
    const many = Object.fromEntries(Array.from({ length: 40 }, (_, i) => [`src/f${i}.ts`, `export function f${i}(x: number) { return x + ${i}; }\n`]));
    const root = gitRepo(many);
    const d = makeDeps();
    const p = profileFor(root);
    let calls = 0;
    const flaky: Embedder = { model: "m1", embed: async (texts) => { if (++calls === 2) throw new Error("embedding server unreachable: restarted"); return texts.map(() => new Float32Array([1, 0])); } };
    const r = await buildIndex(d, p, "r", { full: false }, { embedder: flaky, graph: null });
    expect(r.layers.embeddings).toEqual({ status: "unavailable", detail: "32 of 40 symbols embedded (32 this build), then: embedding server unreachable: restarted" });
    const after = fakeEmbedder();
    const r2 = await buildIndex(d, p, "r", { full: false }, { embedder: after, graph: null });
    expect(after.seen).toHaveLength(8);
    expect(r2.layers.embeddings.status).toBe("ok");
  });

  it("embeds at most embedLimit symbols per build (pending), and the next builds finish the rest", async () => {
    const many = Object.fromEntries(Array.from({ length: 12 }, (_, i) => [`src/f${i}.ts`, `export function f${i}(x: number) { return x + ${i}; }\n`]));
    const d = makeDeps();
    const p = profileFor(gitRepo(many));
    const e = fakeEmbedder();
    const r = await buildIndex(d, p, "r", { full: false, embedLimit: 5 }, { embedder: e, graph: null });
    expect(r.layers.embeddings).toEqual({ status: "pending", detail: "5 of 12 symbols embedded (5 this build); the next build continues" });
    expect(e.seen).toHaveLength(5);
    const second = await buildIndex(d, p, "r", { full: false, embedLimit: 5 }, { embedder: e, graph: null });
    expect(second.layers.embeddings.detail).toBe("10 of 12 symbols embedded (5 this build); the next build continues");
    expect((await buildIndex(d, p, "r", { full: false, embedLimit: 5 }, { embedder: e, graph: null })).layers.embeddings.status).toBe("ok");
    expect(e.seen).toHaveLength(12);
  });

  it("reads at most embedLimit symbol bodies per build, never every un-embedded one", async () => {
    const many = Object.fromEntries(Array.from({ length: 12 }, (_, i) => [`src/f${i}.ts`, `export function f${i}(x: number) { return x + ${i}; }\n`]));
    const d = makeDeps();
    const p = profileFor(gitRepo(many));
    const bodies: number[] = [];
    const prepare = Database.prototype.prepare;
    vi.spyOn(Database.prototype, "prepare").mockImplementation(function (this: Database.Database, source: string) {
      const stmt = prepare.call(this, source);
      if (!source.includes("s.body")) return stmt;
      const all = stmt.all.bind(stmt);
      return Object.assign(stmt, { all: (limit: number) => { const rows = all(limit); bodies.push(rows.length); return rows; } });
    } as typeof Database.prototype.prepare);
    const r = await buildIndex(d, p, "r", { full: false, embedLimit: 5 }, { embedder: fakeEmbedder(), graph: null });
    expect(r.layers.embeddings.detail).toBe("5 of 12 symbols embedded (5 this build); the next build continues");
    expect(bodies).toEqual([5]);
  });

  it("is unavailable, not stampless, on a first build whose embedder fails", async () => {
    const root = gitRepo(FILES);
    const broken: Embedder = { model: "m1", embed: async () => { throw new Error("embedding server unreachable: down"); } };
    const r = await buildIndex(makeDeps(), profileFor(root), "r", { full: false }, { embedder: broken, graph: null });
    expect(r.layers.embeddings.status).toBe("unavailable");
  });

  it("a quick build never calls the embedder and leaves the layer as it was", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    await buildIndex(d, p, "r", { full: false }, { embedder: fakeEmbedder(), graph: null });
    fs.writeFileSync(path.join(root, "src/c.ts"), "export function c() { return 3; }\n");
    git(root, "add", "-A");
    const e = fakeEmbedder();
    const quick = await buildIndex(d, p, "r", { full: false, quick: true }, { embedder: e, graph: null });
    expect(e.seen).toEqual([]);
    expect(quick.layers.embeddings).toEqual({ status: "ok", detail: "m1 on loopback" });
    expect(quick.files.changed).toBe(1);
  });
});

describe("embedderFor and the build command", () => {
  it("builds the Ollama embedder from the profile, or none when embeddings are off", () => {
    const root = gitRepo(FILES);
    expect(embedderFor(profileFor(root), fakeIndexIo())?.model).toBe("nomic-embed-text");
    expect(embedderFor(profileFor(root, { yaml: "  embeddings:\n    enabled: false\n" }), fakeIndexIo())).toBeNull();
  });

  it("index build reports embeddings ok when Ollama answers, and unavailable with the reason when it doesn't", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES), { index: "index:\n  graph: none\n" });
    const ok = await makeIndexCommand(fakeIndexIo({ fetch: embedFetch() }))(["build"], d);
    expect(ok.stdout).toContain("embeddings ok");
    const down = await makeIndexCommand(fakeIndexIo())(["build", "--full"], d);
    expect(down.stdout).toContain("embeddings unavailable (embedding server unreachable: connect ECONNREFUSED)");
    expect((await makeIndexCommand(fakeIndexIo())(["status"], d)).stdout).toContain("embeddings unavailable (embedding server unreachable");
  });
});

describe("a profile that slips a non-loopback URL or cloud model past the schema (Review Focus 3)", () => {
  it.each([
    ["non-loopback URL", { url: "https://api.example.com", model: "m1" }, "is not loopback"],
    ["cloud model", { url: "http://127.0.0.1:11434", model: "x-cloud" }, "cloud model"],
  ])("a %s leaves embeddings unavailable and the other layers built, with no request made", async (_n, emb, reason) => {
    const root = gitRepo(FILES);
    const p = profileFor(root);
    p.profile.index.embeddings = { enabled: true, ...emb };
    let calls = 0;
    const io = fakeIndexIo({ fetch: async () => { calls++; throw new Error("must not be called"); } });
    const r = await buildIndex(makeDeps(), p, "r", { full: false }, { embedder: embedderOrUnavailable(p, io), graph: null });
    expect(r.layers.embeddings.status).toBe("unavailable");
    expect(r.layers.embeddings.detail).toContain(reason);
    expect(r.layers.structure.status).toBe("ok");
    expect(r.symbols).toBe(3);
    expect(calls).toBe(0);
  });
});

function fakeGraph(fail = false): GraphProvider & { snapshots: string[][] } {
  const snapshots: string[][] = [];
  return {
    version: "9.9",
    snapshots,
    build: async (dir) => {
      snapshots.push(fs.readdirSync(dir, { recursive: true }).map(String).sort());
      if (fail) throw new Error("graphify failed (exit 1): boom");
      return { nodes: [{ id: "a", file: "src/a.ts", name: "add", line: 1 }], edges: [{ src: "a", dst: "a", relation: "calls", confidence: "EXTRACTED" }] };
    },
  };
}

describe("graph layer", () => {
  it("runs on a snapshot of source and docs only, when the inputs changed, retries after a failure, and degrades", async () => {
    // Tracked files under graphify's own names never reach the snapshot: they'd seed its temp/cache or output dir.
    const root = gitRepo({ ...FILES, "docs/readme.md": "# r\n", "data.sqlite": "x", ".sindri-tmp/cache/x.md": "planted", "graphify-out/notes.md": "planted" });
    const d = makeDeps();
    const p = profileFor(root);
    const g = fakeGraph();
    const first = await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    expect(first.layers.graph).toEqual({ status: "ok", detail: "graphify 9.9, 1 nodes, 1 edges" });
    expect(g.snapshots[0]).toEqual(expect.arrayContaining(["docs/readme.md", "src/a.ts"]));
    expect(g.snapshots[0].filter((f) => f.startsWith(".sindri-tmp") || f.startsWith("graphify-out"))).toEqual([]);
    expect(g.snapshots[0]).not.toContain(".env.local.ts");
    expect(g.snapshots[0]).not.toContain("data.sqlite");
    const db = openIndexReadOnly(indexPath(d, "r"));
    expect(db === null ? [] : graphEdges(db)).toHaveLength(1);
    db?.close();
    await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    expect(g.snapshots).toHaveLength(1);
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function add2() { return 1; }\n");
    const failed = await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: fakeGraph(true) });
    expect(failed.layers.graph).toEqual({ status: "unavailable", detail: "graphify failed (exit 1): boom" });
    expect(failed.layers.structure.status).toBe("ok");
    const retry = fakeGraph();
    const again = await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: retry });
    expect(retry.snapshots).toHaveLength(1);
    expect(again.layers.graph.status).toBe("ok");
  });

  it("is unavailable on a first build whose graphify fails", async () => {
    const r = await buildIndex(makeDeps(), profileFor(gitRepo(FILES)), "r", { full: false }, { embedder: null, graph: fakeGraph(true) });
    expect(r.layers.graph.status).toBe("unavailable");
  });

  it("a full build after quick builds still rebuilds the graph for changes a quick build absorbed", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    const g = fakeGraph();
    await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function add2() { return 1; }\n");
    await buildIndex(d, p, "r", { full: false, quick: true }, { embedder: null, graph: g });
    expect(g.snapshots).toHaveLength(1);
    await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    expect(g.snapshots).toHaveLength(2);
  });
});

describe("graphFor and the build command", () => {
  it("builds the provider from the profile and the injected probes, or none when the graph is off", () => {
    const root = gitRepo(FILES);
    expect(graphFor(profileFor(root), makeDeps(), fakeIndexIo())?.version).toBe(GRAPHIFY_PIN);
    expect(graphFor(profileFor(root, { yaml: "  graph: none\n" }), makeDeps(), fakeIndexIo())).toBeNull();
  });

  it("index build runs graphify through the injected probes, sandboxed", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES), { index: "index:\n  embeddings:\n    enabled: false\n" });
    const ran: string[][] = [];
    const io = fakeIndexIo({
      probes: {
        has: () => true,
        getJson: async () => null,
        run: async (argv) => {
          ran.push(argv);
          const snap = argv[argv.indexOf("extract") + 1];
          fs.mkdirSync(path.join(snap, "graphify-out"), { recursive: true });
          fs.writeFileSync(path.join(snap, "graphify-out", "graph.json"), JSON.stringify({ nodes: [{ id: "a", source_file: "src/a.ts", label: "add" }], links: [{ source: "a", target: "a", relation: "calls" }] }));
          return { code: 0, stdout: "", stderr: "" };
        },
      },
    });
    const r = await makeIndexCommand(io)(["build"], d);
    expect(r.stdout).toContain("graph ok");
    expect(ran[0][0]).toBe("/usr/bin/sandbox-exec");
  });
});

describe("graph layer fails closed (Review Focus 4)", () => {
  // Each case: the graph layer is unavailable with the reason, and structure, clones and deps still build.
  async function buildWith(deps: ReturnType<typeof makeDeps>, io: ReturnType<typeof fakeIndexIo>) {
    const p = profileFor(gitRepo(FILES));
    const r = await buildIndex(deps, p, "r", { full: false }, { embedder: null, graph: graphFor(p, deps, io) });
    expect(r.layers.graph.status).toBe("unavailable");
    expect([r.layers.structure.status, r.layers.clones.status, r.layers.deps.status]).toEqual(["ok", "ok", "ok"]);
    expect(r.symbols).toBe(3);
    const db = openIndexReadOnly(indexPath(deps, "r"));
    expect(db === null ? null : graphEdges(db)).toEqual([]);
    db?.close();
    return r.layers.graph.detail;
  }
  const never = async (): Promise<never> => {
    throw new Error("graphify must not run");
  };

  it("graphify missing: nothing runs", async () => {
    const io = fakeIndexIo({ probes: { has: (b) => b === "/usr/bin/sandbox-exec", getJson: async () => null, run: never } });
    expect(await buildWith(makeDeps(), io)).toBe("graphify is not installed (sindri index setup)");
  });

  it("sandbox missing: graphify never runs unsandboxed", async () => {
    const io = fakeIndexIo({ probes: { has: (b) => b === "graphify", getJson: async () => null, run: never } });
    expect(await buildWith(makeDeps(), io)).toMatch(/^no network sandbox available/);
    expect(await buildWith(makeDeps({ system: fakeSystem({ platform: "linux" }) }), io)).toMatch(/^no network sandbox available/);
    expect(await buildWith(makeDeps({ system: fakeSystem({ platform: "win32" }) }), io)).toMatch(/^no network sandbox available/);
  });

  it("graphify trying the network or reading the home dir's secrets: the sandbox refuses, the layer is unavailable", async () => {
    const d = makeDeps();
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const ran: string[][] = [];
    const io = fakeIndexIo({
      probes: {
        has: () => true,
        getJson: async () => null,
        // What graphify sees under the sandbox: no network, and the credential dirs unreadable.
        run: async (argv) => {
          ran.push(argv);
          return { code: 1, stdout: "", stderr: `PermissionError: [Errno 1] Operation not permitted: '${d.home}/.aws/credentials' ${secret}\nTraceback` };
        },
      },
    });
    const detail = await buildWith(d, io);
    expect(ran).toHaveLength(1);
    expect(ran[0].slice(0, 2)).toEqual(["/usr/bin/sandbox-exec", "-p"]);
    expect(ran[0][2]).toContain("(deny network*)");
    expect(ran[0][2]).toContain("(deny lsopen)");
    // By the home's real path: the temp home sits under the /var -> /private/var symlink.
    for (const dir of [".ssh", ".aws", ".gnupg", ".agentic-workflow", "Library/Keychains"]) expect(ran[0][2]).toContain(`(subpath "${fs.realpathSync(d.home)}/${dir}")`);
    expect(detail).toMatch(/^graphify failed \(exit 1\): PermissionError: \[Errno 1\] Operation not permitted/);
    expect(detail).not.toContain(secret);
  });
});

describe("graph layer inputs and stored paths", () => {
  it("reruns graphify for a docs-only change, and not for a package.json-only change", async () => {
    const root = gitRepo({ ...FILES, "docs/readme.md": "# r\n" });
    const d = makeDeps();
    const p = profileFor(root);
    const g = fakeGraph();
    await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    fs.writeFileSync(path.join(root, "package.json"), JSON.stringify({ dependencies: { dayjs: "^2" } }));
    await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    expect(g.snapshots).toHaveLength(1);
    fs.writeFileSync(path.join(root, "docs/readme.md"), "# r2\n");
    await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: g });
    expect(g.snapshots).toHaveLength(2);
  });

  it("stores graphify's absolute snapshot paths relative to the repo root", async () => {
    const p = profileFor(gitRepo(FILES));
    const d = makeDeps();
    const io = fakeIndexIo({
      probes: {
        has: () => true,
        getJson: async () => null,
        run: async (argv) => {
          const snap = argv[argv.indexOf("extract") + 1];
          fs.mkdirSync(path.join(snap, "graphify-out"), { recursive: true });
          const nodes = [{ id: "a", source_file: `${snap}/src/a.ts`, label: "add" }, { id: "b", source_file: `${fs.realpathSync(snap)}/src/b.ts`, label: "sum" }, { id: "x", source_file: "/elsewhere/x.ts" }];
          fs.writeFileSync(path.join(snap, "graphify-out", "graph.json"), JSON.stringify({ nodes, links: [] }));
          return { code: 0, stdout: "", stderr: "" };
        },
      },
    });
    const r = await buildIndex(d, p, "r", { full: false }, { embedder: null, graph: graphFor(p, d, io) });
    expect(r.layers.graph.status).toBe("ok");
    const db = openIndexReadOnly(indexPath(d, "r"));
    expect(db?.prepare("SELECT id, file FROM graph_nodes ORDER BY id").all()).toEqual([{ id: "a", file: "src/a.ts" }, { id: "b", file: "src/b.ts" }, { id: "x", file: null }]);
    db?.close();
  });
});

describe("sindri index query", () => {
  const idx = makeIndexCommand(fakeIndexIo());
  const files = { "src/util/text.ts": BODY("clip"), "src/b.ts": BODY("shorten"), "src/feature.ts": BODY("widen", "out.reverse(); out.sort();") };

  it("lists a symbol, its exact clones and its near clones", async () => {
    const d = await approvedIndexDeps(ring0Repo(files));
    await idx(["build"], d);
    const r = await idx(["query", "clip"], d);
    const lines = r.stdout.trim().split("\n");
    expect(lines[0]).toBe("src/util/text.ts:1 clip");
    expect(lines[1]).toBe("src/b.ts:1 shorten (exact)");
    expect(lines[2]).toMatch(/^src\/feature\.ts:1 widen \(near 0\.\d\d\)$/);
    expect(lines).toHaveLength(3);
    const json = JSON.parse((await idx(["query", "clip", "--json", "--repo", ring0Name(d)], d)).stdout);
    expect(json.map((x: { relation: string }) => x.relation)).toEqual(["match", "exact", "near"]);
  });

  it("says when there is no such symbol, no name, or no index", async () => {
    const d = await approvedIndexDeps(ring0Repo(files), { extraRepos: ["ghost"] });
    await idx(["build", "--repo", ring0Name(d)], d);
    expect((await idx(["query", "nope", "--repo", ring0Name(d)], d)).stdout).toContain(`No symbol named nope in ${ring0Name(d)}.`);
    expect((await idx(["query"], d)).stderr).toContain("SND-CLI-002");
    const noIndex = await idx(["query", "clip", "--repo", "ghost"], d);
    expect(noIndex.stderr).toContain("SND-INDEX-404");
    expect(noIndex.stderr).toContain("fix: sindri index build --repo ghost");
  });
});
