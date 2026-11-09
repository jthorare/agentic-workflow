// Python semantics the linter needs on top of the kernel (src/py): dict access, ==, iteration, str(), the content-tree
// walkers and the small text helpers of lint_packet.py (plain, norm, unlinked_text, outside_fences, parse_attrs ...).
import * as py from "../py";
import { PyError, isOSError, isOsOrValueError } from "../py/errors";
import { PyFloat } from "../py/num";
import { pyStr } from "../py/repr";
import { type PyDict, type PyValue, pyTypeName, strictGet } from "../py/value";
import { ATTRS } from "./patterns";

const re = py.re;

export type { PyDict, PyValue };
export const isDict = (v: unknown): v is PyDict => v instanceof Map;
export const isList = (v: unknown): v is PyValue[] => Array.isArray(v);
export const truthy = py.truthy;
export const str = pyStr;
export const repr = py.repr;

/** d.get(k, dflt) where lint_packet.py guards the receiver (isinstance, or a value the linter built): dflt when d is not
 * a dict. Where lint_packet.py calls .get on a value read from a file unguarded, use dget. */
export function get(d: unknown, k: string, dflt: PyValue = null): PyValue {
  return d instanceof Map && d.has(k) ? (d.get(k) as PyValue) : dflt;
}

/** d.get(k, dflt) as lint_packet.py calls it unguarded: AttributeError, CPython's text, when d is not a dict (a JSON
 * file whose root, or a record in it, has the wrong type), so the run ends as the Python's does. */
export const dget = (d: unknown, k: string, dflt: PyValue = null): PyValue => strictGet(d, k, dflt) as PyValue;

/** d.keys(): AttributeError when d is not a dict. */
export function keys(d: unknown): PyValue[] {
  if (!(d instanceof Map)) throw new PyError("AttributeError", `'${typeName(d)}' object has no attribute 'keys'`);
  return [...d.keys()];
}

/** `k in d` for a dict. */
export const has = (d: unknown, k: string): boolean => d instanceof Map && d.has(k);

/** `a or b` */
export const or = <A, B>(a: A, b: B): A | B => (truthy(a) ? a : b);

const isNum = (v: unknown): v is number | bigint | boolean | PyFloat =>
  typeof v === "number" || typeof v === "bigint" || typeof v === "boolean" || v instanceof PyFloat;

function numVal(v: number | bigint | boolean | PyFloat): number | bigint {
  if (typeof v === "boolean") return v ? 1 : 0;
  if (v instanceof PyFloat) return v.value;
  return v;
}

/** Python ==: 1 == 1.0 == True, lists and dicts by value, str only with str. */
export function eq(a: unknown, b: unknown): boolean {
  if (a === undefined) a = null;
  if (b === undefined) b = null;
  if (isNum(a) && isNum(b)) {
    const x = numVal(a), y = numVal(b);
    if (typeof x === "bigint" || typeof y === "bigint") {
      if (typeof x === "number" && !Number.isInteger(x)) return false;
      if (typeof y === "number" && !Number.isInteger(y)) return false;
      return BigInt(x) === BigInt(y);
    }
    return x === y;
  }
  if (a === null || b === null) return a === b;
  if (typeof a === "string" || typeof b === "string") return a === b;
  if (Array.isArray(a) && Array.isArray(b)) return a.length === b.length && a.every((x, i) => eq(x, b[i]));
  if (a instanceof Map && b instanceof Map) {
    if (a.size !== b.size) return false;
    for (const [k, v] of a) if (!b.has(k) || !eq(v, b.get(k))) return false;
    return true;
  }
  return false;
}

/** `x in (a, b, c)` */
export const inTuple = (x: unknown, xs: readonly unknown[]): boolean => xs.some((y) => eq(x, y));

/** `for x in v`: a list's items, a dict's keys, a str's characters; TypeError for anything else. */
export function iter(v: unknown): PyValue[] {
  if (Array.isArray(v)) return v;
  if (v instanceof Map) return [...v.keys()];
  if (typeof v === "string") return Array.from(v);
  throw new PyError("TypeError", `'${typeName(v)}' object is not iterable`);
}

/** d.items() */
export function items(v: unknown): [string, PyValue][] {
  if (v instanceof Map) return [...v.entries()] as [string, PyValue][];
  throw new PyError("AttributeError", `'${typeName(v)}' object has no attribute 'items'`);
}

