// REFS: every reference token defined and linked (D5), every #anchor link and hotspot href resolves, meta refs families
// point at anchors, and tokens the compiler leaves unlinked by position (lint_packet.Lint.r_refs).
import { Effect } from "effect";
import * as py from "../../py";
import { Packet } from "../packet";
import { LINK_KEYS, TOKEN, token_id } from "../patterns";
import { type PyDict, type PyValue, dget, get, has, items, iter, kind_of, nodes, or, plain, repr, startsPR, str, truthy, unlinked_text, walk } from "../pyops";

const re = py.re;
const HREF = re.compile(`href="#([^"]*)"`);
const SKIP_KEYS = new Set(["did", "id", "key", "src", "href", "alt", "text", "tone", "cls", "type", "file", "lang", "chipTone", "titleText", "term", "ddl",
  "image", "table", "label_id", "grid", "size"]);

export const refs = Effect.gen(function* () {
  const p = yield* Packet;
  const c = p.content;
  if (!truthy(c)) return;
  const ids = p.page_ids;
  for (const tok of iter(get(get(c, "refs", new Map()), "unlinked", []))) {
    const t = str(tok);
    const [f, l] = p.locate_token(t);
    p.add("REFS", "unlinked", "error", `reference token ${t} has no definition on the page (no element with id ${token_id(t)} and no refs ` +
      `family in meta.json): define it (a row, a decision, an {#${token_id(t)}}) or reword`, f, l);
  }
  const bad = new Map<string, PyValue>();
  for (const [, parent, k, v] of walk([get(c, "sections", []), get(c, "lede", [])])) {
    for (const m of HREF.finditer(v)) {
      const t = py.html.unescape(m.group(1)!);
      if (!ids.has(t) && !bad.has(t)) bad.set(t, or(get(parent, "type"), k));
    }
  }
  for (const [t, where] of bad) {
    let [f, l] = t ? p.locate("#" + t + ")") : [null, null];
    if (!f) [f, l] = t ? p.locate("#" + t) : [null, null];
    p.add("REFS", "broken-anchor", "error", `a link in a ${str(where)} points at #${t}, which is not an anchor on the page`, f, l);
  }
  const tables = new Set<unknown>(iter(get(c, "schema", [])).map((t) => get(t, "id")));
  for (const n of nodes(get(c, "sections", []))) {
    if (get(n, "type") !== "diagram") continue;
    const hfile = py.path.join(p.root, "diagrams", str(get(n, "key", "")) + ".hotspots.json");
    for (const hs of iter(get(n, "hotspots", [])) as PyDict[]) {
      const lab = get(hs, "label", "");
      const [f, l] = p.fs.isfile(hfile) ? p.locate_label(lab, hfile) : [null, null];
      const href = or(get(hs, "href"), "");
      if (typeof href === "string" && href.startsWith("#") && !ids.has(href.slice(1))) {
        p.add("REFS", "hotspot-anchor", "error", `hotspot '${str(lab)}' on ${str(get(n, "key"))} links to ${href}, which is not an anchor on the page`, f, l);
      }
      if (truthy(get(hs, "table")) && !tables.has(get(hs, "table"))) {
        p.add("REFS", "hotspot-anchor", "error", `hotspot '${str(lab)}' on ${str(get(n, "key"))} opens table ${str(get(hs, "table"))}, which is not in schema/`, f, l);
      }
    }
  }
  const mp = py.path.join(p.root, "meta.json");
  const fams = or(dget(or(p.meta, new Map()), "refs"), new Map());
  for (const [fam, href] of items(fams)) {
    if (typeof href !== "string" || !href.startsWith("#") || !ids.has(href.slice(1))) {
      const [f, l] = p.locate('"' + fam + '"', [mp]);
      p.add("REFS", "family", "error", `meta.json refs family ${fam} points at ${repr(href)}, which is not an anchor on the page`, f, l);
    }
  }
  // tokens the compiler leaves unlinked by position: titles, legend labels, questions, headings and summaries in HTML
  const seen = new Set<string>();
  for (const [path, parent, k, v] of walk(get(c, "sections", []))) {
    if (SKIP_KEYS.has(k) || !v || path.some(([, pk]) => pk === "subs")) continue;
    const txt = unlinked_text(v);
    const toks = [...TOKEN.finditer(txt)].map((m) => m.group(1)!);
    if (!toks.length) continue;
    const kind = kind_of(parent, k);
    const own = truthy(get(parent, "did")) ? token_id(str(get(parent, "did", ""))) : get(parent, "id");
    const underEl = path.find(([, pk]) => pk === "text");
    const under = underEl ? or(get(underEl[0], "type"), "node") : null;
    const pv = plain(v);
    const pv60 = py.cpSlice(pv, 0, 60);
    for (const tok of toks) {
      if (token_id(tok) === own) continue;
      const key = JSON.stringify([tok, str(kind), typeof kind, k, pv60]);
      if (seen.has(key)) continue;
      seen.add(key);
      const files = startsPR(kind) && p.src ? [...p.prs, p.src] : null;
      const [f, l] = p.locate_text(pv, files);
      const undefined_ = ids.has(token_id(tok)) ? "" : "; it has no definition either";
      if (LINK_KEYS.has(k) && truthy(under)) {
        p.add("REFS", "missed-link", "error", `${tok} is not linked in a ${str(kind)} inside a ${str(under)}'s text (${repr(pv60)}): the compiler's ` +
          `link_tree does not descend into '${str(under)}.text' (compiler fix)${undefined_}`, f, l);
      } else if (LINK_KEYS.has(k)) {
        p.add("REFS", "title-token", "warn", `${tok} is not linked in ${str(kind)}.${k} (${repr(pv60)}): it sits in a heading, summary or ` +
          `term the compiler skips by design${undefined_}`, f, l);
      } else {
        p.add("REFS", "title-token", "warn", `${tok} is not linked in the ${str(kind)} ${k} ${repr(pv60)}: the compiler does not link ` +
          `${str(kind)}.${k}${undefined_}`, f, l);
      }
    }
  }
});

export { has };
