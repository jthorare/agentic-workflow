// re.IGNORECASE as CPython 3.13's sre does it, for re.ts. sre does not use Unicode case folding (JS's i flag does). It
// lowers with its simple map (_sre.unicode_tolower; A-Z only under re.ASCII) and adds the re._casefix equivalences:
//   - a cased literal c matches every ch with lower(ch) in {lower(c)} + casefix[lower(c)], so "i" matches i, I, İ and ı,
//     "s" matches ſ and "k" matches the Kelvin sign; an uncased literal matches only itself;
//   - a class is "cased" when a literal's lower form is cased, a range holds a cased code point, or a member lies past
//     the BMP; a cased class tests lower(ch) against its members lowered (plus casefix), its categories (\w, \d, \s and
//     their negations, so they see lower(ch) too) and, for a range past the BMP, upper(lower(ch)) as well
//     (RANGE_UNI_IGNORE); a class that is not cased compares ch itself;
//   - backreferences compare lower(a) with lower(b), which JS cannot express (re.ts refuses them under IGNORECASE).
// Each case-insensitive atom becomes an explicit JS class here, so the translated pattern never needs JS's i flag.
import { SRE_CASED_RUNS, SRE_CASEFIX, SRE_LOWER, SRE_UPPER } from "./ucd";

/** A set of code points: sorted, merged, inclusive [start, end] pairs flattened. */
export type Ranges = number[];

export function fromRuns(runs: ArrayLike<number>): Ranges {
  return normalize(Array.from(runs));
}

/** Sort and merge arbitrary [a, b] pairs. */
export function normalize(pairs: number[]): Ranges {
  const ps: [number, number][] = [];
  for (let i = 0; i < pairs.length; i += 2) ps.push([pairs[i]!, pairs[i + 1]!]);
  ps.sort((x, y) => x[0] - y[0]);
  const out: Ranges = [];
  for (const [a, b] of ps) {
    const n = out.length;
    if (n && a <= out[n - 1]! + 1) out[n - 1] = Math.max(out[n - 1]!, b);
    else out.push(a, b);
  }
  return out;
}

export const union = (...sets: Ranges[]): Ranges => normalize(sets.flat());

export function complement(a: Ranges): Ranges {
  const out: Ranges = [];
  let next = 0;
  for (let i = 0; i < a.length; i += 2) {
    if (a[i]! > next) out.push(next, a[i]! - 1);
    next = a[i + 1]! + 1;
  }
  if (next <= 0x10ffff) out.push(next, 0x10ffff);
  return out;
}

export const minus = (a: Ranges, b: Ranges): Ranges => complement(union(complement(a), b));

export function has(a: Ranges, cp: number): boolean {
  let lo = 0;
  let hi = a.length / 2 - 1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (cp < a[2 * mid]!) hi = mid - 1;
    else if (cp > a[2 * mid + 1]!) lo = mid + 1;
    else return true;
  }
  return false;
}

export function intersects(a: Ranges, b: Ranges): boolean {
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    if (a[i + 1]! < b[j]!) i += 2;
    else if (b[j + 1]! < a[i]!) j += 2;
    else return true;
  }
  return false;
}

/** The body of a JS character class (u flag) for a set. */
export function classBody(a: Ranges): string {
  const hex = (c: number) => `\\u{${c.toString(16)}}`;
  let out = "";
  for (let i = 0; i < a.length; i += 2) out += a[i] === a[i + 1] ? hex(a[i]!) : `${hex(a[i]!)}-${hex(a[i + 1]!)}`;
  return out;
}

function pairs(table: string): Map<number, number> {
  const m = new Map<number, number>();
  for (const pair of table.split(",")) {
    const k = pair.indexOf(":");
    m.set(parseInt(pair.slice(0, k), 16), parseInt(pair.slice(k + 1), 16));
  }
  return m;
}

interface Fold {
  lower: Map<number, number>; // cp -> lower(cp), only where it moves
  moved: Ranges; // the keys of `lower`
  cased: Ranges;
  fixes: Map<number, readonly number[]>;
}

