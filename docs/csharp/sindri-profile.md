# Sindri profile reference

The profile of the C# Sindri app (`src/Sindri`), schema version 1. It documents the keys of `SindriProfile`
(`src/Sindri/Aw.Sindri.Core/Profile/`), read through `AwJson`. It replaces the reference of the TypeScript profile
(`sindri/src/profile/schema.ts`), which is generated separately and stays until the switch. Unknown keys are errors;
new optional keys join version 1, renames and removals bump it. `node scripts/check-profile-doc.mjs` fails when a
`SindriProfile` key is missing from this page.

The profile is a YAML file, by default `$AW_SINDRI_HOME/profile/profile.yaml` (`AW_SINDRI_PROFILE`). A changed profile
takes effect only after `sindri profile approve` (a human approval). The machine's terminal and extra agent entries are
not in the profile: they live in `launcher.json` (`docs/csharp/sindri-launcher.md`).

## profile.yaml

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `schemaVersion` | `1` | yes |  | The format version; this build reads 1 |
| `user` | string | yes |  | Who runs this Sindri: claim refs `sindri/<user>/<item>` and the event actor's owner |
| `tenant` | string | no | `"local"` | The tenant key every row carries (factory addendum §17.20); one value until team mode |
| `repos` | `RepoEntry[]` | no | none | The repos in the path |
| `services` | map of service name to `ServiceSettings` | no | none | Background services by name, each with its cadence |
| `api` | `ApiSettings` | no | `{ port: 7410 }` | The local API's settings |
| `workflow` | `WorkflowSettings` | no | all defaults | The workflow engine's times (engine spec S1-D11). Left out of the canonical JSON when absent, so a profile without it keeps its approved hash |
| `context` | object | no | none | The context engine's section (context spec §8.8). Sindri keeps the JSON and the engine checks its keys at validate and approve. `context.labeling.maxTokensPerSync` (int > 0, default 50000) caps the model tokens one intent-source sync may spend extracting decisions; over it, the rest stays queued (context spec §8.8, `docs/sindri/context.md`). `context.eval.adjudicator` (bool, default false) lets `context eval backtest` ask the model to label surfaces no PR touched (two runs in reversed order, agreeing answers only), within 1.5M tokens per run (context spec §9.1, §9.4, `docs/sindri/context.md` S-7A). `context.extract.recognizers` (array of `{ kind, glob, pattern, keyFrom }`, default none, at most 50, a pattern at most 512 characters) adds surface recognizers to the built-in ones: `kind` is a surface kind, `glob` a repo-relative glob (a denied path is never read), `pattern` a regex whose named group `keyFrom` becomes `surface:<kind>/<name>`; matches are suggested, never confirmed; a bad entry is SND-CONTEXT-130, too many or too long is 131 (scoping station C2, `docs/csharp/sindri-context.md`) |
| `index` | `IndexSettings` | no | none | Deny paths for every repo's map extraction |
| `launch` | `LaunchSettings` | no | none | The launch targets and the confirm window (UI spec E-4). Left out of the canonical JSON when absent, so a profile without it keeps its approved hash |
| `agents` | `AgentSettings` | no | none | Per-agent budgets and the argv hashes unattended Steps may run (screens spec E-28, launcher L-11). Left out of the canonical JSON when absent |

### `repos[]`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `name` | string | yes |  | The repo's short name |
| `path` | string | yes |  | The absolute path of its checkout |
| `index` | `IndexSettings` | no | none | This repo's `index:` section; its deny paths join the profile's |

### `services.<name>`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `every` | string | yes |  | The cadence: a whole number and a unit, `s`, `m` or `h` (for example `30s`), from `5s` to `24h` |

### `api`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `port` | integer | no | `7410` | The loopback port; 0 picks a free one |

### `workflow`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `deskTimeout` | duration (`1m`, `4h`, `2d`) | no | `2d` | The floor of every desk Step's timeout (W20) |
| `eventHold` | duration | no | `24h` | How long a buffered event signal waits for its wait before it is dropped with `workflow.expired` (§5.4) |
| `effects` | string[] (`effect.*` block ids) | no | none | The effect allowlist (§5.7, RCP-EFFECT-001): the effects a recipe may dispatch. Absent: RCP-EFFECT-001 is skipped and every recipe effect is refused at dispatch (SND-WF-011). An entry that isn't an `effect.*` id is refused before approval |
| `dispatchDelay` | duration (`5s` to `24h`, unit `s`, `m` or `h`) | no | `10s` | The undo window before each effect's activity runs (§5.7); the dev profile sets `60s` |

### `index`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `denyPaths` | string[] | no | none | Repo-relative globs map extraction never reads (`private/**`, `**/*.generated.ts`), added to the built-in secret globs and never replacing them (context spec §6.2). A repo's own are added to the profile's |

### `launch`

Every key that names an agent takes any `AgentId` (pattern `^[a-z][a-z0-9-]{1,31}$`); the profile checks the pattern and never the
catalog, because the catalog is per box.

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `default` | agent id | no | none | `launch.default`: the default launch target (UI spec E-4) |
| `repos.<repo>.default` | agent id | no | none | `launch.repos.<repo>.default`: a repo's default launch target (UI spec E-4) |
| `confirmWindow` | duration | no | `2h` | `launch.confirmWindow`: how long a started launch asks for confirmation before another (UI spec E-4) |

### `agents` (and `agents.<id>`)

A map of agent id to its limits, plus the reserved sibling `softLimit`. An id is lowercase only, so `softLimit` can never collide with one.

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `dailyBudget` | decimal (USD) | no | none | `agents.<id>.dailyBudget`: the agent's daily budget; a target with no usage reader and a budget is refused (`SND-LAUNCH-010`; screens spec E-28) |
| `stepArgvHash` | string or string[] | no | none | `agents.<id>.stepArgvHash`: one `argvHash` or a list, the hashes an unattended Step may run for this agent when its entry isn't built in or a `bin`-only override (SL8; session-launcher spec L-11) |
| `softLimit` | fraction, `0 < x <= 1` | no | `0.8` | `agents.softLimit`: the share of a daily budget that raises the soft warning (screens spec E-28) |

## Keys specified, not yet in `SindriProfile`

These keys are specified by other lanes' specs and join the profile in their phases. Until then the C# profile
refuses them as unknown keys. They are listed here so the launcher's files and the profile agree on names. Every key that
names an agent takes any `AgentId` (pattern `^[a-z][a-z0-9-]{1,31}$`); the profile checks the pattern and never the
catalog, because the catalog is per box.

| Key | Meaning | Specified in |
|---|---|---|
| `workflow.agent.target` | The agent a Step launches | workflow engine spec §6.5 |
