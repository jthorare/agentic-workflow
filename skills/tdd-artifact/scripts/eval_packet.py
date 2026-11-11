#!/usr/bin/env python3
"""Eval loop for a house TDD packet (decisions D10 and D12): capture the input as a schema with labeled data, score six
outputs (the artifact page, the agent-handoff packet, implementation, QA, errors and bugs, token usage), write them
under <build>/eval/, and push them to Braintrust only when asked.

Usage:
  eval_packet.py <packet-dir> [--build <dir>] [--out <dir>] [--push] [--project NAME] [--experiment NAME] [--base NAME]
                 [--with-judge] [--with-agent-probe] [--labels <labels.jsonl>] [--session <id>[,<id>...]] [--run-tests]
                 [--no-gh] [--aws-log-groups <g1,g2>] [--aws-alarm-prefix <prefix>] [--probe <questions.json>]
                 [--no-shots] [--skip <scorer,...>] [--json]

  --build <dir>        the build folder (default <packet>/build): content.json, content.files.json, tdd.html, agents/
  --out <dir>          where the eval writes (default <build>/eval)
  --push               send the dataset record and the six experiments to Braintrust (needs BRAINTRUST_API_KEY).
                       Without it nothing leaves the machine: <out>/run.json holds exactly what a push would send.
  --project NAME       Braintrust project (default tdd-skill)
  --experiment NAME    experiment name stem (default <slug>-rev<rev>); experiments are <stem>-<kind>
  --base NAME          stem to compare against (default <slug>-rev<rev-1> when rev is a number above 1)
  --with-judge         LLM rubric over the document (Claude with ANTHROPIC_API_KEY, else autoevals with OPENAI_API_KEY
                       or BRAINTRUST_API_KEY, else "not run")
  --with-agent-probe   ask a model the probe questions given only the agents/ lane (needs ANTHROPIC_API_KEY)
  --labels <file>      owner review notes, one JSON per line {target, anchor, verdict, note, date} (default
                       <packet>/eval/labels.jsonl when present; template: templates/eval/labels.template.jsonl)
  --session <ids>      Claude Code session ids whose transcripts count toward this revision's token cost
  --run-tests          run the repo's declared test command (package.json scripts.test or pytest), capped at 120 s
  --no-gh              do not call gh (implementation and QA then use local git only)
  --aws-log-groups     CloudWatch log groups for error events tagged tdd=<slug> (or TDD_EVAL_AWS_LOG_GROUPS)
  --aws-alarm-prefix   CloudWatch alarm name prefix for this TDD (or TDD_EVAL_AWS_ALARM_PREFIX)
  --probe <file>       probe questions (default <packet>/eval/probe-questions.json, else templates/eval/probe-questions.json)
  --no-shots           pass --no-shots to artifact_check (contract checks only, no screenshots)
  --skip <names>       scorers or sources to leave out: artifact_check, check_hydrate, lint, implementation, linear, aws
  --json               print the run summary as JSON instead of the table

Writes <out>/input.json, output.{artifact,packet,implementation,qa,bugs,tokens}.json, probe.json, lint.json, shots/,
run.json, appends one line to history.jsonl (local run-to-run comparison), and push.json after --push. A scorer whose tool, key or data is absent is recorded as "not run: <reason>",
never as a zero. Exit 0 after a run (scores are measurements, not gates); 1 when --push fails; 2 on bad usage.
Runs with python3 (stdlib); braintrust, autoevals, anthropic and boto3 are imported only when a flag and a key need
them: use the venv at ~/.local/share/tdd-skill/venv (references/eval-loop.md has the setup command)."""
import json, os, sys
sys.dont_write_bytecode = True

HERE = os.path.dirname(os.path.abspath(__file__))
EVAL = os.path.join(HERE, "eval")
SKILL = os.path.dirname(HERE)