/** type(v).__name__ */
export const typeName = pyTypeName;

/** v.strip() on a value Python requires to be a str. */
export function strip(v: unknown): string {
  if (typeof v !== "string") throw new PyError("AttributeError", `'${typeName(v)}' object has no attribute 'strip'`);
  return py.str.strip(v);
}

/** " ".join(s.split()) */
export const squash = (s: string): string => py.str.split(s).join(" ");

/** Visible text of an inline-HTML string. */
export function plain(s: unknown): string {
  return py.html.unescape(re.sub("<[^>]+>", "", typeof s === "string" ? s : truthy(s) ? str(s) : ""));
}

/** re.sub(r"[^a-z0-9]+", "", (s or "").lower()) */
export function norm(s: unknown): string {
  const t = truthy(s) ? (s as string) : "";
  return re.sub("[^a-z0-9]+", "", py.lower(t));
}

/** Text of an inline-HTML string with links, code and already-flagged missing refs removed. */
export function unlinked_text(s: string): string {
  let t = re.sub(String.raw`<a\b[^>]*>.*?</a>`, " ", s || "", 0, re.S);
  t = re.sub(String.raw`<(code|pre)\b[^>]*>.*?</\1>`, " ", t, 0, re.S);
  t = re.sub(String.raw`<span class="ref-missing"[^>]*>.*?</span>`, " ", t, 0, re.S);
  return plain(t);
}

const FENCE = re.compile(String.raw`^\s*(\`{3,}|~{3,})`);

/** (line number, text) for every source line outside ``` fences. */
export function* outside_fences(lines: readonly string[]): Generator<[number, string]> {
  let fence: string | null = null;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]!;
    const m = FENCE.match(line);
    if (m) {
      const g = m.group(1)!;
      if (fence === null) fence = g[0]!.repeat(py.cpLen(g));
      else if (g.startsWith(fence)) fence = null;
      continue;
    }
    if (fence === null) yield [i + 1, line];
  }
}

export type Attrs = Map<string, string | true>;

/** parse_attrs(text) -> [text without the {attrs}, attrs] */
export function parse_attrs(text: string): [string, Attrs] {
  const attrs: Attrs = new Map();
  const m = ATTRS.search(text);
  if (!m) return [text, attrs];
  for (let part of py.str.split(m.group(1)!, ";")) {
    part = py.str.strip(part);
    if (part.startsWith("#")) attrs.set("id", py.str.strip(part.slice(1)));
    else if (part.includes(":")) {
      const [k, v] = py.str.split(part, ":", 1) as [string, string];
      attrs.set(py.lower(py.str.strip(k)), py.str.strip(v));
    } else if (part) attrs.set(py.lower(part), true);
  }
  return [py.str.rstrip(text.slice(0, m.start())), attrs];
}

/** split_marker(arg) -> [key, title] */
export function split_marker(arg: string): [string, string] {
  const m = re.match(String.raw`\s*(.*?)\s*(?:·|\|)\s*(.*)$`, arg);
  return m ? [py.str.strip(m.group(1)!), py.str.strip(m.group(2)!)] : [py.str.strip(arg), ""];
}

export type PathEl = readonly [PyDict, string];

/** (path, parent dict, key, value) for every string in a content tree (strings directly in lists are not visited). */
export function* walk(node: unknown, path: readonly PathEl[] = []): Generator<[readonly PathEl[], PyDict, string, string]> {
  if (node instanceof Map) {
    for (const [k, v] of node as PyDict) {
      if (typeof v === "string") yield [path, node as PyDict, k, v];
      else yield* walk(v, [...path, [node as PyDict, k]]);
    }
  } else if (Array.isArray(node)) {
    for (const x of node) yield* walk(x, path);
  }
}

/** Every dict in a content tree, preorder. */
export function* nodes(node: unknown): Generator<PyDict> {
  if (node instanceof Map) {
    yield node as PyDict;
    for (const v of node.values()) yield* nodes(v);
  } else if (Array.isArray(node)) {
    for (const x of node) yield* nodes(x);
  }
}

