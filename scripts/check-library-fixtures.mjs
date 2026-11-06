#!/usr/bin/env node
// scripts/check-library-fixtures.mjs — checks the made-up design-doc fixtures (PR review page spec 2026-11-04, §12 Ph0):
//
//   node scripts/check-library-fixtures.mjs [--dir DIR]      (default: tests/fixtures/library)
//
// DIR/expected.json is the contract: the parser's own tests read the same file. This check proves the fixtures say what the
// file says, using only the schema's frontMatter ($defs/frontMatter of schemas/library/design-doc.schema.json) and a tiny
// strict reader for the flat `key: value` / `[a, b]` front matter the convention uses (no YAML library is installed, and
// the raw line has to decide `aw-doc: 1.0`, which YAML would read as 1). It does not parse items; the C# parser does.
//
//  1. Every .md under DIR (README.md aside) is an entry in expected.json, every entry exists (lstat), and the non-md files
//     are exactly the `supporting` list.
//  2. Every code SND-LIB-010..019 has a fixture, and the `codes` map matches the entries' errors.
//  3. A read doc without SND-LIB-010 has front matter that passes $defs/frontMatter; its refs validate against $defs/ref and sit on a line that names their target; frontMatterLinks (parent, supersedes) are its front matter's.
//  4. An SND-LIB-010 fixture's front matter fails it, at the one instance path its badKey names; a near miss has no aw-doc number 1, or a blank first line.
//  5. A generated doc's source-sha256 is the sha256 of its source file (SND-LIB-017: it differs).
//  6. A skipped doc has its reason: a symlink is a symlink, a dot folder or a deny glob is in the path, a near miss is one,
//     a no-front-matter file has none; vault dot folders and deny globs are never read; the vault covers each reason.
//  7. Body codes hold: 013 defines an id twice, 014 has an orphan AC, 015 targets an id nothing defines, 016 names no doc in its scope, 011 has a REQ- with no number.
//     Entries agree with themselves: errors and keepsItems, unique ids per scope (the lowest path keeps a shared one),
//     unique items, a doc over the cap really has more than 2,000 headings.
// Exit codes: 0 ok, 1 a check failed (one line each), 2 a tool error (missing Ajv: run `npm ci --prefix scripts`).

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const dirArg = args.indexOf("--dir");
if (args.length && (args.length !== 2 || dirArg !== 0 || !args[1])) {
  console.error("usage: node scripts/check-library-fixtures.mjs [--dir DIR]");
  process.exit(2);
}
const DIR = dirArg >= 0 ? path.resolve(args[1]) : path.join(HERE, "..", "tests", "fixtures", "library");
const SCHEMA = path.join(HERE, "..", "schemas", "library", "design-doc.schema.json");

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-library-fixtures: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

const CODES = Array.from({ length: 10 }, (_, i) => `SND-LIB-0${10 + i}`);
const KEEP = new Set(["SND-LIB-012", "SND-LIB-015", "SND-LIB-016"]);
const REASONS = new Set(["near-miss", "no-front-matter", "symlink", "dot-folder", "deny"]);

