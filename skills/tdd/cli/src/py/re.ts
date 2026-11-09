// Python's `re` on top of JS RegExp (always the u flag). A Python pattern is translated once and cached:
//   \w \d \s use CPython 3.13's Unicode tables (ucd.ts), \b \B are lookaround pairs on that \w, \A \Z ^ $ and . are
//   rewritten so JS's m and s flags are never used (JS's ^ $ . treat \r, U+2028, U+2029 as line ends; Python only \n),
//   (?P<n>...) and (?P=n) become JS named groups, {,n} becomes {0,n}, literal { } ] get escaped, and escapes JS's u flag
//   rejects (\- \# \& ...) become plain characters.
// Iteration (finditer, sub, split, findall) follows CPython 3.7+: after an empty match at p, the next match may not be
// empty at p but may be a longer match starting at p (JS would skip to p + 1).
// Positions (Match.start/end/span, search pos) are UTF-16 offsets into the JS string, so they slice it directly;
// `str.cpLen(s.slice(0, i))` turns one into Python's index. IGNORECASE never reaches JS's i flag (Unicode case folding):
// each cased literal and class becomes the explicit set sre matches (caseless.ts). Unsupported (throws re.error):
// atomic groups, possessive quantifiers, conditionals, \N{...}, the L flag, backreferences under IGNORECASE.
import { classBody, classSet, complement, fromRuns, literalSet, type Ranges, union } from "./caseless";
import { PyError } from "./errors";
import { D as D_RUNS, DECIMAL_CLASS, S as S_RUNS, SPACE_CLASS, W as W_RUNS, WORD_CLASS } from "./uni";

export const ASCII = 256;
export const A = ASCII;
export const IGNORECASE = 2;
export const I = IGNORECASE;
export const MULTILINE = 8;
export const M = MULTILINE;
export const DOTALL = 16;
export const S = DOTALL;
export const UNICODE = 32;
export const U = UNICODE;
export const VERBOSE = 64;
export const X = VERBOSE;

const FLAG_LETTERS: Record<string, number> = { a: ASCII, i: IGNORECASE, m: MULTILINE, s: DOTALL, u: UNICODE, x: VERBOSE };

interface Translation {
  source: string;
  groups: number;
  names: Map<string, number>;
  canConsume: boolean;
}

const fail = (msg: string, pos: number): never => {
  throw new PyError("re.error", `${msg} at position ${pos}`);
};

function hexCp(cp: number): string {
  return `\\u{${cp.toString(16)}}`;
}

function litOut(cp: number): string {
  const ch = String.fromCodePoint(cp);
  if ("^$\\.*+?()[]{}|/".includes(ch)) return "\\" + ch;
  if (cp < 0x20 || (cp >= 0x7f && cp <= 0xa0) || (cp >= 0xd800 && cp <= 0xdfff)) return hexCp(cp);
  return ch;
}

function litIn(cp: number): string {
  const ch = String.fromCodePoint(cp);
  if ("\\]^-[".includes(ch)) return "\\" + ch;
  if (cp < 0x20 || (cp >= 0x7f && cp <= 0xa0) || (cp >= 0xd800 && cp <= 0xdfff)) return hexCp(cp);
  return ch;
}

const HEX = /^[0-9a-fA-F]+$/;
const OCT = "01234567";
const DIGITS = "0123456789";
const ASCII_LETTERS = /^[A-Za-z]$/;
const SIMPLE: Record<string, number> = { a: 7, f: 12, n: 10, r: 13, t: 9, v: 11, "\\": 92 };

type Esc =
  | { k: "char"; cp: number }
  | { k: "cat"; c: "w" | "d" | "s"; body: string; neg: boolean }
  | { k: "raw"; src: string }
  | { k: "ref"; n: number };

