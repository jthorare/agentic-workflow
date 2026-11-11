// write_agents.py's PageLane: the reduced lane from a built page alone (no content.json).
import { readFileSync } from "node:fs";
import * as re from "../py/re";
import * as sp from "./strip";
import { type Py, PY_WS, abspath, cpSlice, decodeReplace, dumps, lstrip, pjoin, sha256 } from "./_py";
import { BUDGET, GENERATOR, PACK, type FileVal, clip, codeExt, comment, decFileName, dumpLines, front, pad2, rel, slugify, tok, writeTokens } from "./lane";

interface PSlice { path: string; title: string; tokens: number; anchors: string[] }

export class PageLane {
  build: string;
  out: string;
  budget: number;
  page: string;
  pageSha: string;
  root: sp.Node;
  head: string;
  files = new Map<string, FileVal>();
  warn: string[] = [];
  citations: Py[] = [];
  slices: PSlice[] = [];
  imageRows: Map<string, Py>[] = [];
  tables: Py[] = [];
  overBudget: string[] = [];
  largest: [string | null, number] = [null, 0];
  counts = { sql: 0, code: 0, decisions: 0 };

  constructor(build: string, out: string | null = null, budget = BUDGET) {
    this.build = abspath(build);
    this.out = abspath(out || pjoin(this.build, "agents"));
    this.budget = budget;
    this.page = pjoin(this.build, "tdd.html");
    const raw = readFileSync(this.page);
    this.pageSha = sha256(raw).slice(0, 12);
    this.root = sp.parse(decodeReplace(raw));
    this.head = `agents lane \u00b7 from tdd.html (no content.json) \u00b7 content page:${this.pageSha} \u00b7 ${GENERATOR} \u00b7 generated, do not edit`;
  }

  emit(path: string, text: string): void {
    this.files.set(path, text.endsWith("\n") ? text : text + "\n");
  }

