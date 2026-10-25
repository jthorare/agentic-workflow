#!/usr/bin/env node
// THROWAWAY — R0 readiness baseline (docs/superpowers/specs/2026-10-09-agent-readiness-design.md §10, R0).
// Scores the nine agent-readiness pillars per criterion from what this repo shows, and writes a markdown report.
// Replaced by Aw.Readiness in R1b, which becomes the score of record (decision R4). Node >= 20 stdlib only.
//
// Usage: node scripts/readiness-baseline.mjs [--root DIR] [--repo OWNER/NAME] [--no-remote] [--date YYYY-MM-DD] [--out FILE]
// Exit codes: 0 report written, 2 tool error (bad flag, value, root or output path). Remote reads are `gh api`
// GETs only (READINESS_GH_TIMEOUT_MS, a positive integer, bounds each call; default 30000).
// Not the §8 contract: no --json, no delta since the last run, and exit 0/1 does not mean "target level holds".

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { pathToFileURL } from 'node:url';

// The Node/TypeScript apps the per-app criteria score. The toolkit has none since the C# cutover; READINESS_APPS
// (comma-separated repo-relative directories) names some, which is how the fixture tests exercise the per-app code.
const APPS = (process.env.READINESS_APPS ?? '').split(',').map((a) => a.trim()).filter(Boolean);

const PILLARS = [
  'Style & Validation', 'Build System', 'Testing', 'Documentation', 'Development Environment',
  'Debugging & Observability', 'Security', 'Task Discovery', 'Product & Experimentation',
];

const SOURCES = {
  'F-pillar': 'https://docs.factory.com/agent-readiness/overview — criteria examples per pillar (no level given; placed by this script)',
  'F-level': 'https://docs.factory.com/agent-readiness/overview — example criteria per level',
  'S5': 'docs/superpowers/specs/2026-10-09-agent-readiness-design.md §5 — level inferred (Factory gives none)',
  'inferred': 'no published level; placed by this script (see "How it scores")',
};

// GitHub's default label set; a repo with only these has no labeling scheme of its own.
const DEFAULT_LABELS = new Set([
  'bug', 'documentation', 'duplicate', 'enhancement', 'good first issue',
  'help wanted', 'invalid', 'question', 'wontfix',
]);

function die(msg) {
  process.stderr.write(`readiness-baseline: ${msg}\n`);
  process.exit(2);
}

function parseArgs(argv) {
  const opts = { root: null, repo: null, remote: true, date: null, out: null };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const val = () => {
      const v = argv[++i];
      if (v === undefined || v.startsWith('--')) die(`${a} needs a value`);
      return v;
    };
    if (a === '--root') opts.root = val();
    else if (a === '--repo') opts.repo = val();
    else if (a === '--no-remote') opts.remote = false;
    else if (a === '--date') opts.date = val();
    else if (a === '--out') opts.out = val();
    else die(`unknown flag ${a}`);
  }
  return opts;
}

// ---------- filesystem helpers ----------

const SKIP_DIRS = new Set(['node_modules', '.git', 'dist', 'coverage', '.claude', 'bin', 'obj']);

// Sorted, so evidence that names the first match is the same on every filesystem. Symlinks are skipped: a broken
// link would throw and a link back up the tree would recurse forever.
// An unreadable path (a file where a directory was expected, EACCES) contributes nothing instead of aborting.
function walk(dir, out = []) {
  let entries;
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  entries.sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
  for (const e of entries) {
    if (SKIP_DIRS.has(e.name) || e.isSymbolicLink()) continue;
    const p = join(dir, e.name);
    if (e.isDirectory()) walk(p, out); else if (e.isFile()) out.push(p);
  }
  return out;
}

const isFile = (p) => { try { return statSync(p).isFile(); } catch { return false; } };
// { text } when readable, { missing } when absent, { error: code } when it exists but can't be read (EACCES, …).
// Heuristics use read(), which treats an unreadable file as empty; rows that are about the file itself say why.
function readState(p) {
  if (!isFile(p)) return { missing: true };
  try { return { text: readFileSync(p, 'utf8') }; } catch (e) { return { error: e.code ?? String(e) }; }
}
const read = (p) => readState(p).text ?? '';
const firstExisting = (root, paths) => paths.find((p) => existsSync(join(root, p))) ?? null;

// null when there is no package.json; { unreadable: code } or { invalid: true } for a file that exists but can't be
// read or parsed. Both are defects, scored as fail with their own reason.
function pkg(appDir) {
  const r = readState(join(appDir, 'package.json'));
  if (r.missing) return null;
  if (r.error) return { unreadable: r.error };
  try { return JSON.parse(r.text); } catch { return { invalid: true }; }
}
const badPkg = (p) => (p.unreadable ? { pass: false, why: `package.json is not readable (${p.unreadable})` }
  : p.invalid ? { pass: false, why: 'package.json is not valid JSON' } : null);

