// Minimal Python-semantics helpers for the agents lane port (write_agents.py, strip_page.py). Each block is marked with
// the src/py module that replaces it once the kernel lane merges; keep them small and keep the CPython behaviour exact.
import { createHash } from "node:crypto";
import { readFileSync, statSync } from "node:fs";
import { htmlDecodeTree, determineBranch, BinTrieFlags, decodeHTML } from "entities/decode";

// KERNEL: replace with src/py/json at merge
/** A JSON float as Python's json.load sees it (1.0 stays a float and prints "1.0"). Ints are plain numbers. */
export class PyFloat {
  constructor(readonly v: number) {}
}
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type Py = any;
/** A Python dict: insertion order, assignment to an existing key keeps its place. */
export type PyDict = Map<string, Py>;

// KERNEL: replace with src/py/str at merge
const WS = "\\t\\n\\x0b\\x0c\\r\\x1c-\\x1f \\x85\\xa0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000";
/** Python's str.isspace() / re \s set, as a regex character-class body. */
export const PY_WS = WS;
const WS_ONE = new RegExp(`^[${WS}]$`);
const WS_RUN = new RegExp(`[${WS}]+`);
export function isspace1(ch: string): boolean {
  return ch.length > 0 && WS_ONE.test(ch);
}
function charSet(chars: string | undefined): (c: string) => boolean {
  if (chars === undefined) return (c) => WS_ONE.test(c);
  const set = new Set(Array.from(chars));
  return (c) => set.has(c);
}
export function strip(s: string, chars?: string): string {
  return lstrip(rstrip(s, chars), chars);
}
export function lstrip(s: string, chars?: string): string {
  const has = charSet(chars);
  let i = 0;
  while (i < s.length) {
    const cp = s.codePointAt(i)!;
    const c = String.fromCodePoint(cp);
    if (!has(c)) break;
    i += c.length;
  }
  return s.slice(i);
}
export function rstrip(s: string, chars?: string): string {
  const has = charSet(chars);
  let j = s.length;
  while (j > 0) {
    let k = j - 1;
    const lo = s.charCodeAt(k);
    if (lo >= 0xdc00 && lo <= 0xdfff && k > 0) {
      const hi = s.charCodeAt(k - 1);
      if (hi >= 0xd800 && hi <= 0xdbff) k--;
    }
    if (!has(s.slice(k, j))) break;
    j = k;
  }
  return s.slice(0, j);
}
/** str.split() with no separator: runs of Python whitespace, no empty strings. */
export function splitWs(s: string): string[] {
  return s.split(WS_RUN).filter((x) => x !== "");
}
/** str.partition(sep). */
export function partition(s: string, sep: string): [string, string, string] {
  const i = s.indexOf(sep);
  return i < 0 ? [s, "", ""] : [s.slice(0, i), sep, s.slice(i + sep.length)];
}
const ASTRAL = /[\ud800-\udfff]/;
/** len(s) in code points. */
export function cpLen(s: string): number {
  if (!ASTRAL.test(s)) return s.length;
  let n = 0;
  for (const _ of s) n++;
  return n;
}
/** s[a:b] in code points (non-negative bounds). */
export function cpSlice(s: string, a: number, b?: number): string {
  if (!ASTRAL.test(s)) return s.slice(a, b);
  return Array.from(s).slice(a, b).join("");
}
/** First / last code point (s[:1], s[-1:]). */
export function cpFirst(s: string): string {
  if (!s) return "";
  return String.fromCodePoint(s.codePointAt(0)!);
}
export function cpLast(s: string): string {
  if (!s) return "";
  const a = Array.from(s.slice(-2));
  return a[a.length - 1] ?? "";
}
/** Python's sort order for str: code points. */
export function cmpStr(a: string, b: string): number {
  if (a === b) return 0;
  const n = Math.min(a.length, b.length);
  for (let i = 0; i < n; i++) {
    const x = a.charCodeAt(i), y = b.charCodeAt(i);
    if (x !== y) {
      const xs = x >= 0xd800 && x <= 0xdfff, ys = y >= 0xd800 && y <= 0xdfff;
      if (xs !== ys) return xs ? 1 : -1;   // a surrogate (astral) sorts above every BMP unit
      return x - y;
    }
  }
  return a.length - b.length;
}
export function sorted(xs: Iterable<string>): string[] {
  return Array.from(xs).sort(cmpStr);
}
/** dict.fromkeys(xs) as a list: first occurrence order. */
export function uniq<T>(xs: Iterable<T>): T[] {
  return Array.from(new Set(xs));
}
/** Python truthiness. */
export function truthy(v: Py): boolean {
  if (v === null || v === undefined || v === false || v === 0 || v === "" || v === 0n) return false;
  if (v instanceof PyFloat) return v.v !== 0;
  if (Array.isArray(v)) return v.length > 0;
  if (v instanceof Map || v instanceof Set) return v.size > 0;
  return true;
}
/** a or b */
export function or<A, B>(a: A, b: B): A | B {
  return truthy(a) ? a : b;
}
export function isDict(v: Py): v is PyDict {
  return v instanceof Map;
}
/** d.get(k, dflt) on a PyDict (dflt when the key is absent, the stored value even when it is None). */
export function get(d: Py, k: Py, dflt: Py = null): Py {
  if (!(d instanceof Map)) return dflt;
  return d.has(k) ? d.get(k) : dflt;
}
/** str(x) */
export function str(v: Py): string {
  if (typeof v === "string") return v;
  if (v === null || v === undefined) return "None";
  if (v === true) return "True";
  if (v === false) return "False";
  if (typeof v === "number") return Number.isInteger(v) ? (Object.is(v, -0) ? "0" : v.toFixed(0)) : floatRepr(v);
  if (typeof v === "bigint") return v.toString();
  if (v instanceof PyFloat) return floatRepr(v.v);
  return repr(v);
}
/** repr(x) for str / list / dict / scalars (messages only). */
export function repr(v: Py): string {
  if (typeof v === "string") return strRepr(v);
  if (Array.isArray(v)) return "[" + v.map(repr).join(", ") + "]";
  if (v instanceof Map) return "{" + Array.from(v, ([k, x]) => `${repr(k)}: ${repr(x)}`).join(", ") + "}";
  if (v instanceof Set) return v.size ? "{" + Array.from(v, repr).join(", ") + "}" : "set()";
  return str(v);
}
const NONPRINT = /[\p{C}\p{Z}]/u;
function strRepr(s: string): string {
  const q = s.includes("'") && !s.includes('"') ? '"' : "'";
  let out = q;
  for (const ch of s) {
    const cp = ch.codePointAt(0)!;
    if (ch === "\\" || ch === q) out += "\\" + ch;
    else if (ch === "\t") out += "\\t";
    else if (ch === "\n") out += "\\n";
    else if (ch === "\r") out += "\\r";
    else if (cp < 0x20 || cp === 0x7f) out += "\\x" + cp.toString(16).padStart(2, "0");
    else if (cp < 0x7f || ch === " ") out += ch;
    else if (NONPRINT.test(ch)) {
      if (cp <= 0xff) out += "\\x" + cp.toString(16).padStart(2, "0");
      else if (cp <= 0xffff) out += "\\u" + cp.toString(16).padStart(4, "0");
      else out += "\\U" + cp.toString(16).padStart(8, "0");
    } else out += ch;
  }
  return out + q;
}

