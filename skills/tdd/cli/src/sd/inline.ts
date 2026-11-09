// build_doc.py:27 and 360-399: esc, the Inline renderer and strip_tags, on the kernel (src/py).
//
// Python's Inline is a class whose instances are called: `inl = bd.Inline(); inl(s, links=False)`. Inline() here
// returns the same thing, a function with an `anchor_map` property (a Python dict: a Map). build_tdd never sets
// anchor_map, so `#` links pass through unchanged; the lookup is kept for any other caller.
import * as py from "../py";
import { PyError } from "../py/errors";
import type { Match } from "../py/re";

const re = py.re;

/** esc = html.escape (build_doc.py:27; build_prs.py:13 is the same function). */
export const esc = (s: string, quote = true): string => py.html.escape(s, quote);

const CODE = re.compile(String.raw`(` + "`" + String.raw`+)(.+?)\1`);
const ESCAPE = re.compile(String.raw`\\([\\` + "`" + String.raw`*_{}\[\]()#+\-.!|<>~&:])`);
const IMG = re.compile(String.raw`!\[([^\]]*)\]\(([^)\s]+)\)`);
const LINK = re.compile(String.raw`\[([^\]\n]+)\]\(([^)\s]+)(?:\s+&quot;[^&]*&quot;)?\)`);
const EXTERNAL = re.compile("https?://");
const AUTOLINK = re.compile(String.raw`&lt;(https?://[^\s&]+)&gt;`);
const STRONG = re.compile(String.raw`\*\*(?=\S)(.+?)(?<=\S)\*\*`);
const STRONG_U = re.compile(String.raw`(?<![\w])__(?=\S)(.+?)(?<=\S)__(?![\w])`);
const EM = re.compile(String.raw`(?<![\w*])\*(?=[^\s*])([^*\n]+?)(?<=\S)\*(?![\w*])`);
const EM_U = re.compile(String.raw`(?<![\w])_(?=\S)([^_\n]+?)(?<=\S)_(?![\w])`);
const DEL = re.compile(String.raw`~~(?=\S)(.+?)(?<=\S)~~`);
const ESC_SLOT = re.compile("\x00E(\\d+)\x00");
const CODE_SLOT = re.compile("\x00C(\\d+)\x00");
const TAG = re.compile("<[^>]+>");

/** `xs[int(digits)]`: Python's int() of the captured digits (any Unicode decimal digit) and its IndexError. */
export function slot<T>(xs: readonly T[], digits: string): T {
  const k = py.int(digits);
  if (typeof k === "bigint" && k > 0x7fffffffffffffffn) throw new PyError("IndexError", "cannot fit 'int' into an index-sized integer");
  if (typeof k === "bigint" || k >= xs.length) throw new PyError("IndexError", "list index out of range");
  return xs[k]!;
}

/** Inline.__call__(s, links=True) with a given anchor_map. */
export function renderInline(s: string, links = true, anchorMap: ReadonlyMap<string, string> = new Map()): string {
  const codes: string[] = [];
  const escs: string[] = [];
  s = CODE.sub((m) => {
    codes.push(m.group(2)!);
    return `\x00C${codes.length - 1}\x00`;
  }, s);
  s = ESCAPE.sub((m) => {
    escs.push(m.group(1)!);
    return `\x00E${escs.length - 1}\x00`;
  }, s);
  s = esc(s, false);
  s = IMG.sub((m) => `<img src="${py.replace(m.group(2)!, '"', "&quot;")}" alt="${m.group(1)!}" loading="lazy">`, s);
  s = LINK.sub((m: Match) => {
    const text = m.group(1)!;
    let href = m.group(2)!;
    if (!links) return text;
    if (href.startsWith("#")) href = "#" + (anchorMap.get(href.slice(1)) ?? href.slice(1));
    const attrs = EXTERNAL.match(href) ? ' target="_blank" rel="noopener"' : "";
    return `<a href="${py.replace(href, '"', "&quot;")}"${attrs}>${text}</a>`;
  }, s);
  s = AUTOLINK.sub(links ? (m) => `<a href="${m.group(1)!}" target="_blank" rel="noopener">${m.group(1)!}</a>` : "\\1", s);
  s = STRONG.sub("<strong>\\1</strong>", s);
  s = STRONG_U.sub("<strong>\\1</strong>", s);
  s = EM.sub("<em>\\1</em>", s);
  s = EM_U.sub("<em>\\1</em>", s);
  s = DEL.sub("<del>\\1</del>", s);
  s = ESC_SLOT.sub((m) => esc(slot(escs, m.group(1)!), false), s);
  s = CODE_SLOT.sub((m) => "<code>" + esc(py.str.strip(slot(codes, m.group(1)!)), false) + "</code>", s);
  return s;
}

/** An Inline instance: call it as `inl(s, links)`. */
export interface Inline {
  (s: string, links?: boolean): string;
  /** self.anchor_map: section id remaps for `#` links (empty unless a caller fills it). */
  anchor_map: Map<string, string>;
}

/** bd.Inline(): a renderer with an empty anchor_map. */
export function Inline(): Inline {
  const f = ((s: string, links = true) => renderInline(s, links, f.anchor_map)) as Inline;
  f.anchor_map = new Map();
  return f;
}

/** strip_tags(h): the text of an HTML fragment, entities decoded, Python-stripped. */
export function stripTags(h: string): string {
  return py.str.strip(py.html.unescape(TAG.sub("", h)));
}
