// Unicode membership as CPython 3.13 sees it, from the generated tables in ucd.ts (never the engine's \p{...}, whose
// Unicode version is newer). Each table is a sorted Uint32Array of inclusive [start, end] pairs.
import { D_RUNS, LINEBREAK_RUNS, PRINTABLE_RUNS, S_RUNS, W_RUNS } from "./ucd";

function parseRuns(s: string): Uint32Array {
  const parts = s.split(",");
  const out = new Uint32Array(parts.length * 2);
  parts.forEach((p, i) => {
    const dash = p.indexOf("-");
    const a = parseInt(dash < 0 ? p : p.slice(0, dash), 16);
    const b = dash < 0 ? a : parseInt(p.slice(dash + 1), 16);
    out[2 * i] = a;
    out[2 * i + 1] = b;
  });
  return out;
}

/** Index of the run containing cp, or -1. */
function findRun(runs: Uint32Array, cp: number): number {
  let lo = 0;
  let hi = runs.length / 2 - 1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (cp < runs[2 * mid]!) hi = mid - 1;
    else if (cp > runs[2 * mid + 1]!) lo = mid + 1;
    else return mid;
  }
  return -1;
}

function classBody(runs: Uint32Array): string {
  // \uXXXX where it can be (so a class of BMP members, such as \s, also works in a regex without the u flag)
  const hex = (c: number) => (c <= 0xffff ? `\\u${c.toString(16).padStart(4, "0")}` : `\\u{${c.toString(16)}}`);
  let out = "";
  for (let i = 0; i < runs.length; i += 2) {
    const a = runs[i]!;
    const b = runs[i + 1]!;
    out += a === b ? hex(a) : `${hex(a)}-${hex(b)}`;
  }
  return out;
}

export const W = parseRuns(W_RUNS);
export const D = parseRuns(D_RUNS);
export const S = parseRuns(S_RUNS);
export const PRINTABLE = parseRuns(PRINTABLE_RUNS);
export const LINEBREAK = parseRuns(LINEBREAK_RUNS);

export const isWordCp = (cp: number): boolean => findRun(W, cp) >= 0;
export const isDecimalCp = (cp: number): boolean => findRun(D, cp) >= 0;
export const isPrintableCp = (cp: number): boolean => findRun(PRINTABLE, cp) >= 0;
export const isLinebreakCp = (cp: number): boolean => findRun(LINEBREAK, cp) >= 0;

const SPACE_BMP = (() => {
  const t = new Uint8Array(0x3001);
  for (let i = 0; i < S.length; i += 2) for (let c = S[i]!; c <= S[i + 1]!; c++) t[c] = 1;
  return t;
})();
/** str.isspace() for one code point (also re's \s). Every member is in the BMP, at most U+3000. */
export const isSpaceCp = (cp: number): boolean => cp <= 0x3000 && SPACE_BMP[cp] === 1;
/** The same test on a UTF-16 code unit (safe: no whitespace is a surrogate). */
export const isSpaceUnit = isSpaceCp;

/** The value of a Unicode decimal digit (CPython's Py_UNICODE_TODECIMAL), or -1. Nd digits come in aligned runs 0..9. */
export function decimalValue(cp: number): number {
  if (cp >= 0x30 && cp <= 0x39) return cp - 0x30;
  const i = findRun(D, cp);
  return i < 0 ? -1 : (cp - D[2 * i]!) % 10;
}

/**
 * Bodies for a regex character class under the u flag: [${WORD_CLASS}] matches Python's \w. SPACE_CLASS (Python's \s
 * and str.isspace(), all in the BMP) is written with \uXXXX escapes only, so it also works without the u flag.
 */
export const WORD_CLASS = classBody(W);
export const DECIMAL_CLASS = classBody(D);
export const SPACE_CLASS = classBody(S);
