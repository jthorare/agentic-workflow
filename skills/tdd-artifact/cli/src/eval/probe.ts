// ev_probe.py: the handoff probe. A deterministic extractor turns templates/eval/probe-questions.json into concrete
// questions with expected answers from content.json; a loader reads the agents/ lane the way an implementing agent
// would (README first, then the files it links, within a token budget); two scorers use them:
//   probe_answerable  (no model) the share of questions whose evidence and expected answer sit together in one file
//   probe_agent       (--with-agent-probe and a key) a model given ONLY the loaded lane answers; matched per kind
import { Effect } from "effect";
import * as py from "../py";
import { pyStr } from "../py/repr";
import { type Py, IMAGE_EXT, endsWithAny, get, isNum, list, prsOf, ratio, readJson, readText, stripTags, tokensEst, walk } from "./common";
import * as L from "./llm";
import { type ScoreRec, error, notRun, score } from "./score";

const re = py.re;
const path = py.path;

const LINK = re.compile(String.raw`\[([^\]]*)\]\(([^)\s]+)(?:\s+"[^"]*")?\)`);
const SKIP_EXT = [...IMAGE_EXT, ".mp4", ".webm", ".mov", ".pdf"];
const SKIP_FILES = new Set(["llms-full.txt"]);
const WINDOW = 600;

/** str.format(**fields) for "{name}" fields and "{{" / "}}". */
export function pyFormat(tpl: string, fields: Record<string, Py>): string {
  return re.sub(String.raw`\{\{|\}\}|\{([A-Za-z_][A-Za-z0-9_]*)\}`, (m) => {
    const g = m.group(0)!;
    if (g === "{{") return "{";
    if (g === "}}") return "}";
    const k = m.group(1)!;
    if (!(k in fields)) throw new py.PyError("KeyError", py.repr(k));
    return pyStr(fields[k]);
  }, tpl);
}

function idkey(i: Py): [string, number] {
  const m = re.match(String.raw`([A-Za-z]+)-?(\d+)`, py.truthy(i) ? i : "");
  return m ? [py.upper(m.group(1)!), Number(py.int(m.group(2)!))] : [py.truthy(i) ? i : "", 0];
}
const cmpKey = (a: [string, number], b: [string, number]): number => py.cmp(a[0], b[0]) || a[1] - b[1];

function firstClause(text: Py, k = 110): string {
  let t = py.str.split(py.truthy(text) ? text : "").join(" ");
  t = py.str.rstrip(re.split(String.raw`(?<=[.;:])\s`, t)[0]!, ".;:");
  return py.cpSlice(t, 0, k);
}

const CONSTRAINT = re.compile(String.raw`\s+(PRIMARY|NOT|NULL|DEFAULT|REFERENCES|UNIQUE|CHECK|GENERATED|COLLATE|CONSTRAINT)\b.*$`, re.I | re.S);

/** 'uuid PRIMARY KEY DEFAULT gen_random_uuid()' -> 'uuid'; the column's SQL type without its constraints. */
export function baseType(t: Py): string {
  return py.str.strip(CONSTRAINT.sub("", py.str.strip(py.truthy(t) ? t : "")));
}

export interface Question {
  id: string; kind: string; question: string; expected: Py; match: string; evidence: Py[]; accept?: Py;
  answerable_in?: string | null; answer?: string; correct?: boolean;
}

