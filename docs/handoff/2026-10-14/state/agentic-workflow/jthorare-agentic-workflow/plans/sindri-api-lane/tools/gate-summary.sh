#!/bin/bash
# usage: gate-summary.sh <logprefix>
P="$1"
grep -ohE '(error|warning) [A-Z]+[0-9]+: [^[]*' "$P-build.log" "$P-fmt.log" | sort -u | head
grep -hE '^\s*(total|failed):' "$P-test.log" "$P-arch.log"
grep -hE '^\s*failed ' "$P-test.log" "$P-arch.log" | sed 's/(.*//'
