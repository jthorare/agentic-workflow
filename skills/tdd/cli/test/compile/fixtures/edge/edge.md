# Edge Case Ranking — a technical design document for parity

The lede paragraph cites `LEG/Thing.ts:12-40` and mentions D1, PR 2, and an unlinked Q9. {small}

A second lede paragraph with an API mention and a [link](#overview).

## Overview {chip: new; #overview; tone: green}

The lead paragraph of the overview: D1 and D-2 and F3.2 and PR 7a and OS-01, also `src/a.ts`, `LEG/b.py:3`, and `app/x.sql:5-9`.
It continues on a second line with **bold**, *em*, __strong__, _em2_, ~~del~~, `code *not em*`, \*escaped\*, &#91;bracket&#93; and <https://example.com/x>.

### First sub {#first-sub; nav: Custom label}

Text with [chip:new Shiny] and [chip:plain Plain] and [chip:Held held one] chips, an éclair, ١٢ digits, and an emoji 😀 here.

#### A deeper heading

A paragraph with an id. {#para-id; muted; class: extra extra two}

**Lead label**

A paragraph with <script>alert(1)</script> and <a href="javascript:alert(1)" onclick="x()" style="c">bad</a>, <a href='http://ok.example/?a=1&b=2'>ok</a>, <img src=x.png onerror=y> and <span class="note">safe</span>.

[diagram: flow · The flow {fit; #fig-flow-main}]
[legend: **New** = new | Existing = existing grey | Proposed = proposed | Odd = Purple Dashed purple]
[reading: Read the arrows left to right; see D1.]

[diagram: shapes · Shapes without size {alt: Alt text; fit: no}]

[diagram: tiny · A png diagram]

[diagram: missing · Not drawn yet]

[video: clip · A video]

[image: placeholder image]

[embedded content: an embed]

![One shot](mockups/phone.png "A phone shot")

![A](mockups/wide.png "Wide") ![B](mockups/photo.jpg) ![C](mockups/nothere.png "Gone")

| Item | Value | Status | Notes |
|:-----|------:|:------:|-------|
| D1 | 12 | Done | the first |
| R7 | ~3.5% | blocked, maybe | second |
| plain | 1 × 2 | | third |
| Q9 | 1,234 | merged (yesterday) | fourth \| piped |

```sql
-- a comment with NULL and SELECT
CREATE TABLE x (id UUID PRIMARY KEY, n INTEGER NOT NULL DEFAULT 0); -- trailing NOT NULL
SELECT * FROM x WHERE n IS NOT NULL AND n IN (1, 2);
```

```ts
const a = 1; // a comment
```

```python
x = 1  # comment
# whole line
```

```yaml
key: value # note
```

```text
plain & <text>
```

> [!WARNING]
> A warning quote with D1.

> A plain quote.

- [x] done item
- [ ] open item
  continued line
  - nested child
- [ ] third

1. [new] first
2. [changed] second

3. [held] restart

---

<!-- a comment -->

## Containers {#containers; lead: no}

Not a lead because lead: no.

::: cards {four; stat}
### Card one {#card-one}
Card one body with D1.
### Card two
Card two body.
:::

::: cards
A card without a heading.
:::

::: persona Field engineer {#fe-engineer; chip: Beta; tone: amber; shots: auto}
The persona's first paragraph.

![P1](mockups/phone.png)
![P2](mockups/tall.png)
![P3](mockups/phone.png "again")

::: today What they do now {label: Today}
They use a spreadsheet.

![T1](mockups/wide.png)

![T2](mockups/wide.png) ![T3](mockups/phone.png)
:::
:::

::: persona Second persona {shots: cols2; nav: no}
Second persona text.

![Q](mockups/wide.png)
:::

::: duo
First duo paragraph.

Second duo paragraph.
:::

::: fold Details · the one-liner {open}
Folded content.
:::

::: fold Explicit fold · with id {#fold-explicit; nav: yes}
Folded with an id.
:::

::: group Group title · group sub {#grp}
Group paragraph. {lead}
:::

::: decisions
::: decision D1 · Do we rank? {status: Ruled by Ana, 2026-01-02; wide}
We rank by distance.

Read as: the nearest first.
:::
::: decision D2 · Is it held? {status: blocked on legal}
Waiting.
:::
::: decision D3 · Proposed with date {status: Proposed 2026-02-02}
A proposal.
:::
::: decision D4 · Explicit state {state: done; #d4-custom}
:::
::: decision D5 · No status
Read as nothing.
:::
:::

::: options
### Option A {rec}
Option A text.

[diagram: flow · Inside options]
### Option B
- one
- two
:::

::: steps
Intro to the steps.

1. Step one
2. Step two

- loose list
:::

::: cols
:::: col
Column one.
::::
:::: col
Column two.
::::
A stray paragraph.
:::

::: glossary
- **API** — application programming interface
- **ranking**: ordering by score
- **SLA** service level agreement
- not a term
:::

Uses API twice: API, the ranking and ranking again, and SLA.

::: callout Warn
A warn callout.
:::

::: callout Something {tone: violet}
A violet callout.
:::

::: mystery Unknown kind
Unknown body.
:::

## PRs and schema {#prs-schema}

<!-- PR-CARDS -->

The schema lane: tbl-shifts and the PR 1 card.

## 2026 {#}

A section titled with a number; D-numbers D7. and PR1.
