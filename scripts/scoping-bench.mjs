#!/usr/bin/env node
// scripts/scoping-bench.mjs: the scoping benchmark's runner (context scope spec §7, epic #2959).
//
//   node scripts/scoping-bench.mjs --questions tests/fixtures/scoping/<repo>/questions.yaml [--k 25] [--limit N] [--field plain|question]
//        [--only id,id] [--sindri BIN] [--out DIR] [--json]
//   node scripts/scoping-bench.mjs --questions FILE --check [--root DIR]
//
// --check runs no query: it reads the key and says whether every touch, read and decoy path exists under DIR (default: the repo
// this script is in; pass a clone at the key's `commit` for another repo's key), and whether a path is listed twice. Exit 0 ok, 1 findings.
//
// For each question it runs `sindri context query "<plain>" --json` against the Sindri that AW_SINDRI_HOME / AW_SINDRI_PORT name,
// takes the files the answer names in order, and scores the first K of them against the question's answer key:
//   touch recall  = touch files in the top K / touch files      (a scope that misses the file to edit has failed)
//   recall        = (touch + read) files in the top K / (touch + read) files
//   P@K           = (touch + read) files in the top K / files in the top K
//   decoys        = decoy files in the top K
// The files an answer names, in order: each item's node, the nodes on its path, then its cites' keys and refs. Only file keys count
// ('code:P' and 'file:P' as P, 'repo:<repo>/P' as P, '#L..' dropped); features, concepts and records are not files.
// --out DIR keeps each raw answer as DIR/<id>.json. Exit 0 every question answered, 1 a query failed, 2 usage or a bad questions file.
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parse } from 'yaml';

function usage(msg) {
  if (msg) console.error(`scoping-bench: ${msg}`);
  console.error('usage: node scripts/scoping-bench.mjs --questions FILE [--k N] [--limit N] [--field plain|question] [--only ids] [--sindri BIN] [--out DIR] [--json]');
  console.error('       node scripts/scoping-bench.mjs --questions FILE --check [--root DIR]');
  process.exit(2);
}

const opts = { k: 25, limit: null, field: 'plain', sindri: 'sindri', json: false, check: false, root: resolve(dirname(fileURLToPath(import.meta.url)), '..') };
const argv = process.argv.slice(2);
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  const val = () => (i + 1 < argv.length ? argv[++i] : usage(`${a} needs a value`));
  if (a === '--questions') opts.questions = val();
  else if (a === '--k') opts.k = Number(val());
  else if (a === '--limit') opts.limit = Number(val());
  else if (a === '--field') opts.field = val();
  else if (a === '--only') opts.only = val().split(',').filter(Boolean);
  else if (a === '--sindri') opts.sindri = val();
  else if (a === '--out') opts.out = val();
  else if (a === '--json') opts.json = true;
  else if (a === '--check') opts.check = true;
  else if (a === '--root') opts.root = val();
  else if (a === '-h' || a === '--help') usage();
  else usage(`unknown argument '${a}'`);
}
if (!opts.questions) usage('--questions is required');
if (!Number.isInteger(opts.k) || opts.k < 1) usage('--k must be a whole number of 1 or more');
if (opts.limit !== null && (!Number.isInteger(opts.limit) || opts.limit < 1)) usage('--limit must be a whole number of 1 or more');
if (!['plain', 'question'].includes(opts.field)) usage("--field is 'plain' or 'question'");

let keys;
try {
  keys = parse(readFileSync(opts.questions, 'utf8'));
} catch (e) {
  usage(`can't read ${opts.questions}: ${e.message}`);
}
if (!keys || !Array.isArray(keys.questions) || keys.questions.length === 0) usage(`${opts.questions} has no questions list`);
const paths = (list, id, role) => (list ?? []).map((m) => {
  if (!m || typeof m.path !== 'string' || m.path === '') usage(`${id}: every ${role} entry needs a path`);
  return m.path;
});
if (keys.questions.some((q) => !q || typeof q.id !== 'string')) usage('every question needs an id');
if (opts.only) {
  const unknown = opts.only.filter((id) => !keys.questions.some((q) => q.id === id));
  if (unknown.length) usage(`--only names unknown question(s): ${unknown.join(', ')}`);
}
const questions = keys.questions.filter((q) => !opts.only || opts.only.includes(q.id)).map((q) => {
  if (typeof q[opts.field] !== 'string') usage(`${q.id}: no '${opts.field}' text`);
  const touch = paths(q.touch, q.id, 'touch');
  if (touch.length === 0) usage(`${q.id}: no touch files (a key without the files to edit scores nothing)`);
  return { id: q.id, text: q[opts.field], touch, read: paths(q.read, q.id, 'read'), decoys: paths(q.decoys, q.id, 'decoys') };
});
if (opts.check) {
  const findings = [];
  for (const q of questions) {
    const all = [...q.touch.map((p) => ['touch', p]), ...q.read.map((p) => ['read', p]), ...q.decoys.map((p) => ['decoys', p])];
    const seen = new Set();
    for (const [role, p] of all) {
      if (seen.has(p)) findings.push(`${q.id}: ${p} is listed twice`);
      seen.add(p);
      if (!existsSync(join(opts.root, p))) findings.push(`${q.id}: ${role} ${p} does not exist under ${opts.root}`);
    }
  }
  for (const f of findings) console.log(f);
  console.log(findings.length ? `scoping-bench: ${findings.length} finding(s)` : `scoping-bench: ok (${questions.length} question(s))`);
  process.exit(findings.length ? 1 : 0);
}
if (opts.out) mkdirSync(opts.out, { recursive: true });

