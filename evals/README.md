# evals/

Skill evals (#1107; spec `docs/superpowers/specs/2026-10-25-skill-revamp-evals-design.md`), run by `scripts/skill-evals.sh`.
Header of that script has the flags, the staging rules and the exit codes.

```
evals/
  <skill>/{routing,behaviour}/<case>/{prompt.md|case.yaml, graders/*.md, graders/_selftest/}   one skill's cases
  <skill>/fixture.sh                                                                           optional, runs in a scrubbed temp dir
  _families/<family>/<member>/{prompt.md, graders/*.md, graders/_selftest/}                    a family set; <member> is a skill's dir name
  _families/<family>/README.md                                                                 optional, what the family separates
  plugin/ _lib/ mocks/ stubs/                                                                  the eval plugin shell and shared helpers
```

- `bootstrap` lives in `bootstrap/`, not `skills/`; `scripts/skill-evals.sh` treats it as a skill (`evals/bootstrap/...`).
- A family set checks that near-neighbour skills do not steal each other's prompts. Each member's grader is `type: regex`,
  `target: trace`, anchored on the first Skill call. `--selftest` walks every family case; a real run adds a family's whole set
  (ablation none, own cost cap `--family-max-cost-usd`, default 12) when a selected member's description changed vs the merge base,
  or when `--family <name>` is given.
- Selftest replies for a grader sit in `graders/_selftest/<grader>.pass.<ext>` and `.fail.<ext>`: `txt` (reply), `files`, `tools`,
  or `trace` (for `target: trace`: the run's stream-json events, one JSON object per line; a Skill call is an assistant event holding
  `{"type":"tool_use","name":"Skill","input":{"skill":"<name>"}}`; read from the claude 2.1.312 bundle, not yet seen in a real run).
