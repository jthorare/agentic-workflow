"""The handoff probe. A deterministic extractor turns templates/eval/probe-questions.json into concrete questions with
expected answers from content.json; a loader reads the agents/ lane the way an implementing agent would (README first,
then the files it links, within a token budget); two scorers use them:
  probe_answerable  (no model) the share of questions whose evidence and expected answer sit together in one loaded file
  probe_agent       (--with-agent-probe and a key) a model given ONLY the loaded lane answers; exact/contains match"""
import os, re
import ev_common as C
import ev_llm as L

LINK = re.compile(r"\[([^\]]*)\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
SKIP_EXT = C.IMAGE_EXT + (".mp4", ".webm", ".mov", ".pdf")
SKIP_FILES = {"llms-full.txt"}


def idkey(i):
    m = re.match(r"([A-Za-z]+)-?(\d+)", i or "")
    return (m.group(1).upper(), int(m.group(2))) if m else (i or "", 0)


def first_clause(text, n=110):
    t = " ".join((text or "").split())
    t = re.split(r"(?<=[.;:])\s", t)[0].rstrip(".;:")
    return t[:n]


CONSTRAINT = re.compile(r"\s+(PRIMARY|NOT|NULL|DEFAULT|REFERENCES|UNIQUE|CHECK|GENERATED|COLLATE|CONSTRAINT)\b.*$", re.I | re.S)


def base_type(t):
    """'uuid PRIMARY KEY DEFAULT gen_random_uuid()' -> 'uuid'; the column's SQL type without its constraints."""
    return CONSTRAINT.sub("", (t or "").strip()).strip()


def build_questions(content, template):
    kinds = {k["kind"]: k for k in template.get("kinds", [])}
    qs = []
    def add(kind, fields, expected, evidence, accept=None):
        k = kinds[kind]
        q = {"id": f"{kind}-{sum(1 for x in qs if x['kind'] == kind) + 1}", "kind": kind, "question": k["template"].format(**fields),
             "expected": expected, "match": k["match"], "evidence": [e for e in evidence if e]}
        if accept:
            q["accept"] = accept
        qs.append(q)
    decisions = sorted(content.get("decisions") or [], key=lambda d: idkey(d.get("id")))
    schema = content.get("schema") or []
    prs = C.prs_of(content)
    if "decision-by-question" in kinds:
        for d in decisions[:kinds["decision-by-question"]["limit"]]:
            add("decision-by-question", {"question": d.get("question")}, d.get("id"), [(d.get("question") or "")[:48]])
    if "decision-state" in kinds:
        acc = kinds["decision-state"].get("accept", {})
        pick = decisions[::-1][::2][:kinds["decision-state"]["limit"]]
        for d in pick:
            exp = {"done": "decided", "held": "held"}.get(d.get("state"), "open")
            add("decision-state", {"id": d.get("id")}, exp, [d.get("id")], acc)
    for kind, state in (("new-tables", "new"), ("changed-tables", "changed")):
        names = [t.get("table") for t in schema if t.get("state") == state]
        if kind in kinds and names:
            add(kind, {}, names, names)
    if "pr-introduces-table" in kinds:
        n = 0
        for t in schema:
            if t.get("state") != "new" or n >= kinds["pr-introduces-table"]["limit"]:
                continue
            name = t.get("table")
            hit = next((p for p in sorted(prs, key=lambda p: p["n"] or 0) if name and re.search(r"\b" + re.escape(name) + r"\b", " ".join((p["title"], p["change"], p["body"])))), None)
            if hit:
                add("pr-introduces-table", {"table": name}, f"PR {hit['n']}", [name])
                n += 1
    if "pr-by-title" in kinds and prs:
        idx = sorted({0, len(prs) // 2, len(prs) - 1})[:kinds["pr-by-title"]["limit"]]
        for i in idx:
            add("pr-by-title", {"title": prs[i]["title"]}, f"PR {prs[i]['n']}", [prs[i]["title"][:60]])
    if "pr-count" in kinds and prs:
        add("pr-count", {}, str(len(prs)), [])
    if "stat-card" in kinds:
        n = 0
        for node in C.walk(content.get("sections", []), {"cards"}):
            if not node.get("stat"):
                continue
            for c in node.get("cards", []):
                if n >= kinds["stat-card"]["limit"]:
                    break
                fig = C.strip_tags(c.get("title"))
                body = next((C.strip_tags(b.get("html")) for b in c.get("blocks", []) if b.get("html")), "")
                if fig and re.search(r"\d", fig) and body:
                    what = first_clause(body)
                    add("stat-card", {"what": what}, fig, [what[:40]])
                    n += 1
    if "column-type" in kinds:
        n = 0
        for t in schema:
            if t.get("state") != "new" or n >= kinds["column-type"]["limit"]:
                continue
            cols = [c for c in t.get("columns", []) if c.get("change") == "new" and base_type(c.get("type"))]
            col = next((c for c in cols if not re.search(r"(^|_)id$", c.get("name") or "")), cols[0] if cols else None)
            if col:
                add("column-type", {"table": t.get("table"), "column": col["name"]}, base_type(col["type"]), [col["name"]])
                n += 1
    if "measured" in kinds:
        n = 0
        for t in schema:
            for key, mv in (t.get("measured") or {}).items():
                if n >= kinds["measured"]["limit"]:
                    break
                if isinstance(mv, dict) and re.search(r"\d", str(mv.get("value", ""))):
                    add("measured", {"label": mv.get("label") or key, "table": t.get("table")}, str(mv["value"]), [t.get("table")])
                    n += 1
    sha = (content.get("meta") or {}).get("sha")
    if "base-sha" in kinds and sha:
        add("base-sha", {}, sha, [])
    return qs


# ----------------------------------------------------------------------------- matching

def ntok(s):
    s = (s or "").lower()
    s = re.sub(r"\bpr[\s\-#]*(\d+)", r"pr\1", s)
    return re.sub(r"\s+", " ", s)


def numbers(s):
    return set(x.replace(",", "") for x in re.findall(r"\d[\d,]*(?:\.\d+)?", s or ""))


def matches(q, answer):
    a, e, kind = answer or "", q["expected"], q["match"]
    if kind == "token":
        return re.search(r"(?<![a-z0-9])" + re.escape(ntok(e)) + r"(?![a-z0-9])", ntok(a)) is not None
    if kind == "oneof":
        acc = q.get("accept") or {}
        low = a.lower()
        mine = any(re.search(r"\b" + re.escape(w) + r"\b", low) for w in acc.get(e, [e]))
        other = any(re.search(r"\b" + re.escape(w) + r"\b", low) for k, ws in acc.items() if k != e for w in ws)
        return mine and not other
    if kind == "all":
        return all(str(x).lower() in a.lower() for x in e)
    if kind == "numbers":
        want = numbers(e)
        return bool(want) and want <= numbers(a)
    if kind == "contains":
        return re.sub(r"\s+", "", str(e).lower()) in re.sub(r"\s+", "", a.lower())
    if kind == "prefix":
        return str(e)[:7].lower() in a.lower()
    return False


# ----------------------------------------------------------------------------- the handoff loader

def readme_links(text):
    out, optional = [], False
    for line in text.splitlines():
        h = re.match(r"^#{1,3}\s+(.*)", line)
        if h:
            optional = bool(re.search(r"\boptional\b", h.group(1), re.I))
            continue
        if optional:
            continue
        out += [m.group(2) for m in LINK.finditer(line)]
    return out


def load_lane(agents, budget):
    """Simulate an agent reading the lane: README.md, then its links in order (skipping '## Optional', images and
    external URLs), then the links inside those files, breadth first, each file taken whole when it fits the budget."""
    readme = os.path.join(agents, "README.md")
    if not os.path.isfile(readme):
        return None
    docs, seen, skipped = [], set(), []
    used = 0
    queue = [("README.md", 0)]
    while queue:
        rel, depth = queue.pop(0)
        rel = os.path.normpath(rel).replace(os.sep, "/")
        if rel in seen:
            continue
        seen.add(rel)
        p = os.path.join(agents, rel)
        if not os.path.isfile(p):
            skipped.append({"path": rel, "why": "missing"})
            continue
        text = C.read_text(p) or ""
        t = C.tokens_est(text)
        if used + t > budget:
            skipped.append({"path": rel, "why": f"over budget ({t} tokens, {budget - used} left)"})
            continue
        used += t
        docs.append({"path": rel, "tokens": t, "text": text})
        if depth >= 2:
            continue
        links = readme_links(text) if rel == "README.md" else [m.group(2) for m in LINK.finditer(text)]
        base = os.path.dirname(rel)
        for href in links:
            if re.match(r"^[a-z]+:", href) or href.startswith("#"):
                continue
            target = href.split("#", 1)[0]
            if not target or target.lower().endswith(SKIP_EXT) or os.path.basename(target) in SKIP_FILES:
                continue
            queue.append((os.path.join(base, target), depth + 1))
    return {"docs": docs, "tokens": used, "budget": budget, "skipped": skipped}


WINDOW = 600


def has_token(tok, text):
    return re.search(r"(?<![a-z0-9])" + re.escape(ntok(tok)) + r"(?![a-z0-9])", ntok(text)) is not None


def answerable(q, docs):
    """Where the answer sits next to its evidence in one loaded file: on the same line for 'oneof' (an id and its state),
    anywhere in the file for 'all' (a list), within WINDOW characters of the first evidence string otherwise; a 'PR n'
    answer also counts when the evidence line is a table row whose cell or link names PR n (| [6](pr-06.md) | title |)."""
    ev = q["evidence"]
    for d in docs:
        text = d["text"]
        if q["match"] == "all" or not ev:
            if all(e.lower() in text.lower() for e in ev) and matches(q, text):
                return d["path"]
            continue
        if q["match"] == "oneof":
            if any(has_token(ev[0], line) and matches(q, line) for line in text.splitlines()):
                return d["path"]
            continue
        pr = re.fullmatch(r"PR (\d+)", str(q["expected"])) if q["match"] == "token" else None
        if pr:
            cell = re.compile(r"(^|\|)\s*\[?0*" + pr.group(1) + r"\]?(\(|\s*\|)|\bpr-0*" + pr.group(1) + r"\.md\b", re.I)
            if any(all(e.lower() in line.lower() for e in ev) and cell.search(line) for line in text.splitlines()):
                return d["path"]
        low, needle = text.lower(), ev[0].lower()
        start = low.find(needle)
        while start >= 0:
            win = text[max(0, start - WINDOW): start + len(needle) + WINDOW]
            if all(e.lower() in win.lower() for e in ev[1:]) and matches(q, win):
                return d["path"]
            start = low.find(needle, start + 1)
    return None


SYSTEM = ("You are an implementing agent who has been handed a design packet. Answer each question using only the documents "
          "provided. If the documents do not contain the answer, answer \"unknown\". Keep each answer short.")
SCHEMA = {"type": "object", "additionalProperties": False, "required": ["answers"],
          "properties": {"answers": {"type": "array", "items": {"type": "object", "additionalProperties": False, "required": ["id", "answer"],
                                                                 "properties": {"id": {"type": "string"}, "answer": {"type": "string"}}}}}}


def run(ctx, template_path):
    """Returns (scores, probe_record). probe_record is written to build/eval/probe.json."""
    content, build = ctx["content"] or {}, ctx["build"]
    template = C.read_json(template_path) or {}
    budget = int(template.get("budget_tokens") or 20000)
    qs = build_questions(content, template) if content else []
    agents = os.path.join(build, "agents")
    lane = load_lane(agents, budget)
    rec = {"schema": "tdd-eval-probe-run/1", "template": template_path, "budget_tokens": budget, "questions": qs,
           "lane": {k: v for k, v in (lane or {}).items() if k != "docs"} | ({"docs": [{"path": d["path"], "tokens": d["tokens"]} for d in lane["docs"]]} if lane else {})}
    m_ans = "questions whose evidence strings and expected answer occur together in one file the handoff loader read / questions"
    m_agent = (f"a model ({L.JUDGE_MODEL}) given only the loaded lane answers every question in one structured-output call; "
               "per-question match by the template's match kind; value = matched / questions")
    src = "build/agents (handoff loader) + templates/eval/probe-questions.json"
    scores = []
    if not qs:
        return [C.not_run("probe_answerable", "no questions (no content.json)", m_ans, src), C.not_run("probe_agent", "no questions", m_agent, src)], rec
    if not lane:
        scores.append(C.not_run("probe_answerable", "no agents/README.md", m_ans, src, {"questions": len(qs)}))
    else:
        for q in qs:
            q["answerable_in"] = answerable(q, lane["docs"])
        hit = sum(1 for q in qs if q["answerable_in"])
        scores.append(C.score("probe_answerable", C.ratio(hit, len(qs)), m_ans, src,
                              {"questions": len(qs), "answerable": hit, "lane_tokens": lane["tokens"], "lane_files": len(lane["docs"])},
                              {q["id"]: q["answerable_in"] for q in qs}))
    if not ctx["with_agent_probe"]:
        scores.append(C.not_run("probe_agent", "pass --with-agent-probe", m_agent, src, {"questions": len(qs)}))
        return scores, rec
    if not lane:
        scores.append(C.not_run("probe_agent", "no agents/README.md", m_agent, src))
        return scores, rec
    ok, why = L.claude_available()
    if not ok:
        scores.append(C.not_run("probe_agent", why, m_agent, src, {"questions": len(qs)}))
        return scores, rec
    docs = "\n".join(f'<document index="{i}"><source>{d["path"]}</source><document_content>\n{d["text"]}\n</document_content></document>'
                     for i, d in enumerate(lane["docs"], 1))
    ask = "\n".join(f'{q["id"]}: {q["question"]}' for q in qs)
    data, err, row = L.claude_json("probe", SYSTEM, f"{docs}\n\nQuestions (answer every id):\n{ask}", SCHEMA)
    m = {"questions": len(qs), "input_tokens": (row.get("usage") or {}).get("input_tokens"), "output_tokens": (row.get("usage") or {}).get("output_tokens")}
    if err:
        scores.append(C.error("probe_agent", err, m_agent, src, m))
        return scores, rec
    got = {a.get("id"): a.get("answer", "") for a in data.get("answers", [])}
    for q in qs:
        q["answer"] = got.get(q["id"], "")
        q["correct"] = matches(q, q["answer"])
    hit = sum(1 for q in qs if q["correct"])
    m["correct"] = hit
    scores.append(C.score("probe_agent", C.ratio(hit, len(qs)), m_agent, src, m, {q["id"]: q["correct"] for q in qs}))
    return scores, rec
