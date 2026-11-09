"""OUTPUT 1, the artifact page (build/tdd.html and the content.json it renders): contract, hydration, lint, linking,
citations, structure, decisions, diagrams, schema coverage, reading time, owner review notes. Every record says how it
was computed. Tools that are absent are recorded as "not run: <reason>", never as a failure or a zero."""
import glob, os, re, sys
import ev_common as C

COMPILER_LINK_KEYS = {"html", "caption", "reading", "one", "sub", "bodyHtml", "summary", "status", "change", "rollback"}
# Mirror of lint_packet.Lint.SLOTS (the house six); replaced by the linter's own list when it imports.
HOUSE_SLOTS = [
    ("the redesign (overview)", {"redesign", "overview"}, r"\b(redesign|overview)\b"),
    ("front end", {"frontend", "front-end"}, r"\bfront[\s-]?end\b"),
    ("rule coverage or requirements", {"coverage", "requirements", "rules"}, r"\b(coverage|requirements)\b"),
    ("end-to-end flow", {"flow", "end-to-end", "end-to-end-flow", "e2e"}, r"\bend[\s-]to[\s-]end\b"),
    ("decisions and open questions", {"questions", "decisions", "open-questions"}, r"\bdecisions?\b|\bopen questions\b"),
    ("for agents", {"more", "agents", "for-agents"}, r"\bfor agents\b"),
]
DDL_NAME = re.compile(r"(?i)\b(?:CREATE\s+(?:UNLOGGED\s+)?TABLE(?:\s+IF\s+NOT\s+EXISTS)?|ALTER\s+TABLE(?:\s+IF\s+EXISTS)?(?:\s+ONLY)?)\s+(\"?[\w.]+\"?)")
HYDRATE_LINE = re.compile(r"console errors (\d+) \| console warnings (\d+) \(hydration (\d+)\) \| failed steps (\d+)")


def artifact_check_path():
    """Mirror of build_tdd.sd_root(): $SYSTEM_DESIGN_SKILL, the sibling skills/system-design, the provider install,
    then the skill's own vendor/system-design. The first candidate that has scripts/artifact_check.mjs wins, so
    upstream's system-design (which has none) falls through to the vendored copy."""
    here = os.path.dirname(os.path.abspath(__file__))
    skill = os.path.normpath(os.path.join(here, "..", ".."))
    candidates = (os.environ.get("SYSTEM_DESIGN_SKILL"),
                  os.path.normpath(os.path.join(skill, "..", "system-design")),
                  os.path.expanduser("~/.claude/skills/system-design"),
                  os.path.join(skill, "vendor", "system-design"))
    for c in candidates:
        p = os.path.join(c, "scripts", "artifact_check.mjs") if c else None
        if p and os.path.isfile(p):
            return p
    return None


def s_page(page):
    src = "build/tdd.html"
    if not os.path.isfile(page):
        return [C.score("page_exists", 0, "os.path.isfile(build/tdd.html)", src), C.not_run("page_size", "no page", "bytes <= 16 MB (16*1024*1024)", src)]
    b = os.path.getsize(page)
    return [C.score("page_exists", 1, "os.path.isfile(build/tdd.html)", src),
            C.score("page_size", 1 if b <= C.PAGE_LIMIT else 0, "1 when page bytes <= 16 MB (16*1024*1024), else 0", src, {"bytes": b, "limit": C.PAGE_LIMIT})]


def s_artifact_check(page, out, no_shots, skip):
    name, method = "artifact_check", "node artifact_check.mjs <page> <out>/shots: exit 0 -> 1, exit 1 -> 0, exit 2 (bad usage, no Playwright) -> not run"
    src = "<system-design skill>/scripts/artifact_check.mjs"
    if name in skip:
        return C.not_run(name, "skipped (--skip)", method, src)
    if not os.path.isfile(page):
        return C.not_run(name, "no build/tdd.html", method, src)
    tool, node = artifact_check_path(), C.which("node")
    if not tool or not node:
        return C.not_run(name, "artifact_check.mjs absent" if not tool else "node not on PATH", method, src)
    shots = os.path.join(out, "shots")
    os.makedirs(shots, exist_ok=True)
    r = C.run([node, tool, page, shots] + (["--no-shots"] if no_shots else []), timeout=600)
    report = None
    reps = sorted(glob.glob(os.path.join(shots, "*-report.json")), key=os.path.getmtime)
    if reps:
        report = C.read_json(reps[-1])
    m = {"exit": r["code"], "seconds": r["seconds"]}
    if report:
        m.update({"renders": len(report.get("renders", [])), "failures": len(report.get("failures", [])), "warnings": len(report.get("warnings", []))})
    detail = {"tail": C.tail(r["out"] + "\n" + r["err"], 14), "report": os.path.relpath(reps[-1], out) if reps else None,
              "failures": (report or {}).get("failures", [])[:20], "shots": sorted(os.path.relpath(p, out) for p in glob.glob(os.path.join(shots, "*.png")))}
    if r["code"] == 0:
        return C.score(name, 1, method, src, m, detail)
    if r["code"] == 1:
        return C.score(name, 0, method, src, m, detail)
    return C.not_run(name, C.one_line(r["err"] or "exit 2", 160) or "exit 2", method, src, m, detail)


