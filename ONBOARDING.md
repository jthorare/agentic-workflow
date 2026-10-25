# Onboarding: Using Agentic Workflow

A how-to guide for engineers and PMs who want to use Agentic Workflow in their own repos. It takes you from a fresh clone to shipping your first PR with the skills. For the full reference (support matrix, statusline), see [`README.md`](README.md).

> **Prefer a visual walkthrough?** Open [`docs/onboarding.html`](docs/onboarding.html) in a browser (`open docs/onboarding.html`). It has an interactive pipeline, a setup checklist, and a skill finder you can search.

> **Where to type commands.** This guide has two kinds of commands:
>
> | Where | Looks like | How to run it |
> |-------|------------|---------------|
> | **Terminal** | `bash` blocks: `./setup.sh`, `git pull`, `sindri status` | Your shell (zsh, bash) |
> | **Agent session** | Blocks labeled *In your agent session*: `/bootstrap`, `/review 123` | Start `claude`, `codex`, or `cursor-agent` in the repo, then type the skill at the agent's prompt |
>
> Skills are written `/<name>`, the Claude Code and Cursor syntax. In Codex, use `$<name>` (for example, `$review`). If you type a skill into your terminal, the shell treats it as a file path and fails with `zsh: no such file or directory: /prismStatus`.

---

## 1. What you get

Once it's installed, every Claude Code, Codex, or Cursor session on your machine has these:

| Piece | What it does for you |
|-------|----------------------|
| **Skills** (`/officeHours`, `/review`, `/rootCause`, …) | Packaged workflows for planning, design, building, reviewing, debugging, and shipping. They work the same way in every provider. |
| **Safety hooks** | Block `rm -rf`, `git reset --hard`, force-push, pushes to `main`/`master`, and commands that contain secrets. They also add git context when a session starts. |
| **MCP servers** | `serena` (symbol navigation), `prism-mcp` (memory across sessions), `xcodebuildmcp` (iOS simulator, macOS only) |
| **Token savers** | `rtk` rewrites noisy shell commands into compact ones. |
| **Judge** | `judge` makes quick, low-cost decisions for the hooks. |

You don't install anything per repo. Skills and hooks are global. The only per-repo step is running `/bootstrap` once (see [§4](#4-bootstrap-a-repo-once-per-repo)).

---

## 2. Install (about 15 minutes)

### Prerequisites

- At least one agent CLI: `claude`, `codex`, or `cursor-agent`
- Node.js >= 22 (see `.nvmrc`; only for `dembrandt`, `prism-mcp`, `xcodebuildmcp` and the schema checkers)
- Docker Desktop, **installed and running** (Serena needs it)
- `gh`, installed and logged in (`gh auth login`)
- `jq` (`brew install jq`)

`setup.sh` installs `rtk` for you.

### Run setup

```bash
git clone https://github.com/jthorare/agentic-workflow.git ~/repos/agentic-workflow
cd ~/repos/agentic-workflow

./setup.sh --dry-run    # optional: print every change without writing anything
./setup.sh              # installs for every agent CLI it finds on PATH
```

To install for specific providers only, use `./setup.sh --providers claude` (or `codex`, `cursor`, or a comma-separated list). You can re-run setup safely at any time. Run it again after a `git pull` or when you add a provider.

### Provider-specific follow-ups

- **Codex:** start `codex`, run `/hooks` at its prompt, and trust every `aw:*` entry. Codex won't run hooks you haven't trusted. Repeat this after a reinstall that changes a hook.
- **Cursor:** if an MCP server is disabled on first use, run `cursor-agent mcp enable <name>` for it.
- **Claude Code:** nothing else to do. Setup also installs the statusline and shell integration. Open a new terminal so the shell integration loads.

---

## 3. Check it worked

Open a new agent session in any git repo (for example, `cd` into it and run `claude`) and check the following:

| Check | Expected |
|-------|----------|
| Session start output | An `=== Git Context ===` block with your branch and recent commits |
| Type `/` (or `$` in Codex) | Skills such as `officeHours`, `review`, and `rootCause` appear |
| Ask the agent to run `rm -rf /tmp/x` | `BLOCKED: rm -rf is destructive and irreversible.` |
| In the session, run `/prismStatus` | Reports whether the prism-mcp dashboard and MCP connection are reachable |
| In a terminal, run `ls ~/.agentic-workflow/` | `toolkit` (symlink to your clone) and `providers` |

### Common first-day problems

