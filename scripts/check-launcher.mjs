#!/usr/bin/env node
// scripts/check-launcher.mjs — checks the launcher.json contract (spec 2026-10-15-sindri-session-launcher-design.md §4, §8, SL-0):
//
//   node scripts/check-launcher.mjs [--root DIR]
//
// 1. schemas/launcher.schema.json compiles under Ajv 8's Ajv2020 with strict: false and carries an x-aw-keys block
//    (an unknown keyword to 2020-12, which is why strict is off).
// 2. Every file in tests/fixtures/launcher/ (except manifest.json) is listed in manifest.json and has the expected
//    schema verdict; a file that does not parse is invalid. The manifest's `bash` tags are well formed.
// 3. config/launcher/builtins.json (each agent, no "override") and the argv of every entry in config/launcher/terminals.json
//    (as an exec-template viewer) validate against the schema.
// 4. docs/csharp/sindri-launcher.md shows each terminals.json argv as its worked example.
// 4b. Each golden in tests/fixtures/launcher/argv-hash/ names its inputs (a built-in, with an optional launcher.json override,
//    or a full entry); its canonical text and argvHash equal the RFC 8785 (JCS) form and SHA-256 of the merged entry (spec §2).
// 5. x-aw-keys agrees with the schema (key lists, placeholder set).
// Exit codes: 0 ok, 1 a check failed, 2 a tool error (missing Ajv: run `npm ci --prefix scripts`). One line per failure.

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);
const readJson = (rel) => JSON.parse(fs.readFileSync(path.join(ROOT, rel), "utf8"));

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-launcher: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

/** RFC 8785 for the values an argv hash holds: objects (keys sorted by UTF-16 code unit), arrays, strings, null. */
export function jcs(v) {
  if (v === null || typeof v === "string") return JSON.stringify(v);
  if (Array.isArray(v)) return `[${v.map(jcs).join(",")}]`;
  if (typeof v === "object") return `{${Object.keys(v).sort().map((k) => `${JSON.stringify(k)}:${jcs(v[k])}`).join(",")}}`;
  throw new Error(`jcs: unsupported value ${typeof v}`);
}

/** The argvHash input (spec §2) of a merged entry: bin basename, open, resume (null included), sessionId, mcp. */
export function hashInput(entry) {
  return {
    bin: path.posix.basename(entry.bin),
    open: entry.open,
    resume: entry.resume ?? null,
    sessionId: entry.sessionId,
    mcp: entry.mcp ?? "none",
  };
}
export const argvHash = (entry) => `sha256:${crypto.createHash("sha256").update(jcs(hashInput(entry)), "utf8").digest("hex")}`;

