#!/usr/bin/env python3
"""Lint a house TDD packet: the markdown source, meta.json, prs/, diagrams/, mockups/, schema/, and the build that
build_tdd.py wrote (build/content.json + build/content.files.json), against the rules of the house TDD system.

Usage:
  lint_packet.py <packet-dir> [--build <dir>] [--strict] [--json <out>] [--skip <rule,...>] [--compile]

  --build <dir>   the build folder to read (default <packet>/build). When it has no content.json, or with --compile,
                  the source is compiled into a temporary folder (build_tdd.py --no-copy) and that is linted instead;
                  nothing is written into the packet.
  --strict        exit 1 on any warning too.
  --json <out>    also write {packet, build, compiled, findings[], notes[], counts} to <out>.
  --skip <ids>    rule ids (STRUCT) or check ids (CITES/no-line) to leave out, comma separated.

Rules (stable ids; references/validators.md says what each checks, its severity and how to fix it):
  STRUCT  required sections in the house order, numbering, an id on every ##, no literal markdown left on the page
  DNUM    decision D-numbers unique and gapless from D1, a status on each, a date on each done one, in the decisions section
  REFS    every reference token defined and linked (D5), every #anchor link and hotspot href resolves, meta refs families
  CITES   every path:line citation resolves in repo_path at meta.sha with the line range inside the file; sha and sha_date
  DIAG    every [diagram: key] has its svg/png; .mmd not newer than the svg; hotspots match the svg; legend tones
  SCHEMA  schema/*.json against the D7 table contract; measurements carry source and date; expectations carry a basis
  FILES   the publish manifest: nothing missing, nothing over 15 MB, page <= 16 MB, no orphan media, PR files loadable;
          no scaffold placeholder left (a placeholder image the page publishes, a template prompt in the source or status.md)
  META    meta.json header fields; rev or updated moved when the source changed after the last build; artifact_url
  AGENTS  write_agents.check(build_dir) over build/agents (skipped with a note when either is absent)
  XDOC    glossary terms defined differently in sibling packets of the same repo slug (D-numbers are per doc: not checked)
  PARITY  the TypeScript compiler (cli/bin/build-tdd.ts, D19) reproduces the build's content.json byte for byte and its
          manifest's files, missing and warnings (skipped with a note when bun or cli/ is absent)

Output: findings grouped by rule, one line each (`severity CHECK-ID file:line: message`), notes, then
`N errors, M warnings`. Exit 0 when clean or only warnings; 1 on any error (or any warning with --strict); 2 on bad usage.
Stdlib only; runs under python3 -I; git is called through subprocess, read-only."""
import datetime, html, importlib.util, json, os, re, shutil, subprocess, sys, tempfile
sys.dont_write_bytecode = True

