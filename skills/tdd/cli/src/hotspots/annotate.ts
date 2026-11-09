// annotate_hotspots.py in TypeScript: propose (and with --apply write) href / image / note for the boxes of a packet's
// diagram hotspot files (D4). Byte-identical to the Python on stdout and on the rewritten files.
import { SequenceMatcher } from "./difflib";
import * as re from "../py/re";
import { lower } from "../py/str";
import { traceback } from "./shapes";
import {
  D, PyError, S, WS_CHARS, abspath, asStr, cpLen, cpSlice, formatF, floatRepr, getKey, globSuffix, isDict, isFile, isList,
  join, jsonDumps, jsonLoads, num, padEnd, padStart, pyFloat, pyRound, readText, repr, rstrip, splitWs, strip,
  truthy, writeText, type PyDict, type PyValue,
} from "./_py";

export const DOC = "Propose (and with --apply write) href / image / note for the boxes of a packet's diagram hotspot files (D4).\n\nUsage: annotate_hotspots.py <packet-dir> [--build <dir>] [--apply] [--min-score 0.6] [--key <diagram>]\nReads <packet>/diagrams/*.hotspots.json (see diagram_hotspots.py), <build>/content.json (default <packet>/build:\n`anchors` and `defs`) and the mock captions (![alt](mockups/x.jpg \"title\")) of the packet's markdown source.\nFor every box of kind box or frame it proposes:\n  href   an anchor that exists in content.json. Exact table name -> #tbl-<slug>; one decision/row/PR token in the label\n         (D1..D8, E1.., F1.., R1.., PR n) -> that id; a label that equals (after normalising) or closely resembles a\n         persona / fold / heading / section / decision title -> that anchor.\n  note   the target def's text, first ~160 characters, whenever an href is proposed.\n  image  a mock (mockups/..jpg) whose caption shares at least two words with the label and most of them.\nScores: exact matches 1.0; token in label 0.9; fuzzy = difflib ratio or token overlap; token only in the box text 0.5.\nEvery proposal prints with its score and reason. --apply writes only proposals >= --min-score and only into fields\nthat are empty: an existing href / image / note / table is never overwritten. Full-diagram frames (over half the\nviewBox) are skipped: a hotspot over the whole figure would swallow the boxes inside it. Boxes that already carry a\ntable keep their drawer and get no href. Stdlib only. Exit 0; 2 on bad input.";

const FUZZY_KINDS = new Set(["persona", "fold", "heading", "section", "decision", "row", "pr", "milestone"]);
const STOP = new Set(["the", "a", "an", "and", "or", "of", "to", "in", "on", "for", "from", "is", "it", "its", "s", "by",
  "with", "at", "as", "one", "up"]);
// The kernel's re, so re.I folds as sre does (JS's i flag would not match "ı" or "İ" against an "i")
const TOKEN = re.compile(String.raw`\b(PR\s?\d{1,2}|[DEFR]\d{1,2})\b`, re.I);
const WS_RUN = new RegExp(`${S}+`, "gu");
const WS_RUN1 = new RegExp(`${S}+`, "u");
const BASE_TAIL = new RegExp(`${S}*[(·][^\\n]*(?=\\n?$)`, "gu");
const LEAD_NUM = new RegExp(`^${D}+${S}+`, "u");
const LEAD_NUM_DOT = new RegExp(`^${D}+${S}*\\u00b7${S}*`, "u");
const MOCK = new RegExp(`!\\[([^\\]]*)\\]\\(([^${WS_CHARS})]+)(?:${S}+"([^"]*)")?\\)`, "gu");

