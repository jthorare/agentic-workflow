// diagram_hotspots.py in TypeScript: list the boxes of a tldraw-exported SVG into <key>.hotspots.json (D4, D7).
// Byte-identical to the Python on stdout and on the written file; see test/hotspots/shapes.test.ts.
import { HTMLParser, type Attrs } from "./htmlparser";
import {
  D, D_CHARS, PyError, PyFloat, S, basename, cpLen, cpSlice, getKey, isDict, isFile, isList, jsonDumps, jsonLoads, pyFloat,
  pyRound, qualifiedName, readText, splitext, strip, truthy, typeError, writeText, type PyDict, type PyValue,
} from "./_py";

export const DOC = "List the boxes of a tldraw-exported SVG so they can become hover hotspots on the page (D4) and, for a schema\ndiagram, open a table drawer (D7).\n\nUsage: diagram_hotspots.py <diagrams/key.svg> [--write]\nPrints (or with --write creates or merges diagrams/<key>.hotspots.json) one entry per box: its label (the box's first\nbold line), its text, position and size in viewBox units, the tone read from its stroke colour, and empty href /\nimage / note / table fields for the author to fill. build_tdd.py renders only entries that carry an href, image, note\nor table; the rest are kept so a rerun after a redraw matches them by label and keeps what was filled in.\nExit 0; 1 on a file that is not a tldraw SVG export.";

export const TONES: Record<string, string> = {
  "#099268": "green", "#9fa8b2": "grey", "#4465e9": "blue", "#f1ac4b": "amber", "#e03131": "red",
  "#e16919": "orange", "#ae3ec9": "violet", "#1d1d1d": "ink",
};

type Rect = [number, number, number, number];
export interface Shape {
  x: number; y: number; boxes: Rect[]; fo: Rect | null; stroke: string | null; dashed: boolean;
  text: string[]; strong: string[];
}

const NUM = `[${D_CHARS}.\\-]+`;
const MATRIX = new RegExp(`^matrix\\(${S}*${NUM},${S}*${NUM},${S}*${NUM},${S}*${NUM},${S}*(${NUM}),${S}*(${NUM})\\)`, "u");
const PATH_NUM = new RegExp(`-?${D}+(?:\\.${D}+)?`, "gu");
const VIEWBOX = new RegExp(`viewBox="(${NUM})[ ,]+(${NUM})[ ,]+([${D_CHARS}.]+)[ ,]+([${D_CHARS}.]+)"`, "u");

/** Python's min()/max(): the first of equal items wins. */
function pyMin(xs: number[]): number { let m = xs[0]!; for (const x of xs.slice(1)) if (x < m) m = x; return m; }
function pyMax(xs: number[]): number { let m = xs[0]!; for (const x of xs.slice(1)) if (x > m) m = x; return m; }

/** Each tldraw shape is a <g transform="matrix(1,0,0,1,x,y)"> directly under <svg>: its geometry (paths or a rect)
 * and a foreignObject with the rich text. */
export class Shapes extends HTMLParser {
  depth = 0;
  shapes: Shape[] = [];
  cur: Shape | null = null;
  inFo = 0;
  inStrong = 0;

