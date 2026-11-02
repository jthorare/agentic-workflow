#!/usr/bin/env bash
# A-8 (UX pass 2026-10-25): Home's Workflows strip truncates titles, so two rows both read 'Fixture request for ...'.
# Expected: the strip's rows can be told apart at 1280 wide: each title button is wide enough to show its subject (not cut to
# ~14 characters), and the titles that share a prefix differ in what is shown.
source "$(dirname "$0")/lib.sh"

dt() { timeout 120 "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" "$@"; }

uxu_start up recipes workflows "incident start"

dt click --type ListBoxItem --name Home >/dev/null || uxu_die "no Home tab"
dt wait-for --contains "Fixture request for" --state visible --wait-ms 30000 >/dev/null || uxu_die "the strip never showed a 'Fixture request for' row"
sleep 3

# The strip's title buttons: their name is the subject and the recipe ("Fixture request for X, Dev Desk Round Trip").
title_buttons() { dt search --type Button --limit 200 | jq -c '[.matches[] | select(.visible) | select((.name // "") | test(", Dev "))]'; }

# Every instance the fixture started (4) is accounted for on the strip: a titled row, or the "... N more" line the strip shows
# for the rows its fold budget leaves out (8 less the Needs-you rows shown, so 3 or 4 rows here). This only stops a fix that
# passes by hiding rows. Retried for up to 30 s, since the strip and its Needs-you rows land a little after each other.
more_count() { dt search --type Button --limit 200 | jq '[.matches[] | select(.visible) | (.name // "") | select(test("^… [0-9]+ more$")) | capture("(?<n>[0-9]+)").n | tonumber] | add // 0'; }
accounted=""
for _ in $(seq 1 15); do
  rows="$(title_buttons | jq 'length')"; more="$(more_count)"
  accounted=$((rows + more))
  [ "$accounted" -ge 4 ] && break
  sleep 2
done

strip_accounts_for_all_instances() { [ "$accounted" -eq 4 ] || { echo "strip shows $rows titled rows + $more more = $accounted, want 4"; return 1; }; }
strip_titles_are_wide() { title_buttons | jq -e 'length > 0 and all(.[]; .width >= 240)'; }

expect "the 4 started instances are all on the strip (titled rows plus the \"more\" line)" strip_accounts_for_all_instances
expect "every strip title button is at least 240 px wide at 1280 (today about 140)" strip_titles_are_wide

uxu_done
