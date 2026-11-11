// The agents lane's names for the Python-semantics kernel (src/py): re-exports, plus one-line adapters where the lane
// calls a kernel function with a different signature. No Python behaviour is implemented here.
import { dumps as pyDumps } from "../py/json";
import { PyFloat, round } from "../py/num";
import { expanduser as pyExpanduser } from "../py/path";
import { decodeUtf8, readBytes, readText as pyReadText, universalNewlines } from "../py/io";
import { cpSlice, isspace } from "../py/str";
import { split } from "../py/str";

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type Py = any;
/** A Python dict (insertion order; assignment to an existing key keeps its place). */
export type PyDict = Map<string, Py>;

export { PyFloat, floatRepr } from "../py/num";
export { JSONDecodeError, loads } from "../py/json";
export { cmp as cmpStr, cpLen, cpSlice, lstrip, partition, rstrip, sorted, strip } from "../py/str";
export { get, isDict, num, or, truthy, uniq } from "../py/value";
export { pyStr as str, repr } from "../py/repr";
export { escape, html5, unescape } from "../py/html";
export { abspath, basename, dirname, join as pjoin, normpath, relpath, splitext } from "../py/path";
export { isdir, isfile, sha256Hex as sha256, utf8Len } from "../py/io";
/** Python's \s and str.isspace() set, as a class body (\uXXXX escapes: valid with or without the u flag). */
export { SPACE_CLASS as PY_WS } from "../py/uni";

/** ch.isspace() for one character. */
export const isspace1 = (ch: string): boolean => isspace(ch);
/** str.split() with no separator. */
export const splitWs = (s: string): string[] => split(s);
/** s[:1] and s[-1:] */
export const cpFirst = (s: string): string => cpSlice(s, 0, 1);
export const cpLast = (s: string): string => cpSlice(s, -1);
/** round(x, n) on a float, as a float. */
export const pyRound = (x: number, n: number): PyFloat => new PyFloat(round(x, n));
/** json.dumps(v, ensure_ascii=False, indent=indent); a JS undefined (a missing key on the lane's side) is None. */
export const dumps = (v: Py, indent?: number): string =>
  pyDumps(v, { indent: indent ?? null, ensureAscii: false, default: (o) => (o === undefined ? null : o) });
/** os.path.expanduser(p), "~" by default. */
export const expanduser = (p = "~"): string => pyExpanduser(p);
/** bytes.decode("utf-8", "replace") */
export const decodeReplace = (b: Uint8Array): string => decodeUtf8(b, "replace");
/** open(p, encoding="utf-8"[, errors="replace"]).read() */
export const readText = (p: string, replace = false): string =>
  replace ? universalNewlines(decodeUtf8(readBytes(p), "replace")) : pyReadText(p);
