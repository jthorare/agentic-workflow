import { createHash } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import type { LoadedProfile } from "../profile/load.js";
import { denyPathsFor } from "../profile/schema.js";
import { compileExtraPatterns, makeScrubber, type Scrubber } from "../scrub/scrub.js";
import { type IndexDb, indexPath, type Layer, type LayerStatus, openIndex } from "./db.js";
import { EMBED_BATCH, embeddingText, encodeVec, type Embedder } from "./embed.js";
import { readManifestDeps } from "./deps-layer.js";
import { inventory, isGraphInput, isSourcePath, type IndexedFile } from "./files.js";
import { isReservedSnapshotPath, type GraphProvider } from "./graph.js";
import { matchesAny } from "./globs.js";
import { withHeavyLock } from "./heavy-lock.js";
import { refreshMirror } from "./mirror.js";
import { bandKeys, encodeSig, SHINGLE, signature } from "./minhash.js";
import { INDEXER_VERSION, parserId, typescriptParser } from "./parse-ts.js";

// The stamp names the parser (indexer and TypeScript versions): a new one re-parses every file.
// It also includes the utility globs: they decide each symbol's `utility` flag, so
// changing them must re-parse unchanged files.
// It also includes scrub.extraPatterns: they decide what stored bodies redact, so a new pattern
// must reach the bodies of unchanged files.
const structureStamp = (utilityGlobs: readonly string[], extraPatterns: readonly { kind: string; regex: string }[]): string =>
  `${parserId()}+${createHash("sha256").update(JSON.stringify([utilityGlobs, extraPatterns])).digest("hex").slice(0, 8)}`;

export type { Embedder } from "./embed.js";
export type { GraphProvider } from "./graph.js";
export interface Providers {
  embedder: Embedder | null;
  graph: GraphProvider | null;
}

export interface BuildReport {
  repo: string;
  commit: string | null;
  quick: boolean;
  files: { indexed: number; changed: number; removed: number; skipped: number };
  symbols: number;
  layers: Record<Layer, { status: LayerStatus; detail: string }>;
  ms: number;
}

function setLayer(db: IndexDb, layer: Layer, stamp: string, status: LayerStatus, detail: string, now: Date): void {
  db.prepare(
    "INSERT INTO layers (layer, stamp, status, detail, built_at) VALUES (?, ?, ?, ?, ?) ON CONFLICT(layer) DO UPDATE SET stamp = excluded.stamp, status = excluded.status, detail = excluded.detail, built_at = excluded.built_at",
  ).run(layer, stamp, status, detail, now.toISOString());
}

function stampOf(db: IndexDb, layer: Layer): string | null {
  return (db.prepare("SELECT stamp FROM layers WHERE layer = ?").get(layer) as { stamp: string } | undefined)?.stamp ?? null;
}

// A killed or out-of-disk build leaves `<repo>.db.tmp-*`; the heavy-job lock never runs two
// builds of one repo at once (same kind, index-build:<repo>), so anything older than an hour is garbage.
function sweepTmp(deps: Deps, live: string): void {
  const dir = path.dirname(live);
  for (const name of fs.readdirSync(dir)) {
    if (!name.startsWith(`${path.basename(live)}.tmp-`)) continue;
    const file = path.join(dir, name);
    if (deps.now().getTime() - fs.statSync(file).mtimeMs > 3_600_000) fs.rmSync(file, { force: true });
  }
}