// JSONC (tsconfig): drops // and /* */ comments outside strings, then trailing commas. undefined if unparsable.
function parseJsonc(text) {
  let out = '';
  let inStr = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    const n = text[i + 1];
    if (inStr) {
      out += c;
      if (c === '\\') { out += n ?? ''; i++; } else if (c === '"') inStr = false;
    } else if (c === '"') { inStr = true; out += c; }
    else if (c === '/' && n === '/') { while (i < text.length && text[i] !== '\n') i++; out += '\n'; }
    else if (c === '/' && n === '*') { const e = text.indexOf('*/', i + 2); i = e < 0 ? text.length : e + 1; }
    else out += c;
  }
  try { return JSON.parse(out.replace(/,(\s*[}\]])/g, '$1')); } catch { return undefined; }
}

// compilerOptions.strict from a tsconfig, following relative `extends` up to 3 levels.
function tsStrict(file, chain = []) {
  const r = readState(file);
  const name = chain.length ? chain[chain.length - 1] : 'tsconfig.json';
  if (r.missing) return { why: `no ${name}` };
  if (r.error) return { why: `${name} is not readable (${r.error})` };
  const cfg = parseJsonc(r.text);
  if (!cfg || typeof cfg !== 'object') return { why: `${name} is not valid JSONC` };
  const strict = cfg.compilerOptions?.strict;
  if (typeof strict === 'boolean') return { strict, chain };
  if (typeof cfg.extends === 'string' && cfg.extends.startsWith('.') && chain.length < 3) {
    return tsStrict(join(dirname(file), cfg.extends), [...chain, cfg.extends]);
  }
  return { strict: false, chain };
}

// Fenced code blocks under a "Commands" heading of a markdown file.
function commandBlocks(md) {
  const blocks = [];
  let inCommands = false;
  let fence = null;
  for (const line of md.split('\n')) {
    if (fence) {
      if (line.startsWith('```')) { blocks.push(fence); fence = null; } else fence.push(line);
      continue;
    }
    const h = /^#{1,6}\s+(.*)$/.exec(line);
    if (h) inCommands = /^commands\b/i.test(h[1]);
    else if (inCommands && line.startsWith('```')) fence = [];
  }
  return blocks;
}
const allDeps = (p) => Object.keys({ ...(p?.dependencies ?? {}), ...(p?.devDependencies ?? {}) });

// ---------- remote (gh api GET only) ----------

let GH_TIMEOUT_MS = 30000; // set from READINESS_GH_TIMEOUT_MS in main(), after validation
const ghCache = new Map();

// `gh api <path>`, optionally with a `--jq` projection; never a method override or a field flag. `array` says which
// JSON shape the endpoint returns; anything else (an API change, a proxy answering 200) is an error, so the row is
// unknown instead of the run crashing.
function ghGet(path, { jq, array = false } = {}) {
  const key = `${path}\0${jq ?? ''}`;
  if (ghCache.has(key)) return ghCache.get(key);
  let r;
  try {
    const args = jq ? ['api', path, '--jq', jq] : ['api', path];
    const out = execFileSync('gh', args, {
      encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 16 * 1024 * 1024, timeout: GH_TIMEOUT_MS,
    });
    const json = JSON.parse(out);
    const shapeOk = array ? Array.isArray(json) : json !== null && typeof json === 'object' && !Array.isArray(json);
    r = shapeOk ? { ok: true, json } : { ok: false, error: 'unexpected response shape' };
  } catch (e) {
    // Only ETIMEDOUT is a timeout; an output overflow (ENOBUFS) also ends in SIGTERM but must say so.
    const timedOut = e.code === 'ETIMEDOUT';
    const msg = timedOut ? `gh timed out after ${GH_TIMEOUT_MS} ms` : String(e.stderr || e.message || e).trim().split('\n').pop();
    r = { ok: false, error: msg };
  }
  ghCache.set(key, r);
  return r;
}

const repoInfo = (ctx) => ghGet(`repos/${ctx.repo}`);

// ---------- criteria ----------
// repo criteria: check(ctx) -> { state, evidence }
// app criteria:  app(ctx, appDir, rel) -> { pass: boolean, why } or { state: 'n/a', why } where it doesn't apply

function workflows(root) {
  const dir = join(root, '.github', 'workflows');
  let names;
  try { names = readdirSync(dir); } catch { return []; }
  return names.filter((f) => /\.ya?ml$/.test(f)).sort().map((f) => join(dir, f)).filter(isFile);
}

const remoteUnknown = (ctx) => ({ state: 'unknown', evidence: ctx.remoteWhy });

function docPresent(ctx, f) {
  const r = readState(join(ctx.root, f));
  if (r.error) return { state: 'unknown', evidence: `${f} is not readable (${r.error})` };
  return (r.text ?? '').trim() ? { state: 'pass', evidence: `${f} present` } : { state: 'fail', evidence: `no ${f}` };
}
const PRODUCT_SIGNALS = 'docs/readiness/product-signals.md';

