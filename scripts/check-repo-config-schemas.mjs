#!/usr/bin/env node
// scripts/check-repo-config-schemas.mjs — checks the per-repo config schemas (issue #2909, docs/conventions/repo-config.md):
//
//   node scripts/check-repo-config-schemas.mjs [--root DIR]
//
// 1. Every schemas/repo-config/<name>.schema.json compiles under Ajv 8's Ajv2020.
// 2. tests/fixtures/repo-config/acme/.agentic-workflow/<name>.json exists for every schema and passes it, and no other file sits there.
// 3. tests/fixtures/repo-config/invalid/expected.json maps each "<name>/<case>.json" to the JSON pointer it must fail at ("" = the root).
//    Each listed case fails, and fails at that pointer; every file under invalid/ is listed; every schema has an invalid case.
// Exit codes: 0 ok, 1 a check failed, 2 a tool error (missing Ajv: run `npm ci --prefix scripts`). One line per failure.

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
if (rootArg >= 0 && !args[rootArg + 1]) {
  console.error("usage: node scripts/check-repo-config-schemas.mjs [--root DIR]");
  process.exit(2);
}
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");
const SCHEMAS = "schemas/repo-config";
const FIX = "tests/fixtures/repo-config";

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);
const readJson = (rel) => JSON.parse(fs.readFileSync(path.join(ROOT, rel), "utf8"));

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-repo-config-schemas: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

try {
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validators = new Map();
  for (const f of fs.readdirSync(path.join(ROOT, SCHEMAS)).filter((f) => f.endsWith(".schema.json"))) {
    const name = f.replace(/\.schema\.json$/, "");
    try {
      validators.set(name, ajv.compile(readJson(`${SCHEMAS}/${f}`)));
    } catch (e) {
      fail(`${SCHEMAS}/${f}`, `does not compile: ${e.message}`);
    }
  }

  const acme = `${FIX}/acme/.agentic-workflow`;
  for (const [name, validate] of validators) {
    const rel = `${acme}/${name}.json`;
    if (!fs.existsSync(path.join(ROOT, rel))) { fail(rel, "missing: every schema needs a valid acme file"); continue; }
    if (!validate(readJson(rel))) fail(rel, `expected valid, schema says invalid (${ajv.errorsText(validate.errors)})`);
  }
  for (const f of fs.readdirSync(path.join(ROOT, acme))) {
    if (!validators.has(f.replace(/\.json$/, ""))) fail(`${acme}/${f}`, "has no schema");
  }

  const inv = `${FIX}/invalid`;
  const expected = readJson(`${inv}/expected.json`);
  for (const [key, at] of Object.entries(expected)) {
    const where = `${inv}/${key}`;
    const validate = validators.get(key.split("/")[0]);
    if (!validate) { fail(where, "names a schema that does not exist"); continue; }
    if (!fs.existsSync(path.join(ROOT, where))) { fail(where, "listed in expected.json but missing"); continue; }
    if (typeof at !== "string") { fail(where, "expected.json must give a JSON pointer string"); continue; }
    if (validate(readJson(where))) { fail(where, "expected invalid, schema says valid"); continue; }
    const paths = [...new Set(validate.errors.map((e) => e.instancePath))];
    if (!paths.includes(at)) fail(where, `expected to fail at ${at || "(root)"}, fails at ${paths.map((p) => p || "(root)").join(", ")}`);
  }
  for (const dir of fs.readdirSync(path.join(ROOT, inv), { withFileTypes: true }).filter((d) => d.isDirectory())) {
    for (const f of fs.readdirSync(path.join(ROOT, inv, dir.name))) {
      if (!(`${dir.name}/${f}` in expected)) fail(`${inv}/${dir.name}/${f}`, "not listed in expected.json");
    }
  }
  for (const name of validators.keys()) {
    if (!Object.keys(expected).some((k) => k.startsWith(`${name}/`))) fail(`${SCHEMAS}/${name}.schema.json`, "has no invalid case");
  }
} catch (e) {
  console.error(`check-repo-config-schemas: ${e.message}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`check-repo-config-schemas: ${failures.length} failure(s)`);
  process.exit(1);
}
console.log("check-repo-config-schemas: ok");
