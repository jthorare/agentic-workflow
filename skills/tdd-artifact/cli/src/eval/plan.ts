// ev_push.plan: the Braintrust model, written to run.json on every run (exactly what a --push would send). One
// dataset `tdd-inputs` per project, one record per packet revision (id <repo>/<slug>@rev<rev>); per run one
// experiment per output kind named <slug>-rev<rev>-<kind>, each with ONE row whose input is the stable key
// {packet, output}, compared against the previous revision's experiment of the same kind. Nothing here sends.
import { readdirSync } from "node:fs";
import * as py from "../py";
import { pyStr } from "../py/repr";
import { type Py, get, getsize, isInt, isNum } from "./common";
import { type Input } from "./capture";
import { type Output } from "./score";

const re = py.re;
const path = py.path;

export const KINDS = ["artifact", "packet", "implementation", "qa", "bugs", "tokens"];
const MAX_SHOTS = 12;

export function btScores(out: Output): Map<string, Py> {
  const m = new Map<string, Py>();
  for (const s of out.scores) if (s.status === "ok" && s.value !== null) m.set(s.name, s.value);
  for (const s of out.scores) if (s.status === "not run" || s.status === "error") m.set(s.name, null);
  return m;
}

export function btMetrics(out: Output): Map<string, Py> {
  const m = new Map<string, Py>();
  for (const [k, v] of out.metrics) if (isNum(v) && typeof v !== "boolean") m.set(re.sub("[^A-Za-z0-9_]", "_", k), v);
  return m;
}

function sortedGlob(d: string, pred: (nm: string) => boolean): string[] {
  let names: string[];
  try {
    names = readdirSync(d);
  } catch {
    return [];
  }
  return py.sorted(names.filter((nm) => !nm.startsWith(".") && pred(nm)).map((nm) => path.join(d, nm)));
}

export function attachmentsFor(kind: string, page: string, out: string, packet: string, build: string): [string, string][] {
  const files: [string, string][] = [];
  if (kind === "artifact") {
    if (py.io.isfile(page)) files.push([page, "text/html"]);
    const shots = [...sortedGlob(path.join(out, "shots"), (nm) => nm.endsWith("-top.png")), ...sortedGlob(path.join(packet, "shots"), (nm) => nm.endsWith(".png"))];
    for (const p of shots.slice(0, MAX_SHOTS)) files.push([p, "image/png"]);
  } else if (kind === "packet") {
    const readme = path.join(build, "agents", "README.md");
    if (py.io.isfile(readme)) files.push([readme, "text/markdown"]);
  }
  return files;
}

export interface Experiment {
  project: string; name: string; base_experiment: string | null;
  row: { input: Record<string, Py>; output: Output; expected: Record<string, Py>; scores: Map<string, Py>; metrics: Map<string, Py>;
    metadata: Record<string, Py>; tags: string[] };
  attachments: { path: string; content_type: string; bytes: number }[];
}
export interface Run { schema: string; dataset: Record<string, Py>; experiments: Experiment[]; pushed?: boolean }

export interface PlanCtx { input: Input; meta: Py; page: string; out: string; packet: string; build: string }

export function plan(ctx: PlanCtx, outputs: Map<string, Output>, project: string, expBase: string, base0: string | null): Run {
  const inp = ctx.input;
  const pk = inp.packet;
  const rev = get(ctx.meta, "rev");
  const labels = inp.labels;
  const expected = {
    decisions: labels.decisions.map((d) => ({ id: d.id, question: d.question, answer: d.answer, status: d.status, state: d.state, label: d.label })),
    review_notes: labels.review_notes,
  };
  const tags = ["tdd", pk.slug, `rev${pyStr(rev)}`, pk.repo_slug];
  const dataset = { project, name: "tdd-inputs", id: pk.key, input: inp, expected,
    metadata: { slug: pk.slug, repo_slug: pk.repo_slug, rev, digest: inp.digest, tool: "eval_packet/1" }, tags };
  let base = base0;
  if (base === null && isInt(rev) && typeof rev !== "boolean" && Number(rev) > 1) base = `${pk.slug}-rev${Number(rev) - 1}`;
  const exps: Experiment[] = [];
  for (const kind of KINDS) {
    const out = outputs.get(kind);
    if (!out) continue;
    exps.push({
      project, name: `${expBase}-${kind}`, base_experiment: base ? `${base}-${kind}` : null,
      row: {
        input: { packet: `${pk.repo_slug}/${pk.slug}`, output: kind }, output: out,
        expected: { review_notes: labels.review_notes.filter((r) => get(r, "target") === kind), ...(kind === "artifact" || kind === "packet" ? { decisions: expected.decisions } : {}) },
        scores: btScores(out), metrics: btMetrics(out),
        metadata: { packet: pk.key, slug: pk.slug, rev, kind, dataset: "tdd-inputs", dataset_record_id: pk.key, input_digest: inp.digest, tool: "eval_packet/1",
          not_run: new Map(out.scores.filter((s) => s.status !== "ok").map((s) => [s.name, s.reason ?? null])) },
        tags: [...tags, kind],
      },
      attachments: attachmentsFor(kind, ctx.page, ctx.out, ctx.packet, ctx.build).map(([p, t]) => ({ path: p, content_type: t, bytes: getsize(p) })),
    });
  }
  return { schema: "tdd-eval-run/1", dataset, experiments: exps };
}