const CRITERIA = [
  // Style & Validation
  { id: 'SV-1', pillar: 0, level: 1, src: 'F-level', name: 'Linter configured', app: (ctx, d) => {
    const f = firstExisting(d, ['biome.json', 'biome.jsonc', 'eslint.config.js', 'eslint.config.mjs', '.eslintrc.json', '.eslintrc.cjs'])
      ?? firstExisting(ctx.root, ['biome.json', 'biome.jsonc', 'eslint.config.js', 'eslint.config.mjs', '.eslintrc.json']);
    const dep = allDeps(pkg(d)).find((x) => /^(eslint|@biomejs\/biome)$/.test(x));
    return f || dep ? { pass: true, why: f ?? dep } : { pass: false, why: 'no lint config or linter dependency' };
  } },
  { id: 'SV-2', pillar: 0, level: 1, src: 'F-level', name: 'Type checker (strict)', app: (ctx, d) => {
    const r = tsStrict(join(d, 'tsconfig.json'));
    if (r.why) return { pass: false, why: r.why };
    if (!r.strict) return { pass: false, why: 'compilerOptions.strict is not true' };
    return { pass: true, why: r.chain.length ? `compilerOptions.strict via ${r.chain.join(' → ')}` : 'compilerOptions.strict' };
  } },
  { id: 'SV-3', pillar: 0, level: 1, src: 'F-pillar', name: 'Code formatter', app: (ctx, d) => {
    const names = ['biome.json', 'biome.jsonc', '.prettierrc', '.prettierrc.json', 'prettier.config.js', 'dprint.json'];
    const f = firstExisting(d, names) ?? firstExisting(ctx.root, names);
    const dep = allDeps(pkg(d)).find((x) => /^(prettier|@biomejs\/biome|dprint)$/.test(x));
    return f || dep ? { pass: true, why: f ?? dep } : { pass: false, why: 'no formatter config or dependency' };
  } },
  { id: 'SV-4', pillar: 0, level: 2, src: 'F-level', name: 'Pre-commit hooks (tracked config)', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.pre-commit-config.yaml', '.husky', 'lefthook.yml', '.lefthook.yml', '.githooks']);
    return f ? { state: 'pass', evidence: f }
      : { state: 'fail', evidence: 'no tracked hook config (.pre-commit-config.yaml, .husky, lefthook, .githooks); hooks installed per clone are not reproducible' };
  } },

  // Build System
  // Per app: a build script that AGENTS.md documents. The missing single root build command (spec §5 "one-line root
  // commands") is a separate gap, named in the evidence of every app row, not scored here.
  { id: 'BS-1', pillar: 1, level: 2, src: 'S5', name: 'Build command documented', app: (ctx, d, rel) => {
    const p = pkg(d);
    if (!p) return { state: 'n/a', why: 'no package.json; nothing to build (loaded as source)' };
    if (badPkg(p)) return badPkg(p);
    if (!p.scripts?.build) return { pass: false, why: 'no build script' };
    // Documented: a Commands code block lists the package, and either that line names build or the block says
    // each package has a build script.
    const agents = readState(join(ctx.root, 'AGENTS.md'));
    if (agents.error) return { pass: false, why: `AGENTS.md is not readable (${agents.error})` };
    const documented = commandBlocks(agents.text ?? '').some((b) => b.some((l) => l.includes(rel))
      && (b.some((l) => l.includes(rel) && /\bbuild\b/.test(l)) || b.some((l) => /each has [^\n]*\bbuild\b/.test(l))));
    return documented ? { pass: true, why: `heuristic: AGENTS.md Commands block lists ${rel} and build; no single root build command` }
      : { pass: false, why: 'scripts.build not documented in AGENTS.md' };
  } },
  { id: 'BS-2', pillar: 1, level: 2, src: 'S5', name: 'Dependencies pinned (lockfile)', app: (ctx, d) => {
    const p = pkg(d);
    if (!p) return { state: 'n/a', why: 'no package.json, no dependencies to pin' };
    if (badPkg(p)) return badPkg(p);
    const f = firstExisting(d, ['package-lock.json', 'pnpm-lock.yaml', 'yarn.lock', 'packages.lock.json']);
    return f ? { pass: true, why: f } : { pass: false, why: 'no lockfile' };
  } },
  { id: 'BS-3', pillar: 1, level: 2, src: 'inferred', name: 'VCS CLI tools documented', check: (ctx) => {
    const docs = ['AGENTS.md', 'README.md'].map((f) => ({ f, ...readState(join(ctx.root, f)) }));
    const hit = docs.find((d) => /`gh[ `]|\bgh (pr|api|repo|issue)\b/.test(d.text ?? ''));
    if (hit) return { state: 'pass', evidence: `${hit.f} documents the gh CLI` };
    const unreadable = docs.filter((d) => d.error).map((d) => `${d.f} is not readable (${d.error})`);
    return unreadable.length ? { state: 'unknown', evidence: unreadable.join('; ') } : { state: 'fail', evidence: 'gh CLI not documented' };
  } },
  { id: 'BS-4', pillar: 1, level: 4, src: 'F-level', name: 'Regular deployment frequency (≥3 releases in 90 days)', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const r = ghGet(`repos/${ctx.repo}/releases?per_page=100`, { jq: '[.[] | {published_at, draft}]', array: true });
    if (!r.ok) return { state: 'unknown', evidence: `gh api releases: ${r.error}` };
    const cutoff = ctx.now - 90 * 864e5;
    const n = r.json.filter((x) => {
      const t = Date.parse(x.published_at);
      return !x.draft && t >= cutoff && t <= ctx.now;
    }).length;
    return { state: n >= 3 ? 'pass' : 'fail', evidence: `${n} releases published in the 90 days before ${ctx.date}` };
  } },
  // Spec §3 lists the missing runtime pin; §5's "pinned dependencies" is satisfied by global.json for .NET.
  { id: 'BS-5', pillar: 1, level: 2, src: 'S5', name: 'Runtime version pinned', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.nvmrc', '.node-version', '.tool-versions', 'global.json']);
    if (f) return { state: 'pass', evidence: f };
    const ranges = [...new Set(APPS.map((a) => pkg(join(ctx.root, a))?.engines?.node).filter(Boolean))];
    const eng = ranges.length ? `engines.node is a range (${ranges.join(', ')}); ` : '';
    return { state: 'fail', evidence: `${eng}no .nvmrc, .node-version, .tool-versions or global.json (spec §3)` };
  } },

  // Testing
  { id: 'TS-1', pillar: 2, level: 1, src: 'F-level', name: 'Unit tests exist', app: (ctx, d) => {
    const t = walk(d).filter((p) => /\.(test|spec)\.[cm]?[jt]sx?$|Tests?\.cs$/.test(p));
    return t.length ? { pass: true, why: `${t.length} test files` } : { pass: false, why: 'no test files' };
  } },
  { id: 'TS-2', pillar: 2, level: 3, src: 'F-level', name: 'Integration tests exist', app: (ctx, d) => {
    const re = /(^|\/)(integration|e2e|contract|browser|routes)(\/|[.-])/;
    const t = walk(d).filter((p) => /\.(test|spec)\.[cm]?[jt]sx?$/.test(p) && re.test(relative(d, p)));
    const script = Object.keys(pkg(d)?.scripts ?? {}).find((s) => /^test:(integration|e2e|heavy)$/.test(s));
    if (script) return { pass: true, why: `heuristic: matched script ${script}` };
    if (t.length) return { pass: true, why: `heuristic: matched ${t.length} test files under integration/e2e/contract/browser/routes paths` };
    return { pass: false, why: 'no integration/e2e/contract/browser/routes tests or test:integration|e2e|heavy script' };
  } },
  { id: 'TS-3', pillar: 2, level: 3, src: 'S5', name: 'Tests runnable locally (one command)', app: (ctx, d, rel) => {
    if (pkg(d)?.scripts?.test) return { pass: true, why: 'npm test' };
    const agents = read(join(ctx.root, 'AGENTS.md'));
    const line = agents.split('\n').find((l) => l.includes(rel) && /\btest\b/.test(l));
    return line ? { pass: true, why: 'test command in AGENTS.md' } : { pass: false, why: 'no test script or documented test command' };
  } },
  { id: 'TS-4', pillar: 2, level: 4, src: 'F-level', name: 'Fast CI feedback', check: (ctx) => {
    const wf = workflows(ctx.root);
    if (!wf.length) return { state: 'fail', evidence: 'no .github/workflows — no CI at all' };
    return { state: 'unknown', evidence: `${wf.length} workflows; speed needs run durations (not measured by R0)` };
  } },
  { id: 'TS-5', pillar: 2, level: 4, src: 'F-level', name: 'Flaky test detection', check: (ctx) => {
    const wf = workflows(ctx.root);
    const hit = wf.find((f) => /flak|quarantin/i.test(read(f)));
    if (hit) return { state: 'pass', evidence: relative(ctx.root, hit) };
    return { state: 'fail', evidence: wf.length ? 'no flaky/quarantine job in workflows' : 'no .github/workflows — no CI at all' };
  } },

  // Documentation
  { id: 'DOC-1', pillar: 3, level: 2, src: 'F-level', name: 'AGENTS.md', check: (ctx) => docPresent(ctx, 'AGENTS.md') },
  { id: 'DOC-2', pillar: 3, level: 1, src: 'F-level', name: 'README', check: (ctx) => docPresent(ctx, 'README.md') },
  { id: 'DOC-3', pillar: 3, level: 4, src: 'S5', name: 'Documentation freshness checked automatically', check: (ctx) => {
    const wf = workflows(ctx.root);
    const hit = wf.find((f) => /sync-rules\.sh --check|docs?-fresh|lychee|markdown-link-check/.test(read(f)));
    if (hit) return { state: 'pass', evidence: relative(ctx.root, hit) };
    return { state: 'fail', evidence: wf.length ? 'no doc-freshness step in workflows' : 'no .github/workflows; doc checks (sync-rules --check) run by hand only' };
  } },

  // Development Environment
  { id: 'DE-1', pillar: 4, level: 2, src: 'F-level', name: 'Devcontainer', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.devcontainer/devcontainer.json', '.devcontainer.json']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no .devcontainer' };
  } },
  { id: 'DE-2', pillar: 4, level: 2, src: 'F-pillar', name: 'Environment template', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.env.example', '.env.template', '.env.sample']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no .env.example' };
  } },
  { id: 'DE-3', pillar: 4, level: 2, src: 'inferred', name: 'Local services setup', check: (ctx) => {
    const f = firstExisting(ctx.root, ['setup.sh', 'docker-compose.yml', 'docker-compose.yaml', 'compose.yml', 'compose.yaml', 'devbox.json']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no setup script or compose file' };
  } },

  // Debugging & Observability
  { id: 'OBS-1', pillar: 5, level: 3, src: 'S5', name: 'Structured logging', app: (ctx, d) => {
    const dep = allDeps(pkg(d)).find((x) => /^(pino|winston|bunyan)$/.test(x));
    if (dep) return { pass: true, why: dep };
    const src = walk(join(d, 'src')).find((p) => /Fastify\(\s*\{[^}]*logger:\s*(true|\{)/.test(read(p)));
    if (src) return { pass: true, why: `Fastify pino logger (${relative(d, src)})` };
    const jsonl = walk(join(d, 'src')).some((p) => /\.jsonl\b/.test(read(p)));
    return { pass: false, why: jsonl ? 'ad-hoc JSONL writers, no shared logger with levels and ids (spec §3)' : 'no structured logger' };
  } },
  { id: 'OBS-2', pillar: 5, level: 3, src: 'F-level', name: 'Distributed tracing', app: (ctx, d) => {
    const dep = allDeps(pkg(d)).find((x) => x.startsWith('@opentelemetry/'));
    return dep ? { pass: true, why: dep } : { pass: false, why: 'no OpenTelemetry' };
  } },
  { id: 'OBS-3', pillar: 5, level: 3, src: 'F-level', name: 'Metrics collection', app: (ctx, d) => {
    const dep = allDeps(pkg(d)).find((x) => /^(prom-client|@opentelemetry\/sdk-metrics)$/.test(x));
    return dep ? { pass: true, why: dep } : { pass: false, why: 'no metrics library' };
  } },

  // Security
  { id: 'SEC-1', pillar: 6, level: 2, src: 'F-level', name: 'Branch protection on the default branch', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const info = repoInfo(ctx);
    if (!info.ok) return { state: 'unknown', evidence: `gh api: ${info.error}` };
    const b = info.json.default_branch;
    if (typeof b !== 'string' || !/^[\w./-]+$/.test(b)) return { state: 'unknown', evidence: 'unexpected response shape (no default_branch)' };
    const prot = ghGet(`repos/${ctx.repo}/branches/${b}/protection`);
    if (prot.ok) return { state: 'pass', evidence: `classic branch protection on ${b}` };
    const rules = ghGet(`repos/${ctx.repo}/rules/branches/${b}`, { array: true });
    if (rules.ok && rules.json.length) return { state: 'pass', evidence: `${rules.json.length} ruleset rules apply to ${b}` };
    if (rules.ok && /not protected/i.test(prot.error)) return { state: 'fail', evidence: `${b}: protection "${prot.error}"; rulesets: none` };
    return { state: 'unknown', evidence: `protection: ${prot.error}; rules: ${rules.ok ? 'none' : rules.error}` };
  } },
  { id: 'SEC-2', pillar: 6, level: 3, src: 'F-level', name: 'Secret scanning', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const r = repoInfo(ctx);
    const s = r.ok ? r.json.security_and_analysis?.secret_scanning?.status : null;
    if (!s) return { state: 'unknown', evidence: r.ok ? 'security_and_analysis not readable' : `gh api: ${r.error}` };
    const pp = r.json.security_and_analysis?.secret_scanning_push_protection?.status ?? 'n/a';
    return { state: s === 'enabled' ? 'pass' : 'fail', evidence: `secret_scanning ${s}, push_protection ${pp}` };
  } },
  { id: 'SEC-3', pillar: 6, level: 3, src: 'S5', name: 'CODEOWNERS', check: (ctx) => {
    const f = firstExisting(ctx.root, ['CODEOWNERS', '.github/CODEOWNERS', 'docs/CODEOWNERS']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no CODEOWNERS' };
  } },
  { id: 'SEC-4', pillar: 6, level: 3, src: 'inferred', name: 'Dependency update bot', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.github/dependabot.yml', '.github/dependabot.yaml', 'renovate.json', '.renovaterc', '.github/renovate.json']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no Dependabot or Renovate config (spec §3: no dependency update bot)' };
  } },

  // Task Discovery
  { id: 'TD-1', pillar: 7, level: 3, src: 'S5', name: 'Issue templates', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.github/ISSUE_TEMPLATE', '.github/ISSUE_TEMPLATE.md', 'ISSUE_TEMPLATE.md']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no issue templates' };
  } },
  { id: 'TD-2', pillar: 7, level: 3, src: 'S5', name: 'Issue labeling system', check: (ctx) => {
    if (!ctx.remote) return remoteUnknown(ctx);
    const r = ghGet(`repos/${ctx.repo}/labels?per_page=100`, { array: true });
    if (!r.ok) return { state: 'unknown', evidence: `gh api labels: ${r.error}` };
    const own = r.json.map((l) => l.name).filter((n) => !DEFAULT_LABELS.has(n));
    return own.length ? { state: 'pass', evidence: `${own.length} labels beyond GitHub defaults` }
      : { state: 'fail', evidence: `${r.json.length} labels, all GitHub defaults` };
  } },
  { id: 'TD-3', pillar: 7, level: 3, src: 'S5', name: 'PR template', check: (ctx) => {
    const f = firstExisting(ctx.root, ['.github/pull_request_template.md', '.github/PULL_REQUEST_TEMPLATE.md', '.github/PULL_REQUEST_TEMPLATE', 'pull_request_template.md', 'docs/pull_request_template.md']);
    return f ? { state: 'pass', evidence: f } : { state: 'fail', evidence: 'no PR template' };
  } },
  { id: 'TD-4', pillar: 7, level: 5, src: 'S5', name: 'Complex requirements decomposed into parallel execution (pillar placement inferred)', check: () => ({
    state: 'unknown', evidence: 'no observable artifact yet: packets and stacked epics (factory addendum F0–F3) are not built' }) },

  // Product & Experimentation. PE-1/PE-2 pass on the W9 page (spec §5, §6) that frames the existing telemetry and
  // experiment machinery for this pillar.
  { id: 'PE-1', pillar: 8, level: 4, src: 'S5', name: 'Product analytics instrumentation', check: (ctx) => (
    existsSync(join(ctx.root, PRODUCT_SIGNALS)) ? { state: 'pass', evidence: PRODUCT_SIGNALS }
      : { state: 'fail', evidence: `scorer telemetry exists but is not framed as product analytics (spec §3); no W9 page (${PRODUCT_SIGNALS})` }) },
  { id: 'PE-2', pillar: 8, level: 4, src: 'S5', name: 'Experiment infrastructure', check: (ctx) => (
    existsSync(join(ctx.root, PRODUCT_SIGNALS)) ? { state: 'pass', evidence: PRODUCT_SIGNALS }
      : { state: 'fail', evidence: `judge shadow mode and evolve offline comparisons exist but are not listed as experiments (spec §3); no W9 page (${PRODUCT_SIGNALS})` }) },
  { id: 'PE-3', pillar: 8, level: 5, src: 'F-level', name: 'Self-improving systems (pillar placement inferred)', check: () => ({
    state: 'unknown', evidence: 'no observable artifact to check yet: sindri evolve exists (Plan 5); the readiness module proposing its own fixes (§8) does not' }) },
];

