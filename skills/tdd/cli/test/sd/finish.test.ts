// Assets.finish with copying, where shutil.copy2's own rules decide: a destination that is the source under another
// name (a symlinked folder, a hard link, a name differing only in case on a case-insensitive volume) is
// shutil.SameFileError with its text; a destination that is a folder receives the file inside it. The test builds two
// identical trees, parity/oracle.py sd_finish runs build_doc's Assets on one and src/sd on the other, and the results,
// errors and states must match once the trees' paths are swapped.
import { describe, expect, test } from "bun:test";
import { linkSync, mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { join as pjoin } from "../../src/py/path";
import { bd } from "../../src/sd";
import { SKIP_REASON, attempt, hasPython, runOracle, same, show, tagOf, type Outcome } from "./harness";

interface Case { out: string; copy: boolean; refs: [string, string[]][]; setup: (dir: string) => void }

const file = (p: string, body = "png") => {
  mkdirSync(join(p, ".."), { recursive: true });
  writeFileSync(p, body);
};

const CASES: Record<string, Case> = {
  "a-symlinked-folder": { out: "out", copy: true, refs: [["img/a.png", ["src/a.png"]]], setup: (d) => {
    file(join(d, "src", "a.png"));
    mkdirSync(join(d, "out"));
    symlinkSync("../src", join(d, "out", "img"));
  } },
  "b-hard-link": { out: "out", copy: true, refs: [["img/a.png", ["src/a.png"]]], setup: (d) => {
    file(join(d, "src", "a.png"));
    mkdirSync(join(d, "out", "img"), { recursive: true });
    linkSync(join(d, "src", "a.png"), join(d, "out", "img", "a.png"));
  } },
  "c-case-only": { out: ".", copy: true, refs: [["img/a.png", ["Img/a.png"]]], setup: (d) => file(join(d, "Img", "a.png")) },
  "d-plain-copy": { out: "out", copy: true, refs: [["img/b.png", ["src/b.png"]], ["x/none.png", ["src/none.png"]], ["../up.png", ["src/b.png"]]], setup: (d) => file(join(d, "src", "b.png"), "b") },
  "e-dest-is-folder": { out: "out", copy: true, refs: [["img/c.png", ["src/c.png"]]], setup: (d) => {
    file(join(d, "src", "c.png"), "c");
    mkdirSync(join(d, "out", "img", "c.png"), { recursive: true });
  } },
  "f-in-place": { out: ".", copy: true, refs: [["src/a.png", ["src/a.png"]]], setup: (d) => file(join(d, "src", "a.png")) },
  "g-no-copy": { out: "out", copy: false, refs: [["img/a.png", ["src/a.png"]]], setup: (d) => file(join(d, "src", "a.png")) },
};

function build(root: string) {
  for (const [name, c] of Object.entries(CASES)) {
    const d = join(root, name);
    mkdirSync(d, { recursive: true });
    c.setup(d);
    writeFileSync(join(d, "case.json"), JSON.stringify({ out: c.out, copy: c.copy, refs: c.refs }));
  }
}

describe.skipIf(!hasPython)("Assets.finish copying, against shutil.copy2", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  test("SameFileError, folder destinations and plain copies match", () => {
    const root = mkdtempSync(join(tmpdir(), "tdd-sd-finish-"));
    try {
      const py = join(root, "py");
      const ts = join(root, "ts");
      build(py);
      build(ts);
      const g = runOracle<{ rows: { case: string; refs: Outcome[]; finish: Outcome; state: unknown }[] }>("sd_finish", ["--dir", py]);
      expect(g.rows.map((r) => r.case)).toEqual(Object.keys(CASES).sort());
      const swap = (v: unknown) => JSON.parse(JSON.stringify(v).replaceAll(JSON.stringify(py).slice(1, -1), JSON.stringify(ts).slice(1, -1)));
      const diffs: string[] = [];
      for (const row of g.rows) {
        const c = CASES[row.case]!;
        const d = join(ts, row.case);
        const a = new bd.Assets(pjoin(d, c.out), false, c.copy); // os.path.join, as the oracle: no normalising
        const got = {
          case: row.case,
          refs: c.refs.map(([pub, cands]) => attempt(() => a.ref(pub, cands.map((x) => pjoin(d, x))))),
          finish: attempt(() => a.finish()),
          state: tagOf({ out_dir: a.out_dir, inline: a.inline, copy: a.copy, files: a.files, missing: a.missing, order: a.order }),
        };
        const want = swap(row);
        if (!same(want, got)) diffs.push(`${row.case}: python ${show(want, 600)} ts ${show(got, 600)}`);
      }
      const errors = g.rows.filter((r) => "x" in r.finish).map((r) => `${r.case}: ${(r.finish as { x: string[] }).x[0]}`);
      console.log(`sd finish: ${g.rows.length} cases, ${diffs.length} diffs; raised: ${errors.join(", ")}`);
      expect(errors).toContain("a-symlinked-folder: shutil.SameFileError");
      expect(errors).toContain("b-hard-link: shutil.SameFileError");
      expect(diffs).toEqual([]);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  }, 60_000);
});
