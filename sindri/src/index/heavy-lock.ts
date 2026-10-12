import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { awStateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import { tryRename } from "../lock/lock.js";
import type { SystemProbe } from "../system.js";

// The box-wide heavy-job lock: at most AW_HEAVY_SLOTS heavy jobs at a time (default 2). Same
// primitive as config/lib/locks.sh (acquire_any_lock): slot 1 is <lock>, slot k is <lock>.k, each
// a mkdir mutex (mkdir to acquire, rmdir to release). A holder that knows only <lock> holds slot 1,
// so the holders never outnumber the slots. Each slot has its own holder record beside it
// (<slot>.holder.json, so the dir stays empty for rmdir) and its own takeover mutex (<slot>.reclaim).
// Two jobs of the same kind (index-build:<repo>, suite:<id>) never hold slots at once.
export interface HeavyHolder {
  kind: string;
  pid: number;
  host: string;
  bootId: string | null;
  startedAt: string;
  reclaimed: string | null;
}

const HolderSchema = z.object({
  kind: z.string(),
  pid: z.number().int(),
  host: z.string(),
  bootId: z.string().nullable().default(null),
  startedAt: z.string(),
  reclaimed: z.string().nullable().default(null),
});

// A reclaim mutex with no readable record is treated as live this long, then as dead.
const UNREADABLE_GRACE_MS = 60_000;
// The last resort: a lock held this long is reclaimed whoever holds it. That covers a lock with
// no holder record (a crashed config/lib/locks.sh holder never releases) and a holder whose pid
// an unrelated live process reused. doctor warns at the same age.
export const MAX_HEAVY_AGE_MS = 6 * 3_600_000;

const ageText = (ms: number): string => (ms >= 3_600_000 ? `${Math.floor(ms / 3_600_000)} h` : `${Math.max(0, Math.floor(ms / 60_000))} min`);

// $AW_HEAVY_JOB_LOCK, default <state root>/locks/heavy-job.lock (the path locks.sh callers use).
export const heavyLockDir = (stateRoot: string, env: NodeJS.ProcessEnv): string =>
  env.AW_HEAVY_JOB_LOCK ?? path.join(stateRoot, "locks", "heavy-job.lock");
export const DEFAULT_HEAVY_SLOTS = 2;
// $AW_HEAVY_SLOTS: a whole number of at least 1; anything else is the default.
export const heavySlots = (env: NodeJS.ProcessEnv): number => {
  const raw = env.AW_HEAVY_SLOTS?.trim() ?? "";
  const n = /^\d+$/.test(raw) ? Number(raw) : Number.NaN;
  return Number.isSafeInteger(n) && n >= 1 ? n : DEFAULT_HEAVY_SLOTS;
};
// Every slot's dir, slot 1 first.
export const heavyLockDirs = (stateRoot: string, env: NodeJS.ProcessEnv): string[] => {
  const base = heavyLockDir(stateRoot, env);
  return Array.from({ length: heavySlots(env) }, (_, i) => (i === 0 ? base : `${base}.${i + 1}`));
};
const holderFile = (dir: string): string => `${dir}.holder.json`;
const mutexDir = (dir: string): string => `${dir}.reclaim`;
const mutexRecord = (dir: string): string => path.join(mutexDir(dir), "holder.json");

function parseHolder(file: string): HeavyHolder | null {
  try {
    const r = HolderSchema.safeParse(JSON.parse(fs.readFileSync(file, "utf8")));
    return r.success ? r.data : null;
  } catch {
    return null;
  }
}

// The lock dir's holder. A record older than the dir is a leftover beside a lock
// taken without one (config/lib/locks.sh), not its holder: never reclaim on it.
function readHolder(dir: string): HeavyHolder | null {
  const h = parseHolder(holderFile(dir));
  const hs = fs.statSync(holderFile(dir), { throwIfNoEntry: false });
  const ds = fs.statSync(dir, { throwIfNoEntry: false });
  return h !== null && hs !== undefined && ds !== undefined && hs.mtimeMs >= ds.mtimeMs ? h : null;
}

function writeHolder(file: string, h: HeavyHolder): void {
  fs.writeFileSync(file, JSON.stringify(h));
}

const sameHolder = (a: HeavyHolder | null, b: HeavyHolder): boolean =>
  a !== null && a.pid === b.pid && a.host === b.host && a.startedAt === b.startedAt;

// Dead on this host: its pid is gone, or it was recorded on another boot (a reused
// pid looks alive after a reboot). A boot id nobody can read is never evidence.
function dead(h: HeavyHolder, sys: SystemProbe): boolean {
  if (h.host !== sys.hostname()) return false;
  const boot = sys.bootId();
  if (h.bootId !== null && boot !== null && h.bootId !== boot) return true;
  return !sys.pidAlive(h.pid);
}

// Whether the filesystem keeps real birth times, judged from the lock's parent dir: no birth time
// reads as 0, and Node without statx (old kernels, some sandboxes) reports ctime instead. Not judged
// from the lock dir itself: a dir made within one timestamp tick of a change has birth = ctime too.
const keepsBirth = (st: fs.Stats): boolean => st.birthtimeMs !== 0 && st.birthtimeMs !== st.ctimeMs;
// `marked` (stat'ed after the mark) is the dir `checked` was: same inode, and the same birth time
// where the filesystem keeps one.
const sameDir = (marked: fs.Stats, checked: fs.Stats, births: boolean): boolean =>
  marked.ino === checked.ino && (!births || marked.birthtimeMs === checked.birthtimeMs);
const sameFile = (a: fs.Stats, b: fs.Stats | undefined): boolean => b !== undefined && a.ino === b.ino && a.mtimeMs === b.mtimeMs;

// Put a dir that changed hands back. rename() replaces an empty dir already at `dir` (another
// holder's lock), so claim the name with mkdir first and rename onto that claim; when someone
// else has the name, leave the moved dir aside and say so.
function putBack(deps: Deps, stale: string, dir: string): void {
  if (tryMkdir(dir)) {
    fs.renameSync(stale, dir);
    return;
  }
  deps.log(`the heavy-job lock changed hands during a takeover; left ${stale} aside`);
}

// false: the dir is gone (released between the recheck and the mark).
function tryMark(file: string): boolean {
  try {
    fs.writeFileSync(file, "", { flag: "wx" });
    return true;
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code !== "ENOENT") throw e;
    return false;
  }
}

