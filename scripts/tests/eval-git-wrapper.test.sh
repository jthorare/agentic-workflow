#!/usr/bin/env bash
# evals/stubs/bin/git: a pass-through to the real git; under AW_GIT_MODE=readonly it refuses commit, push, worktree add,
# checkout -b and switch -c with exit 1 and `aw-eval: git <subcommand> refused (AW_GIT_MODE=readonly)`.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STUB_DIR="$ROOT/evals/stubs/bin"
REAL_GIT="$(command -v git)"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT
export HOME="$TMP/home"; mkdir -p "$HOME"
export GIT_CONFIG_GLOBAL="$TMP/gitconfig"
"$REAL_GIT" config --file "$GIT_CONFIG_GLOBAL" user.name t
"$REAL_GIT" config --file "$GIT_CONFIG_GLOBAL" user.email t@example.com

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

REPO="$TMP/repo"
"$REAL_GIT" init -q "$REPO"
echo a > "$REPO/a.txt"
"$REAL_GIT" -C "$REPO" add a.txt
"$REAL_GIT" -C "$REPO" commit -qm first

wrapped() { PATH="$STUB_DIR:$PATH" git "$@"; }

for mode in readonly full; do
  out="$(AW_GIT_MODE=$mode wrapped -C "$REPO" status --short 2>&1)"; rc=$?
  [ "$rc" = 0 ] && ok "status passes through ($mode)" || bad "status ($mode) rc=$rc: $out"
  out="$(AW_GIT_MODE=$mode wrapped -C "$REPO" log --oneline 2>&1)"; rc=$?
  { [ "$rc" = 0 ] && grep -q first <<<"$out"; } && ok "log passes through ($mode)" || bad "log ($mode): $out"
done
out="$(AW_GIT_MODE=readonly wrapped -C "$REPO" branch --show-current 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "branch --show-current is allowed in readonly" || bad "branch --show-current rc=$rc: $out"
out="$(AW_GIT_MODE=readonly wrapped -C "$REPO" checkout -q "$("$REAL_GIT" -C "$REPO" rev-parse HEAD)" 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "plain checkout is allowed in readonly" || bad "plain checkout rc=$rc: $out"
"$REAL_GIT" -C "$REPO" checkout -q - 2>/dev/null || true

expect_refused() { # <label> <expected subcommand text> <git args...>
  local label="$1" sub="$2"; shift 2
  local out rc
  out="$(AW_GIT_MODE=readonly wrapped "$@" 2>&1)"; rc=$?
  if [ "$rc" = 1 ] && [ "$out" = "aw-eval: git $sub refused (AW_GIT_MODE=readonly)" ]; then ok "$label refused"; else bad "$label: rc=$rc out=$out"; fi
}
for form in "branch" "branch -a" "branch --list" "tag" "tag -l" "tag --list"; do
  # shellcheck disable=SC2086
  out="$(AW_GIT_MODE=readonly wrapped -C "$REPO" $form 2>&1)"; rc=$?
  [ "$rc" = 0 ] && ok "git $form is allowed in readonly" || bad "git $form rc=$rc: $out"
done
# #1995: a listing flag's commit is its value, not a name to create, whether it follows as its own argument or after `=`.
for form in "branch --contains HEAD" "branch --contains=HEAD" "branch --no-contains HEAD" "branch --merged HEAD" \
  "branch --no-merged HEAD" "branch --points-at HEAD" "branch -a --contains HEAD" "tag --contains HEAD" \
  "tag --no-contains HEAD" "tag --merged HEAD" "tag --no-merged HEAD" "tag --points-at HEAD"; do
  # shellcheck disable=SC2086
  out="$(AW_GIT_MODE=readonly wrapped -C "$REPO" $form 2>&1)"; rc=$?
  [ "$rc" = 0 ] && ok "git $form is allowed in readonly" || bad "git $form rc=$rc: $out"
