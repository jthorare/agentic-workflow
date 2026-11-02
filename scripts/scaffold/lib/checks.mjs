// scripts/scaffold/lib/checks.mjs — the dotnet and repo checks the scaffolds run after writing, in the order the
// scaffold specs list them. A failed check throws a ScaffoldError naming the rule, what was measured and the fix.

import fs from "node:fs";
import path from "node:path";
import { ScaffoldError, run } from "./common.mjs";
import { ARCH_CSPROJ } from "./repo.mjs";

const tail = (out, n = 40) => out.trimEnd().split("\n").slice(-n).join("\n");

export async function mustPass(root, rule, cmd, args, fix, log) {
  const r = await run(root, cmd, args, { log });
  if (r.code !== 0) throw new ScaffoldError(rule, `\`${[cmd, ...args].join(" ")}\` exited ${r.code}\n${tail(r.out)}`, fix);
  return r.out;
}

/** The RS0016 symbols in a build's output for one project (each diagnostic ends in "[<path>/<project file>]"), unique and sorted. */
export function rs0016Symbols(out, project) {
  const tag = `${path.basename(project)}]`;
  const symbols = new Set();
  for (const m of out.matchAll(/RS0016: Symbol '(.*?)' is not part of the declared public API.*$/gm)) if (m[0].endsWith(tag)) symbols.add(m[1]);
  return [...symbols].sort();
}

/** A PublicAPI file's text with `lines` appended (sorted, each once), after what it already declares. */
export function withApiLines(text, lines) {
  const have = text.replace(/\n+$/, "").split("\n");
  const add = [...new Set(lines)].filter((l) => !have.includes(l)).sort();
  return `${[...have, ...add].join("\n")}\n`;
}

/**
 * Declares a library project's new public symbols: builds it and appends every RS0016 symbol the build reports for
 * it to its PublicAPI.Unshipped.txt (journalled, so a revert restores the file). The build's diagnostics are the
 * source because on .NET 10 the RS0016 code fix (`dotnet format analyzers --diagnostics RS0016`) changes nothing,
 * and it never covered source-generated members such as Vogen's (docs/csharp/typed-ids.md). Build a project only
 * after the projects it references have their lines: their RS0016 errors stop it compiling.
 */
export async function declarePublicApi(journal, project, log) {
  const r = await run(journal.root, "dotnet", ["build", project, "-p:TreatWarningsAsErrors=false"], { log });
  const symbols = rs0016Symbols(r.out, project);
  if (r.code !== 0 && symbols.length === 0) {
    throw new ScaffoldError("SCAFFOLD-PUBLICAPI", `\`dotnet build ${project}\` exited ${r.code} with no RS0016 to declare\n${tail(r.out)}`, "fix the build error it prints; the generator reverted its files");
  }
  if (symbols.length) journal.edit(path.join(path.dirname(project), "PublicAPI.Unshipped.txt"), (text) => withApiLines(text, symbols));
  return symbols;
}

/**
 * What the post-write checks cover: the package's test project (it references the generated src projects) and the
 * architecture tests (the generators edit them and deps.yaml), and for format the projects holding a .cs file the
 * generator wrote or edited, limited to those files. The whole solution is not built, tested or format-checked per
 * generator call: scripts/scaffold/e2e.sh builds it once at the end.
 */
export function checkScope(journal, pkg) {
  const files = journal.touched().filter((f) => f.endsWith(".cs"));
  const owner = (file) => {
    for (let dir = path.posix.dirname(file); dir !== "."; dir = path.posix.dirname(dir)) {
      const csproj = fs.readdirSync(journal.abs(dir)).find((n) => n.endsWith(".csproj"));
      if (csproj) return `${dir}/${csproj}`;
    }
    return null;
  };
  const formatProjects = [...new Set(files.map(owner).filter(Boolean))].sort();
  return { testProjects: [`tests/Aw.${pkg}.Tests/Aw.${pkg}.Tests.csproj`, ARCH_CSPROJ], formatProjects, files };
}

