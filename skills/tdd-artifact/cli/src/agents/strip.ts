// strip_page.py as a library: parse a built page into a tolerant tree, strip the chrome, render markdown / text,
// outline the headings. Byte-identical with the Python on the same input (test/agents).
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { HTMLParser, type Attr } from "./htmlparser";
import {
  PY_WS, basename, cpFirst, cpLast, cpLen, decodeReplace, dumps, escape, isspace1, lstrip, sha256, sorted, splitWs, strip as pystrip,
  utf8Len, pjoin,
} from "./_py";
import { PyError } from "../py/errors";
import { int } from "../py/num";

export const VERSION = "strip_page/1";
export const VOID = new Set(["area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"]);
export const BLOCK = new Set(["address", "article", "aside", "blockquote", "body", "caption", "dd", "details", "dialog", "div", "dl", "dt",
  "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header",
  "hgroup", "hr", "html", "li", "main", "nav", "ol", "p", "pre", "section", "summary", "table", "tbody", "td",
  "tfoot", "th", "thead", "tr", "ul", "#root"]);
const P_CLOSERS = new Set([...BLOCK].filter((t) => !["td", "th", "tr", "tbody", "thead", "tfoot", "caption", "li", "dd", "dt", "summary", "#root"].includes(t)));
export const HEADINGS = new Set(["h1", "h2", "h3", "h4", "h5", "h6"]);
const DROP_TAGS = new Set(["script", "style", "template", "noscript", "button", "dialog", "link", "meta", "iframe", "object", "canvas",
  "select", "textarea", "head", "title", "base"]);
const DROP_CLASSES = new Set(["fnav", "fnav-toggle", "hs", "hs-pop", "ref-pop", "ref-panel", "shot-scrim", "drawer", "sr-only", "zoom"]);
const KEEP_ATTRS: Record<string, string[]> = { a: ["href"], img: ["src", "alt", "width", "height"], details: ["open"], td: ["colspan", "rowspan"],
  th: ["colspan", "rowspan", "scope"], ol: ["start", "reversed"], time: ["datetime"] };
const BOOL_ATTRS = new Set(["open", "reversed", "checked", "disabled", "hidden"]);
const BR = "\u2028";   // line-break sentinel that survives whitespace collapsing

export type Child = Node | string;

export class Node {
  tag: string;
  attrs: Map<string, string>;
  children: Child[] = [];
  parent: Node | null;

  constructor(tag: string, attrs?: Iterable<[string, string]>, parent: Node | null = null) {
    this.tag = tag;
    this.attrs = new Map(attrs ?? []);
    this.parent = parent;
  }

  get classes(): string[] {
    return splitWs(this.attrs.get("class") || "");
  }

  get(k: string): string | undefined {
    return this.attrs.get(k);
  }

  text(): string {
    let s = "";
    for (const c of this.children) s += typeof c === "string" ? c : c.text();
    return s;
  }

  /** Depth-first, document order, self included. */
  *elements(): Generator<Node> {
    const stack: Node[] = [this];
    while (stack.length) {
      const n = stack.pop()!;
      yield n;
      for (let i = n.children.length - 1; i >= 0; i--) {
        const c = n.children[i]!;
        if (typeof c !== "string") stack.push(c);
      }
    }
  }

  find(pred: (e: Node) => boolean): Node | null {
    for (const e of this.elements()) if (e !== this && pred(e)) return e;
    return null;
  }
}

function lowerAttrs(attrs: Attr[]): [string, string][] {
  return attrs.map(([k, v]) => [k.toLowerCase(), v !== null ? v : ""]);
}

class Builder extends HTMLParser {
  root = new Node("#root");
  cur: Node = this.root;

  private nearest(tags: Set<string>, stops: Set<string>): Node | null {
    let n: Node | null = this.cur;
    while (n !== null && n !== this.root) {
      if (tags.has(n.tag)) return n;
      if (stops.has(n.tag)) return null;
      n = n.parent;
    }
    return null;
  }

  private close_(n: Node | null): void {
    if (n !== null) this.cur = n.parent!;
  }

