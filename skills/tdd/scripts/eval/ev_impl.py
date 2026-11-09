"""OUTPUT 3 (implementation) and OUTPUT 4 (qa): the TDD's PR plan matched against the real repository.

Matching (references/eval-loop.md, Tagging convention): a PR or commit carries the trailer `TDD: <slug> PR <n>`
(also accepted: `tdd:<slug>`, `tdd:<slug>#<n>`, `tdd: <slug> pr-<n>`); failing that, the TDD's PR title is matched
against merged PR titles (gh, similarity >= 0.85) or commit subjects on the default branch (git log --grep, fixed
string, case-insensitive). git and gh are read-only here: no fetch, no checkout. gh needs read access to the repo."""
import datetime, difflib, json, os, re, sys
import ev_common as C

TEST_FILE = re.compile(r"(^|/)(__tests__|tests?|spec|e2e)/|\.(test|spec|unit\.test|db\.test|e2e)\.[cm]?[jt]sx?$|(^|/)test_[^/]+\.py$|_test\.(py|go)$")
SHORTSTAT = re.compile(r"(\d+) files? changed(?:, (\d+) insertions?\(\+\))?(?:, (\d+) deletions?\(-\))?")
TEST_TIMEOUT = 120


def trailer_rx(slug):
    return re.compile(r"\btdd:\s*" + re.escape(slug) + r"(?:\s*(?:#|pr[\s\-#]*)(\d+))?\b", re.I)


def gh_repo(meta):
    m = re.match(r"https?://github\.com/([^/]+/[^/#?]+?)(?:\.git)?/?$", meta.get("repo_url") or "")
    return m.group(1) if m else None


def git(repo, *args, timeout=60):
    return C.run(["git", "-C", repo, *args], timeout=timeout)


def default_branch(repo):
    for ref in ("origin/main", "origin/master", "main", "master"):
        if git(repo, "rev-parse", "--verify", "-q", ref + "^{commit}")["code"] == 0:
            return ref
    return None


def days_between(a, b):
    try:
        da = datetime.date.fromisoformat(a[:10])
        db = datetime.date.fromisoformat(b[:10])
        return (db - da).days
    except (TypeError, ValueError):
        return None


def similar(a, b):
    return difflib.SequenceMatcher(None, C.norm(re.sub(r"\(#\d+\)\s*$", "", a or "")), C.norm(b)).ratio()


