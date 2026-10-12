// scripts/scaffold/lib/common.mjs — shared plumbing for the C# package and slice scaffolds:
// argument parsing, rule-id refusals, a write journal that can revert everything, the heavy-job
// lock, and command running. Node builtins only: no install step, no network.

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";

/** A refusal or a failed check: printed as "<rule>: <message>" plus "fix: <fix>", exit 1. */
export class ScaffoldError extends Error {
  constructor(rule, message, fix) {
    super(`${rule}: ${message}`);
    this.rule = rule;
    this.fix = fix;
  }
}

/**
 * Parses `--key value` and `--flag` arguments. Keys are kebab-case on the command line and camelCase in the
 * result. A key given twice, a positional argument or an unknown key is a usage error (exit 2).
 */
export function parseArgs(argv, known, flags = []) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const raw = argv[i];
    if (!raw.startsWith("--")) throw new UsageError(`unexpected argument '${raw}'`);
    const key = raw.slice(2).replace(/-([a-z])/g, (_, c) => c.toUpperCase());
    if (!known.includes(key) && !flags.includes(key)) throw new UsageError(`unknown option '${raw}'`);
    if (key in out) throw new UsageError(`option '${raw}' given twice`);
    if (flags.includes(key)) {
      out[key] = true;
      continue;
    }
    if (i + 1 >= argv.length || argv[i + 1].startsWith("--")) throw new UsageError(`option '${raw}' needs a value`);
    out[key] = argv[++i];
  }
  return out;
}

export class UsageError extends Error {}

/**
 * SIGINT, SIGTERM or SIGHUP during a run: caught (never fatal on their own) so the generator can revert what it wrote
 * and release the heavy-job lock. The running command's whole process group is stopped (stopGroup), run() fails that
 * step and refuses to start another (SCAFFOLD-INTERRUPTED), and the caller's catch reverts. Installed by each
 * generator's main() around the whole run, from the first write to the revert.
 */
export const signals = { interrupted: null };
const running = { child: null };
const TRAPPED = ["SIGINT", "SIGTERM", "SIGHUP"];
export function trapSignals(log = console.error) {
  const handlers = TRAPPED.map((sig) => [
    sig,
    () => {
      if (!signals.interrupted) log(`${sig}: stopping the current step; the scaffold will be reverted`);
      signals.interrupted ??= sig;
      stopGroup(running.child);   // don't wait for a long dotnet step to finish on its own
    },
  ]);
  for (const [sig, fn] of handlers) process.on(sig, fn);
  return () => {
    for (const [sig, fn] of handlers) process.off(sig, fn);
  };
}
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Stops a command and everything it started. Each command runs in its own process group (spawn's detached), and the
 * group gets SIGINT, the dotnet CLI's cancel path (on SIGTERM or SIGHUP it exits but leaves its build and test
 * processes running), then SIGKILL if it is still there after 10 s.
 */
function stopGroup(child) {
  if (!child || child.exitCode !== null || child.signalCode !== null) return;
  signalGroup(child.pid, "SIGINT");
  setTimeout(() => signalGroup(child.pid, "SIGKILL"), 10_000).unref();
}
function signalGroup(pid, sig) {
  try {
    process.kill(-pid, sig);
  } catch {
    // the group is already gone
  }
}

function checkInterrupted() {
  if (signals.interrupted) throw new ScaffoldError("SCAFFOLD-INTERRUPTED", `interrupted (${signals.interrupted})`, "nothing to fix: the files were reverted; run it again");
}

/** Splits a comma-separated list; empty or missing gives []. */
export const list = (value) =>
  (value ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);

/**
 * Records every file the scaffold creates or edits, so a failed check can put the tree back exactly as it was
 * (new-package.md "Failure behavior"). Edits keep the original text; created files and directories are removed.
 */
export class Journal {
  constructor(root) {
    this.root = root;
    this.created = [];
    this.createdDirs = [];
    this.edited = new Map();
    /** Called with each existing file before an edit (files this run created are exempt); throws to refuse it. */
    this.editGuard = null;
    /** Called with each new file before it is written; throws to refuse it. */
    this.createGuard = null;
  }

  abs(rel) {
    return path.join(this.root, rel);
  }

  exists(rel) {
    return fs.existsSync(this.abs(rel));
  }

  read(rel) {
    return fs.readFileSync(this.abs(rel), "utf8");
  }

  /** Writes a new file; refuses to overwrite one (the generators check for collisions before writing). */
  create(rel, text) {
    const file = this.abs(rel);
    if (fs.existsSync(file)) throw new Error(`refusing to overwrite ${rel}`);
    if (this.createGuard) this.createGuard(rel);
    this.mkdirs(path.dirname(rel));
    fs.writeFileSync(file, text);
    this.created.push(rel);
  }

  /** Rewrites an existing file through fn(text) -> text, keeping the original for revert. */
  edit(rel, fn) {
    if (this.editGuard && !this.created.includes(rel)) this.editGuard(rel);
    const before = this.read(rel);
    const after = fn(before);
    if (after === before) return;
    if (!this.edited.has(rel) && !this.created.includes(rel)) this.edited.set(rel, before);
    fs.writeFileSync(this.abs(rel), after);
  }

  /**
   * Rewrites an existing file by inserting at an anchor; refuses (the given rule) when the anchor isn't there, so a
   * hand-edited file can't make the scaffold skip a registration silently.
   */
  insert(rel, anchor, replacement, rule, what) {
    if (this.editGuard && !this.created.includes(rel)) this.editGuard(rel);
    const text = this.read(rel);
    if (!anchor.test(text)) {
      throw new ScaffoldError(rule, `${rel}: no anchor for ${what} (${anchor})`, "restore the anchor the scaffold writes (see the scaffold spec's templates) or make this edit by hand");
    }
    this.edit(rel, (t) => t.replace(anchor, replacement));
  }