  handle_starttag(tag: string, attrs: Attr[]): void {
    tag = tag.toLowerCase();
    if (P_CLOSERS.has(tag) && this.cur.tag === "p") this.close_(this.cur);
    if (tag === "li") this.close_(this.nearest(new Set(["li"]), new Set(["ul", "ol", "menu"])));
    else if (tag === "dt" || tag === "dd") this.close_(this.nearest(new Set(["dt", "dd"]), new Set(["dl"])));
    else if (tag === "tr") this.close_(this.nearest(new Set(["tr"]), new Set(["table", "thead", "tbody", "tfoot"])));
    else if (tag === "td" || tag === "th") this.close_(this.nearest(new Set(["td", "th"]), new Set(["tr", "table"])));
    else if (tag === "thead" || tag === "tbody" || tag === "tfoot") this.close_(this.nearest(new Set(["thead", "tbody", "tfoot"]), new Set(["table"])));
    const n = new Node(tag, lowerAttrs(attrs), this.cur);
    this.cur.children.push(n);
    if (!VOID.has(tag)) this.cur = n;
  }

  handle_startendtag(tag: string, attrs: Attr[]): void {
    tag = tag.toLowerCase();
    const n = new Node(tag, lowerAttrs(attrs), this.cur);
    this.cur.children.push(n);
  }

  handle_endtag(tag: string): void {
    tag = tag.toLowerCase();
    if (VOID.has(tag)) return;
    let n: Node | null = this.cur;
    while (n !== null && n !== this.root && n.tag !== tag) n = n.parent;
    if (n === null || n === this.root) return;   // stray end tag
    this.cur = n.parent!;
  }

  handle_data(data: string): void {
    if (data) this.cur.children.push(data);
  }
}

export function parse(text: string): Node {
  const b = new Builder();
  b.feed(text);
  b.close();
  return b.root;
}

const WS_ASCII = /[ \t\r\n\f\v]+/g;
export function norm(s: string): string {
  return pystrip(s.replace(WS_ASCII, " "));
}

// ----------------------------------------------------------------------------- strip

function contentRoot(root: Node): Node {
  const r = root.find((e) => e.get("id") === "tdd-root");
  if (r) return r;
  const bodies = [...root.elements()].filter((e) => e.tag === "body");
  return bodies.length ? bodies[bodies.length - 1]! : root;   // a claude.ai export wraps the page in a host document
}

function* ancestors(e: Node): Generator<Node> {
  let p = e.parent;
  while (p !== null) {
    yield p;
    p = p.parent;
  }
}
export { ancestors };

const LANG_START = /^(?:lang|language)-/;
const LANG = new RegExp(`^(?:lang|language)-([^${PY_WS}]+)`);
function langOf(code: Node): string {
  for (const c of code.classes) {
    if (LANG_START.test(c)) {
      const m = LANG.exec(c);
      return m ? m[1]! : "";
    }
  }
  return "";
}

type TResult = Child | Child[] | null;