HERE = os.path.dirname(os.path.abspath(__file__))
RULES = ("STRUCT", "DNUM", "REFS", "CITES", "DIAG", "SCHEMA", "FILES", "META", "AGENTS", "XDOC", "PARITY")
CLI = os.path.normpath(os.path.join(HERE, "..", "cli"))
FILE_LIMIT = 15 * 1024 * 1024
PAGE_LIMIT = 16 * 1024 * 1024
MEDIA_EXT = (".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".mp4", ".webm", ".mov")
CODE_EXT = {"ts", "tsx", "js", "mjs", "cjs", "py", "sql", "cs", "rb", "go", "rs", "sh", "css", "html", "graphql", "proto"}
ISO_DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
ANY_DATE = re.compile(r"\b\d{4}-\d{2}-\d{2}\b")

# ----------------------------------------------------------------------------- the compiler's vocabulary
# Mirrors of build_tdd.py; replaced by the compiler's own objects when it imports, so the two never drift.
TOKEN = re.compile(r"(?<![\w#/.\-])((?:D|E|F|R|A|M|Q|G|OS|LN|SS|NFR|ADR)-?\d+(?:\.\d+)?|PR\s?\d+)(?![\w\-]|\.\d)")
CITE = re.compile(r"^((?:[A-Z]{2,5}/)?[\w@./-]+\.(?:ts|tsx|js|mjs|cjs|py|sql|json|md|cs|yaml|yml|toml|rb|go|rs|sh|css|html|txt|graphql|proto))(?::(\d+)(?:-(\d+))?)?$")
LINK_KEYS = {"html", "caption", "reading", "one", "sub", "bodyHtml", "summary", "status", "change", "rollback"}
TONE_CLASSES = {"green", "grey", "blue", "amber", "red", "violet", "orange", "dashed"}
MARK = re.compile(r"^\s*\\?\[(embedded content|diagram|video|media|image|legend|reading)\s*:\s*(.*?)\s*\\?\]\s*$", re.I)

def token_id(tok):
    t = tok.strip().lower().replace(" ", "-")
    t = re.sub(r"^pr(\d)", r"pr-\1", t)
    return t.replace(".", "-")

def _load(name, path):
    """Import a sibling script as a module; None when it is absent or fails to import (never fatal)."""
    if not os.path.isfile(path):
        return None
    try:
        spec = importlib.util.spec_from_file_location(name, path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod
    except (Exception, SystemExit):
        return None

_bt = _load("build_tdd", os.path.join(HERE, "build_tdd.py"))
if _bt is not None:
    TOKEN, CITE, token_id = getattr(_bt, "TOKEN", TOKEN), getattr(_bt, "CITE", CITE), getattr(_bt, "token_id", token_id)
    LINK_KEYS = set(getattr(getattr(_bt, "Compiler", None), "LINK_KEYS", LINK_KEYS))
    if getattr(_bt, "LEGEND_TONES", None):
        TONE_CLASSES = {c for v in _bt.LEGEND_TONES.values() for c in v.split()}
    MARK = getattr(_bt.bd, "MARK", MARK) if getattr(_bt, "bd", None) else MARK

def split_marker(arg):
    m = re.match(r"\s*(.*?)\s*(?:·|\|)\s*(.*)$", arg)
    return (m.group(1).strip(), m.group(2).strip()) if m else (arg.strip(), "")

ATTRS = re.compile(r"\s*\{([^{}]*)\}\s*$")

def parse_attrs(text):
    attrs = {}
    m = ATTRS.search(text)
    if not m:
        return text, attrs
    for part in m.group(1).split(";"):
        part = part.strip()
        if part.startswith("#"):
            attrs["id"] = part[1:].strip()
        elif ":" in part:
            k, v = part.split(":", 1)
            attrs[k.strip().lower()] = v.strip()
        elif part:
            attrs[part.lower()] = True
    return text[:m.start()].rstrip(), attrs

def plain(s):
    """Visible text of an inline-HTML string."""
    return html.unescape(re.sub(r"<[^>]+>", "", s or ""))

def norm(s):
    return re.sub(r"[^a-z0-9]+", "", (s or "").lower())

def unlinked_text(s):
    """Text of an inline-HTML string with links, code and already-flagged missing refs removed."""
    s = re.sub(r"<a\b[^>]*>.*?</a>", " ", s or "", flags=re.S)
    s = re.sub(r"<(code|pre)\b[^>]*>.*?</\1>", " ", s, flags=re.S)
    s = re.sub(r'<span class="ref-missing"[^>]*>.*?</span>', " ", s, flags=re.S)
    return plain(s)

def outside_fences(lines):
    """(line number, text) for every source line outside ``` fences."""
    fence = None
    for i, line in enumerate(lines, 1):
        m = re.match(r"^\s*(`{3,}|~{3,})", line)
        if m:
            if fence is None:
                fence = m.group(1)[0] * len(m.group(1))
            elif m.group(1).startswith(fence):
                fence = None
            continue
        if fence is None:
            yield i, line

def walk(node, path=()):
    """Yield (path, parent dict, key, value) for every string in a content tree."""
    if isinstance(node, dict):
        for k, v in node.items():
            if isinstance(v, str):
                yield path, node, k, v
            else:
                yield from walk(v, path + ((node, k),))
    elif isinstance(node, list):
        for x in node:
            yield from walk(x, path)

def nodes(node):
    if isinstance(node, dict):
        yield node
        for v in node.values():
            yield from nodes(v)
    elif isinstance(node, list):
        for x in node:
            yield from nodes(x)

def git(repo, *args):
    try:
        p = subprocess.run(["git", "-C", repo, *args], capture_output=True, timeout=60)
    except (OSError, subprocess.TimeoutExpired) as e:
        return 1, str(e).encode()
    return p.returncode, p.stdout if p.returncode == 0 else p.stderr

def svg_viewbox(path):
    try:
        head = open(path, "rb").read(8192).decode("utf-8", "ignore")
    except OSError:
        return None
    m = re.search(r'viewBox="([\d.\-]+)[ ,]+([\d.\-]+)[ ,]+([\d.]+)[ ,]+([\d.]+)"', head)
    return [float(x) for x in m.groups()] if m else None

def kind_of(parent, k):
    """A reader's name for the node that holds a string."""
    if parent.get("type"):
        return {"p": "paragraph"}.get(parent["type"], parent["type"])
    if k == "label" and "tone" in parent and "x" not in parent:
        return "legend item"
    if "x" in parent and "label" in parent:
        return "hotspot"
    if "n" in parent and "bodyHtml" in parent:
        return "PR card"
    if "ms" in parent:
        return "PR group"
    if "rec" in parent:
        return "option"
    if k == "title" and "blocks" in parent:
        return "card"
    if "cells" in parent or "align" in parent:
        return "table cell"
    return "item"

# ----------------------------------------------------------------------------- the scaffold's placeholders
# What new_packet.py (and `tdd new`) writes for the author to replace (FILES/placeholder and FILES/placeholder-text): the
# marker on every placeholder image in templates/packet/, the placeholder date, and the <angle-bracket> prompts of
# templates/tdd-template.md (outside its one-line comments) and of the status.md skeleton, in a fresh scaffold's order.
# An all-lower-case one-word prompt (<name>, <path>, <n>) is ordinary command-line notation and is left out; every
# template line that carries one also carries a prompt listed here. cli/test/lint/placeholders.test.ts keeps the list
# equal to what the scaffold writes.
PLACEHOLDER_MARK = "PLACEHOLDER · REPLACE ME"
PLACEHOLDER_DATE = "YYYY-MM-DD"
PLACEHOLDER_PROMPTS = (
    "<Title: the finding, not the topic>",
    "<The finding in two to four sentences: what changes, who it reaches first, and what stays exactly as it is today.>",
    "<the finding as the figure title>", "<one sentence that tells the reader where to start and what the arrows carry>", "<What is new>",
    "<The new tables, services or screens, in one short paragraph.>", "<What stays exactly as it is today.>", "<The one deliberate change>",
    "<A change to an existing system, and why it is worth it.>", "<Who sees it first, with a dated count and where the count came from.>", "<Role>",
    "<Moment>", "<What this person sees and does here, in two or three sentences: which screen, which component, and what it reads.>", "<State>",
    "<what the screen shows in that state, and why.>", "<What the shot shows, with the real names and numbers on it>",
    "<Caption: the state this shot shows>", "<What the shot shows>", "<Caption>", "<What the screen does today, in a few words>",
    "<Today's screen, as it is in production>", "<what today's screen does>", "<What changes for this person, in two sentences.>",
    "<the states and screens with no mock, and which built component each one reuses>",
    "<the PRD, the ticket and the threads, each with the date read>", "<The requirement, in the PRD's words>",
    "<The section, card or table that meets it>", "<The requirement>", "<What is still missing, and the open question that decides it (Q1)>",
    "<Why it is out of this design, and who agreed>",
    "<The flow in two or three sentences: the phases in order, and what each one reads and writes.>",
    "<What the reader should notice first, such as which step never runs inside a transaction>",
    "<the new and changed tables and what they reference>", "<Phase one>", "<ALIAS>",
    "<What the design adds here, and the existing code it hooks into.>", "<A detail of that step>", "<Another detail>", "<The next new step.>",
    "<Phase two>", "<A new step.>", "<An unchanged step this design relies on.>", "<The question, as the owner would ask it>",
    "<The background and the gap, with one concrete example in real numbers.>", "<Option A>", "<What A means, what it costs, and who it affects.>",
    "<Option B>", "<What B means, what it costs, and who it affects.>", "<The decision, in one line>", "<Owner>",
    "<The owner's answer in their words, then what it means for the design.>", "<In scope>", "<Out of scope, with the PRD non-goal it matches>",
    "<system name>", "<system response>", "<What it counts>", "<YYYY-MM-DD>", "<How each step is undone, and what stays written.>",
    "<Step one: dark behind the flag>", "<Step two>", "<new_table>", "<what one row is>",
    "<Why this shape: the alternatives, and the invariant the table keeps.>", "<new_column>", "<what it holds>", "<example id>",
    "<YYYY-MM-DD hh:mm>", "<What this row means>", "<What it stands for, and what it means in this design>", "<the owner's words>",
)
# longest first, so a prompt is reported whole; never right after a letter or digit (useState<State>, Array<Role>)
PLACEHOLDER = re.compile(r"(?<!\w)(?:" + "|".join(re.escape(t) for t in sorted((PLACEHOLDER_MARK, PLACEHOLDER_DATE) + PLACEHOLDER_PROMPTS,
                                                                                 key=len, reverse=True)) + ")")
COMMENT = re.compile(r"^\s*<!--.*-->\s*$")  # build_doc.COMMENT: a comment on one line, which the compiler drops

# ----------------------------------------------------------------------------- the linter

class Lint:
    def __init__(self, packet, build=None, compile_=False, skip=()):
        self.root = os.path.abspath(os.path.expanduser(packet))
        self.slug = os.path.basename(self.root.rstrip("/"))
        self.skip = {s.strip() for s in skip if s.strip()}
        self.findings, self.notes = [], []
        self._lines, self._norm = {}, {}
        self.build = os.path.abspath(os.path.expanduser(build)) if build else os.path.join(self.root, "build")
        self.compiled, self.tmp = False, None
        self.meta = self.load_json(os.path.join(self.root, "meta.json"), "META", quiet_missing=True)
        self.src = self.find_source()
        self.prs = sorted(os.path.join(self.root, "prs", f) for f in os.listdir(os.path.join(self.root, "prs"))
                          if f.endswith(".md")) if os.path.isdir(os.path.join(self.root, "prs")) else []
        self.content, self.manifest = None, None
        cpath = os.path.join(self.build, "content.json")
        if compile_ or not os.path.isfile(cpath):
            self.compile(reason="--compile" if compile_ else f"no {self.rel(cpath)}")
        else:
            self.content = self.load_json(cpath, "STRUCT")
            self.manifest = self.load_json(os.path.join(self.build, "content.files.json"), "FILES", quiet_missing=True)
            if self.manifest is None:
                self.add("FILES", "no-manifest", "error", f"no content.files.json beside {self.rel(cpath)}: rebuild with build_tdd.py", self.rel(self.build))
        if self.content is None:
            self.content = {}
        self.page_ids = self.collect_ids()
        self.slots = self.match_slots()

    # -- plumbing
    def add(self, rule, check, sev, msg, file=None, line=None):
        cid = f"{rule}/{check}"
        if rule in self.skip or cid in self.skip:
            return
        self.findings.append({"rule": rule, "check": cid, "severity": sev, "file": file, "line": line, "message": " ".join(str(msg).split())})

    def note(self, msg):
        self.notes.append(" ".join(msg.split()))

    def rel(self, path):
        if not path:
            return path
        p = os.path.abspath(path)
        if p.startswith(self.root + os.sep):
            return os.path.relpath(p, self.root)
        home = os.path.expanduser("~")
        return "~" + p[len(home):] if p.startswith(home + os.sep) else p

    def load_json(self, path, rule, quiet_missing=False):
        if not os.path.isfile(path):
            if not quiet_missing:
                self.add(rule, "missing-file", "error", f"{self.rel(path)} does not exist", self.rel(path))
            return None
        try:
            return json.load(open(path, encoding="utf-8"))
        except (ValueError, OSError) as e:
            self.add(rule, "bad-json", "error", f"{self.rel(path)} is not valid JSON: {e}", self.rel(path))
            return None

    def find_source(self):
        cand = os.path.join(self.root, self.slug + ".md")
        if os.path.isfile(cand):
            return cand
        c = None
        cpath = os.path.join(self.build, "content.json")
        if os.path.isfile(cpath):
            try:
                c = json.load(open(cpath, encoding="utf-8")).get("source")
            except (ValueError, OSError):
                c = None
        if c and os.path.isfile(os.path.join(self.root, c)):
            return os.path.join(self.root, c)
        mds = [f for f in sorted(os.listdir(self.root)) if f.endswith(".md") and f != "status.md" and not f.startswith(("review", "README"))]
        return os.path.join(self.root, mds[0]) if len(mds) == 1 else None

    def compile(self, reason):
        if not self.src:
            self.add("STRUCT", "no-source", "error", f"no source markdown in the packet (expected {self.slug}.md)", None)
            return
        bt = os.path.join(HERE, "build_tdd.py")
        if not os.path.isfile(bt):
            self.add("STRUCT", "no-content", "error", f"{reason}, and build_tdd.py is not beside this script to compile one", None)
            return
        self.tmp = tempfile.mkdtemp(prefix="lint-tdd-")
        p = subprocess.run([sys.executable, "-I", bt, self.src, self.tmp, "--no-copy"], capture_output=True, text=True, cwd=self.root)
        if p.returncode != 0 or not os.path.isfile(os.path.join(self.tmp, "content.json")):
            tail = (p.stderr or p.stdout).strip().splitlines()[-1:] or ["no output"]
            self.add("STRUCT", "no-content", "error", f"{reason}; compiling {self.rel(self.src)} failed: {tail[0]}", self.rel(self.src))
            return
        self.compiled = True
        self.note(f"{reason}: compiled {self.rel(self.src)} with build_tdd.py --no-copy into a temporary folder and linted that "
                  f"(page, stale-build and agents checks need a real build)")
        self.content = json.load(open(os.path.join(self.tmp, "content.json"), encoding="utf-8"))
        self.manifest = json.load(open(os.path.join(self.tmp, "content.files.json"), encoding="utf-8"))

    def cleanup(self):
        if self.tmp:
            shutil.rmtree(self.tmp, ignore_errors=True)

    def lines(self, path):
        if path not in self._lines:
            try:
                self._lines[path] = open(path, encoding="utf-8").read().split("\n")
            except (OSError, UnicodeDecodeError):
                self._lines[path] = []
            self._norm[path] = [norm(l) for l in self._lines[path]]
        return self._lines[path]

    def text_files(self):
        return ([self.src] if self.src else []) + self.prs

    def locate(self, pattern, files=None):
        """(relative file, line) of the first line matching a regex (or containing a literal string)."""
        rx = pattern if hasattr(pattern, "search") else re.compile(re.escape(pattern))
        for f in files or self.text_files():
            for i, line in enumerate(self.lines(f), 1):
                if rx.search(line):
                    return self.rel(f), i
        return None, None

    def locate_all(self, rx, files=None):
        return [(self.rel(f), i) for f in files or self.text_files() for i, line in enumerate(self.lines(f), 1) if rx.search(line)]

    def locate_label(self, label, path):
        """Line of a box label in a hotspots json, whichever way the json escaped it."""
        for enc in (json.dumps(label, ensure_ascii=False), json.dumps(label)):
            f, l = self.locate('"label": ' + enc, [path])
            if f:
                return f, l
        return self.rel(path), None

    def locate_text(self, text, files=None):
        """Locate rendered text in the markdown: literally, then by its letters and digits only (markup differs)."""
        lit = (text or "").strip()[:60]
        if len(lit) >= 3:
            f, l = self.locate(lit, files)
            if f:
                return f, l
        n = norm(text)
        if not n:
            return None, None
        for size in (60, 32, 16, 8):
            probe = n[:size]
            if len(probe) < 4 or (size < 16 and len(n) > size):
                break
            for f in files or self.text_files():
                self.lines(f)
                for i, nl in enumerate(self._norm[f], 1):
                    if probe in nl:
                        return self.rel(f), i
        return None, None

    def locate_token(self, tok):
        return self.locate(re.compile(r"(?<![\w#/.\-])" + re.escape(tok) + r"(?![\w\-]|\.\d)"))

    # -- page facts
    def collect_ids(self):
        c = self.content
        ids = set(c.get("defs", {})) | {a.get("id") for a in c.get("anchors", []) if a.get("id")}
        for n in nodes([c.get("sections", []), c.get("lede", [])]):
            if isinstance(n.get("id"), str) and n["id"]:
                ids.add(n["id"])
        for _, _, _, v in walk([c.get("sections", []), c.get("lede", [])]):
            ids.update(re.findall(r'\bid="([^"]+)"', v))
        ids.update(t.get("id") for t in c.get("schema", []) if t.get("id"))
        return ids

    SLOTS = [
        ("the redesign (overview)", {"redesign", "overview"}, r"\b(redesign|overview)\b"),
        ("front end", {"frontend", "front-end"}, r"\bfront[\s-]?end\b"),
        ("rule coverage or requirements", {"coverage", "requirements", "rules"}, r"\b(coverage|requirements)\b"),
        ("end-to-end flow", {"flow", "end-to-end", "end-to-end-flow", "e2e"}, r"\bend[\s-]to[\s-]end\b"),
        ("decisions and open questions", {"questions", "decisions", "open-questions"}, r"\bdecisions?\b|\bopen questions\b"),
        ("for agents", {"more", "agents", "for-agents"}, r"\bfor agents\b"),
    ]

    def match_slots(self):
        secs, used, out = self.content.get("sections", []), set(), []
        for name, ids, rx in self.SLOTS:
            hit = next((s for s in secs if s["id"] not in used and s["id"] in ids), None) or \
                  next((s for s in secs if s["id"] not in used and re.search(rx, s.get("titleText", ""), re.I)), None)
            if hit:
                used.add(hit["id"])
            out.append((name, hit))
        return out

    def source_sections(self):
        """[(line, title, attrs)] for every ## in the source."""
        if not self.src:
            return []
        out = []
        for i, line in outside_fences(self.lines(self.src)):
            m = re.match(r"^##\s+(.*?)\s*$", line)
            if m:
                t, attrs = parse_attrs(m.group(1))
                out.append((i, t, attrs))
        return out

    # ------------------------------------------------------------------------- STRUCT
    def r_struct(self):
        c = self.content
        if not c:
            return
        secs = c.get("sections", [])
        src_secs = self.source_sections()
        by_title = {norm(re.sub(r"[*`]", "", t)): ln for ln, t, _ in src_secs}
        loc = {s["id"]: (self.rel(self.src), by_title.get(norm(s.get("titleText")), src_secs[k][0] if k < len(src_secs) else None)) for k, s in enumerate(secs)}
        order = "overview, front end, rule coverage or requirements, end-to-end flow, decisions and open questions, for agents"
        placed = []
        for name, hit in self.slots:
            if not hit:
                self.add("STRUCT", "missing-section", "error", f"no '{name}' section (house order: {order}; matched by id or title)", self.rel(self.src))
            else:
                placed.append((secs.index(hit), name, hit))
        # the sections outside the longest run already in house order are the ones out of place
        best = [[p] for p in placed]
        for i in range(len(placed)):
            for j in range(i):
                if placed[j][0] < placed[i][0] and len(best[j]) + 1 > len(best[i]):
                    best[i] = best[j] + [placed[i]]
        keep = {id(p) for p in max(best, key=len)} if best else set()
        for p in placed:
            if id(p) not in keep:
                f, l = loc.get(p[2]["id"], (None, None))
                self.add("STRUCT", "section-order", "warn", f"section {p[2]['num']} '{p[2]['titleText']}' ({p[1]}) is out of the house order ({order})", f, l)
        for k, s in enumerate(secs, 1):
            if s.get("num") != k:
                self.add("STRUCT", "numbering", "error", f"section '{s.get('titleText')}' is numbered {s.get('num')} but is section {k} in order", "build/content.json")
        if src_secs and secs:
            if len(src_secs) != len(secs):
                self.add("STRUCT", "numbering", "error", f"the source has {len(src_secs)} ## sections but content.json has {len(secs)}: the build is stale; rebuild",
                         self.rel(self.src))
            else:
                for k, ((ln, t, _), s) in enumerate(zip(src_secs, secs), 1):
                    if norm(re.sub(r"[*`]", "", t)) != norm(s.get("titleText")):
                        self.add("STRUCT", "numbering", "error", f"section {k} is '{s.get('titleText')}' in content.json but '{t}' in the source: the build "
                                 f"is stale; rebuild", self.rel(self.src), ln)
        for ln, t, attrs in src_secs:
            if not attrs.get("id"):
                self.add("STRUCT", "heading-id", "error", f"'## {t}' has no {{#id}}: its anchor is the title's slug and every link to it breaks when the "
                         f"title changes", self.rel(self.src), ln)
        # explicit ids that did not survive (a collision renamed them, or the block that carries them was dropped)
        seen = {}
        for f in self.text_files()[:1]:
            for ln, line in outside_fences(self.lines(f)):
                m = ATTRS.search(line)
                if not m or not re.match(r"^\s*(#{2,6}\s|:{3,}\s*[a-z]|\\?\[diagram|\S)", line):
                    continue
                for part in m.group(1).split(";"):
                    part = part.strip()
                    if part.startswith("#") and re.fullmatch(r"#[\w-]+", part):
                        seen.setdefault(part[1:], []).append(ln)
        for i, lns in seen.items():
            if len(lns) > 1:
                self.add("STRUCT", "id-collision", "warn", f"explicit id #{i} is set {len(lns)} times (lines {', '.join(map(str, lns))}); later ones are "
                         f"renamed {i}-2 and links go to the first", self.rel(self.src), lns[1])
            elif i not in self.page_ids:
                self.add("STRUCT", "id-collision", "warn", f"explicit id #{i} is not on the page (the block type ignores ids, or the id collided)",
                         self.rel(self.src), lns[0])
        # literal markdown the converters left in page text
        pats = [("an image", re.compile(r"!\[[^\]]*\]\([^)\s]+(?:\s+\"[^\"]*\")?\)")),
                ("a link", re.compile(r"(?<!!)\[[^\]\n]+\]\((?:https?://|#|\.{0,2}/|mockups/|diagrams/|prs/)[^)\s]*\)")),
                ("bold markers", re.compile(r"(?<![*\w])\*\*[^*\s][^*]*?\*\*(?![*\w])")),
                ("a marker", re.compile(r"\[(?:diagram|legend|reading|image|chip)\s*:")),
                ("a container fence", re.compile(r"(?:^|>)\s*:{3,}\s*[a-z]"))]
        done = set()
        for path, parent, k, v in walk([c.get("sections", []), c.get("lede", [])]):
            if k in ("text", "src", "alt", "href", "key", "ddl", "id", "file", "lang", "tone", "cls", "type"):
                continue
            s = re.sub(r"<(code|pre)\b[^>]*>.*?</\1>", " ", v, flags=re.S)
            for what, rx in pats:
                for m in rx.finditer(s):
                    snip = html.unescape(m.group(0))
                    if snip in done:
                        continue
                    done.add(snip)
                    where = kind_of(parent, k)
                    files = (self.prs + [self.src]) if where.startswith("PR") and self.src else None
                    f, l = self.locate(snip[:60], files)
                    if not f:
                        f, l = self.locate_text(plain(snip), files)
                    hint = ("build_prs.py's markdown does not convert [text](#anchor) links in PR bodies" if where == "PR card" and what == "a link"
                            else "two images on adjacent lines become one paragraph: put a blank line between them" if what == "an image"
                            else "the converter does not handle it in this position")
                    self.add("STRUCT", "raw-markdown", "error", f"{what} is literal text in a {where}.{k}: {snip[:80]!r} ({hint})", f, l)

    # ------------------------------------------------------------------------- DNUM
    def r_dnum(self):
        c = self.content
        if not c:
            return
        decs = c.get("decisions", [])
        dsec = self.slots[4][1]["id"] if self.slots and self.slots[4][1] else None
        if not decs:
            dtoks = sorted({m.group(1) for f in self.text_files() for _, l in outside_fences(self.lines(f))
                            for m in re.finditer(r"(?<![\w#/.\-])(D-?\d+)(?![\w\-]|\.\d)", l)}, key=lambda t: int(re.sub(r"\D", "", t)))
            if dtoks:
                self.add("DNUM", "none", "warn", f"the page refers to {len(dtoks)} D-numbers ({', '.join(dtoks[:8])}{'…' if len(dtoks) > 8 else ''}) but defines "
                         f"no ::: decision blocks; write each ruled decision as a decision box in the decisions section", self.rel(self.src))
            return
        seen, dnums, nth = {}, [], {}
        for d in decs:
            did = d.get("id", "")
            hits = self.locate_all(re.compile(r"^\s*:{3,}\s*decision\s+" + re.escape(did) + r"(?![\w.])"), [self.src] if self.src else None)
            k = nth[did] = nth.get(did, -1) + 1
            f, l = hits[k] if k < len(hits) else (hits[0] if hits else (None, None))
            if did in seen:
                self.add("DNUM", "duplicate", "error", f"{did} is defined twice (first at line {seen[did]}); D-numbers are unique per doc", f, l)
            seen.setdefault(did, l)
            m = re.fullmatch(r"D-?(\d+)", did)
            if m:
                dnums.append((int(m.group(1)), f, l))
            elif not TOKEN.fullmatch(did):
                self.add("DNUM", "bad-id", "error", f"decision '{did}' does not start with a reference token (write ::: decision D7 · the question)", f, l)
            status = (d.get("status") or "").strip()
            if not status:
                self.add("DNUM", "no-status", "error", f"{did} has no status (add {{status: <owner>, YYYY-MM-DD}} or {{status: Open · ...}})", f, l)
            elif d.get("state") == "done" and not ANY_DATE.search(status):
                self.add("DNUM", "done-no-date", "error", f"{did} is decided but its status '{status}' carries no YYYY-MM-DD date", f, l)
            if dsec and d.get("section") != dsec:
                self.add("DNUM", "outside-section", "warn", f"{did} sits in section '{d.get('section')}', not in the decisions section '{dsec}'", f, l)
        if dnums:
            have = {n for n, _, _ in dnums}
            top = max(have)
            gap = [n for n in range(1, top + 1) if n not in have]
            if gap:
                f, l = dnums[0][1], dnums[0][2]
                self.add("DNUM", "gap", "error", f"D-numbers must run from D1 with no gap: missing {', '.join('D%d' % n for n in gap)} "
                         f"(defined: {', '.join('D%d' % n for n in sorted(have))})", f, l)

    # ------------------------------------------------------------------------- REFS
    def r_refs(self):
        c = self.content
        if not c:
            return
        ids = self.page_ids
        for tok in c.get("refs", {}).get("unlinked", []):
            f, l = self.locate_token(tok)
            self.add("REFS", "unlinked", "error", f"reference token {tok} has no definition on the page (no element with id {token_id(tok)} and no refs "
                     f"family in meta.json): define it (a row, a decision, an {{#{token_id(tok)}}}) or reword", f, l)
        bad = {}
        for path, parent, k, v in walk([c.get("sections", []), c.get("lede", [])]):
            for m in re.finditer(r'href="#([^"]*)"', v):
                t = html.unescape(m.group(1))
                if t not in ids:
                    bad.setdefault(t, parent.get("type") or k)
        for t, where in bad.items():
            f, l = self.locate("#" + t + ")") if t else (None, None)
            if not f:
                f, l = self.locate("#" + t) if t else (None, None)
            self.add("REFS", "broken-anchor", "error", f"a link in a {where} points at #{t}, which is not an anchor on the page", f, l)
        tables = {t.get("id") for t in c.get("schema", [])}
        for n in nodes(c.get("sections", [])):
            if n.get("type") != "diagram":
                continue
            hfile = os.path.join(self.root, "diagrams", n.get("key", "") + ".hotspots.json")
            for hs in n.get("hotspots", []):
                lab = hs.get("label", "")
                f, l = self.locate_label(lab, hfile) if os.path.isfile(hfile) else (None, None)
                href = hs.get("href") or ""
                if href.startswith("#") and href[1:] not in ids:
                    self.add("REFS", "hotspot-anchor", "error", f"hotspot '{lab}' on {n.get('key')} links to {href}, which is not an anchor on the page", f, l)
                if hs.get("table") and hs["table"] not in tables:
                    self.add("REFS", "hotspot-anchor", "error", f"hotspot '{lab}' on {n.get('key')} opens table {hs['table']}, which is not in schema/", f, l)
        mp = os.path.join(self.root, "meta.json")
        for fam, href in ((self.meta or {}).get("refs") or {}).items():
            if not isinstance(href, str) or not href.startswith("#") or href[1:] not in ids:
                f, l = self.locate('"' + fam + '"', [mp])
                self.add("REFS", "family", "error", f"meta.json refs family {fam} points at {href!r}, which is not an anchor on the page", f, l)
        # tokens the compiler leaves unlinked by position: titles, legend labels, questions, headings and summaries in HTML
        skip_keys = {"did", "id", "key", "src", "href", "alt", "text", "tone", "cls", "type", "file", "lang", "chipTone", "titleText", "term", "ddl",
                     "image", "table", "label_id", "grid", "size"}
        seen = set()
        for path, parent, k, v in walk(c.get("sections", [])):
            if k in skip_keys or not v or any(pk == "subs" for _, pk in path):
                continue
            txt = unlinked_text(v)
            toks = [m.group(1) for m in TOKEN.finditer(txt)]
            if not toks:
                continue
            kind = kind_of(parent, k)
            own = token_id(parent.get("did", "")) if parent.get("did") else parent.get("id")
            under = next((pn.get("type") or "node" for pn, pk in path if pk == "text"), None)
            for tok in toks:
                if token_id(tok) == own:
                    continue
                key = (tok, kind, k, plain(v)[:60])
                if key in seen:
                    continue
                seen.add(key)
                files = (self.prs + [self.src]) if kind.startswith("PR") and self.src else None
                f, l = self.locate_text(plain(v), files)
                undefined = "" if token_id(tok) in ids else "; it has no definition either"
                if k in LINK_KEYS and under:
                    self.add("REFS", "missed-link", "error", f"{tok} is not linked in a {kind} inside a {under}'s text ({plain(v)[:60]!r}): the compiler's "
                             f"link_tree does not descend into '{under}.text' (compiler fix){undefined}", f, l)
                elif k in LINK_KEYS:
                    self.add("REFS", "title-token", "warn", f"{tok} is not linked in {kind}.{k} ({plain(v)[:60]!r}): it sits in a heading, summary or "
                             f"term the compiler skips by design{undefined}", f, l)
                else:
                    self.add("REFS", "title-token", "warn", f"{tok} is not linked in the {kind} {k} {plain(v)[:60]!r}: the compiler does not link "
                             f"{kind}.{k}{undefined}", f, l)

    # ------------------------------------------------------------------------- CITES
    def r_cites(self):
        c = self.content
        cites = c.get("cites", []) if c else []
        meta = self.meta or {}
        uniq = {}
        for x in cites:
            uniq.setdefault(x.get("text"), x)
        repo = meta.get("repo_path")
        if not repo:
            if uniq:
                self.add("CITES", "no-repo", "warn", f"NEEDS_CONTEXT: {len(uniq)} citations not checked: meta.json has no repo_path (add repo_path, "
                         f"repo_url and sha so path:line citations can be verified and linked)", "meta.json" if self.meta is not None else None)
            return
        repo = os.path.abspath(os.path.expanduser(repo))
        rc, out = git(repo, "rev-parse", "--is-inside-work-tree")
        if rc != 0 or out.strip() != b"true":
            self.add("CITES", "no-repo", "warn", f"NEEDS_CONTEXT: repo_path {self.rel(repo)} is not a git checkout here; {len(uniq)} citations not checked",
                     *self.locate('"repo_path"', [os.path.join(self.root, "meta.json")]))
            return
        mpath = os.path.join(self.root, "meta.json")
        url = meta.get("repo_url")
        if url:
            rc, origin = git(repo, "remote", "get-url", "origin")
            if rc == 0:
                o = re.sub(r"(\.git)?/?$", "", origin.decode().strip())
                o = re.sub(r"^git@([^:]+):", r"https://\1/", o)
                if norm(o) != norm(re.sub(r"/?$", "", url)):
                    self.add("CITES", "repo-url", "warn", f"repo_url {url} is not repo_path's origin ({o}): citations link to one repo and are checked in another",
                             *self.locate('"repo_url"', [mpath]))
        sha, ref, verified = meta.get("sha"), None, False
        if sha:
            rc, kind = git(repo, "cat-file", "-t", sha)
            if rc == 0 and kind.strip() == b"commit":
                ref, verified = sha, True
                rc, d = git(repo, "show", "-s", "--format=%cs", sha)
                real = d.decode().strip() if rc == 0 else ""
                if meta.get("sha_date") and real and meta["sha_date"] != real:
                    self.add("CITES", "sha-date", "warn", f"sha_date {meta['sha_date']} but commit {sha} is dated {real}", *self.locate('"sha_date"', [mpath]))
                elif not meta.get("sha_date"):
                    self.add("CITES", "sha-date", "warn", f"meta.json has no sha_date (commit {sha} is dated {real})", *self.locate('"sha"', [mpath]))
            else:
                self.add("CITES", "sha-unknown", "warn", f"NEEDS_CONTEXT: meta.sha {sha} is not a commit in {self.rel(repo)} (a clone older than the sha, or a "
                         f"wrong sha; `git -C {self.rel(repo)} fetch origin` then rerun): citations are checked at HEAD instead, as warnings",
                         *self.locate('"sha"', [mpath]))
        else:
            self.add("CITES", "sha-unknown", "warn", "meta.json has no sha: citations are not linked and are checked at HEAD, as warnings", "meta.json")
        if not ref:
            rc, h = git(repo, "rev-parse", "--short=10", "HEAD")
            if rc != 0:
                return
            ref = h.decode().strip()
        rc, tree = git(repo, "ls-tree", "-r", "--name-only", ref)
        if rc != 0:
            self.add("CITES", "no-repo", "warn", f"git ls-tree {ref} failed in {self.rel(repo)}: {tree.decode(errors='ignore').strip()[:120]}", "meta.json")
            return
        files = set(tree.decode("utf-8", "surrogateescape").splitlines())
        by_base = {}
        for p in files:
            by_base.setdefault(p.rsplit("/", 1)[-1], []).append(p)
        sev = "error" if verified else "warn"
        at = "" if verified else f" [HEAD fallback]"
        nlines = {}
        def count(path):
            if path not in nlines:
                rc, data = git(repo, "show", f"{ref}:{path}")
                nlines[path] = (data.count(b"\n") + (1 if data and not data.endswith(b"\n") else 0)) if rc == 0 else None
            return nlines[path]
        checked = 0
        wt_hits = []
        self.citations = getattr(self, "citations", [])
        def rec(x, path, how):
            self.citations.append({"path": path, "from": x.get("from"), "to": x.get("to"), "section": x.get("section"), "resolved": how,
                                   "checked_sha": ref if how in ("sha", "head") else None})
        for text, x in uniq.items():
            path, a, b = x.get("path") or "", x.get("from"), x.get("to")
            f, l = self.locate("`" + text + "`")
            ext = path.rsplit(".", 1)[-1].lower() if "." in path else ""
            n = None
            local = next((p for p in (os.path.join(self.root, path), os.path.join(self.build, path)) if os.path.isfile(p)), None)
            if os.path.isabs(os.path.expanduser(path)) and os.path.isfile(os.path.expanduser(path)):
                local = os.path.expanduser(path)
            if local:
                n = len(open(local, "rb").read().split(b"\n"))
                rec(x, path, "packet")
            elif path in files:
                n = count(path)
                checked += 1
                rec(x, path, "sha" if verified else "head")
            elif not verified and os.path.isfile(os.path.join(repo, path)):
                # untracked or newer than HEAD: check the working tree and say so once
                n = len(open(os.path.join(repo, path), "rb").read().split(b"\n"))
                checked += 1
                wt_hits.append(path)
                rec(x, path, "working_tree")
            else:
                rec(x, path, "missing")
                suffix = sorted(p for p in by_base.get(path.rsplit("/", 1)[-1], []) if p.endswith("/" + path))
                if suffix:
                    # a short form of a real file: the link the compiler builds (repo_url/blob/sha/<short>) is dead at any commit
                    hint = f"did you mean {suffix[0]}" if len(suffix) == 1 else f"{len(suffix)} files end with it ({', '.join(suffix[:2])}, ...)"
                    if a:
                        self.add("CITES", "short-path", "error", f"`{text}` is a short path, so its line link is dead: {hint}; write the full path or an "
                                 f"alias from meta.path_aliases", f, l)
                    else:
                        self.add("CITES", "short-path", "warn", f"`{text}` is a short path a reader cannot open: {hint}; write the full path or an alias", f, l)
                elif a:
                    self.add("CITES", "path-unknown", sev, f"`{text}`: {path} is not a file in {self.rel(repo)} at {ref}, so the line link is dead{at}", f, l)
                else:
                    self.add("CITES", "path-unknown", sev, f"`{text}`: {path} is not a file in {self.rel(repo)} at {ref} (if the design adds this file, name "
                             f"it in prose rather than as a citation){at}", f, l)
                continue
            if a:
                hi = b or a
                if n is not None and (a < 1 or hi < a or hi > n):
                    self.add("CITES", "line-range", sev, f"`{text}`: lines {a}{'-' + str(b) if b else ''} are outside {path}, which has {n} lines at "
                             f"{ref if not local else 'the packet copy'}{at}", f, l)
            elif ext in CODE_EXT:
                self.add("CITES", "no-line", "warn", f"`{text}` cites a code file without a line or range (write path:line or path:from-to)", f, l)
        if wt_hits:
            self.add("CITES", "working-tree", "warn", f"{len(set(wt_hits))} cited file(s) are not in {ref} but exist in {self.rel(repo)}'s working tree (uncommitted); "
                     f"checked there: commit and set meta.sha so the links resolve", "meta.json")
        if uniq:
            self.note(f"CITES: {len(uniq)} distinct citations, {checked} resolved in {self.rel(repo)} at {ref}"
                      f"{'' if verified else ' (HEAD fallback)'}{f', {len(set(wt_hits))} in the working tree' if wt_hits else ''}")

    # ------------------------------------------------------------------------- DIAG
    def shapes(self, svg):
        """Labels and texts of the shapes in a tldraw svg, by diagram_hotspots.py's own extractor."""
        mod = _load("diagram_hotspots", os.path.join(HERE, "diagram_hotspots.py"))
        if mod is None or not hasattr(mod, "Shapes"):
            return None
        p = mod.Shapes()
        try:
            p.feed(open(svg, encoding="utf-8").read())
        except (OSError, UnicodeDecodeError):
            return None
        labels, texts = set(), []
        for s in p.shapes:
            text = re.sub(r"[ \t]+", " ", "".join(s["text"])).strip()
            if not text:
                continue
            labels.add((s["strong"][0] if s["strong"] else text.split("\n")[0])[:120])
            texts.append(text)
        return labels, texts

    def r_diag(self):
        ddir = os.path.join(self.root, "diagrams")
        keys = []
        if self.src:
            for ln, line in outside_fences(self.lines(self.src)):
                m = MARK.match(line)
                if m and m.group(1).lower() == "diagram":
                    key, title = split_marker(m.group(2))
                    keys.append((key, ln))
        seen = {}
        for key, ln in keys:
            if key in seen:
                self.add("DIAG", "duplicate", "warn", f"diagram {key} is embedded twice (first at line {seen[key]}); embed a figure once and link to it",
                         self.rel(self.src), ln)
                continue
            seen[key] = ln
            src = next((os.path.join(ddir, key + e) for e in (".svg", ".png") if os.path.isfile(os.path.join(ddir, key + e))), None)
            if not src:
                self.add("DIAG", "missing", "error", f"[diagram: {key}] has no diagrams/{key}.svg or .png", self.rel(self.src), ln)
                continue
            mmd = os.path.join(ddir, key + ".mmd")
            if os.path.isfile(mmd) and os.path.getmtime(mmd) > os.path.getmtime(src) + 1:
                self.add("DIAG", "stale-svg", "warn", f"diagrams/{key}.mmd is newer than {self.rel(src)}: redraw the diagram", self.rel(mmd))
            hpath = os.path.join(ddir, key + ".hotspots.json")
            if not os.path.isfile(hpath):
                continue
            spec = self.load_json(hpath, "DIAG")
            if not isinstance(spec, dict):
                continue
            hrel = self.rel(hpath)
            if src.endswith(".svg"):
                vb, hvb = svg_viewbox(src), spec.get("viewBox")
                if vb and hvb and (len(hvb) != 4 or any(abs(float(p) - q) > 0.05 for p, q in zip(hvb, vb))):
                    self.add("DIAG", "viewbox", "error", f"{key}.hotspots.json viewBox {hvb} is not the svg's {vb}: the svg was redrawn; rerun "
                             f"diagram_hotspots.py --write", hrel, self.locate('"viewBox"', [hpath])[1])
            annotated = [b for b in spec.get("boxes", []) if b.get("href") or b.get("image") or b.get("note") or b.get("table")]
            sh = self.shapes(src) if (annotated and src.endswith(".svg")) else None
            if annotated and src.endswith(".svg") and sh is None:
                self.note(f"DIAG: diagram_hotspots.py could not be imported; hotspot labels on {key} not checked")
            for b in annotated:
                lab = b.get("label", "")
                _, hl = self.locate_label(lab, hpath)
                if sh and lab not in sh[0] and not any(lab and lab in t for t in sh[1]):
                    self.add("DIAG", "hotspot-label", "error", f"hotspot '{lab}' is not a box in {key}.svg any more: rerun diagram_hotspots.py --write "
                             f"and move its href/image/note/table to the right box", hrel, hl)
                if b.get("image") and not os.path.isfile(os.path.join(self.root, b["image"])):
                    self.add("DIAG", "hotspot-image", "error", f"hotspot '{lab}' shows image {b['image']}, which does not exist", hrel, hl)
                if b.get("table") and not os.path.isfile(os.path.join(self.root, "schema", b["table"] + ".json")):
                    self.add("DIAG", "hotspot-table", "error", f"hotspot '{lab}' opens table {b['table']}, which has no schema/{b['table']}.json", hrel, hl)
        # legend tones as the compiler resolved them
        for n in nodes((self.content or {}).get("sections", [])):
            items = n.get("legend") if n.get("type") == "diagram" else n.get("items") if n.get("type") == "legend" else None
            for it in items or []:
                classes = (it.get("tone") or "").split()
                dup = sorted({x for x in classes if classes.count(x) > 1})
                unknown = [x for x in classes if x not in TONE_CLASSES]
                if dup or unknown:
                    lab = plain(it.get("label", ""))
                    f, l = self.locate(re.compile(r"\[legend:.*" + re.escape(lab[:30])))
                    what = f"repeats {', '.join(dup)}" if dup else f"has unknown tone {', '.join(unknown)}"
                    self.add("DIAG", "legend-tone", "warn", f"legend item '{lab}' resolves to tone '{it.get('tone')}', which {what} (e.g. `= orange dashed` "
                             f"already means dashed: write `= orange`)", f, l)

    # ------------------------------------------------------------------------- SCHEMA
    def r_schema(self):
        sdir = os.path.join(self.root, "schema")
        if not os.path.isdir(sdir):
            return
        c = self.content or {}
        page_text = " ".join(plain(v) for _, _, k, v in walk(c.get("sections", [])) if k not in ("src", "href"))
        hs_tables = set()
        for f in sorted(os.listdir(os.path.join(self.root, "diagrams"))) if os.path.isdir(os.path.join(self.root, "diagrams")) else []:
            if f.endswith(".hotspots.json"):
                try:
                    hs_tables |= {b.get("table") for b in json.load(open(os.path.join(self.root, "diagrams", f))).get("boxes", []) if b.get("table")}
                except (ValueError, OSError):
                    pass
        for fn in sorted(os.listdir(sdir)):
            if not fn.endswith(".json"):
                continue
            p = os.path.join(sdir, fn)
            rp = self.rel(p)
            t = self.load_json(p, "SCHEMA")
            if t is None:
                continue
            if not isinstance(t, dict):
                self.add("SCHEMA", "shape", "error", f"{fn} is not a JSON object", rp, 1)
                continue
            at = lambda s: self.locate(s, [p])[1]
            for k in ("table", "state", "columns"):
                if k not in t:
                    self.add("SCHEMA", "required", "error", f"{fn} has no '{k}' (required: table, state, columns[])", rp, 1)
            name = t.get("table") or fn[:-5]
            if "state" in t and t["state"] not in ("new", "changed", "existing"):
                self.add("SCHEMA", "state", "error", f"{name}: state '{t['state']}' is not new, changed or existing", rp, at('"state"'))
            cols = t.get("columns")
            if "columns" in t and not isinstance(cols, list):
                self.add("SCHEMA", "required", "error", f"{name}: columns is not a list", rp, at('"columns"'))
                cols = []
            names = []
            for col in cols or []:
                if not isinstance(col, dict) or not col.get("name"):
                    self.add("SCHEMA", "column", "error", f"{name}: a column has no name", rp, at('"columns"'))
                    continue
                names.append(col["name"])
                cl = at('"name": ' + json.dumps(col["name"]))
                if not col.get("type") and not (col.get("note") or "").strip():
                    self.add("SCHEMA", "column", "warn", f"{name}.{col['name']} has no type and no note saying why (D7: write the type, or note "
                             f"that the source does not state it)", rp, cl)
                if "change" in col and col["change"] not in ("new", "changed", "dropped", "unchanged"):
                    self.add("SCHEMA", "column", "error", f"{name}.{col['name']}: change '{col['change']}' is not new, changed, dropped or unchanged", rp, cl)
                if col.get("change") == "changed" and not col.get("was"):
                    self.add("SCHEMA", "column", "warn", f"{name}.{col['name']} is changed but has no 'was' (what it is today)", rp, cl)
                if t.get("state") == "new" and col.get("change") in ("changed", "dropped", "unchanged"):
                    self.add("SCHEMA", "column", "error", f"{name} is a new table but column {col['name']} is '{col['change']}'", rp, cl)
            if t.get("state") == "changed" and cols and all(isinstance(x, dict) and x.get("change", "unchanged") == "unchanged" for x in cols):
                self.add("SCHEMA", "column", "warn", f"{name} is 'changed' but no column is new, changed or dropped", rp, at('"state"'))
            for i, row in enumerate(t.get("sample_rows") or [], 1):
                if not isinstance(row, dict):
                    self.add("SCHEMA", "sample-rows", "error", f"{name}: sample row {i} is not an object", rp, at('"sample_rows"'))
                    continue
                extra = [k for k in row if k not in names]
                if extra:
                    self.add("SCHEMA", "sample-rows", "error", f"{name}: sample row {i} has keys that are not columns: {', '.join(extra)}", rp,
                             at('"' + extra[0] + '"') or at('"sample_rows"'))
            if t.get("sample_rows") and not t.get("sample_source"):
                self.add("SCHEMA", "sample-rows", "warn", f"{name}: sample_rows have no sample_source (where they came from, or 'illustrative')", rp, at('"sample_rows"'))
            for k, m in (t.get("measured") or {}).items():
                if not isinstance(m, dict):
                    self.add("SCHEMA", "measured", "error", f"{name}: measured.{k} is not an object", rp, at('"' + k + '"'))
                    continue
                if m.get("value") is None:
                    continue
                miss = [x for x in ("source", "date") if not m.get(x)]
                if miss:
                    self.add("SCHEMA", "measured", "error", f"{name}: measured.{k} = {m.get('value')} has no {' or '.join(miss)} (D7: a measurement names where "
                             f"and when it was taken; otherwise leave it out and the drawer reads 'not measured')", rp, at('"' + k + '"'))
                elif not ISO_DATE.match(str(m.get("date"))):
                    self.add("SCHEMA", "measured", "error", f"{name}: measured.{k} date '{m.get('date')}' is not YYYY-MM-DD", rp, at('"' + k + '"'))
            for k, e in (t.get("expected") or {}).items():
                if not isinstance(e, dict):
                    self.add("SCHEMA", "expected", "error", f"{name}: expected.{k} is not an object", rp, at('"' + k + '"'))
                    continue
                v = e.get("value")
                numeric = isinstance(v, (int, float)) and not isinstance(v, bool) or (isinstance(v, str) and re.search(r"\d", v))
                if numeric and not (e.get("note") or "").strip():
                    self.add("SCHEMA", "expected", "error", f"{name}: expected.{k} = {v} has no note giving its basis (D7: an expectation says how it was "
                             f"estimated)", rp, at('"' + k + '"'))
            if name not in hs_tables and name not in page_text:
                self.add("SCHEMA", "unused", "warn", f"table {name} is neither a hotspot table on a diagram nor named on the page", rp, 1)

    # ------------------------------------------------------------------------- FILES
    def r_files(self):
        man = self.manifest
        self.placeholders(man)
        if man is None:
            return
        mrel = "build/content.files.json" if not self.compiled else "(compiled) content.files.json"
        files = man.get("files") or {}
        for p in man.get("missing") or []:
            f, l = self.locate(p)
            self.add("FILES", "missing", "error", f"{p} is referenced by the page but not found", f or mrel, l)
        for pub, src in files.items():
            if not src or not os.path.isfile(src):
                self.add("FILES", "missing", "error", f"{pub}: manifest source {self.rel(src)} does not exist", mrel)
            elif os.path.getsize(src) > FILE_LIMIT:
                self.add("FILES", "too-big", "error", f"{pub} is {os.path.getsize(src) / 1048576:.1f} MB; the artifact limit per file is 15 MB", mrel)
        for p in man.get("over_15mb") or []:
            if p not in files:
                self.add("FILES", "too-big", "error", f"{p} is over 15 MB", mrel)
        if not self.compiled:
            page = os.path.join(self.build, man.get("page") or "tdd.html")
            if os.path.isfile(page):
                size = os.path.getsize(page)
                if size > PAGE_LIMIT:
                    self.add("FILES", "page-size", "error", f"{self.rel(page)} is {size / 1048576:.1f} MB; the page limit is 16 MB", self.rel(page))
            else:
                self.note(f"FILES: {self.rel(page)} is not built yet (app/build.ts writes it); page size not checked")
            pb = man.get("page_bytes")
            if isinstance(pb, (int, float)) and pb > PAGE_LIMIT:
                self.add("FILES", "page-size", "error", f"page_bytes {pb} is over 16 MB", mrel)
        # orphan media: published by no manifest of any page in the packet, and shown by no hotspot
        used = set()
        manifests = [man] + [self.load_json(os.path.join(self.root, f), "FILES") for f in os.listdir(self.root) if f.endswith(".files.json")]
        for m in manifests:
            for pub, src in ((m or {}).get("files") or {}).items():
                used.add(os.path.realpath(os.path.join(self.root, pub)))
                if src:
                    used.add(os.path.realpath(src))
        ddir = os.path.join(self.root, "diagrams")
        if os.path.isdir(ddir):
            for f in os.listdir(ddir):
                if f.endswith(".hotspots.json"):
                    try:
                        for b in json.load(open(os.path.join(ddir, f))).get("boxes", []):
                            if b.get("image"):
                                used.add(os.path.realpath(os.path.join(self.root, b["image"])))
                    except (ValueError, OSError):
                        pass
        for sub in ("mockups", "diagrams"):
            d = os.path.join(self.root, sub)
            if not os.path.isdir(d):
                continue
            for dirpath, _, fns in os.walk(d):
                for fn in sorted(fns):
                    p = os.path.join(dirpath, fn)
                    if not fn.lower().endswith(MEDIA_EXT) or os.path.realpath(p) in used:
                        continue
                    rp = self.rel(p)
                    f, l = self.locate(rp)
                    why = f"; it is written at {f}:{l} but the page did not pick it up" if f else ""
                    self.add("FILES", "orphan", "warn", f"{rp} is in the packet but on no page{why}", rp)
                    if os.path.getsize(p) > FILE_LIMIT:
                        self.add("FILES", "too-big", "error", f"{rp} is over 15 MB", rp)
        loaded = set()
        def _prs(node):
            if isinstance(node, dict):
                if node.get("type") == "prs":
                    for g in node.get("groups", []):
                        for q in g.get("prs", []):
                            if q.get("file"):
                                loaded.add(os.path.basename(q["file"]))
                for v in node.values():
                    _prs(v)
            elif isinstance(node, list):
                for x in node:
                    _prs(x)
        _prs((self.content or {}).get("sections", []))
        for p in self.prs:
            fn = os.path.basename(p)
            if not re.fullmatch(r"pr-\d+[a-z]?\.md", fn):
                self.add("FILES", "pr-unloaded", "warn", f"prs/{fn} is not named pr-NN.md or pr-NNa.md (a sub-PR), so the PR loader skips it and it is not on the page", self.rel(p))
            elif loaded and fn not in loaded:
                self.add("FILES", "pr-unloaded", "error", f"prs/{fn} is a PR file but the page has no card for it (the manifest skipped it)", self.rel(p))

    def placeholders(self, man):
        """FILES/placeholder: a file the page publishes (the manifest's files) still carries the scaffold's marker.
        FILES/placeholder-text: a line of the source or of status.md (one-line comments aside) still carries a scaffold
        placeholder. The text check needs no manifest."""
        mark = PLACEHOLDER_MARK.encode("utf-8")
        for pub, src in (man.get("files") or {}).items() if man is not None else ():
            if not src or not os.path.isfile(src):
                continue
            try:
                data = open(src, "rb").read()
            except OSError:
                continue
            at = data.find(mark)
            if at >= 0:
                # the build writes resolved paths: name the file relative to the packet even when its path goes through a link
                real, root = os.path.realpath(src), os.path.realpath(self.root)
                where = os.path.relpath(real, root) if real.startswith(root + os.sep) else self.rel(src)
                self.add("FILES", "placeholder", "error", f'{pub} is still the scaffold\'s placeholder image (it says "{PLACEHOLDER_MARK}"): replace it '
                         f"with the real diagram or shot before publishing", where, data.count(b"\n", 0, at) + 1)
        for path in ([self.src] if self.src else []) + [os.path.join(self.root, "status.md")]:
            if not os.path.isfile(path):
                continue
            for i, line in enumerate(self.lines(path), 1):
                if COMMENT.match(line):
                    continue
                found = []
                for m in PLACEHOLDER.finditer(line):
                    if m.group(0) not in found:
                        found.append(m.group(0))
                if found:
                    names = ", ".join(f'"{t}"' for t in found)
                    self.add("FILES", "placeholder-text", "error", f"scaffold placeholder left in the text: {names}; write the real text (or delete the "
                             f'line) before publishing. The scaffold\'s placeholders are the "{PLACEHOLDER_MARK}" marker, the {PLACEHOLDER_DATE} date '
                             f"and the template's <angle-bracket> prompts", self.rel(path), i)

    # ------------------------------------------------------------------------- META
    def r_meta(self):
        mp = os.path.join(self.root, "meta.json")
        m = self.meta
        if m is None:
            if not os.path.isfile(mp):
                self.add("META", "missing", "error", "the packet has no meta.json (title, people, updated, rev, status)", None)
            return
        at = lambda k: self.locate('"' + k + '"', [mp])[1]
        if not (isinstance(m.get("title"), str) and m["title"].strip()):
            self.add("META", "field", "error", "meta.json has no title (the browser tab title)", "meta.json", at("title"))
        people = m.get("people")
        if not isinstance(people, list) or not any(isinstance(p, dict) and str(p.get("role", "")).lower() == "author" and p.get("name") for p in people):
            self.add("META", "field", "error", "meta.json people has no Author (people: [{role: Author, name: ...}])", "meta.json", at("people"))
        if not (isinstance(m.get("updated"), str) and ISO_DATE.match(m["updated"])):
            self.add("META", "field", "error", f"meta.json updated {m.get('updated')!r} is not an ISO date (YYYY-MM-DD)", "meta.json", at("updated"))
        if not (isinstance(m.get("rev"), int) and not isinstance(m.get("rev"), bool)):
            self.add("META", "field", "error", f"meta.json rev {m.get('rev')!r} is not an integer", "meta.json", at("rev"))
        status = m.get("status")
        if not (isinstance(status, str) and status.strip()):
            self.add("META", "field", "error", "meta.json has no status (e.g. 'Draft for review', 'Reviewed', 'As built')", "meta.json", at("status"))
        # rev / updated must move when the source changed after the last build
        cpath = os.path.join(self.build, "content.json")
        if not self.compiled and os.path.isfile(cpath) and self.content:
            built = os.path.getmtime(cpath)
            srcs = [p for p in self.text_files() + [mp] if p and os.path.isfile(p)]
            for sub in ("schema", "diagrams", "mockups"):
                d = os.path.join(self.root, sub)
                if os.path.isdir(d):
                    srcs += [os.path.join(d, f) for f in os.listdir(d)]
            newer = sorted((p for p in srcs if os.path.getmtime(p) > built + 1), key=os.path.getmtime, reverse=True)
            if newer:
                names = ", ".join(self.rel(p) for p in newer[:4]) + (f" and {len(newer) - 4} more" if len(newer) > 4 else "")
                self.add("META", "stale-build", "warn", f"{names} changed after the last build ({datetime.datetime.fromtimestamp(built):%Y-%m-%d %H:%M}): "
                         f"rebuild before publishing (this lint read the old build)", self.rel(cpath))
                text_changed = [p for p in newer if p.endswith(".md")]
                cm = self.content.get("meta", {})
                if text_changed and cm.get("rev") == m.get("rev") and cm.get("updated") == m.get("updated"):
                    self.add("META", "rev-not-moved", "warn", f"the source changed since the build of rev {m.get('rev')} ({m.get('updated')}) but meta.json "
                             f"rev and updated did not move: bump rev and set updated (and rev_note) for this revision", "meta.json", at("rev"))
        # a published (non-draft) doc names its artifact
        if isinstance(status, str) and status.strip() and not status.strip().lower().startswith("draft"):
            urls = [m.get("artifact_url")]
            st = os.path.join(self.root, "status.md")
            if os.path.isfile(st):
                mm = re.search(r"artifact_url:\s*(https?://\S+)", open(st, encoding="utf-8").read())
                urls.append(mm.group(1) if mm else None)
            pk = os.path.join(self.root, "packet.json")
            if os.path.isfile(pk):
                try:
                    urls.append(json.load(open(pk)).get("artifact_url"))
                except (ValueError, OSError):
                    pass
            if not any(isinstance(u, str) and u.startswith("http") for u in urls):
                self.add("META", "artifact-url", "warn", f"status is '{status}' but no artifact_url is recorded (meta.json, or `artifact_url:` in status.md)",
                         "meta.json", at("status"))

    # ------------------------------------------------------------------------- AGENTS
    def r_agents(self):
        adir = os.path.join(self.build, "agents")
        if self.compiled or not os.path.isdir(adir):
            self.note(f"AGENTS: no {self.rel(adir)}; skipped")
            return
        mod = _load("write_agents", os.path.join(HERE, "write_agents.py"))
        fn = getattr(mod, "check", None) if mod else None
        if not callable(fn):
            self.note("AGENTS: scripts/write_agents.py " + ("has no check(build_dir)" if mod else "is absent or does not import") + "; skipped")
            return
        try:
            res = fn(self.build)
        except Exception as e:  # noqa: BLE001 - another owner's code; report, never crash the lint
            self.add("AGENTS", "check-failed", "error", f"write_agents.check raised {type(e).__name__}: {e}", self.rel(adir))
            return
        if isinstance(res, dict):
            res = res.get("problems") or res.get("findings") or []
        for p in res or []:
            if isinstance(p, dict):
                sev = "warn" if str(p.get("severity", "error")).lower().startswith("warn") else "error"
                self.add("AGENTS", p.get("check") or "problem", sev, p.get("message") or p.get("msg") or json.dumps(p), p.get("file") or self.rel(adir), p.get("line"))
            else:
                self.add("AGENTS", "problem", "error", str(p), self.rel(adir))

    # ------------------------------------------------------------------------- XDOC
    def glossary_of(self, d):
        """{term lower: (term, definition, file, line)} from every ::: glossary block in a packet's root markdown."""
        out = {}
        for fn in sorted(os.listdir(d)):
            p = os.path.join(d, fn)
            if not fn.endswith(".md") or not os.path.isfile(p):
                continue
            depth = None
            for ln, line in outside_fences(self.lines(p)):
                m = re.match(r"^(:{3,})\s*([a-z][\w-]*)?", line)
                if m:
                    if m.group(2) == "glossary" and depth is None:
                        depth = len(m.group(1))
                    elif depth is not None and not m.group(2) and len(m.group(1)) == depth:
                        depth = None
                    continue
                if depth is None:
                    continue
                g = re.match(r"^\s*[-*]\s+\*\*([^*]+)\*\*\s*[:—–-]?\s*(.*)$", line)
                if g:
                    out.setdefault(g.group(1).strip().lower(), (g.group(1).strip(), g.group(2).strip(), p, ln))
        return out

    def r_xdoc(self):
        parent = os.path.dirname(self.root)
        sibs = [os.path.join(parent, d) for d in sorted(os.listdir(parent)) if os.path.isdir(os.path.join(parent, d)) and os.path.join(parent, d) != self.root]
        mine = self.glossary_of(self.root)
        if not mine:
            self.note(f"XDOC: no ::: glossary in this packet; {len(sibs)} sibling packets under {self.rel(parent)} not compared")
            return
        clean = lambda s: norm(re.sub(r"[*_`]", "", s).rstrip("."))
        hits = 0
        for sd in sibs:
            theirs = self.glossary_of(sd)
            for k, (term, d1, f1, l1) in mine.items():
                if k in theirs and clean(theirs[k][1]) != clean(d1):
                    hits += 1
                    _, d2, f2, l2 = theirs[k]
                    self.add("XDOC", "glossary-conflict", "warn", f"glossary term '{term}' is defined differently in {self.rel(f2)}:{l2} ({d2[:60]!r}) than here "
                             f"({d1[:60]!r}); make one definition and use it in both", self.rel(f1), l1)
        self.note(f"XDOC: compared {len(mine)} glossary terms with {len(sibs)} sibling packets; D-numbers are per doc and are not compared")

    # ------------------------------------------------------------------------- PARITY
    def _compile_to(self, cmd, generated):
        """Run one compiler into a new temporary folder (--no-copy, TDD_GENERATED pinned): (folder, error or None). The source
        is passed relative to the packet folder, as the documented build does, so paths in warnings resolve the same way."""
        out = tempfile.mkdtemp(prefix="lint-tdd-parity-")
        env = dict(os.environ, TDD_GENERATED=generated)
        try:
            p = subprocess.run(cmd + [os.path.relpath(self.src, self.root), out, "--no-copy"], capture_output=True, text=True, cwd=self.root, env=env, timeout=300)
        except (OSError, subprocess.TimeoutExpired) as e:
            return out, f"{type(e).__name__}: {e}"
        if p.returncode != 0 or not os.path.isfile(os.path.join(out, "content.json")):
            tail = (p.stderr or p.stdout).strip().splitlines()[-1:] or [f"exit {p.returncode}"]
            return out, f"exit {p.returncode}: {tail[0]}"
        return out, None

    @staticmethod
    def _first_diff(a, b):
        n = min(len(a), len(b))
        i = next((k for k in range(n) if a[k] != b[k]), n)
        line = a[:i].count(b"\n") + 1
        ctx = lambda x: x[max(0, i - 30):i + 30].decode("utf-8", "replace")
        return f"first difference at byte {i} (line {line}; {len(a)} vs {len(b)} bytes): {ctx(a)!r} vs {ctx(b)!r}"

    @staticmethod
    def _manifest_diffs(ma, mb):
        """The manifest fields PARITY compares (files paths, missing, warnings) that differ, described."""
        diffs = []
        for k, f in (("files", lambda m: list((m.get("files") or {}).keys())), ("missing", lambda m: m.get("missing")), ("warnings", lambda m: m.get("warnings"))):
            x, y = f(ma), f(mb)
            if x != y:
                xs, ys = list(x or []), list(y or [])
                j = next((i for i in range(min(len(xs), len(ys))) if xs[i] != ys[i]), min(len(xs), len(ys)))
                diffs.append(f"{k} ({len(xs)} vs {len(ys)}; first at {j}: {xs[j] if j < len(xs) else None!r} vs {ys[j] if j < len(ys) else None!r})")
        return diffs

    def r_parity(self):
        """During the bun port's overlap period (D19) the TypeScript compiler must agree with build_tdd.py byte for byte.
        The build is the first reference; whenever it differs from the TypeScript compile (or the TypeScript compile fails),
        a fresh build_tdd.py compile, spelled the same way, decides whether the difference is the TypeScript's."""
        bun, ts = shutil.which("bun"), os.path.join(CLI, "bin", "build-tdd.ts")
        if not bun or not os.path.isfile(ts):
            self.note(f"PARITY: {'bun is not on PATH' if not bun else self.rel(ts) + ' is absent'}; the TypeScript compiler was not compared")
            return
        if not os.path.isdir(os.path.join(CLI, "node_modules")):
            self.note(f"PARITY: {self.rel(CLI)} has no node_modules (run `bun install --frozen-lockfile` there); the TypeScript compiler was not compared")
            return
        ref_dir = self.tmp if self.compiled else self.build
        cpath, mpath = os.path.join(ref_dir or "", "content.json"), os.path.join(ref_dir or "", "content.files.json")
        generated = self.content.get("generated") if isinstance(self.content, dict) else None
        if not self.src or not os.path.isfile(cpath) or not isinstance(generated, str) or not generated:
            self.note("PARITY: no source, no content.json or no generated stamp to compare with; skipped")
            return
        where = "(compiled) content.json" if self.compiled else self.rel(cpath)
        mfile = "(compiled) content.files.json" if self.compiled else self.rel(mpath)
        src = self.rel(self.src)
        tmps, fresh = [], []

        def python_fresh():
            if not fresh:
                pout, perr = self._compile_to([sys.executable, "-I", os.path.join(HERE, "build_tdd.py")], generated)
                tmps.append(pout)
                fresh.append((pout, perr))
            return fresh[0]

        def read_manifest(path):
            try:
                return json.load(open(path, encoding="utf-8"))
            except (OSError, ValueError):
                return None

        try:
            tout, err = self._compile_to([bun, "--no-install", ts], generated)
            tmps.append(tout)
            if err:
                pout, perr = python_fresh()
                if perr:
                    self.note(f"PARITY: build_tdd.py fails on the current source too ({perr}); the compilers were not compared")
                    return
                self.add("PARITY", "content", "error", f"the TypeScript compiler failed on {src} where build_tdd.py did not ({err})", src)
                return
            mine, mine_m = open(os.path.join(tout, "content.json"), "rb").read(), read_manifest(os.path.join(tout, "content.files.json"))
            ref_m = read_manifest(mpath)
            same_c = open(cpath, "rb").read() == mine
            m_diffs = self._manifest_diffs(ref_m, mine_m) if ref_m is not None and mine_m is not None else None
            if same_c and m_diffs == []:
                self.note(f"PARITY: the TypeScript compiler (cli/bin/build-tdd.ts) reproduced {where} byte for byte with TDD_GENERATED={generated}, "
                          f"and the manifest's files, missing and warnings")
                return
            # the build differs from the TypeScript compile: a fresh build_tdd.py compile decides whose difference it is
            pout, perr = python_fresh()
            if perr:
                self.add("PARITY", "content", "error", f"build_tdd.py fails on {src} ({perr}) where the TypeScript compiler succeeded", src)
                return
            fresh_c, fresh_m = open(os.path.join(pout, "content.json"), "rb").read(), read_manifest(os.path.join(pout, "content.files.json"))
            if fresh_c != mine:
                self.add("PARITY", "content", "error", f"the TypeScript compiler's content.json differs from build_tdd.py's on {src}: "
                         f"{self._first_diff(fresh_c, mine)}; fix cli/src/compile (build_tdd.py stays the reference until the cutover)", src)
                return
            d = self._manifest_diffs(fresh_m or {}, mine_m or {})
            if d:
                self.add("PARITY", "manifest", "warn", f"the TypeScript compiler's manifest differs from build_tdd.py's on {src}: {'; '.join(d)}", src)
                return
            why = (f"{where} differs from a fresh compile of the source (a stale build: rebuild)" if not same_c else
                   f"{mfile} is missing or unreadable" if m_diffs is None else
                   f"{mfile} differs from a fresh compile's ({'; '.join(m_diffs)}): a stale build, files found only under the build folder, "
                   f"or another spelling of the packet path")
            self.note(f"PARITY: {why}; the TypeScript compiler (cli/bin/build-tdd.ts) reproduced a fresh build_tdd.py compile byte for byte "
                      f"with TDD_GENERATED={generated}, and its manifest's files, missing and warnings")
        finally:
            for t in tmps:
                shutil.rmtree(t, ignore_errors=True)

    # ------------------------------------------------------------------------- run
    def run(self):
        try:
            for r in RULES:
                if r not in self.skip:
                    getattr(self, "r_" + r.lower())()
        finally:
            self.cleanup()
        return self.findings

def report(lint, out=sys.stdout):
    errs = sum(1 for f in lint.findings if f["severity"] == "error")
    warns = len(lint.findings) - errs
    print(f"lint_packet: {lint.root}", file=out)
    print(f"  build: {'(compiled to a temp folder)' if lint.compiled else lint.build}", file=out)
    for r in RULES:
        fs = [f for f in lint.findings if f["rule"] == r]
        if not fs:
            continue
        e = sum(1 for f in fs if f["severity"] == "error")
        print(f"\n{r}  {e} error{'s' * (e != 1)}, {len(fs) - e} warning{'s' * (len(fs) - e != 1)}", file=out)
        for f in sorted(fs, key=lambda f: f["severity"] != "error"):
            where = (f["file"] or "") + (f":{f['line']}" if f["file"] and f["line"] else "")
            print(f"  {f['severity']:<5} {f['check']:<26} {where + ': ' if where else ''}{f['message']}", file=out)
    if lint.notes:
        print("\nnotes:", file=out)
        for n in lint.notes:
            print(f"  - {n}", file=out)
    print(f"\n{errs} error{'s' * (errs != 1)}, {warns} warning{'s' * (warns != 1)}", file=out)
    return errs, warns

def main(argv):
    args, opts, i = [], {"build": None, "json": None, "skip": "", "strict": False, "compile": False}, 0
    while i < len(argv):
        x = argv[i]
        if x in ("--build", "--json", "--skip"):
            if i + 1 >= len(argv):
                print(f"{x} needs a value\n\n{__doc__}", file=sys.stderr)
                return 2
            opts[x[2:]] = argv[i + 1]
            i += 2
        elif x in ("--strict", "--compile"):
            opts[x[2:]] = True
            i += 1
        elif x in ("-h", "--help"):
            print(__doc__)
            return 0
        elif x.startswith("--"):
            print(f"unknown option {x}\n\n{__doc__}", file=sys.stderr)
            return 2
        else:
            args.append(x)
            i += 1
    if len(args) != 1 or not os.path.isdir(os.path.expanduser(args[0])):
        print(__doc__ if len(args) != 1 else f"{args[0]}: not a directory", file=sys.stderr)
        return 2
    skip = [s for s in opts["skip"].split(",") if s.strip()]
    lint = Lint(args[0], opts["build"], opts["compile"], skip)
    lint.run()
    errs, warns = report(lint)
    if opts["json"]:
        with open(opts["json"], "w", encoding="utf-8") as fh:
            json.dump({"packet": lint.root, "build": None if lint.compiled else lint.build, "compiled": lint.compiled, "findings": lint.findings,
                       "notes": lint.notes, "citations": getattr(lint, "citations", []), "counts": {"errors": errs, "warnings": warns,
                       "by_rule": {r: sum(1 for f in lint.findings if f["rule"] == r) for r in RULES}}}, fh, indent=1, ensure_ascii=False)
    return 1 if errs or (opts["strict"] and warns) else 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
