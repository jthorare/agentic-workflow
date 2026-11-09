// ev_packet.py: OUTPUT 2, the packet for implementation and agent handoff (build/agents/ as the agents lane writes
// it, plus the packet files): lane present and current, budgets, anchors sliced once, SQL extracted, map edges
// resolve, images indexed, PRs copied, schema.md present, and the handoff probe's records.
import * as py from "../py";
import { BUDGET, EXEMPT as WRITER_EXEMPT } from "../agents/lane";
import { check as agentsCheck } from "../agents/write";
import { type Py, IMAGE_EXT, endsWithAny, get, getsize, isInt, list, prsOf, ratio, readJson, readText, sqlFences, tokensEst, walkDir } from "./common";
import { type Input } from "./capture";
import { type Output, type ScoreRec, error, metric, notRun, output, score } from "./score";

const re = py.re;
const HEX = re.compile("[0-9a-fA-F]+");
const path = py.path;

/** write_agents.README_MAX (the writer keeps it private; test/eval checks it against the Python). */
export const README_MAX = 8192;
export const SLICE_BUDGET = BUDGET;
export const EXEMPT: ReadonlySet<string> = WRITER_EXEMPT;
const TEXT_EXT = [".md", ".txt", ".json", ".sql", ".html", ".ts", ".tsx", ".js", ".py", ".sh", ".yaml", ".yml", ".css"];
const FM_ANCHORS = re.compile(String.raw`^anchors:\s*\[(.*)\]\s*$`, re.M);
const FM_ITEM = re.compile(String.raw`"((?:[^"\\]|\\.)*)"|([^,\s][^,]*)`);

export function laneFiles(agents: string): string[] {
  const out: string[] = [];
  for (const [dp, fn] of walkDir(agents, false)) for (const f of py.sorted(fn)) out.push(path.relpath(path.join(dp, f), agents));
  return out;
}

/** bytes.decode("unicode_escape") of s.encode(): the escapes Python's codec reads, every other byte as Latin-1. */
export function unicodeEscape(s: string): string {
  const b = new TextEncoder().encode(s);
  let out = "";
  let i = 0;
  const hex = (len: number): number => {
    const h = String.fromCharCode(...b.slice(i, i + len));
    if (h.length !== len || !HEX.fullmatch(h)) throw new py.PyError("UnicodeDecodeError", `'unicodeescape' codec can't decode bytes: truncated \\xXX escape`);
    i += len;
    return parseInt(h, 16);
  };
  const SIMPLE: Record<string, string> = { "\\": "\\", "'": "'", '"': '"', a: "\x07", b: "\b", f: "\f", n: "\n", r: "\r", t: "\t", v: "\v" };
  while (i < b.length) {
    const c = b[i]!;
    if (c !== 0x5c) {
      out += String.fromCharCode(c);
      i++;
      continue;
    }
    i++;
    if (i >= b.length) throw new py.PyError("UnicodeDecodeError", "'unicodeescape' codec can't decode byte 0x5c: \\ at end of string");
    const e = String.fromCharCode(b[i]!);
    i++;
    if (e === "\n") continue;
    if (e in SIMPLE) out += SIMPLE[e];
    else if (e >= "0" && e <= "7") {
      let v = parseInt(e, 8);
      for (let k = 0; k < 2 && i < b.length && b[i]! >= 0x30 && b[i]! <= 0x37; k++) v = v * 8 + (b[i++]! - 0x30);
      out += String.fromCodePoint(v);
    } else if (e === "x") out += String.fromCodePoint(hex(2));
    else if (e === "u") out += String.fromCodePoint(hex(4));
    else if (e === "U") {
      const v = hex(8);
      if (v > 0x10ffff) throw new py.PyError("UnicodeDecodeError", "'unicodeescape' codec can't decode bytes: illegal Unicode character");
      out += String.fromCodePoint(v);
    } else if (e === "N") throw new py.PyError("UnicodeDecodeError", "'unicodeescape' codec: \\N{...} escapes are not supported here");
    else out += "\\" + e; // an unknown escape keeps its backslash
  }
  return out;
}

export function frontAnchors(text: string): string[] {
  const head = text.startsWith("---") ? py.str.split(text, "\n---", 1)[0]! : "";
  const m = FM_ANCHORS.search(head);
  if (!m) return [];
  const items: string[] = [];
  for (const g of FM_ITEM.findall(m.group(1)!)) {
    const [q, plain] = g as string[];
    items.push(q ? unicodeEscape(q) : py.str.strip(plain || ""));
  }
  return items.filter((x) => x);
}

