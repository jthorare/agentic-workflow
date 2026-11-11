"""Shared helpers for the TDD eval lane (stdlib only): score records, subprocess runs, content.json walking, hashes,
token estimates. Loaded by eval_packet.py into sys.modules["ev_common"]; the other ev_* modules import it by that name."""
import datetime, hashlib, html, importlib.util, json, os, re, shutil, subprocess, sys, time

sys.dont_write_bytecode = True
EVAL_DIR = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(EVAL_DIR)
SKILL = os.path.dirname(SCRIPTS)
PAGE_LIMIT = 16 * 1024 * 1024
CHARS_PER_TOKEN = 3.6          # the agents-lane convention (research/agent-files-2026.md 3: tokens_est = chars/3.6)
IMAGE_EXT = (".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif")
SQL_LANGS = ("sql", "psql", "postgres")
# Mirror of build_tdd.TOKEN (also mirrored in lint_packet.py): reference tokens such as D7, E3, F3.2, R2, PR 7, OS-01.
TOKEN = re.compile(r"(?<![\w#/.\-])((?:D|E|F|R|A|M|Q|G|OS|LN|SS|NFR|ADR)-?\d+(?:\.\d+)?|PR\s?\d+[a-z]?)(?![\w\-]|\.\d)")
LINK_KEYS = ("html", "caption", "reading", "one", "sub", "bodyHtml", "summary", "status", "change", "rollback", "title")


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load(relpath, name):
    """Import a file under scripts/eval/ (or an absolute path) as a module; registered in sys.modules under name."""
    path = relpath if os.path.isabs(relpath) else os.path.join(EVAL_DIR, relpath)
    if name in sys.modules:
        return sys.modules[name]
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


def load_sibling_script(filename, name):
    """Import a sibling script in scripts/ (another worker's file) defensively: (module, None) or (None, reason)."""
    path = os.path.join(SCRIPTS, filename)
    if not os.path.isfile(path):
        return None, f"{filename} is absent"
    try:
        return load(path, name), None
    except SystemExit as e:
        return None, f"{filename} exited on import ({e})"
    except Exception as e:  # another worker's file may be mid-edit
        return None, f"{filename} failed to import: {type(e).__name__}: {str(e)[:160]}"


# ----------------------------------------------------------------------------- score records

def score(name, value, method, source, metrics=None, detail=None, status="ok", reason=None):
    """One scorer result. value is 0..1 (or None for a metric-only record); method says how it was computed."""
    if value is not None:
        value = float(value)
        if not 0.0 <= value <= 1.0:
            raise ValueError(f"score {name}={value} outside 0..1")
        value = round(value, 4)
    rec = {"name": name, "value": value, "status": status, "method": method, "source": source}
    if reason:
        rec["reason"] = reason
    if metrics:
        rec["metrics"] = metrics
    if detail is not None:
        rec["detail"] = detail
    return rec


def not_run(name, reason, method, source, metrics=None, detail=None):
    return score(name, None, method, source, metrics, detail, status="not run", reason=reason)


def error(name, reason, method, source, metrics=None, detail=None):
    return score(name, None, method, source, metrics, detail, status="error", reason=reason)


def metric(name, metrics, method, source, detail=None):
    """A record with numbers only (no 0..1 score), e.g. reading minutes or byte sizes."""
    return score(name, None, method, source, metrics, detail, status="ok")


def ratio(num, den):
    return (num / den) if den else None


def output(kind, packet_key, scores, data=None, extra=None):
    flat = {}
    for s in scores:
        for k, v in (s.get("metrics") or {}).items():
            if isinstance(v, bool):
                v = int(v)
            if isinstance(v, (int, float)):
                flat[f"{s['name']}.{k}"] = v
    out = {"schema": "tdd-eval-output/1", "kind": kind, "packet": packet_key, "generated": now(), "scores": scores,
           "metrics": flat, "counts": {"ok": sum(1 for s in scores if s["status"] == "ok" and s["value"] is not None),
                                       "metric_only": sum(1 for s in scores if s["status"] == "ok" and s["value"] is None),
                                       "not_run": sum(1 for s in scores if s["status"] == "not run"),
                                       "error": sum(1 for s in scores if s["status"] == "error")}}
    if data is not None:
        out["data"] = data
    if extra:
        out.update(extra)
    return out


# ----------------------------------------------------------------------------- files and subprocesses

def sha256_bytes(b):
    return hashlib.sha256(b).hexdigest()


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_json(path, default=None):
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return default


def write_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(obj, fh, indent=1, ensure_ascii=False, default=str)
        fh.write("\n")


