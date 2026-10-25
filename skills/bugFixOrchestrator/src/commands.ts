import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import type { Deps } from "./deps.js";
import { readRunEvidence, type RunEvidence } from "./evidence.js";
import { parseHandoff } from "./handoff.js";
import { MAX_ATTEMPTS, TicketSchema, type Candidate, type Mode, type Phase, type State } from "./schema.js";
import { loadState, runsDir, saveState, stateFile } from "./store.js";

export interface Result {
  exitCode: 0 | 1 | 3;
  stdout: string;
  stderr?: string;
}

const ok = (stdout: unknown): Result => ({ exitCode: 0, stdout: typeof stdout === "string" ? stdout : JSON.stringify(stdout) });
const refuse = (reason: string): Result => ({ exitCode: 3, stdout: "", stderr: `refused: ${reason}` });
const bad = (reason: string): Result => ({ exitCode: 1, stdout: "", stderr: reason });

function transition(dir: string, state: State, deps: Deps, command: string, to: Phase, evidence: string | null): State {
  const next: State = { ...state, phase: to, history: [...state.history, { at: deps.now().toISOString(), command, from: state.phase, to, evidence }] };
  saveState(dir, next);
  return next;
}

function withState(dir: string, fn: (state: State) => Result): Result {
  const state = loadState(dir);
  if ("error" in state) return bad(state.error);
  return fn(state);
}

function requirePhase(state: State, allowed: readonly Phase[]): Result | null {
  if (allowed.includes(state.phase)) return null;
  return refuse(`phase is ${state.phase}; this step needs ${allowed.join(" or ")}`);
}

// Untracked files count too: a new fixture or helper can change what a test
// does while the commit stays the same.
function dirty(deps: Deps, cwd: string): boolean {
  return deps.git(cwd, ["status", "--porcelain", "--untracked-files=all"]) !== "";
}

const repoRoot = (deps: Deps, cwd: string): string => fs.realpathSync(deps.git(cwd, ["rev-parse", "--show-toplevel"]));

/**
 * The check must be a tracked file inside the repo whose working copy is the
 * committed version — candidate worktrees are created from the baseline
 * commit, so an untracked check would not exist there.
 */
function committedCheck(deps: Deps, cwd: string, checkPath: string): { rel: string; abs: string } | Result {
  const root = repoRoot(deps, cwd);
  const resolved = path.resolve(cwd, checkPath);
  if (!fs.existsSync(resolved)) return refuse(`check file not found: ${resolved}`);
  // realpath both sides: git reports the resolved toplevel (/private/var on
  // macOS) while cwd may be a symlinked spelling (/var).
  const abs = fs.realpathSync(resolved);
  const rel = path.relative(root, abs);
  if (rel.startsWith("..") || path.isAbsolute(rel)) return refuse(`check file ${abs} is outside the repo ${root}`);
  let committed: string;
  try {
    committed = deps.git(root, ["rev-parse", `HEAD:${rel}`]);
  } catch {
    return refuse(`check file ${rel} is not committed — commit it on the bugfix branch first`);
  }
  if (deps.git(root, ["hash-object", rel]) !== committed) return refuse(`check file ${rel} has uncommitted changes`);
  return { rel, abs };
}

const INTERPRETERS = new Set(["sh", "bash", "zsh", "dash", "ksh", "fish", "node", "deno", "bun", "python", "python3", "ruby", "perl", "php"]);
const NON_RUNNERS = new Set(["grep", "egrep", "fgrep", "rg", "cat", "test", "[", "true", "false", "echo", "printf", "ls", "stat", "find", "diff", "cmp", "head", "tail", "wc"]);

/** A command that could "pass" without running the check: inline code or a non-runner. */
function inlineCodeWrapper(argv: readonly string[]): string | null {
  const bin = path.basename(argv[0]);
  if (NON_RUNNERS.has(bin)) return `\`${bin}\``;
  if (INTERPRETERS.has(bin) || /^python\d/.test(bin)) {
    const inline = argv.slice(1).find((a) => /^-[a-zA-Z]*[ce]$/.test(a) || a === "--eval" || a === "--command");
    if (inline !== undefined) return `inline code (\`${bin} ${inline}\`)`;
  }
  return null;
}

