No project docs here (no AGENTS.md, README, rules or planning files), so I'm enhancing from the request alone.

## Enhanced Prompt

**Original:** Add rate limiting to the recipe-scaling API, per API key, 60 requests a minute, answering 429 with a Retry-After header.

**Docs discovered / read / skipped:**
- none found

**Full task:**
- **Goal:** Per-API-key rate limit of 60 requests a minute on the recipe-scaling API.
- **Constraints:** Over-limit calls answer 429 with a Retry-After header.
- **Out of scope:** Limits for anything but the recipe-scaling endpoints.
- **Done when:** the 61st call inside a minute with one key gets 429 and Retry-After; a second key is unaffected.

Should I proceed with this, or adjust anything?

```aw-result
{"skill": "enhancePrompt", "artifacts": ["/ws/.aw-state/x/prompts/20261101-120000.md"], "summary": "Enhanced the rate limiting request, no docs found", "prompt_path": "/ws/.aw-state/x/prompts/20261101-120000.md"}
```
