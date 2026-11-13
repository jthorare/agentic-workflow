#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; return 0; }
trap __aw_tmp_clean EXIT
. "$(dirname "${BASH_SOURCE[0]}")/../../../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REAL_DOCTOR="$DIR/../../doctor.sh"

# Every test runs a copy of doctor.sh in its own skill layout
# (<skill>/scripts/doctor.sh), so a deployment.env committed next to the real
# SKILL.md (a fork's) never leaks into these tests.
new_skill() {
  local skill; skill="$(mktemp -d)"
  mkdir -p "$skill/scripts"
  cp "$REAL_DOCTOR" "$skill/scripts/doctor.sh"
  echo "$skill"
}

# Fake curl/docker/nc. docker and nc append their argv to <bin>/calls.
setup_fake_bins() {
  local bin_dir="$1" login_html="$2" pg_ready="$3" port_open="$4"
  mkdir -p "$bin_dir"
  cat > "$bin_dir/curl" <<EOF
#!/usr/bin/env bash
echo "$login_html"
EOF
  cat > "$bin_dir/docker" <<EOF
#!/usr/bin/env bash
echo "docker \$*" >> "$bin_dir/calls"
[ "$pg_ready" = "1" ] && exit 0 || exit 1
EOF
  cat > "$bin_dir/nc" <<EOF
#!/usr/bin/env bash
echo "nc \$*" >> "$bin_dir/calls"
[ "$port_open" = "1" ] && exit 0 || exit 1
EOF
  chmod +x "$bin_dir/curl" "$bin_dir/docker" "$bin_dir/nc"
  : > "$bin_dir/calls"
}

# run_doctor <skill> <bin> [VAR=value ...] — a clean environment: no inherited UI_EVIDENCE_* settings.
run_doctor() {
  local skill="$1" bin_dir="$2"; shift 2
  env -i HOME="$HOME" PATH="$bin_dir:/usr/bin:/bin" "$@" bash "$skill/scripts/doctor.sh" 2>&1
}

test_healthy_stack_with_only_a_title_exits_0_and_probes_nothing_unconfigured() {
  local skill bin_dir; skill="$(new_skill)"; bin_dir="$(mktemp -d)"
  setup_fake_bins "$bin_dir" "<title>TestApp</title>" 0 0
  run_doctor "$skill" "$bin_dir" UI_EVIDENCE_APP_TITLE="TestApp" > /dev/null || { echo "FAIL: expected exit 0 with only the title configured"; exit 1; }
  [ ! -s "$bin_dir/calls" ] || { echo "FAIL: expected no docker/nc probe without a configured container or port, got: $(cat "$bin_dir/calls")"; exit 1; }
  echo "PASS: test_healthy_stack_with_only_a_title_exits_0_and_probes_nothing_unconfigured"
}

test_foreign_process_on_3000_is_refused_not_double_driven() {
  local skill bin_dir out; skill="$(new_skill)"; bin_dir="$(mktemp -d)"
  setup_fake_bins "$bin_dir" "<html>some other app</html>" 1 1
  if out="$(run_doctor "$skill" "$bin_dir" UI_EVIDENCE_APP_TITLE="TestApp")"; then
    echo "FAIL: expected a non-zero exit when :3000 isn't this app (RF-1)"; exit 1
  fi
  echo "$out" | has -i "not.*this app\|foreign" || { echo "FAIL: expected a clear refusal reason, got: $out"; exit 1; }
  echo "PASS: test_foreign_process_on_3000_is_refused_not_double_driven"
}

test_configured_postgres_not_ready_fails_naming_the_container() {
  local skill bin_dir out; skill="$(new_skill)"; bin_dir="$(mktemp -d)"
  setup_fake_bins "$bin_dir" "<title>TestApp</title>" 0 1
  if out="$(run_doctor "$skill" "$bin_dir" UI_EVIDENCE_APP_TITLE="TestApp" UI_EVIDENCE_DB_CONTAINER="db_1")"; then echo "FAIL: expected non-zero when postgres isn't ready"; exit 1; fi
  echo "$out" | has "postgres in container db_1" || { echo "FAIL: expected postgres and the container named in the failure, got: $out"; exit 1; }
  echo "PASS: test_configured_postgres_not_ready_fails_naming_the_container"
}