  run(): this {
    const decisions: { id: string; anchor: string; question: string; status: string; state: string | null; body: sp.Child[] }[] = [];
    for (const e of this.root.elements()) {
      if (e.classes.includes("dec") && e.get("id")) {
        const first = <T>(it: Iterable<sp.Node>, f: (x: sp.Node) => T | undefined, d: T): T => {
          for (const x of it) { const v = f(x); if (v !== undefined) return v; }
          return d;
        };
        const did = first(e.elements(), (x) => (x.classes.includes("id") ? sp.norm(x.text()) : undefined), e.get("id")!.toUpperCase());
        const q = first(e.elements(), (x) => (sp.HEADINGS.has(x.tag) ? sp.norm(x.text()) : undefined), "");
        const st = first(e.elements(), (x) => (x.classes.some((c) => c === "chip" || c === "pill") ? sp.norm(x.text()) : undefined), "");
        const state = e.classes.find((c) => c === "done" || c === "open" || c === "held") ?? null;
        const body = e.children.filter((ch) => !(typeof ch !== "string" && ch.classes.includes("head")));
        decisions.push({ id: did, anchor: e.get("id")!, question: q, status: st, state, body });
      }
    }
    const imgs: Map<string, Py>[] = [];
    for (const e of this.root.elements()) {
      if (e.tag === "img" && e.get("src") && !(e.get("src") ?? "").startsWith("data:")) {
        let fig = e.parent;
        while (fig !== null && fig.tag !== "figure" && fig.tag !== "#root") fig = fig.parent;
        let cap = "";
        if (fig !== null && fig.tag === "figure") {
          let fc: sp.Node | null = null;
          for (const x of fig.elements()) if (x.tag === "figcaption") { fc = x; break; }
          cap = fc !== null ? sp.norm(fc.text()) : "";
        }
        imgs.push(new Map<string, Py>([["src", e.get("src")], ["alt", e.get("alt") || ""], ["caption", cap],
          ["kind", fig !== null && fig.classes.includes("diagram") ? "diagram" : "image"],
          ["width", e.get("width") ?? null], ["height", e.get("height") ?? null],
          ["anchor", fig !== null && fig.tag === "figure" ? fig.get("id") ?? null : null]]));
      }
    }
    const [content, title, removed] = sp.strip(this.root);
    const codes: { lang: string; text: string; guessed: boolean }[] = [];
    const preHook = (lang: string, text: string) => { codes.push({ lang: lang || PageLane.guess(text), text, guessed: !lang }); };
    const mdBlocks = sp.render(content, "md", { pre: preHook });
    let sections = [...content.elements()].filter((e) => e.tag === "section" && e.get("id"));
    sections = sections.filter((s) => ![...sp.ancestors(s)].some((a) => a.tag === "section"));
    if (!sections.length) {
      this.warn.push("no <section id> elements: the page went into one slice");
      sections = [content];
    }
    sections.forEach((s, i0) => {
      const i = i0 + 1;
      const sid = s.get("id") || "page";
      let h: sp.Node | null = null;
      for (const x of s.elements()) if (sp.HEADINGS.has(x.tag)) { h = x; break; }
      const name = h !== null ? sp.norm(h.text()) : sid;
      const blocks: string[] = [];
      for (const ch of s.children) {
        if (typeof ch === "string") {
          if (sp.isBlankPy(ch)) continue;
          blocks.push(sp.norm(ch));
          continue;
        }
        const imgHook = (src: string, alt: string) => ` ![${alt}](${rel(pjoin(this.build, src), pjoin(this.out, "sections"))}) `;
        blocks.push(...sp.render(PageLane.wrap(ch), "md", { img: imgHook }));
      }
      if (blocks.length && s !== content) blocks[0] = blocks[0] + (!blocks[0]!.split("\n")[0]!.includes("{#") ? ` {#${sid}}` : "");
      const parts: string[][] = [];
      let cur: string[] = [], size = 300;
      for (const b of blocks) {
        if (cur.length && size + tok(b) > this.budget * PACK) {
          parts.push(cur);
          cur = [];
          size = 300;
        }
        cur.push(b);
        size += tok(b);
      }
      parts.push(cur);
      parts.forEach((pb, k0) => {
        const k = k0 + 1;
        const path = k === 1 ? `sections/${pad2(i)}-${slugify(sid)}.md` : `sections/${pad2(i)}-${slugify(sid)}-part-${k}.md`;
        const body = pb.join("\n\n");
        const anchors = [...body.matchAll(ANCHOR_RE)].map((m) => m[1]!);
        const ftitle = name + (k > 1 ? ` (part ${k})` : "");
        const tokens = tok(body) + 60;
        this.emit(path, front([["id", sid], ["title", ftitle], ["part", k], ["parts", parts.length], ["tokens", tokens], ["anchors", anchors], ["generated", this.head]]) + "\n\n" + body);
        this.slices.push({ path, title: ftitle, tokens, anchors });
      });
    });
    let nSql = 0, nCode = 0;
    for (const b of codes) {
      const ext = codeExt(b.lang);
      let f: string;
      if (ext === "sql") {
        nSql += 1;
        f = `sql/${pad2(nSql)}-page.sql`;
      } else {
        nCode += 1;
        f = `code/${pad2(nCode)}-page.${ext}`;
      }
      this.emit(f, comment(ext, this.head) + "\n" + comment(ext, `block from tdd.html \u00b7 lang ${b.lang}` + (b.guessed ? " (guessed)" : "")) + "\n\n" + b.text);
    }
    this.emit("tdd.md", `> ${this.head}\n\n` + mdBlocks.join("\n\n"));
    this.emit("tdd.txt", `> ${this.head}\n` + sp.render(content, "txt").join("\n"));
    imgs.forEach((im, i0) => {
      const row = new Map<string, Py>([["id", `IMG.${pad2(i0 + 1)}`], ["path", rel(pjoin(this.build, im.get("src")), this.out)]]);
      for (const [k, v] of im) if (k !== "src") row.set(k, v);
      row.set("source", im.get("src"));
      this.imageRows.push(row);
    });
    if (this.imageRows.length) this.emit("images/index.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["copied", false], ["images", this.imageRows]])));
    for (const d of decisions) {
      const fm: [string, Py][] = [["id", d.id], ["anchor", d.anchor], ["status", d.status || "not stated"], ["state", d.state], ["question", d.question], ["generated", this.head]];
      const w = new sp.Node("div");
      w.children = d.body;
      this.emit("decisions/" + decFileName(d.id), front(fm) + "\n\n" + `# ${d.id} \u00b7 ${d.question}\n\n**Status:** ${d.status || "not stated"} \u00b7 state: ${d.state ?? "None"}\n\n` + sp.render(w, "md").join("\n\n"));
    }
    const anchors: Map<string, Py>[] = [];
    for (const e of content.elements()) {
      const id = e.get("id");
      if (id) {
        const f = this.slices.find((s) => s.anchors.includes(id))?.path ?? null;
        anchors.push(new Map<string, Py>([["id", id], ["tag", e.tag], ["text", clip(sp.norm(e.text()), 120)], ["file", f]]));
      }
    }
    this.emit("refs.json", dumpLines(new Map<string, Py>([["_generated", this.head], ["anchors", anchors]])));
    const body = sp.pyStripText(content.children.map(sp.toHtml).join(""));
    this.emit("page/plain.html", `<!doctype html>\n<!-- ${this.head} -->\n<html lang="en"><head><meta charset="utf-8"><title>${title}</title></head>\n<body>\n${body}\n</body></html>`);
    this.emit("page/outline.json", dumps({ _generated: this.head, title, removed, headings: sp.outline(content) }, 1));
    const secs = this.slices.map((s) => `- [${s.title}](${s.path}) ~${s.tokens} tok`).join("\n");
    this.emit("README.md", `> ${this.head}\n\n# ${title} \u00b7 agents lane (from the page)\n\nBuilt from tdd.html because content.json was missing: ` +
      "no decisions index beyond the page's decision cards, no citation index, no map. Rebuild with build_tdd.py for the full lane.\n\n" +
      "Eval scores, when the eval has run, are in `../eval/output.*.json`. If you change the packet, rebuild, rerun the eval " +
      "(`eval_packet.py`) and leave your own scores in your return; never pass `--push`.\n\n" +
      `## Sections\n${secs}\n\n## Files\n- [tdd.md](tdd.md), [tdd.txt](tdd.txt): the whole page\n- [page/plain.html](page/plain.html), [page/outline.json](page/outline.json)\n` +
      `- sql/ (${nSql}), code/ (${nCode}), decisions/ (${decisions.length}), [images/index.json](images/index.json) (${this.imageRows.length})\n- [refs.json](refs.json): every id and its slice\n`);
    this.emit("llms.txt", `# ${title}\n\n> Agents lane built from the page alone. ${this.head}\n\n## Sections\n` +
      this.slices.map((s) => `- [${s.title}](${s.path}): ~${s.tokens} tokens`).join("\n") + "\n\n## Optional\n- [tdd.md](tdd.md): the whole page\n");
    writeTokens(this);
    this.counts = { sql: nSql, code: nCode, decisions: decisions.length };
    return this;
  }

  static wrap(el: sp.Node): sp.Node {
    const w = new sp.Node("div");
    w.children = [el];
    return w;
  }

  static guess(text: string): string {
    const head = cpSlice(lstrip(text), 0, 200);
    if (re.match(String.raw`(?i)(create|alter|select|insert|update|delete|with|drop|begin)\b`, head)) return "sql";
    if (/^(\/\/|export |import |interface |type |const |function )/.test(head)) return "ts";
    return "";
  }
}

const ANCHOR_RE = new RegExp(`\\{#([^}${PY_WS}]+)\\}`, "g");
