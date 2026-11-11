// Python's json module, lossless: loads() keeps key order (PyDict), int vs float (1.0 stays a PyFloat), big ints
// (bigint), NaN/Infinity, last-value-wins at the key's first position, and raises CPython's JSONDecodeError text;
// dumps() mirrors CPython's encoder byte for byte (indent, separators, ensure_ascii, sort_keys, default, allow_nan).
import { PyError } from "./errors";
import { PyFloat, floatRepr, fromBigInt } from "./num";
import { cmp } from "./str";
import type { PyDict, PyValue } from "./value";

export class JSONDecodeError extends PyError {
  readonly msg: string;
  readonly pos: number;
  readonly lineno: number;
  readonly colno: number;
  constructor(msg: string, doc: string, unitPos: number) {
    // CPython counts in code points
    const before = doc.slice(0, unitPos);
    const pos = [...before].length;
    const nl = before.lastIndexOf("\n");
    const lineno = before.split("\n").length;
    const colno = nl < 0 ? pos + 1 : [...before.slice(nl + 1)].length + 1;
    super("JSONDecodeError", `${msg}: line ${lineno} column ${colno} (char ${pos})`);
    this.msg = msg;
    this.pos = pos;
    this.lineno = lineno;
    this.colno = colno;
  }
}

const WS = (c: number) => c === 32 || c === 9 || c === 10 || c === 13;
const NUMBER = /(-?(?:0|[1-9][0-9]*))(\.[0-9]+)?([eE][-+]?[0-9]+)?/y;
const ESC: Record<string, string> = { '"': '"', "\\": "\\", "/": "/", b: "\b", f: "\f", n: "\n", r: "\r", t: "\t" };

class StopIteration {
  constructor(readonly pos: number) {}
}