/** Remove the page chrome in place. Returns [content node, title, counts of what was removed]. */
export function strip(root: Node): [Node, string, Map<string, number>] {
  const titles = [...root.elements()].filter((e) => e.tag === "title").map((e) => norm(e.text()));
  const title = [...titles].reverse().find((t) => t) ?? "";
  const removed = new Map<string, number>();
  const content = contentRoot(root);
  const bump = (k: string) => removed.set(k, (removed.get(k) ?? 0) + 1);

  const transform = (n: Node): TResult => {
    const tag = n.tag, cls = new Set(n.classes);
    if (tag === "svg") {
      let label = n.get("aria-label") || "";
      if (!label) {
        label = "";
        for (const e of n.elements()) if (e.tag === "title") { label = norm(e.text()); break; }
      }
      bump("svg");
      return label ? `[inline SVG: ${label}]` : null;
    }
    if (DROP_TAGS.has(tag)) {
      bump(tag);
      return null;
    }
    const hit = [...cls].filter((c) => DROP_CLASSES.has(c));
    if (hit.length) {
      bump("." + sorted(hit)[0]);
      return null;
    }
    if (n.attrs.has("hidden") && (n.attrs.get("hidden") || "").toLowerCase() !== "until-found") {
      bump("[hidden]");
      return null;
    }
    if (n.get("aria-hidden") === "true") {
      bump("[aria-hidden]");
      return null;
    }
    if (tag === "nav" && (n.get("aria-label") || "").toLowerCase() === "contents") {
      bump("nav");
      return null;
    }
    if (tag === "a" && (cls.has("ref") || n.attrs.has("data-ref"))) {
      const rid = n.get("data-ref") || lstrip(n.get("href") || "", "#");
      const t = norm(n.text());
      bump("a.ref");
      return rid ? `${t} (\u2192 #${rid})` : t;
    }
    if (tag === "input") {
      if ((n.get("type") || "").toLowerCase() === "checkbox") return n.attrs.has("checked") ? "[x] " : "[ ] ";
      return null;
    }
    if (tag === "span" && (cls.has("chip") || cls.has("pill"))) return ` [${norm(n.text())}] `;
    if (cls.has("key") && (tag === "span" || tag === "div")) {
      const items: string[] = [];
      for (const sp of n.children) {
        if (typeof sp === "string") continue;
        let sw: Node | null = null;
        for (const e of sp.elements()) if (e.classes.includes("sw")) { sw = e; break; }
        const tone = (sw ? sw.classes : []).filter((c) => c !== "sw").join(" ");
        const label = norm(sp.text());
        if (label) items.push(tone ? `${label} = ${tone}` : label);
      }
      return items.length ? " Legend: " + items.join(" \u00b7 ") + ". " : null;
    }
    if (tag === "pre") {
      let lang = n.get("data-lang") || "";
      const code = n.children.find((c): c is Node => typeof c !== "string" && c.tag === "code");
      if (code !== undefined) {
        lang = lang || langOf(code);
        code.attrs = lang ? new Map([["class", `language-${lang}`]]) : new Map();
        code.children = [code.text()];   // drop highlighting spans, keep the raw text
      }
      n.attrs = new Map();
      return n;
    }
    clean(n);
    if (tag === "details") n.attrs.set("open", "");
    const allowed = new Set(["id", ...(KEEP_ATTRS[tag] ?? [])]);
    n.attrs = new Map([...n.attrs].filter(([k]) => allowed.has(k)));
    if (tag === "span" && !n.attrs.size) {
      // React SSR puts no whitespace between elements: pad unwrapped spans so words don't glue together
      for (const a of ancestors(n)) if (a.tag === "pre" || a.tag === "code") return n.children;
      return [" " + (cls.has("one") ? "\u00b7 " : ""), ...n.children, " "];
    }
    return n;
  };

  const clean = (n: Node): void => {
    const out: Child[] = [];
    for (const ch of n.children) {
      if (typeof ch === "string") {
        out.push(ch);
        continue;
      }
      const r = transform(ch);
      if (r === null) continue;
      for (const x of Array.isArray(r) ? r : [r]) {
        if (typeof x !== "string") x.parent = n;
        out.push(x);
      }
    }
    n.children = out;
  };

  if (content !== root) {
    for (const e of root.elements()) {
      if ((e.tag === "script" || e.tag === "style" || e.tag === "template")) {
        let inside = false;
        for (const a of ancestors(e)) if (a === content) { inside = true; break; }
        if (!inside) bump(e.tag + " (outside the page root)");
      }
    }
  }
  clean(content);
  return [content, title, removed];
}

export function toHtml(n: Child): string {
  if (typeof n === "string") return escape(n, false);
  let inner = "";
  for (const c of n.children) inner += toHtml(c);
  if (n.tag === "#root") return inner;
  let attrs = "";
  for (const [k, v] of n.attrs) attrs += BOOL_ATTRS.has(k) && v === "" ? ` ${k}` : ` ${k}="${escape(v)}"`;
  if (VOID.has(n.tag)) return `<${n.tag}${attrs}>`;
  const nl = BLOCK.has(n.tag) ? "\n" : "";
  return `<${n.tag}${attrs}>${inner}</${n.tag}>${nl}`;
}