const sameArgv = (a: readonly string[] | null, b: readonly string[] | null): boolean =>
  a !== null && b !== null && a.length === b.length && a.every((x, i) => x === b[i]);

/** Appends to the run registry, re-reading state first: the run may have taken minutes. */
function registerRun(dir: string, evidence: string, deps: Deps): void {
  const fresh = loadState(dir) as State;
  saveState(dir, { ...fresh, runs: [...fresh.runs, { evidence, sha256: deps.sha256(evidence) }] });
}

// Files a fix must not touch without the user's approval: the check itself,
// tests, fixtures, mocks, snapshots, test config, and the check's own
// directory (unless that is the repo root).
const PROTECTED = new RegExp(
  [
    // test-only directories
    String.raw`(^|/)(tests?|__tests__|specs?|fixtures?|__fixtures__|mocks?|__mocks__|__snapshots__|e2e|cypress|playwright|test-?utils|testing|testdata|\.ui-evidence)(/|$)`,
    // test-file naming across ecosystems
    String.raw`\.(test|spec)\.[^/]+$`, String.raw`(^|/)test_[^/]+\.py$`, String.raw`_test\.(go|py|rb)$`, String.raw`\.snap$`,
    // runner config and setup
    String.raw`(^|/)(vitest|vite|jest|playwright|karma|cypress|babel)\.(config|setup|workspace)\.[^/]+$`, String.raw`(^|/)(jest|vitest)\.setup[^/]*$`,
    String.raw`(^|/)(jest|vitest)-setup[^/]*$`, String.raw`(^|/)setupTests\.[^/]+$`, String.raw`(^|/)conftest\.py$`, String.raw`(^|/)\.mocharc[^/]*$`,
    String.raw`(^|/)(pytest\.ini|tox\.ini|pyproject\.toml|setup\.cfg)$`, String.raw`(^|/)\.babelrc[^/]*$`, String.raw`(^|/)\.env\.test[^/]*$`, String.raw`(^|/)tsconfig[^/]*\.json$`,
    // package manifests carry test scripts and runner config
    String.raw`(^|/)package\.json$`,
  ].join("|"),
  "i",
);
// FooTest.swift / MyAppUITests.swift / APITest.java: the capital T is
// case-sensitive, so Latest.kt or Contest.cs don't match.
const JVM_STYLE_TEST = /(^|\/)[^/]*Tests?\.(swift|kt|java|cs)$/;
function protectedFiles(files: readonly string[], checkPath: string): string[] {
  const checkDir = path.dirname(checkPath);
  return files.filter((f) => f === checkPath || PROTECTED.test(f) || JVM_STYLE_TEST.test(f) || (checkDir !== "." && f.startsWith(`${checkDir}/`)));
}

function readRun(dir: string, state: State, evidencePath: string, deps: Deps): RunEvidence | Result {
  const ev = readRunEvidence(evidencePath);
  if ("error" in ev) return refuse(ev.error);
  // Evidence only counts when run-test / run-ui wrote it and it is unmodified.
  const abs = path.resolve(evidencePath);
  const registered = state.runs.find((r) => r.evidence === abs);
  if (registered === undefined || registered.sha256 !== deps.sha256(abs)) return refuse(`evidence must come from bugfix-state run-test / run-ui for this bugfix (${abs} is not a registered, unmodified result)`);
  if (ev.checkSha256 === null) return refuse("the evidence records no check hash — update ui-evidence (summary.json needs scriptSha256)");
  return ev;
}

const isEvaluated = (c: Candidate): boolean => c.run !== null && (!c.run.passed || c.judge !== null);
const isEligible = (c: Candidate): boolean => c.run?.passed === true && c.judge?.decision === "resolved";

export function init(dir: string, ticketFile: string, deps: Deps): Result {
  let raw: unknown;
  try {
    raw = JSON.parse(fs.readFileSync(ticketFile, "utf8"));
  } catch {
    return bad(`cannot read ticket JSON: ${ticketFile}`);
  }
  const ticket = TicketSchema.safeParse(raw);
  if (!ticket.success) return bad(`invalid ticket: ${ticket.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ")}`);
  const existing = loadState(dir);
  if (!("error" in existing) && (existing.status === "active" || existing.status === "needs-human")) {
    return refuse(`an unfinished bugfix already exists at ${stateFile(dir)} (status ${existing.status}); resume it instead`);
  }
  const state: State = {
    version: 1,
    ticket: ticket.data,
    phase: "intake",
    status: "active",
    attempt: 0,
    attemptMode: null,
    handoff: null,
    investigation: null,
    check: null,
    baseline: null,
    candidates: [],
    runs: [],
    resolvedBy: null,
    history: [{ at: deps.now().toISOString(), command: "init", from: null, to: "intake", evidence: path.resolve(ticketFile) }],
  };
  saveState(dir, state);
  return ok({ state: stateFile(dir) });
}

