// Keys, from the environment only (never from a file), as Effect Config values wrapped in Redacted: printing, logging
// or JSON-encoding one shows "<redacted>", so a key cannot reach a record, run.json or stdout by accident. A key is
// read with Redacted.value only at the call that sends it (the push, a Linear or Anthropic request, the AWS client).
// An empty variable counts as absent, as `os.environ.get(...)` being falsy does in the Python.
import { Config, ConfigProvider, Effect, Option, Redacted } from "effect";

export interface Keys {
  readonly braintrust: Option.Option<Redacted.Redacted<string>>;
  readonly anthropic: Option.Option<Redacted.Redacted<string>>;
  readonly openai: Option.Option<Redacted.Redacted<string>>;
  readonly linear: Option.Option<Redacted.Redacted<string>>;
  readonly aws: {
    readonly accessKeyId: Option.Option<Redacted.Redacted<string>>;
    readonly secretAccessKey: Option.Option<Redacted.Redacted<string>>;
    readonly sessionToken: Option.Option<Redacted.Redacted<string>>;
    readonly region: Option.Option<string>;
  };
}

const key = (name: string) => Config.option(Config.Redacted(name));

export const KeysConfig: Config.Config<Keys> = Config.all({
  braintrust: key("BRAINTRUST_API_KEY"),
  anthropic: key("ANTHROPIC_API_KEY"),
  openai: key("OPENAI_API_KEY"),
  linear: key("LINEAR_API_KEY"),
  aws: Config.all({
    accessKeyId: key("AWS_ACCESS_KEY_ID"),
    secretAccessKey: key("AWS_SECRET_ACCESS_KEY"),
    sessionToken: key("AWS_SESSION_TOKEN"),
    region: Config.option(Config.String("AWS_REGION")).pipe(
      Config.flatMap((r) => (Option.isSome(r) ? Config.succeed(r) : Config.option(Config.String("AWS_DEFAULT_REGION"))))),
  }),
});

/** The keys from an environment record (process.env by default). */
export function loadKeys(env: Record<string, string | undefined> = process.env): Effect.Effect<Keys, Config.ConfigError> {
  const clean: Record<string, string> = {};
  for (const [k, v] of Object.entries(env)) if (v !== undefined && v !== "") clean[k] = v;
  return KeysConfig.parse(ConfigProvider.fromEnv({ env: clean }));
}

export const has = (k: Option.Option<unknown>): boolean => Option.isSome(k);
/** The secret itself, for the one call that sends it. */
export const reveal = (k: Option.Option<Redacted.Redacted<string>>): string | null => (Option.isSome(k) ? Redacted.value(k.value) : null);
