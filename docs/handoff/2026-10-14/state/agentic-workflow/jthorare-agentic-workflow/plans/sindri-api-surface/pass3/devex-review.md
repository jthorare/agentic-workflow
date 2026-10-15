# Plan Devex Review — sindri-api-surface (pass 3, rev 3, final)

**Plan:** docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md
**Reviewed:** 2026-10-10
**Scope:** I checked each pass-2 finding against rev 3 (§13 "Pass 2 → rev 3"), the S1 worktree `.claude/worktrees/sindri-s1-app-shell` (a7d2ada, read-only), `scripts/scaffold/new-slice.mjs`, `scripts/check-env-example.sh`, `scripts/env-scan-ignore.txt` and `claude --help`. This report lists only findings that are still open or new in rev 3. I didn't re-raise owner decisions.

## Summary

Rev 3 resolves both pass-2 HIGH findings:
- **C7 and §12 no longer rely on `--files-only`.** P1 adds a real `--slice-only` mode to `new-slice.mjs`, with a case in `scaffold.test.sh`. The mode doesn't exist yet (the script only has `--files-only`), which is correct for a plan.
- **P1's signal can pass now.** It uses the live hash from `profile validate --json` and sends `Content-Type`, and the evidence is read with `sqlite3 -readonly`. §2.1 now says that binding failures (400 and 415) are logged but not audited.

Pass 2's MEDIUM findings are also fixed:
- The shared-file rules are now one rule: a unit adds its own anchor lines in its own branch, and the integrator resolves the conflicts.
- Evidence before P5 is read with `sqlite3`.
- The agent-variable list is in `env-scan-ignore.txt`, and `CURSOR_TRACE_ID` and `CODEX_SESSION_ID` are dropped.
- `--allowedTools` is added.

Two new MEDIUM findings remain:
1. **The P8 prover command loses its prompt.** `--allowedTools` takes a variadic list, so it swallows `"<journey>"`.
2. **Units can't reach green under `PublicAPI.Unshipped.txt`'s "integrator only, never by hand" rule.** `--slice-only` edits no shared file, so it can't declare the new public records either.

Time to first success: ~10–15 min.

## Findings by severity

### CRITICAL

None.

### HIGH

None.

### MEDIUM

- **Setup / P8 prover (new, from the pass-2 `--allowedTools` fix):** The command as written runs without its prompt.
  - `claude --help` lists the flag as `--allowedTools, --allowed-tools <tools...>`, which is variadic. So `--allowedTools 'mcp__sindri__*' 'Bash(curl:*)' "<journey>"` parses the journey as a third tool name.
  - The `-p` run then has no prompt and exits with an input error, or reads stdin.
  - P8's signal uses the same command shape ("`claude -p --mcp-config … --strict-mcp-config --allowedTools …`").
  - Evidence: §8 "Proof" code block (`claude -p --mcp-config <tmp>/sindri-mcp.json --strict-mcp-config \ --allowedTools 'mcp__sindri__*' 'Bash(curl:*)' "<journey>"`); Phases table P8 row.
  - Fix: Pass the tools as a single comma-separated argument, `--allowedTools 'mcp__sindri__*,Bash(curl:*)'`, and put the prompt immediately after `-p`. For example: `claude -p "<journey>" --mcp-config … --strict-mcp-config --allowedTools 'mcp__sindri__*,Bash(curl:*)'`. Using `--` before the prompt also works.