const catSets = new Map<string, Ranges>();
/** \w \d \s as code point sets (CPython's Unicode tables, or ASCII under re.ASCII), for case-insensitive classes. */
function catSet(c: "w" | "d" | "s", asc: boolean): Ranges {
  const key = c + (asc ? "a" : "u");
  let r = catSets.get(key);
  if (!r) {
    if (asc) r = c === "w" ? [0x30, 0x39, 0x41, 0x5a, 0x5f, 0x5f, 0x61, 0x7a] : c === "d" ? [0x30, 0x39] : [0x09, 0x0d, 0x20, 0x20];
    else r = fromRuns(c === "w" ? W_RUNS : c === "d" ? D_RUNS : S_RUNS);
    catSets.set(key, r);
  }
  return r;
}

function translate(pattern: string, flags0: number, offset: number): Translation {
  let flags = flags0;
  const p = pattern;
  let i = 0;
  // leading global flags: (?aimsux) groups at the very start
  for (;;) {
    const m = /^\(\?([aiLmsux]+)\)/.exec(p.slice(i));
    if (!m) break;
    for (const c of m[1]!) {
      if (c === "L") fail("bad inline flags: cannot use 'L' flag with a str pattern", i);
      flags |= FLAG_LETTERS[c]!;
    }
    i += m[0].length;
  }
  let groups = 0;
  const names = new Map<string, number>();
  let canConsume = false;
  let look = 0; // lookaround depth
  const stack: { flags: number; look: boolean; at: number }[] = [];
  let out = "";
  let lastAtom = -1; // index in `out` where the last repeatable atom starts, or -1

  const word = () => ((flags & ASCII) !== 0 ? "A-Za-z0-9_" : WORD_CLASS);
  const cat = (c: string): string => {
    const asc = (flags & ASCII) !== 0;
    if (c === "w") return word();
    if (c === "d") return asc ? "0-9" : DECIMAL_CLASS;
    return asc ? "\\t\\n\\v\\f\\r " : SPACE_CLASS;
  };
  const caseless = () => (flags & IGNORECASE) !== 0;
  /** A literal outside a class: under IGNORECASE a cased one becomes the class of what sre matches. */
  const lit = (v: number): string => {
    const set = caseless() ? literalSet(v, (flags & ASCII) !== 0) : null;
    return set ? `[${classBody(set)}]` : litOut(v);
  };
  const cp = (j: number) => p.codePointAt(j)!;
  const w = (j: number) => (cp(j) > 0xffff ? 2 : 1);

  function readEscape(inClass: boolean): Esc {
    // p[i] === "\\"
    const at = i;
    i++;
    if (i >= p.length) fail("bad escape (end of pattern)", at);
    const c = p[i]!;
    const cc = cp(i);
    i += w(i);
    if (c === "w" || c === "d" || c === "s") return { k: "cat", c, body: cat(c), neg: false };
    if (c === "W" || c === "D" || c === "S") {
      const l = c.toLowerCase() as "w" | "d" | "s";
      return { k: "cat", c: l, body: cat(l), neg: true };
    }
    if (c in SIMPLE) return { k: "char", cp: SIMPLE[c]! };
    if (c === "x" || c === "u" || c === "U") {
      const n = c === "x" ? 2 : c === "u" ? 4 : 8;
      const h = p.slice(i, i + n);
      if (h.length !== n || !HEX.test(h)) fail(`incomplete escape \\${c}${h}`, at);
      i += n;
      const v = parseInt(h, 16);
      if (v > 0x10ffff) fail(`bad escape \\${c}${h}`, at);
      return { k: "char", cp: v };
    }
    if (c === "N") fail("\\N{...} is not supported by the kernel", at);
    if (inClass) {
      if (c === "b") return { k: "char", cp: 8 };
      if (OCT.includes(c)) {
        let e = c;
        while (e.length < 3 && i < p.length && OCT.includes(p[i]!)) e += p[i++];
        const v = parseInt(e, 8);
        if (v > 0o377) fail(`octal escape value \\${e} outside of range 0-0o377`, at);
        return { k: "char", cp: v };
      }
      if (DIGITS.includes(c)) fail(`bad escape \\${c}`, at);
      if (ASCII_LETTERS.test(c)) fail(`bad escape \\${c}`, at);
      return { k: "char", cp: cc };
    }
    if (c === "A") return { k: "raw", src: "(?<![\\s\\S])" };
    if (c === "Z") return { k: "raw", src: "(?![\\s\\S])" };
    if (c === "b" || c === "B") {
      const W = `[${word()}]`;
      return c === "b"
        ? { k: "raw", src: `(?:(?<=${W})(?!${W})|(?<!${W})(?=${W}))` }
        : { k: "raw", src: `(?:(?<=${W})(?=${W})|(?<!${W})(?!${W})(?:(?<=[\\s\\S])|(?=[\\s\\S])))` };
    }
    if (c === "0") {
      let e = "";
      while (e.length < 2 && i < p.length && OCT.includes(p[i]!)) e += p[i++];
      return { k: "char", cp: parseInt("0" + e, 8) };
    }
    if (DIGITS.includes(c)) {
      // two octal digits after the first make an octal escape; else a group reference of one or two digits
      if (OCT.includes(c) && OCT.includes(p[i] ?? "x") && OCT.includes(p[i + 1] ?? "x")) {
        const e = c + p[i] + p[i + 1];
        i += 2;
        const v = parseInt(e, 8);
        if (v > 0o377) fail(`octal escape value \\${e} outside of range 0-0o377`, at);
        return { k: "char", cp: v };
      }
      let e = c;
      if (i < p.length && DIGITS.includes(p[i]!)) e += p[i++];
      const n = parseInt(e, 10);
      if (n > groups) fail(`invalid group reference ${n}`, at + 1);
      return { k: "ref", n };
    }
    if (ASCII_LETTERS.test(c)) fail(`bad escape \\${c}`, at);
    return { k: "char", cp: cc };
  }

  function readClass(): string {
    // p[i] === "["
    const at = i;
    i++;
    let negate = false;
    if (p[i] === "^") {
      negate = true;
      i++;
    }
    const lits: number[] = [];
    const ranges: number[] = [];
    const cats: { c: "w" | "d" | "s"; body: string; neg: boolean }[] = [];
    let first = true;
    for (;;) {
      if (i >= p.length) fail("unterminated character set", at);
      if (p[i] === "]" && !first) {
        i++;
        break;
      }
      first = false;
      let lo: number;
      if (p[i] === "\\") {
        const e = readEscape(true);
        if (e.k === "cat") {
          cats.push(e);
          if (p[i] === "-" && p[i + 1] !== "]" && i + 1 < p.length) fail("bad character range", i);
          continue;
        }
        if (e.k !== "char") fail("bad escape in character set", i);
        lo = (e as { cp: number }).cp;
      } else {
        lo = cp(i);
        i += w(i);
      }
      if (p[i] === "-" && i + 1 < p.length && p[i + 1] !== "]") {
        i++;
        let hi: number;
        if (p[i] === "\\") {
          const e = readEscape(true);
          if (e.k !== "char") fail("bad character range", i);
          hi = (e as { cp: number }).cp;
        } else {
          hi = cp(i);
          i += w(i);
        }
        if (hi < lo) fail("bad character range", i);
        ranges.push(lo, hi);
      } else lits.push(lo);
    }
    canConsume ||= look === 0;
    if (caseless()) {
      const asc = (flags & ASCII) !== 0;
      const uniq = [...new Set(lits)];
      let set: Ranges | null;
      if (uniq.length === 1 && ranges.length === 0 && cats.length === 0) {
        // sre_parse turns a one-literal class into LITERAL / NOT_LITERAL
        set = literalSet(uniq[0]!, asc) ?? [uniq[0]!, uniq[0]!];
      } else set = classSet(lits, ranges, union(...cats.map((k) => (k.neg ? complement(catSet(k.c, asc)) : catSet(k.c, asc)))), asc);
      if (set) return `[${negate ? "^" : ""}${classBody(set)}]`;
    }
    let pos = lits.map(litIn).join("");
    for (let k = 0; k < ranges.length; k += 2) pos += `${litIn(ranges[k]!)}-${litIn(ranges[k + 1]!)}`;
    const negs: string[] = [];
    for (const k of cats) {
      if (k.neg) negs.push(k.body);
      else pos += k.body;
    }
    if (negs.length === 0) return `[${negate ? "^" : ""}${pos}]`;
    const alts = [...(pos ? [`[${pos}]`] : []), ...negs.map((b) => `[^${b}]`)];
    return negate ? `(?:(?!${alts.join("|")})[\\s\\S])` : `(?:${alts.join("|")})`;
  }

  while (i < p.length) {
    const c = p[i]!;
    if ((flags & VERBOSE) !== 0) {
      if (" \t\n\r\v\f".includes(c)) {
        i++;
        continue;
      }
      if (c === "#") {
        while (i < p.length && p[i] !== "\n") i++;
        continue;
      }
    }
    const atomStart = out.length;
    if (c === "\\") {
      const escAt = i;
      const e = readEscape(false);
      if (e.k === "char") {
        out += lit(e.cp);
        canConsume ||= look === 0;
      } else if (e.k === "cat") {
        out += `[${e.neg ? "^" : ""}${e.body}]`;
        canConsume ||= look === 0;
      } else if (e.k === "ref") {
        if (caseless()) fail("backreferences under IGNORECASE are not supported by the kernel", escAt);
        out += `(?:\\${e.n + offset})`;
        canConsume ||= look === 0;
      } else out += e.src;
      lastAtom = atomStart;
      continue;
    }
    if (c === "[") {
      out += readClass();
      lastAtom = atomStart;
      continue;
    }
    if (c === ".") {
      i++;
      out += (flags & DOTALL) !== 0 ? "[\\s\\S]" : "[^\\n]";
      canConsume ||= look === 0;
      lastAtom = atomStart;
      continue;
    }
    if (c === "^") {
      i++;
      out += (flags & MULTILINE) !== 0 ? "(?<![^\\n])" : "(?<![\\s\\S])";
      lastAtom = -1;
      continue;
    }
    if (c === "$") {
      i++;
      out += (flags & MULTILINE) !== 0 ? "(?![^\\n])" : "(?=\\n?(?![\\s\\S]))";
      lastAtom = -1;
      continue;
    }
    if (c === "|") {
      i++;
      out += "|";
      lastAtom = -1;
      continue;
    }
    if (c === "(") {
      const at = i;
      i++;
      if (p[i] !== "?") {
        groups++;
        out += "(";
        stack.push({ flags, look: false, at: atomStart });
        lastAtom = -1;
        continue;
      }
      i++;
      const d = p[i];
      if (d === ":") {
        i++;
        out += "(?:";
        stack.push({ flags, look: false, at: atomStart });
      } else if (d === "P" && p[i + 1] === "<") {
        const end = p.indexOf(">", i + 2);
        if (end < 0) fail("missing >, unterminated name", i);
        const name = p.slice(i + 2, end);
        if (!/^[\p{L}\p{Nl}_][\p{L}\p{N}\p{Mn}\p{Mc}\p{Pc}]*$/u.test(name)) fail(`bad character in group name '${name}'`, i + 2);
        groups++;
        names.set(name, groups);
        i = end + 1;
        out += `(?<${name}>`;
        stack.push({ flags, look: false, at: atomStart });
      } else if (d === "P" && p[i + 1] === "=") {
        const end = p.indexOf(")", i + 2);
        if (end < 0) fail("missing ), unterminated name", i);
        const name = p.slice(i + 2, end);
        if (!names.has(name)) fail(`unknown group name '${name}'`, i + 2);
        if (caseless()) fail("backreferences under IGNORECASE are not supported by the kernel", at);
        i = end + 1;
        out += `(?:\\k<${name}>)`;
        canConsume ||= look === 0;
        lastAtom = atomStart;
        continue;
      } else if (d === "=" || d === "!") {
        i++;
        out += `(?${d}`;
        look++;
        stack.push({ flags, look: true, at: atomStart });
      } else if (d === "<" && (p[i + 1] === "=" || p[i + 1] === "!")) {
        out += `(?<${p[i + 1]}`;
        i += 2;
        look++;
        stack.push({ flags, look: true, at: atomStart });
      } else if (d === "#") {
        const end = p.indexOf(")", i);
        if (end < 0) fail("missing ), unterminated comment", at);
        i = end + 1;
        continue;
      } else if (d !== undefined && /[aiLmsux-]/.test(d)) {
        const m = /^([aiLmsux]*)(?:-([imsx]+))?([:)])/.exec(p.slice(i));
        if (!m) fail("unknown extension ?" + d, at + 1);
        if (m![3] === ")") fail("global flags not at the start of the expression", at);
        let f = flags;
        if (/a/.test(m![1]!) && /u/.test(m![1]!)) fail("bad inline flags: flags 'a', 'u' and 'L' are incompatible", i);
        if (/[au]/.test(m![1]!)) f &= ~(ASCII | UNICODE); // a scoped type flag replaces the outer one (_combine_flags)
        for (const ch of m![1]!) {
          if (ch === "L") fail("bad inline flags: cannot use 'L' flag with a str pattern", i);
          f |= FLAG_LETTERS[ch]!;
        }
        for (const ch of m![2] ?? "") f &= ~FLAG_LETTERS[ch]!;
        i += m![0].length;
        out += "(?:"; // every flag is applied by the translation itself
        stack.push({ flags, look: false, at: atomStart });
        flags = f;
      } else if (d === ">" || d === "(") {
        fail(d === ">" ? "atomic groups are not supported by the kernel" : "conditional groups are not supported by the kernel", at);
      } else fail(`unknown extension ?${d ?? ""}`, at + 1);
      lastAtom = -1;
      continue;
    }
    if (c === ")") {
      i++;
      const top = stack.pop();
      if (!top) fail("unbalanced parenthesis", i - 1);
      flags = top!.flags;
      if (top!.look) look--;
      out += ")";
      lastAtom = top!.at; // the group as a whole is what a following quantifier repeats
      continue;
    }
    if (c === "*" || c === "+" || c === "?" || c === "{") {
      let q = "";
      if (c === "{") {
        const m = /^\{(\d*)(?:(,)(\d*))?\}/.exec(p.slice(i));
        if (!m || (p[i + 1] === "}")) {
          // not a quantifier: a literal brace
          i++;
          out += "\\{";
          canConsume ||= look === 0;
          lastAtom = atomStart;
          continue;
        }
        const lo = m[1] || "0";
        q = m[2] ? `{${lo},${m[3] ?? ""}}` : `{${lo}}`;
        if (m[2] && m[3] && Number(m[3]) < Number(lo)) fail("min repeat greater than max repeat", i);
        i += m[0].length;
      } else {
        q = c;
        i++;
      }
      if (lastAtom < 0) fail("nothing to repeat", i - 1);
      if (p[i] === "?") {
        q += "?";
        i++;
      } else if (p[i] === "+") fail("possessive quantifiers are not supported by the kernel", i);
      out += q;
      if (p[i] === "*" || p[i] === "+" || p[i] === "?" || (p[i] === "{" && /^\{\d*(?:,\d*)?\}/.test(p.slice(i)) && p[i + 1] !== "}"))
        fail("multiple repeat", i);
      lastAtom = -1;
      continue;
    }
    // a literal character
    const v = cp(i);
    i += w(i);
    out += lit(v);
    canConsume ||= look === 0;
    lastAtom = atomStart;
  }
  if (stack.length) fail("missing ), unterminated subpattern", p.length);
  return { source: out, groups, names, canConsume };
}

