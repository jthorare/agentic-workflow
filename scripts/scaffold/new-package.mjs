#!/usr/bin/env node
// scripts/scaffold/new-package.mjs — the /new-package generator (docs/csharp/scaffolds/new-package.md).
//
//   node scripts/scaffold/new-package.mjs --name <Name> --tier <1-3> --error-prefix <PREFIX> --shape small|full
//        --host cli|http|none --first-slice <Slice> --behavior <snake_case> [--note "<one line>"] [--depends A,B]
//        [--binary <exe name>] [--command <name>] [--route "<METHOD> /path"] [--ids Name:prim,...] [--ports I...]
//        [--root DIR] [--files-only]
//
// Creates a package that passes every check on its first commit, with one red test: the projects, their public API
// files, the error catalogue, the registrations, the composition root and DI validation test, the deps.yaml node and
// edges, the solution entries and the architecture-test registration; then /new-slice's first slice. Runs the
// checks in the spec's order under the heavy-job lock and reverts every file it wrote if one fails. --files-only
// writes the files and skips the checks (the generator's own unit tests use it; never on a real package).
// Exit codes: 0 done (only the first slice's test is red), 1 refused or a check failed, 2 usage error.

import path from "node:path";
import { fileURLToPath } from "node:url";
import { Journal, ScaffoldError, UsageError, trapSignals, list, lower, parseArgs, withHeavyLock } from "./lib/common.mjs";
import * as P from "./lib/package-templates.mjs";
import * as R from "./lib/repo.mjs";
import { build, checkScope, declarePublicApi, depsClean, expectOnlyRed, formatClean, mustPass, noStrayChanges, strayChanges } from "./lib/checks.mjs";
import { resolveSlice, writeSlice, nextSteps } from "./new-slice.mjs";

const KNOWN = ["name", "tier", "errorPrefix", "note", "shape", "host", "depends", "firstSlice", "behavior", "binary", "command", "route", "ids", "ports", "root"];
const RESERVED = ["Common", "Shared", "Utils", "Helpers"];
const REQUIRED = ["name", "tier", "errorPrefix", "shape", "host", "firstSlice", "behavior"];

