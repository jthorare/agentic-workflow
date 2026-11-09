// ev_push.push: send run.json's plan to Braintrust. This module is imported only under --push (run.ts loads it with a
// dynamic import after every output is written), and it imports the optional `braintrust` SDK only when it runs, so
// no code path can push by accident (D10). The key is BRAINTRUST_API_KEY from the environment (Redacted config),
// revealed only into the SDK's login call. No test runs this module: the first TypeScript push is the integrator's,
// after the owner's go (D10, D18).
//
// SDK calls (the braintrust JavaScript SDK's documented surface, written from its docs and NOT exercised here: there
// was no key and no install in this lane; the N8 review checked these names against braintrust 3.43.0's
// dist/index.d.ts; check them against the installed SDK before the first push):
//   login({apiKey}); initDataset({project, dataset, description}) -> Dataset; Dataset.insert({id, input, expected,
//   metadata, tags}); Dataset.summarize() -> {datasetUrl}; init({project, experiment, description, baseExperiment,
//   metadata, tags, gitMetadataSettings: {collect: "none"}, setCurrent: false}) -> Experiment; Experiment.log({input,
//   output, expected, scores, metrics, metadata, tags}); new Attachment({data: <path>, filename, contentType});
//   Experiment.summarize() -> {experimentName, experimentUrl, comparisonExperimentName, scores}; flush().
import { Data, Effect, Option } from "effect";
import * as py from "../py";
import { pyStr } from "../py/repr";
import { type Py, importOptional } from "./common";
import { type Keys, reveal } from "./config";
import { type Run } from "./plan";

/** The Python's SystemExit message: printed alone on stderr, exit 1. */
export class PushExit extends Data.TaggedError("PushExit")<{ readonly message: string }> {}
/** Any other push failure: "push failed: <Type>: <message>", exit 1. */
export class PushFailed extends Data.TaggedError("PushFailed")<{ readonly type: string; readonly message: string }> {}

/** A kernel value (Maps, PyFloat) as the plain JSON the SDK serialises. */
const plain = (v: unknown): Py => JSON.parse(py.json.dumps(v, { default: (o) => pyStr(o) }));

const SPEC = "braintrust";

export const push = (run: Run, keys: Pick<Keys, "braintrust">) => Effect.gen(function* () {
  if (Option.isNone(keys.braintrust)) return yield* new PushExit({ message: "--push needs BRAINTRUST_API_KEY in the environment (never in a file)" });
  // ev_push imports braintrust inside push(): ImportError and any other failure end as "push failed: <Type>: <msg>"
  const bt: Py = yield* Effect.tryPromise({ try: () => importOptional(SPEC),
    catch: (e) => new PushFailed({ type: (e as Error)?.name ?? "Error", message: String((e as Error)?.message ?? e) }) });
  if (!bt) return yield* new PushFailed({ type: "ModuleNotFoundError", message: `No module named '${SPEC}' (bun add --no-save braintrust in skills/tdd/cli)` });
  const call = <A>(f: () => Promise<A> | A) => Effect.tryPromise({
    try: async () => f(),
    catch: (e) => new PushFailed({ type: (e as Error)?.name ?? "Error", message: String((e as Error)?.message ?? e) }),
  });
  const apiKey = reveal(keys.braintrust)!;
  yield* call(() => bt.login({ apiKey }));
  const d = run.dataset;
  const ds = yield* call(() => bt.initDataset({ project: d.project, dataset: d.name, description: "House TDD eval inputs: one record per packet revision", apiKey }));
  yield* call(() => ds.insert({ id: d.id, input: plain(d.input), expected: plain(d.expected), metadata: plain(d.metadata), tags: d.tags }));
  const dsum: Py = yield* call(() => ds.summarize());
  const result: { dataset_url: Py; experiments: Py[] } = { dataset_url: dsum?.datasetUrl ?? null, experiments: [] };
  for (const e of run.experiments) {
    const exp: Py = yield* call(() => bt.init({ project: e.project, experiment: e.name,
      description: `TDD eval: ${pyStr(e.row.metadata.packet)} (${pyStr(e.row.metadata.kind)})`, baseExperiment: e.base_experiment ?? undefined,
      metadata: plain(e.row.metadata), tags: e.row.tags, gitMetadataSettings: { collect: "none" }, setCurrent: false, apiKey }));
    const output: Py = plain(e.row.output);
    if (e.attachments.length)
      output.files = e.attachments.map((a) => new bt.Attachment({ data: a.path, filename: py.path.basename(a.path), contentType: a.content_type }));
    yield* call(() => exp.log({ input: e.row.input, output, expected: plain(e.row.expected), scores: plain(e.row.scores), metrics: plain(e.row.metrics),
      metadata: plain(e.row.metadata), tags: e.row.tags }));
    const s: Py = yield* call(() => exp.summarize());
    const diffs: Record<string, Py> = {};
    for (const [k, v] of Object.entries((s?.scores ?? {}) as Record<string, Py>))
      diffs[k] = { score: v?.score ?? null, diff: v?.diff ?? null, improvements: v?.improvements ?? null, regressions: v?.regressions ?? null };
    result.experiments.push({ name: s?.experimentName ?? e.name, url: s?.experimentUrl ?? null, compared_to: s?.comparisonExperimentName ?? null, scores: diffs });
  }
  yield* call(() => bt.flush());
  return result;
});
