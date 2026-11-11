#!/usr/bin/env python3
"""Propose (and with --apply write) href / image / note for the boxes of a packet's diagram hotspot files (D4).

Usage: annotate_hotspots.py <packet-dir> [--build <dir>] [--apply] [--min-score 0.6] [--key <diagram>]
Reads <packet>/diagrams/*.hotspots.json (see diagram_hotspots.py), <build>/content.json (default <packet>/build:
`anchors` and `defs`) and the mock captions (![alt](mockups/x.jpg "title")) of the packet's markdown source.
For every box of kind box or frame it proposes:
  href   an anchor that exists in content.json. Exact table name -> #tbl-<slug>; one decision/row/PR token in the label
         (D1..D8, E1.., F1.., R1.., PR n) -> that id; a label that equals (after normalising) or closely resembles a
         persona / fold / heading / section / decision title -> that anchor.
  note   the target def's text, first ~160 characters, whenever an href is proposed.
  image  a mock (mockups/..jpg) whose caption shares at least two words with the label and most of them.
Scores: exact matches 1.0; token in label 0.9; fuzzy = difflib ratio or token overlap; token only in the box text 0.5.
Every proposal prints with its score and reason. --apply writes only proposals >= --min-score and only into fields
that are empty: an existing href / image / note / table is never overwritten. Full-diagram frames (over half the
viewBox) are skipped: a hotspot over the whole figure would swallow the boxes inside it. Boxes that already carry a
table keep their drawer and get no href. Stdlib only. Exit 0; 2 on bad input."""
import difflib, glob, json, os, re, sys

FUZZY_KINDS = {"persona", "fold", "heading", "section", "decision", "row", "pr", "milestone"}
STOP = {"the", "a", "an", "and", "or", "of", "to", "in", "on", "for", "from", "is", "it", "its", "s", "by", "with", "at", "as", "one", "up"}
TOKEN = re.compile(r"\b(PR\s?\d{1,2}|[DEFR]\d{1,2})\b", re.I)


def norm(s):
    s = re.sub(r"[^a-z0-9 ]+", " ", s.lower().replace("'", "").replace("’", ""))
    return re.sub(r"\s+", " ", s).strip()


def words(s):
    out = []
    for w in norm(s).split():
        if w in STOP or w.isdigit():
            continue
        if len(w) > 3 and w.endswith("s"):
            w = w[:-1]
        out.append(w)
    return out


def tok_id(t):
    t = re.sub(r"\s+", "", t.lower())
    return "pr-" + t[2:] if t.startswith("pr") else t


def snippet(text, n=160):
    text = re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", text or "")).strip()
    if len(text) <= n:
        return text
    cut = text[:n].rsplit(" ", 1)[0].rstrip(",;:. ")
    return cut + "…"


class Index:
    def __init__(self, content):
        self.anchors = {a["id"]: a for a in content["anchors"]}
        self.defs = content.get("defs", {})
        self.tables = {}
        for t in content.get("schema", []):
            self.tables[t["table"]] = t["id"]
        for a in content["anchors"]:
            if a["kind"] == "table" and a["id"].startswith("tbl-"):
                self.tables.setdefault(a["id"][4:].replace("-", "_"), a["id"])

    def note(self, aid):
        d = self.defs.get(aid)
        return snippet(d["text"]) if d and d.get("text") else ""

    def ok(self, href):
        return href.startswith("#") and href[1:] in self.anchors

    def titles(self):
        for a in self.anchors.values():
            if a["kind"] in FUZZY_KINDS:
                full = norm(a["title"])
                yield a["id"], full, norm(a["title"].split("·", 1)[1]) if "·" in a["title"] else ""


def mocks(md_path):
    out = []
    if not md_path or not os.path.isfile(md_path):
        return out
    for m in re.finditer(r'!\[([^\]]*)\]\(([^\s)]+)(?:\s+"([^"]*)")?\)', open(md_path, encoding="utf-8").read()):
        if "mockups/" in m.group(2):
            out.append({"src": m.group(2), "alt": m.group(1), "title": m.group(3) or ""})
    return out