// KERNEL: replace with src/py/num at merge
/** repr(float): shortest round-trip digits, exponent below 1e-4 or from 1e16, ".0" on integral values. */
export function floatRepr(x: number): string {
  if (Number.isNaN(x)) return "nan";
  if (x === Infinity) return "inf";
  if (x === -Infinity) return "-inf";
  if (x === 0) return Object.is(x, -0) ? "-0.0" : "0.0";
  const [mant, expS] = x.toExponential().split("e") as [string, string];
  const neg = mant.startsWith("-");
  const digits = mant.replace("-", "").replace(".", "");
  const exp = Number(expS);
  let body: string;
  if (exp < -4 || exp >= 16) {
    body = digits[0] + (digits.length > 1 ? "." + digits.slice(1) : "") + "e" + (exp < 0 ? "-" : "+") + String(Math.abs(exp)).padStart(2, "0");
  } else if (exp >= 0) {
    const intPart = digits.length > exp + 1 ? digits.slice(0, exp + 1) : digits.padEnd(exp + 1, "0");
    const frac = digits.length > exp + 1 ? digits.slice(exp + 1) : "0";
    body = intPart + "." + frac;
  } else {
    body = "0." + "0".repeat(-exp - 1) + digits;
  }
  return (neg ? "-" : "") + body;
}
/** round(x, n) for n >= 0: half-even on the exact binary value, as CPython. Returns a Python float. */
export function pyRound(x: number, n: number): PyFloat {
  if (!Number.isFinite(x) || Math.abs(x) >= 1e21) return new PyFloat(x);
  const neg = x < 0 || Object.is(x, -0);
  const exact = Math.abs(x).toFixed(100);   // exact decimal expansion of the double (toFixed is exact per spec)
  const [ip, fp] = exact.split(".") as [string, string];
  const keep = fp.slice(0, n), rest = fp.slice(n);
  let digits = BigInt(ip + keep);
  const half = "5" + "0".repeat(rest.length - 1);
  if (rest > half || (rest === half && digits % 2n === 1n)) digits += 1n;
  const s = digits.toString().padStart(n + 1, "0");
  const v = Number(n ? s.slice(0, s.length - n) + "." + s.slice(s.length - n) : s);
  return new PyFloat(neg ? -v : v);
}
export function num(v: Py): number {
  return v instanceof PyFloat ? v.v : typeof v === "bigint" ? Number(v) : (v as number);
}

