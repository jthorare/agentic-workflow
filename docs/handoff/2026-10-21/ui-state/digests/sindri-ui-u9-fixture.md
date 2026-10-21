# T-F: sindri-ui-fixture.sh context verb
Branch feat/sindri-ui-u9-fixture, head 5a8f8f925 (pushed, no PR). Commits: d74786433 feat (script), 5a8f8f925 test.
Usage (AW_SINDRI_HOME guarded, run after `up`): context up | context draft | context down
- up: git repo aw-split-panes (fixed author/date, 1 commit) under $AW_SINDRI_HOME/ui-fixture-context; added to the live profile's repos: and approved; map-tabs.json copied there and imported as the owner typing "import map <sha256 first 6>" under the human pty; prints "aw-split-panes".
- draft: `context map build` as the owner (job, followed).
- down: removes the dir, the profile entry, re-approves; idempotent.
Gate: sindri-ui-fixture.test.sh 158 ok, 0 not ok; tmux-isolation test + ci script clean; check-env-example ok.
Not run: real C# binary end to end. Assumption: the import challenge hash6 is sha256 of the file bytes (MapFileReader); unverified against a live server.
