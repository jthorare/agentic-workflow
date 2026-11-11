// build_tdd.Compiler in TypeScript (build_tdd.py:286-903): plain synchronous code on the Python-semantics kernel.
// Every method keeps the Python's order of side effects (ids claimed, definitions and citations recorded, warnings,
// asset references), because content.json, content.files.json and the summary line depend on that order.
// Node dicts are plain objects (their keys are fixed words, so insertion order is kept); every dict whose keys come
// from the source (defs, glossary, refs families, meta passthrough) is a Map, as Python's dict.
import * as py from "../py";
import { PyError } from "../py/errors";
import { float as floatOf } from "../py/num";
import type { Match } from "../py/re";
import { pyStr } from "../py/repr";
import type { PyDict, PyValue } from "../py/value";
import { get, getKey, truthy, uniq } from "../py/value";
import { mul, sub, truediv } from "../py/ops";
import * as sd from "../sd";
import { CITE, LEGEND_TONES, LINK_KEYS, SQL_KW, TOKEN, token_id } from "../vocab";
import type { Args } from "./args";
import { parse_attrs, parse_ext, split_title, type Attrs, type Block, type BoxBlock } from "./attrs";
import { makedirs } from "./errors";
import { load_prs } from "./prs";
import { link_refs, type Stats } from "./refs";
import { sanitize_html } from "./sanitize";

const re = py.re;
const { esc, slug, stripTags: strip_tags } = sd;
const strip = (s: string, chars?: string) => py.str.strip(s, chars);

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type Node = Record<string, any>;

export interface Ctx {
  section?: string;
  p_class?: string;
  top?: boolean;
  shots?: string;
  grid?: string;
  has_group?: boolean;
}

interface Section {
  title: string;
  attrs: Attrs;
  blocks: Block[];
  subs: { id: string; title: string }[];
  has_group: boolean;
  num: number;
  id: string;
}

interface Shot {
  alt: string;
  src: string;
  title: string;
}

/** attrs.get(k, dflt) */
const aget = <D>(attrs: Attrs, k: string, dflt: D): string | true | D => (attrs.has(k) ? attrs.get(k)! : dflt);

/** A value a str method runs on: AttributeError as CPython words it otherwise. */
function strOf(v: unknown, method: string): string {
  if (typeof v === "string") return v;
  const t = v === null || v === undefined ? "NoneType" : v === true || v === false ? "bool" : typeof v === "number" ? "int" : Array.isArray(v) ? "list" : v instanceof Map ? "dict" : typeof v;
  throw new PyError("AttributeError", `'${t}' object has no attribute '${method}'`);
}

function dictOf(v: unknown): PyDict {
  if (v instanceof Map) return v;
  const t = v === null ? "NoneType" : Array.isArray(v) ? "list" : typeof v === "string" ? "str" : typeof v === "number" ? "int" : typeof v;
  throw new PyError("AttributeError", `'${t}' object has no attribute 'get'`);
}

const STATUS_DONE = ["as built", "reviewed", "approved", "accepted"];
const STATUS_HELD = ["superseded", "withdrawn"];
const PROPOSED_STROKE = re.compile(String.raw`<path\b(?![^>]*stroke-dasharray)([^>]*\bstroke="#e16919"[^>]*)/>`);
const IMG_ONLY = re.compile(String.raw`^(?:\s*!\[[^\]]*\]\([^)]*\)\s*)+$`);
const IMG = re.compile(String.raw`!\[([^\]]*)\]\(\s*(\S+?)(?:\s+"([^"]*)")?\s*\)`);
const CHIP_STRIP = re.compile(String.raw`\[chip:\w[\w-]*\s+([^\]]+)\]`);
const CHIP = re.compile(String.raw`\[chip:(\w[\w-]*)\s+([^\]]+)\]`);
const CODE = re.compile("<code>([^<]+)</code>");
const DATE = re.compile(String.raw`\d{4}-\d{2}-\d{2}`);
const SKIP_TREE = new Set(["subs", "hotspots", "nav"]);

export interface Summary {
  cpath: string;
  line: string;
  warnings: string[];
}

export class Compiler {
  readonly a: Args;
  readonly warn: string[] = [];
  readonly md_dir: string;
  readonly out_dir: string;
  readonly inline0 = sd.Inline();
  readonly assets: sd.Assets;
  readonly meta: PyDict;
  readonly diagram_dir: string | null;
  readonly prs_dir: string | null;
  readonly schema_dir: string | null;
  readonly ids = new Set<string>();
  readonly defs = new Map<string, Node>();
  readonly anchors: Node[] = [];
  readonly cites: Node[] = [];
  readonly decisions: Node[] = [];
  readonly glossary = new Map<string, string>();
  fig_no = 0;
  pr_count = 0;
  readonly pending_hrefs: [string, PyValue, string][] = [];
  cur_anchor: string | null = null;
  _measuring = false;
  readonly aliases: PyValue;
  cur_section: Section | null = null;
  readonly schema: PyDict[];

  constructor(a: Args) {
    this.a = a;
    this.md_dir = py.path.dirname(py.path.abspath(a.src));
    this.out_dir = py.path.abspath(a.out);
    this.assets = new sd.Assets(this.out_dir, false, !a.no_copy);
    const metaJson = py.path.join(this.md_dir, "meta.json");
    const meta_path = truthy(a.meta) ? a.meta : py.io.isfile(metaJson) ? metaJson : null;
    this.meta = meta_path ? dictOf(sd.loadJson(meta_path, "--meta")) : new Map();
    const proj = this.meta.get("project");
    if (proj instanceof Map && typeof proj.get("label") === "string") {
      const m = re.match(String.raw`\s*\[([^\]]+)\]\(([^)]+)\)\s*$`, proj.get("label") as string);
      if (m) {
        const label = m.group(1)!;
        const href = truthy(get(proj, "href")) ? proj.get("href")! : m.group(2)!;
        proj.set("label", label);
        proj.set("href", href);
      }
    }
    const dd = py.path.join(this.md_dir, "diagrams");
    this.diagram_dir = truthy(a.diagrams) ? a.diagrams : py.io.isdir(dd) ? dd : null;
    const pd = py.path.join(this.md_dir, "prs");
    this.prs_dir = truthy(a.prs) ? a.prs : py.io.isdir(pd) ? pd : null;
    const sdir = py.path.join(this.md_dir, "schema");
    this.schema_dir = truthy(a.schema) ? a.schema : py.io.isdir(sdir) ? sdir : null;
    this.aliases = get(this.meta, "path_aliases", new Map());
    this.schema = this.load_schema();
  }

