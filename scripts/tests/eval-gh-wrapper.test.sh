#!/usr/bin/env bash
# evals/stubs/bin/gh and curl: under AW_GIT_MODE=readonly gh refuses writes (exit 1, `aw-eval: gh <sub> refused (AW_GIT_MODE=readonly)`),
# then answers from .aw-eval/gh.json, then passes through to the real gh; curl never touches the network. Uses a fake real gh.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STUB_DIR="$ROOT/evals/stubs/bin"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT

mkdir -p "$TMP/real" "$TMP/ws/sub/deep"
cat >"$TMP/real/gh" <<'EOF'
#!/usr/bin/env bash
echo "REAL-GH $*"
EOF
chmod +x "$TMP/real/gh"

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }
CWD="$TMP/ws"
wrapped() { (cd "$CWD" && PATH="$STUB_DIR:$TMP/real:$PATH" "$@"); }
gh_ro() { AW_GIT_MODE=readonly wrapped gh "$@" 2>&1; }

expect_refused() { # <label> <sub text> <gh args...>
  local label="$1" sub="$2" out rc; shift 2
  out="$(gh_ro "$@")"; rc=$?
  if [ "$rc" = 1 ] && [ "$out" = "aw-eval: gh $sub refused (AW_GIT_MODE=readonly)" ]; then ok "$label refused"; else bad "$label: rc=$rc out=$out"; fi
}
expect_real() { # <label> <gh args...>
  local label="$1" out rc; shift
  out="$(gh_ro "$@")"; rc=$?
  if [ "$rc" = 0 ] && [ "$out" = "REAL-GH $*" ]; then ok "$label passes through"; else bad "$label: rc=$rc out=$out"; fi
}

for f in create merge comment review edit close; do expect_refused "pr $f" "pr $f" pr "$f" 3; done
expect_refused "issue create" "issue create" issue create -t x
expect_refused "issue comment" "issue comment" issue comment 3 -b x
expect_refused "-R before the subcommand" "pr merge" -R o/r pr merge 3
expect_refused "--repo before the subcommand" "pr merge" --repo o/r pr merge 3
expect_refused "--repo=X before the subcommand" "pr merge" --repo=o/r pr merge 3
expect_refused "-R after the subcommand" "pr merge" pr merge 3 -R o/r
expect_refused "api -X POST" "api" api -X POST repos/o/r/issues
expect_refused "api -XPOST" "api" api -XPOST repos/o/r/issues
expect_refused "api --method POST" "api" api --method POST repos/o/r/issues
expect_refused "api --method=post" "api" api --method=post repos/o/r/issues
expect_refused "api -X PUT" "api" api -X PUT repos/o/r/x
expect_refused "api -X patch" "api" api -X patch repos/o/r/x
expect_refused "api -X DELETE" "api" api -X DELETE repos/o/r/x
expect_refused "api -f with no method" "api" api repos/o/r/issues -f title=x
expect_refused "api -F with no method" "api" api repos/o/r/issues -F body=@f
expect_refused "api --field with no method" "api" api repos/o/r/issues --field title=x
expect_refused "api --raw-field with no method" "api" api repos/o/r/issues --raw-field title=x
expect_refused "api --input with no method" "api" api repos/o/r/issues --input f.json
expect_refused "-R then api -f" "api" -R o/r api repos/o/r/issues -f title=x

expect_real "pr view" pr view 3
expect_real "pr list with -R" -R o/r pr list --json number
expect_real "pr checks" pr checks 3
expect_real "api read" api repos/o/r/pulls/3
expect_real "api -X GET with a field" api -X GET search/issues -f q=bug
expect_real "api --method GET with a field" api --method GET search/issues -f q=bug
expect_real "api --method=get with a field" api --method=get search/issues -f q=bug
expect_real "api -XGET with a field" api -XGET search/issues -f q=bug
out="$(AW_GIT_MODE=full wrapped gh pr merge 3 2>&1)"; rc=$?
{ [ "$rc" = 0 ] && [ "$out" = "REAL-GH pr merge 3" ]; } && ok "full mode lets a write through" || bad "full mode: rc=$rc out=$out"

