#!/usr/bin/env node
// scripts/check-deps.mjs — checks deps.yaml, the declared package graph (spec §4b.1a, §4e B10).
//
//   node scripts/check-deps.mjs [--root DIR] [--mermaid [--base-file FILE]]
//
// Validates deps.yaml against schemas/deps.schema.json; checks the reference, kind, tier, cycle and
// fan-out rules; scans the TypeScript packages, hooks and statusline for cli/db/file edges, and the C#
// projects for http, file and db edges (string literals naming another package's routes or state paths), and
// fails on any undeclared one or any whose status is wrong; and, once src/ or tests/ have C# projects, checks
// their ProjectReferences, per-project fan-out and project- and compose-edge status. A compose edge lets a Host
// reference a lower package's Features and Infrastructure for their Add…() registration methods (DEPS-COMPOSE-001;
// ARCH-COMPOSE-001 checks the calls in the compiled Host). --mermaid also prints the graph
// and the edge list (added/removed against --base-file) for a PR summary.
// Exit codes: 0 ok, 1 a rule failed, 2 a tool error. Node builtins only: no install step, no network.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { YamlError, parseYaml } from "./deps/yaml.mjs";
import { SchemaError, SUPPORTED_KEYWORDS, validate } from "./deps/schema.mjs";
import { ScanError, buildFiles, readProjects, scan, scanCSharp } from "./deps/scan.mjs";

export { parseYaml, validate, SUPPORTED_KEYWORDS };

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SCHEMA = path.join(HERE, "..", "schemas", "deps.schema.json");
const RERUN = "node scripts/check-deps.mjs";
const DAY = 86_400_000;

class ToolError extends Error {}

const tierLabel = (n) => `tier ${n.tier}${n.sublevel ? `.${n.sublevel}` : ""}`;
const rank = (n) => n.tier * 10 + (n.sublevel ?? 0);
const edgeKey = (e) => `${e.from}|${e.to}|${e.kind}`;
const edgeText = (e) => `${e.from} -> ${e.to} (${e.kind})`;
const isHostProject = (name) => /\.Host$/.test(name);
const linked = (kind) => kind === "project" || kind === "compose"; // C# edges between packages, pointing down the tiers

// What a C# project is: "tests" or "testing" (test code), "analyzer" (a project of an analyzer: true
// package) or "prod". Test code is every project under tests/, plus any project named *.Tests or
// *.Testing (also with a further suffix, such as *.Tests.Integration). Within test code, a name with a
// .Testing segment is test-support that other test code may reference; everything else is a leaf.
// A test: true package's own *.Testing project outside tests/ (Aw.Kernel.Testing) is that package's
// production code. `file` is the project's repo-relative path, or null for a referenced name.
function roleOf(name, owner, file) {
  const underTests = file !== null && file.startsWith("tests/");
  const testing = /\.Testing(\.|$)/.test(name);
  if (underTests || testing || /\.Tests(\.|$)/.test(name)) {
    if (testing && owner?.test && !underTests) return owner.analyzer ? "analyzer" : "prod";
    return testing ? "testing" : "tests";
  }
  if (owner?.analyzer) return "analyzer";
  return "prod";
}

const yamlEdge = (from, to, kind, status) => [`      - from: ${from}`, `        to: ${to}`, `        kind: ${kind}`, `        status: ${status}`, "        note: <why this edge exists>"].join("\n");

function findCycles(edges) {
  const adj = new Map();
  for (const e of edges) { if (!adj.has(e.from)) adj.set(e.from, new Set()); adj.get(e.from).add(e.to); }
  const cycles = new Map();
  const visit = (start, node, trail) => {
    for (const next of [...(adj.get(node) ?? [])].sort()) {
      if (next === start) {
        const k = trail.indexOf([...trail].sort()[0]);
        const canon = [...trail.slice(k), ...trail.slice(0, k)];
        cycles.set(canon.join(">"), [...canon, canon[0]]);
      } else if (!trail.includes(next) && next > start) visit(start, next, [...trail, next]);
    }
  };
  for (const n of [...adj.keys()].sort()) visit(n, n, [n]);
  return [...cycles.values()];
}

function parseDate(s) {
  const d = new Date(`${s}T00:00:00Z`);
  return Number.isNaN(d.getTime()) || d.toISOString().slice(0, 10) !== s ? null : d;
}

