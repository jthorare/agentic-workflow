// Python str methods with Python's boundaries. Strings stay JS strings (UTF-16); where Python counts code points
// (len, slicing, sort order, the empty-string replace) these helpers count code points too.
import { PyError } from "./errors";
import { LOWER_X, UPPER_X } from "./ucd";
import { isLinebreakCp, isSpaceUnit } from "./uni";

const MAX = Number.MAX_SAFE_INTEGER;

/** str.isspace(): non-empty and every character whitespace. */
export function isspace(s: string): boolean {
  if (s.length === 0) return false;
  for (let i = 0; i < s.length; i++) if (!isSpaceUnit(s.charCodeAt(i))) return false;
  return true;
}

function charSet(chars: string): Set<number> {
  const set = new Set<number>();
  for (const ch of chars) set.add(ch.codePointAt(0)!);
  return set;
}

/** Code point that ends at unit index `end` (exclusive), and its width in units. */
function cpBefore(s: string, end: number): [number, number] {
  const lo = s.charCodeAt(end - 1);
  if (lo >= 0xdc00 && lo <= 0xdfff && end >= 2) {
    const hi = s.charCodeAt(end - 2);
    if (hi >= 0xd800 && hi <= 0xdbff) return [((hi - 0xd800) << 10) + (lo - 0xdc00) + 0x10000, 2];
  }
  return [lo, 1];
}

function stripLeft(s: string, chars: string | null | undefined): number {
  let i = 0;
  if (chars == null) {
    while (i < s.length && isSpaceUnit(s.charCodeAt(i))) i++;
    return i;
  }
  const set = charSet(chars);
  while (i < s.length) {
    const cp = s.codePointAt(i)!;
    if (!set.has(cp)) break;
    i += cp > 0xffff ? 2 : 1;
  }
  return i;
}

function stripRight(s: string, chars: string | null | undefined, floor: number): number {
  let j = s.length;
  if (chars == null) {
    while (j > floor && isSpaceUnit(s.charCodeAt(j - 1))) j--;
    return j;
  }
  const set = charSet(chars);
  while (j > floor) {
    const [cp, w] = cpBefore(s, j);
    if (!set.has(cp)) break;
    j -= w;
  }
  return j;
}

/** str.strip(chars=None). */
export function strip(s: string, chars?: string | null): string {
  const i = stripLeft(s, chars);
  return s.slice(i, stripRight(s, chars, i));
}
/** str.lstrip(chars=None); lstrip(p, "./") strips a character set, not a prefix. */
export function lstrip(s: string, chars?: string | null): string {
  return s.slice(stripLeft(s, chars));
}
/** str.rstrip(chars=None). */
export function rstrip(s: string, chars?: string | null): string {
  return s.slice(0, stripRight(s, chars, 0));
}

/** str.split(sep=None, maxsplit=-1): maxsplit keeps the remainder (JS's limit drops it). */
export function split(s: string, sep?: string | null, maxsplit = -1): string[] {
  let count = maxsplit < 0 ? MAX : maxsplit;
  const out: string[] = [];
  const n = s.length;
  if (sep == null) {
    let i = 0;
    while (count-- > 0) {
      while (i < n && isSpaceUnit(s.charCodeAt(i))) i++;
      if (i === n) break;
      const j = i;
      i++;
      while (i < n && !isSpaceUnit(s.charCodeAt(i))) i++;
      out.push(s.slice(j, i));
    }
    if (i < n) {
      while (i < n && isSpaceUnit(s.charCodeAt(i))) i++;
      if (i !== n) out.push(s.slice(i));
    }
    return out;
  }
  if (sep === "") throw new PyError("ValueError", "empty separator");
  let i = 0;
  while (count-- > 0) {
    const k = s.indexOf(sep, i);
    if (k < 0) break;
    out.push(s.slice(i, k));
    i = k + sep.length;
  }
  out.push(s.slice(i));
  return out;
}

/** str.rsplit(sep=None, maxsplit=-1). */
export function rsplit(s: string, sep?: string | null, maxsplit = -1): string[] {
  let count = maxsplit < 0 ? MAX : maxsplit;
  const out: string[] = [];
  if (sep == null) {
    let i = s.length - 1;
    while (count-- > 0) {
      while (i >= 0 && isSpaceUnit(s.charCodeAt(i))) i--;
      if (i < 0) break;
      const j = i;
      i--;
      while (i >= 0 && !isSpaceUnit(s.charCodeAt(i))) i--;
      out.push(s.slice(i + 1, j + 1));
    }
    if (i >= 0) {
      while (i >= 0 && isSpaceUnit(s.charCodeAt(i))) i--;
      if (i >= 0) out.push(s.slice(0, i + 1));
    }
    return out.reverse();
  }
  if (sep === "") throw new PyError("ValueError", "empty separator");
  let j = s.length;
  while (count-- > 0) {
    if (j < sep.length) break;
    const k = s.lastIndexOf(sep, j - sep.length);
    if (k < 0) break;
    out.push(s.slice(k + sep.length, j));
    j = k;
  }
  out.push(s.slice(0, j));
  return out.reverse();
}

/** str.partition(sep) -> [head, sep, tail]; [s, "", ""] when sep is absent. */
export function partition(s: string, sep: string): [string, string, string] {
  if (sep === "") throw new PyError("ValueError", "empty separator");
  const k = s.indexOf(sep);
  return k < 0 ? [s, "", ""] : [s.slice(0, k), sep, s.slice(k + sep.length)];
}

