#!/usr/bin/env node
// scripts/check-tool-examples.mjs — checks the registered Sindri MCP tool examples (issue 4249):
//
//   node scripts/check-tool-examples.mjs [--root DIR]
//
// The schemas are the last-known tools/list golden, tests/Aw.Sindri.Tests/Golden/Mcp/tools-list/default.json: the C#
// tests pin it to what the running Sindri serves, so this check needs no Sindri. Every line of output says which
// golden it read and at which commit (`last-known: <path> at <commit>`).
//
// 1. config/tool-examples/sindri-mcp.json holds { examples: [{ tool, args, source: "<file>:<line>" }] }. Each example's
//    tool is in the golden (else "unknown: ... is not in the last-known golden"), its args pass the tool's inputSchema
//    (required fields, unknown fields, types, enums; Ajv 2020), and its source line exists and names the tool.
// 2. Every `mcp: sindri/<tool>` in skills/ and bootstrap/ (*.md) names a tool in the golden, and has an example whose
//    source is that file:line.
// A finding names the place, the field and the field names the tool takes, never the schema or its descriptions.
// A missing golden or examples file is "unknown" and fails: absence is never a pass.
// Exit codes: 0 ok, 1 findings (one per line), 2 usage or a tool error (Ajv missing: run `npm ci --prefix scripts`).

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const GOLDEN = "tests/Aw.Sindri.Tests/Golden/Mcp/tools-list/default.json";
const EXAMPLES = "config/tool-examples/sindri-mcp.json";
const SCAN_DIRS = ["skills", "bootstrap"];
const REF = /mcp:\s*sindri\/([A-Za-z0-9_]+)/g;

function usage(msg) {
  console.error(`check-tool-examples: ${msg}`);
  console.error("usage: node scripts/check-tool-examples.mjs [--root DIR]");
  process.exit(2);
}
let ROOT = path.join(HERE, "..");
const argv = process.argv.slice(2);
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === "--root") {
    if (i + 1 >= argv.length || argv[i + 1].startsWith("--")) usage("--root needs a directory");
    ROOT = path.resolve(argv[++i]);
  } else if (argv[i] === "-h" || argv[i] === "--help") {
    console.log("usage: node scripts/check-tool-examples.mjs [--root DIR]  (exit 0 ok, 1 findings, 2 usage or Ajv missing)");
    process.exit(0);
  } else usage(`unknown argument: ${argv[i]}`);
}

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-tool-examples: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

const findings = [];
const done = () => {
  for (const f of findings) console.log(f);
  console.log(findings.length ? `check-tool-examples: ${findings.length} finding(s)` : "check-tool-examples: ok");
  process.exit(findings.length ? 1 : 0);
};