export function advanceInvestigate(dir: string, handoffPath: string, deps: Deps): Result {
  return withState(dir, (state) => {
    const wrongPhase = requirePhase(state, ["intake"]);
    if (wrongPhase) return wrongPhase;
    let md: string;
    try {
      md = fs.readFileSync(handoffPath, "utf8");
    } catch {
      return refuse(`cannot read handoff: ${handoffPath}`);
    }
    const handoff = parseHandoff(md);
    if ("error" in handoff) return refuse(handoff.error);
    if (handoff.status !== "diagnosed") return refuse(`handoff status is "${handoff.status}", expected "diagnosed" (run /rootCause --investigate-only)`);
    if (!handoff.hypotheses.some((h) => h.result === "confirmed")) return refuse("no hypothesis is confirmed — ask the user before fixing an unconfirmed cause");
    if (handoff.rootCause === "") return refuse("the handoff has no ## Root Cause text");
    const abs = path.resolve(handoffPath);
    const investigation = { rootCause: handoff.rootCause, hypotheses: handoff.hypotheses.map(({ n, text, files, result }) => ({ n, text, files, result })) };
    transition(dir, { ...state, handoff: abs, investigation }, deps, "advance investigate", "investigate", abs);
    return ok({ phase: "investigate", hypotheses: handoff.hypotheses.length });
  });
}

export function advanceReproduce(dir: string, evidencePath: string, checkPath: string, cwd: string, deps: Deps): Result {
  return withState(dir, (state) => {
    const wrongPhase = requirePhase(state, ["investigate"]);
    if (wrongPhase) return wrongPhase;
    const ev = readRun(dir, state, evidencePath, deps);
    if ("exitCode" in ev) return ev;
    if (ev.outcome === "passed") return refuse("the baseline check passed on the unfixed code — the bug is not reproduced, so this check cannot prove a fix");
    if (ev.outcome === "broken") return refuse("the baseline run has broken steps and no failed step — a broken selector is not a reproduction; repair the script and re-run");
    if (ev.commit === null) return refuse("the evidence records no commit — pass --app-build $(git rev-parse HEAD) to ui-evidence");
    const head = deps.git(cwd, ["rev-parse", "HEAD"]);
    if (ev.commit !== head) return refuse(`the evidence ran against ${ev.commit}, but ${cwd} is at ${head}`);
    const check = committedCheck(deps, cwd, checkPath);
    if ("exitCode" in check) return check;
    const sha256 = deps.sha256(check.abs);
    if (ev.checkSha256 !== sha256) return refuse("the run executed a different version of the check file");
    const abs = path.resolve(evidencePath);
    transition(
      dir,
      { ...state, check: { kind: ev.kind, path: check.rel, sha256, command: ev.command }, baseline: { evidence: abs, commit: ev.commit } },
      deps,
      "advance reproduce",
      "reproduce",
      abs,
    );
    return ok({ phase: "reproduce", check: check.rel, baselineCommit: ev.commit });
  });
}

