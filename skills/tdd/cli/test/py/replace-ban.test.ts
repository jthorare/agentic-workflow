// The ban the port relies on: JS's string-pattern .replace("x", ...) changes only the first occurrence where Python's
// str.replace changes all of them. Outside src/py, code calls py.replace (src/py/str.ts) or uses a regex with /g.
import { expect, test } from "bun:test";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const SRC = new URL("../../src", import.meta.url).pathname;
const BANNED = /\.replace\(\s*["'`]/;

function* files(dir: string): Generator<string> {
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) yield* files(p);
    else if (/\.(ts|tsx|js|mjs)$/.test(name)) yield p;
  }
}

test("no string-pattern .replace( in src/ outside src/py", () => {
  const hits: string[] = [];
  for (const f of files(SRC)) {
    const rel = relative(SRC, f);
    if (rel.startsWith("py/")) continue;
    readFileSync(f, "utf8").split("\n").forEach((line, i) => {
      if (BANNED.test(line)) hits.push(`src/${rel}:${i + 1}: ${line.trim()}`);
    });
  }
  expect(hits).toEqual([]);
});

test("the ban's pattern catches what it should", () => {
  expect(BANNED.test(`s.replace(".", "-")`)).toBe(true);
  expect(BANNED.test("s.replace( 'a', b)")).toBe(true);
  expect(BANNED.test("s.replace(/\\./g, \"-\")")).toBe(false);
  expect(BANNED.test("s.replaceAll(\".\", \"-\")")).toBe(false);
});
