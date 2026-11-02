// Unit table for the C# scaffold generators (scripts/scaffold/new-package.mjs, new-slice.mjs). No dotnet: each case
// copies the repo files the generators read into a temp tree and runs them with --files-only, then checks the
// refusal (rule id, nothing written), the files written, the edits, and that a failure leaves the tree as it was.
// Run by scripts/tests/scaffold.test.sh; prints "<n> passed" and exits 1 on any failure. The dotnet half (build,
// lanes, format, check-deps on a scaffolded package) is scripts/scaffold/e2e.sh, run by CI's scaffold job.
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, "..", "..");
const NEW_PACKAGE = path.join(REPO, "scripts/scaffold/new-package.mjs");
const NEW_SLICE = path.join(REPO, "scripts/scaffold/new-slice.mjs");
const SEED = ["deps.yaml", "Directory.Packages.props", "AgenticWorkflow.slnx", "tests/Aw.Architecture.Tests/Packages.cs", "tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj", "src/Aw.Kernel/KernelErrors.cs"];

let passed = 0;
const failures = [];
const t = (name, fn) => {
  try {
    fn();
    passed++;
  } catch (e) {
    failures.push(`${name}: ${e.message}`);
  }
};

const trees = [];
function tree() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aw-scaffold-unit-"));
  trees.push(dir);
  for (const rel of SEED) {
    fs.mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true });
    fs.copyFileSync(path.join(REPO, rel), path.join(dir, rel));
  }
  return dir;
}

function hashTree(dir) {
  const h = crypto.createHash("sha256");
  const walk = (d) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true }).sort((a, b) => (a.name < b.name ? -1 : 1))) {
      const p = path.join(d, e.name);
      h.update(path.relative(dir, p));
      if (e.isDirectory()) walk(p);
      else h.update(fs.readFileSync(p));
    }
  };
  walk(dir);
  return h.digest("hex");
}

const run = (script, dir, args) => {
  const r = spawnSync("node", [script, "--root", dir, "--files-only", ...args], { encoding: "utf8", env: { ...process.env, AW_HEAVY_LOCK_HELD: "1" } });
  return { code: r.status, out: `${r.stdout}${r.stderr}` };
};
const pkg = (dir, extra = {}) => {
  const base = { name: "Readiness", tier: "1", "error-prefix": "READINESS", shape: "small", host: "cli", binary: "aw-readiness", "first-slice": "CheckRepo", behavior: "reports_level_one_for_an_empty_repo", ...extra };
  const args = Object.entries(base).flatMap(([k, v]) => (v === undefined ? [] : [`--${k}`, v]));
  return run(NEW_PACKAGE, dir, args);
};
const slice = (d, extra) => run(NEW_SLICE, d, Object.entries(extra).flatMap(([k, v]) => [`--${k}`, v]));
const read = (dir, rel) => fs.readFileSync(path.join(dir, rel), "utf8");
const exists = (dir, rel) => fs.existsSync(path.join(dir, rel));
const refused = (r, rule) => {
  assert.equal(r.code, 1, r.out);
  assert.match(r.out, new RegExp(`^${rule}: `, "m"));
  assert.match(r.out, /^fix: /m);
};
const untouched = (fn) => {
  const dir = tree();
  const before = hashTree(dir);
  const r = fn(dir);
  assert.equal(hashTree(dir), before, "the tree changed");
  return r;
};

// ---- /new-package refusals: rule id, a fix, nothing written ----
t("NP-INPUT: a missing input", () => untouched((d) => refused(pkg(d, { tier: undefined }), "NP-INPUT")));
t("NP-NAME: a reserved name", () => untouched((d) => refused(pkg(d, { name: "Utils" }), "NP-NAME")));
t("NP-NAME: not PascalCase", () => untouched((d) => refused(pkg(d, { name: "readiness" }), "NP-NAME")));
t("NP-EXISTS: src/<Name> exists", () =>
  untouched((d) => {
    fs.mkdirSync(path.join(d, "src/Readiness"));
    const r = pkg(d);
    fs.rmdirSync(path.join(d, "src/Readiness"));
    refused(r, "NP-EXISTS");
  }));
t("NP-TIER: tier 0 is reserved", () => untouched((d) => refused(pkg(d, { tier: "0", name: "Newthing", note: "x" }), "NP-TIER")));
t("NP-DEPS-MISMATCH: tier differs from deps.yaml", () => untouched((d) => refused(pkg(d, { tier: "2" }), "NP-DEPS-MISMATCH")));
t("NP-INPUT: an undeclared package needs a note", () => untouched((d) => refused(pkg(d, { name: "Newthing", "error-prefix": "NEWTHING" }), "NP-INPUT")));
t("NP-PREFIX: malformed", () => untouched((d) => refused(pkg(d, { "error-prefix": "rd" }), "NP-PREFIX")));
t("NP-PREFIX: taken by the kernel", () => untouched((d) => refused(pkg(d, { "error-prefix": "KERNEL" }), "NP-PREFIX")));
t("NP-SMALL-HTTP", () => untouched((d) => refused(pkg(d, { host: "http" }), "NP-SMALL-HTTP")));
t("NP-DEPENDS: not a lower tier", () => untouched((d) => refused(pkg(d, { depends: "Packets" }), "NP-DEPENDS")));
t("NP-DEPENDS: unknown package", () => untouched((d) => refused(pkg(d, { depends: "Nope" }), "NP-DEPENDS")));
t("NP-FANOUT: more than 4 references", () =>
  untouched((d) => refused(pkg(d, { name: "Fanout", tier: "3", note: "x", "error-prefix": "FANOUT", depends: "Judge,Scorer,Packets" }), "NP-FANOUT")));
// A small App is its own host, so it also references Aw.Kernel.Telemetry: Core + Kernel + Telemetry + 2 depends = 5.
t("NP-FANOUT: a small App with 2 depends counts the telemetry reference", () =>
  untouched((d) => {
    const r = pkg(d, { name: "Fanout", tier: "2", note: "x", "error-prefix": "FANOUT", binary: "aw-fanout", depends: "Readiness,Packets" });
    refused(r, "NP-FANOUT");
    assert.match(r.out, /App would hold 5 in-repo references \(Core, Aw\.Kernel, Aw\.Kernel\.Telemetry and 2 depends\)/);
  }));