// KERNEL: replace with src/py/json at merge
export class JSONDecodeError extends Error {}
function decodeErr(msg: string, doc: string, pos: number): JSONDecodeError {
  const lineno = (doc.slice(0, pos).match(/\n/g) ?? []).length + 1;
  const nl = doc.lastIndexOf("\n", pos - 1);
  const cpos = cpLen(doc.slice(0, pos));
  const colno = nl < 0 ? cpos + 1 : cpos - cpLen(doc.slice(0, nl));
  return new JSONDecodeError(`${msg}: line ${lineno} column ${colno} (char ${cpos})`);
}
const NUMBER_RE = /(-?(?:0|[1-9][0-9]*))(\.[0-9]+)?([eE][-+]?[0-9]+)?/y;
const WS_JSON = /[ \t\n\r]*/y;
const ESC: Record<string, string> = { '"': '"', "\\": "\\", "/": "/", b: "\b", f: "\f", n: "\n", r: "\r", t: "\t" };
/** json.loads: dicts as Map (Python dict semantics), floats as PyFloat, ints as number (bigint past 2**53). */
export function loads(doc: string): Py {
  if (doc.startsWith("\ufeff")) throw decodeErr("Unexpected UTF-8 BOM (decode using utf-8-sig)", doc, 0);
  let i = 0;
  const ws = () => { WS_JSON.lastIndex = i; WS_JSON.exec(doc); i = WS_JSON.lastIndex; };
  const scanString = (start: number): string => {
    // start points after the opening quote
    let out = "";
    let j = start;
    for (;;) {
      let k = j;
      while (k < doc.length) {
        const c = doc.charCodeAt(k);
        if (c === 34 || c === 92 || c < 0x20) break;
        k++;
      }
      out += doc.slice(j, k);
      if (k >= doc.length) throw decodeErr("Unterminated string starting at", doc, start - 1);
      const c = doc[k]!;
      if (c === '"') { i = k + 1; return out; }
      if (c !== "\\") throw decodeErr("Invalid control character at", doc, k);
      const e = doc[k + 1];
      if (e === undefined) throw decodeErr("Unterminated string starting at", doc, start - 1);
      if (e !== "u") {
        const r = ESC[e];
        if (r === undefined) throw decodeErr("Invalid \\escape", doc, k);
        out += r;
        j = k + 2;
        continue;
      }
      const hex = doc.slice(k + 2, k + 6);
      if (!/^[0-9a-fA-F]{4}$/.test(hex)) throw decodeErr("Invalid \\uXXXX escape", doc, k + 1);
      let u = parseInt(hex, 16);
      j = k + 6;
      if (u >= 0xd800 && u <= 0xdbff && doc.slice(j, j + 2) === "\\u") {
        const hex2 = doc.slice(j + 2, j + 6);
        if (!/^[0-9a-fA-F]{4}$/.test(hex2)) throw decodeErr("Invalid \\uXXXX escape", doc, j + 1);
        const u2 = parseInt(hex2, 16);
        if (u2 >= 0xdc00 && u2 <= 0xdfff) {
          u = 0x10000 + (((u - 0xd800) << 10) | (u2 - 0xdc00));
          j += 6;
        }
      }
      out += String.fromCodePoint(u);
    }
  };
  const value = (): Py => {
    const c = doc[i];
    if (c === '"') { return scanString(i + 1); }
    if (c === "{") {
      const d: PyDict = new Map();
      i++;
      ws();
      if (doc[i] === "}") { i++; return d; }
      for (;;) {
        if (doc[i] !== '"') throw decodeErr("Expecting property name enclosed in double quotes", doc, i);
        const k = scanString(i + 1);
        ws();
        if (doc[i] !== ":") throw decodeErr("Expecting ':' delimiter", doc, i);
        i++;
        ws();
        d.set(k, valueAt());
        ws();
        if (doc[i] === "}") { i++; return d; }
        if (doc[i] !== ",") throw decodeErr("Expecting ',' delimiter", doc, i);
        const comma = i;
        i++;
        ws();
        if (doc[i] !== '"') throw doc[i] === "}" ? decodeErr("Illegal trailing comma before end of object", doc, comma) : decodeErr("Expecting property name enclosed in double quotes", doc, i);
      }
    }
    if (c === "[") {
      const a: Py[] = [];
      i++;
      ws();
      if (doc[i] === "]") { i++; return a; }
      for (;;) {
        a.push(valueAt());
        ws();
        if (doc[i] === "]") { i++; return a; }
        if (doc[i] !== ",") throw decodeErr("Expecting ',' delimiter", doc, i);
        const comma = i;
        i++;
        ws();
        if (doc[i] === "]") throw decodeErr("Illegal trailing comma before end of array", doc, comma);
      }
    }
    if (doc.startsWith("null", i)) { i += 4; return null; }
    if (doc.startsWith("true", i)) { i += 4; return true; }
    if (doc.startsWith("false", i)) { i += 5; return false; }
    if (doc.startsWith("NaN", i)) { i += 3; return new PyFloat(NaN); }
    if (doc.startsWith("Infinity", i)) { i += 8; return new PyFloat(Infinity); }
    if (doc.startsWith("-Infinity", i)) { i += 9; return new PyFloat(-Infinity); }
    NUMBER_RE.lastIndex = i;
    const m = NUMBER_RE.exec(doc);
    if (m) {
      i = NUMBER_RE.lastIndex;
      if (m[2] || m[3]) return new PyFloat(Number(m[0]));
      const n = Number(m[1]);
      return Number.isSafeInteger(n) ? n : BigInt(m[1]!);
    }
    throw decodeErr("Expecting value", doc, i);
  };
  const valueAt = (): Py => {
    if (i >= doc.length) throw decodeErr("Expecting value", doc, i);
    return value();
  };
  ws();
  const v = valueAt();
  ws();
  if (i !== doc.length) throw decodeErr("Extra data", doc, i);
  return v;
}
function encStr(s: string): string {
  // py_encode_basestring (ensure_ascii=False)
  return '"' + s.replace(/[\x00-\x1f\\"]/g, (c) => {
    switch (c) {
      case '"': return '\\"';
      case "\\": return "\\\\";
      case "\n": return "\\n";
      case "\r": return "\\r";
      case "\t": return "\\t";
      case "\b": return "\\b";
      case "\f": return "\\f";
      default: return "\\u" + c.charCodeAt(0).toString(16).padStart(4, "0");
    }
  }) + '"';
}
function encFloat(x: number): string {
  if (Number.isNaN(x)) return "NaN";
  if (x === Infinity) return "Infinity";
  if (x === -Infinity) return "-Infinity";
  return floatRepr(x);
}
function entries(v: Py): [string, Py][] {
  return v instanceof Map ? Array.from(v.entries()) : Object.entries(v);
}
/** json.dumps(v, ensure_ascii=False, indent=indent) with CPython's separators. Maps and plain objects are dicts. */
export function dumps(v: Py, indent?: number): string {
  const nl = indent !== undefined;
  const item = nl ? "," : ", ";
  const enc = (x: Py, level: number): string => {
    if (x === null || x === undefined) return "null";
    if (x === true) return "true";
    if (x === false) return "false";
    if (typeof x === "string") return encStr(x);
    if (typeof x === "number") return Number.isInteger(x) ? str(x) : encFloat(x);
    if (typeof x === "bigint") return x.toString();
    if (x instanceof PyFloat) return encFloat(x.v);
    const inner = nl ? "\n" + " ".repeat(indent * (level + 1)) : "";
    const close = nl ? "\n" + " ".repeat(indent * level) : "";
    if (Array.isArray(x)) {
      if (!x.length) return "[]";
      return "[" + inner + x.map((y) => enc(y, level + 1)).join(item + inner) + close + "]";
    }
    if (x instanceof Set) throw new TypeError("Object of type set is not JSON serializable");
    const es = entries(x);
    if (!es.length) return "{}";
    return "{" + inner + es.map(([k, y]) => encStr(k) + ": " + enc(y, level + 1)).join(item + inner) + close + "}";
  };
  return enc(v, 0);
}

