#!/usr/bin/env bash
# aw:dup-script-gate — the duplicate-script gate (#4221 PR 2; contract docs/hooks/duplicate-script.md). PreToolUse on Bash
# and Write (Claude Code). Before an agent writes or runs a script that redoes a helper the command catalogs already have
# (scripts/aw-commands.sh: the repo's .agentic-workflow/commands.json and the toolkit's catalog), it points at the helper with
# its filled invocation. Policy duplicate-script/1 ships advisory: the suggestion goes out as additionalContext and the call
# goes on. In block mode an unambiguous match is denied (exit 2). `# aw-dup-ok: <reason>` lets one through and is recorded.
# The hot path is pure bash on the raw payload: a call that writes no script and runs no polling loop exits here, with no
# jq, no git and no file read. Everything after the pre-filter is config/lib/dup-script.sh. Fails open. Bash 3.2.
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
IFS= read -r -d '' INPUT || true
RE_TOOL='"tool_name"[[:space:]]*:[[:space:]]*"(Bash|Write)"'
[[ $INPUT =~ $RE_TOOL ]] || exit 0
DS_TOOL="${BASH_REMATCH[1]}"
if [ "$DS_TOOL" = Bash ]; then
  # A heredoc, a polling loop, or output sent to a script file. Anything else is no candidate.
  RE_LOOP='(while|until).*sleep|sleep.*(while|until)'
  RE_TOFILE='(>|tee)[^"]*\.(sh|bash|py)([^A-Za-z0-9]|$)'
  [[ $INPUT == *'<<'* ]] || [[ $INPUT =~ $RE_LOOP ]] || [[ $INPUT =~ $RE_TOFILE ]] || exit 0
else
  # A script file: a shebang, or a script extension.
  RE_SHEBANG='"content"[[:space:]]*:[[:space:]]*"#!'
  RE_EXT='"file_path"[[:space:]]*:[[:space:]]*"[^"]*\.(sh|bash|py|mjs|js)"'
  [[ $INPUT =~ $RE_SHEBANG ]] || [[ $INPUT =~ $RE_EXT ]] || exit 0
fi
# ---- only a candidate gets past this line ----
HOOK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for lib in "$HOOK_DIR/lib/dup-script.sh" "$HOOK_DIR/../lib/dup-script.sh"; do
  [ -f "$lib" ] && { . "$lib"; break; }
done
command -v ds_main > /dev/null 2>&1 || exit 0
ds_main "$INPUT"
exit $?
