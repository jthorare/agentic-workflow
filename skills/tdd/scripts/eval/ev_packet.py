"""OUTPUT 2, the packet for implementation and agent handoff (build/agents/ as write_agents.py writes it, plus the
packet files): lane present and current, budgets, anchors sliced once, SQL extracted, map edges resolve, images
indexed, PRs copied, schema.md present, and the handoff probe (deterministic answerability; a model probe with a key)."""
import glob, os, re
import ev_common as C

README_MAX = 8192            # write_agents.README_MAX; replaced by the writer's constant when it imports
SLICE_BUDGET = 8000          # write_agents.BUDGET (tokens as the writer counts them: ceil(chars / 4))
EXEMPT = {"tdd.md", "tdd.txt", "map.json", "refs.json", "citations.json", "tokens.json", "images/index.json",
          "page/plain.html", "page/outline.json", "decisions/decisions.json", "prs/prs.json"}   # write_agents.EXEMPT mirror
TEXT_EXT = (".md", ".txt", ".json", ".sql", ".html", ".ts", ".tsx", ".js", ".py", ".sh", ".yaml", ".yml", ".css")
FM_ANCHORS = re.compile(r"^anchors:\s*\[(.*)\]\s*$", re.M)
FM_ITEM = re.compile(r'"((?:[^"\\]|\\.)*)"|([^,\s][^,]*)')


def writer():
    mod, why = C.load_sibling_script("write_agents.py", "tdd_eval_write_agents")
    return mod, why


def lane_files(agents):
    out = []
    for dp, dn, fn in os.walk(agents):
        dn[:] = sorted(dn)
        for f in sorted(fn):
            p = os.path.join(dp, f)
            out.append(os.path.relpath(p, agents).replace(os.sep, "/"))
    return out


def front_anchors(text):
    head = text.split("\n---", 1)[0] if text.startswith("---") else ""
    m = FM_ANCHORS.search(head)
    if not m:
        return []
    items = []
    for q, plain in FM_ITEM.findall(m.group(1)):
        items.append(q.encode().decode("unicode_escape") if q else plain.strip())
    return [x for x in items if x]


def s_present(agents):
    readme = os.path.join(agents, "README.md")
    ok = os.path.isfile(readme)
    return C.score("agents_present", 1 if ok else 0, "1 when build/agents/README.md exists", "build/agents",
                   {"files": len(lane_files(agents)) if os.path.isdir(agents) else 0})


def s_current(build, agents, mod, why):
    method = "write_agents.check(build_dir) -> [] means the lane matches content.json and tdd.html: 1, else 0 (problems listed)"
    if not os.path.isfile(os.path.join(agents, "README.md")):
        return C.not_run("agents_current", "no build/agents/README.md", method, "scripts/write_agents.py")
    if not mod or not hasattr(mod, "check"):
        return C.not_run("agents_current", why or "write_agents.check is absent", method, "scripts/write_agents.py")
    try:
        problems = list(mod.check(build))
    except Exception as e:
        return C.error("agents_current", f"check raised {type(e).__name__}: {str(e)[:160]}", method, "scripts/write_agents.py")
    return C.score("agents_current", 0 if problems else 1, method, "scripts/write_agents.py", {"problems": len(problems)}, problems[:20])


def s_readme(agents, limit):
    p = os.path.join(agents, "README.md")
    method = f"1 when agents/README.md bytes <= {limit} (8 KB, the writer's front-door limit)"
    if not os.path.isfile(p):
        return C.not_run("readme_bytes", "no agents/README.md", method, "build/agents/README.md")
    b = os.path.getsize(p)
    return C.score("readme_bytes", 1 if b <= limit else 0, method, "build/agents/README.md", {"bytes": b, "limit": limit})