try {
  const schema = readJson("schemas/launcher.schema.json");
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validate = ajv.compile(schema);
  const keys = schema["x-aw-keys"];
  if (!keys) fail("schemas/launcher.schema.json", "no x-aw-keys block");

  const agentSchema = { $ref: `${schema.$id}#/$defs/agent` };
  const validateAgent = ajv.compile(agentSchema);
  const validateViewer = ajv.compile({ $ref: `${schema.$id}#/$defs/viewer` });

  // 2. fixtures
  const dir = "tests/fixtures/launcher";
  const manifest = readJson(`${dir}/manifest.json`);
  const listed = new Set();
  for (const fx of manifest.fixtures) {
    const where = `${dir}/${fx.file}`;
    if (listed.has(fx.file)) fail(where, "listed twice in manifest.json");
    listed.add(fx.file);
    if (!["valid", "invalid"].includes(fx.schema)) fail(where, `schema tag must be valid or invalid, not ${fx.schema}`);
    if (!["accept", "refuse"].includes(fx.bash)) fail(where, `bash tag must be accept or refuse, not ${fx.bash}`);
    if (!fs.existsSync(path.join(ROOT, where))) { fail(where, "listed in manifest.json but missing"); continue; }
    let verdict;
    try {
      verdict = validate(JSON.parse(fs.readFileSync(path.join(ROOT, where), "utf8"))) ? "valid" : "invalid";
    } catch {
      verdict = "invalid";
    }
    if (verdict !== fx.schema) fail(where, `expected ${fx.schema}, schema says ${verdict}${verdict === "invalid" ? ` (${ajv.errorsText(validate.errors)})` : ""}`);
    if (fx.schema === "valid" && fx.bash !== "accept") fail(where, "a schema-valid fixture must be bash: accept");
  }
  for (const f of fs.readdirSync(path.join(ROOT, dir))) {
    if (f.endsWith(".json") && f !== "manifest.json" && !listed.has(f)) fail(`${dir}/${f}`, "not listed in manifest.json");
  }

  // 3. data files
  const builtins = readJson("config/launcher/builtins.json");
  for (const id of ["claude-code", "codex", "cursor"]) if (!builtins.agents?.[id]) fail("config/launcher/builtins.json", `no ${id} entry`);
  for (const [id, entry] of Object.entries(builtins.agents ?? {})) {
    if ("override" in entry) fail("config/launcher/builtins.json", `${id} must not carry "override"`);
    if (!validateAgent(entry)) fail("config/launcher/builtins.json", `${id}: ${ajv.errorsText(validateAgent.errors)}`);
    if (keys && !new RegExp(keys.agentIdPattern).test(id)) fail("config/launcher/builtins.json", `${id} does not match the agent id pattern`);
  }
  const terminals = readJson("config/launcher/terminals.json");
  for (const t of terminals.terminals ?? []) {
    const viewer = { kind: "exec-template", name: t.name, argv: t.argv };
    if (!validateViewer(viewer)) fail("config/launcher/terminals.json", `${t.id}: ${ajv.errorsText(validateViewer.errors)}`);
    if (!t.argv.includes("{cmd...}")) fail("config/launcher/terminals.json", `${t.id}: argv has no {cmd...}`);
    if (!t.titleless && !t.argv.some((a) => a.includes("{title}"))) fail("config/launcher/terminals.json", `${t.id}: argv has no {title}`);
  }

  const doc = fs.readFileSync(path.join(ROOT, "docs/csharp/sindri-launcher.md"), "utf8");
  for (const t of terminals.terminals ?? []) {
    if (!doc.includes(`[${t.argv.map((a) => JSON.stringify(a)).join(", ")}]`)) fail("docs/csharp/sindri-launcher.md", `${t.id}: the worked example is not config/launcher/terminals.json's argv`);
  }

  // 4. goldens
  const gdir = `${dir}/argv-hash`;
  const goldens = fs.readdirSync(path.join(ROOT, gdir)).filter((f) => f.endsWith(".json"));
  if (goldens.length === 0) fail(gdir, "no goldens");
  for (const f of goldens) {
    const where = `${gdir}/${f}`;
    const g = readJson(where);
    let entry;
    if (g.builtin) {
      entry = { ...builtins.agents[g.builtin] };
      if (g.override) {
        const { override: _o, ...fields } = g.override;
        entry = { ...entry, ...fields };
      }
    } else entry = g.entry;
    if (!entry) { fail(where, "names neither builtin nor entry"); continue; }
    for (const k of g.absent ?? []) if (k in entry) fail(where, `${k} is listed as absent but the entry has it`);
    const canonical = jcs(hashInput(entry));
    if (g.canonical !== canonical) fail(where, `canonical is ${canonical}`);
    if (g.argvHash !== argvHash(entry)) fail(where, `argvHash is ${argvHash(entry)}`);
    if (g.equalsBuiltin) {
      const b = argvHash(builtins.agents[g.equalsBuiltin]);
      if (g.argvHash !== b) fail(where, `should equal ${g.equalsBuiltin}'s hash ${b}`);
    }
  }

  // 5. x-aw-keys against the schema
  if (keys) {
    const sameSet = (what, a, b) => { if (JSON.stringify([...a].sort()) !== JSON.stringify([...b].sort())) fail("schemas/launcher.schema.json", `x-aw-keys.${what} is ${JSON.stringify(a)}, schema has ${JSON.stringify(b)}`); };
    sameSet("root", keys.root, Object.keys(schema.properties));
    sameSet("agent", keys.agent, Object.keys(schema.$defs.agent.properties));
    sameSet("host", keys.host, Object.keys(schema.$defs.host.properties));
    sameSet("reportedId", keys.reportedId, Object.keys(schema.$defs.reportedId.properties));
    sameSet("meta", keys.meta, Object.keys(schema.$defs.meta.properties));
    sameSet("viewerKinds", keys.viewerKinds, schema.$defs.viewer.properties.kind.enum);
    sameSet("hostKinds", keys.hostKinds, schema.$defs.host.properties.kind.enum);
    sameSet("sessionIdModes", keys.sessionIdModes, schema.$defs.agent.properties.sessionId.enum);
    sameSet("usageReaders", keys.usageReaders, schema.$defs.agent.properties.usage.enum);
    sameSet("mcpForms", keys.mcpForms, schema.$defs.agent.properties.mcp.enum);
    sameSet("viewer", keys.viewer, ["kind", "name", "argv"]);
    const pat = schema.$defs.argvElement.anyOf[1].pattern;
    const inPattern = [...pat.match(/\\\{\(\?:([A-Za-z|]+)\)\\\}/)[1].split("|")].map((p) => `{${p}}`);
    sameSet("placeholders", keys.placeholders, [...inPattern, keys.splicePlaceholder]);
    if (keys.agentIdPattern !== schema.$defs.agentId.pattern) fail("schemas/launcher.schema.json", "x-aw-keys.agentIdPattern differs from $defs.agentId");
  }
} catch (e) {
  console.error(`check-launcher: ${e.message}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`check-launcher: ${failures.length} failure(s)`);
  process.exit(1);
}
console.log("check-launcher: ok");
