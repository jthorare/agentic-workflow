// The file-reading half of the D1 surface on inputs a compile rarely meets: this test writes SVG, PNG, JSON and other
// files (hand-picked edge cases plus a seeded batch) into a temporary folder, parity/oracle.py sd_files runs
// build_doc's img_size, svg_size, png_size, load_json and Assets(inline=True).ref(embed=True) over each, and src/sd
// must give the same results, exceptions and SystemExit texts.
import { describe, expect, test } from "bun:test";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { bd } from "../../src/sd";
import { SKIP_REASON, attempt, hasPython, runOracle, same, show, tagOf, uriDigest, type Outcome } from "./harness";

const enc = (s: string) => Buffer.from(s, "utf8");
const png = (w: number, h: number, extra = 0) => {
  const b = Buffer.alloc(24 + extra);
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]).copy(b);
  b.writeUInt32BE(w >>> 0, 16);
  b.writeUInt32BE(h >>> 0, 20);
  return b;
};

/** name -> bytes: the hand-picked cases. */
function fixed(): [string, Uint8Array][] {
  const svg = (attrs: string) => enc(`<?xml version="1.0"?>\n<svg xmlns="http://www.w3.org/2000/svg" ${attrs}><rect/></svg>`);
  return [
    ["a.svg", svg('width="100" height="50"')],
    ["b-px.svg", svg('width="10.5px" height="20.5px"')],
    ["c-half.svg", svg('width="0.5" height="1.5" viewBox="0 0 9 9"')],
    ["d-viewbox.svg", svg('viewBox="0 0 640.5 480"')],
    ["e-commas.svg", svg('viewBox="0,0,1.5,2.5"')],
    ["f-neg.svg", svg('viewBox="-10 -20.5 300 200"')],
    ["g-bad.svg", svg('width="1.2.3" height="4"')],
    ["h-dot.svg", svg('width="." height="4"')],
    ["i-arabic.svg", svg('width="١٢" height="٣"')],
    ["j-huge.svg", svg(`width="1${"0".repeat(400)}" height="1"`)],
    ["k-none.svg", enc("<html><body>no svg here</body></html>")],
    ["l-late.svg", enc(" ".repeat(5000) + '<svg width="1" height="1"></svg>')],
    ["m-split.svg", Buffer.concat([enc('<svg width="7" height="8" data-x="'), Buffer.from("é".repeat(2100), "utf8"), enc('">')])],
    ["n-latin1.svg", Buffer.concat([enc('<svg width="3"'), Buffer.from([0xff, 0xfe]), enc(' height="4">')])],
    ["o-upper.SVG", svg('width="11" height="12"')],
    ["p-tag-upper.svg", enc('<SVG width="1" height="2">')],
    ["q-multiline.svg", enc('<svg\n  width="3"\n  height="4"\n>')],
    ["r-onlywidth.svg", svg('width="5"')],
    ["s-datawidth.svg", svg('data-width="5" data-height="6"')],
    ["t-empty.svg", new Uint8Array()],
    ["u-ok.png", png(1, 1)],
    ["v-big.png", png(4000, 3000, 100)],
    ["w-max.PNG", png(0xffffffff, 0x7fffffff)],
    ["x-trunc.png", png(1, 1).subarray(0, 10)],
    ["x-sigonly.png", png(1, 1).subarray(0, 8)],
    ["y-jpeg.png", Buffer.from([0xff, 0xd8, 0xff, 0xe0, 0, 16, 0x4a, 0x46, 0x49, 0x46, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0])],
    ["z-empty.png", new Uint8Array()],
    ["photo.jpg", Buffer.from([0xff, 0xd8, 0xff, 0xd9])],
    ["anim.gif", enc("GIF89a")],
    ["pic.webp", enc("RIFF....WEBP")],
    ["data.bin", Buffer.from([0, 1, 2, 3])],
    ["notes.txt", enc("plain")],
    ["draw.svg.gz", Buffer.from([0x1f, 0x8b])],
    ["meta.json", enc('{"title": "T", "n": 1, "f": 1.0, "neg": -0.0, "big": 123456789012345678901234567890, "e": 1e400, "nan": NaN, "a": [1, 2.5e-7, {"k": null}], "dup": 1, "dup": 2, "u": "\\u00e9\\ud83d\\ude00"}')],
    ["bom.json", Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), enc("{}")])],
    ["badutf8.json", Buffer.concat([enc('{"a": "'), Buffer.from([0xc3, 0x28]), enc('"}')])],
    ["trunc.json", enc('{"a": [1, 2')],
    ["crlf.json", enc('{\r\n "a": 1,\r\n "b": ,\r\n}')],
    ["empty.json", new Uint8Array()],
    ["list.json", enc("[1, 2, 3]\n")],
    ["trailing.json", enc('{"a": 1} x')],
    // CPython's int-digit limit (4,300) and json nesting limit (JSON_MAX_DEPTH), each against what the scanner meets first
    ["int-4300.json", enc("[" + nines(4300) + "]")],
    ["int-4301.json", enc(nines(4301))],
    ["int-neg.json", enc('{"n": -' + nines(5000) + "}")],
    ["int-dup.json", enc('{"a": ' + nines(5000) + ', "a": 1}')],
    ["int-before-error.json", enc("[" + nines(5000) + ", ]")],
    ["int-after-error.json", enc('{"a" ' + nines(5000) + "}")],
    ["int-extra.json", enc("1 " + nines(5000))],
    ["int-in-string.json", enc('["' + nines(5000) + '"]')],
    ["int-float.json", enc(nines(5000) + ".5")],
    ["int-exp.json", enc("[" + nines(5000) + "e2, 0" + nines(5000) + "]")],
    ["deep-9998.json", enc("[".repeat(9998) + "]".repeat(9998))],
    ["deep-9999.json", enc("[".repeat(9999) + "]".repeat(9999))],
    ["deep-obj-9999.json", enc('{"a":'.repeat(9999) + "1" + "}".repeat(9999))],
    ["deep-mixed.json", enc(Array.from({ length: 10000 }, (_, i) => (i % 2 ? '{"k":' : "[")).join("") + "0")],
    ["deep-after-error.json", enc("[1 " + "[".repeat(10000))],
    ["deep-then-int.json", enc("[".repeat(9999) + nines(5000))],
    ["deep-int-first.json", enc("[" + nines(5000) + ", " + "[".repeat(9999))],
    ["deep-huge.json", enc("[".repeat(200000))],
    ["deep-in-string.json", enc('["' + "[".repeat(20000) + '"]')],
  ];
}

