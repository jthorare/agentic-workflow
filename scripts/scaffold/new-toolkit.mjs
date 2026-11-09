#!/usr/bin/env node
// scripts/scaffold/new-toolkit.mjs — the /new-toolkit generator (docs/csharp/scaffolds/new-toolkit.md).
//
//   node scripts/scaffold/new-toolkit.mjs --name <Name> --tool <snake_case> [--dir DIR] [--needs a,b,...]
//        [--feed DIR] [--root REPO] [--files-only]
//
// Creates a toolkit project outside the repo (default $AW_SINDRI_HOME/toolkits/<name>/): the project, Program.cs, one
// sample tool, a test project with one red test and its own build rails (Directory.Build.props, Directory.Packages.props,
// .editorconfig, global.json, nuget.config with the local SDK folder feed). Restores, builds with warnings as errors and
// runs the unit lane (exactly one failure: the sample tool's test) under the heavy-job lock; reverts every file it wrote
// if a step fails. --files-only writes the files and skips the dotnet steps (the generator's unit tests; never on a real toolkit).
// --needs: database:<name>, disk:<size> or disk:<name>:<size>, connection:<service>:<name>, comma separated.
// --root is the agentic-workflow checkout whose global.json and Directory.Packages.props it copies (default: this script's repo).
// Exit codes: 0 done (only the sample test is red), 1 refused or a step failed, 2 usage error.

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Journal, ScaffoldError, UsageError, trapSignals, list, kebab, parseArgs, withHeavyLock } from "./lib/common.mjs";
import * as T from "./lib/toolkit-templates.mjs";
import { build, expectOnlyRed, formatClean, mustPass } from "./lib/checks.mjs";

const KNOWN = ["name", "tool", "dir", "needs", "feed", "root"];
const RESERVED = ["Common", "Shared", "Utils", "Helpers"];
const HERE = path.dirname(fileURLToPath(import.meta.url));
export const SDK_VERSION = "0.1.0-local.*";   // scripts/install-cs.sh packs the SDK as 0.1.0-local.<stamp>

const sindriHome = (env = process.env) => env.AW_SINDRI_HOME || path.join(env.AW_STATE_DIR || path.join(os.homedir(), ".agentic-workflow"), "sindri-app");
export const defaultFeed = (env = process.env) => env.AW_SINDRI_SDK_FEED || path.join(sindriHome(env), "sdk", "packages");

const pascal = (snake) => snake.split("_").map((w) => w[0].toUpperCase() + w.slice(1)).join("");

/** Parses --needs into [{ kind, name, size?, service? }]; a bad entry is NT-NEEDS. */
export function parseNeeds(text) {
  const bad = (entry, why) => new ScaffoldError("NT-NEEDS", `need '${entry}': ${why}`, "use database:<name>, disk:<size> (or disk:<name>:<size>) or connection:<service>:<name>; names are [a-z0-9-], sizes like 1g or 500m");
  const nameOk = (s) => /^[a-z0-9][a-z0-9-]{0,30}$/.test(s ?? "");
  const needs = [];
  for (const entry of list(text)) {
    const [kind, ...rest] = entry.split(":");
    let need;
    if (kind === "database" && rest.length === 1 && nameOk(rest[0])) need = { kind, name: rest[0] };
    else if (kind === "disk" && rest.length === 1 && /^[1-9][0-9]*[kmgt](i?b)?$/i.test(rest[0])) need = { kind, name: "data", size: rest[0] };
    else if (kind === "disk" && rest.length === 2 && nameOk(rest[0]) && /^[1-9][0-9]*[kmgt](i?b)?$/i.test(rest[1])) need = { kind, name: rest[0], size: rest[1] };
    else if (kind === "connection" && rest.length === 2 && nameOk(rest[0]) && nameOk(rest[1])) need = { kind, service: rest[0], name: rest[1] };
    else if (!["database", "disk", "connection"].includes(kind)) throw bad(entry, "unknown kind");
    else throw bad(entry, "malformed");
    if (needs.some((n) => n.kind === need.kind && n.name === need.name)) throw bad(entry, `${need.kind} '${need.name}' is listed twice`);
    needs.push(need);
  }
  return needs;
}