echo "--- canned answers"
mkdir -p "$TMP/ws/.aw-eval"
cat >"$TMP/ws/.aw-eval/gh.json" <<'EOF'
[
 {"match": ["pr","view","3","--json","title,state"], "stdout": "FIRST", "exit": 0},
 {"match": ["pr","view","3"], "stdout": "SECOND"},
 {"match": ["pr","view","4"], "stdout": "FOUR", "stderr": "warn\n", "exit": 5},
 {"match": ["issue","list","--state=open"], "stdout": "OPEN-ISSUES"}
]
EOF
canned() { # <label> <want stdout> <gh args...>
  local label="$1" want="$2" out rc; shift 2
  out="$(gh_ro "$@")"; rc=$?
  if [ "$rc" = 0 ] && [ "$out" = "$want" ]; then ok "$label"; else bad "$label: rc=$rc out=$out"; fi
}
canned "a call matching an entry gets its stdout" SECOND pr view 3
canned "the first matching entry wins" FIRST pr view 3 --json title,state
canned "-R before the subcommand does not matter" FIRST -R o/r pr view 3 --json title,state
canned "-R after does not matter either" FIRST pr view 3 --json title,state -R o/r
canned "--repo=X does not matter" SECOND pr view 3 --repo=o/r
canned "--json a,b matches --json b,a" FIRST pr view 3 --json state,title
canned "a flag in the call but not in match is ignored" SECOND pr view 3 --web
canned "an extra flag next to a matching flag is ignored" FIRST pr view 3 --json title,state --comments
canned "--state=open matches --state open" OPEN-ISSUES issue list --state open
canned "--state=open matches --state=open" OPEN-ISSUES issue list --state=open --limit 5
out="$(gh_ro pr view 4)"; rc=$?
{ [ "$rc" = 5 ] && [ "$out" = "FOURwarn" ]; } && ok "stderr and exit come from the entry" || bad "entry exit: rc=$rc out=$out"
out="$(gh_ro pr view 9)"; rc=$?
{ [ "$rc" = 1 ] && [ "$out" = "aw-eval: gh pr view 9 has no canned answer" ]; } && ok "no match with a file present: no-canned-answer line, exit 1" || bad "no match: rc=$rc out=$out"
canned "a call whose --json set differs from the first entry falls to the next matching one" SECOND pr view 3 --json state
CWD="$TMP/ws/sub/deep"
canned "a call from a subdirectory still finds the file" SECOND pr view 3
expect_refused "a write from a subdirectory is still refused" "pr merge" pr merge 3
out="$(AW_GIT_MODE=readonly wrapped gh pr merge 3 2>&1)"
CWD="$TMP/ws"
rm "$TMP/ws/.aw-eval/gh.json"
expect_real "no file: the real gh answers" pr view 3
CWD="$TMP/ws/sub"; expect_real "no file, from a subdirectory: the real gh answers" pr view 3
CWD="$TMP/ws"

echo "--- curl"
C() { (cd "$CWD" && PATH="$STUB_DIR:$PATH" curl "$@" 2>&1); }
out="$(C https://app.example.invalid/health)"; rc=$?
{ [ "$rc" = 7 ] && [ "$out" = "aw-eval: curl GET https://app.example.invalid/health has no canned answer" ]; } && ok "curl with no file: no-canned-answer line, exit 7" || bad "curl no file: rc=$rc out=$out"
cat >"$TMP/ws/.aw-eval/curl.json" <<'EOF'
[
 {"match": ["GET","https://app.example.invalid/health"], "stdout": "{\"ok\":true}"},
 {"match": ["POST","https://app.example.invalid/deploy"], "stdout": "DEPLOYED", "exit": 0},
 {"match": ["https://app.example.invalid/any"], "stdout": "ANY-FIRST"},
 {"match": ["GET","https://app.example.invalid/any"], "stdout": "ANY-SECOND"}
]
EOF
out="$(C -s https://app.example.invalid/health/live)"; rc=$?
{ [ "$rc" = 0 ] && [ "$out" = '{"ok":true}' ]; } && ok "curl matches a URL prefix" || bad "curl prefix: rc=$rc out=$out"
out="$(C -X POST https://app.example.invalid/deploy)"; rc=$?
{ [ "$rc" = 0 ] && [ "$out" = DEPLOYED ]; } && ok "curl reads the method from -X" || bad "curl -X: rc=$rc out=$out"
out="$(C --request POST https://app.example.invalid/deploy)"; [ "$out" = DEPLOYED ] && ok "curl reads the method from --request" || bad "curl --request: $out"
out="$(C -d x=1 https://app.example.invalid/deploy)"; [ "$out" = DEPLOYED ] && ok "curl -d means POST" || bad "curl -d: $out"
out="$(C --data-raw x https://app.example.invalid/deploy)"; [ "$out" = DEPLOYED ] && ok "curl --data* means POST" || bad "curl --data: $out"
out="$(C -F f=@x https://app.example.invalid/deploy)"; [ "$out" = DEPLOYED ] && ok "curl -F means POST" || bad "curl -F: $out"
out="$(C https://app.example.invalid/deploy)"; rc=$?
{ [ "$rc" = 7 ] && [ "$out" = "aw-eval: curl GET https://app.example.invalid/deploy has no canned answer" ]; } && ok "a GET does not match a POST entry" || bad "curl method mismatch: rc=$rc out=$out"
out="$(C https://app.example.invalid/any)"; [ "$out" = ANY-FIRST ] && ok "curl: the first matching entry wins; a lone item is a GET prefix" || bad "curl first match: $out"
out="$(C https://elsewhere.invalid/)"; rc=$?
{ [ "$rc" = 7 ] && [ "$out" = "aw-eval: curl GET https://elsewhere.invalid/ has no canned answer" ]; } && ok "curl: no match, exit 7, no network" || bad "curl no match: rc=$rc out=$out"
CWD="$TMP/ws/sub/deep"
out="$(C https://app.example.invalid/health)"; [ "$out" = '{"ok":true}' ] && ok "curl from a subdirectory finds the file" || bad "curl subdir: $out"

echo
[ "$fails" -eq 0 ] && echo "eval-gh-wrapper: all passed" || echo "eval-gh-wrapper: $fails failed"
[ "$fails" -eq 0 ]
