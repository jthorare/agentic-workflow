// DIAG: every [diagram: key] has its svg/png, a .mmd is not newer than its svg, hotspot files match their svg (viewBox,
// labels through the TypeScript Shapes of src/hotspots), hotspot images and tables exist, legend tones are known
// (lint_packet.Lint.r_diag).
import { Effect } from "effect";
import * as py from "../../py";
import { Shapes } from "../../hotspots/shapes";
import { float } from "../../py/num";
import { Packet, type LintPacket } from "../packet";
import { TONE_CLASSES } from "../patterns";
import { type PyDict, type PyValue, get, isDict, iter, nodes, or, plain, repr, split_marker, str, truthy } from "../pyops";

const re = py.re;
const { join } = py.path;
const VIEWBOX = re.compile(String.raw`viewBox="([\d.\-]+)[ ,]+([\d.\-]+)[ ,]+([\d.]+)[ ,]+([\d.]+)"`);
const HSPACE = re.compile("[ \t]+");

/** svg_viewbox(path): the four numbers of viewBox="..." in the first 8 KB, or null. */
export function svg_viewbox(p: LintPacket, path: string): number[] | null {
  let head: string;
  try {
    head = py.io.decodeUtf8(p.fs.readBytes(path).subarray(0, 8192), "ignore");
  } catch {
    return null;
  }
  const m = VIEWBOX.search(head);
  return m ? m.groups().map((x) => pyFloat(x)) : null;
}

/** float(x) with CPython's rules and ValueError text (the kernel's float). */
const pyFloat = (v: PyValue | undefined): number => float(v as Parameters<typeof float>[0]);

/** Labels and texts of the shapes in a tldraw svg, by diagram_hotspots' own extractor (fed, never closed, as the Python). */
export function shapes(p: LintPacket, svg: string): [Set<string>, string[]] | null {
  let text: string;
  try {
    text = p.fs.readText(svg);
  } catch {
    return null;
  }
  const s = new Shapes();
  s.feed(text);
  const labels = new Set<string>();
  const texts: string[] = [];
  for (const sh of s.shapes) {
    const t = py.str.strip(re.sub(HSPACE, " ", sh.text.join("")));
    if (!t) continue;
    labels.add(py.cpSlice(sh.strong.length ? sh.strong[0]! : py.str.split(t, "\n")[0]!, 0, 120));
    texts.push(t);
  }
  return [labels, texts];
}

const pyList = (xs: number[]): string => repr(xs.map((x) => new py.PyFloat(x)));