export function norm(s: string): string {
  const t = lower(s).replaceAll("'", "").replaceAll("’", "").replace(/[^a-z0-9 ]+/gu, " ");
  return strip(t.replace(WS_RUN, " "));
}
export function words(s: string): string[] {
  const out: string[] = [];
  for (let w of splitWs(norm(s))) {
    if (STOP.has(w) || /^[0-9]+$/.test(w)) continue;
    if (w.length > 3 && w.endsWith("s")) w = w.slice(0, -1);
    out.push(w);
  }
  return out;
}
export function tokId(t: string): string {
  const u = lower(t).replace(WS_RUN, "");
  return u.startsWith("pr") ? "pr-" + u.slice(2) : u;
}
const findTokens = (s: string): string[] => TOKEN.findall(s) as string[];
export function snippet(text: string, n = 160): string {
  const t = strip(text.replace(/<[^>]+>/g, "").replace(WS_RUN, " "));
  if (cpLen(t) <= n) return t;
  const head = cpSlice(t, 0, n);
  const sp = head.lastIndexOf(" ");
  return rstrip(sp >= 0 ? head.slice(0, sp) : head, ",;:. ") + "…";
}

export class Index {
  anchors = new Map<string, PyDict>();
  defs: PyValue;
  tables = new Map<string, PyValue>();
  constructor(content: PyValue) {
    const anchors = getKey(content, "anchors");
    for (const a of iter(anchors)) this.anchors.set(asStr(getKey(a, "id"), "id"), a as PyDict);
    this.defs = (content as PyDict).has("defs") ? (content as PyDict).get("defs")! : new Map();
    const schema = (content as PyDict).has("schema") ? (content as PyDict).get("schema")! : [];
    for (const t of iter(schema)) this.tables.set(asStr(getKey(t, "table"), "table"), getKey(t, "id"));
    for (const a of iter(anchors)) {
      const id = asStr(getKey(a, "id"), "id");
      if (getKey(a, "kind") === "table" && id.startsWith("tbl-")) {
        const k = id.slice(4).replaceAll("-", "_");
        if (!this.tables.has(k)) this.tables.set(k, id);
      }
    }
  }
  note(aid: string): string {
    if (!isDict(this.defs)) throw new PyError("AttributeError", "defs has no attribute 'get'");
    const d = this.defs.get(aid);
    if (!truthy(d)) return "";
    if (!isDict(d!)) throw new PyError("AttributeError", "def has no attribute 'get'");
    const t = d.get("text");
    return truthy(t) ? snippet(asStr(getKey(d, "text"), "text")) : "";
  }
  ok(href: string): boolean { return href.startsWith("#") && this.anchors.has(href.slice(1)); }
  *titles(): Generator<[string, string, string]> {
    for (const a of this.anchors.values()) {
      const kind = getKey(a, "kind");
      if (typeof kind === "string" && FUZZY_KINDS.has(kind)) {
        const title = asStr(getKey(a, "title"), "title");
        const dot = title.indexOf("·");
        yield [asStr(getKey(a, "id")), norm(title), dot >= 0 ? norm(title.slice(dot + 1)) : ""];
      }
    }
  }
}
function iter(v: PyValue): PyValue[] {
  if (isList(v)) return v;
  if (isDict(v)) return [...v.keys()];
  if (typeof v === "string") return Array.from(v);
  throw new PyError("TypeError", "object is not iterable");
}

export interface Mock { src: string; alt: string; title: string }
export function mocks(mdPath: string | null): Mock[] {
  const out: Mock[] = [];
  if (!mdPath || !isFile(mdPath)) return out;
  for (const m of readText(mdPath).matchAll(MOCK)) {
    if (m[2]!.includes("mockups/")) out.push({ src: m[2]!, alt: m[1]!, title: m[3] || "" });
  }
  return out;
}

type Proposal = [string, number, string];

const setOf = (xs: string[]) => new Set(xs);
const isSubset = (a: Set<string>, b: Set<string>) => [...a].every((x) => b.has(x));
/** Python's min(a, b) / max(a, b) on numbers: the first argument wins ties. */
const min2 = (a: number, b: number) => (b < a ? b : a);
const max2 = (a: number, b: number) => (b > a ? b : a);

