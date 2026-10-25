#!/usr/bin/env bash
# aw_setup_onyx_finish <rc> <later-command>  — the last lines of setup.sh: the Onyx step's result, after everything else has run.
# 0 (Onyx is up) and 3 (skipped, its warning and the command to run later already printed) return 0; any other code is a failure
# (1 is `onyx up` failing; 2, 126, 127 and the like are the script itself aborting) and is returned with the message.
aw_setup_onyx_finish() {
  local rc="${1:-0}" later="${2:-sindri onyx up}"
  case "$rc" in
    0|3) return 0 ;;
  esac
  echo "" >&2
  echo "Onyx was not set up (exit $rc, see above). The rest of setup is done; rerun: $later   (or ./setup.sh --with-sindri)" >&2
  return "$rc"
}
