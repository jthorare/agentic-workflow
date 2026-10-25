# Commit Strategy

This document defines how commits are structured, messaged, and branched in the agentic-workflow repository.

---

## Conventional Commit Format

Every commit message follows the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>: <subject>
```

Or with an optional scope:

```
<type>(<scope>): <subject>
```

### Types

| Type | When to use | Example |
|------|-------------|---------|
| `feat` | New feature or capability | `feat: initial agentic-workflow toolkit` |
| `fix` | Bug fix or correctness improvement | `fix: address review findings -- atomicity, security, DX` |
| `docs` | Documentation-only changes | `docs: add planning documents` |
| `chore` | Build config, dependencies, tooling | `chore: upgrade vitest to v3` |
| `refactor` | Code restructuring without behavior change | `refactor: extract transcript parsing to a shared helper` |
| `test` | Adding or updating tests | `test: add edge case for an empty transcript` |
| `style` | Formatting, whitespace (no logic change) | `style: fix trailing commas in schemas` |

### Scope (optional)

Scope narrows the area of change. Use the package or module name:

```
feat(sindri): add the ledger command
fix(sindri): refuse a ledger newer than the binary
test(judge): cover the empty outbox path
```

---

## Subject Line Rules

| Rule | Correct | Incorrect |
|------|---------|-----------|
| Lowercase first word | `fix: address review findings` | `Fix: Address review findings` |
| No period at end | `feat: add the ledger command` | `feat: add the ledger command.` |
| Imperative mood | `fix: validate the profile before a ledger write` | `fix: validated the profile before a ledger write` |
| Concise (under 72 chars) | `fix: address review findings` | `fix: I fixed the review findings that were found during the code review` |
| Describe the "why" when possible | `fix: address review findings -- atomicity, security, DX` | `fix: change some code` |

The subject should complete the sentence: "This commit will ___."

---

## Body Format

For non-trivial changes, add a body separated by a blank line. Use the body to explain **what** changed and **why**:

```
fix: address review findings -- atomicity, security, DX

- Run each ledger migration in one IMMEDIATE transaction
- Back up ledger.db before migrating so a failed run can be restored
- Scrub secrets from judge outbox lines before they are written
- Add --json to every sindri read command
```

Body guidelines:
- Wrap lines at ~72 characters.
- Use bullet points (`-`) for multiple changes.
- Reference issue numbers if applicable: `Closes #42`.

---

## What Makes a Good Commit

### Good: atomic and focused

```
feat: add the ledger command

Adds `sindri ledger --after N` to page the ledger from the command
line. Includes tests.
```

```
fix: address review findings -- atomicity, security, DX

- Run each ledger migration in one IMMEDIATE transaction
- Back up ledger.db before migrating so a failed run can be restored
- Scrub secrets from judge outbox lines before they are written
```

### Bad: vague or mixed

```
update code
```

```
fix stuff and add some features
```

```
WIP
```

---

## Branch Naming

Branches use a type prefix followed by a `/` and a kebab-case descriptor:

| Pattern | Example |
|---------|---------|
| `feat/<description>` | `feat/sindri-ledger-command` |
| `fix/<description>` | `fix/ledger-newer-than-binary` |
| `docs/<description>` | `docs/planning-docs` |
| `chore/<description>` | `chore/upgrade-xunit` |
| `refactor/<description>` | `refactor/extract-validation` |
| `test/<description>` | `test/judge-outbox-edge-cases` |

Branch names should be short but descriptive. Use the same type prefixes as commit messages.

---

## Commit Granularity

Prefer **small, atomic commits** that each represent one logical change:

- One commit per feature, bug fix, or refactor step.
- If a fix requires changes across multiple layers (service, controller, schema, test), those go in a single commit -- they are one logical change.
- Avoid mixing unrelated changes in the same commit.
- "Fix review findings" commits are acceptable when addressing a batch of related feedback, but use bullet points in the body to enumerate changes.

---

## Examples From This Repository

```
6176148 feat: initial agentic-workflow toolkit
a0d0755 fix: address review findings -- atomicity, security, DX
```

The first commit is a large initial scaffold (acceptable for project bootstrapping). The second is a targeted fix commit addressing specific review feedback, with the subject summarizing the themes (atomicity, security, DX) and the body (if present) enumerating individual changes.
