// ev_impl.py: OUTPUT 3 (implementation) and OUTPUT 4 (qa), the TDD's PR plan matched against the real repository.
// A PR or commit carries the trailer `TDD: <slug> PR <n>` (also `tdd:<slug>`, `tdd:<slug>#<n>`, `tdd: <slug> pr-<n>`);
// failing that, the PR title is matched against merged PR titles (gh, similarity >= 0.85) or commit subjects on the
// default branch (git log --grep, fixed string, case-insensitive). git and gh are read-only: no fetch, no checkout.
import { Effect } from "effect";
import * as py from "../py";
import { pyStr } from "../py/repr";
import { SequenceMatcher } from "../hotspots/difflib";
import { type Py, fixed, fl, get, isInt, norm, prsOf, pySum, ratio, readJson, readText, tail } from "./common";
import { type Input } from "./capture";
import { python } from "./artifact";
import { type Output, type ScoreRec, metric, notRun, output, score, settle, unscored } from "./score";
import { Tools } from "./tools";

const re = py.re;
const path = py.path;

const TEST_FILE = re.compile(String.raw`(^|/)(__tests__|tests?|spec|e2e)/|\.(test|spec|unit\.test|db\.test|e2e)\.[cm]?[jt]sx?$|(^|/)test_[^/]+\.py$|_test\.(py|go)$`);
const SHORTSTAT = re.compile(String.raw`(\d+) files? changed(?:, (\d+) insertions?\(\+\))?(?:, (\d+) deletions?\(-\))?`);
export const TEST_TIMEOUT = 120;

export const trailerRx = (slug: string) => re.compile(String.raw`\btdd:\s*` + re.escape(slug) + String.raw`(?:\s*(?:#|pr[\s\-#]*)(\d+))?\b`, re.I);

export function ghRepo(meta: Py): string | null {
  const m = re.match(String.raw`https?://github\.com/([^/]+/[^/#?]+?)(?:\.git)?/?$`, py.truthy(get(meta, "repo_url")) ? get(meta, "repo_url") : "");
  return m ? m.group(1) : null;
}

/** (date.fromisoformat(b[:10]) - date.fromisoformat(a[:10])).days, or None. */
// date.fromisoformat's two date forms, ASCII digits only ([0-9], not \d, which is Unicode in Python's re)
const ISO_DASH = re.compile("([0-9]{4})-([0-9]{2})-([0-9]{2})");
const ISO_BASIC = re.compile("([0-9]{4})([0-9]{2})([0-9]{2})");

export function daysBetween(a: Py, b: Py): number | null {
  const parse = (s: Py): number | null => {
    if (typeof s !== "string") return null;
    const t = py.cpSlice(s, 0, 10);
    const m = ISO_DASH.fullmatch(t) ?? ISO_BASIC.fullmatch(t);
    if (!m) return null;
    const [y, mo, d] = [Number(m.group(1)), Number(m.group(2)), Number(m.group(3))];
    const ms = Date.UTC(y, mo - 1, d);
    const dt = new Date(ms);
    if (y < 1 || dt.getUTCFullYear() !== y || dt.getUTCMonth() !== mo - 1 || dt.getUTCDate() !== d) return null;
    return ms;
  };
  const da = parse(a);
  const db = parse(b);
  return da === null || db === null ? null : Math.round((db - da) / 86400000);
}

export function similar(a: Py, b: Py): number {
  const x = norm(re.sub(String.raw`\(#\d+\)\s*$`, "", py.truthy(a) ? a : ""));
  return new SequenceMatcher<string>(null, x, norm(b)).ratio();
}

export interface Repo {
  slug: string | null; meta: Py; path: string; rx: py.re.Pattern | null; localReason: string | null; ghReason: string | null;
  branch: string | null; headDate: string | null; hasSha: boolean; gh: string | null;
}

const git = (repo: string, args: string[], timeout = 60) => Effect.gen(function* () {
  const t = yield* Tools;
  return yield* t.run(["git", "-C", repo, ...args], { timeout });
});

