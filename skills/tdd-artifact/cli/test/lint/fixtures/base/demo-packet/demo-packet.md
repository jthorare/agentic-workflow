# Demo Queue

## The redesign {#redesign}

The queue gains a retry lane; every job keeps its order. The worker in `SRC/app.ts:3-5` stays as it is (D1).

[diagram: flow · Technical flow: jobs move through one queue {fit}]
[legend: new or changed = new | the engine = engine | proposed = proposed]
[reading: Start at the queue box.]

::: cards {four}
#### What is new
A retry lane in `demo_table`.

#### What doesn't change
The worker loop.
:::

## Front end {#frontend; lead: no}

::: persona Operator · Retry {chip: changed; #fe-retry}
The operator sees the retry count. {small}

![The retry count on the queue page](mockups/shot.png "Retry count")
:::

## Rule coverage {#coverage; lead: no}

| # | Requirement | Status | How the design meets it |
|---|---|---|---|
| R1 | Jobs keep their order | Planned | The retry lane (D2) |

## End-to-end flow {#flow}

The helper in `SRC/deep/util.ts:2` reads the job; notes live in `docs/notes.md`.

## Decisions and open questions {#questions; lead: no}

::: decisions
:::: decision D1 · Keep the worker loop? {status: Owner, 2026-01-02}
Yes, unchanged.
::::

:::: decision D2 · Add a retry lane? {status: Owner, 2026-01-03}
Yes, in its own table.
::::
:::

## For agents and additional context {#more; lead: no}

::: group Engineering reference · The PR order and the terms.
:::: fold PR order · Each PR with its proof {#prs}
<!-- PR-CARDS -->
::::

:::: fold Glossary · Terms {#glossary}
::::: glossary
- **LANE** — a queue partition with its own retry budget
:::::
::::
:::
