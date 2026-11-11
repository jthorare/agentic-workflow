// lint_packet.py and tdd lint on the real packets ($TDD_PARITY_PACKETS, ":"-separated; skipped when unset): the
// exit code, stdout, stderr and lint.json identical, on the packet's build and with --compile (each linter compiles
// the source with its own compiler; PARITY then compares the two).
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { existsSync } from "node:fs";
import { basename, join } from "node:path";
import { HAVE_PYTHON, pyLint, rmrf, tempDir, tsLint } from "./harness";

const PACKETS = (process.env.TDD_PARITY_PACKETS ?? "").split(":").filter((p) => p && existsSync(p));

describe.skipIf(!HAVE_PYTHON || PACKETS.length === 0)("tdd lint on real packets ($TDD_PARITY_PACKETS)", () => {
  let out = "";
  beforeAll(() => { out = tempDir("packets"); });
  afterAll(() => rmrf(out));

  for (const [i, pk] of PACKETS.entries()) {
    for (const extra of [[], ["--compile"]]) {
      test(`packet ${i + 1} (${basename(pk)}) ${extra.join(" ") || "build"}`, () => {
        const scratch = join(out, `${i}${extra.length ? "c" : "b"}`);
        Bun.spawnSync(["mkdir", "-p", scratch]);
        const py = pyLint([pk, ...extra], out, scratch);
        const ts = tsLint([pk, ...extra], out, scratch);
        expect(py.err).not.toContain("Traceback");
        expect(ts.out).toBe(py.out);
        expect(ts.err).toBe(py.err);
        expect(ts.json).toBe(py.json);
        expect(ts.code).toBe(py.code);
      }, 120_000);
    }
  }
});
