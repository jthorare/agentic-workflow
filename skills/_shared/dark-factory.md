# Dark Factory (shared)

Optional local-inference delegation gate (CD12) built on real prism-mcp tools. Referenced by bugHunt, officeHours, archReview, design-implement-web/-ios.
Referenced via: SHARED_DIR pattern (CD2).

## Gate (CD12)

Enabled iff `~/.agentic-workflow/<repo-slug>/dark-factory.json` exists **and** contains `"enabled": true`:

```bash
source "$SHARED_DIR/repo-slug.sh"
grep -q '"enabled": *true' "$AW_DIR/dark-factory.json" 2>/dev/null && echo "dark-factory: enabled" || echo "dark-factory: disabled"
```

Config absent or not enabled ⇒ **skip silently — don't mention dark factory at all.** Read `max_iterations` from the config (default **2**).

## Flow

1. **Build the objective** from the consumer's template below — concrete, with the failing command / target description filled in.
2. `mcp: prism-mcp/session_task_route { task: <objective>, project: $REPO_SLUG }`.
3. If the route target is `claw`: `mcp: prism-mcp/prism_infer { prompt: <objective>, project: $REPO_SLUG, conversation_id: <if known>, cloud_fallback: false }`.
4. **Host verifies before accepting** — run `TEST_CMD` (or the consumer's own check) against the produced output. Retry up to `max_iterations` times with the failure fed back into the prompt.
5. Unavailable, refused, or failed verification ⇒ fall through to the consumer's manual path. Never accept unverified output.

## Consumer objective templates

**bugHunt (fix):**
> "Fix the bug: \<one-sentence description\>. Root cause: \<root cause from Step 3\>. Reproduce with: \<test command from Step 2\>. The fix must: (1) make the failing test pass, (2) introduce no new test failures, (3) be minimal — only the identified defect and the mechanism named below are changed, (4) include a regression test for the specific edge case. Prevention decision (bugHunt Step 3.6, `prevent-the-class.md`): \<failure class, invariant, other instances, chosen mechanism or \"existing guard\" or \"deferred: link\"\>; build that mechanism with its own proof, and label any stopgap as mitigation."

**officeHours (doc-quality):**
> "Adversarially evaluate these four spec docs for the '{feature}' feature. Check: (1) product.md contains no technical implementation details, (2) engineering.md contains no user personas or business metrics, (3) design-brief.md contains no technical constraints, (4) TASKS.md references domain docs rather than duplicating implementation details, (5) all selected EARS requirement types are present with at least 2 requirements each, (6) every product.md requirement traces to at least one acceptance criterion, (7) TASKS.md dependency graph is topologically sorted with no circular dependencies. For each failing criterion, provide file:line evidence."

**archReview (review-gap):**
> "Adversarially challenge this architecture review of: {target description from Step 1}. Find: (1) failure modes not identified in the edge case analysis, (2) risks whose impact or likelihood is understated, (3) suggested improvements that are vague or insufficient ('add monitoring' is not a mitigation — what specifically?), (4) component boundaries where error propagation is unaddressed. For each finding, provide component:function or file:line evidence."

**design-implement (token-compliance):**
> "Audit the generated web components for design token compliance. Check: (1) no hardcoded color values (hex, rgb, hsl, named CSS colors) in any generated component file, (2) no hardcoded spacing values (px, rem, em) that correspond to a token defined in tokens.css, (3) all interactive elements have a defined :focus and :hover state using token values, (4) no Tailwind arbitrary values (e.g. w-[347px]) that correspond to a design token. For each violation, provide file:line evidence."