t("NP-FANOUT: a small App with 1 depend, and full Features with 2, stay within 4", () => {
  const ported = () => {   // a tree where Readiness and Packets are ported (their Core projects exist)
    const d = tree();
    for (const n of ["Readiness", "Packets"]) {
      const rel = `src/${n}/Aw.${n}.Core/Aw.${n}.Core.csproj`;
      fs.mkdirSync(path.dirname(path.join(d, rel)), { recursive: true });
      fs.copyFileSync(path.join(REPO, rel), path.join(d, rel));
    }
    return d;
  };
  const small = pkg(ported(), { name: "Fanout", tier: "2", note: "x", "error-prefix": "FANOUT", binary: "aw-fanout", depends: "Packets" });
  assert.equal(small.code, 0, small.out);
  const full = pkg(ported(), { name: "Fanout", tier: "2", note: "x", "error-prefix": "FANOUT", binary: "aw-fanout", shape: "full", depends: "Readiness,Packets" });
  assert.equal(full.code, 0, full.out);
});
t("full cli: the Host starts telemetry (Program, csproj, deps edge)", () => {
  const d = tree();
  const r = pkg(d, { name: "Democli", tier: "1", note: "a scaffold probe", "error-prefix": "DEMOCLI", shape: "full", binary: "aw-democli" });
  assert.equal(r.code, 0, r.out);
  const host = "src/Democli/Aw.Democli.Host";
  assert.match(read(d, `${host}/Composition.cs`), /\.AddKernelTelemetry\(TelemetryOptions\.FromEnvironment\("aw-democli", environment, clock\)\)/);
  assert.match(read(d, `${host}/Program.cs`), /_ = provider\.GetRequiredService<TelemetryRuntime>\(\);/);
  assert.match(read(d, `${host}/Aw.Democli.Host.csproj`), /<ProjectReference Include="\.\.\/\.\.\/Aw\.Kernel\.Telemetry\/Aw\.Kernel\.Telemetry\.csproj" \/>/);
  assert.match(read(d, "deps.yaml"), /- from: Democli\n {4}to: KernelTelemetry\n {4}kind: project\n {4}status: current/);
});
t("NP-PINS: a missing pin stops with the pin commit to make", () =>
  untouched((d) => {
    const props = path.join(d, "Directory.Packages.props");
    const before = fs.readFileSync(props, "utf8");
    fs.writeFileSync(props, before.replace(/\s*<PackageVersion Include="Microsoft\.Extensions\.DependencyInjection" [^>]*\/>/, ""));
    const r = pkg(d);
    fs.writeFileSync(props, before);
    refused(r, "NP-PINS");
    assert.match(r.out, /Microsoft\.Extensions\.DependencyInjection\b/);
    assert.match(r.out, /pin commit/);
  }));
t("a bad first slice reverts the whole package (NS-NAME)", () => untouched((d) => refused(pkg(d, { "first-slice": "x" }), "NS-NAME")));
t("a refusal after the package files are written reverts them (NS-ROUTE)", () =>
  untouched((d) => refused(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{nope}" }), "NS-ROUTE")));
t("NP-SMALL-NONE: a small package needs an entry point", () => untouched((d) => refused(pkg(d, { host: "none" }), "NP-SMALL-NONE")));
t("NP-DEPENDS: a declared but unported package", () => untouched((d) => refused(pkg(d, { name: "Scorerx", tier: "2", note: "x", "error-prefix": "SCORERX", depends: "Judge" }), "NP-DEPENDS")));
t("NP-EXISTS: the tests project already exists (Architecture)", () => untouched((d) => refused(pkg(d, { name: "Architecture", note: "x", "error-prefix": "ARCHX" }), "NP-EXISTS")));
t("NS-IDS: an id listed twice is refused, not a crash", () => untouched((d) => refused(pkg(d, { ids: "ThingId:string,ThingId:string" }), "NS-IDS")));
t("NS-ROUTE: a GET with an id outside the route", () =>
  untouched((d) => refused(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "GetItem", behavior: "gets_an_item", route: "GET /items", ids: "ItemId:string" }), "NS-ROUTE")));
t("NS-ROUTE: the same id twice in a route", () =>
  untouched((d) => refused(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "GetItem", behavior: "gets_an_item", route: "GET /a/{itemId}/{itemId}", ids: "ItemId:string" }), "NS-ROUTE")));
t("usage error: unknown option exits 2", () => untouched((d) => assert.equal(run(NEW_PACKAGE, d, ["--nope", "x"]).code, 2)));

// ---- /new-package output: small cli ----
t("small cli: the projects, the deps edge, the registry and the red test", () => {
  const d = tree();
  const r = pkg(d);
  assert.equal(r.code, 0, r.out);
  for (const f of [
    "src/Readiness/Aw.Readiness.Core/Aw.Readiness.Core.csproj",
    "src/Readiness/Aw.Readiness.Core/VogenDefaults.cs",
    "src/Readiness/Aw.Readiness.Core/ReadinessErrors.cs",
    "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj",
    "src/Readiness/Aw.Readiness.App/Composition.cs",
    "src/Readiness/Aw.Readiness.App/Commands/CheckRepoCommand.cs",
    "src/Readiness/Aw.Readiness.App/Features/CheckRepo/CheckRepoService.cs",
    "tests/Aw.Readiness.Tests/HostTests.cs",
    "tests/Aw.Readiness.Tests/CheckRepo/CheckRepoTests.cs",
    "tests/Aw.Readiness.Testing/CheckRepoRequestBuilder.cs",
  ])
    assert.ok(exists(d, f), `missing ${f}`);
  assert.ok(!exists(d, "src/Readiness/Aw.Readiness.Host"), "a small package has no Host");
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<AssemblyName>aw-readiness<\/AssemblyName>/);
  // PublishAot only under a RID, so the committed packages.lock.json stays RID-free (csharp.md "AOT scope").
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<AwAotEntryPoint>true<\/AwAotEntryPoint>/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<PublishAot Condition="'\$\(RuntimeIdentifier\)' != ''">true<\/PublishAot>/);
  assert.match(read(d, "deps.yaml"), /- from: Readiness\n {4}to: Kernel\n {4}kind: project\n {4}status: current/);
  // the host starts Kernel telemetry (spec §4b.4): Compose registers it, Program resolves it, the host references it
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Composition.cs"), /\.AddKernelTelemetry\(TelemetryOptions\.FromEnvironment\("aw-readiness", environment, clock\)\)/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Program.cs"), /_ = provider\.GetRequiredService<TelemetryRuntime>\(\);/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj"), /<ProjectReference Include="\.\.\/\.\.\/Aw\.Kernel\.Telemetry\/Aw\.Kernel\.Telemetry\.csproj" \/>/);
  assert.match(read(d, "deps.yaml"), /- from: Readiness\n {4}to: KernelTelemetry\n {4}kind: project\n {4}status: current/);
  assert.match(read(d, "tests/Aw.Readiness.Tests/HostTests.cs"), /public void Telemetry_is_composed_and_exports_nothing\(\)/);
  assert.match(read(d, "tests/Aw.Architecture.Tests/Packages.cs"), /"Aw\.Readiness\.Core",\n {8}"Aw\.Readiness\.App",\n {4}\];/);
  assert.match(read(d, "tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj"), /src\/Readiness\/Aw\.Readiness\.App\/Aw\.Readiness\.App\.csproj/);
  assert.match(read(d, "AgenticWorkflow.slnx"), /<Folder Name="\/src\/Readiness\/">/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/PublicAPI.Unshipped.txt"), /^#nullable enable\n$/);
  assert.match(read(d, "tests/Aw.Readiness.Testing/.editorconfig"), /dotnet_diagnostic\.MA0182\.severity = none/);
  assert.doesNotMatch(read(d, "src/Readiness/Aw.Readiness.Core/Aw.Readiness.Core.csproj"), /RS0041/, "no Guid/int id yet");
  const red = read(d, "tests/Aw.Readiness.Tests/CheckRepo/CheckRepoTests.cs");
  assert.match(red, /public async Task CheckRepo_reports_level_one_for_an_empty_repo\(\)/);
  assert.match(red, /Assert\.Fail\("red: assert reports_level_one_for_an_empty_repo"\)/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Features/CheckRepo/CheckRepoRequest.cs"), /internal sealed record CheckRepoRequest/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Commands/CommandTable.cs"), /CheckRepoCommand\.Name => await CheckRepoCommand\.Run/);
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Commands/CheckRepoCommand.cs"), /Name = "check-repo"/);
  assert.match(r.out, /^Commit: test: readiness package scaffold with a red CheckRepo test$/m);
  assert.match(r.out, /Make CheckRepoTests pass: replace the Assert\.Fail with the real assertion/);
});

