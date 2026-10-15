---
name: review_shell_hooks_overflagging
description: Review agents over-flag shell script issues for agentic-workflow hooks and setup scripts — user considers none of the typical findings real problems in this context
type: feedback
---

For agentic-workflow's shell hook scripts and setup.sh, review agents consistently over-flag concerns that are not real problems in context. When reviewing PRs touching `config/hooks/*.sh` or `setup.sh`, do not flag:

- **URL encoding of localhost URLs** — REPO_SLUG injected into `http://localhost:3100/...` is not a real injection risk; the bridge is user-controlled
- **curl-pipe-sh for dev tool installers** — acceptable pattern for personal developer toolkit setup scripts
- **Unpinned pip/brew dependencies** — version pinning is not expected in this personal toolkit's setup.sh
- **echo vs printf for variable expansion** — minor shell idiom preference, not a real issue
- **curl-pipe-sh supply chain concerns** — not a concern for a developer toolkit that the user owns and controls
- **Hook ordering fragility** — the setup.sh jq-based ordering strategy is intentional and sufficient
- **set -euo pipefail omission comments** — not required; bridge-context.sh having it and rtk-rewrite.sh not is fine
- **Word boundary false positives on grep patterns** (e.g. git diff-tree, cargo test-unit) — commands Claude Code realistically executes don't trigger these edge cases
- **jq -R multiline JSON output** — Claude Code Bash inputs are single-line; this edge case doesn't occur in practice
- **Fatal prerequisites in setup.sh** — the user chose fatal for rtk/headroom intentionally; it's not a violation of a convention they care about enforcing

**Why:** This is a personal developer toolkit, not a production security-sensitive application. The user dismisses all such findings as non-issues (PR #31 review — all findings rejected).

**How to apply:** When reviewing PRs in this repo that touch shell scripts, hooks, or setup.sh, skip security/correctness concerns that only apply in adversarial or production contexts. Focus on functional bugs, actual logic errors, and meaningful behavioral issues.