export class Match {
  readonly re: Pattern;
  readonly string: string;
  readonly pos: number;
  /** [start, end] per group (UTF-16), -1 when the group did not take part. */
  private readonly spans: Int32Array;
  constructor(re: Pattern, string: string, pos: number, exec: RegExpExecArray, shift: number) {
    this.re = re;
    this.string = string;
    this.pos = pos;
    const n = re.groups + 1;
    this.spans = new Int32Array(2 * n).fill(-1);
    const ind = exec.indices!;
    for (let g = 0; g < n; g++) {
      const span = ind[g === 0 ? 0 : g + shift];
      if (span) {
        this.spans[2 * g] = span[0];
        this.spans[2 * g + 1] = span[1];
      }
    }
  }
  private index(g: number | string): number {
    if (typeof g === "string") {
      const n = this.re.groupindex.get(g);
      if (n === undefined) throw new PyError("IndexError", "no such group");
      return n;
    }
    if (!Number.isInteger(g) || g < 0 || g > this.re.groups) throw new PyError("IndexError", "no such group");
    return g;
  }
  /** m.group(g): the text, or null for a group that did not take part (Python's None). */
  group(g: number | string = 0): string | null {
    const n = this.index(g);
    const a = this.spans[2 * n]!;
    return a < 0 ? null : this.string.slice(a, this.spans[2 * n + 1]);
  }
  /** m.groups(default=None). */
  groups<D extends string | null = null>(dflt: D = null as D): (string | D)[] {
    const out: (string | D)[] = [];
    for (let g = 1; g <= this.re.groups; g++) out.push(this.group(g) ?? dflt);
    return out;
  }
  groupdict<D extends string | null = null>(dflt: D = null as D): Record<string, string | D> {
    const out: Record<string, string | D> = {};
    for (const [k, n] of this.re.groupindex) out[k] = this.group(n) ?? dflt;
    return out;
  }
  start(g: number | string = 0): number {
    return this.spans[2 * this.index(g)]!;
  }
  end(g: number | string = 0): number {
    return this.spans[2 * this.index(g) + 1]!;
  }
  span(g: number | string = 0): [number, number] {
    return [this.start(g), this.end(g)];
  }
  /** m.expand(template): \1, \g<1>, \g<name>, and the escapes Python's templates allow. */
  expand(template: string): string {
    return expandTemplate(this.re, template)(this);
  }
}