// ---- /new-package output: full http with an id ----
t("full http: Host, loopback, endpoint, id, route binding test, undeclared node added", () => {
  const d = tree();
  const r = pkg(d, { name: "Demohttp", note: "a scaffold probe", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{itemId}/start", ids: "ItemId:string,OwnerId:Guid" });
  assert.equal(r.code, 0, r.out);
  const host = "src/Demohttp/Aw.Demohttp.Host";
  for (const f of [`${host}/LoopbackBinding.cs`, `${host}/LoopbackGuard.cs`, `${host}/ValueObjectValidation.cs`, `${host}/HttpResults.cs`, `${host}/Endpoints/StartItemEndpoint.cs`, `${host}/Endpoints/StartItemBody.cs`, "src/Demohttp/Aw.Demohttp.Core/Ids/ItemId.cs", "tests/Aw.Demohttp.Tests/LoopbackBindingTests.cs", "tests/Aw.Demohttp.Tests/Ids/ItemIdRouteBindingTests.cs", "tests/Aw.Demohttp.Tests/Ids/ItemIdTests.cs"])
    assert.ok(exists(d, f), `missing ${f}`);
  assert.match(read(d, `${host}/Aw.Demohttp.Host.csproj`), /Sdk="Microsoft\.NET\.Sdk\.Web"/);
  assert.match(read(d, `${host}/Aw.Demohttp.Host.csproj`), /<AwAotEntryPoint>true<\/AwAotEntryPoint>/);
  assert.match(read(d, `${host}/Endpoints/EndpointTable.cs`), /MapGroup\("\/demohttp"\)\.MapStartItem\(\);/);
  assert.match(read(d, `${host}/Endpoints/StartItemEndpoint.cs`), /MapPost\("\/items\/\{itemId\}\/start", static async \(ItemId itemId, StartItemBody body,/);
  assert.match(read(d, "deps.yaml"), /- id: Demohttp\n {4}kind: package\n {4}tier: 1\n/);
  assert.match(read(d, "deps.yaml"), /- from: Demohttp\n {4}to: Kernel\n {4}kind: project\n {4}status: current/);
  assert.doesNotMatch(read(d, `${host}/Composition.cs`), /using Microsoft\.Extensions\.DependencyInjection;/, "the Web SDK's implicit usings cover it");
  assert.match(read(d, "src/Demohttp/Aw.Demohttp.Core/Ids/ItemId.cs"), /\[JsonConverter\(typeof\(StringIdJsonConverter<ItemId>\)\)\]/);
  assert.match(read(d, "src/Demohttp/Aw.Demohttp.Core/Aw.Demohttp.Core.csproj"), /<NoWarn>\$\(NoWarn\);RS0041<\/NoWarn>/, "OwnerId is a Guid id");
  assert.ok(!exists(d, "tests/Aw.Demohttp.Testing/.editorconfig"), "full-shape builders are public");
  assert.match(read(d, `${host}/Program.cs`), /Pipeline\.Configure\(app\)/);
  // telemetry: the binary name defaults to the lowercase package name; ASP.NET's own providers are cleared
  assert.match(read(d, `${host}/Composition.cs`), /\.AddKernelTelemetry\(TelemetryOptions\.FromEnvironment\("demohttp", environment, clock\)\)/);
  // the http host resolves the runtime in LoopbackBinding.Build, so Program.cs and every test that builds a host dispose it
  assert.match(read(d, `${host}/LoopbackBinding.cs`), /var app = builder\.Build\(\);\n {8}_ = app\.Services\.GetRequiredService<TelemetryRuntime>\(\);/);
  assert.doesNotMatch(read(d, `${host}/Program.cs`), /TelemetryRuntime/);
  assert.match(read(d, `${host}/LoopbackBinding.cs`), /_ = builder\.Logging\.ClearProviders\(\);/);
  assert.match(read(d, `${host}/Aw.Demohttp.Host.csproj`), /<ProjectReference Include="\.\.\/\.\.\/Aw\.Kernel\.Telemetry\/Aw\.Kernel\.Telemetry\.csproj" \/>/);
  assert.match(read(d, "deps.yaml"), /- from: Demohttp\n {4}to: KernelTelemetry\n {4}kind: project\n {4}status: current/);
  for (const layer of ["Core", "Features", "Infrastructure"])
    assert.doesNotMatch(read(d, `src/Demohttp/Aw.Demohttp.${layer}/Aw.Demohttp.${layer}.csproj`), /Aw\.Kernel\.Telemetry/, `only the host references telemetry, not ${layer}`);
  assert.match(read(d, "tests/Aw.Demohttp.Tests/LoopbackBindingTests.cs"), /Pipeline\.Configure\(app\)/);
  assert.match(read(d, "tests/Aw.Demohttp.Tests/LoopbackBindingTests.cs"), /public async Task Kernel_telemetry_is_the_only_logger\(\)/);
  assert.match(read(d, "tests/Aw.Demohttp.Tests/HostTests.cs"), /public void The_log_level_comes_from_the_environment_Compose_is_given\(\)/);
  assert.match(read(d, "tests/Aw.Demohttp.Tests/LoopbackBindingTests.cs"), /"ASPNETCORE_PREFERHOSTINGURLS=true;ASPNETCORE_URLS=http:\/\/0\.0\.0\.0:0"/);
  // a second http slice: GET by a route id that already exists, chained on the same group, its own JSON lines
  const r2 = slice(d, { package: "Demohttp", slice: "GetItem", behavior: "gets_an_item", surface: "http", route: "GET /items/{itemId}" });
  assert.equal(r2.code, 0, r2.out);
  assert.match(read(d, `${host}/Endpoints/EndpointTable.cs`), /MapGroup\("\/demohttp"\)\.MapStartItem\(\)\.MapGetItem\(\);/);
  assert.match(read(d, `${host}/Endpoints/GetItemEndpoint.cs`), /MapGet\("\/items\/\{itemId\}", static async \(ItemId itemId, IService<GetItemRequest, GetItemResponse> service, CancellationToken ct\)/);
  assert.match(read(d, `${host}/Endpoints/GetItemEndpoint.cs`), /new GetItemRequest\(itemId\)/);
  assert.ok(!exists(d, `${host}/Endpoints/GetItemBody.cs`), "GET has no body");
  assert.match(read(d, `${host}/DemohttpJsonContext.cs`), /typeof\(GetItemResponse\)[\s\S]*typeof\(StartItemBody\)|typeof\(StartItemBody\)[\s\S]*typeof\(GetItemResponse\)/);
  assert.equal((read(d, "tests/Aw.Demohttp.Tests/Ids/IdsJsonContext.cs").match(/JsonSerializable/g) ?? []).length, 2, "ItemId reused, not redeclared");
});

// ---- /new-slice ----
t("new-slice: a second cli slice adds one command arm, one registration and one red test", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const r = slice(d, { package: "Readiness", slice: "ListRepos", behavior: "lists_nothing_for_an_empty_profile", surface: "cli", command: "list", ids: "RepoId:string" });
  assert.equal(r.code, 0, r.out);
  const table = read(d, "src/Readiness/Aw.Readiness.App/Commands/CommandTable.cs");
  assert.match(table, /CheckRepoCommand\.Name[\s\S]*ListReposCommand\.Name[\s\S]*_ => CommandOutput\.Usage/);
  const reg = read(d, "src/Readiness/Aw.Readiness.App/Features/ReadinessFeaturesServiceCollectionExtensions.cs");
  assert.equal((reg.match(/new ValidatingService</g) ?? []).length, 2);
  assert.doesNotMatch(read(d, "src/Readiness/Aw.Readiness.App/Features/ListRepos/ListReposService.cs"), /SuppressMessage/, "registered: nothing to suppress");
  assert.match(read(d, "src/Readiness/Aw.Readiness.App/Commands/ListReposCommand.cs"), /RepoId\.TryParse\(args\[0\], CultureInfo\.InvariantCulture, out var repoId\)/);
  assert.match(r.out, /^Commit: test: red ListRepos test \(lists_nothing_for_an_empty_profile\)$/m);
});
const sliceRefused = (extra, rule) => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const before = hashTree(d);
  refused(slice(d, extra), rule);
  assert.equal(hashTree(d), before, "the tree changed");
};
const base = { package: "Readiness", slice: "Other", behavior: "does_something", surface: "cli" };
t("NS-NAME (new-slice)", () => sliceRefused({ ...base, slice: "other" }, "NS-NAME"));
t("NS-BEHAVIOR (new-slice)", () => sliceRefused({ ...base, behavior: "Does Something" }, "NS-BEHAVIOR"));
t("NS-IDS: malformed", () => sliceRefused({ ...base, ids: "Thing:string" }, "NS-IDS"));
t("NS-IDS: an existing id with another primitive", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  assert.equal(slice(d, { ...base, ids: "ThingId:string" }).code, 0);
  const before = hashTree(d);
  refused(slice(d, { ...base, slice: "Third", command: "third", ids: "ThingId:Guid" }), "NS-IDS");
  assert.equal(hashTree(d), before);
});
t("NS-PORTS: not an interface name", () => sliceRefused({ ...base, ports: "ThingStore" }, "NS-PORTS"));
t("NS-PORTS: listed twice", () => sliceRefused({ ...base, ports: "IThingStore,IThingStore" }, "NS-PORTS"));
t("NS-EDIT: a hand-edited anchor is refused, not skipped", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const table = path.join(d, "src/Readiness/Aw.Readiness.App/Commands/CommandTable.cs");
  fs.writeFileSync(table, fs.readFileSync(table, "utf8").replace("_ => CommandOutput.Usage", "_ => Fallback"));
  const before = hashTree(d);
  refused(slice(d, base), "NS-EDIT");
  assert.equal(hashTree(d), before);
});
t("NS-EXISTS: the slice folder exists", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const before = hashTree(d);
  refused(slice(d, { package: "Readiness", slice: "CheckRepo", behavior: "again", surface: "cli" }), "NS-EXISTS");
  assert.equal(hashTree(d), before);
});
t("NS-SURFACE: surface doesn't match the host", () => sliceRefused({ ...base, surface: "http", route: "GET /x" }, "NS-SURFACE"));
t("NS-DEPS: ports + TimeProvider over 5", () => sliceRefused({ ...base, ports: "IA1x,IB1x,IC1x,ID1x,IE1x" }, "NS-DEPS"));
t("NS-COMMAND: the command is taken", () => sliceRefused({ ...base, command: "check-repo" }, "NS-COMMAND"));
t("NS-PACKAGE: no such package", () => untouched((d) => refused(slice(d, { package: "Nothere", slice: "Other", behavior: "does_something", surface: "cli" }), "NS-PACKAGE")));
t("new-slice: a new port gets its interface, fake and contract base", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const r = slice(d, { package: "Readiness", slice: "Score", behavior: "scores_a_repo", surface: "cli", ports: "IRepoReader" });
  assert.equal(r.code, 0, r.out);
  assert.match(read(d, "src/Readiness/Aw.Readiness.Core/Ports/IRepoReader.cs"), /^namespace Aw\.Readiness\.Core\.Ports;$/m);
  assert.match(read(d, "tests/Aw.Readiness.Testing/InMemoryRepoReader.cs"), /^using Aw\.Readiness\.Core\.Ports;$/m);
  assert.match(read(d, "tests/Aw.Readiness.Testing/RepoReaderContract.cs"), /^using Aw\.Readiness\.Core\.Ports;$/m);
});
// A port is found anywhere under Core (Core/Ports/ is where the generator and the ported packages put them), and an
// existing one is reused as it is: its fake and contract base are the package's, not the generator's to rewrite.
for (const where of ["Ports/IRepoReader.cs", "IRepoReader.cs", "Ports/Repo/IRepoReader.cs"]) {
  t(`new-slice: an existing port (Core/${where}) is reused, its fake and contract left alone`, () => {
    const d = tree();
    assert.equal(pkg(d).code, 0);
    const port = path.join(d, "src/Readiness/Aw.Readiness.Core", where);
    fs.mkdirSync(path.dirname(port), { recursive: true });
    fs.writeFileSync(port, "namespace Aw.Readiness.Core.Ports;\n\npublic interface IRepoReader;\n");
    fs.writeFileSync(path.join(d, "tests/Aw.Readiness.Testing/InMemoryRepoReader.cs"), "// the package's own fake\n");
    const r = slice(d, { package: "Readiness", slice: "Score", behavior: "scores_a_repo", surface: "cli", ports: "IRepoReader" });
    assert.equal(r.code, 0, r.out);
    assert.equal(read(d, "tests/Aw.Readiness.Testing/InMemoryRepoReader.cs"), "// the package's own fake\n");
    assert.ok(!exists(d, "tests/Aw.Readiness.Testing/RepoReaderContract.cs"), "wrote a contract base for an existing port");
    const ports = fs.readdirSync(path.join(d, "src/Readiness/Aw.Readiness.Core"), { recursive: true }).filter((f) => f.endsWith("IRepoReader.cs"));
    assert.deepEqual(ports, [where], "wrote a second IRepoReader");
  });
}
t("NS-PORTS: a new port whose fake or contract base already exists", () => {
  for (const file of ["InMemoryRepoReader.cs", "RepoReaderContract.cs"]) {
    const d = tree();
    assert.equal(pkg(d).code, 0);
    fs.writeFileSync(path.join(d, "tests/Aw.Readiness.Testing", file), "// stray\n");
    const before = hashTree(d);
    refused(slice(d, { ...base, ports: "IRepoReader" }), "NS-PORTS");
    assert.equal(hashTree(d), before, "the tree changed");
  }
});

