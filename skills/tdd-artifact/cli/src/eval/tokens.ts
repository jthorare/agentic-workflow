// ev_tokens.py: OUTPUT 6, token usage and efficiency: the Claude Code session(s) that produced this revision
// (--session), the eval's own model calls (judge, probe), build sizes, and a cost-per-revision line. Costs are API
// list-price equivalents from llm.PRICES (a subscription bill differs); every number names how it was computed.
import { lstatSync, readFileSync } from "node:fs";
import * as py from "../py";
import { pyStr } from "../py/repr";
import { type Py, PyFloat, fixed, fl, get, getsize, globNames, isFloat, n, pyRound, pySum, readJson, tokensEst } from "./common";
import { type Input } from "./capture";
import * as L from "./llm";
import { type Output, type ScoreRec, metric, notRun, output } from "./score";

const path = py.path;

export const projects = (): string => path.expanduser("~/.claude/projects");

/** glob(~/.claude/projects/*\/<sid>.jsonl) and glob(~/.claude/projects/*\/<sid>/subagents/*.jsonl), in glob's order. */
export function sessionFiles(sid: string): [string[], string[]] {
  const root = projects();
  const main: string[] = [];
  const subs: string[] = [];
  for (const d of globNames(root, true)) {
    const p = path.join(root, d, `${sid}.jsonl`);
    if (py.io.exists(p) || isLink(p)) main.push(p);
  }
  for (const d of globNames(root, true)) {
    const sub = path.join(root, d, sid, "subagents");
    if (!py.io.isdir(sub)) continue;
    for (const f of globNames(sub)) if (f.endsWith(".jsonl")) subs.push(path.join(sub, f));
  }
  return [main, subs];
}

function isLink(p: string): boolean {
  try {
    return lstatSync(p).isSymbolicLink();
  } catch {
    return false;
  }
}

/** int(u.get(k) or 0) */
const intOf = (v: Py): number => (typeof v === "string" ? Number(py.int(v)) : Math.trunc(n(v)));

interface PerModel { messages: number; input_tokens: number; output_tokens: number; cache_read_input_tokens: number; cache_creation_input_tokens: number;
  cache_5m: number; cache_1h: number; usd: PyFloat; priced: boolean }

/** message.usage over assistant records, one per message.id (the last record wins), per model, with its cost. */
export function sumUsage(paths: string[]): Map<string, PerModel> {
  const last = new Map<Py, [string, Py]>();
  for (const p of paths) {
    let text: string;
    try {
      text = py.io.decodeUtf8(readFileSync(p), "replace");
    } catch {
      continue;
    }
    for (const line of splitKeepLines(text)) {
      if (!line.includes('"usage"')) continue;
      let o: Py;
      try {
        o = py.json.loads(line);
      } catch {
        continue;
      }
      const m = get(o, "message");
      if (!(m instanceof Map) || !(get(m, "usage") instanceof Map)) continue;
      const mid = py.truthy(get(m, "id")) ? get(m, "id") : get(o, "uuid");
      last.set(mid, [py.truthy(get(m, "model")) ? get(m, "model") : "unknown", m.get("usage")]);
    }
  }
  const per = new Map<string, PerModel>();
  for (const [model, u] of last.values()) {
    if (!per.has(model)) per.set(model, { messages: 0, input_tokens: 0, output_tokens: 0, cache_read_input_tokens: 0, cache_creation_input_tokens: 0,
      cache_5m: 0, cache_1h: 0, usd: fl(0), priced: true });
    const t = per.get(model)!;
    t.messages += 1;
    for (const k of ["input_tokens", "output_tokens", "cache_read_input_tokens", "cache_creation_input_tokens"] as const) t[k] += intOf(get(u, k));
    const cc = py.truthy(get(u, "cache_creation")) ? get(u, "cache_creation") : new Map();
    t.cache_5m += intOf(get(cc, "ephemeral_5m_input_tokens"));
    t.cache_1h += intOf(get(cc, "ephemeral_1h_input_tokens"));
    const c = L.cost(model, u);
    if (c === null) t.priced = false;
    else t.usd = fl(py.round(t.usd.value + c.value, 6));
  }
  return per;
}

/** Iterating a text file in Python: lines split after "\n" (universal newlines: "\r\n" and "\r" end lines too). */
function splitKeepLines(text: string): string[] {
  const t = py.io.universalNewlines(text);
  const out: string[] = [];
  let i = 0;
  while (i < t.length) {
    const j = t.indexOf("\n", i);
    if (j < 0) {
      out.push(t.slice(i));
      break;
    }
    out.push(t.slice(i, j + 1));
    i = j + 1;
  }
  return out;
}

