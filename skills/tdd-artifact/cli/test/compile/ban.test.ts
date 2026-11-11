// The compiler's share of the port's bans (review M6). The .replace ban (test/py/replace-ban.test.ts) walks src/, bin/
// and parity/ recursively; this asserts its walk reaches src/compile, src/vocab.ts and bin/build-tdd.ts and that they
// pass it. The kernel ban src/sd holds itself to (test/sd/ban.test.ts: no regex literal or RegExp, no JS case, trim,
// JSON or number parsing, no .split / .match / .replace on a plain string) holds for the compiler too: everything
// Python-sensitive goes through src/py. A kernel call is py.<module>.<fn>(...) or a compiled pattern (an ALL_CAPS
// constant, `re`, `mark`); the check here follows a dotted receiver back to its root, so py.path.split counts as the
// kernel and x.path.split does not. bin/build-tdd.ts's shebang line is not code and is left out.
import { describe, expect, test } from "bun:test";
import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { scan, scannedFiles, tokenize } from "../py/replace-scan";

const CLI = new URL("../..", import.meta.url).pathname;
const COMPILE = join(CLI, "src", "compile");
const COMPILER_FILES = [
  ...readdirSync(COMPILE).filter((f) => f.endsWith(".ts")).map((f) => join(COMPILE, f)),
  join(CLI, "src", "vocab.ts"),
  join(CLI, "bin", "build-tdd.ts"),
];

const BANNED_IDS = new Set(["RegExp", "toLowerCase", "toUpperCase", "toLocaleLowerCase", "toLocaleUpperCase", "trim", "trimStart", "trimEnd",
  "localeCompare", "JSON", "Number", "parseInt", "parseFloat", "normalize"]);
const PATTERN_METHODS = new Set(["match", "search", "split", "replace", "replaceAll", "matchAll"]);

function violations(src: string): string[] {
  const code = src.startsWith("#!") ? src.slice(src.indexOf("\n")) : src;
  const out: string[] = [];
  if (code.includes("\\p{") || code.includes("\\P{")) out.push("a \\p{...} class");
  const toks = tokenize(code);
  toks.forEach((t, k) => {
    if (t.kind === "regex") out.push(`${t.line}: regex literal ${t.value}`);
    if (t.kind === "id" && BANNED_IDS.has(t.value)) out.push(`${t.line}: ${t.value}`);
    if (t.kind === "id" && PATTERN_METHODS.has(t.value) && toks[k - 1]?.value === "." && toks[k + 1]?.value === "(") {
      let j = k - 2; // the receiver; walk a dotted chain (py.str.split) back to its root
      while (toks[j]?.kind === "id" && toks[j - 1]?.value === "." && toks[j - 2]?.kind === "id") j -= 2;
      const recv = toks[k - 2];
      const root = toks[j];
      const ok = recv?.kind === "id" && (/^[A-Z][A-Z0-9_]*$/.test(recv.value) || ["mark", "re", "py"].includes(recv.value) || root?.value === "py");
      if (!ok) out.push(`${t.line}: .${t.value}( on ${recv?.value ?? "?"}`);
    }
  });
  return out;
}

describe("the compiler's bans (review M6)", () => {
  test("the .replace ban's walk includes src/compile, src/vocab.ts and bin/build-tdd.ts", () => {
    const walked = new Set(scannedFiles(CLI).map((f) => relative(CLI, f)));
    const want = COMPILER_FILES.map((f) => relative(CLI, f));
    expect(want.length).toBeGreaterThanOrEqual(10); // eight modules in src/compile, vocab, the bin
    for (const f of ["src/compile/compiler.ts", "src/compile/sanitize.ts", "src/compile/attrs.ts", "src/vocab.ts", "bin/build-tdd.ts"]) expect(want).toContain(f);
    expect(want.filter((f) => !walked.has(f))).toEqual([]);
  });

  test("they pass the .replace ban", () => {
    const hits = COMPILER_FILES.flatMap((f) => scan(readFileSync(f, "utf8")).map((h) => `${relative(CLI, f)}:${h.line}: ${h.why}`));
    expect(hits).toEqual([]);
  });

  test("they use the kernel for every Python-sensitive operation (no regex literal, RegExp, JS split, case or trim)", () => {
    const hits = COMPILER_FILES.flatMap((f) => violations(readFileSync(f, "utf8")).map((v) => `${relative(CLI, f)}:${v}`));
    expect(hits).toEqual([]);
  });

  test("the check catches what it bans", () => {
    expect(violations("const a = /x/i;")).toHaveLength(1);
    expect(violations('const a = new RegExp("x");')).toHaveLength(1);
    expect(violations("s.toLowerCase()")).toHaveLength(1);
    expect(violations('text.split("\\n")')).toHaveLength(1);
    expect(violations('m.group(1)!.split(";")')).toHaveLength(1);
    expect(violations('x.path.split("/")')).toHaveLength(1);
    expect(violations('s.match("x")')).toHaveLength(1);
    expect(violations("JSON.stringify(x)")).toHaveLength(1);
    expect(violations('const p = "\\\\p{L}";')).toHaveLength(1);
    expect(violations('#!/usr/bin/env bun\nconst a = /x/;')).toEqual(["2: regex literal /x/"]);
    expect(violations('#!/usr/bin/env bun\nTOKEN.sub("", s)')).toEqual([]);
    expect(violations('HEAD.match(l); re.sub("a", "b", s); re.split("x", s); py.replace(s, "a", "b"); py.str.split(s, "\\n"); py.path.split(p)')).toEqual([]);
  });
});
