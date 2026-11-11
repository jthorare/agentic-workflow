// The hotspot tools' names for the Python-semantics kernel (src/py): re-exports, plus one-line adapters where a tool
// calls a kernel function with a different signature. No Python behaviour is implemented here.
import { dumps } from "../py/json";
import { DECIMAL_CLASS, SPACE_CLASS, WORD_CLASS } from "../py/uni";
import type { PyValue } from "../py/value";

export { PyError, keyError, qualifiedName, typeError, valueError } from "../py/errors";
export { PyFloat, float as pyFloat, floatRepr, formatFixed as formatF, round as pyRound } from "../py/num";
export { cmp as cmpCodepoint, cpLen, cpSlice, isspace as isSpace, ljust as padEnd, lstrip, rjust as padStart, rstrip, strip } from "../py/str";
export { asStr, getKey, isDict, isList, num, truthy, type PyDict, type PyValue } from "../py/value";
export { repr } from "../py/repr";
export { html5 as html5Get, unescape, unescapeAttrValue as unescapeAttr } from "../py/html";
export { loads as jsonLoads } from "../py/json";
export { abspath, basename, dirname, expanduser, join, normpath, splitext } from "../py/path";
export { exists, globSorted as globSuffix, isdir as isDir, isfile as isFile, listdir, readText, writeText } from "../py/io";

/** Python's \s, \d, \w on str patterns (CPython 3.13's tables), as regex fragments for the u flag: the *_CHARS
 * bodies go inside a [...] class, S / D / W stand alone. */
export const WS_CHARS = SPACE_CLASS;
export const D_CHARS = DECIMAL_CLASS;
export const S = `[${SPACE_CLASS}]`;
export const D = `[${DECIMAL_CLASS}]`;
export const W = `[${WORD_CLASS}]`;

/** str.split() with no separator. */
export { split as splitWs } from "../py/str";
/** json.dumps(v, indent=indent, ensure_ascii=ensureAscii) */
export const jsonDumps = (v: PyValue, opts: { indent?: number | null; ensureAscii?: boolean } = {}): string =>
  dumps(v, { indent: opts.indent ?? null, ensureAscii: opts.ensureAscii ?? true });
