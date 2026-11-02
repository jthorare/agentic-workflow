#!/usr/bin/env bash
# Workspace for the verify-web cases: a small recipe site (Next.js) with a documented dev URL. Offline, nothing to install.
set -eu
AW_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/../_lib" && pwd)"
. "$AW_LIB/fixture-lib.sh"
mkdir -p app/recipes
cat > package.json <<'P'
{
  "name": "pantry-notes",
  "private": true,
  "scripts": { "dev": "next dev -p 3000", "start": "next start -p 3000" },
  "dependencies": { "next": "15.0.0", "react": "19.0.0", "react-dom": "19.0.0" }
}
P
cat > AGENTS.md <<'P'
# Pantry Notes
A recipe site. Dev server: http://localhost:3000 (already running, seeded with two recipes).
P
cat > app/recipes/page.tsx <<'P'
export default function Recipes() {
  return <main><h1>Recipes</h1><button>Add recipe</button></main>;
}
P
aw_fixture_repo