export function buildQuestions(content: Py, template: Py): Question[] {
  const kinds = new Map<string, Py>(list(get(template, "kinds", [])).map((k) => [k.get("kind"), k]));
  const qs: Question[] = [];
  const add = (kind: string, fields: Record<string, Py>, expected: Py, evidence: Py[], accept?: Py): void => {
    const k = kinds.get(kind);
    const q: Question = { id: `${kind}-${qs.filter((x) => x.kind === kind).length + 1}`, kind, question: pyFormat(k.get("template"), fields),
      expected, match: k.get("match"), evidence: evidence.filter((e) => py.truthy(e)) };
    if (py.truthy(accept)) q.accept = accept;
    qs.push(q);
  };
  const limit = (kind: string): number => Number(kinds.get(kind).get("limit"));
  const decisions = list(get(content, "decisions")).map((d, i) => [d, idkey(get(d, "id")), i] as const)
    .sort((a, b) => cmpKey(a[1], b[1]) || a[2] - b[2]).map((x) => x[0]);
  const schema = list(get(content, "schema"));
  const prs = prsOf(content);
  if (kinds.has("decision-by-question"))
    for (const d of decisions.slice(0, limit("decision-by-question")))
      add("decision-by-question", { question: get(d, "question") }, get(d, "id"), [py.cpSlice(py.truthy(get(d, "question")) ? get(d, "question") : "", 0, 48)]);
  if (kinds.has("decision-state")) {
    const acc = get(kinds.get("decision-state"), "accept", new Map());
    const pick = [...decisions].reverse().filter((_, i) => i % 2 === 0).slice(0, limit("decision-state"));
    for (const d of pick) {
      const st = get(d, "state");
      const exp = st === "done" ? "decided" : st === "held" ? "held" : "open";
      add("decision-state", { id: get(d, "id") }, exp, [get(d, "id")], acc);
    }
  }
  for (const [kind, state] of [["new-tables", "new"], ["changed-tables", "changed"]] as const) {
    const names = schema.filter((t) => get(t, "state") === state).map((t) => get(t, "table"));
    if (kinds.has(kind) && names.length) add(kind, {}, names, names);
  }
  if (kinds.has("pr-introduces-table")) {
    let k = 0;
    const byN = prs.map((p, i) => [p, i] as const).sort((a, b) => Number(py.truthy(a[0].n) ? a[0].n : 0) - Number(py.truthy(b[0].n) ? b[0].n : 0) || a[1] - b[1]).map((x) => x[0]);
    for (const t of schema) {
      if (get(t, "state") !== "new" || k >= limit("pr-introduces-table")) continue;
      const name = get(t, "table");
      const hit = byN.find((p) => py.truthy(name) && re.search(String.raw`\b` + re.escape(name) + String.raw`\b`, [p.title, p.change, p.body].join(" ")) !== null);
      if (hit) {
        add("pr-introduces-table", { table: name }, `PR ${pyStr(hit.n)}`, [name]);
        k++;
      }
    }
  }
  if (kinds.has("pr-by-title") && prs.length) {
    const idx = [...new Set([0, Math.floor(prs.length / 2), prs.length - 1])].sort((a, b) => a - b).slice(0, limit("pr-by-title"));
    for (const i of idx) add("pr-by-title", { title: prs[i]!.title }, `PR ${pyStr(prs[i]!.n)}`, [py.cpSlice(prs[i]!.title, 0, 60)]);
  }
  if (kinds.has("pr-count") && prs.length) add("pr-count", {}, String(prs.length), []);
  if (kinds.has("stat-card")) {
    let k = 0;
    for (const node of walk(get(content, "sections", []), new Set(["cards"]))) {
      if (!py.truthy(get(node, "stat"))) continue;
      for (const c of list(get(node, "cards", []))) {
        if (k >= limit("stat-card")) break;
        const fig = stripTags(get(c, "title"));
        const b = list(get(c, "blocks", [])).find((x) => py.truthy(get(x, "html")));
        const body = b ? stripTags(get(b, "html")) : "";
        if (fig && re.search(String.raw`\d`, fig) && body) {
          const what = firstClause(body);
          add("stat-card", { what }, fig, [py.cpSlice(what, 0, 40)]);
          k++;
        }
      }
    }
  }
  if (kinds.has("column-type")) {
    let k = 0;
    for (const t of schema) {
      if (get(t, "state") !== "new" || k >= limit("column-type")) continue;
      const cols = list(get(t, "columns", [])).filter((c) => get(c, "change") === "new" && baseType(get(c, "type")));
      const col = cols.find((c) => !re.search(String.raw`(^|_)id$`, py.truthy(get(c, "name")) ? get(c, "name") : "")) ?? cols[0] ?? null;
      if (col) {
        add("column-type", { table: get(t, "table"), column: col.get("name") }, baseType(col.get("type")), [col.get("name")]);
        k++;
      }
    }
  }
  if (kinds.has("measured")) {
    let k = 0;
    for (const t of schema) {
      const meas = get(t, "measured");
      for (const [key, mv] of py.truthy(meas) ? (meas as Map<string, Py>) : new Map<string, Py>()) {
        if (k >= limit("measured")) break;
        if (mv instanceof Map && re.search(String.raw`\d`, pyStr(get(mv, "value", "")))) {
          add("measured", { label: py.truthy(get(mv, "label")) ? get(mv, "label") : key, table: get(t, "table") }, pyStr(mv.get("value")), [get(t, "table")]);
          k++;
        }
      }
    }
  }
  const sha = get(py.truthy(get(content, "meta")) ? get(content, "meta") : new Map(), "sha");
  if (kinds.has("base-sha") && py.truthy(sha)) add("base-sha", {}, sha, []);
  return qs;
}

