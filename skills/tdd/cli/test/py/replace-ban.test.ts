// The ban the port relies on: JS's string-pattern .replace("x", ...) changes only the first occurrence where Python's
// str.replace changes all of them. Every .ts / .js under src/ (src/py included), bin/ and parity/ calls py.replace
// (src/py/str.ts) or passes a RegExp; replace-scan.ts tokenizes each file, so a pattern on the next line, a const
// holding a string, s["replace"](...) or .replace.call(...) is caught too. No file is exempt.
import { expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { relative } from "node:path";
import { scan, scannedFiles } from "./replace-scan";

const ROOT = new URL("../..", import.meta.url).pathname;

test("no .replace( with a pattern that is not provably a RegExp in src/, bin/ or parity/", () => {
  const hits: string[] = [];
  let n = 0;
  for (const f of scannedFiles(ROOT)) {
    n++;
    const text = readFileSync(f, "utf8");
    for (const h of scan(text)) hits.push(`${relative(ROOT, f)}:${h.line}: ${h.why}: ${text.split("\n")[h.line - 1]!.trim()}`);
  }
  expect(n).toBeGreaterThan(30); // the walk found the tree
  expect(hits).toEqual([]);
});

test("the scanner catches the ways round a line grep", () => {
  const bad = [
    `s.replace(".", "-")`,
    `s.replace( 'a', b)`,
    "s.replace(`a`, b)",
    `s.replace(\n  ".",\n  "-",\n)`,
    `s?.replace(".", "-")`,
    `s.replace /* c */ (".", "-")`,
    `s["replace"](".", "-")`,
    `String.prototype.replace.call(s, ".", "-")`,
    `const DOT = "."; s.replace(DOT, "-")`,
    `const DOT = /\\./g; let DOT2 = "."; s.replace(DOT2, "-")`,
    `s.replace(cond ? "a" : "b", "-")`,
    `s.replace(pick(), "-")`,
    "const t = `${s.replace(\".\", \"-\")}`",
    "const t = `a ${`b ${s.replace(\".\", \"-\")}`}`",
    `const RX = /a/g; const RX2 = RX; RX2 = "x"; s.replace(RX2, "-")`,
  ];
  for (const src of bad) expect({ src, hits: scan(src).length }).toEqual({ src, hits: 1 });
  const good = [
    `s.replace(/\\./g, "-")`,
    `s.replace(\n  /\\./g,\n  "-")`,
    `s.replace(new RegExp("\\\\.", "g"), "-")`,
    `s.replace(RegExp("a", "g"), "-")`,
    `const DOT = /\\./g; s.replace(DOT, "-")`,
    `const WS: RegExp = new RegExp("\\\\s+", "g"); s.replace(WS, " ")`,
    `class A { private readonly RX = /a/g; f(s: string) { return s.replace(this.RX, "b"); } }`,
    `s.replace(pick() as RegExp, "-")`,
    `s.replaceAll(".", "-")`,
    `// s.replace(".", "-") in a comment`,
    `/* s.replace(".", "-") */`,
    `const doc = 's.replace(".", "-") in a string';`,
    "const doc = `s.replace(\".\", \"-\") in a template`;",
    `import * as py from "../py"; py.replace(s, ".", "-")`,
    `import * as str from "../src/py/str"; str.replace(s, ".", "-")`,
    `const r = a / 2; s.replace(/[/]/g, "-")`,
  ];
  for (const src of good) expect({ src, hits: scan(src) }).toEqual({ src, hits: [] });
});