/** Validates the inputs against the repo (refusing with the spec's rule ids) and returns the resolved package. */
export function resolvePackage(journal, input) {
  const missing = REQUIRED.filter((k) => input[k] === undefined);
  if (missing.length) throw new ScaffoldError("NP-INPUT", `missing ${missing.map((k) => `--${k.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)}`).join(", ")}`, "ask the user for each missing input; never guess a tier or a prefix");
  const { name, errorPrefix, shape, host } = input;
  if (!/^[A-Z][A-Za-z0-9]{2,30}$/.test(name) || RESERVED.includes(name)) throw new ScaffoldError("NP-NAME", `'${name}' must match ^[A-Z][A-Za-z0-9]{2,30}$ and not be ${RESERVED.join(", ")}`, "pick a PascalCase domain name");
  for (const dir of [`src/${name}`, `tests/Aw.${name}.Tests`, `tests/Aw.${name}.Testing`]) {
    if (journal.exists(dir)) throw new ScaffoldError("NP-EXISTS", `${dir} already exists`, "pick another name; to add a use case to an existing package, run /new-slice");
  }
  if (!["small", "full"].includes(shape)) throw new ScaffoldError("NP-INPUT", `--shape '${shape}' is not small or full`, "small unless the package is expected over ~1,500 lines");
  if (!["cli", "http", "none"].includes(host)) throw new ScaffoldError("NP-INPUT", `--host '${host}' is not cli, http or none`, "pick the package's entry point");
  if (shape === "small" && host === "http") throw new ScaffoldError("NP-SMALL-HTTP", "shape small with host http: the Web SDK's generated public Program would land in App, a library with RS0016 on", "use --shape full for an http package");
  if (shape === "small" && host === "none") throw new ScaffoldError("NP-SMALL-NONE", "shape small with host none: nothing outside the package references App, so an App with no entry point can't be composed (its internal Add…() methods are never called)", "use --shape full for a library package, or --host cli");

  const tier = Number(input.tier);
  if (!/^[0-3]$/.test(String(input.tier))) throw new ScaffoldError("NP-TIER", `--tier '${input.tier}' is not 0–3`, "read the tier from deps.yaml");
  if (tier === 0) throw new ScaffoldError("NP-TIER", "tier 0 is reserved for Aw.Kernel*, Redaction and Analyzers", "a new package is tier 1–3");
  const deps = R.readDeps(journal);
  const declared = R.nodeOf(deps, name);
  if (declared) {
    const wrong = [];
    if (declared.tier !== tier) wrong.push(`tier ${declared.tier} (input ${tier})`);
    if (declared.src !== `src/${name}`) wrong.push(`src ${declared.src} (expected src/${name})`);
    if (declared.projectPrefix !== `Aw.${name}`) wrong.push(`projectPrefix ${declared.projectPrefix} (expected Aw.${name})`);
    if (declared.kind !== "package") wrong.push(`kind ${declared.kind}`);
    if (wrong.length) throw new ScaffoldError("NP-DEPS-MISMATCH", `deps.yaml declares ${name} with ${wrong.join(", ")}`, "deps.yaml is the tier table of record: fix the input, or change deps.yaml in its own reviewed commit");
  } else if (!input.note) {
    throw new ScaffoldError("NP-INPUT", `${name} isn't declared in deps.yaml, so --note is required`, "ask the user for a one-line note: what the package is for");
  }

  if (!/^[A-Z0-9]{2,16}$/.test(errorPrefix)) throw new ScaffoldError("NP-PREFIX", `'${errorPrefix}' must match ^[A-Z0-9]{2,16}$`, "ask the user for the prefix; codes are AW-<prefix>-NNN");
  const inUse = R.errorPrefixesInUse(journal.root);
  if (inUse.has(errorPrefix) || errorPrefix === "KERNEL") throw new ScaffoldError("NP-PREFIX", `AW-${errorPrefix}- is taken (${inUse.get(errorPrefix) ?? "Aw.Kernel"})`, "ask the user for another prefix; never derive one");

  const depends = list(input.depends);
  const repeated = depends.find((d, i) => depends.indexOf(d) !== i);
  if (repeated) throw new ScaffoldError("NP-DEPENDS", `${repeated} is listed twice`, "list each package once");
  // Features (full) or App (small) holds Core, Aw.Kernel and one Core per depends entry; a small App is also the host, so
  // it references Aw.Kernel.Telemetry too (a full Host is exempt from fan-out).
  const telemetryRef = shape === "small" && P.startsTelemetry({ host }) ? 1 : 0;
  const refs = 2 + telemetryRef + depends.length;
  if (refs > 4) throw new ScaffoldError("NP-FANOUT", `${shape === "small" ? "App" : "Features"} would hold ${refs} in-repo references (Core, Aw.Kernel${telemetryRef ? ", Aw.Kernel.Telemetry" : ""} and ${depends.length} depends); the limit is 4`, "depend on fewer packages, or reach one through its CLI");
  for (const d of depends) {
    const node = R.nodeOf(deps, d);
    if (!node || node.kind !== "package") throw new ScaffoldError("NP-DEPENDS", `'${d}' is not a package in deps.yaml`, "depend only on declared packages");
    if (!(node.tier < tier)) throw new ScaffoldError("NP-DEPENDS", `${d} is tier ${node.tier}, not below ${name}'s tier ${tier}`, "project edges point strictly down the tiers; use a CLI or HTTP contract instead");
    if (node.tier === 0) throw new ScaffoldError("NP-DEPENDS", `${d} is a tier-0 package; Features reference only another package's Core`, "add tier-0 libraries by hand to the project that needs them, with a deps.yaml edge");
    if (!journal.exists(`src/${d}/Aw.${d}.Core/Aw.${d}.Core.csproj`)) throw new ScaffoldError("NP-DEPENDS", `${d} is declared but not ported yet (no src/${d}/Aw.${d}.Core)`, `port ${d} first, or reach it through its CLI until then`);
  }

  const missingPins = R.missingPins(journal, host);
  if (missingPins.length) {
    throw new ScaffoldError(
      "NP-PINS",
      `Directory.Packages.props has no pin for ${missingPins.join(", ")}`,
      `make the pin commit first: add a <PackageVersion Include="…" Version="[x.y.z]" /> per package (at least 14 days old), run \`dotnet restore AgenticWorkflow.slnx\` (unlocked) and commit the lock files it changes with the pins, then re-run /new-package`,
    );
  }

  const binary = input.binary ?? lower(name);
  if (!/^[a-z][a-z0-9-]{1,40}$/.test(binary)) throw new ScaffoldError("NP-INPUT", `--binary '${binary}' is not a lowercase executable name`, "e.g. aw-readiness");

  return { name, lower: lower(name), binary, tier, errorPrefix, note: input.note ?? declared.note, shape, host, depends, declared: Boolean(declared) };
}

