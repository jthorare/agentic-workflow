[tool_use] Skill {"skill": "cso"}

Threat model written to /work/.aw-state/city-permits/plans/permit-lookup/security-review.md

Summary: Two CRITICAL findings. GET /api/permits/{permitId} has no ownership check and ids are sequential, so any signed-in user can enumerate every holder's name, address and plate (A01, IDOR). The plate search builds SQL by string concatenation (A03, SQL injection). Passwords use unsalted SHA-1 and proof-of-residence PDFs sit in a public bucket.

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 2 |
| A02 Cryptographic Failures | findings: 1 |
| A03 Injection | findings: 1 |
| A04 Insecure Design | findings: 1 |
| A05 Security Misconfiguration | findings: 1 |
| A06 Vulnerable & Outdated Components | N/A - no dependencies listed |
| A07 Identification & Authentication Failures | findings: 1 |
| A08 Software & Data Integrity Failures | No findings. |
| A09 Security Logging & Monitoring Failures | findings: 1 |
| A10 SSRF | No findings. |

Recommendation: DO-NOT-SHIP

```aw-result
{"skill": "cso", "artifacts": ["/work/.aw-state/city-permits/plans/permit-lookup/security-review.md"], "summary": "DO-NOT-SHIP: IDOR on permit ids, SQL injection in plate search", "verdict": "BLOCKED", "mode": "plan"}
```

## Next steps
- /rootCause if a finding maps to a known incident
