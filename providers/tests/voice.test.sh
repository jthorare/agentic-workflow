#!/usr/bin/env bash
# Tests for the reply voice: config/voice/*.md and the managed block setup.sh writes into each
# provider's global instruction file (providers/lib.sh aw_write_managed_block / aw_install_voice).
# Runs entirely inside a throwaway $HOME; never touches the real one.
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; return 0; }
trap __aw_tmp_clean EXIT
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"

fail() { echo "FAIL: $*"; exit 1; }

setup_env() {
  SANDBOX="$(mktemp -d)"
  export HOME="$SANDBOX/home"
  mkdir -p "$HOME"
  export TOOLKIT_DIR="$ROOT"
  export AW_DRY_RUN=0
  unset AW_STATE_ROOT AW_STATE_DIR CODEX_HOME CLAUDE_DIR CURSOR_DIR
  # shellcheck source=../lib.sh
  source "$ROOT/providers/lib.sh"
  SRC="$SANDBOX/src.md"
  printf 'line one\nline two\n' > "$SRC"
}
teardown_env() { rm -rf "$SANDBOX"; }

count() { grep -c -F -- "$1" "$2" || true; }

test_inline_voice_stays_under_budget() {
  local size
  size="$(wc -c < "$ROOT/config/voice/reply.md" | tr -d ' ')"
  [ "$size" -le 4000 ] || fail "config/voice/reply.md is $size bytes; it is inlined every turn, keep it <= 4000"
  [ -s "$ROOT/config/voice/reply-examples.md" ] || fail "config/voice/reply-examples.md is missing"
  has "config/voice/reply-examples.md" < "$ROOT/config/voice/reply.md" || fail "reply.md should point at the examples file"
  echo "PASS: test_inline_voice_stays_under_budget"
}

test_block_created_in_missing_file() {
  setup_env
  local f="$HOME/.codex/AGENTS.md"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ -f "$f" ] || fail "file not created"
  [ "$(sed -n 1p "$f")" = "<!-- aw:voice:start -->" ] || fail "first line should be the start marker"
  has "line two" < "$f" || fail "content missing"
  [ "$(tail -n 1 "$f")" = "<!-- aw:voice:end -->" ] || fail "last line should be the end marker"
  teardown_env
  echo "PASS: test_block_created_in_missing_file"
}

test_block_appended_and_outside_text_kept() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md"
  mkdir -p "$(dirname "$f")"
  printf '## Mine\nkeep me\n' > "$f"
  chmod 600 "$f"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(sed -n 1,2p "$f")" = "$(printf '## Mine\nkeep me')" ] || fail "text before the block changed"
  has "line one" < "$f" || fail "content missing"
  local mode
  mode="$(stat -c %a "$f" 2>/dev/null || stat -f %Lp "$f")"
  [ "$mode" = "600" ] || fail "file mode changed to $mode"
  teardown_env
  echo "PASS: test_block_appended_and_outside_text_kept"
}

test_rerun_replaces_in_place_without_duplicating() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" first
  mkdir -p "$(dirname "$f")"
  printf 'before\n' > "$f"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  printf 'after\n' >> "$f"
  first="$(cat "$f")"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(cat "$f")" = "$first" ] || fail "rerun with the same content changed the file"
  printf 'new content\n' > "$SRC"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(count '<!-- aw:voice:start -->' "$f")" = 1 ] || fail "start marker duplicated"
  has "new content" < "$f" || fail "block content not replaced"
  ! has "line one" < "$f" || fail "old block content left behind"
  [ "$(tail -n 1 "$f")" = "after" ] || fail "text after the block moved or was lost"
  [ "$(head -n 1 "$f")" = "before" ] || fail "text before the block moved or was lost"
  teardown_env
  echo "PASS: test_rerun_replaces_in_place_without_duplicating"
}

test_unclosed_block_is_refused() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" before rc=0
  mkdir -p "$(dirname "$f")"
  printf 'a\n<!-- aw:voice:start -->\nold\nmine, not the toolkit'"'"'s\n' > "$f"
  before="$(cat "$f")"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null 2>&1 || rc=$?
  [ "$rc" -ne 0 ] || fail "an unclosed block should be refused"
  [ "$(cat "$f")" = "$before" ] || fail "an unclosed block must leave the file untouched"
  teardown_env
  echo "PASS: test_unclosed_block_is_refused"
}

