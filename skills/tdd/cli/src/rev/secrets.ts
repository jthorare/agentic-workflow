// Keys come only from the environment, read as Redacted config values, and never reach the journal, the state, the
// table or the progress lines: every string the runner writes passes through `scrub`. No child process gets a key it
// does not need. A secret is any variable named in KEY_NAMES, any AWS_* variable, and any name ending in _KEY, _TOKEN,
// _SECRET or _PASSWORD. Every child gets the environment without them; while the run lasts they are also taken out
// of process.env (run.ts), so the children of the in-process tools (lint's git, PARITY's python3) inherit none.
// Exceptions, passed by name: load-db gets DATABASE_URL; with --push the eval gets what its push and its sources read
// (EVAL_PUSH_KEYS), and nothing else gets a key, with or without --push (D10's gate: no code path can push by accident).
import { Config, Context, Effect, Layer, Option, Redacted } from "effect";

/** Keys the skill's tools read: the eval lane's (Braintrust, the model keys, Linear, AWS), gh's, and the database url. */
export const KEY_NAMES = [
  "BRAINTRUST_API_KEY", "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LINEAR_API_KEY", "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY",
  "AWS_SESSION_TOKEN", "GH_TOKEN", "GITHUB_TOKEN", "DATABASE_URL",
] as const;

/** Whether a variable is a secret: never in a child's environment unless passed by name. */
export const isSecretName = (name: string): boolean =>
  (KEY_NAMES as readonly string[]).includes(name) || name.startsWith("AWS_") || /(?:_KEY|_TOKEN|_SECRET|_PASSWORD)$/.test(name);

/** What the eval gets with --push: the push key, and the keys its Linear, AWS and (without --no-gh) gh sources read.
 *  Never the model keys: the runner asks for no judge and no probe. "AWS_*" is every AWS_ variable. */
export const evalPushKeys = (noGh: boolean): string[] =>
  ["BRAINTRUST_API_KEY", "LINEAR_API_KEY", "AWS_*", ...(noGh ? [] : ["GH_TOKEN", "GITHUB_TOKEN"])];

export interface SecretsApi {
  /** Names of the secrets set in the environment (names only, for the journal). */
  readonly present: readonly string[];
  readonly braintrust: Option.Option<Redacted.Redacted<string>>;
  readonly databaseUrl: Option.Option<Redacted.Redacted<string>>;
  /** Replace every secret value (and a database url's password) in `s` with <redacted:NAME>. */
  readonly scrub: (s: string) => string;
  /** A child's environment: the parent's as the run started, without any secret, plus the secrets named in `pass`
   *  ("AWS_*" for every AWS_ variable). */
  readonly childEnv: (pass?: readonly string[]) => Record<string, string>;
}

export class Secrets extends Context.Service<Secrets, SecretsApi>()("tdd/rev/Secrets") {}

/** Build the service from named Redacted values and the environment the run started with (copied). */
export function makeSecrets(values: ReadonlyMap<string, Redacted.Redacted<string>>, env: Record<string, string | undefined>): SecretsApi {
  const snapshot: Record<string, string> = {};
  for (const [k, v] of Object.entries(env)) if (v !== undefined) snapshot[k] = v;
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
    childEnv: (pass = []) => {
      const out: Record<string, string> = {};
      for (const [k, v] of Object.entries(snapshot)) {
        if (!isSecretName(k) || pass.includes(k) || (k.startsWith("AWS_") && pass.includes("AWS_*"))) out[k] = v;
      }
      return out;
    },
  };
}

/** The live service: every secret-named variable in the environment, each read through Config.Redacted (the
 *  environment provider). */
export const SecretsLive = Layer.effect(
  Secrets,
  Effect.gen(function* () {
    const values = new Map<string, Redacted.Redacted<string>>();
    const names = [...new Set([...KEY_NAMES, ...Object.keys(process.env).filter(isSecretName)])];
    for (const name of names) {
      const v = yield* Config.option(Config.Redacted(name)).pipe(Effect.orElseSucceed(() => Option.none()));
      if (Option.isSome(v) && Redacted.value(v.value) !== "") values.set(name, v.value);
    }
    return makeSecrets(values, process.env);
  }),
);

/** Take every secret out of process.env (the in-process tools spawn children with it); returns the restore. */
export function withdrawSecrets(): () => void {
  const saved: [string, string][] = [];
  for (const [k, v] of Object.entries(process.env)) {
    if (v !== undefined && isSecretName(k)) {
      saved.push([k, v]);
      delete process.env[k];
    }
  }
  return () => { for (const [k, v] of saved) process.env[k] = v; };
}