// ----------------------------------------------------------------------------- markdown / text

function ticks(s: string): string {
  return s.includes("`") ? `\`\` ${s} \`\`` : `\`${s}\``;
}

export interface Hooks {
  ref?: ((id: string, text: string) => string) | undefined;
  cite?: ((text: string) => string) | undefined;
  href?: ((url: string, text: string) => string | null) | undefined;
  img?: ((src: string, alt: string) => string | null) | undefined;
  pre?: ((lang: string, text: string) => void) | undefined;
  heading_shift?: number | undefined;
}

const WS_ANY = /[ \t\r\n\f\v]+/g;

export class Renderer {
  md: boolean;
  shift: number;
  constructor(mode: string, private readonly h: Hooks = {}) {
    this.md = mode === "md";
    this.shift = h.heading_shift ?? 0;
  }

  sfx(aid: string | undefined): string {
    return aid ? ` {#${aid}}` : "";
  }

  blocks(n: Node): string[] {
    const out: string[] = [];
    let run: string[] = [];
    const flush = () => {
      const t = this.finish(run.join(""));
      run = [];
      if (t) out.push(t);
    };
    for (const ch of n.children) {
      if (typeof ch === "string") run.push(ch);
      else if (BLOCK.has(ch.tag)) {
        flush();
        out.push(...this.block(ch));
      } else run.push(this.inlineEl(ch));
    }
    flush();
    return out;
  }

  finish(s: string): string {
    s = pystrip(s.replace(WS_ANY, " "));
    return pystrip(s.replaceAll(" " + BR, BR).replaceAll(BR + " ", BR).replaceAll(BR, this.md ? "\n" : " "));
  }

  block(el: Node): string[] {
    const t = el.tag;
    const aid = el.get("id");
    if (HEADINGS.has(t)) {
      const lvl = Math.max(1, Math.min(6, Number(t[1]) + this.shift));
      const s = this.inline(el);
      return s ? ["#".repeat(lvl) + " " + s + this.sfx(aid)] : [];
    }
    if (t === "p") {
      const s = this.inline(el);
      return s ? [s + this.sfx(aid)] : aid ? [`{#${aid}}`] : [];
    }
    if (t === "ul" || t === "ol") {
      const s = this.list(el);
      return s ? [s] : [];
    }
    if (t === "table") {
      const s = this.table(el);
      return s ? [s] : [];
    }
    if (t === "pre") return [this.pre(el)];
    if (t === "hr") return ["---"];
    if (t === "blockquote") {
      const inner = this.md ? this.blocks(el).join("\n\n") : this.blocks(el).join("\n");
      return inner ? [inner.split("\n").map((l) => (l ? "> " + l : ">")).join("\n")] : [];
    }
    if (t === "details") {
      const summ = el.children.find((c): c is Node => typeof c !== "string" && c.tag === "summary");
      const head = summ !== undefined ? this.inline(summ) : "";
      const rest = new Node("div");
      rest.children = el.children.filter((c) => c !== summ);
      const out = head || aid ? [(this.md ? `**${head}**` : head) + this.sfx(aid)] : [];
      return [...out, ...this.blocks(rest)];
    }
    if (t === "summary") {
      const s = this.inline(el);
      return s ? [(this.md ? `**${s}**` : s) + this.sfx(aid)] : [];
    }
    if (t === "dl") {
      const lines: string[] = [];
      for (const c of el.children) {
        if (typeof c !== "string" && (c.tag === "dt" || c.tag === "dd" || c.tag === "div")) {
          const pairs = c.tag !== "div" ? [c] : c.children.filter((x): x is Node => typeof x !== "string");
          for (const x of pairs) {
            const s = this.inline(x);
            if (x.tag === "dt") lines.push("- " + (this.md ? `**${s}**` : s) + this.sfx(x.get("id") || c.get("id")));
            else if (s) lines.push((lines.length ? "  " : "") + s);
          }
        }
      }
      return lines.length ? [lines.join("\n")] : [];
    }
    if (t === "li") {
      const s = this.inline(el);
      return s ? ["- " + s] : [];
    }
    const bs = this.blocks(el);
    if (aid) {
      const b0 = bs[0];
      if (b0 !== undefined && (b0.startsWith("#") || b0.startsWith("**")) && !b0.split("\n")[0]!.includes("{#")) {
        const i = b0.indexOf("\n");
        const first = i < 0 ? b0 : b0.slice(0, i), more = i < 0 ? "" : b0.slice(i + 1);
        bs[0] = first + this.sfx(aid) + (more ? "\n" + more : "");
      } else bs.unshift(`{#${aid}}`);
    }
    return bs;
  }

