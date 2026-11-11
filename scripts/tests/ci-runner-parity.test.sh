#!/usr/bin/env bash
# The bash-tests job runs on macos-15 (bash 3.2, BSD sed/find/touch). Six suites passed on the Linux box and failed there (#2534):
# meta-hooks, check-skill-blocks, check-skill-requires, eval-av-fakes, heavy and install-cs. This test reproduces the runner's
# conditions on any box, in three parts:
#   1. a static scan of those suites (and the stubs they run) for what bash 3.2 or BSD tools reject;
#   2. the quick suites run with sed, touch and find that refuse the GNU-only forms the way the BSD ones do;
#   3. heavy and install-cs run on a PATH without setsid and timeout, which macOS doesn't ship (main f936ef2d13 on the runner:
#      heavy's perl fallback read `--expensive` as a perl switch, exit 25; install-cs's staged rows ran `timeout`, exit 127).
# Parts 4 and 5 (#2584, #2757) add the forms found on the runner since.
# A suite made portable passes every part on Linux and macOS alike; none of this weakens a suite's own assertions.
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

# Part 2: sed, touch and find that refuse the GNU-only forms, first on PATH for the quick suites.
SHIM="$(mktemp -d)"
NOTOOLS="$(mktemp -d)"
trap 'rm -f "$SHIM/sed" "$SHIM/touch" "$SHIM/find"; rmdir "$SHIM"; find "$NOTOOLS" -type l -delete; rmdir "$NOTOOLS"' EXIT
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

for s in scripts/tests/check-skill-requires.test.sh scripts/tests/check-skill-blocks.test.sh scripts/tests/eval-av-fakes.test.sh config/hooks/tests/meta-hooks.test.sh scripts/tests/meta-start-lock-guard.test.sh; do
  if out="$(cd "$ROOT" && env -u AW_META_LANE -u AW_META_TEAM -u TMUX PATH="$SHIM:$PATH" bash "$s" 2>&1)"; then
    ok "$s passes with BSD-style sed, touch and find"
  else
    bad "$s fails with BSD-style sed, touch and find"
    printf '%s\n' "$out" | grep -E 'FAIL|not ok|invalid command|illegal time' | head -5 | sed 's/^/      | /'
  fi
done

# Part 3: every tool on PATH but setsid, timeout and gtimeout, linked into one folder that is the whole PATH.
IFS=: read -r -a path_dirs <<< "$PATH"
for d in "${path_dirs[@]}"; do
  [ -d "$d" ] || continue
  for t in "$d"/*; do
    n="${t##*/}"
    case "$n" in setsid|timeout|gtimeout) continue ;; esac
    if [ -x "$t" ] && [ ! -d "$t" ] && [ ! -e "$NOTOOLS/$n" ]; then ln -s "$t" "$NOTOOLS/$n"; fi
  done
done
for s in scripts/tests/heavy.test.sh scripts/tests/install-cs.test.sh; do
  if out="$(cd "$ROOT" && env -u AW_META_LANE -u AW_META_TEAM -u TMUX PATH="$NOTOOLS" bash "$s" 2>&1)"; then
    ok "$s passes without setsid and timeout on PATH"
  else
    bad "$s fails without setsid and timeout on PATH"
    printf '%s\n' "$out" | grep -E 'not ok|command not found|Unrecognized switch' | head -5 | sed 's/^/      | /'
  fi
done

# Part 4 (#2584): three more suites failed on the runner, each from one macOS difference reproduced here.
# 4a. bash 3.2 ends a command substitution at the `)` of a bare case pattern: `$(case X in "$P"/*) ...` does not parse,
#     `$(case X in ("$P"/*) ...` does (sindri-ui-fixture-reviews.test.sh:108 printed " echo inside ;; *) echo outside").
FILES="$(cd "$ROOT" && git ls-files '*.sh' | tr '\n' ' ')" \
  scan "bash 3.2 ends \$( ) at a bare case pattern's ) (write the pattern as (pattern))" '\$\(case .* in +[^( ]'
