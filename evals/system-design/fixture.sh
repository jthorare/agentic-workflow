#!/usr/bin/env bash
# Workspace for the system-design forced case: a made-up repo for a ferry-booking desk with a design-tokens file,
# an AGENTS.md that documents no read access to data (so the status doc must say sizes are unknown), and a ticket file.
# No remote, writes only under $PWD.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
git init -q -b main
mkdir -p src/lookup docs
cat > AGENTS.md <<'MD'
# Ferry booking desk

A small web app for the harbour office: staff look up a booking by its reference and print a boarding slip.

- Code: `src/lookup/` (the lookup page and its API), `src/slip/` (slip printing).
- Style: `design-tokens.json` at the repo root.
- Tracker: none. Paste tickets by hand.
- Data: no read access is documented for this repo.
MD
cat > design-tokens.json <<'JSON'
{
  "color": {
    "background": { "$value": "#f6f1e7", "$type": "color" },
    "text": { "$value": "#1d2b36", "$type": "color" },
    "accent": { "$value": "{color.harbour}" },
    "harbour": { "$value": "#14586b" }
  },
  "font": { "family-body": { "$value": ["Inter", "Helvetica", "sans-serif"] } }
}
JSON
cat > docs/ticket-FERRY-31.md <<'MD'
# FERRY-31: Show a passenger's booking history on the lookup page

Staff at the desk look up a booking by reference. Today they see only that booking. They want the passenger's
earlier bookings (last 12 months) next to it, so they can spot repeat no-shows.

Notes from the harbour office: the desk PCs sit behind a slow link. Nothing else is decided yet.
MD
cat > src/lookup/lookup.ts <<'TS'
export async function findBooking(ref: string) {
  return db.one("select * from booking where ref = $1", [ref]);
}
TS
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial"