  // -- ids and definitions
  uid(base: string | null | undefined): string {
    const b = base || "x";
    let i = 2;
    let cand = b;
    while (this.ids.has(cand)) {
      cand = `${pyStr(b)}-${i}`; // f"{base}-{i}": a bare {id} flag is True, which Python prints as "True-2"
      i += 1;
    }
    this.ids.add(cand);
    return cand;
  }

  define(did: string, kind: string, title: string | null, text: string | null = ""): void {
    const t = strip(strip_tags(this.inline(CHIP_STRIP.sub(String.raw`\1`, title || ""), false)));
    const x = py.cpSlice(strip(strip_tags(!(text || "").includes("<") ? this.inline(text as string, false) : (text as string))), 0, 600);
    const sec = this.cur_section ? this.cur_section.id : null;
    this.defs.set(did, { kind, title: t, text: x, section: sec });
    this.anchors.push({ id: did, kind, title: t, section: sec });
    this.cur_anchor = did;
  }

  text_of(s: string): string {
    this._measuring = true;
    try {
      return strip_tags(this.inline(s, false));
    } finally {
      this._measuring = false;
    }
  }

  // -- inline
  /** The source author is trusted, but a stray <script>, on* handler or javascript: URL must never reach a viewer. */
  sanitize(h: string): string {
    return sanitize_html(h);
  }

  inline(s: string, links = true): string {
    let h = this.sanitize(this.inline0(s, links));
    h = CHIP.sub((m) => {
      const k = py.lower(m.group(1)!);
      return `<span class="chip${k !== "plain" ? " " + esc(k) : ""}">${m.group(2)}</span>`;
    }, h);
    return CODE.sub((m) => this.cite_code(m), h);
  }

  cite_code(m: Match): string {
    const text = py.html.unescape(m.group(1)!);
    const c = CITE.match(text);
    if (!c) return m.group(0)!;
    const path = c.group(1)!;
    const a = c.group(2);
    const b = c.group(3);
    let full = path;
    if (this.aliases instanceof Map) {
      for (const [alias, real] of this.aliases) {
        if (full.startsWith(alias + "/")) {
          full = py.str.rstrip(strOf(real, "rstrip"), "/") + py.cpSlice(full, py.cpLen(alias));
          break;
        }
      }
    } else strOf(this.aliases, "items");
    const cite: Node = {
      text, path: full, from: a ? py.int(a) : null, to: b ? py.int(b) : null,
      section: this.cur_section ? this.cur_section.id : null, anchor: this.cur_anchor,
    };
    if (!this._measuring) this.cites.push(cite);
    const code = `<code class="cite">${m.group(1)}</code>`;
    const url = get(this.meta, "repo_url");
    const sha = get(this.meta, "sha");
    if (truthy(url) && truthy(sha) && a) {
      const frag = `#L${a}` + (b ? `-L${b}` : "");
      const href = `${py.str.rstrip(strOf(url, "rstrip"), "/")}/blob/${pyStr(sha)}/${full}${frag}`;
      cite.href = href;
      return `<a class="cite-link" href="${esc(href)}" target="_blank" rel="noopener">${code}</a>`;
    }
    return code;
  }

  // -- schema lane (D7)
  load_schema(): PyDict[] {
    const tables: PyDict[] = [];
    if (!this.schema_dir) return tables;
    for (const f of py.io.listdirSorted(this.schema_dir)) {
      if (!f.endsWith(".json")) continue;
      const t = dictOf(sd.loadJson(py.path.join(this.schema_dir, f), `schema/${f}`));
      if (!t.has("table")) t.set("table", py.path.splitext(f)[0]);
      if (!t.has("state")) t.set("state", "existing");
      const tid = this.uid("tbl-" + slug(strOf(t.get("table"), "lower")));
      t.set("id", tid);
      this.define(tid, "table", `${pyStr(t.get("table"))} (${pyStr(t.get("state"))})`, get(t, "summary", "") as string);
      for (const c of iter(get(t, "columns", []))) {
        const cd = dictOf(c);
        if (!cd.has("change")) cd.set("change", t.get("state") === "new" ? "new" : "unchanged");
      }
      tables.push(t);
    }
    return tables;
  }

  // -- E4: draw_diagrams.mjs paints every box solid, while the legend swatch for a proposed box is dashed. Dash the
  // tldraw-orange strokes (the proposed tone) in every copied diagram SVG.
  dash_proposed(files: Map<string, string>): void {
    for (const published of files.keys()) {
      if (!published.endsWith(".svg") || !published.startsWith("diagrams/")) continue;
      const dest = py.path.join(this.out_dir, published);
      if (!py.io.isfile(dest)) continue;
      const svg = py.io.readText(dest);
      const [out, n] = PROPOSED_STROKE.subn(String.raw`<path stroke-dasharray="9 6"\1/>`, svg);
      if (n) py.io.writeText(dest, out);
    }
  }

  // -- figures
  diagram_src(key: string): [string, string[]] {
    const cands: string[] = [];
    for (const d of [this.diagram_dir, py.path.join(this.md_dir, "diagrams")]) {
      if (!d) continue;
      for (const ext of [".svg", ".png"]) cands.push(py.path.join(d, key + ext));
    }
    const src = cands.find((c) => py.io.isfile(c)) ?? null;
    const ext = src ? py.path.splitext(src)[1] : ".svg";
    return [`diagrams/${key}${ext}`, src ? [src] : []];
  }

  node_diagram(key: string, title: string, attrs: Attrs, legend: string | null = null, reading: string | null = null): Node {
    const [published, cands] = this.diagram_src(key);
    const [url, src] = this.assets.ref(published, cands);
    const idAttr = aget(attrs, "id", null);
    const fid = this.uid(truthy(idAttr) ? (idAttr as string) : `fig-${slug(key)}`);
    this.fig_no += 1;
    if (!src) {
      this.warn.push(`diagram ${key}: no ${published} in ${this.diagram_dir || "diagrams/"}`);
      return { type: "placeholder", id: fid, html: `Diagram ${esc(key)} is missing: ${this.inline(title)}` };
    }
    const size = sd.imgSize(src) ?? [0, 0];
    const alt = aget(attrs, "alt", null);
    const fit = aget(attrs, "fit", null);
    const node: Node = {
      type: "diagram", id: fid, key, src: url, width: size[0], height: size[1], title: this.inline(title),
      alt: truthy(alt) ? alt : this.text_of(title), fit: truthy(fit) && fit !== "no",
      legend: legend ? this.legend_items(legend) : [], reading: reading ? this.inline(reading) : "", hotspots: this.hotspots(key, src, size),
    };
    this.define(fid, "diagram", title, reading || "");
    if (truthy(idAttr)) this.nav_sub(fid, this.text_of(title), attrs);
    return node;
  }

