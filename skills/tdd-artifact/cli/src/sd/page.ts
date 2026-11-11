// build_doc.py:487-490 and 496-516: STATUS_WORDS, GENERIC_TAILS, page_title, load_json, on the kernel.
import * as py from "../py";
import { PyError } from "../py/errors";
import { JSONDecodeError } from "../py/json";
import type { PyValue } from "../py/value";
import { RecursionError } from "./errors";

const re = py.re;
const { split, strip } = py.str;

/** STATUS_WORDS: a status word -> its tone (a Python dict; `.get(k) ?? null` is `dict.get(k)`). */
export const STATUS_WORDS: ReadonlyMap<string, string> = new Map([
  ["ruled", "ruled"], ["binding", "ruled"], ["accepted", "ruled"], ["merged", "built"], ["built", "built"], ["done", "built"],
  ["shipped", "built"], ["landed", "built"], ["live", "built"], ["draft", "proposed"], ["proposed", "proposed"], ["open", "proposed"],
  ["wip", "proposed"], ["planned", "proposed"], ["held", "held"], ["blocked", "held"], ["rejected", "held"], ["closed", "held"],
  ["default", "default"], ["pending", "default"], ["backlog", "default"],
]);

/** GENERIC_TAILS: [pattern, replacement], tried in order under re.I on a title longer than four words. */
export const GENERIC_TAILS: readonly (readonly [string, string])[] = [
  [String.raw`\s+technical\s+spec(ification)?$`, " Spec"], [String.raw`\s+technical\s+design(\s+doc(ument)?)?$`, " TDD"],
  [String.raw`\s+design\s+doc(ument)?$`, " Design Doc"], [String.raw`\s+system\s+design$`, " Design"],
];

const TITLE_CUT = re.compile(String.raw`\s+[—–|:]\s+|:\s+`);

/** page_title(h1): the browser-tab title, at most four words, before any " — ", " | " or ": ". */
export function pageTitle(h1: string): string {
  let t = strip(TITLE_CUT.split(h1, 1)[0]!);
  if (split(t).length > 4) {
    for (const [pat, rep] of GENERIC_TAILS) {
      const t2 = re.sub(pat, rep, t, 0, re.I);
      if (t2 !== t) {
        t = t2;
        break;
      }
    }
  }
  const words = split(t);
  return words.length > 4 ? words.slice(0, 4).join(" ") : t;
}

/** sys.exit(message): the caller prints `message` to stderr and exits with `code` (1). */
export class SystemExit extends Error {
  readonly code: number;
  constructor(code: number, message: string) {
    super(message);
    this.code = code;
    this.name = "SystemExit";
  }
}

// CPython's OSError text for what open() can meet: "[Errno N] strerror: 'path'".
const STRERROR: Record<string, string> = {
  ENOENT: "No such file or directory", EISDIR: "Is a directory", EACCES: "Permission denied", ENOTDIR: "Not a directory",
  ELOOP: "Too many levels of symbolic links", ENAMETOOLONG: "File name too long", EPERM: "Operation not permitted",
  EMFILE: "Too many open files", ENXIO: "Device not configured", EIO: "Input/output error",
};

/** str(e) for an exception open(path).read() / json.loads raise that load_json catches (OSError, ValueError); null
 * for anything else (which propagates, as it does in Python). */
function caughtText(e: unknown, path: string): string | null {
  if (e instanceof PyError) {
    const value = ["ValueError", "UnicodeDecodeError", "JSONDecodeError"].includes(e.type);
    return value || e.type === "OSError" || e.type === "FileNotFoundError" ? e.message : null;
  }
  const code = (e as { code?: unknown; errno?: unknown } | null)?.code;
  const errno = (e as { errno?: unknown } | null)?.errno;
  if (typeof code === "string" && typeof errno === "number" && STRERROR[code])
    return `[Errno ${Math.abs(errno)}] ${STRERROR[code]}: ${py.strRepr(path)}`;
  if (typeof code === "string" && typeof errno === "number") return `[Errno ${Math.abs(errno)}] ${(e as Error).message}: ${py.strRepr(path)}`;
  return null;
}

/** sys.int_info.default_max_str_digits: json.loads refuses a longer int literal (CPython counts the digits, not the sign). */
export const INT_MAX_STR_DIGITS = 4300;
/** The deepest container nesting json.loads accepts in CPython 3.13: its C scanner enters each array or object through
 * Py_EnterRecursiveCall, and 3.13 counts C recursion only (limit 10,000), so this is the same at any Python call depth.
 * Measured: 9,998 levels load, 9,999 raise RecursionError (test/sd/files.test.ts re-checks both against python3). */
export const JSON_MAX_DEPTH = 9998;

