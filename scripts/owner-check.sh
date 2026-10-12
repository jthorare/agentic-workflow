#!/usr/bin/env bash
# owner-check.sh <stage> up|down — sets up and tears down the scratch Sindri instance an owner check runs against
# (context spec, "Work data stays with the owner": the owner-check.md outline, steps 1–4 and 7). The owner runs it,
# in the work repo, after a stage's PR is ready; stage owners never run it against a real home.
#
#   scripts/owner-check.sh <stage> up [--seed] [--services a,b] [--labeling] [--session] [--repo NAME]
#   scripts/owner-check.sh <stage> down
#
# up   1. Build: the PR head's src/Sindri/aot-smoke/run.sh (S1), then copies its whole output directory
#         (artifacts/aot-sindri-smoke/bin/: the binary and its SQLite library) into the scratch dir
#         $AW_STATE_DIR/sindri-ownercheck-<stage>/bin.
#      2. Home: AW_SINDRI_HOME=$AW_STATE_DIR/sindri-app-ownercheck-<stage>, deleted first and created fresh (0700),
#         with AW_SINDRI_PORT=7480, so it never takes the real instance's home or port.
#      3. Seed (--seed): the merged build ($SINDRI_MAIN) exports the map from the real home (AW_SINDRI_HOME,
#         else $AW_STATE_DIR/sindri-app; AW_SINDRI_PORT unset) into <scratch home>/seed.json, so teardown deletes
#         it. Never copies context.db.
#      4. Profile: the real profile (AW_SINDRI_PROFILE, else <real home>/profile/profile.yaml) copied in and edited:
#         the engine's services (context-docs-refresh, context-sync, context-rebuild, context-purge) kept only when
#         --services names them; context.labeling.enabled true with --labeling, else false where set;
#         context.sessions.enabled true with --session, else false where set. The edits are tried on a temp
#         copy before step 1, so a profile they can't edit stops before anything is built. Then starts the instance
#         (sindri --headless), and stops for the terminal steps: `sindri profile approve <hash>` (you type the
#         hash's first 6 characters) and, with --seed, `sindri context map import <seed>`.
#      With --session it writes the MCP config for step 5 (only `sindri-ownercheck`, on this port, with the
#      X-Sindri-Repo header: --repo, else the directory name of the current repo's main checkout, also from a
#      linked worktree) and prints the claude command.
# down 7. Teardown: `sindri stop`, then stops anything still listening on 7480 (an aborted check), and deletes the
#         MCP config, the scratch home and the build.
#
# One owner check at a time: up refuses while something listens on 7480. Environment: AW_STATE_DIR (default
# ~/.agentic-workflow), AW_SINDRI_HOME / AW_SINDRI_PROFILE (the real home and profile, read only), SINDRI_MAIN (the
# merged build's command, for --seed). OWNER_CHECK_PORT replaces 7480 in scripts/tests/owner-check.test.sh only.
# Exit codes: 0 ok; 1 a step failed (the message names it); 2 usage or a refused setup.
set -uo pipefail

ENGINE_SERVICES="context-docs-refresh context-sync context-rebuild context-purge"
die()   { echo "owner-check: $*" >&2; exit 2; }   # usage or a refused setup
broke() { echo "owner-check: $*" >&2; exit 1; }   # a step failed
usage() { echo "usage: scripts/owner-check.sh <stage> up [--seed] [--services a,b] [--labeling] [--session] [--repo NAME]" >&2
          echo "       scripts/owner-check.sh <stage> down" >&2; exit 2; }

[ "$#" -ge 2 ] || usage
given="$1"; stage="$(printf '%s' "$1" | tr 'A-Z' 'a-z')"; action="$2"; shift 2
case "$stage" in ''|-*|*[!a-z0-9-]*) die "stage must be a plain stage id such as s-3 (got '$given')" ;; esac
case "$action" in up|down) ;; *) usage ;; esac

seed=0 labeling=0 session=0 services="" repo=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --seed) seed=1 ;;
    --labeling) labeling=1 ;;
    --session) session=1 ;;
    --services) [ "$#" -ge 2 ] || usage; services="${2//,/ }"; shift ;;
    --repo) [ "$#" -ge 2 ] || usage; repo="$2"; shift ;;
    *) echo "owner-check: unknown option $1" >&2; usage ;;
  esac
  shift
