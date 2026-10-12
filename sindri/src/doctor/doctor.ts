import fs from "node:fs";
import path from "node:path";

import { parseFlags } from "../args.js";
import { awStateDir, stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { indexPath, layers, meta } from "../index/db.js";
import { indexBuilt, readIndex } from "../index/health.js";
import { GRAPH_OVER_CAP } from "../index/graph.js";
import { MAX_HEAVY_AGE_MS, heavyLockState } from "../index/heavy-lock.js";
import type { IndexProbes } from "../index/io.js";
import { GRAPHIFY_PIN } from "../index/pins.js";
import { hasModel, installedGraphify, sandboxedVersionArgv, tagsUrl } from "../index/setup.js";
import { evolveOverlayCheck } from "../evolve/overlay.js";
import { privateRepos, unsetPrivateRepos, wordsFile } from "../evolve/privacy-derived.js";
import { LEDGER_SCHEMA_VERSION, ledgerPath, readLedger, schemaVersion } from "../ledger/db.js";
import { inspectLock } from "../lock/lock.js";
import type { Command } from "../main.js";
import { fromError, success, type ExitCode } from "../output.js";
import { approvalProblem, approvalState, type ApprovalState } from "../profile/approve.js";
import { loadProfile, resolveProfileRoot, type LoadedProfile } from "../profile/load.js";
import { spoolDir } from "../index/spool.js";
import { hookBinary, hooksPathFix, isSindriHook, isTemplateHook, PRE_COMMIT_MARKER, preCommitPath } from "../scrub/commands.js";

const lines = (text: string): string[] => text.split("\n");

function isExecutable(p: string): boolean {
  try {
    fs.accessSync(p, fs.constants.X_OK);
    return true;
  } catch {
    return false;
  }
}

export interface Check {
  name: string;
  status: "ok" | "warn" | "fail";
  detail: string;
  fix?: string;
}

const INIT_FIX = "sindri profile init --ring0 (or sindri profile init)";

function stateDirCheck(deps: Deps): Check {
  const dir = stateDir(deps);
  if (!fs.existsSync(dir)) return { name: "state-dir", status: "warn", detail: "not created yet", fix: INIT_FIX };
  const local = deps.system.isLocalDisk(dir);
  if (local === false) return { name: "state-dir", status: "fail", detail: "on a network filesystem", fix: "set AW_STATE_DIR to a local path (spec §9.1)" };
  const mode = fs.statSync(dir).mode & 0o777;
  const problems = [...(mode === 0o700 ? [] : [`mode ${mode.toString(8)} (want 700)`]), ...(local === null ? ["can't tell if it is on local disk"] : [])];
  if (problems.length === 0) return { name: "state-dir", status: "ok", detail: dir };
  return { name: "state-dir", status: "warn", detail: problems.join("; "), fix: `chmod 700 ${dir}` };
}

function ledgerCheck(deps: Deps): Check {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) return { name: "ledger", status: "ok", detail: "no ledger yet" };
  try {
    // Doctor is read-only: readLedger never migrates, backs up, chmods or leaves files.
    const v = readLedger(file, schemaVersion);
    if (v > LEDGER_SCHEMA_VERSION) throw new SindriError("SND-LEDGER-001", `ledger schema v${v} is newer than this sindri (v${LEDGER_SCHEMA_VERSION})`);
    return { name: "ledger", status: "ok", detail: `schema v${v} of ${LEDGER_SCHEMA_VERSION}` };
  } catch (e) {
    const code = e instanceof SindriError ? `${e.code} ` : "";
    return { name: "ledger", status: "fail", detail: `${code}${(e as Error).message}`, fix: "upgrade sindri: scripts/install-sindri.sh from the latest main" };
  }
}