/** The production and test projects, as repo-relative paths. */
export function projectsOf(p) {
  const src = (layer) => ({ name: `Aw.${p.name}.${layer}`, path: `src/${p.name}/Aw.${p.name}.${layer}/Aw.${p.name}.${layer}.csproj`, layer });
  const prod = p.shape === "small" ? [src("Core"), src("App")] : [src("Core"), src("Features"), src("Infrastructure"), ...(p.host === "none" ? [] : [src("Host")])];
  const tests = ["Tests", "Testing"].map((t) => ({ name: `Aw.${p.name}.${t}`, path: `tests/Aw.${p.name}.${t}/Aw.${p.name}.${t}.csproj`, layer: t }));
  return { prod, tests };
}

const API_HEADER = "#nullable enable\n";

/** Writes every package file and edit (no checks, no slice). */
export function writePackage(journal, p) {
  const { prod, tests } = projectsOf(p);
  const dir = (layer) => `src/${p.name}/Aw.${p.name}.${layer}`;
  const api = (layer) => {
    journal.create(`${dir(layer)}/PublicAPI.Shipped.txt`, API_HEADER);
    journal.create(`${dir(layer)}/PublicAPI.Unshipped.txt`, API_HEADER);
  };

  journal.create(`${dir("Core")}/Aw.${p.name}.Core.csproj`, P.coreCsproj(p));
  api("Core");
  journal.create(`${dir("Core")}/VogenDefaults.cs`, P.vogenDefaults(p));
  journal.create(`${dir("Core")}/${p.name}Errors.cs`, P.errors(p));
  journal.create(`${dir("Core")}/${p.name}CoreServiceCollectionExtensions.cs`, P.coreRegistration(p));

  let rootNs = null;
  if (p.shape === "full") {
    journal.create(`${dir("Features")}/Aw.${p.name}.Features.csproj`, P.featuresCsproj(p));
    api("Features");
    journal.create(`${dir("Features")}/${p.name}FeaturesServiceCollectionExtensions.cs`, P.featuresRegistration(p, `Aw.${p.name}.Features`, "public"));

    const infra = dir("Infrastructure");
    journal.create(`${infra}/Aw.${p.name}.Infrastructure.csproj`, P.infrastructureCsproj(p));
    journal.create(`${infra}/.editorconfig`, P.boundaryEditorconfig("Boundary adapters"));
    api("Infrastructure");
    journal.create(`${infra}/${p.name}Settings.cs`, P.settings(p, `Aw.${p.name}.Infrastructure`));
    journal.create(`${infra}/SettingsFromEnvironment.cs`, P.settingsFromEnvironment(p, `Aw.${p.name}.Infrastructure`));
    journal.create(`${infra}/${p.name}InfrastructureServiceCollectionExtensions.cs`, P.infrastructureRegistration(p, `Aw.${p.name}.Infrastructure`, "public"));

    if (p.host !== "none") {
      rootNs = `Aw.${p.name}.Host`;
      const host = dir("Host");
      journal.create(`${host}/Aw.${p.name}.Host.csproj`, P.hostCsproj(p));
      journal.create(`${host}/.editorconfig`, P.boundaryEditorconfig("The composition root"));
      journal.create(`${host}/Composition.cs`, P.composition(p, rootNs, [`Aw.${p.name}.Core`, `Aw.${p.name}.Features`, `Aw.${p.name}.Infrastructure`], { webSdk: p.host === "http" }));
      journal.create(`${host}/${p.name}JsonContext.cs`, P.jsonContext(p, rootNs));
      if (p.host === "cli") {
        journal.create(`${host}/Program.cs`, P.programCli(p, rootNs));
        journal.create(`${host}/Commands/CommandTable.cs`, P.commandTable(p, rootNs));
        journal.create(`${host}/Commands/CommandOutput.cs`, P.commandOutput(p, rootNs));
      } else {
        journal.create(`${host}/Program.cs`, P.programHttp(p));
        journal.create(`${host}/LoopbackBinding.cs`, P.loopbackBinding(p));
        journal.create(`${host}/Pipeline.cs`, P.pipeline(p));
        journal.create(`${host}/LoopbackGuard.cs`, P.loopbackGuard(p));
        journal.create(`${host}/ValueObjectValidation.cs`, P.valueObjectValidation(p));
        journal.create(`${host}/HttpResults.cs`, P.httpResults(p));
        journal.create(`${host}/Endpoints/EndpointTable.cs`, P.endpointTable(p));
      }
    }
  } else {
    const app = dir("App");
    journal.create(`${app}/Aw.${p.name}.App.csproj`, P.appCsproj(p));
    journal.create(`${app}/.editorconfig`, P.smallEditorconfig());
    api("App");
    journal.create(`${app}/Features/${p.name}FeaturesServiceCollectionExtensions.cs`, P.featuresRegistration(p, `Aw.${p.name}.App.Features`, "internal"));
    journal.create(`${app}/Infrastructure/${p.name}Settings.cs`, P.settings(p, `Aw.${p.name}.App.Infrastructure`));
    journal.create(`${app}/Infrastructure/SettingsFromEnvironment.cs`, P.settingsFromEnvironment(p, `Aw.${p.name}.App.Infrastructure`));
    journal.create(`${app}/Infrastructure/${p.name}InfrastructureServiceCollectionExtensions.cs`, P.infrastructureRegistration(p, `Aw.${p.name}.App.Infrastructure`, "internal"));
    if (p.host === "cli") {
      rootNs = `Aw.${p.name}.App`;
      journal.create(`${app}/Composition.cs`, P.composition(p, rootNs, [`Aw.${p.name}.Core`, `Aw.${p.name}.App.Features`, `Aw.${p.name}.App.Infrastructure`], { webSdk: false }));
      journal.create(`${app}/${p.name}JsonContext.cs`, P.jsonContext(p, rootNs));
      journal.create(`${app}/Program.cs`, P.programCli(p, rootNs));
      journal.create(`${app}/Commands/CommandTable.cs`, P.commandTable(p, rootNs));
      journal.create(`${app}/Commands/CommandOutput.cs`, P.commandOutput(p, rootNs));
    }
  }

  const t = `tests/Aw.${p.name}.Tests`;
  journal.create(`${t}/Aw.${p.name}.Tests.csproj`, P.testsCsproj(p, prod.map((x) => x.path)));
  journal.create(`${t}/${p.name}ErrorsTests.cs`, P.errorsTests(p));
  if (rootNs) journal.create(`${t}/HostTests.cs`, P.hostTests(p, rootNs));
  if (p.host === "http") {
    journal.create(`${t}/ProcessEnvironment.cs`, P.loopbackCollection(p));
    journal.create(`${t}/LoopbackBindingTests.cs`, P.loopbackTests(p));
    journal.create(`${t}/HttpJsonProfileTests.cs`, P.httpJsonProfileTests(p));
  }
  const testing = `tests/Aw.${p.name}.Testing`;
  journal.create(`${testing}/Aw.${p.name}.Testing.csproj`, P.testingCsproj(p));
  journal.create(`${testing}/A.cs`, P.builderRoot(p));
  if (p.shape === "small") journal.create(`${testing}/.editorconfig`, P.smallTestingEditorconfig());

  R.addToSolution(journal, p.name, prod.map((x) => x.path), tests.map((x) => x.path));
  if (!p.declared) R.addDepsNode(journal, p);
  const edges = [R.ensureProjectEdge(journal, p.name, "Kernel", "Result, IService, ports (scaffolded by /new-package)")];
  if (P.startsTelemetry(p)) edges.push(R.ensureProjectEdge(journal, p.name, "KernelTelemetry", "the host starts Kernel telemetry in Compose (scaffolded by /new-package)"));
  for (const d of p.depends) edges.push(R.ensureProjectEdge(journal, p.name, d, `${p.name}'s Features reference ${d}'s Core (scaffolded by /new-package)`));
  R.registerWithArchitectureTests(journal, prod);
  return { prod, tests, edges };
}