class Repo:
    def __init__(self, meta, slug, allow_gh=True):
        self.slug, self.meta = slug, meta
        self.path = os.path.expanduser(meta.get("repo_path") or "")
        self.rx = trailer_rx(slug)
        self.local_reason = self.gh_reason = None
        self.branch = self.head_date = None
        self.has_sha = False
        if not self.path or not os.path.isdir(self.path):
            self.local_reason = f"no repo at meta.repo_path ({meta.get('repo_path') or 'unset'})"
        elif not C.which("git"):
            self.local_reason = "git not on PATH"
        elif git(self.path, "rev-parse", "--git-dir")["code"] != 0:
            self.local_reason = f"{self.path} is not a git repository"
        else:
            self.branch = default_branch(self.path)
            if not self.branch:
                self.local_reason = "no main/master branch"
            else:
                self.head_date = git(self.path, "log", "-1", "--format=%cs", self.branch)["out"].strip()
                sha = meta.get("sha")
                self.has_sha = bool(sha) and git(self.path, "cat-file", "-e", f"{sha}^{{commit}}")["code"] == 0
        self.gh = gh_repo(meta) if allow_gh else None
        if not allow_gh:
            self.gh_reason = "gh disabled (--no-gh)"
        elif not self.gh:
            self.gh_reason = "meta.repo_url is not a github.com repository"
        elif not C.which("gh"):
            self.gh_reason = "gh not on PATH"
        else:
            r = C.run(["gh", "repo", "view", self.gh, "--json", "name"], timeout=60)
            if r["code"] != 0:
                self.gh_reason = f"gh cannot read {self.gh}: " + C.one_line(r["err"].split("Authorize")[0], 140)

    # -- local git
    def local_trailers(self):
        if self.local_reason:
            return {}
        r = git(self.path, "log", self.branch, "-i", "-E", "--grep=tdd:[[:space:]]*" + re.sub(r"([.^$*+?()\[\]{}|\\])", r"\\\1", self.slug), "--format=%H%x1f%cI%x1f%s%x1f%b%x1e", timeout=120)
        out = {}
        for rec in r["out"].split("\x1e"):
            parts = rec.strip("\n").split("\x1f")
            if len(parts) < 4:
                continue
            for m in self.rx.finditer(parts[2] + "\n" + parts[3]):
                if m.group(1):
                    out.setdefault(int(m.group(1)), {"sha": parts[0], "date": parts[1], "subject": parts[2], "how": "trailer (git log)"})
        return out

    def local_title(self, title):
        if self.local_reason or not title:
            return None
        r = git(self.path, "log", self.branch, "-i", "-F", f"--grep={title}", "--format=%H%x1f%cI%x1f%s", "-n", "5", timeout=120)
        for line in r["out"].splitlines():
            h, d, s = (line.split("\x1f") + ["", "", ""])[:3]
            if title.lower() in s.lower():
                return {"sha": h, "date": d, "subject": s, "how": "title in commit subject (git log --grep -F)"}
        return None

    def local_diff(self, sha):
        parents = git(self.path, "rev-list", "--parents", "-n", "1", sha)["out"].split()
        rng = [f"{sha}^1", sha] if len(parents) > 2 else [f"{sha}^", sha] if len(parents) == 2 else None
        if not rng:
            return None, []
        st = git(self.path, "diff", "--shortstat", *rng)["out"]
        names = git(self.path, "diff", "--name-only", *rng)["out"].split("\n")
        m = SHORTSTAT.search(st)
        stat = {"files": int(m.group(1)), "additions": int(m.group(2) or 0), "deletions": int(m.group(3) or 0)} if m else None
        return stat, [n for n in names if n]

    # -- gh
    def gh_json(self, args, timeout=90):
        r = C.run(["gh", *args, "-R", self.gh], timeout=timeout)
        if r["code"] != 0:
            return None, C.one_line(r["err"].split("Authorize")[0], 160)
        try:
            return json.loads(r["out"] or "null"), None
        except ValueError as e:
            return None, f"gh output is not JSON ({e})"

    def gh_trailers(self):
        if self.gh_reason:
            return {}, None
        data, err = self.gh_json(["pr", "list", "--state", "merged", "--search", f"\"tdd:{self.slug}\" in:body", "--limit", "100",
                                  "--json", "number,title,body,mergedAt,additions,deletions,url"])
        out = {}
        for p in data or []:
            for m in self.rx.finditer((p.get("title") or "") + "\n" + (p.get("body") or "")):
                if m.group(1):
                    out.setdefault(int(m.group(1)), dict(p, how="trailer (gh pr list)"))
        return out, err

    def gh_title(self, title):
        if self.gh_reason or not title:
            return None, None
        data, err = self.gh_json(["pr", "list", "--state", "merged", "--search", f"{title} in:title", "--limit", "5",
                                  "--json", "number,title,mergedAt,additions,deletions,url"])
        best = max(data or [], key=lambda p: similar(p.get("title"), title), default=None)
        if best and similar(best.get("title"), title) >= 0.85:
            return dict(best, how=f"title similarity {similar(best.get('title'), title):.2f} (gh pr list)"), err
        return None, err

    def gh_view(self, number):
        data, err = self.gh_json(["pr", "view", str(number), "--json", "reviews,files,mergedAt,additions,deletions,mergeCommit"])
        return data, err


def skipped(reason):
    """A (repo, rows, gh_error) triple for a skipped implementation pass."""
    r = Repo.__new__(Repo)
    r.slug = r.meta = r.rx = r.branch = r.head_date = r.gh = None
    r.path, r.has_sha, r.local_reason, r.gh_reason = "", False, reason, reason
    return r, [], None


def review_rounds(reviews):
    """Distinct reviewed commits when gh reports review.commit.oid; else submitted reviews that approve or request changes."""
    subs = [r for r in reviews or [] if (r.get("state") or "") not in ("PENDING", "DISMISSED")]
    oids = {((r.get("commit") or {}).get("oid")) for r in subs if (r.get("commit") or {}).get("oid")}
    if oids:
        return len(oids), "distinct reviewed commits (reviews[].commit.oid)"
    return sum(1 for r in subs if r.get("state") in ("APPROVED", "CHANGES_REQUESTED")), "reviews that approve or request changes"


