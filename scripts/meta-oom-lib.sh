#!/usr/bin/env bash
# Sourced by scripts/install-meta.sh and scripts/meta-launch.sh: systemd's DefaultOOMPolicy for the user manager (issue #2931).
# With the default `stop`, one OOM kill inside a lane's tmux scope stops the whole scope, agent included; `continue` kills only the
# process the kernel picked. Linux with systemctl only: elsewhere every function prints nothing.
#
#   meta_oom_policy              the user manager's DefaultOOMPolicy (stop, kill, continue), or nothing when it can't be read
#   meta_oom_dropin_path         where the fix is written: ${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user.conf.d/10-oom-continue.conf
#   meta_oom_warn_line <prefix>  one warning line starting with <prefix> when the policy is known and is not continue, else nothing

meta_oom_policy() {
  [ "$(uname 2> /dev/null)" = Linux ] || return 0
  command -v systemctl > /dev/null 2>&1 || return 0
  local out
  if command -v timeout > /dev/null 2>&1; then   # a wedged user bus must not hang an install or a launch
    out="$(timeout 10 systemctl --user show -p DefaultOOMPolicy --value 2> /dev/null)" || return 0
  else
    out="$(systemctl --user show -p DefaultOOMPolicy --value 2> /dev/null)" || return 0
  fi
  printf '%s\n' "$out" | head -n 1
}

meta_oom_dropin_path() { printf '%s\n' "${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user.conf.d/10-oom-continue.conf"; }

meta_oom_warn_line() {
  local v
  v="$(meta_oom_policy)"
  case "$v" in
    '' | continue) return 0 ;;
  esac
  printf '%s WARNING systemd'"'"'s DefaultOOMPolicy is %s: one OOM kill in a pane stops the whole pane, agent included. Fix: scripts/install-meta.sh writes %s (or write [Manager] DefaultOOMPolicy=continue there), then systemctl --user daemon-reload\n' \
    "$1" "$v" "$(meta_oom_dropin_path)"
}
