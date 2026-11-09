"""LLM-judged rubric over the document text (--with-judge only): clarity, evidence grounding, decision completeness,
consistency of terms, each 0..1 with a one-line reason. Claude (claude-sonnet-5-5) when ANTHROPIC_API_KEY is set; else
autoevals' LLMClassifier with its default model when OPENAI_API_KEY or BRAINTRUST_API_KEY is set; else "not run"."""
import os
import ev_common as C
import ev_llm as L

CRITERIA = {
    "clarity": "A reader new to the project can follow what is being built, for whom and why; terms are defined before use; each section stands on its own.",
    "evidence_grounding": "Claims about today's system and every number carry evidence (a path:line citation at a commit, a measured source and date); proposals are marked as proposals, never stated as facts.",
    "decision_completeness": "Every open question is an explicit decision with its options, owner and status; decided ones say who decided and when, and their consequences are stated.",
    "term_consistency": "One concept keeps one name throughout; identifiers (D-numbers, PR numbers, table and column names) are used the same way everywhere.",
}
SYSTEM = ("You grade technical design documents against a fixed rubric. Score each criterion from 0 to 1 (0 = absent, 0.5 = partly met, "
          "1 = fully met) and give a reason of at most two sentences that cites a concrete place in the document. Grade only what the text shows.")
SCHEMA = {"type": "object", "additionalProperties": False, "required": list(CRITERIA),
          "properties": {k: {"type": "object", "additionalProperties": False, "required": ["score", "reason"],
                             "properties": {"score": {"type": "number"}, "reason": {"type": "string"}}} for k in CRITERIA}}
CHOICES = {"A": 1.0, "B": 0.75, "C": 0.5, "D": 0.25, "E": 0.0}


def doc_text(build, content):
    p = os.path.join(build, "agents", "tdd.md")
    if os.path.isfile(p):
        return C.read_text(p), "build/agents/tdd.md"
    if content:
        return C.content_text(content), "content.json text (C.content_text)"
    return None, None


def judge(build, content, enabled):
    names = [f"judge_{k}" for k in CRITERIA]
    method = "LLM rubric (references/eval-loop.md, Judge): one structured-output call, 0..1 per criterion"
    if not enabled:
        return [C.not_run(n, "pass --with-judge", method, "judge") for n in names]
    text, src = doc_text(build, content)
    if not text:
        return [C.not_run(n, "no document text (no agents/tdd.md, no content.json)", method, "judge") for n in names]
    ok, why = L.claude_available()
    if ok:
        user = f"<document>\n{text}\n</document>\n\nRubric:\n" + "\n".join(f"- {k}: {v}" for k, v in CRITERIA.items()) + "\n\nReturn the JSON object."
        data, err, row = L.claude_json("judge", SYSTEM, user, SCHEMA)
        m = {"input_tokens": (row.get("usage") or {}).get("input_tokens"), "output_tokens": (row.get("usage") or {}).get("output_tokens")}
        if err:
            return [C.error(n, err, method + f" ({L.JUDGE_MODEL})", src, m) for n in names]
        out = []
        for k in CRITERIA:
            v = max(0.0, min(1.0, float((data.get(k) or {}).get("score", 0))))
            out.append(C.score(f"judge_{k}", v, method + f" ({L.JUDGE_MODEL}, anthropic SDK, output_config json_schema; clamped to 0..1)", src,
                               m if k == "clarity" else None, {"reason": (data.get(k) or {}).get("reason", "")}))
        return out
    ok2, why2 = L.autoevals_available()
    if ok2:
        from autoevals import LLMClassifier
        from autoevals.oai import get_default_model
        model = get_default_model()
        out = []
        for k, crit in CRITERIA.items():
            prompt = ("You are grading a technical design document on one criterion.\nCriterion: " + crit +
                      "\n\nDocument:\n{{output}}\n\nChoose one: (A) fully met, (B) mostly met, (C) partly met, (D) barely met, (E) absent.")
            try:
                r = LLMClassifier(name=f"judge_{k}", prompt_template=prompt, choice_scores=CHOICES, use_cot=True).eval(output=text)
            except Exception as e:
                out.append(C.error(f"judge_{k}", f"autoevals raised {type(e).__name__}: {str(e)[:160]}", method, src))
                continue
            L.LEDGER.append({"purpose": f"judge_{k}", "provider": "autoevals", "model": model, "at": C.now(), "usage": None,
                             "usd": None, "note": "autoevals does not expose token usage on the Score"})
            out.append(C.score(f"judge_{k}", r.score if r.score is not None else 0, method + f" (autoevals LLMClassifier, {model}; A..E -> 1..0)", src,
                               detail={"rationale": str((r.metadata or {}).get("rationale", ""))[:400]}))
        return out
    return [C.not_run(n, f"{why}; {why2}", method, src or "judge") for n in names]
