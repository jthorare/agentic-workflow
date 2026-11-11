// tdd hotspots vs diagram_hotspots.py. (1) A seeded corpus of 300 tldraw-like SVGs (transforms with ties at .25 /
// .75, paths, rects, unparseable sizes, nested and self-closing groups, foreignObject rich text with entities, long
// titles, frames) run through one Python process and the TS in process: stdout, stderr, exit codes, and with --write
// the written files, including merges into seeded .hotspots.json files that carry odd JSON values. (2) Every diagram
// SVG of the two real packets, spawned as python3 -I and bun bin/tdd.ts on copies: print, --write over the existing
// file, and --write from scratch.
import { describe, expect, test } from "bun:test";
import { readdirSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { run } from "../../src/hotspots/shapes";
import { copyPacket, packets, py, pyJson, pyMainLoop, readUtf8, rmrf, rng, sameRun, snapshot, tempDir, ts } from "./_harness";

const COLORS = ["#099268", "#9fa8b2", "#4465E9", "#f1ac4b", "#e03131", "#e16919", "#ae3ec9", "#1d1d1d", "#123456", "none", "currentColor"];
const WORDS = ["Award", "record", "open_shift_post", "Queue", "D3", "PR 7", "ranking", "list", "·", "&amp;", "it&#x27;s", "é", "😀", "R2"];

function corpus(dir: string) {
  const r = rng(424242);
  const num = () => r.pick([0, 0.25, 0.75, 1.25, -2.75, 12.625, 100, 0.05, 1e-7, 33.333333, 2364.21875]) + r.int(-50, 900) * r.pick([1, 0.5, 0.1]);
  const fmt = (x: number) => (r.next() < 0.1 ? x.toFixed(2) : String(x));
  const text = (long: boolean) => {
    const n = long ? r.int(18, 40) : r.int(1, 6);
    return Array.from({ length: n }, () => r.pick(WORDS)).join(r.pick([" ", "  ", " \t "]));
  };
  const files: { path: string; labels: string[] }[] = [];
  for (let k = 0; k < 300; k++) {
    const [vw, vh] = [r.pick([400, 800, 1200.5, 2364.21875]), r.pick([300, 600.25, 1084])];
    let s = `<svg xmlns="http://www.w3.org/2000/svg" width="${vw}" height="${vh}" viewBox="${r.int(-60, 0)} ${r.int(-200, 0)} ${vw} ${vh}" ` +
      `class="tl-container tl-theme__light"><defs><style>@font-face {src: url("data:x<y>")}</style><clipPath id="c"><rect x="0" y="0" width="1" height="1"/></clipPath></defs>`;
    const labels: string[] = [];
    const nshapes = r.int(0, 9);
    for (let i = 0; i < nshapes; i++) {
      if (r.next() < 0.05) { s += `<g transform="matrix(1, 0, 0, 1, ${fmt(num())}, ${fmt(num())})"/>`; continue; }
      const wrap = r.next() < 0.08;
      if (wrap) s += `<g transform="translate(3, 4)">`;
      s += `<g transform="matrix(1, 0, 0, 1, ${fmt(num())}, ${fmt(num())})" opacity="1">`;
      const geo = r.int(0, 4);
      if (geo === 1 || geo === 2) {
        for (let p = r.int(1, 2); p > 0; p--) {
          const w = r.pick([0, 120.5, 300, 950.75, 2000]), h = r.pick([0, 60.25, 80, 700]);
          s += `<path d="M${fmt(r.int(-5, 5))},0 L${fmt(w)},0 L${fmt(w)},${fmt(h)} L0,${fmt(h)} Z" stroke="${r.pick(COLORS)}"` +
            (r.next() < 0.3 ? ` stroke-dasharray="4 2"` : "") + (r.next() < 0.1 ? " fill" : "") + `/>`;
        }
      } else if (geo === 3) {
        s += `<rect x="${fmt(num())}" y="${fmt(num())}" width="${r.pick(["120", "0", "auto", "33.5", "1e2"])}" height="${r.pick(["60", "45.25", "x"])}"/>`;
      }
      if (r.next() < 0.15) s += `<g transform="matrix(1, 0, 0, 1, 5, 5)"><path d="M0,0 L9,9" stroke="#e03131"/></g>`;
      if (r.next() < 0.85) {
        const long = r.next() < 0.15;
        const strong = r.next() < 0.6 ? text(false) : "";
        if (strong) labels.push(strong.replaceAll("&amp;", "&").replaceAll("&#x27;", "'"));
        s += `<foreignObject x="${fmt(r.int(0, 20))}" y="${fmt(r.int(0, 20))}" width="${r.pick(["200", "950", "1000", "0", "nope"])}" height="${r.pick(["40", "80.5", "300"])}">` +
          `<div xmlns="http://www.w3.org/1999/xhtml" class="tl-rich-text"><p dir="auto">` + (strong ? `<strong>${strong}</strong> ` : "") +
          `${text(long)}</p>` + (r.next() < 0.4 ? `<p dir="auto">${text(false)}</p>` : "") + `</div></foreignObject>`;
      }
      s += `</g>`;
      if (wrap) s += `</g>`;
    }
    s += `</svg>\n`;
    const path = join(dir, `d${String(k).padStart(3, "0")}.svg`);
    writeFileSync(path, s);
    files.push({ path, labels });
  }
  writeFileSync(join(dir, "plain.svg"), `<svg viewBox="0 0 10 10"><g transform="matrix(1,0,0,1,0,0)"></g></svg>`);
  return files;
}
function seed(files: { path: string; labels: string[] }[]) {
  files.forEach(({ path, labels }, i) => {
    if (i % 2 || !labels.length) return;
    const boxes = labels.map((l, j) => `{"label": ${JSON.stringify(l)}, "href": ${j % 3 ? '""' : '"#d3"'}, "image": null, ` +
      `"note": ${j % 2 ? "1.0" : '"kept note"'}, "table": ${j % 4 === 1 ? "[1, 2]" : '"t_' + j + '"'}}`);
    boxes.push(`{"label": "unused", "href": "#x"}`, `{"label": 7, "href": "#n"}`);
    writeFileSync(path.replace(/\.svg$/, ".hotspots.json"), `{"svg": "x", "boxes": [${boxes.join(", ")}]}`);
  });
}

describe("synthetic tldraw SVGs", () => {
  test("print, usage and error paths match the Python", () => {
    const dir = tempDir("shapes");
    try {
      const files = corpus(dir);
      const argvs = [...files.map((f) => [f.path]), [], [files[0]!.path, files[1]!.path], [join(dir, "plain.svg")], [join(dir, "missing.svg")]];
      const want = pyJson<[number, string, string][]>(pyMainLoop("diagram_hotspots.py"), argvs);
      const bad = argvs.filter((a, i) => !sameRun(run(a), want[i]!));
      expect(bad).toEqual([]);
      expect(want.filter((w) => w[0] === 0).length).toBe(files.length);
    } finally { rmrf(dir); }
  }, 60_000);

  test("--write creates and merges .hotspots.json byte for byte", () => {
    const dir = tempDir("shapes-w");
    try {
      const files = corpus(dir); seed(files);
      const argvs = files.map((f) => [f.path, "--write"]);
      const want = pyJson<[number, string, string][]>(pyMainLoop("diagram_hotspots.py"), argvs);
      const pyTree = snapshot(dir);
      for (const n of readdirSync(dir)) rmSync(join(dir, n));
      const again = corpus(dir); seed(again);
      const bad = argvs.filter((a, i) => !sameRun(run(a), want[i]!));
      expect(bad).toEqual([]);
      expect(snapshot(dir)).toEqual(pyTree);
      expect(want.filter((w) => /, [1-9]\d* annotated\)/.test(w[1])).length).toBeGreaterThan(20);
    } finally { rmrf(dir); }
  }, 60_000);
});

