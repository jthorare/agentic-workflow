// Text I/O against CPython: strict / ignore / replace UTF-8 decoding of 5,000 byte strings built from valid,
// truncated, overlong, surrogate and out-of-range sequences (with CPython's UnicodeDecodeError text), universal
// newlines as TextIOWrapper reads them, plus writeText, copy2 and sorted listdir on a temp folder.
import { describe, expect, test } from "bun:test";
import { mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { PyError } from "../../src/py/errors";
import { copy2, decodeUtf8, listdirSorted, readText, universalNewlines, writeText } from "../../src/py/io";
import { SKIP_REASON, hasPython, oracle } from "./oracle";

const T = 60_000;
const bytes = (h: string) => Uint8Array.from(h.match(/../g) ?? [], (x) => parseInt(x, 16));

describe("io: files", () => {
  test("readText (BOM kept, \\r\\n and \\r folded), writeText, copy2 keeps bytes and mtime, listdir in code point order", () => {
    const d = mkdtempSync(join(tmpdir(), "tdd-py-io-"));
    try {
      writeFileSync(join(d, "a.txt"), Uint8Array.from([0xef, 0xbb, 0xbf, 0x61, 0x0d, 0x0a, 0x62, 0x0d, 0x63]));
      expect(readText(join(d, "a.txt"))).toBe("\ufeffa\nb\nc");
      writeText(join(d, "b.txt"), "x\r\ny é");
      expect(readFileSync(join(d, "b.txt"))).toEqual(Buffer.from("x\r\ny é"));
      expect(() => writeText(join(d, "c.txt"), "a\ud800")).toThrow(PyError);
      copy2(join(d, "a.txt"), join(d, "\u{1F600}.txt"));
      expect(readFileSync(join(d, "\u{1F600}.txt"))).toEqual(readFileSync(join(d, "a.txt")));
      expect(Math.trunc(statSync(join(d, "\u{1F600}.txt")).mtimeMs)).toBe(Math.trunc(statSync(join(d, "a.txt")).mtimeMs));
      writeFileSync(join(d, "\ue000.txt"), "");
      expect(listdirSorted(d)).toEqual(["a.txt", "b.txt", "\ue000.txt", "\u{1F600}.txt"]);
    } finally {
      rmSync(d, { recursive: true, force: true });
    }
  });
});

describe.skipIf(!hasPython)("io vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("utf-8 decode strict / ignore / replace and universal newlines", () => {
    const g = oracle<{ rows: [string, [boolean, string], string, string, string | null][] }>("utf8");
    const bad: string[] = [];
    for (const [h, [ok, strict], ignore, replace, text] of g.rows) {
      const b = bytes(h);
      let got: string;
      try {
        got = decodeUtf8(b);
        if (!ok || got !== strict) bad.push(`strict ${h}: ${strict} != ${got}`);
      } catch (e) {
        got = (e as Error).message;
        if (ok || got !== strict) bad.push(`strict ${h}: ${strict} != error ${got}`);
      }
      if (decodeUtf8(b, "ignore") !== ignore) bad.push(`ignore ${h}`);
      if (decodeUtf8(b, "replace") !== replace) bad.push(`replace ${h}`);
      if (text !== null && universalNewlines(decodeUtf8(b)) !== text) bad.push(`newlines ${h}`);
      if (bad.length > 10) break;
    }
    expect(bad).toEqual([]);
  }, T);
});
