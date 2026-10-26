#!/usr/bin/env node
// tests/fixtures/line/selfcheck.mjs — the line fixture's own checks (line spec 10.3, phase LN-F). No dependencies.
//
//   node selfcheck.mjs [--json]
//       Every gold file's shape and keys; the packet hashes and their chain; the design checks that run on plain files
//       (DSN-COV, ACC, DEC, CITE, XSPEC, STYLE, LENS, ESC); the plan's DAG, sizes and base rules (PLN-*); the hidden
//       sets, their seals and canaries; the two style packs and the two lens packs; the scripted owner answers; every
//       planted item, found by its tag; and no secret-shaped file.
//   node selfcheck.mjs style-check <spec.md> <pack-id> [--json]
//       DSN-STYLE-001 for one spec against a style pack of this fixture (plain | formal). Exit 0 holds, 1 findings.
//   node selfcheck.mjs diff-scope <repo> <base> <head> <P1..P4> [--json]
//       The files the range base..head changes that the phase's allowedFiles do not allow (writeScope, the
//       test-change paths, and new test files). Exit 0 none, 1 some.
//   node selfcheck.mjs leak-check <report.json> <P1..P4>
//       The deterministic overlap check of spec 9.3: an observed line that shares a run of 8 or more words with a
//       scenario of the phase's hidden set. Exit 0 clean, 1 leaks.
//
// Packet hashes (the parents in index.json, freeze.json) are real `aw-packets hash` values; this script checks that they
// agree with each other, not that they are the hash (Aw.Packets.Tests' LineFixtureTests does that against the tool). The
// pack pins and file hashes (dirHash, fileHash) are the fixture's own; see README.md, "Choices". Exit: 0 every check passes, 1 a check fails, 2 usage or an unreadable input.
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SCOPING = path.join(HERE, "..", "scoping");
const at = (...p) => path.join(HERE, ...p);
const readJson = (p) => JSON.parse(fs.readFileSync(p, "utf8"));
const readText = (p) => fs.readFileSync(p, "utf8");
const readJsonl = (p) => readText(p).split("\n").filter(Boolean).map((l) => JSON.parse(l));
const sha = (b) => crypto.createHash("sha256").update(b).digest("hex");
const PHASES = ["P1", "P2", "P3", "P4"];

function walk(d, base = d) {
  return fs.readdirSync(d, { withFileTypes: true })
    .flatMap((e) => (e.isDirectory() ? walk(path.join(d, e.name), base) : [path.relative(base, path.join(d, e.name)).split(path.sep).join("/")])).sort();
}
export const dirHash = (dir) => "sha256:" + sha(walk(dir).map((n) => `${n}\0${sha(fs.readFileSync(path.join(dir, n)))}\n`).join(""));
export const fileHash = (p) => "sha256:" + sha(fs.readFileSync(p));

// --- a small shape validator: required keys, types, enums, and no unknown keys when `strict` ---
function shape(value, spec, where, errors) {
  const typeOf = (v) => (v === null ? "null" : Array.isArray(v) ? "array" : typeof v);
  if (typeof spec === "string") {
    const want = spec.split("|");
    if (!want.includes(typeOf(value)) && !(want.includes("integer") && Number.isInteger(value))) errors.push(`${where}: expected ${spec}, got ${typeOf(value)}`);
    return;
  }
  if (Array.isArray(spec)) {
    if (!Array.isArray(value)) { errors.push(`${where}: expected array`); return; }
    value.forEach((v, i) => shape(v, spec[0], `${where}[${i}]`, errors));
    return;
  }
  if (typeOf(value) !== "object") { errors.push(`${where}: expected object, got ${typeOf(value)}`); return; }
  for (const [k, s] of Object.entries(spec)) {
    const optional = k.endsWith("?");
    const key = optional ? k.slice(0, -1) : k;
    if (!(key in value)) { if (!optional) errors.push(`${where}: missing "${key}"`); continue; }
    shape(value[key], s, `${where}.${key}`, errors);
  }
}
const check = [];
const ok = (name, cond, detail = "") => check.push({ name, ok: Boolean(cond), detail: cond ? "" : String(detail) });
const shapeOk = (name, value, spec) => { const e = []; shape(value, spec, name, e); ok(`${name} has the spec's shape`, e.length === 0, e.slice(0, 4).join("; ")); return e.length === 0; };

// --- DSN-STYLE-001 ---
const words = (s) => s.split(/\s+/).filter(Boolean).length;
export function styleCheck(specText, pack) {
  const findings = [];
  const lines = specText.split("\n");
  const heads = []; let cur = null;
  for (const line of lines) {
    const m = line.match(/^## (.+?)\s*$/);
    if (m) { cur = { heading: m[1], body: [] }; heads.push(cur); } else if (cur) cur.body.push(line);
  }
  const want = pack.sections;
  const known = new Set(want.map((s) => s.heading));
  for (const h of heads) if (!known.has(h.heading)) findings.push(`unknown section "${h.heading}"`);
  let last = -1;
  for (const s of want) {
    const i = heads.findIndex((h) => h.heading === s.heading);
    if (i < 0) { if (s.required) findings.push(`missing required section "${s.heading}"`); continue; }
    if (i < last) findings.push(`section "${s.heading}" is out of order`);
    last = i;
    const n = words(heads[i].body.join(" "));
    if (s.maxWords !== undefined && n > s.maxWords) findings.push(`section "${s.heading}" has ${n} words, over ${s.maxWords}`);
  }
  for (const f of pack.rules.forbidden) {
    const re = new RegExp(`(^|[^\\p{L}\\p{N}'])${f.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}($|[^\\p{L}\\p{N}'])`, "iu");
    if (re.test(specText)) findings.push(`forbidden phrase "${f}"`);
  }
  if (pack.rules.maxLines !== undefined && lines.length > pack.rules.maxLines) findings.push(`${lines.length} lines, over ${pack.rules.maxLines}`);
  return findings;
}

// --- the overlap check of spec 9.3 ---
const norm = (s) => s.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, " ").trim().split(" ").filter(Boolean);
const grams = (ws, n) => { const out = new Set(); for (let i = 0; i + n <= ws.length; i++) out.add(ws.slice(i, i + n).join(" ")); return out; };
function scenarioGrams(set) {
  const out = new Set();
  for (const s of set.scenarios) for (const t of [...(s.steps ?? []), s.expected ?? ""]) for (const g of grams(norm(t), 8)) out.add(g);
  return out;
}
const overlaps = (text, gs) => [...grams(norm(text), 8)].filter((g) => gs.has(g));

