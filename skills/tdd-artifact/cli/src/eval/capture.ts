// ev_capture.py: the INPUT. The packet as a schema (source markdown, meta.json, lane files with hashes) plus labeled
// data (owner decisions as question -> answer labels, citations as evidence labels, owner review notes from
// labels.jsonl), and the digest over the source, meta and lane hashes.
import * as py from "../py";
import { pyStr, strRepr } from "../py/repr";
import { type Py, get, list, readJson, readText, sha256Bytes, sha256File, stripTags, walk, walkDir, getsize, now } from "./common";

const re = py.re;
const path = py.path;

export const LANES = ["diagrams", "prs", "schema", "mockups", "research", "shots", "media"];
const META_FIELDS = ["title", "rev", "updated", "sha", "sha_date", "repo_url", "status", "rev_note", "repo_path"];
export const VERDICTS = ["pass", "fail", "needs-work", "question"];
export const TARGETS = ["artifact", "packet", "implementation", "qa", "bugs", "tokens"];
const WORD = re.compile("[A-Za-z0-9'’]+");
const DATE = re.compile(String.raw`\b(\d{4}-\d{2}-\d{2})\b`);
const WORD_STRIP = re.compile(String.raw`\x60\x60\x60.*?\x60\x60\x60|\]\([^)]*\)|[\x60*_#|\[\]()>{}-]`, re.S);

/** The markdown source: content.json 'source' when present, else <slug>.md, else the only top-level .md. */
export function findSource(packet: string, content: Py): string | null {
  const cands: string[] = [];
  if (py.truthy(content) && py.truthy(get(content, "source"))) cands.push(path.join(packet, get(content, "source")));
  cands.push(path.join(packet, path.basename(path.normpath(packet)) + ".md"));
  for (const c of cands) if (py.io.isfile(c)) return c;
  const mds = py.io.listdirSorted(packet).filter((f) => f.endsWith(".md") && !["status.md", "readme.md"].includes(py.lower(f)));
  return mds.length === 1 ? path.join(packet, mds[0]!) : null;
}

/** The compiler's word count (build_tdd.Compiler.build): code fences, link targets and markdown punctuation removed. */
export function wordsOf(text: string): number {
  return WORD.findall(WORD_STRIP.sub(" ", text)).length;
}

export interface FileRow { path: string; sha256: string; bytes: number }

export function laneFiles(packet: string, lane: string): FileRow[] | null {
  const root = path.join(packet, lane);
  if (!py.io.isdir(root)) return null;
  const out: FileRow[] = [];
  for (const [dp, fn] of walkDir(root, true))
    for (const f of py.sorted(fn)) {
      if (f.startsWith(".")) continue;
      const p = path.join(dp, f);
      out.push({ path: path.relpath(p, packet), sha256: sha256File(p), bytes: getsize(p) });
    }
  return out;
}

/** labels.jsonl -> [records, problems]. Each line {target, anchor, verdict, note, date[, author]}. */
export function readLabels(p: string | null): [Map<string, Py>[], string[]] {
  const recs: Map<string, Py>[] = [];
  const problems: string[] = [];
  if (!p) return [recs, problems];
  const text = readText(p);
  if (text === null) return [recs, [`${p}: cannot read`]];
  const base = path.basename(p);
  py.str.splitlines(text).forEach((raw, idx) => {
    const i = idx + 1;
    const line = py.str.strip(raw);
    if (!line || line.startsWith("//") || line.startsWith("#")) return;
    let r: Py;
    try {
      r = py.json.loads(line);
    } catch (e) {
      problems.push(`${base}:${i}: not JSON (${(e as Error).message})`);
      return;
    }
    const missing = ["target", "anchor", "verdict", "note", "date"].filter((k) => !(r instanceof Map && r.has(k)));
    if (missing.length) {
      problems.push(`${base}:${i}: missing ${missing.join(", ")}`);
      return;
    }
    if (!TARGETS.includes(r.get("target"))) {
      problems.push(`${base}:${i}: target ${py.repr(r.get("target"))} not one of ${TARGETS.join(", ")}`);
      return;
    }
    if (!VERDICTS.includes(r.get("verdict"))) {
      problems.push(`${base}:${i}: verdict ${py.repr(r.get("verdict"))} not one of ${VERDICTS.join(", ")}`);
      return;
    }
    r.set("line", i);
    recs.push(r);
  });
  return [recs, problems];
}