function writeStructure(db: IndexDb, files: IndexedFile[], utilityGlobs: readonly string[], stamp: string, scrubber: Scrubber): { changed: number; removed: number } {
  if (stampOf(db, "structure") !== stamp) db.exec("DELETE FROM files");
  const known = new Map((db.prepare("SELECT path, hash FROM files").all() as { path: string; hash: string }[]).map((r) => [r.path, r.hash]));
  const sources = files.filter((f) => isSourcePath(f.path));
  const live = new Set(sources.map((f) => f.path));
  const removed = [...known.keys()].filter((p) => !live.has(p));
  const changed = sources.filter((f) => known.get(f.path) !== f.hash);
  const insertSymbol = db.prepare(
    `INSERT INTO symbols (file, name, kind, start_line, end_line, exported, utility, signature, ast_hash, token_count, complexity, callees, minhash, body)
     VALUES (@file, @name, @kind, @startLine, @endLine, @exported, @utility, @signature, @astHash, @tokenCount, @complexity, @callees, @minhash, @body)`,
  );
  const insertBand = db.prepare("INSERT INTO bands (key, symbol_id) VALUES (?, ?)");
  db.transaction(() => {
    for (const p of removed) db.prepare("DELETE FROM files WHERE path = ?").run(p);
    for (const f of changed) {
      db.prepare("DELETE FROM files WHERE path = ?").run(f.path);
      db.prepare("INSERT INTO files (path, hash, size) VALUES (?, ?, ?)").run(f.path, f.hash, f.size);
      const utility = matchesAny(f.path, utilityGlobs) ? 1 : 0;
      for (const s of typescriptParser.parse(f.path, f.text)) {
        const sig = signature(s.tokens);
        const { lastInsertRowid } = insertSymbol.run({
          ...s, exported: s.exported ? 1 : 0, utility, tokenCount: s.tokens.length,
          callees: JSON.stringify(s.callees), minhash: encodeSig(sig), body: scrubber.scrub(s.text).text,
        });
        // Fewer tokens than one shingle: every such symbol would share bands with every other, so none is banded.
        if (s.tokens.length >= SHINGLE) for (const key of bandKeys(sig)) insertBand.run(key, Number(lastInsertRowid));
      }
    }
  })();
  return { changed: changed.length, removed: removed.length };
}

function writeDeps(db: IndexDb, files: IndexedFile[]): void {
  const rows = files.filter((f) => f.path === "package.json" || f.path.endsWith("/package.json")).flatMap((f) => readManifestDeps(f.path, f.text));
  db.transaction(() => {
    db.exec("DELETE FROM deps");
    for (const r of rows) db.prepare("INSERT OR REPLACE INTO deps (manifest, name, version, kind, tags) VALUES (?, ?, ?, ?, ?)").run(r.manifest, r.name, r.version, r.kind, JSON.stringify(r.tags));
  })();
}

// Symbols embedded per build at most; the rest wait for the next build (pending), so one
// first build of a big repo can't hold the heavy-job lock for hours.
export const EMBED_PER_BUILD = 4096;

async function embedLayer(db: IndexDb, embedder: Embedder | null, now: Date, limit: number): Promise<void> {
  if (embedder === null) {
    setLayer(db, "embeddings", "none", "disabled", "no embedder configured", now);
    return;
  }
  const stamp = `${embedder.model}@${INDEXER_VERSION}`;
  const previous = stampOf(db, "embeddings");
  if (previous !== stamp) db.exec("DELETE FROM embeddings");
  // The cap is applied in SQL: only this build's bodies are read, never every un-embedded one.
  const missing = "FROM symbols s LEFT JOIN embeddings e ON e.symbol_id = s.id WHERE e.symbol_id IS NULL AND s.kind != 'class'";
  const remaining = (db.prepare(`SELECT COUNT(*) AS n ${missing}`).get() as { n: number }).n;
  const total = (db.prepare("SELECT COUNT(*) AS n FROM symbols s WHERE s.kind != 'class'").get() as { n: number }).n;
  const batch = db.prepare(`SELECT s.id, s.name, s.signature, s.body ${missing} ORDER BY s.id LIMIT ?`).all(limit) as { id: number; name: string; signature: string; body: string }[];
  const insert = db.prepare("INSERT OR REPLACE INTO embeddings (symbol_id, model, vector) VALUES (?, ?, ?)");
  let done = 0;
  // Total embedded so far (this and earlier builds), so the count rises build over build.
  const progress = (): string => `${total - (remaining - done)} of ${total} symbols embedded (${done} this build)`;
  try {
    // One request's vectors are written as they arrive: a failure keeps what was embedded, and
    // the next build asks only for the rest.
    for (let i = 0; i < batch.length; i += EMBED_BATCH) {
      const chunk = batch.slice(i, i + EMBED_BATCH);
      const vectors = await embedder.embed(chunk.map(embeddingText));
      db.transaction(() => {
        chunk.forEach((s, k) => insert.run(s.id, embedder.model, encodeVec(vectors[k])));
      })();
      done += chunk.length;
    }
    if (batch.length < remaining) setLayer(db, "embeddings", stamp, "pending", `${progress()}; the next build continues`, now);
    else setLayer(db, "embeddings", stamp, "ok", `${embedder.model} on loopback`, now);
  } catch (e) {
    // The other layers stay usable; the next build retries (Review Focus 3). Vectors written
    // this build are this stamp's, so the stamp is kept and the next build doesn't drop them.
    const message = (e as Error).message;
    setLayer(db, "embeddings", done === 0 ? (previous ?? "none") : stamp, "unavailable", done === 0 ? message : `${progress()}, then: ${message}`, now);
  }
}

