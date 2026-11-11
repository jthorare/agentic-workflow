// The component catalog (templates/components.json) against the compiler and the renderer: every example compiles,
// without a warning, to the node type its entry names; every node type the renderer draws is in the catalog; the
// renderer has a case for each; and the gallery packet built from the catalog is one both compilers agree on.
import { describe, expect, test } from "bun:test";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { NODE_TYPES } from "../../../app/src/types";
import { main as components } from "../../bin/components";
import { gate } from "../../parity/gate";
import { compile, parse_args } from "../../src/compile";
import { SKILL, exampleSource, loadCatalog, writeGallery } from "../../src/components";
import { SKIP_REASON, hasPython } from "../py/oracle";

const cat = loadCatalog();
const T = 120_000;

function types(v: unknown, out = new Set<string>()): Set<string> {
  if (Array.isArray(v)) for (const x of v) types(x, out);
  else if (v && typeof v === "object") {
    const o = v as Record<string, unknown>;
    if (typeof o.type === "string") out.add(o.type);
    for (const k in o) types(o[k], out);
  }
  return out;
}

/** Compile `source` as <name>.md inside a fresh gallery packet (so image, diagram and PR paths resolve). */
function compiled(name: string, source: string): { warnings: string[]; types: Set<string> } {
  const dir = mkdtempSync(join(tmpdir(), "tdd-cat-"));
  try {
    const packet = join(dir, "p");
    writeGallery(packet, cat);
    writeFileSync(join(packet, `${name}.md`), source);
    const r = compile(parse_args([join(packet, `${name}.md`), join(packet, "build"), "--meta", join(packet, "meta.json"), "--no-copy"]));
    const content = JSON.parse(readFileSync(join(packet, "build", "content.json"), "utf8"));
    return { warnings: r.warnings, types: types([content.lede, content.sections.map((s: { blocks: unknown }) => s.blocks)]) };
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

describe("the component catalog", () => {
  test("names are unique and every entry is complete", () => {
    const names = cat.components.map((c) => c.name);
    expect(new Set(names).size).toBe(names.length);
    for (const c of cat.components) {
      expect([c.name, ["markdown", "container", "marker", "inline"].includes(c.kind)]).toEqual([c.name, true]);
      for (const k of ["what", "syntax", "where", "renders", "example"] as const) expect([c.name, k, c[k].trim().length > 0]).toEqual([c.name, k, true]);
    }
  });

  test("the node types it lists are exactly the ones the renderer draws (app/src/types.ts NODE_TYPES)", () => {
    const listed = new Set([...cat.components.flatMap((c) => (c.node ? [c.node] : [])), ...Object.keys(cat.internal)]);
    expect([...listed].sort()).toEqual([...NODE_TYPES].sort());
  });

  test("the renderer has a case for every node type", () => {
    const blocks = readFileSync(join(SKILL, "app", "src", "components", "Blocks.tsx"), "utf8");
    expect(NODE_TYPES.filter((t) => !blocks.includes(`case "${t}":`))).toEqual([]);
  });

  for (const c of cat.components) {
    test(`${c.name}: its example compiles without a warning${c.node ? ` to a ${c.node} node` : ""}`, () => {
      const r = compiled(c.name, exampleSource(c));
      expect(r.warnings).toEqual([]);
      if (c.node) expect([...r.types]).toContain(c.node);
      expect([...r.types].filter((t) => t === "box" || t === "placeholder")).toEqual([]);
    }, T);
  }
});

describe("the gallery packet", () => {
  test("holds every listed node type, and refuses a folder that is not empty", () => {
    const dir = mkdtempSync(join(tmpdir(), "tdd-gal-"));
    try {
      const packet = join(dir, "gallery");
      const files = writeGallery(packet, cat);
      expect(files).toContain("gallery.md");
      const r = compile(parse_args([join(packet, "gallery.md"), join(packet, "build"), "--meta", join(packet, "meta.json"), "--no-copy"]));
      expect(r.warnings).toEqual([]);
      const content = JSON.parse(readFileSync(join(packet, "build", "content.json"), "utf8"));
      const seen = types(content.sections.map((s: { blocks: unknown }) => s.blocks));
      expect(cat.components.flatMap((c) => (c.node && !seen.has(c.node) ? [c.name] : []))).toEqual([]);
      expect(() => writeGallery(packet, cat)).toThrow(/not empty/);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);

  test.skipIf(!hasPython)("build_tdd.py and the TypeScript compiler agree on it byte for byte", () => {
    const dir = mkdtempSync(join(tmpdir(), "tdd-gal-"));
    try {
      const packet = join(dir, "gallery");
      writeGallery(packet, cat);
      expect(gate(packet, { src: "gallery.md" }).problems).toEqual([]);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);
  if (!hasPython) console.warn(SKIP_REASON);
});

describe("tdd components", () => {
  const run = async (argv: string[]) => {
    const out: string[] = [], err: string[] = [];
    const o = process.stdout.write, e = process.stderr.write;
    process.stdout.write = ((s: string) => { out.push(String(s)); return true; }) as typeof process.stdout.write;
    process.stderr.write = ((s: string) => { err.push(String(s)); return true; }) as typeof process.stderr.write;
    try { return { code: await components(argv), out: out.join(""), err: err.join("") }; }
    finally { process.stdout.write = o; process.stderr.write = e; }
  };
  test("lists every component, describes one, and prints JSON", async () => {
    const list = await run([]);
    expect(list.code).toBe(0);
    expect(list.out.trimEnd().split("\n").length).toBe(cat.components.length);
    const one = await run(["persona"]);
    expect(one.out).toContain("::: persona");
    expect(one.out).toContain("Attributes:");
    expect(JSON.parse((await run(["--json", "fold"])).out).node).toBe("fold");
    expect(JSON.parse((await run(["--json"])).out).components.length).toBe(cat.components.length);
  });
  test("an unknown name exits 1 and names the choices; bad usage exits 2", async () => {
    const r = await run(["nope"]);
    expect([r.code, r.err.includes("one of: section")]).toEqual([1, true]);
    expect((await run(["--gallery"])).code).toBe(2);
    expect((await run(["a", "b"])).code).toBe(2);
  });
});