def propose_href(b, ix):
    label = b["label"]
    base = re.sub(r"\s*[(·].*$", "", label).strip()
    # 1. tables
    if base in ix.tables:
        return "#" + ix.tables[base], 1.0, f"label is table {base}"
    first = re.split(r"\s+", label.strip())[0].strip(",;:")
    if first in ix.tables and len(label.split()) > 1:
        return "#" + ix.tables[first], 0.55, f"label starts with table {first} but names more (not exact)"
    # 2. decision / row / PR tokens
    toks = {tok_id(t) for t in TOKEN.findall(label)}
    toks = {t for t in toks if t in ix.anchors}
    if len(toks) == 1:
        t = toks.pop()
        return "#" + t, 0.9, f"label names {t.upper()}"
    if not toks:
        tt = {tok_id(t) for t in TOKEN.findall(b.get("text", ""))}
        tt = {t for t in tt if t in ix.anchors}
        if len(tt) == 1:
            t = tt.pop()
            return "#" + t, 0.5, f"only the box text mentions {t.upper()}"
    # 3. titles
    lab = re.sub(r"^\d+\s+", "", norm(label))
    if b.get("kind") == "frame" or len(lab.split()) < 2:
        return None
    best = (0.0, None, "")
    for aid, full, suffix in ix.titles():
        for cand, what in ((full, "title"), (suffix, "title after the dot")):
            if not cand:
                continue
            if lab == cand:
                s = 1.0
            else:
                r = difflib.SequenceMatcher(None, lab, cand).ratio()
                lw, cw = set(words(lab)), set(words(cand))
                j = len(lw & cw) / len(lw | cw) if lw | cw else 0
                if abs(len(lab) - len(cand)) > 0.5 * max(len(lab), len(cand)):
                    s = 0
                elif lw <= cw or cw <= lw:  # every content word of one is in the other
                    s = round(min(r, max(j, r - 0.15)), 2)
                else:  # a word differs (queue vs request): never confident
                    s = round(min(r, 0.55), 2)
            if s > best[0]:
                best = (s, aid, f"label ~ {what} of {aid}: {cand!r}")
    if best[1] and best[0] >= 0.5:
        return "#" + best[1], best[0], best[2]
    return None


def propose_image(b, ms):
    """A run of consecutive words shared with a caption title (alt text is ignored) that covers >= 60% of the label."""
    lw = words(re.sub(r"^\d+\s*\u00b7\s*", "", b["label"]))
    if len(lw) < 2:
        return None
    found = []
    for m in ms:
        cw = words(m["title"])
        mt = difflib.SequenceMatcher(None, lw, cw, autojunk=False).find_longest_match(0, len(lw), 0, len(cw))
        if mt.size >= 2 and mt.size / len(lw) >= 0.6:
            found.append((mt.size / len(lw), m, lw[mt.a:mt.a + mt.size]))
    found.sort(key=lambda x: -x[0])
    if found and (len(found) == 1 or found[0][0] > found[1][0]):
        sc, m, run = found[0]
        return m["src"], round(sc, 2), f"caption {m['title']!r} shares the run {' '.join(run)!r}"
    return None


def main(argv):
    args, apply_, min_score, build, key = [], False, 0.6, None, None
    it = iter(argv)
    for a in it:
        if a == "--apply":
            apply_ = True
        elif a == "--min-score":
            min_score = float(next(it))
        elif a == "--build":
            build = next(it)
        elif a == "--key":
            key = next(it)
        else:
            args.append(a)
    if len(args) != 1:
        sys.exit(__doc__)
    pk = os.path.abspath(args[0])
    build = os.path.abspath(build) if build else os.path.join(pk, "build")
    cj = os.path.join(build, "content.json")
    if not os.path.isfile(cj):
        print(f"no {cj}: compile the packet first", file=sys.stderr)
        return 2
    ix = Index(json.load(open(cj, encoding="utf-8")))
    md = next(iter(sorted(glob.glob(os.path.join(pk, "*.md")))), None)
    ms = mocks(md)
    counts = {"proposed": 0, "applied": 0}
    print(f"{'diagram':14} {'label':44} {'proposal':36} {'score':>5}  reason")
    for f in sorted(glob.glob(os.path.join(pk, "diagrams", "*.hotspots.json"))):
        k = os.path.basename(f)[: -len(".hotspots.json")]
        if key and k != key:
            continue
        spec = json.load(open(f, encoding="utf-8"))
        vb = spec.get("viewBox") or [0, 0, 1, 1]
        changed = False
        for b in spec["boxes"]:
            if b.get("kind") not in ("box", "frame"):
                continue
            if b["kind"] == "frame" and b["w"] * b["h"] > 0.5 * vb[2] * vb[3]:
                continue
            props = {}
            h = None if b.get("table") else propose_href(b, ix)
            if h and not ix.ok(h[0]):
                h = None
            if h:
                props["href"] = h
                n = ix.note(h[0][1:])
                if n:
                    props["note"] = (n, h[1], "def text of " + h[0])
            im = propose_image(b, ms)
            if im:
                props["image"] = im
            for fld, (val, score, why) in props.items():
                counts["proposed"] += 1
                held = b.get(fld)
                tag = "" if not held else " [kept: already set]"
                shown = val if fld != "note" else val[:30] + "…"
                print(f"{k:14} {b['label'][:44]:44} {fld + ' ' + shown:36} {score:5.2f}  {why}{tag}")
                if apply_ and score >= min_score and not held:
                    b[fld] = val
                    counts["applied"] += 1
                    changed = True
        if apply_ and changed:
            json.dump(spec, open(f, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print(f"proposals {counts['proposed']}; applied {counts['applied']} (min-score {min_score}{'' if apply_ else ', dry run'})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
