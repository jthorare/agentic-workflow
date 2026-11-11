// build_doc.py:193-356, the markdown subset: MARK and the block patterns, is_block_start, split_row, indent_of,
// parse_list, parse_blocks, split_marker, slug. Line for line, every string and regex operation on the kernel (src/py),
// so the result equals the Python's for any input (test/sd replays a real compile and 10,000 fuzzed documents).
//
// MARK is a module global in Python that build_tdd.py replaces (it adds `legend` and `reading`); parse_blocks and
// is_block_start read it at call time. Here it is the live binding `MARK`, changed with setMark() (or `bd.MARK = p`
// on the facade in index.ts); every function that reads it also takes it as an optional last parameter, whose default
// is read at call time like the Python global.
import * as py from "../py";
import type { Pattern } from "../py/re";

const re = py.re;
const { lstrip, strip } = py.str;

/** build_doc's own MARK (build_doc.py:193). */
export const DEFAULT_MARK: Pattern = re.compile(String.raw`^\s*\\?\[(embedded content|diagram|video|media|image)\s*:\s*(.*?)\s*\\?\]\s*$`, re.I);
/** The marker-line pattern parse_blocks and is_block_start use: build_doc.MARK, read at call time. */
export let MARK: Pattern = DEFAULT_MARK;
/** `bd.MARK = p`: returns the pattern it replaced. */
export function setMark(p: Pattern): Pattern {
  const old = MARK;
  MARK = p;
  return old;
}

export const HEAD: Pattern = re.compile(String.raw`^(#{1,6})\s+(.*?)\s*#*\s*$`);
export const FENCE: Pattern = re.compile("^\\s*(`{3,}|~{3,})\\s*([\\w+-]*)");
export const LI: Pattern = re.compile(String.raw`^(\s*)([-*+]|\d+[.)])\s+(.*)$`);
export const HR: Pattern = re.compile(String.raw`^\s*([-*_])(\s*\1){2,}\s*$`);
export const TSEP: Pattern = re.compile(String.raw`^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$`);
export const COMMENT: Pattern = re.compile(String.raw`^\s*<!--.*-->\s*$`);

const CHECK = re.compile(String.raw`^\\?\[([ xX])\\?\]\s+(.*)$`);
const DIGITS = re.compile(String.raw`\d+`);
const QUOTE_PREFIX = re.compile(String.raw`^\s*>\s?`);
const ADMONITION = re.compile(String.raw`^\s*\\?\[!(\w+)\\?\]\s*$`);
const IMG = re.compile(String.raw`!\[([^\]]*)\]\(([^)\s]+)(?:\s+\"([^\"]*)\")?\)`);
const MARKER_SPLIT = re.compile(String.raw`\s*(.*?)\s*(?:·|\|)\s*(.*)$`);
const NOT_SLUG = re.compile("[^a-z0-9]+");

/** A Python dict lookup table (a Map, so "constructor" or "__proto__" is just a missing key). */
const QUOTE_KINDS: ReadonlyMap<string, string> = new Map([
  ["note", "note"], ["info", "note"], ["important", "note"], ["tip", "good"], ["good", "good"], ["success", "good"],
  ["warning", "warn"], ["warn", "warn"], ["caution", "danger"], ["danger", "danger"],
]);

// The blocks, with their keys in the order the Python dicts are built (content.json key order follows it).
export interface ListItem { text: string; check: boolean | null; children: ListBlock[] }
export interface ListBlock { t: "list"; ordered: boolean; start: number | bigint; items: ListItem[] }
export interface CodeBlock { t: "code"; lang: string; text: string }
export interface HeadBlock { t: "h"; level: number; text: string }
export interface MarkerBlock { t: "marker"; kind: string; arg: string }
export interface CommentBlock { t: "comment"; text: string }
export interface HrBlock { t: "hr" }
export interface QuoteBlock { t: "quote"; kind: string | null; blocks: Block[] }
export interface TableBlock { t: "table"; head: string[]; align: (string | null)[]; rows: string[][] }
export interface ImgBlock { t: "img"; alt: string; src: string; title: string }
export interface ParaBlock { t: "p"; text: string }
export type Block = CodeBlock | HeadBlock | MarkerBlock | CommentBlock | HrBlock | QuoteBlock | TableBlock | ListBlock | ImgBlock | ParaBlock;

