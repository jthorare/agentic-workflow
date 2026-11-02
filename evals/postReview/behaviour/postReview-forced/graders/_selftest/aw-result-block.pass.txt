[tool_use] Skill {"skill": "postReview"}
[tool_use] Read {"file_path": "/work/permit-desk-ws/.aw-state/permit-desk-ws/reviews/23.json"}

PR 23 ("Owner search and renew endpoint") has a saved review from two agents, and `posted` is false. AW_GIT_MODE is readonly, so I posted nothing and left the saved file as it was.

Where the comments would land:
- security-sentinel: `src/permits/lookup.py` (position 31, owner search builds SQL from user text) and `src/api/renew.py` (position 18, renew() never checks who is calling)
- performance-oracle: `src/permits/list.py` (position 9, one owner query per permit) plus one top-level note about the missing index on permits.expires

Would run:
```bash
gh api repos/permit-desk-demo/permit-desk/pulls/23/reviews --method POST --input review-security-sentinel.json
gh api repos/permit-desk-demo/permit-desk/pulls/23/reviews --method POST --input review-performance-oracle.json
```
Each JSON file carries commit_id fa1473a4442d090cf5faee738ec502348bf6ef58, event COMMENT, the agent's summary body and its inline comments (security-sentinel: lookup.py at 31, renew.py at 18; performance-oracle: list.py at 9).

That is 4 comments in 2 API calls.

```aw-result
{"skill": "postReview", "artifacts": [], "summary": "readonly: 2 batched reviews printed under Would run, nothing posted"}
```

## Next steps
- /addressReview