export const diag = Effect.gen(function* () {
  const p = yield* Packet;
  const ddir = join(p.root, "diagrams");
  const keys: [string, number][] = [];
  for (const [ln, arg] of p.diagramMarks()) keys.push([split_marker(arg)[0], ln]);
  const seen = new Map<string, number>();
  for (const [key, ln] of keys) {
    if (seen.has(key)) {
      p.add("DIAG", "duplicate", "warn", `diagram ${key} is embedded twice (first at line ${seen.get(key)}); embed a figure once and link to it`, p.rel(p.src), ln);
      continue;
    }
    seen.set(key, ln);
    const src = [".svg", ".png"].map((e) => join(ddir, key + e)).find((q) => p.fs.isfile(q)) ?? null;
    if (!src) {
      p.add("DIAG", "missing", "error", `[diagram: ${key}] has no diagrams/${key}.svg or .png`, p.rel(p.src), ln);
      continue;
    }
    const mmd = join(ddir, key + ".mmd");
    if (p.fs.isfile(mmd) && p.fs.getmtime(mmd) > p.fs.getmtime(src) + 1) {
      p.add("DIAG", "stale-svg", "warn", `diagrams/${key}.mmd is newer than ${p.rel(src)}: redraw the diagram`, p.rel(mmd));
    }
    const hpath = join(ddir, key + ".hotspots.json");
    if (!p.fs.isfile(hpath)) continue;
    const spec = p.load_json(hpath, "DIAG", false);
    if (!isDict(spec)) continue;
    const hrel = p.rel(hpath);
    if (src.endsWith(".svg")) {
      const vb = svg_viewbox(p, src);
      const hvb = get(spec, "viewBox");
      if (vb && truthy(hvb)) {
        const hv = iter(hvb);
        let differs = hv.length !== 4;
        if (!differs) for (let i = 0; i < Math.min(hv.length, vb.length); i++) if (Math.abs(pyFloat(hv[i]!) - vb[i]!) > 0.05) { differs = true; break; }
        if (differs) {
          p.add("DIAG", "viewbox", "error", `${key}.hotspots.json viewBox ${str(hvb)} is not the svg's ${pyList(vb)}: the svg was redrawn; rerun ` +
            `diagram_hotspots.py --write`, hrel, p.locate('"viewBox"', [hpath])[1]);
        }
      }
    }
    const annotated = (iter(get(spec, "boxes", [])) as PyDict[]).filter((b) =>
      truthy(get(b, "href")) || truthy(get(b, "image")) || truthy(get(b, "note")) || truthy(get(b, "table")));
    const sh = annotated.length && src.endsWith(".svg") ? shapes(p, src) : null;
    if (annotated.length && src.endsWith(".svg") && sh === null) {
      p.note(`DIAG: diagram_hotspots.py could not be imported; hotspot labels on ${key} not checked`);
    }
    for (const b of annotated) {
      const lab = get(b, "label", "");
      const hl = p.locate_label(lab, hpath)[1];
      if (sh && !sh[0].has(lab as string) && !sh[1].some((t) => truthy(lab) && t.includes(lab as string))) {
        p.add("DIAG", "hotspot-label", "error", `hotspot '${str(lab)}' is not a box in ${key}.svg any more: rerun diagram_hotspots.py --write ` +
          `and move its href/image/note/table to the right box`, hrel, hl);
      }
      if (truthy(get(b, "image")) && !p.fs.isfile(join(p.root, str(get(b, "image"))))) {
        p.add("DIAG", "hotspot-image", "error", `hotspot '${str(lab)}' shows image ${str(get(b, "image"))}, which does not exist`, hrel, hl);
      }
      if (truthy(get(b, "table")) && !p.fs.isfile(join(p.root, "schema", str(get(b, "table")) + ".json"))) {
        p.add("DIAG", "hotspot-table", "error", `hotspot '${str(lab)}' opens table ${str(get(b, "table"))}, which has no schema/${str(get(b, "table"))}.json`, hrel, hl);
      }
    }
  }
  // legend tones as the compiler resolved them
  for (const n of nodes(get(or(p.content, new Map()), "sections", []))) {
    const t = get(n, "type");
    const its = t === "diagram" ? get(n, "legend") : t === "legend" ? get(n, "items") : null;
    for (const it of iter(or(its, [])) as PyDict[]) {
      const tone = or(get(it, "tone"), "");
      const classes = py.str.split(str(tone));
      const counts = new Map<string, number>();
      for (const x of classes) counts.set(x, (counts.get(x) ?? 0) + 1);
      const dup = py.sorted(new Set(classes.filter((x) => counts.get(x)! > 1)));
      const unknown = classes.filter((x) => !TONE_CLASSES.has(x));
      if (dup.length || unknown.length) {
        const lab = plain(get(it, "label", ""));
        const [f, l] = p.locate(re.compile(String.raw`\[legend:.*` + re.escape(py.cpSlice(lab, 0, 30))));
        const what = dup.length ? `repeats ${dup.join(", ")}` : `has unknown tone ${unknown.join(", ")}`;
        p.add("DIAG", "legend-tone", "warn", `legend item '${lab}' resolves to tone '${str(get(it, "tone"))}', which ${what} (e.g. \`= orange dashed\` ` +
          `already means dashed: write \`= orange\`)`, f, l);
      }
    }
  }
});