const nines = (n: number) => "9".repeat(n);

/** The container nesting of a parsed value, iteratively (as the oracle's json_depth). */
function depth(v: unknown): number {
  let best = 0;
  const stack: [unknown, number][] = [[v, 1]];
  while (stack.length) {
    const [x, d] = stack.pop()!;
    if (Array.isArray(x) || x instanceof Map) {
      best = Math.max(best, d);
      for (const y of Array.isArray(x) ? x : x.values()) stack.push([y, d + 1]);
    }
  }
  return best;
}

/** A seeded batch of SVG root tags and PNG headers (mulberry32). */
function seeded(n: number): [string, Uint8Array][] {
  let a = 0x5eed;
  const rnd = () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  const pick = <T>(xs: T[]) => xs[Math.floor(rnd() * xs.length)]!;
  const num = () => pick(["0", "1", "2.5", "3.5", "0.5", "1.5", "640", "12.25", "99.999", "1e3", "-4", ".5", "5.", "١٢", "７", "", "1_0", "00012", "7.4999999999999999"]);
  const out: [string, Uint8Array][] = [];
  for (let i = 0; i < n; i++) {
    if (rnd() < 0.75) {
      const parts: string[] = [];
      if (rnd() < 0.7) parts.push(`width="${num()}${pick(["", "", "px", "em", "%"])}"`);
      if (rnd() < 0.7) parts.push(`height="${num()}${pick(["", "", "px"])}"`);
      if (rnd() < 0.6) parts.push(`viewBox="${num()}${pick([" ", ",", ", ", "  "])}${num()} ${num()}${pick([" ", ","])}${num()}"`);
      if (rnd() < 0.2) parts.push(pick(['class="x"', "data-w='1'", 'xmlns="http://www.w3.org/2000/svg"', "\n"]));
      parts.sort(() => rnd() - 0.5);
      out.push([`seed-${String(i).padStart(3, "0")}.svg`, enc(`${pick(["", "<?xml?>", "<!-- c -->\n"])}<svg ${parts.join(" ")}>`)]);
    } else {
      const w = Math.floor(rnd() * 2 ** 32);
      const h = Math.floor(rnd() * 2 ** 32);
      const b = png(w, h);
      out.push([`seed-${String(i).padStart(3, "0")}.png`, rnd() < 0.1 ? b.subarray(0, 8 + Math.floor(rnd() * 16)) : b]);
    }
  }
  return out;
}

