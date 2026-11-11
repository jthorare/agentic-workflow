// src/sd does every string, regex, number and JSON operation through the kernel (src/py), whose semantics are
// CPython's. This rejects the JS built-ins that differ from Python (README "Traps"): a regex literal or RegExp (JS
// \w \d \s \b $ . and the i flag), \p{...} classes, toLowerCase / toUpperCase / trim* / localeCompare, JSON, Number(),
// parseInt / parseFloat, and a .match / .search / .split / .replace / .replaceAll / .matchAll call on anything but a
// kernel pattern or the kernel itself (an ALL_CAPS constant, `mark`, `re`, `py`, `py.str`).
import { expect, test } from "bun:test";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { tokenize } from "../py/replace-scan";
import { CLI } from "./harness";

const BANNED_IDS = new Set(["RegExp", "toLowerCase", "toUpperCase", "toLocaleLowerCase", "toLocaleUpperCase", "trim", "trimStart", "trimEnd",
  "localeCompare", "JSON", "Number", "parseInt", "parseFloat", "normalize"]);
const PATTERN_METHODS = new Set(["match", "search", "split", "replace", "replaceAll", "matchAll"]);

export function violations(src: string): string[] {
  const out: string[] = [];
  if (src.includes("\\p{") || src.includes("\\P{")) out.push("a \\p{...} class");
  const toks = tokenize(src);
  toks.forEach((t, k) => {
    if (t.kind === "regex") out.push(`${t.line}: regex literal ${t.value}`);
    if (t.kind === "id" && BANNED_IDS.has(t.value)) out.push(`${t.line}: ${t.value}`);
    if (t.kind === "id" && PATTERN_METHODS.has(t.value) && toks[k - 1]?.value === "." && toks[k + 1]?.value === "(") {
      const recv = toks[k - 2];
      const ok = recv?.kind === "id" && (/^[A-Z][A-Z0-9_]*$/.test(recv.value) || ["mark", "re", "py", "str"].includes(recv.value));
      if (!ok) out.push(`${t.line}: .${t.value}( on ${recv?.value ?? "?"}`);
    }
  });
  return out;
}

test("src/sd uses the kernel for every Python-sensitive operation", () => {
  const dir = join(CLI, "src", "sd");
  const files = readdirSync(dir).filter((f) => f.endsWith(".ts"));
  expect(files.length).toBeGreaterThanOrEqual(6);
  const hits = files.flatMap((f) => violations(readFileSync(join(dir, f), "utf8")).map((v) => `${f}:${v}`));
  expect(hits).toEqual([]);
});

test("the check catches what it bans", () => {
  expect(violations("const a = /x/i;")).toHaveLength(1);
  expect(violations('const a = new RegExp("x");')).toHaveLength(1);
  expect(violations("s.toLowerCase()")).toHaveLength(1);
  expect(violations('s.split("|")')).toHaveLength(1);
  expect(violations('s.match("x")')).toHaveLength(1);
  expect(violations('HEAD.match(l); re.sub("a", "b", s); TITLE_CUT.split(h, 1); py.replace(s, "a", "b"); py.str.split(s)')).toEqual([]);
  expect(violations('const p = "\\\\p{L}";')).toHaveLength(1);
});