  list(el: Node): string {
    const ordered = el.tag === "ol";
    let num = pyInt(el.get("start") || "1") ?? 1;
    const lines: string[] = [];
    for (const li of el.children) {
      if (typeof li === "string" || li.tag !== "li") continue;
      const marker = ordered ? `${num}.` : "-";
      num += 1;
      const parts = this.blocks(li);
      const pad = " ".repeat(cpLen(marker) + 1);
      let first = parts.length ? parts[0]! : "";
      if (li.get("id")) {
        const i = first.indexOf("\n");
        const f0 = i < 0 ? first : first.slice(0, i), fr = i < 0 ? "" : first.slice(i + 1);
        first = f0 + this.sfx(li.get("id")) + (fr ? "\n" + fr : "");
      }
      const body = [pyRstrip(`${marker} ${first}`), ...parts.slice(1)];
      const text = body.join("\n");
      lines.push(text.split("\n").map((l, i) => (i && l ? pad + l : l)).join("\n"));
    }
    return lines.join("\n");
  }

  cell(c: Node): string {
    return pystrip(this.blocks(c).map((b) => b.replaceAll("\n", " ")).join(" "));
  }

  table(el: Node): string {
    const rows: [string[], boolean][] = [];
    for (const tr of el.elements()) {
      if (tr.tag !== "tr") continue;
      let owner = tr.parent;
      while (owner !== null && owner.tag !== "table") owner = owner.parent;
      if (owner !== el) continue;
      const cells = tr.children.filter((c): c is Node => typeof c !== "string" && (c.tag === "td" || c.tag === "th"));
      const texts = cells.map((c) => this.cell(c));
      if (tr.get("id") && texts.length) texts[0] += this.sfx(tr.get("id"));
      const head = (tr.parent !== null && tr.parent.tag === "thead") || (cells.length > 0 && cells.every((c) => c.tag === "th"));
      rows.push([texts, head]);
    }
    if (!rows.length) return "";
    if (!this.md) return rows.map(([r]) => r.join(" | ")).join("\n");
    const ncol = Math.max(...rows.map(([r]) => r.length));
    const esc = (s: string) => s.replaceAll("|", "\\|");
    let head: string[], body: [string[], boolean][];
    if (rows[0]![1]) [head, body] = [rows[0]![0], rows.slice(1)];
    else [head, body] = [Array<string>(ncol).fill(""), rows];
    const line = (cells: string[]) => "| " + [...cells, ...Array<string>(Math.max(0, ncol - cells.length)).fill("")].map(esc).join(" | ") + " |";
    return [line(head), "| " + Array<string>(ncol).fill("---").join(" | ") + " |", ...body.map(([r]) => line(r))].join("\n");
  }

  pre(el: Node): string {
    const code = el.children.find((c): c is Node => typeof c !== "string" && c.tag === "code");
    const src = code !== undefined ? code : el;
    const text = pystrip(src.text(), "\n");
    let lang = el.get("data-lang") || "";
    if (code !== undefined && !lang) lang = langOf(code);
    if (this.h.pre) this.h.pre(lang, text);
    if (!this.md) return text.split("\n").map((l) => "    " + l).join("\n");
    const fence = text.includes("```") ? "~~~~" : "```";
    return `${fence}${lang}\n${text}\n${fence}`;
  }

