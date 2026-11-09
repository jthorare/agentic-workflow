// The parity gate on real packets ($TDD_PARITY_PACKETS): green for two Python compiles with TDD_GENERATED pinned
// (Python against TypeScript is test/compile/compile.test.ts),
// red when the second run is not pinned (so the gate can fail), and py/json reproduces the fresh compiler output
// (content.json indent 1, content.files.json indent 2, both without a trailing newline) byte for byte.
import { describe, expect, test } from "bun:test";
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join, resolve } from "node:path";
import { gate, runCompiler } from "../../parity/gate";
import { decodeUtf8 } from "../../src/py/io";
import { dumps, loads } from "../../src/py/json";
import { PACKETS, hasPython } from "../py/oracle";

const T = 120_000;
const BUILD_TDD = resolve(new URL("../../../scripts/build_tdd.py", import.meta.url).pathname);

describe.skipIf(PACKETS.length === 0 || !hasPython)("parity gate on real packets", () => {
  for (const packet of PACKETS) {
    const name = basename(packet);

    test(`${name}: two pinned Python compiles are identical (gate green)`, () => {
      const r = gate(packet, { b: "python" });
      expect(r.problems).toEqual([]);
      expect(r.green).toBe(true);
      expect(r.summary).toMatch(/^\d+ sections, \d+ diagrams, .* \d+ files \(\d+ missing\)$/);
    }, T);

    test(`${name}: an unpinned second run turns the gate red`, () => {
      const d = mkdtempSync(join(tmpdir(), "tdd-gate-red-"));
      try {
        const exe = join(d, "unpinned.sh");
        writeFileSync(exe, `#!/bin/sh\nTDD_GENERATED= exec python3 -I -B "${BUILD_TDD}" "$@"\n`);
        chmodSync(exe, 0o755);
        const r = gate(packet, { b: exe, generated: "1999-01-01T00:00:00Z" });
        expect(r.green).toBe(false);
        expect(r.problems.some((p) => p.startsWith("content.json: first difference") && p.includes("(line 3;"))).toBe(true);
      } finally {
        rmSync(d, { recursive: true, force: true });
      }
    }, T);

    test(`${name}: py/json reproduces the fresh compiler output byte for byte`, () => {
      const d = mkdtempSync(join(tmpdir(), "tdd-gate-json-"));
      try {
        const out = join(d, "build");
        const s = runCompiler("python", resolve(packet), `${name}.md`, out, { TDD_GENERATED: "2000-01-01T00:00:00Z", TZ: "UTC" });
        expect(s.exit).toBe(0);
        const c = decodeUtf8(readFileSync(join(out, "content.json")));
        const m = decodeUtf8(readFileSync(join(out, "content.files.json")));
        expect(dumps(loads(c), { indent: 1, ensureAscii: false })).toBe(c);
        expect(dumps(loads(m), { indent: 2, ensureAscii: false })).toBe(m);
      } finally {
        rmSync(d, { recursive: true, force: true });
      }
    }, T);
  }
});

describe("gate --out never deletes what it did not write", () => {
  // a stand-in compiler: writes content.json, content.files.json and sub/x.txt into its out argument
  const fake = (d: string) => {
    const exe = join(d, "fake.sh");
    writeFileSync(exe, `#!/bin/sh\nmkdir -p "$2/sub" && printf '{}' > "$2/content.json" && printf '{}' > "$2/content.files.json" && echo x > "$2/sub/x.txt" && echo "wrote $2/content.json: 1 sections"\n`);
    chmodSync(exe, 0o755);
    mkdirSync(join(d, "pk"));
    writeFileSync(join(d, "pk", "pk.md"), "# pk\n");
    return exe;
  };

  test("a folder with a foreign file is refused and left intact; the gate's own output can be re-gated", () => {
    const d = mkdtempSync(join(tmpdir(), "tdd-gate-out-"));
    try {
      const exe = fake(d);
      const out = join(d, "out");
      mkdirSync(out);
      writeFileSync(join(out, "notes.txt"), "mine");
      expect(() => gate(join(d, "pk"), { a: exe, b: exe, out })).toThrow("the gate did not write (notes.txt)");
      expect(readFileSync(join(out, "notes.txt"), "utf8")).toBe("mine");

      const fresh = join(d, "fresh");
      const r = gate(join(d, "pk"), { a: exe, b: exe, out: fresh });
      expect(r.problems).toEqual([]);
      expect(r.b.files).toBe(3);
      expect(existsSync(join(fresh, "sub", "x.txt"))).toBe(true); // B's output stays
      expect(gate(join(d, "pk"), { a: exe, b: exe, out: fresh }).green).toBe(true); // its own earlier output: re-gated

      writeFileSync(join(fresh, "sub", "draft.md"), "mine");
      expect(() => gate(join(d, "pk"), { a: exe, b: exe, out: fresh })).toThrow("the gate did not write (sub/draft.md)");
      expect(readFileSync(join(fresh, "sub", "draft.md"), "utf8")).toBe("mine");
      expect(existsSync(join(fresh, "content.json"))).toBe(true);
    } finally {
      rmSync(d, { recursive: true, force: true });
    }
  });
});
