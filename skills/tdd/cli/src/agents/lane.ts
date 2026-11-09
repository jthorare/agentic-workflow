// write_agents.py's Lane: the agents/ lane from content.json (+ tdd.html), method for method. Every string it emits must
// equal the Python's byte for byte (test/agents), so the shapes of the Python are kept: content nodes stay Python dicts
// (Map), "x or y" stays truthiness, f-string interpolation goes through str().
import { readFileSync, statSync, readdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import * as re from "../py/re";
import { lower } from "../py/str";
import * as sp from "./strip";
import {
  type Py, type PyDict, PyFloat, basename, cpLen, cpSlice, dirname as pdirname, dumps, expanduser, get, isdir, isfile, loads,
  num, or, pjoin, pyRound, readText, relpath, repr, sha256, sorted, splitext, str as S, strip as pystrip, truthy, uniq, utf8Len,
  abspath, lstrip, PY_WS,
} from "./_py";
import { DEFAULT_LLMS, DEFAULT_README } from "./texts";

export const GENERATOR = "write_agents/1";
export const BUDGET = 8000;
const README_MAX = 8192;
const CODE_INLINE_MAX = 60;   // lines; a longer block is linked from section slices instead of pasted
export const PACK = 0.9;      // pack slices to this share of the budget; links and footers are added after packing
export const EXEMPT = new Set(["tdd.md", "tdd.txt", "map.json", "refs.json", "citations.json", "tokens.json", "images/index.json",
  "page/plain.html", "page/outline.json", "decisions/decisions.json", "prs/prs.json"]);
const TEMPLATES = fileURLToPath(new URL("../../../templates/agents/", import.meta.url));

export type CopyRef = { copy: string };
export type FileVal = string | CopyRef;

export function tok(s: string): number {
  return Math.floor((cpLen(s) + 3) / 4);
}
export function yq(s: Py): string {
  return dumps(s === null || s === undefined ? "" : s);
}
export function rel(target: string, fromDir: string | null | undefined): string {
  return relpath(target, fromDir || ".");
}
export function dirn(p: string): string {
  return pdirname(p) || ".";
}
export function slugify(s: Py): string {
  const t = pystrip(((s || "") as string).toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/-{2,}/g, "-"), "-");
  return t || "x";
}
export function plain(html: Py): string {
  return truthy(html) ? sp.renderInline(html as string, "txt") : "";
}
const WS_RUN = new RegExp(`[${PY_WS}]+`, "g");
export function clip(s: Py, n: number): string {
  const t = pystrip(((s || "") as string).replace(WS_RUN, " "));
  return cpLen(t) <= n ? t : pystripR(cpSlice(t, 0, n - 1)) + "\u2026";
}
function pystripR(s: string): string {
  return s.replace(new RegExp(`[${PY_WS}]+$`), "");
}
function isPlainDict(x: Py): boolean {
  return x instanceof Map || (typeof x === "object" && x !== null && !Array.isArray(x) && !(x instanceof PyFloat));
}
/** JSON with one array item per line: small, greppable, still valid JSON. */
export function dumpLines(obj: Py): string {
  const ser = (v: Py, ind: number): string => {
    if (Array.isArray(v) && v.length && v.every(isPlainDict)) {
      const pad = " ".repeat(ind + 1);
      return "[\n" + v.map((x) => pad + dumps(x)).join(",\n") + "\n" + " ".repeat(ind) + "]";
    }
    const es: [string, Py][] | null = v instanceof Map ? [...v.entries()] : isPlainDict(v) ? Object.entries(v) : null;
    if (es && es.length && ind === 0) {
      const pad = " ".repeat(ind + 1);
      return "{\n" + es.map(([k, x]) => `${pad}${dumps(k)}: ${ser(x, ind + 1)}`).join(",\n") + "\n}";
    }
    return dumps(v);
  };
  return ser(obj, 0) + "\n";
}
const YAML_WORDS = new Set(["true", "false", "yes", "no", "on", "off", "null", "y", "n"]);
// Python's patterns through the kernel's re (\d and \w are CPython's, not the engine's \p{...})
const DATE = re.compile(String.raw`\d{4}-\d{2}-\d{2}`);
const PLAIN_ID = re.compile(String.raw`[A-Za-z_][\w.\-]*`);
/** A YAML scalar: plain when unambiguous (an id, a word, a date), else a JSON-quoted string. */
export function ys(v: Py): string {
  const s = S(v);
  if (DATE.fullmatch(s)) return s;
  if (PLAIN_ID.fullmatch(s) && !YAML_WORDS.has(lower(s))) return s;
  return yq(s);
}
export function front(fields: [string, Py][]): string {
  const lines = ["---"];
  for (const [k, v] of fields) {
    if (v === null || v === undefined) continue;
    if (typeof v === "boolean") lines.push(`${k}: ${v ? "true" : "false"}`);
    else if (typeof v === "number" || typeof v === "bigint" || v instanceof PyFloat) lines.push(`${k}: ${S(v)}`);
    else if (Array.isArray(v)) lines.push(`${k}: [` + v.map(ys).join(", ") + "]");
    else lines.push(`${k}: ${ys(v)}`);
  }
  lines.push("---");
  return lines.join("\n");
}
function L(v: Py): Py[] {
  return Array.isArray(v) ? v : [];
}
export function kids(n: PyDict): Py[] {
  const t = get(n, "type");
  if (t === "cards") return L(get(n, "cards", [])).flatMap((c: Py) => L(get(c, "blocks", [])));
  if (t === "persona") return [...L(or(get(n, "text"), [])), ...L(or(get(n, "media"), [])), ...(truthy(get(n, "today")) ? [n.get("today")] : [])];
  if (t === "today") return [...L(or(get(n, "media"), [])), ...L(or(get(n, "blocks"), []))];
  if (t === "duo") return [...L(or(get(n, "items"), []))];
  if (t === "cols") return L(get(n, "cols", [])).flatMap((col: Py) => L(col));
  if (t === "options") return L(get(n, "options", [])).flatMap((o: Py) => L(get(o, "blocks", [])));
  if (t === "list") return L(get(n, "items", [])).flatMap((it: Py) => L(get(it, "children", [])));
  return [...L(or(get(n, "blocks"), []))];
}
const SQL_LANGS = new Set(["sql", "psql", "postgres", "postgresql", "pgsql", "plpgsql", "mysql", "sqlite"]);
const EXT: Record<string, string> = { ts: "ts", typescript: "ts", tsx: "tsx", js: "js", javascript: "js", mjs: "mjs", jsx: "jsx",
  py: "py", python: "py", sh: "sh", bash: "sh", shell: "sh", zsh: "sh", console: "sh",
  yaml: "yaml", yml: "yaml", toml: "toml", json: "jsonc", jsonc: "jsonc", go: "go", rust: "rs",
  rs: "rs", swift: "swift", kotlin: "kt", kt: "kt", java: "java", cs: "cs", csharp: "cs",
  c: "c", cpp: "cpp", css: "css", graphql: "graphql", gql: "graphql", rb: "rb", ruby: "rb",
  proto: "proto", hcl: "hcl", tf: "tf", dockerfile: "dockerfile" };
const SLASH = new Set(["ts", "tsx", "js", "mjs", "jsx", "jsonc", "go", "rs", "swift", "kt", "java", "cs", "c", "cpp", "proto"]);
const HASH = new Set(["py", "sh", "yaml", "toml", "rb", "graphql", "hcl", "tf", "dockerfile"]);
export function codeExt(lang: Py): string {
  const l = ((lang || "") as string).toLowerCase();
  return SQL_LANGS.has(l) ? "sql" : Object.hasOwn(EXT, l) ? EXT[l]! : "txt";
}
export function comment(ext: string, text: string): string {
  if (ext === "sql") return `-- ${text}`;
  if (SLASH.has(ext)) return `// ${text}`;
  if (HASH.has(ext)) return `# ${text}`;
  if (ext === "css") return `/* ${text} */`;
  return `> ${text}`;
}
export function pad2(n: Py): string {
  return S(n).padStart(2, "0");
}
const DEC_FILE = re.compile(String.raw`([A-Za-z]+)-?(\d+(?:\.\d+)?)$`);
export function decFileName(did: Py): string {
  const m = DEC_FILE.match((did || "") as string);
  return (m ? `${m.group(1)!.toUpperCase()}-${m.group(2)}` : slugify(did)) + ".md";
}
export function readJson(p: string): Py {
  return loads(readText(p));
}

// ============================================================================ the lane

export interface Cx {
  mode: string;
  file?: string | null;
  record?: boolean;
  section?: Py;
  used?: Set<string>;
  cur?: Py;
  whole?: boolean;
  sizing?: boolean;
  ids?: boolean;
}
interface Code { node: PyDict | null; lang: string; text: string; section: Py; anchor: Py; source: Py; file?: string }
interface Img { node: PyDict; kind: string; in_today: boolean; section: Py; anchor: Py; published?: string; row?: Map<string, Py>;
  src_abs?: string | null; boxes?: Map<string, Py>[]; hotspots_file?: string | null }
interface PrRec { pr: PyDict; ms: string; note: string; section: Py }
interface Unit { kind: string; node: PyDict; d: number; crumbs: [string, Py][]; lo?: number; hi?: number; tokens?: number }
interface Slice { section: PyDict; part: number; parts: number; units: Unit[]; path: string; anchors: string[]; tokens?: number }

export class Lane {
  build: string;
  out: string;
  budget: number;
  copyImages: boolean;
  c: PyDict;
  manifest: PyDict;
  meta: PyDict;
  sha: string;
  packet: string | null;
  page: string;
  pageSha: string | null;
  head: string;
  defs: PyDict;
  citeByText = new Map<Py, PyDict>();
  files = new Map<string, FileVal>();
  warn: string[] = [];
  near = new Map<object, Py>();
  anchorSection = new Map<string, Py>();
  codes: Code[] = [];
  images: Img[] = [];
  prs: PrRec[] = [];
  decNodes = new Map<string, PyDict>();
  codeFile = new Map<object, string>();
  imgFile = new Map<string, string>();
  prFile = new Map<Py, string>();
  anchorFile = new Map<string, string>();
  citeOcc: { text: Py; section: Py; anchor: Py; file: Py }[] = [];
  refLinks: Map<string, Py>[] = [];
  slices: Slice[] = [];
  filesPlanned = new Set<string>();
  imageRows: Map<string, Py>[] = [];
  tables: PyDict[] = [];
  ddlFile = new Map<Py, string>();
  decisionIndex: Map<string, Py>[] = [];
  prIndex: Map<string, Py>[] = [];
  citations: Map<string, Py>[] = [];
  tokenIndex = new Map<string, Map<string, Py>>();
  mapCounts = { entities: 0, edges: 0, dangling: 0 };
  overBudget: string[] = [];
  largest: [string | null, number] = [null, 0];