  legend_items(arg: string): Node[] {
    const items: Node[] = [];
    for (const part of re.split(String.raw`\s*\|\s*`, strip(arg)) as string[]) {
      if (!part) continue;
      const [label, tone] = part.includes("=") ? (py.str.rsplit(part, "=", 1) as [string, string]) : [part, part];
      const words = py.str.split(strip(tone)).map((t) => {
        const w = py.lower(strip(t));
        return LEGEND_TONES.get(w) ?? w;
      });
      const cls = uniq(py.str.split(words.join(" "))).join(" ");
      items.push({ label: this.inline(strip(py.str.lstrip(strip(label), "*"))), tone: cls });
    }
    return items;
  }

  hotspots(key: string, src: string, size: [number, number]): Node[] {
    const path = py.path.join(py.path.dirname(src), key + ".hotspots.json");
    if (!py.io.isfile(path)) return [];
    const spec = dictOf(sd.loadJson(path, "hotspots"));
    let vb: PyValue = get(spec, "viewBox");
    if (!truthy(vb)) {
      const head = py.io.decodeUtf8(py.io.readBytes(src).subarray(0, 4096), "ignore");
      const m = re.search(String.raw`viewBox="([\d.\-]+)[ ,]+([\d.\-]+)[ ,]+([\d.]+)[ ,]+([\d.]+)"`, head);
      vb = m ? m.groups().map((x) => new py.PyFloat(floatOf(x!))) : size[0] ? [0, 0, size[0], size[1]] : null;
    }
    if (!truthy(vb)) {
      this.warn.push(`hotspots ${key}: no viewBox`);
      return [];
    }
    const four = iter(vb);
    if (four.length !== 4) throw new PyError("ValueError", `${four.length > 4 ? "too many values to unpack (expected 4)" : `not enough values to unpack (expected 4, got ${four.length})`}`);
    const [x0, y0, w, h] = four as [PyValue, PyValue, PyValue, PyValue];
    const out: Node[] = [];
    const tables = new Map<PyValue, PyValue>(this.schema.map((t) => [t.get("table")!, t.get("id")!]));
    // round(v / by * 100, 2) with Python's operators, so a bad viewBox or box fails with CPython's exception text
    const pct = (v: PyValue, by: PyValue) => new py.PyFloat(py.round((mul(truediv(v, by), 100) as py.PyFloat).value, 2));
    for (const bv of iter(get(spec, "boxes", []))) {
      const b = dictOf(bv);
      if (!(truthy(get(b, "href")) || truthy(get(b, "image")) || truthy(get(b, "note")) || truthy(get(b, "table")))) continue;
      const hs: Node = {
        label: get(b, "label", ""), x: pct(sub(getKey(b, "x"), x0), w), y: pct(sub(getKey(b, "y"), y0), h),
        w: pct(getKey(b, "w"), w), h: pct(getKey(b, "h"), h), href: get(b, "href", ""), note: get(b, "note", ""), tone: get(b, "tone", ""),
      };
      if (truthy(get(b, "table"))) {
        const tb = b.get("table")!;
        if (tables.has(tb)) hs.table = tables.get(tb);
        else this.warn.push(`hotspot ${key}/${py.repr(get(b, "label", ""))}: table ${pyStr(tb)} has no schema/${pyStr(tb)}.json`);
      }
      if (truthy(get(b, "image"))) {
        const image = strOf(b.get("image"), "startswith");
        const published = sd.publishedFor(image, "images");
        const [url, s] = this.assets.ref(published, [py.path.join(this.md_dir, image), py.path.join(this.out_dir, published)]);
        if (!s) this.warn.push(`hotspot image ${image} (${key}): not found`);
        hs.image = url;
      }
      const href = strOf(hs.href, "startswith");
      if (href.startsWith("#") && href.slice(1)) this.pending_hrefs.push([key, get(b, "label", ""), href.slice(1)]);
      out.push(hs);
    }
    return out;
  }

  node_shot(b: Shot, size_mode: string): Node {
    const published = sd.publishedFor(b.src, "images");
    const cands = [py.path.join(this.md_dir, b.src), py.path.join(this.out_dir, published), ...(py.path.isabs(b.src) ? [b.src] : [])];
    const [url, s] = this.assets.ref(published, cands);
    if (!s) this.warn.push(`image ${b.src}: not found`);
    const size = sd.imgSize(s) ?? Compiler.jpeg_size(s) ?? [0, 0];
    let cls = size_mode;
    if (cls === "auto") {
      const [w, h] = size;
      cls = w ? (w <= 420 && h > w ? "phone" : w && h / w >= 1.2 ? "col" : w >= 1000 ? "wide" : "mid") : "mid";
    }
    return { type: "shot", src: url, alt: strip_tags(b.alt || ""), caption: b.title ? this.inline(b.title) : "", size: cls, width: size[0], height: size[1] };
  }

  static jpeg_size(path: string | null): [number, number] | null {
    if (!path || !(py.lower(path).endsWith(".jpg") || py.lower(path).endsWith(".jpeg"))) return null;
    let d: Uint8Array;
    try {
      d = py.io.readBytes(path);
    } catch {
      return null;
    }
    const at = (k: number) => {
      if (k >= d.length) throw new PyError("IndexError", "index out of range");
      return d[k]!;
    };
    const be = (a: number, b: number) => {
      let v = 0;
      for (let k = a; k < Math.min(b, d.length); k++) v = v * 256 + d[k]!;
      return v;
    };
    let i = 2;
    while (i < d.length && d[i] === 0xff) {
      const marker = at(i + 1);
      const L = be(i + 2, i + 4);
      if (marker === 0xc0 || marker === 0xc1 || marker === 0xc2) return [be(i + 7, i + 9), be(i + 5, i + 7)];
      i += 2 + L;
    }
    return null;
  }