// KERNEL: replace with src/py/html at merge
export function escape(s: string, quote = true): string {
  s = s.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
  return quote ? s.replaceAll('"', "&quot;").replaceAll("'", "&#x27;") : s;
}
const INVALID_CHARREFS: Record<number, string> = {
  0: "\ufffd", 13: "\r", 128: "\u20ac", 129: "\x81", 130: "\u201a", 131: "\u0192", 132: "\u201e", 133: "\u2026",
  134: "\u2020", 135: "\u2021", 136: "\u02c6", 137: "\u2030", 138: "\u0160", 139: "\u2039", 140: "\u0152", 141: "\x8d",
  142: "\u017d", 143: "\x8f", 144: "\x90", 145: "\u2018", 146: "\u2019", 147: "\u201c", 148: "\u201d", 149: "\u2022",
  150: "\u2013", 151: "\u2014", 152: "\u02dc", 153: "\u2122", 154: "\u0161", 155: "\u203a", 156: "\u0153", 157: "\x9d",
  158: "\u017e", 159: "\u0178",
};
function invalidCodepoint(n: number): boolean {
  return (n >= 0x1 && n <= 0x8) || (n >= 0xe && n <= 0x1f) || (n >= 0x7f && n <= 0x9f) || (n >= 0xfdd0 && n <= 0xfdef) ||
    n === 0xb || (n & 0xfffe) === 0xfffe;
}
/** The value of html.entities.html5[name] (name with or without its ";"), or undefined. */
export function html5(name: string): string | undefined {
  let idx = 0;
  let cur = htmlDecodeTree[0]!;
  let vlen = (cur & BinTrieFlags.VALUE_LENGTH) >> 14;
  for (let k = 0; k < name.length; k++) {
    idx = determineBranch(htmlDecodeTree, cur, idx + Math.max(1, vlen), name.charCodeAt(k));
    if (idx < 0) return undefined;
    cur = htmlDecodeTree[idx]!;
    vlen = (cur & BinTrieFlags.VALUE_LENGTH) >> 14;
  }
  if (vlen === 0 || !name.length) return undefined;
  return decodeHTML("&" + name);
}
const CHARREF = /&(#[0-9]+;?|#[xX][0-9a-fA-F]+;?|[^\t\n\f <&#;]{1,32};?)/gu;
/** html.unescape */
export function unescape(s: string): string {
  if (!s.includes("&")) return s;
  return s.replace(CHARREF, (_m, g: string) => {
    if (g[0] === "#") {
      const n = g[1] === "x" || g[1] === "X" ? parseInt(rstrip(g.slice(2), ";"), 16) : parseInt(rstrip(g.slice(1), ";"), 10);
      if (n in INVALID_CHARREFS) return INVALID_CHARREFS[n]!;
      if ((n >= 0xd800 && n <= 0xdfff) || n > 0x10ffff) return "\ufffd";
      if (invalidCodepoint(n)) return "";
      return String.fromCodePoint(n);
    }
    const v = html5(g);
    if (v !== undefined) return v;
    const cps = Array.from(g);
    for (let x = cps.length - 1; x > 1; x--) {
      const head = cps.slice(0, x).join("");
      const hv = html5(head);
      if (hv !== undefined) return hv + cps.slice(x).join("");
    }
    return "&" + g;
  });
}

// KERNEL: replace with src/py/path at merge
export function pjoin(a: string, ...ps: string[]): string {
  let path = a;
  for (const b of ps) {
    if (b.startsWith("/")) path = b;
    else if (!path || path.endsWith("/")) path += b;
    else path += "/" + b;
  }
  return path;
}
export function normpath(path: string): string {
  if (!path) return ".";
  let initial = path.startsWith("/") ? 1 : 0;
  if (initial && path.startsWith("//") && !path.startsWith("///")) initial = 2;
  const out: string[] = [];
  for (const comp of path.split("/")) {
    if (comp === "" || comp === ".") continue;
    if (comp !== ".." || (!initial && !out.length) || (out.length && out[out.length - 1] === "..")) out.push(comp);
    else if (out.length) out.pop();
  }
  const p = "/".repeat(initial) + out.join("/");
  return p || ".";
}
export function abspath(p: string): string {
  return normpath(p.startsWith("/") ? p : pjoin(process.cwd(), p));
}
export function relpath(path: string, start = "."): string {
  const s = abspath(start).split("/").filter(Boolean);
  const p = abspath(path).split("/").filter(Boolean);
  let i = 0;
  while (i < s.length && i < p.length && s[i] === p[i]) i++;
  const parts = [...Array<string>(s.length - i).fill(".."), ...p.slice(i)];
  return parts.length ? parts.join("/") : ".";
}
export function dirname(p: string): string {
  const i = p.lastIndexOf("/") + 1;
  let head = p.slice(0, i);
  if (head && head !== "/".repeat(head.length)) head = rstrip(head, "/");
  return head;
}
export function basename(p: string): string {
  return p.slice(p.lastIndexOf("/") + 1);
}
export function splitext(p: string): [string, string] {
  const sep = p.lastIndexOf("/");
  const dot = p.lastIndexOf(".");
  if (dot > sep) {
    let f = sep + 1;
    while (f < dot) {
      if (p[f] !== ".") return [p.slice(0, dot), p.slice(dot)];
      f++;
    }
  }
  return [p, ""];
}
export function expanduser(): string {
  return process.env.HOME ?? "";
}

// KERNEL: replace with src/py/io at merge
export function isfile(p: string): boolean {
  try { return statSync(p).isFile(); } catch { return false; }
}
export function isdir(p: string): boolean {
  try { return statSync(p).isDirectory(); } catch { return false; }
}
/** bytes.decode("utf-8", "replace"): the BOM is kept, invalid sequences become U+FFFD. */
export function decodeReplace(b: Uint8Array): string {
  return new TextDecoder("utf-8", { ignoreBOM: true }).decode(b);
}
/** open(p, encoding="utf-8").read(): text mode turns \r\n and \r into \n. */
export function readText(p: string, replace = false): string {
  const b = readFileSync(p);
  const s = replace ? decodeReplace(b) : new TextDecoder("utf-8", { ignoreBOM: true, fatal: true }).decode(b);
  return s.includes("\r") ? s.replace(/\r\n?/g, "\n") : s;
}
export function sha256(b: Uint8Array | string): string {
  return createHash("sha256").update(b).digest("hex");
}
export function utf8Len(s: string): number {
  return Buffer.byteLength(s, "utf8");
}
