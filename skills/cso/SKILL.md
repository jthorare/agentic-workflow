---
name: cso
description: "Use when a plan or pull request needs a security check: auth gaps, exposed data, injection, abuse cases, \"is this safe to ship\". Walks the OWASP Top 10 and STRIDE, then writes rated findings with fixes. Plan mode before building, diff mode on a PR. Not for scaling or component-design questions."
argument-hint: "[--plan|--diff] [path-or-pr#] [--output <path>] [--slim]"
allowed-tools: Bash(gh *), Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# CSO — Security Threat Modeling

OWASP Top 10 + STRIDE threat model. Pre-impl (plan) or post-impl (diff) mode. Auto-detects mode from invocation context.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

## Overview

Runs a two-axis security review (OWASP Top 10 + STRIDE) against either a plan doc or a PR diff. Each finding gets a severity rating and a concrete mitigation. Plan-mode output lands at the canonical `plans/<feature>/security-review.md`; diff mode writes to `security/`. Fits the pipeline at two points: pre-ship gate, or fanned out from `/autoplan` for early plan-stage security check.

**Severity rubric** (2 lines, scale per `_shared/severity.md`): CRITICAL = exploitable as designed / data loss / cannot ship; HIGH = real vulnerability needing preconditions, or an unmet security requirement.
MEDIUM = hardening gap worth fixing, non-blocking; LOW = defense-in-depth nit.

## Inputs

- Mode flag: `--plan <path>` or `--diff <pr-or-branch>`
- Auto-detect: if no flag, try `gh pr view --json number,state` on current branch — if it succeeds, use `--diff <pr#>`; else `--plan` against the newest plan per `_shared/plan-discovery.md`:
  ```bash
  SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
  source "$SHARED_DIR/repo-slug.sh"
  PLAN_DIR=$(ls -1dt "$AW_DIR/plans/"*/ 2>/dev/null | head -1)
  ```
- `--output <path>` (optional) — explicit output file path overriding the mode default. `/autoplan` passes `--output <feature-dir>/security-review.md`.
- `--slim` (optional) — compact output: Coverage table, findings, and verdict only

## Steps

1. Resolve mode and target (Inputs block above).
2. Read source material:
   - Plan mode: read the sibling docs per the `_shared/plan-layout.md` reader matrix — `plan.md` + `engineering.md` + `product.md` (legacy aliases: `requirements.md` → product.md, `design.md` → engineering.md, only if the canonical file is absent). A TL;DR alone cannot surface A01/A03 — the engineering doc is where access control and injection surfaces live.
   - Diff mode: `gh pr diff <pr#>` OR `git diff main...HEAD` if branch-based. Also check the newest `plans/*/security-review.md` for a prior plan-stage threat model of this feature and read it if present.
3. **OWASP Top 10 pass** — evaluate ALL ten categories A01–A10 (enumerated in the Coverage table below); every category must appear in the output (findings, `No findings.`, or `N/A — <reason>`).
4. **STRIDE pass** — identify findings for each of: **S**poofing · **T**ampering · **R**epudiation · **I**nformation Disclosure · **D**enial of Service · **E**levation of Privilege.
5. For each finding: title, severity (per the rubric above and `_shared/severity.md`), description, attack scenario, mitigation.
6. Resolve target slug:
   - Plan mode: `<feature>` (parent dir of plan.md)
   - Diff mode: `pr-<pr#>` or `branch-<branch-name>`
7. Resolve output path:
   - If `--output <path>` was provided, use it verbatim. Ensure the parent dir exists with `mkdir -p "$(dirname <path>)"`.
   - Plan mode default: the **canonical** `$AW_DIR/plans/<feature>/security-review.md` — the same path `/autoplan` uses, so diff-mode runs and downstream skills can always find the plan-stage model. Optionally also copy to `$AW_DIR/security/<target-slug>-threat-model.md` if the user asks for it.
   - Diff mode default: `$AW_DIR/security/<target-slug>-threat-model.md`. Ensure the `security/` dir exists.
8. Write to the resolved output path:
   ```markdown
   # Threat Model — <target>

   **Mode:** plan | diff
   **Source:** <path or pr#>
   **Reviewed:** <ISO date>

   ## Summary
   <3–5 sentences. Worst findings, overall posture.>

   ## Coverage
   <All 10 rows mandatory — an unchecked category must never look clean.>
   | Category | Status |
   |---|---|
   | A01 Broken Access Control | findings: N / No findings. / N/A — <reason> |
   | A02 Cryptographic Failures | … |
   | A03 Injection | … |
   | A04 Insecure Design | … |
   | A05 Security Misconfiguration | … |
   | A06 Vulnerable & Outdated Components | … |
   | A07 Identification & Authentication Failures | … |
   | A08 Software & Data Integrity Failures | … |
   | A09 Security Logging & Monitoring Failures | … |
   | A10 SSRF | … |

   ## OWASP Top 10 findings
   <ALL 10 headings A01–A10 are mandatory. A heading with nothing to report
   contains the literal `No findings.` or `N/A — <reason>` — never omit it.>
   ### A01 — Broken Access Control
   - **<title>** [CRITICAL]
     - Where: <file:line or plan-line>
     - Attack: <scenario>
     - Mitigation: <concrete fix>
   ### A02 — Cryptographic Failures
   <findings, `No findings.`, or `N/A — <reason>`>
   <etc. through A10>

   ## STRIDE findings
   ### S — Spoofing
   <findings or "No findings.">
   <etc.>

   ## Verdict
   - **Critical findings:** N
   - **High findings:** N
   - **Recommendation:** SHIP | FIX-CRITICAL-FIRST | DO-NOT-SHIP
     (rule-derived: any CRITICAL ⇒ DO-NOT-SHIP; any HIGH ⇒ FIX-CRITICAL-FIRST; else SHIP)
   <End the file with the normalized final line per `_shared/severity.md` (CD9
   mapping: SHIP→PASS, FIX-CRITICAL-FIRST→NEEDS_WORK, DO-NOT-SHIP→BLOCKED):>
   verdict: <PASS|NEEDS_WORK|BLOCKED>
   ```

## Outputs

- Plan mode default: `$AW_DIR/plans/<feature>/security-review.md` (canonical — matches `/autoplan` placement; optional copy to `security/` on request)
- Diff mode default: `$AW_DIR/security/<target-slug>-threat-model.md`
- When `--output <path>` is supplied: that exact path

## Next steps

- `/review` — if pre-ship and findings exist, run general code review
- `/shipRelease` — if all findings are LOW or none
- `/rootCause` — if a CRITICAL finding maps to a known incident

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/cso-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`cso`), `artifacts` (absolute paths: the security review (`$AW_DIR/plans/<feature>/security-review.md` or `$AW_DIR/security/<target-slug>-threat-model.md`)), `summary` (one line),
plus `mode` (`plan | pr`); `verdict` (`PASS | NEEDS_WORK | BLOCKED`, copied from the report's last `verdict:` line).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