async function graphLayer(
  db: IndexDb, provider: GraphProvider | null, deps: Deps, repoPath: string, deny: string[], ix: LoadedProfile["profile"]["index"], now: Date,
): Promise<void> {
  if (provider === null) {
    setLayer(db, "graph", "none", "disabled", "no graph provider configured", now);
    return;
  }
  const stamp = `graphify@${provider.version}`;
  let snap: string | null = null;
  try {
    // Tracked, non-denied source and docs files. The digest covers exactly these, so a
    // docs-only change reruns graphify and a package.json-only change does not.
    const inv = await inventory(deps.git, repoPath, { denyPaths: deny, maxFileKB: ix.maxFileKB, maxTotalMB: ix.maxTotalMB, select: isGraphInput });
    const digest = createHash("sha256").update(inv.files.map((f) => `${f.path}:${f.hash}`).join("\n")).digest("hex");
    const hasGraph = (db.prepare("SELECT COUNT(*) AS n FROM graph_nodes").get() as { n: number }).n > 0;
    const stored = (db.prepare("SELECT value FROM meta WHERE key = 'graph_digest'").get() as { value: string } | undefined)?.value;
    if (stored === digest && stampOf(db, "graph") === stamp && hasGraph) return;
    // A snapshot: graphify writes graphify-out/ into the directory it reads, so it never runs
    // on the working tree (spec amendment 3).
    snap = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-graph-"));
    // Never graphify's own dirs: a tracked file there would seed its temp/cache or output.
    for (const f of inv.files.filter((x) => !isReservedSnapshotPath(x.path))) {
      fs.mkdirSync(path.dirname(path.join(snap, f.path)), { recursive: true });
      fs.writeFileSync(path.join(snap, f.path), f.text);
    }
    const g = await provider.build(snap);
    db.transaction(() => {
      db.exec("DELETE FROM graph_nodes; DELETE FROM graph_edges;");
      for (const n of g.nodes) db.prepare("INSERT OR REPLACE INTO graph_nodes (id, file, name, line) VALUES (?, ?, ?, ?)").run(n.id, n.file, n.name, n.line);
      for (const e of g.edges) db.prepare("INSERT INTO graph_edges (src, dst, relation, confidence) VALUES (?, ?, ?, ?)").run(e.src, e.dst, e.relation, e.confidence);
      db.prepare("INSERT OR REPLACE INTO meta (key, value) VALUES ('graph_digest', ?)").run(digest);
    })();
    setLayer(db, "graph", stamp, "ok", `graphify ${provider.version}, ${g.nodes.length} nodes, ${g.edges.length} edges`, now);
  } catch (e) {
    setLayer(db, "graph", stampOf(db, "graph") ?? "none", "unavailable", (e as Error).message, now);
  } finally {
    if (snap !== null) fs.rmSync(snap, { recursive: true, force: true });
  }
}

