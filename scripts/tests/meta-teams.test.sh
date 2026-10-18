#!/usr/bin/env bash
# Tests for named meta teams in the shell glue: scripts/meta-team-lib.sh (which team's config and state directory a script uses) and the
# Claude hooks' team derivation (config/hooks/meta-stop-gate.sh, meta-outbox.sh). A stub aw-meta records the AW_META_TEAM it was run with.
# Run: bash scripts/tests/meta-teams.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOP="$(cd "$DIR/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
command -v jq > /dev/null || { echo "ok - skipped: jq not installed"; exit 0; }
W="$(mktemp -d)"; trap 'rm -r "$W"' EXIT
# shellcheck source=../meta-team-lib.sh
. "$DIR/meta-team-lib.sh"

resolve() { # state [team]: "<rc> <dir relative to state> <config relative to state>"
  local rc
  meta_resolve "$1" "${2:-}" 2> "$W/err"; rc=$?
  printf '%s %s %s' "$rc" "${META_DIR#"$1"/}" "${META_CONFIG#"$1"/}"
}
mk_named() { mkdir -p "$1/meta/teams/$2"; jq -n --arg t "$2" '{team:$t,repo:"/r",lanes:["a"]}' > "$1/meta/teams/$2/team.json"; }
mk_legacy() { mkdir -p "$1/meta"; jq -n --arg t "$2" '{team:$t,repo:"/r",lanes:["a"]}' > "$1/meta/team.json"; }

S="$W/none"; mkdir -p "$S"
check "no team on disk: the legacy flat paths (as before)" "$(resolve "$S")" "0 meta meta/team.json"

S="$W/legacy"; mk_legacy "$S" t2
check "legacy config, nothing selected: flat layout" "$(resolve "$S")" "0 meta meta/team.json"
check "legacy config, its own team selected: flat layout" "$(resolve "$S" t2)" "0 meta meta/team.json"
check "legacy config, another team selected: that team's directory" "$(resolve "$S" other)" "0 meta/teams/other meta/teams/other/team.json"
mk_named "$S" two
check "legacy config wins over named teams when nothing is selected" "$(resolve "$S")" "0 meta meta/team.json"

S="$W/one"; mk_named "$S" alpha
check "one named team, nothing selected: it is used" "$(resolve "$S")" "0 meta/teams/alpha meta/teams/alpha/team.json"

S="$W/many"; mk_named "$S" alpha; mk_named "$S" beta
check "two named teams, nothing selected: refused (rc 2)" "$(resolve "$S" | cut -c1)" "2"
check "the refusal names both teams" "$(grep -c 'alpha beta' "$W/err")" "1"
check "two named teams, one selected" "$(resolve "$S" beta)" "0 meta/teams/beta meta/teams/beta/team.json"
check "a bad team name is refused (rc 2)" "$(resolve "$S" '../x' | cut -c1)" "2"

# An empty teams/<t> (no team.json) does not hide the legacy team of that name; meta_pick_config ignores another team's AW_META_CONFIG.
S="$W/emptydir"; mk_legacy "$S" t2; mkdir -p "$S/meta/teams/t2"
check "empty teams/<t> without a config: the legacy layout still wins" "$(resolve "$S" t2)" "0 meta meta/team.json"
S="$W/pick"; mk_named "$S" foo; mk_legacy "$S" t2
picked() { # team override-file: the config path chosen, relative to the state directory
  local rc; meta_resolve "$S" "$1" 2> /dev/null; AW_META_CONFIG="$2" meta_pick_config "$1" | sed "s#^$S/##"
}
check "meta_pick_config: no override uses the team's own config" "$(picked foo "")" "meta/teams/foo/team.json"
check "meta_pick_config: an override for the same team is used" "$(picked foo "$S/meta/teams/foo/team.json")" "meta/teams/foo/team.json"
check "meta_pick_config: another team's override is ignored" "$(picked foo "$S/meta/team.json")" "meta/teams/foo/team.json"

# meta-lane.sh registers the meta role for ITS team, not for whichever team the state directory resolves to without a selection.
S="$W/lane"; mk_legacy "$S" t2; mk_named "$S" foo
mkdir -p "$S/bin" "$W/home" "$W/repo"
jq -n --arg r "$W/repo" '{team:"foo",repo:$r,metaLane:"meta",lanes:["api"]}' > "$S/meta/teams/foo/team.json"
printf '#!/usr/bin/env bash\necho "team=${AW_META_TEAM:-} args=$*" >> "%s/roles"\n' "$W" > "$S/bin/aw-meta"
printf '#!/usr/bin/env bash\nexit 0\n' > "$W/claude-noop"
chmod +x "$S/bin/aw-meta" "$W/claude-noop"
: > "$W/roles"
env -i PATH="$PATH" HOME="$W/home" AW_STATE_DIR="$S" AW_META_BIN="$S/bin/aw-meta" AW_META_CLAUDE="$W/claude-noop" AW_META_NO_SHELL=1 \
  AW_META_CONFIG="$S/meta/teams/foo/team.json" bash "$DIR/meta-lane.sh" foo meta > /dev/null 2>&1