function tryMkdir(dir: string): boolean {
  try {
    fs.mkdirSync(dir);
    return true;
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code !== "EEXIST") throw e;
    return false;
  }
}

// Exclusive takeover, the tick lock's pattern (lock/lock.ts): only the process that
// creates <dir>.reclaim may move a dead holder's lock aside, it re-checks the holder
// inside the mutex, and it keeps the mutex until its own dir and holder record both
// exist, so two waiters on one dead holder can never both enter. The mutex holds its
// taker's record; it is cleared only when that taker is dead (an unreadable one after
// 60 s), and a taker removes it only while it is still its own. true: the lock is ours.
// `gone`: the holder to replace (null: the lock has no holder record); `due`: re-checked on the
// lock dir inside the mutex (its age, for the last-resort reclaim).
function reclaim(deps: Deps, dir: string, gone: HeavyHolder | null, me: HeavyHolder, due: (st: fs.Stats) => boolean): boolean {
  if (!tryMkdir(mutexDir(dir))) {
    const taker = parseHolder(mutexRecord(dir));
    const st = fs.statSync(mutexDir(dir), { throwIfNoEntry: false });
    const stale = taker === null ? st !== undefined && deps.now().getTime() - st.mtimeMs > UNREADABLE_GRACE_MS : dead(taker, deps.system);
    if (stale) fs.rmSync(mutexDir(dir), { recursive: true, force: true });
    return false;
  }
  try {
    writeHolder(mutexRecord(dir), me);
    // The age and the holder come from before the mark: writing it changes the dir's mtime.
    // The record's identity is taken before it is read, so one written in between never passes as checked.
    const record = fs.statSync(holderFile(dir), { throwIfNoEntry: false });
    const st = fs.statSync(dir, { throwIfNoEntry: false });
    const now = st === undefined ? null : readHolder(dir);
    if (st === undefined || !due(st) || (gone === null ? now !== null : !sameHolder(now, gone))) return false; // released, or it changed hands
    // A dir holding a file can't be rmdir'ed, so once marked it can't change hands. The mark's
    // name is one only this takeover knows; the inode number alone can't tell a replacement
    // apart, because Linux hands a freed inode to the next mkdir at once.
    const births = keepsBirth(fs.statSync(path.dirname(dir))); // the reclaim mutex lives there too
    const id = ulid(deps.now());
    const mark = `.reclaim-${id}`;
    if (!tryMark(path.join(dir, mark))) return false; // released in between
    const stale = `${dir}.stale-${id}`;
    let moved = false;
    try {
      // The decisive check, now the dir is pinned: the marked dir is the one checked, and its
      // holder record is the one checked (or its holder released it meanwhile).
      const marked = fs.statSync(dir, { throwIfNoEntry: false });
      const after = fs.statSync(holderFile(dir), { throwIfNoEntry: false });
      if (marked === undefined || !sameDir(marked, st, births) || (after !== undefined && !sameFile(after, record))) return false;
      // The dir exists, so nobody else can enter while its dead holder's record goes.
      fs.rmSync(holderFile(dir), { force: true });
      if (!tryRename(dir, stale)) return false;
      moved = true;
    } finally {
      if (!moved) fs.rmSync(path.join(dir, mark), { force: true });
    }
    // Moved a dir other than the one marked (it changed hands in between): put it back.
    if (!fs.existsSync(path.join(stale, mark))) {
      putBack(deps, stale, dir);
      return false;
    }
    // Ours, with anything a crashed takeover left in it.
    fs.rmSync(stale, { recursive: true, force: true });
    if (!tryMkdir(dir)) return false; // a waiter that wasn't reclaiming got in first
    try {
      writeHolder(holderFile(dir), me);
    } catch (e) {
      fs.rmdirSync(dir);
      throw e;
    }
    return true;
  } finally {
    if (sameHolder(parseHolder(mutexRecord(dir)), me)) fs.rmSync(mutexDir(dir), { recursive: true, force: true });
  }
}