export function startAttempt(dir: string, mode: string, deps: Deps): Result {
  if (mode !== "A" && mode !== "B" && mode !== "C") return bad(`--mode must be A, B or C (got "${mode}")`);
  return withState(dir, (state) => {
    const wrongPhase = requirePhase(state, ["reproduce", "evaluate"]);
    if (wrongPhase) return wrongPhase;
    const current = state.candidates.filter((c) => c.attempt === state.attempt);
    const eligible = current.find(isEligible);
    if (eligible) return refuse(`candidate ${eligible.id} already resolves the ticket; run advance report --candidate ${eligible.id}`);
    const pending = current.find((c) => !isEvaluated(c));
    if (pending) return refuse(`candidate ${pending.id} has not been evaluated yet`);
    if (state.attempt >= MAX_ATTEMPTS) return refuse(`attempt cap (${MAX_ATTEMPTS}) reached; run advance report --unresolved`);
    if (mode === "B") {
      if (state.attempt === 0) return refuse("mode B (competing implementers) is the escalation after a failed attempt");
      const open = (state.investigation as NonNullable<State["investigation"]>).hypotheses.filter((h) => h.result !== "ruled-out").length;
      if (open < 2) return refuse(`mode B needs at least 2 hypotheses not ruled out; the handoff has ${open}`);
    }
    const attempt = state.attempt + 1;
    transition(dir, { ...state, attempt, attemptMode: mode as Mode, status: "active" }, deps, `start-attempt ${mode}`, "fix", null);
    return ok({ phase: "fix", attempt, mode });
  });
}

export function recordCandidate(dir: string, branch: string, cwd: string, hypothesis: string | undefined, allowTestChanges: boolean, deps: Deps): Result {
  if (hypothesis !== undefined && !/^\d+$/.test(hypothesis)) return bad(`--hypothesis must be a number (got "${hypothesis}")`);
  const hypothesisN = hypothesis === undefined ? null : Number(hypothesis);
  return withState(dir, (state) => {
    // evaluate too: in mode B the first candidate may be evaluated before the second is recorded.
    const wrongPhase = requirePhase(state, ["fix", "evaluate"]);
    if (wrongPhase) return wrongPhase;
    const mode = state.attemptMode as Mode;
    const inAttempt = state.candidates.filter((c) => c.attempt === state.attempt).length;
    const cap = mode === "B" ? 2 : 1;
    if (inAttempt >= cap) return refuse(`mode ${mode} allows ${cap} candidate(s) per attempt`);
    const actualBranch = deps.git(cwd, ["rev-parse", "--abbrev-ref", "HEAD"]);
    if (actualBranch !== branch) return refuse(`${cwd} is on branch ${actualBranch}, not ${branch}`);
    if (dirty(deps, cwd)) return refuse(`${cwd} has uncommitted or untracked changes; commit the fix first`);
    const commit = deps.git(cwd, ["rev-parse", "HEAD"]);
    const base = (state.baseline as { commit: string }).commit;
    if (commit === base) return refuse("no commits since the baseline — nothing to evaluate");
    try {
      deps.git(cwd, ["merge-base", "--is-ancestor", base, commit]);
    } catch {
      return refuse(`${commit} does not build on the baseline ${base} — start the worktree from the baseline commit`);
    }
    if (deps.git(cwd, ["rev-parse", `${commit}^{tree}`]) === deps.git(cwd, ["rev-parse", `${base}^{tree}`])) return refuse("the candidate's tree is identical to the baseline's — nothing was fixed");
    const duplicate = state.candidates.find((c) => c.commit === commit);
    if (duplicate) return refuse(`commit ${commit} is already candidate ${duplicate.id}`);
    if (mode === "B") {
      if (hypothesisN === null) return refuse("mode B candidates must name their hypothesis (--hypothesis <n>)");
      const taken = state.candidates.find((c) => c.attempt === state.attempt && c.hypothesis === hypothesisN);
      if (taken) return refuse(`hypothesis ${hypothesisN} is already pursued by ${taken.id} in this attempt`);
    }
    if (hypothesisN !== null) {
      const h = (state.investigation as NonNullable<State["investigation"]>).hypotheses.find((x) => x.n === hypothesisN);
      if (h === undefined) return refuse(`the handoff has no hypothesis ${hypothesisN}`);
      if (h.result === "ruled-out") return refuse(`hypothesis ${hypothesisN} was ruled out`);
    }
    // -z: raw (unquoted) paths; --no-renames: a moved test reports its old path too.
    const changedFiles = deps.git(cwd, ["diff", "--name-only", "--no-renames", "-z", base, commit]).split("\0").filter((f) => f !== "");
    const touched = protectedFiles(changedFiles, (state.check as NonNullable<State["check"]>).path);
    // Weakening what the check exercises (fixtures, helpers, config) is as
    // bad as editing the check itself.
    if (touched.length > 0 && !allowTestChanges) return refuse(`the fix touches test/fixture/config files (${touched.join(", ")}); ask the user, and pass --allow-test-changes only if they approve`);
    const candidate: Candidate = {
      id: `c${state.candidates.length + 1}`, attempt: state.attempt, mode, branch, cwd: path.resolve(cwd), commit,
      hypothesis: hypothesisN, changedFiles, judgeInputDigest: null, run: null, judge: null,
    };
    transition(dir, { ...state, candidates: [...state.candidates, candidate] }, deps, `record-candidate ${candidate.id}`, state.phase, null);
    return ok({ candidate: candidate.id, commit });
  });
}