  // -- blocks
  nodes(blocks: Block[], ctx: Ctx): Node[] {
    const out: Node[] = [];
    let i = 0;
    while (i < blocks.length) {
      const b = blocks[i]!;
      if (b.t === "marker" && b.kind === "diagram") {
        const [key, title0] = sd.splitMarker(b.arg);
        const [title, attrs] = parse_attrs(title0);
        let legend: string | null = null;
        let reading: string | null = null;
        let j = i + 1;
        for (;;) {
          const x = blocks[j];
          if (!(x && x.t === "marker" && (x.kind === "legend" || x.kind === "reading"))) break;
          if (x.kind === "legend") legend = x.arg;
          else reading = x.arg;
          j += 1;
        }
        out.push(this.node_diagram(key, title, attrs, legend, reading));
        i = j;
        continue;
      }
      const n = this.node(b, ctx);
      if (truthy(n)) out.push(n!);
      i += 1;
    }
    return out;
  }

  para(text: string, ctx: Ctx, default_cls = ""): Node {
    if (IMG_ONLY.match(text) && IMG.findall(text).length > 1) {
      const shots = (IMG.findall(text) as string[][]).map(([a, src, t]) => this.node_shot({ alt: a!, src: src!, title: t! }, ctx.shots ?? "auto"));
      if (truthy(ctx.grid)) for (const sh of shots) sh.size = "col";
      return { type: "shots", grid: ctx.grid ?? "", media: shots };
    }
    const [t, attrs] = parse_attrs(text);
    text = t;
    let cls = py.str.split(default_cls || "").filter((c) => c);
    cls = cls.concat(["small", "muted", "lead"].filter((k) => truthy(attrs.get(k))));
    if (truthy(attrs.get("class"))) cls = cls.concat(py.str.split(strOf(attrs.get("class"), "split")));
    const node: Node = { type: "p", html: this.inline(text), cls: uniq(cls) };
    if (truthy(attrs.get("id"))) {
      node.id = this.uid(attrs.get("id") as string);
      this.define(node.id, "paragraph", py.cpSlice(text, 0, 80), text);
    }
    if (re.fullmatch(String.raw`\*\*[^*]+\*\*`, strip(text))) node.cls.push("lead-label");
    return node;
  }

  node(b: Block, ctx: Ctx): Node | null {
    switch (b.t) {
      case "p":
        return this.para(b.text, ctx, ctx.p_class ?? "");
      case "box":
        return this.node_box(b, ctx);
      case "h": {
        const [text, attrs] = parse_attrs(b.text);
        const lvl = Math.min(Math.max(b.level, 3), 6);
        const idAttr = attrs.get("id");
        const hid = truthy(idAttr) ? (idAttr as string) : lvl === 3 ? slug(this.text_of(text)) : null;
        const node: Node = { type: "heading", level: lvl, html: this.inline(text) };
        if (hid) {
          node.id = this.uid(hid);
          this.define(node.id, "heading", text);
          if (lvl === 3 && truthy(ctx.top)) this.nav_sub(node.id, this.text_of(text), attrs);
        }
        return node;
      }
      case "list":
        return this.node_list(b, ctx);
      case "table":
        return this.node_table(b);
      case "code":
        return this.node_code(b);
      case "quote":
        return { type: "callout", tone: b.kind || "quote", blocks: this.nodes(b.blocks, { ...ctx, p_class: "", top: false }) };
      case "hr":
        return { type: "hr" };
      case "comment":
        return b.text.includes("PR-CARDS") ? this.node_prs() : null;
      case "img":
        return this.node_shot(b, ctx.shots ?? "auto");
      case "marker": {
        const { kind, arg } = b;
        if (kind === "legend") return { type: "legend", items: this.legend_items(arg) };
        if (kind === "reading") return this.para(arg, ctx, "small muted");
        if (kind === "image" || kind === "embedded content") return { type: "placeholder", html: `Image: ${this.inline(arg)}` };
        this.warn.push(`unsupported marker [${kind}: ...] in a TDD page`);
        return null;
      }
    }
    return null;
  }

  node_list(b: sd.ListBlock, ctx: Ctx, steps = false): Node {
    const checks = b.items.every((it) => it.check !== null);
    const items: Node[] = [];
    for (const it of b.items) {
      let text = it.text;
      const cls: string[] = [];
      const m = re.match(String.raw`^\\?\[(new|changed|done|open|held)\\?\]\s+`, text);
      if (m) {
        cls.push(m.group(1)!);
        text = text.slice(m.end());
      }
      items.push({ html: this.inline(text), cls, check: it.check, children: it.children.map((c) => this.node_list(c, ctx)) });
    }
    return { type: "list", ordered: Boolean(b.ordered), start: b.start, steps, checks, items };
  }

  node_table(b: Extract<sd.Block, { t: "table" }>): Node {
    const ncol = Math.max(b.head.length, ...b.rows.map((r) => r.length));
    const pad = (r: string[]) => r.concat(Array<string>(Math.max(0, ncol - r.length)).fill(""));
    const head = pad(b.head);
    const rows = b.rows.map(pad);
    const numeric = (c: string) => {
      const t = this.text_of(c);
      return Boolean(re.fullmatch(String.raw`[~≈<>]?\s*[\d][\d.,%\s×x+\-–/:]*`, t)) && py.cpLen(t) < 24;
    };
    const num_col: boolean[] = [];
    for (let j = 0; j < ncol; j++) num_col.push(rows.filter((r) => strip(r[j]!)).every((r) => numeric(r[j]!)) && rows.some((r) => strip(r[j]!)));
    const status_col = head.map((h) => Boolean(re.fullmatch("(state|status)", py.lower(this.text_of(h)))));
    const align = (j: number) => (j < b.align.length ? b.align[j]! : null);
    const node: Node = { type: "table", cols: ncol, head: head.map((h, j) => ({ html: this.inline(h), align: align(j) })), rows: [] };
    for (const r of rows) {
      const first = strip(this.text_of(r[0]!));
      let rid: string | null = null;
      const tm = TOKEN.fullmatch(first);
      if (tm && !this.ids.has(token_id(first))) {
        rid = token_id(first);
        this.ids.add(rid);
        this.define(rid, "row", first + " · " + this.text_of(ncol > 1 ? r[1]! : ""), this.text_of(ncol > 2 ? r[2]! : ncol > 1 ? r[1]! : ""));
      }
      const cells: Node[] = [];
      r.forEach((c, j) => {
        const cls = (num_col[j] ? ["num"] : []).concat(j === 0 && tm ? ["tok"] : []);
        let content = this.inline(c);
        if (status_col[j] && strip(c) && !content.includes('<span class="chip')) {
          const k = sd.STATUS_WORDS.get(re.split(String.raw`[\s,;(]+`, py.lower(strip_tags(content)))[0]!);
          if (k) content = `<span class="chip ${k}">${content}</span>`;
        }
        cells.push({ html: content, cls, align: j < b.align.length && !num_col[j] ? b.align[j]! : null });
      });
      node.rows.push({ id: rid, cells });
    }
    return node;
  }