export function check(root, today = new Date().toISOString().slice(0, 10)) {
  const file = path.join(root, "deps.yaml");
  if (!fs.existsSync(file)) throw new ToolError(`deps.yaml not found at ${file}`);
  const fails = [];
  const info = [];
  const fail = (rule, title, measured, limit, fix) => fails.push({ rule, title, measured, limit, fix });

  let doc;
  try { doc = parseYaml(fs.readFileSync(file, "utf8")); } catch (e) {
    if (!(e instanceof YamlError)) throw e;
    fail("DEPS-YAML-001", "deps.yaml is outside the supported YAML subset", e.message, "the YAML subset described at the top of deps.yaml", "rewrite that line with block style and plain or quoted scalars");
    return { fails, info, doc: null, valid: false };
  }
  const schemaErrs = validate(JSON.parse(fs.readFileSync(SCHEMA, "utf8")), doc);
  for (const m of schemaErrs) fail("DEPS-SCHEMA-001", "deps.yaml does not match its schema", `deps.yaml ${m}`, "schemas/deps.schema.json", `edit the field at ${m.split(":")[0]} in deps.yaml to match schemas/deps.schema.json`);
  if (schemaErrs.length) return { fails, info, doc, valid: false };

  // ---- references and invariants ----
  const byId = new Map();
  for (const p of doc.packages) {
    if (byId.has(p.id)) fail("DEPS-REF-002", "duplicate package id", p.id, "package ids are unique", `rename one of the ${p.id} entries in deps.yaml`);
    byId.set(p.id, p);
  }
  const prefixes = new Map();
  for (const p of doc.packages.filter((x) => x.projectPrefix)) {
    if (prefixes.has(p.projectPrefix)) fail("DEPS-REF-004", "two packages share a projectPrefix", `${p.projectPrefix} (${prefixes.get(p.projectPrefix)}, ${p.id})`, "each package has its own projectPrefix", `give ${p.id} a prefix of its own in deps.yaml`);
    else prefixes.set(p.projectPrefix, p.id);
  }
  const nsOwner = new Map();
  for (const p of doc.packages.filter((x) => x.namespace)) {
    if (nsOwner.has(p.namespace)) fail("DEPS-REF-005", "two packages own one state path", `${p.namespace} (${nsOwner.get(p.namespace)}, ${p.id})`, "each namespace, db and files path has one owner", `give ${p.id} a namespace of its own in deps.yaml`);
    else nsOwner.set(p.namespace, p.id);
  }
  for (const p of doc.packages) {
    for (const entry of [...(p.db ?? []), ...(p.files ?? []), ...(p.namespace ? [p.namespace] : [])]) {
      for (const [ns, owner] of nsOwner) {
        if (owner === p.id || entry === p.namespace) continue;
        if (entry === ns || entry.startsWith(`${ns}/`) || ns.startsWith(`${entry}/`)) fail("DEPS-REF-005", "two packages own one state path", `${entry} (${p.id}) overlaps namespace ${ns} (${owner})`, "each namespace, db and files path has one owner", `move ${entry} out of ${ns}/, or let ${owner} own it`);
      }
    }
  }
  const seenEdges = new Set();
  const edges = doc.edges.filter((e) => {
    const missing = [e.from, e.to].filter((id) => !byId.has(id));
    for (const id of missing) fail("DEPS-REF-001", "edge names an unknown package", `${edgeText(e)}: ${id} is not under packages`, "edges connect declared packages", `declare ${id} under packages in deps.yaml, or fix the edge`);
    if (seenEdges.has(edgeKey(e))) { fail("DEPS-REF-003", "duplicate edge", edgeText(e), "one entry per (from, to, kind)", `delete the second ${edgeText(e)} entry in deps.yaml`); return false; }
    seenEdges.add(edgeKey(e));
    return missing.length === 0;
  });

  // ---- exceptions: dates, expiry; usage is checked at the end ----
  const active = [];
  for (const x of doc.exceptions) {
    if (!byId.has(x.subject)) { fail("DEPS-REF-001", "exception names an unknown package", `${x.rule} for ${x.subject}`, "exceptions name declared packages", `fix the subject of the ${x.rule} exception in deps.yaml`); continue; }
    const rec = parseDate(x.recorded);
    const exp = parseDate(x.expires);
    const span = rec && exp ? Math.round((exp - rec) / DAY) : null;
    if (!rec || !exp || span < 0 || span > doc.limits.exceptionMaxDays) {
      const why = !rec || !exp ? `not a calendar date (recorded ${x.recorded}, expires ${x.expires})` : span < 0 ? `recorded ${x.recorded} is after expires ${x.expires}` : `${span} days from recorded to expires`;
      fail("DEPS-EXC-003", "exception dates are invalid", `${x.rule} for ${x.subject}: ${why}`, `real dates, recorded <= expires, at most ${doc.limits.exceptionMaxDays} days apart (limits.exceptionMaxDays)`, "fix recorded and expires in deps.yaml");
      continue;
    }
    if (x.expires < today) { fail("DEPS-EXC-001", "expired exception", `${x.rule} for ${x.subject} expired ${x.expires} (today ${today})`, "an exception is valid until its expires date", "fix the violation, or renew the exception in a reviewed PR with a new reason and dates"); continue; }
    if (x.excuses) { // DEPS-CYCLE-001: the one declared cli or http edge it excuses, leaving its subject; covered at the cycle check
      const at = `${x.excuses.from} -> ${x.excuses.to} (${x.excuses.kind})`;
      const why = x.excuses.from !== x.subject ? `${at} does not leave ${x.subject}` : !seenEdges.has(`${x.excuses.from}|${x.excuses.to}|${x.excuses.kind}`) ? `${at} is not a declared edge` : null;
      if (why) { fail("DEPS-EXC-005", "cycle exception excuses an edge it can't", `${x.rule} for ${x.subject}: ${why}`, "a DEPS-CYCLE-001 exception excuses one declared cli or http edge leaving its subject", `declare (from: ${x.subject}, to: <package>, kind: cli or http) and name it under excuses, or fix the exception's excuses`); continue; }
      active.push({ ...x, used: false, measured: null });
      info.push(`exception ${x.rule} ${at} (expires ${x.expires}): ${x.reason}`);
      continue;
    }
    active.push({ ...x, used: false, measured: null });
    info.push(`exception ${x.rule} ${x.subject} (expires ${x.expires}): ${x.reason}`);
  }
  const excepted = (rule, subject, measured) => {
    const x = active.find((a) => a.rule === rule && a.subject === subject);
    if (!x) return false;
    x.used = true;
    x.measured = measured;
    return true;
  };
  const measure = (rule, subject, measured) => { for (const x of active) if (x.rule === rule && x.subject === subject && !x.used) x.measured = measured; };

  // Every kind and tier rule for one edge; true when one failed it.
  function kindsAndTiers(e, a, b) {
    let bad = false;
    const flag = (...args) => { bad = true; fail(...args); };
    if (b.kind !== "package") {
      flag("DEPS-KIND-002", "edge into a shell node", edgeText(e), `shell nodes only consume packages; nothing depends on them (${e.to} is ${b.kind})`, `move the shared state or contract into a package and point the edge the other way (${e.to} -> <package>)`);
      return bad;
    }
    // DEPS-KIND-004 depends only on the target, so it holds for an http edge from a shell node too.
    if (e.kind === "http" && !(b.routes ?? []).length) flag("DEPS-KIND-004", "http edge to a package that serves no routes", edgeText(e), `an http edge calls a package's HTTP API, declared as its routes (${e.to} lists none)`, `add routes: [/<prefix>/] to ${e.to} in deps.yaml, or change the edge's kind`);
    if (a.kind !== "package") {
      if (linked(e.kind)) flag("DEPS-KIND-001", `${e.kind} edge outside the C# packages`, edgeText(e), `project and compose edges connect kind: package nodes (${e.from} is ${a.kind})`, `change the edge to kind: cli or file in deps.yaml`);
      return bad;
    }
    if (e.kind === "compose" && b.tier === 0) flag("DEPS-COMPOSE-001", "compose edge into a tier-0 package", edgeText(e), "a tier-0 package has no Features or Infrastructure to compose; its library projects are used whole through a project edge", "delete the compose edge; reference the tier-0 library projects under the project edge");
    if (linked(e.kind) && b.test && !a.test) flag("DEPS-KIND-003", `${e.kind} edge from production code into a test-support package`, edgeText(e), "only test projects reference test-support packages, and they need no declared edge", `delete the edge from deps.yaml and move the reference into ${e.from}'s *.Tests project`);
    if (linked(e.kind) && rank(a) <= rank(b)) flag("DEPS-TIER-001", `${e.kind} edge does not point to a lower tier`, `${e.from} (${tierLabel(a)}) -> ${e.to} (${tierLabel(b)})`, `a ${e.kind} edge points strictly down the tiers (§4b.1a)`, `change it to kind: cli and call ${e.to}'s CLI, or move the shared code down a tier`);
    if ((e.kind === "db" || e.kind === "file") && rank(a) < rank(b)) flag("DEPS-TIER-002", `${e.kind} edge points to a higher tier`, `${e.from} (${tierLabel(a)}) -> ${e.to} (${tierLabel(b)})`, "db and file edges point down or across, never up", `replace it with (from: ${e.from}, to: ${e.to}, kind: cli) and ask ${e.to}'s CLI`);
    return bad;
  }
  // ---- kinds and tiers ----
  const misplaced = new Set(); // compose edges a kind or tier rule already failed: no DEPS-COMPOSE-001 on top
  for (const e of edges) if (kindsAndTiers(e, byId.get(e.from), byId.get(e.to)) && e.kind === "compose") misplaced.add(edgeKey(e));
  for (const e of edges.filter((x) => x.kind === "compose" && !misplaced.has(edgeKey(x)) && !seenEdges.has(`${x.from}|${x.to}|project`))) {
    fail("DEPS-COMPOSE-001", "compose edge without a project edge", `${edgeText(e)}: no project edge ${e.from} -> ${e.to}`, "a compose edge sits beside a project edge to the same package: the Host and Features reach the target's Core through the project edge, and the compose edge adds only the Add…() registration calls", `declare the project edge too (the Host and Features reference ${e.to}'s Core):\n${yamlEdge(e.from, e.to, "project", e.status)}`);
  }
  // A cycle exception covers the cycle its excused edge closes: the one cycle that goes away without that edge.
  const cycles = findCycles(edges);
  const coveredCycles = new Set();
  for (const x of active.filter((a) => a.excuses)) {
    const { from, to, kind } = x.excuses;
    const at = `${from} -> ${to} (${kind})`;
    const without = new Set(findCycles(edges.filter((e) => !(e.from === from && e.to === to && e.kind === kind))).map((c) => c.join(">")));
    const closes = cycles.filter((c) => !without.has(c.join(">")));
    // Only the first exception on a cycle covers it: a second one (say, the other direction) suppresses nothing.
    const duplicate = closes.length === 1 && coveredCycles.has(closes[0]);
    if (closes.length === 1 && !duplicate) { x.used = true; coveredCycles.add(closes[0]); }
    const parallel = closes.length === 0 && cycles.some((c) => c.some((n, i) => n === from && c[i + 1] === to));
    x.measured = duplicate ? `${closes[0].join(" -> ")} is already covered by another DEPS-CYCLE-001 exception`
      : closes.length === 1 ? closes[0].join(" -> ")
      : closes.length > 1 ? `${closes.length} cycles pass through ${at} (${closes.map((c) => c.join(" -> ")).join("; ")}), more than the one an exception covers`
      : parallel ? `every cycle through ${at} also has another ${from} -> ${to} edge` : `no cycle passes through ${at}`;
    x.fix = duplicate ? `delete one of the two exceptions on ${closes[0].join(" -> ")}`
      : closes.length > 1 ? `break the other cycle through ${at}: the exception covers one`
      : parallel ? `remove the other ${from} -> ${to} edge: only the excused ${kind} edge may close the cycle` : undefined;
  }
  for (const c of cycles.filter((x) => !coveredCycles.has(x))) fail("DEPS-CYCLE-001", "the declared graph has a cycle", c.join(" -> "), "the graph is acyclic across every edge kind (§4b.1a); a DEPS-CYCLE-001 exception covers the one cycle its excused cli or http edge closes", "delete one edge of the cycle in deps.yaml, or move the shared part into a lower tier");

  // ---- cli/db/file edges in the source ----
  const declared = new Map(edges.map((e) => [edgeKey(e), e]));
  const found = scan(root, doc.packages);
  for (const [k, h] of scanCSharp(root, doc.packages)) if (!found.has(k)) found.set(k, h);
  let undeclared = 0;
  for (const [k, h] of found) {
    const d = declared.get(k);
    if (!d) {
      undeclared++;
      fail("DEPS-EDGE-001", "undeclared edge in source", `${h.from} -> ${h.to} (${h.kind}): ${h.where} ${h.what}`, "every cli, db, file and http edge is declared in deps.yaml", `remove the call, or add this under edges in deps.yaml (a reviewed change):\n${yamlEdge(h.from, h.to, h.kind, "current")}`);
    } else if (d.status === "planned") fail("DEPS-STALE-002", "planned edge found in source", `${edgeText(d)}: ${h.where} ${h.what}`, "an edge the source has today is status: current", `set status: current on ${edgeText(d)} in deps.yaml`);
  }
  for (const e of edges.filter((x) => x.status === "current" && !linked(x.kind) && !found.has(edgeKey(x)))) {
    // Only the C# scan finds http calls, so an http edge from a shell node can't be checked (review run 4).
    const from = byId.get(e.from);
    if (e.kind === "http" && from.kind !== "package") {
      info.push(`${edgeText(e)}: not scanned (http calls from ${from.kind} sources are not detected)`);
      continue;
    }
    const where = e.kind === "http"
      ? `no string literal naming ${e.to}'s routes (${byId.get(e.to).routes?.join(", ") ?? "none"}) in ${e.from}'s C# projects`
      : `no match under ${byId.get(e.from).scan?.join(", ") ?? "(no scan dirs)"} or in ${e.from}'s C# projects`;
    fail("DEPS-STALE-001", "declared current edge not found in source", `${edgeText(e)}: ${where}`, "a status: current edge exists in the source today", "set status: planned in deps.yaml if it is still intended, or delete the edge");
  }
  info.push(`scan: ${found.size} cli/db/file/http edges found in source, ${undeclared === 0 ? "all declared" : `${undeclared} undeclared (DEPS-EDGE-001 above)`}`);

  checkProjects(root, doc, byId, declared, edges, { fail, info, excepted, measure });

  for (const x of active.filter((a) => !a.used)) {
    fail("DEPS-EXC-002", "exception suppresses nothing", `${x.rule} for ${x.subject}: ${x.measured ?? "no projects to measure yet"}`, "an exception exists only while its rule would fail, and a cycle exception covers exactly one cycle", x.fix ?? `delete the ${x.rule} exception for ${x.subject} from deps.yaml`);
  }
  return { fails, info, doc, valid: true };
}