const defaultBranch = (repo: string) => Effect.gen(function* () {
  for (const ref of ["origin/main", "origin/master", "main", "master"])
    if ((yield* git(repo, ["rev-parse", "--verify", "-q", ref + "^{commit}"])).code === 0) return ref;
  return null;
});

export const openRepo = (meta: Py, slug: string, allowGh = true) => Effect.gen(function* () {
  const tools = yield* Tools;
  const r: Repo = { slug, meta, path: path.expanduser(py.truthy(get(meta, "repo_path")) ? get(meta, "repo_path") : ""), rx: trailerRx(slug),
    localReason: null, ghReason: null, branch: null, headDate: null, hasSha: false, gh: null };
  if (!r.path || !py.io.isdir(r.path)) r.localReason = `no repo at meta.repo_path (${py.truthy(get(meta, "repo_path")) ? pyStr(get(meta, "repo_path")) : "unset"})`;
  else if (!tools.which("git")) r.localReason = "git not on PATH";
  else if ((yield* git(r.path, ["rev-parse", "--git-dir"])).code !== 0) r.localReason = `${r.path} is not a git repository`;
  else {
    r.branch = yield* defaultBranch(r.path);
    if (!r.branch) r.localReason = "no main/master branch";
    else {
      r.headDate = py.str.strip((yield* git(r.path, ["log", "-1", "--format=%cs", r.branch])).out);
      const sha = get(meta, "sha");
      r.hasSha = py.truthy(sha) && (yield* git(r.path, ["cat-file", "-e", `${pyStr(sha)}^{commit}`])).code === 0;
    }
  }
  r.gh = allowGh ? ghRepo(meta) : null;
  if (!allowGh) r.ghReason = "gh disabled (--no-gh)";
  else if (!r.gh) r.ghReason = "meta.repo_url is not a github.com repository";
  else if (!tools.which("gh")) r.ghReason = "gh not on PATH";
  else {
    const v = yield* tools.run(["gh", "repo", "view", r.gh, "--json", "name"], { timeout: 60 });
    if (v.code !== 0) r.ghReason = `gh cannot read ${r.gh}: ` + oneLineGh(v.err, 140);
  }
  return r;
});

const oneLineGh = (err: string, limit: number): string => {
  const s = py.str.split(err, "Authorize")[0]!;
  let t = py.str.split(s).join(" ");
  t = re.sub(String.raw`https?://\S*(authorization_request|token|sso)\S*`, "<url omitted>", t);
  return py.cpSlice(t, 0, limit);
};

type Hit = Record<string, Py>;

const localTrailers = (r: Repo) => Effect.gen(function* () {
  const out = new Map<number, Hit>();
  if (r.localReason) return out;
  const esc = re.sub(String.raw`([.^$*+?()\[\]{}|\\])`, String.raw`\\\1`, r.slug!);
  const res = yield* git(r.path, ["log", r.branch!, "-i", "-E", "--grep=tdd:[[:space:]]*" + esc, "--format=%H%x1f%cI%x1f%s%x1f%b%x1e"], 120);
  for (const rec of res.out.split("\x1e")) {
    const parts = py.str.strip(rec, "\n").split("\x1f");
    if (parts.length < 4) continue;
    for (const m of r.rx!.finditer(parts[2] + "\n" + parts[3])) {
      const g = m.group(1);
      if (g) {
        const k = Number(py.int(g));
        if (!out.has(k)) out.set(k, { sha: parts[0], date: parts[1], subject: parts[2], how: "trailer (git log)" });
      }
    }
  }
  return out;
});

