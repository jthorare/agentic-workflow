// sources/aws.py: CloudWatch Logs Insights error events whose structured field `tdd` equals the slug (JSON logs carry
// "tdd": "<slug>" and "level": "error"), counted per day over a window, in the log groups named by --aws-log-groups or
// TDD_EVAL_AWS_LOG_GROUPS; and CloudWatch alarms whose name starts with --aws-alarm-prefix or
// TDD_EVAL_AWS_ALARM_PREFIX, with their state. A foundation, as the Python is: not exercised against AWS.
//
// Unlike boto3's default chain, credentials come from the environment only (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY,
// AWS_SESSION_TOKEN, as Redacted config) and never from ~/.aws files; the region from AWS_REGION or
// AWS_DEFAULT_REGION. The AWS SDK for JavaScript (@aws-sdk/client-cloudwatch-logs, @aws-sdk/client-cloudwatch) is an
// optional dependency imported only when a query is configured. Nothing is written to AWS.
import { Effect, Option } from "effect";
import * as py from "../../py";
import { strRepr } from "../../py/repr";
import { type Py, importOptional } from "../common";
import { type Keys, reveal } from "../config";

const re = py.re;
const SLUG = re.compile("^[a-z0-9][a-z0-9-]{0,80}$");

export const insightsQuery = (slug: string): string =>
  `fields @timestamp | filter tdd = "${slug}" and (level = "error" or level = "ERROR" or severity = "error") ` +
  "| stats count(*) as errors by bin(1d) as day | sort day asc";

const optional = (spec: string): Effect.Effect<Py | null> => Effect.promise(() => importOptional(spec));
const LOGS = "@aws-sdk/client-cloudwatch-logs";
const CW = "@aws-sdk/client-cloudwatch";

export const query = (aws: Keys["aws"], slug: string, logGroups: string[] = [], alarmPrefix: string | null = null, days = 30, waitS = 60) =>
  Effect.gen(function* () {
    if (!SLUG.match(slug || "")) return { status: "error", reason: `slug ${strRepr(slug)} is not [a-z0-9-]` } as Record<string, Py>;
    const groups = logGroups.filter((g) => g);
    if (!groups.length && !alarmPrefix)
      return { status: "not run", reason: "no log groups (TDD_EVAL_AWS_LOG_GROUPS) or alarm prefix (TDD_EVAL_AWS_ALARM_PREFIX) configured" } as Record<string, Py>;
    const logsMod = groups.length ? yield* optional(LOGS) : null;
    const cwMod = alarmPrefix ? yield* optional(CW) : null;
    if ((groups.length && !logsMod) || (alarmPrefix && !cwMod))
      return { status: "not run", reason: `the AWS SDK is not installed (bun add --no-save ${LOGS} ${CW} in skills/tdd/cli)` } as Record<string, Py>;
    if (Option.isNone(aws.accessKeyId) || Option.isNone(aws.secretAccessKey))
      return { status: "not run", reason: "no AWS credentials (AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY in the environment)" } as Record<string, Py>;
    if (Option.isNone(aws.region)) return { status: "not run", reason: "no AWS region (AWS_REGION)" } as Record<string, Py>;
    const region = aws.region.value;
    const credentials = { accessKeyId: reveal(aws.accessKeyId)!, secretAccessKey: reveal(aws.secretAccessKey)!,
      ...(Option.isSome(aws.sessionToken) ? { sessionToken: reveal(aws.sessionToken)! } : {}) };
    const out: Record<string, Py> = { status: "ok", region, window_days: days };
    const errText = (e: Py) => `${e?.name ?? "Error"}: ${py.cpSlice(String(e?.message ?? e), 0, 160)}`;
    if (groups.length) {
      const client = new logsMod.CloudWatchLogsClient({ region, credentials });
      const end = Math.floor(Date.now() / 1000);
      const r = yield* Effect.promise(async () => {
        try {
          const started = await client.send(new logsMod.StartQueryCommand({ logGroupNames: groups.slice(0, 50), startTime: end - days * 86400, endTime: end,
            queryString: insightsQuery(slug), limit: 1000 }));
          const t0 = Date.now();
          let res: Py = {};
          while (Date.now() - t0 < waitS * 1000) {
            res = await client.send(new logsMod.GetQueryResultsCommand({ queryId: started.queryId }));
            if (["Complete", "Failed", "Cancelled", "Timeout", "Unknown"].includes(res.status)) break;
            await new Promise((f) => setTimeout(f, 1500));
          }
          const perDay = new Map<string, number>();
          for (const row of res.results ?? []) {
            const f = new Map<Py, Py>(row.map((c: Py) => [c.field, c.value]));
            if (f.get("day")) perDay.set(py.cpSlice(f.get("day"), 0, 10), Math.trunc(Number(f.get("errors") || 0)));
          }
          return { status: res.status ?? null, groups: groups.slice(0, 50), per_day: perDay, total: [...perDay.values()].reduce((a, b) => a + b, 0),
            query: insightsQuery(slug), scanned_bytes: res.statistics?.bytesScanned ?? null };
        } catch (e) {
          return { status: "error", reason: errText(e) };
        }
      });
      out.logs = r;
    }
    if (alarmPrefix) {
      const cw = new cwMod.CloudWatchClient({ region, credentials });
      out.alarms = yield* Effect.promise(async () => {
        const alarms: Record<string, Py>[] = [];
        let token: string | undefined;
        try {
          for (;;) {
            const r: Py = await cw.send(new cwMod.DescribeAlarmsCommand({ AlarmNamePrefix: alarmPrefix, ...(token ? { NextToken: token } : {}) }));
            for (const a of [...(r.MetricAlarms ?? []), ...(r.CompositeAlarms ?? [])])
              alarms.push({ name: a.AlarmName ?? null, state: a.StateValue ?? null, since: a.StateUpdatedTimestamp ? String(a.StateUpdatedTimestamp) : "" });
            token = r.NextToken;
            if (!token) break;
          }
          return { prefix: alarmPrefix, count: alarms.length, in_alarm: alarms.filter((a) => a.state === "ALARM").length, alarms: alarms.slice(0, 50) };
        } catch (e) {
          return { status: "error", reason: errText(e) };
        }
      });
    }
    return out;
  });
