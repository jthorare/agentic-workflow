#!/usr/bin/env node
// scripts/check-skill-blocks.mjs — checks every skills/*/block.yaml, the Sindri block manifests (spec
// 2026-11-05-sindri-skill-flows-design.md §3.1 "Checks"):
//
//   node scripts/check-skill-blocks.mjs [--root DIR]
//
// 1. Each manifest matches schemas/skill-block.schema.json (Ajv 2020), and sits beside a SKILL.md.
// 2. Block ids are unique across all manifests.
// 3. Every {name} slot and every { flag, input } in `args` is a declared input, and every required input appears.
// 4. `outcomes.success` is one of the mapped outcomes (with a `map`); `values` sit on text inputs only.
// 5. Every output's `from` names a top-level key, or an `artifacts[<file name>]`, of the SKILL.md's Result-block
//    ```aw-result example (which must exist once, parse as JSON, and name the skill).
// 6. Derived fact keys (`fact:<idCamel><Output>`) are unique across all manifests and at most 64 characters.
// Exit codes: 0 ok, 1 a check failed (one `file: reason` per finding), 2 a tool error (missing Ajv or yaml: run `npm ci --prefix scripts`).

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");
const MAX_FACT_KEY = 64;

const findings = [];
const fail = (where, msg) => findings.push(`${where}: ${msg}`);

let Ajv2020, YAML;
try {
  const req = createRequire(path.join(HERE, "package.json"));
  Ajv2020 = req("ajv/dist/2020").default;
  YAML = req("yaml");
} catch (e) {
  console.error(`check-skill-blocks: Ajv or yaml is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

const camel = (id) => id.replace(/-([a-z0-9])/g, (_, c) => c.toUpperCase());
const cap = (s) => s[0].toUpperCase() + s.slice(1);

/** The JSON of the one ```aw-result fence in SKILL.md's `## Result block` section, or a finding. */
function awResult(skillMd, where) {
  const text = fs.readFileSync(skillMd, "utf8");
  const m = /^## Result block[ \t]*$([\s\S]*?)(?=^## |(?![\s\S]))/m.exec(text);
  if (!m) { fail(where, "SKILL.md has no '## Result block' section"); return null; }
  const fences = [...m[1].matchAll(/^```aw-result[ \t]*\n([\s\S]*?)^```[ \t]*$/gm)];
  if (fences.length !== 1) { fail(where, `the Result block has ${fences.length} aw-result blocks (need exactly one)`); return null; }
  try { return JSON.parse(fences[0][1]); }
  catch (e) { fail(where, `the aw-result block is not valid JSON (${e.message})`); return null; }
}

try {
  const schema = JSON.parse(fs.readFileSync(path.join(ROOT, "schemas/skill-block.schema.json"), "utf8"));
  const validate = new Ajv2020({ strict: false, allErrors: true }).compile(schema);

  const skillsDir = path.join(ROOT, "skills");
  const ids = new Map();   // block id -> where
  const facts = new Map(); // fact key -> where
  const dirs = fs.existsSync(skillsDir) ? fs.readdirSync(skillsDir, { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name).sort() : [];

  for (const dir of dirs) {
    const file = path.join(skillsDir, dir, "block.yaml");
    if (!fs.existsSync(file)) continue;
    const rel = `skills/${dir}/block.yaml`;

    let doc;
    try { doc = YAML.parse(fs.readFileSync(file, "utf8")); }
    catch (e) { fail(rel, `not valid YAML (${String(e.message).split("\n")[0]})`); continue; }

    if (!validate(doc)) {
      for (const err of validate.errors) fail(rel, `schema: ${err.instancePath || "/"} ${err.message}`);
      continue;
    }

    const skillMd = path.join(skillsDir, dir, "SKILL.md");
    let result = null;
    if (!fs.existsSync(skillMd)) fail(rel, "no SKILL.md beside it");
    else {
      result = awResult(skillMd, rel);
      if (result && result.skill !== dir) fail(rel, `the aw-result example says skill '${result.skill}', want '${dir}'`);
    }
    const keys = result ? new Set(Object.keys(result)) : null;
    const artifactNames = result && Array.isArray(result.artifacts) ? new Set(result.artifacts.map((p) => String(p).split("/").pop())) : new Set();

    for (const b of doc.blocks) {
      const at = `${rel} block '${b.id}'`;
      if (ids.has(b.id)) fail(at, `id also used in ${ids.get(b.id)}`); else ids.set(b.id, rel);

      // args: slots and flags name declared inputs; every required input appears.
      const used = new Set();
      for (const item of b.args) {
        const names = typeof item === "string" ? [...item.matchAll(/\{([^{}]*)\}/g)].map((m) => m[1]) : [item.input];
        for (const n of names) {
          if (!(n in b.inputs)) fail(at, `args names '${n}', which is not a declared input`); else used.add(n);
        }
      }
      for (const [n, inp] of Object.entries(b.inputs)) {
        if (inp.required && !used.has(n)) fail(at, `required input '${n}' does not appear in args`);
        if (inp.values && inp.type !== "text") fail(at, `input '${n}' has values but is a ${inp.type} input (values need type text)`);
      }

      // outcomes
      const { map, success } = b.outcomes;
      if (map) {
        if (!Object.values(map).includes(success)) fail(at, `success '${success}' is not one of the mapped outcomes (${[...new Set(Object.values(map))].join(", ")})`);
      }

      // outputs: from names a key or an artifact file name of the aw-result example; outcomes.from a key.
      if (keys && b.outcomes.from && !keys.has(b.outcomes.from)) fail(at, `outcomes.from '${b.outcomes.from}' is not a key of the aw-result block (keys: ${[...keys].join(", ")})`);
      for (const [name, out] of Object.entries(b.outputs)) {
        const am = /^artifacts\[([^\]]+)\]$/.exec(out.from);
        if (keys) {
          if (am) {
            if (!artifactNames.has(am[1])) fail(at, `output '${name}' reads artifacts[${am[1]}], which no path in the aw-result example's artifacts ends in (it has: ${[...artifactNames].join(", ") || "none"})`);
          } else if (!keys.has(out.from)) fail(at, `output '${name}' reads '${out.from}', which is not a key of the aw-result block (keys: ${[...keys].join(", ")})`);
        }
        const fk = `fact:${camel(b.id)}${cap(name)}`;
        if (fk.length > MAX_FACT_KEY) fail(at, `fact key '${fk}' is ${fk.length} characters (max ${MAX_FACT_KEY})`);
        if (facts.has(fk)) fail(at, `fact key '${fk}' is also derived in ${facts.get(fk)}`); else facts.set(fk, at);
      }
    }
  }
} catch (e) {
  console.error(`check-skill-blocks: ${e.message}`);
  process.exit(2);
}

if (findings.length) {
  for (const f of findings) console.log(f);
  process.exit(1);
}
console.log("check-skill-blocks: ok");