// ----------------------------------------------------------------------------- matching

function ntok(s: Py): string {
  let t = py.lower(py.truthy(s) ? s : "");
  t = re.sub(String.raw`\bpr[\s\-#]*(\d+)`, String.raw`pr\1`, t);
  return re.sub(String.raw`\s+`, " ", t);
}

function numbers(s: Py): Set<string> {
  return new Set((re.findall(String.raw`\d[\d,]*(?:\.\d+)?`, py.truthy(s) ? s : "") as string[]).map((x) => py.replace(x, ",", "")));
}

export function matches(q: Question, answer: Py): boolean {
  const a: string = py.truthy(answer) ? answer : "";
  const e = q.expected;
  const kind = q.match;
  if (kind === "token") return re.search(String.raw`(?<![a-z0-9])` + re.escape(ntok(e)) + String.raw`(?![a-z0-9])`, ntok(a)) !== null;
  if (kind === "oneof") {
    const acc: Map<string, Py> = py.truthy(q.accept) ? q.accept : new Map();
    const low = py.lower(a);
    const words = (k: Py): Py[] => (acc.has(k) ? list(acc.get(k)) : [k]);
    const hit = (w: Py) => re.search(String.raw`\b` + re.escape(w) + String.raw`\b`, low) !== null;
    const mine = words(e).some(hit);
    const other = [...acc.entries()].some(([k, ws]) => k !== e && list(ws).some(hit));
    return mine && !other;
  }
  if (kind === "all") return list(e).every((x) => py.lower(a).includes(py.lower(pyStr(x))));
  if (kind === "numbers") {
    const want = numbers(e);
    const got = numbers(a);
    return want.size > 0 && [...want].every((x) => got.has(x));
  }
  if (kind === "contains") return re.sub(String.raw`\s+`, "", py.lower(a)).includes(re.sub(String.raw`\s+`, "", py.lower(pyStr(e))));
  if (kind === "prefix") return py.lower(a).includes(py.lower(py.cpSlice(pyStr(e), 0, 7)));
  return false;
}

// ----------------------------------------------------------------------------- the handoff loader

export function readmeLinks(text: string): string[] {
  const out: string[] = [];
  let optional = false;
  for (const line of py.str.splitlines(text)) {
    const h = re.match(String.raw`^#{1,3}\s+(.*)`, line);
    if (h) {
      optional = re.search(String.raw`\boptional\b`, h.group(1)!, re.I) !== null;
      continue;
    }
    if (optional) continue;
    for (const m of LINK.finditer(line)) out.push(m.group(2)!);
  }
  return out;
}

export interface Doc { path: string; tokens: number; text: string }
export interface Lane { docs: Doc[]; tokens: number; budget: number; skipped: { path: string; why: string }[] }

/** README.md, then its links in order (skipping '## Optional', images and external URLs), then the links inside those
 * files, breadth first, each file taken whole when it fits the budget. */
export function loadLane(agents: string, budget: number): Lane | null {
  if (!py.io.isfile(path.join(agents, "README.md"))) return null;
  const docs: Doc[] = [];
  const seen = new Set<string>();
  const skipped: { path: string; why: string }[] = [];
  let used = 0;
  const queue: [string, number][] = [["README.md", 0]];
  while (queue.length) {
    const [rel0, depth] = queue.shift()!;
    const rel = path.normpath(rel0);
    if (seen.has(rel)) continue;
    seen.add(rel);
    const p = path.join(agents, rel);
    if (!py.io.isfile(p)) {
      skipped.push({ path: rel, why: "missing" });
      continue;
    }
    const text = readText(p) || "";
    const t = tokensEst(text);
    if (used + t > budget) {
      skipped.push({ path: rel, why: `over budget (${t} tokens, ${budget - used} left)` });
      continue;
    }
    used += t;
    docs.push({ path: rel, tokens: t, text });
    if (depth >= 2) continue;
    const links = rel === "README.md" ? readmeLinks(text) : [...LINK.finditer(text)].map((m) => m.group(2)!);
    const base = path.dirname(rel);
    for (const href of links) {
      if (re.match("^[a-z]+:", href) || href.startsWith("#")) continue;
      const target = py.str.split(href, "#", 1)[0]!;
      if (!target || endsWithAny(py.lower(target), SKIP_EXT) || SKIP_FILES.has(path.basename(target))) continue;
      queue.push([path.join(base, target), depth + 1]);
    }
  }
  return { docs, tokens: used, budget, skipped };
}