function updateCandidate(state: State, next: Candidate): State {
  return { ...state, candidates: state.candidates.map((c) => (c.id === next.id ? next : c)) };
}

export function recordRun(dir: string, candidateId: string, evidencePath: string, deps: Deps): Result {
  return withState(dir, (state) => {
    const wrongPhase = requirePhase(state, ["fix", "evaluate"]);
    if (wrongPhase) return wrongPhase;
    const candidate = state.candidates.find((c) => c.id === candidateId);
    if (candidate === undefined) return bad(`unknown candidate: ${candidateId}`);
    if (candidate.run !== null) return refuse(`a run is already recorded for ${candidateId}`);
    const ev = readRun(dir, state, evidencePath, deps);
    if ("exitCode" in ev) return ev;
    const check = state.check as NonNullable<State["check"]>;
    if (ev.kind !== check.kind) return refuse(`the baseline check was ${check.kind}, this evidence is ${ev.kind}`);
    if (ev.outcome === "broken") return refuse("the run has broken steps and no failed step — repair the selector and re-run; a broken run is not a verdict on the fix");
    if (ev.kind === "test" && !sameArgv(ev.command, check.command)) return refuse(`the test command ${JSON.stringify(ev.command)} differs from the baseline's ${JSON.stringify(check.command)}`);
    if (ev.commit !== candidate.commit) return refuse(`the evidence ran against ${ev.commit ?? "no recorded commit"}, not ${candidateId}'s commit ${candidate.commit}`);
    const head = deps.git(candidate.cwd, ["rev-parse", "HEAD"]);
    if (head !== candidate.commit) return refuse(`${candidateId}'s branch moved to ${head} after record-candidate; record a new candidate`);
    if (dirty(deps, candidate.cwd)) return refuse(`${candidate.cwd} has uncommitted or untracked changes`);
    const checkFile = path.join(repoRoot(deps, candidate.cwd), check.path);
    // The frozen-check rule: an implementer must not "fix" the bug by
    // weakening the check — neither the file nor what the run executed.
    if (!fs.existsSync(checkFile) || deps.sha256(checkFile) !== check.sha256 || ev.checkSha256 !== check.sha256) return refuse(`the check file ${check.path} changed since the baseline`);
    const abs = path.resolve(evidencePath);
    const passed = ev.outcome === "passed";
    transition(
      dir,
      updateCandidate(state, { ...candidate, run: { evidence: abs, passed, commit: candidate.commit, recordedAt: deps.now().toISOString() } }),
      deps,
      `record-run ${candidateId}`,
      "evaluate",
      abs,
    );
    return ok({ candidate: candidateId, passed, next: passed ? `judge ${candidateId}` : "start-attempt with the failure reasons" });
  });
}

export function advanceReport(dir: string, candidateId: string | undefined, unresolved: boolean, deps: Deps): Result {
  if ((candidateId === undefined) === !unresolved) return bad("pass exactly one of --candidate <id> or --unresolved");
  return withState(dir, (state) => {
    if (state.phase === "report") return refuse(`already reported (status ${state.status})`);
    if (unresolved) {
      transition(dir, { ...state, status: "unresolved" }, deps, "advance report --unresolved", "report", null);
      return ok({ phase: "report", status: "unresolved" });
    }
    const candidate = state.candidates.find((c) => c.id === candidateId);
    if (candidate === undefined) return bad(`unknown candidate: ${candidateId}`);
    if (!isEligible(candidate)) return refuse(`${candidateId} needs a passing run and a "resolved" judge decision (run: ${candidate.run === null ? "none" : candidate.run.passed ? "passed" : "failed"}, judge: ${candidate.judge?.decision ?? "none"})`);
    transition(dir, { ...state, status: "resolved", resolvedBy: candidate.id }, deps, `advance report ${candidate.id}`, "report", candidate.run!.evidence);
    return ok({ phase: "report", status: "resolved", candidate: candidate.id });
  });
}