/** json.loads(s): the Python value, with ints as number/bigint and floats as PyFloat. */
export function loads(s: string): PyValue {
  if (s.charCodeAt(0) === 0xfeff) throw new JSONDecodeError("Unexpected UTF-8 BOM (decode using utf-8-sig)", s, 0);
  const n = s.length;
  const skip = (i: number) => {
    while (i < n && WS(s.charCodeAt(i))) i++;
    return i;
  };

  function scanString(end: number): [string, number] {
    const begin = end - 1;
    let out = "";
    for (;;) {
      let j = end;
      while (j < n) {
        const c = s.charCodeAt(j);
        if (c === 34 || c === 92 || c < 0x20) break;
        j++;
      }
      if (j >= n) throw new JSONDecodeError("Unterminated string starting at", s, begin);
      out += s.slice(end, j);
      const c = s.charCodeAt(j);
      end = j + 1;
      if (c === 34) return [out, end];
      if (c !== 92) throw new JSONDecodeError("Invalid control character at", s, j);
      if (end >= n) throw new JSONDecodeError("Unterminated string starting at", s, begin);
      const e = s[end]!;
      if (e !== "u") {
        const r = ESC[e];
        if (r === undefined) throw new JSONDecodeError("Invalid \\escape", s, end - 1);
        out += r;
        end++;
        continue;
      }
      const hex4 = (k: number): number => {
        const h = s.slice(k, k + 4);
        if (h.length < 4 || !/^[0-9a-fA-F]{4}$/.test(h)) throw new JSONDecodeError("Invalid \\uXXXX escape", s, k - 1);
        return parseInt(h, 16);
      };
      let u = hex4(end + 1);
      end += 5;
      if (u >= 0xd800 && u <= 0xdbff && s[end] === "\\" && s[end + 1] === "u") {
        const u2 = hex4(end + 2);
        if (u2 >= 0xdc00 && u2 <= 0xdfff) {
          u = 0x10000 + (((u - 0xd800) << 10) | (u2 - 0xdc00));
          end += 6;
        }
      }
      out += String.fromCodePoint(u);
    }
  }

  function scanOnce(idx: number): [PyValue, number] {
    if (idx >= n) throw new StopIteration(idx);
    const c = s[idx]!;
    if (c === '"') return scanString(idx + 1);
    if (c === "{") return parseObject(idx + 1);
    if (c === "[") return parseArray(idx + 1);
    if (c === "n" && s.startsWith("null", idx)) return [null, idx + 4];
    if (c === "t" && s.startsWith("true", idx)) return [true, idx + 4];
    if (c === "f" && s.startsWith("false", idx)) return [false, idx + 5];
    NUMBER.lastIndex = idx;
    const m = NUMBER.exec(s);
    if (m) {
      const end = idx + m[0].length;
      if (m[2] !== undefined || m[3] !== undefined) return [new PyFloat(Number(m[0])), end];
      return [fromBigInt(BigInt(m[1]!)), end];
    }
    if (c === "N" && s.startsWith("NaN", idx)) return [new PyFloat(NaN), idx + 3];
    if (c === "I" && s.startsWith("Infinity", idx)) return [new PyFloat(Infinity), idx + 8];
    if (c === "-" && s.startsWith("-Infinity", idx)) return [new PyFloat(-Infinity), idx + 9];
    throw new StopIteration(idx);
  }

  function value(idx: number): [PyValue, number] {
    try {
      return scanOnce(idx);
    } catch (e) {
      if (e instanceof StopIteration) throw new JSONDecodeError("Expecting value", s, e.pos);
      throw e;
    }
  }

  function parseObject(end: number): [PyDict, number] {
    const obj: PyDict = new Map();
    let ch = s[end];
    if (ch !== '"') {
      end = skip(end);
      ch = s[end];
      if (ch === "}") return [obj, end + 1];
      if (ch !== '"') throw new JSONDecodeError("Expecting property name enclosed in double quotes", s, end);
    }
    end++;
    for (;;) {
      let key: string;
      [key, end] = scanString(end);
      if (s[end] !== ":") {
        end = skip(end);
        if (s[end] !== ":") throw new JSONDecodeError("Expecting ':' delimiter", s, end);
      }
      end = skip(end + 1);
      let v: PyValue;
      [v, end] = value(end);
      obj.set(key, v);
      end = skip(end);
      const next = s[end];
      end++;
      if (next === "}") break;
      if (next !== ",") throw new JSONDecodeError("Expecting ',' delimiter", s, end - 1);
      const comma = end - 1;
      end = skip(end);
      const q = s[end];
      end++;
      if (q !== '"') {
        if (q === "}") throw new JSONDecodeError("Illegal trailing comma before end of object", s, comma);
        throw new JSONDecodeError("Expecting property name enclosed in double quotes", s, end - 1);
      }
    }
    return [obj, end];
  }

  function parseArray(end: number): [PyValue[], number] {
    const arr: PyValue[] = [];
    end = skip(end);
    if (s[end] === "]") return [arr, end + 1];
    for (;;) {
      let v: PyValue;
      [v, end] = value(end);
      arr.push(v);
      end = skip(end);
      const next = s[end];
      end++;
      if (next === "]") break;
      if (next !== ",") throw new JSONDecodeError("Expecting ',' delimiter", s, end - 1);
      const comma = end - 1;
      end = skip(end);
      if (s[end] === "]") throw new JSONDecodeError("Illegal trailing comma before end of array", s, comma);
    }
    return [arr, end];
  }

  const [v, end0] = value(skip(0));
  const end = skip(end0);
  if (end !== n) throw new JSONDecodeError("Extra data", s, end);
  return v;
}

export interface DumpsOptions {
  /** None (compact), a number of spaces, or a string. */
  indent?: number | string | null;
  /** [item, key]; default [", ", ": "], or [",", ": "] when indent is set. */
  separators?: readonly [string, string];
  ensureAscii?: boolean;
  sortKeys?: boolean;
  allowNan?: boolean;
  /** Called for a value the encoder does not know; returns something it does (json.dumps(default=...)). */
  default?: (o: unknown) => unknown;
}

