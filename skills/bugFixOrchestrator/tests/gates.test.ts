import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import { beforeEach, describe, expect, it } from "vitest";

import { main } from "../src/cli.js";
import type { Deps } from "../src/deps.js";
import { StateSchema, type State } from "../src/schema.js";
import { HANDOFF, TICKET, commitFile, git, makeRepo, testDeps, tmpDir, writeJson } from "./helpers.js";

let repo: string;
let state: string;
let scratch: string;
let deps: Deps;

const run = (...argv: string[]) => main(["--state", state, ...argv], deps);
const readState = (): State => StateSchema.parse(JSON.parse(fs.readFileSync(path.join(state, "state.json"), "utf8")));
const out = <T>(res: { exitCode: number; stdout: string; stderr?: string }): T => {
  expect(res.stderr ?? "").toBe("");
  return JSON.parse(res.stdout) as T;
};

/** init → investigate with the given handoff text. */
function investigated(handoff = HANDOFF): void {
  out(run("init", "--ticket", writeJson(scratch, "ticket.json", TICKET)));
  fs.writeFileSync(path.join(scratch, "handoff.md"), handoff);
  out(run("advance", "investigate", "--evidence", path.join(scratch, "handoff.md")));
}

/** Commits a ui-evidence script and reproduces through run-ui (fixed.txt absent → the step fails). */
function reproducedWithUi(): string {
  commitFile(repo, "script.json", '{"steps":[]}');
  const baseline = out<{ evidence: string; exitCode: number }>(run("run-ui", "--check", "script.json", "--cwd", repo));
  expect(baseline.exitCode).toBe(2);
  out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "script.json", "--cwd", repo));
  return git(repo, "rev-parse", "HEAD");
}

beforeEach(() => {
  repo = makeRepo();
  scratch = tmpDir();
  state = path.join(tmpDir(), "bugfix", "phone");
  deps = testDeps();
});

describe("run-ui", { timeout: 30_000 }, () => {
  it("runs ui-evidence on the committed script, registers its summary, and drives the UI path to a passing run", () => {
    investigated();
    const base = reproducedWithUi();
    expect(readState().check).toMatchObject({ kind: "ui-evidence", path: "script.json", command: null });
    out(run("start-attempt", "--mode", "A"));
    git(repo, "checkout", "-q", "-b", "fix", base);
    commitFile(repo, "fixed.txt", "ok\n");
    out(run("record-candidate", "--branch", "fix", "--cwd", repo));
    const after = out<{ evidence: string; exitCode: number }>(run("run-ui", "--check", "script.json", "--cwd", repo));
    expect(after.exitCode).toBe(0);
    expect(out(run("record-run", "c1", "--evidence", after.evidence))).toMatchObject({ passed: true });
    expect(out(run("judge", "c1"))).toMatchObject({ decision: "resolved" });
    expect(JSON.parse(fs.readFileSync(path.join(state, "judge-c1.json"), "utf8"))).toMatchObject({ checkKind: "ui-evidence", checkSummary: "the ui-evidence script script.json" });
  });

  it("refuses a dirty tree or a missing check, and reports a run that wrote no summary", () => {
    investigated();
    fs.writeFileSync(path.join(repo, "stray.txt"), "x");
    expect(run("run-ui", "--check", "check.sh", "--cwd", repo).stderr).toContain("uncommitted or untracked");
    fs.rmSync(path.join(repo, "stray.txt"));
    expect(run("run-ui", "--check", "missing.json", "--cwd", repo).stderr).toContain("check file not found");
    deps = { ...deps, uiEvidenceBin: path.join(scratch, "does-not-exist.js") };
    const res = run("run-ui", "--check", "check.sh", "--cwd", repo);
    expect(res.exitCode).toBe(3);
    expect(res.stderr).toContain("ui-evidence wrote no summary");
  });
});

