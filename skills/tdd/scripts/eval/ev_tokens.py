"""OUTPUT 6, token usage and efficiency: the Claude Code session(s) that produced this revision (--session), the eval's
own model calls (judge, probe), build sizes, and a cost-per-revision line. Costs are API list-price equivalents from
ev_llm.PRICES (a subscription bill differs); every number names how it was computed."""
import glob, json, os
import ev_common as C
import ev_llm as L

PROJECTS = os.path.expanduser("~/.claude/projects")


def session_files(sid):
    main = glob.glob(os.path.join(PROJECTS, "*", f"{sid}.jsonl"))
    subs = glob.glob(os.path.join(PROJECTS, "*", sid, "subagents", "*.jsonl"))
    return main, subs


def sum_usage(paths):
    """Sum message.usage over assistant records, one per message.id (a streamed message is written several times; the
    last record wins). Returns per-model totals and API-equivalent cost."""
    last = {}
    for p in paths:
        try:
            fh = open(p, encoding="utf-8", errors="replace")
        except OSError:
            continue
        with fh:
            for line in fh:
                if '"usage"' not in line:
                    continue
                try:
                    o = json.loads(line)
                except ValueError:
                    continue
                m = o.get("message")
                if not isinstance(m, dict) or not isinstance(m.get("usage"), dict):
                    continue
                mid = m.get("id") or o.get("uuid")
                last[mid] = (m.get("model") or "unknown", m["usage"])
    per = {}
    for model, u in last.values():
        t = per.setdefault(model, {"messages": 0, "input_tokens": 0, "output_tokens": 0, "cache_read_input_tokens": 0, "cache_creation_input_tokens": 0,
                                   "cache_5m": 0, "cache_1h": 0, "usd": 0.0, "priced": True})
        t["messages"] += 1
        for k in ("input_tokens", "output_tokens", "cache_read_input_tokens", "cache_creation_input_tokens"):
            t[k] += int(u.get(k) or 0)
        cc = u.get("cache_creation") or {}
        t["cache_5m"] += int(cc.get("ephemeral_5m_input_tokens") or 0)
        t["cache_1h"] += int(cc.get("ephemeral_1h_input_tokens") or 0)
        c = L.cost(model, u)
        if c is None:
            t["priced"] = False
        else:
            t["usd"] = round(t["usd"] + c, 6)
    return per