export type Repl = string | ((m: Match) => string);

const CACHE = new Map<string, Pattern>();

export class Pattern {
  readonly pattern: string;
  readonly flags: number;
  readonly groups: number;
  readonly groupindex: ReadonlyMap<string, number>;
  /** The translated JS source (d and u flags only). */
  readonly source: string;
  private readonly rx: RegExp;
  private readonly sticky: RegExp;
  private readonly whole: RegExp;
  private readonly canConsume: boolean;
  private advance: RegExp | null = null;

  constructor(pattern: string, flags = 0) {
    this.pattern = pattern;
    const t = translate(pattern, flags, 0);
    this.flags = flags;
    this.groups = t.groups;
    this.groupindex = t.names;
    this.source = t.source;
    this.canConsume = t.canConsume;
    const f = "du";
    try {
      this.rx = new RegExp(t.source, "g" + f);
      this.sticky = new RegExp(t.source, "y" + f);
      this.whole = new RegExp(`(?:${t.source})(?![\\s\\S])`, "y" + f);
    } catch (e) {
      throw new PyError("re.error", `the kernel could not translate ${JSON.stringify(pattern)}: ${(e as Error).message}`);
    }
  }

  /** pattern.search(string, pos=0) */
  search(s: string, pos = 0): Match | null {
    this.rx.lastIndex = pos;
    const e = this.rx.exec(s);
    return e ? new Match(this, s, pos, e, 0) : null;
  }
  /** pattern.match(string, pos=0): anchored at pos. */
  match(s: string, pos = 0): Match | null {
    this.sticky.lastIndex = pos;
    const e = this.sticky.exec(s);
    return e ? new Match(this, s, pos, e, 0) : null;
  }
  /** pattern.fullmatch(string, pos=0) */
  fullmatch(s: string, pos = 0): Match | null {
    this.whole.lastIndex = pos;
    const e = this.whole.exec(s);
    return e ? new Match(this, s, pos, e, 0) : null;
  }