  constructor(build: string, packet: string | null = null, out: string | null = null, budget = BUDGET, copyImages = true) {
    this.build = abspath(build);
    this.out = abspath(out || pjoin(this.build, "agents"));
    this.budget = budget;
    this.copyImages = copyImages;
    this.c = readJson(pjoin(this.build, "content.json"));
    const fpath = pjoin(this.build, "content.files.json");
    this.manifest = isfile(fpath) ? readJson(fpath) : new Map();
    this.meta = or(get(this.c, "meta"), new Map());
    this.sha = or(get(this.meta, "sha"), "");
    let src: Py = or(get(this.c, "source"), "");
    if (packet) this.packet = abspath(packet);
    else {
      const parent = pdirname(this.build);
      this.packet = truthy(src) && isfile(pjoin(parent, src)) ? parent : null;
    }
    this.page = pjoin(this.build, "tdd.html");
    this.pageSha = isfile(this.page) ? sha256(readFileSync(this.page)).slice(0, 12) : null;
    const home = expanduser();
    if (typeof src === "string" && src.startsWith(home + "/")) src = "~" + src.slice(home.length);
    this.head = `agents lane \u00b7 from ${S(src)} \u00b7 rev ${S(or(get(this.meta, "rev"), "-"))} \u00b7 sha ${this.sha || "none"} \u00b7 ` +
      `content ${S(get(this.c, "generated"))} \u00b7 page ${this.pageSha || "none"} \u00b7 ${GENERATOR} \u00b7 generated, do not edit`;
    this.defs = or(get(this.c, "defs"), new Map());
    for (const ct of L(or(get(this.c, "cites"), []))) {
      const k = get(ct, "text");
      if (!this.citeByText.has(k)) this.citeByText.set(k, ct);
    }
  }

  // ------------------------------------------------------------------ helpers
  emit(path: string, text: string): void {
    this.files.set(path, text.endsWith("\n") ? text : text + "\n");
  }
  mdHead(): string {
    return `> ${this.head}`;
  }
  sectionTitle(s: PyDict): string {
    return `${S(s.get("num"))} \u00b7 ${S(or(get(s, "titleText"), plain(get(s, "title"))))}`;
  }
  sections(): PyDict[] {
    return L(or(get(this.c, "sections"), []));
  }

  // ------------------------------------------------------------------ pass 1: annotate the tree in document order
  annotate(): void {
    for (const s of this.sections()) {
      this.anchorSection.set(s.get("id"), s.get("id"));
      this.ann(L(or(get(s, "blocks"), [])), s.get("id"), s.get("id"), false);
    }
    let nSql = 0, nCode = 0;
    for (const blk of this.codes) {
      const ext = codeExt(blk.lang);
      let path: string;
      if (ext === "sql") {
        nSql += 1;
        path = `sql/${pad2(nSql)}-${S(blk.section)}.sql`;
      } else {
        nCode += 1;
        path = `code/${pad2(nCode)}-${S(blk.section)}.${ext}`;
      }
      blk.file = path;
      if (blk.node !== null) this.codeFile.set(blk.node, path);
    }
    for (const img of this.images) {
      const pub: string = or(get(img.node, "src"), "");
      img.published = pub;
      this.imgFile.set(pub, this.copyImages ? "images/" + pub : rel(pjoin(this.build, pub), this.out));
    }
    for (const p of this.prs) {
      this.prFile.set(get(p.pr, "label", p.pr.get("n")), or(get(p.pr, "file"), `prs/pr-${pad2(p.pr.get("n"))}.md`));
    }
    for (const t of L(or(get(this.c, "schema"), []))) {
      if (truthy(get(t, "id"))) this.anchorFile.set(t.get("id"), "schema/schema.md");
    }
  }

  ann(nodes: Py[], enc: Py, sid: Py, inToday: boolean): void {
    let cur = enc;
    for (const n of nodes || []) {
      if (!(n instanceof Map)) continue;
      const t = get(n, "type"), nid = get(n, "id");
      if (t === "heading" && truthy(nid)) {
        cur = nid;
        this.near.set(n, nid);
        this.anchorSection.set(nid, sid);
        continue;
      }
      const here = or(nid, cur);
      this.near.set(n, here);
      if (truthy(nid)) this.anchorSection.set(nid, sid);
      if (t === "code") {
        this.codes.push({ node: n, lang: or(get(n, "lang"), ""), text: or(get(n, "text"), ""), section: sid, anchor: here, source: "page" });
      } else if (t === "diagram") {
        this.images.push({ node: n, kind: "diagram", in_today: inToday, section: sid, anchor: or(nid, here) });
      } else if (t === "shot") {
        this.images.push({ node: n, kind: "mock", in_today: inToday, section: sid, anchor: here });
      } else if (t === "table") {
        for (const r of L(or(get(n, "rows"), []))) if (truthy(get(r, "id"))) this.anchorSection.set(r.get("id"), sid);
      } else if (t === "glossary") {
        for (const it of L(or(get(n, "items"), []))) this.anchorSection.set(it.get("id"), sid);
      } else if (t === "decision" && truthy(nid)) {
        this.decNodes.set(nid, n);
      } else if (t === "prs") {
        for (const g of L(or(get(n, "groups"), []))) {
          if (truthy(get(g, "id"))) this.anchorSection.set(g.get("id"), sid);
          for (const p of L(or(get(g, "prs"), []))) {
            this.anchorSection.set(p.get("id"), sid);
            this.prs.push({ pr: p, ms: plain(get(g, "ms", "")), note: plain(get(g, "note", "")), section: sid });
            sp.render(or(get(p, "bodyHtml"), ""), "txt", {
              pre: (lang, text) => this.codes.push({ node: null, lang, text, section: sid, anchor: p.get("id"),
                source: or(get(p, "file"), `prs/pr-${pad2(p.get("n"))}.md`) }),
            });
          }
        }
      }
      if (t === "cards") {
        for (const c of L(or(get(n, "cards"), []))) {
          if (truthy(get(c, "id"))) this.anchorSection.set(c.get("id"), sid);
          this.ann(L(or(get(c, "blocks"), [])), or(get(c, "id"), here), sid, inToday);
        }
        continue;
      }
      this.ann(kids(n), here, sid, inToday || t === "today");
    }
  }

  // ------------------------------------------------------------------ inline html -> md / txt
  hooks(cx: Cx): sp.Hooks {
    const ref = (aid: string, text: string): string => {
      if (cx.record) this.refLinks.push(new Map<string, Py>([["token", text], ["to", aid], ["from", cx.cur ?? null], ["section", cx.section ?? null], ["file", cx.file ?? null]]));
      if (cx.used !== undefined && aid) cx.used.add(aid);
      return this.link(aid, text, cx);
    };
    const href = (url: string, text: string): string | null => {
      if (url.startsWith("#") && url.length > 1) return this.link(url.slice(1), text, cx);
      if (cx.mode === "txt") return text;
      return null;
    };
    const cite = (text: string): string => {
      const ct = this.citeByText.get(text);
      let shown: string;
      if (truthy(ct)) {
        const path = or(get(ct, "path"), text);
        const lines = truthy(get(ct, "from")) ? `:${S(ct!.get("from"))}` + (truthy(get(ct, "to")) ? `-${S(ct!.get("to"))}` : "") : "";
        shown = S(path) + lines;
      } else shown = text;
      if (cx.record) this.citeOcc.push({ text, section: cx.section ?? null, anchor: cx.cur ?? null, file: cx.file ?? null });
      return cx.mode === "md" ? `\`${shown}\`` : shown;
    };
    const img = (src: string, alt: string): string | null => {
      const target = this.imgFile.get(src);
      if (!target) return null;
      if (cx.mode === "txt") return ` [image: ${alt}] `;
      return ` ![${alt}](${rel(target, dirn(cx.file!))}) `;
    };
    return { ref, href, cite, img };
  }

  link(aid: string, text: string, cx: Cx): string {
    if (cx.mode === "txt") return text;
    if (cx.whole) return `[${text}](#${aid})`;
    if (cx.sizing) return `[${text}](00-placeholder-file.md#${aid})`;
    const tf = this.anchorFile.get(aid);
    if (!tf) return text;
    if (tf === cx.file) return `[${text}](#${aid})`;
    return `[${text}](${rel(tf, dirn(cx.file!))}#${aid})`;
  }

  ih(s: Py, cx: Cx): string {
    return truthy(s) ? sp.renderInline(s, cx.mode, this.hooks(cx)) : "";
  }
  bh(s: Py, cx: Cx, shift: number): string[] {
    return truthy(s) ? sp.render(s, cx.mode, { heading_shift: shift, ...this.hooks(cx) }) : [];
  }
  sfx(aid: Py, cx: Cx): string {
    return truthy(aid) && (cx.ids ?? true) ? ` {#${S(aid)}}` : "";
  }
  bold(s: string, cx: Cx): string {
    return cx.mode === "md" && s ? `**${s}**` : s;
  }
  nearOf(n: object, cx: Cx): Py {
    return this.near.has(n) ? this.near.get(n) : (cx.cur ?? null);
  }

  // ------------------------------------------------------------------ node renderers (return a list of blocks)
  rNodes(nodes: Py, d: number, cx: Cx): string[] {
    const out: string[] = [];
    for (const n of L(nodes)) out.push(...this.rNode(n, d, cx));
    return out;
  }

  rNode(n: Py, d: number, cx: Cx): string[] {
    if (!(n instanceof Map)) return [];
    cx.cur = this.nearOf(n, cx);
    const t = get(n, "type"), md = cx.mode === "md";
    if (t === "p") {
      const s = this.ih(get(n, "html"), cx);
      return s ? [s + this.sfx(get(n, "id"), cx)] : [];
    }
    if (t === "heading") {
      const lvl = Math.max(1, Math.min(6, num(or(get(n, "level"), 3)) + d - 3));
      return ["#".repeat(lvl) + " " + this.ih(get(n, "html"), cx) + this.sfx(get(n, "id"), cx)];
    }
    if (t === "list") {
      const s = this.rList(n, cx);
      return s ? [s] : [];
    }
    if (t === "table") return [this.rTable(n, cx)];
    if (t === "code") return [this.rCode(n, cx)];
    if (t === "callout") {
      const inner = this.rNodes(get(n, "blocks"), d, cx);
      const tone = (or(get(n, "tone"), "note") as string).toLowerCase();
      const label: string | null = tone === "quote" ? null : ({ warn: "WARNING", danger: "CAUTION", good: "TIP" } as Record<string, string>)[tone] ?? "NOTE";
      if (md) {
        const lines = [...(label ? [`[!${label}]`] : []), ...inner.join("\n\n").split("\n")];
        return [lines.map((l) => (l ? "> " + l : ">")).join("\n")];
      }
      return [(label ? `${label}: ` : "> ") + inner.join(" ")];
    }
    if (t === "hr") return ["---"];
    if (t === "placeholder") {
      const s = this.ih(get(n, "html"), cx);
      return [(md ? `*${s}*` : s) + this.sfx(get(n, "id"), cx)];
    }
    if (t === "shot") return this.rShot(n, cx);
    if (t === "legend") return [this.legend(get(n, "items"), cx)];
    if (t === "diagram") return this.rDiagram(n, cx);
    if (t === "cards") {
      const out: string[] = [];
      for (const c of L(or(get(n, "cards"), []))) out.push(...this.rCard(c, d, cx));
      return out;
    }
    if (t === "persona") {
      const out = [...this.rHead(n, d, cx), ...this.rNodes(get(n, "text"), d + 1, cx)];
      for (const m of L(or(get(n, "media"), []))) out.push(...this.rNode(m, d + 1, cx));
      if (truthy(get(n, "today"))) out.push(...this.rNode(n.get("today"), d + 1, cx));
      return out;
    }
    if (t === "today") {
      const out = this.rHead(n, d, cx);
      for (const m of L(or(get(n, "media"), []))) out.push(...this.rNode(m, d, cx));
      return [...out, ...this.rNodes(get(n, "blocks"), d, cx)];
    }
    if (t === "fold" || t === "group" || t === "decision") return [...this.rHead(n, d, cx), ...this.rNodes(get(n, "blocks"), d + 1, cx)];
    if (t === "decisions" || t === "steps" || t === "col" || t === "box") return this.rNodes(get(n, "blocks"), d, cx);
    if (t === "duo") return this.rNodes(get(n, "items"), d, cx);
    if (t === "cols") return L(or(get(n, "cols"), [])).flatMap((col: Py) => this.rNodes(col, d, cx));
    if (t === "options") {
      const out: string[] = [];
      for (const o of L(or(get(n, "options"), []))) {
        out.push(this.bold(this.ih(get(o, "title"), cx), cx) + (truthy(get(o, "rec")) ? " (recommended)" : ""));
        out.push(...this.rNodes(get(o, "blocks"), d, cx));
      }
      return out;
    }
    if (t === "glossary") {
      const lines = L(or(get(n, "items"), [])).map((it: Py) => `- ${this.bold(it.get("term"), cx)}${this.sfx(it.get("id"), cx)}: ${this.ih(get(it, "html"), cx)}`);
      return lines.length ? [lines.join("\n")] : [];
    }
    if (t === "prs") return this.rPrs(n, d, cx);
    if (truthy(get(n, "blocks"))) return this.rNodes(get(n, "blocks"), d, cx);
    return [];
  }