done
[ "$action" = up ] || [ "$seed$labeling$session$services$repo" = 000 ] || usage
for s in $services; do
  case " $ENGINE_SERVICES " in *" $s "*) ;; *) die "--services names the engine's services only ($ENGINE_SERVICES), not '$s'" ;; esac
done

root="$(cd "$(dirname "$0")/.." && pwd)"
state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
real_home="${AW_SINDRI_HOME:-$state/sindri-app}"
real_profile="${AW_SINDRI_PROFILE:-$real_home/profile/profile.yaml}"
port="${OWNER_CHECK_PORT:-7480}"
home="$state/sindri-app-ownercheck-$stage"
scratch="$state/sindri-ownercheck-$stage"
bin="$scratch/bin/sindri"
# up reads the real home; AW_SINDRI_HOME set to the owner-check home (the export line up prints) would make it read the
# scratch one. down never reads the real home, so it runs from that shell too.
if [ "$action" = up ]; then
  case "$real_home/" in */sindri-app-ownercheck-*/*) die "AW_SINDRI_HOME points at an owner-check home ($real_home); unset it (the real home is read, never used)" ;; esac
  case "$real_profile" in */sindri-app-ownercheck-*/*) die "AW_SINDRI_PROFILE points into an owner-check home ($real_profile); unset it" ;; esac
fi

bounded() { perl -e 'alarm shift; exec @ARGV or die "exec $ARGV[0]: $!"' 30 "$@"; }   # SIGALRM after 30 s
listening() { (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; }
listeners() { if command -v lsof >/dev/null; then lsof -nP -t -iTCP:"$port" -sTCP:LISTEN 2>/dev/null; else fuser -n tcp "$port" 2>/dev/null; fi; }
# Every call to the PR's build: the scratch home and port, and never AW_SINDRI_PROFILE (it would point the scratch
# instance at the real profile instead of the edited copy in the scratch home).
in_scratch=(env -u AW_SINDRI_PROFILE AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port")

# yaml_set <file> <a> <b> <c> <value> <insert> — sets a.b.c in a block-style YAML file; with insert=0 only where the
# key exists. Exit 3 when it can't edit in place (a flow mapping or a scalar on the way).
yaml_set() {
  awk -v A="$2" -v B="$3" -v C="$4" -v V="$5" -v INS="$6" '
    function ind(s) { match(s, /[^ ]/); return RSTART - 1 }
    function key(s, k) { return s ~ ("^ *" k ":( |$)") }
    function rest(s) { sub(/^ *[^:]*:/, "", s); sub(/(^| )#.*$/, "", s); gsub(/ /, "", s); return s }
    function pad(n) { return sprintf("%" n "s", "") }
    function put_c(i) { if (!done && INS) print pad(i) C ": " V; done = 1 }
    function put_bc(i) { if (!done && INS) { print pad(i) B ":"; print pad(i * 2) C ": " V }; done = 1 }
    { line = $0 }
    line ~ /^ *(#.*)?$/ { print; next }
    st == 0 && ind(line) == 0 && key(line, A) { if (rest(line) != "") exit 3; st = 1; print; next }
    st == 1 && ind(line) == 0 { put_bc(ia ? ia : 2); st = 3 }
    st == 1 { if (!ia) ia = ind(line)
              if (ind(line) == ia && key(line, B)) { if (rest(line) != "") exit 3; st = 2; print; next } }
    st == 2 && ind(line) <= ia { put_c(ib ? ib : ia * 2); st = ind(line) == 0 ? 3 : 1 }
    st == 2 { if (!ib) ib = ind(line)
              if (ind(line) == ib && key(line, C)) { print pad(ib) C ": " V; done = 1; next } }
    { print }
    END { if (st == 0 && INS) { print A ":"; print "  " B ":"; print "    " C ": " V }
          if (st == 1) put_bc(ia ? ia : 2)
          if (st == 2) put_c(ib ? ib : ia * 2) }' "$1" >"$1.new" || { rm -f "$1.new"; return 3; }
  mv "$1.new" "$1"
}

# drop_services <file> <keep...> — removes the engine services not kept from the top-level services: block. Exit 3
# when services is a flow mapping that names an engine service, 4 when a kept service isn't there.
drop_services() {
  local f="$1"; shift
  awk -v ENGINE="$ENGINE_SERVICES" -v KEEP="$*" '
    BEGIN { n = split(ENGINE, e, " "); for (i = 1; i <= n; i++) eng[e[i]] = 1; m = split(KEEP, k, " "); for (i = 1; i <= m; i++) keep[k[i]] = 1 }
    function ind(s) { match(s, /[^ ]/); return RSTART - 1 }
    /^ *(#.*)?$/ { if (!skip) print; next }
    ind($0) == 0 { inside = ($0 ~ /^services:/); skip = 0
                   if (inside && $0 !~ /^services: *(#.*)?$/) { for (s in eng) if (index($0, s)) { flow = 1; exit 3 } }
                   print; next }
    inside && !ci { ci = ind($0) }
    inside && ind($0) == ci { name = $0; sub(/^ */, "", name); sub(/:.*/, "", name)
                              seen[name] = 1; skip = (name in eng) && !(name in keep) }
    !skip { print }
    END { if (flow) exit 3; for (s in keep) if (!(s in seen)) { print s > "/dev/stderr"; exit 4 } }' "$f" >"$f.new" || { local rc=$?; rm -f "$f.new"; return "$rc"; }
  mv "$f.new" "$f"
}