const hasToken = (tok: Py, text: string): boolean =>
  re.search(String.raw`(?<![a-z0-9])` + re.escape(ntok(tok)) + String.raw`(?![a-z0-9])`, ntok(text)) !== null;

/** Whether a JS string holds a surrogate code unit (an astral character), where UTF-16 and code point indexes part. */
const ASTRAL = {
  test(s: string): boolean {
    for (let i = 0; i < s.length; i++) {
      const c = s.charCodeAt(i);
      if (c >= 0xd800 && c <= 0xdfff) return true;
    }
    return false;
  },
};

/** text.lower().find(needle, start) mapped back onto text, by code point as Python indexes. */
function windows(text: string, needle: string): Generator<string> {
  const low = py.lower(text);
  if (!ASTRAL.test(text) && !ASTRAL.test(low) && !ASTRAL.test(needle)) {
    return (function* () {
      let start = low.indexOf(needle);
      while (start >= 0) {
        yield text.slice(Math.max(0, start - WINDOW), start + needle.length + WINDOW);
        start = low.indexOf(needle, start + 1);
      }
    })();
  }
  const L = Array.from(low);
  const T = Array.from(text);
  const N = Array.from(needle);
  return (function* () {
    for (let s = 0; s + N.length <= L.length; s++) {
      let ok = true;
      for (let j = 0; j < N.length && ok; j++) ok = L[s + j] === N[j];
      if (ok) yield T.slice(Math.max(0, s - WINDOW), s + N.length + WINDOW).join("");
    }
  })();
}

/** Where the answer sits next to its evidence in one loaded file (ev_probe.answerable). */
export function answerable(q: Question, docs: Doc[]): string | null {
  const ev: string[] = q.evidence.map((e) => pyStr(e));
  for (const d of docs) {
    const text = d.text;
    if (q.match === "all" || !ev.length) {
      if (ev.every((e) => py.lower(text).includes(py.lower(e))) && matches(q, text)) return d.path;
      continue;
    }
    if (q.match === "oneof") {
      if (py.str.splitlines(text).some((line) => hasToken(ev[0], line) && matches(q, line))) return d.path;
      continue;
    }
    const pr = q.match === "token" ? re.fullmatch(String.raw`PR (\d+)`, pyStr(q.expected)) : null;
    if (pr) {
      const g = pr.group(1)!;
      const cell = re.compile(String.raw`(^|\|)\s*\[?0*` + g + String.raw`\]?(\(|\s*\|)|\bpr-0*` + g + String.raw`\.md\b`, re.I);
      if (py.str.splitlines(text).some((line) => ev.every((e) => py.lower(line).includes(py.lower(e))) && cell.search(line) !== null)) return d.path;
    }
    for (const win of windows(text, py.lower(ev[0]!)))
      if (ev.slice(1).every((e) => py.lower(win).includes(py.lower(e))) && matches(q, win)) return d.path;
  }
  return null;
}

export const SYSTEM = "You are an implementing agent who has been handed a design packet. Answer each question using only the documents " +
  "provided. If the documents do not contain the answer, answer \"unknown\". Keep each answer short.";
export const SCHEMA = { type: "object", additionalProperties: false, required: ["answers"],
  properties: { answers: { type: "array", items: { type: "object", additionalProperties: false, required: ["id", "answer"],
    properties: { id: { type: "string" }, answer: { type: "string" } } } } } };

export const M_ANS = "questions whose evidence strings and expected answer occur together in one file the handoff loader read / questions";
export const M_AGENT = `a model (${L.JUDGE_MODEL}) given only the loaded lane answers every question in one structured-output call; ` +
  "per-question match by the template's match kind; value = matched / questions";
export const PROBE_SRC = "build/agents (handoff loader) + templates/eval/probe-questions.json";

