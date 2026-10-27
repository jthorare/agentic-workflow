// scripts/scaffold/lib/repo.mjs — what the scaffolds read from and edit in the repo: deps.yaml (the tier table
// and edges of record), Directory.Packages.props (the pins), AgenticWorkflow.slnx, and the architecture-test
// registry (tests/Aw.Architecture.Tests: Packages.cs and its csproj).

import fs from "node:fs";
import path from "node:path";
import { parseYaml } from "../../deps/yaml.mjs";

export const DEPS = "deps.yaml";
export const PINS = "Directory.Packages.props";
export const SLNX = "AgenticWorkflow.slnx";
export const ARCH_DIR = "tests/Aw.Architecture.Tests";
export const ARCH_PACKAGES = `${ARCH_DIR}/Packages.cs`;
export const ARCH_CSPROJ = `${ARCH_DIR}/Aw.Architecture.Tests.csproj`;

export function readDeps(journal) {
  return parseYaml(journal.read(DEPS));
}

export const nodeOf = (deps, id) => (deps.packages ?? []).find((n) => n.id === id);

/** The package pins a scaffold needs, by role. */
export function requiredPins(host) {
  const pins = [
    "Microsoft.Extensions.DependencyInjection.Abstractions",
    "Microsoft.Extensions.DependencyInjection",
    "Microsoft.Extensions.TimeProvider.Testing",
    "Vogen",
    "xunit.v3",
  ];
  if (host === "http") pins.push("Microsoft.AspNetCore.Mvc.Testing");
  return pins;
}

export function missingPins(journal, host) {
  const props = journal.read(PINS);
  return requiredPins(host).filter((p) => !new RegExp(`<PackageVersion\\s+Include="${p.replace(/\./g, "\\.")}"`).test(props));
}

/** Every "AW-<PREFIX>-" literal in src/**\/*Errors.cs (the error prefixes in use). */
export function errorPrefixesInUse(root) {
  const found = new Map();
  const walk = (dir) => {
    if (!fs.existsSync(dir)) return;
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) {
        if (e.name !== "bin" && e.name !== "obj") walk(p);
      } else if (/Errors\.cs$/.test(e.name)) {
        for (const m of fs.readFileSync(p, "utf8").matchAll(/"AW-([A-Z0-9]+)-/g)) found.set(m[1], path.relative(root, p));
      }
    }
  };
  walk(path.join(root, "src"));
  return found;
}

const quote = (s) => `"${s.replace(/\\/g, "\\\\").replace(/"/g, '\\"')}"`;

/** Adds a package node to deps.yaml, at the end of the packages list (before the top-level `edges:` key). */
export function addDepsNode(journal, { name, tier, note }) {
  journal.edit(DEPS, (text) => {
    const at = text.search(/^edges:/m);
    if (at < 0) throw new Error("deps.yaml has no top-level 'edges:' key");
    const node = [
      `  - id: ${name}`,
      "    kind: package",
      `    tier: ${tier}`,
      `    projectPrefix: Aw.${name}`,
      `    src: src/${name}`,
      `    note: ${quote(note)}`,
      "",
      "",
    ].join("\n");
    const before = text.slice(0, at).replace(/\n*$/, "\n");
    return `${before}${node}${text.slice(at)}`;
  });
}

/**
 * Makes a project edge current: a planned edge flips to current, a missing one is added as current with a note.
 * Returns "flipped", "added" or "current" (already current).
 */
export function ensureProjectEdge(journal, from, to, note) {
  let outcome = "current";
  journal.edit(DEPS, (text) => {
    const block = new RegExp(`(^  - from: ${from}\\n    to: ${to}\\n    kind: project\\n    status: )(planned|current)`, "m");
    const m = text.match(block);
    if (m) {
      if (m[2] === "planned") outcome = "flipped";
      return text.replace(block, "$1current");
    }
    outcome = "added";
    const edge = [`  - from: ${from}`, `    to: ${to}`, "    kind: project", "    status: current", `    note: ${quote(note)}`, ""].join("\n");
    const at = text.search(/^exceptions:/m);
    if (at < 0) return `${text.replace(/\n*$/, "\n")}${edge}`;
    return `${text.slice(0, at).replace(/\n*$/, "\n")}${edge}\n${text.slice(at)}`;
  });
  return outcome;
}

/** Adds a solution folder for the package's projects (spec: "/src/<Name>/") and the test projects under "/tests/". */
export function addToSolution(journal, name, srcProjects, testProjects) {
  journal.edit(SLNX, (text) => {
    const srcFolder = [`  <Folder Name="/src/${name}/">`, ...srcProjects.map((p) => `    <Project Path="${p}" />`), "  </Folder>"].join("\n");
    let out = text.replace(/(<Folder Name="\/src\/">[\s\S]*?<\/Folder>\n)/, `$1${srcFolder}\n`);
    if (out === text) throw new Error(`${SLNX} has no "/src/" folder`);
    const tests = testProjects.map((p) => `    <Project Path="${p}" />\n`).join("");
    const next = out.replace(/(<Folder Name="\/tests\/">[\s\S]*?)(  <\/Folder>)/, `$1${tests}$2`);
    if (next === out) throw new Error(`${SLNX} has no "/tests/" folder`);
    return next;
  });
}

/** Registers production projects with the architecture tests (Packages.cs line + ProjectReference each). */
export function registerWithArchitectureTests(journal, projects) {
  journal.edit(ARCH_PACKAGES, (text) => {
    const m = text.match(/(Production \{ get; \} =\s*\[\n)([\s\S]*?)(\s*\];)/);
    if (!m) throw new Error(`${ARCH_PACKAGES}: no Production list`);
    const lines = projects.map((p) => `        "${p.name}",\n`).join("");
    return text.replace(m[0], `${m[1]}${m[2]}\n${lines.replace(/\n$/, "")}${m[3]}`);
  });
  journal.edit(ARCH_CSPROJ, (text) => {
    const m = text.match(/(<ItemGroup Label="Registered packages">[\s\S]*?)(  <\/ItemGroup>)/);
    if (!m) throw new Error(`${ARCH_CSPROJ}: no "Registered packages" item group`);
    const refs = projects.map((p) => `    <ProjectReference Include="../../${p.path}" />\n`).join("");
    return text.replace(m[0], `${m[1]}${refs}${m[2]}`);
  });
}

/**
 * What an existing package looks like: its shape (full or small) and host (cli, http or none), from its project
 * files. Returns null when src/<name>/ doesn't hold a scaffolded package.
 */
export function packageShape(journal, name) {
  const app = `src/${name}/Aw.${name}.App/Aw.${name}.App.csproj`;
  const host = `src/${name}/Aw.${name}.Host/Aw.${name}.Host.csproj`;
  const features = `src/${name}/Aw.${name}.Features/Aw.${name}.Features.csproj`;
  if (journal.exists(app)) {
    return { shape: "small", host: /<OutputType>Exe<\/OutputType>/.test(journal.read(app)) ? "cli" : "none" };
  }
  if (journal.exists(host)) {
    return { shape: "full", host: /Sdk="Microsoft\.NET\.Sdk\.Web"/.test(journal.read(host)) ? "http" : "cli" };
  }
  if (journal.exists(features)) return { shape: "full", host: "none" };
  return null;
}
