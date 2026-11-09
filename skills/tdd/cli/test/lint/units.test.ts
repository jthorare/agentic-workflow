// Unit tests of the linter's parts against CPython where Python decides (Git failure text, realpath, getmtime,
// surrogateescape, the usage text), and of what only the TypeScript linter has: rule isolation, the swappable services
// (a Git that answers from a table, an Agents check that raises, a Compiler that is absent, a TypeScript compiler that
// fails).
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { chmodSync, mkdirSync, readFileSync, symlinkSync, utimesSync, writeFileSync, copyFileSync, rmSync } from "node:fs";
import { join } from "node:path";
import { Effect, Layer } from "effect";
import { LINT_PACKET_DOC } from "../../src/lint/doc";
import { Finding } from "../../src/lint/finding";
import { Packet } from "../../src/lint/packet";
import { decodeSurrogateEscape } from "../../src/lint/pyops";
import { exitCode, lintJson, report } from "../../src/lint/report";
import { TsCompiler, firstDiff } from "../../src/lint/rules/parity";
import { PyError } from "../../src/py/errors";
import type { RuleId } from "../../src/lint/patterns";
import { AllLive, RULE_EFFECTS, lintEffect, type Rule } from "../../src/lint/run";
import { Agents, Compiler, Git, gitLive, liveFs, timeoutText } from "../../src/lint/services";
import { HAVE_PYTHON, SCRIPTS, T0, caseFrom, makeRepo, makeTemplate, pyLint, rmrf, skillCopy, tempDir, touchAll, type Repo } from "./harness";

const dec = new TextDecoder();
function py(code: string, ...args: string[]): string {
  const r = Bun.spawnSync(["python3", "-I", "-B", "-c", code, ...args], { stdout: "pipe", stderr: "pipe" });
  if (r.exitCode !== 0) throw new Error(dec.decode(r.stderr));
  return dec.decode(r.stdout);
}