describe("record-candidate gates", { timeout: 30_000 }, () => {
  beforeEach(() => {
    investigated();
    const baseline = out<{ evidence: string }>(run("run-test", "--check", "check.sh", "--cwd", repo, "--", "sh", "check.sh"));
    out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "check.sh", "--cwd", repo));
    out(run("start-attempt", "--mode", "A"));
  });

  it("refuses a commit that doesn't build on the baseline", () => {
    git(repo, "checkout", "-q", "--orphan", "elsewhere");
    commitFile(repo, "fixed.txt", "ok\n", "unrelated root");
    expect(run("record-candidate", "--branch", "elsewhere", "--cwd", repo).stderr).toContain("does not build on the baseline");
  });

  it("refuses an empty commit whose tree equals the baseline's", () => {
    git(repo, "commit", "-q", "--allow-empty", "-m", "nothing");
    expect(run("record-candidate", "--branch", "main", "--cwd", repo).stderr).toContain("identical to the baseline's");
  });

  it("refuses fixture/test/config changes unless the user approved them", () => {
    fs.mkdirSync(path.join(repo, "tests", "fixtures"), { recursive: true });
    commitFile(repo, "fixed.txt", "ok\n");
    commitFile(repo, "tests/fixtures/user.json", "{}");
    commitFile(repo, "vitest.config.ts", "export default {}");
    const res = run("record-candidate", "--branch", "main", "--cwd", repo);
    expect(res.stderr).toContain("tests/fixtures/user.json, vitest.config.ts");
    out(run("record-candidate", "--branch", "main", "--cwd", repo, "--allow-test-changes"));
    expect(readState().candidates[0].changedFiles).toEqual(["fixed.txt", "tests/fixtures/user.json", "vitest.config.ts"]);
  });

  it("treats files beside a check in a subdirectory as protected", () => {
    // A fresh bugfix whose check lives in checks/.
    state = path.join(tmpDir(), "bugfix", "sub");
    fs.mkdirSync(path.join(repo, "checks"));
    commitFile(repo, "checks/phone.sh", "test -f fixed.txt\n");
    investigated();
    const baseline = out<{ evidence: string }>(run("run-test", "--check", "checks/phone.sh", "--cwd", repo, "--", "sh", "checks/phone.sh"));
    out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "checks/phone.sh", "--cwd", repo));
    out(run("start-attempt", "--mode", "A"));
    commitFile(repo, "checks/helper.sh", "true\n");
    commitFile(repo, "fixed.txt", "ok\n");
    expect(run("record-candidate", "--branch", "main", "--cwd", repo).stderr).toContain("(checks/helper.sh)");
  });

  it("flags JVM-style test names, __fixtures__ and tsconfig, but not production files that merely end in 'test'", () => {
    fs.mkdirSync(path.join(repo, "src", "__fixtures__"), { recursive: true });
    commitFile(repo, "src/Latest.kt", "class Latest");
    commitFile(repo, "src/ContestTest.kt", "class ContestTest");
    commitFile(repo, "src/MyAppUITests.swift", "");
    commitFile(repo, "src/APITest.java", "");
    commitFile(repo, "src/__fixtures__/a.json", "{}");
    commitFile(repo, "tsconfig.test.json", "{}");
    const res = run("record-candidate", "--branch", "main", "--cwd", repo);
    expect(res.stderr).toContain("(src/APITest.java, src/ContestTest.kt, src/MyAppUITests.swift, src/__fixtures__/a.json, tsconfig.test.json)");
    expect(res.stderr).not.toContain("Latest.kt,");
  });

  it("validates --hypothesis against the handoff", () => {
    commitFile(repo, "fixed.txt", "ok\n");
    expect(run("record-candidate", "--branch", "main", "--cwd", repo, "--hypothesis", "x")).toMatchObject({ exitCode: 1 });
    expect(run("record-candidate", "--branch", "main", "--cwd", repo, "--hypothesis", "9").stderr).toContain("has no hypothesis 9");
    out(run("record-candidate", "--branch", "main", "--cwd", repo, "--hypothesis", "2"));
    expect(readState().candidates[0].hypothesis).toBe(2);
  });

  it("refuses a hypothesis that was ruled out at investigate time, whatever the file says later", () => {
    state = path.join(tmpDir(), "bugfix", "ruled");
    investigated(HANDOFF.replace("| Medium | untested |", "| Medium | ruled-out |"));
    const baseline = out<{ evidence: string }>(run("run-test", "--check", "check.sh", "--cwd", repo, "--", "sh", "check.sh"));
    out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "check.sh", "--cwd", repo));
    out(run("start-attempt", "--mode", "A"));
    fs.writeFileSync(path.join(scratch, "handoff.md"), HANDOFF);
    commitFile(repo, "fixed.txt", "ok\n");
    expect(run("record-candidate", "--branch", "main", "--cwd", repo, "--hypothesis", "2").stderr).toContain("hypothesis 2 was ruled out");
    expect(run("record-candidate", "--branch", "main", "--cwd", repo, "--hypothesis", "")).toMatchObject({ exitCode: 1 });
    out(run("record-candidate", "--branch", "main", "--cwd", repo, "--hypothesis", "1"));
  });

  it("sees renamed and specially-named protected files, case-insensitively, and package manifests", () => {
    // A fresh bugfix whose baseline already contains tests/helper.ts.
    state = path.join(tmpDir(), "bugfix", "renames");
    fs.mkdirSync(path.join(repo, "tests"));
    commitFile(repo, "tests/helper.ts", "x");
    investigated();
    const baseline = out<{ evidence: string }>(run("run-test", "--check", "check.sh", "--cwd", repo, "--", "sh", "check.sh"));
    out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "check.sh", "--cwd", repo));
    out(run("start-attempt", "--mode", "A"));
    commitFile(repo, "fixed.txt", "ok\n");
    git(repo, "mv", "tests/helper.ts", "src-helper.ts");
    git(repo, "commit", "-qm", "move the helper out of tests/");
    expect(run("record-candidate", "--branch", "main", "--cwd", repo).stderr).toContain("tests/helper.ts");
    git(repo, "reset", "-q", "--hard", "HEAD~1");
    fs.mkdirSync(path.join(repo, "Fixtures"));
    commitFile(repo, "Fixtures/é.json", "{}");
    commitFile(repo, "package.json", "{}");
    const res = run("record-candidate", "--branch", "main", "--cwd", repo);
    expect(res.stderr).toContain("Fixtures/é.json");
    expect(res.stderr).toContain("package.json");
  });
});