# Part 5 (#2757): four more forms that passed on Linux and failed on the runner, scanned over every tracked .sh but this one.
FILES="$(cd "$ROOT" && git ls-files '*.sh' | grep -v '^scripts/tests/ci-runner-parity.test.sh$' | tr '\n' ' ')"
# 5a. 4a checks only the first pattern; a bare later one cuts the substitution too (sindri-ui-fixture-reviews.test.sh:113
#     printed " echo outside ;; esac)").
scan "bash 3.2 ends \$( ) at a bare case pattern after ;; too (write it as (*))" '\$\(case .*;;[[:space:]]*([^([:space:];e]|e[^s]|es[^a]|esa[^c])'
# 5b. touch -d in any suite, unless the line falls back to touch -t (meta-start-lock-guard.test.sh:109: the "old" guard
#     stayed young, so the takeover kept it). The Part 1 scan covers only its own suite list.
hits="$(cd "$ROOT" && for f in $FILES; do grep -nE -- 'touch[[:space:]]+-d[[:space:]]' "$f" 2> /dev/null | grep -vE '^[0-9]+:[[:space:]]*#' | grep -v 'touch -t' | sed "s|^|$f:|"; done)"
if [ -n "$hits" ]; then bad "BSD touch has no -d 'N minutes ago' in any suite (add a touch -t fallback)"; printf '%s\n' "$hits" | sed 's/^/      | /'
else ok "BSD touch has no -d 'N minutes ago' in any suite (add a touch -t fallback)"; fi
# 5c. BSD wc pads its count ("       1"), so "$(wc -l <f)" = 1 is false there (skill-evals: "calls: alpha-natural-1").
scan "BSD wc pads counts: compare \$(wc ...) as a string only after tr -d ' '" 'wc -[lcw][^|)]*\)" *(!=|=) '
# 5d. macOS /tmp is a symlink to /private/tmp, and the stub claude runs under env -i, so its $PWD is the physical path:
#     skill-evals' /tmp roots must be taken through pwd -P ("claude cwd"). 4b moves only TMPDIR, which they ignore.
hits="$(cd "$ROOT" && grep -nE 'mktemp -d /tmp/' scripts/tests/skill-evals.test.sh | grep -v 'pwd -P')"
if [ -n "$hits" ]; then bad "skill-evals.test.sh takes its /tmp roots through pwd -P (macOS /tmp -> /private/tmp)"; printf '%s\n' "$hits" | sed 's/^/      | scripts\/tests\/skill-evals.test.sh:/'
else ok "skill-evals.test.sh takes its /tmp roots through pwd -P (macOS /tmp -> /private/tmp)"; fi
# 5e. the padding is behavioural too: 4b runs skill-evals with a wc that pads like BSD's.
WCSHIM="$(mktemp -d)"
cat > "$WCSHIM/wc" <<SH
#!/bin/bash
out="\$("$(command -v wc)" "\$@")" || exit \$?
printf '%s\n' "\$out" | "$real_sed" -E 's/^ *([0-9]+)/       \1/'
SH
chmod +x "$WCSHIM/wc"

# 4b. /var -> /private/var: mktemp -d under a symlinked TMPDIR, as macOS's /var/folders is (skill-evals: "AW_DIR differs"),
#     with a wc that pads its count as BSD's does (5e).
LINKTMP="$(mktemp -u "${TMPDIR:-/tmp}/parity-linktmp.XXXXXX")"; REALTMP="$(mktemp -d)"; ln -s "$REALTMP" "$LINKTMP"
if out="$(cd "$ROOT" && env -u AW_META_LANE -u AW_META_TEAM -u TMUX TMPDIR="$LINKTMP" PATH="$WCSHIM:$PATH" bash scripts/tests/skill-evals.test.sh 2>&1)"; then
  ok "skill-evals.test.sh passes with TMPDIR behind a symlink (macOS /var -> /private/var) and BSD wc's padded counts"
else
  bad "skill-evals.test.sh fails with TMPDIR behind a symlink (macOS /var -> /private/var) and BSD wc's padded counts"
  printf '%s\n' "$out" | grep -E 'FAIL' | head -5 | sed 's/^/      | /'
fi
command rm -f "$LINKTMP" "$WCSHIM/wc"; rmdir "$REALTMP" "$WCSHIM" 2> /dev/null
# 4c. heavy-cap-reserve on a PATH without timeout: test-portable.sh's fallback timeout must not leave its sleep holding
#     the caller's stdout ("reserve 1, both slots busy, timeout 2 gives up inside 6 s": got "none slow 10s").
if out="$(cd "$ROOT" && env -u AW_META_LANE -u AW_META_TEAM -u TMUX PATH="$NOTOOLS" bash scripts/tests/heavy-cap-reserve.test.sh 2>&1)"; then
  ok "heavy-cap-reserve.test.sh passes without setsid and timeout on PATH"
else
  bad "heavy-cap-reserve.test.sh fails without setsid and timeout on PATH"
  printf '%s\n' "$out" | grep -E 'not ok|FAIL' | head -5 | sed 's/^/      | /'
fi
# 4d. no /proc/meminfo (macOS): heavy.sh's memory guard must read vm_stat instead, or slot 2 and the meta reserve are gone
#     (the wrapper rows got 0 for 124 and 124 for 0). A fake vm_stat reports 32 GB free; bwrap hides /proc/meminfo.
if command -v bwrap > /dev/null 2>&1 && bwrap --dev-bind / / true 2> /dev/null; then
  cat > "$SHIM/vm_stat" <<'SH'
#!/bin/bash
cat <<'OUT'
Mach Virtual Memory Statistics: (page size of 16384 bytes)
Pages free:                              1048576.
Pages active:                             200000.
Pages inactive:                           524288.
Pages speculative:                        524288.
Pages throttled:                               0.
Pages wired down:                         100000.
OUT
SH
  chmod +x "$SHIM/vm_stat"
  if out="$(cd "$ROOT" && env -u AW_META_LANE -u AW_META_TEAM -u TMUX PATH="$SHIM:$PATH" \
      bwrap --dev-bind / / --ro-bind /dev/null /proc/meminfo bash scripts/tests/heavy-cap-reserve.test.sh 2>&1)"; then
    ok "heavy-cap-reserve.test.sh passes without /proc/meminfo (vm_stat on PATH)"
  else
    bad "heavy-cap-reserve.test.sh fails without /proc/meminfo (vm_stat on PATH)"
    printf '%s\n' "$out" | grep -E 'not ok|FAIL' | head -5 | sed 's/^/      | /'
  fi
  command rm -f "$SHIM/vm_stat"
else
  echo "SKIP: heavy-cap-reserve without /proc/meminfo (no working bwrap)"
fi

[ "$FAILS" -eq 0 ] || { echo "ci-runner-parity: $FAILS check(s) failed"; exit 1; }
echo "ci-runner-parity: all checks passed"