  /** A non-empty match starting exactly at p (CPython's must_advance retry after an empty match at p). */
  private nonEmptyAt(s: string, p: number): Match | null {
    if (!this.canConsume) return null;
    if (!this.advance) {
      const t = translate(this.pattern, this.flags, 1);
      this.advance = new RegExp(`(?<=^([\\s\\S]*))(?:${t.source})(?<!^\\1)`, "ydu");
    }
    this.advance.lastIndex = p;
    const e = this.advance.exec(s);
    return e ? new Match(this, s, p, e, 1) : null;
  }

  /** pattern.finditer(string): CPython's empty-match rules. */
  *finditer(s: string, pos = 0): Generator<Match> {
    let p = pos;
    let mustAdvance = false;
    while (p <= s.length) {
      let m: Match | null = null;
      if (mustAdvance) {
        m = this.nonEmptyAt(s, p);
        if (!m) {
          if (p >= s.length) return;
          p += (s.codePointAt(p)! > 0xffff ? 2 : 1);
          m = this.search(s, p);
        }
      } else m = this.search(s, p);
      if (!m) return;
      yield m;
      mustAdvance = m.end() === m.start();
      p = m.end();
    }
  }

  /** pattern.findall(string): strings, group 1, or tuples (as arrays) of groups; unmatched groups give "". */
  findall(s: string): (string | string[])[] {
    const out: (string | string[])[] = [];
    for (const m of this.finditer(s)) {
      if (this.groups === 0) out.push(m.group(0)!);
      else if (this.groups === 1) out.push(m.group(1) ?? "");
      else out.push(m.groups(""));
    }
    return out;
  }

