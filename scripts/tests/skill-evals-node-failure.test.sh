#!/usr/bin/env bash
# Tests for scripts/skill-evals.sh --selftest when node itself fails (#2027): a node that exits non-zero while checking a valid
# regex grader or tool_used input_match is reported as SELFTEST-REGEX "node failed (exit N)", never read as "compiles fine".
# A stub `node` exits 9 on any call that builds a RegExp and runs the real node otherwise. No model, no network, no login.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REAL="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="${SKILL_EVALS_SCRIPT:-$REAL/scripts/skill-evals.sh}"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/skill-evals-nodefail.XXXXXX")"
cleanup() { find "$TMPROOT" -delete 2>/dev/null || true; }
trap cleanup EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

REAL_NODE="$(command -v node)"
REPO="$TMPROOT/repo" BIN="$TMPROOT/bin" STATE="$TMPROOT/state"
mkdir -p "$REPO/scripts" "$REPO/evals" "$REPO/skills/alpha" "$REPO/skills/_shared" "$REPO/config/voice" "$STATE" "$BIN" "$TMPROOT/home"
cp "$SCRIPT" "$REPO/scripts/skill-evals.sh"
cp -a "$REAL/evals/plugin" "$REAL/evals/stubs" "$REAL/evals/_lib" "$REAL/evals/mocks" "$REPO/evals/"
cp "$REAL/skills/_shared/repo-slug.sh" "$REPO/skills/_shared/"
printf -- '---\nname: alpha\ndescription: Use when the cart total looks wrong after a bulk order. Not for new features.\n---\nalpha body\n' >"$REPO/skills/alpha/SKILL.md"
printf 'preamble\n' >"$REPO/skills/_preamble.md"
echo "voice" >"$REPO/config/voice/joi.md"

r="$REPO/evals/alpha/routing/alpha-natural-1" b="$REPO/evals/alpha/behaviour/alpha-forced"
mkdir -p "$r/graders/_selftest" "$b/graders/_selftest"
printf -- '---\nmax_turns: 5\ntimeout_seconds: 60\nallowed_tools: [Skill]\n---\nThe order total looks off.\n' >"$r/prompt.md"
printf -- '---\ntype: tool_used\ntool: Skill\ninput_match: alpha\narm: with-only\n---\n' >"$r/graders/skill-fired.md"
printf 'Skill\t{"skill":"alpha"}\n' >"$r/graders/_selftest/skill-fired.pass.tools"
printf 'Bash\tls\n' >"$r/graders/_selftest/skill-fired.fail.tools"
printf 'schema_version: "1.1"\nname: alpha-forced\n' >"$b/case.yaml"
printf -- '---\nmax_turns: 5\ntimeout_seconds: 60\nallowed_tools: [Skill, Write]\n---\nUse the alpha skill.\n' >"$b/prompt.md"
printf -- '---\ntype: regex\n---\nALPHA-DONE\n' >"$b/graders/has-done.md"
printf 'all ALPHA-DONE\n' >"$b/graders/_selftest/has-done.pass.txt"
printf 'nothing here\n' >"$b/graders/_selftest/has-done.fail.txt"
printf '#!/usr/bin/env bash\nexit 0\n' >"$REPO/evals/alpha/fixture.sh"

git -C "$REPO" init -q -b main
git -C "$REPO" config user.email t@example.invalid
git -C "$REPO" config user.name t
git -C "$REPO" config commit.gpgsign false
git -C "$REPO" add -A
git -C "$REPO" commit -q -m base
git -C "$REPO" update-ref refs/remotes/origin/main HEAD
git -C "$REPO" checkout -q -b feat/test

cat >"$BIN/node" <<EOF
#!/usr/bin/env bash
case "\$*" in *"new RegExp"*) echo "node: stub failure" >&2; exit 9 ;; esac
exec "$REAL_NODE" "\$@"
EOF
chmod +x "$BIN/node"

set +e
OUT="$(cd "$REPO" && env -i PATH="$BIN:$PATH" HOME="$TMPROOT/home" TERM=dumb LANG=C.UTF-8 AW_STATE_DIR="$STATE" bash scripts/skill-evals.sh --skills alpha --selftest 2>&1)"
RC=$?
set -e

[ "$RC" -eq 1 ] && ok "selftest exits 1 when node fails" || { fail "selftest exit $RC, want 1"; printf '%s\n' "$OUT" | sed 's/^/      | /'; }
for g in skill-fired has-done; do
  if printf '%s\n' "$OUT" | has -E -- "SELFTEST-REGEX: .*$g\.md: .*node failed \(exit 9\)"; then
    ok "$g.md: a node failure is reported"
  else
    fail "$g.md: no 'node failed (exit 9)' line"; printf '%s\n' "$OUT" | sed 's/^/      | /'
  fi
done

echo
echo "skill-evals-node-failure: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