- **Ramp-up / parallel delivery (new, from the C7 and shared-file rewrite):** A unit can't build green under these two rules together:
  - §12 and C7: `--slice-only` "edits no shared file".
  - Shared files: "`PublicAPI.Unshipped.txt` is regenerated from `RS0016` by the integrator, never by hand".

  Why it fails:
  - `Aw.Sindri.Features` and `Core` are libraries with `PublicAPI.*.txt` files (S1).
  - RS0016 fires for every new public record, and with warnings as errors it stops the build.
  - So a unit's new `<Slice>Request`, `<Slice>Response` and ids break the build in its own worktree. That breaks `--slice-only`'s own red check (the generator's `expectOnlyRed` needs a build that compiles), and it breaks the unit's red-to-green.

  Today's generator handles this with `declarePublicApi` (the `sliceApi` step), and that step edits `PublicAPI.Unshipped.txt`, which is a shared file.
  - Evidence: §12 "Scaffolding a slice" step 1 ("It touches no shared file"); C7 ("edits no shared file"); "Shared files" ("regenerated from `RS0016` by the integrator, never by hand"). S1: `src/Sindri/Aw.Sindri.{Core,Features,Infrastructure}/PublicAPI.Unshipped.txt`. `new-slice.mjs` `sliceApi` → `declarePublicApi`. `.editorconfig:153` (RS0016 enforces public declarations in libraries).
  - Fix: Make `PublicAPI.Unshipped.txt` the one shared file that `--slice-only` does edit:
    - The unit declares its symbols with the generator's `declarePublicApi` in its own branch.
    - The integrator re-derives the file from RS0016 when conflicts come up during the merge.
    - Change "never by hand" to "never typed by hand; the generator or the RS0016 code fix writes it".
    - Also say that `--slice-only` keeps the build, red and format checks. Only the shared-file inserts are skipped.

### LOW

- **Setup / env (pass-2 MEDIUM, partly resolved):** `CODEX_SANDBOX` is only set when Codex runs a command inside its sandbox (for example `seatbelt` on macOS). With full access or a sandbox bypass it isn't set. `CURSOR_AGENT` is listed, but the plan doesn't say anyone checked it against a real Cursor agent run, which was the pass-2 condition. The impact is small: a miss sends `human` without a terminal from a non-TTY agent shell, and `protected` is still refused. `desk` and `decision` would be allowed, though. Fix: say in §6 that the list was checked in a real Codex (sandboxed and full-access) and Cursor agent session, or accept the gap in writing. Evidence: §6 "Who sends what".
- **Error observability:** Refusals of `StopProcess` aren't defined for the audit. It's `SelfAudited` ("`process.stopped` is its audit"), but a refused `stop` (P1 signal: "In an agent session, `$SINDRI stop` gets `SND-AUTH-002`") emits no `process.stopped`. The plan doesn't say whether `AuditingService` still writes `command.handled` on that refusal. Fix: one sentence in §2.1 saying a self-audited command's *refusals* are audited normally. Evidence: §2.1 "When the audit can't be written"; P1 signal.
- **Ramp-up:** The CLI in an agent session sends `session` with no `X-Sindri-Repo` (§6 sets the header only for `sindri mcp`). Under §3.1 it then sees only items with no repo. P3's signal works because the `item add` there has no repo. But an implementer who adds `--repo` to that step gets a mismatch and won't know why. Fix: say in §6 whether the CLI sends `X-Sindri-Repo` for session callers, and note the rule next to P3's signal. Evidence: §3.1 ("A `session` query without `X-Sindri-Repo` answers only items that have no repo"); §6; P3 signal.
- **Docs (carried from pass 2):** "Docs per phase" still doesn't name `.agents/rules/mcp-servers.md`. P8's `sindri mcp` is a new MCP server that stays unregistered until the switch, and a reader of that rule should learn that. It also doesn't say that `planning/API_CONTRACT.md` stays the TS contract until the cutover. Evidence: §12 "Docs per phase".
- **Backwards compatibility (carried from pass 2):** C2 still says S1's "handler bodies don't change; their registrations move to `AddCommand`". But S1's request records must now implement `ICommand<…>` or `IQuery<…>`, which changes their declarations and `PublicAPI.Unshipped.txt`. Say so in C2. Evidence: §10 C2.

## Pass-2 resolution check

| Pass-2 finding | Status |
|---|---|
| HIGH: C7 relies on `--files-only` | Resolved (`--slice-only` with a scaffold test). It created the PublicAPI MEDIUM above |
| HIGH: P1 signal can't pass | Resolved (live hash, `Content-Type`, `sqlite3 -readonly`, binding failures logged and not audited) |
| MEDIUM: shared-file rules contradict each other | Resolved (one rule: own lines in own branch, the integrator merges) |
| MEDIUM: no `GET /ledger` before P5 | Resolved (`sqlite3 -readonly`) |
| MEDIUM: agent variables and `env-scan-ignore` | Mostly resolved (LOW above) |
| MEDIUM: `claude -p` permissions | Resolved in intent. The syntax drops the prompt (MEDIUM above) |
| LOW: validator required | Resolved (`AcceptAllValidator<T>`) |
| LOW: authorize before validate | Resolved |
| LOW: `script` CRLF | Resolved |
| LOW: docs list | Not resolved (LOW above) |
| LOW: C2 record changes | Not resolved (LOW above) |

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | None | 0 |
| Setup / install (time-to-first-success: ~10–15 min) | MEDIUM | 2 |
| Error messages & observability | LOW | 1 |
| Docs completeness | LOW | 1 |
| Backwards compatibility | LOW | 1 |
| Ramp-up cost (tribal-knowledge items: 3) | MEDIUM | 2 |

Time-to-first-success assumptions:
- A fresh implementer worktree on P1.
- Cold locked restore plus build: ~5–8 min.
- `--slice-only` plus the anchor lines: ~2–3 min, plus a few minutes to find the RS0016 fix the first time.
- `sindri-prove.sh` with its temp home and port 0, then the seeding: ~2 min.

Tribal knowledge still unwritten:
1. `--allowedTools` is variadic and takes a following positional argument.
2. A unit has to declare its own public symbols in `PublicAPI.Unshipped.txt` to build at all.
3. The CLI inside an agent session sends no `X-Sindri-Repo`, so it sees only items with no repo.

## Verdict

Both pass-2 HIGH findings are resolved, and so are all pass-2 MEDIUM findings. What's left are two new MEDIUM findings with one-line fixes (the variadic `--allowedTools` swallowing the P8 prompt, and the PublicAPI rule contradicting `--slice-only`) and five LOW findings. None blocks the plan.
verdict: PASS