def read_text(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except OSError:
        return None


def tokens_est(text_or_len):
    n = text_or_len if isinstance(text_or_len, int) else len(text_or_len or "")
    return int(round(n / CHARS_PER_TOKEN))


def which(name):
    return shutil.which(name)


def run(cmd, cwd=None, timeout=300):
    """Run a command; returns {code, out, err, seconds, timeout}. code is None when it could not start or timed out."""
    t0 = time.time()
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout)
        return {"code": p.returncode, "out": p.stdout, "err": p.stderr, "seconds": round(time.time() - t0, 2), "timeout": False}
    except subprocess.TimeoutExpired as e:
        out = e.stdout.decode("utf-8", "replace") if isinstance(e.stdout, bytes) else (e.stdout or "")
        return {"code": None, "out": out, "err": f"timed out after {timeout}s", "seconds": round(time.time() - t0, 2), "timeout": True}
    except OSError as e:
        return {"code": None, "out": "", "err": f"{type(e).__name__}: {e}", "seconds": round(time.time() - t0, 2), "timeout": False}


def tail(text, n=12):
    lines = [l for l in (text or "").splitlines() if l.strip()]
    return lines[-n:]


def one_line(text, limit=200):
    s = " ".join((text or "").split())
    # never echo an auth URL or token that a CLI prints in an error
    s = re.sub(r"https?://\S*(authorization_request|token|sso)\S*", "<url omitted>", s)
    return s[:limit]


# ----------------------------------------------------------------------------- content.json

def strip_tags(s):
    return html.unescape(re.sub(r"<[^>]+>", "", s or "")).strip()


def walk(node, want=None):
    """Yield every dict node in the block tree (optionally only those whose type is in want), document order."""
    if isinstance(node, dict):
        if want is None or node.get("type") in want:
            yield node
        for v in node.values():
            if isinstance(v, (dict, list)):
                yield from walk(v, want)
    elif isinstance(node, list):
        for x in node:
            yield from walk(x, want)


def sql_fences(content):
    return [n for n in walk(content.get("sections", []), {"code"}) if (n.get("lang") or "").lower() in SQL_LANGS]


def prs_of(content):
    out = []
    for n in walk(content.get("sections", []), {"prs"}):
        for g in n.get("groups", []):
            for p in g.get("prs", []):
                out.append({"n": p.get("n"), "id": p.get("id"), "title": strip_tags(p.get("title")), "change": strip_tags(p.get("change")),
                            "milestone": strip_tags(g.get("ms")), "file": p.get("file"), "body": strip_tags(p.get("bodyHtml"))})
    return out


def content_text(content, limit_chars=None):
    """Plain text of the page in document order: title, lede, each section's title and its blocks' text."""
    parts = [content.get("titleText") or strip_tags(content.get("title"))]
    for p in content.get("lede", []):
        parts.append(strip_tags(p.get("html")))
    for s in content.get("sections", []):
        parts.append(f"\n## {s.get('num')}. {s.get('titleText')}")
        for n in walk(s.get("blocks", [])):
            t = n.get("type")
            if t == "heading":
                parts.append("### " + strip_tags(n.get("html")))
            elif t in ("p",):
                parts.append(strip_tags(n.get("html")))
            elif t == "list":
                parts.extend("- " + strip_tags(i.get("html")) for i in n.get("items", []))
            elif t == "table":
                parts.append(" | ".join(strip_tags(h.get("html")) for h in n.get("head", [])))
                parts.extend(" | ".join(strip_tags(c.get("html")) for c in r.get("cells", [])) for r in n.get("rows", []))
            elif t == "code":
                parts.append(f"```{n.get('lang') or ''}\n{n.get('text', '')}\n```")
            elif t == "decision":
                parts.append(f"Decision {n.get('did')}: {strip_tags(n.get('question'))} [{strip_tags(n.get('status'))}]")
            elif t in ("fold", "group", "persona"):
                parts.append("#### " + strip_tags(n.get("title")) + ((" · " + strip_tags(n.get("one"))) if n.get("one") else ""))
            elif t == "cards":
                for c in n.get("cards", []):
                    parts.append("* " + strip_tags(c.get("title")))
            elif t == "diagram":
                parts.append(f"[diagram: {strip_tags(n.get('title'))}] {strip_tags(n.get('reading'))}")
            elif t == "prs":
                for g in n.get("groups", []):
                    for p in g.get("prs", []):
                        parts.append(f"PR {p.get('n')}: {strip_tags(p.get('title'))}. {strip_tags(p.get('change'))}")
    text = "\n".join(x for x in parts if x)
    return text[:limit_chars] if limit_chars else text


def norm(s):
    return re.sub(r"\s+", " ", (s or "").strip().lower())