def evaluate(ctx, packet_out):
    inp = ctx["input"]
    key, build = inp["packet"]["key"], ctx["build"]
    scores, data = [], {"price_source": L.PRICE_SOURCE}
    s_method = ("sum of message.usage over assistant records in ~/.claude/projects/*/<session>.jsonl and <session>/subagents/*.jsonl, "
                "deduplicated by message.id (last record wins); cost = ev_llm.cost per model (API list-price equivalent)")
    sids = [s.strip() for s in (ctx.get("sessions") or []) if s.strip()]
    session_usd = None
    if not sids:
        scores.append(C.not_run("session_tokens", "pass --session <id> (the Claude Code session ids that built this revision)", s_method, "~/.claude/projects"))
    else:
        mains, subs, missing = [], [], []
        for sid in sids:
            m, s = session_files(sid)
            if not m and not s:
                missing.append(sid)
            mains += m
            subs += s
        if not mains and not subs:
            scores.append(C.not_run("session_tokens", f"no transcript for {', '.join(missing)} under ~/.claude/projects", s_method, "~/.claude/projects"))
        else:
            per_main, per_sub = sum_usage(mains), sum_usage(subs)
            def tot(per, k):
                return sum(v[k] for v in per.values())
            session_usd = round(tot(per_main, "usd") + tot(per_sub, "usd"), 4)
            m = {"input_tokens": tot(per_main, "input_tokens") + tot(per_sub, "input_tokens"),
                 "output_tokens": tot(per_main, "output_tokens") + tot(per_sub, "output_tokens"),
                 "cache_read_tokens": tot(per_main, "cache_read_input_tokens") + tot(per_sub, "cache_read_input_tokens"),
                 "cache_write_tokens": tot(per_main, "cache_creation_input_tokens") + tot(per_sub, "cache_creation_input_tokens"),
                 "messages": tot(per_main, "messages") + tot(per_sub, "messages"), "usd_api_equivalent": session_usd,
                 "subagent_files": len(subs)}
            unpriced = sorted({k for per in (per_main, per_sub) for k, v in per.items() if not v["priced"]})
            scores.append(C.metric("session_tokens", m, s_method, f"{len(mains)} session file(s), {len(subs)} subagent file(s)",
                                   {"main_by_model": per_main, "subagents_by_model": per_sub, "unpriced_models": unpriced, "missing_sessions": missing}))
    e_method = "usage returned by each judge/probe API call this run (ev_llm.LEDGER); cost by ev_llm.cost"
    calls = list(L.LEDGER)
    eval_usd = round(sum(c.get("usd") or 0 for c in calls), 6)
    if not calls:
        scores.append(C.not_run("eval_calls", "no model call this run (no --with-judge / --with-agent-probe, or no key)", e_method, "ev_llm.LEDGER"))
    else:
        scores.append(C.metric("eval_calls", {"calls": len(calls), "input_tokens": sum((c.get("usage") or {}).get("input_tokens", 0) for c in calls),
                                              "output_tokens": sum((c.get("usage") or {}).get("output_tokens", 0) for c in calls), "usd": eval_usd,
                                              "unpriced_calls": sum(1 for c in calls if c.get("usd") is None)}, e_method, "ev_llm.LEDGER", calls))
    sizes = {}
    for name, rel in (("content_json", "content.json"), ("tdd_html", "tdd.html"), ("content_files_json", "content.files.json")):
        p = os.path.join(build, rel)
        if os.path.isfile(p):
            sizes[f"{name}_bytes"] = os.path.getsize(p)
            if rel != "tdd.html":
                sizes[f"{name}_tokens_est"] = C.tokens_est(os.path.getsize(p))
    man = C.read_json(os.path.join(build, "content.files.json")) or {}
    media = [v for v in (man.get("files") or {}).values() if isinstance(v, str) and os.path.isfile(v)]
    if media:
        sizes["media_files"], sizes["media_bytes"] = len(media), sum(os.path.getsize(v) for v in media)
    lane = next((s for s in packet_out["scores"] if s["name"] == "lane_size" and s["status"] == "ok"), None)
    if lane:
        sizes.update({f"agents_{k}": v for k, v in lane["metrics"].items()})
    if inp.get("source"):
        sizes["source_bytes"], sizes["source_tokens_est"] = inp["source"]["bytes"], C.tokens_est(inp["source"]["bytes"])
    scores.append(C.metric("build_sizes", sizes, "bytes on disk; *_tokens_est = bytes / 3.6 (an estimate, not a tokenizer)", "build/"))
    rev = (ctx["meta"] or {}).get("rev")
    parts = [f"session ${session_usd:.4f}" if session_usd is not None else "session not measured (no --session)", f"eval calls ${eval_usd:.4f}"]
    measured_any = session_usd is not None or eval_usd > 0
    total = (session_usd or 0) + eval_usd
    line = (f"rev {rev}: ${total:.4f} API-equivalent ({'; '.join(parts)})" if measured_any
            else f"rev {rev}: cost not measured (no --session and no judge/probe calls this run)")
    scores.append(C.metric("cost_per_revision", {"usd_api_equivalent": (round(total, 4) if measured_any else None), "session_measured": int(session_usd is not None)},
                           "session cost (when --session) + this run's eval-call cost; API list-price equivalent, not a bill", "ev_tokens", {"line": line}))
    data["cost_line"] = line
    return C.output("tokens", key, scores, data)
