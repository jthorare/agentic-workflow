// STRUCT: the house sections in order, numbering against the source, an {#id} on every ##, explicit ids that survived,
// and no literal markdown left on the page (lint_packet.Lint.r_struct).
import { Effect } from "effect";
import * as py from "../../py";
import { Packet, type Loc, sid } from "../packet";
import { ATTRS } from "../patterns";
import { type PyDict, eq, get, iter, kind_of, norm, outside_fences, plain, repr, startsPR, str, truthy, walk } from "../pyops";

const re = py.re;
const ORDER = "overview, front end, rule coverage or requirements, end-to-end flow, decisions and open questions, for agents";
const STARS = re.compile("[*`]");
const ID_LINE = re.compile(String.raw`^\s*(#{2,6}\s|:{3,}\s*[a-z]|\\?\[diagram|\S)`);
const ID_PART = re.compile(String.raw`#[\w-]+`);
const CODE = re.compile(String.raw`<(code|pre)\b[^>]*>.*?</\1>`, re.S);
const PATS: readonly (readonly [string, py.re.Pattern])[] = [
  ["an image", re.compile(String.raw`!\[[^\]]*\]\([^)\s]+(?:\s+\"[^\"]*\")?\)`)],
  ["a link", re.compile(String.raw`(?<!!)\[[^\]\n]+\]\((?:https?://|#|\.{0,2}/|mockups/|diagrams/|prs/)[^)\s]*\)`)],
  ["bold markers", re.compile(String.raw`(?<![*\w])\*\*[^*\s][^*]*?\*\*(?![*\w])`)],
  ["a marker", re.compile(String.raw`\[(?:diagram|legend|reading|image|chip)\s*:`)],
  ["a container fence", re.compile(String.raw`(?:^|>)\s*:{3,}\s*[a-z]`)],
];
const RAW_SKIP = new Set(["text", "src", "alt", "href", "key", "ddl", "id", "file", "lang", "tone", "cls", "type"]);