// ---- /new-slice --slice-only (Sindri API spec C7): the slice's own files plus its lines in A.cs, IdsJsonContext and
// PublicAPI.Unshipped.txt (the build declares those; --files-only skips it). Every other file stays byte-identical.
function fileHashes(dir) {
  const out = new Map();
  for (const rel of fs.readdirSync(dir, { recursive: true })) {
    const p = path.join(dir, rel);
    if (fs.statSync(p).isFile()) out.set(rel, crypto.createHash("sha256").update(fs.readFileSync(p)).digest("hex"));
  }
  return out;
}
/** The paths added or changed between two fileHashes maps, sorted; fails on a removed path. */
function changedFiles(before, after) {
  for (const rel of before.keys()) assert.ok(after.has(rel), `removed ${rel}`);
  return [...after].filter(([rel, h]) => before.get(rel) !== h).map(([rel]) => rel).sort();
}
/** The text inserted into `before` to make `after`, when that is a single contiguous insertion (else it fails). */
function insertedOnly(before, after) {
  let p = 0;
  while (p < before.length && before[p] === after[p]) p++;
  let s = 0;
  while (s < before.length - p && before[before.length - 1 - s] === after[after.length - 1 - s]) s++;
  assert.equal(p + s, before.length, "the file changed other than by one insertion");
  return after.slice(p, after.length - s);
}
const sliceOnly = (d, extra) => run(NEW_SLICE, d, ["--slice-only", ...Object.entries(extra).flatMap(([k, v]) => [`--${k}`, v])]);