const NEEDS_ESC_ASCII = /[\x00-\x1f"\\\x7f-\uffff]/;
const NEEDS_ESC = /[\x00-\x1f"\\]/;
const SHORT: Record<number, string> = { 34: '\\"', 92: "\\\\", 10: "\\n", 13: "\\r", 9: "\\t", 8: "\\b", 12: "\\f" };

/** A str as CPython's encoder writes it (lowercase \u escapes; astral characters as surrogate pairs when ASCII). */
export function encodeString(s: string, ensureAscii = true): string {
  if (!(ensureAscii ? NEEDS_ESC_ASCII : NEEDS_ESC).test(s)) return `"${s}"`;
  let out = '"';
  for (let i = 0; i < s.length; i++) {
    const c = s.charCodeAt(i);
    const short = SHORT[c];
    if (short !== undefined) out += short;
    else if (c < 0x20 || (ensureAscii && c >= 0x7f)) out += "\\u" + c.toString(16).padStart(4, "0");
    else out += s[i];
  }
  return out + '"';
}

function typeName(o: unknown): string {
  if (o === undefined) return "undefined";
  if (typeof o === "function") return "function";
  if (typeof o === "symbol") return "symbol";
  if (o instanceof Set) return "set";
  const ctor = (o as { constructor?: { name?: string } }).constructor;
  return ctor?.name ?? typeof o;
}

/** json.dumps(obj, ...) with CPython's exact output. */
export function dumps(obj: unknown, opts: DumpsOptions = {}): string {
  const rawIndent = opts.indent ?? null;
  const indent = rawIndent === null ? null : typeof rawIndent === "string" ? rawIndent : " ".repeat(Math.max(0, rawIndent));
  const [itemSep, keySep] = opts.separators ?? (indent === null ? [", ", ": "] : [",", ": "]);
  const ascii = opts.ensureAscii ?? true;
  const allowNan = opts.allowNan ?? true;
  const sortKeys = opts.sortKeys ?? false;
  const markers = new Set<object>();
  const parts: string[] = [];

  const float = (x: number): string => {
    if (Number.isFinite(x)) return floatRepr(x);
    if (!allowNan) throw new PyError("ValueError", `Out of range float values are not JSON compliant: ${floatRepr(x)}`);
    return Number.isNaN(x) ? "NaN" : x > 0 ? "Infinity" : "-Infinity";
  };
  const number = (x: number): string => (Number.isInteger(x) ? BigInt(x).toString() : float(x));

  const keyString = (k: unknown): string => {
    if (typeof k === "string") return k;
    if (k instanceof PyFloat) return float(k.value);
    if (typeof k === "number") return number(k);
    if (typeof k === "bigint") return k.toString();
    if (k === true) return "true";
    if (k === false) return "false";
    if (k === null) return "null";
    throw new PyError("TypeError", `keys must be str, int, float, bool or None, not ${typeName(k)}`);
  };

  const enter = (o: object) => {
    if (markers.has(o)) throw new PyError("ValueError", "Circular reference detected");
    markers.add(o);
  };

  function encode(o: unknown, level: number): void {
    if (o === null) parts.push("null");
    else if (o === true) parts.push("true");
    else if (o === false) parts.push("false");
    else if (typeof o === "string") parts.push(encodeString(o, ascii));
    else if (typeof o === "number") parts.push(number(o));
    else if (typeof o === "bigint") parts.push(o.toString());
    else if (o instanceof PyFloat) parts.push(float(o.value));
    else if (Array.isArray(o)) encodeList(o, level);
    else if (o instanceof Map) encodeDict([...o.entries()], o, level);
    else if (typeof o === "object" && o !== null && (Object.getPrototypeOf(o) === Object.prototype || Object.getPrototypeOf(o) === null))
      encodeDict(Object.entries(o), o, level);
    else {
      if (!opts.default) throw new PyError("TypeError", `Object of type ${typeName(o)} is not JSON serializable`);
      if (typeof o === "object" && o !== null) enter(o);
      encode(opts.default(o), level);
      if (typeof o === "object" && o !== null) markers.delete(o);
    }
  }

  function encodeList(lst: unknown[], level: number): void {
    if (lst.length === 0) {
      parts.push("[]");
      return;
    }
    enter(lst);
    let sep = itemSep;
    let nl = "";
    parts.push("[");
    if (indent !== null) {
      level++;
      nl = "\n" + indent.repeat(level);
      sep = itemSep + nl;
      parts.push(nl);
    }
    lst.forEach((x, i) => {
      if (i) parts.push(sep);
      encode(x, level);
    });
    if (indent !== null) parts.push("\n" + indent.repeat(level - 1));
    parts.push("]");
    markers.delete(lst);
  }

  function encodeDict(entries: [unknown, unknown][], owner: object, level: number): void {
    if (entries.length === 0) {
      parts.push("{}");
      return;
    }
    enter(owner);
    let sep = itemSep;
    let nl = "";
    parts.push("{");
    if (indent !== null) {
      level++;
      nl = "\n" + indent.repeat(level);
      sep = itemSep + nl;
      parts.push(nl);
    }
    let items = entries.map(([k, v]) => [keyString(k), v] as [string, unknown]);
    if (sortKeys) items = items.sort((a, b) => cmp(a[0], b[0]));
    items.forEach(([k, v], i) => {
      if (i) parts.push(sep);
      parts.push(encodeString(k, ascii), keySep);
      encode(v, level);
    });
    if (indent !== null) parts.push("\n" + indent.repeat(level - 1));
    parts.push("}");
    markers.delete(owner);
  }

  encode(obj, 0);
  return parts.join("");
}