// The strict front-matter reader. Returns { fm } or { fm: null, why }. Scalars: "double" or 'single' quoted text is a string;
// an unquoted integer is a number; anything else (1.0, yes) is a string, because the line as written decides.
function scalar(raw) {
  const s = raw.trim();
  if (/^"(?:[^"\\]|\\.)*"$/.test(s)) return JSON.parse(s);
  if (/^'[^']*'$/.test(s)) return s.slice(1, -1);
  if (/^-?\d+$/.test(s)) return Number(s);
  return s;
}
function readFrontMatter(text) {
  const lines = text.split("\n");
  if (lines[0] !== "---") return { fm: null, why: lines[0] === "" ? "blank first line" : "no front matter on line 1" };
  const end = lines.indexOf("---", 1);
  if (end < 0) return { fm: null, why: "front matter is never closed" };
  const fm = {};
  for (const [i, line] of lines.slice(1, end).entries()) {
    if (line.trim() === "" || /^\s*#/.test(line)) continue;
    const m = line.match(/^([A-Za-z][\w-]*):\s*(.*?)\s*$/);
    if (!m) return { fm: null, why: `front matter line ${i + 2} is not 'key: value'` };
    let v = m[2].replace(/(^|\s)#.*$/, "");
    if (/^\[.*\]$/.test(v)) {
      const inner = v.slice(1, -1).trim();
      fm[m[1]] = inner === "" ? [] : inner.split(",").map(scalar);
    } else fm[m[1]] = scalar(v);
  }
  return { fm };
}
const head4k = (text) => Buffer.from(text, "utf8").subarray(0, 4096).toString("utf8");

// The item-bearing lines of a doc body (headings ## to ####, list items, table rows), outside fences and quotes.
function scanItems(text) {
  const ev = [];
  const lines = text.split("\n");
  let i = 0, fence = null;
  if (lines[0] === "---") { const end = lines.indexOf("---", 1); if (end > 0) i = end + 1; }
  for (; i < lines.length; i++) {
    const ln = lines[i], n = i + 1, f = ln.match(/^ {0,3}(```|~~~)/);
    if (fence) { if (f && f[1] === fence) fence = null; continue; }
    if (f) { fence = f[1]; continue; }
    if (/^\s*>/.test(ln)) continue;
    let m;
    if ((m = ln.match(/^(#{2,4})\s+(.*)$/))) ev.push({ type: "h", text: m[2], line: n });
    else if ((m = ln.match(/^\s*(?:[-*]|\d+\.)\s+(.*)$/))) ev.push({ type: "li", text: m[1], line: n });
    else if (/^\|/.test(ln)) {
      const cells = ln.trim().replace(/^\||\|$/g, "").split("|").map((c) => c.trim());
      if (!cells.every((c) => /^:?-+:?$/.test(c))) ev.push({ type: "tr", text: cells[0], cells, line: n });
    }
  }
  return ev;
}
const idOf = (t) => (t.replace(/\*\*/g, "").match(/^(Ph\d+(?:[a-z]|-\d+)?|REQ-[UESOW]?\d+|AC-\d+|TP-\d+|TASK-\d+)(?![A-Za-z0-9-])/) ?? [])[1];

// Walk DIR without following symlinks.
const found = new Map(); // relative path -> { symlink }
function walk(rel) {
  for (const d of fs.readdirSync(path.join(DIR, rel), { withFileTypes: true })) {
    const r = rel ? `${rel}/${d.name}` : d.name;
    if (d.isSymbolicLink()) found.set(r, { symlink: true });
    else if (d.isDirectory()) walk(r);
    else if (d.isFile()) found.set(r, { symlink: false });
  }
}

try {
  const schema = JSON.parse(fs.readFileSync(SCHEMA, "utf8"));
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  ajv.addSchema(schema);
  const frontMatterOk = ajv.getSchema(`${schema.$id}#/$defs/frontMatter`);
  const refOk = ajv.getSchema(`${schema.$id}#/$defs/ref`);

  const expected = JSON.parse(fs.readFileSync(path.join(DIR, "expected.json"), "utf8"));
  walk("");
  const isDoc = (p) => p.endsWith(".md") && p !== "README.md";
  const entries = new Map();
  for (const e of expected.files) {
    if (entries.has(e.path)) fail(e.path, "listed twice in expected.json");
    entries.set(e.path, e);
  }
  const supporting = new Set(expected.supporting ?? []);

  // 1. files vs entries
  for (const p of found.keys()) {
    if (isDoc(p) && !entries.has(p)) fail(p, "is a .md under the fixtures but not in expected.json");
    if (!isDoc(p) && p !== "README.md" && p !== "expected.json" && !supporting.has(p)) fail(p, "is a file under the fixtures but not in expected.json (files or supporting)");
  }
  for (const p of entries.keys()) if (!found.has(p)) fail(p, "is in expected.json but missing on disk");
  for (const p of supporting) if (!found.has(p)) fail(p, "is in expected.json's supporting list but missing on disk");

  // 2. every code has a fixture; the codes map is the entries' errors
  const derived = {};
  for (const e of expected.files) for (const c of e.errors ?? []) (derived[c] ??= []).push(e.path);
  for (const c of CODES) {
    if (!(expected.codes?.[c]?.length)) fail(c, "has no fixture in expected.json's codes map");
    else if (JSON.stringify([...expected.codes[c]].sort()) !== JSON.stringify([...(derived[c] ?? [])].sort())) fail(c, `codes map ${JSON.stringify(expected.codes[c])} does not match the entries' errors ${JSON.stringify(derived[c] ?? [])}`);
  }
  for (const c of Object.keys(expected.codes ?? {})) if (!CODES.includes(c)) fail(c, "is not a code from SND-LIB-010 to SND-LIB-019");

  const deny = (expected.vault?.deny ?? []).map((g) => g.replace(/\/\*\*$/, "/"));
  const denied = (p) => p.startsWith("vault/") && deny.some((d) => p.slice("vault/".length).startsWith(d));
  const dotted = (p, rootDir) => p.slice(rootDir.length + 1).split("/").slice(0, -1).some((s) => s.startsWith("."));

  const docs = [];
  for (const e of expected.files) {
    const p = e.path;
    const f = found.get(p);
    if (!f) continue;
    const errs = e.errors ?? [];
    for (const c of errs) if (!CODES.includes(c)) fail(p, `names ${c}, which is not SND-LIB-010..019`);
    // 7. self-consistency
    const keeps = errs.every((c) => KEEP.has(c));
    if (!!e.keepsItems !== (e.read && keeps)) fail(p, `keepsItems is ${e.keepsItems} but errors ${JSON.stringify(errs)} and read=${e.read} say ${e.read && keeps}`);
    if (!e.keepsItems && (e.items ?? []).length) fail(p, "keepsItems is false but items are listed");
    if (new Set(e.items ?? []).size !== (e.items ?? []).length) fail(p, "lists an item id twice");
    for (const a of Object.keys(e.parents ?? {})) if (!(e.items ?? []).includes(a)) fail(p, `parents names ${a}, which is not one of its items`);
    for (const k of Object.keys(e.frontMatterLinks ?? {})) if (!["parent", "supersedes"].includes(k)) fail(p, `frontMatterLinks has ${k}, want parent or supersedes`);
    if (!e.why) fail(p, "has no why");

    const rootDir = (expected.roots?.[e.root]?.dir) ?? e.path.split("/")[0];
    if (e.root === "vault" && (dotted(p, rootDir) || denied(p)) && e.read) fail(p, "sits in a vault dot folder or deny glob but is read");

    if (!e.read) {
      if (!REASONS.has(e.skipReason)) { fail(p, `is skipped with reason ${JSON.stringify(e.skipReason)}, want one of ${[...REASONS].join(", ")}`); continue; }
      if ((e.skipReason === "symlink") !== f.symlink) fail(p, `skipReason ${e.skipReason} but the file is ${f.symlink ? "" : "not "}a symlink`);
      if (f.symlink) continue;
      const text = fs.readFileSync(path.join(DIR, p), "utf8");
      const { fm } = readFrontMatter(text);
      if (e.skipReason === "dot-folder" && !dotted(p, rootDir)) fail(p, "skipReason dot-folder but no dot folder in the path");
      if (e.skipReason === "deny" && !denied(p)) fail(p, "skipReason deny but no deny glob matches");
      if (e.skipReason === "no-front-matter" && fm && "aw-doc" in fm) fail(p, "skipReason no-front-matter but the file has aw-doc front matter");
      if (e.skipReason === "near-miss") {
        // 4. a near miss: aw-doc present in the first 4 KiB but not the number 1 (or after a blank first line)
        if (!e.nearMiss) fail(p, "skipReason near-miss without nearMiss: true");
        const blank = text.startsWith("\n") && /^aw-doc:/m.test(head4k(text));
        const notOne = fm && "aw-doc" in fm && fm["aw-doc"] !== 1;
        if (!blank && !notOne) fail(p, "is listed as a near miss but has aw-doc: 1 on line 1 (or no aw-doc at all)");
        if (!errs.includes("SND-LIB-019")) fail(p, "is a near miss without SND-LIB-019");
      } else if (e.nearMiss) fail(p, "nearMiss: true with another skipReason");
      if (!["near-miss"].includes(e.skipReason) && errs.length) fail(p, "is skipped for a reason other than a near miss but lists errors");
      continue;
    }
    if (f.symlink) { fail(p, "is a symlink but listed as read"); continue; }
    const text = fs.readFileSync(path.join(DIR, p), "utf8");
    const { fm, why } = readFrontMatter(text);
    if (e.nearMiss) fail(p, "nearMiss: true on a doc that is read");
    if (!fm) { fail(p, `is read but its front matter cannot be read (${why})`); continue; }
    const valid = frontMatterOk(fm);
    if (errs.includes("SND-LIB-010")) {
      if (valid) fail(p, "SND-LIB-010 fixture: its front matter passes $defs/frontMatter, so it breaks no key");
      else {
        const at = [...new Set(frontMatterOk.errors.map((x) => x.instancePath))];
        if (!e.badKey) fail(p, "SND-LIB-010 fixture names no badKey (the instance path it fails at)");
        else if (at.length !== 1 || at[0] !== e.badKey) fail(p, `SND-LIB-010 fixture fails at ${at.join(", ")}, want only ${e.badKey}`);
      }
    } else if (!valid) {
      fail(p, `front matter fails $defs/frontMatter: ${ajv.errorsText(frontMatterOk.errors)}`);
    }
    docs.push({ e, fm, text });
    // front matter links (parent, supersedes) are not refs; refs validate against $defs/ref and point at their own line
    for (const k of ["parent", "supersedes"]) if (fm[k] !== e.frontMatterLinks?.[k]) fail(p, `frontMatterLinks.${k} is ${JSON.stringify(e.frontMatterLinks?.[k])} but the front matter has ${JSON.stringify(fm[k])}`);
    const flines = text.split("\n");
    for (const r of e.refs ?? []) {
      if (!refOk(r)) { fail(p, `ref ${JSON.stringify(r)} fails $defs/ref: ${ajv.errorsText(refOk.errors)}`); continue; }
      if (r.line > flines.length) fail(p, `ref ${r.from}->${r.to} is on line ${r.line}, past the end (${flines.length} lines)`);
      else if (!flines[r.line - 1].includes(r.to.split("#").pop())) fail(p, `ref ${r.from}->${r.to}: line ${r.line} does not mention ${r.to.split("#").pop()}`);
    }

    // 5. generated docs
    if (fm.generated !== undefined) {
      if (!e.source) fail(p, "is generated but names no source in expected.json");
      else if (!found.has(e.source)) fail(p, `source ${e.source} is missing`);
      else {
        const sha = crypto.createHash("sha256").update(fs.readFileSync(path.join(DIR, e.source))).digest("hex");
        const same = fm["source-sha256"] === sha;
        if (errs.includes("SND-LIB-017") && same) fail(p, "SND-LIB-017 fixture: source-sha256 still matches its source");
        if (!errs.includes("SND-LIB-017") && !same) fail(p, `source-sha256 ${fm["source-sha256"]} is not the sha256 of ${e.source} (${sha}); regenerate the doc`);
      }
    } else if (e.source) fail(p, "names a source but the doc has no generated: key");
    if (errs.includes("SND-LIB-017") && fm.generated === undefined) fail(p, "SND-LIB-017 fixture is not a generated doc");
    // too large by items
    if (errs.includes("SND-LIB-018")) {
      const n = text.split("\n").filter((l) => /^#{2,4}\s/.test(l)).length;
      if (n <= 2000 && Buffer.byteLength(text) <= 1048576) fail(p, `SND-LIB-018 fixture has ${n} headings and fits in 1 MiB, so it is not too large`);
    }
  }

  // unique doc ids per scope: the lowest path keeps a shared id, the others have SND-LIB-012
  const byScope = new Map();
  for (const d of docs) {
    if (d.e.errors.includes("SND-LIB-010") || typeof d.fm.id !== "string") continue;
    const k = `${d.e.scope} ${d.fm.id}`;
    (byScope.get(k) ?? byScope.set(k, []).get(k)).push(d.e.path);
  }
  for (const [k, paths] of byScope) {
    paths.sort();
    paths.forEach((p, i) => {
      const has012 = entries.get(p).errors.includes("SND-LIB-012");
      if (i === 0 && has012) fail(p, `has SND-LIB-012 but is the lowest path with id ${k.split(" ")[1]} in ${k.split(" ")[0]}`);
      if (i > 0 && !has012) fail(p, `shares id ${k.split(" ")[1]} in ${k.split(" ")[0]} with ${paths[0]} but lacks SND-LIB-012`);
    });
  }

  // 7b. the body codes: the fixture really holds the problem its code names
  const withCode = (c) => docs.filter((d) => d.e.errors.includes(c));
  for (const d of withCode("SND-LIB-013")) {
    const ev = scanItems(d.text);
    const dup = (type) => { const seen = new Set(); return ev.filter((x) => x.type === type).map((x) => idOf(x.text)).filter(Boolean).some((id) => seen.size === seen.add(id).size); };
    if (!dup("h") && !dup("li")) fail(d.e.path, "SND-LIB-013 fixture defines no id twice (two headings or two list items starting with it)");
  }
  for (const d of withCode("SND-LIB-014")) {
    const ev = scanItems(d.text);
    const orphan = ev.some((x, i) => {
      const id = x.type !== "h" && idOf(x.text);
      if (!id || !id.startsWith("AC-")) return false;
      let j = i - 1;
      while (j >= 0 && ev[j].type !== "h") { if ((idOf(ev[j].text) ?? "").startsWith("REQ-")) return false; j--; }
      return !ev.some((r) => r.type === "tr" && (idOf(r.text) ?? "").startsWith("REQ-") && r.cells.slice(1).some((c) => c.includes(id)));
    });
    if (!orphan) fail(d.e.path, "SND-LIB-014 fixture has no AC without a requirement above it in its section and without a Traceability row");
  }
  for (const d of withCode("SND-LIB-015")) {
    if (!(d.e.refs ?? []).length) { fail(d.e.path, "SND-LIB-015 fixture asserts no ref"); continue; }
    for (const r of d.e.refs) {
      const target = r.to.split("#").pop();
      const holder = docs.find((o) => o.e.scope === d.e.scope && scanItems(o.text).some((x) => idOf(x.text) === target));
      if (holder) fail(d.e.path, `SND-LIB-015 fixture: ${target} is defined in ${holder.e.path}, so the ref is not broken`);
    }
  }
  for (const d of withCode("SND-LIB-016")) {
    const vals = Object.values(d.e.frontMatterLinks ?? {}).filter((v) => !v.startsWith("doc:"));
    if (!vals.length) fail(d.e.path, "SND-LIB-016 fixture has no parent or supersedes");
    else if (!vals.some((v) => !docs.some((o) => o.e.scope === d.e.scope && o.fm.id === v))) fail(d.e.path, "SND-LIB-016 fixture: its parent and supersedes all match a doc id in the scope");
  }
  for (const d of withCode("SND-LIB-011")) {
    if (!scanItems(d.text).some((x) => /^\*{0,2}REQ-(?![UESOW]?\d)/.test(x.text))) fail(d.e.path, "SND-LIB-011 fixture has no REQ- without a number");
  }

  // 6. the vault covers each reason
  const vault = expected.files.filter((e) => e.root === "vault" && e.scope === "vault");
  for (const r of ["symlink", "no-front-matter", "deny", "dot-folder"]) if (!vault.some((e) => e.skipReason === r)) fail("vault", `has no fixture skipped as ${r}`);
  if (!vault.some((e) => e.read)) fail("vault", "has no doc that is read");
  if (!expected.files.some((e) => e.path.includes(".obsidian/")) && ![...supporting].some((p) => p.includes(".obsidian/"))) fail("vault", "has no .obsidian/ file");
  if (expected.files.filter((e) => e.nearMiss).length < 3) fail("expected.json", "has fewer than 3 near misses (aw-doc \"1\", 1.0, a blank first line)");
} catch (e) {
  console.error(`check-library-fixtures: ${e.message}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`check-library-fixtures: ${failures.length} failure(s)`);
  process.exit(1);
}
console.log("check-library-fixtures: ok");