function lockCheck(deps: Deps): Check {
  const dir = stateDir(deps);
  const l = inspectLock(dir, deps.system, deps.now);
  const who = l.owner === null ? "an unreadable owner" : `${l.owner.host}/${l.owner.pid}`;
  const state = { free: "free", held: l.owner === null ? `held by ${who}` : `held by ${who} since ${l.owner.startedAt}`, stale: `stale lock from ${who}; the next run takes it over` }[l.state];
  const detail = l.leftovers.length > 0 ? `${state}; leftovers: ${l.leftovers.join(", ")}` : state;
  // A stale lock alone needs nothing: the next run takes it over. Leftover dirs
  // from a crashed takeover need removing; that is safe.
  if (l.leftovers.length === 0) return { name: "lock", status: "ok", detail };
  return { name: "lock", status: "warn", detail, fix: `remove ${l.leftovers.map((n) => path.join(dir, n)).join(" ")} (left by a crashed run)` };
}

function approvedCheck(deps: Deps, loaded: LoadedProfile): { check: Check; state: ApprovalState | null } {
  const file = ledgerPath(stateDir(deps));
  let state: ApprovalState = { kind: "never-approved" };
  if (fs.existsSync(file)) {
    try {
      state = readLedger(file, (db) => approvalState(deps, db, loaded.hash));
    } catch {
      // The ledger check reports why the ledger can't be read.
      const check: Check = { name: "profile-approved", status: "warn", detail: "can't read approvals from the ledger (see the ledger check)", fix: "fix the ledger check first, then rerun sindri doctor" };
      return { check, state: null };
    }
  }
  if (state.kind === "approved") return { check: { name: "profile-approved", status: "ok", detail: loaded.hash.slice(0, 12) }, state };
  return { check: { name: "profile-approved", status: "warn", detail: approvalProblem(state, loaded.hash), fix: "sindri profile approve" }, state };
}

async function profileChecks(deps: Deps, loaded: LoadedProfile): Promise<{ checks: Check[]; used: LoadedProfile }> {
  const { check, state } = approvedCheck(deps, loaded);
  // Spec §8.7: runs use the approved snapshot, so the checks below read it when there
  // is one; the live profile only when nothing usable is approved yet.
  const snapshot = state?.kind === "approved" || state?.kind === "changed-since-approval" ? state.approved : null;
  const used = snapshot ?? loaded;
  const host = deps.system.hostname();
  const active = used.profile.hosts.active;
  const b = used.profile.budget;
  const out: Check[] = [
    check,
    {
      name: "profile-in-use",
      status: "ok",
      detail: snapshot === null ? "live profile (nothing approved yet); the checks below read it" : `approved profile ${snapshot.hash.slice(0, 12)}; the checks below read it`,
    },
    active === host
      ? { name: "active-host", status: "ok", detail: host }
      : { name: "active-host", status: "warn", detail: `this host is ${host}; hosts.active is ${active}`, fix: "edit hosts.active, then sindri profile approve" },
    {
      name: "budget",
      status: "ok",
      detail: b.perItem === undefined && b.perDay === undefined ? "unset (not enforced before rollout step 3a)" : `perItem ${b.perItem ?? "unset"}, perDay ${b.perDay ?? "unset"} tokens`,
    },
  ];
  for (const [name, repo] of Object.entries(used.repos)) {
    const hook = await preCommitPath(deps.git, repo.path);
    const text = hook !== null && fs.existsSync(hook) ? fs.readFileSync(hook, "utf8") : "";
    const bin = isSindriHook(text) ? hookBinary(text) : null;
    // The installer refuses a core.hooksPath dir (it never writes there), so it is never the fix for one.
    const manual = hook === null ? null : await hooksPathFix(deps, repo.path, hook, text);
    const fix = manual ?? `sindri scrub --install-pre-commit --repo ${repo.path}`;
    const missing = bin !== null && path.isAbsolute(bin) && !isExecutable(bin);
    if (bin === null) out.push({ name: `pre-commit:${name}`, status: "warn", detail: "secret-scan hook not installed", fix });
    // The template copy (git init.templateDir) never refuses a commit: a missing binary or a failed
    // `repo status` skips the scan. `repo onboard` replaces it with the full hook, which fails closed.
    else if (isTemplateHook(text)) {
      const onboardFix = `sindri repo onboard ${repo.path}`;
      out.push(
        missing
          ? { name: `pre-commit:${name}`, status: "warn", detail: `template hook calls ${bin}, which is missing, so the secret scan is skipped`, fix: `scripts/install-sindri.sh, then ${onboardFix}` }
          : { name: `pre-commit:${name}`, status: "warn", detail: "template copy: it skips the secret scan if sindri goes missing or repo status fails", fix: onboardFix },
      );
    } else if (missing) out.push({ name: `pre-commit:${name}`, status: "warn", detail: `hook calls ${bin}, which is missing, so every commit is refused`, fix: `scripts/install-sindri.sh, then ${fix}` });
    // A v1 hook (Plan 2) runs the secret scan only; a hand-merged v2 hook may lack the shape step.
    else if (!lines(text).includes(PRE_COMMIT_MARKER)) out.push({ name: `pre-commit:${name}`, status: "warn", detail: "hook is v1: secret scan only, no shape recording", fix });
    else if (used.profile.shape.record && !lines(text).some((l) => l.startsWith('"$SINDRI" shape --record'))) {
      out.push({ name: `pre-commit:${name}`, status: "warn", detail: "hook doesn't run sindri shape --record (shape.record is on)", fix });
    } else out.push({ name: `pre-commit:${name}`, status: "ok", detail: `${hook} → ${bin}` });
  }
  // Spool files the ledger refused (unreadable or malformed) wait in quarantine/ for a look.
  const quarantine = path.join(spoolDir(deps), "quarantine");
  const held = fs.existsSync(quarantine) ? fs.readdirSync(quarantine).length : 0;
  if (held > 0) out.push({ name: "shape-spool", status: "warn", detail: `${held} quarantined shape run(s) in ${quarantine}`, fix: `inspect, then remove ${quarantine}` });
  return { checks: out, used };
}

