#!/usr/bin/env bash
# The bash-tests job runs on macos-15 (bash 3.2, BSD sed/find/touch). Six suites passed on the Linux box and failed there (#2534):
# meta-hooks, check-skill-blocks, check-skill-requires, eval-av-fakes, heavy and install-cs. This test reproduces the runner's
# conditions on any box, in two parts:
#   1. a static scan of those suites (and the stubs they run) for what bash 3.2 or BSD tools reject;
#   2. the quick suites run with sed, touch and find that refuse the GNU-only forms the way the BSD ones do.
# A suite made portable passes both parts on Linux and macOS alike; none of this weakens a suite's own assertions.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SUITES="config/hooks/tests/meta-hooks.test.sh scripts/tests/check-skill-blocks.test.sh scripts/tests/check-skill-requires.test.sh scripts/tests/eval-av-fakes.test.sh scripts/tests/heavy.test.sh scripts/tests/install-cs.test.sh"
FILES="$SUITES $(cd "$ROOT" && ls evals/stubs/bin/* | tr '\n' ' ')"
FAILS=0
bad() { FAILS=$((FAILS + 1)); echo "FAIL: $1"; }
ok()  { echo "PASS: $1"; }

# scan LABEL ERE: no non-comment line of FILES matches ERE
scan() {
  local label="$1" re="$2" hits
  hits="$(cd "$ROOT" && for f in $FILES; do grep -nE -- "$re" "$f" 2> /dev/null | grep -vE '^[0-9]+:[[:space:]]*#' | sed "s|^|$f:|"; done)"
  if [ -n "$hits" ]; then bad "$label"; printf '%s\n' "$hits" | sed 's/^/      | /'; else ok "$label"; fi
}

scan "bash 3.2 has no mapfile or readarray (stubs/bin/bun line 29 on the runner)" '\b(mapfile|readarray)\b'
scan "bash 3.2 has no BASHPID (heavy.test.sh line 54: unbound variable)" '\$\{?BASHPID'
scan "bash 3.2 reads a brace inside \${var:-...} differently (check-skill-blocks 'valid manifest': the aw-result JSON broke)" '\$\{[0-9A-Za-z_]+:-\{'
scan "BSD sed reads the next argument of a bare -i as the backup suffix (sed: invalid command code)" 'sed[[:space:]]+(-[A-Za-z]+[[:space:]]+)*-i[[:space:]]'
scan "BSD find has no -printf (install-cs.test.sh: the staged-scripts rows ran /heavy, exit 127)" 'find[[:space:]].*-printf'
scan "BSD touch has no -d 'N seconds ago' (meta-hooks: out of range or illegal time specification)" 'touch[[:space:]]+-d[[:space:]]'

# skills/_shared/skill-lock.sh must run on bash 3.2: none of the bash 4 or GNU-only forms, and no bash >= 4 refusal.
lockhits="$(grep -nE '\b(mapfile|readarray|coproc)\b|\$\{?BASHPID|declare[[:space:]]+-A|\$\{[A-Za-z_]+(,,|\^\^)|wait[[:space:]]+-n|\{[a-z_]+\}[<>]|sed[[:space:]]+-i[[:space:]]|find[[:space:]].*-printf|stat[[:space:]]+-c|date[[:space:]]+-d|readlink[[:space:]]+-f|touch[[:space:]]+-d|BASH_VERSINFO' "$ROOT/skills/_shared/skill-lock.sh" | grep -vE '^[0-9]+:[[:space:]]*#' || true)"
if [ -n "$lockhits" ]; then bad "skill-lock.sh uses a bash 4 or GNU-only form"; printf '%s\n' "$lockhits" | sed 's/^/      | /'; else ok "skill-lock.sh has no bash 4 or GNU-only forms"; fi

# Part 2: sed, touch and find that refuse the GNU-only forms, first on PATH for the quick suites.
SHIM="$(mktemp -d)"
trap 'rm -f "$SHIM/sed" "$SHIM/touch" "$SHIM/find"; rmdir "$SHIM"' EXIT
real_sed="$(command -v sed)"; real_touch="$(command -v touch)"; real_find="$(command -v find)"
cat > "$SHIM/sed" <<SH
#!/bin/bash
# BSD sed: a bare -i takes the next word as its backup suffix, so \`sed -i EXPR FILE\` reads EXPR as the suffix.
for a in "\$@"; do case "\$a" in -i) echo "sed: 1: invalid command code f" >&2; exit 1 ;; esac; done
exec "$real_sed" "\$@"
SH
cat > "$SHIM/touch" <<SH
#!/bin/bash
for a in "\$@"; do case "\$a" in -d) echo "touch: out of range or illegal time specification: YYYY-MM-DDThh:mm:SS[.frac][tz]" >&2; exit 1 ;; esac; done
exec "$real_touch" "\$@"
SH
cat > "$SHIM/find" <<SH
#!/bin/bash
for a in "\$@"; do case "\$a" in -printf) echo "find: -printf: unknown primary or operator" >&2; exit 1 ;; esac; done
exec "$real_find" "\$@"
SH
chmod +x "$SHIM/sed" "$SHIM/touch" "$SHIM/find"

for s in scripts/tests/check-skill-requires.test.sh scripts/tests/check-skill-blocks.test.sh scripts/tests/eval-av-fakes.test.sh config/hooks/tests/meta-hooks.test.sh; do
  if out="$(cd "$ROOT" && env -u AW_META_LANE -u AW_META_TEAM -u TMUX PATH="$SHIM:$PATH" bash "$s" 2>&1)"; then
    ok "$s passes with BSD-style sed, touch and find"
  else
    bad "$s fails with BSD-style sed, touch and find"
    printf '%s\n' "$out" | grep -E 'FAIL|not ok|invalid command|illegal time' | head -5 | sed 's/^/      | /'
  fi
done

[ "$FAILS" -eq 0 ] || { echo "ci-runner-parity: $FAILS check(s) failed"; exit 1; }
echo "ci-runner-parity: all checks passed"