describe.skipIf(!HAVE_PYTHON)("against CPython", () => {
  let dir = "";
  beforeAll(() => { dir = tempDir("units"); });
  afterAll(() => rmrf(dir));

  test("the usage text is lint_packet.py's docstring", () => {
    const doc = py("import ast, sys; print(ast.parse(open(sys.argv[1], encoding='utf-8').read()).body[0].value.value, end='')", join(SCRIPTS, "lint_packet.py"));
    expect(LINT_PACKET_DOC).toBe(doc);
  });

  test("git timeout and missing-binary text match subprocess's", async () => {
    const cmd = ["git", "-C", "/r", "show", "abc:x y.ts"];
    expect(timeoutText(cmd, 60)).toBe(py("import subprocess, sys; print(subprocess.TimeoutExpired(sys.argv[1:], 60), end='')", ...cmd));
    expect(timeoutText(cmd, 0.25)).toBe(py("import subprocess, sys; print(subprocess.TimeoutExpired(sys.argv[1:], 0.25), end='')", ...cmd));
    const slow = join(dir, "slow-git");
    writeFileSync(slow, "#!/bin/sh\nexec sleep 3\n");
    chmodSync(slow, 0o755);
    const [rc, out] = await Effect.runPromise(gitLive(0.25, slow).run("/r", ["show", "abc:x y.ts"]));
    expect(rc).toBe(1);
    expect(dec.decode(out)).toBe(timeoutText([slow, "-C", "/r", "show", "abc:x y.ts"], 0.25));
    const missing = join(dir, "no-such-git");
    const [rc2, out2] = await Effect.runPromise(gitLive(60, missing).run("/r", ["status"]));
    expect(rc2).toBe(1);
    expect(dec.decode(out2)).toBe(py("import subprocess, sys\ntry:\n subprocess.run([sys.argv[1], '-C', '/r', 'status'], capture_output=True)\nexcept OSError as e:\n print(e, end='')", missing));
  });

  test("realpath follows links, loops and missing parts as posixpath.realpath does", () => {
    const r = join(dir, "links");
    mkdirSync(join(r, "a", "b"), { recursive: true });
    writeFileSync(join(r, "a", "b", "f.png"), "x");
    symlinkSync("a/b", join(r, "rel"));
    symlinkSync(join(r, "a"), join(r, "abs"));
    symlinkSync("loop2", join(r, "loop1"));
    symlinkSync("loop1", join(r, "loop2"));
    symlinkSync("nowhere/x", join(r, "dangling"));
    const probes = ["rel/f.png", "abs/b/../b/f.png", "loop1/x", "dangling/y", "a/./b//f.png", "missing/../a/b", "rel/../../x", "abs/b/f.png/z"].map((p) => join(r, p));
    const want = JSON.parse(py("import os, sys, json; print(json.dumps([os.path.realpath(p) for p in sys.argv[1:]]))", ...probes));
    expect(probes.map((p) => liveFs.realpath(p))).toEqual(want);
  });

  test("getmtime is CPython's float, to the last bit", () => {
    const files = [1, 2, 3].map((i) => join(dir, `m${i}`));
    files.forEach((f, i) => { writeFileSync(f, "x"); utimesSync(f, 1767225600.123456789 + i * 0.333333, 1767225600.987654321 + i * 0.111111); });
    const want = JSON.parse(py("import os, sys, json; print(json.dumps([repr(os.path.getmtime(p)) for p in sys.argv[1:]]))", ...files));
    expect(files.map((f) => String(liveFs.getmtime(f)))).toEqual(want.map((s: string) => String(Number(s))));
  });

  test("surrogateescape decoding matches CPython on invalid UTF-8", () => {
    const cases = [[0x61, 0xff, 0x62], [0xe2, 0x82, 0x41], [0xe2, 0x82], [0xf0, 0x9f, 0x98, 0x80, 0xc0, 0xaf], [0xed, 0xa0, 0x80], [0xf4, 0x90, 0x80, 0x80], [0x80, 0x80]];
    for (const bytes of cases) {
      const want = JSON.parse(py("import sys, json; print(json.dumps([ord(c) for c in bytes(json.loads(sys.argv[1])).decode('utf-8', 'surrogateescape')]))", JSON.stringify(bytes)));
      const got = Array.from(decodeSurrogateEscape(new Uint8Array(bytes))).map((c) => c.codePointAt(0));
      expect(got).toEqual(want);
    }
  });

  test("first_diff wording and context match Lint._first_diff", () => {
    const a = new TextEncoder().encode("{\n \"a\": \"caf\u00e9 one\",\n \"b\": 1\n}"), b = new TextEncoder().encode("{\n \"a\": \"caf\u00e9 two\",\n \"b\": 1\n}");
    const want = py("import sys, json\nsys.path[:0] = [sys.argv[3]]\nimport lint_packet\nprint(lint_packet.Lint._first_diff(bytes(json.loads(sys.argv[1])), bytes(json.loads(sys.argv[2]))), end='')",
      JSON.stringify([...a]), JSON.stringify([...b]), SCRIPTS);
    expect(firstDiff(a, b)).toBe(want);
  });
});