interface Golden {
  rows: { name: string; img_size: Outcome; svg_size: Outcome; png_size: Outcome; load_json: Outcome; embed: Outcome; state: unknown }[];
  extra: [string | null, Outcome][];
  published_for: [string, string, Outcome][];
}

describe.skipIf(!hasPython)("img_size, load_json and Assets on written files", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  test("every file: the same sizes, data: URLs, parsed JSON, exceptions and exit texts", () => {
    const dir = mkdtempSync(join(tmpdir(), "tdd-sd-files-"));
    try {
      const files = [...fixed(), ...seeded(400)];
      for (const [name, bytes] of files) writeFileSync(join(dir, name), bytes);
      mkdirSync(join(dir, "zz-dir.svg"));
      const g = runOracle<Golden>("sd_files", ["--dir", dir]);
      expect(g.rows).toHaveLength(files.length + 1);
      const diffs: string[] = [];
      for (const row of g.rows) {
        const p = join(dir, row.name);
        const a = new bd.Assets(dir, true, false);
        const got = {
          name: row.name,
          img_size: attempt(() => bd.img_size(p)),
          svg_size: attempt(() => bd.svg_size(p)),
          png_size: attempt(() => bd.png_size(p)),
          load_json: row.name.startsWith("deep-") ? attempt(() => depth(bd.load_json(p, "--meta"))) : attempt(() => bd.load_json(p, "--meta")),
          embed: attempt(() => uriDigest(a.ref(row.name.startsWith("x") ? "../" + row.name : row.name, [null, "", p], true))),
          state: tagOf({ out_dir: a.out_dir, inline: a.inline, copy: a.copy, files: a.files, missing: a.missing, order: a.order }),
        };
        for (const k of Object.keys(got) as (keyof typeof got)[])
          if (!same(row[k], got[k])) diffs.push(`${row.name} ${k}: python ${show(row[k])} ts ${show(got[k])}`);
      }
      for (const [p, want] of g.extra) {
        const got = p && /\.(svg|png)$/i.test(p) ? attempt(() => bd.img_size(p)) : attempt(() => bd.load_json(p, "schema/x.json"));
        if (!same(want, got)) diffs.push(`load_json(${show(p)}): python ${show(want)} ts ${show(got)}`);
      }
      for (const [p, prefix, want] of g.published_for) {
        const got = attempt(() => bd.published_for(p, prefix));
        if (!same(want, got)) diffs.push(`published_for(${show(p)}, ${show(prefix)}): python ${show(want)} ts ${show(got)}`);
      }
      const raised = g.rows.filter((r) => Object.values(r).some((v) => typeof v === "object" && v !== null && "x" in v)).length;
      console.log(`sd files: ${g.rows.length} files, ${g.extra.length} missing or odd paths, ${g.published_for.length} published_for cases, ${diffs.length} diffs (${raised} files where Python raised or exited)`);
      expect(diffs).toEqual([]);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, 120_000);
});
