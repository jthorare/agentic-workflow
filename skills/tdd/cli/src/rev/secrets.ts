// Keys come only from the environment, read as Redacted config values, and never reach the journal, the state or the
// table: every string the runner writes passes through `scrub`. Child processes get the environment without the keys
// that could send data off the machine, except BRAINTRUST_API_KEY for the eval when --push is given (D10's gate: no
// code path can push by accident).
import { Config, Context, Effect, Layer, Option, Redacted } from "effect";

/** Keys the skill's tools read (the eval lane's five, gh's, and the database url, which may hold a password). */
export const KEY_NAMES = [
  "BRAINTRUST_API_KEY", "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LINEAR_API_KEY", "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY",
  "AWS_SESSION_TOKEN", "GH_TOKEN", "GITHUB_TOKEN", "DATABASE_URL",
] as const;
/** Any other variable whose name says it is a secret is scrubbed too. */
const SECRET_NAME = /(?:_API_KEY|_TOKEN|_SECRET|_PASSWORD|SECRET_ACCESS_KEY)$/;
/** Never passed to a child without --push: the push key, the model keys (the runner never asks for a judge or a probe)
 *  and the Linear key (the eval's Linear source; the runner also skips it, and AWS, without --push). */
export const WITHHELD = ["BRAINTRUST_API_KEY", "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LINEAR_API_KEY"] as const;

export interface SecretsApi {
  /** Names of the keys set in the environment (names only, for the journal). */
  readonly present: readonly string[];
  readonly braintrust: Option.Option<Redacted.Redacted<string>>;
  readonly databaseUrl: Option.Option<Redacted.Redacted<string>>;
  /** Replace every key value (and a database url's password) in `s` with <redacted:NAME>. */
  readonly scrub: (s: string) => string;
  /** The environment for a child process: the parent's, without WITHHELD unless `push` (then BRAINTRUST_API_KEY stays). */
  readonly childEnv: (push: boolean) => Record<string, string>;
}

export class Secrets extends Context.Service<Secrets, SecretsApi>()("tdd/rev/Secrets") {}

/** Build the service from named Redacted values and the environment the children inherit. */
export function makeSecrets(values: ReadonlyMap<string, Redacted.Redacted<string>>, env: Record<string, string | undefined>): SecretsApi {
  const pairs: [string, string][] = [];
  for (const [name, r] of values) {
    const v = Redacted.value(r);
    // a value this short is not a key (a flag such as "true"); scrubbing it would garble every line it appears in
    if (v.length >= 8) pairs.push([name, v]);
    if (name === "DATABASE_URL") {
      try {
        const pw = decodeURIComponent(new URL(v).password);
        if (pw.length >= 4) pairs.push([name, pw]);
      } catch {
        // not a url: the whole value is scrubbed above
      }
    }
  }
  pairs.sort((a, b) => b[1].length - a[1].length); // longest first, so a password inside a url never splits it
  const scrub = (s: string): string => {
    let t = s;
    for (const [name, v] of pairs) if (t.includes(v)) t = t.split(v).join(`<redacted:${name}>`);
    return t;
  };
  return {
    present: [...values.keys()].sort(),
    braintrust: Option.fromNullishOr(values.get("BRAINTRUST_API_KEY")),
    databaseUrl: Option.fromNullishOr(values.get("DATABASE_URL")),
    scrub,
    childEnv: (push) => {
      const out: Record<string, string> = {};
      for (const [k, v] of Object.entries(env)) if (v !== undefined) out[k] = v;
      for (const k of WITHHELD) if (!(push && k === "BRAINTRUST_API_KEY")) delete out[k];
      return out;
    },
  };
}

/** The live service: each known key through Config.Redacted (the environment provider), plus any other secret-named
 *  variable in the environment. */
export const SecretsLive = Layer.effect(
  Secrets,
  Effect.gen(function* () {
    const values = new Map<string, Redacted.Redacted<string>>();
    for (const name of KEY_NAMES) {
      const v = yield* Config.option(Config.Redacted(name)).pipe(Effect.orElseSucceed(() => Option.none()));
      if (Option.isSome(v) && Redacted.value(v.value) !== "") values.set(name, v.value);
    }
    for (const [name, v] of Object.entries(process.env)) {
      if (!values.has(name) && v && SECRET_NAME.test(name)) values.set(name, Redacted.make(v, { label: name }));
    }
    return makeSecrets(values, process.env);
  }),
);
