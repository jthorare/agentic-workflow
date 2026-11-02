#!/usr/bin/env node
// scripts/scaffold/new-slice.mjs — the /new-slice generator (docs/csharp/scaffolds/new-slice.md).
//
//   node scripts/scaffold/new-slice.mjs --package <Pkg> --slice <Slice> --behavior <snake_case> --surface cli|http|none
//        [--command <name>] [--route "<METHOD> /path/{id}"] [--ids Name:string,Other:Guid] [--ports IItemStore,...]
//        [--root DIR] [--slice-only] [--files-only]
//
// Adds one use case to an existing package, red: the failing test that names the behavior, the request and response
// records, a parameterless service stub that throws NotSupportedException, the validator, the registration, the CLI
// command or endpoint, ids and ports with their fakes and contract bases. Then runs the checks (under the heavy-job
// lock) and reverts every file it wrote if one fails. --files-only writes the files and skips the checks (the
// generator's own unit tests use it; never use it on a real package).
// --slice-only is for slices scaffolded into one package in parallel branches: it writes the slice's own files, ids
// and ports, and edits only its own lines in A.cs, IdsJsonContext and PublicAPI.Unshipped.txt. It leaves out the
// registration, the command-table arm or endpoint-table call, the JSON context lines and the command or endpoint file,
// and prints them for the implementer to add by hand. Any other shared edit, any new shared file, and a missing A.cs
// or IdsJsonContext (when it adds an id) are refused (NS-SLICE-ONLY).
// Exit codes: 0 done (the new test is red, everything else green), 1 refused or a check failed, 2 usage error.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Journal, ScaffoldError, UsageError, trapSignals, camel, kebab, list, parseArgs, withHeavyLock } from "./lib/common.mjs";
import { builderRoot, byNamespace, usingsBlock } from "./lib/package-templates.mjs";
import { nodeOf, packageShape, readDeps } from "./lib/repo.mjs";
import * as T from "./lib/slice-templates.mjs";
import { build, checkScope, declarePublicApi, expectOnlyRed, formatClean, noStrayChanges, strayChanges } from "./lib/checks.mjs";

const PRIMS = ["string", "Guid", "int"];
const METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"];
export const KNOWN = ["package", "slice", "behavior", "surface", "command", "route", "ids", "ports", "root"];