function pinsFrom(root) {
  const file = path.join(root, "Directory.Packages.props");
  const text = fs.existsSync(file) ? fs.readFileSync(file, "utf8") : "";
  const global = (id) => text.match(new RegExp(`<GlobalPackageReference Include="${id.replace(/\./g, "\\.")}" Version="([^"]+)"`))?.[1];
  const pkg = (id) => text.match(new RegExp(`<PackageVersion Include="${id.replace(/\./g, "\\.")}" Version="([^"]+)"`))?.[1];
  const analyzers = ["Microsoft.CodeAnalysis.NetAnalyzers", "Meziantou.Analyzer", "Roslynator.Analyzers"].map((id) => ({ id, version: global(id) }));
  const xunit = { id: "xunit.v3", version: pkg("xunit.v3") };
  const missing = [...analyzers, xunit].filter((p) => !p.version).map((p) => p.id);
  if (missing.length) throw new ScaffoldError("NT-PINS", `${file} has no pin for ${missing.join(", ")}`, "run /new-toolkit from a checkout of agentic-workflow, or pass --root <checkout>");
  return { analyzers, xunit };
}

function sdkVersionOf(root) {
  const file = path.join(root, "global.json");
  const v = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, "utf8")).sdk?.version : null;
  if (!v) throw new ScaffoldError("NT-PINS", `${file} has no sdk.version`, "run /new-toolkit from a checkout of agentic-workflow, or pass --root <checkout>");
  return v;
}

/** Validates the input and returns the resolved toolkit record. Writes nothing. */
export function resolveToolkit(input, { filesOnly = false, env = process.env } = {}) {
  const name = input.name;
  if (!name) throw new ScaffoldError("NT-INPUT", "--name is required", "ask the user for the toolkit name");
  if (!/^[A-Z][A-Za-z0-9]{2,30}$/.test(name) || RESERVED.includes(name)) throw new ScaffoldError("NT-NAME", `'${name}' must be PascalCase, match ^[A-Z][A-Za-z0-9]{2,30}$ and not be ${RESERVED.join("/")}`, "pass e.g. --name Contracts");
  const tool = input.tool;
  if (!tool) throw new ScaffoldError("NT-INPUT", "--tool is required", "ask the user for the first tool's name, snake_case, e.g. diff_contracts");
  if (!/^[a-z][a-z0-9_]{2,40}$/.test(tool) || tool.endsWith("_") || tool.includes("__")) throw new ScaffoldError("NT-TOOL", `'${tool}' is not a snake_case tool name`, "pass e.g. --tool diff_contracts");
  const needs = parseNeeds(input.needs);

  const root = path.resolve(input.root ?? path.join(HERE, "..", ".."));
  const dir = path.resolve(input.dir ?? path.join(sindriHome(env), "toolkits", kebab(name)));
  if (fs.existsSync(dir) && (!fs.statSync(dir).isDirectory() || fs.readdirSync(dir).length > 0)) throw new ScaffoldError("NT-EXISTS", `${dir} exists and is not empty`, "pick another --dir or --name; the generator never overwrites");

  const feed = path.resolve(input.feed ?? defaultFeed(env));
  if (!filesOnly && !(fs.existsSync(feed) && fs.readdirSync(feed).some((f) => /^Aw\.Sindri\.Toolkit\..+\.nupkg$/.test(f)))) {
    throw new ScaffoldError("NT-SDK", `no Aw.Sindri.Toolkit package in the SDK feed ${feed}`, "run scripts/install-cs.sh (it packs the SDK there), or set AW_SINDRI_SDK_FEED");
  }
  return { name, kebab: kebab(name), tool, toolPascal: pascal(tool), needs, dir, feed, root, sdkVersion: SDK_VERSION, globalSdk: sdkVersionOf(root), pins: pinsFrom(root) };
}