export function evaluate(inp: Input, build: string, meta: Py, sessions: string[], ledger: L.LedgerRow[], packetOut: Output): Output {
  const key = inp.packet.key;
  const scores: ScoreRec[] = [];
  const data: Record<string, Py> = { price_source: L.PRICE_SOURCE };
  const sMethod = "sum of message.usage over assistant records in ~/.claude/projects/*/<session>.jsonl and <session>/subagents/*.jsonl, " +
    "deduplicated by message.id (last record wins); cost = ev_llm.cost per model (API list-price equivalent)";
  const sids = sessions.map((s) => py.str.strip(s)).filter((s) => s);
  let sessionUsd: number | PyFloat | null = null;
  if (!sids.length) scores.push(notRun("session_tokens", "pass --session <id> (the Claude Code session ids that built this revision)", sMethod, "~/.claude/projects"));
  else {
    const mains: string[] = [];
    const subs: string[] = [];
    const missing: string[] = [];
    for (const sid of sids) {
      const [m, s] = sessionFiles(sid);
      if (!m.length && !s.length) missing.push(sid);
      mains.push(...m);
      subs.push(...s);
    }
    if (!mains.length && !subs.length)
      scores.push(notRun("session_tokens", `no transcript for ${missing.join(", ")} under ~/.claude/projects`, sMethod, "~/.claude/projects"));
    else {
      const perMain = sumUsage(mains);
      const perSub = sumUsage(subs);
      const tot = (per: Map<string, PerModel>, k: keyof PerModel): number | bigint | PyFloat => pySum([...per.values()].map((v) => v[k]));
      const both = (k: keyof PerModel): number => n(tot(perMain, k)) + n(tot(perSub, k));
      const usdSum = [tot(perMain, "usd"), tot(perSub, "usd")];
      sessionUsd = pyRound(usdSum.some(isFloat) ? fl(n(usdSum[0]) + n(usdSum[1])) : n(usdSum[0]) + n(usdSum[1]), 4);
      const m = { input_tokens: both("input_tokens"), output_tokens: both("output_tokens"), cache_read_tokens: both("cache_read_input_tokens"),
        cache_write_tokens: both("cache_creation_input_tokens"), messages: both("messages"), usd_api_equivalent: sessionUsd, subagent_files: subs.length };
      const unpriced = py.sorted(new Set([...perMain, ...perSub].filter(([, v]) => !v.priced).map(([k]) => k)));
      scores.push(metric("session_tokens", m, sMethod, `${mains.length} session file(s), ${subs.length} subagent file(s)`,
        { main_by_model: perMain, subagents_by_model: perSub, unpriced_models: unpriced, missing_sessions: missing }));
    }
  }
  const eMethod = "usage returned by each judge/probe API call this run (ev_llm.LEDGER); cost by ev_llm.cost";
  const calls = [...ledger];
  const evalUsd = pyRound(pySum(calls.map((c) => (py.truthy(c.usd) ? c.usd : 0))), 6);
  if (!calls.length) scores.push(notRun("eval_calls", "no model call this run (no --with-judge / --with-agent-probe, or no key)", eMethod, "ev_llm.LEDGER"));
  else
    scores.push(metric("eval_calls", { calls: calls.length, input_tokens: pySum(calls.map((c) => c.usage?.input_tokens ?? 0)),
      output_tokens: pySum(calls.map((c) => c.usage?.output_tokens ?? 0)), usd: evalUsd, unpriced_calls: calls.filter((c) => c.usd === null || c.usd === undefined).length },
    eMethod, "ev_llm.LEDGER", calls));
  const sizes: Record<string, Py> = {};
  for (const [name, rel] of [["content_json", "content.json"], ["tdd_html", "tdd.html"], ["content_files_json", "content.files.json"]] as const) {
    const p = path.join(build, rel);
    if (py.io.isfile(p)) {
      sizes[`${name}_bytes`] = getsize(p);
      if (rel !== "tdd.html") sizes[`${name}_tokens_est`] = tokensEst(getsize(p));
    }
  }
  const man = readJson(path.join(build, "content.files.json"));
  const filesD = get(py.truthy(man) ? man : new Map(), "files");
  const media = (py.truthy(filesD) ? [...(filesD as Map<string, Py>).values()] : []).filter((v) => typeof v === "string" && py.io.isfile(v));
  if (media.length) {
    sizes.media_files = media.length;
    sizes.media_bytes = media.reduce((a, v) => a + getsize(v), 0);
  }
  const lane = packetOut.scores.find((s) => s.name === "lane_size" && s.status === "ok");
  if (lane && lane.metrics) for (const [k, v] of lane.metrics instanceof Map ? lane.metrics : Object.entries(lane.metrics)) sizes[`agents_${k}`] = v;
  if (inp.source) {
    sizes.source_bytes = inp.source.bytes;
    sizes.source_tokens_est = tokensEst(inp.source.bytes);
  }
  scores.push(metric("build_sizes", sizes, "bytes on disk; *_tokens_est = bytes / 3.6 (an estimate, not a tokenizer)", "build/"));
  const rev = get(meta, "rev");
  const parts = [sessionUsd !== null ? `session $${fixed(sessionUsd, 4)}` : "session not measured (no --session)", `eval calls $${fixed(evalUsd, 4)}`];
  const measuredAny = sessionUsd !== null || n(evalUsd) > 0;
  const totalParts = [sessionUsd !== null && py.truthy(sessionUsd) ? sessionUsd : 0, evalUsd];
  const total = totalParts.some(isFloat) ? fl(n(totalParts[0]) + n(totalParts[1])) : n(totalParts[0]) + n(totalParts[1]);
  const line = measuredAny ? `rev ${pyStr(rev)}: $${fixed(total, 4)} API-equivalent (${parts.join("; ")})`
    : `rev ${pyStr(rev)}: cost not measured (no --session and no judge/probe calls this run)`;
  scores.push(metric("cost_per_revision", { usd_api_equivalent: measuredAny ? pyRound(total, 4) : null, session_measured: sessionUsd !== null ? 1 : 0 },
    "session cost (when --session) + this run's eval-call cost; API list-price equivalent, not a bill", "ev_tokens", { line }));
  data.cost_line = line;
  return output("tokens", key, scores, data);
}
