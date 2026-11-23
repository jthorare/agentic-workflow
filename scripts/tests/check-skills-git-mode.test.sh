#!/usr/bin/env bash
# scripts/check-skills-git-mode.sh: the real skills/ tree passes; a planted skill with a git or gh write command and no
# Git mode section fails naming file:line; adding the section fixes it; bad usage exits 2.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/check-skills-git-mode.sh"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

out="$(bash "$CHECK" 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "the real skills/ tree passes" || bad "real tree rc=$rc: $out"

SK="$TMP/skills"
mkdir -p "$SK/readsOnly" "$SK/pusher" "$SK/_shared"
printf '# readsOnly\n\n`git status` and `gh pr view 3` only.\n' > "$SK/readsOnly/SKILL.md"
printf '# pusher\n\nRun `git push origin main` when done.\n' > "$SK/pusher/SKILL.md"
printf 'git commit -m x\n' > "$SK/_shared/frag.md"

out="$(bash "$CHECK" "$SK" 2>&1)"; rc=$?
if [ "$rc" = 1 ] && grep -q "pusher/SKILL.md:3" <<<"$out" && grep -q "'pusher'" <<<"$out"; then
  ok "a planted git push without the section fails naming it"
else
  bad "planted pusher rc=$rc out=$out"
fi
grep -q readsOnly <<<"$out" && bad "a read-only skill was flagged" || ok "read-only git and gh commands are not flagged"
grep -q _shared <<<"$out" && bad "_shared was flagged" || ok "_shared fragments are not scanned"

printf '\n## Git mode\n\n`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.\n' >> "$SK/pusher/SKILL.md"
bash "$CHECK" "$SK" >/dev/null 2>&1 && ok "adding the section makes it pass" || bad "section did not fix it"

printf '# gh\n\n```\ngh api repos/o/r/pulls/1/reviews --method POST --input -\n```\n' > "$SK/readsOnly/SKILL.md"
out="$(bash "$CHECK" "$SK" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -q "readsOnly/SKILL.md:4" <<<"$out"; } && ok "gh api --method POST is a write" || bad "gh api rc=$rc out=$out"

mkdir -p "$SK/helper"
printf '# helper\n' > "$SK/helper/SKILL.md"
printf 'gh pr comment 3 --body x\n' > "$SK/helper/extra.md"
out="$(bash "$CHECK" "$SK" 2>&1)"; rc=$?
grep -q "helper/extra.md:1" <<<"$out" && ok "a write command in a skill's side file counts" || bad "side file: rc=$rc out=$out"

for cmd in "git rebase origin/main" "git revert HEAD" "git merge x" "git cherry-pick abc" "git am p.patch" "git tag v1" "git branch topic"; do
  printf '# t\n\nRun `%s`.\n' "$cmd" > "$SK/helper/SKILL.md"; rm -f "$SK/helper/extra.md"
  out="$(bash "$CHECK" "$SK" 2>&1)"; grep -q "helper/SKILL.md:3" <<<"$out" && ok "$cmd is a write" || bad "$cmd not flagged"
done
printf '# t\n\n`git tag -l`, `git tag`, `git branch --list`, `git merge-base a b`, `git branch --show-current`.\n' > "$SK/helper/SKILL.md"
out="$(bash "$CHECK" "$SK" 2>&1)"; grep -q helper <<<"$out" && bad "a read form was flagged" || ok "tag/branch read forms and merge-base are not flagged"

bash "$CHECK" "$TMP/missing" >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "a missing dir exits 2" || bad "missing dir rc=$rc"
bash "$CHECK" a b >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "two arguments exit 2" || bad "two args rc=$rc"

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