/** Writes every file (no dotnet steps). Paths are relative to the journal root, which is `t.dir`'s nearest existing ancestor. */
export function writeToolkit(journal, t, rel) {
  const at = (...p) => path.join(rel, ...p);
  const tests = `${t.name}.Toolkit.Tests`;
  journal.create(at("global.json"), T.globalJson(t.globalSdk));
  journal.create(at("nuget.config"), T.nugetConfig(t.feed));
  journal.create(at("Directory.Build.props"), T.directoryBuildProps());
  journal.create(at("Directory.Packages.props"), T.directoryPackagesProps(t));
  journal.create(at(".editorconfig"), T.editorconfig());
  journal.create(at(".gitignore"), T.gitignore());
  journal.create(at(`${t.name}.Toolkit.csproj`), T.toolkitCsproj(t));
  journal.create(at("Program.cs"), T.program(t));
  journal.create(at(`${t.name}Tools.cs`), T.tools(t));
  journal.create(at(`${t.name}JsonContext.cs`), T.jsonContext(t));
  journal.create(at(tests, `${tests}.csproj`), T.testsCsproj(t));
  journal.create(at(tests, `${t.toolPascal}Tests.cs`), T.sampleTest(t));
}

/** Restore (writing the lock files), build with warnings as errors, the unit lane (one red test), format. Runs in t.dir, where global.json and nuget.config apply. */
export async function checkToolkit(journal, t, rel, log) {
  const toolkitCsproj = `${t.name}.Toolkit.csproj`;
  const testsCsproj = path.join(`${t.name}.Toolkit.Tests`, `${t.name}.Toolkit.Tests.csproj`);
  journal.watch(path.join(rel, "packages.lock.json"));
  journal.watch(path.join(rel, `${t.name}.Toolkit.Tests`, "packages.lock.json"));
  const ci = process.env.CI;
  delete process.env.CI;   // a new project has no lock file yet and locked mode would refuse its first restore
  try {
    for (const proj of [toolkitCsproj, testsCsproj]) {
      await mustPass(t.dir, "NT-RESTORE", "dotnet", ["restore", proj, "--force"], "the SDK package must be in the feed (scripts/install-cs.sh) and every other package pinned in Directory.Packages.props", log);
    }
    const files = journal.touched().filter((f) => f.endsWith(".cs")).map((f) => path.relative(rel, f));
    const scope = { testProjects: [testsCsproj], formatProjects: [testsCsproj, toolkitCsproj], files };
    await build(t.dir, scope, log);
    const lane = await expectOnlyRed(t.dir, scope, `${t.toolPascal}Tests.${t.toolPascal}_answers_the_subject_it_was_given`, log);
    await formatClean(t.dir, scope, log);
    return lane;
  } finally {
    if (ci !== undefined) process.env.CI = ci;
  }
}

export async function main(argv, { log = (line) => console.error(line), out = (line) => console.log(line) } = {}) {
  let input;
  try {
    input = parseArgs(argv, KNOWN, ["filesOnly"]);
  } catch (e) {
    if (e instanceof UsageError) {
      log(`new-toolkit: ${e.message}`);
      return 2;
    }
    throw e;
  }
  let journal = null;
  const untrap = trapSignals(log);
  try {
    const t = resolveToolkit(input, { filesOnly: Boolean(input.filesOnly) });
    // The journal root is the nearest ancestor that exists, so a revert also removes the folders the run made.
    let base = t.dir;
    while (!fs.existsSync(base)) base = path.dirname(base);
    journal = new Journal(base);
    const rel = path.relative(base, t.dir) || ".";
    if (rel !== ".") journal.mkdirs(rel);
    writeToolkit(journal, t, rel);
    if (!input.filesOnly) {
      await withHeavyLock(async () => checkToolkit(journal, t, rel, log));
    }
    const files = journal.touched();
    out(`new-toolkit: ${t.name} (tool ${t.tool}, ${t.needs.length} needs) — ${files.length} files in ${t.dir}`);
    for (const f of files) out(`  ${f}`);
    out(`Make ${t.toolPascal}Tests pass: replace the stub in ${t.name}Tools.cs, then add tools with .Tool<Req,Res>(...) (the toolkit prints its manifest with \`dotnet run -- --manifest\`).`);
    return 0;
  } catch (e) {
    const count = journal ? journal.touched().length : 0;
    journal?.revert();
    const err = e instanceof ScaffoldError ? e : new ScaffoldError("SCAFFOLD-INTERNAL", e.message, "a generator bug: the files were reverted; report the message");
    log(err.message);
    if (err.fix) log(`fix: ${err.fix}`);
    if (count) log(`reverted ${count} files`);
    return 1;
  } finally {
    untrap();
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = await main(process.argv.slice(2));
}
