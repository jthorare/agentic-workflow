// Source parsing on top of system-design's block parser (build_tdd.py:62-120): trailing {attrs}, the ::: containers
// (nesting by fence length) and the "Title · one-liner" split.
import * as py from "../py";
import * as sd from "../sd";
import { MARK } from "../vocab";

const re = py.re;

/** An {attrs} group: "#id" -> id, "k: v" -> k (lowercased) = v, a bare word -> word (lowercased) = true. */
export type Attrs = Map<string, string | true>;

export interface BoxBlock {
  t: "box";
  kind: string;
  title: string;
  attrs: Attrs;
  blocks: Block[];
}
/** A block of build_doc's parser, or a ::: container. */
export type Block = sd.Block | BoxBlock;

export const BOX = re.compile(String.raw`^(:{3,})\s*([a-z][\w-]*)?\s*(.*?)\s*$`);
export const ATTRS = re.compile(String.raw`\s*\{([^{}]*)\}\s*$`);

/** 'Title {chip: new; #fe-ranked; wide}' -> ['Title', {chip: 'new', id: 'fe-ranked', wide: true}]. */
export function parse_attrs(text: string): [string, Attrs] {
  const attrs: Attrs = new Map();
  const m = ATTRS.search(text);
  if (!m) return [text, attrs];
  for (let part of py.str.split(m.group(1)!, ";")) {
    part = py.str.strip(part);
    if (!part) continue;
    if (part.startsWith("#")) attrs.set("id", py.str.strip(part.slice(1)));
    else if (part.includes(":")) {
      const [k, , v] = py.str.partition(part, ":");
      attrs.set(py.lower(py.str.strip(k)), py.str.strip(v));
    } else attrs.set(py.lower(part), true);
  }
  return [py.str.rstrip(text.slice(0, m.start())), attrs];
}

/** build_doc's block parser (with the tdd MARK) plus ::: containers, nested by fence length. */
export function parse_ext(lines: string[]): Block[] {
  const out: Block[] = [];
  const run: string[] = [];
  let i = 0;
  const n = lines.length;
  const flush = () => {
    if (run.length) {
      out.push(...sd.parseBlocks(run, MARK));
      run.length = 0;
    }
  };
  while (i < n) {
    const m = BOX.match(lines[i]!);
    if (m && m.group(2)) {
      flush();
      const fence = m.group(1)!;
      const kind = py.lower(m.group(2)!);
      const [title, attrs] = parse_attrs(m.group(3)!);
      let depth = 1;
      const body: string[] = [];
      i += 1;
      while (i < n) {
        const mm = BOX.match(lines[i]!);
        if (mm && mm.group(1)!.length === fence.length) {
          if (mm.group(2)) depth += 1;
          else {
            depth -= 1;
            if (depth === 0) break;
          }
        }
        body.push(lines[i]!);
        i += 1;
      }
      out.push({ t: "box", kind, title, attrs, blocks: parse_ext(body) });
      i += 1;
      continue;
    }
    run.push(lines[i]!);
    i += 1;
  }
  flush();
  return out;
}

/** 'Title · one-liner' -> ['Title', 'one-liner']; no separator -> [text.strip(), '']. */
export function split_title(text: string): [string, string] {
  const parts = re.split(String.raw`\s+·\s+`, text, 1);
  return parts.length === 2 ? [py.str.strip(parts[0]!), py.str.strip(parts[1]!)] : [py.str.strip(text), ""];
}
