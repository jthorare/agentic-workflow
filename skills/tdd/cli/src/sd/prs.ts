// build_prs.py:12-83, the part the compiler uses: BLOCK, esc, front, inline, split_row, markdown, first_sentence, on
// the kernel. (load and build are not used: build_tdd.py has its own load_prs with sub-PRs and depends_on.)
import * as py from "../py";
import { esc, slot } from "./inline";

const re = py.re;
const { partition, split, splitlines, strip } = py.str;

export { esc };

/** BLOCK: a line that ends a paragraph (build_prs.py:12). */
export const BLOCK = "(#{1,6} |```|\\||\\s*([-*]|\\d+\\.) )";

const FRONT = re.compile(String.raw`\A---\n(.*?)\n---\n`, re.S);
const COMMENT_TAIL = re.compile(String.raw`\s+#.*$`);
const CODE = re.compile("`([^`]+)`");
const BOLD = re.compile(String.raw`\*\*([^*]+)\*\*`);
const ITALIC = re.compile(String.raw`(?<![\w*])\*([^*\n]+)\*(?![\w*])`);
const CODE_SLOT = re.compile("\x00(\\d+)\x00");
const LINK = re.compile(String.raw`\[([^\]]+)\]\((https?://[^)\s]+)\)`);
const HEADING = re.compile("#{1,6} ");
const TABLE_SEP = re.compile(String.raw`\|[\s:|-]+\|?\s*$`);
const ITEM = re.compile(String.raw`\s*([-*]|\d+\.) `);
const ORDERED = re.compile(String.raw`\s*\d+\.`);
const ITEM_PREFIX = re.compile(String.raw`^\s*([-*]|\d+\.) `);
const BLOCK_RE = re.compile(BLOCK);
const SENTENCE = re.compile(String.raw`.*?[.!?](?=\s|$)`);

/** front(md) -> [meta, body]: `key: value` lines between leading `---` fences (a trailing ` # comment` and surrounding
 * quotes dropped; a later key overwrites in place, as in a Python dict). */
export function front(md: string): [Map<string, string>, string] {
  const m = FRONT.match(md);
  const meta = new Map<string, string>();
  if (m) {
    for (const line of splitlines(m.group(1)!)) {
      const [k, , v] = partition(line, ":");
      if (strip(v)) meta.set(strip(k), strip(COMMENT_TAIL.sub("", strip(v)), "\"'"));
    }
    md = md.slice(m.end());
  }
  return [meta, md];
}

/** inline(s): escape, then code spans, **bold**, *italic*, and http(s) links. */
export function inline(s: string): string {
  s = esc(s, false);
  const codes: string[] = [];
  s = CODE.sub((m) => {
    codes.push(m.group(1)!);
    return `\x00${codes.length - 1}\x00`;
  }, s); // code spans first, so * inside them is never bold or italic
  s = BOLD.sub("<b>\\1</b>", s);
  s = ITALIC.sub("<i>\\1</i>", s);
  s = CODE_SLOT.sub((m) => `<code>${slot(codes, m.group(1)!)}</code>`, s);
  return LINK.sub('<a href="\\2">\\1</a>', s);
}

/** split_row(r) (build_prs's: no escaped pipes, unlike build_doc's). */
export function splitRow(r: string): string[] {
  return split(strip(strip(r), "|"), "|").map((c) => strip(c));
}

/** markdown(md): headings, tables, lists, code fences, paragraphs, inline code/bold/links: a PR deep dive's subset. */
export function markdown(md: string): string {
  const out: string[] = [];
  const lines = splitlines(md);
  let i = 0;
  while (i < lines.length) {
    const ln = lines[i]!;
    if (ln.startsWith("```")) {
      let j = i + 1;
      while (j < lines.length && !lines[j]!.startsWith("```")) j++;
      out.push("<pre><code>" + esc(lines.slice(i + 1, j).join("\n")) + "</code></pre>");
      i = j + 1;
    } else if (HEADING.match(ln)) {
      const n = ln.length - py.str.lstrip(ln, "#").length; // '#' is one UTF-16 unit
      out.push(`<h${n}>${inline(strip(ln.slice(n)))}</h${n}>`);
      i++;
    } else if (ln.startsWith("|") && i + 1 < lines.length && TABLE_SEP.match(lines[i + 1]!)) {
      const head = splitRow(ln);
      let j = i + 2;
      const rows: string[][] = [];
      while (j < lines.length && lines[j]!.startsWith("|")) {
        rows.push(splitRow(lines[j]!));
        j++;
      }
      out.push("<table><thead><tr>" + head.map((c) => `<th>${inline(c)}</th>`).join("") + "</tr></thead><tbody>"
        + rows.map((r) => "<tr>" + r.map((c) => `<td>${inline(c)}</td>`).join("") + "</tr>").join("") + "</tbody></table>");
      i = j;
    } else if (ITEM.match(ln)) {
      const tag = ORDERED.match(ln) ? "ol" : "ul";
      const items: string[] = [];
      while (i < lines.length && ITEM.match(lines[i]!)) {
        items.push(ITEM_PREFIX.sub("", lines[i]!));
        i++;
      }
      out.push(`<${tag}>` + items.map((x) => `<li>${inline(x)}</li>`).join("") + `</${tag}>`);
    } else if (strip(ln)) {
      const para = [ln];
      i++;
      while (i < lines.length && strip(lines[i]!) && !BLOCK_RE.match(lines[i]!)) {
        para.push(lines[i]!);
        i++;
      }
      out.push("<p>" + inline(para.join(" ")) + "</p>");
    } else i++;
  }
  return out.join("\n");
}

/** first_sentence(s): up to the first . ! or ? followed by whitespace or the end, at most 240 code points. */
export function firstSentence(s: string): string {
  const m = SENTENCE.match(s);
  return py.cpSlice(m ? m.group(0)! : s, 0, 240);
}
