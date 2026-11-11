"""AWS source for output.bugs.json (a foundation: written against the botocore service model, not yet exercised).

CloudWatch Logs Insights: error-level events whose structured field `tdd` equals the slug (the log-line convention in
references/eval-loop.md: JSON logs carry "tdd": "<slug>" and "level": "error"), counted per day over a window, in the
log groups named by --aws-log-groups or TDD_EVAL_AWS_LOG_GROUPS. CloudWatch alarms: alarms whose name starts with
--aws-alarm-prefix or TDD_EVAL_AWS_ALARM_PREFIX (convention: tdd-<slug>-...), with their state.

Verified 2026-11-08 against botocore 1.43.126's service model: logs StartQuery(logGroupNames, startTime, endTime,
queryString, limit) -> queryId; GetQueryResults(queryId) -> status in Scheduled|Running|Complete|Failed|Cancelled|
Timeout|Unknown, results [[{field, value}]]; cloudwatch DescribeAlarms(AlarmNamePrefix, NextToken) -> MetricAlarms,
CompositeAlarms (AlarmName, StateValue, StateUpdatedTimestamp). Credentials: boto3's default chain; region from
AWS_REGION / AWS_DEFAULT_REGION or the profile. Nothing is written to AWS; no key is stored."""
import os, re, time

SLUG = re.compile(r"^[a-z0-9][a-z0-9-]{0,80}$")


def insights_query(slug):
    return (f'fields @timestamp | filter tdd = "{slug}" and (level = "error" or level = "ERROR" or severity = "error") '
            f'| stats count(*) as errors by bin(1d) as day | sort day asc')


def session(region=None):
    try:
        import boto3
    except ImportError:
        return None, "boto3 not installed (pip install boto3 into the tdd-skill venv)"
    s = boto3.Session(region_name=region or os.environ.get("AWS_REGION") or os.environ.get("AWS_DEFAULT_REGION"))
    if s.get_credentials() is None:
        return None, "no AWS credentials (boto3 default chain)"
    if not s.region_name:
        return None, "no AWS region (AWS_REGION)"
    return s, None


def query(slug, log_groups=(), alarm_prefix=None, days=30, region=None, wait_s=60):
    if not SLUG.match(slug or ""):
        return {"status": "error", "reason": f"slug {slug!r} is not [a-z0-9-]"}
    log_groups = [g for g in log_groups if g]
    if not log_groups and not alarm_prefix:
        return {"status": "not run", "reason": "no log groups (TDD_EVAL_AWS_LOG_GROUPS) or alarm prefix (TDD_EVAL_AWS_ALARM_PREFIX) configured"}
    s, why = session(region)
    if not s:
        return {"status": "not run", "reason": why}
    out = {"status": "ok", "region": s.region_name, "window_days": days}
    try:
        from botocore.exceptions import BotoCoreError, ClientError
    except ImportError:
        BotoCoreError = ClientError = Exception
    if log_groups:
        logs = s.client("logs")
        end = int(time.time())
        try:
            qid = logs.start_query(logGroupNames=log_groups[:50], startTime=end - days * 86400, endTime=end, queryString=insights_query(slug), limit=1000)["queryId"]
            t0, res = time.time(), {}
            while time.time() - t0 < wait_s:
                res = logs.get_query_results(queryId=qid)
                if res.get("status") in ("Complete", "Failed", "Cancelled", "Timeout", "Unknown"):
                    break
                time.sleep(1.5)
            per_day = {}
            for row in res.get("results") or []:
                f = {c.get("field"): c.get("value") for c in row}
                if f.get("day"):
                    per_day[f["day"][:10]] = int(float(f.get("errors") or 0))
            out["logs"] = {"status": res.get("status"), "groups": log_groups[:50], "per_day": per_day, "total": sum(per_day.values()),
                           "query": insights_query(slug), "scanned_bytes": (res.get("statistics") or {}).get("bytesScanned")}
        except (BotoCoreError, ClientError) as e:
            out["logs"] = {"status": "error", "reason": f"{type(e).__name__}: {str(e)[:160]}"}
    if alarm_prefix:
        cw = s.client("cloudwatch")
        alarms, token = [], None
        try:
            while True:
                kw = {"AlarmNamePrefix": alarm_prefix, **({"NextToken": token} if token else {})}
                r = cw.describe_alarms(**kw)
                for a in (r.get("MetricAlarms") or []) + (r.get("CompositeAlarms") or []):
                    alarms.append({"name": a.get("AlarmName"), "state": a.get("StateValue"), "since": str(a.get("StateUpdatedTimestamp") or "")})
                token = r.get("NextToken")
                if not token:
                    break
            out["alarms"] = {"prefix": alarm_prefix, "count": len(alarms), "in_alarm": sum(1 for a in alarms if a["state"] == "ALARM"), "alarms": alarms[:50]}
        except (BotoCoreError, ClientError) as e:
            out["alarms"] = {"status": "error", "reason": f"{type(e).__name__}: {str(e)[:160]}"}
    return out