  /** pattern.subn(repl, string, count=0) -> [new string, number of substitutions]. */
  subn(repl: Repl, s: string, count = 0): [string, number] {
    const fn = typeof repl === "function" ? repl : expandTemplate(this, repl);
    let out = "";
    let last = 0;
    let n = 0;
    for (const m of this.finditer(s)) {
      if (count > 0 && n >= count) break;
      out += s.slice(last, m.start()) + fn(m);
      last = m.end();
      n++;
    }
    return [out + s.slice(last), n];
  }
  /** pattern.sub(repl, string, count=0) */
  sub(repl: Repl, s: string, count = 0): string {
    return this.subn(repl, s, count)[0];
  }

  /** pattern.split(string, maxsplit=0): groups are kept, unmatched ones as null (None). */
  split(s: string, maxsplit = 0): (string | null)[] {
    const out: (string | null)[] = [];
    let last = 0;
    let n = 0;
    for (const m of this.finditer(s)) {
      if (maxsplit > 0 && n >= maxsplit) break;
      out.push(s.slice(last, m.start()));
      for (let g = 1; g <= this.groups; g++) out.push(m.group(g));
      last = m.end();
      n++;
    }
    out.push(s.slice(last));
    return out;
  }
}

function expandTemplate(re: Pattern, template: string): (m: Match) => string {
  if (!template.includes("\\")) return () => template;
  const parts: (string | number)[] = [];
  let lit = "";
  let i = 0;
  const T = template;
  while (i < T.length) {
    const c = T[i]!;
    if (c !== "\\") {
      lit += c;
      i++;
      continue;
    }
    const d = T[i + 1];
    if (d === undefined) throw new PyError("re.error", `bad escape (end of pattern) at position ${i}`);
    if (d === "g") {
      const m = /^<([^>]*)>/.exec(T.slice(i + 2));
      if (!m) throw new PyError("re.error", `missing < at position ${i + 2}`);
      const name = m[1]!;
      let n: number;
      if (/^[0-9]+$/.test(name)) n = Number(name);
      else {
        const k = re.groupindex.get(name);
        if (k === undefined) throw new PyError("IndexError", `unknown group name '${name}'`);
        n = k;
      }
      if (n > re.groups) throw new PyError("re.error", `invalid group reference ${n} at position ${i + 3}`);
      if (lit) parts.push(lit), (lit = "");
      parts.push(n);
      i += 3 + m[0].length - 1;
      continue;
    }
    if (d === "0") {
      let e = "";
      let j = i + 2;
      while (e.length < 2 && OCT.includes(T[j] ?? "x")) e += T[j++];
      lit += String.fromCharCode(parseInt("0" + e, 8));
      i = j;
      continue;
    }
    if (DIGITS.includes(d)) {
      let j = i + 2;
      if (OCT.includes(d) && OCT.includes(T[j] ?? "x") && OCT.includes(T[j + 1] ?? "x")) {
        const v = parseInt(d + T[j] + T[j + 1], 8);
        if (v > 0o377) throw new PyError("re.error", `octal escape value outside of range 0-0o377 at position ${i}`);
        lit += String.fromCharCode(v);
        i = j + 2;
        continue;
      }
      let e = d;
      if (DIGITS.includes(T[j] ?? "x")) e += T[j++];
      const n = Number(e);
      if (n > re.groups) throw new PyError("re.error", `invalid group reference ${n} at position ${i + 1}`);
      if (lit) parts.push(lit), (lit = "");
      parts.push(n);
      i = j;
      continue;
    }
    const simple: Record<string, string> = { a: "\x07", b: "\b", f: "\f", n: "\n", r: "\r", t: "\t", v: "\v", "\\": "\\" };
    if (d in simple) {
      lit += simple[d];
      i += 2;
      continue;
    }
    if (ASCII_LETTERS.test(d)) throw new PyError("re.error", `bad escape \\${d} at position ${i}`);
    lit += "\\" + d;
    i += 2;
  }
  if (lit) parts.push(lit);
  return (m) => {
    let s = "";
    for (const x of parts) s += typeof x === "string" ? x : (m.group(x) ?? "");
    return s;
  };
}