  rHead(n: PyDict, d: number, cx: Cx): string[] {
    cx.cur = this.nearOf(n, cx);
    const t = get(n, "type"), h = "#".repeat(Math.max(1, Math.min(6, d)));
    if (t === "fold") {
      const out = [`${h} ${this.ih(get(n, "title"), cx)}${this.sfx(get(n, "id"), cx)}`];
      const one = this.ih(get(n, "one"), cx);
      if (one) out.push(cx.mode === "md" ? `*${one}*` : one);
      return out;
    }
    if (t === "group") {
      const out = [`${h} ${this.ih(get(n, "title"), cx)}${this.sfx(get(n, "id"), cx)}`];
      const sub = this.ih(get(n, "sub"), cx);
      return [...out, ...(sub ? [sub] : [])];
    }
    if (t === "decision") {
      const st = this.ih(get(n, "status"), cx);
      return [`${h} ${S(get(n, "did"))} \u00b7 ${this.ih(get(n, "question"), cx)}${this.sfx(get(n, "id"), cx)}`,
        `${this.bold("Status:", cx)} ${st || "not stated"} \u00b7 state: ${S(get(n, "state"))}`];
    }
    if (t === "persona") {
      const chip = truthy(get(n, "chip")) ? ` [${S(n.get("chip"))}]` : "";
      return [`${h} ${this.ih(get(n, "title"), cx)}${chip}${this.sfx(get(n, "id"), cx)}`];
    }
    if (t === "today") {
      const s = this.ih(get(n, "summary"), cx);
      return [rstripPy(`${this.bold(S(or(get(n, "label"), "Today")) + ":", cx)} ${s}`)];
    }
    return [];
  }

  rCard(c: PyDict, d: number, cx: Cx): string[] {
    const out: string[] = [], title = this.ih(get(c, "title"), cx), aid = get(c, "id");
    if (truthy(aid)) cx.cur = aid;
    if (title) out.push(this.bold(title, cx) + this.sfx(aid, cx));
    else if (truthy(aid) && (cx.ids ?? true)) out.push(`{#${S(aid)}}`);
    return [...out, ...this.rNodes(get(c, "blocks"), d + 1, cx)];
  }

  rList(n: PyDict, cx: Cx, lo = 0, hi: number | null = null): string {
    const all = L(or(get(n, "items"), []));
    const items = all.slice(lo, hi ?? undefined);
    let k = num(or(get(n, "start"), 1)) + lo;
    const lines: string[] = [];
    for (const it of items) {
      const marker = truthy(get(n, "ordered")) ? `${S(k)}.` : "-";
      k += 1;
      let text = this.ih(get(it, "html"), cx);
      if (get(it, "check") !== null) text = (truthy(it.get("check")) ? "[x] " : "[ ] ") + text;
      if (truthy(get(it, "cls"))) text = L(it.get("cls")).map((c: Py) => `[${S(c)}]`).join(" ") + " " + text;
      const pad = " ".repeat(cpLen(marker) + 1);
      const [first, ...rest] = text.split("\n");
      lines.push(rstripPy(`${marker} ${first}`));
      lines.push(...rest.map((r) => pad + r));
      for (const ch of L(or(get(it, "children"), []))) {
        const sub = this.rList(ch, cx);
        lines.push(...sub.split("\n").map((l) => (l ? pad + l : l)));
      }
    }
    return lines.join("\n");
  }

  rTable(n: PyDict, cx: Cx, lo = 0, hi: number | null = null): string {
    const base = this.nearOf(n, cx);
    const head = L(or(get(n, "head"), [])).map((h: Py) => this.ih(get(h, "html"), cx));
    const body: string[][] = [];
    for (const r of L(or(get(n, "rows"), [])).slice(lo, hi ?? undefined)) {
      cx.cur = or(get(r, "id"), base);
      const cells = L(or(get(r, "cells"), [])).map((c: Py) => this.ih(get(c, "html"), cx));
      if (truthy(get(r, "id")) && cells.length) cells[0] += this.sfx(r.get("id"), cx);
      body.push(cells);
    }
    cx.cur = base;
    if (cx.mode === "txt") return [...(head.some((x) => x) ? [head.join(" | ")] : []), ...body.map((r) => r.join(" | "))].join("\n");
    const ncol = Math.max(head.length, ...body.map((r) => r.length), 1);
    const esc = (s: string) => s.replaceAll("|", "\\|").replaceAll("\n", " ");
    const line = (cells: string[]) => "| " + [...cells, ...Array<string>(Math.max(0, ncol - cells.length)).fill("")].map(esc).join(" | ") + " |";
    const heads = L(or(get(n, "head"), [])), seps: string[] = [];
    for (let j = 0; j < ncol; j++) {
      const al = j < heads.length ? get(heads[j], "align") : null;
      seps.push(({ left: ":---", center: ":---:", right: "---:" } as Record<string, string>)[al as string] ?? "---");
    }
    return [line(head), "| " + seps.join(" | ") + " |", ...body.map(line)].join("\n");
  }

  rCode(n: PyDict, cx: Cx): string {
    const lang: string = or(get(n, "lang"), ""), text = pystrip(or(get(n, "text"), "") as string, "\n");
    const path = this.codeFile.get(n);
    const lines = text.split("\n");
    if (cx.mode === "txt") return lines.map((l) => "    " + l).join("\n");
    const here = dirn(cx.file || "tdd.md");
    if (path && !cx.whole && lines.length > CODE_INLINE_MAX) {
      return `*${lang || "code"} block, ${lines.length} lines, ~${tok(text)} tokens: [${path}](${rel(path, here)})*`;
    }
    const fence = text.includes("```") ? "~~~~" : "```";
    let s = `${fence}${lang}\n${text}\n${fence}`;
    if (path) s += `\n\n*Extracted to [${path}](${rel(path, here)}).*`;
    return s;
  }

  imgRel(pub: string, cx: Cx): string {
    const target = this.imgFile.get(pub) || pub;
    return rel(target, dirn(cx.file || "tdd.md"));
  }

  rShot(n: PyDict, cx: Cx): string[] {
    const alt = (or(get(n, "alt"), "") as string).replace(/[\[\]]/g, "");
    const cap = this.ih(get(n, "caption"), cx);
    if (cx.mode === "txt") return [pystrip(`[mock: ${cap}] ${alt}`)];
    const out = [`![${alt}](${this.imgRel(or(get(n, "src"), ""), cx)})`];
    if (cap) out[0] += `\n*${cap}*`;
    return out;
  }

  legend(items: Py, cx: Cx): string {
    return "Legend: " + L(or(items, [])).map((i: Py) => `${this.ih(get(i, "label"), cx)} = ${S(get(i, "tone"))}`).join(" \u00b7 ");
  }

  rDiagram(n: PyDict, cx: Cx): string[] {
    cx.cur = or(get(n, "id"), cx.cur ?? null);
    const title = this.ih(get(n, "title"), cx);
    const alt = (or(get(n, "alt"), "") as string).replace(/[\[\]]/g, "");
    const out: string[] = [];
    if (cx.mode === "md") {
      out.push(`**Figure \u00b7 ${title}**${this.sfx(get(n, "id"), cx)}`);
      out.push(`![${alt}](${this.imgRel(or(get(n, "src"), ""), cx)})`);
    } else {
      out.push(`[diagram: ${title}]${this.sfx(get(n, "id"), cx)}` + (alt && alt !== plain(get(n, "title")) ? ` ${alt}` : ""));
    }
    const tail: string[] = [];
    if (truthy(get(n, "legend"))) tail.push(this.legend(n.get("legend"), cx));
    if (truthy(get(n, "reading"))) tail.push("Reading: " + this.ih(n.get("reading"), cx));
    const side = this.sidecarPath(n);
    if (side && cx.mode === "md" && this.filesPlanned.has(side)) tail.push(`Box text: [${side}](${rel(side, dirn(cx.file || "tdd.md"))})`);
    if (tail.length) out.push(cx.mode === "md" ? tail.join("\n") : tail.join(" "));
    return out;
  }

  rPrs(n: PyDict, d: number, cx: Cx): string[] {
    const out: string[] = [];
    for (const g of L(or(get(n, "groups"), []))) {
      out.push("#".repeat(Math.max(1, Math.min(6, d))) + " " + plain(get(g, "ms", "")) + (truthy(get(g, "note")) ? ` \u00b7 ${this.ih(g.get("note"), cx)}` : "") + this.sfx(get(g, "id"), cx));
      for (const p of L(or(get(g, "prs"), []))) {
        cx.cur = p.get("id");
        out.push("#".repeat(Math.max(1, Math.min(6, d + 1))) + ` PR ${S(get(p, "label", p.get("n")))} \u00b7 ${this.ih(get(p, "title"), cx)}${this.sfx(p.get("id"), cx)}`);
        const lines = [`${this.bold("Change:", cx)} ${this.ih(get(p, "change"), cx)}`];
        if (truthy(get(p, "rollback"))) lines.push(`${this.bold("Rollback:", cx)} ${this.ih(p.get("rollback"), cx)}`);
        const pf = this.prFile.get(get(p, "label", p.get("n")));
        if (cx.mode === "md" && pf) lines.push(`${this.bold("Deep dive:", cx)} [${pf}](${rel(pf, dirn(cx.file || "tdd.md"))})`);
        out.push(cx.mode === "md" ? lines.join("\n") : lines.join(" "));
        if (cx.whole) out.push(...this.bh(get(p, "bodyHtml"), cx, d + 2 - 2));
      }
    }
    return out;
  }