const PROXY_VARS = ["HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy"] as const;

// Node's fetch ignores HTTP(S)_PROXY unless NODE_USE_ENV_PROXY or --use-env-proxy turns that on;
// then even the loopback embedding request can go through a proxy.
function proxyCheck(env: NodeJS.ProcessEnv): Check | null {
  const byVar = (env.NODE_USE_ENV_PROXY ?? "") !== "";
  const byOption = (env.NODE_OPTIONS ?? "").split(/\s+/).includes("--use-env-proxy");
  if (!byVar && !byOption) return null;
  const source = byVar ? "NODE_USE_ENV_PROXY is set" : "NODE_OPTIONS has --use-env-proxy";
  const proxies = PROXY_VARS.filter((k) => (env[k] ?? "") !== "");
  const detail = proxies.length === 0 ? `${source}, so Node may send the embedding request through a proxy` : `${source}, so ${proxies.join(", ")} then apply to loopback embedding traffic`;
  const fix = [...(byVar ? ["unset NODE_USE_ENV_PROXY"] : []), ...(byOption ? ["remove --use-env-proxy from NODE_OPTIONS"] : [])].join(" and ");
  return { name: "embedding-proxy", status: "warn", detail, fix: `${fix} for sindri` };
}

// The fix that helps one down layer: a graph over the cap needs the profile key raised,
// a pending layer only needs the next build, anything else needs setup.
function layerFix(l: { layer: string; status: string; detail: string }, repo: string): string {
  const build = `sindri index build --repo ${repo}`;
  if (l.layer === "graph" && GRAPH_OVER_CAP.test(l.detail)) return `raise index.graphMaxMB in the profile (max 512), then ${build}`;
  return l.status === "pending" ? build : "sindri index setup";
}

