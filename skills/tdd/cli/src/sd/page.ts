// build_doc.py:487-490 and 496-516: STATUS_WORDS, GENERIC_TAILS, page_title, load_json, on the kernel.
import * as py from "../py";
import { PyError } from "../py/errors";
import type { PyValue } from "../py/value";

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

/** load_json(path, what): the parsed file (kernel values: dicts are Maps, floats PyFloat), null for an empty path;
 * an unreadable or invalid file throws SystemExit(1, "{what} {path}: {error}") as build_doc's sys.exit does. */
export function loadJson(path: string | null | undefined, what: string): PyValue {
  if (!path) return null;
  try {
    if (path.includes("\0")) throw new PyError("ValueError", "embedded null byte");
    return py.json.loads(py.io.readText(path));
  } catch (e) {
    const text = caughtText(e, path);
    if (text === null) throw e;
    throw new SystemExit(1, `${what} ${path}: ${text}`);
  }
}