// embedLimit: symbols embedded per build (default EMBED_PER_BUILD). lockTimeoutMs: how long to
// wait for the heavy-job lock (default 10 min; 0 tries once).
export async function buildIndex(
  deps: Deps, loaded: LoadedProfile, repo: string, o: { full: boolean; quick?: boolean; mirror?: boolean; embedLimit?: number; lockTimeoutMs?: number }, providers: Providers,
): Promise<BuildReport> {
  const cfg = loaded.repos[repo];
  if (cfg === undefined) throw new SindriError("SND-PROFILE-004", `no repo named ${repo}`);
  const ix = loaded.profile.index;
  const quick = o.quick === true;
  return withHeavyLock(deps, `index-build:${repo}`, o.lockTimeoutMs ?? 600_000, async () => {
    const started = deps.now().getTime();
    // Inside the heavy-job lock: a clone of a big repo is heavy too.
    if (o.mirror === true) await refreshMirror(deps, repo, cfg.path);
    const deny = denyPathsFor(ix, cfg.index);
    const inv = await inventory(deps.git, cfg.path, {
      denyPaths: deny,
      maxFileKB: ix.maxFileKB,
      maxTotalMB: ix.maxTotalMB,
      select: (p) => isSourcePath(p) || p === "package.json" || p.endsWith("/package.json"),
    });
    const live = indexPath(deps, repo);
    fs.mkdirSync(path.dirname(live), { recursive: true, mode: 0o700 });
    sweepTmp(deps, live);
    // Build into a temp copy and rename it over the live file: a crash or a full
    // disk leaves the previous complete index in place (Review Focus 2).
    const tmp = `${live}.tmp-${ulid(deps.now())}`;
    if (!o.full && fs.existsSync(live)) fs.copyFileSync(live, tmp);
    const db = openIndex(tmp);
    try {
      const now = deps.now();
      const stamp = structureStamp(ix.utilityGlobs, loaded.profile.scrub.extraPatterns);
      const scrubber = makeScrubber(compileExtraPatterns(loaded.profile.scrub.extraPatterns));
      const { changed, removed } = writeStructure(db, inv.files, ix.utilityGlobs, stamp, scrubber);
      setLayer(db, "structure", stamp, "ok", "TypeScript compiler API", now);
      setLayer(db, "clones", stamp, "ok", "AST hash + MinHash/LSH", now);
      writeDeps(db, inv.files);
      setLayer(db, "deps", `deps@${INDEXER_VERSION}`, "ok", "package.json manifests", now);
      if (quick) {
        // Quick builds never touch the network layers; a fresh index marks them pending.
        for (const layer of ["embeddings", "graph"] as const) {
          if (stampOf(db, layer) === null) setLayer(db, layer, "none", "pending", "not built yet (sindri index build)", now);
        }
      } else {
        await embedLayer(db, providers.embedder, now, o.embedLimit ?? EMBED_PER_BUILD);
        await graphLayer(db, providers.graph, deps, cfg.path, deny, ix, now);
      }
      const head = await deps.git.run(["rev-parse", "HEAD"], cfg.path);
      const commit = head.ok ? head.stdout.trim() : null;
      db.prepare("INSERT OR REPLACE INTO meta (key, value) VALUES ('commit', ?), ('built_at', ?)").run(commit ?? "", now.toISOString());
      const symbols = (db.prepare("SELECT COUNT(*) AS n FROM symbols").get() as { n: number }).n;
      const report: BuildReport = {
        repo,
        commit,
        quick,
        files: { indexed: inv.files.length, changed, removed, skipped: inv.skipped.length },
        symbols,
        layers: Object.fromEntries((db.prepare("SELECT layer, status, detail FROM layers").all() as { layer: Layer; status: LayerStatus; detail: string }[]).map((l) => [l.layer, { status: l.status, detail: l.detail }])) as BuildReport["layers"],
        ms: deps.now().getTime() - started,
      };
      db.close();
      fs.renameSync(tmp, live);
      return report;
    } catch (e) {
      if (db.open) db.close();
      fs.rmSync(tmp, { force: true });
      throw e;
    }
  });
}