  node_code(b: Extract<sd.Block, { t: "code" }>): Node {
    const lang = b.lang;
    const raw = b.text;
    let text = esc(raw, false);
    if (lang === "sql" || lang === "psql" || lang === "postgres") {
      text = re.sub("(--[^\\n]*)", String.raw`<span class="c">\1</span>`, text);
      text = re.sub(SQL_KW, (m) => `<span class="k">${m.group(0)}</span>`, text);
      text = re.sub('<span class="c">(.*?)</span>', (m) => '<span class="c">' + re.sub("</?span[^>]*>", "", m.group(1)!) + "</span>", text);
    } else if (["ts", "tsx", "js", "typescript", "javascript", "mjs"].includes(lang)) {
      text = re.sub("(//[^\\n]*)", String.raw`<span class="c">\1</span>`, text);
    } else if (["py", "python", "sh", "bash", "yaml", "yml", "toml"].includes(lang)) {
      text = re.sub("(?m)(#[^\\n]*)$", String.raw`<span class="c">\1</span>`, text);
    }
    return { type: "code", lang, text: raw, html: text, section: this.cur_section ? this.cur_section.id : null, anchor: this.cur_anchor };
  }

  // -- containers
  node_box(b: BoxBlock, ctx: Ctx): Node {
    const { kind, title, attrs, blocks } = b;
    const inner: Ctx = { ...ctx, top: false };
    switch (kind) {
      case "cards":
        return this.box_cards(blocks, attrs, inner);
      case "persona":
        return this.box_persona(title, attrs, blocks, inner);
      case "duo":
        return { type: "duo", items: this.nodes(blocks, inner) };
      case "today":
        return this.box_today(title, attrs, blocks, inner);
      case "fold":
        return this.box_fold(title, attrs, blocks, inner);
      case "group": {
        const [t, sub] = split_title(title);
        const gid = truthy(attrs.get("id")) ? this.uid(attrs.get("id") as string) : null;
        ctx.has_group = true;
        if (this.cur_section !== null) this.cur_section.has_group = true;
        return { type: "group", id: gid, title: this.inline(t), sub: this.inline(sub), blocks: this.nodes(blocks, { ...inner, p_class: "" }) };
      }
      case "decisions":
        return { type: "decisions", blocks: this.nodes(blocks, { ...inner, p_class: "small muted" }) };
      case "decision":
        return this.box_decision(title, attrs, blocks, inner);
      case "options":
        return this.box_options(blocks, inner);
      case "steps": {
        const lists = blocks.filter((x): x is sd.ListBlock => x.t === "list").map((x) => this.node_list(x, inner, true));
        const rest = this.nodes(blocks.filter((x) => x.t !== "list"), inner);
        return { type: "steps", blocks: rest.concat(lists) };
      }
      case "cols":
        return { type: "cols", cols: blocks.map((x) => (x.t === "box" && x.kind === "col" ? this.nodes(x.blocks, inner) : [this.node(x, inner)])) };
      case "col":
        return { type: "col", blocks: this.nodes(blocks, inner) };
      case "glossary":
        return this.box_glossary(blocks);
      case "callout": {
        const low = py.lower(strip(title));
        const ta = attrs.get("tone");
        const tone = truthy(ta) ? ta : ["note", "warn", "good", "danger", "violet", "blue"].includes(low) ? low : "note";
        return { type: "callout", tone, blocks: this.nodes(blocks, { ...inner, p_class: "" }) };
      }
    }
    this.warn.push(`unknown container ::: ${kind}; rendered as a plain block`);
    return { type: "box", kind, blocks: this.nodes(blocks, inner) };
  }

  box_cards(blocks: Block[], attrs: Attrs, ctx: Ctx): Node {
    const cards: { title: string; attrs: Attrs; blocks: Block[] }[] = [];
    let cur: (typeof cards)[number] | null = null;
    for (const x of blocks) {
      if (x.t === "h") {
        const [text, hattrs] = parse_attrs(x.text);
        cur = { title: text, attrs: hattrs, blocks: [] };
        cards.push(cur);
      } else if (cur !== null) cur.blocks.push(x);
      else {
        cur = { title: "", attrs: new Map(), blocks: [x] };
        cards.push(cur);
      }
    }
    const out: Node[] = [];
    for (const c of cards) {
      const node: Node = { title: this.inline(c.title), blocks: this.nodes(c.blocks, { ...ctx, p_class: "" }) };
      if (truthy(c.attrs.get("id"))) {
        node.id = this.uid(c.attrs.get("id") as string);
        this.define(node.id, "card", c.title, (node.blocks as Node[]).map((n) => strip_tags(n.html ?? "")).join(" "));
      }
      out.push(node);
    }
    return { type: "cards", four: truthy(attrs.get("four")), stat: truthy(attrs.get("stat")), cards: out };
  }

  box_persona(title: string, attrs: Attrs, blocks: Block[], ctx: Ctx): Node {
    const idAttr = attrs.get("id");
    const pid = this.uid(truthy(idAttr) ? (idAttr as string) : "fe-" + slug(this.text_of(title)));
    const shots_mode = aget(attrs, "shots", "auto");
    const text: Block[] = [];
    const media: Extract<Block, { t: "img" }>[] = [];
    const today: BoxBlock[] = [];
    for (const x of blocks) {
      if (x.t === "img") media.push(x);
      else if (x.t === "box" && x.kind === "today") today.push(x);
      else text.push(x);
    }
    const per_shot = typeof shots_mode === "string" && ["phone", "mid", "wide", "col"].includes(shots_mode) ? shots_mode : "auto";
    const shots = media.map((x) => this.node_shot(x, per_shot));
    let grid = typeof shots_mode === "string" && (shots_mode === "cols3" || shots_mode === "cols2") ? shots_mode : "";
    if (!grid && shots_mode === "auto" && shots.length >= 3 && shots.every((s) => s.size === "col" || s.size === "phone")) grid = "cols3";
    if (grid) for (const s of shots) s.size = "col";
    const body = this.nodes(text, { ...ctx, p_class: "small" });
    const fp = body.find((n) => n.type === "p");
    const first_p = fp ? strip_tags(fp.html) : "";
    this.define(pid, "persona", title, first_p);
    this.nav_sub(pid, this.text_of(title), attrs);
    const node: Node = {
      type: "persona", id: pid, title: this.inline(title), chip: aget(attrs, "chip", ""), chipTone: aget(attrs, "tone", "new"), grid,
      text: body, media: shots, today: null,
    };
    if (today.length) {
      const td = today[0]!;
      node.today = this.box_today(td.title, td.attrs, td.blocks, { ...ctx, shots: per_shot, grid });
    }
    return node;
  }