const isDigit = (c: number) => c >= 48 && c <= 57;

/** The first over-long int literal and the first over-deep container in s, as code point positions: a JSON lexer
 * (strings, numbers, brackets) that agrees with CPython's scanner on every prefix the scanner accepts. */
function jsonLimits(s: string): { int: [number, number] | null; deep: [number, "array" | "object"] | null } {
  let int: [number, number] | null = null;
  let deep: [number, "array" | "object"] | null = null;
  let depth = 0;
  const n = s.length;
  let i = 0;
  while (i < n && !(int && deep)) {
    const c = s.charCodeAt(i);
    if (c === 34) { // a string: up to the next unescaped quote
      i++;
      while (i < n && s.charCodeAt(i) !== 34) i += s.charCodeAt(i) === 92 ? 2 : 1;
      i++;
    } else if (c === 91 || c === 123) {
      depth++;
      if (depth > JSON_MAX_DEPTH && !deep) deep = [i, c === 91 ? "array" : "object"];
      i++;
    } else if (c === 93 || c === 125) {
      depth--;
      i++;
    } else if (c === 45 || isDigit(c)) { // -?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][-+]?[0-9]+)?
      const start = i;
      if (c === 45) i++;
      if (i >= n || !isDigit(s.charCodeAt(i))) continue;
      const d0 = i;
      if (s.charCodeAt(i) === 48) i++;
      else while (i < n && isDigit(s.charCodeAt(i))) i++;
      const digits = i - d0;
      let real = false;
      if (s.charCodeAt(i) === 46 && isDigit(s.charCodeAt(i + 1))) {
        real = true;
        i += 2;
        while (i < n && isDigit(s.charCodeAt(i))) i++;
      }
      const e = s.charCodeAt(i);
      if (e === 101 || e === 69) {
        let j = i + 1;
        if (s.charCodeAt(j) === 43 || s.charCodeAt(j) === 45) j++;
        if (isDigit(s.charCodeAt(j))) {
          real = true;
          while (j < n && isDigit(s.charCodeAt(j))) j++;
          i = j;
        }
      }
      if (!real && digits > INT_MAX_STR_DIGITS && !int) int = [start, digits];
    } else i++;
  }
  const cp = (unit: number) => py.cpLen(s.slice(0, unit));
  return { int: int && [cp(int[0]), int[1]], deep: deep && [cp(deep[0]), deep[1]] };
}

/** json.loads(s) with CPython's two limits the kernel's loads leaves out: an int literal over 4,300 digits is
 * ValueError, a container deeper than JSON_MAX_DEPTH is RecursionError. Whichever the scanner meets first wins,
 * a JSONDecodeError included. */
export function jsonLoads(s: string): PyValue {
  let value: PyValue = null;
  let failure: unknown = null;
  let at = Infinity;
  try {
    value = py.json.loads(s);
  } catch (e) {
    if (e instanceof JSONDecodeError) at = e.pos;
    else if (!(e instanceof RangeError)) throw e; // a JS stack overflow on nesting the scan below must account for
    failure = e;
  }
  const { int, deep } = jsonLimits(s);
  const intAt = int && int[0] < at ? int[0] : Infinity;
  const deepAt = deep && deep[0] < at ? deep[0] : Infinity;
  if (intAt < deepAt)
    throw new PyError("ValueError", `Exceeds the limit (${INT_MAX_STR_DIGITS} digits) for integer string conversion: value has ${int![1]} digits; use sys.set_int_max_str_digits() to increase the limit`);
  if (deepAt < Infinity) throw new RecursionError(`maximum recursion depth exceeded while decoding a JSON ${deep![1]} from a unicode string`);
  if (failure) throw failure;
  return value;
}

/** load_json(path, what): the parsed file (kernel values: dicts are Maps, floats PyFloat), null for an empty path;
 * an unreadable or invalid file throws SystemExit(1, "{what} {path}: {error}") as build_doc's sys.exit does. A
 * RecursionError (nesting past JSON_MAX_DEPTH) is not caught there, so it propagates here too. */
export function loadJson(path: string | null | undefined, what: string): PyValue {
  if (!path) return null;
  let text: string;
  try {
    if (path.includes("\0")) throw new PyError("ValueError", "embedded null byte");
    text = py.io.readText(path);
  } catch (e) {
    throw exit(e, what, path);
  }
  try {
    return jsonLoads(text);
  } catch (e) {
    throw exit(e, what, path);
  }
}

/** The SystemExit load_json turns an OSError or ValueError into; anything else unchanged. */
function exit(e: unknown, what: string, path: string): unknown {
  const text = caughtText(e, path);
  return text === null ? e : new SystemExit(1, `${what} ${path}: ${text}`);
}
