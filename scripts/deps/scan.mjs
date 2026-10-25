// scripts/deps/scan.mjs — finds cli, db and file edges in the TypeScript and shell source, and reads
// C# project files. A heuristic scan, not a proof: what it can't see is listed in the deps.yaml header.

import fs from "node:fs";
import path from "node:path";

const SKIP_SOURCE_DIRS = new Set(["node_modules", "dist", "coverage", "tests", "test", "__tests__", "fixtures", ".git", "bin", "obj"]);
const SKIP_PROJECT_DIRS = new Set(["node_modules", ".git", "bin", "obj"]);
const TEST_FILE = /\.(test|spec)\.([cm]?[jt]sx?|sh)$/;
const TS_FILE = /\.[cm]?[jt]sx?$/;

const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const Q = "[\"'`]";
const envName = (bin) => `${bin.toUpperCase().replace(/-/g, "_")}_BIN`;

function walk(dir, skip, keep, out = []) {
  if (!fs.existsSync(dir)) return out;
  if (fs.statSync(dir).isFile()) return keep(dir) ? [...out, dir] : out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!skip.has(e.name)) walk(p, skip, keep, out); } else if (keep(p)) out.push(p);
  }
  return out;
}

// ---------- TypeScript: comments blanked, string, template and regex interiors masked ----------

export class ScanError extends Error {}

// A `/` starts a regex literal after one of these, or after one of the keywords; otherwise it divides.
const BEFORE_REGEX = new Set([..."(,=:[!&|?{};+-*%<>~^"]);
const REGEX_KEYWORDS = new Set(["return", "typeof", "case", "in", "of", "delete", "void", "throw", "new", "else", "do", "yield", "await", "instanceof"]);

// Returns the text with comments replaced by spaces (newlines kept, so offsets and lines still line up)
// and a mask that is true inside string, template and regex literals. Throws ScanError on a template
// literal or block comment still open at the end of the file, so a lexing error can't hide calls.
export function lexTs(src) {
  const out = src.split("");
  const inStr = new Uint8Array(src.length);
  const stack = []; // template nesting: a number is the brace depth inside ${ }
  let i = 0;
  const lineOf = (k) => src.slice(0, k).split("\n").length;
  const blank = (a, b) => { for (let k = a; k < b; k++) if (out[k] !== "\n") out[k] = " "; };
  const quoted = (q) => {
    const start = i++;
    while (i < src.length && src[i] !== q && src[i] !== "\n") { if (src[i] === "\\") i++; i++; }
    inStr.fill(1, start + 1, Math.min(i, src.length));
    i++;
  };
  const template = (opened) => {
    const start = i;
    while (i < src.length && src[i] !== "`") {
      if (src[i] === "\\") { i += 2; continue; }
      if (src[i] === "$" && src[i + 1] === "{") { inStr.fill(1, start, i); stack.push(0); i += 2; return; }
      i++;
    }
    if (i >= src.length) throw new ScanError(`unterminated template literal opened on line ${lineOf(opened)}`);
    inStr.fill(1, start, i);
    i++;
  };
  const regexAhead = () => {
    let k = i - 1;
    while (k >= 0 && /\s/.test(out[k])) k--;
    if (k < 0) return true;
    // A postfix ++ or -- (after an identifier, `)` or `]`) ends a value, so the slash divides.
    if ((out[k] === "+" || out[k] === "-") && out[k - 1] === out[k] && k >= 2 && /[A-Za-z0-9_$)\]]/.test(out[k - 2])) return false;
    if (BEFORE_REGEX.has(out[k])) return true;
    if (!/[A-Za-z0-9_$]/.test(out[k])) return false;
    let w = k;
    while (w > 0 && /[A-Za-z0-9_$]/.test(out[w - 1])) w--;
    return REGEX_KEYWORDS.has(out.slice(w, k + 1).join(""));
  };
  const regex = () => { // i at the opening slash; returns false if no closing slash on this line
    let j = i + 1;
    let cls = false;
    while (j < src.length && src[j] !== "\n") {
      if (src[j] === "\\") { j += 2; continue; }
      if (src[j] === "[") cls = true;
      else if (src[j] === "]") cls = false;
      else if (src[j] === "/" && !cls) break;
      j++;
    }
    if (j >= src.length || src[j] !== "/") return false;
    inStr.fill(1, i + 1, j);
    i = j + 1;
    while (i < src.length && /[a-z]/.test(src[i])) i++;
    return true;
  };
  const templates = [];
  while (i < src.length) {
    const c = src[i];
    if (stack.length && c === "}" && stack[stack.length - 1] === 0) {
      stack.pop(); i++;
      const opened = templates.pop();
      template(opened);
      if (stack.length > templates.length) templates.push(opened);
      continue;
    }
    if (c === "/" && src[i + 1] === "/") { const e = src.indexOf("\n", i); const end = e === -1 ? src.length : e; blank(i, end); i = end; continue; }
    if (c === "/" && src[i + 1] === "*") {
      const e = src.indexOf("*/", i + 2);
      if (e === -1) throw new ScanError(`unterminated block comment opened on line ${lineOf(i)}`);
      blank(i, e + 2); i = e + 2; continue;
    }
    if (c === "/" && regexAhead() && regex()) continue;
    if (c === '"' || c === "'") { quoted(c); continue; }
    if (c === "`") { const opened = i; i++; template(opened); if (stack.length > templates.length) templates.push(opened); continue; }
    if (stack.length && c === "{") stack[stack.length - 1]++;
    if (stack.length && c === "}") stack[stack.length - 1]--;
    i++;
  }
  if (stack.length) throw new ScanError(`unterminated template literal opened on line ${lineOf(templates[templates.length - 1] ?? 0)}`);
  return { code: out.join(""), inStr };
}

