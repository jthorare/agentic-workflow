// The oracle loads system-design's build_doc.py / build_prs.py unchanged; slug, esc and strip_tags composed from the
// kernel (re.sub, lower, strip, code point slicing, html) reproduce them on 2,000 strings. This is the shape PR 10's
// conformance suite takes for the whole D1 surface.
import { describe, expect, test } from "bun:test";
import { escape, unescape } from "../../src/py/html";
import * as re from "../../src/py/re";
import { cpSlice, lower, strip } from "../../src/py/str";
import { SKIP_REASON, hasPython, oracle } from "./oracle";

const slug = (s: string) => strip(cpSlice(strip(re.sub("[^a-z0-9]+", "-", lower(s)), "-"), 0, 48), "-") || "section";
const stripTags = (h: string) => strip(unescape(re.sub("<[^>]+>", "", h)));

describe.skipIf(!hasPython)("system-design helpers through the kernel", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  test("slug / esc / strip_tags", () => {
    const g = oracle<{ sha256: Record<string, string>; rows: [string, string, string, string, string][] }>("sd");
    expect(Object.keys(g.sha256).sort()).toEqual(["build_doc.py", "build_prs.py"]);
    const bad = g.rows.filter(([s, sl, e, eq, st]) => slug(s) !== sl || escape(s) !== e || escape(s, false) !== eq || stripTags(s) !== st);
    expect(bad.slice(0, 5)).toEqual([]);
  }, 60_000);
});
