// The Python value model the kernel passes around, and Python truthiness.
//   None -> null, bool -> boolean, str -> string, int -> number (integral, safe) or bigint, float -> PyFloat,
//   list -> array, dict -> PyDict (a Map, so integer-like keys keep their place: plain objects reorder them).
import { PyFloat } from "./num";

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