  inline(el: Node): string {
    const parts: string[] = [];
    for (const ch of el.children) {
      if (typeof ch === "string") parts.push(ch);
      else if (BLOCK.has(ch.tag)) parts.push(" " + this.block(ch).map((b) => b.replaceAll("\n", " ")).join(" ") + " ");
      else parts.push(this.inlineEl(ch));
    }
    return this.finish(parts.join(""));
  }

  inlineEl(el: Node): string {
    const t = el.tag, cls = el.classes;
    if (t === "br") return BR;
    if (t === "img") {
      const src = el.get("src") || "", alt = norm(el.get("alt") || "");
      if (this.h.img) {
        const r = this.h.img(src, alt);
        if (r !== null) return r;
      }
      if (!this.md) return alt ? ` [image: ${alt}] ` : " [image] ";
      return ` ![${alt.replaceAll("[", "(").replaceAll("]", ")")}](${src}) `;
    }
    if (t === "a") {
      const cite = el.find((e) => e.tag === "code" && e.classes.includes("cite"));
      if (cite !== null && this.h.cite) return this.h.cite(norm(cite.text()));
      const text = this.inline(el);
      if (cls.includes("ref") || el.attrs.has("data-ref")) {
        const rid = el.get("data-ref") || lstrip(el.get("href") || "", "#");
        if (this.h.ref) return this.h.ref(rid, text);
        return this.md && rid ? `[${text}](#${rid})` : text;
      }
      const href = el.get("href") || "";
      if (this.h.href) {
        const r = this.h.href(href, text);
        if (r !== null) return r;
      }
      if (!this.md || !href || !text) return text;
      return `[${text}](${href})`;
    }
    if (t === "code") {
      let s = el.text();
      if (cls.includes("cite") && this.h.cite) return this.h.cite(norm(s));
      s = norm(s);
      return this.md && s ? ticks(s) : s;
    }
    if (["strong", "b", "em", "i", "del", "s", "strike"].includes(t)) {
      const s = this.inline(el);
      if (!s) return "";
      const raw = el.text();
      const lead = isspace1(cpFirst(raw)) ? " " : "";
      const trail = isspace1(cpLast(raw)) ? " " : "";
      const mark = t === "strong" || t === "b" ? "**" : t === "em" || t === "i" ? "*" : "~~";
      return lead + (this.md ? `${mark}${s}${mark}` : s) + trail;
    }
    if (t === "input") return (el.get("type") || "").toLowerCase() === "checkbox" ? (el.attrs.has("checked") ? "[x] " : "[ ] ") : "";
    if (t === "span" && (cls.includes("chip") || cls.includes("pill"))) return ` [${this.inline(el)}] `;
    return this.inline(el);
  }
}

function pyRstrip(s: string): string {
  return s.replace(new RegExp(`[${PY_WS}]+$`), "");
}

/** int(s) (src/py/num: Unicode digits and whitespace, sign, underscores between digits); null on ValueError. */
export function pyInt(s: string): number | null {
  try {
    return Number(int(s));
  } catch (e) {
    if (e instanceof PyError && e.type === "ValueError") return null;
    throw e;
  }
}

function asNode(x: Node | string): Node {
  return typeof x === "string" ? parse(x) : x;
}

/** Block-level render of a node or an HTML string: one string per block. */
export function render(x: Node | string, mode = "md", hooks: Hooks = {}): string[] {
  return new Renderer(mode, hooks).blocks(asNode(x));
}

export function renderInline(x: Node | string, mode = "md", hooks: Hooks = {}): string {
  return new Renderer(mode, hooks).inline(asNode(x));
}

// ----------------------------------------------------------------------------- outline

export interface OutlineItem {
  level: number;
  kind: string;
  id: string | null;
  text: string;
  children?: OutlineItem[];
}

function ownerId(h: Node): string | null {
  let p = h.parent;
  while (p !== null && p.tag !== "#root") {
    if (p.get("id")) {
      let first: Node | null = null;
      for (const e of p.elements()) if (HEADINGS.has(e.tag) || e.tag === "summary") { first = e; break; }
      return first === h ? p.get("id")! : null;
    }
    p = p.parent;
  }
  return null;
}

