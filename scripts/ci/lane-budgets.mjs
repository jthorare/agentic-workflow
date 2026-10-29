#!/usr/bin/env node
// scripts/ci/lane-budgets.mjs — the per-package lane budget check (spec §4b.5,
// .agents/rules/csharp-testing.md "Lane budgets").
//
//   node scripts/ci/lane-budgets.mjs [--root DIR] [--configuration C] [--dotnet PATH] [--budget unit=S,integration=S]
//
// For every test project in AgenticWorkflow.slnx (a project named *.Tests, or *.Tests.<x>) and every
// budgeted lane, it times
//   dotnet test --project <test project> --no-build --configuration <C> --filter-trait "Lane=<lane>" --ignore-exit-code 8
// sums the wall clock per package (the deps.yaml package whose projectPrefix starts the project's name,
// longest wins; a test assembly no package owns, such as Aw.Architecture.Tests, is its own row, and so is a project listed in
// PROJECT_BUDGETS) and fails a row at or over its lane's budget. Budgets: unit 10 s, integration 60 s, with the owner's per-package
// and per-project exceptions below; heavy has none and isn't run.
//
// The wall clock is the test run only: run it after the solution is built (CI's `dotnet build` step), so
// --no-build keeps compile time out of the budget. It includes each test host's start-up, which is part
// of what an agent waits for. --budget overrides the numbers (for this check's own tests); CI passes none.
//
// Rule ids: LANE-PKG-001 a package's lane at or over its budget; LANE-PKG-002 a timed run failed (a red test, or
// any exit but 0 and 8). Exit codes: 0 ok, 1 a rule failed, 2 a tool error. Node builtins only.

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { parseYaml } from "../deps/yaml.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const BUDGETS = { unit: 10, integration: 60 };
// Per-package exceptions to the lane budget, each an owner decision: SindriUi's headless Avalonia tests (real fonts, rendering)
// cannot run in 60 s on CI (owner, 2026-10-16). Sindri's integration lane is wait-bound on real Temporal servers: 120 s
// (owner, 2026-10-16: 63.7 s in CI after fail-fast + 8 threads). An explicit --budget for a lane overrides this table too.
// Raised by the owner on 2026-10-28 (q148, superseding q147's measured + 25%) to about twice the slowest main CI run seen, since
// the same lanes swing 35-80% between runs on GitHub's shared runners (run 40734081015 then 40747387454: Sindri unit 27.3 s then
// 48.4 s, SindriUi integration 224.7 s then 303.3 s, Context unit 9.66 s then 14.9 s). Still blocking; #1546 brings them back down.
const PACKAGE_BUDGETS = {
  SindriUi: { unit: 28, integration: 600 },
  Sindri: { unit: 100, integration: 340 },
  Context: { unit: 30 },
};
// Per-test-project rows: a test project named here is measured as a row of its own (the row is the project's name) and kept out
// of its package's sum, with its own limits. For a package whose slow suites cannot fit one project's budget: Aw.Sindri.Engine.Tests
// holds the Temporal-bound suites split out of Aw.Sindri.Tests so that package's integration lane fits 120 s (owner, 2026-10-18).
// An explicit --budget for a lane overrides this table too. Raised by the owner on 2026-10-28 (q148, superseding q147) to about
// twice the slowest run seen (125.3 s in run 40734081015); #1546 brings it back down.
const PROJECT_BUDGETS = { "Aw.Sindri.Engine.Tests": { integration: 250 } };
const SOLUTION = "AgenticWorkflow.slnx";

class ToolError extends Error {}

function parseArgs(argv) {
  const opts = { root: path.resolve(HERE, "..", ".."), configuration: "Release", dotnet: "dotnet", budgets: { ...BUDGETS }, explicit: new Set() };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const value = () => {
      if (i + 1 >= argv.length) throw new ToolError(`${a} needs a value`);
      return argv[++i];
    };
    if (a === "--root") opts.root = path.resolve(value());
    else if (a === "--configuration") opts.configuration = value();
    else if (a === "--dotnet") opts.dotnet = value();
    else if (a === "--budget") {
      for (const pair of value().split(",")) {
        const [lane, secs] = pair.split("=");
        if (!Object.hasOwn(BUDGETS, lane)) throw new ToolError(`--budget: unknown lane '${lane}' (lanes: ${Object.keys(BUDGETS).join(", ")})`);
        const n = Number(secs);
        if (!Number.isFinite(n) || n <= 0) throw new ToolError(`--budget: '${pair}' needs a positive number of seconds`);
        opts.budgets[lane] = n;
        opts.explicit.add(lane);
      }
    } else throw new ToolError(`unknown argument '${a}'`);
  }
  return opts;
}

// Test projects in the solution, as repo-relative paths. Only *.Tests projects run tests; *.Testing
// projects are test support (fakes, builders, contract bases) and have none.
function testProjects(root) {
  const sln = path.join(root, SOLUTION);
  if (!fs.existsSync(sln)) throw new ToolError(`${SOLUTION} not found under ${root}`);
  const paths = [...fs.readFileSync(sln, "utf8").matchAll(/<Project\b[^>]*\sPath="([^"]+)"/g)].map((m) => m[1].replaceAll("\\", "/"));
  const tests = paths.filter((p) => /\.Tests(\.[^./]+)?\.csproj$/.test(path.basename(p))).sort();
  // Nothing timed is not a pass: a naming or solution-format drift must not turn the check off.
  if (tests.length === 0) throw new ToolError(`no *.Tests project in ${SOLUTION}; nothing to time`);
  return tests;
}