  box_today(title: string, attrs: Attrs, blocks: Block[], ctx: Ctx): Node {
    const shots = blocks.filter((x): x is Extract<Block, { t: "img" }> => x.t === "img").map((x) => this.node_shot(x, ctx.shots ?? "auto"));
    if (truthy(ctx.grid)) for (const s of shots) s.size = "col";
    let rest = this.nodes(blocks.filter((x) => x.t !== "img"), { ...ctx, p_class: "small" });
    for (const n of rest.filter((n) => n.type === "shots")) {
      shots.push(...(n.media as Node[]));
      rest = rest.filter((x) => x !== n);
    }
    return { type: "today", label: aget(attrs, "label", "Today, for comparison"), summary: this.inline(title), blocks: rest, media: shots, grid: ctx.grid ?? "" };
  }

  box_fold(title: string, attrs: Attrs, blocks: Block[], ctx: Ctx): Node {
    const [t, one] = split_title(title);
    const idAttr = attrs.get("id");
    const explicit = truthy(idAttr);
    const fid = this.uid(truthy(idAttr) ? (idAttr as string) : "fold-" + slug(this.text_of(t)));
    this.define(fid, "fold", t, one);
    if (explicit) this.nav_sub(fid, this.text_of(t), attrs);
    return { type: "fold", id: fid, title: this.inline(t), one: this.inline(one), open: truthy(attrs.get("open")), blocks: this.nodes(blocks, { ...ctx, p_class: "" }) };
  }

  box_decision(title: string, attrs: Attrs, blocks: Block[], ctx: Ctx): Node {
    const [did, question] = split_title(title);
    const status = aget(attrs, "status", "") as string;
    let state = attrs.get("state") as string | true | undefined;
    if (!truthy(state)) {
      const low = py.lower(strOf(status, "lower"));
      state = re.search(DATE, status) && !re.match("(open|provisional|proposed|waiting)", low) ? "done" : "open";
      if (re.match("(held|blocked|rejected)", low)) state = "held";
    }
    const idAttr = attrs.get("id");
    const anchor = this.uid(truthy(idAttr) ? (idAttr as string) : token_id(did));
    const body = this.nodes(blocks, { ...ctx, p_class: "" });
    const paras = body.filter((n) => n.type === "p").map((n) => py.html.unescape(strip_tags(n.html)));
    const first_p = paras.length ? paras[0]! : "";
    const reading = paras.find((t) => py.lower(t).startsWith("read as")) ?? "";
    const quote = first_p !== reading ? first_p : "";
    const status_text = this.text_of(status);
    const m_date = DATE.search(status_text);
    const by = status_text ? strip(re.split("[,·]", status_text, 1)[0]!) : "";
    this.define(anchor, "decision", `${did} · ${question}`, first_p);
    this.decisions.push({
      id: did, anchor, question: this.text_of(question), status: status_text, state, section: this.cur_section ? this.cur_section.id : null,
      quote, reading, decidedBy: by, decidedOn: m_date ? m_date.group(0) : null,
    });
    this.nav_sub(anchor, `${did} · ${this.text_of(question)}`, attrs);
    return { type: "decision", id: anchor, did, question: this.inline(question), status: this.inline(status), state, wide: truthy(attrs.get("wide")), blocks: body };
  }

  box_options(blocks: Block[], ctx: Ctx): Node {
    const opts: Node[] = [];
    let cur: Node | null = null;
    for (const x of blocks) {
      if (x.t === "h") {
        const [text, hattrs] = parse_attrs(x.text);
        cur = { title: this.inline(text), rec: truthy(hattrs.get("rec")), blocks: [] };
        opts.push(cur);
      } else if (cur !== null) cur.blocks.push(this.node(x, ctx));
    }
    return { type: "options", options: opts };
  }

  box_glossary(blocks: Block[]): Node {
    const items: Node[] = [];
    for (const x of blocks) {
      if (x.t !== "list") continue;
      for (const it of x.items) {
        const m = re.match(String.raw`^\*\*([^*]+)\*\*\s*[:—–-]?\s*(.*)$`, it.text);
        if (!m) continue;
        const term = strip(m.group(1)!);
        const definition = strip(m.group(2)!);
        const gid = this.uid("g-" + slug(term));
        this.glossary.set(term, gid);
        this.define(gid, "glossary", term, definition);
        items.push({ id: gid, term, html: this.inline(definition) });
      }
    }
    return { type: "glossary", items };
  }