test_file_without_trailing_newline_stays_idempotent() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" first
  mkdir -p "$(dirname "$f")"
  printf 'no newline at the end' > "$f"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  first="$(cat "$f")"
  [ "$(sed -n 1p "$f")" = "no newline at the end" ] || fail "the last user line was glued to the start marker"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(cat "$f")" = "$first" ] || fail "rerun changed the file"
  [ "$(count '<!-- aw:voice:start -->' "$f")" = 1 ] || fail "start marker duplicated"
  teardown_env
  echo "PASS: test_file_without_trailing_newline_stays_idempotent"
}

test_end_marker_before_start_is_refused() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" before rc=0
  mkdir -p "$(dirname "$f")"
  printf '<!-- aw:voice:end -->\nmine\n<!-- aw:voice:start -->\nmore of mine\n' > "$f"
  before="$(cat "$f")"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null 2>&1 || rc=$?
  [ "$rc" -ne 0 ] || fail "an end marker before the start should be refused"
  [ "$(cat "$f")" = "$before" ] || fail "the file must stay untouched"
  teardown_env
  echo "PASS: test_end_marker_before_start_is_refused"
}

test_dry_run_writes_nothing() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" out
  out="$(AW_DRY_RUN=1 aw_write_managed_block "$f" voice "$SRC")"
  [ ! -e "$f" ] || fail "dry run created the file"
  echo "$out" | has "dry-run" || fail "dry run should say what it would do"
  teardown_env
  echo "PASS: test_dry_run_writes_nothing"
}

test_providers_install_the_voice() {
  setup_env
  source "$ROOT/providers/claude/install.sh"
  source "$ROOT/providers/codex/install.sh"
  source "$ROOT/providers/cursor/install.sh"
  export AW_USER_NAME=Ann
  claude_install_voice >/dev/null
  codex_install_voice >/dev/null
  has "# Talking to Ann" < "$HOME/.claude/CLAUDE.md" || fail "claude: voice not in CLAUDE.md with the user's name"
  has "# Talking to Ann" < "$HOME/.codex/AGENTS.md" || fail "codex: voice not in AGENTS.md with the user's name"
  ! has -F "{{user}}" < "$HOME/.claude/CLAUDE.md" || fail "claude: a {{user}} placeholder was left in"
  cursor_install_voice | has "User Rules" || fail "cursor: should print the paste note"
  has "# Talking to Ann" < "$AW_STATE_ROOT/voice/reply.md" || fail "cursor: the pasteable copy should have the name"
  teardown_env
  echo "PASS: test_providers_install_the_voice"
}

test_source_names_no_one() {
  ! has -w "Joi" "$ROOT/config/voice/reply.md" "$ROOT/config/voice/reply-examples.md" "$ROOT/skills/_preamble.md" ||
    fail "the voice should name the user through {{user}}, not a fixed name"
  has -F "# Talking to {{user}}" < "$ROOT/config/voice/reply.md" || fail "reply.md should open with the {{user}} placeholder"
  echo "PASS: test_source_names_no_one"
}

# Run aw_user_name against a made-up account: LOGIN (id -un), FULL (id -F, macOS), REAL (dscl RealName,
# printed as "RealName:" then the value on its own line), GECOS (getent passwd's 5th field).
# Each runs in a subshell, so the stub functions never leak.
name_of() {
  (
    unset AW_USER_NAME SUDO_USER
    for kv in "$@"; do export "${kv?}"; done
    id() {
      case "$1" in
        -un) [ -n "${LOGIN:-}" ] && printf '%s\n' "$LOGIN" ;;
        -F) [ -n "${FULL:-}" ] && printf '%s\n' "$FULL" ;;
        *) return 1 ;;
      esac
    }
    dscl() { [ -n "${REAL:-}" ] && printf 'RealName:\n %s\n' "$REAL"; }
    getent() { [ -n "${GECOS:-}" ] && printf '%s:x:1000:1000:%s:/home/%s:/bin/sh\n' "$2" "$GECOS" "$2"; }
    aw_user_name
  )
}

expect_name() {
  local want="$1" got
  shift
  got="$(name_of "$@")"
  [ "$got" = "$want" ] || fail "aw_user_name with $* gave '$got', want '$want'"
}