const PACKETS = packets();
describe.skipIf(!PACKETS.length)("real packets", () => {
  for (const pk of PACKETS) {
    test(`every diagram of ${pk.split("/").pop()}: print, --write over the existing file, --write from scratch`, () => {
      const root = tempDir("pk");
      try {
        const A = join(root, "py"), B = join(root, "ts");
        copyPacket(pk, A); copyPacket(pk, B);
        const svgs = readdirSync(join(A, "diagrams")).filter((n) => n.endsWith(".svg")).sort();
        expect(svgs.length).toBeGreaterThan(0);
        for (const svg of svgs) {
          const rel = join("diagrams", svg);
          expect(ts("hotspots", [rel], B)).toEqual(py("diagram_hotspots.py", [rel], A));
          expect(ts("hotspots", [rel, "--write"], B)).toEqual(py("diagram_hotspots.py", [rel, "--write"], A));
        }
        expect(snapshot(B)).toEqual(snapshot(A));
        for (const side of [A, B]) for (const n of readdirSync(join(side, "diagrams"))) if (n.endsWith(".hotspots.json")) rmSync(join(side, "diagrams", n));
        for (const svg of svgs) {
          const rel = join("diagrams", svg);
          const p = py("diagram_hotspots.py", [rel, "--write"], A);
          expect(p.code).toBe(0);
          expect(ts("hotspots", [rel, "--write"], B)).toEqual(p);
          const j = rel.replace(/\.svg$/, ".hotspots.json");
          expect(readUtf8(join(B, j))).toBe(readUtf8(join(A, j)));
        }
        expect(snapshot(B)).toEqual(snapshot(A));
      } finally { rmrf(root); }
    }, 120_000);
  }
});