t("--slice-only cli: only the slice's files, its id files and its A.cs factory; the shared lines are printed", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  // the phase's contract unit owns IdsJsonContext.cs; a normal slice with an id stands in for it here
  assert.equal(slice(d, { package: "Readiness", slice: "ListThings", behavior: "lists_things", surface: "cli", command: "things", ids: "ThingId:string" }).code, 0);
  const a = "tests/Aw.Readiness.Testing/A.cs";
  const aBefore = read(d, a);
  const ctx = "tests/Aw.Readiness.Tests/Ids/IdsJsonContext.cs";
  const ctxBefore = read(d, ctx);
  const before = fileHashes(d);
  const r = sliceOnly(d, { package: "Readiness", slice: "ListRepos", behavior: "lists_nothing_for_an_empty_profile", surface: "cli", command: "list", ids: "RepoId:string" });
  assert.equal(r.code, 0, r.out);
  const app = "src/Readiness/Aw.Readiness.App";
  assert.deepEqual(changedFiles(before, fileHashes(d)), [
    `${app}/Features/ListRepos/ListReposRequest.cs`,
    `${app}/Features/ListRepos/ListReposResponse.cs`,
    `${app}/Features/ListRepos/ListReposService.cs`,
    `${app}/Features/ListRepos/ListReposValidator.cs`,
    "src/Readiness/Aw.Readiness.Core/Ids/RepoId.cs",
    "tests/Aw.Readiness.Testing/A.cs",
    "tests/Aw.Readiness.Testing/ListReposRequestBuilder.cs",
    "tests/Aw.Readiness.Tests/Ids/IdsJsonContext.cs",
    "tests/Aw.Readiness.Tests/Ids/RepoIdHolder.cs",
    "tests/Aw.Readiness.Tests/Ids/RepoIdTests.cs",
    "tests/Aw.Readiness.Tests/ListRepos/ListReposTests.cs",
  ]);
  const added = insertedOnly(aBefore, read(d, a));
  assert.match(added, /public static ListReposRequestBuilder ListReposRequest\(\) => new\(\);/);
  assert.equal(insertedOnly(ctxBefore, read(d, ctx)).trim(), "[JsonSerializable(typeof(RepoIdHolder))]");
  assert.doesNotMatch(added, /CheckRepo/);
  assert.ok(!exists(d, `${app}/Commands/ListReposCommand.cs`), "--slice-only wrote the command file");
  // Nothing in the assembly uses the unregistered stub and validator yet: MA0182 (an error) is suppressed until the branch registers them.
  for (const f of ["ListReposService", "ListReposValidator"]) {
    const text = read(d, `${app}/Features/ListRepos/${f}.cs`);
    assert.match(text, /^using System\.Diagnostics\.CodeAnalysis;$/m, f);
    assert.match(text, new RegExp(`\\[SuppressMessage\\("Design", "MA0182:Avoid unused internal types", Justification = "[^"]+"\\)\\]\\ninternal sealed class ${f}`), f);
  }
  assert.match(r.out, /remove the \[SuppressMessage\] for MA0182 from ListReposService and ListReposValidator/);
  assert.match(r.out, /^Shared lines to add in your branch/m);
  assert.match(r.out, /ListReposCommand\.Name => await ListReposCommand\.Run\(rest, scope\.ServiceProvider, json\)/);
  assert.match(r.out, /new ValidatingService<ListReposRequest, ListReposResponse>\(/);
  assert.match(r.out, /\[JsonSerializable\(typeof\(ListReposRequest\)\)\]\n\s*\[JsonSerializable\(typeof\(ListReposResponse\)\)\]/);
  assert.match(r.out, /Commands\/ListReposCommand\.cs/);
  assert.doesNotMatch(r.out, /For Sindri/, "the Sindri-only note prints only for Sindri");
  // the file list comes before the block, and the block before the next steps
  assert.ok(r.out.indexOf("ListReposTests.cs") < r.out.indexOf("Shared lines") && r.out.indexOf("Shared lines") < r.out.indexOf("Next (make"), r.out);
});