/** Every decision box: {question -> owner answer}. The answer is the status line plus the box's first paragraph. */
export function decisionLabels(content: Py): Record<string, Py>[] {
  const bodies = new Map<Py, string>();
  for (const nd of walk(get(content, "sections", []), new Set(["decision"]))) {
    const first = list(get(nd, "blocks", [])).find((b) => get(b, "type") === "p" && py.truthy(get(b, "html")));
    bodies.set(get(nd, "id"), first ? stripTags(get(first, "html")) : "");
  }
  const out: Record<string, Py>[] = [];
  for (const d of list(get(content, "decisions", []))) {
    const status = get(d, "status", "");
    const m = DATE.search(status);
    const state = get(d, "state");
    out.push({
      kind: "decision", id: get(d, "id"), anchor: get(d, "anchor"), section: get(d, "section"),
      question: get(d, "question"), status, state,
      answer: py.cpSlice(bodies.get(get(d, "anchor")) || "", 0, 600),
      label: state === "done" ? "owner-ruling" : state === "held" ? "held" : "open-proposal",
      date: m ? m.group(1) : null,
    });
  }
  return out;
}

export function evidenceLabels(content: Py, meta: Py): Record<string, Py>[] {
  return list(get(content, "cites", [])).map((c, idx) => ({
    kind: "citation", id: `CIT.${String(idx + 1).padStart(4, "0")}`, text: get(c, "text"), path: get(c, "path"), from: get(c, "from"),
    to: get(c, "to"), section: get(c, "section"), href: get(c, "href"), pinned_sha: py.truthy(get(c, "href")) ? get(meta, "sha") : null,
  }));
}

export interface Input {
  schema: string;
  captured: string;
  packet: { slug: string; repo_slug: string; dir: string; key: string; build: string };
  source: { path: string; sha256: string; bytes: number; words: number; words_method: string } | null;
  meta: Record<string, Py>;
  meta_sha256: string | null;
  files: Record<string, FileRow[]>;
  content: Record<string, Py> | null;
  labels: {
    decisions: Record<string, Py>[];
    evidence: Record<string, Py>[];
    review_notes: Map<string, Py>[];
    review_notes_source: string | null;
    review_notes_problems: string[];
  };
  digest: string;
}

export function capture(packetDir: string, build: string, labelsPath: string | null = null): [Input, Py, Py] {
  const packet = path.abspath(packetDir);
  const content = readJson(path.join(build, "content.json"));
  const metaPath = path.join(packet, "meta.json");
  const meta0 = readJson(metaPath, new Map());
  const meta = py.truthy(meta0) ? meta0 : new Map();
  const slug = path.basename(packet);
  const repoSlug = path.basename(path.dirname(packet)) === "system-design" ? path.basename(path.dirname(path.dirname(packet))) : path.basename(path.dirname(packet));
  const rev = get(meta, "rev");
  const key = `${repoSlug}/${slug}@rev${rev !== null ? pyStr(rev) : "none"}`;
  const src = findSource(packet, content);
  let source: Input["source"] = null;
  if (src) {
    const text = readText(src) || "";
    source = { path: path.relpath(src, packet), sha256: sha256File(src), bytes: getsize(src), words: wordsOf(text),
      words_method: "build_tdd word regex over the markdown (fences, link targets, markdown punctuation removed)" };
  }
  const files: Record<string, FileRow[]> = {};
  for (const lane of LANES) {
    const v = laneFiles(packet, lane);
    if (v !== null) files[lane] = v;
  }
  const [labels, problems] = readLabels(labelsPath);
  const metaOut: Record<string, Py> = {};
  for (const k of META_FIELDS) if (meta instanceof Map && meta.has(k)) metaOut[k] = meta.get(k);
  if (py.truthy(get(meta, "project"))) metaOut.project = get(get(meta, "project"), "label");
  const contentPath = path.join(build, "content.json");
  const inp: Input = {
    schema: "tdd-eval-input/1", captured: now(),
    packet: { slug, repo_slug: repoSlug, dir: packet, key, build: path.abspath(build) },
    source,
    meta: metaOut,
    meta_sha256: py.io.isfile(metaPath) ? sha256File(metaPath) : null,
    files,
    content: py.truthy(content) ? { path: "content.json", sha256: sha256File(contentPath), generated: get(content, "generated"),
      template: get(content, "template"), stats: get(content, "stats") } : null,
    labels: {
      decisions: py.truthy(content) ? decisionLabels(content) : [],
      evidence: py.truthy(content) ? evidenceLabels(content, meta) : [],
      review_notes: labels,
      review_notes_source: labelsPath ? path.abspath(labelsPath) : null,
      review_notes_problems: problems,
    },
    digest: "",
  };
  const basis = new Map<string, Py>([["source", source ? source.sha256 : null], ["meta", inp.meta_sha256],
    ["files", new Map(Object.entries(files).map(([k, v]) => [k, v.map((f) => f.sha256)]))]]);
  inp.digest = sha256Bytes(py.json.dumps(basis, { sortKeys: true }));
  return [inp, content, meta];
}

export { strRepr };
