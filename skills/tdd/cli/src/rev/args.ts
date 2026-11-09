// `tdd rev` argv: the packet, the step selection (--from, --only, --reuse), the flags passed to the eval and
// artifact_check (--no-shots, --no-gh), the push gate (--push), the publish record for load-db (--published) and the
// output form (--json).
import { SURFACES, type Surface } from "../db/plan";
import { STEPS, stepName, type StepName } from "./steps";

export interface RevArgs {
  readonly packet: string;
  readonly noShots: boolean;
  readonly noGh: boolean;
  readonly push: boolean;
  readonly reuse: boolean;
  readonly json: boolean;
  readonly published: { readonly surface: Surface; readonly url: string } | null;
  readonly from: StepName | null;
  readonly only: StepName | null;
}

export const USAGE = `usage: tdd rev <packet-dir> [--no-shots] [--no-gh] [--push] [--published <artifact|notion|other> <url>]
                    [--from <step>] [--only <step>] [--reuse] [--json]

  Runs SKILL.md's build steps (step 5) for one packet as one program, in this order:
    compile         src/compile in-process: <slug>.md -> build/content.json (the line tdd build prints)
    app-build       bun run app/build.ts <build> -> build/tdd.html
    hydrate         bun run app/check_hydrate.ts <build>/tdd.html: zero console errors on hydrate
    agents          src/agents in-process -> build/agents/
    lint            src/lint in-process -> build/lint.json (findings are measured here; the step fails only when
                    the linter cannot run)
    eval            the eval lane -> build/eval/ (today eval_packet.py in the eval venv; not run when it is absent)
    load-db         tdd load-db after lint; skipped when Postgres is unreachable ($DATABASE_URL, else
                    postgres://localhost/tdd_system)
    push            only with --push: the eval's Braintrust push (BRAINTRUST_API_KEY from the environment)
    artifact-check  node <system-design>/scripts/artifact_check.mjs build/tdd.html shots
  Every step appends to build/.rev/journal.jsonl and records its outcome (ok, skipped with a reason, or failed with
  the error and its stderr tail) in build/.rev/state.json. With neither --from nor --only, a run resumes at the
  first step not done in the current cycle, or at an earlier step whose inputs changed since it ran; once every
  step is done, the next run starts a new cycle at compile.

  --from <step>   run that step and every step after it
  --only <step>   run that one step
  --reuse         skip a step whose inputs and outputs are unchanged since its last ok (noted in the journal)
  --no-shots      contract checks only, no screenshots (passed to the eval and to artifact-check)
  --no-gh         the eval does not call gh
  --push          let the eval push to Braintrust. Without it no key reaches a child process and the eval runs
                  with --skip linear,aws; with --no-gh as well, nothing connects beyond the local database.
  --published     record that this build was published on that surface (passed to load-db)
  --json          print the final table as JSON on stdout; the steps' own output goes to stderr
  Steps: ${STEPS.join(", ")}.
  Keys come only from the environment and never reach the journal. Exit 0 when every step that ran is ok or
  skipped, 1 when one failed, 2 on bad usage, 130 or 143 when interrupted (the next run resumes at that step).`;

/** Parse argv: the arguments, or a usage message (exit 2), or "help" (exit 0). */
export function parseArgs(argv: readonly string[]): RevArgs | { error: string } | "help" {
  const pos: string[] = [];
  let noShots = false, noGh = false, push = false, reuse = false, json = false;
  let published: RevArgs["published"] = null;
  let from: StepName | null = null, only: StepName | null = null;
  const step = (flag: string, v: string | undefined): StepName | { error: string } => {
    if (v === undefined || v.startsWith("--")) return { error: `${flag} needs a step (${STEPS.join(", ")})` };
    return stepName(v) ?? { error: `${flag}: unknown step ${v} (${STEPS.join(", ")})` };
  };
  for (let i = 0; i < argv.length; i++) {
    const x = argv[i]!;
    if (x === "-h" || x === "--help") return "help";
    else if (x === "--no-shots") noShots = true;
    else if (x === "--no-gh") noGh = true;
    else if (x === "--push") push = true;
    else if (x === "--reuse") reuse = true;
    else if (x === "--json") json = true;
    else if (x === "--from" || x === "--only") {
      const s = step(x, argv[++i]);
      if (typeof s !== "string") return s;
      if (x === "--from") from = s;
      else only = s;
    } else if (x === "--published") {
      const surface = argv[++i], url = argv[++i];
      if (!surface || !url || surface.startsWith("--") || url.startsWith("--")) return { error: "--published needs a surface and a url" };
      if (!(SURFACES as readonly string[]).includes(surface)) return { error: `--published surface must be one of ${SURFACES.join(", ")}` };
      if (!/^https?:\/\/\S+$/.test(url)) return { error: "--published url must be an http(s) url" };
      published = { surface: surface as typeof SURFACES[number], url };
    } else if (x.startsWith("-")) return { error: `unknown option ${x}` };
    else pos.push(x);
  }
  if (pos.length !== 1) return { error: pos.length ? `one packet folder, not ${pos.length}` : "a packet folder is required" };
  if (from && only) return { error: "--from and --only cannot be combined" };
  return { packet: pos[0]!, noShots, noGh, push, reuse, json, published, from, only };
}