export function sPresent(agents: string): ScoreRec {
  const ok = py.io.isfile(path.join(agents, "README.md"));
  return score("agents_present", ok ? 1 : 0, "1 when build/agents/README.md exists", "build/agents",
    { files: py.io.isdir(agents) ? laneFiles(agents).length : 0 });
}

export function sCurrent(build: string, agents: string): ScoreRec {
  const method = "write_agents.check(build_dir) -> [] means the lane matches content.json and tdd.html: 1, else 0 (problems listed)";
  if (!py.io.isfile(path.join(agents, "README.md"))) return notRun("agents_current", "no build/agents/README.md", method, "scripts/write_agents.py");
  let problems: string[];
  try {
    problems = [...agentsCheck(build)];
  } catch (e) {
    const name = e instanceof py.PyError ? e.type : (e as Error).name;
    return error("agents_current", `check raised ${name}: ${py.cpSlice((e as Error).message, 0, 160)}`, method, "scripts/write_agents.py");
  }
  return score("agents_current", problems.length ? 0 : 1, method, "scripts/write_agents.py", { problems: problems.length }, problems.slice(0, 20));
}

export function sReadme(agents: string, limit: number): ScoreRec {
  const p = path.join(agents, "README.md");
  const method = `1 when agents/README.md bytes <= ${limit} (8 KB, the writer's front-door limit)`;
  if (!py.io.isfile(p)) return notRun("readme_bytes", "no agents/README.md", method, "build/agents/README.md");
  const b = getsize(p);
  return score("readme_bytes", b <= limit ? 1 : 0, method, "build/agents/README.md", { bytes: b, limit });
}

export function sLargest(agents: string, budget: number, exempt: ReadonlySet<string>, files: string[]): ScoreRec {
  const method = `largest non-exempt text file in agents/ (tokens as the writer counts them: ceil(chars/4), from tokens.json when present) ` +
    `<= ${budget}: 1, else 0; exempt files (whole-document and index files) are listed by write_agents.EXEMPT`;
  if (!files.length) return notRun("largest_slice", "no agents/ lane", method, "build/agents");
  const tj = readJson(path.join(agents, "tokens.json"));
  const per = new Map<Py, Py>();
  for (const r of list(get(py.truthy(tj) ? tj : new Map(), "files"))) {
    if (!(r instanceof Map)) continue;
    per.set(py.truthy(get(r, "path")) ? get(r, "path") : get(r, "file"), get(r, "tokens"));
  }
  const rows: [Py, string, number][] = [];
  for (const f of files) {
    if (exempt.has(f) || !endsWithAny(f, TEXT_EXT)) continue;
    const text = readText(path.join(agents, f)) || "";
    const t4 = isInt(per.get(f)) ? per.get(f) : Math.floor((py.cpLen(text) + 3) / 4);
    rows.push([t4, f, tokensEst(text)]);
  }
  if (!rows.length) return notRun("largest_slice", "no non-exempt text files", method, "build/agents");
  rows.sort((a, b) => Number(b[0]) - Number(a[0]) || py.cmp(b[1], a[1]) || b[2] - a[2]);
  const [t4, f, t36] = rows[0]!;
  const over = rows.filter((r) => Number(r[0]) > budget).map((r) => r[1]);
  return score("largest_slice", Number(t4) <= budget ? 1 : 0, method, "build/agents + tokens.json",
    { largest_tokens: t4, largest_tokens_chars_per_3_6: t36, budget, slices: rows.length, over_budget: over.length },
    { largest: f, over_budget: over.slice(0, 20), top5: rows.slice(0, 5).map((r) => ({ file: r[1], tokens: r[0] })) });
}

