# Verification Plan Template (shared)

Literal template for `proof/<stage-slug>/verification-plan.md`, written by specToProvenPR Step 2 and consumed by verify-app/verify-web/verify-ios via `--journey <path>`.
Referenced via: SHARED_DIR pattern (CD2).

Copy this template verbatim, filling the `<...>` placeholders:

```markdown
---
aw-doc: 1
id: <epic>-<stage-slug>-verification
kind: verification
title: "Verification plan: <stage>"
status: draft
parent: <epic>-stages
repos: [<owner/name>]
---

# Verification Plan — <stage>
created: <ISO>          # GATE: must predate the stage's first implementation
                        # commit (git log --diff-filter=A --format=%cI -- <files> | tail -1)
app_entry: <command to run the app + URL or simulator target>

## Journeys (≥1, per _shared/verification-lenses.md rules)
| row | action | target | assertion | verifies |

## Lenses (≥3 named from the catalog)
| lens | what will be checked | pass condition |

## Independent cross-checks (≥1 per numeric/behavioral claim)
| claim | cross-check command (NOT the app's own code path) | expected |

## Out of scope
```

## Rules

- `created` must predate the stage's first implementation commit — verify with the git command in the template comment. A plan written after implementation is invalid; regenerate the stage.
- Front matter per `_shared/design-doc-front-matter.md` (leave `repos` out when it can't be read). Each journey row's first cell is `TP-<n>` (numbered from 1 in this plan); its `verifies` cell names what the row proves, as refs: the stage's phase (`<epic>-stages#Ph<n>`) and any spec items it covers (`doc:repo:<name>/<spec id>#REQ-3`).
- Journeys must satisfy the validity rules in `_shared/verification-lenses.md` (≥3 interactive steps; ≥1 post-mutation assertion; navigate-only plans rejected).
- ≥3 named lenses from the catalog; each lens row states a concrete pass condition.
- Every numeric or behavioral claim needs ≥1 independent cross-check whose command does not go through the app's own code path (e.g., `curl` the API directly, query the DB, grep server logs).
- `## Out of scope` lists what this plan deliberately does not verify — empty is allowed but the heading is required.
