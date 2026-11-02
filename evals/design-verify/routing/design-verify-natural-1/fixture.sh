#!/usr/bin/env bash
# Offline web fixture: two files, no server, no URLs.
set -euo pipefail
mkdir -p src
cat > package.json <<'EOF'
{ "name": "recipe-site", "private": true, "dependencies": { "react": "18.3.1", "vite": "5.4.0" } }
EOF
cat > src/PricingPage.tsx <<'EOF'
export function PricingPage() { return <main><h1>Plans</h1></main>; }
EOF