test_docker_invoked_exactly_once_with_the_configured_container_and_user() {
  local skill bin_dir; skill="$(new_skill)"; bin_dir="$(mktemp -d)"
  setup_fake_bins "$bin_dir" "<title>TestApp</title>" 1 1
  run_doctor "$skill" "$bin_dir" UI_EVIDENCE_APP_TITLE="TestApp" UI_EVIDENCE_DB_CONTAINER="db_1" UI_EVIDENCE_DB_USER="app" > /dev/null
  local calls; calls="$(grep '^docker' "$bin_dir/calls")"
  [ "$calls" = "docker exec db_1 pg_isready -U app" ] || { echo "FAIL: expected one pg_isready in db_1 as app, got: $calls"; exit 1; }
  echo "PASS: test_docker_invoked_exactly_once_with_the_configured_container_and_user"
}

test_configured_api_port_not_open_fails_naming_the_port() {
  local skill bin_dir out; skill="$(new_skill)"; bin_dir="$(mktemp -d)"
  setup_fake_bins "$bin_dir" "<title>TestApp</title>" 1 0
  if out="$(run_doctor "$skill" "$bin_dir" UI_EVIDENCE_APP_TITLE="TestApp" UI_EVIDENCE_API_PORT="5100")"; then echo "FAIL: expected non-zero when the API port isn't open"; exit 1; fi  # test-ports: ok: nc is a stub, nothing binds the port
  echo "$out" | has "api-server (:5100)" || { echo "FAIL: expected the api port named in the failure, got: $out"; exit 1; }
  grep -q "^nc -z localhost 5100$" "$bin_dir/calls" || { echo "FAIL: expected nc on the configured port, got: $(cat "$bin_dir/calls")"; exit 1; }
  echo "PASS: test_configured_api_port_not_open_fails_naming_the_port"
}

test_missing_app_title_config_fails_with_reason() {
  local skill bin_dir out; skill="$(new_skill)"; bin_dir="$(mktemp -d)"
  setup_fake_bins "$bin_dir" "<title>TestApp</title>" 1 1
  if out="$(run_doctor "$skill" "$bin_dir" UI_EVIDENCE_CONFIG="/nonexistent")"; then
    echo "FAIL: expected non-zero when UI_EVIDENCE_APP_TITLE isn't configured"; exit 1
  fi
  echo "$out" | has -i "UI_EVIDENCE_APP_TITLE" || { echo "FAIL: expected the missing config var named in the failure, got: $out"; exit 1; }
  echo "PASS: test_missing_app_title_config_fails_with_reason"
}

test_deployment_env_supplies_settings_and_local_file_and_environment_win() {
  local skill bin_dir out; skill="$(new_skill)"; bin_dir="$(mktemp -d)"
  setup_fake_bins "$bin_dir" "<title>Local App</title>" 1 1
  cat > "$skill/deployment.env" <<'EOF'
# committed deployment defaults
UI_EVIDENCE_APP_TITLE="Deployed App"
export UI_EVIDENCE_DB_CONTAINER=deployed_db
# test-ports: ok: an env value in a fixture file, nc is a stub and nothing binds it
UI_EVIDENCE_API_PORT=4100
EOF
  printf 'UI_EVIDENCE_APP_TITLE='"'"'Local App'"'"'\n' > "$skill/.ui-evidence.local.env"
  out="$(run_doctor "$skill" "$bin_dir" UI_EVIDENCE_API_PORT="6000")" || { echo "FAIL: expected the local title to win over deployment.env, got: $out"; exit 1; }  # test-ports: ok: nc is a stub, nothing binds the port
  grep -q "^docker exec deployed_db pg_isready -U postgres$" "$bin_dir/calls" || { echo "FAIL: expected deployment.env's container, got: $(cat "$bin_dir/calls")"; exit 1; }
  grep -q "^nc -z localhost 6000$" "$bin_dir/calls" || { echo "FAIL: expected the environment's port to win, got: $(cat "$bin_dir/calls")"; exit 1; }
  echo "PASS: test_deployment_env_supplies_settings_and_local_file_and_environment_win"
}

test_settings_files_are_parsed_never_executed() {
  local skill bin_dir marker; skill="$(new_skill)"; bin_dir="$(mktemp -d)"; marker="$(mktemp -d)/ran"
  setup_fake_bins "$bin_dir" "<title>TestApp</title>" 1 1
  printf 'touch %s\nUI_EVIDENCE_APP_TITLE=$(touch %s)\n' "$marker" "$marker" > "$skill/deployment.env"
  run_doctor "$skill" "$bin_dir" > /dev/null || true
  [ ! -e "$marker" ] || { echo "FAIL: a settings file line was executed"; exit 1; }
  echo "PASS: test_settings_files_are_parsed_never_executed"
}

