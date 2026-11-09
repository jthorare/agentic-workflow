// The TypeScript compiler (PR 11) against build_tdd.py: the parity gate (Python as A, TypeScript as B, one out path,
// TDD_GENERATED pinned) on the edge fixture (every container, hotspot variant, image sizing, sub-PRs, aliases, the
// sanitizer, a CRLF SVG that dash_proposed rewrites) and on $TDD_PARITY_PACKETS, with --no-copy and --strict too;
// argv errors (exit code, stdout, stderr); the vocabulary against build_tdd.py's own objects; the sha256 pin.
import { describe, expect, test } from "bun:test";
import { chmodSync, cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { basename, join, resolve } from "node:path";
import { spawnSync } from "bun";
import { deprecationPair, gate, splitStderr } from "../../parity/gate";
import { BUILD_TDD_SHA256, DOC } from "../../src/compile";
import { sha256Hex } from "../../src/py/io";
import { CITE_SOURCE, LEGEND_TONES, LINK_KEYS, MARK_SOURCE, SKIP_TAGS, SQL_KW, TOKEN_SOURCE, token_id } from "../../src/vocab";
import { PACKETS, SKIP_REASON, hasPython } from "../py/oracle";

const T = 120_000;
const CLI = resolve(new URL("../..", import.meta.url).pathname);
const SCRIPTS = resolve(CLI, "..", "scripts");
const BUILD_TDD = join(SCRIPTS, "build_tdd.py");
const EDGE = join(CLI, "test", "compile", "fixtures", "edge");
const REFERENCE_LINE = "6 sections, 6 diagrams, 8 decisions, 11 PRs, 7 schema tables, 171 reference links (2 unlinked), 54 citations, 23 files (0 missing)";

/** A temp copy of the edge fixture whose meta.json repo_path sits under $HOME (home_rel must print it as ~/...). */
function edgeCopy(): { dir: string; packet: string } {
  const dir = mkdtempSync(join(tmpdir(), "tdd-compile-edge-"));
  const packet = join(dir, "edge");
  cpSync(EDGE, packet, { recursive: true });
  const meta = readFileSync(join(packet, "meta.json"), "utf8");
  writeFileSync(join(packet, "meta.json"), meta.split('"~/src/edge"').join(JSON.stringify(join(homedir(), "src", "edge"))));
  return { dir, packet };
}

describe.skipIf(!hasPython)("the TypeScript compiler matches build_tdd.py", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("edge fixture: gate green (content.json, content.files.json, every file, stdout, stderr)", () => {
    const { dir, packet } = edgeCopy();
    try {
      const r = gate(packet);
      expect(r.problems).toEqual([]);
      expect(r.summary).toBe("4 sections, 4 diagrams, 5 decisions, 3 PRs, 2 schema tables, 25 reference links (8 unlinked), 7 citations, 7 files (3 missing)");
      expect(r.b.files).toBe(9);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);

  test("edge fixture with --no-copy: gate green", () => {
    const { dir, packet } = edgeCopy();
    try {
      expect(gate(packet, { args: ["--no-copy"] }).problems).toEqual([]);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);

  test("edge fixture with --strict: both exit 1 with the same streams and files", () => {
    const { dir, packet } = edgeCopy();
    try {
      const r = gate(packet, { args: ["--strict"] });
      expect(r.a.exit).toBe(1);
      expect(r.b.exit).toBe(1);
      expect(r.problems.filter((p) => !/^compiler [AB] \((python|ts)\) exited 1: /.test(p))).toEqual([]);
      expect(r.b.stderr.trimEnd().endsWith("--strict: 17 warning(s)")).toBe(true);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);

  test("bare attribute flags ({id}, {chip}, {state} ... are True): gate green, ids print as Python's True-2", () => {
    const r = gate(join(CLI, "test", "compile", "fixtures", "bare"));
    expect(r.problems).toEqual([]);
    expect(r.summary).toBe("3 sections, 0 diagrams, 1 decisions, 0 PRs, 0 schema tables, 0 reference links (1 unlinked), 0 citations, 0 files (0 missing)");
  }, T);

  test("the gate compares a compiler line that looks like an interpreter warning (a TS mutant of it turns it red)", () => {
    const { dir, packet } = edgeCopy();
    try {
      const hs = join(packet, "diagrams", "flow.hotspots.json");
      const spec = JSON.parse(readFileSync(hs, "utf8"));
      spec.boxes.push({ label: "x:1: Warning: y", x: 1, y: 1, w: 1, h: 1, href: "#nowhere2" });
      writeFileSync(hs, JSON.stringify(spec));
      expect(gate(packet).problems).toEqual([]);
      const mutant = join(dir, "ts-mutant.sh");
      writeFileSync(mutant, `#!/bin/bash\n"${process.execPath}" --no-install "${join(CLI, "bin", "build-tdd.ts")}" "$@" 2>"${dir}/err"; c=$?\nsed 's/Warning: y/Warning: z/' "${dir}/err" >&2\nexit $c\n`);
      chmodSync(mutant, 0o755);
      const r = gate(packet, { b: mutant });
      expect(r.problems.some((p) => p.startsWith("stderr differs") && p.includes("Warning: z"))).toBe(true);
      expect(r.notes.every((n) => n.includes("DeprecationWarning: 'maxsplit' is passed as positional argument"))).toBe(true);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);

  for (const packet of PACKETS) {
    const name = basename(packet);
    test(`${name}: gate green, python vs ts`, () => {
      const r = gate(packet);
      expect(r.problems).toEqual([]);
      if (name === "open-shift-ranking") expect(r.summary).toBe(REFERENCE_LINE);
    }, T);
    test(`${name}: gate green with --no-copy`, () => {
      expect(gate(packet, { args: ["--no-copy"] }).problems).toEqual([]);
    }, T);
  }

  // argv errors: the same exit code, stdout and stderr (interpreter warnings aside), and nothing written
  const cases: [string, (d: string) => string[]][] = [
    ["no arguments", () => []],
    ["one positional", () => ["edge.md"]],
    ["three positionals", (d) => ["edge.md", join(d, "o"), "extra"]],
    ["a flag without its value", () => ["edge.md", "out", "--meta"]],
    ["an unknown option", (d) => ["edge.md", join(d, "o"), "--bogus"]],
    ["a missing source", (d) => ["nope.md", join(d, "o")]],
    ["a missing --meta", (d) => ["edge.md", join(d, "o"), "--meta", "missing.json"]],
    ["a --meta that is not JSON", (d) => ["edge.md", join(d, "o"), "--meta", "edge.md"]],
    ["an empty --prs folder", (d) => ["edge.md", join(d, "o"), "--prs", "schema"]],
    ["a --diagrams folder without the diagrams", (d) => ["edge.md", join(d, "o"), "--diagrams", "mockups", "--no-copy"]],
  ];
  for (const [what, argv] of cases) {
    test(`argv: ${what}`, () => {
      const { dir, packet } = edgeCopy();
      try {
        const run = (cmd: string[]) => {
          const r = spawnSync(cmd, { cwd: packet, env: { ...process.env, TDD_GENERATED: "2000-01-01T00:00:00Z" }, stdout: "pipe", stderr: "pipe" });
          return { exit: r.exitCode, stdout: r.stdout.toString(), stderr: splitStderr(r.stderr.toString(), cmd[0] === "python3" ? "python" : "ts").own };
        };
        const a = argv(join(dir, "a"));
        const b = argv(join(dir, "b"));
        const py = run(["python3", "-I", "-B", BUILD_TDD, ...a]);
        const ts = run([process.execPath, "--no-install", join(CLI, "bin", "build-tdd.ts"), ...b]);
        const norm = (s: string) => s.split(join(dir, "a")).join("<out>").split(join(dir, "b")).join("<out>");
        expect({ exit: ts.exit, stdout: norm(ts.stdout), stderr: norm(ts.stderr) }).toEqual({ exit: py.exit, stdout: norm(py.stdout), stderr: norm(py.stderr) });
      } finally {
        rmSync(dir, { recursive: true, force: true });
      }
    }, T);
  }

  test("lint_packet.py PARITY: passes on a fresh build, a note and a skip without bun", () => {
    const { dir, packet } = edgeCopy();
    try {
      const b = spawnSync(["python3", "-I", "-B", "-W", "ignore", BUILD_TDD, "edge.md", "build"], { cwd: packet, stdout: "pipe", stderr: "pipe" });
      expect(b.exitCode).toBe(0);
      const lint = (path: string) => {
        const out = join(dir, `lint-${path ? "bun" : "nobun"}.json`);
        const r = spawnSync(["python3", "-I", "-B", join(SCRIPTS, "lint_packet.py"), packet, "--json", out], {
          cwd: packet, stdout: "pipe", stderr: "pipe", env: { ...process.env, PATH: path },
        });
        expect([0, 1]).toContain(r.exitCode ?? -1);
        return JSON.parse(readFileSync(out, "utf8")) as { findings: { rule: string }[]; notes: string[]; counts: { by_rule: Record<string, number> } };
      };
      const withBun = lint(process.env.PATH ?? "");
      expect(withBun.findings.filter((f) => f.rule === "PARITY")).toEqual([]);
      expect(withBun.notes.some((n) => n.startsWith("PARITY: the TypeScript compiler (cli/bin/build-tdd.ts) reproduced build/content.json byte for byte"))).toBe(true);
      expect(withBun.counts.by_rule.PARITY).toBe(0);
      const py = spawnSync(["python3", "-I", "-c", "import sys; print(sys.executable)"], { stdout: "pipe" }).stdout.toString().trim();
      const noBun = lint(`${py.slice(0, py.lastIndexOf("/"))}:/usr/bin:/bin`);
      expect(noBun.notes).toContain("PARITY: bun is not on PATH; the TypeScript compiler was not compared");
      expect(noBun.findings).toEqual(withBun.findings);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);

  test("bad output folders: the same exit code and traceback line as CPython, no bun stack dump", () => {
    const { dir, packet } = edgeCopy();
    const ro = join(dir, "ro");
    try {
      writeFileSync(join(dir, "afile"), "x");
      mkdirSync(ro);
      chmodSync(ro, 0o555);
      const last = (s: string) => s.trimEnd().split("\n").pop();
      for (const out of ["afile", "afile/sub", "ro/out", "ro/out/deeper"])
        for (const flags of [[], ["--no-copy"]]) {
          const o = join(dir, out);
          const run = (cmd: string[]) => spawnSync([...cmd, "edge.md", o, ...flags], { cwd: packet, stdout: "pipe", stderr: "pipe" });
          const py = run(["python3", "-I", "-B", "-W", "ignore", BUILD_TDD]);
          const ts = run([process.execPath, "--no-install", join(CLI, "bin", "build-tdd.ts")]);
          const tsErr = ts.stderr.toString();
          expect({ out, flags, exit: ts.exitCode, last: last(tsErr) }).toEqual({ out, flags, exit: py.exitCode, last: last(py.stderr.toString()) });
          expect(py.exitCode).toBe(1);
          expect(/Bun v|\n\s+at /.test(tsErr)).toBe(false);
        }
    } finally {
      chmodSync(ro, 0o755);
      rmSync(dir, { recursive: true, force: true });
    }
  }, T);

  test("vocab.ts is build_tdd.py's vocabulary (and eval/ev_common.py's TOKEN)", () => {
    const code = `
import importlib.util, json, os, sys
def load(p, n):
    s = importlib.util.spec_from_file_location(n, p); m = importlib.util.module_from_spec(s); s.loader.exec_module(m); return m
bt = load(${JSON.stringify(BUILD_TDD)}, "build_tdd")
ev = load(${JSON.stringify(join(SCRIPTS, "eval", "ev_common.py"))}, "ev_common")
toks = ["D7", "PR 7", "PR12a", "F3.2", "D-7", "OS-01", " pr 3 "]
print(json.dumps({"token": bt.TOKEN.pattern, "ev_token": ev.TOKEN.pattern, "cite": bt.CITE.pattern, "tones": list(bt.LEGEND_TONES.items()),
  "link_keys": sorted(bt.Compiler.LINK_KEYS), "skip": sorted(bt.SKIP_TAGS), "mark": bt.bd.MARK.pattern, "mark_flags": int(bt.bd.MARK.flags),
  "sql": bt.SQL_KW, "ids": [bt.token_id(t) for t in toks]}, ensure_ascii=False))`;
    const r = spawnSync(["python3", "-I", "-B", "-W", "ignore", "-c", code], { stdout: "pipe", stderr: "pipe" });
    expect(r.exitCode).toBe(0);
    const g = JSON.parse(r.stdout.toString());
    expect(TOKEN_SOURCE).toBe(g.token);
    expect(TOKEN_SOURCE).toBe(g.ev_token);
    expect(CITE_SOURCE).toBe(g.cite);
    expect([...LEGEND_TONES]).toEqual(g.tones);
    expect([...LINK_KEYS].sort()).toEqual(g.link_keys);
    expect([...SKIP_TAGS].sort()).toEqual(g.skip);
    expect(MARK_SOURCE).toBe(g.mark);
    expect(g.mark_flags & 2).toBe(2); // re.I
    expect(SQL_KW).toBe(g.sql);
    expect(["D7", "PR 7", "PR12a", "F3.2", "D-7", "OS-01", " pr 3 "].map(token_id)).toEqual(g.ids);
  }, T);
});

describe("the gate's stderr filter", () => {
  test("only build_tdd.py's DeprecationWarning pair, only on Python runs", () => {
    const pair = deprecationPair()!;
    expect(pair[0]).toMatch(/build_tdd\.py:\d+: DeprecationWarning: 'maxsplit' is passed as positional argument$/);
    const look = "warning: hotspot flow/'x:1: Warning: y': anchor #nowhere2 is not defined on the page";
    const other = "/elsewhere/build_tdd.py:1: SyntaxWarning: invalid escape sequence";
    const err = [pair[0], pair[1], look, other, ""].join("\n");
    expect(splitStderr(err, "python")).toEqual({ own: [look, other, ""].join("\n"), interpreter: [pair[0], pair[1]] });
    expect(splitStderr(err, "ts")).toEqual({ own: err, interpreter: [] });
    expect(splitStderr([pair[0], "  something else", ""].join("\n"), "python").interpreter).toEqual([]);
  });
});

describe("the port's pins", () => {
  test("scripts/build_tdd.py is the file the port reproduces (twin rule: change both, then the pin)", () => {
    expect(sha256Hex(readFileSync(BUILD_TDD))).toBe(BUILD_TDD_SHA256);
  });
  test("the usage text is build_tdd.py's docstring", () => {
    const src = readFileSync(BUILD_TDD, "utf8");
    expect(src.includes(`"""${DOC}"""`)).toBe(true);
  });
});