  // ------------------------------------------------------------------ slicing
  anchorsUnder(x: Py): string[] {
    const found: string[] = [];
    const walk = (v: Py): void => {
      if (v instanceof Map) {
        const id = v.get("id");
        if (typeof id === "string" && this.defs.has(id)) found.push(id);
        for (const [k, w] of v) if (k !== "hotspots" && k !== "legend" && k !== "head") walk(w);
      } else if (Array.isArray(v)) for (const w of v) walk(w);
    };
    walk(x);
    return found;
  }

  unitAnchors(u: Unit): string[] {
    const k = u.kind, n = u.node;
    if (k === "head") return this.defs.has(get(n, "id")) && get(n, "id") !== null ? [n.get("id")] : [];
    if (k === "rows") return L(or(get(n, "rows"), [])).slice(u.lo!, u.hi!).filter((r: Py) => this.defs.has(get(r, "id"))).map((r: Py) => r.get("id"));
    if (k === "items") return [];
    return uniq(this.anchorsUnder(n));
  }

  renderUnit(u: Unit, cx: Cx): string[] {
    const k = u.kind, n = u.node, d = u.d;
    if (k === "node") return this.rNode(n, d, cx);
    if (k === "head") return this.rHead(n, d, cx);
    if (k === "card") return this.rCard(n, d, cx);
    cx.cur = this.nearOf(n, cx);
    if (k === "rows") return [this.rTable(n, cx, u.lo!, u.hi!)];
    if (k === "items") return [this.rList(n, cx, u.lo!, u.hi!)];
    return [];
  }

  unitTokens(u: Unit, sid: Py): number {
    const cx: Cx = { mode: "md", file: "sections/x.md", sizing: true, section: sid, used: new Set() };
    const text = this.renderUnit(u, cx).join("\n\n");
    return tok(text) + 25 * cx.used!.size;
  }

  expand(u: Unit): Unit[] | null {
    const n = u.node, d = u.d, crumbs = u.crumbs;
    const t = u.kind === "node" ? get(n, "type") : null;
    const mk = (kind: string, node: Py, dd: number, extra: { lo?: number; hi?: number } = {}, cr?: [string, Py][]): Unit =>
      ({ kind, node, d: dd, crumbs: cr !== undefined ? cr : crumbs, ...extra });
    if (t === "group" || t === "fold" || t === "decision" || t === "persona" || t === "today") {
      let title = plain(or(or(or(get(n, "title"), get(n, "question")), get(n, "label")), ""));
      if (t === "decision") title = `${S(get(n, "did"))} \u00b7 ${title}`;
      const cr: [string, Py][] = [...crumbs, [clip(title, 60), get(n, "id")]];
      let ch: Unit[];
      if (t === "persona") {
        ch = [...L(or(get(n, "text"), [])), ...L(or(get(n, "media"), []))].map((x) => mk("node", x, d + 1, {}, cr));
        if (truthy(get(n, "today"))) ch.push(mk("node", n.get("today"), d + 1, {}, cr));
      } else if (t === "today") {
        ch = [...L(or(get(n, "media"), [])), ...L(or(get(n, "blocks"), []))].map((x) => mk("node", x, d, {}, cr));
      } else {
        ch = L(or(get(n, "blocks"), [])).map((x: Py) => mk("node", x, d + 1, {}, cr));
      }
      return [mk("head", n, d), ...ch];
    }
    if (t === "decisions" || t === "steps" || t === "col" || t === "box") return L(or(get(n, "blocks"), [])).map((x: Py) => mk("node", x, d));
    if (t === "duo") return L(or(get(n, "items"), [])).map((x: Py) => mk("node", x, d));
    if (t === "cols") return L(or(get(n, "cols"), [])).flatMap((col: Py) => L(col).map((x: Py) => mk("node", x, d)));
    if (t === "cards") return L(or(get(n, "cards"), [])).map((c: Py) => mk("card", c, d));
    if (t === "table" || t === "list") {
      const key = t === "table" ? "rows" : "items";
      const seq = L(or(get(n, key), []));
      if (seq.length < 2) return null;
      const limit = this.budget * PACK;
      const chunks: [number, number][] = [];
      let lo = 0, size = 0;
      for (let i = 0; i < seq.length; i++) {
        const one = this.unitTokens(mk(key, n, d, { lo: i, hi: i + 1 }), "x");
        if (i > lo && size + one > limit) {
          chunks.push([lo, i]);
          lo = i;
          size = 0;
        }
        size += one;
      }
      chunks.push([lo, seq.length]);
      if (chunks.length < 2) return null;
      return chunks.map(([a, b]) => mk(key, n, d, { lo: a, hi: b }));
    }
    return null;
  }

  fit(units: Unit[], sid: Py, limit: number): Unit[] {
    const out: Unit[] = [];
    for (const u of units) {
      u.tokens = this.unitTokens(u, sid);
      if (u.tokens > limit) {
        const sub = this.expand(u);
        if (sub && sub.length) {
          out.push(...this.fit(sub, sid, limit));
          continue;
        }
      }
      out.push(u);
    }
    return out;
  }

  sliceSections(): void {
    const limit = this.budget * PACK;
    for (const s of this.sections()) {
      const sid = s.get("id"), numS = s.get("num");
      const units = this.fit(L(or(get(s, "blocks"), [])).map((b: Py) => ({ kind: "node", node: b, d: 3, crumbs: [] })), sid, limit);
      const parts: Unit[][] = [];
      let cur: Unit[] = [], size = 400;
      for (const u of units) {
        if (cur.length && size + u.tokens! > limit) {
          const carry: Unit[] = [];
          while (cur.length) {
            const last = cur[cur.length - 1]!;
            if (last.kind === "head" || (last.kind === "node" && get(last.node, "type") === "heading")) carry.unshift(cur.pop()!);
            else break;
          }
          if (cur.length) parts.push(cur);
          cur = carry;
          size = 400 + carry.reduce((a, x) => a + x.tokens!, 0);
        }
        cur.push(u);
        size += u.tokens!;
      }
      if (cur.length || !parts.length) parts.push(cur);
      const total = parts.length;
      parts.forEach((pu, idx) => {
        const k = idx + 1;
        const path = k === 1 ? `sections/${pad2(numS)}-${S(sid)}.md` : `sections/${pad2(numS)}-${S(sid)}-part-${k}.md`;
        const anchors = uniq([...(k === 1 ? [sid] : []), ...pu.flatMap((u) => this.unitAnchors(u))]);
        for (const a of anchors) if (!this.anchorFile.has(a)) this.anchorFile.set(a, path);
        this.slices.push({ section: s, part: k, parts: total, units: pu, path, anchors });
      });
    }
  }

  writeSlices(): void {
    this.slices.forEach((sl, si) => {
      const s = sl.section, k = sl.part, total = sl.parts, path = sl.path;
      const sid = s.get("id");
      const cx: Cx = { mode: "md", file: path, record: true, section: sid, used: new Set(), cur: sid };
      const title = this.sectionTitle(s) + (k > 1 ? ` (part ${k})` : "");
      const chip = truthy(get(s, "chip")) ? ` [${S(s.get("chip"))}]` : "";
      const blocks: string[] = [];
      if (k === 1) blocks.push(`## ${this.sectionTitle(s)}${chip} {#${S(sid)}}`);
      else {
        const first = this.slices[si - 1]!.path;
        blocks.push(`## ${title}`);
        blocks.push(`*Continues [part ${k - 1}](${rel(first, "sections")}) of section ${S(s.get("num"))} ` +
          `([#${S(sid)}](${rel(this.anchorFile.get(sid) ?? path, "sections")}#${S(sid)})).*`);
      }
      const crumbs = sl.units.length ? sl.units[0]!.crumbs : [];
      if (k > 1 && crumbs.length) {
        const trail = crumbs.map(([t, a]) => (truthy(a) ? `[${t}](${rel(this.anchorFile.get(a) ?? path, "sections")}#${S(a)})` : t)).join(" \u203a ");
        blocks.push(`*Inside: ${trail}.*`);
      }
      for (const u of sl.units) blocks.push(...this.renderUnit(u, cx));
      if (k < total) {
        const nxt = this.slices[si + 1]!.path;
        blocks.push(`*Continued in [part ${k + 1}](${rel(nxt, "sections")}).*`);
      }
      const ext = sorted(cx.used!).filter((a) => !sl.anchors.includes(a) && this.defs.has(a));
      if (ext.length) {
        const lines = [`**Defined elsewhere** (${ext.length}; one line each, follow the link for the full text):`];
        for (const a of ext.slice(0, 60)) {
          const df = this.defs.get(a);
          const f = this.anchorFile.get(a);
          const target = f ? `${rel(f, "sections")}#${a}` : `#${a}`;
          let extra = "";
          if (get(df, "kind") === "decision") {
            const dx = L(or(get(this.c, "decisions"), [])).find((x: Py) => x.get("anchor") === a);
            const did = dx !== undefined ? dx.get("id") : null;
            if (truthy(did)) extra = ` \u00b7 [record](../decisions/${decFileName(did)})`;
          }
          lines.push(`- [${a}](${target}) \u00b7 ${S(get(df, "kind"))} \u00b7 ${clip(get(df, "title"), 110)}${extra}`);
        }
        blocks.push(lines.join("\n"));
      }
      const body = blocks.filter((b) => b).join("\n\n");
      const fm = (tokens: number): [string, Py][] => [["id", sid], ["title", title], ["section", s.get("num")], ["part", k], ["parts", total],
        ["tokens", tokens], ["anchors", sl.anchors], ["generated", this.head]];
      let text = front(fm(0)) + "\n\n" + body + "\n";
      const tokens = tok(text) + 2;
      text = front(fm(tokens)) + "\n\n" + body + "\n";
      sl.tokens = tokens;
      this.emit(path, text);
    });
  }

