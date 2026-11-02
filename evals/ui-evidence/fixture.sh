#!/usr/bin/env bash
# Workspace for the ui-evidence cases: a small made-up shop web app whose settings page "changed". The stack itself is the
# stub `ui-evidence` binary (evals/stubs/bin/ui-evidence); nothing here runs.
set -eu
. "$(dirname "${BASH_SOURCE[0]}")/../_lib/fixture-lib.sh"
mkdir -p web/src
cat > README.md <<'Q'
# shop
A small web shop. Dev stack: `npm run dev` serves the app on http://localhost:3000 (seeded data).
Tests: `npm test`.
Q
cat > web/src/settings.html <<'Q'
<main>
  <h1>Settings</h1>
  <label>Display name <input id="display-name" name="displayName"></label>
  <label><input type="checkbox" id="email-updates"> Email me order updates</label>
  <button id="save">Save changes</button>
</main>
Q
cat > CHANGES.md <<'Q'
# Change under review (PR 57)
Settings page: added the "Email me order updates" checkbox next to the display name.
Q
aw_fixture_repo "shop"