/** A reader's name for the node that holds a string. */
export function kind_of(parent: PyDict, k: string): PyValue {
  const t = get(parent, "type");
  if (truthy(t)) return t === "p" ? "paragraph" : t;
  if (k === "label" && parent.has("tone") && !parent.has("x")) return "legend item";
  if (parent.has("x") && parent.has("label")) return "hotspot";
  if (parent.has("n") && parent.has("bodyHtml")) return "PR card";
  if (parent.has("ms")) return "PR group";
  if (parent.has("rec")) return "option";
  if (k === "title" && parent.has("blocks")) return "card";
  if (parent.has("cells") || parent.has("align")) return "table cell";
  return "item";
}

/** where.startswith("PR") for a kind_of value. */
export const startsPR = (kind: PyValue): boolean => typeof kind === "string" && kind.startsWith("PR");

/** bytes.decode("utf-8", "surrogateescape"): an undecodable byte b becomes U+DC00+b. */
export function decodeSurrogateEscape(b: Uint8Array): string {
  try {
    return py.io.decodeUtf8(b, "strict");
  } catch {
    let out = "";
    let i = 0;
    const n = b.length;
    while (i < n) {
      const c = b[i]!;
      let need = 0, cp = 0, lo = 0x80, hi = 0xbf;
      if (c < 0x80) { out += String.fromCharCode(c); i++; continue; }
      if (c >= 0xc2 && c <= 0xdf) { need = 1; cp = c & 0x1f; }
      else if (c >= 0xe0 && c <= 0xef) { need = 2; cp = c & 0x0f; if (c === 0xe0) lo = 0xa0; if (c === 0xed) hi = 0x9f; }
      else if (c >= 0xf0 && c <= 0xf4) { need = 3; cp = c & 0x07; if (c === 0xf0) lo = 0x90; if (c === 0xf4) hi = 0x8f; }
      else { out += String.fromCharCode(0xdc00 + c); i++; continue; }
      let ok = i + need <= n - 1;
      if (ok) {
        for (let k = 1; k <= need; k++) {
          const d = b[i + k]!;
          const a = k === 1 ? lo : 0x80, z = k === 1 ? hi : 0xbf;
          if (d < a || d > z) { ok = false; break; }
          cp = (cp << 6) | (d & 0x3f);
        }
      }
      if (ok) { out += String.fromCodePoint(cp); i += need + 1; }
      else { out += String.fromCharCode(0xdc00 + c); i++; }
    }
    return out;
  }
}

const ERRNO: Record<string, [number, string, string]> = {
  ENOENT: [2, "No such file or directory", "FileNotFoundError"],
  EACCES: [13, "Permission denied", "PermissionError"],
  EPERM: [1, "Operation not permitted", "PermissionError"],
  EISDIR: [21, "Is a directory", "IsADirectoryError"],
  ENOTDIR: [20, "Not a directory", "NotADirectoryError"],
  EEXIST: [17, "File exists", "FileExistsError"],
  ELOOP: [62, "Too many levels of symbolic links", "OSError"],
  ENAMETOOLONG: [63, "File name too long", "OSError"],
};

/** [type(e).__name__, str(e)] as Python would print an exception the TypeScript raised for the same cause. */
export function pyException(e: unknown): [string, string] {
  if (e instanceof PyError) return [e.type === "re.error" ? "PatternError" : e.type, e.message];
  const sys = e as { code?: string; path?: string; message?: string; name?: string };
  if (sys && typeof sys.code === "string" && ERRNO[sys.code]) {
    const [n, text, name] = ERRNO[sys.code]!;
    return [name, sys.path !== undefined ? `[Errno ${n}] ${text}: ${py.strRepr(sys.path)}` : `[Errno ${n}] ${text}`];
  }
  if (e instanceof Error) return [e.name || "Error", e.message];
  return ["Exception", String(e)];
}

export { isOSError, isOsOrValueError };

/** An exception lint_packet.py raises too, at the same place and for the same input: a kernel PyError (CPython's type
 * and text) or a file-system error. It ends the run as it ends lint_packet.py's (a traceback, exit 1, no report and no
 * lint.json). Any other exception is a defect of this linter, which rule isolation reports as RULE/check-failed. */
export const mirrorsPython = (e: unknown): boolean => e instanceof PyError || isOSError(e);

/** str(e) for an OSError / ValueError the linter reports in a message. */
export const pyErrText = (e: unknown): string => pyException(e)[1];