def _load(name):
    import importlib.util
    if name in sys.modules:
        return sys.modules[name]
    spec = importlib.util.spec_from_file_location(name, os.path.join(EVAL, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


VALUE_FLAGS = {"--build", "--out", "--project", "--experiment", "--base", "--labels", "--session", "--aws-log-groups", "--aws-alarm-prefix", "--probe", "--skip"}
BOOL_FLAGS = {"--push", "--with-judge", "--with-agent-probe", "--run-tests", "--no-gh", "--no-shots", "--json"}


def parse(argv):
    opts, pos, i = {}, [], 0
    while i < len(argv):
        x = argv[i]
        if x in ("-h", "--help"):
            print(__doc__)
            sys.exit(0)
        if x in VALUE_FLAGS:
            if i + 1 >= len(argv):
                print(f"{x} needs a value", file=sys.stderr)
                sys.exit(2)
            opts[x[2:].replace("-", "_")] = argv[i + 1]
            i += 2
        elif x in BOOL_FLAGS:
            opts[x[2:].replace("-", "_")] = True
            i += 1
        elif x.startswith("--"):
            print(f"unknown option {x}\n{__doc__}", file=sys.stderr)
            sys.exit(2)
        else:
            pos.append(x)
            i += 1
    if len(pos) != 1:
        print(__doc__, file=sys.stderr)
        sys.exit(2)
    opts["packet"] = os.path.abspath(pos[0])
    if not os.path.isdir(opts["packet"]):
        print(f"{pos[0]}: no such packet directory", file=sys.stderr)
        sys.exit(2)
    return opts


def short(s):
    if s["status"] != "ok":
        return s.get("reason") or ""
    m = s.get("metrics") or {}
    keys = [k for k in m if not k.startswith(("exit", "seconds"))][:4]
    return ", ".join(f"{k}={m[k]}" for k in keys)


def table(outputs):
    lines = [f"{'output':<15}{'scorer':<26}{'value':>7}  {'status':<8} detail", "-" * 110]
    for kind, out in outputs.items():
        for s in out["scores"]:
            v = "" if s["value"] is None else f"{s['value']:.3f}"
            lines.append(f"{kind:<15}{s['name']:<26}{v:>7}  {s['status']:<8} {short(s)[:70]}")
    return "\n".join(lines)


def main(argv):
    o = parse(argv)
    C = _load("ev_common")
    _load("ev_llm")          # ev_judge, ev_probe and ev_tokens import it by name
    packet = o["packet"]
    build = os.path.abspath(o.get("build") or os.path.join(packet, "build"))
    out = os.path.abspath(o.get("out") or os.path.join(build, "eval"))
    os.makedirs(out, exist_ok=True)
    labels = o.get("labels") or (os.path.join(packet, "eval", "labels.jsonl") if os.path.isfile(os.path.join(packet, "eval", "labels.jsonl")) else None)
    probe_path = o.get("probe") or next((p for p in (os.path.join(packet, "eval", "probe-questions.json"),) if os.path.isfile(p)),
                                        os.path.join(SKILL, "templates", "eval", "probe-questions.json"))
    cap = _load("ev_capture")
    inp, content, meta = cap.capture(packet, build, labels)
    C.write_json(os.path.join(out, "input.json"), inp)
    A, K, P, J, I, B, T, U = (_load(n) for n in ("ev_artifact", "ev_packet", "ev_probe", "ev_judge", "ev_impl", "ev_bugs", "ev_tokens", "ev_push"))
    skip = {s.strip() for s in (o.get("skip") or "").split(",") if s.strip()}
    ctx = {"packet": packet, "build": build, "out": out, "page": os.path.join(build, "tdd.html"), "content": content, "meta": meta, "input": inp,
           "skip": skip, "no_shots": bool(o.get("no_shots")), "with_agent_probe": bool(o.get("with_agent_probe")), "run_tests": bool(o.get("run_tests")),
           "no_gh": bool(o.get("no_gh")), "sessions": (o.get("session") or "").split(",") if o.get("session") else [],
           "aws_log_groups": o.get("aws_log_groups"), "aws_alarm_prefix": o.get("aws_alarm_prefix"), "owner_review": A.s_owner_review}
    ctx["judge"] = lambda c: J.judge(build, c, bool(o.get("with_judge")))
    outputs = {}
    outputs["artifact"] = A.evaluate(ctx)
    probe_scores, probe_rec = P.run(ctx, probe_path)
    C.write_json(os.path.join(out, "probe.json"), probe_rec)
    outputs["packet"] = K.evaluate(ctx, probe_scores)
    matched = I.skipped("skipped (--skip implementation)") if "implementation" in skip else I.match_prs(ctx)
    outputs["implementation"] = I.evaluate_impl(ctx, matched)
    outputs["qa"] = I.evaluate_qa(ctx, matched, outputs["artifact"])
    outputs["bugs"] = B.evaluate(ctx)
    outputs["tokens"] = T.evaluate(ctx, outputs["packet"])
    for kind, data in outputs.items():
        C.write_json(os.path.join(out, f"output.{kind}.json"), data)
    rev = meta.get("rev")
    stem = o.get("experiment") or f"{inp['packet']['slug']}-rev{rev}"
    run = U.plan(ctx, outputs, o.get("project") or "tdd-skill", stem, o.get("base"))
    run["pushed"] = False
    C.write_json(os.path.join(out, "run.json"), run)
    with open(os.path.join(out, "history.jsonl"), "a", encoding="utf-8") as fh:   # append-only: one line per run, for local diffs
        fh.write(json.dumps({"at": C.now(), "packet": inp["packet"]["key"], "digest": inp["digest"], "content_generated": (inp.get("content") or {}).get("generated"),
                             "scores": {k: {s["name"]: s["value"] for s in v["scores"] if s["value"] is not None} for k, v in outputs.items()},
                             "not_run": {k: sorted(s["name"] for s in v["scores"] if s["status"] != "ok") for k, v in outputs.items()}}, ensure_ascii=False) + "\n")
    pushed = None
    if o.get("push"):
        try:
            pushed = U.push(run)
            C.write_json(os.path.join(out, "push.json"), pushed)
        except SystemExit as e:
            print(str(e), file=sys.stderr)
            return 1
        except Exception as e:
            print(f"push failed: {type(e).__name__}: {e}", file=sys.stderr)
            return 1
    files = sorted(f for f in os.listdir(out) if f.endswith(".json"))
    if o.get("json"):
        print(json.dumps({"out": out, "files": files, "packet": inp["packet"]["key"],
                          "summary": {k: {s["name"]: {"value": s["value"], "status": s["status"], **({"reason": s["reason"]} if s.get("reason") else {})} for s in v["scores"]}
                                      for k, v in outputs.items()},
                          "experiments": [e["name"] for e in run["experiments"]], "pushed": pushed}, indent=1))
    else:
        print(f"eval {inp['packet']['key']} -> {out}")
        print(table(outputs))
        counts = {k: v["counts"] for k, v in outputs.items()}
        tot = {c: sum(v[c] for v in counts.values()) for c in ("ok", "metric_only", "not_run", "error")}
        print("-" * 110)
        print(f"scored {tot['ok']} · metrics {tot['metric_only']} · not run {tot['not_run']} · errors {tot['error']}  | files: {', '.join(files)}")
        print(outputs["tokens"].get("data", {}).get("cost_line", ""))
        if pushed:
            print(f"dataset: {pushed.get('dataset_url')}")
            for e in pushed["experiments"]:
                print(f"experiment {e['name']}: {e['url']} (compared to {e['compared_to'] or 'none'})")
        else:
            print(f"not pushed: run.json holds the dataset record and {len(run['experiments'])} experiments a --push would send")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