export function runTest(dir: string, checkPath: string, cwd: string, argv: string[], deps: Deps): Result {
  if (argv.length === 0) return bad("usage: bugfix-state run-test --state <dir> --check <file> [--cwd <dir>] -- <command...>");
  return withState(dir, (state) => {
    if (dirty(deps, cwd)) return refuse(`${cwd} has uncommitted or untracked changes; commit before running the check so the result maps to a commit`);
    const check = committedCheck(deps, cwd, checkPath);
    if ("exitCode" in check) return check;
    // The command must name the check file itself as an argument (not inside a
    // shell string), so a baseline of `false` and a run of `true` can't stand in.
    const namesCheck = (a: string): boolean => {
      const p = path.resolve(cwd, a);
      return a === check.rel || (fs.existsSync(p) && fs.realpathSync(p) === check.abs);
    };
    if (!argv.some(namesCheck)) return refuse(`the command must pass the check file (${check.rel}) as an argument`);
    const wrapper = inlineCodeWrapper(argv);
    if (wrapper !== null) return refuse(`the command must execute the check, not ${wrapper}`);
    const checkSha256 = deps.sha256(check.abs);
    const commit = deps.git(cwd, ["rev-parse", "HEAD"]);
    fs.mkdirSync(runsDir(dir), { recursive: true });
    const stamp = deps.now().toISOString().replace(/[:.]/g, "-");
    const log = path.join(runsDir(dir), `${stamp}-test.log`);
    const exitCode = deps.run(argv, cwd, log);
    const evidence = path.join(runsDir(dir), `${stamp}-test-result.json`);
    fs.writeFileSync(evidence, JSON.stringify({ kind: "test", command: argv, exitCode, commit, checkSha256, log }, null, 2) + "\n");
    registerRun(dir, evidence, deps);
    return ok({ evidence, exitCode, commit });
  });
}

/** Runs the ui-evidence CLI on the committed script and registers its summary.json. */
export function runUi(dir: string, checkPath: string, cwd: string, deps: Deps): Result {
  return withState(dir, () => {
    if (dirty(deps, cwd)) return refuse(`${cwd} has uncommitted or untracked changes; commit before running the check so the result maps to a commit`);
    const check = committedCheck(deps, cwd, checkPath);
    if ("exitCode" in check) return check;
    const commit = deps.git(cwd, ["rev-parse", "HEAD"]);
    fs.mkdirSync(runsDir(dir), { recursive: true });
    const stamp = deps.now().toISOString().replace(/[:.]/g, "-");
    const runDir = path.join(runsDir(dir), `${stamp}-ui`);
    const log = path.join(runsDir(dir), `${stamp}-ui.log`);
    const exitCode = deps.run([process.execPath, deps.uiEvidenceBin, check.abs, runDir, "--app-build", commit], cwd, log);
    const evidence = path.join(runDir, "summary.json");
    if (!fs.existsSync(evidence)) return refuse(`ui-evidence wrote no summary (exit ${exitCode}); see ${log}`);
    registerRun(dir, evidence, deps);
    return ok({ evidence, exitCode, commit });
  });
}

const firstLine = (text: string): string => text.trim().split("\n")[0];

/** Bound on the stored diff FIELD, so a runaway diff can't bloat judge-<id>.json; the cut is inside the string, so the JSON stays valid. */
export const DIFF_FIELD_CAP = 200_000;

const DIFF_EXCLUDES = [":(exclude)package-lock.json", ":(exclude)**/dist/**"];

/** The tip of the main the candidate last merged in (the second parent of its latest first-parent merge), if it merged one. */
function mergedMainTip(deps: Deps, cwd: string, base: string, commit: string): string | undefined {
  try {
    const latestMerge = deps.git(cwd, ["rev-list", "--first-parent", "--merges", "--max-count=1", `${base}..${commit}`]);
    return latestMerge === "" ? undefined : deps.git(cwd, ["rev-parse", `${latestMerge}^2`]);
  } catch {
    return undefined;
  }
}