def match_prs(ctx):
    content, meta = ctx["content"] or {}, ctx["meta"]
    slug = ctx["input"]["packet"]["slug"]
    repo = Repo(meta, slug, allow_gh=not ctx.get("no_gh"))
    prs = C.prs_of(content)
    lt = repo.local_trailers()
    gt, gerr = repo.gh_trailers()
    rows = []
    for p in prs:
        n = p["n"]
        row = {"n": n, "title": p["title"], "change": p["change"], "merged": False}
        hit = gt.get(n)
        if not hit and not repo.gh_reason:
            hit, _ = repo.gh_title(p["title"])
        if hit:
            row.update({"merged": True, "source": "gh", "how": hit["how"], "number": hit.get("number"), "url": hit.get("url"),
                        "merged_at": hit.get("mergedAt"), "additions": hit.get("additions"), "deletions": hit.get("deletions"), "gh_title": hit.get("title")})
            view, verr = repo.gh_view(hit["number"])
            if view:
                rounds, how = review_rounds(view.get("reviews"))
                row.update({"review_rounds": rounds, "review_rounds_how": how, "files": [f.get("path") for f in view.get("files") or []]})
            else:
                row["view_error"] = verr
        else:
            lh = lt.get(n) or repo.local_title(p["title"])
            if lh:
                stat, names = repo.local_diff(lh["sha"])
                row.update({"merged": True, "source": "git", "how": lh["how"], "sha": lh["sha"][:12], "merged_at": lh["date"], "subject": lh["subject"],
                            "files": names, **({"additions": stat["additions"], "deletions": stat["deletions"]} if stat else {})})
        if row.get("merged_at"):
            row["days_from_rev"] = days_between(meta.get("updated"), row["merged_at"])
        rows.append(row)
    return repo, rows, gerr