export function proposeHref(b: PyDict, ix: Index): Proposal | null {
  const label = asStr(getKey(b, "label"), "label");
  const base = strip(label.replace(BASE_TAIL, ""));
  // 1. tables
  if (ix.tables.has(base)) return ["#" + str(ix.tables.get(base)!), 1.0, `label is table ${base}`];
  const stripped = strip(label);
  const ws = WS_RUN1.exec(stripped);
  const first = strip(ws ? stripped.slice(0, ws.index) : stripped, ",;:");
  if (ix.tables.has(first) && splitWs(label).length > 1) {
    return ["#" + str(ix.tables.get(first)!), 0.55, `label starts with table ${first} but names more (not exact)`];
  }
  // 2. decision / row / PR tokens
  const toks = setOf(findTokens(label).map(tokId).filter((t) => ix.anchors.has(t)));
  if (toks.size === 1) {
    const t = [...toks][0]!;
    return ["#" + t, 0.9, `label names ${t.toUpperCase()}`];
  }
  if (toks.size === 0) {
    const text = b.has("text") ? asStr(b.get("text")!, "text") : "";
    const tt = setOf(findTokens(text).map(tokId).filter((t) => ix.anchors.has(t)));
    if (tt.size === 1) {
      const t = [...tt][0]!;
      return ["#" + t, 0.5, `only the box text mentions ${t.toUpperCase()}`];
    }
  }
  // 3. titles
  const lab = norm(label).replace(LEAD_NUM, "");
  if (b.get("kind") === "frame" || splitWs(lab).length < 2) return null;
  let best: [number, string | null, string] = [0.0, null, ""];
  for (const [aid, full, suffix] of ix.titles()) {
    for (const [cand, what] of [[full, "title"], [suffix, "title after the dot"]] as const) {
      if (!cand) continue;
      let s: number;
      if (lab === cand) s = 1.0;
      else {
        const r = new SequenceMatcher<string>(null, lab, cand).ratio();
        const lw = setOf(words(lab)), cw = setOf(words(cand));
        const union = new Set([...lw, ...cw]);
        const inter = [...lw].filter((x) => cw.has(x)).length;
        const j = union.size ? inter / union.size : 0;
        if (Math.abs(cpLen(lab) - cpLen(cand)) > 0.5 * Math.max(cpLen(lab), cpLen(cand))) s = 0;
        else if (isSubset(lw, cw) || isSubset(cw, lw)) s = pyRound(min2(r, max2(j, r - 0.15)), 2);
        else s = pyRound(min2(r, 0.55), 2);
      }
      if (s > best[0]) best = [s, aid, `label ~ ${what} of ${aid}: ${repr(cand)}`];
    }
  }
  if (best[1] && best[0] >= 0.5) return ["#" + best[1], best[0], best[2]];
  return null;
}
function str(v: PyValue): string {
  // "#" + ix.tables[...] in Python requires a str.
  if (typeof v !== "string") throw new PyError("TypeError", 'can only concatenate str (not "other") to str');
  return v;
}

export function proposeImage(b: PyDict, ms: Mock[]): Proposal | null {
  const lw = words(asStr(getKey(b, "label"), "label").replace(LEAD_NUM_DOT, ""));
  if (lw.length < 2) return null;
  const found: [number, Mock, string[]][] = [];
  for (const m of ms) {
    const cw = words(m.title);
    const mt = new SequenceMatcher<string>(null, lw, cw, false).findLongestMatch(0, lw.length, 0, cw.length);
    if (mt.size >= 2 && mt.size / lw.length >= 0.6) found.push([mt.size / lw.length, m, lw.slice(mt.a, mt.a + mt.size)]);
  }
  found.sort((x, y) => -x[0] - -y[0]);
  if (found.length && (found.length === 1 || found[0]![0] > found[1]![0])) {
    const [sc, m, run] = found[0]!;
    return [m.src, pyRound(sc, 2), `caption ${repr(m.title)} shares the run ${repr(run.join(" "))}`];
  }
  return null;
}

