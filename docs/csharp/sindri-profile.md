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
| `context` | object | no | none | The context engine's section (context spec §8.8). Sindri keeps the JSON and the engine checks its keys at validate and approve. `context.cascadeComponentFanOut` (int 1 to 50, default 5) caps how many features one code unit may implement for the opt-in `component` cascade hop to walk through it; a unit over the cap is skipped and reported in `skippedComponents` (scoping station C4, `docs/csharp/sindri-context.md`). `context.labeling.maxTokensPerSync` (int > 0, default 50000) caps the model tokens one intent-source sync may spend extracting decisions; over it, the rest stays queued (context spec §8.8, `docs/sindri/context.md`). `context.eval.adjudicator` (bool, default false) lets `context eval backtest` ask the model to label surfaces no PR touched (two runs in reversed order, agreeing answers only), within 1.5M tokens per run (context spec §9.1, §9.4, `docs/sindri/context.md` S-7A). `context.extract.recognizers` (array of `{ kind, glob, pattern, keyFrom }`, default none, at most 50, a pattern at most 512 characters) adds surface recognizers to the built-in ones: `kind` is a surface kind, `glob` a repo-relative glob (a denied path is never read), `pattern` a regex whose named group `keyFrom` becomes `surface:<kind>/<name>`; matches are suggested, never confirmed; a bad entry is SND-CONTEXT-130, too many or too long is 131 (scoping station C2, `docs/csharp/sindri-context.md`). `context.extract.graphify` (`{ timeoutMinutes, maxSnapshotMB, maxFileMB, maxGraphMB }`, whole numbers, defaults 60, 2048, 16, 256, `0` is no limit; `timeoutMinutes` at most 70,000) bounds the graphify cluster pass of a map build; a negative, fractional or over-the-cap value (`timeoutMinutes` over 70,000, a size over 10,000,000) is SND-CONTEXT-133 at the key path (`docs/sindri/context.md` S-4). `context.code.schemaGlobs` (string[], default migration and schema globs; `[]` turns the schema scan off) and `context.code.testGlobs` (string[], default common test globs) steer the code lens's lexical search (`sindri context code`, context spec S-CL). Globs are repo-relative, at most 50, and never override `denyPaths` |
| `index` | `IndexSettings` | no | none | Deny paths for every repo's map extraction |
| `launch` | `LaunchSettings` | no | none | The launch targets and the confirm window (UI spec E-4). Left out of the canonical JSON when absent, so a profile without it keeps its approved hash |
| `agents` | `AgentSettings` | no | none | Per-agent budgets and the argv hashes unattended Steps may run (screens spec E-28, launcher L-11). Left out of the canonical JSON when absent |
| `setup` | `SetupSettings` | no | none | What Sindri does on its own while the owner sets it up once (setup-once spec §7.1). Left out of the canonical JSON when absent, so a profile without it keeps its approved hash |

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
| `agent` | object (`target`) | no | none | The agent runtime (engine spec §6.5, launcher L-14). `agent.target` is the agent a Step whose runtime is `agent` launches: any `AgentId` (pattern `^[a-z][a-z0-9-]{1,31}$`), checked by pattern only, never against the catalog (the catalog is per box). Read from the approved profile when an instance starts. Absent, or with no launcher registered: there is no agent runtime, a deskCapable Step runs at the desk and any other parks with `SND-WF-010`. The per-Step permission mode comes from the Step's policy, so the spec's `workflow.agent.permissionMode` is dropped |
| `judge` | object (`providers`) | no | none | Which providers a Judge Step (`step.judge.choose`) may ask (DT-J plan Q8). `judge.providers` is a list of `jev`, `claude-cli`, `codex-cli`, `cursor-cli`, `rules`; absent: `claude-cli`, `codex-cli`, `cursor-cli`, `rules`. `jev` runs only when listed, so it is opt-in. `rules` is always kept, last. A recipe's own `providers` list only narrows this one and never adds `jev`. A name that is not one of the five is refused before approval (`workflow.judge.providers[i]`). A repo may set its own list under `repos[].workflow.judge.providers`; it wins over the box's |
| `forge` | object | no | none | The forge port's settings: `adapter`, `hosts` and `credential` (see `workflow.forge` below) |
| `route` | object (`startable`) | no | none | The router stub's setting (engine P7 plan D8; see `workflow.route` below) |
| `packs` | object | no | none | The style and lens packs a repo uses and their pins (line spec §5; see `workflow.packs` below) |
| `line` | object | no | none | How many phases run at once, how many pull requests stay open and how deep a stack goes (line spec §8; see `workflow.line` below) |
| `plan` | object | no | none | The size classes and the largest phase size (line spec §6.3; see `workflow.plan` below) |
| `prove` | object (`holdoutMin`) | no | none | The hidden-scenario minimum (line spec §7.3; see `workflow.prove` below) |
| `build` | object (`commands`) | no | none | The commands that check a phase's change (line spec §7.2) |
| `ship` | object (`draft`) | no | none | Whether the line opens draft pull requests (line spec §7.5) |
| `design` | object (`models`) | no | none | The design writer and lens models (line spec §4; see `workflow.design` below) |
| `review` | object (`models`) | no | none | The review lens model (line spec §7.4) |
| `steps` | object (`longMax`) | no | none | The longest a block Step may declare (line spec LN-WF7) |
| `docs` | object (`globs`) | no | none | Where docs sync may write (line spec §7.6) |

