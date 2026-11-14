#!/usr/bin/env bash
# scripts/format-touched.sh [--fix] <file.cs>...
#
# A real `dotnet format` check (or fix) of the named C# files, in any argument form: absolute, relative to the cwd, or
# relative to the repo root.
#
# Why (#3564, #2602, #2639): `dotnet format <csproj|dir> --verify-no-changes --include <path>` checks a file only when it
# runs from the repo root with a csproj argument and a repo-root-RELATIVE --include. An absolute include, a directory
# argument, or a project-relative include matches nothing: exit 0, "Formatted 0 of N files", 0 errors. "Formatted X of N"
# counts the project's files, so a clean file never shows up in the output, and a clean result can't prove the file was read.
# So this script builds the one form that works, and proves each file is in the run before it runs.
#
# What it does:
#   1. Turns every argument into a repo-root-relative path (the root is `git rev-parse --show-toplevel` of the file's dir).
#   2. Maps each file to the csproj in its nearest ancestor dir (exactly one *.csproj there).
#   3. Runs ONE `dotnet format <csproj> --verify-no-changes --include <files...>` per csproj, cwd = the repo root, through
#      `${AW_HEAVY:-$HOME/.agentic-workflow/bin-r1b/heavy} --light` when that file exists, plain `dotnet` otherwise.
#      `--fix` runs the same without --verify-no-changes.
#   4. Prints `format <csproj>: <n> files checked, <k> errors` per csproj. Any " error " line counts as a finding even
#      when dotnet exits 0 (#2298).
#
# "The file is in the run" is proved structurally, before the run, because the output can't show it: the file sits under
# the csproj's directory (SDK default globbing), is not under obj/ or bin/, and matches no `<Compile Remove="...">` in the
# csproj or in a Directory.Build.props / .targets above it; when the csproj sets EnableDefaultCompileItems=false, a
# `<Compile Include=>` must match it. What is read, and what is not:
#   - A pattern is relative to the dir of the file that holds it (a props file's own dir, not the project's), as in MSBuild.
#   - Globs follow MSBuild: `**/` matches zero or more dirs, `**` anything, `*` and `?` stay inside one dir; `\` is `/`.
#   - `$(MSBuildProjectDirectory)` and `$(MSBuildThisFileDirectory)` at the start of a pattern are resolved. Any other `$(Var)`
#     can't be read: the file is reported "not checked" rather than guessed.
#   - A Compile item written over several lines is read. Single or double quotes both work.
#   - Not read: `Exclude=` on a Compile Include, `Update=`, items from imports other than the Directory.Build.* files above,
#     conditions on items, and lists built from properties. Not read from MSBuild itself: `dotnet msbuild -getItem:Compile`
#     needs a restore.
#
# Exit: 0 every named file checked and clean; 1 a format finding, a failed dotnet run, or a file not checked
# ("not checked: <file> (<reason>)"); 2 usage (no files, a missing or non-.cs file, a file outside a git repo or outside
# every project, a dir with two csproj, files from two repos).
set -uo pipefail

usage() { echo "usage: format-touched.sh [--fix] <file.cs>..." >&2; exit 2; }

fix=0
files=()
for a in "$@"; do
  case "$a" in
    --fix) fix=1 ;;
    -h|--help) usage ;;
    *) files+=("$a") ;;
  esac
done
[ "${#files[@]}" -gt 0 ] || usage