# Worktree mode: a fake worktree with a slot file, fake psql/lsof answers.
setup_worktree_fakes() {
  local bin_dir="$1" served_from="$2" db_exists="$3"
  setup_fake_bins "$bin_dir" "<title>TestApp</title>" 1 1
  cat > "$bin_dir/docker" <<EOF
#!/usr/bin/env bash
case "\$*" in *pg_database*) [ "$db_exists" = "1" ] && echo 1 ;; esac
exit 0
EOF
  cat > "$bin_dir/curl" <<EOF
#!/usr/bin/env bash
echo "\$*" >> "$bin_dir/curl-calls"
echo "<title>TestApp</title>"
EOF
  cat > "$bin_dir/lsof" <<EOF
#!/usr/bin/env bash
case "\$*" in *-sTCP:LISTEN*) echo 4242 ;; *) echo "p4242"; echo "n$served_from" ;; esac
EOF
  chmod +x "$bin_dir/docker" "$bin_dir/curl" "$bin_dir/lsof"
}

# The slot settings a deployment would put in its deployment.env.
SLOT_SETTINGS=(UI_EVIDENCE_SLOT_FILE=.stack-slot UI_EVIDENCE_SLOT_KEY=STACK_SLOT UI_EVIDENCE_SLOT_COUNT=15 UI_EVIDENCE_SLOT_PORT_STEP=10 "UI_EVIDENCE_SLOT_DATABASE=app_{slot}")

# run_worktree_doctor <bin> <worktree> [VAR=value ...]: a slot run with a configured database container and API port.
run_worktree_doctor() {
  local bin_dir="$1" wt="$2"; shift 2
  run_doctor "$(new_skill)" "$bin_dir" UI_EVIDENCE_APP_TITLE="TestApp" UI_EVIDENCE_DB_CONTAINER=db UI_EVIDENCE_API_PORT=4100 UI_EVIDENCE_WORKTREE="$wt" "${SLOT_SETTINGS[@]}" "$@"  # test-ports: ok: nc is a stub, nothing binds the port
}

test_worktree_slot_targets_its_own_ports_and_prints_host() {
  local bin_dir wt out; bin_dir="$(mktemp -d)"; wt="$(mktemp -d)"
  echo "STACK_SLOT=2" > "$wt/.stack-slot"
  setup_worktree_fakes "$bin_dir" "$wt/apps/web" 1
  out="$(run_worktree_doctor "$bin_dir" "$wt")" || { echo "FAIL: expected a healthy slot, got: $out"; exit 1; }
  has "localhost:3020/login" "$bin_dir/curl-calls" || { echo "FAIL: expected the slot's web port probed"; exit 1; }
  has "^nc -z localhost 4120$" "$bin_dir/calls" || { echo "FAIL: expected the slot's API port (configured + 10N) probed, got: $(cat "$bin_dir/calls")"; exit 1; }
  echo "$out" | has -- "--host http://localhost:3020" || { echo "FAIL: expected the runner host printed, got: $out"; exit 1; }
  echo "$out" | has "app_2" || { echo "FAIL: expected the slot database printed, got: $out"; exit 1; }
  echo "PASS: test_worktree_slot_targets_its_own_ports_and_prints_host"
}

test_worktree_slot_served_by_another_checkout_is_refused() {
  local bin_dir wt out; bin_dir="$(mktemp -d)"; wt="$(mktemp -d)"
  echo "STACK_SLOT=2" > "$wt/.stack-slot"
  setup_worktree_fakes "$bin_dir" "/somewhere/else/apps/web" 1
  if out="$(run_worktree_doctor "$bin_dir" "$wt")"; then
    echo "FAIL: expected a refusal when the slot is served from another checkout"; exit 1
  fi
  echo "$out" | has -i "foreign" || { echo "FAIL: expected a foreign-stack reason, got: $out"; exit 1; }
  echo "PASS: test_worktree_slot_served_by_another_checkout_is_refused"
}

test_worktree_slot_without_database_fails_with_reason() {
  local bin_dir wt out; bin_dir="$(mktemp -d)"; wt="$(mktemp -d)"
  echo "STACK_SLOT=2" > "$wt/.stack-slot"
  setup_worktree_fakes "$bin_dir" "$wt/apps/web" 0
  if out="$(run_worktree_doctor "$bin_dir" "$wt")"; then
    echo "FAIL: expected non-zero when app_2 does not exist"; exit 1
  fi
  echo "$out" | has "app_2" || { echo "FAIL: expected the missing database named, got: $out"; exit 1; }
  echo "PASS: test_worktree_slot_without_database_fails_with_reason"
}