done
echo b > "$REPO/b.txt"; "$REAL_GIT" -C "$REPO" add b.txt
expect_refused "commit" "commit" -C "$REPO" commit -m x
expect_refused "push" "push" -C "$REPO" push origin main
expect_refused "worktree add" "worktree add" -C "$REPO" worktree add "$TMP/wt"
expect_refused "checkout -b" "checkout -b" -C "$REPO" checkout -b feature
expect_refused "switch -c" "switch -c" -C "$REPO" switch -c feature
expect_refused "merge" "merge" -C "$REPO" merge main
expect_refused "rebase" "rebase" -C "$REPO" rebase origin/main
expect_refused "cherry-pick" "cherry-pick" -C "$REPO" cherry-pick HEAD
expect_refused "revert" "revert" -C "$REPO" revert HEAD
expect_refused "am" "am" -C "$REPO" am x.patch
expect_refused "tag <name>" "tag" -C "$REPO" tag v1
expect_refused "tag -a <name>" "tag" -C "$REPO" tag -a v1 -m x
expect_refused "branch <name>" "branch" -C "$REPO" branch newbranch
expect_refused "branch <name> <start>" "branch" -C "$REPO" branch newbranch HEAD
expect_refused "branch -f <name>" "branch" -C "$REPO" branch -f newbranch
expect_refused "tag <name> <commit>" "tag" -C "$REPO" tag v2 HEAD
# #2001: these are single flag words, yet each writes .git/config (the upstream, or the description; GIT_EDITOR stops a hang if one gets through).
export GIT_EDITOR=true
expect_refused "branch --set-upstream-to=<x>" "branch" -C "$REPO" branch --set-upstream-to=origin/x
expect_refused "branch -u<x>" "branch" -C "$REPO" branch -uorigin/x
expect_refused "branch --unset-upstream" "branch" -C "$REPO" branch --unset-upstream
expect_refused "branch --edit-description" "branch" -C "$REPO" branch --edit-description
out="$(AW_GIT_MODE=readonly wrapped -C "$REPO" branch -vv 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "git branch -vv is allowed in readonly" || bad "git branch -vv rc=$rc: $out"
# git takes unambiguous prefixes (--u, --e) and bundled short flags (-vu<x>, -a -u<x>).
expect_refused "branch --u" "branch" -C "$REPO" branch --u
expect_refused "branch --e" "branch" -C "$REPO" branch --e
expect_refused "branch -vu<x>" "branch" -C "$REPO" branch -vuorigin/x
expect_refused "branch -a -u<x>" "branch" -C "$REPO" branch -a -uother
for f in '--format=%(upstream)' '--sort=-committerdate'; do
  out="$(AW_GIT_MODE=readonly wrapped -C "$REPO" branch "$f" 2>&1)"; rc=$?
  [ "$rc" = 0 ] && ok "git branch $f is allowed in readonly" || bad "git branch $f rc=$rc: $out"
done
expect_refused "commit after -c option" "commit" -c core.editor=true -C "$REPO" commit -m x

[ "$("$REAL_GIT" -C "$REPO" rev-list --count HEAD)" = 1 ] && ok "no commit was made by the refused forms" || bad "a refused commit landed"
[ -z "$("$REAL_GIT" -C "$REPO" branch --list feature)" ] && ok "no branch was made" || bad "a refused branch landed"

out="$(AW_GIT_MODE=full wrapped -C "$REPO" commit -qm second 2>&1)"; rc=$?
{ [ "$rc" = 0 ] && [ "$("$REAL_GIT" -C "$REPO" rev-list --count HEAD)" = 2 ]; } && ok "full mode passes commit through" || bad "full commit rc=$rc: $out"
out="$(env -u AW_GIT_MODE PATH="$STUB_DIR:$PATH" git -C "$REPO" checkout -q -b other 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "unset AW_GIT_MODE means full" || bad "unset mode rc=$rc: $out"

# The wrapper must not exec itself when it is the only git on PATH.
out="$(PATH="$STUB_DIR" AW_GIT_MODE=full "$(command -v bash)" "$STUB_DIR/git" --version 2>&1)"; rc=$?
{ [ "$rc" = 127 ] && grep -q "no real git" <<<"$out"; } && ok "no real git on PATH fails clearly" || bad "no real git: rc=$rc out=$out"

# claude plugin eval runs a case's scaffold in the run's environment: the stub first on PATH, AW_GIT_MODE=readonly.
# The fixture's own commit must still land, and the agent that runs after it still sees readonly.
FIX="$TMP/fixture"; mkdir -p "$FIX"; echo c > "$FIX/c.txt"
out="$(cd "$FIX" && export AW_GIT_MODE=readonly PATH="$STUB_DIR:$PATH" && . "$ROOT/evals/_lib/fixture-lib.sh" && aw_fixture_repo init 2>&1 && printf 'mode=%s' "$AW_GIT_MODE")"; rc=$?
{ [ "$rc" = 0 ] && [ "$("$REAL_GIT" -C "$FIX" rev-list --count HEAD 2>/dev/null)" = 1 ] && grep -q 'mode=readonly$' <<<"$out"; } \
  && ok "aw_fixture_repo commits under the eval's readonly stub and leaves the mode readonly" || bad "aw_fixture_repo under readonly: rc=$rc out=$out"

# Every fixture in the tree scaffolds the same way, so each one must finish under the stub too (ui-evidence and
# verify-desktop called git directly and every case in them errored before the agent started).
while IFS= read -r f; do
  rel="${f#"$ROOT"/}"
  ws="$(mktemp -d "$TMP/ws.XXXXXX")"
  out="$(cd "$ws" && AW_GIT_MODE=readonly PATH="$STUB_DIR:$PATH" bash "$f" 2>&1)"; rc=$?
  [ "$rc" = 0 ] && ok "$rel scaffolds under the readonly stub" || bad "$rel under the readonly stub: rc=$rc $(printf '%s' "$out" | tail -1)"
done < <(find "$ROOT/evals" -name fixture.sh -type f | sort)

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