def s_hydrate(page, skip):
    name, method = "check_hydrate", "bun run app/check_hydrate.ts <page> (jsdom hydrate + interactions): exit 0 -> 1, exit 1 -> 0, exit 2 -> not run"
    src = "app/check_hydrate.ts"
    if name in skip:
        return C.not_run(name, "skipped (--skip)", method, src)
    tool, app = os.path.join(C.SKILL, "app", "check_hydrate.ts"), os.path.join(C.SKILL, "app")
    if not os.path.isfile(page):
        return C.not_run(name, "no build/tdd.html", method, src)
    if not os.path.isfile(tool):
        return C.not_run(name, "app/check_hydrate.ts absent", method, src)
    bun = C.which("bun")
    if not bun:
        return C.not_run(name, "bun not on PATH", method, src)
    if not os.path.isdir(os.path.join(app, "node_modules")):
        return C.not_run(name, "app/node_modules absent (bun install)", method, src)
    r = C.run([bun, "run", tool, page], cwd=app, timeout=300)
    m = {"exit": r["code"], "seconds": r["seconds"]}
    hit = HYDRATE_LINE.search(r["out"])
    if hit:
        m.update(dict(zip(("console_errors", "console_warnings", "hydration_warnings", "failed_steps"), map(int, hit.groups()))))
    detail = {"tail": C.tail(r["out"] + "\n" + r["err"], 12)}
    if r["code"] == 0:
        return C.score(name, 1, method, src, m, detail)
    if r["code"] == 1:
        return C.score(name, 0, method, src, m, detail)
    return C.not_run(name, C.one_line(r["err"], 160) or f"exit {r['code']}", method, src, m, detail)


def s_lint(packet, build, out, skip):
    name = "lint"
    method = ("python3 -I scripts/lint_packet.py <packet> --build <build> --json <out>/lint.json; value = rules with zero "
              "error-severity findings / rules that reported (STRUCT, DNUM, REFS, CITES, DIAG, SCHEMA, FILES, META, AGENTS, XDOC)")
    src = "scripts/lint_packet.py"
    if name in skip:
        return C.not_run(name, "skipped (--skip)", method, src)
    tool = os.path.join(C.SCRIPTS, "lint_packet.py")
    if not os.path.isfile(tool):
        return C.not_run(name, "scripts/lint_packet.py absent", method, src)
    jpath = os.path.join(out, "lint.json")
    if os.path.exists(jpath):
        os.remove(jpath)
    r = C.run([sys.executable, "-I", tool, packet, "--build", build, "--json", jpath], timeout=600)
    data = C.read_json(jpath)
    if not data:
        return C.not_run(name, f"no JSON written (exit {r['code']}): {C.one_line(r['err'] or r['out'], 160)}", method, src, {"exit": r["code"]})
    findings = data.get("findings", [])
    rules = sorted({f["rule"] for f in findings} | set((data.get("counts") or {}).get("by_rule", {}).keys()))
    by_rule = {}
    for rl in rules:
        fs = [f for f in findings if f["rule"] == rl]
        by_rule[rl] = {"errors": sum(1 for f in fs if f["severity"] == "error"), "warnings": sum(1 for f in fs if f["severity"] != "error")}
    clean = sum(1 for v in by_rule.values() if v["errors"] == 0)
    m = {"exit": r["code"], "errors": (data.get("counts") or {}).get("errors"), "warnings": (data.get("counts") or {}).get("warnings"),
         "rules": len(by_rule), "rules_clean": clean, "seconds": r["seconds"]}
    for rl, v in by_rule.items():
        m[f"{rl}.errors"], m[f"{rl}.warnings"] = v["errors"], v["warnings"]
    detail = {"by_rule": by_rule, "errors": [f"{f['check']} {f.get('file') or ''}:{f.get('line') or ''} {f['message']}"[:240] for f in findings if f["severity"] == "error"][:40],
              "notes": data.get("notes", [])[:20], "json": "lint.json"}
    return C.score(name, C.ratio(clean, len(by_rule)) if by_rule else 1, method, src, m, detail)