async function indexChecks(deps: Deps, loaded: LoadedProfile, probes: IndexProbes): Promise<Check[]> {
  const out: Check[] = [];
  const ix = loaded.profile.index;
  for (const repo of Object.keys(loaded.repos).sort()) {
    const r = readIndex(indexPath(deps, repo), (db) => ({ m: meta(db), all: layers(db) }));
    const rebuild = `sindri index build --repo ${repo}`;
    if (r.state === "missing") {
      out.push({ name: `index:${repo}`, status: "warn", detail: "no index", fix: rebuild });
      continue;
    }
    // A rebuild keeps a same-version index file, so a damaged one needs the file removed first.
    if (r.state === "damaged") {
      out.push({ name: `index:${repo}`, status: "warn", detail: `damaged (${r.detail})`, fix: `rm ${indexPath(deps, repo)}, then ${rebuild}` });
      continue;
    }
    const { m, all } = r.value;
    const down = all.filter((l) => l.status === "unavailable" || l.status === "pending");
    const ageH = m.builtAt === null ? null : (deps.now().getTime() - Date.parse(m.builtAt)) / 3_600_000;
    if (ageH === null) out.push({ name: `index:${repo}`, status: "warn", detail: "never built", fix: rebuild });
    else if (ageH > ix.maxAgeHours) out.push({ name: `index:${repo}`, status: "warn", detail: `stale (built ${Math.floor(ageH)} h ago)`, fix: rebuild });
    else if (down.length > 0) out.push({ name: `index:${repo}`, status: "warn", detail: down.map((l) => `${l.layer} ${l.status}: ${l.detail}`).join("; "), fix: [...new Set(down.map((l) => layerFix(l, repo)))].join("; ") });
    else out.push({ name: `index:${repo}`, status: "ok", detail: `built ${Math.floor(ageH)} h ago` });
  }
  if (!ix.embeddings.enabled) {
    out.push({ name: "embeddings", status: "ok", detail: "off (index.embeddings.enabled: false)" });
  } else {
    const tags = await probes.getJson(tagsUrl(ix.embeddings.url), 2000);
    out.push(
      hasModel(tags, ix.embeddings.model)
        ? { name: "embeddings", status: "ok", detail: `${ix.embeddings.model} on ${ix.embeddings.url}` }
        : { name: "embeddings", status: "warn", detail: tags === null ? "Ollama not answering on loopback" : `model ${ix.embeddings.model} not pulled`, fix: "sindri index setup" },
    );
    const proxy = proxyCheck(deps.env);
    if (proxy !== null) out.push(proxy);
  }
  if (ix.graph === "none") {
    out.push({ name: "graphify", status: "ok", detail: "off (index.graph: none)" });
  } else {
    const boxed = sandboxedVersionArgv(deps.system.platform, probes, deps.home, deps.env.XDG_RUNTIME_DIR);
    if (boxed === null) {
      out.push({ name: "graphify", status: "warn", detail: "no network sandbox", fix: "sindri index setup" });
    } else {
      const version = await installedGraphify(probes, boxed);
      out.push(
        version === GRAPHIFY_PIN
          ? { name: "graphify", status: "ok", detail: `${GRAPHIFY_PIN}, sandboxed` }
          : { name: "graphify", status: "warn", detail: `not installed at ${GRAPHIFY_PIN}${version === null ? "" : ` (found ${version})`}`, fix: "sindri index setup" },
      );
    }
  }
  // One line per slot of the box-wide heavy-job lock: slot 1 is the plain lock ("heavy-lock"), slot k is "heavy-lock.k".
  for (const heavy of heavyLockState(awStateDir(deps), deps.now, deps.env)) {
    const name = heavy.slot === 1 ? "heavy-lock" : `heavy-lock.${heavy.slot}`;
    const who = heavy.holder === null ? "" : ` by ${heavy.holder.kind} (pid ${heavy.holder.pid})`;
    if (!heavy.held) out.push({ name, status: "ok", detail: "free" });
    else if (heavy.ageMs !== null && heavy.ageMs > MAX_HEAVY_AGE_MS) {
      // Remove the holder record too: left beside a lock dir, it would name a later locks.sh lock's holder.
      out.push({ name, status: "warn", detail: `held for over 6 h${who}`, fix: `if that process is gone: rm -r ${heavy.dir} && rm -f ${heavy.dir}.holder.json (a crashed takeover can leave a .reclaim-* mark inside)` });
    } else out.push({ name, status: "ok", detail: `held${heavy.holder === null ? " by an unknown job" : ` by ${heavy.holder.kind}`}` });
  }
  return out;
}