// Only our own lock: the holder names us, or there is none (its write failed). A
// cleanup error is logged, never thrown over the job's own result.
function release(deps: Deps, dir: string, me: HeavyHolder): void {
  try {
    const h = parseHolder(holderFile(dir));
    if (h !== null && !sameHolder(h, me)) return;
    fs.rmSync(holderFile(dir), { force: true });
    fs.rmdirSync(dir);
  } catch (e) {
    deps.log(`could not release the heavy-job lock ${dir}: ${(e as Error).message}`);
  }
}

// One slot, tried once: take it when free, or take it over when its holder is dead on this host
// (killed, crashed, rebooted) or has held it past MAX_HEAVY_AGE_MS, whoever holds it. Returns the
// holder record to keep (it may name the takeover) or null when the slot stays busy.
function trySlot(deps: Deps, dir: string, slot: number, me: HeavyHolder): { me: HeavyHolder; written: boolean } | null {
  if (tryMkdir(dir)) return { me, written: false };
  const h = readHolder(dir);
  const st = fs.statSync(dir, { throwIfNoEntry: false });
  const aged = (s: fs.Stats): boolean => deps.now().getTime() - s.mtimeMs > MAX_HEAVY_AGE_MS;
  const isDead = h !== null && dead(h, deps.system);
  const why = isDead ? `dead pid ${h.pid} (${h.kind})` : st !== undefined && aged(st) ? `a lock held over 6 h${h === null ? " with no holder record" : ` by ${h.kind} (pid ${h.pid})`}` : null;
  if (why === null) return null;
  const taken: HeavyHolder = { ...me, reclaimed: why };
  if (!reclaim(deps, dir, h, taken, isDead ? () => true : aged)) return null;
  deps.log(`reclaiming the heavy-job lock${slot === 1 ? "" : ` slot ${slot}`} left by ${why}`);
  return { me: taken, written: true };
}