def evaluate_impl(ctx, matched):
    repo, rows, gerr = matched
    key = ctx["input"]["packet"]["key"]
    planned = len(rows)
    merged = [r for r in rows if r["merged"]]
    src = f"{repo.gh or 'gh'} + {repo.path or 'repo_path'}"
    method = ("merged PRs / planned PRs; a planned PR counts as merged when a merged GitHub PR or a default-branch commit carries its "
              "trailer (TDD: <slug> PR <n>) or its title; gh is preferred, local git is used only when the clone contains meta.sha")
    trusted = (not repo.gh_reason) or (not repo.local_reason and repo.has_sha)
    data = {"planned": planned, "repo": {"path": repo.path, "branch": repo.branch, "head_date": repo.head_date, "has_meta_sha": repo.has_sha,
                                         "local_reason": repo.local_reason, "gh": repo.gh, "gh_reason": repo.gh_reason, "gh_search_error": gerr},
            "prs": rows}
    m = {"planned": planned, "merged": len(merged)}
    adds = [r["additions"] for r in merged if isinstance(r.get("additions"), int)]
    dels = [r["deletions"] for r in merged if isinstance(r.get("deletions"), int)]
    days = [r["days_from_rev"] for r in merged if isinstance(r.get("days_from_rev"), int)]
    rounds = [r["review_rounds"] for r in merged if isinstance(r.get("review_rounds"), int)]
    if adds:
        m.update({"additions": sum(adds), "deletions": sum(dels)})
    if days:
        m.update({"days_from_rev_median": sorted(days)[len(days) // 2], "days_from_rev_max": max(days)})
    if rounds:
        m["review_rounds_mean"] = round(sum(rounds) / len(rounds), 2)
    scores = []
    if not planned:
        scores.append(C.not_run("implementation_merged", "no PR plan in content.json", method, src))
    elif not trusted:
        why = "; ".join(x for x in (repo.gh_reason, repo.local_reason or (f"local clone lacks meta.sha {ctx['meta'].get('sha')} (HEAD {repo.head_date}; git fetch is needed, not run by the eval)" if not repo.has_sha else None)) if x)
        scores.append(C.not_run("implementation_merged", why, method, src, m))
    else:
        scores.append(C.score("implementation_merged", C.ratio(len(merged), planned), method + ("" if not repo.gh_reason else f" (local git as of {repo.head_date})"), src, m))
    rr_method = "per merged PR: distinct reviewed commits (gh reviews[].commit.oid) else approving/changes-requested reviews; metric only"
    scores.append(C.metric("review_rounds", {"prs_with_reviews": len(rounds), **({"mean": m["review_rounds_mean"]} if rounds else {})}, rr_method, "gh pr view --json reviews")
                  if rounds else C.not_run("review_rounds", repo.gh_reason or "no merged PR with review data", rr_method, "gh pr view --json reviews"))
    ttm_method = "days from meta.updated (the TDD rev date) to each matched PR's merge date; metric only"
    scores.append(C.metric("time_to_merge", {"prs": len(days), "median_days": m["days_from_rev_median"], "max_days": m["days_from_rev_max"]}, ttm_method, src)
                  if days else C.not_run("time_to_merge", "no merged PR matched" if trusted else "not trusted (see implementation_merged)", ttm_method, src))
    scores.append(ctx["owner_review"](ctx["input"], "implementation"))
    return C.output("implementation", key, scores, data)


def declared_test_command(repo_path):
    pj = C.read_json(os.path.join(repo_path, "package.json")) or {}
    scripts = pj.get("scripts") or {}
    name = next((n for n in ("test", "test:unit") if scripts.get(n)), None)
    if name:
        pm = "bun" if any(os.path.exists(os.path.join(repo_path, f)) for f in ("bun.lock", "bun.lockb")) else \
             "pnpm" if os.path.exists(os.path.join(repo_path, "pnpm-lock.yaml")) else "yarn" if os.path.exists(os.path.join(repo_path, "yarn.lock")) else "npm"
        return [pm, "run", name], f"package.json scripts.{name} ({scripts[name][:80]})"
    py = C.read_text(os.path.join(repo_path, "pyproject.toml")) or ""
    if "[tool.pytest" in py:
        return [sys.executable, "-m", "pytest", "-q"], "pyproject.toml [tool.pytest]"
    return None, None


def evaluate_qa(ctx, matched, artifact_out):
    repo, rows, _ = matched
    key = ctx["input"]["packet"]["key"]
    merged = [r for r in rows if r["merged"]]
    scores = []
    tt_method = "merged PRs whose changed files include a test file (tests/, __tests__/, *.test.*, *.spec.*, test_*.py, *_test.py) / merged PRs"
    with_files = [r for r in merged if r.get("files") is not None]
    for r in with_files:
        r["test_files"] = [f for f in r["files"] if TEST_FILE.search(f)]
    if not with_files:
        scores.append(C.not_run("qa_tests_touched", "no merged PR with a file list (see output.implementation.json)", tt_method, "gh pr view --json files / git diff --name-only"))
    else:
        hit = sum(1 for r in with_files if r["test_files"])
        scores.append(C.score("qa_tests_touched", C.ratio(hit, len(with_files)), tt_method, "gh pr view --json files / git diff --name-only",
                              {"merged_with_files": len(with_files), "with_tests": hit, "test_files": sum(len(r["test_files"]) for r in with_files)},
                              [{"n": r["n"], "test_files": r["test_files"][:20]} for r in with_files]))
    tc_method = f"the repo's declared test command at its current HEAD, capped at {TEST_TIMEOUT} s: exit 0 -> 1, else 0; over the cap -> not run: too slow"
    cmd, origin = (declared_test_command(repo.path) if not repo.local_reason else (None, None))
    if repo.local_reason:
        scores.append(C.not_run("qa_test_command", repo.local_reason, tc_method, "repo"))
    elif not cmd:
        scores.append(C.not_run("qa_test_command", "the repo declares no test command (package.json scripts.test or test:unit, pyproject [tool.pytest])", tc_method, repo.path))
    elif not ctx.get("run_tests"):
        scores.append(C.not_run("qa_test_command", f"pass --run-tests to run `{' '.join(cmd)}` ({origin}) in {repo.path}", tc_method, origin))
    elif not C.which(cmd[0]) and not os.path.isabs(cmd[0]):
        scores.append(C.not_run("qa_test_command", f"{cmd[0]} not on PATH", tc_method, origin))
    else:
        r = C.run(cmd, cwd=repo.path, timeout=TEST_TIMEOUT)
        if r["timeout"]:
            scores.append(C.not_run("qa_test_command", f"too slow (> {TEST_TIMEOUT} s)", tc_method, origin, {"seconds": r["seconds"]}))
        else:
            scores.append(C.score("qa_test_command", 1 if r["code"] == 0 else 0, tc_method, origin, {"exit": r["code"], "seconds": r["seconds"]},
                                  {"tail": C.tail(r["out"] + "\n" + r["err"], 15)}))
    for name in ("artifact_check", "check_hydrate"):
        rec = next((dict(s) for s in artifact_out["scores"] if s["name"] == name), None)
        if rec:
            rec["source"] = f"copied from output.artifact.json ({rec['source']})"
            scores.append(rec)
    scores.append(ctx["owner_review"](ctx["input"], "qa"))
    return C.output("qa", key, scores, {"prs": [{"n": r["n"], "merged": r["merged"], "test_files": r.get("test_files")} for r in rows]})
