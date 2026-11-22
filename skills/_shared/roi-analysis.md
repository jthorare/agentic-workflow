# ROI and Data-Analysis Plan (shared)

The one copy of the plan that opens every project map: system-design's TDD (`tdd.html`, and `doc.md` from it) and
officeHours' `plan.md`. It says how the project will show its return. Referenced via: SHARED_DIR pattern (CD2).

## The rule

The plan is the map's **first section**, before the problem and the approach. It is a plan only. Writing it runs nothing
against production data: no query, no export, no dashboard edit. It writes the queries down for later: read-only, and run
only by the person who owns that access.

## What the section holds

Title it `ROI and data-analysis plan`. In the TDD it is `<section id="roi">`, the page's first section. In `plan.md`
it is the first `##` heading. Fill these five parts in order:

1. **The bet.** One sentence: what the project changes, and the number that shows it. Give the baseline and the target,
   each tagged `owner decision (D<n>)` or `agent default`. Example: "Repeat no-shows at the desk drop from 40 a month to
   under 10 (target: agent default)."
2. **Metrics.** One table row per metric: metric, why it shows the return, source, how to read it, baseline, target.
   Three to five rows; one metric that moves when the project works beats five that move anyway.
3. **Sources.** Each metric names exactly one source of these three kinds:
   1. *Production-data analysis:* a read-only query against the access the project documents (`AGENTS.md` or its
      rules). Write the query text in the plan. No documented access: write "no data access" and list the metric
      under open questions.
   2. *A dashboard:* an existing dashboard and the panel on it, by name.
   3. *Research:* user interviews, support tickets or a published study, with a link or a file path.
4. **Dashboard shape.** One panel per metric. Give each panel a chart kind (line over time, bar per group, single
   number) and a time range. Add its split (per team, per plan, none) and a line at the target. Describe it; do not
   build it.
5. **Where the numbers live.** Measured values stay on this machine under `$AW_DIR/roi/<slug>/`: one `metrics.jsonl`
   line per reading (`{"metric": ..., "value": ..., "at": ..., "source": ...}`) and the query files beside it. Never
   commit them, and never send them to an outside service.

## Bad cases it stops

- A "success metric" with no source: nobody can read it after launch. It goes to open questions.
- A target nobody gave: "cut load time by 50%" untagged. Tag it `agent default` or ask.
- A plan step that runs a query on production while you write the plan. The plan lists the query; it never runs it.