// Publish treats a repo as public unless it says otherwise; a private repo needs a built index and a word list or publish refuses.
// The tracker repo is the publish target, so it is skipped even if it sets the flag. Nothing here names a derived identifier.
function privacyChecks(deps: Deps, loaded: LoadedProfile): Check[] {
  const out: Check[] = [];
  for (const repo of unsetPrivateRepos(loaded)) {
    out.push({ name: `privacy:${repo}`, status: "warn", detail: "private: is not set, so sindri evolve publish treats this repo as public", fix: `set private: true (or false) in repos/${repo}.yaml, then sindri profile approve` });
  }
  const priv = privateRepos(loaded);
  for (const repo of priv) {
    out.push(indexBuilt(indexPath(deps, repo))
      ? { name: `privacy:${repo}`, status: "ok", detail: "private; its identifiers are held at publish" }
      : { name: `privacy:${repo}`, status: "warn", detail: "private, but no usable code index: sindri evolve publish will refuse (SND-EVOLVE-017)", fix: `sindri index build --repo ${repo}` });
  }
  const wf = wordsFile(deps);
  if (priv.length > 0 && !fs.existsSync(wf)) {
    out.push({ name: "privacy:words", status: "warn", detail: `${wf} is missing: sindri evolve publish will refuse (SND-EVOLVE-018)`, fix: "install a word list at /usr/share/dict/words, or set SINDRI_WORDS" });
  }
  // Judge independence: stage's generalize rewrite runs on models.challenger and its judge on models.adjudicator.
  // Checked once publishing is set up (privacy.denyTerms is set; publish refuses without it), so a fresh install with
  // the default models stays all ok.
  const { challenger, adjudicator } = loaded.profile.models;
  if (loaded.profile.privacy.denyTerms.length === 0) return out;
  out.push(challenger === adjudicator
    ? { name: "privacy:judge", status: "warn", detail: `models.challenger and models.adjudicator are both ${challenger}, so the generalize judge shares the rewriter's blind spots`, fix: "set models.adjudicator (or models.challenger) to a different model in the private profile, then sindri profile approve" }
    : { name: "privacy:judge", status: "ok", detail: `the generalize rewriter (${challenger}) and judge (${adjudicator}) are different models` });
  return out;
}

export async function runChecks(deps: Deps, nodeVersion: string = process.versions.node, probes: IndexProbes = deps.io.probes): Promise<Check[]> {
  const major = Number(nodeVersion.split(".")[0]);
  const checks: Check[] = [
    // One floor with package.json engines and .nvmrc: better-sqlite3 13 (the ledger) needs 22.
    major >= 22 ? { name: "node", status: "ok", detail: nodeVersion } : { name: "node", status: "fail", detail: `${nodeVersion} (need >= 22, which better-sqlite3 13 requires)`, fix: "install Node 22 or newer (see .nvmrc)" },
    stateDirCheck(deps),
    { name: "boot-id", status: "ok", detail: deps.system.bootId() === null ? "unreadable; stale-lock checks use pids only" : "readable" },
    ledgerCheck(deps),
    lockCheck(deps),
    evolveOverlayCheck(deps),
  ];
  const root = resolveProfileRoot(deps);
  if (root === null) return [...checks, { name: "profile", status: "warn", detail: "no profile", fix: INIT_FIX }];
  const r = loadProfile(root);
  if (!r.ok) return [...checks, { name: "profile", status: "fail", detail: `${r.issues.length} issue(s) in ${root}`, fix: "sindri profile validate" }];
  const profile = await profileChecks(deps, r.value);
  // Like the profile checks, the index checks read the approved profile when there is one.
  return [...checks, { name: "profile", status: "ok", detail: root }, ...profile.checks, ...(await indexChecks(deps, profile.used, probes)), ...privacyChecks(deps, profile.used)];
}

export const doctorCommand: Command = async (args, deps) => {
  const json = args.includes("--json");
  try {
    parseFlags(args, { json: { type: "boolean" } });
    const checks = await runChecks(deps);
    const exitCode: ExitCode = checks.some((c) => c.status === "fail") ? 2 : checks.some((c) => c.status === "warn") ? 1 : 0;
    const width = Math.max(...checks.map((c) => c.name.length));
    const text = checks
      .map((c) => `${c.status.padEnd(4)} ${c.name.padEnd(width)}  ${c.detail}${c.status !== "ok" && c.fix !== undefined ? `\n     fix: ${c.fix}` : ""}`)
      .join("\n");
    return success(text, checks, json, exitCode);
  } catch (e) {
    return fromError(e, json);
  }
};
