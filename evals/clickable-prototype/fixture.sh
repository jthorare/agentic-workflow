#!/usr/bin/env bash
# Offline fixture for clickable-prototype. Writes only under $PWD; no URLs, no server.
set -euo pipefail
printf '{"name":"recipe-box","private":true,"scripts":{"dev":"vite"},"dependencies":{"react":"18.3.0"},"devDependencies":{"vite":"5.4.0","playwright":"1.47.0"}}\n' > package.json
mkdir -p src/routes && printf 'export default function Recipes() { return <main data-testid="recipes" /> }\n' > src/routes/Recipes.tsx
printf '# recipe-box\n\nRun locally: `npm run dev` (http://localhost:5173). Seed: `npm run seed`. Login: cook@example.test, password in `.env.local`.\n' > AGENTS.md
