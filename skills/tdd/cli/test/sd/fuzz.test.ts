// PR 10 acceptance, fuzz half: seeded markdown-shaped documents (parity/oracle.py sd_fuzz: block lines from the subset's
// grammar, inline runs from its syntax, Unicode, NULs and noise) through every text function of the D1 surface, against
// a live python3. Half the documents parse with build_doc's own MARK and half with build_tdd's. TDD_SD_FUZZ sets the
// count (default 10,000); a Python exception must be the same exception with the same text.
import { describe, expect, test } from "bun:test";
import * as py from "../../src/py";
import type { Pattern } from "../../src/py/re";
import { bd, bp } from "../../src/sd";
import { SKIP_REASON, attempt, hasPython, runOracle, same, show, type Outcome } from "./harness";

const COUNT = Number.parseInt(process.env.TDD_SD_FUZZ ?? "10000", 10);

interface Row {
  doc: string; line: string; mark: 0 | 1;
  [k: string]: unknown;
}

describe.skipIf(!hasPython)("fuzzed documents through src/sd and system-design", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  test(`${COUNT} documents, every text function, 0 diffs`, () => {
    const g = runOracle<{ marks: [string, number][]; rows: Row[] }>("sd_fuzz", ["--count", String(COUNT), "--seed", "1729"]);
    expect(g.rows).toHaveLength(COUNT);
    const marks: Pattern[] = g.marks.map(([p, f]) => py.re.compile(p, f));
    const prev = bd.MARK;
    const diffs: string[] = [];
    let compared = 0;
    const cmp = (k: number, name: string, want: unknown, got: Outcome) => {
      compared++;
      if (!same(want, got)) diffs.push(`#${k} ${name}: python ${show(want)} ts ${show(got)}`);
    };
    try {
      g.rows.forEach((row, k) => {
        bd.MARK = marks[row.mark]!;
        const { doc, line } = row;
        const lines = py.str.split(doc, "\n");
        const inl = bd.Inline();
        const rInl = attempt(() => inl(doc));
        cmp(k, "parse_blocks", row.parse_blocks, attempt(() => bd.parse_blocks(lines)));
        cmp(k, "is_block_start", row.is_block_start, attempt(() => bd.is_block_start(line)));
        cmp(k, "Inline", row.Inline, rInl);
        cmp(k, "Inline_line", row.Inline_line, attempt(() => inl(line, false)));
        cmp(k, "strip_tags", row.strip_tags, attempt(() => bd.strip_tags(doc)));
        if (row.strip_tags_inline !== null) {
          const want = row.strip_tags_inline;
          cmp(k, "strip_tags_inline", want, "r" in rInl ? attempt(() => bd.strip_tags((rInl.r as { v: string }).v)) : rInl);
        }
        cmp(k, "slug", row.slug, [attempt(() => bd.slug(line)), attempt(() => bd.slug(doc))] as unknown as Outcome);
        cmp(k, "split_marker", row.split_marker, attempt(() => bd.split_marker(line)));
        cmp(k, "page_title", row.page_title, attempt(() => bd.page_title(line)));
        cmp(k, "split_row", row.split_row, attempt(() => bd.split_row(line)));
        cmp(k, "esc", row.esc, [attempt(() => bd.esc(line)), attempt(() => bd.esc(line, false))] as unknown as Outcome);
        cmp(k, "bp.front", row["bp.front"], attempt(() => bp.front(doc)));
        cmp(k, "bp.inline", row["bp.inline"], attempt(() => bp.inline(doc)));
        cmp(k, "bp.markdown", row["bp.markdown"], attempt(() => bp.markdown(doc)));
        cmp(k, "bp.split_row", row["bp.split_row"], attempt(() => bp.split_row(line)));
        cmp(k, "bp.first_sentence", row["bp.first_sentence"], attempt(() => bp.first_sentence(doc)));
      });
    } finally {
      bd.MARK = prev;
    }
    const raised = g.rows.filter((r) => Object.values(r).some((v) => typeof v === "object" && v !== null && "x" in v)).length;
    console.log(`sd fuzz: ${COUNT} documents, ${compared} comparisons, ${diffs.length} diffs (${raised} documents where Python raised)`);
    expect(diffs.slice(0, 8)).toEqual([]);
    expect(diffs.length).toBe(0);
  }, 600_000);
});