| Symptom | Fix |
|---------|-----|
| `serena` MCP fails to connect | Docker isn't running. Start Docker Desktop, then restart the session. |
| `⚠ prism-mcp dashboard unreachable at :7180` at session start | The Mind Palace dashboard isn't up. It only affects memory features. Run `/prismStatus` in your agent session for details. |
| `zsh: no such file or directory: /prismStatus` (or any `/<skill>`) | You typed a skill into your terminal. Skills run inside the agent: start `claude` (or `codex`, `cursor-agent`), then type `/prismStatus` at its prompt. |
| Skills missing in Codex or Cursor | Re-run `./setup.sh --providers <name>`, then check `~/.agentic-workflow/providers` |
| Hooks do nothing in Codex | You haven't trusted them yet. Run `/hooks` in `codex`. |
| A skill says the toolkit isn't installed | `~/.agentic-workflow/toolkit` is missing or broken. Re-run `./setup.sh` from your clone. |
| `judge` warning at session start | Run `judge health`. Judge needs at least one of `claude`, `codex`, or `cursor-agent` on PATH. |
| `github` MCP auth errors | Re-authenticate with `gh auth login`. Skills fall back to the `gh` CLI. |

---

## 4. Bootstrap a repo (once per repo)

Start an agent session in the repo you want to work in, then run:

*In your agent session:*

```
/bootstrap
```

It checks the repo's documentation and generates what's missing, adapted to your stack:

- **`AGENTS.md`**: the navigation doc every agent reads first. `CLAUDE.md` is a symlink to it.
- **`.agents/rules/*.md`**: rules scoped by glob (for example, "when editing `src/db/**`, follow these conventions"). They're linked into `.claude/rules` and `.cursor/rules` so every provider loads them.
- **`planning/*.md`**: architecture, ERD, code style, testing, and other docs from the 17-doc standard. It only writes the ones that are missing.
- **`.serena/project.yml`**: turns on Serena symbol navigation for the repo.

Options: `--force` regenerates docs that already exist. `--product-docs <url-or-path>` pulls in external product docs (Confluence, SharePoint, a shared drive) when it writes the product-facing docs.

Commit the output. From then on, every agent session in the repo starts with the same context.

---

## 5. Your first feature, end to end

This walkthrough takes a small feature from idea to merged PR. You don't have to run every step. Each one is useful on its own.

### Step 1: Clarify the idea *(optional)*

*In your agent session:*

```
/withInterview officeHours "add CSV export to the reports page"
```

The agent interviews you first, then runs the skill you named (`officeHours` here) with a better prompt. Use it when the idea is still fuzzy. If the idea is already clear, `/enhancePrompt <request>` does a lighter version: it reads the repo docs and rewrites your request with that context.

### Step 2: Write the spec

*In your agent session:*

```
/officeHours add CSV export to the reports page
```

This is a working session. The agent proposes requirements, and you push back and fill in constraints it doesn't know about. You end up with:

```
~/.agentic-workflow/<repo-slug>/plans/<feature>/
├── plan.md          # the canonical handoff for later skills
├── product.md       # problem, EARS requirements, acceptance criteria
├── engineering.md   # approach, architecture decisions, open questions
├── design-brief.md  # UX goals and interactions
└── TASKS.md         # atomic task breakdown
```

### Step 3: Pressure-test the plan *(optional but cheap)*

*In your agent session:*

```
/autoplan
```

This runs the product, architecture, design, devex, and security reviews in parallel and consolidates the findings, including places where the reviews disagree. To run one review on its own, use `/productReview --mode mvp`, `/archReview`, `/planDesignReview`, `/planDevexReview`, or `/cso --plan`.

### Step 4: Design *(UI work only)*

*In your agent session:*

```
/design-language https://example.com     # brand personality + tokens (design-tokens.json, .impeccable.md)
/design-mockup reports-page              # HTML or SwiftUI mockup, iterate until approved
/design-implement reports-page           # production code from the approved mockup
/design-verify reports-page              # screenshot diff against the mockup baseline
```

Each of these checks whether the repo is web or iOS and routes to the matching `-web` or `-ios` skill. `/design-shotgun` gives you 4–6 variant directions to choose from before you mock up. `/design-refine` polishes an existing screen.

### Step 5: Build it

*In your agent session:*

```
/specToProvenPR ~/.agentic-workflow/<repo-slug>/plans/<feature>/plan.md
```

This splits the spec into stages. For each stage it plans, implements, tests the result by hand in the running app, proves it with an evidence pack, and gathers UI evidence when the stage changes the web UI. Only then does it open a draft PR and loop on review until there are no findings. If you stop partway, run `/specToProvenPR` with no argument to pick up from `stages.md`.

If you'd rather build it yourself, just work normally and run `/verify-app auto` to check your change in the running app. It uses Playwright for web and the simulator for iOS, and writes an evidence pack.

### Step 6: Review

*In your agent session:*

```
/review 123           # parallel reviewers, one per domain → ~/.agentic-workflow/<repo-slug>/reviews/123.json
/postReview 123       # publish findings to GitHub as batched PR comments (one review per agent)
/addressReview 123    # parallel agents implement the fixes; re-run until clean
```

`/review` never posts to GitHub. It only writes to your local state file, so you can read the findings first. `/addressReview` also picks up new comments from human reviewers on the PR.

### Step 7: Ship

*In your agent session:*