// --- allowedFiles ---
const globRe = (g) => new RegExp("^" + g.split("**").map((p) => p.replace(/[.+^${}()|[\]\\]/g, "\\$&").replace(/\*/g, "[^/]*").replace(/\?/g, "[^/]")).join(".*") + "$");
function allowedOf(task) {
  const scope = task.writeScope.map((s) => s.replace(/^new:\s*/, ""));
  const testChange = task.rules.filter((r) => r.startsWith("test-change:")).map((r) => r.split(":")[1]);
  return { scope, testChange };
}
export function outsideAllowed(task, changed) {
  const { scope, testChange } = allowedOf(task);
  const res = scope.map(globRe);
  return changed.filter(({ status, file }) => !(res.some((r) => r.test(file)) || testChange.includes(file) || (status === "A" && /^tests\//.test(file))));
}

const SECRET_PATTERNS = [/A(KIA|SIA)[0-9A-Z]{16}/, /gh[posur]_[A-Za-z0-9_]{36,}/, /github_pat_[A-Za-z0-9_]{22,}/, /[Bb]earer\s+[A-Za-z0-9._/+=-]{20,}/, /-----BEGIN [A-Z ]*PRIVATE KEY-----/, /xox[baprs]-[A-Za-z0-9-]{10,}/, /sk-[A-Za-z0-9]{32,}/];
const EVIDENCE_KINDS = ["test", "ui-browser", "api-transcript", "cli-transcript", "ios-sim", "migration", "a11y", "perf", "behavior-sim", "design-parity", "db-rows"];
const REAL_APP = ["ui-browser", "api-transcript", "cli-transcript", "ios-sim"];
const UNMEASURABLE = ["fast", "quick", "clean", "intuitive", "easy", "seamless", "robust", "user-friendly", "smooth"];
const HOPS = ["state", "dependency", "flow", "component"];
const SIZE_LIMITS = { XS: { files: 3, items: 2 }, S: { files: 8, items: 4 }, M: { files: 20, items: 8 } };
const SCOPE_FILES = ["scope-map.json", "acceptance.json", "coverage.json", "lenses.json", "size.json", "questions.json", "answers.jsonl", "challenge.json", "relevance.json", "gap-search.json", "graph.json", "rounds.jsonl", "runs.json", "sources.json", "brief.md"];
const DESIGN_FILES = ["packet.json", "spec.md", "sections.json", "coverage.json", "decisions.json", "acceptance.json", "signals.json", "contracts.json", "lens-rounds.jsonl", "style.json", "escapes.json", "runs.json", "index.json"];
const PLAN_FILES = ["packet.json", "tasks.json", "graph.json", "exec-review.json", "holdout.json", "answers.jsonl", "index.json"];

function parseMapEdges() {
  const lines = readText(path.join(SCOPING, "map.yaml")).split("\n");
  const out = [];
  for (let i = lines.indexOf("edges:") + 1; i < lines.length; i++) {
    let m = lines[i].match(/^  - \{from: "([^"]+)", to: "([^"]+)", type: ([\w-]+)\}/);
    if (m) { out.push({ from: m[1], to: m[2], type: m[3] }); continue; }
    m = lines[i].match(/^  - from: (\S+)/);
    if (m) { const e = { from: m[1], to: "", type: "" }; for (let j = i + 1; j < lines.length && /^    /.test(lines[j]); j++) { const t = lines[j].match(/^    (to|type): (\S+)/); if (t) e[t[1]] = t[2]; } out.push(e); }
  }
  return out;
}

function run() {
  const allFiles = walk(HERE);
  // ---------------- extends the scoping fixture ----------------
  const scopingHead = readText(path.join(SCOPING, "history", "golden-head.txt")).split("\n")[0];
  const goldenLines = readText(at("history", "golden-head.txt")).split("\n").filter(Boolean);
  ok("history/golden-head.txt pins main (the scoping head) and the four P1 tags", goldenLines.length === 5 && goldenLines[0] === scopingHead && goldenLines.every((l) => /^[0-9a-f]{40}$/.test(l)), goldenLines.join(","));
  const mapKeys = new Set([...readText(path.join(SCOPING, "map.yaml")).matchAll(/^\s*- key: (\S+)\s*$/gm)].map((m) => m[1]));
  const repoHas = (p) => fs.existsSync(path.join(SCOPING, "repo", p));
  const scopingGold = { expiry: readJson(path.join(SCOPING, "gold", "expiry.json")), archive: readJson(path.join(SCOPING, "gold", "archive.json")) };

  // ---------------- freeze records and the packet chain ----------------
  const freeze = readJson(at("gold", "freeze.json"));
  shapeOk("gold/freeze.json", freeze, { schemaVersion: "integer", frozen: [{ packet: "string", kind: "string", hash: "string", by: "string", station: "string", at: "string" }] });
  const frozenBy = new Map(freeze.frozen.map((f) => [f.packet, f]));
  for (const f of freeze.frozen) ok(`${f.packet}: freeze record is owner-frozen, names a sha256 hash and a kind its packet directory agrees with`, f.by === "owner" && /^sha256:[0-9a-f]{64}$/.test(f.hash) && fs.existsSync(at("gold", f.packet)) && f.kind === f.packet.split("/")[0].replace("scope", "scoping"), `${f.packet} ${f.kind} ${f.hash}`);
  const idx = (p) => readJson(at("gold", p, "index.json"));
  const accFile = (p) => at("gold", p, "acceptance.json");

  // ---------------- scope packets ----------------
  const scopes = {};
  for (const c of ["expiry", "archive"]) {
    const dir = `scope/${c}`;
    const names = walk(at("gold", dir));
    ok(`${dir}: holds exactly the files of scoping spec 8.3`, JSON.stringify(names) === JSON.stringify([...SCOPE_FILES].sort()), names.join(","));
    const j = (n) => readJson(at("gold", dir, n));
    const sm = j("scope-map.json"), acc = j("acceptance.json"), cov = j("coverage.json"), lnRaw = j("lenses.json");
    const LENSES = ["code", "product", "related", "history"];
    const ln = { lenses: LENSES.map((l) => ({ lens: l, ...(lnRaw[l] ?? {}) })) };
    scopes[c] = { sm, acc, cov, ln };
    const g = scopingGold[c];
    const surf = { id: "string", key: "string", kind: "string", title: "string", detail: "string", citations: ["string"], trust: "string", lens: "string", graphStatus: "string" };
    shapeOk(`${dir}/scope-map.json`, sm, { schemaVersion: "integer", change: "string", baseSha: "string", surfaces: [surf], implications: [{ id: "string", text: "string", citations: ["string"], surfaces: ["string"] }], workstreams: [{ id: "string", title: "string", surfaces: ["string"], deps: ["string"] }], flagged: [{ item: "string", reason: "string" }], excluded: [{ item: "string", reason: "string" }], unmapped: [{ path: "string", reason: "string", trust: "string" }], unresolved: [{}] });
    ok(`${dir}: baseSha is the scoping fixture's head`, sm.baseSha === scopingHead);
    ok(`${dir}: every surface is a map node, or a new one the gold names; ids are S1..Sn`, sm.surfaces.every((s, i) => s.id === `S${i + 1}` && (mapKeys.has(s.key) ? s.graphStatus === "mapped" : s.graphStatus === "new" && g.newSurfaces.some((n) => n.key === s.key) || s.key.startsWith("code:") && repoHas(s.key.slice(5)))), sm.surfaces.filter((s) => !mapKeys.has(s.key)).map((s) => s.key).join(","));
    ok(`${dir}: excluded equals the scoping gold's decoys; unmapped its unmapped paths`, JSON.stringify(sm.excluded.map((e) => e.item)) === JSON.stringify(g.decoys.map((d) => d.key)) && JSON.stringify(sm.unmapped.map((u) => u.path)) === JSON.stringify(g.unmapped.map((u) => u.path)));
    ok(`${dir}: no decoy is a surface`, !sm.surfaces.some((s) => g.decoys.some((d) => d.key === s.key)));
    shapeOk(`${dir}/acceptance.json`, acc, { schemaVersion: "integer", parent: "null", items: [{ id: "string", text: "string", form: "string", surfaces: ["string"], citations: ["string"], trust: "string", status: "string", evidence: [], parent: "null" }] });
    ok(`${dir}: acceptance equals the scoping gold's ids and texts, all failing with no evidence`, JSON.stringify(acc.items.map((a) => [a.id, a.text])) === JSON.stringify(g.acceptance.map((a) => [a.id, a.text])) && acc.items.every((a) => a.status === "failing" && a.evidence.length === 0), "differs");
    ok(`${dir}: every acceptance surface is a scope surface`, acc.items.every((a) => a.surfaces.every((id) => sm.surfaces.some((s) => s.id === id))));
    // coverage and lenses (SCP-LENS-001)
    shapeOk(`${dir}/coverage.json`, cov, { schemaVersion: "integer", lenses: [{ lens: "string", state: "string", searched: { anchors: ["string"], sources: [{ id: "string", state: "string", "reason?": "string" }], mapVersion: "string", depth: "integer", hops: ["string"] }, limits: [{}], mapCoverage: {}, found: { count: "integer", ids: ["string"] } }] });
    const lensShape = { anchors: ["string"], depth: "integer", hops: ["string"], mapVersion: "string", foundCount: "integer", nodes: [{ key: "string", kind: "string", label: "string", band: "string", lensItem: "string", trust: "string" }], edges: [{ from: "string", to: "string", type: "string", provenance: "string", confirmed: "boolean", trust: "string" }], items: [{ lensItem: "string", kind: "string", label: "string", citations: ["string"], trust: "string" }], truncated: ["string"] };
    shapeOk(`${dir}/lenses.json`, lnRaw, { schemaVersion: "integer", code: lensShape, product: lensShape, related: lensShape, history: lensShape });
    ok(`${dir}/lenses.json: a closed file, with the four lens keys and each lens's nodes in exactly the shape {key, kind, label, band, lensItem, trust} (schemas/packets/lenses.schema.json of LN-PKT1)`, JSON.stringify(Object.keys(lnRaw).sort()) === JSON.stringify(["code", "history", "product", "related", "schemaVersion"]) && LENSES.every((l) => lnRaw[l].nodes.every((n) => JSON.stringify(Object.keys(n).sort()) === JSON.stringify(["band", "key", "kind", "label", "lensItem", "trust"])) && JSON.stringify(Object.keys(lnRaw[l]).sort()) === JSON.stringify(["anchors", "depth", "edges", "foundCount", "hops", "items", "mapVersion", "nodes", "truncated"])));
    ok(`${dir}/lenses.json: one entry per lens, code product related history`, JSON.stringify(ln.lenses.map((l) => l.lens)) === JSON.stringify(["code", "product", "related", "history"]) && JSON.stringify(cov.lenses.map((l) => l.lens)) === JSON.stringify(["code", "product", "related", "history"]));
    const lensErr = [];
    for (const l of ln.lenses) {
      const row = cov.lenses.find((r) => r.lens === l.lens);
      if (!Array.isArray(l.hops)) { lensErr.push(`${l.lens}: hops must be an array of hop kinds, not ${typeof l.hops}`); continue; }
      if (JSON.stringify(row.searched.anchors) !== JSON.stringify(l.anchors) || row.searched.depth !== l.depth || row.searched.mapVersion !== l.mapVersion || JSON.stringify(row.searched.hops) !== JSON.stringify(l.hops)) lensErr.push(`${l.lens}: searched != lenses`);
      if (!l.hops.every((h) => HOPS.includes(h))) lensErr.push(`${l.lens}: hops must be hop kinds ${HOPS.join("|")}`);
      if ((l.depth === 0) !== (l.hops.length === 0)) lensErr.push(`${l.lens}: hops is [] if and only if depth is 0`);
      const ids = [...l.nodes.map((n) => n.lensItem), ...l.items.map((i) => i.lensItem)];
      if (JSON.stringify([...ids].sort()) !== JSON.stringify([...row.found.ids].sort()) || row.found.count !== ids.length || l.foundCount !== ids.length) lensErr.push(`${l.lens}: found ids differ`);
      if (!ids.every((id) => id.startsWith(`L-${l.lens}-`))) lensErr.push(`${l.lens}: an id is not L-${l.lens}-n`);
      const ends = new Set([...l.nodes.map((n) => n.key), ...l.anchors]);
      if (!l.edges.every((e) => ends.has(e.from) && ends.has(e.to))) lensErr.push(`${l.lens}: an edge end is not found or an anchor`);
      if (![...l.nodes, ...l.edges, ...l.items].every((x) => x.trust === "untrusted")) lensErr.push(`${l.lens}: trust must be untrusted`);
      if (!l.nodes.every((n) => mapKeys.has(n.key) || (n.key.startsWith("code:") && repoHas(n.key.slice(5))))) lensErr.push(`${l.lens}: a node is not in the map`);
      if (l.hops.length && new Set(l.hops).size !== l.hops.length) lensErr.push(`${l.lens}: a hop kind repeats`);
      if (row.state === "found" && ids.length === 0) lensErr.push(`${l.lens}: state found with no ids`);
    }
    ok(`${dir}: lenses.json agrees with coverage.json (SCP-LENS-001); hops are hop-kind arrays`, lensErr.length === 0, lensErr.join("; "));
    ok(`${dir}: a lens that found nothing says so (nothing-found)`, cov.lenses.every((r) => (r.found.count === 0) === (r.state === "nothing-found")));
    const rel = ln.lenses.find((l) => l.lens === "related");
    const everyKey = new Set(ln.lenses.flatMap((l) => l.nodes.map((n) => n.key)));
    ok(`${dir}: the lens results hold every scoping must-find graph key, and the related lens adds what the cascade reaches`, g.mustFind.filter((m) => !m.key.startsWith("linear:") && !m.key.startsWith("commit:")).every((m) => everyKey.has(m.key)) && ln.lenses.reduce((n, l) => n + l.foundCount, 0) === g.mustFind.length + rel.nodes.length && rel.nodes.length >= 1, `${rel.nodes.length}`);
    // what Cascade.Affects returns (Cascade.cs Steps and Ascend): a reached surface or action is reported as its owning feature
    const mapEdges = parseMapEdges();
    const inE = (n, t) => mapEdges.filter((e) => e.to === n && e.type === t), outE = (n, t) => mapEdges.filter((e) => e.from === n && e.type === t);
    const ascendOf = (n) => { const sf = n.startsWith("action:") ? outE(n, "on")[0]?.to : n; if (!sf || !sf.startsWith("surface:")) return n; return outE(sf, "part-of").map((e) => e.to).find((x) => x.startsWith("feature:")) ?? n; };
    const relErr = [];
    const trace = readJson(at("gold", "scope-trace", `${c}.related-trace.json`));
    if (JSON.stringify(Object.keys(trace).sort()) !== JSON.stringify(rel.nodes.map((n) => n.key).sort())) relErr.push("related-trace.json keys differ from the related nodes");
    for (const n of rel.nodes) {
      const r = trace[n.key], p = r?.path ?? [];
      const reads = p.filter((x) => x.startsWith("reads ")).map((x) => x.slice(6)), writes = p.filter((x) => x.startsWith("writes ")).map((x) => x.slice(7));
      if (!mapKeys.has(n.key)) relErr.push(`${n.key}: not a map key`);
      if (n.band !== "product" || n.trust !== "untrusted") relErr.push(`${n.key}: band or trust`);
      if (!r || !(r.depth >= 1 && r.depth <= rel.depth)) relErr.push(`${n.key}: reach.depth`);
      if (rel.anchors.includes(n.key)) relErr.push(`${n.key}: is an anchor, not a reached node`);
      if (!rel.anchors.includes(p[0])) relErr.push(`${n.key}: path does not start at an anchor`);
      if (writes.length !== reads.length || writes.length !== r?.depth) relErr.push(`${n.key}: a state hop is one writes and one reads; depth ${r?.depth}`);
      const last = reads.at(-1);
      if (last === undefined || ascendOf(last) !== n.key) relErr.push(`${n.key}: the last reader ${last} does not ascend to it`);
      for (const w of writes) if (!mapEdges.some((e) => e.type === "writes" && e.to === w)) relErr.push(`${n.key}: no writes edge to ${w}`);
      if (last && writes.length && !inE(writes.at(-1), "reads").some((e) => e.from === last)) relErr.push(`${n.key}: ${last} does not read ${writes.at(-1)}`);
    }
    const structure = ["part-of", "on", "reads", "writes", "step-of", "depends-on", "moves-to"];
    ok(`${dir}: related is a cascade lens (depth 2, all four hops, component last); every node is a map key the cascade reaches from an anchor by a real state hop, with its depth and path; edges are structure edges among the reached nodes and the anchors`, rel.depth === 2 && JSON.stringify(rel.hops) === JSON.stringify(["state", "dependency", "flow", "component"]) && relErr.length === 0 && rel.edges.every((e) => structure.includes(e.type)), relErr.join("; "));
    ok(`${dir}: coverage.json found ids are sorted numerically within each lens`, cov.lenses.every((r) => r.found.ids.every((id, i, a) => i === 0 || Number(a[i - 1].split("-").pop()) < Number(id.split("-").pop()))));
    const rounds = readJsonl(at("gold", dir, "rounds.jsonl"));
    ok(`${dir}/rounds.jsonl: a first round and one that adds the challenger's finding${c === "expiry" ? ", then the rescope" : ""}`, rounds[0].trigger === "first" && rounds[1].trigger === "found" && (c === "expiry" ? rounds[2]?.trigger === "rescope" : rounds.length === 2));
    const answersLog = readJsonl(at("gold", dir, "answers.jsonl"));
    ok(`${dir}/answers.jsonl: one batch per scripted batch of the scoping answers`, answersLog.length === readJson(path.join(SCOPING, "answers", `${c}.json`)).batches.length);
    const challenge = j("challenge.json");
    ok(`${dir}/challenge.json: the last round is empty`, challenge.rounds.length >= 2 && challenge.rounds.at(-1).verdict === "nothingNew" && !challenge.rounds.at(-1).surfaces && !challenge.rounds.at(-1).behaviours);
    ok(`${dir}: has no index.json (the scoping kind lists fourteen files; its hash is the frozen record)`, !fs.existsSync(at("gold", dir, "index.json")));
    ok(`${dir}/questions.json: every question answered, one recommended option each`, (() => { const asked = new Set(readJsonl(at("gold", dir, "answers.jsonl")).flatMap((b) => b.questionIds)); return j("questions.json").questions.every((q) => asked.has(q.id) && q.options.filter((o) => o.recommended).length === 1); })());
  }

  // ---------------- design packet ----------------
  const D = "design/expiry";
  const dnames = walk(at("gold", D));
  ok(`${D}: holds exactly the files of line spec 3.2 row 2`, JSON.stringify(dnames) === JSON.stringify([...DESIGN_FILES].sort()), dnames.join(","));
  const dj = (n) => readJson(at("gold", D, n));
  const spec = readText(at("gold", D, "spec.md"));
  const sections = dj("sections.json"), dcov = dj("coverage.json"), decisions = dj("decisions.json"), dacc = dj("acceptance.json"), signals = dj("signals.json"), contracts = dj("contracts.json"), style = dj("style.json"), escapes = dj("escapes.json"), druns = dj("runs.json");
  const lensRounds = readJsonl(at("gold", D, "lens-rounds.jsonl"));
  shapeOk(`${D}/sections.json`, sections, { schemaVersion: "integer", sections: [{ id: "string", parent: "string|null", heading: "string", covers: ["string"], cites: ["string"], assumes: ["string"] }] });
  shapeOk(`${D}/coverage.json`, dcov, { schemaVersion: "integer", surfaces: [{ key: "string", id: "string", sections: ["string"] }], excluded: [{ key: "string", id: "string", reason: "string" }], unmapped: [{ path: "string", sections: ["string"] }] });
  shapeOk(`${D}/decisions.json`, decisions, { schemaVersion: "integer", decisions: [{ id: "string", question: "string", affects: ["string"], cites: ["string"], options: [{ id: "string", answer: "string", tradeoffs: "string", "recommended?": "boolean" }], status: "string", chosen: "string" }] });
  shapeOk(`${D}/acceptance.json`, dacc, { schemaVersion: "integer", parent: "string", items: [{ id: "string", text: "string", status: "string", evidence: [], trust: "string" }] });
  shapeOk(`${D}/contracts.json`, contracts, { schemaVersion: "integer", baseSha: "string", edits: [{ id: "string", lane: "string", target: { file: "string", heading: "string" }, quote: "string", change: "string", row: "integer" }] });
  shapeOk(`${D}/style.json`, style, { schemaVersion: "integer", style: { id: "string", version: "string", hash: "string" }, lenses: { id: "string", version: "string", hash: "string" } });
  shapeOk(`${D}/escapes.json`, escapes, { schemaVersion: "integer", escapes: [{ from: "string", station: "string", kind: "string", id: "string", why: "string" }] });
  shapeOk(`${D}/runs.json`, druns, { schemaVersion: "integer", runs: [{ step: "string", via: "string", seedHash: "string", model: "string", vendor: "string", effort: "string", tokens: "integer" }] });
  const si = idx(D);
  const IDX = { schemaVersion: "integer", kind: "string", parents: ["string"], acceptance: { self: "string|null", extends: "string|null" } };
  shapeOk(`${D}/index.json`, si, IDX);
  const dpk = readJson(at("gold", D, "packet.json"));
  shapeOk(`${D}/packet.json`, dpk, { schemaVersion: "integer", kind: "string", id: "string", repo: "string", baseSha: "string", status: "string", hash: "null", links: Object.fromEntries(["spec", "sections", "coverage", "decisions", "acceptance", "signals", "contracts", "lensRounds", "style", "escapes", "runs", "index"].map((k) => [k, "string"])) });
  ok(`${D}/packet.json: kind design, baseSha is the scoping head, links name each of the packet's files`, dpk.kind === "design" && dpk.baseSha === scopingHead && Object.values(dpk.links).sort().join() === DESIGN_FILES.filter((f) => f !== "packet.json").sort().join());
  ok(`${D}/index.json: kind design, parent is the frozen expiry scope packet, chain extends its acceptance`, si.kind === "design" && JSON.stringify(si.parents) === JSON.stringify([frozenBy.get("scope/expiry").hash]) && si.acceptance.extends === fileHash(accFile("scope/expiry")) && si.acceptance.self === fileHash(accFile(D)) && dacc.parent === fileHash(accFile("scope/expiry")));
  const surfaceByKey = new Map(scopes.expiry.sm.surfaces.map((s) => [s.key, s]));
  const surfaceById = new Map(scopes.expiry.sm.surfaces.map((s) => [s.id, s]));
  // DSN-COV-001
  const covered = new Set(dcov.surfaces.map((s) => s.key)), excluded = new Set(dcov.excluded.map((s) => s.key));
  ok("DSN-COV-001: every scope surface is covered by a section or excluded with a reason", scopes.expiry.sm.surfaces.every((s) => covered.has(s.key) !== excluded.has(s.key) || (covered.has(s.key) && excluded.has(s.key) && false)) && dcov.excluded.every((e) => e.reason.length > 10), scopes.expiry.sm.surfaces.filter((s) => !covered.has(s.key) && !excluded.has(s.key)).map((s) => s.key).join(","));
  const secById = new Map(sections.sections.map((s) => [s.id, s]));
  ok("DSN-COV-001: coverage.json agrees with each section's covers", dcov.surfaces.every((c) => c.sections.length > 0 && c.sections.every((sid) => secById.get(sid)?.covers.includes(c.id))) && sections.sections.every((s) => s.covers.every((id) => surfaceById.has(id))));
  ok("DSN-COV-002: every scope question was answered at the scope stage, so none is open", (() => { const asked = new Set(readJsonl(at("gold", "scope/expiry", "answers.jsonl")).flatMap((b) => b.questionIds)); return readJson(at("gold", "scope/expiry", "questions.json")).questions.every((q) => asked.has(q.id)); })());
  // spec.md vs sections.json
  const headings = [...spec.matchAll(/^(#{2,3}) (.+?)\s*$/gm)].map((m) => ({ level: m[1].length, text: m[2] }));
  ok("spec.md headings are exactly sections.json's, in order", JSON.stringify(headings.map((h) => h.text)) === JSON.stringify(sections.sections.map((s) => s.heading)), headings.map((h) => h.text).join("|"));
  ok("sections.json parents agree with heading levels", sections.sections.every((s, i) => (headings[i].level === 2) === (s.parent === null) && (s.parent === null || secById.get(s.parent)?.heading === [...headings.slice(0, i)].reverse().find((h) => h.level === 2)?.text)));
  // ACC-001/002
  const live = dacc.items.filter((a) => a.status !== "superseded");
  const sigErr = [];
  for (const a of live) {
    const ss = signals.signals[a.id];
    if (!ss || ss.length === 0) { sigErr.push(`${a.id}: no signal`); continue; }
    for (const s of ss) {
      if (!EVIDENCE_KINDS.includes(s.kind)) sigErr.push(`${s.id}: kind ${s.kind}`);
      if (!["head", "base+head"].includes(s.runsAt)) sigErr.push(`${s.id}: runsAt`);
      if (!/\d/.test(s.statement) && !/'[^']+'/.test(s.statement)) sigErr.push(`${s.id}: no number or quoted reference`);
      for (const t of UNMEASURABLE) if (new RegExp(`\\b${t}\\b`, "i").test(s.statement)) sigErr.push(`${s.id}: unmeasurable term "${t}"`);
    }
  }
  ok("DSN-ACC-001/002: every live item has a measurable signal of a known evidence kind", sigErr.length === 0, sigErr.join("; "));
  // ACC-003 / LC-ACC-001
  const scopeAcc = new Map(scopes.expiry.acc.items.map((a) => [a.id, a]));
  const addedItems = dacc.items.filter((a) => !scopeAcc.has(a.id));
  ok("DSN-ACC-003 (LC-ACC-001): scope ids and texts unchanged; each added item cites a section or decision", [...scopeAcc.values()].every((a) => dacc.items.find((x) => x.id === a.id)?.text === a.text) && addedItems.length === 1 && addedItems[0].id === "AC-11" && addedItems[0].citations.every((c) => secById.has(c) || decisions.decisions.some((d) => d.id === c)));
  ok("DSN-ACC: every item stays failing with no evidence (only the proof pack flips it)", dacc.items.every((a) => a.status === "failing" && a.evidence.length === 0));
  // DEC-001 and the D3 shape
  const decErr = [];
  for (const d of decisions.decisions) {
    if (d.options.length < 2 || d.options.length > 3) decErr.push(`${d.id}: options`);
    if (d.options.filter((o) => o.recommended).length !== 1) decErr.push(`${d.id}: recommended`);
    if (d.affects.length === 0) decErr.push(`${d.id}: affects`);
    if (!d.options.every((o) => o.id.startsWith(`${d.id}.`) && o.tradeoffs)) decErr.push(`${d.id}: option ids or trade-offs`);
    const rec = d.options.find((o) => o.recommended)?.id;
    for (const sid of d.affects) {
      const s = secById.get(sid);
      if (!s || !s.assumes.includes(rec)) decErr.push(`${d.id}: ${sid} lacks assumes ${rec} in sections.json`);
      const body = spec.split(/^#{2,3} /m).find((b) => b.startsWith(s?.heading ?? "\0"));
      if (!body || !body.includes(`assumes: ${rec}`)) decErr.push(`${d.id}: ${sid} lacks "assumes: ${rec}" in spec.md`);
    }
    if (!d.cites.every((c) => surfaceById.has(c) || scopeAcc.has(c))) decErr.push(`${d.id}: a cite does not resolve`);
  }
  ok("DSN-DEC-001: every decision has 2-3 options, one recommended, affects, and its sections assume the recommendation", decErr.length === 0, decErr.join("; "));
  const d3 = decisions.decisions.find((d) => d.id === "D3");
  ok("the design has three decisions, one of them D3 (where link expiry runs), with the chosen options recorded from the plan's answers", decisions.decisions.length === 3 && decisions.decisions.every((d) => d.status === "answered" && d.options.some((o) => o.id === d.chosen)) && readJsonl(at("gold", "plan", "expiry", "answers.jsonl")).at(-1).answers.every((a) => decisions.decisions.find((d) => d.id === a.decision)?.chosen === a.pick) && d3 && /where does link expiry run/i.test(d3.question) && d3.options.find((o) => o.recommended).id === "D3.a" && d3.options.some((o) => o.id === "D3.b" && /nightly/i.test(o.answer)));
  // CITE-001
  ok("DSN-CITE-001: every section cites scope ids or a decision", sections.sections.every((s) => s.cites.length > 0 && s.cites.every((c) => scopeAcc.has(c) || surfaceById.has(c) || decisions.decisions.some((d) => d.id === c) || /^S\d+$/.test(c))));
  const cited = [...spec.matchAll(/\bS(\d+)\b/g)].map((m) => `S${m[1]}`);
  ok("spec.md cites only scope surface ids that exist", cited.every((s) => surfaceById.has(s)), cited.filter((s) => !surfaceById.has(s)).join(","));
  // XSPEC
  const xErr = [];
  const lanes = new Set();
  const rowsBody = spec.split(/^## /m).find((b) => b.startsWith("Rows to other lanes")) ?? "";
  for (const e of contracts.edits) {
    const f = path.join(SCOPING, "repo", e.target.file);
    if (!fs.existsSync(f)) { xErr.push(`${e.id}: no file`); continue; }
    const t = readText(f);
    if (!new RegExp(`^#+ ${e.target.heading.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\s*$`, "m").test(t)) xErr.push(`${e.id}: no heading`);
    if (!t.includes(e.quote)) xErr.push(`${e.id}: quote not in target`);
    if (lanes.has(e.lane)) xErr.push(`${e.id}: lane twice`);
    lanes.add(e.lane);
    if (!new RegExp(`^${e.row}\\. .*\\(${e.id}\\)`, "m").test(rowsBody)) xErr.push(`${e.id}: no numbered row ${e.row}`);
  }
  ok("DSN-XSPEC-001..003: each edit's file, heading and quoted text exist at the base; one numbered row each, one lane per row", xErr.length === 0 && contracts.baseSha === scopingHead, xErr.join("; "));
  // STYLE-001 both ways
  const plain = readJson(at("packs", "style", "plain", "pack.json")), formal = readJson(at("packs", "style", "formal", "pack.json"));
  const formalSpec = readText(at("gold", "design", "expiry.formal.spec.md"));
  const sf = (s, p) => styleCheck(s, p);
  ok("DSN-STYLE-001: the gold spec passes the plain pack", sf(spec, plain).length === 0, sf(spec, plain).join("; "));
  ok("DSN-STYLE-001: the gold spec fails the formal pack", sf(spec, formal).length > 0);
  ok("DSN-STYLE-001: the formal rendering passes the formal pack", sf(formalSpec, formal).length === 0, sf(formalSpec, formal).join("; "));
  ok("DSN-STYLE-001: the formal rendering fails the plain pack", sf(formalSpec, plain).length > 0);
  ok("DSN-STYLE-001: a missing required section, a forbidden phrase and an over-long section each fail the plain pack", sf(spec.replace(/^## Data$/m, "## Storage"), plain).some((f) => /missing required section "Data"/.test(f)) && sf(spec.replace("works as it does today", "works as it does today, in order to stay safe"), plain).some((f) => /in order to/.test(f)) && sf(spec.replace("## Data\n", "## Data\n" + "word ".repeat(200) + "\n"), plain).some((f) => /over 160/.test(f)));
  ok("DSN-STYLE-001: a forbidden phrase and a swapped order fail the formal pack", sf(formalSpec.replace("The create route", "We make the create route"), formal).some((f) => /"we"/.test(f)) && sf(formalSpec.replace("## Scope", "## Zzz").replace("## Purpose", "## Scope").replace("## Zzz", "## Purpose"), formal).some((f) => /out of order|unknown|missing/.test(f)));
  // LENS-001..003, ESC-001
  const last = Math.max(...lensRounds.map((r) => r.round));
  const lastF = lensRounds.filter((r) => r.round === last).flatMap((r) => r.findings);
  ok("DSN-LENS-001: the last round has no CRITICAL or HIGH, and every round ran every lens of the pack", lastF.every((f) => !["CRITICAL", "HIGH"].includes(f.severity)) && [...new Set(lensRounds.map((r) => r.round))].every((n) => lensRounds.filter((r) => r.round === n).length === 5));
  ok("DSN-LENS-002: the lens model and vendor differ from the writer's", (() => { const wri = druns.runs.find((r) => r.step === "step.design.draft"), le = druns.runs.find((r) => r.step === "step.lens.run"); return wri && le && wri.model !== le.model && wri.vendor !== le.vendor; })());
  ok("DSN-LENS-003: every MEDIUM and LOW of every round has a written resolution; every HIGH has one too", lensRounds.flatMap((r) => r.findings).every((f) => f.resolution && f.resolution.length > 10 && secById.has(f.cite)));
  const fiveDir = at("packs", "lenses", "design-five"), twoDir = at("packs", "lenses", "design-two");
  ok("style.json pins the plain and design-five packs by the packs' hashes", style.style.id === "plain" && style.style.hash === dirHash(at("packs", "style", "plain")) && style.lenses.id === "design-five" && style.lenses.hash === dirHash(fiveDir) && lensRounds.every((r) => r.packHash === style.lenses.hash));
  ok("DSN-ESC-001: items and surfaces the scope packet lacks are listed as escapes", addedItems.every((a) => escapes.escapes.some((e) => e.kind === "acceptance" && e.id === a.id)) && escapes.escapes.filter((e) => e.kind === "surface").every((e) => !surfaceByKey.has(e.id) && scopes.expiry.sm.excluded.some((x) => x.item === e.id)));

  // ---------------- lens and style packs ----------------
  const packs = {};
  for (const [kind, id] of [["style", "plain"], ["style", "formal"], ["lenses", "design-five"], ["lenses", "design-two"]]) {
    const dir = at("packs", kind, id);
    const pj = readJson(path.join(dir, "pack.json"));
    packs[id] = pj;
    if (kind === "style") {
      shapeOk(`packs/style/${id}/pack.json`, pj, { kind: "string", id: "string", version: "string", title: "string", sections: [{ id: "string", heading: "string", required: "boolean", guidance: "string", "maxWords?": "integer" }], rules: { forbidden: ["string"], "maxLines?": "integer", tables: "string", voice: "string" }, template: "string", examples: ["string"] });
      const tHeads = [...readText(path.join(dir, pj.template)).matchAll(/^## (.+?)\s*$/gm)].map((m) => m[1]);
      ok(`pack ${id}: template headings are the pack's sections, in order`, JSON.stringify(tHeads) === JSON.stringify(pj.sections.map((s) => s.heading)), tHeads.join("|"));
      ok(`pack ${id}: 2-4 examples, one of them a hard case (spec 5.1)`, pj.examples.length >= 2 && pj.examples.length <= 4 && pj.examples.some((e) => /hard/.test(e)) && walk(path.join(dir, "examples")).length === pj.examples.length);
      ok(`pack ${id}: voice and example files exist`, fs.existsSync(path.join(dir, pj.rules.voice)) && pj.examples.every((e) => fs.existsSync(path.join(dir, e))));
    } else {
      shapeOk(`packs/lenses/${id}/pack.json`, pj, { kind: "string", id: "string", version: "string", applies: "string", lenses: [{ id: "string", title: "string", prompt: "string", when: "string|object" }] });
      ok(`pack ${id}: at most 10 lenses; every prompt exists; no stray file`, pj.lenses.length <= 10 && pj.lenses.every((l) => fs.existsSync(path.join(dir, l.prompt))) && walk(dir).length === 1 + pj.lenses.length, walk(dir).join(","));
    }
    ok(`pack ${id}: pack.json says kind ${kind} and its own id`, pj.kind === kind && pj.id === id);
  }
  ok("lens packs: the five-lens pack is product, architecture, design, devex, security; the two-lens pack has two", JSON.stringify(packs["design-five"].lenses.map((l) => l.id)) === JSON.stringify(["product", "architecture", "design", "devex", "security"]) && packs["design-two"].lenses.length === 2);
  const heads = (p) => new Set(p.sections.map((s) => s.heading));
  ok("the two style packs really differ: no shared heading, different forbidden phrases, tables rule, section count and limits", [...heads(plain)].every((h) => !heads(formal).has(h)) && plain.rules.tables !== formal.rules.tables && JSON.stringify(plain.rules.forbidden) !== JSON.stringify(formal.rules.forbidden) && plain.sections.length !== formal.sections.length && plain.rules.maxLines !== formal.rules.maxLines && readText(at("packs", "style", "plain", "voice.md")) !== readText(at("packs", "style", "formal", "voice.md")) && !plain.rules.forbidden.some((f) => formal.rules.forbidden.includes(f)));
  const profile = readJson(at("profile-packs.json"));
  const pins = profile.workflow.packs.pins;
  ok("profile-packs.json: the defaults are plain and design-five; every pin equals its pack's hash", profile.workflow.packs.style.default === "plain" && profile.workflow.packs.lenses.design.default === "design-five" && Object.entries(pins).every(([k, h]) => h === dirHash(at("packs", ...k.split("/")))) && Object.keys(pins).length === 4);
  const defect = readJson(at("gold", "lens", "design-defect.json"));
  const specSec = (id) => { const s = secById.get(id); return (spec.split(/^## /m).find((b) => b.startsWith(s.heading)) ?? "").replace(/\s+/g, " "); };
  const lensPrompts = (dir) => walk(path.join(dir, "lenses")).map((f) => readText(path.join(dir, "lenses", f)).toLowerCase()).join("\n");
  ok("PL-LENS: the defect replaces real text of its section; only design-two's lens prompts name its triggers", specSec(defect.section).includes(defect.goodText) && !specSec(defect.section).includes(defect.defectiveText) && defect.triggers.every((t) => lensPrompts(twoDir).includes(t)) && defect.triggers.every((t) => !lensPrompts(fiveDir).includes(t)) && packs["design-two"].lenses.some((l) => l.id === defect.lens) && !packs["design-five"].lenses.some((l) => l.id === defect.lens) && defect.severity === "HIGH");

  // ---------------- plan packet ----------------
  const P = "plan/expiry";
  const pnames = walk(at("gold", P));
  ok(`${P}: holds exactly the files of line spec 3.2 row 3`, JSON.stringify(pnames) === JSON.stringify([...PLAN_FILES].sort()), pnames.join(","));
  const pk = readJson(at("gold", P, "packet.json")), plan = readJson(at("gold", P, "tasks.json")), graph = readJson(at("gold", P, "graph.json")), exec = readJson(at("gold", P, "exec-review.json")), hold = readJson(at("gold", P, "holdout.json"));
  const task = { id: "string", workstream: "string", title: "string", deps: ["string"], size: "string", writeScope: ["string"], readSeeds: ["string"], acceptance: [{ id: "string", "text?": "string", evidence: ["string"], holdout: "boolean" }], rules: ["string"], budget: { tokens: "integer", turns: "integer", wallMinutes: "integer" } };
  shapeOk(`${P}/packet.json`, pk, { schemaVersion: "integer", kind: "string", id: "string", repo: "string", baseSha: "string", status: "string", hash: "null", links: { tasks: "string", graph: "string", execReview: "string", holdout: "string", answers: "string", index: "string" } });
  shapeOk(`${P}/tasks.json`, plan, { schemaVersion: "integer", workstreams: [{ id: "string", title: "string" }], tasks: [task] });
  ok(`${P}/packet.json: kind plan, links name each of the packet's files`, pk.kind === "plan" && Object.values(pk.links).sort().join() === PLAN_FILES.filter((f) => f !== "packet.json").sort().join());
  const tasks = plan.tasks;
  const byId = new Map(tasks.map((t) => [t.id, t]));
  ok("PKT: baseSha is the scoping head; ids unique; workstreams declared; deps resolve", pk.baseSha === scopingHead && new Set(tasks.map((t) => t.id)).size === tasks.length && tasks.every((t) => plan.workstreams.some((w) => w.id === t.workstream) && t.deps.every((d) => byId.has(d))));
  ok("the plan has four phases P1..P4", JSON.stringify(tasks.map((t) => t.id)) === JSON.stringify(PHASES));
  const reach = (from) => { const seen = new Set(); const go = (id) => { for (const d of byId.get(id).deps) if (!seen.has(d)) { seen.add(d); go(d); } }; go(from); return seen; };
  ok("PKT-GRAPH-002: the task graph is acyclic", tasks.every((t) => !reach(t.id).has(t.id)));
  const dagOk = JSON.stringify(byId.get("P1").deps) === "[]" && JSON.stringify(byId.get("P2").deps) === '["P1"]' && JSON.stringify(byId.get("P3").deps) === '["P1"]' && JSON.stringify([...byId.get("P4").deps].sort()) === '["P2","P3"]';
  ok("the DAG: P1 alone; P2 stacked on P1; P3 beside P2 (also on P1, neither orders the other); P4 after both", dagOk && !reach("P2").has("P3") && !reach("P3").has("P2"));
  const globOverlap = (a, b) => globRe(a).test(b) || globRe(b).test(a) || a === b;
  const wsOf = (t) => allowedOf(t).scope.concat(allowedOf(t).testChange);
  const unordered = []; for (const a of tasks) for (const b of tasks) if (a.id < b.id && !reach(a.id).has(b.id) && !reach(b.id).has(a.id)) unordered.push([a, b]);
  ok("PKT-SCOPE-002: two phases no dependency orders (P2, P3) never write the same path", unordered.length === 1 && unordered[0].map((t) => t.id).join() === "P2,P3" && wsOf(unordered[0][0]).every((x) => wsOf(unordered[0][1]).every((y) => !globOverlap(x, y))));
  const scopeErr = [];
  for (const t of tasks) for (const s of t.writeScope) {
    if (s.startsWith("new: ")) { if (repoHas(s.slice(5))) scopeErr.push(`${t.id}: ${s} exists`); } else if (!repoHas(s)) scopeErr.push(`${t.id}: ${s} missing`);
    for (const r of allowedOf(t).testChange) if (!repoHas(r)) scopeErr.push(`${t.id}: test ${r} missing`);
  }
  ok("PKT-SCOPE-001: allowed files exist at the base, or are declared new and absent; test-change files exist", scopeErr.length === 0, scopeErr.join("; "));
  const sizeErr = [];
  for (const t of tasks) {
    const lim = SIZE_LIMITS[t.size]; const files = t.writeScope.length + allowedOf(t).testChange.length; const items = t.acceptance.filter((a) => !a.holdout).length;
    if (!lim) sizeErr.push(`${t.id}: size ${t.size}`); else if (files > lim.files || items > lim.items) sizeErr.push(`${t.id}: ${files} files and ${items} items over ${t.size}`);
  }
  ok("PLN-SIZE-001: every phase is XS, S or M and within its limits (3/2, 8/4, 20/8)", sizeErr.length === 0, sizeErr.join("; "));
  const owner = new Map();
  for (const t of tasks) for (const a of t.acceptance.filter((x) => !x.holdout)) owner.set(a.id, [...(owner.get(a.id) ?? []), t.id]);
  ok("PLN-ACC-001: every live design item is in exactly one phase, text unchanged", live.every((a) => (owner.get(a.id) ?? []).length === 1) && tasks.every((t) => t.acceptance.filter((a) => !a.holdout).every((a) => a.text === dacc.items.find((x) => x.id === a.id)?.text)) && owner.size === live.length);
  const sigE = [];
  for (const t of tasks) {
    const kinds = t.acceptance.filter((a) => !a.holdout).flatMap((a) => signals.signals[a.id].map((s) => s.kind));
    if (!kinds.some((k) => REAL_APP.includes(k))) sigE.push(`${t.id}: no real-app signal`);
    for (const a of t.acceptance.filter((x) => !x.holdout)) if (JSON.stringify([...new Set(signals.signals[a.id].map((s) => s.kind))].sort()) !== JSON.stringify([...a.evidence].sort())) sigE.push(`${t.id}/${a.id}: evidence differs from the design's signals`);
    if (!t.rules.some((r) => /^kind:(feature|fix|refactor|docs|config)$/.test(r))) sigE.push(`${t.id}: no kind`);
  }
  ok("PLN-SIG-001: every phase has a real-app signal; evidence kinds are the design's signals' kinds; each has a kind rule", sigE.length === 0, sigE.join("; "));
  // PLN-STACK-001 and graph.json derived from the deps
  const wave = (id) => 1 + Math.max(0, ...byId.get(id).deps.map(wave));
  const gerr = [];
  shapeOk(`${P}/graph.json`, graph, { schemaVersion: "integer", parallel: "integer", maxStack: "integer", phases: [{ id: "string", stack: "integer", wave: "integer", base: { rule: "string", ref: "string" }, waitsForMerge: "boolean" }], waves: [["string"]], criticalPath: ["string"], prCount: "integer" });
  for (const g of graph.phases) {
    const t = byId.get(g.id);
    if (g.wave !== wave(g.id)) gerr.push(`${g.id}: wave`);
    if (g.waitsForMerge !== (t.deps.length > 1)) gerr.push(`${g.id}: waitsForMerge`);
    if (t.deps.length === 0 ? g.base.rule !== "default-branch" : g.base.rule !== "dependency-head" || !t.deps.includes(g.base.ref)) gerr.push(`${g.id}: base`);
  }
  ok("PLN-STACK-001 and spec 8.3: waves and bases follow the deps; a phase on two unmerged deps waits for merge; the stack is at most 4 deep", gerr.length === 0 && graph.maxStack === 4 && graph.parallel === 2 && Math.max(...tasks.map((t) => wave(t.id))) <= graph.maxStack && JSON.stringify(graph.waves) === '[["P1"],["P2","P3"],["P4"]]' && graph.prCount === 4 && graph.waves.every((w) => w.length <= graph.parallel), gerr.join("; "));
  ok("graph.json: P2 is stacked on P1's head branch, P3 beside it on the same base, P4 waits for merge", graph.phases.find((g) => g.id === "P2").base.ref === "P1" && graph.phases.find((g) => g.id === "P3").base.ref === "P1" && graph.phases.find((g) => g.id === "P3").stack !== graph.phases.find((g) => g.id === "P2").stack && graph.phases.find((g) => g.id === "P4").waitsForMerge);
  shapeOk(`${P}/exec-review.json`, exec, { schemaVersion: "integer", model: "string", result: "string", phases: [{ id: "string", executable: "boolean", contextMissing: ["string"], allowedFilesEnough: "boolean", signalsCheckable: "boolean" }], objections: [] });
  ok("PLN-EXEC-001: the executability review reports executable for every phase, on the builder model", exec.result === "executable" && exec.phases.length === 4 && exec.phases.every((p) => p.executable && p.allowedFilesEnough && p.signalsCheckable) && exec.model === "fixture-builder-model");
  const pi = idx(P);
  shapeOk(`${P}/index.json`, pi, IDX);
  ok(`${P}/index.json: kind plan, parents design then scope, chain extends the design's acceptance`, pi.kind === "plan" && pi.acceptance.self === null && JSON.stringify(pi.parents) === JSON.stringify([frozenBy.get("design/expiry").hash, frozenBy.get("scope/expiry").hash]) && pi.acceptance.extends === fileHash(accFile(D)));

  // ---------------- hidden sets ----------------
  shapeOk(`${P}/holdout.json`, hold, { schemaVersion: "integer", minPerPhase: "integer", phases: [{ id: "string", setHash: "string", count: "integer", canaries: "integer", scenarioIds: ["string"] }] });
  const sets = {}; const phrases = new Map();
  for (const ph of PHASES) {
    const set = readJson(at("holdout", "expiry", `${ph}.set.json`)); sets[ph] = set;
    const seal = readJson(at("holdout", "expiry", `${ph}.seal.json`));
    shapeOk(`holdout/expiry/${ph}.set.json`, set, { schemaVersion: "integer", phase: "string", scenarios: [{ id: "string", item: "string|null", kind: "string", "phrase?": "string", steps: ["string"], expected: "string" }] });
    const canary = set.scenarios.filter((s) => s.kind === "canary");
    ok(`${ph}: at least two scenarios and exactly one canary carrying a phrase`, set.scenarios.length - 1 >= hold.minPerPhase && canary.length === 1 && /^[a-z]+(-[a-z0-9]+){3}$/.test(canary[0].phrase ?? ""));
    phrases.set(ph, canary[0]?.phrase);
    const h = hold.phases.find((x) => x.id === ph);
    ok(`${ph}: holdout.json holds the set's hash, count and ids only; the seal binds the set to the plan packet`, h.setHash === fileHash(at("holdout", "expiry", `${ph}.set.json`)) && h.count === set.scenarios.length && h.scenarioIds.join() === set.scenarios.map((s) => s.id).join() && seal.setHash === h.setHash && seal.planHash === fileHash(at("gold", P, "packet.json")) && h.canaries === 1);
    const hid = byId.get(ph).acceptance.filter((a) => a.holdout);
    ok(`${ph}: the plan's hidden entries are the scenario ids with no text (PKT-HOLD-002)`, hid.map((a) => a.id).join() === set.scenarios.map((s) => s.id).join() && hid.every((a) => !("text" in a)));
    ok(`${ph}: each scenario ties to one of the phase's visible items`, set.scenarios.filter((s) => s.kind !== "canary").every((s) => byId.get(ph).acceptance.some((a) => a.id === s.item && !a.holdout)));
  }
  ok("the four canary phrases are distinct", new Set(phrases.values()).size === 4);
  // canaries only where planted; hidden text nowhere else
  const canaryErr = [];
  const leakOK = new Set(["gold/prove/holdout-result.leaky.json"]);
  for (const [ph, phrase] of phrases) {
    const where = allFiles.filter((f) => readText(at(f)).includes(phrase));
    if (where.join() !== `holdout/expiry/${ph}.set.json`) canaryErr.push(`${ph}: ${where.join(",") || "nowhere"}`);
  }
  ok("each canary phrase appears in its hidden set and in no other fixture file (the history script reads it at run time)", canaryErr.length === 0, canaryErr.join("; "));
  const allGrams = new Set(Object.values(sets).flatMap((s) => [...scenarioGrams(s)]));
  const leakFiles = allFiles.filter((f) => !f.startsWith("holdout/") && !leakOK.has(f)).filter((f) => overlaps(readText(at(f)), allGrams).length > 0);
  ok("no hidden scenario text (a run of 8 words) appears outside the store, except in the planted leaky report", leakFiles.length === 0, leakFiles.join(","));

  // ---------------- owner answers ----------------
  const ans = readJson(at("answers", "owner.json"));
  shapeOk("answers/owner.json", ans, { schemaVersion: "integer", change: "string", scoping: "string", visits: [{ visit: "integer", stop: "string", answers: [{ decision: "string", pick: "string" }], "action?": "string", expects: {} }] });
  const rec = new Map(decisions.decisions.map((d) => [d.id, d.options.find((o) => o.recommended).id]));
  const v1 = ans.visits[0], v2 = ans.visits[1];
  const against = v1.answers.filter((a) => a.pick !== rec.get(a.decision));
  ok("owner answers: every pick is a real option; visit 1 answers all three decisions; exactly one pick (D3.b) is against the recommendation", v1.answers.length === 3 && v1.answers.every((a) => decisions.decisions.find((d) => d.id === a.decision)?.options.some((o) => o.id === a.pick)) && against.length === 1 && against[0].pick === "D3.b");
  const rev = v1.expects.designRevision;
  ok("owner answers: the pick forces one redesign of exactly D3's sections, records its conflict with AC-2, and widens P2 by a file no other phase uses", v1.expects.route === "redesign" && v1.expects.settle === "changed" && JSON.stringify(rev.changedSections) === JSON.stringify(d3.affects) && rev.mustRecordConflict.every((id) => scopeAcc.has(id)) && rev.removesAssumes.join() === "D3.a" && rev.planImpact.P2.addWriteScope.every((p) => repoHas(p) && tasks.every((t) => !t.writeScope.includes(p))) && v2.action === "approve" && v2.expects.route === "approved" && ans.scoping.endsWith("answers/expiry.json") && fs.existsSync(path.join(SCOPING, "answers", "expiry.json")));
  const planAnswers = readJsonl(at("gold", P, "answers.jsonl"));
  ok(`${P}/answers.jsonl: the recommended path, so settle reports assumed`, planAnswers.length === 1 && planAnswers[0].answers.every((a) => a.pick === rec.get(a.decision)));

  // ---------------- planted items ----------------
  const planted = readJson(at("planted.json"));
  shapeOk("planted.json", planted, { schemaVersion: "integer", extends: "string", note: "string", items: [{ id: "string", what: "string", file: "string", tag: "string" }] });
  ok("planted.json names the five planted items of spec 10.3", JSON.stringify(planted.items.map((i) => i.id)) === JSON.stringify(["PL-LENS", "PL-SCOPE", "PL-RED", "PL-CANARY", "PL-LEAK"]) && planted.extends === "tests/fixtures/scoping");
  const count = (file, tag) => readText(at(file)).split(tag).length - 1;
  for (const it of planted.items) {
    const tagText = it.tag === "_planted" ? `"_planted": "${it.id}"` : it.tag;
    ok(`${it.id}: tagged exactly once in ${it.file}`, fs.existsSync(at(it.file)) && count(it.file, tagText) === 1, `${count(it.file, tagText)}`);
    if (it.also) ok(`${it.id}: and tagged once in ${it.also.file}`, count(it.also.file, it.also.tag) === 1);
  }
  const marks = allFiles.flatMap((f) => [...readText(at(f)).matchAll(/(?:PLANTED |"_planted": ")(PL-[A-Z]+)/g)].map((m) => m[1]));
  ok("every PLANTED marker in the fixture belongs to a manifest item, and every item is marked", [...new Set(marks)].sort().join() === planted.items.map((i) => i.id).sort().join());
  const byTag = (id) => planted.items.find((i) => i.id === id);
  const p1 = byTag("PL-SCOPE");
  ok("PL-SCOPE: the outside path exists in the repo and no part of P1's allowedFiles covers it", p1.outside.every((f) => repoHas(f) && outsideAllowed(byId.get(p1.phase), [{ status: "M", file: f }]).length === 1) && outsideAllowed(byId.get("P1"), [{ status: "A", file: "migrations/006_link_expiry.sql" }, { status: "M", file: "src/sharing/links.ts" }, { status: "M", file: "src/sharing/routes.ts" }, { status: "A", file: "tests/expiry.test.ts" }, { status: "M", file: "tests/sharing.test.ts" }]).length === 0);
  const red = readJson(at("gold", "build", "red.json"));
  shapeOk("gold/build/red.json", red, { schemaVersion: "integer", phase: "string", commit: "string", tests: [{ id: "string", file: "string", name: "string", outcome: "string", assertion: "string", "_planted?": "string" }] });
  const buildSh = readText(at("history", "build.sh"));
  ok("PL-RED: exactly one test passed on the red commit, it is the tagged one, and the history script creates it", red.tests.filter((t) => t.outcome === "passed").length === 1 && red.tests.filter((t) => t.outcome === "failed").length === 2 && red.tests.find((t) => t.outcome === "passed")._planted === "PL-RED" && red.tests.every((t) => buildSh.includes(`it("${t.name}"`)));
  ok("PL-CANARY: the history script reads the P1 phrase from the hidden set and puts it in a commit message", /holdout\/expiry\/P1\.set\.json/.test(buildSh) && /commit [^\n]*"test: cover the boundary case, see \$CANARY"/.test(buildSh) && !buildSh.includes(phrases.get("P1")));
  ok("PL-SCOPE and PL-RED/PL-CANARY tags in build.sh sit before the commit they plant", /# PLANTED PL-SCOPE[^\n]*\nprintf [^\n]*\ncommit [^\n]*"feat: a digest line/.test(buildSh) && /# PLANTED PL-CANARY[^\n]*\nprintf [^\n]*\ncommit [^\n]*\$CANARY/.test(buildSh) && /\/\/ PLANTED PL-RED\n\s+it\(/.test(buildSh));
  const leaky = readJson(at("gold", "prove", "holdout-result.leaky.json")), clean = readJson(at("gold", "prove", "holdout-result.clean.json"));
  const shape2 = { schemaVersion: "integer", phase: "string", commit: "string", "_planted?": "string", results: [{ scenario: "string", item: "string|null", verdict: "string", category: "string", observed: "string" }] };
  shapeOk("gold/prove/holdout-result.leaky.json", leaky, shape2); shapeOk("gold/prove/holdout-result.clean.json", clean, shape2);
  const p2g = scenarioGrams(sets.P2);
  const hits = (r) => r.results.filter((x) => overlaps(x.observed, p2g).length > 0).map((x) => x.scenario);
  ok("PL-LEAK: the leaky report's observed line shares 8 words with its scenario; the clean one shares none; no report has a canary phrase or an observed line over 200 characters", hits(leaky).join() === byTag("PL-LEAK").scenario && hits(clean).length === 0 && [leaky, clean].every((r) => r.results.every((x) => x.observed.length <= 200 && ![...phrases.values()].some((p) => x.observed.includes(p)))));

  // ---------------- no secret-shaped file ----------------
  const leaks = allFiles.filter((f) => SECRET_PATTERNS.some((p) => p.test(readText(at(f)))));
  ok("no file in the fixture holds a secret-shaped string", leaks.length === 0, leaks.join(","));
  return check;
}

function diffScope(repo, base, head, phase) {
  const plan = readJson(at("gold", "plan", "expiry", "tasks.json"));
  const task = plan.tasks.find((t) => t.id === phase);
  const out = execFileSync("git", ["-C", repo, "diff", "--name-status", "--no-renames", `${base}..${head}`], { encoding: "utf8" });
  const changed = out.split("\n").filter(Boolean).map((l) => { const [status, ...f] = l.split("\t"); return { status, file: f.join("\t") }; });
  return outsideAllowed(task, changed).map((c) => c.file);
}

function main(argv) {
  const json = argv.includes("--json");
  const args = argv.filter((a) => !a.startsWith("--"));
  const usage = () => { console.error("usage: selfcheck.mjs [--json] | style-check <spec.md> <plain|formal> | diff-scope <repo> <base> <head> <P1..P4> | leak-check <report.json> <P1..P4>"); return 2; };
  try {
    if (args[0] === "style-check") {
      if (args.length !== 3 || !["plain", "formal"].includes(args[2])) return usage();
      const f = styleCheck(readText(args[1]), readJson(at("packs", "style", args[2], "pack.json")));
      console.log(json ? JSON.stringify({ findings: f }) : f.length ? f.map((x) => `DSN-STYLE-001 ${x}`).join("\n") : "ok");
      return f.length ? 1 : 0;
    }
    if (args[0] === "diff-scope") {
      if (args.length !== 5 || !PHASES.includes(args[4])) return usage();
      const o = diffScope(args[1], args[2], args[3], args[4]);
      console.log(json ? JSON.stringify({ outside: o }) : o.join("\n"));
      return o.length ? 1 : 0;
    }
    if (args[0] === "leak-check") {
      if (args.length !== 3 || !PHASES.includes(args[2])) return usage();
      const gs = scenarioGrams(readJson(at("holdout", "expiry", `${args[2]}.set.json`)));
      const bad = readJson(args[1]).results.filter((r) => overlaps(r.observed, gs).length > 0).map((r) => r.scenario);
      console.log(json ? JSON.stringify({ leaks: bad }) : bad.join("\n"));
      return bad.length ? 1 : 0;
    }
    if (args.length) return usage();
  } catch (e) { console.error(`selfcheck: ${e.message}`); return 2; }
  let results;
  try { results = run(); } catch (e) { check.push({ name: "selfcheck crashed on malformed or missing gold", ok: false, detail: String(e.message) }); results = check; }
  const failed = results.filter((r) => !r.ok);
  if (json) console.log(JSON.stringify({ passed: results.length - failed.length, failed: failed.length, results }));
  else {
    for (const r of results) console.log(`${r.ok ? "ok" : "not ok"} - ${r.name}${r.ok ? "" : `\n  ${r.detail}`}`);
    console.log(`${results.length - failed.length}/${results.length} checks pass`);
  }
  return failed.length ? 1 : 0;
}

// Real paths on both sides: on macOS a temp dir is reached through the /var -> /private/var symlink, and a plain
// path compare there never runs main, so a copy of the fixture would "pass" by doing nothing.
// exitCode, not process.exit(): exit drops stdout a small pipe has not taken yet (a loaded box gives new pipes 4 KB).
if (process.argv[1] && fs.realpathSync(path.resolve(process.argv[1])) === fs.realpathSync(fileURLToPath(import.meta.url))) process.exitCode = main(process.argv.slice(2));