// ---------- scoring ----------

function evaluate(ctx) {
  return CRITERIA.map((c) => {
    if (c.check) {
      const r = c.check(ctx);
      return { ...c, scope: 'repo', state: r.state, evidence: r.evidence,
        credit: r.state === 'pass' ? 1 : 0, score: r.state === 'pass' ? '1 / 1' : r.state === 'fail' ? '0 / 1' : '—' };
    }
    const per = APPS.map((rel) => {
      const d = join(ctx.root, rel);
      if (!existsSync(d)) return { rel, state: 'fail', why: 'absent' };
      const r = c.app(ctx, d, rel);
      return { rel, state: r.state ?? (r.pass ? 'pass' : 'fail'), why: r.why };
    });
    const list = (s) => per.filter((p) => p.state === s).map((p) => `${p.rel} (${p.why})`);
    const [passes, fails, nas] = [list('pass'), list('fail'), list('n/a')];
    const evidence = [passes.length && `pass: ${passes.join(', ')}`, fails.length && `fail: ${fails.join(', ')}`,
      nas.length && `n/a: ${nas.join(', ')}`].filter(Boolean).join('; ');
    // An app where the criterion doesn't apply leaves the denominator; all n/a makes the criterion n/a.
    const n = APPS.length - nas.length;
    if (!n) return { ...c, scope: 'app', state: 'n/a', evidence, credit: 0, score: '—' };
    return { ...c, scope: 'app', state: passes.length === n ? 'pass' : 'fail', evidence,
      credit: passes.length / n, score: `${passes.length} / ${n}` };
  });
}

