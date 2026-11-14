#!/usr/bin/env bash
# scripts/scaffold/e2e.sh keeps the disk small and times its steps (#3700). No real dotnet or generator runs here:
# a copy of e2e.sh runs in a throwaway git repo with stub `node` and `dotnet` first on PATH.
#
#   1. the cli tree's build output is gone before the http step starts (the peak is one tree, not two);
#   2. no `dotnet build AgenticWorkflow.slnx` is called, and the script text has none either;
#   3. one `e2e: <step> <N>s` line per finished step.
#
# The stub node, run as the http generator, records whether the cli tree still holds artifacts, bin or obj, then exits 1,
# so the run ends there; the checks read what was seen up to that point.
set -uo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
E2E="$HERE/../scaffold/e2e.sh"
fail=0 pass=0
ok() { pass=$((pass + 1)); }
bad() { fail=$((fail + 1)); echo "FAIL: $*" >&2; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
repo="$work/repo"; fake="$work/fakebin"; log="$work/log"; tmproot="$work/tmp"
mkdir -p "$repo/scripts/scaffold" "$fake" "$log" "$tmproot"
cp "$E2E" "$repo/scripts/scaffold/e2e.sh"
git -C "$repo" init -q
git -C "$repo" add -A
git -C "$repo" -c user.name=t -c user.email=t@localhost commit -q -m init

cat > "$fake/node" <<'EOF'
#!/usr/bin/env bash
case "$*" in *--input-type*) exit 3 ;; esac
script="$1"; shift
echo "node $(basename "$script") $*" >> "$FAKE_LOG/node.log"
if [[ "$script" == *new-package.mjs ]]; then
  name=""; first=""; prev=""
  for a in "$@"; do
    [ "$prev" = "--name" ] && name="$a"
    [ "$prev" = "--first-slice" ] && first="$a"
    prev="$a"
  done
  case " $* " in
    *" --host http "*)
      echo "http-started" > "$FAKE_LOG/http-started"
      small="$(dirname "$PWD")/small"
      if [ -d "$small" ] && find "$small" -maxdepth 4 \( -name artifacts -o -name bin -o -name obj \) | grep -q .; then
        echo "cli tree still holds build output" > "$FAKE_LOG/leak"
      fi
      exit 1 ;;
  esac
  mkdir -p "src/$name/Aw.$name.App/Features/$first" "tests/Aw.$name.Tests/$first" artifacts/bin
  echo x > "src/$name/Aw.$name.App/Features/$first/${first}Service.cs"
  echo x > "tests/Aw.$name.Tests/$first/${first}Tests.cs"
fi
exit 0
EOF

cat > "$fake/dotnet" <<'EOF'
#!/usr/bin/env bash
echo "dotnet $*" >> "$FAKE_LOG/dotnet.log"
if [ "${1:-}" = "--info" ]; then echo "  RID: linux-x64"; exit 0; fi
mkdir -p artifacts/bin obj
if [ "${1:-}" = "publish" ]; then
  out=""; prev=""
  for a in "$@"; do [ "$prev" = "-o" ] && out="$a"; prev="$a"; done
  mkdir -p "$out"
  printf '#!/bin/sh\necho "{\\"eventName\\":\\"AW-TELEMETRY-001\\"}" >&2\nexit 2\n' > "$out/scaffoldsmall"
  chmod +x "$out/scaffoldsmall"
fi
exit 0
EOF
chmod +x "$fake/node" "$fake/dotnet"

out="$work/out.txt"
(cd "$repo" && env -u CI PATH="$fake:$PATH" FAKE_LOG="$log" TMPDIR="$tmproot" AW_HEAVY_LOCK_HELD=1 \
  bash scripts/scaffold/e2e.sh >"$out" 2>&1)

[ -f "$log/http-started" ] && ok || bad "the run never reached the http step: $(tail -5 "$out" | tr '\n' ' ')"
[ ! -f "$log/leak" ] && ok || bad "$(cat "$log/leak") when the http step started"
if grep -Eq 'dotnet build AgenticWorkflow\.slnx' "$log/dotnet.log" 2>/dev/null; then bad "a whole-solution build ran"; else ok; fi
if grep -Eq 'dotnet build AgenticWorkflow\.slnx' "$E2E"; then bad "e2e.sh still builds the whole solution"; else ok; fi
grep -Eq '^e2e: .+ [0-9]+s$' "$out" && ok || bad "no 'e2e: <step> <N>s' line in the output"
[ -z "$(ls -A "$tmproot" 2>/dev/null)" ] && ok || bad "the run left files in its TMPDIR: $(ls "$tmproot")"

echo "scaffold-e2e: $pass passed, 0 skipped, $fail failed"
[ "$fail" = 0 ]
