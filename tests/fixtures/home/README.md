# Home fixture rows

`rows.json` holds the six rows of the worked example in the Home spec (`docs/superpowers/specs/2026-11-12-sindri-home-inbox-design.md` §4.2) at 09:10 on 2026-11-12.
The `rank` values are written by hand from that table: blocks 3, 2, 2, 2, 1, 0; `position` is the order the spec gives (meta, review 3120, ready 3099, ideas, ready 3101, the DM).
The API goldens (`Golden/Api/home*`) and the `home` mode of `scripts/sindri-ui-fixture.sh` read this file.