function packagesOf(root) {
  const file = path.join(root, "deps.yaml");
  if (!fs.existsSync(file)) throw new ToolError(`deps.yaml not found under ${root}`);
  let doc;
  try { doc = parseYaml(fs.readFileSync(file, "utf8")); } catch (e) { throw new ToolError(`deps.yaml: ${e.message}`); }
  return (doc.packages ?? []).filter((p) => p.projectPrefix);
}

// The package a project belongs to: the longest projectPrefix that starts its name (check-deps' rule).
function ownerOf(name, packages) {
  if (Object.hasOwn(PROJECT_BUDGETS, name)) return name;
  let best = null;
  for (const p of packages) {
    const pre = p.projectPrefix;
    if ((name === pre || name.startsWith(`${pre}.`)) && (!best || pre.length > best.projectPrefix.length)) best = p;
  }
  return best ? best.id : `(${name})`;
}

const commandFor = (opts, project, lane) => [
  "test", "--project", project, "--no-build", "--configuration", opts.configuration,
  "--filter-trait", `Lane=${lane}`, "--ignore-exit-code", "8",
];
const shown = (args) => `dotnet ${args.map((a) => (a.startsWith("Lane=") ? `"${a}"` : a)).join(" ")}`;

function time(opts, project, lane) {
  const args = commandFor(opts, project, lane);
  const t0 = process.hrtime.bigint();
  const r = spawnSync(opts.dotnet, args, { cwd: opts.root, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  const secs = Number(process.hrtime.bigint() - t0) / 1e9;
  if (r.error) throw new ToolError(`could not run ${opts.dotnet}: ${r.error.message}`);
  return { secs, status: r.status ?? -1, output: `${r.stdout ?? ""}${r.stderr ?? ""}`, command: shown(args) };
}

// A package's limit for a lane: --budget, else the package's own exception, else the lane's budget.
const limitFor = (opts, pkg, lane) => (opts.explicit.has(lane) ? undefined : (PROJECT_BUDGETS[pkg] ?? PACKAGE_BUDGETS[pkg])?.[lane]) ?? opts.budgets[lane];

// Truncated, never rounded up, so a passing row just under its limit never prints the limit itself.
const fmt = (s) => {
  const digits = s < 10 ? 2 : 1;
  const f = 10 ** digits;
  return (Math.floor(s * f + 1e-9) / f).toFixed(digits).replace(/\.?0+$/, "") || "0";
};

export function main(argv, out = (l) => console.log(l)) {
  let opts;
  let projects;
  let packages;
  try {
    opts = parseArgs(argv);
    projects = testProjects(opts.root);
    packages = packagesOf(opts.root);
  } catch (e) {
    if (e instanceof ToolError) { console.error(`lane-budgets: ${e.message}`); return 2; }
    throw e;
  }

  const rows = new Map(); // `${pkg}\t${lane}` -> { pkg, lane, secs, projects: [{ project, secs, command }] }
  const failures = [];
  for (const project of projects) {
    const name = path.basename(project, ".csproj");
    const pkg = ownerOf(name, packages);
    for (const lane of Object.keys(opts.budgets)) {
      let run;
      try { run = time(opts, project, lane); } catch (e) {
        if (e instanceof ToolError) { console.error(`lane-budgets: ${e.message}`); return 2; }
        throw e;
      }
      const key = `${pkg}\t${lane}`;
      if (!rows.has(key)) rows.set(key, { pkg, lane, secs: 0, projects: [] });
      const row = rows.get(key);
      row.secs += run.secs;
      row.projects.push({ project, secs: run.secs, command: run.command });
      if (run.status !== 0) {
        failures.push(`LANE-PKG-002 ${name} ${lane}: the timed run failed (exit ${run.status}); rerun: ${run.command}`);
        for (const l of run.output.trim().split("\n").slice(-15)) failures.push(`    | ${l}`);
      }
    }
  }

  const sorted = [...rows.values()].sort((a, b) => a.pkg.localeCompare(b.pkg) || a.lane.localeCompare(b.lane));
  const width = Math.max(7, ...sorted.map((r) => r.pkg.length));
  const summary = ["| Package | Lane | Seconds | Limit | Projects |", "|---|---|---|---|---|"];
  for (const r of sorted) {
    const limit = limitFor(opts, r.pkg, r.lane);
    const over = r.secs >= limit;
    out(`${over ? "OVER" : "ok  "}  ${r.pkg.padEnd(width)}  ${r.lane.padEnd(11)}  ${fmt(r.secs)} s  limit ${fmt(limit)} s  (${r.projects.length} project${r.projects.length === 1 ? "" : "s"})`);
    summary.push(`| ${r.pkg} | ${r.lane} | ${fmt(r.secs)} | ${fmt(limit)} | ${r.projects.map((p) => `${path.basename(p.project, ".csproj")} ${fmt(p.secs)} s`).join(", ")} |`);
    if (over) {
      const slowest = [...r.projects].sort((a, b) => b.secs - a.secs)[0];
      failures.unshift(`LANE-PKG-001 ${r.pkg} ${r.lane} lane: ${fmt(r.secs)} s, limit ${fmt(limit)} s (spec §4b.5). `
        + `Slowest project: ${path.basename(slowest.project, ".csproj")} ${fmt(slowest.secs)} s. `
        + `Fix: move slow tests to the integration or heavy lane, or speed them up; measure with: ${slowest.command}`);
    }
  }
  if (process.env.GITHUB_STEP_SUMMARY) {
    fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, `### Lane budgets per package\n\n${summary.join("\n")}\n\n`);
  }
  if (failures.length > 0) {
    out("");
    for (const f of failures) out(f);
    return 1;
  }
  out(`lane budgets: ${sorted.length} package lanes within budget (${projects.length} test projects)`);
  return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  process.exitCode = main(process.argv.slice(2));
}