export function sAnchors(agents: string, content: Py, files: string[]): ScoreRec {
  const method = "content.json anchors that appear in exactly one slice / all anchors; slices = sections/*.md front matter 'anchors:' lists, " +
    "plus refs.json 'file' for anchors no section slice lists (tables live in schema/schema.md)";
  if (!files.length) return notRun("anchors_sliced_once", "no agents/ lane", method, "build/agents/sections");
  const anchors: Py[] = list(get(content, "anchors")).map((a) => a.get("id"));
  const count = new Map<Py, number>(anchors.map((a) => [a, 0]));
  const where = new Map<Py, string[]>(anchors.map((a) => [a, []]));
  for (const f of files)
    if (f.startsWith("sections/") && f.endsWith(".md"))
      for (const a of frontAnchors(readText(path.join(agents, f)) || ""))
        if (count.has(a)) {
          count.set(a, count.get(a)! + 1);
          where.get(a)!.push(f);
        }
  const refs = readJson(path.join(agents, "refs.json"));
  for (const r of list(get(py.truthy(refs) ? refs : new Map(), "anchors"))) {
    const a = get(r, "id");
    const f = get(r, "file");
    if (count.has(a) && count.get(a) === 0 && py.truthy(f) && !f.startsWith("sections/") && py.io.isfile(path.join(agents, f))) {
      count.set(a, 1);
      where.set(a, [f]);
    }
  }
  const once = anchors.filter((a) => count.get(a) === 1).length;
  const dup = py.sorted(anchors.filter((a) => count.get(a)! > 1));
  const missing = py.sorted(anchors.filter((a) => count.get(a) === 0));
  if (!anchors.length) return notRun("anchors_sliced_once", "content.json has no anchors", method, "content.json anchors");
  return score("anchors_sliced_once", ratio(once, anchors.length), method, "build/agents/sections + refs.json",
    { anchors: anchors.length, once, duplicated: dup.length, missing: missing.length },
    { duplicated: new Map(dup.slice(0, 20).map((a) => [a, where.get(a)])), missing: missing.slice(0, 30) });
}

const squash = (s: Py): string => re.sub(String.raw`\s+`, " ", py.str.strip(py.truthy(s) ? s : ""));

export function sSql(agents: string, content: Py, files: string[]): ScoreRec {
  const fences = sqlFences(content);
  const method = "sql fences in content.json whose whitespace-normalised text is found in agents/sql/*.sql / sql fences";
  if (!fences.length) return notRun("sql_extracted", "no sql fences in content.json", method, "build/agents/sql");
  const sqlf = files.filter((f) => f.startsWith("sql/") && f.endsWith(".sql"));
  if (!files.length) return notRun("sql_extracted", "no agents/ lane", method, "build/agents/sql", { fences: fences.length });
  const blob = squash(sqlf.map((f) => readText(path.join(agents, f)) || "").join("\n"));
  const found = new Set<number>();
  fences.forEach((f, i) => {
    const s = squash(get(f, "text"));
    if (s && blob.includes(s)) found.add(i);
  });
  return score("sql_extracted", ratio(found.size, fences.length), method, "build/agents/sql",
    { fences: fences.length, extracted: found.size, sql_files: sqlf.length },
    { missing_fences: fences.flatMap((f, i) => found.has(i) ? [] : [{ section: get(f, "section"),
      first_line: py.truthy(get(f, "text")) ? py.cpSlice(py.str.splitlines(get(f, "text"))[0]!, 0, 100) : "" }]) });
}

export function sMap(agents: string): ScoreRec {
  const method = "map.json edges whose from and to are both entity ids / edges";
  const m = readJson(path.join(agents, "map.json"));
  if (!py.truthy(m)) return notRun("map_edges_resolve", "no agents/map.json", method, "build/agents/map.json");
  const ids = new Set(list(get(m, "entities")).map((e) => get(e, "id")));
  const edges = list(get(m, "edges"));
  const okE = (e: Py) => ids.has(get(e, "from")) && ids.has(get(e, "to"));
  const ok = edges.filter(okE);
  const bad = edges.filter((e) => !okE(e));
  if (!edges.length) return notRun("map_edges_resolve", "map.json has no edges", method, "build/agents/map.json", { entities: ids.size });
  const rels = new Map<Py, number>();
  for (const e of edges) rels.set(get(e, "rel"), (rels.get(get(e, "rel")) ?? 0) + 1);
  return score("map_edges_resolve", ratio(ok.length, edges.length), method, "build/agents/map.json",
    { entities: ids.size, edges: edges.length, resolved: ok.length, dangling: bad.length }, { rels, dangling: bad.slice(0, 10) });
}

export function sImages(agents: string, build: string): ScoreRec {
  const man = readJson(path.join(build, "content.files.json"));
  const filesD = get(py.truthy(man) ? man : new Map(), "files");
  const imgs = new Set<string>(py.truthy(filesD) ? [...(filesD as Map<string, Py>).keys()].filter((p) => endsWithAny(py.lower(p), IMAGE_EXT)) : []);
  const method = "images in content.files.json that have a row in agents/images/index.json (matched on the row's published 'source' path) / images";
  if (!imgs.size) return notRun("images_indexed", "content.files.json lists no images", method, "build/agents/images/index.json");
  const idx = readJson(path.join(agents, "images", "index.json"));
  if (!py.truthy(idx)) return notRun("images_indexed", "no agents/images/index.json", method, "build/agents/images/index.json", { images: imgs.size });
  const rows = list(get(idx, "images"));
  const have = new Set<Py>([...rows.map((r) => get(r, "source")), ...rows.map((r) => get(r, "published"))]);
  const hit = [...imgs].filter((x) => have.has(x));
  return score("images_indexed", ratio(hit.length, imgs.size), method, "build/agents/images/index.json",
    { images: imgs.size, indexed: hit.length, rows: rows.length, with_caption: rows.filter((r) => py.truthy(get(r, "caption"))).length,
      with_alt: rows.filter((r) => py.truthy(get(r, "alt"))).length }, { unindexed: py.sorted([...imgs].filter((x) => !have.has(x))).slice(0, 20) });
}

