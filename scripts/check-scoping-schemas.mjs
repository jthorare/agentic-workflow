#!/usr/bin/env node
// scripts/check-scoping-schemas.mjs — checks the scoping station's output schemas (scoping spec 2026-10-15, SC-Y):
//
//   node scripts/check-scoping-schemas.mjs [--root DIR]
//
// 1. Every schemas/scoping/*.json except seeds.json compiles under Ajv 8's Ajv2020.
// 2. schemas/scoping/examples/manifest.json lists every example file, each with the verdict its schema must give.
// 3. Every payload in recipes/sindri/evals/workflow.scoping and workflow.idea-to-brief whose fact has a schema (start inputs and trigger
//    outputs) satisfies it; a string for such a fact is a failure, since the facts are JSON values.
// 4. seeds.json holds exactly the facts recipes/sindri/workflow.scoping.yaml seeds at intake, and each seed that has a schema satisfies it.
// 5. The line fixture's gold scope packets (tests/fixtures/line/gold/scope/*): scope-map, acceptance, challenge and coverage satisfy their schemas, questions the 7.3 shape, every cited id resolves, coverage rows follow SCP-COV-002.
// Exit codes: 0 ok, 1 a check failed, 2 a tool error (missing Ajv: run `npm ci --prefix scripts`). One line per failure.

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(HERE, "..");
const DIR = "schemas/scoping";
const SEED_SCHEMAS = {
  "fact:scopeMap": "scope-map.json",
  "fact:acceptance": "acceptance.json",
  "fact:challenge": "challenge.json",
  "fact:questions": "questions.json",
  "fact:openQuestions": "open-questions.json",
  "fact:gapSearch": "gap-search.json",
};

const FACT_SCHEMAS = {
  "fact:scopeInput": "scope-input.json",
  "fact:brief": "brief.json",
  "fact:scopeMap": "scope-map.json",
  "fact:acceptance": "acceptance.json",
  "fact:questions": "questions.json",
  "fact:openQuestions": "open-questions.json",
  "fact:challenge": "challenge.json",
  "fact:challengeRound": "challenge-round.json",
  "fact:coverage": "coverage.json",
};

const failures = [];
const fail = (where, msg) => failures.push(`${where}: ${msg}`);
const readJson = (rel) => JSON.parse(fs.readFileSync(path.join(ROOT, rel), "utf8"));

let Ajv2020;
try {
  Ajv2020 = createRequire(path.join(HERE, "package.json"))("ajv/dist/2020").default;
} catch (e) {
  console.error(`check-scoping-schemas: Ajv is not installed (${e.code ?? e.message}); run: npm ci --prefix scripts`);
  process.exit(2);
}