# edit_profile <file> — step 4's edits, refusing (exit 2) a profile they can't edit in place.
edit_profile() {
  local p="$1" rc missing
  missing="$(drop_services "$p" $services 2>&1 >/dev/null)"; rc=$?
  [ "$rc" = 0 ] || [ "$rc" != 3 ] || die "step 4: services: is a flow mapping; rewrite it in block style in the real profile, then rerun"
  [ "$rc" = 0 ] || die "step 4: the check names $(echo $missing) but the real profile's services: has no such entry"
  yaml_set "$p" context sessions enabled "$([ "$session" = 1 ] && echo true || echo false)" "$session" \
    || die "step 4: can't set context.sessions.enabled in place (a flow mapping); rewrite context: in block style in the real profile"
  yaml_set "$p" context labeling enabled "$([ "$labeling" = 1 ] && echo true || echo false)" "$labeling" \
    || die "step 4: can't set context.labeling.enabled in place (a flow mapping); rewrite context: in block style in the real profile"
}

down() {
  [ ! -x "$bin" ] || bounded "${in_scratch[@]}" "$bin" stop >/dev/null 2>&1 || true
  local pids i
  pids="$(listeners)"
  if [ -n "$pids" ]; then
    kill $pids 2>/dev/null || true
    for i in $(seq 1 50); do listening || break; sleep 0.1; done
    ! listening || { kill -9 $(listeners) 2>/dev/null || true; sleep 0.2; }
  fi
  ! listening || broke "something still listens on port $port; stop it by hand"
  case "$home" in */sindri-app-ownercheck-"$stage") rm -rf "$home" ;; *) die "refusing to delete $home" ;; esac
  case "$scratch" in */sindri-ownercheck-"$stage") rm -rf "$scratch" ;; *) die "refusing to delete $scratch" ;; esac
  echo "owner-check $stage down: stopped port $port; deleted $home and $scratch (build, MCP config)"
  echo "In a shell that ran up's export line: unset SINDRI AW_SINDRI_HOME AW_SINDRI_PORT"
}