const localTitle = (r: Repo, title: string) => Effect.gen(function* () {
  if (r.localReason || !title) return null;
  const res = yield* git(r.path, ["log", r.branch!, "-i", "-F", `--grep=${title}`, "--format=%H%x1f%cI%x1f%s", "-n", "5"], 120);
  for (const line of py.str.splitlines(res.out)) {
    const [h, d, s] = [...line.split("\x1f"), "", "", ""];
    if (py.lower(s!).includes(py.lower(title))) return { sha: h, date: d, subject: s, how: "title in commit subject (git log --grep -F)" } as Hit;
  }
  return null;
});

const localDiff = (r: Repo, sha: string) => Effect.gen(function* () {
  const parents = py.str.split((yield* git(r.path, ["rev-list", "--parents", "-n", "1", sha])).out);
  const rng = parents.length > 2 ? [`${sha}^1`, sha] : parents.length === 2 ? [`${sha}^`, sha] : null;
  if (!rng) return [null, [] as string[]] as const;
  const st = (yield* git(r.path, ["diff", "--shortstat", ...rng])).out;
  const names = (yield* git(r.path, ["diff", "--name-only", ...rng])).out.split("\n");
  const m = SHORTSTAT.search(st);
  const stat = m ? { files: Number(py.int(m.group(1)!)), additions: Number(py.int(m.group(2) || "0")), deletions: Number(py.int(m.group(3) || "0")) } : null;
  return [stat, names.filter((x) => x)] as const;
});

const ghJson = (r: Repo, args: string[], timeout = 90) => Effect.gen(function* () {
  const tools = yield* Tools;
  const res = yield* tools.run(["gh", ...args, "-R", r.gh!], { timeout });
  if (res.code !== 0) return [null, oneLineGh(res.err, 160)] as const;
  try {
    return [py.json.loads(res.out || "null"), null] as const;
  } catch (e) {
    return [null, `gh output is not JSON (${(e as Error).message})`] as const;
  }
});

const ghTrailers = (r: Repo) => Effect.gen(function* () {
  const out = new Map<number, Py>();
  if (r.ghReason) return [out, null] as const;
  const [data, err] = yield* ghJson(r, ["pr", "list", "--state", "merged", "--search", `"tdd:${r.slug}" in:body`, "--limit", "100",
    "--json", "number,title,body,mergedAt,additions,deletions,url"]);
  for (const p of Array.isArray(data) ? data : []) {
    for (const m of r.rx!.finditer(pyStr(get(p, "title") || "") + "\n" + pyStr(get(p, "body") || ""))) {
      const g = m.group(1);
      if (g && !out.has(Number(py.int(g)))) out.set(Number(py.int(g)), new Map([...(p as Map<string, Py>), ["how", "trailer (gh pr list)"]]));
    }
  }
  return [out, err] as const;
});

const ghTitle = (r: Repo, title: string) => Effect.gen(function* () {
  if (r.ghReason || !title) return [null, null] as const;
  const [data, err] = yield* ghJson(r, ["pr", "list", "--state", "merged", "--search", `${title} in:title`, "--limit", "5",
    "--json", "number,title,mergedAt,additions,deletions,url"]);
  let best: Py = null;
  let bestS = -1;
  for (const p of Array.isArray(data) ? data : []) {
    const s = similar(get(p, "title"), title);
    if (s > bestS) [best, bestS] = [p, s];
  }
  if (best && bestS >= 0.85) return [new Map([...(best as Map<string, Py>), ["how", `title similarity ${fixed(fl(bestS), 2)} (gh pr list)`]]), err] as const;
  return [null, err] as const;
});

const ghView = (r: Repo, num: Py) => ghJson(r, ["pr", "view", pyStr(num), "--json", "reviews,files,mergedAt,additions,deletions,mergeCommit"]);

export type Matched = readonly [Repo, Record<string, Py>[], string | null];

/** A (repo, rows, gh_error) triple for a skipped implementation pass. */
export function skipped(reason: string): Matched {
  return [{ slug: null, meta: null, path: "", rx: null, localReason: reason, ghReason: reason, branch: null, headDate: null, hasSha: false, gh: null }, [], null];
}