export function sPrs(agents: string, content: Py, files: string[]): ScoreRec {
  const prs = prsOf(content).filter((p) => p.n !== null && p.n !== undefined).map((p) => p.n);
  const method = "PR numbers in content.json whose prs/pr-NN.md is in agents/prs/ / PR numbers";
  if (!prs.length) return notRun("prs_copied", "no PR plan in content.json", method, "build/agents/prs");
  if (!files.length) return notRun("prs_copied", "no agents/ lane", method, "build/agents/prs", { prs: prs.length });
  const have = new Set<number>();
  for (const f of files) {
    const m = re.match(String.raw`prs/pr-0*(\d+)\b.*\.md$`, f);
    if (m) have.add(Number(py.int(m.group(1)!)));
  }
  const inHave = (x: Py) => have.has(Number(x instanceof py.PyFloat ? x.value : x));
  return score("prs_copied", ratio(prs.filter(inHave).length, prs.length), method, "build/agents/prs", { prs: prs.length, copied: prs.filter(inHave).length },
    { missing: prs.filter((x) => !inHave(x)) });
}

export function sSchemaMd(agents: string, content: Py): ScoreRec {
  const method = "1 when agents/schema/schema.md (or agents/schema.md) exists and names every schema table, else 0";
  const tables = list(get(content, "schema")).map((t) => get(t, "table"));
  if (!tables.length) return notRun("schema_md", "no schema tables in content.json", method, "build/agents/schema/schema.md");
  const p = [path.join(agents, "schema", "schema.md"), path.join(agents, "schema.md")].find((x) => py.io.isfile(x)) ?? null;
  if (!p) return score("schema_md", 0, method, "build/agents/schema/schema.md", { tables: tables.length, named: 0 });
  const text = readText(p) || "";
  const named = tables.filter((t) => py.truthy(t) && text.includes(t));
  return score("schema_md", named.length === tables.length ? 1 : 0, method, path.relpath(p, path.dirname(agents)),
    { tables: tables.length, named: named.length, bytes: getsize(p) }, { unnamed: tables.filter((t) => !named.includes(t)) });
}

export function sLaneSize(agents: string, files: string[]): ScoreRec {
  if (!files.length) return notRun("lane_size", "no agents/ lane", "sum of bytes and chars/3.6 token estimate over agents/ text files", "build/agents");
  let b = 0;
  let t = 0;
  let k = 0;
  for (const f of files) {
    const p = path.join(agents, f);
    b += getsize(p);
    if (endsWithAny(f, TEXT_EXT)) {
      k++;
      t += tokensEst(getsize(p));
    }
  }
  return metric("lane_size", { files: files.length, text_files: k, bytes: b, tokens_est: t },
    "bytes of every file under agents/; tokens_est = text bytes / 3.6 (an estimate, not a tokenizer)", "build/agents");
}

export function evaluate(build: string, content0: Py, inp: Input, probeScores: ScoreRec[], ownerReview: (inp: Input, t: string) => ScoreRec): Output {
  const content = py.truthy(content0) ? content0 : new Map();
  const agents = path.join(build, "agents");
  const files = py.io.isdir(agents) ? laneFiles(agents) : [];
  const scores = [sPresent(agents), sCurrent(build, agents), sReadme(agents, README_MAX), sLargest(agents, SLICE_BUDGET, EXEMPT, files)];
  if (py.truthy(content)) scores.push(sAnchors(agents, content, files), sSql(agents, content, files));
  scores.push(sMap(agents), sImages(agents, build));
  if (py.truthy(content)) scores.push(sPrs(agents, content, files), sSchemaMd(agents, content));
  scores.push(sLaneSize(agents, files), ...probeScores, ownerReview(inp, "packet"));
  return output("packet", inp.packet.key, scores, undefined, { writer: null });
}