### The line's keys (`workflow.packs`, `.line`, `.plan`, `.prove`, `.build`, `.ship`, `.design`, `.review`, `.steps`, `.docs`)

Line spec Appendix D and LN-WF9. Every key is optional and each is a re-approval: `profile approve` lists the paths that changed. A profile that sets none of them keeps its canonical JSON and its approval. A pack or model id takes the id pattern only (a pack: `^[a-z][a-z0-9-]{1,39}$`; an agent: `^[a-z][a-z0-9-]{1,31}$`), never a catalog.

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `workflow.packs.style` | object (`default`, `repos`) | no | `plain` | The style pack the design is written in (line spec §5.1): `default` is the pack id for every repo, `repos` maps a repo name to the pack it uses instead |
| `workflow.packs.lenses` (`lenses`) | object (`design`, `review`) | no | `design-five`, `code-review` | The lens packs (line spec §5.2): `design` and `review` each take `default` and `repos` as `style` does |
| `default` | pack id | no | the pack named above | A pack kind's pack for every repo without its own entry (`workflow.packs.style.default`, `.lenses.design.default`, `.lenses.review.default`) |
| `repos` | map of repo name to pack id | no | none | A repo's pack for that kind (`workflow.packs.style.repos.<repo>` …); a repo name is non-empty text |
| `workflow.packs.pins` (`pins`) | map of `style/<id>` or `lenses/<id>` to `sha256:` and 64 lower-case hex digits | no | `{}` | The hash a machine or repo pack must have (line spec §5.1); a pack with no pin, or another hash, is `SND-PACK-002` |
| `workflow.line.parallel` (`parallel`) | integer, 1 or more | no | `2` | How many phases run at once. **Cross-key rule:** a value above the box's heavy-slot cap (`$AW_STATE_DIR/heavy-slots-cap`, else `AW_HEAVY_SLOTS`) is refused by `profile validate` and `profile approve` (`workflow.line.parallel`); a value left out is never held to the cap, and `line doctor` repeats the check when the cap later drops |
| `workflow.line.maxOpenPrs` (`maxOpenPrs`) | integer, 1 or more | no | `4` | How many pull requests the line keeps open |
| `workflow.line.maxStack` (`maxStack`) | integer, 1 or more | no | `4` | How deep a stack of phases may go (`PLN-STACK-001`) |
| `workflow.plan.maxPhaseSize` (`maxPhaseSize`) | `XS`, `S` or `M` | no | `M` | The largest size a phase may be (`PLN-SIZE-001`); a phase larger than `M` is split (LN5) |
| `workflow.plan.sizes` (`sizes`) | map of `XS`, `S`, `M`, `L`, `XL` to `files`, `items` and `budget` | no | spec Q5 | The limits of each size class, written to the sizes file `aw-packets validate --sizes` reads (`PKT-SIZE-001`, LN-PKT3; `LineProfile.SizesFile`). A size or a field left out takes its default: XS 3 files and 2 items, S 8 and 4, M 20 and 8; L (40, 16) and XL (80, 32) are provisional, and so is every budget (XS 100k tokens, 30 turns, 20 minutes; S 400k, 60, 45; M 800k, 100, 90; L 1.6M, 150, 180; XL 3.2M, 200, 360), tuned after LN-P from the merge records |
| `files` | integer, 0 or more | no | per size | `workflow.plan.sizes.<size>.files`: the most files a phase's write scope covers |
| `items` | integer, 0 or more | no | per size | `workflow.plan.sizes.<size>.items`: the most acceptance items a phase holds, hidden scenarios included |
| `budget` | object (`tokens`, `turns`, `wallMinutes`) | no | per size | `workflow.plan.sizes.<size>.budget`: the most a phase may spend |
| `tokens` | integer, 0 or more | no | per size | `…budget.tokens` |
| `turns` | integer, 0 or more | no | per size | `…budget.turns` |
| `wallMinutes` | integer, 0 or more | no | per size | `…budget.wallMinutes` |
| `workflow.prove.holdoutMin` | integer, 1 or more | no | `2` | The fewest hidden scenarios a phase needs besides its canary (`PLN-HOLD-003`, `PRV-HOLD-002`) |
| `workflow.build.commands` | string[] | no | none | The commands that check a phase's change (`step.build.checks`); each one command line of 1 to 1000 characters with no line break, and a list that is there is not empty. `line doctor` asks when it is absent |
| `workflow.ship.draft` | boolean | no | `false` | Whether `effect.pr.open` opens the pull request as a draft |
| `workflow.design.models` | object (`writer`, `lens`) | no | profile roles | The agents that write the design (`writer`) and run its lenses (`lens`); `DSN-LENS-002` warns when they are one model |
| `workflow.review.models` | object (`lens`) | no | profile roles | The agent that runs the review lenses |
| `workflow.steps.longMax` | duration | no | `2h` | The longest `MaxDuration` a block Step may declare (LN-WF7) |
| `workflow.docs.globs` | string[] | no | `docs/**`, `*.md` | The paths docs sync may write (BD5): repo-relative globs with `/` separators and no `..`, as `index.denyPaths` takes them |