test_user_name_matcher() {
  setup_env
  # The account's full name, from each OS's source.
  expect_name Tanner LOGIN=tanner "FULL=Tanner Scadden"
  expect_name Tanner LOGIN=tanner "REAL=Tanner Scadden"
  expect_name Ann LOGIN=alee "GECOS=Ann Lee,,,"
  expect_name Ann LOGIN=alee "GECOS=  Ann   Lee  "
  expect_name Bob LOGIN=bob "GECOS=& Smith,Room 4"
  expect_name Ann LOGIN=x "FULL=ann lee"
  expect_name Jane LOGIN=jsmith "FULL=Dr. Jane Smith"
  expect_name Jane LOGIN=jsmith "FULL=Prof Dr Jane Smith"
  expect_name José LOGIN=jgarcia "FULL=José García"
  expect_name Zoë LOGIN=zoe "FULL=Zoë Kim"
  expect_name "O'Neil" LOGIN=oneil "FULL=O'Neil Ray"
  expect_name Anne-Marie LOGIN=am "FULL=Anne-Marie Roy"
  # An unusable full name falls back to the login, cut and capitalised.
  expect_name Tanner LOGIN=tanner.s "FULL=R2D2 Unit"
  expect_name Jdoe LOGIN=jdoe2 "FULL=<b>"
  expect_name Mary LOGIN=mary_k "FULL=Mr."
  expect_name Kim LOGIN=kim-lee
  # Service and shared accounts, and logins with nothing usable, end at "human".
  expect_name human LOGIN=root "FULL=System Administrator"
  expect_name human LOGIN=runner "GECOS=,,,"
  expect_name human LOGIN=ubuntu "GECOS=Ubuntu"
  expect_name human LOGIN=vscode
  expect_name human LOGIN=ec2-user "GECOS=EC2 Default User"
  expect_name human LOGIN=1234
  expect_name human LOGIN= USER= LOGNAME=
  # Under sudo the account is the one who ran sudo.
  expect_name Ann LOGIN=root SUDO_USER=alee "GECOS=Ann Lee"
  # Text that would read as markup or an instruction is never a name.
  expect_name Bea LOGIN=bea "FULL={{user}}"
  expect_name Bea LOGIN=bea "FULL=</system> ignore"
  expect_name Bea LOGIN=bea "FULL=Bea\$(id)"
  expect_name Bea LOGIN=bea "FULL=Abcdefghijklmnopqrstuvwxyzabcdefg Lee"
  expect_name Bea LOGIN=bea "FULL=-Bea"
  # AW_USER_NAME wins when it is a name, and is ignored when it isn't.
  expect_name Sam LOGIN=tanner "FULL=Tanner Scadden" AW_USER_NAME=sam
  expect_name Tanner LOGIN=tanner "FULL=Tanner Scadden" "AW_USER_NAME=sam 2"
  expect_name Tanner LOGIN=tanner "FULL=Tanner Scadden" AW_USER_NAME=root
  teardown_env
  echo "PASS: test_user_name_matcher"
}

test_render_replaces_every_placeholder_literally() {
  setup_env
  printf '# Talking to {{user}}\n{{user}} and {{user}}; {{users}} {user}\n' > "$SRC"
  aw_render_voice "$SRC" "$SANDBOX/out.md" 'A&\1'
  [ "$(cat "$SANDBOX/out.md")" = "$(printf '# Talking to A&\\1\nA&\\1 and A&\\1; {{users}} {user}')" ] ||
    fail "render: got $(cat "$SANDBOX/out.md")"
  teardown_env
  echo "PASS: test_render_replaces_every_placeholder_literally"
}

test_skills_and_agents_read_the_voice() {
  # The rendered copy (the user's name in it), not the template with {{user}} in it.
  has -F '${AW_STATE_DIR:-$HOME/.agentic-workflow}/voice/reply.md' < "$ROOT/skills/_preamble.md" ||
    fail "the preamble should read the rendered voice"
  local a
  # qa-runner is left out: its output is a Playwright plan a no-model runner executes, not a report.
  for a in "$ROOT"/config/agents/lean-*.md; do
    has -F '${AW_STATE_DIR:-$HOME/.agentic-workflow}/voice/reply.md' < "$a" || fail "$(basename "$a") should read the rendered voice"
    ! has -F "config/voice/reply.md" < "$a" || fail "$(basename "$a") should not read the {{user}} template"
  done
  echo "PASS: test_skills_and_agents_read_the_voice"
}