/** The spec's command list, in order. Each lock file a restore writes is journalled first, so revert restores it. */
export async function checkPackage(journal, p, s, log) {
  const root = journal.root;
  const { prod, tests } = projectsOf(p);
  const restored = [...prod, ...tests, { path: R.ARCH_CSPROJ }];
  // Record every lock file first: restoring one project also writes the lock files of the projects it references.
  for (const proj of restored) journal.watch(path.join(path.dirname(proj.path), "packages.lock.json"));
  for (const proj of restored) {
    // --force: a restore no-op cache left by an earlier, reverted run would otherwise skip rewriting the lock file.
    await mustPass(root, "SCAFFOLD-RESTORE", "dotnet", ["restore", proj.path, "--force"], "a package the templates reference must be pinned in Directory.Packages.props", log);
  }
  await mustPass(root, "SCAFFOLD-RESTORE", "dotnet", ["restore", R.SLNX, "--locked-mode"], "no existing lock file may change here: a new pin belongs in its own commit (NP-PINS)", log);
  // Every library layer's public API, the first slice's included, in reference order (Core before Features).
  for (const proj of prod.filter((x) => ["Core", "Features", "Infrastructure"].includes(x.layer))) await declarePublicApi(journal, proj.path, log);
  const scope = checkScope(journal, p.name);
  await build(root, scope, log);
  const lane = await expectOnlyRed(root, scope, `${s.slice}Tests.${s.slice}_${s.behavior}`, log);
  await formatClean(root, scope, log);
  await depsClean(root, log);
  return lane;
}

