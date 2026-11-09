#!/usr/bin/env python3
"""List the boxes of a tldraw-exported SVG so they can become hover hotspots on the page (D4) and, for a schema
diagram, open a table drawer (D7).

Usage: diagram_hotspots.py <diagrams/key.svg> [--write]
Prints (or with --write creates or merges diagrams/<key>.hotspots.json) one entry per box: its label (the box's first
bold line), its text, position and size in viewBox units, the tone read from its stroke colour, and empty href /
image / note / table fields for the author to fill. build_tdd.py renders only entries that carry an href, image, note
or table; the rest are kept so a rerun after a redraw matches them by label and keeps what was filled in.
Exit 0; 1 on a file that is not a tldraw SVG export."""
import json, os, re, sys
from html.parser import HTMLParser

TONES = {"#099268": "green", "#9fa8b2": "grey", "#4465e9": "blue", "#f1ac4b": "amber", "#e03131": "red", "#e16919": "orange", "#ae3ec9": "violet", "#1d1d1d": "ink"}

class Shapes(HTMLParser):
    """Each tldraw shape is a <g transform="matrix(1,0,0,1,x,y)"> directly under <svg>: its geometry (paths or a
    rect) and a foreignObject with the rich text."""
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.depth, self.shapes, self.cur = 0, [], None
        self.in_fo, self.in_strong = 0, 0
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "g":
            self.depth += 1
            m = re.match(r"matrix\(\s*[\d.\-]+,\s*[\d.\-]+,\s*[\d.\-]+,\s*[\d.\-]+,\s*([\d.\-]+),\s*([\d.\-]+)\)", a.get("transform", ""))
            if self.depth == 1 and m:
                self.cur = {"x": float(m.group(1)), "y": float(m.group(2)), "boxes": [], "fo": None, "stroke": None, "dashed": False, "text": [], "strong": []}
                self.shapes.append(self.cur)
            return
        if self.cur is None:
            return
        if tag == "path" and a.get("d"):
            nums = [float(v) for v in re.findall(r"-?\d+(?:\.\d+)?", a["d"])]
            xs, ys = nums[0::2], nums[1::2]
            if len(xs) >= 2 and len(ys) >= 2:
                self.cur["boxes"].append((min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys)))
            if a.get("stroke") and a["stroke"] not in ("none",):
                self.cur["stroke"] = a["stroke"].lower()
            if a.get("stroke-dasharray"):
                self.cur["dashed"] = True
        elif tag == "rect":
            try:
                self.cur["boxes"].append((float(a.get("x", 0)), float(a.get("y", 0)), float(a.get("width", 0)), float(a.get("height", 0))))
            except ValueError:
                pass
        elif tag == "foreignobject":
            try:
                self.cur["fo"] = (float(a.get("x", 0)), float(a.get("y", 0)), float(a.get("width", 0)), float(a.get("height", 0)))
            except ValueError:
                pass
            self.in_fo += 1
        elif tag == "strong" and self.in_fo:
            self.in_strong += 1
        elif tag == "p" and self.in_fo:
            self.cur["text"].append("\n")
    def handle_endtag(self, tag):
        if tag == "g":
            self.depth -= 1
            if self.depth < 1:
                self.cur = None
        elif tag == "foreignobject" and self.in_fo:
            self.in_fo -= 1
        elif tag == "strong" and self.in_strong:
            self.in_strong -= 1
    def handle_data(self, data):
        if self.cur is not None and self.in_fo and data.strip():
            self.cur["text"].append(data)
            if self.in_strong:
                self.cur["strong"].append(data.strip())

def main(argv):
    write = "--write" in argv
    args = [x for x in argv if x != "--write"]
    if len(args) != 1:
        sys.exit(__doc__)
    path = args[0]
    src = open(path, encoding="utf-8").read()
    vb = re.search(r'viewBox="([\d.\-]+)[ ,]+([\d.\-]+)[ ,]+([\d.]+)[ ,]+([\d.]+)"', src)
    if not vb or "tl-" not in src:
        sys.exit(f"{path}: not a tldraw SVG export (no viewBox or tl- classes)")
    p = Shapes()
    p.feed(src)
    vbx = [float(x) for x in vb.groups()]
    boxes = []
    for s in p.shapes:
        text = re.sub(r"[ \t]+", " ", "".join(s["text"])).strip()
        if not text:
            continue
        geo = s["boxes"] or ([s["fo"]] if s["fo"] else [])
        if not geo:
            continue
        x, y, w, h = max(geo, key=lambda r: r[2] * r[3])
        if w <= 0 or h <= 0:
            continue
        area = w * h
        kind = "frame" if (area > 0.12 * vbx[2] * vbx[3] or (not s["boxes"] and w > 900)) else "text" if not s["boxes"] else "box"
        if kind == "text" and len(text) > 90:
            kind = "title"
        label = s["strong"][0] if s["strong"] else text.split("\n")[0]
        boxes.append({"label": label[:120], "kind": kind, "tone": TONES.get(s["stroke"] or "", "grey") + (" dashed" if s["dashed"] else ""),
                      "x": round(s["x"] + x, 1), "y": round(s["y"] + y, 1), "w": round(w, 1), "h": round(h, 1),
                      "text": text[:400], "href": "", "image": "", "note": "", "table": ""})
    out = {"svg": os.path.basename(path), "viewBox": vbx, "boxes": boxes}
    jpath = os.path.splitext(path)[0] + ".hotspots.json"
    if write:
        if os.path.isfile(jpath):
            old = json.load(open(jpath, encoding="utf-8"))
            by_label = {b["label"]: b for b in old.get("boxes", [])}
            for b in boxes:
                o = by_label.get(b["label"])
                if o:
                    for k in ("href", "image", "note", "table"):
                        b[k] = o.get(k, "")
        json.dump(out, open(jpath, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
        print(f"wrote {jpath}: {len(boxes)} shapes ({sum(1 for b in boxes if b['kind'] == 'box')} boxes, {sum(1 for b in boxes if b['href'] or b['image'] or b['note'] or b['table'])} annotated)")
    else:
        print(json.dumps(out, indent=1, ensure_ascii=False))

if __name__ == "__main__":
    main(sys.argv[1:])