up() {
  ! listening || die "port $port is in use: another owner check runs (one at a time), or an aborted one left it; run scripts/owner-check.sh <stage> down first"
  local main=()
  if [ "$seed" = 1 ]; then
    [ -n "${SINDRI_MAIN:-}" ] || die "--seed needs SINDRI_MAIN, the merged build's sindri command, to export the real map"
    read -ra main <<<"$SINDRI_MAIN"
  fi
  if [ "$session" = 1 ] && [ -z "$repo" ]; then
    # The main checkout's directory, also from a linked worktree (whose own directory name isn't the repo's).
    repo="$(basename "$(dirname "$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null)")" 2>/dev/null)"
    [ -n "$repo" ] && [ "$repo" != . ] || die "--session needs --repo NAME outside a git repo (the X-Sindri-Repo header)"
  fi
  [ -f "$real_profile" ] && [ -r "$real_profile" ] || die "the real profile isn't readable: $real_profile"
  local smoke="$root/src/Sindri/aot-smoke/run.sh"
  [ -x "$smoke" ] || die "step 1 needs $smoke (S1's AOT smoke, #163) on the PR head"
  # Step 4's profile edits, tried first on a temp copy: a profile they can't edit stops here, before the build and export.
  edited="$(mktemp "${TMPDIR:-/tmp}/owner-check-profile.XXXXXX")" || die "can't make a temp file"
  trap 'rm -f "$edited" "$edited.new"' EXIT
  # CRLF stripped: the edits match keys line by line, and a key ending in \r would slip past them.
  tr -d '\r' <"$real_profile" >"$edited" && chmod 600 "$edited" || die "can't copy the real profile: $real_profile"
  edit_profile "$edited"

  echo "== 1. build (src/Sindri/aot-smoke/run.sh)"
  env -u AW_SINDRI_PROFILE -u AW_SINDRI_HOME -u AW_SINDRI_PORT "$smoke" || broke "step 1: the AOT build or its smoke failed"
  case "$scratch" in */sindri-ownercheck-"$stage") rm -rf "$scratch" ;; *) die "refusing to delete $scratch" ;; esac
  mkdir -p "$scratch" && chmod 700 "$scratch" && cp -R "$root/artifacts/aot-sindri-smoke/bin" "$scratch/bin" \
    || broke "step 1: can't copy the build into $scratch"
  [ -x "$bin" ] || broke "step 1: no sindri binary in the build output"

  echo "== 2. home $home (port $port)"
  case "$home" in */sindri-app-ownercheck-"$stage") rm -rf "$home" ;; *) die "refusing to delete $home" ;; esac
  (umask 077 && mkdir -p "$home/profile") || broke "step 2: can't create $home"
  chmod 700 "$home"

  if [ "$seed" = 1 ]; then
    echo "== 3. seed export from the real home (merged build)"
    env -u AW_SINDRI_PORT AW_SINDRI_HOME="$real_home" "${main[@]}" context map export "$home/seed.json" \
      || { rm -f "$home/seed.json"; broke "step 3: the export failed; then scripts/owner-check.sh $stage down"; }
  fi

  echo "== 4. profile, start, approve"
  local p="$home/profile/profile.yaml" rc
  (umask 077 && cp "$edited" "$p") || broke "step 4: can't copy the edited profile into $home"
  rc=0; bounded "${in_scratch[@]}" "$bin" --headless || rc=$?
  # Exit 1 is "needs approval" or "went away before its status answered" (S1); only a listener tells them apart.
  [ "$rc" -le 1 ] && listening || broke "step 4: the scratch instance isn't running on port $port (exit $rc); see $home/logs/serve.log, then scripts/owner-check.sh $stage down"
  local hash
  hash="$({ bounded "${in_scratch[@]}" "$bin" profile validate --json || true; } | sed -nE 's/.*"hash":"([0-9a-f]{64})".*/\1/p')"
  [ -n "$hash" ] || broke "step 4: profile validate gave no hash; run: AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port $bin profile validate"
  echo "-- approve at this terminal: type the first 6 characters of the hash when asked"
  "${in_scratch[@]}" "$bin" profile approve "$hash" || broke "step 4: approval failed; retry: AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port $bin profile approve $hash, or scripts/owner-check.sh $stage down"
  if [ "$seed" = 1 ]; then
    echo "-- import the seed at this terminal"
    "${in_scratch[@]}" "$bin" context map import "$home/seed.json" \
      || broke "step 4: import failed; retry: AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port $bin context map import $home/seed.json, or scripts/owner-check.sh $stage down"
  fi

  if [ "$session" = 1 ]; then
    (umask 077 && printf '{\n  "mcpServers": {\n    "sindri-ownercheck": {\n      "type": "http",\n      "url": "http://127.0.0.1:%s/sindri/mcp",\n      "headers": { "X-Sindri-Repo": "%s" }\n    }\n  }\n}\n' "$port" "$repo" >"$scratch/mcp.json")
  fi
  echo
  echo "owner-check $stage up. For the steps in owner-check.md:"
  echo "  unset AW_SINDRI_PROFILE; export SINDRI=$bin AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port"
  [ "$session" = 0 ] || echo "  claude --strict-mcp-config --mcp-config $scratch/mcp.json   # first call context_sources: build and port must be this instance's"
  echo "When done (pass or fail): scripts/owner-check.sh $stage down"
}

"$action"
