#!/usr/bin/env node
// scripts/check-handoff-schema.mjs — checks the handoff inbox contract (schemas/handoff/inbox.openapi.yaml, docs/sindri/handoff.md):
//
//   node scripts/check-handoff-schema.mjs [--root DIR]
//
// 1. The OpenAPI file parses, is 3.1, and has the three operations (listOffers GET /offers, claimOffer POST /offers/{id}/claim,
//    submitResult POST /offers/{id}/result), each with the status codes the contract names.
// 2. components.schemas compiles under Ajv 8's Ajv2020 (OpenAPI 3.1 schema objects are JSON Schema 2020-12).
// 3. Every `examples` value in the file validates against its media type's schema.
// 4. Every file in tests/fixtures/handoff/fake-inbox/ (except manifest.json) is listed in manifest.json and gets the verdict it lists;
//    an invalid one that lists `names` has that word in its error text.
// 5. docs/sindri/handoff.md names each path.
// Exit codes: 0 ok, 1 a check failed, 2 a tool error (missing Ajv or yaml: run `npm ci --prefix scripts`). One line per failure.

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);

let Ajv2020, YAML;
try {
  const req = createRequire(path.join(HERE, "package.json"));
  Ajv2020 = req("ajv/dist/2020").default;
  YAML = req("yaml");
} catch (e) {
  console.error(`check-handoff-schema: Ajv or yaml is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

const SPEC = "schemas/handoff/inbox.openapi.yaml";
const OPERATIONS = [
  { id: "listOffers", method: "get", path: "/offers", codes: ["200", "401", "403"], body: false },
  { id: "claimOffer", method: "post", path: "/offers/{id}/claim", codes: ["200", "401", "403", "404", "409", "410"], body: true },
  { id: "submitResult", method: "post", path: "/offers/{id}/result", codes: ["200", "401", "403", "404", "409", "410"], body: true },
];

/** Rewrites OpenAPI component refs to the $defs of the schema the checker compiles. */
const rewrite = (v) => {
  if (Array.isArray(v)) return v.map(rewrite);
  if (v && typeof v === "object") {
    return Object.fromEntries(Object.entries(v).map(([k, x]) => [k, k === "$ref" && typeof x === "string" ? x.replace("#/components/schemas/", "#/$defs/") : rewrite(x)]));
  }
  return v;
};

try {
  let doc;
  try {
    doc = YAML.parse(fs.readFileSync(path.join(ROOT, SPEC), "utf8"));
  } catch (e) {
    fail(SPEC, `does not parse (${e.message.split("\n")[0]})`);
  }

  if (doc) {
    if (!String(doc.openapi ?? "").startsWith("3.1")) fail(SPEC, `openapi is ${doc.openapi}, want 3.1.x`);

    // 1. operations
    for (const op of OPERATIONS) {
      const o = doc.paths?.[op.path]?.[op.method];
      if (!o) { fail(SPEC, `no ${op.method.toUpperCase()} ${op.path}`); continue; }
      if (o.operationId !== op.id) fail(SPEC, `${op.method.toUpperCase()} ${op.path} has operationId ${o.operationId}, want ${op.id}`);
      for (const code of op.codes) if (!o.responses?.[code]) fail(SPEC, `${op.id} has no ${code} response`);
      if (op.body && !o.requestBody?.content?.["application/json"]?.schema) fail(SPEC, `${op.id} has no JSON request body schema`);
    }
    for (const p of Object.keys(doc.paths ?? {})) if (!OPERATIONS.some((o) => o.path === p)) fail(SPEC, `path ${p} is not part of the contract`);
    const listParams = (doc.paths?.["/offers"]?.get?.parameters ?? []).map((p) => `${p.name}:${p.required === true}`).sort().join(",");
    if (listParams !== "owner:true,pageToken:false") fail(SPEC, `listOffers parameters are ${listParams}, want owner (required) and pageToken (optional)`);

    // 2. schemas compile
    const ajv = new Ajv2020({ strict: false, allErrors: true, validateFormats: false });
    const schemas = doc.components?.schemas ?? {};
    for (const need of ["Link", "Offer", "OfferPage", "ClaimRequest", "ClaimResponse", "ResultRequest", "ResultAccepted"]) {
      if (!schemas[need]) fail(SPEC, `components.schemas.${need} is missing`);
    }
    const ID = "https://agentic-workflow.local/schemas/handoff/inbox";
    const validators = {};
    try {
      ajv.addSchema({ $id: ID, $defs: rewrite(schemas) });
      for (const name of Object.keys(schemas)) validators[name] = ajv.compile({ $ref: `${ID}#/$defs/${name}` });
    } catch (e) {
      fail(SPEC, `components.schemas do not compile (${e.message.split("\n")[0]})`);
    }
    const verdict = (name, value) => {
      const v = validators[name];
      if (!v) return { ok: false, text: `no schema ${name}` };
      const ok = v(value);
      return { ok, text: ok ? "" : ajv.errorsText(v.errors, { dataVar: name }) };
    };

    // 3. examples in the spec
    const walk = (node, where) => {
      const media = node?.content?.["application/json"];
      const ref = media?.schema?.$ref;
      if (media?.examples && ref) {
        const name = ref.split("/").pop();
        for (const [ex, body] of Object.entries(media.examples)) {
          const r = verdict(name, body.value);
          if (!r.ok) fail(SPEC, `${where} example '${ex}' is not a valid ${name}: ${r.text}`);
        }
      }
    };
    for (const op of OPERATIONS) {
      const o = doc.paths?.[op.path]?.[op.method];
      if (!o) continue;
      walk(o.requestBody, `${op.id} request`);
      for (const [code, res] of Object.entries(o.responses ?? {})) walk(res, `${op.id} ${code}`);
    }

    // 4. fixtures
    const dir = "tests/fixtures/handoff/fake-inbox";
    let manifest;
    try {
      manifest = JSON.parse(fs.readFileSync(path.join(ROOT, dir, "manifest.json"), "utf8"));
    } catch (e) {
      fail(`${dir}/manifest.json`, `does not read (${e.message.split("\n")[0]})`);
    }
    if (manifest) {
      const listed = new Set();
      for (const fx of manifest.fixtures ?? []) {
        const where = `${dir}/${fx.file}`;
        listed.add(fx.file);
        let value;
        try {
          value = JSON.parse(fs.readFileSync(path.join(ROOT, where), "utf8"));
        } catch (e) {
          fail(where, `does not read (${e.message.split("\n")[0]})`);
          continue;
        }
        const r = verdict(fx.schema, value);
        if (r.ok !== fx.valid) fail(where, `expected ${fx.valid ? "valid" : "invalid"} ${fx.schema}, got ${r.ok ? "valid" : "invalid"}${r.ok ? "" : ` (${r.text})`}`);
        if (!fx.valid && !r.ok && fx.names && !r.text.includes(fx.names)) fail(where, `error text lacks '${fx.names}': ${r.text}`);
      }
      for (const f of fs.readdirSync(path.join(ROOT, dir))) {
        if (f !== "manifest.json" && !listed.has(f)) fail(`${dir}/${f}`, "not listed in manifest.json");
      }
    }

    // 5. docs
    let docs = "";
    try { docs = fs.readFileSync(path.join(ROOT, "docs/sindri/handoff.md"), "utf8"); } catch { fail("docs/sindri/handoff.md", "missing"); }
    if (docs) for (const op of OPERATIONS) if (!docs.includes(op.path)) fail("docs/sindri/handoff.md", `does not name ${op.path}`);
  }
} catch (e) {
  console.error(`check-handoff-schema: ${e.stack ?? e}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(f);
  process.exit(1);
}
console.log("check-handoff-schema: ok");