// One answer key as a repo-relative file path, or null when the key is not a file.
export function fileOf(key) {
  if (typeof key !== 'string') return null;
  let p = key.replace(/#.*$/, '');
  if (p.startsWith('code:') || p.startsWith('file:')) p = p.slice(5);
  else if (p.startsWith('repo:')) p = p.slice(5).replace(/^[^/]+\//, '');
  else if (/^[a-z]+:/.test(p)) return null;   // feature:, concept:, record:, map: ...
  else p = p.replace(/^[^/]+\//, '');            // a cite ref id is '<repo>/P'
  return p === '' ? null : p;
}

function filesOf(answer) {
  const seen = [];
  const add = (k) => { const f = fileOf(k); if (f && !seen.includes(f)) seen.push(f); };
  for (const section of answer.sections ?? []) {
    for (const item of section.items ?? []) {
      add(item.node);
      for (const step of (item.path ?? []).slice(1)) add(step.slice(step.indexOf(' ') + 1));
      for (const c of item.cites ?? []) {
        const cite = answer.cites?.[c];
        if (!cite || String(cite.key).startsWith('map:')) continue;   // the map version cite ('map:<repo>/vN') names no file
        add(cite.key);
        for (const r of cite.refs ?? []) add(r.id);
      }
    }
  }
  return seen;
}

const ratio = (n, d) => (d === 0 ? 0 : n / d);
const rows = [];
let failed = 0;
for (const q of questions) {
  const started = process.hrtime.bigint();
  const run = spawnSync(opts.sindri, ['context', 'query', q.text, ...(opts.limit === null ? [] : ['--limit', String(opts.limit)]), '--json'], { encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
  const seconds = Number(process.hrtime.bigint() - started) / 1e9;
  if (opts.out) writeFileSync(`${opts.out}/${q.id}.json`, JSON.stringify({ rc: run.status, stdout: run.stdout, stderr: run.stderr }));
  let answer = null;
  if (run.status === 0) {
    try { answer = JSON.parse(run.stdout); } catch { answer = null; }
  }
  if (!answer) {
    failed++;
    const why = run.error ? run.error.message : (run.stderr || '').trim().split('\n')[0] || `exit ${run.status}`;
    rows.push({ id: q.id, seconds, error: why.slice(0, 200) });
    continue;
  }
  const files = filesOf(answer);
  const top = files.slice(0, opts.k);
  const inTop = (list) => list.filter((f) => top.includes(f));
  const relevant = [...new Set([...q.touch, ...q.read])];
  rows.push({
    id: q.id, seconds, files: files.length,
    touch: { hit: inTop(q.touch).length, of: q.touch.length }, read: { hit: inTop(q.read).length, of: q.read.length },
    touchRecall: ratio(inTop(q.touch).length, q.touch.length), recall: ratio(inTop(relevant).length, relevant.length),
    precision: ratio(inTop(relevant).length, top.length), decoysHit: inTop(q.decoys),
    missedTouch: q.touch.filter((f) => !top.includes(f)), top5: top.slice(0, 5),
  });
}

const answered = rows.filter((r) => !r.error);
const mean = (f) => ratio(answered.reduce((s, r) => s + f(r), 0), answered.length);
const sorted = rows.map((r) => r.seconds).sort((a, b) => a - b);
const pct = (p) => (sorted.length === 0 ? 0 : sorted[Math.min(sorted.length - 1, Math.ceil(p * sorted.length) - 1)]);
const totals = {
  questions: rows.length, answered: answered.length, failed, k: opts.k, limit: opts.limit,
  touchRecall: mean((r) => r.touchRecall), recall: mean((r) => r.recall), precision: mean((r) => r.precision),
  decoysHit: answered.reduce((s, r) => s + r.decoysHit.length, 0), p50Seconds: pct(0.5), p95Seconds: pct(0.95),
};

const f2 = (x) => x.toFixed(2);
if (opts.json) {
  console.log(JSON.stringify({ questions: opts.questions, field: opts.field, rows, totals }, null, 2));
} else {
  console.log(`# scoping-bench ${opts.questions} · ${opts.field} · top ${opts.k}${opts.limit === null ? '' : ` · reads ${opts.limit}`}`);
  for (const r of rows) {
    if (r.error) { console.log(`${r.id}  ${f2(r.seconds)}s  ERROR ${r.error}`); continue; }
    console.log(`${r.id}  ${f2(r.seconds)}s  files=${r.files}  touch ${r.touch.hit}/${r.touch.of}  read ${r.read.hit}/${r.read.of}  recall=${f2(r.recall)}  P@${opts.k}=${f2(r.precision)}  decoys ${r.decoysHit.length}`
      + (r.missedTouch.length ? `  missed touch: ${r.missedTouch.join(', ')}` : '') + (r.decoysHit.length ? `  decoys hit: ${r.decoysHit.join(', ')}` : ''));
    console.log(`    top 5: ${r.top5.join(', ') || '(no files)'}`);
  }
  console.log(`== ${totals.answered}/${totals.questions} answered · touch recall ${f2(totals.touchRecall)} · recall ${f2(totals.recall)} · P@${opts.k} ${f2(totals.precision)} · decoys ${totals.decoysHit} · p50 ${f2(totals.p50Seconds)}s · p95 ${f2(totals.p95Seconds)}s`);
}
process.exit(failed > 0 ? 1 : 0);