/** Distinct reviewed commits when gh reports review.commit.oid; else reviews that approve or request changes. */
export function reviewRounds(reviews: Py): [number, string] {
  const subs = (Array.isArray(reviews) ? reviews : []).filter((r) => !["PENDING", "DISMISSED"].includes(py.truthy(get(r, "state")) ? get(r, "state") : ""));
  const oid = (r: Py) => get(py.truthy(get(r, "commit")) ? get(r, "commit") : new Map(), "oid");
  const oids = new Set(subs.filter((r) => py.truthy(oid(r))).map(oid));
  if (oids.size) return [oids.size, "distinct reviewed commits (reviews[].commit.oid)"];
  return [subs.filter((r) => ["APPROVED", "CHANGES_REQUESTED"].includes(get(r, "state"))).length, "reviews that approve or request changes"];
}

const hv = (h: Py, k: string): Py => (h instanceof Map ? get(h, k) : h[k] ?? null);

export const matchPrs = (content0: Py, meta: Py, slug: string, noGh: boolean) => Effect.gen(function* () {
  const content = py.truthy(content0) ? content0 : new Map();
  const repo = yield* openRepo(meta, slug, !noGh);
  const prs = prsOf(content);
  const lt = yield* localTrailers(repo);
  const [gt, gerr] = yield* ghTrailers(repo);
  const rows: Record<string, Py>[] = [];
  for (const p of prs) {
    const num = p.n;
    const row: Record<string, Py> = { n: num, title: p.title, change: p.change, merged: false };
    const nk = isInt(num) ? Number(num) : NaN;
    let hit: Py = gt.get(nk) ?? null;
    if (!hit && !repo.ghReason) hit = (yield* ghTitle(repo, p.title))[0];
    if (hit) {
      Object.assign(row, { merged: true, source: "gh", how: hv(hit, "how"), number: hv(hit, "number"), url: hv(hit, "url"), merged_at: hv(hit, "mergedAt"),
        additions: hv(hit, "additions"), deletions: hv(hit, "deletions"), gh_title: hv(hit, "title") });
      const [view, verr] = yield* ghView(repo, hv(hit, "number"));
      if (py.truthy(view)) {
        const [rounds, how] = reviewRounds(get(view, "reviews"));
        Object.assign(row, { review_rounds: rounds, review_rounds_how: how, files: (Array.isArray(get(view, "files")) ? get(view, "files") : []).map((f: Py) => get(f, "path")) });
      } else row.view_error = verr;
    } else {
      const lh = lt.get(nk) ?? (yield* localTitle(repo, p.title));
      if (lh) {
        const [stat, names] = yield* localDiff(repo, lh.sha);
        Object.assign(row, { merged: true, source: "git", how: lh.how, sha: py.cpSlice(lh.sha, 0, 12), merged_at: lh.date, subject: lh.subject, files: [...names],
          ...(stat ? { additions: stat.additions, deletions: stat.deletions } : {}) });
      }
    }
    if (py.truthy(row.merged_at)) row.days_from_rev = daysBetween(get(meta, "updated"), row.merged_at);
    rows.push(row);
  }
  return [repo, rows, gerr] as Matched;
});