  // ------------------------------------------------------------------ whole document
  writeWhole(): void {
    const c = this.c, m = this.meta;
    for (const [mode, path] of [["md", "tdd.md"], ["txt", "tdd.txt"]] as const) {
      const cx: Cx = { mode, file: path, whole: true, record: false, cur: null };
      const md = mode === "md";
      const blocks = [`> ${this.head}`, `# ${S(or(get(c, "titleText"), plain(get(c, "title"))))}`];
      const facts: string[] = [];
      if (truthy(get(m, "eyebrow"))) facts.push(plain(m.get("eyebrow")));
      const proj = get(m, "project");
      if (proj instanceof Map && truthy(get(proj, "label"))) {
        const lab: string = proj.get("label");
        if (md) facts.push("Project " + (/^\[[^\n]*\]\([^\n]*\)(?=\n?$)/.test(lab) ? lab : truthy(get(proj, "href")) ? `[${lab}](${S(get(proj, "href"))})` : lab));
        else facts.push("Project " + lab.replace(/^\[([^\n]*?)\]\([^\n]*\)(?=\n?$)/, "$1"));
      }
      for (const p of L(or(get(m, "people"), []))) facts.push(`${S(get(p, "role"))} ${S(get(p, "name"))}`);
      facts.push(`Updated ${S(or(get(m, "updated"), "not stated"))}` + (truthy(get(m, "rev")) ? ` (rev ${S(m.get("rev"))}: ${S(get(m, "revNote"))})` : ""));
      facts.push(`Status: ${S(get(m, "status"))}`);
      if (truthy(get(m, "repoUrl")) || this.sha) {
        facts.push(`Code base ${S(or(or(get(m, "repoUrl"), get(m, "repoPath")), ""))} at ${this.sha || "no sha"}` + (truthy(get(m, "shaDate")) ? ` (${S(m.get("shaDate"))})` : ""));
      }
      blocks.push(facts.join(" \u00b7 "));
      if (truthy(get(m, "pathAliases"))) {
        blocks.push("Path aliases in citations: " + [...(m.get("pathAliases") as PyDict)].map(([k, v]) => `${k} = ${S(v)}`).join(", ") +
          " (citations below are written out in full).");
      }
      for (const p of L(or(get(c, "lede"), []))) blocks.push(...this.rNode(p, 3, cx));
      const toc: string[] = [];
      for (const s of this.sections()) toc.push(md ? `- [${this.sectionTitle(s)}](#${S(s.get("id"))})` : `- ${this.sectionTitle(s)}`);
      blocks.push((md ? "## Contents\n\n" : "## Contents\n") + toc.join("\n"));
      for (const s of this.sections()) {
        const chip = truthy(get(s, "chip")) ? ` [${S(s.get("chip"))}]` : "";
        blocks.push(`## ${this.sectionTitle(s)}${chip} {#${S(s.get("id"))}}`);
        blocks.push(...this.rNodes(get(s, "blocks"), 3, cx));
      }
      if (this.tables.length) {
        blocks.push("## Schema tables");
        blocks.push(...this.schemaBlocks(cx, true));
      }
      if (truthy(get(m, "footer"))) blocks.push("---", plain(m.get("footer")));
      this.emit(path, blocks.filter((b) => b).join(md ? "\n\n" : "\n"));
    }
  }

