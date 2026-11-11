// The lint mutation corpus (PR 14 acceptance): every case runs lint_packet.py and `tdd lint` on the same mutated copy
// of the base packet and must give the same exit code, stdout, stderr and lint.json bytes (a case where lint_packet.py
// ends with a traceback: the same exit code, no report and no lint.json from either, and the traceback's last line);
// each case's expected check ids must fire, and across the corpus every check id lint_packet.py declares fires at least once.
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { CASES, USAGE, type Ctx } from "./fixtures/mutations";
import { HAVE_PYTHON, SCRIPTS, T0, caseFrom, makeRepo, makeTemplate, pyLint, rmrf, tempDir, touchAll, tsLint, type Repo } from "./harness";

/** Every RULE/check id lint_packet.py can emit, read from its source: the literal pairs plus load_json's per-rule ids. */
export function inventory(): string[] {
  const src = readFileSync(join(SCRIPTS, "lint_packet.py"), "utf8");
  const ids = new Set<string>();
  for (const m of src.matchAll(/self\.add\("([A-Z]+)", "([a-z0-9-]+)"/g)) ids.add(`${m[1]}/${m[2]}`);
  // load_json(path, rule) adds RULE/bad-json (and RULE/missing-file when not quiet) for the rules that call it
  for (const m of src.matchAll(/load_json\((.*?), "([A-Z]+)"(, quiet_missing=True)?\)/g)) {
    ids.add(`${m[2]}/bad-json`);
    if (!m[3]) ids.add(`${m[2]}/missing-file`);
  }
  // reachable only when a path load_json is given vanishes between the isfile check and the read: not a fixture
  for (const unreachable of ["STRUCT/missing-file", "DIAG/missing-file"]) ids.delete(unreachable);
  return [...ids].sort();
}

const fired = new Set<string>();
/** check id -> the cases that fired it ($TDD_LINT_COVERAGE_OUT writes it as JSON, for the PR's evidence). */
const firedBy = new Map<string, string[]>();

describe.skipIf(!HAVE_PYTHON)("lint corpus: lint_packet.py and tdd lint agree byte for byte", () => {
  let ROOT = "";
  let repo: Repo;
  let template = "";
  beforeAll(() => {
    ROOT = tempDir("corpus");
    repo = makeRepo(ROOT);
    template = makeTemplate(ROOT, repo);
  });
  afterAll(() => rmrf(ROOT));

  for (const k of CASES) {
    test(k.name, () => {
      const dir = caseFrom(template, ROOT, `case-${k.name}`);
      const out = join(ROOT, `out-${k.name}`);
      mkdirSync(out);
      const c: Ctx = { root: dir, pk: join(dir, "demo-packet"), build: join(dir, "demo-packet", "build"), repo, scripts: join(dir, "scripts-alone") };
      k.mutate?.(c);
      touchAll(dir, T0);
      k.times?.(c);
      const args = k.args ? k.args(c) : ["demo-packet"];
      const env = k.env?.(c) ?? {};
      const py = pyLint(args, dir, out, { ...(k.pyScript ? { script: k.pyScript(c) } : {}), env: {} });
      const ts = tsLint(args, dir, out, { env });
      if (k.crash !== undefined) {
        // the run ends as lint_packet.py's does: no report, no lint.json, exit 1, and the traceback's last line
        const last = (e: string) => e.trimEnd().split("\n").at(-1);
        expect(py.err.startsWith("Traceback (most recent call last):")).toBe(true);
        expect(ts.err.startsWith("Traceback (most recent call last):")).toBe(true);
        expect(last(ts.err)).toBe(last(py.err)!);
        expect(last(py.err)).toBe(k.crash);
        expect(ts.out).toBe(py.out);
        expect(ts.json).toBe(py.json);
        expect(ts.code).toBe(py.code);
        expect(py.code).toBe(1);
        return;
      }
      expect(py.err).not.toContain("Traceback");
      expect(ts.out).toBe(py.out);
      expect(ts.err).toBe(py.err);
      expect(ts.json).toBe(py.json);
      expect(ts.code).toBe(py.code);
      if (k.code !== undefined) expect(py.code).toBe(k.code);
      const ids = (JSON.parse(py.json ?? "{}").findings ?? []).map((f: { check: string }) => f.check) as string[];
      for (const id of ids) {
        fired.add(id);
        if (!firedBy.has(id)) firedBy.set(id, []);
        if (!firedBy.get(id)!.includes(k.name)) firedBy.get(id)!.push(k.name);
      }
      for (const e of k.expect) expect(ids).toContain(e);
    }, 60_000);
  }

  for (const u of USAGE) {
    test(`usage: ${u.name}`, () => {
      const dir = caseFrom(template, ROOT, `usage-${u.name}`);
      const py = pyLint(u.args, dir, dir, { json: false });
      const ts = tsLint(u.args, dir, dir, { json: false });
      expect(ts).toEqual(py);
      expect(py.code === 0 || py.code === 2).toBe(true);
    });
  }

  test("every check id lint_packet.py declares fired at least once", () => {
    const inv = inventory();
    const out = process.env.TDD_LINT_COVERAGE_OUT;
    if (out) writeFileSync(out, JSON.stringify({ inventory: inv, cases: CASES.length, firedBy: Object.fromEntries([...firedBy].sort()) }, null, 1) + "\n");
    const missing = inv.filter((id) => !fired.has(id));
    expect(missing).toEqual([]);
    expect(inventory().length).toBeGreaterThanOrEqual(60);
  });
});
