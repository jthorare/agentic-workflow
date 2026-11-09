// os.path (posixpath) against CPython on 3,000 random paths: join, normpath, abspath, relpath, basename, dirname,
// splitext, expanduser, isabs, lstrip("./"), and build_tdd.home_rel.
import { describe, expect, test } from "bun:test";
import * as path from "../../src/py/path";
import { lstrip } from "../../src/py/str";
import { SKIP_REASON, hasPython, oracle } from "./oracle";

const T = 60_000;

describe("path: fixed cases", () => {
  test("R16 examples", () => {
    expect(path.join("/md", "/abs/x.svg")).toBe("/abs/x.svg");
    expect(path.basename("a/b/")).toBe("");
    expect(path.dirname("x")).toBe("");
    expect(path.relpath("/a/b", "/a/b")).toBe(".");
    expect(lstrip("./../x", "./")).toBe("x");
    expect(path.normpath("//a//b/../c")).toBe("//a/c");
  });
});

describe.skipIf(!hasPython)("path vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("every function on random paths (same cwd and HOME as this process)", () => {
    const g = oracle<{ cwd: string; home: string; rows: Record<string, any>[]; homerel: [unknown, unknown][] }>("path");
    expect(g.cwd).toBe(process.cwd());
    const bad: string[] = [];
    for (const r of g.rows) {
      const { p, q, rr = r.r } = r as { p: string; q: string; rr?: string };
      const got: Record<string, unknown> = {
        join2: path.join(p, q), join3: path.join(p, q, rr!), normpath: path.normpath(p), abspath: path.abspath(p),
        basename: path.basename(p), dirname: path.dirname(p), splitext: path.splitext(p), expanduser: path.expanduser(p),
        isabs: path.isabs(p), lstrip: lstrip(p, "./"), relpath: p ? path.relpath(p, q) : null, homerel: [r.homerel[0], path.homeRel(r.homerel[0])],
      };
      for (const k of Object.keys(got)) if (JSON.stringify(got[k]) !== JSON.stringify(r[k]) && bad.length < 10) bad.push(`${k}(${JSON.stringify(p)}, ${JSON.stringify(q)}): ${JSON.stringify(r[k])} != ${JSON.stringify(got[k])}`);
    }
    for (const [x, want] of g.homerel) if (JSON.stringify(path.homeRel(x)) !== JSON.stringify(want)) bad.push(`home_rel(${JSON.stringify(x)})`);
    expect(bad).toEqual([]);
  }, T);
});