describe.skipIf(!HAVE_PYTHON)("services swapped by layer", () => {
  let ROOT = "";
  let repo: Repo;
  let template = "";
  beforeAll(() => {
    ROOT = tempDir("units-pk");
    repo = makeRepo(ROOT);
    template = makeTemplate(ROOT, repo);
  });
  afterAll(() => rmrf(ROOT));

  /** A lint on the live services, with `override` replacing the ones it provides (the later layer wins). */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const run = (packet: string, override: Layer.Layer<any> | null, table: Partial<Record<RuleId, Rule>> = RULE_EFFECTS, skip: string[] = []) =>
    Effect.runPromise(Effect.provide(lintEffect({ packet, skip }, undefined, table), override ? Layer.merge(AllLive, override) : AllLive));

  test("no compiler: STRUCT/no-content as lint_packet.py words it when build_tdd.py is not beside it", async () => {
    const dir = caseFrom(template, ROOT, "no-compiler");
    const pk = join(dir, "demo-packet");
    rmSync(join(pk, "build"), { recursive: true });
    rmSync(join(pk, "diagrams", "flow.hotspots.json"));
    const alone = join(dir, "scripts-alone");
    mkdirSync(alone);
    copyFileSync(join(SCRIPTS, "lint_packet.py"), join(alone, "lint_packet.py"));
    touchAll(dir, T0);
    const want = pyLint(["demo-packet", "--skip", "PARITY"], dir, dir, { script: join(alone, "lint_packet.py") });
    const absent = Layer.succeed(Compiler, { available: () => false, compile: () => Effect.die("not called") });
    const here = process.cwd();
    process.chdir(dir);
    try {
      const r = await Effect.runPromise(Effect.provide(lintEffect({ packet: "demo-packet", skip: ["PARITY"] }), Layer.merge(AllLive, absent)));
      expect(report(r)).toBe(want.out);
      expect(lintJson(r)).toBe(want.json!);
      expect(r.findings.map((f) => f.check)).toContain("STRUCT/no-content");
    } finally {
      process.chdir(here);
    }
  });

  test("a rule that raises a linter defect becomes RULE/check-failed and the other rules run", async () => {
    const pk = join(caseFrom(template, ROOT, "isolation"), "demo-packet");
    const table = {
      ...RULE_EFFECTS,
      STRUCT: Effect.sync(() => { throw new TypeError("boom"); }) as Rule,
      DNUM: Effect.gen(function* () { const p = yield* Packet; p.add("DNUM", "none", "warn", "before the defect"); yield* Effect.die(new RangeError("late")); }) as Rule,
    };
    const r = await run(pk, null, table);
    const checks = r.findings.map((f) => `${f.check} ${f.severity} ${f.message}`);
    expect(checks[0]).toBe("STRUCT/check-failed error the STRUCT rule raised TypeError: boom; the other rules ran (a linter defect: please report it)");
    expect(checks).toContain("DNUM/none warn before the defect");
    expect(checks).toContain("DNUM/check-failed error the DNUM rule raised RangeError: late; the other rules ran (a linter defect: please report it)");
    expect(r.notes.some((n) => n.startsWith("XDOC: compared"))).toBe(true);
  });

  test("--skip RULE/check-failed does not hide a broken rule: exit 1, as lint_packet.py's run ends", async () => {
    const dir = caseFrom(template, ROOT, "skip-check-failed");
    const pk = join(dir, "demo-packet");
    const table = { ...RULE_EFFECTS, DNUM: Effect.sync(() => { throw new TypeError("injected"); }) as Rule };
    const skipped = await run(pk, null, table, ["DNUM/check-failed", "DNUM/partial"]);
    expect(skipped.findings.filter((f) => f.check === "DNUM/check-failed").map((f) => f.message))
      .toEqual(["the DNUM rule raised TypeError: injected; the other rules ran (a linter defect: please report it)"]);
    expect(exitCode(skipped, false)).toBe(1);
    // lint_packet.py with the same raise in r_dnum: a traceback and exit 1 under the same --skip
    const alone = join(dir, "scripts-injected");
    mkdirSync(alone);
    const src = readFileSync(join(SCRIPTS, "lint_packet.py"), "utf8");
    const at = "    def r_dnum(self):\n";
    expect(src.includes(at)).toBe(true);
    writeFileSync(join(alone, "lint_packet.py"), src.replace(at, at + "        raise TypeError(\"injected\")\n"));
    const want = pyLint(["demo-packet", "--skip", "DNUM/check-failed,DNUM/partial"], dir, dir, { script: join(alone, "lint_packet.py"), json: false });
    expect(want.code).toBe(1);
    expect(want.err.trimEnd().split("\n").at(-1)).toBe("TypeError: injected");
    // --skip DNUM leaves the rule out: nothing raised, nothing reported
    const ruleSkipped = await run(pk, null, table, ["DNUM"]);
    expect(ruleSkipped.findings.some((f) => f.rule === "DNUM")).toBe(false);
  });

  test("a rule that raises what lint_packet.py raises (a PyError) ends the run instead of being isolated", async () => {
    const pk = join(caseFrom(template, ROOT, "py-raise"), "demo-packet");
    const table = { ...RULE_EFFECTS, FILES: Effect.sync(() => { throw new PyError("AttributeError", "'list' object has no attribute 'get'"); }) as Rule };
    await expect(run(pk, null, table)).rejects.toThrow("'list' object has no attribute 'get'");
  });

  test("PARITY: the TypeScript compiler fails where build_tdd.py does not, worded as lint_packet.py words it", async () => {
    const dir = caseFrom(template, ROOT, "ts-compile-fails");
    const skill = skillCopy(dir + "-skill", "the TypeScript compiler fails on purpose");
    touchAll(dir, T0);
    const want = pyLint(["demo-packet"], dir, dir, { script: join(skill, "scripts", "lint_packet.py") });
    const failing = Layer.succeed(TsCompiler, { compile: () => [1, "the TypeScript compiler fails on purpose"] as const });
    const here = process.cwd();
    process.chdir(dir);
    try {
      const r = await Effect.runPromise(Effect.provide(lintEffect({ packet: "demo-packet" }), Layer.merge(AllLive, failing)));
      expect(report(r)).toBe(want.out);
      expect(lintJson(r)).toBe(want.json!);
      expect(r.findings.filter((f) => f.rule === "PARITY").map((f) => f.message))
        .toEqual(["the TypeScript compiler failed on demo-packet.md where build_tdd.py did not (exit 1: the TypeScript compiler fails on purpose)"]);
    } finally {
      process.chdir(here);
    }
  });

  test("AGENTS: a check that raises is AGENTS/check-failed with Python's exception name", async () => {
    const dir = caseFrom(template, ROOT, "agents-raise");
    mkdirSync(join(dir, "demo-packet", "build", "agents"));
    const raising = Layer.succeed(Agents, { check: () => { throw Object.assign(new Error("x"), { code: "EISDIR", path: "/p/agents/x" }); } });
    const r = await run(join(dir, "demo-packet"), raising, { AGENTS: RULE_EFFECTS.AGENTS });
    expect(r.findings.map((f) => f.message)).toEqual(["write_agents.check raised IsADirectoryError: [Errno 21] Is a directory: '/p/agents/x'"]);
  });

  test("CITES on a Git that answers from a table: no repository needed", async () => {
    const pk = join(caseFrom(template, ROOT, "git-table"), "demo-packet");
    const enc = new TextEncoder();
    const answers: Record<string, readonly [number, string]> = {
      "rev-parse --is-inside-work-tree": [0, "true\n"],
      "remote get-url origin": [0, "git@example.com:demo/queue.git\n"],
      [`cat-file -t ${repo.sha}`]: [1, "fatal: Not a valid object name\n"],
      "rev-parse --short=10 HEAD": [0, "abcdef0123\n"],
      "ls-tree -r --name-only abcdef0123": [0, "src/app.ts\nsrc/deep/util.ts\ndocs/notes.md\n"],
      "show abcdef0123:src/app.ts": [0, "1\n2\n"],
      "show abcdef0123:src/deep/util.ts": [128, "fatal: bad\n"],
    };
    const calls: string[] = [];
    const table = Layer.succeed(Git, { run: (_repo: string, args: readonly string[]) => Effect.sync(() => {
      calls.push(args.join(" "));
      const a = answers[args.join(" ")] ?? [1, "unexpected\n"];
      return [a[0], enc.encode(a[1])] as const;
    }) });
    const r = await run(pk, table, { CITES: RULE_EFFECTS.CITES });
    expect(r.findings.map((f) => `${f.check}: ${f.message}`)).toEqual([
      `CITES/sha-unknown: NEEDS_CONTEXT: meta.sha ${repo.sha} is not a commit in ${repo.path} (a clone older than the sha, or a wrong sha; \`git -C ${repo.path} fetch origin\` then rerun): citations are checked at HEAD instead, as warnings`,
      "CITES/line-range: `SRC/app.ts:3-5`: lines 3-5 are outside src/app.ts, which has 2 lines at abcdef0123 [HEAD fallback]",
    ]);
    expect(r.notes).toEqual(["CITES: 3 distinct citations, 3 resolved in " + repo.path + " at abcdef0123 (HEAD fallback)"]);
    expect(r.citations.map((c) => c.get("resolved"))).toEqual(["head", "head", "head"]);
    // every tracked cited path is counted, with or without a line (Lint.r_cites's count(), memoised per path)
    expect(calls.filter((c) => c.startsWith("show abcdef0123:")).sort()).toEqual(["show abcdef0123:docs/notes.md", "show abcdef0123:src/app.ts", "show abcdef0123:src/deep/util.ts"]);
  });
});

test("a Finding is validated when it is made", () => {
  expect(new Finding({ rule: "X", check: "X/y", severity: "warn", file: null, line: 3, message: "m" }).toPy().get("line")).toBe(3);
  expect(() => new Finding({ rule: "X", check: "X/y", severity: "fatal" as "warn", file: null, line: null, message: "m" })).toThrow();
});
