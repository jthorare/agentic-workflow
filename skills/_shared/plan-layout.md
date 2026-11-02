# Plan Directory Layout (shared)

Canonical `plans/<date>-<slug>/` manifest and reader matrix for officeHours, autoplan, the 5 plan lenses, withInterview, and specToProvenPR.
Referenced via: SHARED_DIR pattern (CD2).

## Manifest (officeHours names win)

`$AW_DIR/plans/<date>-<slug>/` contains:

| File | Purpose |
|---|---|
| `plan.md` | Top-level plan / consolidated spec |
| `product.md` | Product requirements (EARS) |
| `engineering.md` | Engineering design |
| `design-brief.md` | Design brief |
| `TASKS.md` | Task breakdown |
| `interview.md` | withInterview transcript (if run) |
| `stages.md` | specToProvenPR stage state |
| `product-review.md` · `arch-review.md` · `design-review.md` · `devex-review.md` · `security-review.md` | Lens review outputs |
| `consolidated-review.md` | autoplan rollup |

## Reader matrix

| Lens | Reads |
|---|---|
| productReview | plan.md + product.md + engineering.md |
| archReview | plan.md + engineering.md + TASKS.md |
| planDesignReview | plan.md + design-brief.md + product.md |
| planDevexReview | plan.md + engineering.md + TASKS.md |
| cso (plan mode) | plan.md + engineering.md + product.md |

## Legacy aliases

`requirements.md` → `product.md`; `design.md` → `engineering.md`. Fall back to the legacy name only if the canonical file is absent.
