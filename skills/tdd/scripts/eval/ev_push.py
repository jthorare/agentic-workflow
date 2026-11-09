"""The Braintrust model and the push (only under --push).

Model (references/eval-loop.md, Push model): one dataset `tdd-inputs` per project, one record per packet revision
(id <repo>/<slug>@rev<rev>, input = the captured input schema, expected = the labels); per run, one experiment per
output kind (artifact, packet, implementation, qa, bugs, tokens) named <slug>-rev<rev>-<kind>, each with ONE row whose
input is the stable key {packet, output} so Braintrust lines the row up across revisions, compared against the previous
revision's experiment of the same kind. The braintrust import happens only here.

SDK calls (braintrust 0.49.3, read in the installed source 2026-11-08; not exercised against the API: no key):
  braintrust.login(api_key=...)
  braintrust.init_dataset(project=, name=, description=, metadata=) -> Dataset   (creates the project if absent)
  Dataset.insert(id=, input=, expected=, metadata=, tags=)   (an existing id is overwritten: upsert)
  Dataset.summarize() -> DatasetSummary(dataset_url, ...)     (flushes)
  braintrust.init(project=, experiment=, description=, base_experiment=, metadata=, tags=, git_metadata_settings=,
                  set_current=False) -> Experiment            (a missing base_experiment only logs a warning)
  Experiment.log(input=, output=, expected=, scores={name: 0..1|None}, metrics={name: number}, metadata=, tags=)
  braintrust.Attachment(data=<path>, filename=, content_type=)  placed anywhere in the logged event; uploaded by the SDK
  Experiment.summarize() -> ExperimentSummary(experiment_url, comparison_experiment_name, comparison)  (flushes)"""
import glob, os, re
import ev_common as C

KINDS = ("artifact", "packet", "implementation", "qa", "bugs", "tokens")
MAX_SHOTS = 12


def bt_scores(out):
    return {s["name"]: s["value"] for s in out["scores"] if s["status"] == "ok" and s["value"] is not None} | \
           {s["name"]: None for s in out["scores"] if s["status"] in ("not run", "error")}


def bt_metrics(out):
    return {re.sub(r"[^A-Za-z0-9_]", "_", k): v for k, v in out.get("metrics", {}).items() if isinstance(v, (int, float)) and not isinstance(v, bool)}


def attachments_for(kind, ctx):
    files = []
    if kind == "artifact":
        page = ctx["page"]
        if os.path.isfile(page):
            files.append((page, "text/html"))
        shots = sorted(glob.glob(os.path.join(ctx["out"], "shots", "*-top.png"))) + sorted(glob.glob(os.path.join(ctx["packet"], "shots", "*.png")))
        files += [(p, "image/png") for p in shots[:MAX_SHOTS]]
    elif kind == "packet":
        readme = os.path.join(ctx["build"], "agents", "README.md")
        if os.path.isfile(readme):
            files.append((readme, "text/markdown"))
    return files


def plan(ctx, outputs, project, exp_base, base):
    inp = ctx["input"]
    pk, rev = inp["packet"], (ctx["meta"] or {}).get("rev")
    labels = inp["labels"]
    expected = {"decisions": [{k: d[k] for k in ("id", "question", "answer", "status", "state", "label")} for d in labels["decisions"]],
                "review_notes": labels["review_notes"]}
    tags = ["tdd", pk["slug"], f"rev{rev}", pk["repo_slug"]]
    dataset = {"project": project, "name": "tdd-inputs", "id": pk["key"], "input": inp, "expected": expected,
               "metadata": {"slug": pk["slug"], "repo_slug": pk["repo_slug"], "rev": rev, "digest": inp["digest"], "tool": "eval_packet/1"}, "tags": tags}
    if base is None and isinstance(rev, int) and rev > 1:
        base = f"{pk['slug']}-rev{rev - 1}"
    exps = []
    for kind in KINDS:
        out = outputs.get(kind)
        if not out:
            continue
        exps.append({"project": project, "name": f"{exp_base}-{kind}", "base_experiment": f"{base}-{kind}" if base else None,
                     "row": {"input": {"packet": f"{pk['repo_slug']}/{pk['slug']}", "output": kind},
                             "output": out, "expected": {"review_notes": [r for r in labels["review_notes"] if r["target"] == kind]} | (
                                 {"decisions": expected["decisions"]} if kind in ("artifact", "packet") else {}),
                             "scores": bt_scores(out), "metrics": bt_metrics(out),
                             "metadata": {"packet": pk["key"], "slug": pk["slug"], "rev": rev, "kind": kind, "dataset": "tdd-inputs",
                                          "dataset_record_id": pk["key"], "input_digest": inp["digest"], "tool": "eval_packet/1",
                                          "not_run": {s["name"]: s.get("reason") for s in out["scores"] if s["status"] != "ok"}},
                             "tags": tags + [kind]},
                     "attachments": [{"path": p, "content_type": t, "bytes": os.path.getsize(p)} for p, t in attachments_for(kind, ctx)]})
    return {"schema": "tdd-eval-run/1", "dataset": dataset, "experiments": exps}


def push(run):
    """Send the plan. Returns {dataset_url, experiments: [{name, url, comparison, summary}]}. Raises on a missing key."""
    key = os.environ.get("BRAINTRUST_API_KEY")
    if not key:
        raise SystemExit("--push needs BRAINTRUST_API_KEY in the environment (never in a file)")
    import braintrust
    from braintrust.git_fields import GitMetadataSettings
    braintrust.login(api_key=key)
    d = run["dataset"]
    ds = braintrust.init_dataset(project=d["project"], name=d["name"], description="House TDD eval inputs: one record per packet revision")
    ds.insert(id=d["id"], input=d["input"], expected=d["expected"], metadata=d["metadata"], tags=d["tags"])
    dsum = ds.summarize()
    result = {"dataset_url": getattr(dsum, "dataset_url", None), "experiments": []}
    for e in run["experiments"]:
        exp = braintrust.init(project=e["project"], experiment=e["name"], description=f"TDD eval: {e['row']['metadata']['packet']} ({e['row']['metadata']['kind']})",
                              base_experiment=e["base_experiment"], metadata=e["row"]["metadata"], tags=e["row"]["tags"],
                              git_metadata_settings=GitMetadataSettings(collect="none"), set_current=False)
        output = dict(e["row"]["output"])
        if e["attachments"]:
            output["files"] = [braintrust.Attachment(data=a["path"], filename=os.path.basename(a["path"]), content_type=a["content_type"]) for a in e["attachments"]]
        exp.log(input=e["row"]["input"], output=output, expected=e["row"]["expected"], scores=e["row"]["scores"],
                metrics=e["row"]["metrics"], metadata=e["row"]["metadata"], tags=e["row"]["tags"])
        s = exp.summarize()
        comp = getattr(s, "comparison", None)
        diffs = {k: {"score": v.score, "diff": v.diff, "improvements": v.improvements, "regressions": v.regressions}
                 for k, v in (getattr(comp, "scores", None) or {}).items()}
        result["experiments"].append({"name": s.experiment_name, "url": s.experiment_url, "compared_to": s.comparison_experiment_name, "scores": diffs})
    braintrust.flush()
    return result