/** The deterministic part: questions, the lane, probe_answerable. */
export function prepare(content0: Py, build: string, templatePath: string) {
  const content = py.truthy(content0) ? content0 : new Map();
  const tj = readJson(templatePath);
  const template = py.truthy(tj) ? tj : new Map();
  const b = get(template, "budget_tokens");
  const budget = py.truthy(b) ? Number(isNum(b) ? b : py.int(b)) : 20000;
  const qs = py.truthy(content) ? buildQuestions(content, template) : [];
  const lane = loadLane(path.join(build, "agents"), budget);
  const laneRec: Record<string, Py> = lane ? { tokens: lane.tokens, budget: lane.budget, skipped: lane.skipped, docs: lane.docs.map((d) => ({ path: d.path, tokens: d.tokens })) } : {};
  const rec = { schema: "tdd-eval-probe-run/1", template: templatePath, budget_tokens: budget, questions: qs, lane: laneRec };
  return { qs, lane, rec };
}

/** ev_probe.run, the part without a model: the questions, the lane, the probe record and probe_answerable. */
export function answerablePart(content: Py, build: string, templatePath: string) {
  const { qs, lane, rec } = prepare(content, build, templatePath);
  let rec1: ScoreRec;
  if (!qs.length) rec1 = notRun("probe_answerable", "no questions (no content.json)", M_ANS, PROBE_SRC);
  else if (!lane) rec1 = notRun("probe_answerable", "no agents/README.md", M_ANS, PROBE_SRC, { questions: qs.length });
  else {
    for (const q of qs) q.answerable_in = answerable(q, lane.docs);
    const hit = qs.filter((q) => q.answerable_in).length;
    rec1 = score("probe_answerable", ratio(hit, qs.length), M_ANS, PROBE_SRC,
      { questions: qs.length, answerable: hit, lane_tokens: lane.tokens, lane_files: lane.docs.length }, new Map(qs.map((q) => [q.id, q.answerable_in])));
  }
  return { qs, lane, rec, answerable: rec1 };
}

export const AGENT_SPEC = { name: "probe_agent", method: M_AGENT, source: PROBE_SRC } as const;

/** ev_probe.run, the model part (probe_agent): the call runs only with --with-agent-probe and a key, exactly as in the
 * Python, and returns its ledger row. Answers are written onto the questions (probe.json carries them). */
export const agentPart = (qs: Question[], lane: Lane | null, withAgentProbe: boolean, keys: L.LlmKeys) => Effect.gen(function* () {
  const ledger: L.LedgerRow[] = [];
  if (!qs.length) return { score: notRun("probe_agent", "no questions", M_AGENT, PROBE_SRC), ledger };
  if (!withAgentProbe) return { score: notRun("probe_agent", "pass --with-agent-probe", M_AGENT, PROBE_SRC, { questions: qs.length }), ledger };
  if (!lane) return { score: notRun("probe_agent", "no agents/README.md", M_AGENT, PROBE_SRC), ledger };
  const [ok, why] = yield* L.claudeAvailable(keys);
  if (!ok) return { score: notRun("probe_agent", why!, M_AGENT, PROBE_SRC, { questions: qs.length }), ledger };
  const docs = lane.docs.map((d, i) => `<document index="${i + 1}"><source>${d.path}</source><document_content>\n${d.text}\n</document_content></document>`).join("\n");
  const ask = qs.map((q) => `${q.id}: ${q.question}`).join("\n");
  const [data, err, row] = yield* L.claudeJson(keys, "probe", SYSTEM, `${docs}\n\nQuestions (answer every id):\n${ask}`, SCHEMA);
  ledger.push(row);
  const m: Record<string, Py> = { questions: qs.length, input_tokens: row.usage?.input_tokens ?? null, output_tokens: row.usage?.output_tokens ?? null };
  if (err) return { score: error("probe_agent", err, M_AGENT, PROBE_SRC, m), ledger };
  const got = new Map<Py, Py>(list(get(data, "answers", [])).map((a) => [get(a, "id"), get(a, "answer", "")]));
  for (const q of qs) {
    q.answer = got.get(q.id) ?? "";
    q.correct = matches(q, q.answer);
  }
  const hit = qs.filter((q) => q.correct).length;
  m.correct = hit;
  return { score: score("probe_agent", ratio(hit, qs.length), M_AGENT, PROBE_SRC, m, new Map(qs.map((q) => [q.id, q.correct]))), ledger };
});
