#!/usr/bin/env bash
# Workspace for the bootstrap cases: a small TypeScript recipe API with a stub README, no AGENTS.md, no .agents/rules, no
# .serena. One planning doc already exists (planning/TESTING.md) with a marker line that must survive a run.
# File names are chosen so the skill's doc audit finds exactly one doc (TESTING) and 16 missing: no eslint/lint file, no
# schema/setup/ci/deploy-named file, and no README heading that matches a doc keyword.
# Offline, no remote. Writes only under $PWD.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
mkdir -p src/routes planning
cat > package.json <<'P'
{
  "name": "recipe-api",
  "version": "0.3.0",
  "private": true,
  "scripts": { "dev": "tsx src/index.ts", "test": "vitest run" },
  "dependencies": { "express": "^4.19.2", "better-sqlite3": "^11.1.2" },
  "devDependencies": { "typescript": "^5.5.0", "vitest": "^2.0.0", "tsx": "^4.16.0" }
}
P
cat > tsconfig.json <<'P'
{ "compilerOptions": { "target": "ES2022", "module": "NodeNext", "strict": true, "outDir": "dist" }, "include": ["src"] }
P
printf 'node_modules/\ndist/\n' > .gitignore
cat > README.md <<'P'
# recipe-api

Small HTTP API for storing and fetching recipes.
P
cat > src/index.ts <<'P'
import express from "express";
import { recipesRouter } from "./routes/recipes.js";

const app = express();
app.use(express.json());
app.use("/recipes", recipesRouter);
app.listen(3000);
P
cat > src/db.ts <<'P'
import Database from "better-sqlite3";

export const db = new Database("recipes.db");
db.exec(`CREATE TABLE IF NOT EXISTS recipes (
  id INTEGER PRIMARY KEY,
  title TEXT NOT NULL,
  servings INTEGER NOT NULL DEFAULT 2,
  steps TEXT NOT NULL
)`);
P
cat > src/routes/recipes.ts <<'P'
import { Router } from "express";
import { db } from "../db.js";

export const recipesRouter = Router();

recipesRouter.get("/", (_req, res) => {
  res.json(db.prepare("SELECT id, title, servings FROM recipes").all());
});

recipesRouter.post("/", (req, res) => {
  const { title, servings = 2, steps } = req.body;
  if (!title || !steps) return res.status(400).json({ error: "title and steps are required" });
  const info = db.prepare("INSERT INTO recipes (title, servings, steps) VALUES (?, ?, ?)").run(title, servings, steps);
  res.status(201).json({ id: info.lastInsertRowid });
});
P
cat > src/recipes.test.ts <<'P'
import { describe, it, expect } from "vitest";

describe("recipes", () => {
  it("rejects a recipe without steps", () => {
    expect(true).toBe(true);
  });
});
P
cat > planning/TESTING.md <<'P'
# Testing

MARKER-KEEP-7f3a: hand-written by the team, do not regenerate.

## Coverage targets

Route handlers 80%, db layer 60%.
P
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial recipe api"