/** str.rpartition(sep) -> [head, sep, tail]; ["", "", s] when sep is absent. */
export function rpartition(s: string, sep: string): [string, string, string] {
  if (sep === "") throw new PyError("ValueError", "empty separator");
  const k = s.lastIndexOf(sep);
  return k < 0 ? ["", "", s] : [s.slice(0, k), sep, s.slice(k + sep.length)];
}

/** str.splitlines(keepends=False): \n \r \r\n \v \f \x1c \x1d \x1e \x85 U+2028 U+2029, no trailing empty line. */
export function splitlines(s: string, keepends = false): string[] {
  const out: string[] = [];
  const n = s.length;
  let i = 0;
  let j = 0;
  while (i < n) {
    while (i < n && !isLinebreakCp(s.charCodeAt(i))) i++;
    let eol = i;
    if (i < n) {
      i += s.charCodeAt(i) === 13 && i + 1 < n && s.charCodeAt(i + 1) === 10 ? 2 : 1;
      if (keepends) eol = i;
    }
    out.push(s.slice(j, eol));
    j = i;
  }
  return out;
}

/**
 * str.replace(old, new, count=-1): every occurrence by default (JS's string-pattern replace changes only the first;
 * src/ outside src/py must call this instead, test/py/replace-ban.test.ts enforces it). An empty `old` inserts
 * `new` before every code point and at the end.
 */
export function replace(s: string, old: string, repl: string, count = -1): string {
  let left = count < 0 ? MAX : count;
  if (left === 0) return s;
  if (old === "") {
    let out = "";
    let i = 0;
    while (i < s.length && left > 0) {
      const w = s.codePointAt(i)! > 0xffff ? 2 : 1;
      out += repl + s.slice(i, i + w);
      i += w;
      left--;
    }
    if (i === s.length && left > 0) return out + repl;
    return out + s.slice(i);
  }
  let out = "";
  let i = 0;
  while (left-- > 0) {
    const k = s.indexOf(old, i);
    if (k < 0) break;
    out += s.slice(i, k) + repl;
    i = k + old.length;
  }
  return out + s.slice(i);
}

const HAS_SURROGATE = /[\ud800-\udfff]/;

/** len(s): code points, not UTF-16 units. */
export function cpLen(s: string): number {
  if (!HAS_SURROGATE.test(s)) return s.length;
  let n = 0;
  for (const _ of s) n++;
  return n;
}

function sliceIndex(v: number | null | undefined, len: number, dflt: number): number {
  if (v == null) return dflt;
  if (v < 0) return Math.max(0, v + len);
  return Math.min(v, len);
}

/** s[start:stop] with Python's slice rules (negative indices, clamping), counting code points. */
export function cpSlice(s: string, start?: number | null, stop?: number | null): string {
  if (!HAS_SURROGATE.test(s)) {
    const a = sliceIndex(start, s.length, 0);
    const b = sliceIndex(stop, s.length, s.length);
    return a < b ? s.slice(a, b) : "";
  }
  const cps = Array.from(s);
  const a = sliceIndex(start, cps.length, 0);
  const b = sliceIndex(stop, cps.length, cps.length);
  return a < b ? cps.slice(a, b).join("") : "";
}

/** Python's str ordering: by code point (JS's < compares UTF-16 units, which differs above U+FFFF). */
export function cmp(a: string, b: string): number {
  const n = Math.min(a.length, b.length);
  let k = 0;
  while (k < n && a.charCodeAt(k) === b.charCodeAt(k)) k++;
  if (k === n) return a.length === b.length ? 0 : a.length < b.length ? -1 : 1;
  // step back to a code point boundary: a shared high surrogate may pair with what differs
  const prev = k > 0 ? a.charCodeAt(k - 1) : 0;
  const at = prev >= 0xd800 && prev <= 0xdbff ? k - 1 : k;
  const x = a.codePointAt(at)!;
  const y = b.codePointAt(at)!;
  if (x !== y) return x < y ? -1 : 1;
  // the same lone high surrogate in both: the code points that start at k decide
  const x2 = a.codePointAt(k)!;
  const y2 = b.codePointAt(k)!;
  return x2 < y2 ? -1 : x2 > y2 ? 1 : a.charCodeAt(k) < b.charCodeAt(k) ? -1 : 1;
}

/** sorted(xs) for strings: a new array in code point order. */
export function sorted(xs: Iterable<string>): string[] {
  return [...xs].sort(cmp);
}

function caseMap(table: ReadonlyArray<readonly [number, string]>, js: (s: string) => string) {
  const map = new Map<number, string>(table.map(([c, s]) => [c, s]));
  const body = table.map(([c]) => `\\u{${c.toString(16)}}`).join("");
  const any = body ? new RegExp(`[${body}]`, "u") : null;
  return (s: string): string => {
    if (any === null || !any.test(s)) return js(s);
    let out = "";
    let run = "";
    for (const ch of s) {
      const x = map.get(ch.codePointAt(0)!);
      if (x === undefined) run += ch;
      else {
        out += js(run) + x;
        run = "";
      }
    }
    return out + js(run);
  };
}

/** str.lower() as CPython 3.13 maps it (Unicode 15.1), not the engine's newer tables. */
export const lower = caseMap(LOWER_X, (s) => s.toLowerCase());
/** str.upper() as CPython 3.13 maps it. */
export const upper = caseMap(UPPER_X, (s) => s.toUpperCase());

/** str.ljust(width, fill) / format(s, "<width"): pad on the right to `width` code points. */
export function ljust(s: string, width: number, fill = " "): string {
  return s + fill.repeat(Math.max(0, width - cpLen(s)));
}

/** str.rjust(width, fill) / format(s, ">width"). */
export function rjust(s: string, width: number, fill = " "): string {
  return fill.repeat(Math.max(0, width - cpLen(s))) + s;
}