export async function main(argv, { log = (line) => console.error(line), out = (line) => console.log(line) } = {}) {
  let input;
  try {
    input = parseArgs(argv, KNOWN, ["filesOnly"]);
  } catch (e) {
    if (e instanceof UsageError) {
      log(`new-package: ${e.message}`);
      return 2;
    }
    throw e;
  }
  const root = path.resolve(input.root ?? process.cwd());
  const journal = new Journal(root);
  const before = input.filesOnly ? [] : await strayChanges(journal);
  const untrap = trapSignals(log);
  try {
    const p = resolvePackage(journal, input);
    if (!/^[A-Z][A-Za-z0-9]{2,40}$/.test(input.firstSlice)) throw new ScaffoldError("NS-NAME", `'${input.firstSlice}' is not a PascalCase use case name`, "pass e.g. --first-slice CheckRepo");
    if (!/^[a-z][a-z0-9_]{2,100}$/.test(input.behavior)) throw new ScaffoldError("NS-BEHAVIOR", `'${input.behavior}' is not a snake_case behavior`, "pass e.g. --behavior reports_level_one_for_an_empty_repo");
    const written = writePackage(journal, p);
    const s = resolveSlice(journal, { package: p.name, slice: input.firstSlice, behavior: input.behavior, surface: p.host, command: input.command, route: input.route, ids: input.ids, ports: input.ports });
    writeSlice(journal, s);
    if (!input.filesOnly) {
      await withHeavyLock(async () => checkPackage(journal, p, s, log));
      await noStrayChanges(journal, before);
    }
    out(`new-package: ${p.name} (${p.shape}, host ${p.host}, tier ${p.tier}) — ${journal.touched().length} files`);
    for (const f of journal.touched()) out(`  ${f}`);
    out(`deps.yaml: ${p.declared ? "node already declared" : "node added"}; edges ${written.edges.join(", ")}`);
    out("architecture tests: the new projects are registered in tests/Aw.Architecture.Tests (Packages.cs and its csproj)");
    out(`Commit: test: ${p.lower} package scaffold with a red ${s.slice} test`);
    out(`Make ${s.slice}Tests pass: replace the Assert.Fail with the real assertion, add the ports the service needs, then implement it in Features/${s.slice}/ (see /new-slice).`);
    out(nextSteps(s));
    return 0;
  } catch (e) {
    const count = journal.touched().length;
    journal.revert();
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