export const struct = Effect.gen(function* () {
  const p = yield* Packet;
  const c = p.content;
  if (!truthy(c)) return;
  const secs = iter(get(c, "sections", [])) as PyDict[];
  const src_secs = p.source_sections();
  const by_title = new Map<string, number>();
  for (const [ln, t] of src_secs) by_title.set(norm(re.sub(STARS, "", t)), ln);
  const loc = new Map<unknown, Loc>();
  secs.forEach((s, k) => {
    const dflt = k < src_secs.length ? src_secs[k]![0] : null;
    const key = norm(get(s, "titleText"));
    loc.set(sid(s), [p.rel(p.src), by_title.has(key) ? by_title.get(key)! : dflt]);
  });
  type Placed = readonly [number, string, PyDict];
  const placed: Placed[] = [];
  for (const [name, hit] of p.slots) {
    if (!hit) {
      p.add("STRUCT", "missing-section", "error", `no '${name}' section (house order: ${ORDER}; matched by id or title)`, p.rel(p.src));
    } else placed.push([secs.indexOf(hit), name, hit]);
  }
  // the sections outside the longest run already in house order are the ones out of place
  const best: Placed[][] = placed.map((x) => [x]);
  for (let i = 0; i < placed.length; i++) {
    for (let j = 0; j < i; j++) {
      if (placed[j]![0] < placed[i]![0] && best[j]!.length + 1 > best[i]!.length) best[i] = [...best[j]!, placed[i]!];
    }
  }
  let keep = new Set<Placed>();
  if (best.length) {
    let top = best[0]!;
    for (const b of best) if (b.length > top.length) top = b;
    keep = new Set(top);
  }
  for (const x of placed) {
    if (!keep.has(x)) {
      const [f, l] = loc.get(sid(x[2])) ?? [null, null];
      p.add("STRUCT", "section-order", "warn",
        `section ${str(get(x[2], "num"))} '${str(get(x[2], "titleText"))}' (${x[1]}) is out of the house order (${ORDER})`, f, l);
    }
  }
  secs.forEach((s, i) => {
    const k = i + 1;
    if (!eq(get(s, "num"), k)) {
      p.add("STRUCT", "numbering", "error", `section '${str(get(s, "titleText"))}' is numbered ${str(get(s, "num"))} but is section ${k} in order`, "build/content.json");
    }
  });
  if (src_secs.length && secs.length) {
    if (src_secs.length !== secs.length) {
      p.add("STRUCT", "numbering", "error", `the source has ${src_secs.length} ## sections but content.json has ${secs.length}: the build is stale; rebuild`, p.rel(p.src));
    } else {
      src_secs.forEach(([ln, t], i) => {
        const s = secs[i]!;
        if (norm(re.sub(STARS, "", t)) !== norm(get(s, "titleText"))) {
          p.add("STRUCT", "numbering", "error", `section ${i + 1} is '${str(get(s, "titleText"))}' in content.json but '${t}' in the source: the build is stale; rebuild`,
            p.rel(p.src), ln);
        }
      });
    }
  }
  for (const [ln, t, attrs] of src_secs) {
    if (!truthy(attrs.get("id") ?? null)) {
      p.add("STRUCT", "heading-id", "error", `'## ${t}' has no {#id}: its anchor is the title's slug and every link to it breaks when the title changes`, p.rel(p.src), ln);
    }
  }
  // explicit ids that did not survive (a collision renamed them, or the block that carries them was dropped)
  const seen = new Map<string, number[]>();
  for (const f of p.text_files().slice(0, 1)) {
    for (const [ln, line] of outside_fences(p.lines(f))) {
      const m = ATTRS.search(line);
      if (!m || !ID_LINE.match(line)) continue;
      for (let part of py.str.split(m.group(1)!, ";")) {
        part = py.str.strip(part);
        if (part.startsWith("#") && ID_PART.fullmatch(part)) {
          const id = part.slice(1);
          if (!seen.has(id)) seen.set(id, []);
          seen.get(id)!.push(ln);
        }
      }
    }
  }
  for (const [i, lns] of seen) {
    if (lns.length > 1) {
      p.add("STRUCT", "id-collision", "warn", `explicit id #${i} is set ${lns.length} times (lines ${lns.join(", ")}); later ones are renamed ${i}-2 and links go to the first`,
        p.rel(p.src), lns[1]!);
    } else if (!p.page_ids.has(i)) {
      p.add("STRUCT", "id-collision", "warn", `explicit id #${i} is not on the page (the block type ignores ids, or the id collided)`, p.rel(p.src), lns[0]!);
    }
  }
  // literal markdown the converters left in page text
  const done = new Set<string>();
  for (const [, parent, k, v] of walk([get(c, "sections", []), get(c, "lede", [])])) {
    if (RAW_SKIP.has(k)) continue;
    const s = re.sub(CODE, " ", v);
    for (const [what, rx] of PATS) {
      for (const m of rx.finditer(s)) {
        const snip = py.html.unescape(m.group(0)!);
        if (done.has(snip)) continue;
        done.add(snip);
        const where = kind_of(parent, k);
        const files = startsPR(where) && p.src ? [...p.prs, p.src] : null;
        let [f, l] = p.locate(py.cpSlice(snip, 0, 60), files);
        if (!f) [f, l] = p.locate_text(plain(snip), files);
        const hint = where === "PR card" && what === "a link" ? "build_prs.py's markdown does not convert [text](#anchor) links in PR bodies"
          : what === "an image" ? "two images on adjacent lines become one paragraph: put a blank line between them"
          : "the converter does not handle it in this position";
        p.add("STRUCT", "raw-markdown", "error", `${what} is literal text in a ${str(where)}.${k}: ${repr(py.cpSlice(snip, 0, 80))} (${hint})`, f, l);
      }
    }
  }
});
