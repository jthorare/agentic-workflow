#!/usr/bin/env bash
# skills/_shared/worktree-path.sh: the default root, worktrees.json, ~ and relative roots, AW_WORKTREE_ROOT over the file,
# a root inside the repo refused, malformed JSON falling back with a warning, the same answers without jq, usage exit 2.
# HOME points at a temp dir; nothing under the real home is read or written.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HELPER="$ROOT/skills/_shared/worktree-path.sh"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

TMP="$(cd "$TMP" && pwd -P)"
export HOME="$TMP/home"
mkdir -p "$HOME"
unset AW_WORKTREE_ROOT
REPO="$TMP/work/webapp"
git init -q -b main "$REPO"
CFG="$REPO/.agentic-workflow/worktrees.json"
mkdir -p "$REPO/.agentic-workflow"

# NOJQ: a PATH with the tools the helper needs and no jq
NOJQ="$TMP/nojq-bin"
mkdir -p "$NOJQ"
for t in git dirname basename sed tr cat; do ln -s "$(command -v "$t")" "$NOJQ/$t"; done

run() { (cd "$REPO" && bash "$HELPER" "$@" 2>"$TMP/err"); }
run_nojq() { (cd "$REPO" && PATH="$NOJQ" "$(command -v bash)" "$HELPER" "$@" 2>"$TMP/err"); }
HAVE_JQ=0; command -v jq >/dev/null 2>&1 && HAVE_JQ=1

check() { # <label> <want stdout> <cmd...>
  local label="$1" want="$2" got rc
  shift 2
  got="$("$@")"; rc=$?
  { [ "$rc" = 0 ] && [ "$got" = "$want" ]; } && ok "$label" || bad "$label: rc=$rc got=$got want=$want"
}

check "default root is ~/repos/aw-wt/<repo-name>" "$HOME/repos/aw-wt/webapp/fix" run fix

printf '{"root": "/data/wt"}\n' > "$CFG"
check "worktrees.json root" "/data/wt/fix" run fix
check "worktrees.json root, no jq" "/data/wt/fix" run_nojq fix

printf '{"root": "~/trees/web"}\n' > "$CFG"
check "~ expands to HOME" "$HOME/trees/web/fix" run fix
check "~ expands to HOME, no jq" "$HOME/trees/web/fix" run_nojq fix

printf '{"root": "../trees"}\n' > "$CFG"
check "a relative root is relative to the main checkout" "$TMP/work/trees/fix" run fix
check "a relative root, no jq" "$TMP/work/trees/fix" run_nojq fix

printf '{\n  "other": 1,\n  "root": "/data/multi"\n}\n' > "$CFG"
check "a multi-line file, no jq" "/data/multi/fix" run_nojq fix

printf '{"root": "/data/wt"}\n' > "$CFG"
got="$(cd "$REPO" && AW_WORKTREE_ROOT=/env/wt bash "$HELPER" fix 2>/dev/null)"
[ "$got" = "/env/wt/fix" ] && ok "AW_WORKTREE_ROOT wins over the file" || bad "env override got=$got"

# a linked worktree finds the same main checkout
git -C "$REPO" -c user.email=t@t -c user.name=t commit -q --allow-empty -m first
git -C "$REPO" worktree add -q "$TMP/linked" -b linked
got="$(cd "$TMP/linked" && bash "$HELPER" fix 2>/dev/null)"
[ "$got" = "/data/wt/fix" ] && ok "run from a linked worktree, the root is the main checkout's" || bad "linked got=$got"
unset got

for r in "$REPO" "$REPO/trees" "sub" ".." "$REPO/../webapp/x"; do
  printf '{"root": "%s"}\n' "$r" > "$CFG"
  out="$(run fix)"; rc=$?
  if [ "$r" = ".." ]; then
    # ".." is the parent of the repo: outside it
    [ "$rc" = 0 ] && [ "$out" = "$TMP/work/fix" ] && ok "a root that is the repo's parent is allowed" || bad "parent root rc=$rc out=$out"
  else
    { [ "$rc" = 1 ] && [ -z "$out" ] && grep -q "worktree-path: root .* is inside the repo $REPO" "$TMP/err"; } \
      && ok "root '$r' inside the repo is refused (exit 1, message)" || bad "inside '$r' rc=$rc out=$out err=$(cat "$TMP/err")"
  fi
done

printf '{"root": ' > "$CFG"
out="$(run fix)"; rc=$?
{ [ "$rc" = 0 ] && [ "$out" = "$HOME/repos/aw-wt/webapp/fix" ] && grep -q "worktree-path:.*malformed" "$TMP/err"; } \
  && ok "malformed JSON falls back to the default with a warning" || bad "malformed rc=$rc out=$out err=$(cat "$TMP/err")"
out="$(run_nojq fix)"; rc=$?
{ [ "$rc" = 0 ] && [ "$out" = "$HOME/repos/aw-wt/webapp/fix" ] && grep -q "worktree-path:.*malformed" "$TMP/err"; } \
  && ok "malformed JSON, no jq, same fallback" || bad "malformed no jq rc=$rc out=$out"

printf '{"other": 1}\n' > "$CFG"
out="$(run fix)"; rc=$?
{ [ "$rc" = 0 ] && [ "$out" = "$HOME/repos/aw-wt/webapp/fix" ] && [ -s "$TMP/err" ]; } \
  && ok "no \"root\" key falls back with a warning" || bad "no root rc=$rc out=$out"

[ "$HAVE_JQ" = 1 ] && ok "jq is on PATH, so the jq branch ran above" || echo "NOTE: no jq on this box; the jq branch was not exercised"

run >/dev/null; rc=$?
{ [ "$rc" = 2 ] && grep -q usage "$TMP/err"; } && ok "no name exits 2 with usage" || bad "no name rc=$rc"
run a b >/dev/null; rc=$?
[ "$rc" = 2 ] && ok "two names exit 2" || bad "two names rc=$rc"

NOREPO="$TMP/plain"; mkdir -p "$NOREPO"
(cd "$NOREPO" && GIT_CEILING_DIRECTORIES="$TMP" bash "$HELPER" fix >/dev/null 2>&1); rc=$?
[ "$rc" = 1 ] && ok "outside a repo exits 1" || bad "outside repo rc=$rc"

[ -e "$HOME/repos" ] && bad "the helper created a directory" || ok "the helper creates nothing"

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