check "meta-lane.sh: role-set runs for the launched team (AW_META_TEAM)" "$(sed -n 's/^team=\([^ ]*\) args=role-set.*/\1/p' "$W/roles" | head -1)" "foo"
check "meta-lane.sh: the lane's session id lands in that team's lane-sessions" "$([ -s "$S/meta/teams/foo/lane-sessions/meta" ] && echo yes):$([ -e "$S/meta/lane-sessions/meta" ] && echo flat)" "yes:"

# install-meta.sh and meta-launch.sh refuse to guess with several teams and none selected (before building or touching tmux sessions).
S="$W/amb"; mk_named "$S" alpha; mk_named "$S" beta; mkdir -p "$W/home"
OUT="$(env -i PATH="$PATH" HOME="$W/home" AW_STATE_DIR="$S" bash "$DIR/install-meta.sh" 2>&1)"; RC=$?
check "install-meta.sh: several teams, none selected: refused (rc 3), naming the way out" "$RC:$(grep -c 'AW_META_TEAM' <<< "$OUT")" "3:1"
if command -v tmux > /dev/null; then
  OUT="$(env -i PATH="$PATH" HOME="$W/home" AW_STATE_DIR="$S" bash "$DIR/meta-launch.sh" --dry-run 2>&1)"; RC=$?
  check "meta-launch.sh: several teams, none selected: refused (rc 2) listing them" "$RC:$(grep -c 'alpha beta' <<< "$OUT")" "2:1"
  mkdir -p "$S/meta/teams/gamma"; jq -n '{team:"other",repo:"/r",lanes:["a"]}' > "$S/meta/teams/gamma/team.json"
  OUT="$(env -i PATH="$PATH" HOME="$W/home" AW_STATE_DIR="$S" bash "$DIR/meta-launch.sh" --team gamma --dry-run 2>&1)"; RC=$?
  check "meta-launch.sh: a team.json whose team differs from its directory is refused (rc 2)" "$RC:$(grep -c "not the selected 'gamma'" <<< "$OUT")" "2:1"
fi

# The Claude hooks: AW_META_TEAM wins; else the tmux socket's name, only for a team that exists on disk.
stub() { # state: an aw-meta stub that records the team it ran with and the arguments
  mkdir -p "$1/bin"
  printf '#!/usr/bin/env bash\necho "team=${AW_META_TEAM:-} args=$*" >> "%s/calls"\nexit 0\n' "$W" > "$1/bin/aw-meta"
  chmod +x "$1/bin/aw-meta"
}
hook() { # script state tmux-var preset-team: the team the stub saw ("-" when none)
  local script="$1" state="$2" tmuxv="$3" team="$4" input
  : > "$W/calls"
  input='{"session_id":"s1","transcript_path":"/t.jsonl","tool_name":"SendMessage"}'
  printf '%s' "$input" | env -i PATH="$PATH" HOME="$W/home" AW_STATE_DIR="$state" ${tmuxv:+TMUX="$tmuxv"} ${team:+AW_META_TEAM="$team"} bash "$TOP/config/hooks/$script" > /dev/null 2>&1
  sed -n 's/^team=\([^ ]*\) .*/\1/p' "$W/calls" | head -1 | sed 's/^$/-/'
}
S="$W/hooks"; mk_named "$S" alpha; mk_named "$S" beta; stub "$S"
check "stop gate: socket of a named team selects it" "$(hook meta-stop-gate.sh "$S" /tmp/tmux-1/beta,99,0 "")" "beta"
check "stop gate: AW_META_TEAM wins over the socket" "$(hook meta-stop-gate.sh "$S" /tmp/tmux-1/beta,99,0 alpha)" "alpha"
check "stop gate: a socket that is no team selects nothing" "$(hook meta-stop-gate.sh "$S" /tmp/tmux-1/default,99,0 "")" "-"
check "stop gate: no tmux, no variable selects nothing" "$(hook meta-stop-gate.sh "$S" "" "")" "-"
check "outbox: socket of a named team selects it" "$(hook meta-outbox.sh "$S" /tmp/tmux-1/alpha,99,0 "")" "alpha"
check "outbox: a socket that is no team selects nothing" "$(hook meta-outbox.sh "$S" /tmp/tmux-1/nope,99,0 "")" "-"
S="$W/hooks-legacy"; mk_legacy "$S" t2; stub "$S"
check "stop gate: the legacy team's own socket selects it" "$(hook meta-stop-gate.sh "$S" /tmp/tmux-1/t2,99,0 "")" "t2"
check "stop gate: another socket does not select a team on a legacy layout" "$(hook meta-stop-gate.sh "$S" /tmp/tmux-1/other,99,0 "")" "-"
exit "$fail"
