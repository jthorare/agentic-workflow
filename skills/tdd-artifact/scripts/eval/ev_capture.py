"""INPUT capture: the packet as a schema (source markdown, meta.json, lane files with hashes) plus labeled data
(owner decisions as question -> answer labels, citations as evidence labels, owner review notes from labels.jsonl)."""
import os, re
import ev_common as C

LANES = ("diagrams", "prs", "schema", "mockups", "research", "shots", "media")
META_FIELDS = ("title", "rev", "updated", "sha", "sha_date", "repo_url", "status", "rev_note", "repo_path")
VERDICTS = ("pass", "fail", "needs-work", "question")
TARGETS = ("artifact", "packet", "implementation", "qa", "bugs", "tokens")
WORD = re.compile(r"[A-Za-z0-9'’]+")
DATE = re.compile(r"\b(\d{4}-\d{2}-\d{2})\b")


def find_source(packet, meta, content):
    """The markdown source: content.json 'source' when present, else <slug>.md, else the only top-level .md."""
    cands = []
    if content and content.get("source"):
        cands.append(os.path.join(packet, content["source"]))
    cands.append(os.path.join(packet, os.path.basename(os.path.normpath(packet)) + ".md"))
    for c in cands:
        if os.path.isfile(c):
            return c
    mds = [f for f in sorted(os.listdir(packet)) if f.endswith(".md") and f.lower() not in ("status.md", "readme.md")]
    return os.path.join(packet, mds[0]) if len(mds) == 1 else None


def words_of(text):
    """The compiler's word count (build_tdd.Compiler.build): code fences, link targets and markdown punctuation removed."""
    return len(WORD.findall(re.sub(r"```.*?```|\]\([^)]*\)|[`*_#|\[\]()>{}-]", " ", text, flags=re.S)))


def lane_files(packet, lane):
    root = os.path.join(packet, lane)
    if not os.path.isdir(root):
        return None
    out = []
    for dp, dn, fn in os.walk(root):
        dn[:] = sorted(d for d in dn if not d.startswith("."))
        for f in sorted(fn):
            if f.startswith("."):
                continue
            p = os.path.join(dp, f)
            out.append({"path": os.path.relpath(p, packet), "sha256": C.sha256_file(p), "bytes": os.path.getsize(p)})
    return out


def read_labels(path):
    """labels.jsonl -> (records, problems). Each line {target, anchor, verdict, note, date[, author]}."""
    recs, problems = [], []
    if not path:
        return recs, problems
    text = C.read_text(path)
    if text is None:
        return recs, [f"{path}: cannot read"]
    for i, line in enumerate(text.splitlines(), 1):
        line = line.strip()
        if not line or line.startswith("//") or line.startswith("#"):
            continue
        try:
            import json
            r = json.loads(line)
        except ValueError as e:
            problems.append(f"{os.path.basename(path)}:{i}: not JSON ({e})")
            continue
        missing = [k for k in ("target", "anchor", "verdict", "note", "date") if k not in r]
        if missing:
            problems.append(f"{os.path.basename(path)}:{i}: missing {', '.join(missing)}")
            continue
        if r["target"] not in TARGETS:
            problems.append(f"{os.path.basename(path)}:{i}: target {r['target']!r} not one of {', '.join(TARGETS)}")
            continue
        if r["verdict"] not in VERDICTS:
            problems.append(f"{os.path.basename(path)}:{i}: verdict {r['verdict']!r} not one of {', '.join(VERDICTS)}")
            continue
        r["line"] = i
        recs.append(r)
    return recs, problems


def decision_labels(content):
    """Every decision box: {question -> owner answer}. The answer is the status line plus the box's first paragraph."""
    bodies = {}
    for n in C.walk(content.get("sections", []), {"decision"}):
        first = next((C.strip_tags(b.get("html")) for b in n.get("blocks", []) if b.get("type") == "p" and b.get("html")), "")
        bodies[n.get("id")] = first
    out = []
    for d in content.get("decisions", []):
        status = d.get("status", "")
        m = DATE.search(status)
        who = re.split(r"[,·]", status)
        out.append({"kind": "decision", "id": d.get("id"), "anchor": d.get("anchor"), "section": d.get("section"),
                    "question": d.get("question"), "status": status, "state": d.get("state"),
                    "answer": (bodies.get(d.get("anchor")) or "")[:600],
                    "label": "owner-ruling" if d.get("state") == "done" else ("held" if d.get("state") == "held" else "open-proposal"),
                    "date": m.group(1) if m else None})
    return out


def evidence_labels(content, meta):
    out = []
    for i, c in enumerate(content.get("cites", []), 1):
        out.append({"kind": "citation", "id": f"CIT.{i:04d}", "text": c.get("text"), "path": c.get("path"), "from": c.get("from"),
                    "to": c.get("to"), "section": c.get("section"), "href": c.get("href"), "pinned_sha": meta.get("sha") if c.get("href") else None})
    return out


def capture(packet, build, labels_path=None):
    packet = os.path.abspath(packet)
    content = C.read_json(os.path.join(build, "content.json"))
    meta_path = os.path.join(packet, "meta.json")
    meta = C.read_json(meta_path, {}) or {}
    slug = os.path.basename(packet)
    repo_slug = os.path.basename(os.path.dirname(os.path.dirname(packet))) if os.path.basename(os.path.dirname(packet)) == "system-design" else os.path.basename(os.path.dirname(packet))
    rev = meta.get("rev")
    key = f"{repo_slug}/{slug}@rev{rev if rev is not None else 'none'}"
    src = find_source(packet, meta, content)
    source = None
    if src:
        text = C.read_text(src) or ""
        source = {"path": os.path.relpath(src, packet), "sha256": C.sha256_file(src), "bytes": os.path.getsize(src), "words": words_of(text),
                  "words_method": "build_tdd word regex over the markdown (fences, link targets, markdown punctuation removed)"}
    files = {lane: lane_files(packet, lane) for lane in LANES}
    labels, problems = read_labels(labels_path)
    inp = {
        "schema": "tdd-eval-input/1", "captured": C.now(),
        "packet": {"slug": slug, "repo_slug": repo_slug, "dir": packet, "key": key, "build": os.path.abspath(build)},
        "source": source,
        "meta": {k: meta.get(k) for k in META_FIELDS if k in meta} | ({"project": (meta.get("project") or {}).get("label")} if meta.get("project") else {}),
        "meta_sha256": C.sha256_file(meta_path) if os.path.isfile(meta_path) else None,
        "files": {k: v for k, v in files.items() if v is not None},
        "content": {"path": "content.json", "sha256": C.sha256_file(os.path.join(build, "content.json")), "generated": content.get("generated"),
                    "template": content.get("template"), "stats": content.get("stats")} if content else None,
        "labels": {
            "decisions": decision_labels(content) if content else [],
            "evidence": evidence_labels(content, meta) if content else [],
            "review_notes": labels,
            "review_notes_source": os.path.abspath(labels_path) if labels_path else None,
            "review_notes_problems": problems,
        },
    }
    digest_basis = {"source": (source or {}).get("sha256"), "meta": inp["meta_sha256"],
                    "files": {k: [f["sha256"] for f in v] for k, v in inp["files"].items()}}
    import json
    inp["digest"] = C.sha256_bytes(json.dumps(digest_basis, sort_keys=True).encode())
    return inp, content, meta