export async function build(root, scope, log) {
  for (const project of scope.testProjects) await mustPass(root, "SCAFFOLD-BUILD", "dotnet", ["build", project, "-warnaserror"], "fix the template or the root settings it disagrees with; the generator reverted its files", log);
}

/**
 * Runs the unit lane and returns the names of the failing tests. dotnet test (Microsoft.Testing.Platform) prints
 * one "failed <name> (<time>)" line per failing test.
 */
export async function unitLaneFailures(root, scope, log) {
  // One run per test project; the output is joined and the worst exit code kept.
  const runs = [];
  for (const project of scope.testProjects) runs.push(await run(root, "dotnet", ["test", "--project", project, "--filter-trait", "Lane=unit", "--ignore-exit-code", "8"], { log }));
  const r = { code: Math.max(...runs.map((x) => x.code)), out: runs.map((x) => x.out).join("\n") };
  const failed = [...r.out.matchAll(/^\s*failed (\S+)/gm)].map((m) => m[1]);
  // The run summary's "failed: N" line (one per run; per-project summaries print "failed with N error(s)").
  const failedCount = [...r.out.matchAll(/^\s*failed: (\d+)$/gm)].reduce((sum, m) => sum + Number(m[1]), 0);
  return { code: r.code, failed: [...new Set(failed)], failedCount, out: r.out };
}

/** The lane run must fail on exactly the one red test the scaffold wrote, and nothing else. */
export async function expectOnlyRed(root, scope, redTest, log) {
  const lane = await unitLaneFailures(root, scope, log);
  const others = lane.failed.filter((name) => !name.endsWith(redTest));
  // The red test fails for the right reason: the parameterless stub's NotSupportedException, not a build or DI problem.
  const slice = redTest.split(".")[0].replace(/Tests$/, "");
  const sawRed = lane.failed.some((name) => name.endsWith(redTest)) && lane.out.includes(`NotSupportedException : ${slice}: make ${slice}Tests pass`);
  // Exit 2 is "some tests failed"; anything else (a crashed or aborted test host) is not the red test.
  if (others.length > 0 || !sawRed || lane.code !== 2 || lane.failedCount !== 1) {
    throw new ScaffoldError(
      "SCAFFOLD-RED",
      `expected exactly one failing test (${redTest}) and exit 2; failing: ${lane.failed.length ? lane.failed.join(", ") : "none"} (summary failed: ${lane.failedCount}, exit ${lane.code})\n${tail(lane.out)}`,
      "every other test must pass and the red test must fail with the stub's NotSupportedException",
    );
  }
  return lane;
}

/** `dotnet format <project> --verify-no-changes --include <the .cs files written or edited>`, per project holding one. */
export async function formatClean(root, scope, log) {
  for (const project of scope.formatProjects) {
    await mustPass(root, "SCAFFOLD-FORMAT", "dotnet", ["format", project, "--verify-no-changes", "--include", ...scope.files], "the templates must already be formatted; run `dotnet format` and copy the result into the template", log);
  }
}

export async function depsClean(root, log) {
  return mustPass(root, "SCAFFOLD-DEPS", "node", ["scripts/check-deps.mjs"], "declare the package and its edges in deps.yaml", log);
}

/** Done criterion: git status shows only the files the scaffold wrote. Returns the changed paths it didn't write. */
export async function strayChanges(journal) {
  const r = await run(journal.root, "git", ["status", "--porcelain", "--untracked-files=all"], { log: () => {} });
  if (r.code !== 0) return [];
  const touched = new Set(journal.touched());
  return r.out
    .split("\n")
    .filter(Boolean)
    .map((l) => l.slice(3).replace(/^"|"$/g, ""))
    .filter((f) => !touched.has(f));
}

/** Fails SCAFFOLD-STRAY when a path changed during the run that the scaffold didn't write and wasn't changed before. */
export async function noStrayChanges(journal, before) {
  const stray = (await strayChanges(journal)).filter((f) => !before.includes(f));
  if (stray.length) throw new ScaffoldError("SCAFFOLD-STRAY", `files changed that the scaffold didn't write: ${stray.join(", ")}`, "a command rewrote an existing file (usually a lock file or a PublicAPI file): find out why before committing");
}
