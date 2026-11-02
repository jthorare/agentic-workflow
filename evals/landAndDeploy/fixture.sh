#!/usr/bin/env bash
# Workspace for the landAndDeploy cases: a small tool-lending library on a merged PR. Offline, no remote.
# deploy.json is untracked and git-ignored (the skill refuses a tracked one). Its deploy command is a local script that prints
# "deploy-ok". The canned gh answer says PR 18 is merged; the canned curl answers make the checkout ping smoke test return 503.
# Writes only under $PWD.
set -eu
mkdir -p scripts src .agentic-workflow .aw-eval
cat > README.md <<'P'
# toollib
Lend-and-return tracker for a neighbourhood tool library. Late fees are charged per day.
P
cat > src/late_fee.py <<'P'
def late_fee(days_late: int, per_day_cents: int = 50) -> int:
    return max(days_late, 0) * per_day_cents
P
cat > scripts/deploy.sh <<'P'
#!/usr/bin/env bash
# Stand-in deploy: prints only. Under the eval, Bash can't write anywhere a grader sees (the 0a probe, #1892).
set -eu
echo "toollib: deploy-ok release pushed"
P
chmod +x scripts/deploy.sh
printf '.agentic-workflow/deploy.json\n.aw-eval/\n.aw-state/\n' > .gitignore
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial toollib"
cat > src/late_fee.py <<'P'
def late_fee(days_late: int, per_day_cents: int = 50, cap_cents: int = 2500) -> int:
    return min(max(days_late, 0) * per_day_cents, cap_cents)
P
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "late fees: cap at 25 dollars (#18)"
SHA=$(git rev-parse HEAD)
cat > .agentic-workflow/deploy.json <<'P'
{
  "command": "bash scripts/deploy.sh",
  "healthUrl": "https://toollib.example.test/health",
  "smokeTests": [
    "curl -fsS https://toollib.example.test/api/ping",
    "curl -fsS https://toollib.example.test/api/catalog",
    "curl -fsS https://toollib.example.test/api/checkout/ping"
  ],
  "timeout": 60
}
P
cat > .aw-eval/gh.json <<P
[
  {"match": ["pr", "view"],
   "stdout": "{\"number\":18,\"state\":\"MERGED\",\"mergedAt\":\"2026-11-01T09:12:44Z\",\"mergeCommit\":{\"oid\":\"$SHA\"},\"headRefName\":\"late-fee-cap\",\"baseRefName\":\"main\",\"url\":\"https://example.test/toollib/pull/18\"}\n",
   "stderr": "", "exit": 0}
]
P
cat > .aw-eval/curl.json <<'P'
[
  {"match": ["GET", "https://toollib.example.test/health"], "stdout": "{\"status\":\"ok\",\"http\":200}\n", "stderr": "", "exit": 0},
  {"match": ["HEAD", "https://toollib.example.test/health"], "stdout": "HTTP/2 200\n", "stderr": "", "exit": 0},
  {"match": ["GET", "https://toollib.example.test/api/ping"], "stdout": "{\"pong\":true}\n", "stderr": "", "exit": 0},
  {"match": ["GET", "https://toollib.example.test/api/catalog"], "stdout": "{\"tools\":212}\n", "stderr": "", "exit": 0},
  {"match": ["GET", "https://toollib.example.test/api/checkout/ping"], "stdout": "", "stderr": "curl: (22) The requested URL returned error: 503\n", "exit": 22}
]
P