  // ------------------------------------------------------------------ decisions
  writeDecisions(): void {
    const index: Map<string, Py>[] = [];
    const refsBy = new Map<Py, Set<Py>>();
    for (const l of this.refLinks) {
      if (!refsBy.has(l.get("to"))) refsBy.set(l.get("to"), new Set());
      refsBy.get(l.get("to"))!.add(l.get("section"));
    }
    for (const dc of L(or(get(this.c, "decisions"), []))) {
      const did = dc.get("id"), aid = dc.get("anchor");
      const path = "decisions/" + decFileName(did);
      const n = this.decNodes.get(aid);
      const cx: Cx = { mode: "md", file: path, ids: false, record: false, cur: aid, section: get(dc, "section") };
      const date = DATE.search((or(get(dc, "status"), "")) as string);
      const secFile = this.anchorFile.get(aid);
      const referenced = sorted([...(refsBy.get(aid) ?? new Set())].filter((x) => truthy(x) && x !== get(dc, "section")) as string[]);
      const fm: [string, Py][] = [["id", did], ["anchor", aid], ["status", or(get(dc, "status"), "not stated")], ["state", get(dc, "state")],
        ["date", date ? date.group(0) : null], ["question", get(dc, "question")], ["section", get(dc, "section")],
        ["section_file", secFile ?? null], ["referenced_from", referenced], ["generated", this.head]];
      const blocks = [`# ${S(did)} \u00b7 ${S(get(dc, "question"))}`,
        `**Status:** ${S(or(get(dc, "status"), "not stated"))} \u00b7 state: ${S(get(dc, "state"))}` +
        (secFile ? ` \u00b7 on the page in [${secFile}#${S(aid)}](${rel(secFile, "decisions")}#${S(aid)})` : "")];
      if (truthy(n)) blocks.push(...this.rNodes(get(n, "blocks"), 2, cx));
      this.emit(path, front(fm) + "\n\n" + blocks.join("\n\n"));
      index.push(new Map<string, Py>([["id", did], ["anchor", aid], ["question", get(dc, "question")], ["status", get(dc, "status")],
        ["state", get(dc, "state")], ["date", date ? date.group(0) : null], ["section", get(dc, "section")], ["section_file", secFile ?? null],
        ["file", path], ["referenced_from", referenced]]));
    }
    if (index.length) this.emit("decisions/decisions.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["decisions", index]])));
    this.decisionIndex = index;
  }

  // ------------------------------------------------------------------ citations, refs
  recordPrBodies(): void {
    for (const p of this.prs) {
      const pr = p.pr;
      const cx: Cx = { mode: "txt", file: this.prFile.get(get(pr, "label", pr.get("n"))) ?? null, record: true, section: p.section, cur: pr.get("id") };
      this.bh(get(pr, "bodyHtml"), cx, 0);
    }
  }

  writeCitations(): void {
    const occ: Map<string, Py>[] = [];
    this.citeOcc.forEach((o, i) => {
      const ct = or(this.citeByText.get(o.text), new Map());
      occ.push(new Map<string, Py>([["id", `CIT.${S(i + 1).padStart(4, "0")}`], ["text", o.text], ["path", or(get(ct, "path"), o.text)],
        ["from", get(ct, "from")], ["to", get(ct, "to")], ["sha", this.sha || null], ["href", get(ct, "href")], ["section", o.section],
        ["anchor", o.anchor], ["file", o.file]]));
    });
    this.citations = occ;
    const distinct = new Set(this.citeOcc.map((o) => o.text));
    const cites = L(or(get(this.c, "cites"), []));
    const notRendered = sorted(new Set(cites.map((ct: Py) => ct.get("text")).filter((t: Py) => !distinct.has(t))) as Set<string>);
    this.emit("citations.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["sha", this.sha || null], ["sha_date", get(this.meta, "shaDate")],
      ["path_aliases", or(get(this.meta, "pathAliases"), new Map())],
      ["counts", new Map<string, Py>([["on_page", occ.length], ["distinct_paths", new Set(occ.map((o) => o.get("path"))).size],
        ["compiler_cites", cites.length], ["compiler_only", notRendered]])],
      ["citations", occ]])));
    const by = new Map<string, Map<string, Py>[]>();
    for (const o of occ) {
      const k = o.get("path");
      if (!by.has(k)) by.set(k, []);
      by.get(k)!.push(o);
    }
    const lines = [this.mdHead(), "", `# Citations by file (${occ.length} on the page, ${by.size} files, at sha ${this.sha || "none"})`, "",
      "Re-read a cited file at the sha before editing (`git show " + (this.sha || "<sha>") + ":<path>`); if it drifted, report it."];
    for (const path of sorted(by.keys())) {
      lines.push("", `## ${path}`);
      for (const o of by.get(path)!) {
        const rng = truthy(o.get("from")) ? `L${S(o.get("from"))}` + (truthy(o.get("to")) ? `-${S(o.get("to"))}` : "") : "whole file";
        const frag = truthy(o.get("anchor")) && this.anchorFile.get(o.get("anchor")) === o.get("file") ? `#${S(o.get("anchor"))}` : "";
        const where = truthy(o.get("file")) ? `[${S(o.get("file"))}${frag}](${S(o.get("file"))}${frag})` : "";
        lines.push(`- ${rng} \u00b7 section ${S(o.get("section"))} \u00b7 ${where}` + (truthy(o.get("href")) ? ` \u00b7 [source](${S(o.get("href"))})` : ""));
      }
    }
    this.emit("citations.md", lines.join("\n"));
  }

  writeRefs(): void {
    const anchors = new Map<string, Map<string, Py>>();
    for (const a of L(or(get(this.c, "anchors"), []))) {
      const df = get(this.defs, a.get("id"), new Map());
      anchors.set(a.get("id"), new Map<string, Py>([["kind", get(a, "kind")], ["title", get(a, "title")], ["text", get(df, "text", "")],
        ["section", this.anchorSection.get(a.get("id")) ?? null], ["file", this.anchorFile.get(a.get("id")) ?? null]]));
    }
    const tokens = new Map<string, Map<string, Py>>();
    for (const l of this.refLinks) {
      const k = l.get("token");
      if (!tokens.has(k)) tokens.set(k, new Map<string, Py>([["anchor", l.get("to")], ["file", this.anchorFile.get(l.get("to")) ?? null], ["uses", 0]]));
      const t = tokens.get(k)!;
      t.set("uses", t.get("uses") + 1);
    }
    this.tokenIndex = tokens;
    const refs = or(get(this.c, "refs"), new Map());
    this.emit("refs.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["linked", get(refs, "linked")], ["unlinked", or(get(refs, "unlinked"), [])],
      ["families", or(get(this.meta, "refs"), new Map())], ["glossary", or(get(this.c, "glossary"), new Map())],
      ["anchors", [...anchors].map(([k, v]) => new Map<string, Py>([["id", k], ...v]))],
      ["tokens", sorted(tokens.keys()).map((k) => new Map<string, Py>([["token", k], ...tokens.get(k)!]))],
      ["links", this.refLinks]])));
  }

  // ------------------------------------------------------------------ sql / code
  writeCode(): void {
    const totals = new Map<string, number>();
    for (const b of this.codes) {
      const folder = b.file!.split("/")[0]!;
      totals.set(folder, (totals.get(folder) ?? 0) + 1);
    }
    const idx = new Map<string, number>();
    for (const b of this.codes) {
      const folder = b.file!.split("/")[0]!;
      idx.set(folder, (idx.get(folder) ?? 0) + 1);
      const ext = b.file!.slice(b.file!.lastIndexOf(".") + 1);
      const s = this.sections().find((x) => x.get("id") === b.section);
      const where = s !== undefined ? `section ${this.sectionTitle(s)} (#${S(b.section)})` : `section ${S(b.section)}`;
      let info = `${where} \u00b7 anchor #${S(b.anchor)} \u00b7 slice ${this.anchorFile.get(b.anchor) || this.anchorFile.get(b.section) || "-"}`;
      if (b.source !== "page") info += ` \u00b7 from ${S(b.source)}`;
      info += ` \u00b7 block ${idx.get(folder)} of ${totals.get(folder)} \u00b7 lang ${b.lang || "none"}`;
      this.emit(b.file!, comment(ext, this.head) + "\n" + comment(ext, info) + "\n\n" + pystrip(b.text, "\n"));
    }
  }

  // ------------------------------------------------------------------ schema (D7)
  schemaTables(): [PyDict[], string | null] {
    const tables = L(or(get(this.c, "schema"), []));
    if (tables.length) return [tables, "content.json"];
    const sdir = this.packet ? pjoin(this.packet, "schema") : null;
    if (sdir && isdir(sdir)) {
      const raw: PyDict[] = [];
      for (const f of sorted(readdirSync(sdir))) {
        if (!f.endsWith(".json")) continue;
        let t: Py;
        try {
          t = readJson(pjoin(sdir, f));
        } catch (e) {
          this.warn.push(`schema/${f}: unreadable (${(e as Error).message})`);
          continue;
        }
        if (!t.has("table")) t.set("table", splitext(f)[0]);
        raw.push(t);
      }
      if (raw.length) this.warn.push(`content.json has no schema tables but the packet has ${raw.length} schema/*.json: rebuild with build_tdd.py; copied the raw files`);
      return [raw, "packet schema/ (raw, content.json predates it)"];
    }
    return [[], null];
  }

  schemaBlocks(cx: Cx, ids: boolean): string[] {
    const out: string[] = [];
    for (const t of this.tables) {
      const name = get(t, "table"), tid = get(t, "id");
      out.push(`### ${S(name)} (${S(get(t, "state", "existing"))})` + (ids && truthy(tid) ? ` {#${S(tid)}}` : ""));
      if (truthy(get(t, "summary"))) out.push(S(t.get("summary")));
      const cols = L(or(get(t, "columns"), []));
      if (cols.length) {
        const rows = ["| column | type | change | note | was |", "| --- | --- | --- | --- | --- |"];
        for (const col of cols) {
          rows.push("| " + ["name", "type", "change", "note", "was"].map((k) => S(or(get(col, k), "")).replaceAll("|", "\\|").replaceAll("\n", " ")).join(" | ") + " |");
        }
        out.push(rows.join("\n"));
      }
      for (const [label, key, fields] of [["Measured", "measured", ["value", "unit", "source", "date", "note"]], ["Expected", "expected", ["value", "unit", "note"]]] as const) {
        const vals = or(get(t, key), new Map());
        if (truthy(vals)) {
          const rows = [`| ${label.toLowerCase()} | ` + fields.join(" | ") + " |", "| --- | " + fields.map(() => "---").join(" | ") + " |"];
          for (const [metric, v0] of vals as PyDict) {
            const v: PyDict = v0 instanceof Map ? v0 : new Map([["value", v0]]);
            rows.push(`| ${metric} | ` + fields.map((f) => S(get(v, f) !== null ? get(v, f, "") : "").replaceAll("|", "\\|")).join(" | ") + " |");
          }
          out.push(`**${label}**\n\n` + rows.join("\n"));
        } else out.push(key === "measured" ? `**${label}:** not measured` : `**${label}:** not stated`);
      }
      if (truthy(get(t, "sample_rows"))) {
        out.push(`Sample rows: ${L(t.get("sample_rows")).length} in schema/${slugify(name)}.json` + (truthy(get(t, "sample_source")) ? ` (source: ${S(t.get("sample_source"))})` : ""));
      }
      if (truthy(get(t, "ddl"))) {
        const f = this.ddlFile.get(name);
        out.push(f ? `DDL: [${f}](${rel(f, dirn(cx.file || "tdd.md"))})` : "DDL: in the JSON");
      }
      for (const note of L(or(get(t, "notes"), []))) out.push(`- ${S(note)}`);
    }
    return out;
  }

  writeSchema(): void {
    const [tables, origin] = this.schemaTables();
    this.tables = tables;
    this.ddlFile = new Map();
    if (!this.tables.length) return;
    let nSql = this.codes.filter((b) => b.file!.startsWith("sql/")).length;
    for (const t of this.tables) {
      const name = get(t, "table");
      if (truthy(get(t, "ddl"))) {
        nSql += 1;
        const f = `sql/${pad2(nSql)}-schema-${slugify(name)}.sql`;
        this.ddlFile.set(name, f);
        this.emit(f, comment("sql", this.head) + "\n" + comment("sql", `DDL of table ${S(name)} (${S(get(t, "state"))}) from schema/${slugify(name)}.json`) + "\n\n" + pystrip(t.get("ddl"), "\n"));
      }
      this.emit(`schema/${slugify(name)}.json`, dumps(new Map<string, Py>([["_generated", this.head + ` \u00b7 from ${S(origin)}`], ...t]), 1));
    }
    const cx: Cx = { mode: "md", file: "schema/schema.md" };
    this.emit("schema/schema.md", [this.mdHead(), `# Schema tables (${this.tables.length})`,
      "Change flags: new | changed | dropped | unchanged. A missing measurement reads \"not measured\"; nothing here is estimated by this tool. " +
      "Table anchors ({#tbl-...}) are defined here, not in sections/: on the page they open in the schema drawer.",
      ...this.schemaBlocks(cx, true)].join("\n\n"));
  }

  // ------------------------------------------------------------------ PRs
  writePrs(): void {
    if (!this.prs.length) return;
    const rows: string[] = [], idx: Map<string, Py>[] = [];
    const pdir = this.packet ? pjoin(this.packet, "prs") : null;
    for (const p of this.prs) {
      const pr = p.pr, n = get(pr, "label", pr.get("n"));
      const path = this.prFile.get(n)!;
      const fileOr = (dflt: () => string): string => (truthy(get(pr, "file")) ? S(pr.get("file")) : dflt());
      const src = pdir ? pjoin(pdir, basename(fileOr(() => `pr-${pyIntFmt(n)}.md`))) : null;
      let text: string, origin: string;
      if (src && isfile(src)) {
        let raw = readText(src);
        raw = re.sub(String.raw`\]\(#([\w\-]+)\)`, (m) => {
          const g1 = m.group(1)!;
          return this.anchorFile.has(g1) ? `](${rel(this.anchorFile.get(g1)!, "prs")}#${g1})` : m.group(0)!;
        }, raw);
        const note = `# generated copy of ${fileOr(() => `prs/pr-${pyIntFmt(n)}.md`)} (page links point into sections/) \u00b7 ${this.head}`;
        if (raw.startsWith("---\n")) text = "---\n" + note + "\n" + raw.slice(4);
        else text = `> ${cpSlice(note, 2)}\n\n` + raw;
        origin = "packet copy";
      } else {
        const cx: Cx = { mode: "md", file: path, ids: false };
        text = [this.mdHead(), `# PR ${S(n)}: ${plain(get(pr, "title"))}`, ...this.bh(get(pr, "bodyHtml"), cx, 0)].join("\n\n");
        origin = "rendered from content.json";
      }
      this.emit(path, text);
      const row = new Map<string, Py>([["n", n], ["anchor", pr.get("id")], ["milestone", p.ms], ["title", plain(get(pr, "title"))],
        ["change", plain(get(pr, "change"))], ["rollback", plain(get(pr, "rollback")) || "not stated"], ["file", path],
        ["slice", this.anchorFile.get(pr.get("id")) ?? null], ["origin", origin]]);
      idx.push(row);
      const esc = (s: Py) => ((s || "") as string).replaceAll("|", "\\|");
      rows.push(`| [${S(n)}](${rel(path, "prs")}) | ${esc(row.get("milestone"))} | ${esc(row.get("title"))} | ${esc(row.get("change"))} | ${esc(row.get("rollback"))} |`);
    }
    this.prIndex = idx;
    this.emit("prs/prs.md", [this.mdHead(), "", `# PRs (${idx.length}, in merge order)`, "",
      "| PR | milestone | title | change | rollback |", "| --- | --- | --- | --- | --- |", ...rows].join("\n"));
    this.emit("prs/prs.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["prs", idx]])));
  }

  // ------------------------------------------------------------------ images
  sidecarPath(n: PyDict): string | null {
    const pub: string = or(get(n, "src"), "");
    return pub ? "images/" + splitext(pub)[0] + ".md" : null;
  }

  hotspotBoxes(key: string, srcAbs: string | null): [string | null, Map<string, Py>[]] {
    const cands: string[] = [];
    if (this.packet) cands.push(pjoin(this.packet, "diagrams", key + ".hotspots.json"));
    if (srcAbs) cands.push(pjoin(pdirname(srcAbs), key + ".hotspots.json"));
    const path = cands.find((p) => isfile(p)) ?? null;
    if (!path) return [null, []];
    let spec: Py;
    try {
      spec = readJson(path);
    } catch {
      return [path, []];
    }
    const vb = get(spec, "viewBox");
    const out: Map<string, Py>[] = [];
    for (const b of L(or(get(spec, "boxes"), []))) {
      let box: Py = null;
      if (truthy(vb) && L(vb).length === 4 && truthy(vb[2]) && truthy(vb[3]) && ["x", "y", "w", "h"].every((k) => b.has(k))) {
        const [v0, v1, v2, v3] = (vb as Py[]).map(num) as [number, number, number, number];
        box = [pyRound((num(b.get("x")) - v0) / v2 * 100, 1), pyRound((num(b.get("y")) - v1) / v3 * 100, 1),
          pyRound(num(b.get("w")) / v2 * 100, 1), pyRound(num(b.get("h")) / v3 * 100, 1)];
      }
      out.push(new Map<string, Py>([["label", get(b, "label", "")], ["kind", get(b, "kind", "")], ["tone", get(b, "tone", "")], ["text", get(b, "text", "")],
        ["box", box], ["href", or(get(b, "href"), "")], ["table", or(get(b, "table"), "")], ["note", or(get(b, "note"), "")]]));
    }
    return [path, out];
  }

  planImages(): void {
    this.filesPlanned = new Set();
    this.imageRows = [];
    const files = or(get(this.manifest, "files"), new Map());
    this.images.forEach((img, i0) => {
      const n = img.node, pub = img.published!;
      let srcAbs: string | null = isfile(pjoin(this.build, pub)) ? pjoin(this.build, pub) : get(files, pub);
      if (!srcAbs || !isfile(srcAbs)) {
        this.warn.push(`image ${pub}: not found in the build`);
        srcAbs = null;
      }
      const row = new Map<string, Py>([["id", `IMG.${pad2(i0 + 1)}`], ["path", this.imgFile.get(pub) ?? null], ["source", pub], ["kind", img.kind],
        ["in_today_block", img.in_today],
        ["title", img.kind === "diagram" ? plain(get(n, "title")) : plain(get(n, "caption"))],
        ["caption", img.kind === "diagram" ? plain(get(n, "reading")) : plain(get(n, "caption"))],
        ["alt", or(get(n, "alt"), "")], ["section", img.section], ["anchor", img.anchor],
        ["width", or(get(n, "width"), null)], ["height", or(get(n, "height"), null)],
        ["format", lstrip(splitext(pub)[1], ".").toLowerCase()],
        ["bytes", srcAbs ? statSync(srcAbs).size : null],
        ["sha256", srcAbs ? sha256(readFileSync(srcAbs)) : null]]);
      if (img.kind === "diagram") {
        row.set("legend", L(or(get(n, "legend"), [])).map((x: Py) => `${plain(get(x, "label"))} = ${S(get(x, "tone"))}`));
        row.set("hotspots", L(or(get(n, "hotspots"), [])).map((h: Py) => new Map<string, Py>([["label", get(h, "label", "")], ["href", get(h, "href", "")], ["table", get(h, "table", "")]])));
        const [hsPath, boxes] = this.hotspotBoxes(or(get(n, "key"), splitext(basename(pub))[0]), srcAbs);
        row.set("boxes", boxes.length);
        row.set("labels", uniq(boxes.filter((b) => truthy(b.get("label"))).map((b) => clip(b.get("label"), 60))));
        if (boxes.length) {
          const side = this.sidecarPath(n)!;
          row.set("sidecar", side);
          this.filesPlanned.add(side);
          img.boxes = boxes;
          img.hotspots_file = hsPath;
        }
      }
      img.row = row;
      img.src_abs = srcAbs;
      this.imageRows.push(row);
    });
  }

  writeImages(): void {
    const copied = new Set<string>();
    for (const img of this.images) {
      const row = img.row!;
      if (this.copyImages && img.src_abs && !copied.has(row.get("path"))) {
        this.files.set(row.get("path"), { copy: img.src_abs });
        copied.add(row.get("path"));
      }
      if (img.boxes && img.boxes.length) {
        const side: string = row.get("sidecar");
        const here = dirn(side);
        const sliceFile = this.anchorFile.get(row.get("anchor"));
        const lines = [this.mdHead(), "", `# Diagram: ${S(row.get("title"))}`, "",
          `- File: [${basename(or(row.get("path"), row.get("source")))}](${truthy(row.get("path")) ? rel(row.get("path"), here) : S(row.get("source"))}) ` +
          `(${S(row.get("width"))}\u00d7${S(row.get("height"))} ${S(row.get("format"))}, ${S(row.get("bytes"))} bytes)`,
          `- On the page: section ${S(row.get("section"))}, anchor #${S(row.get("anchor"))}` + (sliceFile ? ` in [${sliceFile}](${rel(sliceFile, here)}#${S(row.get("anchor"))})` : ""),
          `- Alt: ${S(row.get("alt"))}`];
        if (truthy(row.get("caption"))) lines.push(`- Reading: ${S(row.get("caption"))}`);
        if (truthy(row.get("legend"))) lines.push(`- Legend: ${(row.get("legend") as string[]).join(" \u00b7 ")}`);
        lines.push(`- Box source: ${basename(img.hotspots_file!)}; box = x, y, w, h in percent of the image`);
        lines.push("", `## Boxes (${img.boxes.length})`, "", "| label | kind | tone | text | box | link |", "| --- | --- | --- | --- | --- | --- |");
        for (const b of img.boxes) {
          const link = or(or(b.get("href"), truthy(b.get("table")) ? `table ${S(b.get("table"))}` : ""), b.get("note"));
          const cells = [b.get("label"), b.get("kind"), b.get("tone"), (b.get("text") as string).split("\n").filter((x) => pystrip(x)).join(" / "),
            L(or(b.get("box"), [])).map((v: Py) => S(v)).join(", "), link];
          lines.push("| " + cells.map((x) => S(x).replaceAll("|", "\\|")).join(" | ") + " |");
        }
        this.emit(side, lines.join("\n"));
      }
    }
    if (this.imageRows.length) {
      this.emit("images/index.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["copied", this.copyImages],
        ["note", "paths are relative to agents/; images are byte copies (never re-encoded) unless copied is false"],
        ["images", this.imageRows]])));
    }
  }

  // ------------------------------------------------------------------ page strip (D8)
  writePage(): void {
    if (!this.pageSha) return;
    const root = sp.parse(readText(this.page, true));
    const [content, title, removed] = sp.strip(root);
    const head = `${this.head} \u00b7 stripped by ${sp.VERSION}`;
    const body = sp.bodyHtml(content).replace(/\n{3,}/g, "\n\n");
    this.emit("page/plain.html", `<!doctype html>\n<!-- ${head} -->\n<html lang="en"><head><meta charset="utf-8"><title>${title}</title></head>\n<body>\n${body}\n</body></html>`);
    this.emit("page/outline.json", dumps({ _generated: head, title, removed, headings: sp.outline(content) }, 1));
  }

  // ------------------------------------------------------------------ map
  writeMap(): void {
    const ent: Map<string, Py>[] = [], edges: Map<string, Py>[] = [];
    const E = (kw: [string, Py][]) => ent.push(new Map(kw.filter(([, v]) => v !== null && v !== undefined)));
    const Ln = (a: string, r: string, b: string, kw: [string, Py][] = []) =>
      edges.push(new Map<string, Py>([["from", a], ["rel", r], ["to", b], ...kw.filter(([, v]) => v !== null && v !== undefined)]));
    const secTitle = new Map<Py, string>();
    for (const s of this.sections()) secTitle.set(s.get("id"), this.sectionTitle(s));
    for (const s of this.sections()) {
      E([["id", `SEC.${S(s.get("id"))}`], ["kind", "section"], ["path", this.anchorFile.get(s.get("id")) ?? null], ["anchor", s.get("id")], ["title", secTitle.get(s.get("id"))]]);
    }
    const secIds = new Set(this.sections().map((s) => s.get("id")));
    const A = (aid: Py) => (secIds.has(aid) ? `SEC.${S(aid)}` : `ANC.${S(aid)}`);
    for (const a of L(or(get(this.c, "anchors"), []))) {
      if (get(a, "kind") === "section") continue;
      const aid = a.get("id"), f = this.anchorFile.get(aid);
      E([["id", `ANC.${S(aid)}`], ["kind", "anchor"], ["subkind", get(a, "kind")], ["path", f ?? null], ["anchor", aid], ["title", clip(get(a, "title"), 120)]]);
      if (truthy(this.anchorSection.get(aid))) Ln(`SEC.${S(this.anchorSection.get(aid))}`, "contains", `ANC.${S(aid)}`);
      if (f) Ln(`ANC.${S(aid)}`, "in_file", `FILE.${f}`);
    }
    for (const d of this.decisionIndex) {
      E([["id", `DEC.${S(d.get("id"))}`], ["kind", "decision"], ["path", d.get("file")], ["anchor", d.get("anchor")], ["state", d.get("state")], ["title", clip(d.get("question"), 120)]]);
      Ln(`DEC.${S(d.get("id"))}`, "anchored_at", A(d.get("anchor")));
      if (truthy(d.get("section"))) Ln(`DEC.${S(d.get("id"))}`, "in_section", `SEC.${S(d.get("section"))}`);
    }
    for (const p of this.prIndex) {
      const lab = p.has("label") ? p.get("label") : p.get("n");
      E([["id", `PR.${S(lab)}`], ["kind", "pr"], ["path", p.get("file")], ["anchor", p.get("anchor")], ["milestone", p.get("milestone")], ["title", clip(p.get("title"), 120)]]);
      Ln(`PR.${S(lab)}`, "anchored_at", A(p.get("anchor")));
      Ln(`PR.${S(p.get("n"))}`, "documented_by", `FILE.${S(p.get("file"))}`);
      const sec = this.anchorSection.get(p.get("anchor"));
      if (truthy(sec)) Ln(`PR.${S(p.get("n"))}`, "in_section", `SEC.${S(sec)}`);
    }
    for (const t of this.tables) {
      const tid = `TBL.${S(get(t, "table"))}`;
      E([["id", tid], ["kind", "table"], ["path", `schema/${slugify(get(t, "table"))}.json`], ["anchor", get(t, "id")], ["state", get(t, "state")]]);
      if (truthy(get(t, "id"))) Ln(tid, "anchored_at", A(t.get("id")));
      Ln(tid, "documented_by", "FILE.schema/schema.md");
    }
    const tblById = new Map<Py, string>();
    for (const t of this.tables) tblById.set(get(t, "id"), `TBL.${S(get(t, "table"))}`);
    for (const img of this.images) {
      const row = img.row!;
      const iid = `IMG.${S(row.get("source"))}`;
      E([["id", iid], ["kind", "image"], ["path", row.get("path")], ["anchor", row.get("anchor")], ["image_kind", row.get("kind")], ["title", clip(row.get("title"), 120)]]);
      Ln(iid, "in_section", `SEC.${S(row.get("section"))}`);
      if (truthy(row.get("anchor"))) {
        Ln(iid, row.get("kind") === "diagram" ? "anchored_at" : "appears_under",
          row.get("anchor") !== row.get("section") ? `ANC.${S(row.get("anchor"))}` : `SEC.${S(row.get("section"))}`);
      }
      for (const h of L(or(row.get("hotspots"), []))) {
        if (truthy(get(h, "table"))) Ln(iid, "hotspot", tblById.has(h.get("table")) ? tblById.get(h.get("table"))! : A(h.get("table")), [["note", get(h, "label")]]);
        else if (((or(get(h, "href"), "")) as string).startsWith("#")) Ln(iid, "hotspot", A((h.get("href") as string).slice(1)), [["note", get(h, "label")]]);
      }
    }
    const repo = new Set<string>();
    for (const o of this.citations) {
      E([["id", o.get("id")], ["kind", "citation"], ["path", o.get("file")], ["anchor", o.get("anchor")], ["repo_path", o.get("path")],
        ["lines", truthy(o.get("from")) ? [o.get("from"), o.get("to")] : null]]);
      Ln(o.get("id"), "cites", `REPO.${S(o.get("path"))}`, [["sha", this.sha || null]]);
      if (truthy(o.get("anchor"))) Ln(o.get("anchor") !== o.get("section") ? `ANC.${S(o.get("anchor"))}` : `SEC.${S(o.get("section"))}`, "has_cite", o.get("id"));
      repo.add(o.get("path"));
    }
    for (const pth of sorted(repo)) E([["id", `REPO.${pth}`], ["kind", "repo_path"], ["path", pth]]);
    for (const b of this.codes) {
      const kind = b.file!.startsWith("sql/") ? "sql" : "code";
      const cid = `${kind.toUpperCase()}.${basename(b.file!)}`;
      E([["id", cid], ["kind", kind], ["path", b.file!], ["anchor", b.anchor], ["lang", b.lang || null]]);
      Ln(cid, "extracted_from", b.anchor !== b.section ? `ANC.${S(b.anchor)}` : `SEC.${S(b.section)}`);
      Ln(cid, "in_section", `SEC.${S(b.section)}`);
    }
    for (const t of this.tables) {
      const f = this.ddlFile.get(get(t, "table"));
      if (f) {
        const cid = `SQL.${basename(f)}`;
        E([["id", cid], ["kind", "sql"], ["path", f], ["anchor", get(t, "id")], ["lang", "sql"]]);
        Ln(cid, "extracted_from", `TBL.${S(get(t, "table"))}`);
      }
    }
    for (const tkn of sorted(this.tokenIndex.keys())) {
      const v = this.tokenIndex.get(tkn)!;
      E([["id", `TOK.${tkn}`], ["kind", "token"], ["anchor", v.get("anchor")], ["path", v.get("file")], ["uses", v.get("uses")]]);
      Ln(`TOK.${tkn}`, "defined_by", get(get(this.defs, v.get("anchor"), new Map()), "kind") !== "section" ? `ANC.${S(v.get("anchor"))}` : `SEC.${S(v.get("anchor"))}`);
    }
    const seen = new Map<string, [Py, Py, number]>();
    for (const l of this.refLinks) {
      const src = or(l.get("from"), l.get("section"));
      if (!truthy(src)) continue;
      const key = JSON.stringify([S(src), S(l.get("to")), typeof src, typeof l.get("to")]);
      const e = seen.get(key);
      if (e) e[2] += 1;
      else seen.set(key, [src, l.get("to"), 1]);
    }
    for (const [a, b, cnt] of seen.values()) {
      const fa = get(get(this.defs, a, new Map()), "kind") === "section" ? `SEC.${S(a)}` : `ANC.${S(a)}`;
      const fb = get(get(this.defs, b, new Map()), "kind") === "section" ? `SEC.${S(b)}` : `ANC.${S(b)}`;
      Ln(fa, "references", fb, [["count", cnt]]);
    }
    for (const f of sorted(this.files.keys())) {
      const v = this.files.get(f)!;
      if (typeof v === "string") E([["id", `FILE.${f}`], ["kind", "file"], ["path", f], ["tokens", tok(v)]]);
    }
    const ids = new Set(ent.map((e) => e.get("id")));
    const dangling = sorted(new Set(edges.flatMap((e) => [e.get("from"), e.get("to")]).filter((x) => !ids.has(x))));
    if (dangling.length) this.warn.push(`map.json: ${dangling.length} edge end(s) without an entity, e.g. ${repr(dangling.slice(0, 3))}`);
    this.mapCounts = { entities: ent.length, edges: edges.length, dangling: dangling.length };
    this.emit("map.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["schema_version", 1],
      ["packet", new Map<string, Py>([["slug", splitext(or(get(this.c, "source"), ""))[0]], ["rev", get(this.meta, "rev")], ["base_sha", this.sha || null]])],
      ["kinds", ["section", "anchor", "decision", "pr", "table", "image", "citation", "repo_path", "sql", "code", "token", "file"]],
      ["rels", sorted(new Set(edges.map((e) => e.get("rel"))))],
      ["entities", ent], ["edges", edges]])));
  }

  // ------------------------------------------------------------------ README, llms.txt
  summaryText(): string {
    for (const p of L(or(get(this.c, "lede"), []))) {
      const t = plain(get(p, "html"));
      if (t) return clip(t, 420);
    }
    for (const s of this.sections()) {
      for (const b of L(or(get(s, "blocks"), []))) if (get(b, "type") === "p") return clip(plain(get(b, "html")), 420);
    }
    return "";
  }

  template(name: string, dflt: string): string {
    const path = pjoin(TEMPLATES, name);
    return isfile(path) ? readText(path) : dflt;
  }

  fill(tpl: string, values: Map<string, Py>): string {
    const out = re.sub(String.raw`\{\{(\w+)\}\}`, (m) => S(values.has(m.group(1)!) ? values.get(m.group(1)!) : ""), tpl);
    return pystrip(out.replace(/\n{3,}/g, "\n\n")) + "\n";
  }

  writeIndex(): void {
    const c = this.c, m = this.meta;
    const secLines: string[] = [], llmSecs: string[] = [];
    for (const sl of this.slices) {
      const s = sl.section;
      const name = this.sectionTitle(s) + (sl.part > 1 ? ` (part ${sl.part})` : "");
      secLines.push(`- [${name}](${sl.path}) ~${sl.tokens} tok \u00b7 ${sl.anchors.length} anchors`);
      llmSecs.push(`- [${name}](${sl.path}): ~${sl.tokens} tokens; anchors ${sl.anchors.slice(0, 6).join(", ")}${sl.anchors.length > 6 ? " \u2026" : ""}`);
    }
    const decLines = this.decisionIndex.map((d) => `- [${S(d.get("id"))}](${S(d.get("file"))}) ${S(d.get("state"))} \u00b7 ${clip(d.get("question"), 70)}`);
    const llmDec = this.decisionIndex.map((d) => `- [${S(d.get("id"))} \u00b7 ${clip(d.get("question"), 90)}](${S(d.get("file"))}): ${S(d.get("state"))}; ${clip(d.get("status"), 60)}`);
    const llmPrs = this.prIndex.map((p) => `- [PR ${S(p.get("n"))} \u00b7 ${clip(p.get("title"), 70)}](${S(p.get("file"))}): ${S(p.get("milestone"))}; ${clip(p.get("change"), 90)}`);
    const nSql = [...this.files.keys()].filter((f) => f.startsWith("sql/")).length;
    const nCode = [...this.files.keys()].filter((f) => f.startsWith("code/")).length;
    const refs = or(get(c, "refs"), new Map());
    const counts = `${L(or(get(c, "sections"), [])).length} sections in ${this.slices.length} slices \u00b7 ${this.decisionIndex.length} decisions \u00b7 ` +
      `${this.prIndex.length} PRs \u00b7 ${this.citations.length} citations \u00b7 ${L(or(get(c, "anchors"), [])).length} anchors \u00b7 ` +
      `${this.refLinks.length} reference links (${L(or(get(refs, "unlinked"), [])).length} unlinked tokens) \u00b7 ` +
      `${nSql} sql \u00b7 ${nCode} code \u00b7 ${this.tables.length} schema tables \u00b7 ${this.imageRows.length} images`;
    const files: [string, string][] = [["tdd.md", "whole document, markdown; ids as {#id}"], ["tdd.txt", "whole document, one paragraph per line"],
      ["decisions/decisions.json", "decision index: id, state, status, date, slice"],
      ["citations.md", "citations grouped by repo file"], ["citations.json", "every path:line with section, anchor, slice, href"],
      ["refs.json", "anchor -> slice and definition; token -> anchor; every reference link"],
      ["prs/prs.md", "PR order: milestone, title, change, rollback"], ["schema/schema.md", "tables: columns, change flags, measured/expected"],
      ["images/index.json", "what each picture holds: title, caption, alt, size, boxes"], ["page/plain.html", "the built page, chrome stripped"],
      ["map.json", "entity/edge graph for programs (jq), not for reading whole"], ["tokens.json", "bytes, sha256, ~tokens per file"]];
    const fileRows = files.filter(([f]) => this.files.has(f))
      .map(([f, what]) => { const v = this.files.get(f); return `| [${f}](${f}) | ${what} | ~${typeof v === "string" ? tok(v) : "-"} |`; }).join("\n");
    const allw = [...L(or(get(this.manifest, "warnings"), []))];
    const notes = [...allw.slice(0, 6), ...(allw.length > 6 ? [`and ${allw.length - 6} more in content.files.json`] : [])];
    const unl = L(or(get(refs, "unlinked"), []));
    const aliases = [...((or(get(m, "pathAliases"), new Map())) as PyDict)].map(([k, v]) => `\`${k}\` = \`${S(v)}\``).join(", ");
    const values = new Map<string, Py>([
      ["header", this.mdHead()], ["head_raw", this.head], ["title", or(get(c, "titleText"), plain(get(c, "title")))], ["summary", this.summaryText()],
      ["status", or(get(m, "status"), "not stated")], ["rev", or(get(m, "rev"), "-")], ["rev_note", or(get(m, "revNote"), "")], ["updated", or(get(m, "updated"), "not stated")],
      ["repo", or(or(get(m, "repoUrl"), get(m, "repoPath")), "no repo named")], ["sha", this.sha || "none"], ["sha_date", or(get(m, "shaDate"), "date not stated")],
      ["source", or(get(c, "source"), "")], ["generated", or(get(c, "generated"), "")], ["budget", this.budget],
      ["sections", secLines.join("\n") || "- none"], ["decisions", decLines.join("\n") || "- none recorded"],
      ["files", fileRows], ["counts", counts], ["aliases", aliases || "none"],
      ["notes", notes.map((w) => `- build warning: ${clip(w, 160)}`).join("\n") || "- none"],
      ["unlinked", unl.map(S).join(", ") || "none"],
      ["llm_sections", llmSecs.join("\n") || "- none"], ["llm_decisions", llmDec.join("\n") || "- none recorded"],
      ["llm_prs", llmPrs.join("\n") || "- none"], ["n_sql", nSql], ["n_code", nCode],
      ["tdd_md_tokens", tok((this.files.get("tdd.md") as string | undefined) ?? "")], ["tdd_txt_tokens", tok((this.files.get("tdd.txt") as string | undefined) ?? "")],
    ]);
    let readme = this.fill(this.template("README.template.md", DEFAULT_README), values);
    if (utf8Len(readme) > README_MAX) {
      values.set("sections", this.slices.map((sl) => `- [${sl.path.split("/").pop()}](${sl.path})`).join("\n"));
      values.set("decisions", "- see [decisions/decisions.json](decisions/decisions.json)");
      readme = this.fill(this.template("README.template.md", DEFAULT_README), values);
      this.warn.push("README.md over 8 KB with the full index; wrote the compact index");
    }
    this.emit("README.md", readme);
    this.emit("llms.txt", this.fill(this.template("llms.template.txt", DEFAULT_LLMS), values));
  }

  // ------------------------------------------------------------------ run
  run(): this {
    this.annotate();
    this.planImages();
    this.sliceSections();
    this.writeSlices();
    this.writeSchema();
    this.writeWhole();
    this.recordPrBodies();
    this.writeDecisions();
    this.writeCitations();
    this.writeRefs();
    this.writeCode();
    this.writePrs();
    this.writeImages();
    this.writePage();
    this.writeMap();
    this.writeIndex();
    writeTokens(this);
    return this;
  }
}