```
/shipRelease          # sync, test, check coverage, push, open PR → /landAndDeploy → /canary → /syncDocs
```

Pass `--no-deploy` to stop once the PR is open. The deploy chain reads `.agentic-workflow/deploy.json` in your repo. Run `/landAndDeploy --setup` once to create it.

After a deploy, `/canary` watches error rate, latency, and logs. If things look unhealthy, it hands the incident to `/rootCause`.

---

## 6. Which skill do I reach for?

Everything in the right-hand column is a skill, so type it in your agent session.

| I want to… | Use |
|------------|-----|
| Turn a vague idea into a spec | `/withInterview officeHours …` → `/officeHours` |
| Sanity-check a plan before building | `/autoplan` (or one lens: `/productReview`, `/archReview`, `/cso --plan`) |
| Take an approved spec all the way to PRs | `/specToProvenPR <plan.md>` |
| Confirm my change works in the real app | `/verify-app auto` |
| Debug a specific error or failing behavior | `/rootCause "<error message>"` |
| Find and fix bugs in an area, with regression tests | `/bugHunt --tier standard <area>` |
| Get a bug report without code changes (triage) | `/bugReport <area>` |
| Review a PR | `/review <pr>` → `/postReview <pr>` → `/addressReview <pr>` |
| Security-check a PR diff | `/cso --diff <pr>` |
| Set up or evolve a design system | `/design-analyze`, `/design-language`, `/design-evolve` |
| Ship and deploy | `/shipRelease` |
| Refresh README, CHANGELOG, or ARCHITECTURE after shipping | `/syncDocs` |
| Run a weekly retro on what shipped | `/weeklyRetro --weeks 1` |
| Onboard a new repo | `/bootstrap` |

---

## 7. Where things live

| Path | Contents |
|------|----------|
| `~/.agentic-workflow/toolkit` | Symlink to your clone. Skills read shared fragments through it. |
| `~/.agentic-workflow/<repo-slug>/plans/` | Output from `/officeHours` and the plan reviews |
| `~/.agentic-workflow/<repo-slug>/reviews/<pr>.json` | `/review` state, the source of truth for `/postReview` and `/addressReview` |
| `~/.agentic-workflow/<repo-slug>/…` | Other skill artifacts, such as evidence packs and design baselines, grouped by domain |
| `<repo>/AGENTS.md`, `<repo>/.agents/rules/` | Per-repo agent context written by `/bootstrap` |

Skill output lives outside your repo, so it never clutters your git status. Treat plan files as short-lived. Once the plan is captured in GitHub issues (an epic plus one issue per `TASKS.md` entry), the issues are the source of truth.

---

## 8. Day-to-day extras

### Coordinate multiple agents

Agents hand off through Prism handoffs (`session_save_handoff` / `session_load_context`), judge's outbox and teammate names (agent teams), and Sindri's ledger; there is no bridge server.

### Trim skills per repo *(Claude Code only)*

```bash
./setup.sh --profile web-app --target ~/repos/my-web-app --dry-run
./setup.sh --profile web-app --target ~/repos/my-web-app
```

A profile turns off skills that don't apply to a repo (for example, the iOS design skills in a web app). Profiles: `web-app`, `ios`, `personal`. The changes go to that repo's `.claude/settings.local.json`.

### Sindri

`./setup.sh` installs the C# Sindri (control plane, workflow engine, context engine and a desktop UI) with the other binaries; there is no opt-in flag (`--with-sindri` is accepted and does nothing). It also stands the pinned Onyx up unless you pass `--no-onyx`; that step needs Docker and you at a terminal.

```bash
sindri doctor                     # expect every line ok
sindri                            # start the control plane and open the app
sindri status                     # where it stands
sindri profile init               # scaffold your profile
sindri profile validate           # check it; `sindri profile approve` at a terminal
```

See [`docs/sindri/README.md`](docs/sindri/README.md), or open `docs/sindri.html` for the visual walkthrough.

### Tune the judge

`/judge` lets you inspect, tune, and undo the decisions `judge` made for hooks in the current session: `config get/set`, `why`, `undo`, and `health`.

---

## 9. Staying up to date

```bash
cd ~/repos/agentic-workflow
git pull
./setup.sh
```

Skills are symlinks, so skill text updates as soon as you pull. Re-run `setup.sh` to pick up new hooks, MCP servers, and rebuilt binaries. In Codex, re-trust any hooks that changed with `/hooks`.

---

## 10. Getting help

- **Reference:** [`README.md`](README.md), [`planning/PROVIDERS.md`](planning/PROVIDERS.md) (per-provider paths and hook events), [`skills/_shared/capabilities.md`](skills/_shared/capabilities.md) (how skill steps map to each provider's tools)
- **A skill's full instructions:** `skills/<name>/SKILL.md`
- **Working on the toolkit itself:** [`planning/LOCAL_DEV.md`](planning/LOCAL_DEV.md) and [`AGENTS.md`](AGENTS.md)