  override handle_starttag(tag: string, attrs: Attrs): void {
    const a = new Map<string, string | null>(attrs);
    const get = (k: string, dflt: string | number | null): string | number | null => (a.has(k) ? a.get(k)! : dflt);
    if (tag === "g") {
      this.depth += 1;
      const t = get("transform", "");
      if (t === null) throw typeError("expected string or bytes-like object, got 'NoneType'");
      const m = MATRIX.exec(t as string);
      if (this.depth === 1 && m) {
        this.cur = { x: pyFloat(m[1]!), y: pyFloat(m[2]!), boxes: [], fo: null, stroke: null, dashed: false, text: [], strong: [] };
        this.shapes.push(this.cur);
      }
      return;
    }
    const cur = this.cur;
    if (cur === null) return;
    if (tag === "path" && a.get("d")) {
      const nums = (a.get("d")!.match(PATH_NUM) ?? []).map((v) => pyFloat(v));
      const xs = nums.filter((_, i) => i % 2 === 0), ys = nums.filter((_, i) => i % 2 === 1);
      if (xs.length >= 2 && ys.length >= 2) {
        const x0 = pyMin(xs), y0 = pyMin(ys);
        cur.boxes.push([x0, y0, pyMax(xs) - x0, pyMax(ys) - y0]);
      }
      const stroke = a.get("stroke");
      if (stroke && stroke !== "none") cur.stroke = stroke.toLowerCase();
      if (a.get("stroke-dasharray")) cur.dashed = true;
    } else if (tag === "rect") {
      const r = this.rect(get);
      if (r) cur.boxes.push(r);
    } else if (tag === "foreignobject") {
      const r = this.rect(get);
      if (r) cur.fo = r;
      this.inFo += 1;
    } else if (tag === "strong" && this.inFo) {
      this.inStrong += 1;
    } else if (tag === "p" && this.inFo) {
      cur.text.push("\n");
    }
  }
  /** (float(a.get("x", 0)), ...) inside `try: ... except ValueError: pass`; None still raises TypeError. */
  private rect(get: (k: string, d: string | number | null) => string | number | null): Rect | null {
    try {
      return [pyFloat(get("x", 0)), pyFloat(get("y", 0)), pyFloat(get("width", 0)), pyFloat(get("height", 0))];
    } catch (e) {
      if (e instanceof PyError && e.type === "ValueError") return null;
      throw e;
    }
  }
  override handle_endtag(tag: string): void {
    if (tag === "g") {
      this.depth -= 1;
      if (this.depth < 1) this.cur = null;
    } else if (tag === "foreignobject" && this.inFo) this.inFo -= 1;
    else if (tag === "strong" && this.inStrong) this.inStrong -= 1;
  }
  override handle_data(data: string): void {
    if (this.cur !== null && this.inFo && strip(data)) {
      this.cur.text.push(data);
      if (this.inStrong) this.cur.strong.push(strip(data));
    }
  }
}

export interface Box {
  label: string; kind: string; tone: string; x: number; y: number; w: number; h: number; text: string;
  href: PyValue; image: PyValue; note: PyValue; table: PyValue;
}
const FIELDS = ["href", "image", "note", "table"] as const;

/** The `out` dict of diagram_hotspots.main for one SVG source; throws the Python script's exit message on a file that
 * is not a tldraw export. */
export function listBoxes(path: string, src: string): { viewBox: number[]; boxes: Box[] } {
  const vb = VIEWBOX.exec(src);
  if (!vb || !src.includes("tl-")) throw new ExitMessage(`${path}: not a tldraw SVG export (no viewBox or tl- classes)`);
  const p = new Shapes();
  p.feed(src);
  const vbx = [vb[1]!, vb[2]!, vb[3]!, vb[4]!].map((x) => pyFloat(x));
  const boxes: Box[] = [];
  for (const s of p.shapes) {
    const text = strip(s.text.join("").replace(/[ \t]+/g, " "));
    if (!text) continue;
    const geo: Rect[] = s.boxes.length ? s.boxes : s.fo ? [s.fo] : [];
    if (!geo.length) continue;
    let best = geo[0]!, bk = best[2] * best[3];
    for (const r of geo.slice(1)) { const k = r[2] * r[3]; if (k > bk) { best = r; bk = k; } }
    const [x, y, w, h] = best;
    if (w <= 0 || h <= 0) continue;
    const area = w * h;
    let kind = area > 0.12 * vbx[2]! * vbx[3]! || (!s.boxes.length && w > 900) ? "frame" : !s.boxes.length ? "text" : "box";
    if (kind === "text" && cpLen(text) > 90) kind = "title";
    const label = s.strong.length ? s.strong[0]! : text.split("\n")[0]!;
    boxes.push({
      label: cpSlice(label, 0, 120), kind, tone: (TONES[s.stroke ?? ""] ?? "grey") + (s.dashed ? " dashed" : ""),
      x: pyRound(s.x + x, 1), y: pyRound(s.y + y, 1), w: pyRound(w, 1), h: pyRound(h, 1),
      text: cpSlice(text, 0, 400), href: "", image: "", note: "", table: "",
    });
  }
  return { viewBox: vbx, boxes };
}