// Levels: every repo starts at L1. Walking the levels that have scored criteria in order, each one that reaches
// 80% (fractional credit for app criteria; n/a and unknown are not scored) unlocks the level above it; the first
// one below 80% stops the walk. Levels with nothing scored neither unlock nor block. Empty levels below the reached
// level (and an empty Level 1, where every repo starts) are marked `skipped`, so a level reached without evidence
// of its own reads as inferred; empty levels at or above it are not. Nothing scored at all gives level null (—).
export function levelOf(rows) {
  const byLevel = [1, 2, 3, 4, 5].map((L) => {
    const scored = rows.filter((r) => r.level === L && (r.state === 'pass' || r.state === 'fail'));
    const credit = scored.reduce((s, r) => s + r.credit, 0);
    return { L, n: scored.length, credit, ratio: scored.length ? credit / scored.length : null, skipped: false };
  });
  if (byLevel.every((b) => b.ratio === null)) return { level: null, byLevel };
  let level = 1;
  for (const b of byLevel) {
    if (b.ratio === null) continue;
    if (b.ratio < 0.8) break;
    level = Math.min(5, b.L + 1);
  }
  for (const b of byLevel) b.skipped = b.ratio === null && (b.L < level || b.L === 1);
  return { level, byLevel };
}

// ---------- render ----------