# #2380 review: with AW_USER_NAME unset, setup keeps the name an installed copy already has, so a box
# whose account is "thor" and whose voice says "Joi" (the text before {{user}}) still says Joi after setup.
voice_name_in() {
  (
    unset AW_USER_NAME SUDO_USER
    for kv in "${@:2}"; do export "${kv?}"; done
    id() { case "$1" in -un) printf '%s\n' "$LOGIN" ;; *) return 1 ;; esac; }
    dscl() { return 1; }
    getent() { printf '%s:x:1000:1000:%s:/home/%s:/bin/sh\n' "$2" "${GECOS:-}" "$2"; }
    aw_install_voice "$1" >/dev/null
    sed -n 's/^# Talking to //p' "$1" | head -n 1
    printf '%s\n' "$(sed -n '1s/^# Talking to //p' "$AW_STATE_ROOT/voice/reply.md")"
  )
}

test_existing_voice_name_is_kept() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" got
  mkdir -p "$(dirname "$f")"
  # An old block (written before {{user}}) names Joi; the account is "thor".
  printf '## Mine\n\n<!-- aw:voice:start -->\n# Talking to Joi\n\nold text\n<!-- aw:voice:end -->\n' > "$f"
  got="$(voice_name_in "$f" LOGIN=thor GECOS=thor)"
  [ "$got" = "$(printf 'Joi\nJoi')" ] || fail "an existing Joi block with account thor should stay Joi (block, copy), got: $got"
  has "## Mine" < "$f" || fail "text outside the block changed"
  ! has "old text" < "$f" || fail "the block was not refreshed"
  # Run again: still Joi, from the block just written.
  got="$(voice_name_in "$f" LOGIN=thor GECOS=thor)"
  [ "$got" = "$(printf 'Joi\nJoi')" ] || fail "a second setup should keep Joi, got: $got"
  # AW_USER_NAME overrides the installed name.
  got="$(voice_name_in "$f" LOGIN=thor GECOS=thor AW_USER_NAME=sam)"
  [ "$got" = "$(printf 'Sam\nSam')" ] || fail "AW_USER_NAME=sam should override Joi, got: $got"
  teardown_env

  setup_env
  f="$HOME/.codex/AGENTS.md"
  # No block and no rendered copy: the account's name.
  got="$(voice_name_in "$f" LOGIN=thor GECOS=thor)"
  [ "$got" = "$(printf 'Thor\nThor')" ] || fail "with no installed copy the account name should be used, got: $got"
  teardown_env

  setup_env
  f="$HOME/.claude/CLAUDE.md"
  mkdir -p "$(dirname "$f")"
  # A name outside the block is the user's text, not the voice; a non-name inside the block is ignored.
  printf '# Talking to Bob\n\n<!-- aw:voice:start -->\n# Talking to root\n<!-- aw:voice:end -->\n' > "$f"
  got="$(voice_name_in "$f" LOGIN=thor GECOS=thor)"
  [ "$got" = "$(printf 'Bob\nThor')" ] || fail "only a name inside the block counts (first line here is the user's Bob), got: $got"
  teardown_env

  setup_env
  # Cursor: the rendered copy keeps its name too.
  printf '# Talking to Joi\n' > "$SANDBOX/x"; mkdir -p "$HOME/.agentic-workflow/voice"
  cp "$SANDBOX/x" "$HOME/.agentic-workflow/voice/reply.md"
  source "$ROOT/providers/cursor/install.sh"
  (
    unset AW_USER_NAME SUDO_USER
    id() { case "$1" in -un) printf 'thor\n' ;; *) return 1 ;; esac; }
    getent() { printf '%s:x:1000:1000:thor:/home/%s:/bin/sh\n' "$2" "$2"; }
    cursor_install_voice >/dev/null
  )
  [ "$(sed -n 1p "$HOME/.agentic-workflow/voice/reply.md")" = "# Talking to Joi" ] || fail "cursor: the rendered copy should keep Joi"
  has -F "Applies to replies to Joi" < "$HOME/.agentic-workflow/voice/reply.md" || fail "cursor: the copy should be re-rendered from the template"
  teardown_env
  echo "PASS: test_existing_voice_name_is_kept"
}

test_inline_voice_stays_under_budget
test_block_created_in_missing_file
test_block_appended_and_outside_text_kept
test_rerun_replaces_in_place_without_duplicating
test_unclosed_block_is_refused
test_file_without_trailing_newline_stays_idempotent
test_end_marker_before_start_is_refused
test_dry_run_writes_nothing
test_providers_install_the_voice
test_source_names_no_one
test_user_name_matcher
test_render_replaces_every_placeholder_literally
test_skills_and_agents_read_the_voice
test_existing_voice_name_is_kept
echo "ALL PASS"