def unlinked_occurrences(content, tokens):
    counts = {t: 0 for t in tokens}
    if not tokens:
        return counts
    want = set(tokens)
    def visit(node):
        if isinstance(node, dict):
            for k, v in node.items():
                if k in COMPILER_LINK_KEYS and isinstance(v, str) and v:
                    txt = re.sub(r"(?s)<a\b[^>]*>.*?</a>|<code\b[^>]*>.*?</code>", " ", v)
                    for m in C.TOKEN.finditer(C.strip_tags(txt)):
                        t = re.sub(r"\s+", " ", m.group(1))
                        if t in want:
                            counts[t] += 1
                elif isinstance(v, (dict, list)) and k != "text":
                    visit(v)
        elif isinstance(node, list):
            for x in node:
                visit(x)
    visit(content.get("sections", []))
    visit(content.get("lede", []))
    return counts


def s_refs(content):
    refs = content.get("refs") or {}
    linked, unlinked = int(refs.get("linked") or 0), list(refs.get("unlinked") or [])
    occ = unlinked_occurrences(content, unlinked)
    n_un = sum(occ.values()) or len(unlinked)
    method = ("linked / (linked + unlinked occurrences); linked = content.refs.linked (links the compiler wrote); unlinked occurrences = "
              "reference tokens listed in content.refs.unlinked counted in the compiler's linkable HTML strings outside <a> and <code> "
              "(falls back to the distinct count when none is found)")
    return C.score("refs_linked", C.ratio(linked, linked + n_un) if (linked + n_un) else 1, method, "content.json refs + sections",
                   {"linked": linked, "unlinked_tokens": len(unlinked), "unlinked_occurrences": n_un}, {"unlinked": occ})


def s_cites(content):
    cites = content.get("cites") or []
    href = sum(1 for c in cites if c.get("href"))
    no_line = sum(1 for c in cites if not c.get("from"))
    with_line = len(cites) - no_line
    return C.score("cites_href", C.ratio(href, len(cites)) if cites else 1,
                   "citations with an href (repo_url/blob/sha/path#Lx) / all citations; a citation without a line range gets no href by design (metrics split it out)",
                   "content.json cites", {"total": len(cites), "with_href": href, "without_href": len(cites) - href, "without_line": no_line,
                                          "line_bearing_with_href": href, "line_bearing": with_line})


def house_slots():
    mod, _ = C.load_sibling_script("lint_packet.py", "tdd_eval_lint_packet")
    slots = getattr(getattr(mod, "Lint", None), "SLOTS", None) if mod else None
    return (slots, "lint_packet.Lint.SLOTS") if slots else (HOUSE_SLOTS, "ev_artifact.HOUSE_SLOTS (mirror of lint_packet.Lint.SLOTS)")


def s_sections(content):
    slots, origin = house_slots()
    secs, used, hits = content.get("sections", []), set(), []
    for name, ids, rx in slots:
        hit = next((s for s in secs if s["id"] not in used and s["id"] in ids), None) or \
              next((s for s in secs if s["id"] not in used and re.search(rx, (s.get("titleText") or "").lower())), None)
        if hit:
            used.add(hit["id"])
        hits.append({"slot": name, "section": hit["id"] if hit else None})
    present = sum(1 for h in hits if h["section"])
    return C.score("sections_house", C.ratio(present, len(slots)), f"house sections matched by id or title regex / {len(slots)} (slots from {origin})",
                   "content.json sections", {"present": present, "required": len(slots), "sections": len(secs)}, hits)


def s_decisions(content):
    ds = content.get("decisions") or []
    with_status = sum(1 for d in ds if (d.get("status") or "").strip())
    states = {}
    for d in ds:
        states[d.get("state") or "none"] = states.get(d.get("state") or "none", 0) + 1
    m = {"total": len(ds), "with_status": with_status}
    m.update({f"state_{k}": v for k, v in states.items()})
    if not ds:
        return C.not_run("decisions_status", "no decisions in content.json", "decisions with a non-empty status / decisions", "content.json decisions", m)
    return C.score("decisions_status", C.ratio(with_status, len(ds)), "decisions with a non-empty status / decisions (states counted as metrics)", "content.json decisions", m)


