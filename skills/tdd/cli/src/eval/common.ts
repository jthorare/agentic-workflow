// ev_common.py in TypeScript: the content.json walk, text and file helpers, and the Python number rules the records
// need. Content is read through the kernel's json (src/py), so dicts are Maps in file order and ints, floats and
// key order survive to the written files byte for byte.
import { createHash } from "node:crypto";
import { lstatSync, mkdirSync, readdirSync, readFileSync, statSync } from "node:fs";
import { fileURLToPath } from "node:url";
import * as py from "../py";
import { PyError } from "../py/errors";
import { decodeUtf8, readBytes, universalNewlines, writeText } from "../py/io";
import { PyFloat, formatFixed } from "../py/num";
import { pyStr } from "../py/repr";
import { get } from "../py/value";

export type Py = any; // eslint-disable-line @typescript-eslint/no-explicit-any
export { get, pyStr, PyFloat };

/** skills/tdd (ev_common.SKILL), without a trailing slash. */
export const SKILL = py.path.normpath(fileURLToPath(new URL("../../../", import.meta.url)));
export const SCRIPTS = py.path.join(SKILL, "scripts");
export const PAGE_LIMIT = 16 * 1024 * 1024;
export const CHARS_PER_TOKEN = 3.6;
export const IMAGE_EXT = [".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif"];
export const SQL_LANGS = ["sql", "psql", "postgres"];

const re = py.re;

// ----------------------------------------------------------------------------- Python values

/** isinstance(v, (int, float)) (bool included, as in Python). */
export function isNum(v: unknown): boolean {
  return typeof v === "number" || typeof v === "bigint" || typeof v === "boolean" || v instanceof PyFloat;
}
/** isinstance(v, int) (bool included). */
export function isInt(v: unknown): boolean {
  return typeof v === "boolean" || typeof v === "bigint" || (typeof v === "number" && Number.isInteger(v));
}
/** A Python number as a JS number. */
export function n(v: unknown): number {
  return py.truthy(v) ? Number(v instanceof PyFloat ? v.value : v) : 0;
}
export const isFloat = (v: unknown): boolean => v instanceof PyFloat || (typeof v === "number" && !Number.isInteger(v));
export const fl = (x: number): PyFloat => new PyFloat(x);
/** round(v, nd): an int stays an int, a float is rounded half-even on its binary value. */
export function pyRound(v: unknown, nd: number): number | PyFloat {
  if (isFloat(v)) return fl(py.round(n(v), nd));
  return typeof v === "boolean" ? (v ? 1 : 0) : n(v);
}
/** sum(xs): an int unless a float joins in. */
export function pySum(xs: Iterable<unknown>): number | PyFloat {
  let t = 0;
  let float = false;
  for (const x of xs) {
    if (isFloat(x)) float = true;
    t += n(x);
  }
  return float ? fl(t) : t;
}
/** f"{x:.nf}" */
export const fixed = (x: unknown, nd: number): string => formatFixed(n(x), nd);
/** int(v) for an int, float, bool or str. */
export function pyInt(v: unknown): number {
  if (typeof v === "string") return Number(py.int(v));
  if (typeof v === "boolean") return v ? 1 : 0;
  return Math.trunc(n(v));
}
/** a / b in Python 3 (always a float). */
export const div = (a: number, b: number): PyFloat => fl(a / b);
/** ratio(num, den): num / den, or None when den is 0. */
export function ratio(num: number, den: number): PyFloat | null {
  return den ? div(num, den) : null;
}
/** A dict keyed by data (insertion order, any key): json.dumps writes Map keys as Python does. */
export type Dict = Map<Py, Py>;
/** list(x) for a value Python would iterate; None and non-lists are []. */
export function list(v: unknown): Py[] {
  return Array.isArray(v) ? v : [];
}
/** (v or "") for a value used as text. */
export function text(v: unknown): string {
  if (typeof v === "string") return v;
  if (!py.truthy(v)) return "";
  throw new PyError("TypeError", `expected string or bytes-like object, got '${typeof v}'`);
}

