"""OUTPUT 5, errors and bugs joined back to the TDD (a foundation): Linear issues and AWS error signals that name the
TDD by the tagging convention. Each source runs only with its key or credentials; otherwise "not run: <reason>"."""
import os
import ev_common as C

LIN = C.load("sources/linear.py", "tdd_eval_source_linear")
AWS = C.load("sources/aws.py", "tdd_eval_source_aws")


def evaluate(ctx):
    inp, content = ctx["input"], ctx["content"] or {}
    slug, key = inp["packet"]["slug"], inp["packet"]["key"]
    prs = [p["n"] for p in C.prs_of(content)]
    ds = [d.get("id") for d in content.get("decisions") or []]
    scores, data = [], {}
    lin_method = "Linear issues naming the TDD (label tdd:<slug>, or the slug in title/description); value = 1 / (1 + open issues), 1.0 = none open"
    lin = LIN.query(slug, prs, ds) if "linear" not in ctx["skip"] else {"status": "not run", "reason": "skipped (--skip)"}
    data["linear"] = lin
    if lin["status"] == "ok":
        m = {"issues": lin["count"], "open": lin["open"]}
        m.update({f"state_{k}": v for k, v in lin["by_state"].items()})
        scores.append(C.score("linear_open_bugs", 1 / (1 + lin["open"]), lin_method, "Linear GraphQL (scripts/eval/sources/linear.py)", m,
                              [{k: i[k] for k in ("identifier", "title", "state", "created", "url", "decisions", "prs")} for i in lin["issues"][:50]]))
    elif lin["status"] == "error":
        scores.append(C.error("linear_open_bugs", lin["reason"], lin_method, "Linear GraphQL"))
    else:
        scores.append(C.not_run("linear_open_bugs", lin["reason"], lin_method, "Linear GraphQL"))
    groups = [g.strip() for g in (ctx.get("aws_log_groups") or os.environ.get("TDD_EVAL_AWS_LOG_GROUPS") or "").split(",") if g.strip()]
    prefix = ctx.get("aws_alarm_prefix") or os.environ.get("TDD_EVAL_AWS_ALARM_PREFIX") or None
    aws = AWS.query(slug, groups, prefix) if "aws" not in ctx["skip"] else {"status": "not run", "reason": "skipped (--skip)"}
    data["aws"] = aws
    e_method = "CloudWatch Logs Insights error events with tdd=<slug> over the window; value = 1 / (1 + error events)"
    a_method = "CloudWatch alarms with the TDD's name prefix; value = 1 when none is in ALARM, else 0"
    if aws["status"] != "ok":
        f = C.error if aws["status"] == "error" else C.not_run
        scores += [f("aws_errors", aws["reason"], e_method, "CloudWatch Logs Insights"), f("aws_alarms_clear", aws["reason"], a_method, "CloudWatch DescribeAlarms")]
    else:
        lg = aws.get("logs")
        if not lg:
            scores.append(C.not_run("aws_errors", "no log groups configured", e_method, "CloudWatch Logs Insights"))
        elif lg.get("status") != "Complete":
            scores.append(C.error("aws_errors", f"query status {lg.get('status')}: {lg.get('reason', '')}", e_method, "CloudWatch Logs Insights"))
        else:
            scores.append(C.score("aws_errors", 1 / (1 + lg["total"]), e_method, "CloudWatch Logs Insights", {"errors": lg["total"], "days_with_errors": len(lg["per_day"])}, lg["per_day"]))
        al = aws.get("alarms")
        if not al:
            scores.append(C.not_run("aws_alarms_clear", "no alarm prefix configured", a_method, "CloudWatch DescribeAlarms"))
        elif al.get("status") == "error":
            scores.append(C.error("aws_alarms_clear", al["reason"], a_method, "CloudWatch DescribeAlarms"))
        else:
            scores.append(C.score("aws_alarms_clear", 0 if al["in_alarm"] else 1, a_method, "CloudWatch DescribeAlarms", {"alarms": al["count"], "in_alarm": al["in_alarm"]}, al["alarms"]))
    scores.append(ctx["owner_review"](inp, "bugs"))
    return C.output("bugs", key, scores, data)