function rstripPy(s: string): string {
  return s.replace(new RegExp(`[${PY_WS}]+$`), "");
}

/** f"{int(n):02d}" (int() of an int or a decimal string). */
function pyIntFmt(n: Py): string {
  const v = typeof n === "number" ? Math.trunc(n) : typeof n === "string" ? sp.pyInt(n) : n instanceof PyFloat ? Math.trunc(n.value) : null;
  if (v === null || Number.isNaN(v)) throw new Error(`invalid literal for int() with base 10: ${repr(n)}`);
  return S(v).padStart(2, "0");
}

// ------------------------------------------------------------------ tokens.json (shared with PageLane)
export interface TokenLane {
  files: Map<string, FileVal>;
  head: string;
  budget: number;
  overBudget: string[];
  largest: [string | null, number];
}
export function writeTokens(lane: TokenLane): void {
  const rows = new Map<string, Map<string, Py>>();
  const over: string[] = [];
  for (const f of sorted(lane.files.keys())) {
    const v = lane.files.get(f)!;
    if (typeof v !== "string") {
      const b = readFileSync(v.copy);
      rows.set(f, new Map<string, Py>([["bytes", b.length], ["sha256", sha256(b)], ["tokens", null], ["kind", "binary copy"]]));
      continue;
    }
    const t = tok(v);
    rows.set(f, new Map<string, Py>([["bytes", utf8Len(v)], ["sha256", sha256(v)], ["tokens", t]]));
    if (!EXEMPT.has(f) && t > lane.budget) over.push(f);
  }
  const texts = [...rows].filter(([, r]) => r.get("tokens") !== null);
  const slices = texts.filter(([f]) => !EXEMPT.has(f));
  let largest: string | null = null, best = -Infinity;
  for (const [f, r] of slices) if (r.get("tokens") > best) { best = r.get("tokens"); largest = f; }
  lane.overBudget = over;
  lane.largest = [largest, largest ? best : 0];
  const doc = new Map<string, Py>([["_generated", lane.head], ["method", "tokens = ceil(chars / 4): an estimate, not a tokenizer; binary files have no token count"],
    ["budget", lane.budget], ["budget_exempt", sorted(EXEMPT)], ["over_budget", over],
    ["largest_slice", largest ? new Map<string, Py>([["path", largest], ["tokens", lane.largest[1]]]) : null],
    ["total_text_tokens", texts.reduce((a, [, r]) => a + r.get("tokens"), 0)], ["files", rows]]);
  lane.files.set("tokens.json", dumps(doc, 1) + "\n");
}

