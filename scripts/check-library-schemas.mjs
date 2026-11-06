#!/usr/bin/env node
// scripts/check-library-schemas.mjs — checks the design-doc schema (PR review page spec 2026-11-04, §4 and Ph0):
//
//   node scripts/check-library-schemas.mjs [--root DIR]
//
// 1. Every schemas/library/*.json compiles under Ajv 8's Ajv2020.
// 2. schemas/library/examples/manifest.json lists every example file, each with the verdict its schema must give and its source in the spec.
// 3. An invalid example names the instance path it fails at (failsAt), and the schema fails it there, so it fails for its own rule.
// 4. Every schema has a valid and an invalid example.
// Exit codes: 0 ok, 1 a check failed, 2 a tool error (missing Ajv: run `npm ci --prefix scripts`). One line per failure.

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");
const DIR = "schemas/library";

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);
const readJson = (rel) => JSON.parse(fs.readFileSync(path.join(ROOT, rel), "utf8"));

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-library-schemas: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

try {
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validators = new Map();
  const files = fs.readdirSync(path.join(ROOT, DIR)).filter((f) => f.endsWith(".json"));
  for (const f of files) {
    try {
      validators.set(f, ajv.compile(readJson(`${DIR}/${f}`)));
    } catch (e) {
      fail(`${DIR}/${f}`, `does not compile: ${e.message}`);
    }
  }

  const edir = `${DIR}/examples`;
  const manifest = readJson(`${edir}/manifest.json`);
  const listed = new Set();
  for (const ex of manifest.examples) {
    const where = `${edir}/${ex.file}`;
    if (listed.has(ex.file)) fail(where, "listed twice in manifest.json");
    listed.add(ex.file);
    const validate = validators.get(ex.schema);
    if (!validate) { fail(where, `names ${ex.schema}, which is not a schema here`); continue; }
    if (!fs.existsSync(path.join(ROOT, where))) { fail(where, "listed in manifest.json but missing"); continue; }
    if (!ex.source) fail(where, "names no source in the spec");
    const ok = validate(readJson(where));
    if (ok !== ex.valid) {
      fail(where, `expected ${ex.valid ? "valid" : "invalid"} against ${ex.schema}, schema says ${ok ? "valid" : "invalid"}${ok ? "" : ` (${ajv.errorsText(validate.errors)})`}`);
      continue;
    }
    if (ex.valid) continue;
    if (typeof ex.failsAt !== "string") { fail(where, "an invalid example names no failsAt (the instance path it fails at)"); continue; }
    const paths = [...new Set(validate.errors.map((e) => e.instancePath))];
    if (!paths.includes(ex.failsAt)) fail(where, `expected to fail at ${ex.failsAt || "/"}, fails at ${paths.join(", ") || "/"}`);
  }
  for (const f of fs.readdirSync(path.join(ROOT, ...edir.split("/")))) if (f !== "manifest.json" && !listed.has(f)) fail(`${edir}/${f}`, "not listed in manifest.json");
  for (const f of files) {
    if (!manifest.examples.some((e) => e.schema === f && e.valid)) fail(`${DIR}/${f}`, "has no valid example");
    if (!manifest.examples.some((e) => e.schema === f && !e.valid)) fail(`${DIR}/${f}`, "has no invalid example");
  }
} catch (e) {
  console.error(`check-library-schemas: ${e.message}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`check-library-schemas: ${failures.length} failure(s)`);
  process.exit(1);
}
console.log("check-library-schemas: ok");