// Who holds a busy slot, for the wait and busy messages.
function busyText(deps: Deps, dir: string): { who: string; held: string } {
  const cur = readHolder(dir);
  const st = fs.statSync(dir, { throwIfNoEntry: false });
  return {
    who: cur === null ? "" : ` (${cur.kind}, pid ${cur.pid}, since ${cur.startedAt})`,
    held: st === undefined ? "" : `; held ${ageText(deps.now().getTime() - st.mtimeMs)}`,
  };
}

// A live holder of another slot running the same kind of job (two index builds of one repo, one
// suite twice): they would share files, so the later one gives its slot back and waits.
function twinOf(deps: Deps, dirs: string[], mine: string, me: HeavyHolder): HeavyHolder | null {
  for (const d of dirs) {
    if (d === mine) continue;
    const h = readHolder(d);
    if (h !== null && h.kind === me.kind && !sameHolder(h, me) && !dead(h, deps.system)) return h;
  }
  return null;
}

export async function withHeavyLock<T>(deps: Deps, kind: string, timeoutMs: number, fn: () => Promise<T>): Promise<T> {
  const dirs = heavyLockDirs(awStateDir(deps), deps.env);
  fs.mkdirSync(path.dirname(dirs[0]), { recursive: true });
  const sys = deps.system;
  const fresh: HeavyHolder = { kind, pid: sys.pid, host: sys.hostname(), bootId: sys.bootId(), startedAt: deps.now().toISOString(), reclaimed: null };
  let got: { dir: string; me: HeavyHolder } | null = null;
  // Bounded by attempts, not wall time, so a fake clock can't loop forever.
  const attempts = Math.ceil(timeoutMs / 1000) + 1;
  for (let i = 1; got === null; i++) {
    let twin: HeavyHolder | null = null;
    for (const [k, dir] of dirs.entries()) {
      const r = trySlot(deps, dir, k + 1, fresh);
      if (r === null) continue;
      try {
        if (!r.written) writeHolder(holderFile(dir), r.me);
      } catch (e) {
        release(deps, dir, r.me);
        throw e;
      }
      // Our record is written before we look, so of two same-kind jobs starting together at least one sees the other.
      twin = dirs.length === 1 ? null : twinOf(deps, dirs, dir, r.me);
      if (twin === null) got = { dir, me: r.me };
      else release(deps, dir, r.me);
      break;
    }
    if (got !== null) break;
    // One slot reads exactly as the single lock did; several name each slot's holder.
    const texts = dirs.map((d) => busyText(deps, d));
    const busy =
      twin !== null
        ? ` (${kind} already runs in another slot, pid ${twin.pid}, since ${twin.startedAt})`
        : dirs.length === 1
          ? `${texts[0].who}${texts[0].held}`
          : ` (all ${dirs.length} slots: ${texts.map((t, k) => `slot ${k + 1}${t.who}${t.held}`).join(" | ")})`;
    if (i >= attempts) throw new SindriError("SND-INDEX-001", `the heavy-job lock is busy${busy}`);
    if (i === 1) deps.log(twin !== null ? `waiting for the other ${kind} to finish` : dirs.length === 1 ? `waiting for the heavy-job lock${texts[0].who}` : `waiting for a heavy-job lock slot (all ${dirs.length} busy)`);
    await deps.sleep(1000);
  }
  try {
    return await fn();
  } finally {
    release(deps, got.dir, got.me);
  }
}

export interface HeavySlotState {
  slot: number;
  dir: string;
  held: boolean;
  holder: HeavyHolder | null;
  ageMs: number | null;
}

// Every slot, slot 1 first.
export function heavyLockState(stateRoot: string, now: () => Date, env: NodeJS.ProcessEnv): HeavySlotState[] {
  return heavyLockDirs(stateRoot, env).map((dir, i) => {
    const st = fs.statSync(dir, { throwIfNoEntry: false });
    if (st === undefined) return { slot: i + 1, dir, held: false, holder: null, ageMs: null };
    return { slot: i + 1, dir, held: true, holder: readHolder(dir), ageMs: now().getTime() - st.mtimeMs };
  });
}
