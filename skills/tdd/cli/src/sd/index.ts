// The system-design surface the tdd compiler uses (research/bun-port-2026.md section 1.2, D1 option A), in plain
// synchronous TypeScript on the kernel: 14 names from build_doc.py and 4 from build_prs.py, pinned by ../../UPSTREAM.json.
//
// Two ways in:
// - the camelCase functions (parseBlocks, Inline, stripTags, imgSize, Assets, ..., and `prs.markdown` etc.);
// - `bd` and `bp`, objects with the Python names, so a port of build_tdd.py reads like the original:
//   `bd.MARK = TDD_MARK; bd.parse_blocks(run); const inl = bd.Inline(); inl(s, false); bp.markdown(body)`.
//   Assigning bd.MARK sets the pattern parse_blocks and is_block_start read at call time, as in Python.
import type { Pattern } from "../py/re";
import { Assets, FILE_LIMIT, imgSize, pngSize, publishedFor, svgSize } from "./assets";
import * as blocks from "./blocks";
import { COMMENT, FENCE, HEAD, HR, LI, TSEP, indentOf, isBlockStart, parseBlocks, parseList, slug, splitMarker, splitRow } from "./blocks";
import { Inline, esc, stripTags } from "./inline";
import { GENERIC_TAILS, STATUS_WORDS, loadJson, pageTitle } from "./page";
import * as prs from "./prs";

export * from "./assets";
export * from "./blocks";
export * from "./errors";
export * from "./inline";
export * from "./page";
export * as prs from "./prs";

/** build_doc, by its Python names. */
export const bd = {
  /** The marker pattern; assigning it changes what parse_blocks and is_block_start match from then on. */
  get MARK(): Pattern {
    return blocks.MARK;
  },
  set MARK(p: Pattern) {
    blocks.setMark(p);
  },
  HEAD, FENCE, LI, HR, TSEP, COMMENT,
  is_block_start: (l: string): boolean => isBlockStart(l),
  split_row: splitRow,
  indent_of: indentOf,
  parse_list: (lines: readonly string[], i: number, base: number) => parseList(lines, i, base),
  parse_blocks: (lines: readonly string[]) => parseBlocks(lines),
  split_marker: splitMarker,
  slug,
  Inline,
  strip_tags: stripTags,
  svg_size: svgSize,
  png_size: pngSize,
  img_size: imgSize,
  Assets,
  published_for: publishedFor,
  STATUS_WORDS,
  GENERIC_TAILS,
  page_title: pageTitle,
  load_json: loadJson,
  esc,
  FILE_LIMIT,
};

/** build_prs, by its Python names (the part the compiler uses). */
export const bp = {
  BLOCK: prs.BLOCK,
  esc: prs.esc,
  front: prs.front,
  inline: prs.inline,
  split_row: prs.splitRow,
  markdown: prs.markdown,
  first_sentence: prs.firstSentence,
};