test_worktree_without_claimed_slot_fails_with_reason() {
  local bin_dir wt out; bin_dir="$(mktemp -d)"; wt="$(mktemp -d)"
  setup_worktree_fakes "$bin_dir" "$wt" 1
  if out="$(run_worktree_doctor "$bin_dir" "$wt")"; then
    echo "FAIL: expected non-zero for a worktree with no slot file"; exit 1
  fi
  echo "$out" | has "claim a stack slot" || { echo "FAIL: expected the claim step named, got: $out"; exit 1; }
  echo "PASS: test_worktree_without_claimed_slot_fails_with_reason"
}

test_worktree_slot_follows_the_configured_step_without_a_database_check() {
  local bin_dir wt out; bin_dir="$(mktemp -d)"; wt="$(mktemp -d)"
  echo "STACK_SLOT=3" > "$wt/.stack-slot"
  setup_worktree_fakes "$bin_dir" "$wt" 0
  out="$(run_worktree_doctor "$bin_dir" "$wt" UI_EVIDENCE_SLOT_PORT_STEP=100 UI_EVIDENCE_SLOT_DATABASE=)" || { echo "FAIL: expected a healthy slot with no database configured, got: $out"; exit 1; }
  has "localhost:3300/login" "$bin_dir/curl-calls" || { echo "FAIL: expected web port 3000 + 100 * 3 probed"; exit 1; }
  if echo "$out" | has "database:"; then echo "FAIL: expected no database line without UI_EVIDENCE_SLOT_DATABASE, got: $out"; exit 1; fi
  echo "PASS: test_worktree_slot_follows_the_configured_step_without_a_database_check"
}

test_worktree_mode_needs_configured_slots() {
  local bin_dir wt out; bin_dir="$(mktemp -d)"; wt="$(mktemp -d)"
  setup_worktree_fakes "$bin_dir" "$wt" 1
  if out="$(run_doctor "$(new_skill)" "$bin_dir" UI_EVIDENCE_APP_TITLE="TestApp" UI_EVIDENCE_WORKTREE="$wt")"; then
    echo "FAIL: expected non-zero for UI_EVIDENCE_WORKTREE with no slots configured"; exit 1
  fi
  echo "$out" | has "no worktree slots are configured" || { echo "FAIL: expected the missing slot settings named, got: $out"; exit 1; }
  if out="$(run_worktree_doctor "$bin_dir" "$wt" UI_EVIDENCE_SLOT_KEY=)"; then echo "FAIL: expected non-zero with UI_EVIDENCE_SLOT_KEY unset"; exit 1; fi
  echo "$out" | has "UI_EVIDENCE_SLOT_KEY must be a variable name" || { echo "FAIL: expected the missing key named, got: $out"; exit 1; }
  echo "STACK_SLOT=2" > "$wt/.stack-slot"
  if out="$(run_worktree_doctor "$bin_dir" "$wt" "UI_EVIDENCE_SLOT_DATABASE=x'; drop{slot}")"; then echo "FAIL: expected a refused database name"; exit 1; fi
  echo "$out" | has "UI_EVIDENCE_SLOT_DATABASE must name" || { echo "FAIL: expected the database name refused, got: $out"; exit 1; }
  echo "PASS: test_worktree_mode_needs_configured_slots"
}

test_healthy_stack_with_only_a_title_exits_0_and_probes_nothing_unconfigured
test_foreign_process_on_3000_is_refused_not_double_driven
test_configured_postgres_not_ready_fails_naming_the_container
test_docker_invoked_exactly_once_with_the_configured_container_and_user
test_configured_api_port_not_open_fails_naming_the_port
test_missing_app_title_config_fails_with_reason
test_deployment_env_supplies_settings_and_local_file_and_environment_win
test_settings_files_are_parsed_never_executed
test_worktree_slot_targets_its_own_ports_and_prints_host
test_worktree_slot_served_by_another_checkout_is_refused
test_worktree_slot_without_database_fails_with_reason
test_worktree_without_claimed_slot_fails_with_reason
test_worktree_slot_follows_the_configured_step_without_a_database_check
test_worktree_mode_needs_configured_slots
echo "All doctor tests passed."