/** re.compile(pattern, flags=0), cached like Python's. */
export function compile(pattern: string | Pattern, flags = 0): Pattern {
  if (pattern instanceof Pattern) return pattern;
  const key = `${flags}\u0000${pattern}`;
  let r = CACHE.get(key);
  if (!r) {
    r = new Pattern(pattern, flags);
    CACHE.set(key, r);
  }
  return r;
}

export const search = (p: string | Pattern, s: string, flags = 0) => compile(p, flags).search(s);
export const match = (p: string | Pattern, s: string, flags = 0) => compile(p, flags).match(s);
export const fullmatch = (p: string | Pattern, s: string, flags = 0) => compile(p, flags).fullmatch(s);
export const finditer = (p: string | Pattern, s: string, flags = 0) => compile(p, flags).finditer(s);
export const findall = (p: string | Pattern, s: string, flags = 0) => compile(p, flags).findall(s);
export const sub = (p: string | Pattern, repl: Repl, s: string, count = 0, flags = 0) => compile(p, flags).sub(repl, s, count);
export const subn = (p: string | Pattern, repl: Repl, s: string, count = 0, flags = 0) => compile(p, flags).subn(repl, s, count);
export const split = (p: string | Pattern, s: string, maxsplit = 0, flags = 0) => compile(p, flags).split(s, maxsplit);

const SPECIAL = new Set("()[]{}?*+-|^$\\.&~# \t\n\r\v\f");
/** re.escape(s): a backslash before each of ()[]{}?*+-|^$\.&~# and whitespace \t\n\r\v\f (Python 3.7+). */
export function escape(s: string): string {
  let out = "";
  for (const ch of s) out += SPECIAL.has(ch) ? "\\" + ch : ch;
  return out;
}