/** The tree of the baseline with that main merged in; undefined when the merge conflicts or git can't do it. */
function baselinePlusMain(deps: Deps, cwd: string, base: string, mainTip: string): string | undefined {
  try {
    return deps.git(cwd, ["merge-tree", "--write-tree", base, mainTip]).split("\n")[0];
  } catch {
    return undefined;
  }
}

/**
 * What the candidate itself changed, as a diff or (stat) a diff stat. Without a
 * merge that is the diff from the baseline. After merging main it is the diff
 * from the baseline-plus-main tree, so main's moves never show, even in a file
 * the fix also edited. When that merge conflicts, the candidate's own commits
 * on its first-parent line, as patches.
 */
function ownChange(deps: Deps, cwd: string, base: string, commit: string, stat: boolean): string {
  const pathspec = ["--", ".", ...DIFF_EXCLUDES];
  const mainTip = mergedMainTip(deps, cwd, base, commit);
  if (mainTip === undefined) return deps.git(cwd, ["diff", ...(stat ? ["--stat"] : []), base, commit, ...pathspec]);
  const tree = baselinePlusMain(deps, cwd, base, mainTip);
  if (tree !== undefined) return deps.git(cwd, ["diff", ...(stat ? ["--stat"] : []), tree, commit, ...pathspec]);
  return deps.git(cwd, ["log", "--first-parent", "--no-merges", stat ? "--stat" : "-p", "--format=commit %h %s", `${base}..${commit}`, ...pathspec]);
}

function fullDiff(deps: Deps, cwd: string, base: string, commit: string): string | undefined {
  let diff: string;
  try {
    diff = ownChange(deps, cwd, base, commit, false);
  } catch {
    return undefined;
  }
  return diff.length <= DIFF_FIELD_CAP ? diff : `${diff.slice(0, DIFF_FIELD_CAP)}\n[truncated ${diff.length - DIFF_FIELD_CAP} chars]`;
}

/**
 * Asks judge resolution-check exactly once per candidate, on an input the
 * helper builds from state (ticket text verbatim, the snapshotted root cause,
 * a description of the frozen check, the diff stat and full diff), and records that
 * decision. The agent never picks among decisions, so a verdict can't be
 * re-rolled.
 */
