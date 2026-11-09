// Python-semantics helpers the hotspot tools need before the shared kernel (src/py/*) is on this branch.
// Each export names the kernel module that replaces it at merge; behaviour is CPython 3.13's, tested against
// python3 -I in test/hotspots/py.test.ts.
import { readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { homedir, userInfo } from "node:os";
import { DecodingMode, EntityDecoder, htmlDecodeTree } from "entities/decode";

// ---------------------------------------------------------------------------------------------------------------
// Errors that stand for the Python exceptions the scripts let escape (a traceback and exit 1 in Python).
// KERNEL: replace with src/py/errors at merge
export class PyError extends Error {
  constructor(readonly pyType: string, message: string) { super(message); }
}
export const valueError = (m: string) => new PyError("ValueError", m);
export const typeError = (m: string) => new PyError("TypeError", m);
export const keyError = (m: string) => new PyError("KeyError", m);

// ---------------------------------------------------------------------------------------------------------------
// re: Python's \s, \d, \w on str patterns, as regex source fragments for the u flag.
// KERNEL: replace with src/py/re at merge
export const WS_CHARS = "\\t\\n\\v\\f\\r\\x1c-\\x1f \\x85\\xa0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000";
export const S = `[${WS_CHARS}]`;
export const D = "\\p{Nd}";
export const W = "[\\p{L}\\p{N}_]";
const WS_RE = new RegExp(`^${S}$`, "u");

// ---------------------------------------------------------------------------------------------------------------
// str: isspace, strip with or without a character set, split(), code point length / slice / compare.
// KERNEL: replace with src/py/str at merge
export const isSpace = (ch: string): boolean => WS_RE.test(ch);

export function strip(s: string, chars?: string): string { return rstrip(lstrip(s, chars), chars); }
export function lstrip(s: string, chars?: string): string {
  const cps = Array.from(s);
  const set = chars === undefined ? null : new Set(Array.from(chars));
  let i = 0;
  while (i < cps.length && (set ? set.has(cps[i]!) : isSpace(cps[i]!))) i++;
  return cps.slice(i).join("");
}
export function rstrip(s: string, chars?: string): string {
  const cps = Array.from(s);
  const set = chars === undefined ? null : new Set(Array.from(chars));
  let j = cps.length;
  while (j > 0 && (set ? set.has(cps[j - 1]!) : isSpace(cps[j - 1]!))) j--;
  return cps.slice(0, j).join("");
}
const WS_RUN = new RegExp(`${S}+`, "u");
/** str.split() with no separator: runs of whitespace, no empty strings. */
export const splitWs = (s: string): string[] => s.split(WS_RUN).filter((x) => x !== "");
const SURROGATE = /[\ud800-\udfff]/;
export const cpLen = (s: string): number => (SURROGATE.test(s) ? Array.from(s).length : s.length);
/** s[start:end] counted in code points, start and end >= 0. */
export function cpSlice(s: string, start: number, end?: number): string {
  if (!SURROGATE.test(s)) return s.slice(start, end);
  return Array.from(s).slice(start, end).join("");
}
/** Python's str ordering: by code point. */
export function cmpCodepoint(a: string, b: string): number {
  const x = Array.from(a), y = Array.from(b);
  for (let i = 0; i < Math.min(x.length, y.length); i++) {
    const d = x[i]!.codePointAt(0)! - y[i]!.codePointAt(0)!;
    if (d) return d;
  }
  return x.length - y.length;
}
/** format(s, "<width"): pad on the right to width code points. */
export const padEnd = (s: string, width: number): string => s + " ".repeat(Math.max(0, width - cpLen(s)));
/** format(s, ">width"). */
export const padStart = (s: string, width: number): string => " ".repeat(Math.max(0, width - cpLen(s))) + s;

// ---------------------------------------------------------------------------------------------------------------
// num: float(str), round(x, n), float repr, format(x, ".nf").
// KERNEL: replace with src/py/num at merge
const ND = /^\p{Nd}$/u;
function decimalValue(cp: number): number {
  const ch = String.fromCodePoint(cp);
  if (!ND.test(ch)) return -1;
  let start = cp;
  while (start > 0 && ND.test(String.fromCodePoint(start - 1))) start--;
  return (cp - start) % 10;
}

/** float(x) for a str (Unicode digits and whitespace, underscores between digits, inf / nan) or a number. */
export function pyFloat(v: string | number | null | undefined): number {
  if (typeof v === "number") return v;
  if (v === null || v === undefined) throw typeError("float() argument must be a string or a real number, not 'NoneType'");
  // _PyUnicode_TransformDecimalAndSpaceToASCII: ASCII kept as is, other whitespace to ' ', other decimals to ASCII;
  // then only ASCII whitespace is stripped.
  let t = "";
  for (const ch of v) {
    const cp = ch.codePointAt(0)!;
    if (cp < 127) t += ch;
    else if (isSpace(ch)) t += " ";
    else {
      const d = decimalValue(cp);
      if (d < 0) { t += "?"; break; }
      t += String(d);
    }
  }
  t = t.replace(/^[ \t\n\r\v\f]+/, "").replace(/[ \t\n\r\v\f]+$/, "");
  let u = "", prev = "";
  const bad = () => valueError(`could not convert string to float: ${repr(v)}`);
  for (const c of t) {
    if (c === "_") { if (!(prev >= "0" && prev <= "9")) throw bad(); }
    else { u += c; if (prev === "_" && !(c >= "0" && c <= "9")) throw bad(); }
    prev = c;
  }
  if (prev === "_") throw bad();
  const sp = /^([+-]?)(inf|infinity|nan)$/i.exec(u);
  if (sp) return sp[2]!.toLowerCase() === "nan" ? NaN : sp[1] === "-" ? -Infinity : Infinity;
  if (!/^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$/.test(u)) throw bad();
  return Number(u);
}

function decompose(x: number): { m: bigint; e: number } {
  const dv = new DataView(new ArrayBuffer(8));
  dv.setFloat64(0, x);
  const hi = dv.getUint32(0), lo = dv.getUint32(4);
  const eb = (hi >>> 20) & 0x7ff;
  let m = (BigInt(hi & 0xfffff) << 32n) | BigInt(lo);
  if (eb === 0) return { m, e: -1074 };
  m |= 1n << 52n;
  return { m, e: eb - 1075 };
}
/** |x| * 10**n rounded half-even on the exact binary value, as an integer. */
function scaledHalfEven(ax: number, n: number): bigint {
  const { m, e } = decompose(ax);
  const p = 10n ** BigInt(n);
  if (e >= 0) return (m << BigInt(e)) * p;
  const num = m * p, den = 1n << BigInt(-e);
  let q = num / den;
  const r2 = 2n * (num - q * den);
  if (r2 > den || (r2 === den && (q & 1n) === 1n)) q += 1n;
  return q;
}
function fixedDigits(q: bigint, n: number): string {
  const s = q.toString().padStart(n + 1, "0");
  return n ? `${s.slice(0, s.length - n)}.${s.slice(s.length - n)}` : s;
}
/** round(x, n) for a float x and n >= 0 (CPython: correctly rounded, ties to even). */
export function pyRound(x: number, n: number): number {
  if (!Number.isFinite(x) || x === 0) return x;
  const { e } = decompose(Math.abs(x));
  if (e >= 0) return x;
  const v = Number(fixedDigits(scaledHalfEven(Math.abs(x), n), n));
  return x < 0 ? -v : v;
}
/** format(x, ".nf"). */
export function formatF(x: number, n: number): string {
  if (Number.isNaN(x)) return "nan";
  if (!Number.isFinite(x)) return x < 0 ? "-inf" : "inf";
  const neg = x < 0 || Object.is(x, -0);
  return (neg ? "-" : "") + fixedDigits(x === 0 ? 0n : scaledHalfEven(Math.abs(x), n), n);
}
/** repr(float) / str(float). */
export function floatRepr(x: number): string {
  if (Number.isNaN(x)) return "nan";
  if (x === Infinity) return "inf";
  if (x === -Infinity) return "-inf";
  if (x === 0) return Object.is(x, -0) ? "-0.0" : "0.0";
  const s = String(Math.abs(x));
  const ei = s.indexOf("e");
  const mant = ei >= 0 ? s.slice(0, ei) : s;
  const exp = ei >= 0 ? Number(s.slice(ei + 1)) : 0;
  const dot = mant.indexOf(".");
  const ip = dot >= 0 ? mant.slice(0, dot) : mant;
  let digits = ip + (dot >= 0 ? mant.slice(dot + 1) : "");
  let decpt = ip.length + exp;
  const lz = /^0*/.exec(digits)![0].length;
  digits = digits.slice(lz).replace(/0+$/, "");
  decpt -= lz;
  let body: string;
  if (decpt <= -4 || decpt > 16) {
    const e10 = decpt - 1;
    body = digits[0]! + (digits.length > 1 ? "." + digits.slice(1) : "") + "e" + (e10 < 0 ? "-" : "+") +
      String(Math.abs(e10)).padStart(2, "0");
  } else if (decpt <= 0) body = "0." + "0".repeat(-decpt) + digits;
  else if (decpt >= digits.length) body = digits + "0".repeat(decpt - digits.length) + ".0";
  else body = digits.slice(0, decpt) + "." + digits.slice(decpt);
  return (x < 0 ? "-" : "") + body;
}

// ---------------------------------------------------------------------------------------------------------------
// repr(str).
// KERNEL: replace with src/py/repr at merge
const NONPRINT = /^[\p{Cc}\p{Cf}\p{Cs}\p{Co}\p{Cn}\p{Zl}\p{Zp}\p{Zs}]$/u;
export function repr(s: string): string {
  const q = s.includes("'") && !s.includes('"') ? '"' : "'";
  let out = q;
  for (const ch of s) {
    const cp = ch.codePointAt(0)!;
    if (ch === q || ch === "\\") out += "\\" + ch;
    else if (ch === "\t") out += "\\t";
    else if (ch === "\n") out += "\\n";
    else if (ch === "\r") out += "\\r";
    else if (cp < 0x20 || cp === 0x7f) out += "\\x" + cp.toString(16).padStart(2, "0");
    else if (cp < 0x7f || ch === " ") out += ch;
    else if (!NONPRINT.test(ch)) out += ch;
    else if (cp <= 0xff) out += "\\x" + cp.toString(16).padStart(2, "0");
    else if (cp <= 0xffff) out += "\\u" + cp.toString(16).padStart(4, "0");
    else out += "\\U" + cp.toString(16).padStart(8, "0");
  }
  return out + q;
}

// ---------------------------------------------------------------------------------------------------------------
// html: html.unescape, and html.parser's attribute-value unescape.
// KERNEL: replace with src/py/html at merge
const INVALID_CHARREFS: Record<number, string> = {
  0x00: "�", 0x0d: "\r", 0x80: "€", 0x81: "\x81", 0x82: "‚", 0x83: "ƒ", 0x84: "„",
  0x85: "…", 0x86: "†", 0x87: "‡", 0x88: "ˆ", 0x89: "‰", 0x8a: "Š", 0x8b: "‹",
  0x8c: "Œ", 0x8d: "\x8d", 0x8e: "Ž", 0x8f: "\x8f", 0x90: "\x90", 0x91: "‘", 0x92: "’",
  0x93: "“", 0x94: "”", 0x95: "•", 0x96: "–", 0x97: "—", 0x98: "˜", 0x99: "™",
  0x9a: "š", 0x9b: "›", 0x9c: "œ", 0x9d: "\x9d", 0x9e: "ž", 0x9f: "Ÿ",
};
function invalidCodepoint(n: number): boolean {
  return (n >= 0x1 && n <= 0x8) || (n >= 0xe && n <= 0x1f) || (n >= 0x7f && n <= 0x9f) ||
    (n >= 0xfdd0 && n <= 0xfdef) || n === 0xb || (n & 0xfffe) === 0xfffe;
}
/** html.entities.html5[key] (key with or without its ';'), or undefined when key is not in the table. */
export function html5Get(key: string): string | undefined {
  if (!key) return undefined;
  let out = "", consumed = -1;
  const dec = new EntityDecoder(htmlDecodeTree, (cp, n) => { out += String.fromCodePoint(cp); consumed = n; });
  dec.startEntity(key.endsWith(";") ? DecodingMode.Strict : DecodingMode.Legacy);
  if (dec.write(key + "\u0000", 0) < 0) dec.end();
  return consumed === key.length + 1 ? out : undefined;
}
function replaceCharref(s: string): string {
  if (s[0] === "#") {
    const hex = s[1] === "x" || s[1] === "X";
    const digits = (hex ? s.slice(2) : s.slice(1)).replace(/;+$/, "");
    const big = BigInt(hex ? "0x" + digits : digits);
    const num = big > 0x10ffffn ? 0x110000 : Number(big);
    const inv = INVALID_CHARREFS[num];
    if (inv !== undefined) return inv;
    if ((num >= 0xd800 && num <= 0xdfff) || num > 0x10ffff) return "�";
    if (invalidCodepoint(num)) return "";
    return String.fromCodePoint(num);
  }
  const whole = html5Get(s);
  if (whole !== undefined) return whole;
  const cps = Array.from(s);
  for (let x = cps.length - 1; x > 1; x--) {
    const v = html5Get(cps.slice(0, x).join(""));
    if (v !== undefined) return v + cps.slice(x).join("");
  }
  return "&" + s;
}
const CHARREF = /&(#[0-9]+;?|#[xX][0-9a-fA-F]+;?|[^\t\n\f <&#;]{1,32};?)/gu;
export function unescape(s: string): string {
  if (!s.includes("&")) return s;
  return s.replace(CHARREF, (_m, g: string) => replaceCharref(g));
}
const ATTR_CHARREF = /&(#[0-9]+|#[xX][0-9a-fA-F]+|[a-zA-Z][a-zA-Z0-9]*)[;=]?/g;
/** html.parser._unescape_attrvalue (CPython 3.13.6+). */
export function unescapeAttr(s: string): string {
  return s.replace(ATTR_CHARREF, (ref) => {
    if (ref.startsWith("&#")) return unescape(ref);
    if (!ref.endsWith("=") && html5Get(ref.slice(1)) !== undefined) return unescape(ref);
    return ref;
  });
}

// ---------------------------------------------------------------------------------------------------------------
// json: a lossless load (ordered dicts, int vs float, NaN / Infinity, last value wins at the first position) and
// dumps with Python's indent, separators and ensure_ascii.
// KERNEL: replace with src/py/json at merge
export class PyFloat {
  constructor(readonly value: number) {}
}
export type PyValue = null | boolean | string | bigint | PyFloat | PyValue[] | PyDict;
export type PyDict = Map<string, PyValue>;
export const isDict = (v: PyValue | undefined): v is PyDict => v instanceof Map;
export const isList = (v: PyValue | undefined): v is PyValue[] => Array.isArray(v);

const JSON_WS = /[ \t\n\r]*/y;
const NUMBER = /(-?(?:0|[1-9][0-9]*))(\.[0-9]+)?([eE][-+]?[0-9]+)?/y;
function ws(s: string, i: number): number { JSON_WS.lastIndex = i; JSON_WS.exec(s); return JSON_WS.lastIndex; }
const jerr = (m: string, i: number) => new PyError("json.decoder.JSONDecodeError", `${m}: char ${i}`);

function scanString(s: string, i: number): [string, number] {
  let out = "";
  for (;;) {
    if (i >= s.length) throw jerr("Unterminated string starting at", i);
    const c = s[i]!;
    if (c === '"') return [out, i + 1];
    if (c === "\\") {
      const e = s[i + 1];
      const simple: Record<string, string> = { '"': '"', "\\": "\\", "/": "/", b: "\b", f: "\f", n: "\n", r: "\r", t: "\t" };
      if (e === undefined) throw jerr("Unterminated string starting at", i);
      if (e === "u") {
        const h = s.slice(i + 2, i + 6);
        if (!/^[0-9a-fA-F]{4}$/.test(h)) throw jerr("Invalid \\uXXXX escape", i);
        out += String.fromCharCode(parseInt(h, 16));
        i += 6;
      } else if (simple[e] !== undefined) { out += simple[e]; i += 2; }
      else throw jerr("Invalid \\escape", i);
      continue;
    }
    if (c.charCodeAt(0) < 0x20) throw jerr("Invalid control character at", i);
    out += c;
    i++;
  }
}
function scanValue(s: string, i: number): [PyValue, number] {
  const c = s[i];
  if (c === undefined) throw jerr("Expecting value", i);
  if (c === '"') return scanString(s, i + 1);
  if (c === "{") {
    const d: PyDict = new Map();
    i = ws(s, i + 1);
    if (s[i] === "}") return [d, i + 1];
    for (;;) {
      if (s[i] !== '"') throw jerr("Expecting property name enclosed in double quotes", i);
      const [k, j] = scanString(s, i + 1);
      i = ws(s, j);
      if (s[i] !== ":") throw jerr("Expecting ':' delimiter", i);
      i = ws(s, i + 1);
      const [v, j2] = scanValue(s, i);
      d.set(k, v);
      i = ws(s, j2);
      if (s[i] === "}") return [d, i + 1];
      if (s[i] !== ",") throw jerr("Expecting ',' delimiter", i);
      i = ws(s, i + 1);
    }
  }
  if (c === "[") {
    const a: PyValue[] = [];
    i = ws(s, i + 1);
    if (s[i] === "]") return [a, i + 1];
    for (;;) {
      const [v, j] = scanValue(s, i);
      a.push(v);
      i = ws(s, j);
      if (s[i] === "]") return [a, i + 1];
      if (s[i] !== ",") throw jerr("Expecting ',' delimiter", i);
      i = ws(s, i + 1);
    }
  }
  if (c === "n" && s.startsWith("null", i)) return [null, i + 4];
  if (c === "t" && s.startsWith("true", i)) return [true, i + 4];
  if (c === "f" && s.startsWith("false", i)) return [false, i + 5];
  NUMBER.lastIndex = i;
  const m = NUMBER.exec(s);
  if (m) {
    const end = NUMBER.lastIndex;
    return [m[2] !== undefined || m[3] !== undefined ? new PyFloat(Number(m[0])) : BigInt(m[1]!), end];
  }
  if (c === "N" && s.startsWith("NaN", i)) return [new PyFloat(NaN), i + 3];
  if (c === "I" && s.startsWith("Infinity", i)) return [new PyFloat(Infinity), i + 8];
  if (c === "-" && s.startsWith("-Infinity", i)) return [new PyFloat(-Infinity), i + 9];
  throw jerr("Expecting value", i);
}
/** json.loads(s). */
export function jsonLoads(s: string): PyValue {
  if (s.startsWith("﻿")) throw jerr("Unexpected UTF-8 BOM (decode using utf-8-sig)", 0);
  const [v, end] = scanValue(s, ws(s, 0));
  if (ws(s, end) !== s.length) throw jerr("Extra data", end);
  return v;
}
function encodeString(s: string, ensureAscii: boolean): string {
  let out = '"';
  for (let i = 0; i < s.length; i++) {
    const c = s[i]!, n = c.charCodeAt(0);
    if (c === '"') out += '\\"';
    else if (c === "\\") out += "\\\\";
    else if (c === "\n") out += "\\n";
    else if (c === "\r") out += "\\r";
    else if (c === "\t") out += "\\t";
    else if (c === "\b") out += "\\b";
    else if (c === "\f") out += "\\f";
    else if (n < 0x20 || (ensureAscii && n > 0x7e)) out += "\\u" + n.toString(16).padStart(4, "0");
    else out += c;
  }
  return out + '"';
}
function encodeFloat(x: number): string {
  if (Number.isNaN(x)) return "NaN";
  if (x === Infinity) return "Infinity";
  if (x === -Infinity) return "-Infinity";
  return floatRepr(x);
}
/** json.dumps(v, indent=indent, ensure_ascii=ensureAscii) with the default separators. */
export function jsonDumps(v: PyValue, opts: { indent?: number | null; ensureAscii?: boolean } = {}): string {
  const indent = opts.indent ?? null, ascii = opts.ensureAscii ?? true;
  const itemSep = indent === null ? ", " : ",";
  const enc = (x: PyValue, level: number): string => {
    if (x === null) return "null";
    if (x === true) return "true";
    if (x === false) return "false";
    if (typeof x === "string") return encodeString(x, ascii);
    if (typeof x === "bigint") return x.toString();
    if (x instanceof PyFloat) return encodeFloat(x.value);
    const nl = indent === null ? "" : "\n" + " ".repeat(indent * (level + 1));
    const close = indent === null ? "" : "\n" + " ".repeat(indent * level);
    if (Array.isArray(x)) {
      if (!x.length) return "[]";
      return "[" + nl + x.map((y) => enc(y, level + 1)).join(itemSep + nl) + close + "]";
    }
    if (!x.size) return "{}";
    const parts: string[] = [];
    for (const [k, y] of x) parts.push(encodeString(k, ascii) + ": " + enc(y, level + 1));
    return "{" + nl + parts.join(itemSep + nl) + close + "}";
  };
  return enc(v, 0);
}
/** bool(v). */
export function truthy(v: PyValue | undefined): boolean {
  if (v === null || v === undefined || v === false) return false;
  if (v === true) return true;
  if (typeof v === "string") return v.length > 0;
  if (typeof v === "bigint") return v !== 0n;
  if (v instanceof PyFloat) return v.value !== 0;
  if (Array.isArray(v)) return v.length > 0;
  return v.size > 0;
}
/** A JSON number (or bool) as a JS number, for arithmetic. */
export function num(v: PyValue | undefined): number {
  if (typeof v === "bigint") return Number(v);
  if (v instanceof PyFloat) return v.value;
  if (typeof v === "boolean") return v ? 1 : 0;
  throw typeError(`unsupported operand type: ${v === null ? "NoneType" : typeof v}`);
}
/** d[key] that raises KeyError like Python. */
export function getKey(d: PyValue | undefined, key: string): PyValue {
  if (!isDict(d)) throw typeError("not a dict");
  const v = d.get(key);
  if (v === undefined) throw keyError(repr(key));
  return v;
}
/** A value that must be a str (Python would raise TypeError on str methods otherwise). */
export function asStr(v: PyValue | undefined, what = "value"): string {
  if (typeof v !== "string") throw typeError(`${what} must be str`);
  return v;
}

// ---------------------------------------------------------------------------------------------------------------
// path (posixpath) and io.
// KERNEL: replace with src/py/path and src/py/io at merge
export function normpath(p: string): string {
  if (!p) return ".";
  let initial = p.startsWith("/") ? 1 : 0;
  if (initial && p.startsWith("//") && !p.startsWith("///")) initial = 2;
  const out: string[] = [];
  for (const c of p.split("/")) {
    if (c === "" || c === ".") continue;
    if (c !== ".." || (!initial && !out.length) || (out.length && out[out.length - 1] === "..")) out.push(c);
    else if (out.length) out.pop();
  }
  const r = "/".repeat(initial) + out.join("/");
  return r || ".";
}
export function join(a: string, ...rest: string[]): string {
  let path = a;
  for (const b of rest) {
    if (b.startsWith("/")) path = b;
    else if (!path || path.endsWith("/")) path += b;
    else path += "/" + b;
  }
  return path;
}
export const abspath = (p: string): string => normpath(p.startsWith("/") ? p : join(process.cwd(), p));
export const basename = (p: string): string => p.slice(p.lastIndexOf("/") + 1);
export function dirname(p: string): string {
  let head = p.slice(0, p.lastIndexOf("/") + 1);
  if (head && head !== "/".repeat(head.length)) head = head.replace(/\/+$/, "");
  return head;
}
export function splitext(p: string): [string, string] {
  const sep = p.lastIndexOf("/"), dot = p.lastIndexOf(".");
  if (dot > sep) {
    for (let f = sep + 1; f < dot; f++) if (p[f] !== ".") return [p.slice(0, dot), p.slice(dot)];
  }
  return [p, ""];
}
export function expanduser(p: string): string {
  if (!p.startsWith("~")) return p;
  let i = p.indexOf("/", 1);
  if (i < 0) i = p.length;
  let home: string;
  if (i === 1) home = process.env.HOME ?? homedir();
  else {
    const name = p.slice(1, i);
    let me = "";
    try { me = userInfo().username; } catch { /* no passwd entry */ }
    if (name !== me) return p;
    home = homedir();
  }
  home = home.replace(/\/+$/, "");
  return home + p.slice(i) || "/";
}
function stat(p: string) { try { return statSync(p); } catch { return null; } }
export const isFile = (p: string): boolean => stat(p)?.isFile() ?? false;
export const isDir = (p: string): boolean => stat(p)?.isDirectory() ?? false;
export const exists = (p: string): boolean => stat(p) !== null;
export const listdir = (p: string): string[] => readdirSync(p);
/** glob.glob(join(dir, "*" + suffix)) sorted: entries not starting with a dot whose name ends with suffix. */
export function globSuffix(dir: string, suffix: string): string[] {
  let names: string[];
  try { names = readdirSync(dir); } catch { return []; }
  return names.filter((n) => !n.startsWith(".") && n.endsWith(suffix)).map((n) => join(dir, n))
    .sort(cmpCodepoint);
}
const UTF8 = new TextDecoder("utf-8", { fatal: true, ignoreBOM: true });
/** open(p, encoding="utf-8").read(): strict UTF-8, BOM kept, universal newlines. */
export function readText(p: string): string {
  let bytes: Uint8Array;
  try { bytes = readFileSync(p); }
  catch (e) { throw new PyError("FileNotFoundError", `[Errno 2] No such file or directory: ${repr(p)} (${(e as Error).message})`); }
  let s: string;
  try { s = UTF8.decode(bytes); } catch { throw new PyError("UnicodeDecodeError", `'utf-8' codec can't decode ${repr(p)}`); }
  return s.replace(/\r\n?/g, "\n");
}
/** open(p, "w", encoding="utf-8").write(s). */
export function writeText(p: string, s: string): void {
  if (/\p{Cs}/u.test(s)) throw new PyError("UnicodeEncodeError", "'utf-8' codec can't encode surrogates");
  writeFileSync(p, s, "utf8");
}
