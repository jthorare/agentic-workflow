"""Model calls made by the eval lane (judge and handoff probe) and the price table used for token cost lines.

Every call is gated on a key in the environment (ANTHROPIC_API_KEY for Claude; OPENAI_API_KEY or BRAINTRUST_API_KEY
for autoevals' default) and appended to LEDGER with the usage the API returned, so output.tokens.json can account for
the eval's own spend. The anthropic package is imported only when a call is made."""
import json, os, time
import ev_common as C

JUDGE_MODEL = "claude-sonnet-5-5"   # in anthropic 2.6.0's ModelParam list (checked in the installed SDK, 2026-11-08)
LEDGER = []

# USD per million tokens, Anthropic first-party list prices: the claude-api skill's model table (cached 2026-10-06,
# read 2026-11-08). cache_read is given only where that table or its prompt-caching page states it; otherwise 0.1x input
# (the page's general rule). Cache writes: 1.25x input for the 5-minute TTL, 2x for the 1-hour TTL (same page).
PRICES = {
    "claude-fable-5-1": {"input": 10.0, "output": 50.0, "cache_read": 0.25},
    "claude-mythos-5-1": {"input": 10.0, "output": 50.0, "cache_read": 0.25},
    "claude-fable-5": {"input": 10.0, "output": 50.0},
    "claude-opus-5-5": {"input": 4.0, "output": 20.0, "cache_read": 0.20},
    "claude-opus-5": {"input": 5.0, "output": 25.0},
    "claude-opus-4-8": {"input": 5.0, "output": 25.0},
    "claude-opus-4-7": {"input": 5.0, "output": 25.0},
    "claude-opus-4-6": {"input": 5.0, "output": 25.0},
    "claude-sonnet-5-5": {"input": 2.0, "output": 10.0, "cache_read": 0.20},
    "claude-sonnet-5": {"input": 2.0, "output": 10.0},
    "claude-sonnet-4-6": {"input": 3.0, "output": 15.0},
    "claude-haiku-5-5": {"input": 0.10, "output": 0.50},
    "claude-haiku-4-5": {"input": 1.0, "output": 5.0},
}
PRICE_SOURCE = "claude-api skill model table (cached 2026-10-06) and its prompt-caching page; API list prices, not a subscription bill"


def price_for(model):
    if not model:
        return None
    base = model.split("[")[0]
    for k in sorted(PRICES, key=len, reverse=True):
        if base == k or base.startswith(k + "-"):
            return k, PRICES[k]
    return None


def cost(model, usage):
    """USD for one usage dict {input_tokens, output_tokens, cache_read_input_tokens, cache_creation_input_tokens,
    cache_creation{ephemeral_5m_input_tokens, ephemeral_1h_input_tokens}}; None when the model is not priced."""
    hit = price_for(model)
    if not hit:
        return None
    _, p = hit
    i, o = usage.get("input_tokens") or 0, usage.get("output_tokens") or 0
    cr = usage.get("cache_read_input_tokens") or 0
    cc = usage.get("cache_creation") or {}
    c5, c1 = cc.get("ephemeral_5m_input_tokens"), cc.get("ephemeral_1h_input_tokens")
    if c5 is None and c1 is None:
        c5, c1 = usage.get("cache_creation_input_tokens") or 0, 0
    read_rate = p.get("cache_read", p["input"] * 0.1)
    usd = (i * p["input"] + o * p["output"] + cr * read_rate + (c5 or 0) * p["input"] * 1.25 + (c1 or 0) * p["input"] * 2.0) / 1e6
    return round(usd, 6)


def claude_available():
    if not os.environ.get("ANTHROPIC_API_KEY"):
        return False, "no ANTHROPIC_API_KEY"
    try:
        import anthropic  # noqa: F401
    except ImportError:
        return False, "anthropic package not installed (use the tdd-skill venv)"
    return True, None


def autoevals_available():
    if not (os.environ.get("OPENAI_API_KEY") or os.environ.get("BRAINTRUST_API_KEY")):
        return False, "no OPENAI_API_KEY or BRAINTRUST_API_KEY for autoevals"
    try:
        import autoevals  # noqa: F401
    except ImportError:
        return False, "autoevals not installed (use the tdd-skill venv)"
    return True, None


def claude_json(purpose, system, user, schema, model=JUDGE_MODEL, max_tokens=16000):
    """One Messages call with a JSON-schema output (output_config.format). Returns (data|None, error|None, ledger_row).
    No sampling parameters (Sonnet 5.5 rejects non-default ones); thinking left at the model default."""
    import anthropic
    client = anthropic.Anthropic()
    t0 = time.time()
    row = {"purpose": purpose, "provider": "anthropic", "model": model, "at": C.now()}
    try:
        resp = client.messages.create(model=model, max_tokens=max_tokens, system=system,
                                      messages=[{"role": "user", "content": user}],
                                      output_config={"format": {"type": "json_schema", "schema": schema}})
    except anthropic.RateLimitError as e:
        row.update({"error": f"rate limited: {str(e)[:160]}", "seconds": round(time.time() - t0, 2)})
        LEDGER.append(row)
        return None, row["error"], row
    except anthropic.APIStatusError as e:
        row.update({"error": f"API {getattr(e, 'status_code', '?')}: {str(e)[:160]}", "seconds": round(time.time() - t0, 2)})
        LEDGER.append(row)
        return None, row["error"], row
    except anthropic.APIConnectionError as e:
        row.update({"error": f"connection: {str(e)[:160]}", "seconds": round(time.time() - t0, 2)})
        LEDGER.append(row)
        return None, row["error"], row
    u = resp.usage
    usage = {"input_tokens": getattr(u, "input_tokens", 0) or 0, "output_tokens": getattr(u, "output_tokens", 0) or 0,
             "cache_read_input_tokens": getattr(u, "cache_read_input_tokens", 0) or 0,
             "cache_creation_input_tokens": getattr(u, "cache_creation_input_tokens", 0) or 0}
    row.update({"usage": usage, "usd": cost(resp.model or model, usage), "stop_reason": resp.stop_reason, "seconds": round(time.time() - t0, 2),
                "response_model": resp.model})
    LEDGER.append(row)
    if resp.stop_reason == "refusal":
        return None, "refusal (stop_reason)", row
    if resp.stop_reason == "max_tokens":
        return None, "hit max_tokens", row
    text = next((b.text for b in resp.content if getattr(b, "type", "") == "text"), "")
    try:
        return json.loads(text), None, row
    except ValueError as e:
        return None, f"response is not JSON ({e})", row