  node_prs(): Node | null {
    if (!this.prs_dir) {
      this.warn.push("<!-- PR-CARDS --> but no prs/ folder (pass --prs)");
      return { type: "placeholder", html: "PR cards: no prs/pr-NN.md yet." };
    }
    const prs = load_prs(this.prs_dir);
    if (!prs.length) {
      this.warn.push(`--prs ${this.prs_dir}: no pr-NN.md files`);
      return null;
    }
    const groups: Node[] = [];
    for (const p of prs) {
      if (!groups.length || groups[groups.length - 1]!.ms !== p.ms) {
        const head = strip(re.split(String.raw`\s+·\s+|:\s+`, p.ms, 1)[0]!);
        let mid: string | null = null;
        if (TOKEN.fullmatch(head) && !this.ids.has(token_id(head))) {
          mid = token_id(head);
          this.ids.add(mid);
          this.define(mid, "milestone", p.ms, p.ms_note || "");
        }
        groups.push({ ms: esc(p.ms), id: mid, note: esc(p.ms_note || ""), prs: [] });
      }
      const pid = this.uid(`pr-${p.label}`);
      let body = py.replace(py.replace(sd.prs.markdown(p.body), "<table>", '<div class="tbl"><table>'), "</table>", "</table></div>");
      body = re.sub(String.raw`^\s*<h2>Summary</h2>\s*`, "", body);
      body = re.sub(String.raw`\[([^\]]+)\]\((#[\w.-]+)\)`, String.raw`<a href="\2">\1</a>`, body);
      body = this.sanitize(CODE.sub((m) => this.cite_code(m), body));
      const mentioned = sortTokens(new Set([...TOKEN.finditer(strip_tags(body))].map((m) => m.group(1)!)));
      const paths = py.sorted(new Set(
        (re.findall("<code>([^<]+)</code>", sd.prs.markdown(p.body)) as string[]).map((x) => CITE.match(py.html.unescape(x))).filter((c) => c).map((c) => c!.group(1)!),
      ));
      const deps_html = p.depends_on.length ? "Depends on " + p.depends_on.map((d) => esc(d)).join(", ") : "";
      groups[groups.length - 1]!.prs.push({
        n: p.n, sub: p.sub, label: p.label, id: pid, title: this.sanitize(sd.prs.inline(p.title)), change: this.sanitize(sd.prs.inline(p.change)),
        dependsOn: p.depends_on, deps: deps_html, decisions: mentioned.filter((t) => t.startsWith("D")), tokens: mentioned, paths,
        rollback: p.rollback && p.rollback !== "not stated" ? this.sanitize(sd.prs.inline(p.rollback)) : "", bodyHtml: body, file: p.file,
      });
      this.define(pid, "pr", `PR ${p.label} · ${p.title}`, p.change);
    }
    this.pr_count = prs.length;
    return { type: "prs", groups };
  }

  nav_sub(sid: string, label: string, attrs: Attrs): void {
    const nav = attrs.get("nav");
    if (nav === "no" || nav === "none" || this.cur_section === null) return;
    if (typeof nav === "string" && nav !== "yes" && nav !== "no") label = nav;
    this.cur_section.subs.push({ id: sid, title: label });
  }

  // -- page
  structure(text: string): [string | null, Block[], Section[]] {
    text = py.replace(py.replace(py.replace(text, "\r\n", "\n"), "&#91;", "["), "&#93;", "]");
    const blocks = parse_ext(py.str.split(text, "\n"));
    let title: string | null = null;
    const pre: Block[] = [];
    const sections: Section[] = [];
    let cur: Section | null = null;
    for (const b of blocks) {
      if (b.t === "h" && b.level === 1 && title === null) {
        title = b.text;
        continue;
      }
      if (b.t === "h" && b.level === 2) {
        const [t, attrs] = parse_attrs(b.text);
        cur = { title: t, attrs, blocks: [], subs: [], has_group: false, num: 0, id: "" };
        sections.push(cur);
        continue;
      }
      (cur ? cur.blocks : pre).push(b);
    }
    sections.forEach((s, k) => {
      s.num = k + 1;
      const idAttr = s.attrs.get("id");
      s.id = this.uid(truthy(idAttr) ? (idAttr as string) : slug(this.text_of(s.title)));
    });
    return [title, pre, sections];
  }

  link_tree(node: unknown, families: Map<string, PyValue>, stats: Stats, seen: Set<string> | null = null): void {
    if (Array.isArray(node)) {
      for (const x of node) this.link_tree(x, families, stats, seen);
    } else if (isDictLike(node)) {
      if (dget(node, "type") === "glossary") return; // the definitions themselves are not re-linked
      for (const [k, v] of dentries(node)) {
        if (SKIP_TREE.has(k)) continue; // rendered as plain text by the app (rail, hotspot cards)
        if (LINK_KEYS.has(k) && typeof v === "string" && v) dset(node, k, link_refs(v, this.ids, families, this.glossary, stats, seen));
        else if (Array.isArray(v) || isDictLike(v)) this.link_tree(v, families, stats, seen);
      }
    }
  }