def s_diagrams(content):
    ds = list(C.walk(content.get("sections", []), {"diagram"}))
    if not ds:
        return C.not_run("diagram_hotspots", "no diagrams", "diagrams with >= 1 hotspot / diagrams", "content.json diagram nodes")
    annotated = [d for d in ds if d.get("hotspots")]
    hs = [h for d in ds for h in d.get("hotspots", [])]
    return C.score("diagram_hotspots", C.ratio(len(annotated), len(ds)), "diagrams with >= 1 hotspot / diagrams", "content.json diagram nodes",
                   {"diagrams": len(ds), "annotated": len(annotated), "hotspots": len(hs), "with_table": sum(1 for h in hs if h.get("table")),
                    "with_note": sum(1 for h in hs if h.get("note")), "with_image": sum(1 for h in hs if h.get("image"))},
                   [{"key": d.get("key"), "hotspots": len(d.get("hotspots", []))} for d in ds])


def ddl_tables(sql):
    return {m.group(1).strip('"').split(".")[-1].lower() for m in DDL_NAME.finditer(sql or "")}


def s_schema(content):
    tables = content.get("schema") or []
    fence_names = set()
    for f in C.sql_fences(content):
        fence_names |= ddl_tables(f.get("text"))
    ddl_names = set(fence_names)
    for t in tables:
        ddl_names |= ddl_tables(t.get("ddl"))
    have = {(t.get("table") or "").lower() for t in tables}
    method = "tables named in DDL (CREATE/ALTER TABLE in sql fences and schema[].ddl) that have a schema card / tables named in DDL"
    m = {"schema_tables": len(tables), "ddl_tables": len(ddl_names), "covered": len(ddl_names & have), "fence_ddl_tables": len(fence_names),
         "new": sum(1 for t in tables if t.get("state") == "new"), "changed": sum(1 for t in tables if t.get("state") == "changed"),
         "existing": sum(1 for t in tables if t.get("state") == "existing")}
    if not ddl_names:
        return C.not_run("schema_ddl", "no DDL on the page", method, "content.json schema + sql fences", m)
    return C.score("schema_ddl", C.ratio(len(ddl_names & have), len(ddl_names)), method, "content.json schema + sql fences", m,
                   {"uncovered": sorted(ddl_names - have)})


def s_reading(content, inp):
    st = content.get("stats") or {}
    m = {"reading_minutes": st.get("readingMinutes"), "words": st.get("words"), "sections": st.get("sections"), "diagrams": st.get("diagrams"),
         "prs": st.get("prs"), "decisions": st.get("decisions")}
    if inp.get("source"):
        m["source_words"] = inp["source"]["words"]
    return C.metric("reading", {k: v for k, v in m.items() if v is not None}, "content.json stats (words / 230 per minute, as build_tdd computes it)", "content.json stats")


def s_owner_review(inp, target):
    notes = [r for r in inp["labels"]["review_notes"] if r["target"] == target]
    method = "owner review notes for this target: pass / (pass + fail + needs-work); 'question' notes are counted, not scored"
    src = inp["labels"].get("review_notes_source") or "labels.jsonl"
    if not notes:
        return C.not_run("owner_review", "no review notes for this target (--labels)", method, src)
    p = sum(1 for r in notes if r["verdict"] == "pass")
    f = sum(1 for r in notes if r["verdict"] in ("fail", "needs-work"))
    q = sum(1 for r in notes if r["verdict"] == "question")
    m = {"notes": len(notes), "pass": p, "fail_or_needs_work": f, "question": q}
    if p + f == 0:
        return C.not_run("owner_review", "only question notes", method, src, m)
    return C.score("owner_review", C.ratio(p, p + f), method, src, m, [{k: r.get(k) for k in ("anchor", "verdict", "note", "date")} for r in notes])


def evaluate(ctx):
    page, content, inp = ctx["page"], ctx["content"], ctx["input"]
    scores = s_page(page)
    scores.append(s_artifact_check(page, ctx["out"], ctx["no_shots"], ctx["skip"]))
    scores.append(s_hydrate(page, ctx["skip"]))
    scores.append(s_lint(ctx["packet"], ctx["build"], ctx["out"], ctx["skip"]))
    if content:
        scores += [s_refs(content), s_cites(content), s_sections(content), s_decisions(content), s_diagrams(content), s_schema(content), s_reading(content, inp)]
    else:
        for n in ("refs_linked", "cites_href", "sections_house", "decisions_status", "diagram_hotspots", "schema_ddl"):
            scores.append(C.not_run(n, "no build/content.json", "see references/eval-loop.md", "content.json"))
    scores += ctx["judge"](content)
    scores.append(s_owner_review(inp, "artifact"))
    return C.output("artifact", inp["packet"]["key"], scores)