/** `l.lstrip().startswith(prefix)` */
const lstripStarts = (l: string, prefix: string): boolean => lstrip(l).startsWith(prefix);

/** is_block_start(l) */
export function isBlockStart(l: string, mark: Pattern = MARK): boolean {
  return Boolean(HEAD.match(l) || FENCE.match(l) || LI.match(l) || HR.match(l) || mark.match(l) || COMMENT.match(l)
    || lstripStarts(l, ">") || lstripStarts(l, "|"));
}

/** split_row(r): a table row's cells; `\|` is a literal pipe. Walks UTF-16 units: only ASCII is compared, and a
 * surrogate pair copied one unit at a time is the same string. */
export function splitRow(r: string): string[] {
  r = strip(r);
  if (r.startsWith("|")) r = r.slice(1);
  if (r.endsWith("|") && !r.endsWith("\\|")) r = r.slice(0, -1);
  const cells: string[] = [];
  let cur = "";
  let i = 0;
  while (i < r.length) {
    if (r[i] === "\\" && i + 1 < r.length && r[i + 1] === "|") {
      cur += "|";
      i += 2;
      continue;
    }
    if (r[i] === "|") {
      cells.push(strip(cur));
      cur = "";
    } else cur += r[i];
    i++;
  }
  cells.push(strip(cur));
  return cells;
}

/** indent_of(l): leading spaces and tabs (ASCII, so UTF-16 lengths give the code point count). */
export function indentOf(l: string): number {
  return l.length - lstrip(l, " \t").length;
}

/** `m.group(2)[0].isdigit()` for an LI match: group 2 is `[-*+]` or `\d+[.)]`, and every `\d` is a digit. */
const ordinal = (marker: string): boolean => !"-*+".includes(marker[0]!);
/** len(m.group(1)) for LI: Python whitespace is all in the BMP, so this is the code point count too. */
const indentLen = (m: py.re.Match): number => py.cpLen(m.group(1)!);

/** parse_list(lines, i, base) -> (block, i) */
export function parseList(lines: readonly string[], i: number, base: number, mark: Pattern = MARK): [ListBlock, number] {
  const first = LI.match(lines[i]!)!;
  const ordered = ordinal(first.group(2)!);
  const start = ordered ? py.int(DIGITS.match(first.group(2)!)!.group(0)!) : 1;
  const items: ListItem[] = [];
  const n = lines.length;
  while (i < n) {
    const m = LI.match(lines[i]!);
    if (!m || indentLen(m) !== base || ordinal(m.group(2)!) !== ordered) break;
    let text = m.group(3)!;
    let check: boolean | null = null;
    const cm = CHECK.match(text);
    if (cm) {
      check = py.lower(cm.group(1)!) === "x";
      text = cm.group(2)!;
    }
    const item: ListItem = { text, check, children: [] };
    items.push(item);
    i++;
    while (i < n) {
      const l = lines[i]!;
      if (!strip(l)) {
        let j = i;
        while (j < n && !strip(lines[j]!)) j++;
        const mm = j < n ? LI.match(lines[j]!) : null;
        if (mm && indentLen(mm) >= base) {
          i = j;
          continue;
        }
        if (j < n && indentOf(lines[j]!) > base && !isBlockStart(strip(lines[j]!), mark)) {
          i = j;
          continue;
        }
        break;
      }
      const mm = LI.match(l);
      if (mm) {
        if (indentLen(mm) > base) {
          const [sub, next] = parseList(lines, i, indentLen(mm), mark);
          i = next;
          item.children.push(sub);
          continue;
        }
        break;
      }
      if (isBlockStart(strip(l), mark) && indentOf(l) <= base) break;
      item.text += " " + strip(l);
      i++;
    }
  }
  return [{ t: "list", ordered, start, items }, i];
}