  build(): Summary {
    const a = this.a;
    const meta = this.meta;
    const src_text = py.io.readText(a.src);
    const [title, pre, sections] = this.structure(src_text);
    const h1v = get(meta, "h1");
    const h1 = (truthy(h1v) ? h1v : title || py.path.splitext(py.path.basename(a.src))[0]) as string;
    const tt = get(meta, "title");
    const tab_title = (truthy(tt) ? tt : sd.pageTitle(this.text_of(h1))) as string;
    const out_sections: Node[] = [];
    for (const s of sections) {
      this.cur_section = s;
      const ctx: Ctx = { section: s.id, p_class: "", top: true };
      const blocks = [...s.blocks];
      const nodes: Node[] = [];
      const lead = s.attrs.get("lead");
      if (lead !== "no" && blocks.length && blocks[0]!.t === "p") nodes.push(this.para((blocks.shift() as { text: string }).text, ctx, "lead"));
      nodes.push(...this.nodes(blocks, ctx));
      this.define(s.id, "section", `${s.num} · ${s.title}`, "");
      out_sections.push({
        id: s.id, num: s.num, title: this.inline(s.title), titleText: this.text_of(s.title),
        chip: aget(s.attrs, "chip", ""), chipTone: aget(s.attrs, "tone", "draft"), hasGroup: s.has_group, blocks: nodes, subs: s.subs,
      });
    }
    this.cur_section = null;
    const status = strOf(get(meta, "status", "Draft for review"), "lower");
    const low = py.lower(status);
    const st = get(meta, "status_tone");
    const tone = truthy(st) ? st : low.startsWith("draft") ? "draft" : STATUS_DONE.some((p) => low.startsWith(p)) ? "done"
      : STATUS_HELD.some((p) => low.startsWith(p)) ? "held" : low.includes("review") ? "proposed" : "draft";
    let author: PyValue = "";
    for (const p of iter(get(meta, "people", []))) {
      const pd = dictOf(p);
      if (!pd.has("role")) throw new PyError("KeyError", py.repr("role"));
      if (py.lower(strOf(pd.get("role"), "lower")) === "author") {
        if (!pd.has("name")) throw new PyError("KeyError", py.repr("name"));
        author = pd.get("name")!;
        break;
      }
    }
    const fv = get(meta, "footer");
    const footer = (truthy(fv) ? fv : (truthy(author) ? `Draft by ${pyStr(author)}, ${pyStr(get(meta, "updated", ""))}. ` : "")
      + "Decisions get a D-number only once the owner answers; everything else is marked as a proposal.") as string;
    for (const [key, label, anchor] of this.pending_hrefs)
      if (!this.ids.has(anchor)) this.warn.push(`hotspot ${key}/${py.repr(label)}: anchor #${anchor} is not defined on the page`);
    const stats: Stats = { linked: 0, unlinked: [] };
    const families = new Map<string, PyValue>(dictOf(get(meta, "refs", new Map())));
    for (const [fam, href] of families)
      if (strOf(href, "startswith").startsWith("#") && !this.ids.has((href as string).slice(1)))
        this.warn.push(`refs family ${fam} points at ${href as string}, which is not an anchor on the page`);
    for (const sec of out_sections) {
      stats.section = sec.id;
      this.link_tree(sec, families, stats, new Set());
    }
    const lede = pre.filter((b): b is Extract<Block, { t: "p" }> => b.t === "p").map((b) => this.para(b.text, { p_class: "" }, "lead"));
    stats.section = "lede";
    this.link_tree(lede, families, stats, new Set());
    const words = re.findall("[A-Za-z0-9'’]+", re.sub("```.*?```|\\]\\([^)]*\\)|[`*_#|\\[\\]()>{}-]", " ", src_text, 0, re.S)).length;
    const unlinked = py.sorted(new Set(stats.unlinked));
    for (const u of unlinked)
      this.warn.push(`reference token ${u} has no definition on the page (no element with id ${token_id(u)}, no refs family in meta.json)`);
    const files = this.assets.finish();
    this.dash_proposed(files);
    const content = {
      template: "tdd/1", generated: py.time.utcStamp(), source: py.path.basename(a.src),
      title: this.inline(h1), titleText: this.text_of(h1), tabTitle: tab_title,
      meta: {
        eyebrow: this.inline(strOf(get(meta, "eyebrow", "Technical design"), "replace")), project: get(meta, "project"), people: get(meta, "people", []),
        updated: get(meta, "updated", ""), rev: get(meta, "rev"), revNote: get(meta, "rev_note", ""), status, statusTone: tone,
        repoUrl: get(meta, "repo_url"), sha: get(meta, "sha"), shaDate: get(meta, "sha_date"), repoPath: py.path.homeRel(get(meta, "repo_path")),
        pathAliases: this.aliases, refs: families, footer: this.inline(footer),
        dataSources: firstTruthy(get(meta, "data_sources"), get(meta, "dataSources"), []),
      },
      lede,
      nav: out_sections.map((s) => ({ id: s.id, num: s.num, title: s.titleText, subs: s.subs })),
      sections: out_sections, defs: this.defs, anchors: this.anchors, decisions: this.decisions, cites: this.cites,
      glossary: this.glossary, schema: this.schema, refs: { linked: stats.linked, unlinked, links: stats.links ?? [] },
      stats: {
        sections: out_sections.length, diagrams: this.fig_no, prs: this.pr_count, decisions: this.decisions.length, words,
        readingMinutes: Math.max(1, py.roundInt(words / 230)),
      },
    };
    makedirs(this.out_dir, true);
    const cpath = py.path.join(this.out_dir, "content.json");
    py.io.writeText(cpath, py.json.dumps(content, { indent: 1, ensureAscii: false }));
    const over: string[] = [];
    for (const [p, s] of files) if (py.io.getsize(s) > sd.FILE_LIMIT) over.push(p);
    const manifest = {
      page: "tdd.html", root: this.out_dir, title: tab_title, template: "tdd/1", content: "content.json",
      files, missing: this.assets.missing, over_15mb: over, warnings: this.warn,
    };
    py.io.writeText(py.path.join(this.out_dir, "content.files.json"), py.json.dumps(manifest, { indent: 2, ensureAscii: false }));
    const line = `wrote ${cpath}: ${out_sections.length} sections, ${this.fig_no} diagrams, ${this.decisions.length} decisions, ${this.pr_count} PRs, ` +
      `${this.schema.length} schema tables, ${stats.linked} reference links (${unlinked.length} unlinked), ${this.cites.length} citations, ` +
      `${files.size} files (${this.assets.missing.length} missing)`;
    return { cpath, line, warnings: this.warn };
  }
}

/** The PR card's mentioned tokens: sorted by (family, the number as a float, the token), build_tdd.py:784. */
function sortTokens(toks: Set<string>): string[] {
  const keyed = [...toks].map((t) => ({ t, fam: re.match("[A-Z]+", t)!.group(0)!, n: floatOf(re.sub(String.raw`[^\d.]`, "", t) || "0") }));
  keyed.sort((x, y) => py.cmp(x.fam, y.fam) || (x.n < y.n ? -1 : x.n > y.n ? 1 : 0) || py.cmp(x.t, y.t));
  return keyed.map((k) => k.t);
}

/** `a or b or c` */
function firstTruthy(...xs: PyValue[]): PyValue {
  for (const x of xs.slice(0, -1)) if (truthy(x)) return x;
  return xs[xs.length - 1]!;
}

/** Iterate a Python list (a str or dict iterates too, but the compiler only meets lists here). */
function iter(v: PyValue | undefined): PyValue[] {
  if (Array.isArray(v)) return v;
  if (v instanceof Map) return [...v.keys()];
  if (typeof v === "string") return [...v];
  const t = v === null || v === undefined ? "NoneType" : typeof v === "boolean" ? "bool" : typeof v === "number" ? "int" : typeof v;
  throw new PyError("TypeError", `'${t}' object is not iterable`);
}

const isDictLike = (v: unknown): v is Node | Map<string, unknown> =>
  v instanceof Map || (typeof v === "object" && v !== null && !Array.isArray(v) && Object.getPrototypeOf(v) === Object.prototype);
const dget = (d: Node | Map<string, unknown>, k: string): unknown => (d instanceof Map ? d.get(k) : d[k]);
const dentries = (d: Node | Map<string, unknown>): [string, unknown][] => (d instanceof Map ? [...d.entries()] : Object.entries(d));
const dset = (d: Node | Map<string, unknown>, k: string, v: unknown): void => {
  if (d instanceof Map) d.set(k, v);
  else d[k] = v;
};

/** The compile, end to end: content.json, content.files.json, the copied files; the stdout line and warnings returned. */
export function compile(a: Args): Summary {
  return new Compiler(a).build();
}
