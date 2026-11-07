#!/usr/bin/env bash
# Every tracked .cs file is free of control bytes other than tab, LF and CR (#2203): 0x00-0x08, 0x0B, 0x0C, 0x0E-0x1F.
# A raw 0x08 in a verbatim regex (where `\b` was meant) built clean and silently never matched; nothing else catches it.
# Usage: scripts/check-cs-control-bytes.sh [REPO_DIR]    (default: this script's repo root)
# Exit: 0 ok, 1 a control byte (one `file:line: control byte 0xNN` line per byte on stderr), 2 usage.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO="${1:-$ROOT}"
[ "$#" -le 1 ] || { echo "usage: check-cs-control-bytes.sh [REPO_DIR]" >&2; exit 2; }
git -C "$REPO" rev-parse --git-dir >/dev/null 2>&1 || { echo "check-cs-control-bytes: not a git checkout: $REPO" >&2; exit 2; }

# perl reads the bytes as they are (a NUL can't sit in a bash variable) and prints the byte as hex, never raw.
git -C "$REPO" ls-files -z -- '*.cs' | (cd "$REPO" && perl -0 -ne '
  chomp; my $f = $_;
  open(my $fh, "<:raw", $f) or next;
  local $/ = "\n";
  while (my $l = <$fh>) {
    while ($l =~ /([\x00-\x08\x0b\x0c\x0e-\x1f])/g) {
      printf STDERR "%s:%d: control byte 0x%02X\n", $f, $., ord($1);
      $bad = 1;
    }
  }
  close $fh; $. = 0;
  END { exit($bad ? 1 : 0) }
')