describe("judge (input built from state)", { timeout: 30_000 }, () => {
  function evaluated(): { base: string; commit: string } {
    investigated();
    const baseline = out<{ evidence: string }>(run("run-test", "--check", "check.sh", "--cwd", repo, "--", "sh", "check.sh"));
    out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "check.sh", "--cwd", repo));
    const base = git(repo, "rev-parse", "HEAD");
    out(run("start-attempt", "--mode", "A"));
    const commit = commitFile(repo, "fixed.txt", "ok\n");
    out(run("record-candidate", "--branch", "main", "--cwd", repo));
    return { base, commit };
  }
  const passingRun = () => out<{ evidence: string }>(run("run-test", "--check", "check.sh", "--cwd", repo, "--", "sh", "check.sh")).evidence;

  it("sends judge the input built from state in schema order — ticket text, snapshotted root cause, frozen check, diff — and records its digest", () => {
    const { base, commit } = evaluated();
    out(run("record-run", "c1", "--evidence", passingRun()));
    fs.writeFileSync(path.join(scratch, "handoff.md"), HANDOFF.replace("updateProfile() omits phone.", "It was fine all along."));
    out(run("judge", "c1"));
    const text = fs.readFileSync(path.join(state, "judge-c1.json"), "utf8");
    const input = JSON.parse(text) as Record<string, unknown>;
    expect(Object.keys(input)).toEqual(["brief", "expected", "actual", "rootCause", "checkKind", "checkSummary", "beforePassed", "afterPassed", "diffStat", "diff"]);
    expect(input).toMatchObject({
      brief: TICKET.brief, rootCause: "updateProfile() omits phone.", checkKind: "test",
      checkSummary: "the regression test check.sh (committed in the baseline before the fix and frozen; it failed there), run as `sh check.sh`", beforePassed: false, afterPassed: true,
      diffStat: git(repo, "diff", "--stat", base, commit, "--", ".", ":(exclude)package-lock.json", ":(exclude)**/dist/**"),
      diff: git(repo, "diff", base, commit, "--", ".", ":(exclude)package-lock.json", ":(exclude)**/dist/**"),
    });
    expect(input.diff).toContain("+ok");
    const candidate = readState().candidates[0];
    expect(candidate.judgeInputDigest).toBe(createHash("sha256").update(text).digest("hex").slice(0, 16));
    expect(candidate.judge).toMatchObject({ decisionId: "d1", decision: "resolved" });
  });

  const isFullDiff = (args: string[]) => args.includes("--") && !args.includes("--stat");

  it("caps the diff field, not the serialized JSON, so the stored input stays valid JSON (B4)", () => {
    evaluated();
    out(run("record-run", "c1", "--evidence", passingRun()));
    const huge = `${"+x\n".repeat(150_000)}`;
    const capDeps: Deps = { ...deps, git: (cwd, args) => (isFullDiff(args) ? huge : deps.git(cwd, args)) };
    expect(main(["--state", state, "judge", "c1"], capDeps).exitCode).toBe(0);
    const input = JSON.parse(fs.readFileSync(path.join(state, "judge-c1.json"), "utf8")) as { diff: string };
    expect(input.diff.length).toBeLessThan(huge.length);
    expect(input.diff).toMatch(/\n\[truncated \d+ chars\]$/);
  });

  it("sends only the candidate's own files when it merged an unrelated change from main", () => {
    investigated();
    const baseline = out<{ evidence: string }>(run("run-test", "--check", "check.sh", "--cwd", repo, "--", "sh", "check.sh"));
    out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "check.sh", "--cwd", repo));
    const base = git(repo, "rev-parse", "HEAD");
    out(run("start-attempt", "--mode", "A"));
    // main moves on after the baseline, in a file the fix never touches.
    git(repo, "checkout", "-q", "-b", "fix", base);
    commitFile(repo, "fixed.txt", "ok\n");
    git(repo, "checkout", "-q", "-");
    commitFile(repo, "unrelated-main.txt", "from main\n");
    git(repo, "checkout", "-q", "fix");
    git(repo, "merge", "-q", "--no-edit", "main");
    out(run("record-candidate", "--branch", "fix", "--cwd", repo));
    out(run("record-run", "c1", "--evidence", passingRun()));
    out(run("judge", "c1"));
    const input = JSON.parse(fs.readFileSync(path.join(state, "judge-c1.json"), "utf8")) as { diff: string; diffStat: string };
    expect(input.diff).toContain("fixed.txt");
    expect(input.diff).not.toContain("unrelated-main.txt");
    expect(input.diffStat).toContain("fixed.txt");
    expect(input.diffStat).not.toContain("unrelated-main.txt");
  });

  it("keeps the fix's hunk but not main's when both edited different lines of the same file", () => {
    commitFile(repo, "shared.txt", "a\nb\nc\nd\ne\n");
    investigated();
    const baseline = out<{ evidence: string }>(run("run-test", "--check", "check.sh", "--cwd", repo, "--", "sh", "check.sh"));
    out(run("advance", "reproduce", "--evidence", baseline.evidence, "--check", "check.sh", "--cwd", repo));
    const base = git(repo, "rev-parse", "HEAD");
    out(run("start-attempt", "--mode", "A"));
    git(repo, "checkout", "-q", "-b", "fix", base);
    commitFile(repo, "fixed.txt", "ok\n");
    commitFile(repo, "shared.txt", "FIX\nb\nc\nd\ne\n");
    git(repo, "checkout", "-q", "main");
    commitFile(repo, "shared.txt", "a\nb\nc\nd\nMAIN\n");
    git(repo, "checkout", "-q", "fix");
    git(repo, "merge", "-q", "--no-edit", "main");
    out(run("record-candidate", "--branch", "fix", "--cwd", repo));
    out(run("record-run", "c1", "--evidence", passingRun()));
    out(run("judge", "c1"));
    const input = JSON.parse(fs.readFileSync(path.join(state, "judge-c1.json"), "utf8")) as { diff: string; diffStat: string };
    expect(input.diff).toContain("+FIX");
    expect(input.diff).not.toContain("MAIN");
    expect(input.diffStat).toContain("shared.txt");
  });

  it("fails open when the full diff cannot be read: judge still gets the stat-only input (B4)", () => {
    evaluated();
    out(run("record-run", "c1", "--evidence", passingRun()));
    const failDeps: Deps = { ...deps, git: (cwd, args) => { if (isFullDiff(args)) throw new Error("maxBuffer exceeded"); return deps.git(cwd, args); } };
    expect(main(["--state", state, "judge", "c1"], failDeps).exitCode).toBe(0);
    const input = JSON.parse(fs.readFileSync(path.join(state, "judge-c1.json"), "utf8")) as Record<string, unknown>;
    expect(Object.keys(input)).toEqual(["brief", "expected", "actual", "rootCause", "checkKind", "checkSummary", "beforePassed", "afterPassed", "diffStat"]);
  });
});