t("--slice-only http: a new string route id adds its files and one IdsJsonContext line; no surface or shared file changes", () => {
  const d = tree();
  const r0 = pkg(d, { name: "Demohttp", note: "a scaffold probe", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{itemId}/start", ids: "ItemId:string" });
  assert.equal(r0.code, 0, r0.out);
  const ctx = "tests/Aw.Demohttp.Tests/Ids/IdsJsonContext.cs";
  const ctxBefore = read(d, ctx);
  const before = fileHashes(d);
  const r = sliceOnly(d, { package: "Demohttp", slice: "GetJob", behavior: "gets_a_job", surface: "http", route: "GET /jobs/{jobId}", ids: "JobId:string" });
  assert.equal(r.code, 0, r.out);
  const f = "src/Demohttp/Aw.Demohttp.Features/GetJob";
  assert.deepEqual(changedFiles(before, fileHashes(d)), [
    "src/Demohttp/Aw.Demohttp.Core/Ids/JobId.cs",
    `${f}/GetJobRequest.cs`,
    `${f}/GetJobResponse.cs`,
    `${f}/GetJobService.cs`,
    `${f}/GetJobValidator.cs`,
    "tests/Aw.Demohttp.Testing/A.cs",
    "tests/Aw.Demohttp.Testing/GetJobRequestBuilder.cs",
    "tests/Aw.Demohttp.Tests/GetJob/GetJobTests.cs",
    ctx,
    "tests/Aw.Demohttp.Tests/Ids/JobIdHolder.cs",
    "tests/Aw.Demohttp.Tests/Ids/JobIdRouteBindingTests.cs",
    "tests/Aw.Demohttp.Tests/Ids/JobIdTests.cs",
  ]);
  assert.equal(insertedOnly(ctxBefore, read(d, ctx)).trim(), "[JsonSerializable(typeof(JobIdHolder))]");
  assert.ok(!exists(d, "src/Demohttp/Aw.Demohttp.Host/Endpoints/GetJobEndpoint.cs"), "--slice-only wrote the endpoint");
  assert.match(r.out, /^Shared lines to add in your branch/m);
  assert.match(r.out, /\.MapGetJob\(\)/);
  assert.match(r.out, /new ValidatingService<GetJobRequest, GetJobResponse>\(/);
  assert.match(r.out, /\[JsonSerializable\(typeof\(GetJobRequest\)\)\]\n\s*\[JsonSerializable\(typeof\(GetJobResponse\)\)\]/);
  assert.doesNotMatch(r.out, /GetJobBody/, "GET has no body");
  assert.match(r.out, /Endpoints\/GetJobEndpoint\.cs/);
});

t("--slice-only http POST with a body id: the printed JSON lines include the body record", () => {
  const d = tree();
  assert.equal(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{itemId}/start", ids: "ItemId:string" }).code, 0);
  const r = sliceOnly(d, { package: "Demohttp", slice: "MoveItem", behavior: "moves_an_item", surface: "http", route: "POST /items/{itemId}/move", ids: "ShelfId:string" });
  assert.equal(r.code, 0, r.out);
  assert.match(r.out, /\[JsonSerializable\(typeof\(MoveItemBody\)\)\]/);
  assert.match(r.out, /Endpoints\/MoveItemBody\.cs/);
  assert.ok(!exists(d, "src/Demohttp/Aw.Demohttp.Host/Endpoints/MoveItemBody.cs"), "--slice-only wrote the body record");
});

const demohttp = (d) => assert.equal(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{itemId}/start", ids: "ItemId:string" }).code, 0);
t("--slice-only: a new port gets its interface, fake and contract base past the create guard", () => {
  const d = tree();
  demohttp(d);
  const before = fileHashes(d);
  const r = sliceOnly(d, { package: "Demohttp", slice: "ListShelves", behavior: "lists_shelves", surface: "http", route: "GET /shelves", ports: "IShelfStore" });
  assert.equal(r.code, 0, r.out);
  const f = "src/Demohttp/Aw.Demohttp.Features/ListShelves";
  assert.deepEqual(changedFiles(before, fileHashes(d)), [
    "src/Demohttp/Aw.Demohttp.Core/Ports/IShelfStore.cs",
    `${f}/ListShelvesRequest.cs`,
    `${f}/ListShelvesResponse.cs`,
    `${f}/ListShelvesService.cs`,
    `${f}/ListShelvesValidator.cs`,
    "tests/Aw.Demohttp.Testing/A.cs",
    "tests/Aw.Demohttp.Testing/InMemoryShelfStore.cs",
    "tests/Aw.Demohttp.Testing/ListShelvesRequestBuilder.cs",
    "tests/Aw.Demohttp.Testing/ShelfStoreContract.cs",
    "tests/Aw.Demohttp.Tests/ListShelves/ListShelvesTests.cs",
  ]);
  assert.ok(!exists(d, "src/Demohttp/Aw.Demohttp.Host/Endpoints/ListShelvesEndpoint.cs"), "--slice-only wrote the endpoint");
});

t("NS-SLICE-ONLY: a new Guid id when Core lacks the RS0041 NoWarn is refused, nothing written", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const before = hashTree(d);
  const r = sliceOnly(d, { package: "Readiness", slice: "ListRuns", behavior: "lists_runs", surface: "cli", ids: "RunId:Guid" });
  refused(r, "NS-SLICE-ONLY");
  assert.match(r.out, /RS0041/);
  assert.match(r.out, /^fix: the NoWarn is in docs\/csharp\/typed-ids\.md; add it in the phase's contract unit, or run without --slice-only$/m);
  assert.equal(hashTree(d), before, "the tree changed");
});

t("--slice-only: a new Guid id is fine when Core already has the RS0041 NoWarn", () => {
  const d = tree();
  assert.equal(pkg(d, { name: "Demohttp", note: "x", "error-prefix": "DEMOHTTP", shape: "full", host: "http", binary: undefined, "first-slice": "StartItem", behavior: "starts_an_item", route: "POST /items/{itemId}/start", ids: "ItemId:string,OwnerId:Guid" }).code, 0);
  const csproj = "src/Demohttp/Aw.Demohttp.Core/Aw.Demohttp.Core.csproj";
  const was = read(d, csproj);
  const r = sliceOnly(d, { package: "Demohttp", slice: "GetBatch", behavior: "gets_a_batch", surface: "http", route: "GET /batches/{batchId}", ids: "BatchId:Guid" });
  assert.equal(r.code, 0, r.out);
  assert.equal(read(d, csproj), was);
  assert.ok(exists(d, "src/Demohttp/Aw.Demohttp.Core/Ids/BatchId.cs"));
});

t("--slice-only: a slice-only refusal leaves a normal second run possible (the guard is per run)", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  refused(sliceOnly(d, { package: "Readiness", slice: "ListRuns", behavior: "lists_runs", surface: "cli", ids: "RunId:Guid" }), "NS-SLICE-ONLY");
  const r = slice(d, { package: "Readiness", slice: "ListRuns", behavior: "lists_runs", surface: "cli", ids: "RunId:Guid" });
  assert.equal(r.code, 0, r.out);
  assert.match(read(d, "src/Readiness/Aw.Readiness.Core/Aw.Readiness.Core.csproj"), /RS0041/);
  assert.doesNotMatch(r.out, /Shared lines to add/);
});

// Sindri's S1 removed its empty A.cs. The normal mode writes the builder root again; --slice-only creates no shared
// file, so it refuses and leaves the tree as it was (the phase's contract unit restores A.cs).
t("new-slice (normal): a package without tests/Aw.<Pkg>.Testing/A.cs gets the builder root with the factory", () => {
  const d = tree();
  demohttp(d);
  fs.rmSync(path.join(d, "tests/Aw.Demohttp.Testing/A.cs"));
  const r = slice(d, { package: "Demohttp", slice: "GetItem", behavior: "gets_an_item", surface: "http", route: "GET /items/{itemId}" });
  assert.equal(r.code, 0, r.out);
  assert.match(read(d, "tests/Aw.Demohttp.Testing/A.cs"), /^namespace Aw\.Demohttp\.Testing;\n[\s\S]*public static class A\n\{\n {4}\/\/\/ <summary>A GetItemRequest builder[\s\S]*public static GetItemRequestBuilder GetItemRequest\(\) => new\(\);\n\}\n$/);
});
t("NS-SLICE-ONLY: a package without tests/Aw.<Pkg>.Testing/A.cs is refused, nothing written", () => {
  const d = tree();
  demohttp(d);
  fs.rmSync(path.join(d, "tests/Aw.Demohttp.Testing/A.cs"));
  const before = hashTree(d);
  const r = sliceOnly(d, { package: "Demohttp", slice: "GetItem", behavior: "gets_an_item", surface: "http", route: "GET /items/{itemId}" });
  refused(r, "NS-SLICE-ONLY");
  assert.match(r.out, /tests\/Aw\.Demohttp\.Testing\/A\.cs/);
  assert.match(r.out, /^fix: add it in the phase's contract unit, or run without --slice-only$/m);
  assert.equal(hashTree(d), before, "the tree changed");
});
t("NS-SLICE-ONLY: a new id when the tests have no IdsJsonContext.cs is refused, nothing written", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  assert.ok(!exists(d, "tests/Aw.Readiness.Tests/Ids/IdsJsonContext.cs"));
  const before = hashTree(d);
  const r = sliceOnly(d, { package: "Readiness", slice: "ListRepos", behavior: "lists_repos", surface: "cli", command: "list", ids: "RepoId:string" });
  refused(r, "NS-SLICE-ONLY");
  assert.match(r.out, /tests\/Aw\.Readiness\.Tests\/Ids\/IdsJsonContext\.cs/);
  assert.match(r.out, /^fix: add it in the phase's contract unit, or run without --slice-only$/m);
  assert.equal(hashTree(d), before, "the tree changed");
});
t("--slice-only: no IdsJsonContext.cs is fine when the slice adds no id", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const r = sliceOnly(d, { package: "Readiness", slice: "ListRepos", behavior: "lists_repos", surface: "cli", command: "list" });
  assert.equal(r.code, 0, r.out);
  assert.ok(!exists(d, "tests/Aw.Readiness.Tests/Ids/IdsJsonContext.cs"));
});

const commonLib = await import(path.join(REPO, "scripts/scaffold/lib/common.mjs"));
t("Journal edit guard: an edit the guard refuses throws its ScaffoldError and leaves the file alone", () => {
  const d = tree();
  const j = new commonLib.Journal(d);
  j.editGuard = (rel) => {
    if (rel !== "deps.yaml") throw new commonLib.ScaffoldError("NS-SLICE-ONLY", `${rel} is shared`, "x");
  };
  const was = read(d, "AgenticWorkflow.slnx");
  assert.throws(() => j.edit("AgenticWorkflow.slnx", (x) => `${x}// more\n`), /^Error: NS-SLICE-ONLY: AgenticWorkflow\.slnx is shared|NS-SLICE-ONLY: AgenticWorkflow\.slnx is shared/);
  assert.equal(read(d, "AgenticWorkflow.slnx"), was);
  j.edit("deps.yaml", (x) => `${x}# more\n`);
  assert.match(read(d, "deps.yaml"), /# more\n$/);
  j.create("new.txt", "a\n");
  j.edit("new.txt", () => "b\n");   // a file this run created is its own, never guarded
  assert.equal(read(d, "new.txt"), "b\n");
});

t("Journal create guard: a create the guard refuses throws its ScaffoldError and writes nothing", () => {
  const d = tree();
  const j = new commonLib.Journal(d);
  j.createGuard = (rel) => {
    if (rel !== "mine.txt") throw new commonLib.ScaffoldError("NS-SLICE-ONLY", `${rel} is shared`, "x");
  };
  assert.throws(() => j.create("sub/shared.txt", "a\n"), /NS-SLICE-ONLY: sub\/shared\.txt is shared/);
  assert.ok(!exists(d, "sub/shared.txt"));
  assert.ok(!exists(d, "sub"), "the guard runs before the directories are made");
  j.create("mine.txt", "a\n");
  assert.equal(read(d, "mine.txt"), "a\n");
});
t("Journal insert: the edit guard runs before the anchor test, so a guarded file reports the guard's rule", () => {
  const d = tree();
  const j = new commonLib.Journal(d);
  j.editGuard = (rel) => {
    throw new commonLib.ScaffoldError("NS-SLICE-ONLY", `${rel} is shared`, "x");
  };
  assert.throws(() => j.insert("deps.yaml", /no such anchor/, "x", "NS-EDIT", "a line"), /NS-SLICE-ONLY: deps\.yaml is shared/);
});

const newSliceLib = await import(NEW_SLICE);
t("sharedLines: the ICommand/IQuery note prints for Sindri only", () => {
  const s = (pkg) => ({ pkg, slice: "GetJob", surface: "none", featuresNs: `Aw.${pkg}.Features.GetJob`, ports: [], visibility: "public", dirs: { registration: `src/${pkg}/R.cs` } });
  assert.match(newSliceLib.sharedLines(s("Sindri")), /For Sindri: mark GetJobRequest ICommand<GetJobResponse> or IQuery<GetJobResponse>/);
  assert.doesNotMatch(newSliceLib.sharedLines(s("Readiness")), /For Sindri/);
});

const checks = await import(path.join(REPO, "scripts/scaffold/lib/checks.mjs"));
t("rs0016Symbols: the build's RS0016 symbols for one project, unique and sorted", () => {
  const proj = "/r/src/P/Aw.P.Features/Aw.P.Features.csproj";
  const line = (file, sym, p) => `${file}(6,22): error RS0016: Symbol '${sym}' is not part of the declared public API (https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/PublicApiAnalyzers/PublicApiAnalyzers.Help.md) [${p}]`;
  const out = [
    line("/r/src/P/Aw.P.Features/X/XRequest.cs", "Aw.P.Features.X.XRequest", proj),
    line("/r/src/P/Aw.P.Features/X/XRequest.cs", "override Aw.P.Features.X.XRequest.ToString() -> string!", proj),
    line("/r/src/P/Aw.P.Features/X/XRequest.cs", "Aw.P.Features.X.XRequest", proj),   // MSBuild repeats errors in its summary
    line("/r/src/P/Aw.P.Core/Ids/XId.cs", "Aw.P.Core.Ids.XId", "/r/src/P/Aw.P.Core/Aw.P.Core.csproj"),
    "/r/src/P/Aw.P.Features/X/XRequest.cs(6,22): warning RS0017: Symbol 'Aw.P.Features.Gone' is part of the declared API, but is either not public or could not be found [" + proj + "]",
  ].join("\n");
  assert.deepEqual(checks.rs0016Symbols(out, proj), ["Aw.P.Features.X.XRequest", "override Aw.P.Features.X.XRequest.ToString() -> string!"]);
});
t("withApiLines: appends after the #nullable header, keeps what's there, adds nothing twice", () => {
  assert.equal(checks.withApiLines("#nullable enable\n", ["B", "A"]), "#nullable enable\nA\nB\n");
  assert.equal(checks.withApiLines("#nullable enable\nA\n\n", ["C", "A"]), "#nullable enable\nA\nC\n");
});

t("checkScope: format gets only the generated .cs files, per project; build and test cover the package's tests and the architecture tests", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const j = new commonLib.Journal(d);
  const touched = ["src/Readiness/Aw.Readiness.Core/Marker.cs", "tests/Aw.Readiness.Tests/MarkerTests.cs"];
  for (const f of touched) j.create(f, "// x\n");
  const scope = checks.checkScope(j, "Readiness");
  assert.deepEqual(scope.files, touched.slice().sort());
  assert.ok(scope.formatProjects.every((x) => x.endsWith(".csproj")) && scope.formatProjects.length === 2);
  assert.deepEqual(scope.testProjects, ["tests/Aw.Readiness.Tests/Aw.Readiness.Tests.csproj", "tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj"]);
});
t("NP-DEPENDS: a package listed twice", () => untouched((d) => refused(pkg(d, { depends: "Kernel,Kernel" }), "NP-DEPENDS")));
t("SIGINT while waiting on the heavy lock reverts everything and exits 1 (SCAFFOLD-INTERRUPTED)", () => {
  const d = tree();
  assert.equal(pkg(d).code, 0);
  const state = fs.mkdtempSync(path.join(os.tmpdir(), "aw-scaffold-unit-state-"));
  trees.push(state);
  fs.mkdirSync(path.join(state, "locks", "heavy-job.lock"), { recursive: true });   // someone else's builds, in both slots
  fs.mkdirSync(path.join(state, "locks", "heavy-job.lock.2"));
  const before = hashTree(d);
  const env = { ...process.env, AW_STATE_DIR: state };
  delete env.AW_HEAVY_LOCK_HELD;
  delete env.AW_HEAVY_JOB_LOCK;
  delete env.AW_HEAVY_SLOTS;
  // spawnSync stops collecting a pipe once its timeout fires, so the child writes to a file instead.
  const log = path.join(state, "out.txt");
  const fd = fs.openSync(log, "w");
  const r = spawnSync("node", [NEW_SLICE, "--root", d, "--package", "Readiness", "--slice", "ListRepos", "--behavior", "lists_nothing", "--surface", "cli", "--command", "list"], { env, stdio: ["ignore", fd, fd], timeout: 1500, killSignal: "SIGINT" });
  fs.closeSync(fd);
  const out = fs.readFileSync(log, "utf8");
  assert.equal(r.status, 1, out);
  assert.match(out, /^SCAFFOLD-INTERRUPTED: /m);
  assert.match(out, /reverted \d+ files/);
  assert.equal(hashTree(d), before, "the tree changed");
});

t("SIGTERM to the generator alone stops a running command and what it started, and fails it (SCAFFOLD-INTERRUPTED)", () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aw-scaffold-unit-sig-"));
  trees.push(dir);
  const script = path.join(dir, "probe.mjs");
  const token = `31.${process.pid}${Date.now() % 100000}`;   // a sleep no other process on the box is running
  fs.writeFileSync(script, `import { run, trapSignals } from ${JSON.stringify(path.join(REPO, "scripts/scaffold/lib/common.mjs"))};
trapSignals(() => {});
const start = Date.now();
try {
  await run(".", "sh", ["-c", "sleep ${token} & wait"], { log: () => {} });   // a grandchild, like dotnet's build nodes
  console.log("not interrupted");
} catch (e) {
  // A shell's background job ignores SIGINT, so this probe waits for the SIGKILL 10 s later; still well under 31 s.
  console.log(e.message, Date.now() - start < 20000 ? "fast" : "slow");
}
`);
  const out = path.join(dir, "out.txt");
  const fd = fs.openSync(out, "w");
  // timeout + killSignal: SIGTERM goes to node's pid only, not its process group, so the sleep never sees it directly.
  spawnSync("node", [script], { cwd: dir, stdio: ["ignore", fd, fd], timeout: 1000, killSignal: "SIGTERM" });
  fs.closeSync(fd);
  assert.match(fs.readFileSync(out, "utf8"), /^SCAFFOLD-INTERRUPTED: interrupted \(SIGTERM\) fast$/m);
  const left = spawnSync("pgrep", ["-f", `^sleep ${token}$`], { encoding: "utf8" });
  assert.equal(left.stdout.trim(), "", "the grandchild outlived the interrupt");
});

const common = await import(path.join(REPO, "scripts/scaffold/lib/common.mjs"));
const colored = await common.run(REPO, "printf", ["\u001b[31mfailed Aw.X.Tests.T.T_does (1ms)\u001b[m\n  failed: 1\n"], { log: () => {} });
t("run() strips ANSI colors (CI colors MTP's 'failed' lines, which the red-test check parses)", () => {
  assert.equal(colored.out, "failed Aw.X.Tests.T.T_does (1ms)\n  failed: 1\n");
});

// The heavy-job lock's slots: slot 1 is <state>/locks/heavy-job.lock, slot 2 that path plus ".2".
const saved = Object.fromEntries(["AW_STATE_DIR", "AW_HEAVY_LOCK_HELD", "AW_HEAVY_JOB_LOCK", "AW_HEAVY_SLOTS"].map((k) => [k, process.env[k]]));
const slotState = fs.mkdtempSync(path.join(os.tmpdir(), "aw-scaffold-unit-slots-"));
trees.push(slotState);
const slotBase = path.join(slotState, "locks", "heavy-job.lock");
process.env.AW_STATE_DIR = slotState;
for (const k of ["AW_HEAVY_LOCK_HELD", "AW_HEAVY_JOB_LOCK", "AW_HEAVY_SLOTS"]) delete process.env[k];
const quiet = { timeoutS: 0, log: () => {} };
const both = await common.withHeavyLock(async () => [fs.existsSync(slotBase), fs.existsSync(`${slotBase}.2`), process.env.AW_HEAVY_LOCK_HELD], quiet);
const leftAfter = fs.readdirSync(path.dirname(slotBase));
fs.mkdirSync(slotBase);   // another heavy job (a plain locks.sh holder) has slot 1
const second = await common.withHeavyLock(async () => fs.existsSync(path.join(`${slotBase}.2`, "owner")), quiet);
fs.mkdirSync(`${slotBase}.2`);
const full = await common.withHeavyLock(async () => "ran", quiet).catch((e) => e);
process.env.AW_HEAVY_SLOTS = "3";
const third = await common.withHeavyLock(async () => fs.existsSync(`${slotBase}.3`), quiet);
process.env.AW_HEAVY_SLOTS = "1";
fs.rmdirSync(`${slotBase}.2`);
const one = await common.withHeavyLock(async () => "ran", quiet).catch((e) => e);
for (const [k, v] of Object.entries(saved)) if (v === undefined) delete process.env[k]; else process.env[k] = v;
t("withHeavyLock takes slot 1, then slot 2 beside a plain holder of slot 1, and fails only when every slot is held", () => {
  assert.deepEqual(both, [true, false, "1"]);
  assert.deepEqual(leftAfter, []);
  assert.equal(second, true);
  assert.equal(full.rule, "SCAFFOLD-LOCK");
  assert.match(full.message, /every slot of the heavy-job lock .* \(2\)/);
  assert.equal(third, true);
  assert.equal(one.rule, "SCAFFOLD-LOCK");
  assert.deepEqual(common.heavySlots({}), 2);
  for (const bad of ["0", "x", "1.5"]) assert.equal(common.heavySlots({ AW_HEAVY_SLOTS: bad }), 2);
});

for (const dir of trees) fs.rmSync(dir, { recursive: true, force: true });
if (failures.length) {
  for (const f of failures) console.error(`FAIL ${f}`);
  console.error(`${passed} passed, ${failures.length} failed`);
  process.exit(1);
}
console.log(`${passed} passed`);
