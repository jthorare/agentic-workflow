// The Python value model the kernel passes around, and Python truthiness.
//   None -> null, bool -> boolean, str -> string, int -> number (integral, safe) or bigint, float -> PyFloat,
//   list -> array, dict -> PyDict (a Map, so integer-like keys keep their place: plain objects reorder them).
import { PyError } from "./errors";
import { PyFloat } from "./num";
import { repr } from "./repr";

export type PyDict = Map<string, PyValue>;
export type PyValue = null | boolean | string | number | bigint | PyFloat | PyValue[] | PyDict;

/** A dict from entries in order (later duplicates overwrite in place, as dict() does). */
export function pyDict(entries: Iterable<readonly [string, PyValue]> = []): PyDict {
  return new Map(entries);
}

/**
 * bool(v) in Python: None, False, 0, 0.0, "", [] and {} are false; NaN is true. JS gets [] and {} (and NaN) wrong,
 * so a ported `if x:` on a list, dict or string is `if (truthy(x))`.
 */
export function truthy(v: unknown): boolean {
  if (v === null || v === undefined) return false;
  switch (typeof v) {
    case "boolean":
      return v;
    case "number":
      return v !== 0; // NaN !== 0, so NaN stays true as in Python
    case "bigint":
      return v !== 0n;
    case "string":
      return v.length > 0;
    case "object":
      if (v instanceof PyFloat) return v.value !== 0;
      if (Array.isArray(v)) return v.length > 0;
      if (v instanceof Map || v instanceof Set) return v.size > 0;
      if (Object.getPrototypeOf(v) === Object.prototype || Object.getPrototypeOf(v) === null) return Object.keys(v).length > 0;
      return true;
    default:
      return true;
  }
}

export const isDict = (v: unknown): v is PyDict => v instanceof Map;
export const isList = (v: unknown): v is PyValue[] => Array.isArray(v);

/** `a or b` */
export function or<A, B>(a: A, b: B): A | B {
  return truthy(a) ? a : b;
}

/** type(v).__name__ for a Python value (NoneType, bool, int, float, str, list, dict). */
export function pyTypeName(v: unknown): string {
  if (v === null || v === undefined) return "NoneType";
  if (typeof v === "boolean") return "bool";
  if (typeof v === "number") return Number.isInteger(v) ? "int" : "float";
  if (typeof v === "bigint") return "int";
  if (typeof v === "string") return "str";
  if (v instanceof PyFloat) return "float";
  if (Array.isArray(v)) return "list";
  if (v instanceof Map) return "dict";
  return typeof v;
}

/** d.get(k, dflt) where Python calls .get on a value that may not be a dict (one read from a file): AttributeError,
 * CPython's text, when d is not a dict; otherwise the stored value even when it is None, or dflt when k is absent. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function strictGet(d: unknown, k: string, dflt: any = null): any {
  if (!(d instanceof Map)) throw new PyError("AttributeError", `'${pyTypeName(d)}' object has no attribute 'get'`);
  return d.has(k) ? d.get(k) : dflt;
}

/** d.get(k, dflt): the stored value even when it is None; dflt when the key is absent or d is not a dict. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function get(d: unknown, k: string, dflt: any = null): any {
  return d instanceof Map && d.has(k) ? d.get(k) : dflt;
}

/** d[k]: KeyError (repr of the key) when absent; TypeError when d is not a dict. */
export function getKey(d: unknown, k: string): PyValue {
  if (!(d instanceof Map)) throw new PyError("TypeError", `'${d === null || d === undefined ? "NoneType" : Array.isArray(d) ? "list" : typeof d}' object is not subscriptable by str`);
  if (!d.has(k)) throw new PyError("KeyError", repr(k));
  return d.get(k) as PyValue;
}

/** A value a str method is about to run on (Python raises AttributeError / TypeError otherwise). */
export function asStr(v: unknown, what = "value"): string {
  if (typeof v !== "string") throw new PyError("TypeError", `${what} must be str`);
  return v;
}

/** A Python int, float or bool as a JS number, for arithmetic; TypeError for anything else (None, str, ...). */
export function num(v: unknown): number {
  if (typeof v === "number") return v;
  if (typeof v === "bigint") return Number(v);
  if (v instanceof PyFloat) return v.value;
  if (typeof v === "boolean") return v ? 1 : 0;
  throw new PyError("TypeError", `unsupported operand type: '${v === null || v === undefined ? "NoneType" : typeof v === "string" ? "str" : typeof v}'`);
}

/** list(dict.fromkeys(xs)): first occurrences, in order. */
export function uniq<T>(xs: Iterable<T>): T[] {
  return Array.from(new Set(xs));
}