// ---------- shell: heredoc bodies and prose skipped, each command segment checked ----------

const PROSE = /^(?:echo|printf|log|die|warn|info|err|error|note|say|usage)\b/;
const PREFIX = /^(?:!\s*|(?:if|then|do|else|elif|while|until|time|nice|nohup|sudo|exec|command|builtin|xargs|env)\s+|timeout\s+\S+\s+|\w+_bin\s+|[A-Za-z_][A-Za-z0-9_]*=(?:"[^"]*"|'[^']*'|\S*)\s+|-\S+\s+|[({]\s*)/;

// Splits one shell line into commands: on ; | & outside quotes, and at $( and backquotes outside single
// quotes (a command substitution inside double quotes is still a command).
function splitShell(line) {
  const segs = [];
  let cur = "";
  let q = null;
  for (let k = 0; k < line.length; k++) {
    const c = line[k];
    if (q === "'") { cur += c; if (c === "'") q = null; continue; }
    if (c === "\\") { cur += c + (line[k + 1] ?? ""); k++; continue; }
    if (c === "$" && line[k + 1] === "(" && line[k + 2] !== "(") { segs.push(cur); cur = ""; k++; continue; }
    if (c === "`") { segs.push(cur); cur = ""; continue; }
    if (q === '"') { cur += c; if (c === '"') q = null; continue; }
    if (c === '"' || c === "'") { q = c; cur += c; continue; }
    if (c === ";" || c === "|" || c === "&") { segs.push(cur); cur = ""; continue; }
    cur += c;
  }
  segs.push(cur);
  return segs;
}

export function shellSegments(line) {
  return splitShell(line).map((s) => s.trim()).filter((s) => s && !PROSE.test(s)).map((s) => {
    let prev;
    do { prev = s; s = s.replace(PREFIX, ""); } while (s !== prev);
    return s;
  });
}

export function shellLines(text) {
  const keep = [];
  let heredoc = null;
  text.split("\n").forEach((line, n) => {
    if (heredoc) { if (line.trim() === heredoc) heredoc = null; return; }
    const t = line.trim();
    if (t.startsWith("#")) return;
    const h = /<<-?\s*(['"]?)([A-Za-z_][A-Za-z0-9_]*)\1/.exec(line.replace(/<<</g, ""));
    if (h) heredoc = h[2];
    keep.push({ text: line, line: n + 1 });
  });
  return keep;
}

// ---------- matchers per target node ----------

function pathPatterns(rel) {
  const segs = rel.split("/");
  const slash = `(?:^|[/}"'\`\\s])${segs.map(esc).join("/")}(?![\\w.-])`;
  if (segs.length === 1) return { ts: new RegExp(`${Q}${esc(rel)}${Q}`, "g"), sh: new RegExp(`/${esc(rel)}(?![\\w.-])`) };
  return { ts: new RegExp(`${segs.map((s) => `${Q}${esc(s)}${Q}`).join("\\s*,\\s*")}|${slash}`, "g"), sh: new RegExp(slash) };
}

export function matchersFor(node) {
  const m = [];
  for (const b of node.bins ?? []) {
    const e = esc(b);
    const env = esc(envName(b));
    m.push({
      kind: "cli", what: `launches \`${b}\``, codeOnly: true,
      ts: new RegExp([
        `\\b\\w*(?:spawn|exec)\\w*\\s*\\(\\s*${Q}${e}(?=${Q}|\\s)`, // spawn/exec*(…"bin" or "bin args", also wrapped
        `(?:\\?\\?|\\|\\|)\\s*${Q}${e}${Q}`, // env fallback: process.env.X ?? "bin"
        `\\b\\w*[Bb]in\\w*\\s*[:=]\\s*${Q}${e}${Q}`, // fooBin = "bin", { judgeBin: "bin" }
        `\\b\\w*[Bb]in\\w*\\([^\\n]*${Q}${e}${Q}`, // findBin("bin")
      ].join("|"), "g"),
      sh: (seg) => new RegExp(`^(?:(?:which|type|command\\s+-v)\\s+)?(?:["']?[^\\s"']*/)?${e}(?=["'\\s)]|$)`).test(seg)
        || new RegExp(`\\b(?:which|type|command\\s+-v)\\s+${e}(?=[\\s)]|$)`).test(seg)
        || new RegExp(`\\$\\{?${env}\\b|\\$\\{\\w+:-["']?(?:[^}"']*/)?${e}["']?\\}`).test(seg),
    });
    // The env name that overrides the binary (process.env.X_BIN, process.env["X_BIN"]) is a use in code or in a string.
    m.push({ kind: "cli", what: `launches \`${b}\` (${envName(b)})`, ts: new RegExp(`\\b${env}\\b`, "g"), sh: () => false });
  }
  for (const p of node.binPaths ?? []) m.push({ kind: "cli", what: `launches ${p}`, ...pathPatterns(p) });
  for (const p of node.db ?? []) m.push({ kind: "db", what: `opens ${p}`, ...pathPatterns(p) });
  for (const p of node.files ?? []) m.push({ kind: "file", what: `uses ${p}`, ...pathPatterns(p) });
  if (node.namespace) {
    const e = esc(node.namespace);
    m.push({ kind: "file", what: `uses ${node.namespace}/ (namespace)`, namespace: true,
      ts: new RegExp(`\\b(?:join|resolve)\\([^)]*?${Q}${e}${Q}\\s*,\\s*${Q}|${Q}[^"'\`\\n]*/${e}/`, "g"), sh: new RegExp(`/${e}/`) });
  }
  return m;
}

// ---------- the scan ----------

const lineAt = (starts, idx) => { let lo = 0; let hi = starts.length - 1; while (lo < hi) { const mid = (lo + hi + 1) >> 1; if (starts[mid] <= idx) lo = mid; else hi = mid - 1; } return lo + 1; };

function tsHits(text, targets) {
  const { code, inStr } = lexTs(text);
  const starts = [0];
  for (let k = 0; k < code.length; k++) if (code[k] === "\n") starts.push(k + 1);
  const hits = [];
  for (const to of targets) {
    for (const m of to.matchers) {
      m.ts.lastIndex = 0;
      for (let r = m.ts.exec(code); r; r = m.ts.exec(code)) {
        if (m.codeOnly && inStr[r.index]) continue;
        hits.push({ to: to.id, m, line: lineAt(starts, r.index) });
      }
    }
  }
  return hits;
}

function shHits(text, targets) {
  const hits = [];
  for (const { text: line, line: n } of shellLines(text)) {
    const segs = shellSegments(line);
    for (const to of targets) {
      for (const m of to.matchers) {
        const hit = typeof m.sh === "function" ? segs.some((s) => m.sh(s)) : m.sh.test(line);
        if (hit) hits.push({ to: to.id, m, line: n });
      }
    }
  }
  return hits;
}

// Map of "from|to|kind" -> first hit { from, to, kind, where, what }. A namespace hit on a line that
// another matcher of the same target already explains (its db file, a listed file, its binary) is dropped.
export function scan(root, nodes) {
  const found = new Map();
  const targets = nodes.map((n) => ({ id: n.id, matchers: matchersFor(n) }));
  for (const from of nodes) {
    const others = targets.filter((t) => t.id !== from.id);
    for (const dir of from.scan ?? []) {
      const files = walk(path.join(root, dir), SKIP_SOURCE_DIRS, (p) => !TEST_FILE.test(p) && (TS_FILE.test(p) || p.endsWith(".sh")));
      for (const file of files) {
        const rel = path.relative(root, file).split(path.sep).join("/");
        const text = fs.readFileSync(file, "utf8");
        let hits;
        try { hits = (file.endsWith(".sh") ? shHits : tsHits)(text, others); } catch (e) {
          if (e instanceof ScanError) throw new ScanError(`${rel}: ${e.message}; the scan can't read this file, so it fails closed`);
          throw e;
        }
        const explained = new Set(hits.filter((h) => !h.m.namespace).map((h) => `${h.to}|${h.line}`));
        for (const h of hits.sort((a, b) => a.line - b.line)) {
          if (h.m.namespace && explained.has(`${h.to}|${h.line}`)) continue;
          const key = `${from.id}|${h.to}|${h.m.kind}`;
          if (!found.has(key)) found.set(key, { from: from.id, to: h.to, kind: h.m.kind, where: `${rel}:${h.line}`, what: h.m.what });
        }
      }
    }
  }
  return found;
}

// ---------- C#: http edges (another package's routes) and file/db edges (its state paths) ----------

// A package that serves an HTTP API lists its route prefixes in `routes` (e.g. /sindri/). An http edge is a
// C# string literal whose text starts with another package's route prefix (plain "…", interpolated $"…",
// verbatim @"…" or raw """…""": the opening quote is followed by the route), in a production project's *.cs
// files. A file or db edge is a C# file holding another package's `files` or `db` path in one literal
// ("sindri-app/control.json") or every segment of it as literals ("sindri-app", "control.json": Path.Combine pieces).
// The project belongs to the package whose projectPrefix starts its name (longest wins), so a client package
// that lives inside the server's src directory is still told apart. Test projects (*.Tests, *.Testing) and
// whole-line // comments are not scanned; /* */ blocks and trailing comments are. Not seen: a route after a base ($"{url}/sindri/x",
// "http://host/sindri/x"), a URL or path built from computed pieces.
const CS_FILE = /\.cs$/;

function ownerByPrefix(name, nodes) {
  let best = null;
  for (const n of nodes) {
    const pre = n.projectPrefix;
    if (pre && (name === pre || name.startsWith(`${pre}.`)) && (!best || pre.length > best.projectPrefix.length)) best = n;
  }
  return best;
}

function sourceFiles(dir) {
  // A project's own *.cs files: nested directories that hold another project are that project's.
  const out = [];
  for (const e of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (SKIP_SOURCE_DIRS.has(e.name) || fs.readdirSync(p).some((f) => /\.csproj$/i.test(f))) continue;
      out.push(...sourceFiles(p));
    } else if (CS_FILE.test(e.name)) out.push(p);
  }
  return out;
}

export function scanCSharp(root, nodes) {
  const found = new Map();
  const servers = nodes.filter((n) => (n.routes ?? []).length > 0)
    .map((n) => ({ id: n.id, routes: n.routes, re: new RegExp(`"(?:${n.routes.map(esc).join("|")})`) }));
  const owners = nodes.flatMap((n) => [
    ...(n.files ?? []).map((f) => ({ id: n.id, kind: "file", path: f })),
    ...(n.db ?? []).map((f) => ({ id: n.id, kind: "db", path: f })),
  ]).map((o) => ({ ...o, whole: new RegExp(`"[^"\\n]*${esc(o.path)}`), segs: o.path.split("/").map((seg) => new RegExp(`"${esc(seg)}"`)) }));
  const files = walk(path.join(root, "src"), SKIP_PROJECT_DIRS, (p) => /\.csproj$/i.test(p));
  for (const proj of files) {
    const name = projName(proj);
    if (/\.(Tests|Testing)(\.|$)/.test(name)) continue;
    const from = ownerByPrefix(name, nodes);
    if (!from) continue;
    for (const file of sourceFiles(path.dirname(proj))) {
      const rel = path.relative(root, file).split(path.sep).join("/");
      const lines = fs.readFileSync(file, "utf8").split("\n").map((l) => (/^\s*\/\//.test(l) ? "" : l));
      for (const to of servers.filter((s) => s.id !== from.id)) {
        const at = lines.findIndex((l) => to.re.test(l));
        const key = `${from.id}|${to.id}|http`;
        if (at >= 0 && !found.has(key)) found.set(key, { from: from.id, to: to.id, kind: "http", where: `${rel}:${at + 1}`, what: `calls ${to.routes.join(", ")}` });
      }
      for (const o of owners.filter((x) => x.id !== from.id)) {
        const whole = lines.findIndex((l) => o.whole.test(l));
        const at = whole >= 0 ? [whole] : o.segs.map((re) => lines.findIndex((l) => re.test(l)));
        const key = `${from.id}|${o.id}|${o.kind}`;
        if (at.every((i) => i >= 0) && !found.has(key)) found.set(key, { from: from.id, to: o.id, kind: o.kind, where: `${rel}:${Math.max(...at) + 1}`, what: `${o.kind === "db" ? "opens" : "uses"} ${o.path}` });
      }
    }
  }
  return found;
}

// ---------- C# projects ----------

const stripXmlComments = (s) => s.replace(/<!--[\s\S]*?-->/g, "");
const projName = (p) => path.basename(p.replace(/\\/g, "/")).replace(/\.csproj$/i, "");

// ProjectReferences in one file: { name, analyzer, buildOrder } where analyzer means OutputItemType="Analyzer"
// (a build-time Roslyn reference, attribute or child element) and buildOrder means a non-analyzer
// ReferenceOutputAssembly="false" (it orders the build and links nothing).
function projectRefs(xml) {
  const refs = [];
  for (const m of xml.matchAll(/<ProjectReference\b([^>]*?)(?:\/>|>([\s\S]*?)<\/ProjectReference>)/g)) {
    const inc = /\bInclude\s*=\s*(["'])(.*?)\1/.exec(m[1]);
    if (!inc) continue;
    const analyzer = /\bOutputItemType\s*=\s*(["'])Analyzer\1/i.test(m[1]) || /<OutputItemType>\s*Analyzer\s*<\/OutputItemType>/i.test(m[2] ?? "");
    const noOutput = /\bReferenceOutputAssembly\s*=\s*(["'])false\1/i.test(m[1]) || /<ReferenceOutputAssembly>\s*false\s*<\/ReferenceOutputAssembly>/i.test(m[2] ?? "");
    refs.push({ name: projName(inc[2]), analyzer, buildOrder: !analyzer && noOutput });
  }
  return refs;
}

// Every *.csproj under src/ and tests/ (any case): runtime refs, analyzer refs, build-order refs, third-party packages.
export function readProjects(root) {
  const files = ["src", "tests"].flatMap((d) => walk(path.join(root, d), SKIP_PROJECT_DIRS, (p) => /\.csproj$/i.test(p)));
  return files.map((file) => {
    const xml = stripXmlComments(fs.readFileSync(file, "utf8"));
    const all = projectRefs(xml);
    const packages = [];
    for (const m of xml.matchAll(/<PackageReference\b([^>]*?)(?:\/>|>([\s\S]*?)<\/PackageReference>)/g)) {
      const inc = /\bInclude\s*=\s*(["'])(.*?)\1/.exec(m[1]);
      if (!inc) continue;
      const priv = /\bPrivateAssets\s*=\s*(["'])all\1/i.test(m[1]) || /<PrivateAssets>\s*all\s*<\/PrivateAssets>/i.test(m[2] ?? "");
      if (!priv) packages.push(inc[2]);
    }
    return {
      file: path.relative(root, file).split(path.sep).join("/"), name: projName(file),
      refs: [...new Set(all.filter((r) => !r.analyzer && !r.buildOrder).map((r) => r.name))],
      buildOrder: [...new Set(all.filter((r) => r.buildOrder).map((r) => r.name))],
      analyzers: [...new Set(all.filter((r) => r.analyzer).map((r) => r.name))],
      packages: [...new Set(packages)].sort(),
    };
  });
}

// Directory.Build.props/.targets files with their ProjectReferences (they apply to every project below).
export function buildFiles(root) {
  const files = [
    ...["Directory.Build.props", "Directory.Build.targets"].map((f) => path.join(root, f)).filter((f) => fs.existsSync(f)),
    ...["src", "tests"].flatMap((d) => walk(path.join(root, d), SKIP_PROJECT_DIRS, (p) => /^Directory\.Build\.(props|targets)$/.test(path.basename(p)))),
  ];
  return files.map((f) => ({ file: path.relative(root, f).split(path.sep).join("/"), refs: projectRefs(stripXmlComments(fs.readFileSync(f, "utf8"))) })).filter((f) => f.refs.length > 0);
}