/** Validates the inputs against the repo and returns the resolved slice (`s` in slice-templates.mjs). */
export function resolveSlice(journal, input) {
  const { package: pkg, slice, behavior, surface } = input;
  if (!pkg || !/^[A-Z][A-Za-z0-9]{2,30}$/.test(pkg)) throw new ScaffoldError("NS-PACKAGE", `'${pkg ?? ""}' is not a package name`, "pass --package <Name> of an existing src/<Name>/");
  const shape = packageShape(journal, pkg);
  const deps = readDeps(journal);
  if (!shape || !nodeOf(deps, pkg)) throw new ScaffoldError("NS-PACKAGE", `src/${pkg}/ is not a scaffolded package declared in deps.yaml`, "create it with /new-package first");
  if (!slice || !/^[A-Z][A-Za-z0-9]{2,40}$/.test(slice)) throw new ScaffoldError("NS-NAME", `'${slice ?? ""}' is not a PascalCase use case name (^[A-Z][A-Za-z0-9]{2,40}$)`, "pass a verb-first name such as CheckRepo");
  if (!behavior || !/^[a-z][a-z0-9_]{2,100}$/.test(behavior)) throw new ScaffoldError("NS-BEHAVIOR", `'${behavior ?? ""}' is not a snake_case behavior`, "pass e.g. reports_level_one_for_an_empty_repo");
  if (surface !== shape.host) throw new ScaffoldError("NS-SURFACE", `surface '${surface ?? ""}' doesn't match the package's host '${shape.host}'`, `pass --surface ${shape.host}`);

  const small = shape.shape === "small";
  const app = `src/${pkg}/Aw.${pkg}.App`;
  const featuresDir = small ? `${app}/Features/${slice}` : `src/${pkg}/Aw.${pkg}.Features/${slice}`;
  if (journal.exists(featuresDir)) throw new ScaffoldError("NS-EXISTS", `${featuresDir} already exists`, "pick another slice name, or extend the existing slice by hand");

  const coreDir = `src/${pkg}/Aw.${pkg}.Core`;
  const existingIds = new Map();
  if (journal.exists(`${coreDir}/Ids`)) {
    for (const f of fs.readdirSync(journal.abs(`${coreDir}/Ids`))) {
      const m = journal.read(`${coreDir}/Ids/${f}`).match(/\[ValueObject<(\w+)>/);
      if (m) existingIds.set(f.replace(/\.cs$/, ""), m[1]);
    }
  }
  const ids = list(input.ids).map((spec) => {
    const m = spec.match(/^([A-Z][A-Za-z0-9]{1,40}Id):(\w+)$/);
    if (!m || !PRIMS.includes(m[2])) throw new ScaffoldError("NS-IDS", `'${spec}' is not Name:primitive with a Name ending in Id and a primitive of ${PRIMS.join(", ")}`, "pass e.g. --ids ItemId:string");
    if (existingIds.has(m[1]) && existingIds.get(m[1]) !== m[2]) throw new ScaffoldError("NS-IDS", `${m[1]} already exists as a ${existingIds.get(m[1])} id`, `pass ${m[1]}:${existingIds.get(m[1])} or drop it (existing ids are reused)`);
    return { name: m[1], prim: m[2], isNew: !existingIds.has(m[1]) };
  });
  const dupe = (names) => names.find((n, i) => names.indexOf(n) !== i);
  if (dupe(ids.map((i) => i.name))) throw new ScaffoldError("NS-IDS", `${dupe(ids.map((i) => i.name))} is listed twice`, "list each id once");
  const known = new Map([...existingIds].map(([name, prim]) => [name, prim]));
  for (const id of ids) known.set(id.name, id.prim);

  let command = null;
  let method = null;
  let routePath = null;
  const routeIds = [];
  if (surface === "cli") {
    command = input.command ?? kebab(slice);
    if (!/^[a-z][a-z0-9-]{1,40}$/.test(command)) throw new ScaffoldError("NS-COMMAND", `'${command}' is not a kebab-case command name`, "pass e.g. --command check");
    const table = journal.read(small ? `${app}/Commands/CommandTable.cs` : `src/${pkg}/Aw.${pkg}.Host/Commands/CommandTable.cs`);
    const commandsDir = small ? `${app}/Commands` : `src/${pkg}/Aw.${pkg}.Host/Commands`;
    const taken = fs.readdirSync(journal.abs(commandsDir)).filter((f) => f.endsWith("Command.cs")).some((f) => journal.read(`${commandsDir}/${f}`).includes(`Name = "${command}"`));
    if (taken || table.includes(`"${command}"`)) throw new ScaffoldError("NS-COMMAND", `command '${command}' is already in the command table`, "pass another --command");
  }
  if (surface === "http") {
    const m = (input.route ?? "").match(/^([A-Z]+) (\/[A-Za-z0-9/{}._-]*)$/);
    if (!m || !METHODS.includes(m[1])) throw new ScaffoldError("NS-ROUTE", `'${input.route ?? ""}' is not "<METHOD> /path"`, 'pass e.g. --route "POST /items/{itemId}/start"');
    method = m[1];
    routePath = m[2].replace(/\{([A-Za-z][A-Za-z0-9]*)\}/g, (_, seg) => {
      const pascal = seg[0].toUpperCase() + seg.slice(1);
      const named = known.has(pascal) ? pascal : seg === "id" && ids.length === 1 ? ids[0].name : null;
      if (!named) throw new ScaffoldError("NS-ROUTE", `route segment {${seg}} names no typed id (known: ${[...known.keys()].join(", ") || "none"})`, `name it after an id ({itemId} for ItemId) and pass the id in --ids`);
      routeIds.push(named);
      return `{${camel(named)}}`;
    });
    if (dupe(routeIds)) throw new ScaffoldError("NS-ROUTE", `the route names ${dupe(routeIds)} twice`, "use each id once in a route");
    const loose = ids.filter((i) => !routeIds.includes(i.name)).map((i) => i.name);
    if (["GET", "DELETE"].includes(method) && loose.length) {
      throw new ScaffoldError("NS-ROUTE", `${method} has no body, and ${loose.join(", ")} ${loose.length > 1 ? "are" : "is"} not in the route`, `put {${camel(loose[0])}} in the route, or use POST`);
    }
  }

  const requestIds = [...ids.map(({ name, prim }) => ({ name, prim }))];
  for (const r of routeIds) if (!requestIds.some((i) => i.name === r)) requestIds.push({ name: r, prim: known.get(r) });

  const ports = list(input.ports);
  for (const port of ports) if (!/^I[A-Z][A-Za-z0-9]{2,40}$/.test(port)) throw new ScaffoldError("NS-PORTS", `'${port}' is not an interface name`, "pass e.g. --ports IItemStore");
  if (dupe(ports)) throw new ScaffoldError("NS-PORTS", `${dupe(ports)} is listed twice`, "list each port once");
  if (ports.length + 1 > 5) throw new ScaffoldError("NS-DEPS", `${ports.length + 1} constructor dependencies (ports + TimeProvider); the limit is 5`, "split the use case");
  // An existing port may sit anywhere in Core (new ones go in Core/Ports/); it is reused with its fake as they are.
  const coreFiles = journal.exists(coreDir) ? fs.readdirSync(journal.abs(coreDir), { recursive: true }).filter((f) => !/^(bin|obj)[\\/]/.test(f)) : [];
  const newPorts = ports.filter((port) => !coreFiles.some((f) => path.basename(f) === `${port}.cs`));
  for (const port of newPorts) {
    for (const rel of [`tests/Aw.${pkg}.Testing/InMemory${port.slice(1)}.cs`, `tests/Aw.${pkg}.Testing/${port.slice(1)}Contract.cs`]) {
      if (journal.exists(rel)) throw new ScaffoldError("NS-PORTS", `${port} is not in ${coreDir}, but ${rel} exists`, `add ${port} to ${coreDir}/Ports/ by hand (the generator then reuses it), or rename the stray file`);
    }
  }

  return {
    pkg,
    shape: shape.shape,
    host: shape.host,
    slice,
    behavior,
    surface,
    command,
    method,
    path: routePath,
    routeIds,
    ids,
    requestIds,
    ports,
    newPorts,
    small,
    visibility: small ? "internal" : "public",
    featuresNs: small ? `Aw.${pkg}.App.Features.${slice}` : `Aw.${pkg}.Features.${slice}`,
    commandsNs: small ? `Aw.${pkg}.App.Commands` : `Aw.${pkg}.Host.Commands`,
    testsRoot: `Aw.${pkg}.Tests`,
    testsNs: `Aw.${pkg}.Tests.${slice}`,
    dirs: {
      core: coreDir,
      features: featuresDir,
      registration: small ? `${app}/Features/${pkg}FeaturesServiceCollectionExtensions.cs` : `src/${pkg}/Aw.${pkg}.Features/${pkg}FeaturesServiceCollectionExtensions.cs`,
      commands: small ? `${app}/Commands` : `src/${pkg}/Aw.${pkg}.Host/Commands`,
      endpoints: `src/${pkg}/Aw.${pkg}.Host/Endpoints`,
      json: small ? `${app}/${pkg}JsonContext.cs` : `src/${pkg}/Aw.${pkg}.Host/${pkg}JsonContext.cs`,
      tests: `tests/Aw.${pkg}.Tests`,
      testing: `tests/Aw.${pkg}.Testing`,
      featuresProject: small ? null : `src/${pkg}/Aw.${pkg}.Features/Aw.${pkg}.Features.csproj`,
      coreProject: `${coreDir}/Aw.${pkg}.Core.csproj`,
    },
  };
}

/** Replaces a file's using block with the sorted union of its usings and `add`. */
export function addUsings(text, add) {
  const lines = text.split("\n");
  const present = lines.filter((l) => /^using [A-Za-z0-9_.]+;$/.test(l)).map((l) => l.slice(6, -1));
  const merged = [...new Set([...present, ...add])].sort(byNamespace);
  const rest = lines.filter((l) => !/^using [A-Za-z0-9_.]+;$/.test(l));
  while (rest.length && rest[0] === "") rest.shift();
  return `${usingsBlock(merged)}\n\n${rest.join("\n")}`;
}

/** The slice's [JsonSerializable] types and the usings they need in the package's JSON context. */
const jsonEntries = (s) => ({
  types: [`${s.slice}Request`, `${s.slice}Response`, ...(s.surface === "http" && T.hasBody(s) ? [`${s.slice}Body`] : [])],
  usings: [s.featuresNs, ...(s.surface === "http" && T.hasBody(s) ? [`Aw.${s.pkg}.Host.Endpoints`] : [])],
});

/** A new Guid or int id needs the RS0041 NoWarn in Core's csproj (see writeSlice). */
const needsRs0041 = (journal, s) => s.ids.some((i) => i.isNew && i.prim !== "string") && !journal.read(s.dirs.coreProject).includes("RS0041");

/** --slice-only: the shared files the slice may still edit (its own lines only): A.cs, IdsJsonContext, PublicAPI. */
const sliceOwned = (s, rel) =>
  rel === `${s.dirs.testing}/A.cs` || rel === `${s.dirs.tests}/Ids/IdsJsonContext.cs` || (rel.startsWith(`src/${s.pkg}/`) && path.basename(rel) === "PublicAPI.Unshipped.txt");

/** --slice-only: the new files that belong to the slice alone; any other new file is a shared one it doesn't create. */
const sliceCreatable = (s, rel) => {
  const d = s.dirs;
  const own = [`${d.testing}/${s.slice}RequestBuilder.cs`];
  for (const id of s.ids.filter((i) => i.isNew)) own.push(`${d.core}/Ids/${id.name}.cs`, `${d.tests}/Ids/${id.name}Tests.cs`, `${d.tests}/Ids/${id.name}Holder.cs`);
  for (const id of s.routeIds) own.push(`${d.tests}/Ids/${id}RouteBindingTests.cs`);
  for (const port of s.newPorts) own.push(`${d.core}/Ports/${port}.cs`, `${d.testing}/InMemory${port.slice(1)}.cs`, `${d.testing}/${port.slice(1)}Contract.cs`);
  return rel.startsWith(`${d.features}/`) || rel.startsWith(`${d.tests}/${s.slice}/`) || own.includes(rel);
};
const CONTRACT_UNIT_FIX = "add it in the phase's contract unit, or run without --slice-only";

/** Writes the slice's files and edits (no checks). --slice-only skips the shared edits and refuses any other. */
export function writeSlice(journal, resolved, { sliceOnly = false } = {}) {
  const s = { ...resolved, sliceOnly };
  const d = s.dirs;
  if (sliceOnly) {
    if (needsRs0041(journal, s)) {
      const id = s.ids.find((i) => i.isNew && i.prim !== "string");
      throw new ScaffoldError("NS-SLICE-ONLY", `the new ${id.prim} id ${id.name} needs the RS0041 NoWarn in ${d.coreProject}, a shared file --slice-only doesn't edit`, `the NoWarn is in docs/csharp/typed-ids.md; ${CONTRACT_UNIT_FIX}`);
    }
    // A.cs and IdsJsonContext.cs take one line per slice; creating a missing one would make every parallel branch add it.
    const shared = [`${d.testing}/A.cs`, ...(s.ids.some((i) => i.isNew) ? [`${d.tests}/Ids/IdsJsonContext.cs`] : [])];
    const missing = shared.find((rel) => !journal.exists(rel));
    if (missing) throw new ScaffoldError("NS-SLICE-ONLY", `${missing} is missing, and --slice-only only adds the slice's line to it; it creates no shared file`, CONTRACT_UNIT_FIX);
    journal.editGuard = (rel) => {
      if (!sliceOwned(s, rel)) throw new ScaffoldError("NS-SLICE-ONLY", `${rel} is a shared file --slice-only doesn't edit`, "add that line by hand in your branch, or run without --slice-only");
    };
    journal.createGuard = (rel) => {
      if (!sliceCreatable(s, rel)) throw new ScaffoldError("NS-SLICE-ONLY", `${rel} is not one of the slice's own files, and --slice-only creates no shared file`, CONTRACT_UNIT_FIX);
    };
  }
  journal.create(`${d.features}/${s.slice}Request.cs`, T.request(s));
  journal.create(`${d.features}/${s.slice}Response.cs`, T.response(s));
  journal.create(`${d.features}/${s.slice}Service.cs`, T.service(s));
  journal.create(`${d.features}/${s.slice}Validator.cs`, T.validator(s));

  if (!sliceOnly) {
    journal.insert(d.registration, /^ {8}return services;$/m, `${T.registration(s)}        return services;`, "NS-EDIT", `the ${s.slice} registration (before 'return services;')`);
    journal.edit(d.registration, (text) => addUsings(text, ["Aw.Kernel.Services", s.featuresNs]));
  }

  // Vogen wraps a Guid or int id's generated ToString(string, IFormatProvider) in #nullable disable: RS0041 on Core,
  // with nothing to fix in user code and no .editorconfig reach into generated files (docs/csharp/typed-ids.md).
  const coreProject = d.coreProject;
  if (needsRs0041(journal, s)) {
    journal.insert(coreProject, /(<RootNamespace>[^<]*<\/RootNamespace>\n)/, `$1${T.rs0041NoWarn}`, "NS-EDIT", "the RS0041 NoWarn (after RootNamespace)");
  }
  for (const id of s.ids.filter((i) => i.isNew)) {
    journal.create(`${d.core}/Ids/${id.name}.cs`, T.typedId(s, id));
    journal.create(`${d.tests}/Ids/${id.name}Tests.cs`, T.idTests(s, id));
    journal.create(`${d.tests}/Ids/${id.name}Holder.cs`, T.idHolder(s, id));
    const ctx = `${d.tests}/Ids/IdsJsonContext.cs`;
    if (journal.exists(ctx)) journal.insert(ctx, /(\ninternal sealed partial class IdsJsonContext)/, `\n[JsonSerializable(typeof(${id.name}Holder))]$1`, "NS-EDIT", `${id.name}Holder in the id tests' JSON context`);
    else journal.create(ctx, T.idsJsonContext(s, [`${id.name}Holder`]));
  }
  for (const port of s.newPorts) {
    journal.create(`${d.core}/Ports/${port}.cs`, T.port(s, port));
    journal.create(`${d.testing}/InMemory${port.slice(1)}.cs`, T.fake(s, port));
    journal.create(`${d.testing}/${port.slice(1)}Contract.cs`, T.contract(s, port));
  }

  if (s.surface === "cli" && !sliceOnly) {
    journal.create(`${d.commands}/${s.slice}Command.cs`, T.command(s));
    journal.insert(`${d.commands}/CommandTable.cs`, /^(\s*_ => CommandOutput\.Usage)/m, `${T.commandArm(s)}$1`, "NS-EDIT", `the '${s.command}' arm (above '_ => CommandOutput.Usage')`);
  }
  if (s.surface === "http") {
    if (!sliceOnly) {
      journal.create(`${d.endpoints}/${s.slice}Endpoint.cs`, T.endpoint(s));
      if (T.hasBody(s)) journal.create(`${d.endpoints}/${s.slice}Body.cs`, T.body(s));
      journal.insert(`${d.endpoints}/EndpointTable.cs`, /(_ = app\.MapGroup\("[^"]*"\)[^;]*);/, `$1${T.endpointCall(s)};`, "NS-EDIT", `Map${s.slice}() (on the '_ = app.MapGroup(...)' chain)`);
    }
    for (const id of s.routeIds) {
      const rel = `${d.tests}/Ids/${id}RouteBindingTests.cs`;
      if (!journal.exists(rel)) journal.create(rel, T.routeBindingTests(s, { name: id, prim: s.requestIds.find((r) => r.name === id).prim }));
    }
  }
  if (s.surface !== "none" && !sliceOnly) {
    const { types, usings } = jsonEntries(s);
    journal.insert(d.json, /(\n(?:internal|public) sealed partial class)/, `\n${types.map((t) => `[JsonSerializable(typeof(${t}))]`).join("\n")}$1`, "NS-EDIT", "the slice's [JsonSerializable] lines (above the context class)");
    journal.edit(d.json, (text) => addUsings(text, usings));
  }

  journal.create(`${d.tests}/${s.slice}/${s.slice}Tests.cs`, T.redTest(s));
  journal.create(`${d.testing}/${s.slice}RequestBuilder.cs`, T.requestBuilder(s));
  // A package may have dropped an empty builder root (Sindri's S1 did): write it again rather than fail on it
  // (--slice-only has refused that case above).
  if (!journal.exists(`${d.testing}/A.cs`)) journal.create(`${d.testing}/A.cs`, builderRoot({ name: s.pkg, shape: s.shape }));
  journal.insert(`${d.testing}/A.cs`, /\{\n\}\n?$|\n\}\n?$/, (end) => (end.startsWith("{") ? `{\n${T.builderFactory(s)}}\n` : `\n\n${T.builderFactory(s)}}\n`), "NS-EDIT", "the builder factory (before A's closing brace)");
}

/** The public API step (before the build): Core first when it gained an id or a port, then Features' records. */
async function sliceApi(journal, s, log) {
  if (s.ids.some((i) => i.isNew) || s.newPorts.length > 0) await declarePublicApi(journal, s.dirs.coreProject, log);
  if (s.dirs.featuresProject) await declarePublicApi(journal, s.dirs.featuresProject, log);
}

/** --slice-only: the lines the normal mode would have added at each shared anchor, for the implementer's branch. */
export function sharedLines(s) {
  const d = s.dirs;
  const out = ["Shared lines to add in your branch (--slice-only left these files alone):"];
  out.push(`  ${d.registration}, before 'return services;' (usings Aw.Kernel.Services, ${s.featuresNs}):`, T.registration(s).trimEnd());
  if (s.surface === "cli") out.push(`  ${d.commands}/CommandTable.cs, above '_ => CommandOutput.Usage':`, T.commandArm(s).trimEnd());
  if (s.surface === "http") out.push(`  ${d.endpoints}/EndpointTable.cs, on the '_ = app.MapGroup(...)' chain:`, `        ${T.endpointCall(s)}`);
  if (s.surface !== "none") {
    const { types, usings } = jsonEntries(s);
    out.push(`  ${d.json}, above the context class (usings ${usings.join(", ")}):`, ...types.map((t) => `[JsonSerializable(typeof(${t}))]`));
  }
  const hand = s.surface === "cli" ? [`${d.commands}/${s.slice}Command.cs`] : s.surface === "http" ? [`${d.endpoints}/${s.slice}Endpoint.cs`, ...(T.hasBody(s) ? [`${d.endpoints}/${s.slice}Body.cs`] : [])] : [];
  if (hand.length) out.push(`  Write by hand: ${hand.join(", ")} (the normal mode's templates are in scripts/scaffold/lib/slice-templates.mjs).`);
  out.push(`  With the registration, remove the [SuppressMessage] for MA0182 from ${s.slice}Service and ${s.slice}Validator.`);
  if (s.pkg === "Sindri") out.push(`  For Sindri: mark ${s.slice}Request ICommand<${s.slice}Response> or IQuery<${s.slice}Response>, and register it with AddCommand or AddQuery in place of the block above (spec §12).`);
  return out.join("\n");
}

export const nextSteps = (s) =>
  [
    `Next (make ${s.slice}Tests pass):`,
    `  1. replace the Assert.Fail in tests/Aw.${s.pkg}.Tests/${s.slice}/${s.slice}Tests.cs with the real assertion`,
    `  2. add the ports to ${s.slice}Service's constructor: ${[...s.ports, "TimeProvider clock"].join(", ")} (fakes in tests/Aw.${s.pkg}.Testing)`,
    `  3. implement ${s.slice}Service; pure decisions go on the domain type in Core, not in the service`,
    "  4. add an integration test if the slice touches SQL",
    '  5. run the lane: dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8',
  ].join("\n");

async function main(argv) {
  let input;
  try {
    input = parseArgs(argv, KNOWN, ["filesOnly", "sliceOnly"]);
  } catch (e) {
    if (e instanceof UsageError) {
      console.error(`new-slice: ${e.message}`);
      return 2;
    }
    throw e;
  }
  const root = path.resolve(input.root ?? process.cwd());
  const journal = new Journal(root);
  const log = (line) => console.error(line);
  const before = input.filesOnly ? [] : await strayChanges(journal);
  const untrap = trapSignals(log);
  try {
    const s = resolveSlice(journal, input);
    writeSlice(journal, s, { sliceOnly: input.sliceOnly === true });
    if (!input.filesOnly) {
      await withHeavyLock(async () => {
        await sliceApi(journal, s, log);
        const scope = checkScope(journal, s.pkg);
        await build(root, scope, log);
        await expectOnlyRed(root, scope, `${s.slice}Tests.${s.slice}_${s.behavior}`, log);
        await formatClean(root, scope, log);
      });
      await noStrayChanges(journal, before);
    }
    console.log(`new-slice: wrote ${journal.touched().length} files for ${s.pkg}.${s.slice}`);
    for (const f of journal.touched()) console.log(`  ${f}`);
    if (input.sliceOnly) console.log(sharedLines(s));
    console.log(`Commit: test: red ${s.slice} test (${s.behavior})`);
    console.log(nextSteps(s));
    return 0;
  } catch (e) {
    const count = journal.touched().length;
    journal.revert();
    const err = e instanceof ScaffoldError ? e : new ScaffoldError("SCAFFOLD-INTERNAL", e.message, "a generator bug: the files were reverted; report the message");
    console.error(err.message);
    if (err.fix) console.error(`fix: ${err.fix}`);
    if (count) console.error(`reverted ${count} files`);
    return 1;
  } finally {
    untrap();
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = await main(process.argv.slice(2));
}