  /** Marks a file a command will rewrite (a lock file), so revert restores it. */
  watch(rel) {
    if (this.created.includes(rel) || this.edited.has(rel)) return;
    if (this.exists(rel)) this.edited.set(rel, this.read(rel));
    else this.created.push(rel);
  }

  mkdirs(relDir) {
    const parts = relDir.split(path.sep).filter(Boolean);
    let cur = "";
    for (const part of parts) {
      cur = cur ? path.join(cur, part) : part;
      if (!fs.existsSync(this.abs(cur))) {
        fs.mkdirSync(this.abs(cur));
        this.createdDirs.push(cur);
      }
    }
  }

  /** Every path the scaffold touched, sorted (for the summary and the git-status check). */
  touched() {
    const changed = [...this.edited.keys()].filter((rel) => !this.exists(rel) || this.read(rel) !== this.edited.get(rel));
    return [...new Set([...this.created.filter((rel) => this.exists(rel)), ...changed])].sort();
  }

  revert() {
    for (const [rel, text] of this.edited) fs.writeFileSync(this.abs(rel), text);
    for (const rel of [...this.created].reverse()) fs.rmSync(this.abs(rel), { force: true });
    for (const dir of [...this.createdDirs].reverse()) fs.rmSync(this.abs(dir), { recursive: true, force: true });
  }
}

/** $AW_HEAVY_SLOTS: a whole number of at least 1; anything else is the default, 2. */
export function heavySlots(env = process.env) {
  const raw = (env.AW_HEAVY_SLOTS ?? "").trim();
  const n = /^\d+$/.test(raw) ? Number(raw) : NaN;
  return Number.isSafeInteger(n) && n >= 1 ? n : 2;
}

/**
 * The box-wide heavy-job lock (config/lib/locks.sh's acquire_any_lock): builds and test runs take one of its
 * AW_HEAVY_SLOTS slots (default 2, so at most two run at a time). Slot 1 is $AW_HEAVY_JOB_LOCK (default
 * <state>/locks/heavy-job.lock), slot k is that path plus ".k"; each is a mkdir mutex. AW_HEAVY_LOCK_HELD=1 means a
 * caller (the `heavy` wrapper, or new-package calling new-slice) already holds a slot.
 */
export async function withHeavyLock(fn, { timeoutS = 5400, log = console.error } = {}) {
  if (process.env.AW_HEAVY_LOCK_HELD === "1") return await fn();
  const state = process.env.AW_STATE_DIR || path.join(os.homedir(), ".agentic-workflow");
  const lock = process.env.AW_HEAVY_JOB_LOCK || path.join(state, "locks", "heavy-job.lock");
  const slots = heavySlots();
  fs.mkdirSync(path.dirname(lock), { recursive: true });
  const start = Date.now();
  let told = false;
  let held = null;
  while (held === null) {
    for (let k = 1; k <= slots && held === null; k++) {
      const dir = k === 1 ? lock : `${lock}.${k}`;
      try {
        fs.mkdirSync(dir);
        held = dir;
      } catch (e) {
        if (e.code !== "EEXIST") throw e;
      }
    }
    if (held !== null) break;
    if (!told) {
      log(`waiting on lock: ${lock} (all ${slots} slots busy)`);
      told = true;
    }
    checkInterrupted();
    if (Date.now() - start > timeoutS * 1000) throw new ScaffoldError("SCAFFOLD-LOCK", `every slot of the heavy-job lock ${lock} (${slots}) was held for ${timeoutS} s`, "wait for the other builds, or remove a stale lock directory");
    await sleep(1000);   // async, so a signal handler gets to run
  }
  fs.writeFileSync(path.join(held, "owner"), `${process.pid} ${new Date().toISOString()} ${process.argv.slice(1).join(" ")}\n`);
  process.env.AW_HEAVY_LOCK_HELD = "1";
  try {
    return await fn();
  } finally {
    delete process.env.AW_HEAVY_LOCK_HELD;
    fs.rmSync(held, { recursive: true, force: true });
  }
}

/** Runs a command in root; returns { code, out } with stdout and stderr together. Never throws on a non-zero exit. */
export async function run(root, cmd, args, { log = console.error } = {}) {
  checkInterrupted();
  log(`$ ${[cmd, ...args].join(" ")}`);
  const r = await new Promise((resolve) => {
    const chunks = [];
    // detached: its own process group, so stopGroup can reach whatever it starts (a terminal's Ctrl-C reaches only
    // this process's group, and the handler forwards it).
    const child = spawn(cmd, args, { cwd: root, env: process.env, stdio: ["ignore", "pipe", "pipe"], detached: true });
    running.child = child;
    child.stdout.on("data", (c) => chunks.push(c));
    child.stderr.on("data", (c) => chunks.push(c));
    child.on("error", (e) => resolve({ code: 127, out: String(e.message) }));
    child.on("close", (code) => resolve({ code: code ?? 1, out: Buffer.concat(chunks).toString("utf8") }));
  });
  if (signals.interrupted && running.child) signalGroup(running.child.pid, "SIGKILL");   // sweep what outlived the command
  running.child = null;
  checkInterrupted();   // a signal that arrived while the command ran fails it, whatever it exited with
  // CI terminals get ANSI colors (\u001b[31mfailed …); the checks parse plain text.
  return { code: r.code, out: r.out.replace(/\u001b\[[0-9;]*m/g, "") };
}

export const lower = (name) => name.toLowerCase();
export const camel = (name) => name[0].toLowerCase() + name.slice(1);
/** CheckRepo -> check-repo. */
export const kebab = (name) => name.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