/** Headings and id-bearing <details> summaries in document order, nested. A heading inside a fold nests under it. */
export function outline(node: Node): OutlineItem[] {
  const flat: OutlineItem[] = [];
  const state = { last: 1 };
  const walk = (n: Node, floor: number): void => {
    for (const ch of n.children) {
      if (typeof ch === "string") continue;
      if (HEADINGS.has(ch.tag)) {
        const lvl = Math.max(Number(ch.tag[1]), floor);
        state.last = lvl;
        flat.push({ level: lvl, kind: "heading", id: ch.get("id") || ownerId(ch), text: norm(ch.text()) });
      } else if (ch.tag === "details" && ch.get("id")) {
        const saved = state.last;
        const lvl = Math.max(saved + 1, floor);
        const summ = ch.children.find((c): c is Node => typeof c !== "string" && c.tag === "summary");
        flat.push({ level: lvl, kind: "fold", id: ch.get("id")!, text: summ !== undefined ? norm(summ.text()) : "" });
        state.last = lvl;
        walk(ch, lvl + 1);
        state.last = saved;
      } else walk(ch, floor);
    }
  };
  walk(node, 1);
  const top: OutlineItem[] = [], stack: OutlineItem[] = [];
  for (const item of flat) {
    item.children = [];
    while (stack.length && stack[stack.length - 1]!.level >= item.level) stack.pop();
    (stack.length ? stack[stack.length - 1]!.children! : top).push(item);
    stack.push(item);
  }
  return top;
}

export function countOutline(items: OutlineItem[]): number {
  return items.reduce((a, i) => a + 1 + countOutline(i.children ?? []), 0);
}

// ----------------------------------------------------------------------------- CLI

export function header(srcName: string, digest: string): string {
  return `stripped from ${srcName} \u00b7 sha256 ${digest.slice(0, 12)} \u00b7 ${VERSION} \u00b7 generated, do not edit`;
}

/** The body HTML of a stripped content node, as both scripts build it. */
export function bodyHtml(content: Node): string {
  const parts = ["body", "html", "#root"].includes(content.tag) ? content.children.map(toHtml).join("") : toHtml(content);
  return pystrip(parts);
}

export interface StripResult {
  title: string;
  in_bytes: number;
  html_bytes: number;
  txt_bytes: number;
  txt_tokens: number;
  headings: number;
  removed: Map<string, number>;
}

export function stripFile(path: string, outDir: string): StripResult {
  const raw = readFileSync(path);
  const digest = sha256(raw);
  const root = parse(decodeReplace(raw));
  const [content, title, removed] = strip(root);
  const head = header(basename(path), digest);
  const body = bodyHtml(content).replace(/\n{3,}/g, "\n\n");
  const plainHtml = `<!doctype html>\n<!-- ${head} -->\n<html lang="en"><head><meta charset="utf-8"><title>${escape(title)}</title></head>\n` +
    `<body>\n${body}\n</body></html>\n`;
  const txt = render(content, "txt").join("\n");
  const plainTxt = `> ${head}\n` + (title && !txt.startsWith("# ") ? `# ${title}\n` : "") + pyRstrip(txt) + "\n";
  const outl = outline(content);
  mkdirSync(outDir, { recursive: true });
  writeFileSync(pjoin(outDir, "plain.html"), plainHtml);
  writeFileSync(pjoin(outDir, "plain.txt"), plainTxt);
  writeFileSync(pjoin(outDir, "outline.json"), dumps({ _generated: head, title, headings: outl }, 1));
  return { title, in_bytes: raw.length, html_bytes: utf8Len(plainHtml), txt_bytes: utf8Len(plainTxt),
    txt_tokens: Math.floor((cpLen(plainTxt) + 3) / 4), headings: countOutline(outl), removed };
}

/** not s.strip() */
export function isBlankPy(s: string): boolean {
  return pystrip(s) === "";
}
/** s.strip() */
export function pyStripText(s: string): string {
  return pystrip(s);
}