export function toPy(svg: string, viewBox: number[], boxes: Box[]): PyDict {
  return new Map<string, PyValue>([
    ["svg", svg],
    ["viewBox", viewBox.map((v) => new PyFloat(v))],
    ["boxes", boxes.map((b) => new Map<string, PyValue>([
      ["label", b.label], ["kind", b.kind], ["tone", b.tone], ["x", new PyFloat(b.x)], ["y", new PyFloat(b.y)],
      ["w", new PyFloat(b.w)], ["h", new PyFloat(b.h)], ["text", b.text], ["href", b.href], ["image", b.image],
      ["note", b.note], ["table", b.table],
    ]))],
  ]);
}

/** A sys.exit(message): stderr gets the message, exit status 1. */
export class ExitMessage extends Error {}

/** diagram_hotspots.main(argv): returns [exit code, stdout, stderr]. */
export function run(argv: string[]): { code: number; out: string; err: string } {
  const write = argv.includes("--write");
  const args = argv.filter((x) => x !== "--write");
  if (args.length !== 1) return { code: 1, out: "", err: DOC + "\n" };
  const path = args[0]!;
  try {
    const src = readText(path);
    const { viewBox, boxes } = listBoxes(path, src);
    const jpath = splitext(path)[0] + ".hotspots.json";
    if (write) {
      if (isFile(jpath)) {
        const old = jsonLoads(readText(jpath));
        if (!isDict(old)) throw new PyError("AttributeError", "'list' object has no attribute 'get'");
        const oldBoxes = old.has("boxes") ? old.get("boxes")! : [];
        const byLabel = new Map<string, PyDict>();
        for (const b of iterate(oldBoxes)) {
          const lab = getKey(b, "label");
          if (isList(lab) || isDict(lab)) throw typeError("unhashable type");
          if (typeof lab === "string") byLabel.set(lab, b as PyDict);
        }
        for (const b of boxes) {
          const o = byLabel.get(b.label);
          if (o) for (const k of FIELDS) b[k] = o.has(k) ? o.get(k)! : "";
        }
      }
      const out = toPy(basename(path), viewBox, boxes);
      writeText(jpath, jsonDumps(out, { indent: 1, ensureAscii: false }));
      const nbox = boxes.filter((b) => b.kind === "box").length;
      const nann = boxes.filter((b) => truthy(b.href) || truthy(b.image) || truthy(b.note) || truthy(b.table)).length;
      return { code: 0, out: `wrote ${jpath}: ${boxes.length} shapes (${nbox} boxes, ${nann} annotated)\n`, err: "" };
    }
    return { code: 0, out: jsonDumps(toPy(basename(path), viewBox, boxes), { indent: 1, ensureAscii: false }) + "\n", err: "" };
  } catch (e) {
    if (e instanceof ExitMessage) return { code: 1, out: "", err: e.message + "\n" };
    return { code: 1, out: "", err: traceback(e) };
  }
}

function iterate(v: PyValue): PyValue[] {
  if (isList(v)) return v;
  if (typeof v === "string") return Array.from(v);
  if (isDict(v)) return [...v.keys()];
  throw typeError("object is not iterable");
}

/** The last line of the traceback Python would print for an escaped exception. */
export function traceback(e: unknown): string {
  if (e instanceof PyError) return `Traceback (most recent call last):\n${qualifiedName(e.type)}${e.message ? ": " + e.message : ""}\n`;
  return `Traceback (most recent call last):\n${e instanceof Error ? e.stack ?? e.message : String(e)}\n`;
}