const pct = (b) => (b.skipped ? 'skip' : b.ratio === null ? '—'
  : `${b.credit.toFixed(2).replace(/\.00$/, '')} / ${b.n} (${Math.round(b.ratio * 100)}%)`);
const lvl = (l) => l.level ?? '—';
const esc = (s) => String(s).replace(/\|/g, '\\|');

function render(ctx, rows) {
  const out = [];
  out.push('# Agent readiness baseline — 2026-10 (R0)', '');
  out.push('> **Throwaway R0 baseline** (readiness spec §10). Generated by `scripts/readiness-baseline.mjs`, a',
    '> manual-pass-equivalent script that will be deleted when `Aw.Readiness` lands in R1b. `Aw.Readiness` becomes',
    '> the score of record (decision R4); no paid Factory report or Droid was used.', '');
  out.push(`Repository: \`${ctx.repo ?? 'unknown'}\` · Apps (${APPS.length}): ${APPS.map((a) => `\`${a}\``).join(', ')}`, '');
  out.push(`Generated: ${ctx.stamp}`, '');

  const overall = levelOf(rows);
  out.push('## Summary', '');
  out.push(`**Overall level: ${lvl(overall)}** (of 5). Cells show passed credit / scored criteria at that level; \`skip\``,
    'is a level with nothing scored below the reached one (or an empty Level 1); `—` is nothing scored.', '');
  out.push('| Pillar | L1 | L2 | L3 | L4 | L5 | Level |', '|---|---|---|---|---|---|---|');
  PILLARS.forEach((name, i) => {
    const l = levelOf(rows.filter((r) => r.pillar === i));
    out.push(`| ${name} | ${l.byLevel.map(pct).join(' | ')} | ${lvl(l)} |`);
  });
  out.push(`| **Overall** | ${overall.byLevel.map(pct).join(' | ')} | **${lvl(overall)}** |`, '');
  const counts = ['pass', 'fail', 'n/a', 'unknown'].map((s) => `${s} ${rows.filter((r) => r.state === s).length}`).join(' · ');
  out.push(`Criteria: ${rows.length} (${counts}).`, '');

  out.push('## How it scores', '',
    '- **Criteria** are the examples Factory publishes per level (`F-level`) or per pillar (`F-pillar`, level placed',
    '  by this script), the criterion families of spec §5 (`S5`, level from §5), or criteria placed by this script',
    '  (`inferred`). See Sources.',
    '- **States:** `pass`, `fail`, `n/a` (with a reason) or `unknown` (no observable artifact to check yet, or the',
    '  data was not readable). `unknown` and `n/a` are not scored.',
    `- **Scope:** \`repo\` criteria are checked once. \`app\` criteria are checked in each of the ${APPS.length} apps (spec §2) and`,
    '  score as a fraction of the apps they apply to (an app where a criterion does not apply is `n/a` and leaves the',
    '  denominator). They count as fractional credit and show `pass` only when every applicable app passes.',
    '- **Levels:** every repo starts at Level 1. Level N+1 unlocks when at least 80% of the scored Level N criteria',
    '  pass and every lower level with scored criteria also reached 80%. Levels with nothing scored (in a pillar, or',
    '  overall) neither unlock nor block; the summary shows the ones below the reached level (and an empty Level 1)',
    '  as `skip`. A pillar with nothing scored has no level (`—`).',
    '- **Remote data** comes from read-only `gh api` GETs (default branch, branch protection, rulesets, secret',
    '  scanning, labels, releases).',
    '- **Not the §8 contract:** this R0 script has no `--json`, no delta since the last run, writes nothing to the',
    '  ledger, and its rows name the measured value and evidence but no limit or fix command. Its exit codes mean',
    '  "report written" (0) or "tool error" (2), not "target level holds". `Aw.Readiness` (R1b) implements §8 and',
    '  treats this file as its first previous run.', '');

  out.push('## Criteria by pillar', '');
  PILLARS.forEach((name, i) => {
    out.push(`### ${name}`, '');
    out.push('| ID | Criterion | Level | Scope | State | Score | Evidence | Source |', '|---|---|---|---|---|---|---|---|');
    for (const r of rows.filter((x) => x.pillar === i)) {
      out.push(`| ${r.id} | ${esc(r.name)} | ${r.level} | ${r.scope} | ${r.state} | ${r.score} | ${esc(r.evidence)} | ${r.src} |`);
    }
    out.push('');
  });

  out.push('## Sources', '');
  for (const [k, v] of Object.entries(SOURCES)) out.push(`- \`${k}\`: ${v}`);
  out.push('');
  return out.join('\n');
}