/** annotate_hotspots.main(argv). */
export function run(argv: string[]): { code: number; out: string; err: string } {
  const out: string[] = [];
  const done = (code: number, err = "") => ({ code, out: out.join(""), err });
  try {
    const args: string[] = [];
    let apply = false, minScore = 0.6, build: string | null = null, key: string | null = null;
    for (let i = 0; i < argv.length; i++) {
      const a = argv[i]!;
      const next = () => {
        if (i + 1 >= argv.length) throw new PyError("StopIteration", "");
        return argv[++i]!;
      };
      if (a === "--apply") apply = true;
      else if (a === "--min-score") minScore = pyFloat(next());
      else if (a === "--build") build = next();
      else if (a === "--key") key = next();
      else args.push(a);
    }
    if (args.length !== 1) return done(1, DOC + "\n");
    const pk = abspath(args[0]!);
    const bdir = build ? abspath(build) : join(pk, "build");
    const cj = join(bdir, "content.json");
    if (!isFile(cj)) return done(2, `no ${cj}: compile the packet first\n`);
    const ix = new Index(jsonLoads(readText(cj)));
    const md = globSuffix(pk, ".md")[0] ?? null;
    const ms = mocks(md);
    let proposed = 0, applied = 0;
    out.push(`${padEnd("diagram", 14)} ${padEnd("label", 44)} ${padEnd("proposal", 36)} ${padStart("score", 5)}  reason\n`);
    for (const f of globSuffix(join(pk, "diagrams"), ".hotspots.json")) {
      const name = f.slice(f.lastIndexOf("/") + 1);
      const k = name.slice(0, name.length - ".hotspots.json".length);
      if (key && k !== key) continue;
      const spec = jsonLoads(readText(f));
      const vbRaw = isDict(spec) ? spec.get("viewBox") : undefined;
      if (!isDict(spec)) throw new PyError("AttributeError", "object has no attribute 'get'");
      const vb = truthy(vbRaw) ? (vbRaw as PyValue[]) : [0n, 0n, 1n, 1n];
      let changed = false;
      for (const b of iter(getKey(spec, "boxes"))) {
        if (!isDict(b)) throw new PyError("AttributeError", "object has no attribute 'get'");
        const kind = b.get("kind");
        if (kind !== "box" && kind !== "frame") continue;
        if (kind === "frame" && num(getKey(b, "w")) * num(getKey(b, "h")) > 0.5 * num(vb[2]) * num(vb[3])) continue;
        const props = new Map<string, Proposal>();
        let h = truthy(b.get("table")) ? null : proposeHref(b, ix);
        if (h && !ix.ok(h[0])) h = null;
        if (h) {
          props.set("href", h);
          const n = ix.note(h[0].slice(1));
          if (n) props.set("note", [n, h[1], "def text of " + h[0]]);
        }
        const im = proposeImage(b, ms);
        if (im) props.set("image", im);
        for (const [fld, [val, score, why]] of props) {
          proposed += 1;
          const held = b.get(fld);
          const tag = !truthy(held) ? "" : " [kept: already set]";
          const shown = fld !== "note" ? val : cpSlice(val, 0, 30) + "…";
          const lab = cpSlice(asStr(getKey(b, "label"), "label"), 0, 44);
          out.push(`${padEnd(k, 14)} ${padEnd(lab, 44)} ${padEnd(fld + " " + shown, 36)} ${padStart(formatF(score, 2), 5)}  ${why}${tag}\n`);
          if (apply && score >= minScore && !truthy(held)) {
            b.set(fld, val);
            applied += 1;
            changed = true;
          }
        }
      }
      if (apply && changed) writeText(f, jsonDumps(spec, { indent: 1, ensureAscii: false }));
    }
    out.push(`proposals ${proposed}; applied ${applied} (min-score ${floatRepr(minScore)}${apply ? "" : ", dry run"})\n`);
    return done(0);
  } catch (e) {
    return done(1, traceback(e));
  }
}