`workflow.effects` gains no key of its own: the line's effects (`effect.line.start-phase`, `effect.line.report`, `effect.pr.open`) are entries of the allowlist. Code reads the values with their defaults through `LineProfile` (`Aw.Sindri.Core.Profile`).

### `repos[].workflow`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `workflow` | object (`judge`) | no | none | A repo's override of the box `workflow:` settings. Only `workflow.judge.providers` is read: the repo's list wins over the box's for items in that repo (see `judge` above) |

### `workflow.forge`

The forge port's settings (PR babysitter spec W27, §10.2; `docs/csharp/forge.md`). The credential itself is never in the profile.

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `adapter` | `github` or `file` | no | none | Which forge adapter the services and effects use; `file` is the file-backed forge tests and proofs use |
| `hosts` | string[] (host names) | no | none | The forge hosts a link or request may name, such as `github.com`; a host outside the list is refused (`AW-FORGE-002`) before anything is sent |
| `credential` | `env:NAME` or `command:<argv>` | no | `env:AW_FORGE_TOKEN` | A variable that holds a long-lived token, or a program run per call that prints a token (or JSON with `token` and `expiresAt`); cached until five minutes before the expiry, re-run once after a 401 |

### `workflow.route`

What the router stub does with an item it routes (workflow engine spec §12). Real routing replaces it; `gate.route.startable` reads this key.

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `startable` | `all` or `none` | no | `all` | `all` lets every routed item start; `none` stops them at the router (the gate fails and the item parks). Any other value is refused before approval (`workflow.route.startable`) |

### `setup`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `mcp` | `auto` or `off` | no | `auto` | `auto` lets Sindri register its MCP server in each repo of the approved profile on its own (local scope, once consent is given); `off` stops that, and the "Connect your agents" button and `sindri mcp register --apply` still work. Any other value is refused before approval (`setup.mcp`) |

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
