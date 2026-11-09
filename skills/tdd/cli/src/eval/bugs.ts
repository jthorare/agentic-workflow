// ev_bugs.py: OUTPUT 5, errors and bugs joined back to the TDD (a foundation): Linear issues and AWS error signals
// that name the TDD by the tagging convention. Each source runs only with its key or credentials; otherwise
// "not run: <reason>". The two sources are loaded lazily, and their queries run side by side.
import { Effect } from "effect";
import * as py from "../py";
import { type Py, get, list, prsOf } from "./common";
import { type Keys } from "./config";
import { type Input } from "./capture";
import { type Output, type ScoreRec, error, notRun, output, score } from "./score";

export const LIN_METHOD = "Linear issues naming the TDD (label tdd:<slug>, or the slug in title/description); value = 1 / (1 + open issues), 1.0 = none open";
const E_METHOD = "CloudWatch Logs Insights error events with tdd=<slug> over the window; value = 1 / (1 + error events)";
const A_METHOD = "CloudWatch alarms with the TDD's name prefix; value = 1 when none is in ALARM, else 0";

export interface BugsArgs { skip: ReadonlySet<string>; awsLogGroups: string | null; awsAlarmPrefix: string | null }

/** The two source queries (Linear, AWS), each an Effect that never fails: [linear, aws] records. */
export const sources = (inp: Input, content0: Py, keys: Keys, a: BugsArgs) => {
  const content = py.truthy(content0) ? content0 : new Map();
  const slug = inp.packet.slug;
  const prs = prsOf(content).map((p) => p.n);
  const ds = list(get(content, "decisions")).map((d) => get(d, "id"));
  const linear = a.skip.has("linear") ? Effect.succeed({ status: "not run", reason: "skipped (--skip)" } as Record<string, Py>)
    : Effect.flatMap(Effect.promise(() => import("./sources/linear")), (L) => L.query(keys.linear, slug, prs, ds));
  const groups = (a.awsLogGroups || process.env.TDD_EVAL_AWS_LOG_GROUPS || "").split(",").map((g) => py.str.strip(g)).filter((g) => g);
  const prefix = a.awsAlarmPrefix || process.env.TDD_EVAL_AWS_ALARM_PREFIX || null;
  const aws = a.skip.has("aws") ? Effect.succeed({ status: "not run", reason: "skipped (--skip)" } as Record<string, Py>)
    : Effect.flatMap(Effect.promise(() => import("./sources/aws")), (A) => A.query(keys.aws, slug, groups, prefix));
  return { linear, aws };
};

export function evaluate(inp: Input, lin: Record<string, Py>, aws: Record<string, Py>, ownerReview: (inp: Input, t: string) => ScoreRec): Output {
  const scores: ScoreRec[] = [];
  const data: Record<string, Py> = { linear: lin };
  if (lin.status === "ok") {
    const m = new Map<string, Py>([["issues", lin.count], ["open", lin.open]]);
    for (const [k, v] of lin.by_state as Map<Py, number>) m.set(`state_${String(k)}`, v);
    scores.push(score("linear_open_bugs", 1 / (1 + lin.open), LIN_METHOD, "Linear GraphQL (scripts/eval/sources/linear.py)", m,
      (lin.issues as Record<string, Py>[]).slice(0, 50).map((i) => ({ identifier: i.identifier, title: i.title, state: i.state, created: i.created, url: i.url,
        decisions: i.decisions, prs: i.prs }))));
  } else if (lin.status === "error") scores.push(error("linear_open_bugs", lin.reason, LIN_METHOD, "Linear GraphQL"));
  else scores.push(notRun("linear_open_bugs", lin.reason, LIN_METHOD, "Linear GraphQL"));
  data.aws = aws;
  if (aws.status !== "ok") {
    const f = aws.status === "error" ? error : notRun;
    scores.push(f("aws_errors", aws.reason, E_METHOD, "CloudWatch Logs Insights"), f("aws_alarms_clear", aws.reason, A_METHOD, "CloudWatch DescribeAlarms"));
  } else {
    const lg = aws.logs;
    if (!py.truthy(lg)) scores.push(notRun("aws_errors", "no log groups configured", E_METHOD, "CloudWatch Logs Insights"));
    else if (lg.status !== "Complete")
      scores.push(error("aws_errors", `query status ${String(lg.status ?? "None")}: ${lg.reason ?? ""}`, E_METHOD, "CloudWatch Logs Insights"));
    else scores.push(score("aws_errors", 1 / (1 + lg.total), E_METHOD, "CloudWatch Logs Insights", { errors: lg.total, days_with_errors: lg.per_day.size }, lg.per_day));
    const al = aws.alarms;
    if (!py.truthy(al)) scores.push(notRun("aws_alarms_clear", "no alarm prefix configured", A_METHOD, "CloudWatch DescribeAlarms"));
    else if (al.status === "error") scores.push(error("aws_alarms_clear", al.reason, A_METHOD, "CloudWatch DescribeAlarms"));
    else scores.push(score("aws_alarms_clear", al.in_alarm ? 0 : 1, A_METHOD, "CloudWatch DescribeAlarms", { alarms: al.count, in_alarm: al.in_alarm }, al.alarms));
  }
  scores.push(ownerReview(inp, "bugs"));
  return output("bugs", inp.packet.key, scores, data);
}
