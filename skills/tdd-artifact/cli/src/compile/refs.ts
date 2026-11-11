// Reference linking (build_tdd.py:124-197): tokens such as D7 or PR 7 become links to their definition (or to a meta.json
// refs family), glossary terms become abbreviation links, never inside links, code, headings or other SKIP_TAGS.
import * as py from "../py";
import type { Match } from "../py/re";
import { SKIP_TAGS, TOKEN, token_id } from "../vocab";

const re = py.re;

export const TAG = re.compile(String.raw`<(/?)([a-zA-Z][\w-]*)([^>]*)>`);
export const ABBR_TERM = re.compile(String.raw`^[A-Z][A-Z0-9][A-Z0-9.-]*$`);
const SKIP_SPAN = re.compile(String.raw`class="(id|ref-missing|fig-title)\b`);
const VOID = new Set(["img", "br", "hr", "input", "source"]);

export interface Link {
  token: string;
  family: string;
  section: string | null;
  anchor: string | null;
  linked: boolean;
}

/** The link counters link_tree threads through a compile: linked count, unlinked tokens, the current section, links. */
export interface Stats {
  linked: number;
  unlinked: string[];
  section?: string | null;
  links?: Link[];
}

const glossaryCache = new Map<string, py.re.Pattern | null>();

function glossaryPattern(glossary: Map<string, string>): py.re.Pattern | null {
  // sorted(glossary, key=len, reverse=True): longest first, ties in insertion order (sort stability under reverse)
  const gl = [...glossary.keys()].map((g, k) => [g, py.cpLen(g), k] as const).sort((a, b) => b[1] - a[1] || a[2] - b[2]).map((x) => x[0]);
  if (!gl.length) return null;
  const src = String.raw`(?<![\w-])(` + gl.map((g) => re.escape(g)).join("|") + String.raw`)(?![\w-])`;
  let p = glossaryCache.get(src);
  if (p === undefined) {
    p = re.compile(src);
    glossaryCache.set(src, p);
  }
  return p;
}

/** Link reference tokens and glossary abbreviations in an inline-HTML string, outside links, code and headings. */
export function link_refs(html_body: string, ids: Set<string>, families: Map<string, unknown>, glossary: Map<string, string>, stats: Stats, seen: Set<string> | null = null): string {
  const out: string[] = [];
  let pos = 0;
  const stack: string[] = [];
  const gl_re = glossaryPattern(glossary);
  const section = () => (stats.section === undefined ? null : stats.section);
  const tok = (m: Match): string => {
    const raw = m.group(1)!;
    const tid = token_id(raw);
    const fam = re.match("[A-Z]+", raw)!.group(0)!;
    const links = (stats.links ??= []);
    if (ids.has(tid)) {
      stats.linked += 1;
      links.push({ token: raw, family: fam, section: section(), anchor: tid, linked: true });
      return `<a class="ref" href="#${tid}" data-ref="${tid}">${raw}</a>`;
    }
    if (families.has(fam)) {
      const href = families.get(fam) as string;
      stats.linked += 1;
      links.push({ token: raw, family: fam, section: section(), anchor: py.str.lstrip(href, "#"), linked: true });
      return `<a class="ref ref-family" href="${href}" data-ref="${py.str.lstrip(href, "#")}">${raw}</a>`;
    }
    stats.unlinked.push(raw);
    links.push({ token: raw, family: fam, section: section(), anchor: null, linked: false });
    return `<span class="ref-missing" data-ref="${tid}">${raw}</span>`;
  };
  const ab = (m: Match): string => {
    const g = m.group(1)!;
    if (seen !== null && !ABBR_TERM.match(g)) {
      if (seen.has(g)) return g;
      seen.add(g);
    }
    stats.linked += 1;
    (stats.links ??= []).push({ token: g, family: "glossary", section: section(), anchor: glossary.get(g)!, linked: true });
    return `<a class="ref ref-abbr" href="#${glossary.get(g)}" data-ref="${glossary.get(g)}">${g}</a>`;
  };
  const repl_text = (text: string): string => {
    text = TOKEN.sub(tok, text);
    if (gl_re) {
      const parts = re.split("(<[^>]+>)", text) as string[];
      let depth = 0;
      parts.forEach((p, k) => {
        if (p.startsWith("<")) depth += p.startsWith("<a") ? 1 : p.startsWith("</a") ? -1 : 0;
        else if (depth === 0 && p) parts[k] = gl_re.sub(ab, p);
      });
      text = parts.join("");
    }
    return text;
  };
  for (const m of TAG.finditer(html_body)) {
    const text = html_body.slice(pos, m.start());
    out.push(text && !stack.length ? repl_text(text) : text);
    out.push(m.group(0)!);
    pos = m.end();
    const closing = m.group(1) === "/";
    const name = py.lower(m.group(2)!);
    const attrs = m.group(3)!;
    if (py.str.rstrip(attrs).endsWith("/") || VOID.has(name)) continue;
    if (stack.length) {
      // inside a skipped element: track every tag so nesting ends exactly at its own close
      if (closing) {
        if (stack.includes(name)) while (stack.length && stack.pop() !== name);
      } else stack.push(name);
      continue;
    }
    const skip = SKIP_TAGS.has(name) || (name === "span" && Boolean(SKIP_SPAN.search(attrs)));
    if (skip && !closing) stack.push(name);
  }
  const tail = html_body.slice(pos);
  out.push(tail && !stack.length ? repl_text(tail) : tail);
  return out.join("");
}
