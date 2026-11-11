<!-- House TDD template (tdd skill). Syntax: the docstring of the tdd skill's scripts/build_tdd.py; how to write each part: the tdd skill's SKILL.md. Ticket {{ticket}}; evidence read at {{sha}} ({{sha_date}}), recorded in meta.json. -->
<!-- Comment rules: build_tdd.py drops a comment only when it sits on ONE line; a comment split over lines renders as text. Never put a comment inside ::::: options, directly inside :::: cols, or before the first #### of a cards box. Delete each guidance comment once its part is written. -->
<!-- Keep the six sections in this order. Placeholders are in <angle brackets>; replace every one. The scaffold also wrote placeholder images marked "PLACEHOLDER · REPLACE ME" (diagrams/redesign.svg, flow.svg and schema.svg; mockups/*.svg) so the page builds and renders from the start: the page shows them until you replace each one, so never publish one. Until then the linter reports each placeholder image (FILES/placeholder) and every line of this file or status.md that still carries a prompt, the YYYY-MM-DD date or the marker (FILES/placeholder-text): those errors are expected in a fresh packet, and the page is ready to publish only when they are gone. Reference tokens (R1, Q1, D1, PR 3, ...) must link to an anchor on the page: the compiler links what it can find, and the linter fails the rest. -->

# {{title}}

<!-- 1 · The redesign. The lead is the finding, not the topic: what changes, for whom, what stays. Then the whole-system diagram (diagrams/redesign.svg is a placeholder until draw_diagrams.mjs draws diagrams/redesign.mmd over it), then four cards. The first paragraph of every section is its lead unless the heading says lead: no. -->
## The redesign {#redesign}

<The finding in two to four sentences: what changes, who it reaches first, and what stays exactly as it is today.>

[diagram: redesign · Redesign: <the finding as the figure title> {fit}]
[legend: new or changed = new | the app = app | the engine = engine | needs a data source = data | proposed = proposed]
[reading: <one sentence that tells the reader where to start and what the arrows carry>]

::: cards {four}
#### <What is new>
<The new tables, services or screens, in one short paragraph.>

#### What doesn't change
<What stays exactly as it is today.>

#### <The one deliberate change>
<A change to an existing system, and why it is worth it.>

#### Who it reaches first
<Who sees it first, with a dated count and where the count came from.>
:::

<!-- 2 · Front end. One persona box per person and moment ("Role · Moment"): text first ({small}), then the shots, then one today fold with the screen as it is now. Shots are mocks built from the project's real components (references/mock-conventions.md); the quoted title is the caption. The four mockups/*.svg files are placeholders: render each state to mockups/<moment>-<state>.png (today's screen: a screenshot saved as mockups/today-<moment>.png, never a mock), point the image line at it and delete the placeholder. shots: cols3 puts three shots in a row; without it the compiler sizes each shot (phone, mid, wide, col). -->
## Front end {#frontend; chip: First-pass mocks; lead: no}

::: persona <Role> · <Moment> {chip: changed; #fe-moment-a; shots: cols3}
<What this person sees and does here, in two or three sentences: which screen, which component, and what it reads.> {small}

**<State>:** <what the screen shows in that state, and why.> {small}

![<What the shot shows, with the real names and numbers on it>](mockups/moment-a-1.svg "<Caption: the state this shot shows>")

![<What the shot shows>](mockups/moment-a-2.svg "<Caption>")

![<What the shot shows>](mockups/moment-a-3.svg "<Caption>")

:::: today <What the screen does today, in a few words>
![<Today's screen, as it is in production>](mockups/today-moment-a.svg "Today: <what today's screen does>")
::::
:::

::: duo
:::: persona <Role> · <Moment> {chip: changed; #fe-moment-b}
<What changes for this person, in two sentences.> {small}
::::

:::: persona <Role> · <Moment> {chip: new; #fe-moment-c}
<What changes for this person, in two sentences.> {small}
::::
:::

Not mocked yet: <the states and screens with no mock, and which built component each one reuses>. {small; muted}

<!-- 3 · Rule coverage. One row per requirement. The first column is its reference token (R1, R2, ...): each row becomes an anchor, so R1 anywhere on the page links here. A column named Status turns words such as planned, open, held, built, merged into chips. Name the source of every requirement and the date you read it. -->
## Rule coverage: the requirements against the PRD {#coverage; lead: no}

Requirements from <the PRD, the ticket and the threads, each with the date read>. {small}

| # | Requirement | Status | How the design meets it |
|---|---|---|---|
| R1 | <The requirement, in the PRD's words> | Planned | <The section, card or table that meets it> |
| R2 | <The requirement> | Open | <What is still missing, and the open question that decides it (Q1)> |
| R3 | <The requirement> | Held | <Why it is out of this design, and who agreed> |

<!-- 4 · End-to-end flow. The phases in order, then the technical flow diagram (diagrams/flow.svg is a placeholder until draw_diagrams.mjs draws diagrams/flow.mmd over it), the schema diagram, and the step-by-step fold with code paths. Code paths are path:line in backticks at the sha in meta.json, shortened with meta.path_aliases (references/evidence-rules.md). -->
## End-to-end flow {#flow}

<The flow in two or three sentences: the phases in order, and what each one reads and writes.>

[diagram: flow · Technical flow: <the finding as the figure title> {fit; #flow-diagram}]
[legend: new or changed = new | the engine = engine | proposed = proposed]
[reading: <What the reader should notice first, such as which step never runs inside a transaction>]

<!-- The schema diagram: one box per table, new tables green (diagrams/schema.svg is a placeholder until draw_diagrams.mjs draws diagrams/schema.mmd over it). In diagrams/schema.hotspots.json give each table box "table": "<name>" so a click opens that table's card from schema/<name>.json (references/schema-lane.md). -->
[diagram: schema · Schema: <the new and changed tables and what they reference> {#schema}]
[reading: Arrows point at the referenced table. The SQL, the CHECKs and example rows are in [the schema reference](#schema-sql).]

::: fold Step by step · Each step with its code path {#steps}
:::: cols
::::: col
### <Phase one>

:::::: steps
1. <What happens today and stays unchanged, with its code path, for example `<ALIAS>/<path>.ts:<line>`.>
2. [new] <What the design adds here, and the existing code it hooks into.>
  - <A detail of that step>
  - <Another detail>
3. [new] <The next new step.>
::::::
:::::

::::: col
### <Phase two>

:::::: steps
1. [new] <A new step.>
2. <An unchanged step this design relies on.>
::::::
:::::
::::
:::

<!-- 5 · Decisions and open questions. Open questions first (Q-numbers), then what is decided. A D-number is only ever the owner's answer, in the owner's words, with the date; status.md carries the same line. Ask one question at a time; when it is answered, rename Q<n> to the next D-number everywhere in this file and move its card under Decided. D1 below is a placeholder dated the day this packet was made, so it renders as decided: replace it with the owner's first answer and the date of that answer (YYYY-MM-DD), or delete it until there is one (its <Owner> is a lint error until then). -->
## Decisions and open questions {#questions; lead: no}

Still open first, then what's decided.

::: decisions
:::: decision Q1 · <The question, as the owner would ask it>? {status: Open · waiting on <owner>; wide}
<The background and the gap, with one concrete example in real numbers.>

::::: options
#### A · <Option A> {rec}
<What A means, what it costs, and who it affects.>
#### B · <Option B>
<What B means, what it costs, and who it affects.>
:::::
::::
:::

### Decided {#decided}

::: decisions
:::: decision D1 · <The decision, in one line> {status: <Owner>, {{date}}}
<The owner's answer in their words, then what it means for the design.>
::::
:::

<!-- 6 · For agents and additional context. Everything a reviewer or an implementer needs that sections 1 to 5 only point to, in groups of folds ("Title · one-line summary {#id}"). The build also writes the agents/ lane from this page (references/agents-lane.md). -->
## For agents and additional context {#more; lead: no}

::: group Context for reviewers · Scope, sizing, rollout and risks.
:::: fold Scope and acceptance criteria · In, out, and acceptance in EARS form {#scope}
::::: cards
#### In
- <In scope>

#### Out
- <Out of scope, with the PRD non-goal it matches>

#### Acceptance (EARS)
- When <trigger>, the <system name> shall <system response>.
:::::
::::

:::: fold How big this is · Read-only counts, each with its query and date {#sizing}
<!-- A count is a measured fact: the number as the card title, then what it counts, the query file in queries/ and the date it ran. A number nobody measured reads "not measured"; never estimate here. -->
::::: cards {stat}
#### not measured
<What it counts> · query `queries/<name>.sql` · <YYYY-MM-DD>

#### not measured
<What it counts> · query `queries/<name>.sql` · <YYYY-MM-DD>
:::::
::::

:::: fold Rollout, dependencies and risks · Flag, order of steps, rollback {#rollout}
::::: callout warn
**Rollback:** <How each step is undone, and what stays written.>
:::::

- <Step one: dark behind the flag>
- <Step two>
::::
:::

::: group Engineering reference · The SQL, the code paths and the PR order. Paths are at {{sha}} ({{sha_date}}).
:::: fold Schema: SQL and example rows · DDL, CHECKs and example rows for each new or changed table {#schema-sql}
### `<new_table>`: <what one row is> [chip:new new]

<Why this shape: the alternatives, and the invariant the table keeps.> {small}

```sql
CREATE TABLE <new_table> (
  id           uuid        PRIMARY KEY,
  <new_column> <type>      NOT NULL,                  -- <what it holds>
  created_at   timestamptz NOT NULL DEFAULT now()
);
```

| id | <new_column> | created_at | reads as |
|---|---|---|---|
| `<example id>` | <value> | <YYYY-MM-DD hh:mm> | <What this row means> |
::::

:::: fold PR order · Each PR with its proof and rollback {#prs}
<!-- One prs/pr-NN.md per PR (front matter: milestone, title, change, rollback; shape: templates/pr-cards.md in the system-design scripts the tdd skill resolves, by default its vendor/system-design). The marker comment below becomes the milestone-grouped PR cards, and "PR 3" anywhere on the page then links to its card. -->
Sized by dependency, not by date. Each PR merges dark behind the flag, or adds no behavior. {small; muted}

<!-- PR-CARDS -->
::::

:::: fold Glossary · Every abbreviation on this page {#glossary}
<!-- One item per term, "**TERM** — definition". Every later use of TERM on the page links to its entry. -->
::::: glossary
- **ABBR** — <What it stands for, and what it means in this design>
:::::
::::
:::