export function judgeCandidate(dir: string, candidateId: string, deps: Deps): Result {
  return withState(dir, (state) => {
    const wrongPhase = requirePhase(state, ["evaluate"]);
    if (wrongPhase) return wrongPhase;
    const candidate = state.candidates.find((c) => c.id === candidateId);
    if (candidate === undefined) return bad(`unknown candidate: ${candidateId}`);
    if (candidate.run?.passed !== true) return refuse(`${candidateId} has no passing run; judge only runs after the check passes`);
    if (candidate.judge !== null) return refuse(`${candidateId} was already judged (${candidate.judge.decision}); a verdict is never re-asked`);
    const base = (state.baseline as { commit: string }).commit;
    const check = state.check as NonNullable<State["check"]>;
    // Derived from state, not agent-written, so it can't steer the verdict.
    const checkSummary = check.kind === "test"
      ? `the regression test ${check.path} (committed in the baseline before the fix and frozen; it failed there), run as \`${(check.command as string[]).join(" ")}\``
      : `the ui-evidence script ${check.path}`;
    // Key order must match ResolutionCheckInputSchema: judge digests the parsed input.
    const input = {
      brief: state.ticket.brief,
      expected: state.ticket.expected,
      actual: state.ticket.actual,
      rootCause: (state.investigation as NonNullable<State["investigation"]>).rootCause,
      checkKind: check.kind,
      checkSummary,
      beforePassed: false,
      afterPassed: true,
      diffStat: ownChange(deps, candidate.cwd, base, candidate.commit, true),
      // Full diff (RF-1: lockfiles and build output excluded; judge caps what it prompts with).
      // Omitted when git fails, so judging fails open to the stat-only question.
      diff: fullDiff(deps, candidate.cwd, base, candidate.commit),
    };
    const text = JSON.stringify(input);
    const judgeInputDigest = createHash("sha256").update(text).digest("hex").slice(0, 16);
    fs.writeFileSync(path.join(dir, `judge-${candidateId}.json`), text);
    const r = deps.judgeRun(text);
    let out: { id?: unknown; reason_code?: unknown; extra?: { reasons?: unknown } } = {};
    try {
      out = JSON.parse(r.stdout) as typeof out;
    } catch {
      // handled per exit code below
    }
    // The judge call can take minutes: record onto the current state, and never
    // over a verdict another call recorded meanwhile.
    const fresh = loadState(dir) as State;
    if ((fresh.candidates.find((c) => c.id === candidateId) as Candidate).judge !== null) return refuse(`${candidateId} was judged by another call meanwhile; that verdict stands`);
    if (r.status === 2) {
      const reasonCode = typeof out.reason_code === "string" ? out.reason_code : "escalated";
      transition(
        dir,
        { ...updateCandidate(fresh, { ...candidate, judgeInputDigest, judge: { decisionId: null, decision: "escalated", reasonCode } }), status: "needs-human" },
        deps,
        `judge ${candidateId}`,
        "evaluate",
        null,
      );
      return ok({ candidate: candidateId, decision: "escalated", reasonCode, status: "needs-human" });
    }
    // No verdict was obtained, so nothing is recorded and a retry is not a re-roll.
    if (r.status !== 0) return refuse(`judge failed (exit ${r.status}): ${firstLine(r.stderr) || firstLine(r.stdout)} — no verdict recorded; fix judge and re-run`);
    if (typeof out.id !== "string") return refuse("judge printed no decision id — no verdict recorded");
    const row = deps.judgeWhy(out.id);
    if (row === null) return refuse(`judge has no decision ${out.id}`);
    if (row.question !== "resolution-check" || row.input_digest !== judgeInputDigest) return refuse(`decision ${out.id} is not a resolution-check decision on the input the helper built`);
    const decision = row.decision;
    if (decision !== "resolved" && decision !== "partial" && decision !== "unresolved") return refuse(`decision ${out.id} has no usable outcome (${String(decision)})`);
    transition(
      dir,
      updateCandidate(fresh, { ...candidate, judgeInputDigest, judge: { decisionId: out.id, decision, reasonCode: row.reason_code } }),
      deps,
      `judge ${candidateId}`,
      "evaluate",
      null,
    );
    const reasons = Array.isArray(out.extra?.reasons) ? out.extra.reasons : [];
    return ok({ candidate: candidateId, decision, decisionId: out.id, reasons });
  });
}

export function status(dir: string): Result {
  return withState(dir, (state) => ok(JSON.stringify(state, null, 2)));
}

// Phase 5 runs in the main checkout detached at the candidate — possibly left
// that way by an interrupted session, so always name the exact commit.
const evaluateStep = (c: Candidate): string => `detach the main checkout at ${c.commit} (${c.id}), run the same check, then record-run ${c.id}`;

export function nextAction(state: State): string {
  if (state.status === "resolved" || state.status === "unresolved") return `done (${state.status}); write resolution.md if not written`;
  if (state.status === "needs-human") return "ask the user how to proceed: start-attempt, or advance report --unresolved";
  switch (state.phase) {
    case "intake":
      return "run /rootCause --investigate-only, then advance investigate --evidence <handoff.md>";
    case "investigate":
      return "build the check (ui-evidence script or regression test), run it on the unfixed code, then advance reproduce";
    case "reproduce":
      return "start-attempt --mode A or C";
    case "fix": {
      const current = state.candidates.filter((c) => c.attempt === state.attempt);
      if (current.length === 0) return "dispatch the implementer(s), then record-candidate";
      return evaluateStep(current.find((c) => c.run === null) as Candidate);
    }
    case "evaluate": {
      const current = state.candidates.filter((c) => c.attempt === state.attempt);
      const eligible = current.find(isEligible);
      if (eligible) return `advance report --candidate ${eligible.id}`;
      const needsRun = current.find((c) => c.run === null);
      if (needsRun) return evaluateStep(needsRun);
      const needsJudge = current.find((c) => !isEvaluated(c));
      if (needsJudge) return `judge ${needsJudge.id}`;
      if (state.attempt < MAX_ATTEMPTS) return "start-attempt with the failure reasons";
      return "advance report --unresolved";
    }
    default:
      return "done";
  }
}

export function resume(dir: string): Result {
  return withState(dir, (state) => ok({ phase: state.phase, status: state.status, attempt: state.attempt, next: nextAction(state) }));
}
