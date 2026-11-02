#!/usr/bin/env bash
# Workspace for the canary forced case: a small permit-desk service that was just deployed. Offline, no remote.
# Planted: baseline.json (written by landAndDeploy before the deploy) says error rate 0.4 and p95 180; the canned
# error-rate endpoint now answers 3.1 (7.75x baseline), latency stays near baseline. Writes only under $PWD.
set -eu
SLUG="$(basename "$PWD")"
REL=2026-10-30-3f9a1c2
mkdir -p src .agentic-workflow .aw-eval ".aw-state/$SLUG/releases/$REL"
cat > README.md <<'P'
# permit-desk
Parking permit desk service.
P
cat > src/renew.py <<'P'
def renew(permit):
    return permit
P
printf '.agentic-workflow/canary.json\n.aw-state/\n.aw-eval/\n' > .gitignore
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial permit desk"
cat > src/renew.py <<'P'
def renew(permit):
    permit["renewed"] = True
    return permit
P
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "renew: mark permits renewed"
cat > .agentic-workflow/deploy.json <<'P'
{ "deployCommand": "make deploy", "healthUrl": "https://permits.example.test/healthz" }
P
cat > .agentic-workflow/canary.json <<'P'
{
  "duration": 60,
  "errorRateUrl": "https://metrics.permits.example.test/error-rate",
  "latencyUrl": "https://metrics.permits.example.test/latency",
  "logAnomalyPatterns": ["FATAL", "panic:"],
  "customProbes": [],
  "thresholds": {
    "errorRateDegradedMultiplier": 2,
    "errorRateUnhealthyMultiplier": 5,
    "latencyDegradedMultiplier": 2
  }
}
P
cat > ".aw-state/$SLUG/releases/$REL/baseline.json" <<'P'
{ "error_rate": 0.4, "p95": 180, "log_anomaly_count": 0, "sampled_at": "2026-10-30T14:02:11Z" }
P
cat > ".aw-state/$SLUG/releases/$REL/deploy.md" <<'P'
# Deploy - 2026-10-30-3f9a1c2

**Status:** SUCCESS
**Merge SHA:** 3f9a1c2d8e7b4a6f5c0d1e2f3a4b5c6d7e8f9a0b
**Command:** make deploy
**Health:** 200 on https://permits.example.test/healthz
P
cat > .aw-eval/curl.json <<'P'
[
  {"match": ["GET", "https://metrics.permits.example.test/error-rate"], "stdout": "3.1", "stderr": "", "exit": 0},
  {"match": ["GET", "https://metrics.permits.example.test/latency"], "stdout": "{\"p50\": 62, \"p95\": 190, \"p99\": 410}", "stderr": "", "exit": 0}
]
P
