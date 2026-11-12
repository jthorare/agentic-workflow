# Per-repo config

Skills that need facts about the repo they run in read JSON files under `<repo>/.agentic-workflow/`.
Each file has a JSON Schema (draft 2020-12) in `schemas/repo-config/`. Every object sets `additionalProperties: false`, so a typo is an error, not a silent no-op.

## Location and git

- Folder: `<repo>/.agentic-workflow/` in the main checkout.
- The folder is gitignored by default. `canary.json` and `deploy.json` hold shell commands that a skill runs, so a tracked copy is refused (see the skills).
- Company values stay in the company's checkout. This repo holds only the fake `acme` example: `tests/fixtures/repo-config/acme/.agentic-workflow/`.
- Every file and every key is optional. A skill that finds a file or key missing keeps its current behaviour.

## Files

| File | Schema | Read by |
|------|--------|---------|
| `canary.json` | `schemas/repo-config/canary.schema.json` | `/canary`, `/landAndDeploy` (pre-deploy baseline) |
| `deploy.json` | `schemas/repo-config/deploy.schema.json` | `/landAndDeploy` |
| `evidence.json` | `schemas/repo-config/evidence.schema.json` | `/ui-evidence`, `/verify-desktop`, `/design-verify-desktop` through `ui-evidence` (`TargetsFile`) |
| `worktrees.json` | `schemas/repo-config/worktrees.schema.json` | `skills/_shared/worktree-path.sh`, `aw-meta` |
| `repo.json` | `schemas/repo-config/repo.schema.json` | see below |

`repo.json` holds keys that two or more skills read. A key only one skill reads goes in that skill's own file.

### canary.json

Keys: `duration`, `errorRateUrl`, `latencyUrl`, `logSource`, `logAnomalyPatterns`, `customProbes[]` (`name`, `command`, `critical`), `thresholds` (`errorRateDegradedMultiplier`, `errorRateUnhealthyMultiplier`, `latencyDegradedMultiplier`). Written by `/canary --setup`.

### deploy.json

Keys: `command`, `healthUrl`, `smokeTests[]`, `timeout`, `journey` (`baseUrl`, `args`). Written by `/landAndDeploy --setup`.

### evidence.json

Keys: `schema` (`evidence.targets/1` or `evidence.repo/1`), `cli` (`buildOutputs`, `allow`, `passEnv`), `databases` (name -> Postgres entry with `identity`), `desktop` (`manifest`, `homeEnv`, `fixture`, `app`, `driver`, `sizes`, `threshold`, `timeoutSeconds`, `env`).
The schema mirrors `Aw.Evidence.Core.Data.TargetsFile`. `TargetsReader` enforces rules JSON Schema can't state (a build output that stays inside the repo, a size that parses). `tests/Aw.Evidence.Tests` loads the acme file through it, so the schema and the reader can't drift apart unseen.

### worktrees.json

Key: `root`, the directory that holds worktrees (`~` is `$HOME`; relative is relative to the main checkout; never inside it).

### repo.json

| Key | Meaning | Read by |
|-----|---------|---------|
| `variants` | `{axis, values: [{name, baseline?}]}`: product variants a behaviour change is checked on; `baseline` marks the "before" one | `/bugFixOrchestrator`, `/verify-web`, `/ui-before-after` |
| `dataRepair` | `{mechanism, never[], validator?}`: how damaged rows are repaired | `/bugFixOrchestrator` |
| `environments` | ordered names (`local`, `preview`, `staging`, `prod`) | `/bugFixOrchestrator`, `/shipRelease`, `/landAndDeploy` |
| `tickets` | `{tracker, followUpTeam, followUpState}`: where follow-ups are filed | `/bugFixOrchestrator`, `/review` |
| `reviewBots` | bot logins whose PR comments are fixed or answered | `/addressReview`, `/shipRelease` |
| `prePush` | cheap commands before a push; `<changed>` stands for the changed files | `/shipRelease`, `/bugFixOrchestrator` |
| `skipReviewLoop` | PR kinds that skip the review loop (for example `revert`) | `/shipRelease` |
| `siblingSurfaces` | a route-map path or a glob list, to find screens next to a changed one | `/verify-web`, `/ui-before-after` |

The "Read by" column for `repo.json` is the planned set. The reader (`skills/_shared/repo-config.sh`) and the skill changes land in later PRs of issue #2909; until then no skill reads `repo.json`.

## The acme example

```json
{
  "variants": { "axis": "plan", "values": [{ "name": "free", "baseline": true }, { "name": "team" }] },
  "environments": ["local", "preview", "staging", "prod"],
  "prePush": ["npm run typecheck", "npx eslint <changed>"],
  "reviewBots": ["acme-review-bot[bot]"],
  "siblingSurfaces": "src/routes.json"
}
```

All five complete files are in `tests/fixtures/repo-config/acme/.agentic-workflow/`.

## Checks

- `node scripts/check-repo-config-schemas.mjs`: the schemas compile, the acme files pass, and each file under `tests/fixtures/repo-config/invalid/<file>/<case>.json` fails at the JSON pointer that `invalid/expected.json` lists for it (`""` is the root, used for an unknown key).
- `bash scripts/tests/check-repo-config-schemas.test.sh`: the same check, plus one planted fault per rule.