export function evaluateImpl(inp: Input, meta: Py, matched: Matched, ownerReview: (inp: Input, t: string) => ScoreRec): Output {
  const [repo, rows, gerr] = matched;
  const key = inp.packet.key;
  const planned = rows.length;
  const merged = rows.filter((r) => r.merged);
  const src = `${repo.gh || "gh"} + ${repo.path || "repo_path"}`;
  const method = "merged PRs / planned PRs; a planned PR counts as merged when a merged GitHub PR or a default-branch commit carries its " +
    "trailer (TDD: <slug> PR <n>) or its title; gh is preferred, local git is used only when the clone contains meta.sha";
  const trusted = !repo.ghReason || (!repo.localReason && repo.hasSha);
  const data = { planned, repo: { path: repo.path, branch: repo.branch, head_date: repo.headDate, has_meta_sha: repo.hasSha,
    local_reason: repo.localReason, gh: repo.gh, gh_reason: repo.ghReason, gh_search_error: gerr }, prs: rows };
  const m: Record<string, Py> = { planned, merged: merged.length };
  const adds = merged.filter((r) => isInt(r.additions)).map((r) => r.additions);
  const dels = merged.filter((r) => isInt(r.deletions)).map((r) => r.deletions);
  const days: number[] = merged.filter((r) => isInt(r.days_from_rev)).map((r) => r.days_from_rev);
  const rounds: number[] = merged.filter((r) => isInt(r.review_rounds)).map((r) => r.review_rounds);
  if (adds.length) Object.assign(m, { additions: pySum(adds), deletions: pySum(dels) });
  if (days.length) Object.assign(m, { days_from_rev_median: [...days].sort((a, b) => a - b)[Math.floor(days.length / 2)], days_from_rev_max: Math.max(...days) });
  if (rounds.length) m.review_rounds_mean = fl(py.round(rounds.reduce((a, b) => a + b, 0) / rounds.length, 2));
  const scores: ScoreRec[] = [];
  if (!planned) scores.push(notRun("implementation_merged", "no PR plan in content.json", method, src));
  else if (!trusted) {
    const lacks = !repo.hasSha ? `local clone lacks meta.sha ${pyStr(get(meta, "sha"))} (HEAD ${pyStr(repo.headDate)}; git fetch is needed, not run by the eval)` : null;
    const why = [repo.ghReason, repo.localReason || lacks].filter((x) => x).join("; ");
    scores.push(notRun("implementation_merged", why, method, src, m));
  } else
    scores.push(score("implementation_merged", ratio(merged.length, planned), method + (!repo.ghReason ? "" : ` (local git as of ${pyStr(repo.headDate)})`), src, m));
  const rrMethod = "per merged PR: distinct reviewed commits (gh reviews[].commit.oid) else approving/changes-requested reviews; metric only";
  scores.push(rounds.length ? metric("review_rounds", { prs_with_reviews: rounds.length, mean: m.review_rounds_mean }, rrMethod, "gh pr view --json reviews")
    : notRun("review_rounds", repo.ghReason || "no merged PR with review data", rrMethod, "gh pr view --json reviews"));
  const ttmMethod = "days from meta.updated (the TDD rev date) to each matched PR's merge date; metric only";
  scores.push(days.length ? metric("time_to_merge", { prs: days.length, median_days: m.days_from_rev_median, max_days: m.days_from_rev_max }, ttmMethod, src)
    : notRun("time_to_merge", trusted ? "no merged PR matched" : "not trusted (see implementation_merged)", ttmMethod, src));
  scores.push(ownerReview(inp, "implementation"));
  return output("implementation", key, scores, data);
}

export function declaredTestCommand(repoPath: string): [string[] | null, string | null] {
  const pj0 = readJson(path.join(repoPath, "package.json"));
  const pj = py.truthy(pj0) ? pj0 : new Map();
  const scripts = py.truthy(get(pj, "scripts")) ? get(pj, "scripts") : new Map();
  const name = ["test", "test:unit"].find((k) => py.truthy(get(scripts, k))) ?? null;
  if (name) {
    const has = (f: string) => py.io.exists(path.join(repoPath, f));
    const pm = has("bun.lock") || has("bun.lockb") ? "bun" : has("pnpm-lock.yaml") ? "pnpm" : has("yarn.lock") ? "yarn" : "npm";
    return [[pm, "run", name], `package.json scripts.${name} (${py.cpSlice(pyStr(get(scripts, name)), 0, 80)})`];
  }
  const pyproject = readText(path.join(repoPath, "pyproject.toml")) || "";
  if (pyproject.includes("[tool.pytest")) return [[python(), "-m", "pytest", "-q"], "pyproject.toml [tool.pytest]"];
  return [null, null];
}