function ownerOf(name, nodes) {
  let best = null;
  for (const n of nodes) {
    const pre = n.projectPrefix;
    if (pre && (name === pre || name.startsWith(`${pre}.`)) && (!best || pre.length > best.projectPrefix.length)) best = n;
  }
  return best;
}

function checkProjects(root, doc, byId, declared, edges, { fail, info, excepted, measure }) {
  const analyzerOk = (name) => ownerOf(name, doc.packages)?.analyzer === true;
  for (const f of buildFiles(root)) {
    for (const r of f.refs) {
      if (!r.analyzer) fail("DEPS-PROJ-005", "ProjectReference in a Directory.Build file", `${f.file} -> ${r.name}`, "runtime references live in each .csproj, where the check can see who owns them (analyzer references may live here)", `move the ProjectReference out of ${f.file} into the projects that need it`);
      else if (!analyzerOk(r.name)) fail("DEPS-PROJ-006", "analyzer reference to a project that is not an analyzer", `${f.file} -> ${r.name}`, "OutputItemType=\"Analyzer\" references point at an analyzer: true package", `point the reference at an analyzer project, or drop OutputItemType="Analyzer"`);
    }
  }
  const projects = readProjects(root);
  const fileOf = new Map(projects.map((p) => [p.name, p.file]));
  const roleOfRef = (ref, refOwner) => roleOf(ref, refOwner, fileOf.get(ref) ?? null);
  // §4b.1a: a package reaches another package only through its Core (tier-0 packages are used whole), or a project the owner lists under `exports`.
  // A tier-0 package's library projects are used whole, but never its Host, Infrastructure, Features or App.
  const contractOk = (ref, refOwner) => (refOwner.tier === 0 ? !/\.(Host|Infrastructure|Features|App)$/.test(ref) : ref === `${refOwner.projectPrefix}.Core` || (refOwner.exports ?? []).includes(ref));
  // §4b.1: Core references Aw.Kernel only and Infrastructure its own Core and Aw.Kernel, so another
  // package's Core is held by Features (App in the small shape) or composed by Host.
  const sourceOk = (name, refOwner) => refOwner.tier === 0 || !/\.(Core|Infrastructure)$/.test(name);
  const contractFail = (pj, ref, refOwner) => fail("DEPS-PROJ-007", "reference into another package past its Core", `${pj.name} -> ${ref} (${refOwner.id})`, `another package is used only through its Core project (${refOwner.projectPrefix}.Core) or a tier-0 package (§4b.1a)`, `reference ${refOwner.projectPrefix}.Core instead, moving what you need into ${refOwner.id}'s Core contracts`);
  // A Host's reference to a lower package's Features or Infrastructure: the compose carve-out of DEPS-PROJ-007.
  const composeRef = (pj, ref, refOwner, owner) => isHostProject(pj.name) && refOwner.tier !== 0 && rank(owner) > rank(refOwner) && [".Features", ".Infrastructure"].some((s) => ref === `${refOwner.projectPrefix}${s}`);
  const real = new Set(); // "Owner|RefOwner" for production cross-package references
  const composed = new Set(); // "Owner|RefOwner" for Host references under the compose carve-out
  const owners = new Set(); // packages with at least one production project
  for (const pj of projects) {
    const owner = ownerOf(pj.name, doc.packages);
    const role = roleOf(pj.name, owner, pj.file);
    const testCode = role === "tests" || role === "testing";
    if (!owner && !testCode) { fail("DEPS-PROJ-002", "C# project no package owns", `${pj.file} (${pj.name})`, "every project name starts with a package's projectPrefix (test assemblies such as Aw.Architecture.Tests excepted)", "rename the project, or declare its package in deps.yaml"); continue; }
    if (role === "prod" || role === "analyzer" || (owner?.test && role === "testing")) owners.add(owner.id);
    for (const a of pj.analyzers) if (!analyzerOk(a)) fail("DEPS-PROJ-006", "analyzer reference to a project that is not an analyzer", `${pj.name} -> ${a}`, "OutputItemType=\"Analyzer\" references point at an analyzer: true package", `point the reference at an analyzer project, or drop OutputItemType="Analyzer"`);
    for (const ref of pj.buildOrder) {
      const refOwner = ownerOf(ref, doc.packages);
      if (!refOwner) { fail("DEPS-PROJ-002", "C# project no package owns", `${pj.name} -> ${ref}`, "every project name starts with a package's projectPrefix", "rename the project, or declare its package in deps.yaml"); continue; }
      if (testCode && owner && (refOwner.id === owner.id || declared.has(`${owner.id}|${refOwner.id}|cli`))) continue; // build order only: no project edge, no fan-out
      fail("DEPS-PROJ-009", "build-order reference without a cli edge", `${pj.name} -> ${ref} (${owner?.id ?? "no owner"} -> ${refOwner.id})`, "a ReferenceOutputAssembly=\"false\" ProjectReference only orders the build, for test code that runs the other package's binary through its declared cli edge", `declare (from: ${owner?.id ?? "<owner>"}, to: ${refOwner.id}, kind: cli) in deps.yaml, or drop the reference`);
    }
    for (const ref of pj.refs) {
      const refOwner = ownerOf(ref, doc.packages);
      const refRole = roleOfRef(ref, refOwner);
      if (refRole === "tests" || (refRole === "testing" && !testCode)) { fail("DEPS-PROJ-003", "reference to test code", `${pj.name} -> ${ref} (${pj.file})`, "nothing references a *.Tests project, and only test code references a *.Testing project", "remove the reference; production code gets fakes through its ports, not from test projects"); continue; }
      if (refRole === "analyzer" && !testCode) { fail("DEPS-PROJ-006", "runtime reference to an analyzer", `${pj.name} -> ${ref}`, "analyzers are referenced with OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\" only", `add OutputItemType="Analyzer" ReferenceOutputAssembly="false" to the reference in ${pj.file}`); continue; }
      if (!refOwner) { fail("DEPS-PROJ-002", "C# project no package owns", `${pj.name} -> ${ref}`, "every project name starts with a package's projectPrefix", "rename the project, or declare its package in deps.yaml"); continue; }
      if (testCode) {
        // Test code follows its owner: its own package, test-support packages, and its owner's project edges.
        if (!owner || refOwner.id === owner.id || refOwner.test || refRole === "analyzer") continue;
        if (declared.has(`${owner.id}|${refOwner.id}|project`)) {
          if (owner.test) real.add(`${owner.id}|${refOwner.id}`); // a test-support package's own projects make its edges current
          if (refRole === "prod" && !contractOk(ref, refOwner)) contractFail(pj, ref, refOwner);
          continue;
        }
        fail("DEPS-PROJ-004", "test code references a package its owner may not", `${pj.name} -> ${ref} (${owner.id} -> ${refOwner.id})`, "test code may reference its own package, test-support packages (test: true) and what its owner has a project edge to", `reference ${refOwner.id} from its own tests, or declare (from: ${owner.id}, to: ${refOwner.id}, kind: project) if ${owner.id} itself needs it`);
        continue;
      }
      if (refOwner.id === owner.id) continue;
      if (composeRef(pj, ref, refOwner, owner)) {
        composed.add(`${owner.id}|${refOwner.id}`);
        if (!declared.has(`${owner.id}|${refOwner.id}|compose`)) fail("DEPS-COMPOSE-001", "Host reference into another package's Features or Infrastructure without a compose edge", `${pj.name} -> ${ref} (${owner.id} -> ${refOwner.id})`, "a Host reaches another package's Features and Infrastructure only for their Add…() registration methods, under a declared compose edge (context spec §4)", `reference ${refOwner.projectPrefix}.Core instead, or declare the compose edge beside the project edge (a reviewed change):\n${yamlEdge(owner.id, refOwner.id, "compose", "current")}`);
        continue;
      }
      real.add(`${owner.id}|${refOwner.id}`); // compose references don't make the project edge current: it needs a Core reference
      if (!contractOk(ref, refOwner)) contractFail(pj, ref, refOwner);
      else if (!sourceOk(pj.name, refOwner)) fail("DEPS-PROJ-008", "Core or Infrastructure references another package", `${pj.name} -> ${ref} (${owner.id} -> ${refOwner.id})`, "another package's Core is referenced from Features (App in the small shape) or Host; Core and Infrastructure use tier-0 packages only (§4b.1)", `move the reference to ${owner.projectPrefix}.Features (or .App), and pass what Core needs in through a port`);
      if (!declared.has(`${owner.id}|${refOwner.id}|project`)) fail("DEPS-PROJ-001", "undeclared ProjectReference", `${pj.name} -> ${ref} (${owner.id} -> ${refOwner.id})`, "every cross-package ProjectReference is a declared project edge", `call ${refOwner.id} through its CLI, or add this under edges in deps.yaml (a reviewed change):\n${yamlEdge(owner.id, refOwner.id, "project", "current")}`);
    }
    if (testCode || isHostProject(pj.name) || owner.test) continue;
    const inRepo = [...pj.refs].sort();
    const m2 = `${pj.name} references ${inRepo.length} in-repo projects (${inRepo.join(", ")})`;
    if (inRepo.length > doc.limits.projectFanOut) { if (!excepted("DEPS-FANOUT-002", owner.id, m2)) fail("DEPS-FANOUT-002", "project fan-out over the limit", m2, `${doc.limits.projectFanOut} in-repo projects per project (limits.projectFanOut; *.Host and test code exempt, analyzer references not counted)`, `split ${pj.name}, or record a DEPS-FANOUT-002 exception for ${owner.id} with a reason and an expiry`); } else measure("DEPS-FANOUT-002", owner.id, m2);
    const m3 = `${pj.name} references ${pj.packages.length} third-party packages (${pj.packages.join(", ")})`;
    if (pj.packages.length > doc.limits.thirdPartyFanOut) { if (!excepted("DEPS-FANOUT-003", owner.id, m3)) fail("DEPS-FANOUT-003", "third-party fan-out over the limit", m3, `${doc.limits.thirdPartyFanOut} third-party packages per project, analyzers (PrivateAssets=all) excluded (limits.thirdPartyFanOut; *.Host and test code exempt)`, `drop a package from ${pj.name} or split it, or record a DEPS-FANOUT-003 exception for ${owner.id}`); } else measure("DEPS-FANOUT-003", owner.id, m3);
  }
  for (const e of edges.filter((x) => x.kind === "project")) {
    const has = real.has(`${e.from}|${e.to}`);
    if (e.status === "current" && !has) fail("DEPS-STALE-001", "declared current edge not found in source", `${edgeText(e)}: ${owners.has(e.from) ? "no ProjectReference" : `${e.from} has no C# projects yet`}`, "a status: current edge exists in the source today", "set status: planned in deps.yaml if it is still intended, or delete the edge");
    if (e.status === "planned" && has) fail("DEPS-STALE-002", "planned edge found in source", `${edgeText(e)}: a ProjectReference exists`, "an edge the source has today is status: current", `set status: current on ${edgeText(e)} in deps.yaml`);
  }
  for (const e of edges.filter((x) => x.kind === "compose" && byId.get(x.to).tier !== 0)) { // tier 0: DEPS-COMPOSE-001 already
    const has = composed.has(`${e.from}|${e.to}`);
    if (e.status === "current" && !has) fail("DEPS-STALE-001", "declared current edge not found in source", `${edgeText(e)}: no ${e.from} Host ProjectReference into ${e.to}'s Features or Infrastructure`, "a status: current edge exists in the source today", "set status: planned in deps.yaml if it is still intended, or delete the edge");
    if (e.status === "planned" && has) fail("DEPS-STALE-002", "planned edge found in source", `${edgeText(e)}: a Host ProjectReference into ${e.to}'s Features or Infrastructure exists`, "an edge the source has today is status: current", `set status: current on ${edgeText(e)} in deps.yaml`);
  }
  info.push(projects.length === 0 ? "project references: no C# projects yet — skipped, ok" : `project references: checked ${projects.length} C# projects`);
}

// ---------- output ----------

const ARROW = { project: "-->", compose: "-->", cli: "-.->", db: "==>", file: "--o", http: "-.->" };

export function mermaid(doc, baseDoc) {
  const out = ["```mermaid", "flowchart TB"];
  const groups = new Map();
  for (const p of doc.packages) {
    const g = p.kind === "package" ? `T${p.tier}` : "X";
    if (!groups.has(g)) groups.set(g, []);
    groups.get(g).push(p.id);
  }
  for (const g of [...groups.keys()].sort()) out.push(`  subgraph ${g}["${g === "X" ? "consumers" : `tier ${g.slice(1)}`}"]`, ...groups.get(g).map((id) => `    ${id}`), "  end");
  for (const e of doc.edges) out.push(`  ${e.from} ${ARROW[e.kind]}|${e.kind}| ${e.to}`);
  out.push("```", "");
  const line = (e) => `${e.from} -> ${e.to} (${e.kind}, ${e.status})`;
  if (!baseDoc) out.push(`edges (${doc.edges.length}):`, ...doc.edges.map((e) => `  ${line(e)}`));
  else {
    const before = new Set(baseDoc.edges.map(line));
    const after = new Set(doc.edges.map(line));
    const added = doc.edges.filter((e) => !before.has(line(e)));
    const removed = baseDoc.edges.filter((e) => !after.has(line(e)));
    out.push(`Added edges (${added.length}):`, ...added.map((e) => `+ ${line(e)}`), `Removed edges (${removed.length}):`, ...removed.map((e) => `- ${line(e)}`));
  }
  return out.join("\n");
}

function readBase(file) {
  if (!fs.existsSync(file)) throw new ToolError(`base file not found: ${file}`);
  let base;
  try { base = parseYaml(fs.readFileSync(file, "utf8")); } catch (e) { throw new ToolError(`base file ${file}: ${e.message}`); }
  if (!base || !Array.isArray(base.edges)) throw new ToolError(`base file ${file} has no edges list`);
  return base;
}

function main(argv) {
  let root = path.join(HERE, "..");
  let wantMermaid = false;
  let baseFile = null;
  let today = new Date().toISOString().slice(0, 10);
  const value = (i) => { const v = argv[i + 1]; if (v === undefined || v === "" || v.startsWith("--")) throw new ToolError(`${argv[i]} needs a value`); return v; };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--root") root = value(i++);
    else if (a === "--mermaid") wantMermaid = true;
    else if (a === "--base-file") baseFile = value(i++);
    else if (a === "--today") { today = value(i++); if (!parseDate(today)) throw new ToolError("--today needs a YYYY-MM-DD date"); } // for tests: pins the date exceptions expire against
    else if (a === "-h" || a === "--help") { console.log("usage: node scripts/check-deps.mjs [--root DIR] [--mermaid [--base-file FILE]] [--today YYYY-MM-DD]"); return 0; }
    else throw new ToolError(`unknown argument ${a}`);
  }
  if (baseFile && !wantMermaid) throw new ToolError("--base-file needs --mermaid");
  const base = baseFile ? readBase(baseFile) : null;
  const { fails, info, doc, valid } = check(path.resolve(root), today);
  for (const f of fails) console.log(`FAIL ${f.rule} ${f.title}\n  measured: ${f.measured}\n  limit: ${f.limit}\n  fix: ${f.fix}\n  rerun: ${RERUN}`);
  for (const l of info) console.log(l);
  if (wantMermaid) console.log(valid ? `\n${mermaid(doc, base)}` : "mermaid: skipped (deps.yaml is invalid; fix the failures above first)");
  if (fails.length) { console.log(`deps: ${fails.length} failure(s)`); return 1; }
  console.log(`deps: ok (${doc.packages.length} packages, ${doc.edges.length} edges)`);
  return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try { process.exitCode = main(process.argv.slice(2)); } catch (e) {
    const known = e instanceof ToolError || e instanceof YamlError || e instanceof SchemaError || e instanceof ScanError;
    console.error(`check-deps: error: ${known ? e.message : e.stack}`);
    process.exitCode = 2;
  }
}