// ----------------------------------------------------------------------------- files

export function now(): string {
  return py.time.formatUtc(new Date());
}

export function sha256Bytes(b: Uint8Array | string): string {
  return createHash("sha256").update(b).digest("hex");
}
export function sha256File(path: string): string {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

/** json.load(open(path)) or default on OSError / ValueError. */
export function readJson(path: string, dflt: Py = null): Py {
  try {
    return py.json.loads(py.io.readText(path));
  } catch {
    return dflt;
  }
}

/** json.dump(obj, indent=1, ensure_ascii=False, default=str) + "\n" */
export function writeJson(path: string, obj: unknown): void {
  mkdirSync(py.path.dirname(path), { recursive: true });
  writeText(path, py.json.dumps(obj, { indent: 1, ensureAscii: false, default: (o) => pyStr(o) }) + "\n");
}

/** open(path, encoding="utf-8", errors="replace").read(), or None on OSError. */
export function readText(path: string): string | null {
  try {
    return universalNewlines(decodeUtf8(readBytes(path), "replace"));
  } catch {
    return null;
  }
}

export function getsize(p: string): number {
  return statSync(p).size;
}

export function tokensEst(textOrLen: string | number | null | undefined): number {
  const len = typeof textOrLen === "number" ? textOrLen : py.cpLen(textOrLen || "");
  return py.roundInt(len / CHARS_PER_TOKEN);
}

export function tail(t: string | null | undefined, k = 12): string[] {
  const lines = py.str.splitlines(t || "").filter((l) => py.str.strip(l) !== "");
  return lines.slice(Math.max(0, lines.length - k));
}

export function oneLine(t: string | null | undefined, limit = 200): string {
  let s = py.str.split(t || "").join(" ");
  s = re.sub(String.raw`https?://\S*(authorization_request|token|sso)\S*`, "<url omitted>", s);
  return py.cpSlice(s, 0, limit);
}

/** str.startswith / endswith with a tuple. */
export const endsWithAny = (s: string, xs: readonly string[]): boolean => xs.some((x) => s.endsWith(x));
export const startsWithAny = (s: string, xs: readonly string[]): boolean => xs.some((x) => s.startsWith(x));

/** os.walk(root) with sorted, non-hidden dirs (when skipHidden) and sorted files: [dirpath, files][] in walk order. */
export function walkDir(root: string, skipHidden: boolean): [string, string[]][] {
  const out: [string, string[]][] = [];
  const visit = (dp: string): void => {
    let names: string[];
    try {
      names = readdirSync(dp);
    } catch {
      return;
    }
    const dirs: string[] = [];
    const files: string[] = [];
    for (const nm of names) {
      const p = py.path.join(dp, nm);
      let isDir = false;
      try {
        isDir = statSync(p).isDirectory();
      } catch {
        isDir = false;
      }
      (isDir ? dirs : files).push(nm);
    }
    out.push([dp, files]);
    const dn = py.sorted(skipHidden ? dirs.filter((d) => !d.startsWith(".")) : dirs);
    for (const d of dn) {
      const p = py.path.join(dp, d);
      let link = false;
      try {
        link = lstatSync(p).isSymbolicLink();
      } catch {
        link = false;
      }
      if (!link) visit(p); // os.walk does not follow symlinked folders
    }
  };
  visit(root);
  return out;
}

/** The entries of a folder that glob's "*" matches (no leading dot), in the file system's order (os.scandir). */
export function globNames(d: string, dirsOnly = false): string[] {
  let names: string[];
  try {
    names = readdirSync(d);
  } catch {
    return [];
  }
  return names.filter((nm) => {
    if (nm.startsWith(".")) return false;
    if (!dirsOnly) return true;
    try {
      return statSync(py.path.join(d, nm)).isDirectory();
    } catch {
      return false;
    }
  });
}

// ----------------------------------------------------------------------------- content.json

export function stripTags(s: unknown): string {
  return py.str.strip(py.html.unescape(re.sub("<[^>]+>", "", text(s))));
}

/** Every dict node in the block tree (optionally only those whose type is in want), document order. */
export function* walk(node: unknown, want: ReadonlySet<string> | null = null): Generator<Map<string, Py>> {
  if (node instanceof Map) {
    if (want === null || want.has(get(node, "type") as string)) yield node;
    for (const v of node.values()) if (v instanceof Map || Array.isArray(v)) yield* walk(v, want);
  } else if (Array.isArray(node)) {
    for (const x of node) yield* walk(x, want);
  }
}

export function sqlFences(content: Py): Map<string, Py>[] {
  return [...walk(get(content, "sections", []), new Set(["code"]))].filter((nd) => SQL_LANGS.includes(py.lower(text(get(nd, "lang")))));
}

export interface PrRow { n: Py; id: Py; title: string; change: string; milestone: string; file: Py; body: string }

export function prsOf(content: Py): PrRow[] {
  const out: PrRow[] = [];
  for (const nd of walk(get(content, "sections", []), new Set(["prs"])))
    for (const g of list(get(nd, "groups", [])))
      for (const p of list(get(g, "prs", [])))
        out.push({ n: get(p, "n"), id: get(p, "id"), title: stripTags(get(p, "title")), change: stripTags(get(p, "change")),
          milestone: stripTags(get(g, "ms")), file: get(p, "file"), body: stripTags(get(p, "bodyHtml")) });
  return out;
}

/** Plain text of the page in document order: title, lede, each section's title and its blocks' text. */
export function contentText(content: Py, limitChars: number | null = null): string {
  const parts: string[] = [py.truthy(get(content, "titleText")) ? get(content, "titleText") : stripTags(get(content, "title"))];
  for (const p of list(get(content, "lede", []))) parts.push(stripTags(get(p, "html")));
  for (const s of list(get(content, "sections", []))) {
    parts.push(`\n## ${pyStr(get(s, "num"))}. ${pyStr(get(s, "titleText"))}`);
    for (const nd of walk(get(s, "blocks", []))) {
      const t = get(nd, "type");
      if (t === "heading") parts.push("### " + stripTags(get(nd, "html")));
      else if (t === "p") parts.push(stripTags(get(nd, "html")));
      else if (t === "list") for (const i of list(get(nd, "items", []))) parts.push("- " + stripTags(get(i, "html")));
      else if (t === "table") {
        parts.push(list(get(nd, "head", [])).map((h) => stripTags(get(h, "html"))).join(" | "));
        for (const r of list(get(nd, "rows", []))) parts.push(list(get(r, "cells", [])).map((c) => stripTags(get(c, "html"))).join(" | "));
      } else if (t === "code") parts.push("```" + pyStr(py.truthy(get(nd, "lang")) ? get(nd, "lang") : "") + "\n" + pyStr(get(nd, "text", "")) + "\n```");
      else if (t === "decision") parts.push(`Decision ${pyStr(get(nd, "did"))}: ${stripTags(get(nd, "question"))} [${stripTags(get(nd, "status"))}]`);
      else if (t === "fold" || t === "group" || t === "persona")
        parts.push("#### " + stripTags(get(nd, "title")) + (py.truthy(get(nd, "one")) ? " · " + stripTags(get(nd, "one")) : ""));
      else if (t === "cards") for (const c of list(get(nd, "cards", []))) parts.push("* " + stripTags(get(c, "title")));
      else if (t === "diagram") parts.push(`[diagram: ${stripTags(get(nd, "title"))}] ${stripTags(get(nd, "reading"))}`);
      else if (t === "prs")
        for (const g of list(get(nd, "groups", [])))
          for (const p of list(get(g, "prs", []))) parts.push(`PR ${pyStr(get(p, "n"))}: ${stripTags(get(p, "title"))}. ${stripTags(get(p, "change"))}`);
    }
  }
  const out = parts.filter((x) => x).join("\n");
  return limitChars ? py.cpSlice(out, 0, limitChars) : out;
}

export function norm(s: unknown): string {
  return re.sub(String.raw`\s+`, " ", py.lower(py.str.strip(text(s))));
}