// --- the golden ---------------------------------------------------------------------------------------------------------
const goldenPath = path.join(ROOT, GOLDEN);
if (!fs.existsSync(goldenPath)) {
  console.log(`unknown: ${GOLDEN} is missing; there is no last-known schema to check the examples against`);
  process.exit(1);
}
let commit = "an uncommitted copy";
try {
  const out = execFileSync("git", ["-C", ROOT, "log", "-1", "--format=%h", "--", GOLDEN], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  if (out) commit = out;
} catch { /* not a git tree */ }
console.log(`last-known: ${GOLDEN} at ${commit}`);

let tools;
try {
  const list = JSON.parse(fs.readFileSync(goldenPath, "utf8"));
  const arr = Array.isArray(list) ? list : list?.tools;
  if (!Array.isArray(arr)) throw new Error("not a list of tools");
  tools = new Map(arr.map((t) => [t.name, t.inputSchema ?? { type: "object" }]));
} catch (e) {
  console.log(`unknown: ${GOLDEN} could not be read (${e.message})`);
  process.exit(1);
}

const ajv = new Ajv2020({ strict: false, allErrors: true });
const validators = new Map();
const validatorFor = (name) => {
  if (!validators.has(name)) validators.set(name, ajv.compile(tools.get(name)));
  return validators.get(name);
};
/** "q, node, key (required)": the field names only. */
const takes = (name) => {
  const s = tools.get(name);
  const req = new Set(s.required ?? []);
  const names = Object.keys(s.properties ?? {}).map((k) => (req.has(k) ? `${k} (required)` : k));
  return `${name} takes ${names.length ? names.join(", ") : "no fields"}`;
};
const field = (e) => e.instancePath.replace(/^\//, "").replace(/\//g, ".");
function describe(e) {
  switch (e.keyword) {
    case "additionalProperties": return `unknown field "${[field(e), e.params.additionalProperty].filter(Boolean).join(".")}"`;
    case "required": return `missing required field "${[field(e), e.params.missingProperty].filter(Boolean).join(".")}"`;
    case "type": return `field "${field(e) || "(args)"}" must be ${e.params.type}`;
    case "enum": return `field "${field(e)}" must be one of ${e.params.allowedValues.join(", ")}`;
    default: return `field "${field(e) || "(args)"}" ${e.message}`;
  }
}

// --- the source files ---------------------------------------------------------------------------------------------------
const lineCache = new Map();
const linesOf = (rel) => {
  if (!lineCache.has(rel)) {
    const p = path.join(ROOT, rel);
    lineCache.set(rel, fs.existsSync(p) && fs.statSync(p).isFile() ? fs.readFileSync(p, "utf8").split("\n") : null);
  }
  return lineCache.get(rel);
};

// --- 1. the examples ----------------------------------------------------------------------------------------------------
const exPath = path.join(ROOT, EXAMPLES);
let examples = [];
if (!fs.existsSync(exPath)) {
  findings.push(`unknown: ${EXAMPLES} is missing; no example is registered`);
} else {
  try {
    const doc = JSON.parse(fs.readFileSync(exPath, "utf8"));
    if (!Array.isArray(doc?.examples)) throw new Error('it has no "examples" list');
    examples = doc.examples;
  } catch (e) {
    findings.push(`${EXAMPLES}: not a valid examples file (${e.message})`);
  }
}
const covered = new Set(); // "<file>:<line> <tool>"
examples.forEach((ex, i) => {
  const tool = typeof ex?.tool === "string" ? ex.tool : "?";
  const source = typeof ex?.source === "string" ? ex.source : "?";
  const where = `${EXAMPLES}: examples[${i}] (${tool}, from ${source})`;
  if (tool === "?" || source === "?" || ex.args === null || typeof ex.args !== "object" || Array.isArray(ex.args)) {
    findings.push(`${where}: needs a string tool, an args object and a string source`);
    return;
  }
  const m = /^(.+):(\d+)$/.exec(source);
  const lines = m ? linesOf(m[1]) : null;
  if (!m) findings.push(`${where}: the source must be <file>:<line>`);
  else if (!lines || Number(m[2]) < 1 || Number(m[2]) > lines.length) findings.push(`${where}: the source line does not exist`);
  else if (!lines[Number(m[2]) - 1].includes(tool)) findings.push(`${where}: the source line does not name ${tool}`);
  else covered.add(`${source} ${tool}`);
  if (!tools.has(tool)) {
    findings.push(`${where}: unknown: ${tool} is not in the last-known golden`);
    return;
  }
  const validate = validatorFor(tool);
  if (!validate(ex.args)) {
    for (const e of validate.errors) findings.push(`${where}: ${describe(e)}; ${takes(tool)}`);
  }
});

// --- 2. the call sites in skills/ and bootstrap/ ------------------------------------------------------------------------
function* markdown(dir) {
  if (!fs.existsSync(dir)) return;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    if (ent.name === "node_modules" || ent.name.startsWith(".")) continue;
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) yield* markdown(p);
    else if (ent.isFile() && ent.name.endsWith(".md")) yield p;
  }
}
let sites = 0;
for (const d of SCAN_DIRS) {
  for (const file of markdown(path.join(ROOT, d))) {
    const rel = path.relative(ROOT, file).split(path.sep).join("/");
    linesOf(rel).forEach((line, n) => {
      for (const r of line.matchAll(REF)) {
        sites++;
        const at = `${rel}:${n + 1}`;
        if (!tools.has(r[1])) findings.push(`${at}: mcp: sindri/${r[1]} names no tool in the last-known golden`);
        else if (!covered.has(`${at} ${r[1]}`)) findings.push(`${at}: mcp: sindri/${r[1]} has no example in ${EXAMPLES}`);
      }
    });
  }
}
console.log(`checked ${examples.length} example(s) and ${sites} call site(s)`);
done();