repo_root=""
rels=()
# Pass 1: normalize every argument to a repo-root-relative path.
for f in "${files[@]}"; do
  case "$f" in *.cs) ;; *) echo "format-touched: not a .cs file: $f" >&2; exit 2 ;; esac
  cand="$f"
  case "$f" in
    /*) ;;
    *)
      cand="$PWD/$f"
      if [ ! -f "$cand" ]; then
        top="$(git rev-parse --show-toplevel 2>/dev/null || true)"
        [ -n "$top" ] && [ -f "$top/$f" ] && cand="$top/$f"
      fi
      ;;
  esac
  [ -f "$cand" ] || { echo "format-touched: no such file: $f" >&2; exit 2; }
  dir="$(cd "$(dirname "$cand")" && pwd -P)"
  abs="$dir/$(basename "$cand")"
  root="$(git -C "$dir" rev-parse --show-toplevel 2>/dev/null)" || { echo "format-touched: not inside a git repo: $f" >&2; exit 2; }
  root="$(cd "$root" && pwd -P)"
  if [ -z "$repo_root" ]; then repo_root="$root"
  elif [ "$repo_root" != "$root" ]; then echo "format-touched: files from two repos: $f" >&2; exit 2; fi
  rels+=("${abs#"$root"/}")
done

# glob_to_re <msbuild glob>: an ERE without anchors. `**/` matches zero or more dirs, `**` anything, `*` and `?` stay inside one dir.
glob_to_re() {
  local g="$1" re="" i=0 n c
  n=${#g}
  while [ "$i" -lt "$n" ]; do
    c="${g:$i:1}"
    case "$c" in
      '*')
        if [ "${g:$i:3}" = '**/' ]; then re="$re(.*/)?"; i=$((i + 3)); continue
        elif [ "${g:$i:2}" = '**' ]; then re="$re.*"; i=$((i + 2)); continue
        else re="$re[^/]*"; fi ;;
      '?') re="$re[^/]" ;;
      '.'|'['|']'|'('|')'|'{'|'}'|'+'|'^'|'$'|'|'|'\') re="$re\\$c" ;;
      *) re="$re$c" ;;
    esac
    i=$((i + 1))
  done
  printf '%s' "$re"
}

# item_patterns <file> <Remove|Include>: the values of every <Compile Remove|Include="..."> in the file, one per line,
# `;` split, `\` turned into `/`. The file is flattened first, so an item written over several lines is read.
item_patterns() {
  tr '\n\r\t' '   ' < "$1" 2>/dev/null \
    | grep -oE "Compile[[:space:]]+$2[[:space:]]*=[[:space:]]*(\"[^\"]*\"|'[^']*')" \
    | sed -E -e "s/^Compile[[:space:]]+$2[[:space:]]*=[[:space:]]*//" -e "s/^[\"']//" -e "s/[\"']\$//" \
    | tr ';' '\n' | tr '\\' '/'
}

# pattern_matches <pattern> <defining dir> <project dir> <file>: all repo-root-relative ("." is the root).
# 0 matches, 1 doesn't, 2 the pattern holds a $(Var) that isn't read. A pattern is relative to the dir of the file that holds
# it; $(MSBuildProjectDirectory)/ is the project dir and $(MSBuildThisFileDirectory) the defining dir.
pattern_matches() {
  local pat="$1" base="$2" pdir="$3" file="$4" sub re full
  case "$pat" in
    '$(MSBuildProjectDirectory)'*) base="$pdir"; pat="${pat#'$(MSBuildProjectDirectory)'}"; pat="${pat#/}" ;;
    '$(MSBuildThisFileDirectory)'*) pat="${pat#'$(MSBuildThisFileDirectory)'}"; pat="${pat#/}" ;;
  esac
  case "$pat" in *'$('*) return 2 ;; esac
  pat="${pat#./}"
  if [ "$base" = "." ]; then sub="$file"; else sub="${file#"$base"/}"; fi
  re="$(glob_to_re "$pat")"
  full="^${re}\$"
  [[ "$sub" =~ $full ]]
}

# Pass 2: csproj per file, and the "is it in the run" proof.
not_checked=0
pairs=""
tab="$(printf '\t')"
for rel in "${rels[@]}"; do
  d="$(dirname "$rel")"
  csproj=""
  while :; do
    cnt=0; found=""
    for p in "$repo_root/$d"/*.csproj; do
      [ -e "$p" ] || continue
      cnt=$((cnt + 1)); found="$p"
    done
    if [ "$cnt" -gt 1 ]; then echo "format-touched: more than one csproj in $d for $rel" >&2; exit 2; fi
    if [ "$cnt" = 1 ]; then csproj="${found#"$repo_root"/}"; break; fi
    [ "$d" = "." ] && break
    d="$(dirname "$d")"
  done
  [ -n "$csproj" ] || { echo "format-touched: no csproj above $rel" >&2; exit 2; }

  pdir="$(dirname "$csproj")"
  if [ "$pdir" = "." ]; then inproj="$rel"; else inproj="${rel#"$pdir"/}"; fi
  reason=""
  case "$inproj" in obj/*|bin/*) reason="under obj/ or bin/, excluded from default compile items" ;; esac

  # The csproj plus every Directory.Build.props / .targets from its dir up to the root (repo-root-relative paths).
  defs=("$csproj")
  up="$pdir"
  while :; do
    for n in Directory.Build.props Directory.Build.targets; do
      if [ "$up" = "." ]; then cand="$n"; else cand="$up/$n"; fi
      [ -f "$repo_root/$cand" ] && defs+=("$cand")
    done
    [ "$up" = "." ] && break
    up="$(dirname "$up")"
  done
  if [ -z "$reason" ]; then
    for df in "${defs[@]}"; do
      ddir="$(dirname "$df")"
      pats="$(item_patterns "$repo_root/$df" Remove)"
      while IFS= read -r pat; do
        [ -n "$pat" ] || continue
        pattern_matches "$pat" "$ddir" "$pdir" "$rel"; m=$?
        if [ "$m" = 0 ]; then reason="matches <Compile Remove=\"$pat\"> in $df"; break 2; fi
        if [ "$m" = 2 ]; then reason="<Compile Remove=\"$pat\"> in $df holds a \$(Var) this script doesn't read"; break 2; fi
      done <<EOF
$pats
EOF
    done
  fi
  if [ -z "$reason" ] && tr '\n\r\t' '   ' < "$repo_root/$csproj" | grep -qE 'EnableDefaultCompileItems[^>]*>[[:space:]]*false'; then
    reason="EnableDefaultCompileItems is false and no <Compile Include> matches"
    incs="$(item_patterns "$repo_root/$csproj" Include)"
    while IFS= read -r pat; do
      [ -n "$pat" ] || continue
      pattern_matches "$pat" "$pdir" "$pdir" "$rel"; m=$?
      if [ "$m" = 0 ]; then reason=""; break; fi
      if [ "$m" = 2 ]; then reason="EnableDefaultCompileItems is false and <Compile Include=\"$pat\"> holds a \$(Var) this script doesn't read"; break; fi
    done <<EOF
$incs
EOF
  fi
  if [ -n "$reason" ]; then
    echo "not checked: $rel ($reason)"
    not_checked=$((not_checked + 1))
    continue
  fi
  pairs="$pairs$csproj$tab$rel
"
done

# Pass 3: one dotnet call per csproj, from the repo root.
heavy="${AW_HEAVY:-$HOME/.agentic-workflow/bin-r1b/heavy}"
runner=(dotnet)
[ -f "$heavy" ] && runner=("$heavy" --light dotnet)

status=0
[ "$not_checked" -gt 0 ] && status=1
cd "$repo_root" || exit 2
out_file="$(mktemp)"
trap 'rm -f "$out_file"' EXIT
csprojs="$(printf '%s' "$pairs" | cut -f1 | sort -u)"
while IFS= read -r csproj; do
  [ -n "$csproj" ] || continue
  inc=()
  while IFS="$tab" read -r c r; do
    [ "$c" = "$csproj" ] && inc+=("$r")
  done <<EOF
$pairs
EOF
  mode=(--verify-no-changes)
  [ "$fix" = 1 ] && mode=()
  "${runner[@]}" format "$csproj" ${mode[@]+"${mode[@]}"} --include "${inc[@]}" > "$out_file" 2>&1 < /dev/null
  rc=$?
  cat "$out_file"
  errs="$(grep -c ' error ' "$out_file" || true)"
  echo "format $csproj: ${#inc[@]} files checked, $errs errors"
  if [ "$rc" != 0 ] || [ "$errs" != 0 ]; then status=1; fi
done <<EOF
$csprojs
EOF
exit "$status"