/** The outer limit on the test command (seconds): above TEST_TIMEOUT, so the tool's own cap ("too slow") wins. */
export const QA_LIMIT = TEST_TIMEOUT + 60;

export const evaluateQa = (inp: Input, matched: Matched, artifactOut: Output, runTests: boolean, ownerReview: (inp: Input, t: string) => ScoreRec,
  limit: number = QA_LIMIT) =>
  Effect.gen(function* () {
    const tools = yield* Tools;
    const [repo, rows] = matched;
    const merged = rows.filter((r) => r.merged);
    const scores: ScoreRec[] = [];
    const ttMethod = "merged PRs whose changed files include a test file (tests/, __tests__/, *.test.*, *.spec.*, test_*.py, *_test.py) / merged PRs";
    const withFiles = merged.filter((r) => r.files !== null && r.files !== undefined);
    for (const r of withFiles) r.test_files = (r.files as string[]).filter((f) => TEST_FILE.search(f) !== null);
    if (!withFiles.length)
      scores.push(notRun("qa_tests_touched", "no merged PR with a file list (see output.implementation.json)", ttMethod, "gh pr view --json files / git diff --name-only"));
    else {
      const hit = withFiles.filter((r) => r.test_files.length).length;
      scores.push(score("qa_tests_touched", ratio(hit, withFiles.length), ttMethod, "gh pr view --json files / git diff --name-only",
        { merged_with_files: withFiles.length, with_tests: hit, test_files: withFiles.reduce((a, r) => a + r.test_files.length, 0) },
        withFiles.map((r) => ({ n: r.n, test_files: r.test_files.slice(0, 20) }))));
    }
    const tcMethod = `the repo's declared test command at its current HEAD, capped at ${TEST_TIMEOUT} s: exit 0 -> 1, else 0; over the cap -> not run: too slow`;
    const [cmd, origin] = !repo.localReason ? declaredTestCommand(repo.path) : [null, null];
    if (repo.localReason) scores.push(notRun("qa_test_command", repo.localReason, tcMethod, "repo"));
    else if (!cmd)
      scores.push(notRun("qa_test_command", "the repo declares no test command (package.json scripts.test or test:unit, pyproject [tool.pytest])", tcMethod, repo.path));
    else if (!runTests) scores.push(notRun("qa_test_command", `pass --run-tests to run \`${cmd.join(" ")}\` (${origin}) in ${repo.path}`, tcMethod, origin!));
    else if (!tools.which(cmd[0]!) && !path.isabs(cmd[0]!)) scores.push(notRun("qa_test_command", `${cmd[0]} not on PATH`, tcMethod, origin!));
    else {
      // the tool returns at TEST_TIMEOUT (its process group killed); the outer limit only bounds a tool that would not
      const o = yield* settle(tools.run(cmd, { cwd: repo.path, timeout: TEST_TIMEOUT }), limit);
      if (o._tag !== "scored") scores.push(unscored({ name: "qa_test_command", method: tcMethod, source: origin! }, o));
      else if (o.value.timeout) scores.push(notRun("qa_test_command", `too slow (> ${TEST_TIMEOUT} s)`, tcMethod, origin!, { seconds: o.value.seconds }));
      else {
        const r = o.value;
        scores.push(score("qa_test_command", r.code === 0 ? 1 : 0, tcMethod, origin!, { exit: r.code, seconds: r.seconds }, { tail: tail(r.out + "\n" + r.err, 15) }));
      }
    }
    for (const name of ["artifact_check", "check_hydrate"]) {
      const s = artifactOut.scores.find((x) => x.name === name);
      if (s) scores.push({ ...s, source: `copied from output.artifact.json (${s.source})` });
    }
    scores.push(ownerReview(inp, "qa"));
    return output("qa", inp.packet.key, scores, { prs: rows.map((r) => ({ n: r.n, merged: r.merged, test_files: r.test_files ?? null })) });
  });