/** parse_blocks(lines) */
export function parseBlocks(lines: readonly string[], mark: Pattern = MARK): Block[] {
  const blocks: Block[] = [];
  let i = 0;
  const n = lines.length;
  while (i < n) {
    const l = lines[i]!;
    if (!strip(l)) {
      i++;
      continue;
    }
    const fm = FENCE.match(l);
    if (fm) {
      const fence = fm.group(1)!;
      const lang = py.lower(fm.group(2)!);
      const buf: string[] = [];
      i++;
      while (i < n && !strip(lines[i]!).startsWith(fence)) {
        buf.push(lines[i]!);
        i++;
      }
      blocks.push({ t: "code", lang, text: buf.join("\n") });
      i++;
      continue;
    }
    const hm = HEAD.match(l);
    if (hm) {
      blocks.push({ t: "h", level: hm.group(1)!.length, text: hm.group(2)! });
      i++;
      continue;
    }
    const mk = mark.match(l);
    if (mk) {
      blocks.push({ t: "marker", kind: py.lower(mk.group(1)!), arg: mk.group(2)! });
      i++;
      continue;
    }
    if (COMMENT.match(l)) {
      blocks.push({ t: "comment", text: strip(l) });
      i++;
      continue;
    }
    if (HR.match(l)) {
      blocks.push({ t: "hr" });
      i++;
      continue;
    }
    if (lstripStarts(l, ">")) {
      let buf: string[] = [];
      while (i < n && lstripStarts(lines[i]!, ">")) {
        buf.push(QUOTE_PREFIX.sub("", lines[i]!));
        i++;
      }
      let kind: string | null = null;
      const am = buf.length ? ADMONITION.match(buf[0]!) : null;
      if (am) {
        kind = QUOTE_KINDS.get(py.lower(am.group(1)!)) ?? "note";
        buf = buf.slice(1);
      }
      blocks.push({ t: "quote", kind, blocks: parseBlocks(buf, mark) });
      continue;
    }
    if (lstripStarts(l, "|") && i + 1 < n && TSEP.match(lines[i + 1]!)) {
      const head = splitRow(l);
      const align: (string | null)[] = [];
      for (let c of splitRow(lines[i + 1]!)) {
        c = strip(c);
        align.push(c.startsWith(":") && c.endsWith(":") ? "center" : c.endsWith(":") ? "right" : null);
      }
      const rows: string[][] = [];
      i += 2;
      while (i < n && lstripStarts(lines[i]!, "|")) {
        rows.push(splitRow(lines[i]!));
        i++;
      }
      blocks.push({ t: "table", head, align, rows });
      continue;
    }
    const lm = LI.match(l);
    if (lm) {
      const [b, next] = parseList(lines, i, indentLen(lm), mark);
      i = next;
      blocks.push(b);
      continue;
    }
    const para = [strip(l)];
    i++;
    while (i < n && strip(lines[i]!) && !isBlockStart(lines[i]!, mark)) {
      para.push(strip(lines[i]!));
      i++;
    }
    const text = para.join(" ");
    const im = IMG.fullmatch(text);
    if (im) blocks.push({ t: "img", alt: im.group(1)!, src: im.group(2)!, title: im.group(3) || "" });
    else blocks.push({ t: "p", text });
  }
  return blocks;
}

/** split_marker(arg): 'key · caption' (or 'key | caption') -> [key, caption]. */
export function splitMarker(arg: string): [string, string] {
  const m = MARKER_SPLIT.match(arg);
  return m ? [strip(m.group(1)!), strip(m.group(2)!)] : [strip(arg), ""];
}

/** slug(s): lower-case ASCII words joined by "-", at most 48 code points, "section" when nothing is left. */
export function slug(s: string): string {
  return strip(py.cpSlice(strip(NOT_SLUG.sub("-", py.lower(s)), "-"), 0, 48), "-") || "section";
}