// ---------- main ----------

function main() {
  const opts = parseArgs(process.argv.slice(2));
  let root = opts.root;
  if (!root) {
    try {
      root = execFileSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
    } catch { die('not in a git repo; pass --root'); }
  }
  if (!existsSync(root) || !statSync(root).isDirectory()) die(`root is not a directory: ${root}`);

  const envMs = process.env.READINESS_GH_TIMEOUT_MS;
  if (envMs !== undefined && envMs !== '') {
    if (!/^[1-9][0-9]*$/.test(envMs)) die(`READINESS_GH_TIMEOUT_MS must be a positive integer (ms), got ${envMs}`);
    GH_TIMEOUT_MS = Number(envMs);
  }
  if (opts.repo && !validRepo(opts.repo)) die(`--repo must be OWNER/NAME, got ${opts.repo}`);
  let repo = opts.repo;
  let originWhy = null;
  if (!repo) ({ repo, why: originWhy } = repoFromOrigin(root));
  const remote = opts.remote && Boolean(repo);
  const remoteWhy = !opts.remote ? 'not read (--no-remote)' : `${originWhy}; pass --repo`;
  const date = opts.date ?? new Date().toISOString().slice(0, 10);
  // The regex alone accepts 2026-13-45; round-trip through Date so an impossible date is a tool error, not NaN.
  const real = /^\d{4}-\d{2}-\d{2}$/.test(date) && !Number.isNaN(Date.parse(`${date}T00:00:00Z`))
    && new Date(`${date}T00:00:00Z`).toISOString().slice(0, 10) === date;
  if (!real) die(`--date must be a real YYYY-MM-DD date, got ${date}`);
  const ctx = {
    root, repo, remote, remoteWhy, date,
    now: Date.parse(`${date}T23:59:59Z`),
    stamp: opts.date ? date : new Date().toISOString(),
  };

  const md = render(ctx, evaluate(ctx));
  if (!opts.out) { process.stdout.write(md); return; }
  try { writeFileSync(opts.out, md); } catch (e) { die(`cannot write ${opts.out}: ${e.message}`); }
}