let unicodeFold: Fold | null = null;
function unicode(): Fold {
  if (unicodeFold) return unicodeFold;
  const lower = pairs(SRE_LOWER);
  const cased: number[] = [];
  for (const part of SRE_CASED_RUNS.split(",")) {
    const d = part.indexOf("-");
    const a = parseInt(d < 0 ? part : part.slice(0, d), 16);
    cased.push(a, d < 0 ? a : parseInt(part.slice(d + 1), 16));
  }
  unicodeFold = {
    lower,
    moved: normalize([...lower.keys()].flatMap((c) => [c, c])),
    cased: normalize(cased),
    fixes: new Map(SRE_CASEFIX.map(([k, v]) => [k, v])),
  };
  return unicodeFold;
}

let asciiFold: Fold | null = null;
function ascii(): Fold {
  if (asciiFold) return asciiFold;
  const lower = new Map<number, number>();
  for (let c = 0x41; c <= 0x5a; c++) lower.set(c, c + 32);
  asciiFold = { lower, moved: [0x41, 0x5a], cased: [0x41, 0x5a, 0x61, 0x7a], fixes: new Map() };
  return asciiFold;
}

let upperMap: Map<number, number> | null = null;
/** sre_upper_unicode, where it moves (RANGE_UNI_IGNORE uses it under re.ASCII too). */
const upper = (): Map<number, number> => (upperMap ??= pairs(SRE_UPPER));

const fold = (asciiOnly: boolean) => (asciiOnly ? ascii() : unicode());

/** Every ch whose lower(ch) is in `tested`. */
function preimage(f: Fold, tested: Ranges): Ranges {
  const extra: number[] = [];
  for (const [c, l] of f.lower) if (has(tested, l)) extra.push(c, c);
  return union(minus(tested, f.moved), normalize(extra));
}

/** A literal under IGNORECASE: the set it matches, or null when it is not cased (it matches only itself). */
export function literalSet(cp: number, asciiOnly: boolean): Ranges | null {
  const f = fold(asciiOnly);
  if (!has(f.cased, cp)) return null;
  const lo = f.lower.get(cp) ?? cp;
  return preimage(f, normalize([lo, lo, ...(f.fixes.get(lo) ?? []).flatMap((x) => [x, x])]));
}

/**
 * A class under IGNORECASE (sre's _optimize_charset, then IN_UNI_IGNORE / IN_IGNORE): `lits` are its literal code
 * points, `ranges` its [lo, hi] pairs, `cats` the union of its categories (each complemented when negated). Returns the
 * set of code points the class matches before any ^, or null when the class is not cased (sre then compares the
 * character itself, so the class is emitted unchanged).
 */
export function classSet(lits: readonly number[], ranges: readonly number[], cats: Ranges, asciiOnly: boolean): Ranges | null {
  const f = fold(asciiOnly);
  const charmap: number[] = []; // lowered members that fit sre's 64K charmap; casefix applies to these only
  const tail: number[] = []; // what sre keeps past the charmap: lowered non-BMP literals, RANGE_UNI_IGNORE
  let cased = false;
  for (const c of lits) {
    const l = f.lower.get(c) ?? c;
    if (l > 0xffff) {
      tail.push(l, l); // no casefix, and the class counts as cased
      cased = true;
    } else {
      charmap.push(l, l);
      if (has(f.cased, l)) cased = true;
    }
  }
  for (let i = 0; i < ranges.length; i += 2) {
    const a = ranges[i]!;
    const b = ranges[i + 1]!;
    const bmp = Math.min(b, 0xffff);
    if (a <= bmp) {
      // lower(c) for every BMP member: the members that do not move, and the lower forms of those that do
      charmap.push(...minus([a, bmp], f.moved));
      for (const [c, l] of f.lower) if (c >= a && c <= bmp) charmap.push(l, l);
    }
    if (b > 0xffff) {
      // RANGE_UNI_IGNORE: lower(ch) in [a, b], or upper(lower(ch)) in [a, b]
      cased = true;
      tail.push(a, b);
      for (const [x, u] of upper()) if (u >= a && u <= b) tail.push(x, x);
    } else if (intersects([a, b], f.cased)) cased = true;
  }
  if (!cased) return null;
  const base = normalize(charmap);
  const fx: number[] = [];
  for (const [k, v] of f.fixes) if (has(base, k)) for (const x of v) fx.push(x, x);
  return preimage(f, union(base, fx, tail, cats));
}
