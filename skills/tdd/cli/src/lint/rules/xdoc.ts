// XDOC: a glossary term defined differently in a sibling packet of the same repo slug (D-numbers are per doc and are
// not compared) (lint_packet.Lint.r_xdoc).
import { Effect } from "effect";
import * as py from "../../py";
import { Packet, type LintPacket } from "../packet";
import { norm, outside_fences, repr } from "../pyops";

const re = py.re;
const { join, dirname } = py.path;
const FENCE = re.compile(String.raw`^(:{3,})\s*([a-z][\w-]*)?`);
const ITEM = re.compile(String.raw`^\s*[-*]\s+\*\*([^*]+)\*\*\s*[:—–-]?\s*(.*)$`);
const MARKS = re.compile("[*_`]");

type Entry = readonly [string, string, string, number];

/** {term lower: (term, definition, file, line)} from every ::: glossary block in a packet's root markdown. */
export function glossary_of(p: LintPacket, d: string): Map<string, Entry> {
  const out = new Map<string, Entry>();
  for (const fn of py.sorted(p.fs.listdir(d))) {
    const path = join(d, fn);
    if (!fn.endsWith(".md") || !p.fs.isfile(path)) continue;
    let depth: number | null = null;
    for (const [ln, line] of outside_fences(p.lines(path))) {
      const m = FENCE.match(line);
      if (m) {
        if (m.group(2) === "glossary" && depth === null) depth = m.group(1)!.length;
        else if (depth !== null && !m.group(2) && m.group(1)!.length === depth) depth = null;
        continue;
      }
      if (depth === null) continue;
      const g = ITEM.match(line);
      if (g) {
        const term = py.str.strip(g.group(1)!);
        const key = py.lower(term);
        if (!out.has(key)) out.set(key, [term, py.str.strip(g.group(2)!), path, ln]);
      }
    }
  }
  return out;
}

const clean = (s: string): string => norm(py.str.rstrip(re.sub(MARKS, "", s), "."));

export const xdoc = Effect.gen(function* () {
  const p = yield* Packet;
  const parent = dirname(p.root);
  const sibs = py.sorted(p.fs.listdir(parent)).map((d) => join(parent, d)).filter((d) => p.fs.isdir(d) && d !== p.root);
  const mine = glossary_of(p, p.root);
  if (!mine.size) {
    p.note(`XDOC: no ::: glossary in this packet; ${sibs.length} sibling packets under ${p.rel(parent)} not compared`);
    return;
  }
  for (const sd of sibs) {
    const theirs = glossary_of(p, sd);
    for (const [k, [term, d1, f1, l1]] of mine) {
      const t = theirs.get(k);
      if (t && clean(t[1]) !== clean(d1)) {
        const [, d2, f2, l2] = t;
        p.add("XDOC", "glossary-conflict", "warn", `glossary term '${term}' is defined differently in ${p.rel(f2)}:${l2} (${repr(py.cpSlice(d2, 0, 60))}) than here ` +
          `(${repr(py.cpSlice(d1, 0, 60))}); make one definition and use it in both`, p.rel(f1), l1);
      }
    }
  }
  p.note(`XDOC: compared ${mine.size} glossary terms with ${sibs.length} sibling packets; D-numbers are per doc and are not compared`);
});
