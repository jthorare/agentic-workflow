#!/usr/bin/env bash
# scripts/check-skills-worktree-path.sh: the real tree passes; a planted skill with a bare `git worktree add` fails naming
# file:line; using worktree-path.sh in the same file fixes it; prose mentions pass; bad usage exits 2.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/check-skills-worktree-path.sh"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

out="$(bash "$CHECK" 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "the real tree passes" || bad "real tree rc=$rc: $out"

SK="$TMP/skills/planted"
mkdir -p "$SK" "$TMP/bootstrap"
printf '# planted\n\n```bash\ngit worktree add -b x "$HOME/wt/x" main\n```\n' > "$SK/SKILL.md"
out="$(bash "$CHECK" "$TMP" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -q "planted/SKILL.md:4" <<<"$out"; } && ok "a fenced bare git worktree add fails naming file:line" || bad "fenced rc=$rc out=$out"

printf '# planted\n\nRun `git -C "$MAIN" worktree add -b x "$WT" main`.\n' > "$SK/SKILL.md"
out="$(bash "$CHECK" "$TMP" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -q "planted/SKILL.md:3" <<<"$out"; } && ok "an inline git -C worktree add fails" || bad "inline rc=$rc out=$out"

printf '#!/usr/bin/env bash\n# git worktree add is described here\ngit -C "$repo" worktree add -q "$path" main\n' > "$SK/make.sh"
printf '# planted\n' > "$SK/SKILL.md"
out="$(bash "$CHECK" "$TMP" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -q "make.sh:3" <<<"$out" && ! grep -q "make.sh:2" <<<"$out"; } && ok "a .sh command fails, its comment does not" || bad "sh rc=$rc out=$out"

printf '#!/usr/bin/env bash\npath="$(bash "$HERE/../../_shared/worktree-path.sh" x)"\ngit -C "$repo" worktree add -q "$path" main\n' > "$SK/make.sh"
bash "$CHECK" "$TMP" >/dev/null 2>&1 && ok "naming worktree-path.sh in the file makes it pass" || bad "helper did not fix the .sh"

: > "$SK/make.sh"
printf '# planted\n\n```bash\nWT=$(worktree-path.sh x)\ngit worktree add -b x "$WT" main\n```\n' > "$SK/SKILL.md"
bash "$CHECK" "$TMP" >/dev/null 2>&1 && ok "using the helper in a SKILL.md passes" || bad "helper did not fix the md"

printf '# planted\n\nNo `git worktree add`: use `git checkout -b`. Teardown runs `git worktree remove`.\n' > "$SK/SKILL.md"
out="$(bash "$CHECK" "$TMP" 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "prose that says no \`git worktree add\` passes" || bad "prose rc=$rc out=$out"

printf '# b\n\n```\ngit worktree add x y\n```\n' > "$TMP/bootstrap/SKILL.md"
out="$(bash "$CHECK" "$TMP" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -q "bootstrap/SKILL.md:4" <<<"$out"; } && ok "bootstrap/ is scanned" || bad "bootstrap rc=$rc out=$out"

bash "$CHECK" "$TMP/missing" >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "a missing dir exits 2" || bad "missing dir rc=$rc"
bash "$CHECK" a b >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "two arguments exit 2" || bad "two args rc=$rc"

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
