#!/usr/bin/env bash
# aw:kill-tree — kill a process and everything under it, for the HOOK-WATCHDOG hooks (done-gate, prompt-sort,
# sindri-tasks; #3778). Sourced, never executed. bash 3.2 compatible. Prints nothing and never fails.
#
#   _aw_kill_tree <pid>   by pid only (`pgrep -P`, never a name match). Each process is frozen (STOP) before its
#                         children are listed, so it can't fork again or print "Terminated"; it dies before its
#                         children, so the script (the top) dies first and the hook returns at once, and its children
#                         are then killed by the pids listed.
#
# Each hook keeps the definition line below as its fallback when this file is missing (an older install), so the
# line must stay one line: config/lib/tests/kill-tree.test.sh checks that every hook's copy matches it.
_aw_kill_tree() { local _c _k; kill -STOP "$1" 2> /dev/null; _k="$(pgrep -P "$1" 2> /dev/null)"; kill -9 "$1" 2> /dev/null; for _c in $_k; do _aw_kill_tree "$_c"; done; return 0; }