try {
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validators = new Map();
  const files = fs.readdirSync(path.join(ROOT, DIR)).filter((f) => f.endsWith(".json") && f !== "seeds.json");
  for (const f of files) {
    try {
      validators.set(f, ajv.compile(readJson(`${DIR}/${f}`)));
    } catch (e) {
      fail(`${DIR}/${f}`, `does not compile: ${e.message}`);
    }
  }

  // 2. examples
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
    const ok = validate(readJson(where));
    if (ok !== ex.valid) fail(where, `expected ${ex.valid ? "valid" : "invalid"} against ${ex.schema}, schema says ${ok ? "valid" : "invalid"}${ok ? "" : ` (${ajv.errorsText(validate.errors)})`}`);
  }
  for (const f of fs.readdirSync(path.join(ROOT, ...edir.split("/")))) if (f !== "manifest.json" && !listed.has(f)) fail(`${edir}/${f}`, "not listed in manifest.json");
  for (const ex of manifest.examples) if (ex.valid && !ex.source) fail(`${edir}/${ex.file}`, "a valid example names its source in the spec");
  for (const f of files) if (!manifest.examples.some((e) => e.schema === f && e.valid)) fail(`${DIR}/${f}`, "has no valid example");

  // 3. eval payloads
  for (const recipeId of ["workflow.scoping", "workflow.idea-to-brief"]) {
    const edir2 = path.join(ROOT, "recipes/sindri/evals", recipeId);
    if (!fs.existsSync(edir2)) { fail(`recipes/sindri/evals/${recipeId}`, "missing"); continue; }
    for (const f of fs.readdirSync(edir2).filter((n) => n.endsWith(".jsonl"))) {
      const where = `recipes/sindri/evals/${recipeId}/${f}`;
      fs.readFileSync(path.join(edir2, f), "utf8").split("\n").filter(Boolean).forEach((text, i) => {
        let line;
        try { line = JSON.parse(text); } catch { fail(`${where}:${i + 1}`, "not JSON"); return; }
        const payloads = Object.entries(line.start?.inputs ?? line.trigger?.outputs ?? {});
        for (const [fact, value] of payloads) {
          const validate = validators.get(FACT_SCHEMAS[fact]);
          if (validate && !validate(value)) fail(`${where}:${i + 1}`, `${fact} does not satisfy ${FACT_SCHEMAS[fact]} (${ajv.errorsText(validate.errors)})`);
        }
      });
    }
  }

  // 4. seeds
  const seeds = readJson(`${DIR}/seeds.json`);
  const recipe = fs.readFileSync(path.join(ROOT, "recipes/sindri/workflow.scoping.yaml"), "utf8");
  const intake = /from: intake, to: read-brief[^\n]*produces: \[([^\]]*)\]/.exec(recipe);
  if (!intake) fail("recipes/sindri/workflow.scoping.yaml", "no intake transition with produces found");
  else {
    const seeded = intake[1].split(",").map((s) => s.trim()).filter(Boolean).sort();
    const have = Object.keys(seeds).sort();
    if (JSON.stringify(seeded) !== JSON.stringify(have)) fail(`${DIR}/seeds.json`, `holds ${have.join(", ")}; the recipe's intake seeds ${seeded.join(", ")}`);
  }
  for (const [fact, schemaFile] of Object.entries(SEED_SCHEMAS)) {
    const validate = validators.get(schemaFile);
    if (!(fact in seeds)) fail(`${DIR}/seeds.json`, `lacks ${fact}`);
    else if (validate && !validate(seeds[fact])) fail(`${DIR}/seeds.json`, `${fact} does not satisfy ${schemaFile} (${ajv.errorsText(validate.errors)})`);
  }

  // 5. the line fixture's gold scope packets: each packet file satisfies its schema (a packet file carries schemaVersion)
  const GOLD_FILES = { "scope-map.json": "scope-map.json", "acceptance.json": "acceptance.json", "challenge.json": "challenge.json", "coverage.json": "coverage.json" };
  const gdir = "tests/fixtures/line/gold/scope";
  if (!fs.existsSync(path.join(ROOT, gdir))) fail(gdir, "missing: the line fixture's gold scope packets");
  else {
    for (const change of fs.readdirSync(path.join(ROOT, gdir))) {
      for (const [file, schemaFile] of Object.entries(GOLD_FILES)) {
        const where = `${gdir}/${change}/${file}`;
        const validate = validators.get(schemaFile);
        if (validate && !validate(readJson(where))) fail(where, `does not satisfy ${schemaFile} (${ajv.errorsText(validate.errors)})`);
      }
      // questions.json is the packet file: every question asked over the rounds (spec 7.2: at most five a round), each a 7.3 question
      const qSchema = readJson(`${DIR}/questions.json`);
      const { maxItems: _perRound, ...allRounds } = qSchema;
      const qwhere = `${gdir}/${change}/questions.json`;
      const qfile = readJson(qwhere);
      if (qfile.schemaVersion !== 1) fail(qwhere, "schemaVersion is not 1");
      // citations resolve (SCP-CITE-001): an L- id in coverage.json's found ids, an A id in answers.jsonl
      const known = new Set(readJson(`${gdir}/${change}/coverage.json`).lenses.flatMap((r) => r.found.ids));
      fs.readFileSync(path.join(ROOT, gdir, change, "answers.jsonl"), "utf8").split("\n").filter(Boolean).forEach((l) => known.add(JSON.parse(l).id));
      const cited = JSON.stringify([readJson(`${gdir}/${change}/scope-map.json`), readJson(`${gdir}/${change}/acceptance.json`), readJson(`${gdir}/${change}/challenge.json`), readJson(qwhere)]);
      for (const id of new Set(cited.match(/"(L-[a-z]+-[0-9]+|A[0-9]+)"/g)?.map((m) => m.slice(1, -1)) ?? [])) if (!known.has(id)) fail(`${gdir}/${change}`, `cites ${id}, which is in no coverage row's found ids or the answer log`);
      // SCP-COV-002: nothing-found needs every source ok; a limited row with nothing found is not-searched
      for (const r of readJson(`${gdir}/${change}/coverage.json`).lenses) {
        const bad = r.searched.sources.some((x) => typeof x === "object" && x.state !== "ok");
        if (r.state === "nothing-found" && (bad || r.limits.length > 0 || r.found.count > 0)) fail(`${gdir}/${change}/coverage.json`, `${r.lens} is nothing-found with a limit, a source that is not ok, or items`);
        if (r.found.count === 0 && r.limits.length > 0 && r.state !== "not-searched") fail(`${gdir}/${change}/coverage.json`, `${r.lens} has a limit and nothing found, so it is not-searched`);
        if (r.state === "found" && r.found.count === 0) fail(`${gdir}/${change}/coverage.json`, `${r.lens} is found with a count of 0`);
      }
      // ids resolve: question surfaces and blocks, acceptance surfaces, and the answers' recordedAs
      const surfaceIds = new Set((readJson(`${gdir}/${change}/scope-map.json`).surfaces ?? []).map((x) => x.id));
      const acceptance = readJson(`${gdir}/${change}/acceptance.json`);
      const acIds = new Set((acceptance.items ?? []).map((x) => x.id));
      if (!Array.isArray(qfile.questions) || qfile.questions.length === 0) fail(qwhere, "questions is empty or not a list");
      for (const q of Array.isArray(qfile.questions) ? qfile.questions : []) {
        for (const s of q.surfaces ?? []) if (!surfaceIds.has(s)) fail(qwhere, `${q.id} names surface ${s}, which is not in scope-map.json`);
        for (const b of q.blocks ?? []) if (!acIds.has(b)) fail(qwhere, `${q.id} blocks ${b}, which is not in acceptance.json`);
      }
      for (const it of acceptance.items ?? []) for (const s of it.surfaces ?? []) if (!surfaceIds.has(s)) fail(`${gdir}/${change}/acceptance.json`, `${it.id} names surface ${s}, which is not in scope-map.json`);
      fs.readFileSync(path.join(ROOT, gdir, change, "answers.jsonl"), "utf8").split("\n").filter(Boolean).forEach((l) => {
        const a = JSON.parse(l);
        for (const r of a.recordedAs ?? []) if (!acIds.has(r) && !surfaceIds.has(r)) fail(`${gdir}/${change}/answers.jsonl`, `${a.id} records ${r}, which is in neither acceptance.json nor scope-map.json`);
      });
      const validateQ = ajv.compile({ ...allRounds, $id: undefined });
      if (!validateQ(qfile.questions)) fail(qwhere, `questions do not satisfy questions.json (${ajv.errorsText(validateQ.errors)})`);
    }
  }
} catch (e) {
  console.error(`check-scoping-schemas: ${e.message}`);
  process.exit(2);
}

if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`check-scoping-schemas: ${failures.length} failure(s)`);
  process.exit(1);
}
console.log("check-scoping-schemas: ok");
