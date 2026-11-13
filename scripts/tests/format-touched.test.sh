#!/usr/bin/env bash
# scripts/format-touched.sh (#3564, #2602, #2639): a real format check of the named .cs files. A fake `dotnet` on PATH
# mimics the real behavior (issue #3564 table): it checks a file only when its cwd is the repo root and --include is
# repo-relative; any other form prints "Formatted 0 of N files" and exits 0. So a script that passes the argument
# through (abs form, or a subdir cwd) reports a broken file as clean, and these cases go red.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/scripts/format-touched.sh"
TMP="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$TMP"' EXIT

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

mkdir -p "$TMP/bin"
cat > "$TMP/bin/dotnet" <<'FAKE'
#!/usr/bin/env bash
echo "cwd=$(pwd -P) argv=$*" >> "$FAKE_LOG"
[ "${1:-}" = format ] || exit 0
shift
proj="$1"; shift
verify=0; incs=(); in_inc=0
for a in "$@"; do
  case "$a" in
    --verify-no-changes) verify=1; in_inc=0 ;;
    --include) in_inc=1 ;;
    -*) in_inc=0 ;;
    *) [ "$in_inc" = 1 ] && incs+=("$a") ;;
  esac
done
[ -n "${FAKE_ERRLINE:-}" ] && echo "$FAKE_ERRLINE"
checked=0; errs=0
for f in "${incs[@]}"; do
  case "$f" in /*) continue ;; esac
  [ "$(pwd -P)" = "$FAKE_REPO" ] || continue
  [ -f "$f" ] || continue
  checked=$((checked + 1))
  if [ "$verify" = 1 ] && grep -q '// BROKEN' "$f"; then
    echo "$FAKE_REPO/$f(1,1): error WHITESPACE: Fix whitespace formatting."
    errs=$((errs + 1))
  fi
done
echo "Formatted $checked of 5 files"
[ "$errs" = 0 ] || exit 2
exit 0
FAKE
chmod +x "$TMP/bin/dotnet"

R="$TMP/repo"
mkdir -p "$R/A" "$R/B" "$R/C/Skip" "$R/D" "$R/sub"
git -C "$R" init -q
echo '<Project Sdk="Microsoft.NET.Sdk" />' > "$R/A/A.csproj"
echo '<Project Sdk="Microsoft.NET.Sdk" />' > "$R/B/B.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup><Compile Remove="Skip\\**" /></ItemGroup>\n</Project>\n' > "$R/C/C.csproj"
echo '<Project />' > "$R/D/D1.csproj"; echo '<Project />' > "$R/D/D2.csproj"
echo 'class Clean { }' > "$R/A/Clean.cs"
printf 'class Broken { }\n// BROKEN\n' > "$R/A/Broken.cs"
echo 'class Bar { }' > "$R/B/Bar.cs"
echo 'class S { }' > "$R/C/Skip/S.cs"
echo 'class C1 { }' > "$R/C/C1.cs"
echo 'class Loose { }' > "$R/Loose.cs"
echo 'class Two { }' > "$R/D/Two.cs"
echo hi > "$R/A/notes.txt"
LOG="$TMP/log"

# run <cwd> <args...>: sets out, rc
run() {
  local cwd="$1"; shift
  : > "$LOG"
  out="$(cd "$cwd" && PATH="$TMP/bin:$PATH" AW_HEAVY="$TMP/no-such-heavy" FAKE_LOG="$LOG" FAKE_REPO="$R" bash "$SCRIPT" "$@" 2>&1)"; rc=$?
}

# broken file, three forms
run "$TMP" "$R/A/Broken.cs"
{ [ "$rc" = 1 ] && printf '%s' "$out" | grep -q 'Broken.cs(1,1): error'; } && ok "broken file, absolute form: exit 1 with the error line" || bad "broken abs rc=$rc out=$out"
run "$R/sub" ../A/Broken.cs
{ [ "$rc" = 1 ] && printf '%s' "$out" | grep -q 'Broken.cs(1,1): error'; } && ok "broken file, cwd-relative form from a subdir: exit 1" || bad "broken cwd-rel rc=$rc out=$out"
run "$R/sub" A/Broken.cs
{ [ "$rc" = 1 ] && printf '%s' "$out" | grep -q 'Broken.cs(1,1): error'; } && ok "broken file, repo-relative form from a subdir: exit 1" || bad "broken repo-rel rc=$rc out=$out"
run "$R" A/Broken.cs
{ [ "$rc" = 1 ] && printf '%s' "$out" | grep -q 'format A/A.csproj: 1 files checked, 1 errors'; } && ok "summary line names the csproj, files and errors" || bad "summary rc=$rc out=$out"

# clean file, three forms
for form in "$TMP|$R/A/Clean.cs" "$R/sub|../A/Clean.cs" "$R/sub|A/Clean.cs"; do
  cwd="${form%%|*}"; arg="${form#*|}"
  run "$cwd" "$arg"
  if [ "$rc" = 0 ] && grep -qx "cwd=$R argv=format A/A.csproj --verify-no-changes --include A/Clean.cs" "$LOG" \
     && printf '%s' "$out" | grep -q 'format A/A.csproj: 1 files checked, 0 errors'; then
    ok "clean file ($arg from $(basename "$cwd")): exit 0, cwd is the repo root, include is repo-relative"
  else
    bad "clean $arg rc=$rc log=$(cat "$LOG") out=$out"
  fi
done

# two projects: one call each
run "$R" A/Clean.cs B/Bar.cs A/Broken.cs
n="$(grep -c 'argv=format' "$LOG")"
{ [ "$rc" = 1 ] && [ "$n" = 2 ] && grep -q 'format A/A.csproj .*--include A/Clean.cs A/Broken.cs' "$LOG" \
  && grep -q 'format B/B.csproj .*--include B/Bar.cs' "$LOG"; } && ok "two projects: one dotnet call per csproj, files grouped" || bad "grouping rc=$rc n=$n log=$(cat "$LOG")"

# not checked
run "$R" C/Skip/S.cs
{ [ "$rc" = 1 ] && printf '%s' "$out" | grep -q 'not checked: C/Skip/S.cs'; } && ok "file under a Compile Remove: exit 1 not checked" || bad "compile remove rc=$rc out=$out"
run "$R" C/C1.cs
[ "$rc" = 0 ] && ok "file in a project with Compile Remove, not matched: checked" || bad "C1 rc=$rc out=$out"
run "$R" Loose.cs
{ [ "$rc" = 2 ] && printf '%s' "$out" | grep -q 'Loose.cs'; } && ok "file outside any project: exit 2 naming it" || bad "loose rc=$rc out=$out"
run "$R" D/Two.cs
{ [ "$rc" = 2 ] && printf '%s' "$out" | grep -q 'Two.cs'; } && ok "dir with two csproj: exit 2 naming the file" || bad "two csproj rc=$rc out=$out"

# usage
run "$R" A/Missing.cs
{ [ "$rc" = 2 ] && printf '%s' "$out" | grep -q 'Missing.cs'; } && ok "missing file: exit 2 naming it" || bad "missing rc=$rc out=$out"
run "$R" A/notes.txt
{ [ "$rc" = 2 ] && printf '%s' "$out" | grep -q 'notes.txt'; } && ok "non-.cs file: exit 2" || bad "txt rc=$rc out=$out"
echo 'class Out { }' > "$TMP/Outside.cs"
run "$TMP" "$TMP/Outside.cs"
{ [ "$rc" = 2 ] && printf '%s' "$out" | grep -q 'Outside.cs'; } && ok "file outside a git repo: exit 2" || bad "outside rc=$rc out=$out"
run "$R"
[ "$rc" = 2 ] && ok "no arguments: exit 2" || bad "no args rc=$rc"

# an error line with exit 0
: > "$LOG"
out="$(cd "$R" && PATH="$TMP/bin:$PATH" AW_HEAVY="$TMP/none" FAKE_LOG="$LOG" FAKE_REPO="$R" FAKE_ERRLINE='A/Clean.cs(2,1): error IMPORTS: Fix imports ordering.' bash "$SCRIPT" A/Clean.cs 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && printf '%s' "$out" | grep -q '1 errors'; } && ok "dotnet exit 0 with an ' error ' line: exit 1" || bad "error line rc=$rc out=$out"

# --fix: no --verify-no-changes
run "$R" --fix A/Broken.cs
{ [ "$rc" = 0 ] && grep -q 'argv=format A/A.csproj --include A/Broken.cs' "$LOG" && ! grep -q verify-no-changes "$LOG"; } && ok "--fix runs without --verify-no-changes" || bad "fix rc=$rc log=$(cat "$LOG")"

# AW_HEAVY used when the file exists
cat > "$TMP/heavy" <<'H'
#!/usr/bin/env bash
echo "heavy $*" >> "$FAKE_LOG"
shift; exec "$@"
H
chmod +x "$TMP/heavy"
: > "$LOG"
out="$(cd "$R" && PATH="$TMP/bin:$PATH" AW_HEAVY="$TMP/heavy" FAKE_LOG="$LOG" FAKE_REPO="$R" bash "$SCRIPT" A/Clean.cs 2>&1)"; rc=$?
{ [ "$rc" = 0 ] && grep -q '^heavy --light dotnet format A/A.csproj' "$LOG"; } && ok "AW_HEAVY present: runs through heavy --light" || bad "heavy rc=$rc log=$(cat "$LOG")"

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
