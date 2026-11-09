// The parity gate on real packets ($TDD_PARITY_PACKETS): green for two Python compiles with TDD_GENERATED pinned,
// red when the second run is not pinned (so the gate can fail), and py/json reproduces the fresh compiler output
// (content.json indent 1, content.files.json indent 2, both without a trailing newline) byte for byte.
import { describe, expect, test } from "bun:test";
import { chmodSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
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
      const r = gate(packet);
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
        const s = runCompiler("python", resolve(packet), `${name}.md`, out, { TDD_GENERATED: "2000-01-01T00:00:00Z", TZ: "UTC" }, true);
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