// The origin's OWNER/NAME when it is a github.com URL. Anything else is never sent to gh; the reason names only the
// host (a URL can carry credentials).
function repoFromOrigin(root) {
  let url;
  try {
    url = execFileSync('git', ['-C', root, 'remote', 'get-url', 'origin'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch { return { repo: null, why: 'no origin remote' }; }
  const m = /^(?:https:\/\/(?:[^@/]+@)?github\.com\/|git@github\.com:|ssh:\/\/git@github\.com\/)([^/]+\/[^/]+?)(?:\.git)?\/?$/.exec(url);
  if (!m) {
    const host = (/^[a-z][a-z0-9+.-]*:\/\/(?:[^@/]+@)?([^/:]+)/i.exec(url) ?? /^[^@\s/]+@([^:/]+):/.exec(url))?.[1] ?? 'unrecognised URL';
    return { repo: null, why: `origin is not a GitHub URL (${host})` };
  }
  return validRepo(m[1]) ? { repo: m[1], why: null } : { repo: null, why: 'origin URL has no OWNER/NAME' };
}

// OWNER/NAME, and neither part may be `.` or `..` (the value is spliced into a gh api path).
const validRepo = (r) => /^[\w.-]+\/[\w.-]+$/.test(r) && !r.split('/').some((x) => x === '.' || x === '..');

// Run only as a CLI; the tests import levelOf. Anything left uncaught is a tool error (exit 2), not a stack trace.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try { main(); } catch (e) { die(`unexpected error: ${e?.message ?? e}`); }
}
