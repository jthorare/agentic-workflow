// The HTML guard, Python against TypeScript: build_tdd.sanitize_html and src/compile/sanitize.ts on the review's 105
// vectors plus the split-tag bypasses (fixtures/sanitize-vectors.json) give identical output, and no split tag
// reassembles a blocked one (script, iframe, object, embed, base, meta, link, style, template).
import { describe, expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { spawnSync } from "bun";
import { sanitize_html } from "../../src/compile/sanitize";
import { SKIP_REASON, hasPython } from "../py/oracle";

const CLI = resolve(new URL("../..", import.meta.url).pathname);
const BUILD_TDD = resolve(CLI, "..", "scripts", "build_tdd.py");
const VECTORS: string[] = JSON.parse(readFileSync(join(CLI, "test", "compile", "fixtures", "sanitize-vectors.json"), "utf8")).vectors;
const BLOCKED = /<\s*\/?\s*(script|iframe|object|embed|base|meta|link|style|template)\b/i;
const SPLIT = VECTORS.filter((v) => /<[a-z]+<\/?[a-z]+>[a-z]+/i.test(v));

describe.skipIf(!hasPython)("sanitize_html: Python and TypeScript agree", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  test(`${VECTORS.length} vectors give identical output`, () => {
    const code = `
import importlib.util, json, sys
s = importlib.util.spec_from_file_location("build_tdd", ${JSON.stringify(BUILD_TDD)}); m = importlib.util.module_from_spec(s)
sys.argv = ["build_tdd.py"]
try: s.loader.exec_module(m)
except SystemExit: pass
print(json.dumps([m.sanitize_html(v) for v in json.load(sys.stdin)]))`;
    const r = spawnSync(["python3", "-I", "-B", "-W", "ignore", "-c", code], { stdin: new TextEncoder().encode(JSON.stringify(VECTORS)), stdout: "pipe", stderr: "pipe" });
    expect(r.exitCode).toBe(0);
    const py: string[] = JSON.parse(r.stdout.toString());
    expect(VECTORS.length).toBe(115);
    const diffs = VECTORS.map((v, k) => [v, py[k], sanitize_html(v)] as const).filter(([, a, b]) => a !== b);
    expect(diffs).toEqual([]);
  }, 60_000);
});

test("split tags never reassemble a blocked tag", () => {
  expect(SPLIT.length).toBeGreaterThanOrEqual(10);
  for (const v of SPLIT) expect({ v, out: BLOCKED.test(sanitize_html(v)) }).toEqual({ v, out: false });
});