def s_largest(agents, budget, exempt, files):
    method = (f"largest non-exempt text file in agents/ (tokens as the writer counts them: ceil(chars/4), from tokens.json when present) "
              f"<= {budget}: 1, else 0; exempt files (whole-document and index files) are listed by write_agents.EXEMPT")
    if not files:
        return C.not_run("largest_slice", "no agents/ lane", method, "build/agents")
    tj = C.read_json(os.path.join(agents, "tokens.json")) or {}
    per = {r.get("path") or r.get("file"): r.get("tokens") for r in (tj.get("files") or []) if isinstance(r, dict)}
    best, rows = None, []
    for f in files:
        if f in exempt or not f.endswith(TEXT_EXT):
            continue
        text = C.read_text(os.path.join(agents, f)) or ""
        t4 = per.get(f) if isinstance(per.get(f), int) else -(-len(text) // 4)
        rows.append((t4, f, C.tokens_est(text)))
    if not rows:
        return C.not_run("largest_slice", "no non-exempt text files", method, "build/agents")
    rows.sort(reverse=True)
    t4, f, t36 = rows[0]
    over = [r[1] for r in rows if r[0] > budget]
    return C.score("largest_slice", 1 if t4 <= budget else 0, method, "build/agents + tokens.json",
                   {"largest_tokens": t4, "largest_tokens_chars_per_3_6": t36, "budget": budget, "slices": len(rows), "over_budget": len(over)},
                   {"largest": f, "over_budget": over[:20], "top5": [{"file": r[1], "tokens": r[0]} for r in rows[:5]]})


def s_anchors(agents, content, files):
    method = ("content.json anchors that appear in exactly one slice / all anchors; slices = sections/*.md front matter 'anchors:' lists, "
              "plus refs.json 'file' for anchors no section slice lists (tables live in schema/schema.md)")
    if not files:
        return C.not_run("anchors_sliced_once", "no agents/ lane", method, "build/agents/sections")
    anchors = [a["id"] for a in content.get("anchors") or []]
    count = {a: 0 for a in anchors}
    where = {a: [] for a in anchors}
    for f in files:
        if f.startswith("sections/") and f.endswith(".md"):
            for a in front_anchors(C.read_text(os.path.join(agents, f)) or ""):
                if a in count:
                    count[a] += 1
                    where[a].append(f)
    refs = C.read_json(os.path.join(agents, "refs.json")) or {}
    for r in refs.get("anchors") or []:
        a, f = r.get("id"), r.get("file")
        if a in count and count[a] == 0 and f and not f.startswith("sections/") and os.path.isfile(os.path.join(agents, f)):
            count[a], where[a] = 1, [f]
    once = sum(1 for a in anchors if count[a] == 1)
    dup = sorted(a for a in anchors if count[a] > 1)
    missing = sorted(a for a in anchors if count[a] == 0)
    if not anchors:
        return C.not_run("anchors_sliced_once", "content.json has no anchors", method, "content.json anchors")
    return C.score("anchors_sliced_once", C.ratio(once, len(anchors)), method, "build/agents/sections + refs.json",
                   {"anchors": len(anchors), "once": once, "duplicated": len(dup), "missing": len(missing)},
                   {"duplicated": {a: where[a] for a in dup[:20]}, "missing": missing[:30]})


def squash(s):
    return re.sub(r"\s+", " ", (s or "").strip())


def s_sql(agents, content, files):
    fences = C.sql_fences(content)
    method = "sql fences in content.json whose whitespace-normalised text is found in agents/sql/*.sql / sql fences"
    if not fences:
        return C.not_run("sql_extracted", "no sql fences in content.json", method, "build/agents/sql")
    sqlf = [f for f in files if f.startswith("sql/") and f.endswith(".sql")]
    if not files:
        return C.not_run("sql_extracted", "no agents/ lane", method, "build/agents/sql", {"fences": len(fences)})
    blob = squash("\n".join(C.read_text(os.path.join(agents, f)) or "" for f in sqlf))
    found = [i for i, f in enumerate(fences) if squash(f.get("text")) and squash(f.get("text")) in blob]
    return C.score("sql_extracted", C.ratio(len(found), len(fences)), method, "build/agents/sql",
                   {"fences": len(fences), "extracted": len(found), "sql_files": len(sqlf)},
                   {"missing_fences": [{"section": fences[i].get("section"), "first_line": (fences[i].get("text") or "").splitlines()[0][:100] if fences[i].get("text") else ""}
                                       for i in range(len(fences)) if i not in found]})


def s_map(agents):
    p = os.path.join(agents, "map.json")
    method = "map.json edges whose from and to are both entity ids / edges"
    m = C.read_json(p)
    if not m:
        return C.not_run("map_edges_resolve", "no agents/map.json", method, "build/agents/map.json")
    ids = {e.get("id") for e in m.get("entities") or []}
    edges = m.get("edges") or []
    ok = [e for e in edges if e.get("from") in ids and e.get("to") in ids]
    bad = [e for e in edges if not (e.get("from") in ids and e.get("to") in ids)]
    if not edges:
        return C.not_run("map_edges_resolve", "map.json has no edges", method, "build/agents/map.json", {"entities": len(ids)})
    rels = {}
    for e in edges:
        rels[e.get("rel")] = rels.get(e.get("rel"), 0) + 1
    return C.score("map_edges_resolve", C.ratio(len(ok), len(edges)), method, "build/agents/map.json",
                   {"entities": len(ids), "edges": len(edges), "resolved": len(ok), "dangling": len(bad)},
                   {"rels": rels, "dangling": bad[:10]})


def s_images(agents, build):
    man = C.read_json(os.path.join(build, "content.files.json")) or {}
    imgs = {p for p in (man.get("files") or {}) if p.lower().endswith(C.IMAGE_EXT)}
    method = "images in content.files.json that have a row in agents/images/index.json (matched on the row's published 'source' path) / images"
    if not imgs:
        return C.not_run("images_indexed", "content.files.json lists no images", method, "build/agents/images/index.json")
    idx = C.read_json(os.path.join(agents, "images", "index.json"))
    if not idx:
        return C.not_run("images_indexed", "no agents/images/index.json", method, "build/agents/images/index.json", {"images": len(imgs)})
    rows = idx.get("images") or []
    have = {r.get("source") for r in rows} | {r.get("published") for r in rows}
    hit = imgs & have
    return C.score("images_indexed", C.ratio(len(hit), len(imgs)), method, "build/agents/images/index.json",
                   {"images": len(imgs), "indexed": len(hit), "rows": len(rows), "with_caption": sum(1 for r in rows if r.get("caption")),
                    "with_alt": sum(1 for r in rows if r.get("alt"))}, {"unindexed": sorted(imgs - have)[:20]})


def s_prs(agents, content, files):
    prs = [p["n"] for p in C.prs_of(content) if p.get("n") is not None]
    method = "PR numbers in content.json whose prs/pr-NN.md is in agents/prs/ / PR numbers"
    if not prs:
        return C.not_run("prs_copied", "no PR plan in content.json", method, "build/agents/prs")
    if not files:
        return C.not_run("prs_copied", "no agents/ lane", method, "build/agents/prs", {"prs": len(prs)})
    have = {int(m.group(1)) for f in files for m in [re.match(r"prs/pr-0*(\d+)\b.*\.md$", f)] if m}
    hit = [n for n in prs if n in have]
    return C.score("prs_copied", C.ratio(len(hit), len(prs)), method, "build/agents/prs", {"prs": len(prs), "copied": len(hit)},
                   {"missing": [n for n in prs if n not in have]})


def s_schema_md(agents, content):
    method = "1 when agents/schema/schema.md (or agents/schema.md) exists and names every schema table, else 0"
    tables = [t.get("table") for t in content.get("schema") or []]
    if not tables:
        return C.not_run("schema_md", "no schema tables in content.json", method, "build/agents/schema/schema.md")
    p = next((x for x in (os.path.join(agents, "schema", "schema.md"), os.path.join(agents, "schema.md")) if os.path.isfile(x)), None)
    if not p:
        return C.score("schema_md", 0, method, "build/agents/schema/schema.md", {"tables": len(tables), "named": 0})
    text = C.read_text(p) or ""
    named = [t for t in tables if t and t in text]
    return C.score("schema_md", 1 if len(named) == len(tables) else 0, method, os.path.relpath(p, os.path.dirname(agents)),
                   {"tables": len(tables), "named": len(named), "bytes": os.path.getsize(p)}, {"unnamed": [t for t in tables if t not in named]})


def s_lane_size(agents, files):
    if not files:
        return C.not_run("lane_size", "no agents/ lane", "sum of bytes and chars/3.6 token estimate over agents/ text files", "build/agents")
    b = t = n = 0
    for f in files:
        p = os.path.join(agents, f)
        b += os.path.getsize(p)
        if f.endswith(TEXT_EXT):
            n += 1
            t += C.tokens_est(os.path.getsize(p))
    return C.metric("lane_size", {"files": len(files), "text_files": n, "bytes": b, "tokens_est": t},
                    "bytes of every file under agents/; tokens_est = text bytes / 3.6 (an estimate, not a tokenizer)", "build/agents")


def evaluate(ctx, probe_scores):
    build, content, inp = ctx["build"], ctx["content"] or {}, ctx["input"]
    agents = os.path.join(build, "agents")
    mod, why = writer()
    files = lane_files(agents) if os.path.isdir(agents) else []
    limit = getattr(mod, "README_MAX", README_MAX) if mod else README_MAX
    budget = getattr(mod, "BUDGET", SLICE_BUDGET) if mod else SLICE_BUDGET
    exempt = set(getattr(mod, "EXEMPT", EXEMPT)) if mod else EXEMPT
    scores = [s_present(agents), s_current(build, agents, mod, why), s_readme(agents, limit), s_largest(agents, budget, exempt, files)]
    if content:
        scores += [s_anchors(agents, content, files), s_sql(agents, content, files)]
    scores += [s_map(agents), s_images(agents, build)]
    if content:
        scores += [s_prs(agents, content, files), s_schema_md(agents, content)]
    scores.append(s_lane_size(agents, files))
    scores += probe_scores
    scores.append(ctx["owner_review"](inp, "packet"))
    return C.output("packet", inp["packet"]["key"], scores, extra={"writer": None if mod else why})
