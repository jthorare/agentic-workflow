import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import { awStateDir, type Deps } from "../src/deps.js";
import { SindriError } from "../src/errors.js";
import { heavyLockDir, heavyLockDirs, heavyLockState, heavySlots, withHeavyLock } from "../src/index/heavy-lock.js";
import { fakeSystem, makeDeps as anySlotDeps } from "./helpers.js";

afterEach(() => vi.restoreAllMocks());

const LOCKS_SH = path.resolve(import.meta.dirname, "../../config/lib/locks.sh");
const holderPath = (root: string): string => `${heavyLockDir(root, {})}.holder.json`;

// The single-lock tests below run with one slot: each slot of the default two keeps exactly these
// semantics (holder record, waits, takeovers, release). The two-slot tests are at the end.
const makeDeps = (o: Partial<Deps> = {}): Deps => {
  const d = anySlotDeps(o);
  return { ...d, env: { AW_HEAVY_SLOTS: "1", ...d.env } };
};
// Slot 1, the plain lock.
const slot1 = (root: string, now: () => Date, env: NodeJS.ProcessEnv) => heavyLockState(root, now, env)[0];

describe("heavy-job lock", () => {
  it("holds the lock for the duration of the job and records the holder", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    const seen = await withHeavyLock(d, "index-build", 0, async () => slot1(root, d.now, d.env));
    expect(seen).toMatchObject({ held: true, holder: { kind: "index-build", pid: 4242, host: "test-host", reclaimed: null } });
    expect(slot1(root, d.now, d.env)).toMatchObject({ slot: 1, held: false, holder: null, ageMs: null });
  });

  it("puts the lock at locks/heavy-job.lock, or at $AW_HEAVY_JOB_LOCK, with the holder beside it", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    expect(heavyLockDir(root, {})).toBe(path.join(root, "locks", "heavy-job.lock"));
    const custom = path.join(root, "elsewhere", "my.lock");
    const d2 = makeDeps({ env: { ...d.env, AW_HEAVY_JOB_LOCK: custom } });
    expect(heavyLockDir(root, d2.env)).toBe(custom);
    await withHeavyLock(d2, "index-build", 0, async () => {
      expect(fs.readdirSync(custom)).toEqual([]);
      expect(fs.existsSync(`${custom}.holder.json`)).toBe(true);
    });
    expect(fs.existsSync(`${custom}.holder.json`)).toBe(false);
  });

  it("is the same lock as config/lib/locks.sh", async () => {
    const d = makeDeps();
    const dir = heavyLockDir(awStateDir(d), d.env);
    await withHeavyLock(d, "index-build", 0, async () => {
      const r = execFileSync("bash", ["-c", `source "${LOCKS_SH}"; acquire_lock "${dir}" 0 && echo got || echo busy`], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
      expect(r.trim()).toBe("busy");
    });
    execFileSync("bash", ["-c", `source "${LOCKS_SH}"; acquire_lock "${dir}" 0`]);
    expect(fs.existsSync(dir)).toBe(true);
    await expect(withHeavyLock(d, "index-build", 0, async () => 1)).rejects.toThrow(SindriError);
    execFileSync("bash", ["-c", `source "${LOCKS_SH}"; release_lock "${dir}"`]);
    expect(await withHeavyLock(d, "index-build", 0, async () => 2)).toBe(2);
  });

  it("waits, logs the holder once, then reports it when still busy", async () => {
    let slept = 0;
    const logs: string[] = [];
    const d = makeDeps({ sleep: async () => { slept++; }, log: (l) => logs.push(l) });
    const root = awStateDir(d);
    await withHeavyLock(d, "test-suite", 0, async () => {
      const err = await withHeavyLock(d, "index-build", 2000, async () => 0).catch((e: unknown) => e);
      expect(err).toBeInstanceOf(SindriError);
      expect((err as SindriError).code).toBe("SND-INDEX-001");
      expect((err as SindriError).message).toContain("test-suite, pid 4242");
    });
    expect(slept).toBe(2);
    expect(logs).toEqual([expect.stringContaining("waiting for the heavy-job lock (test-suite, pid 4242")]);
    fs.mkdirSync(heavyLockDir(root, d.env));
    const err = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err as SindriError).message).toMatch(/^the heavy-job lock is busy; held \d+ (min|h)$/);
    fs.writeFileSync(holderPath(root), JSON.stringify({ nope: 1 }));
    const err2 = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err2 as SindriError).message).toMatch(/^the heavy-job lock is busy; held /);
    expect(slot1(root, () => new Date(Date.now() + 60_000), d.env).ageMs).toBeGreaterThan(0);
  });

  it("reclaims a lock whose holder pid is dead on this host (and says so), but not a live pid or another host's", async () => {
    const logs: string[] = [];
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }), log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const plant = (holder: object): void => {
      fs.mkdirSync(heavyLockDir(root, d.env), { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify(holder));
    };
    plant({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" });
    const seen = await withHeavyLock(d, "next", 0, async () => slot1(root, d.now, d.env).holder);
    expect(seen).toMatchObject({ kind: "next", reclaimed: "dead pid 999 (index-build)" });
    expect(logs).toEqual(["reclaiming the heavy-job lock left by dead pid 999 (index-build)"]);
    plant({ kind: "index-build", pid: 4242, host: "test-host", startedAt: "t" });
    await expect(withHeavyLock(d, "next", 0, async () => 1)).rejects.toThrow("index-build, pid 4242");
    plant({ kind: "index-build", pid: 999, host: "other-host", startedAt: "t" });
    await expect(withHeavyLock(d, "next", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
  });

  it("releases the lock when the job throws, or when the holder record can't be written", async () => {
    const d = makeDeps();
    await expect(withHeavyLock(d, "x", 0, async () => { throw new Error("job failed"); })).rejects.toThrow("job failed");
    expect(slot1(awStateDir(d), d.now, d.env).held).toBe(false);
    vi.spyOn(fs, "writeFileSync").mockImplementationOnce(() => { throw new Error("disk full"); });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("disk full");
    expect(slot1(awStateDir(d), d.now, d.env).held).toBe(false);
  });

  it("rethrows unexpected mkdir errors", async () => {
    const d = makeDeps();
    const real = fs.mkdirSync;
    vi.spyOn(fs, "mkdirSync").mockImplementation((p, opts) => {
      if (String(p) === heavyLockDir(awStateDir(d), d.env)) throw Object.assign(new Error("no space left"), { code: "ENOSPC" });
      return real(p, opts);
    });
    await expect(withHeavyLock(d, "x", 0, async () => 0)).rejects.toThrow("no space left");
  });

  it("says busy without an age when the lock is released between the failed mkdir and the check", async () => {
    const d = makeDeps();
    const real = fs.mkdirSync;
    vi.spyOn(fs, "mkdirSync").mockImplementation((p, opts) => {
      if (String(p) === heavyLockDir(awStateDir(d), d.env)) throw Object.assign(new Error("exists"), { code: "EEXIST" });
      return real(p, opts);
    });
    await expect(withHeavyLock(d, "x", 0, async () => 0)).rejects.toThrow(/^the heavy-job lock is busy$/);
  });

  it("records the boot id and treats a holder from another boot as dead (a reused pid looks alive)", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    const plant = (holder: object): void => {
      fs.mkdirSync(heavyLockDir(root, d.env), { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify(holder));
    };
    expect(await withHeavyLock(d, "x", 0, async () => slot1(root, d.now, d.env).holder?.bootId)).toBe("boot-1");
    plant({ kind: "index-build", pid: 777, host: "test-host", bootId: "boot-0", startedAt: "t" });
    expect(await withHeavyLock(d, "next", 0, async () => slot1(root, d.now, d.env).holder?.reclaimed)).toBe("dead pid 777 (index-build)");
    // Same boot, or a record (or a host) that can't say which boot: the live pid holds it.
    for (const [holder, sys] of [
      [{ kind: "index-build", pid: 777, host: "test-host", bootId: "boot-1", startedAt: "t" }, fakeSystem()],
      [{ kind: "index-build", pid: 777, host: "test-host", startedAt: "t" }, fakeSystem()],
      [{ kind: "index-build", pid: 777, host: "test-host", bootId: "boot-0", startedAt: "t" }, fakeSystem({ bootId: () => null })],
    ] as const) {
      plant(holder);
      await expect(withHeavyLock({ ...d, system: sys }, "next", 0, async () => 1)).rejects.toThrow("index-build, pid 777");
      fs.rmSync(holderPath(root));
      fs.rmdirSync(heavyLockDir(root, d.env));
    }
  });

  it("reclaims a lock held over 6 h as a last resort: no holder record (a crashed locks.sh holder) or a reused live pid", async () => {
    const logs: string[] = [];
    const d = makeDeps({ log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const age = (hours: number): void => {
      const t = new Date(d.now().getTime() - hours * 3_600_000);
      fs.utimesSync(dir, t, t);
    };
    fs.mkdirSync(dir, { recursive: true });
    age(1);
    const busy = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((busy as SindriError).message).toBe("the heavy-job lock is busy; held 1 h");
    age(7);
    expect(await withHeavyLock(d, "next", 0, async () => slot1(root, d.now, d.env).holder?.reclaimed)).toBe("a lock held over 6 h with no holder record");
    fs.mkdirSync(dir);
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 4242, host: "test-host", startedAt: "t" }));
    const t = new Date(d.now().getTime() - 7 * 3_600_000);
    fs.utimesSync(holderPath(root), t, t);
    age(7);
    expect(await withHeavyLock(d, "next", 0, async () => slot1(root, d.now, d.env).holder?.reclaimed)).toBe("a lock held over 6 h by index-build (pid 4242)");
    expect(logs).toEqual([
      "reclaiming the heavy-job lock left by a lock held over 6 h with no holder record",
      "reclaiming the heavy-job lock left by a lock held over 6 h by index-build (pid 4242)",
    ]);
  });

  it("ignores a holder record older than the lock dir (a leftover beside a lock taken by locks.sh)", async () => {
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    const old = new Date(fs.statSync(dir).mtimeMs - 60_000);
    fs.utimesSync(holderPath(root), old, old);
    const err = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err as SindriError).message).toMatch(/^the heavy-job lock is busy; held /);
    expect(fs.existsSync(dir)).toBe(true);
    expect(slot1(root, d.now, d.env).holder).toBeNull();
  });

  it("lets exactly one of two waiters on the same dead holder take the lock over", async () => {
    const dead = fakeSystem({ pidAlive: (p) => p !== 999 });
    const a = makeDeps({ system: dead });
    const b = { ...a, system: { ...dead, pid: 5151 } };
    const root = awStateDir(a);
    fs.mkdirSync(heavyLockDir(root, a.env), { recursive: true });
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    const entered: string[] = [];
    let open!: () => void;
    const gate = new Promise<void>((r) => { open = r; });
    let pB: Promise<void> | null = null;
    const real = fs.readFileSync;
    // A has read the dead holder; before it acts, B runs its whole takeover (up to its job's first await).
    vi.spyOn(fs, "readFileSync").mockImplementation(((p: fs.PathOrFileDescriptor, o?: unknown) => {
      const text = real(p, o as BufferEncoding);
      if (String(p) === holderPath(root) && pB === null) {
        pB = Promise.resolve();
        pB = withHeavyLock(b, "b", 0, async () => { entered.push("b"); await gate; });
      }
      return text;
    }) as typeof fs.readFileSync);
    const errA = await withHeavyLock(a, "a", 0, async () => { entered.push("a"); }).catch((e: unknown) => e);
    expect((errA as SindriError).code).toBe("SND-INDEX-001");
    expect((errA as SindriError).message).toContain("(b, pid 5151");
    expect(entered).toEqual(["b"]);
    expect(slot1(root, a.now, a.env).holder).toMatchObject({ kind: "b", pid: 5151, reclaimed: "dead pid 999 (index-build)" });
    open();
    await pB;
    expect(slot1(root, a.now, a.env).held).toBe(false);
    expect(fs.readdirSync(path.dirname(heavyLockDir(root, a.env)))).toEqual([]);
  });

  it("leaves a live taker's reclaim mutex alone and clears a dead or long-unreadable one", async () => {
    let slept = 0;
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 && p !== 998 }), sleep: async () => { slept++; } });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const mutex = `${dir}.reclaim`;
    const plant = (taker: object | null, ageMs = 0): void => {
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
      fs.mkdirSync(mutex, { recursive: true });
      if (taker !== null) fs.writeFileSync(path.join(mutex, "holder.json"), JSON.stringify(taker));
      const t = new Date(d.now().getTime() - ageMs);
      fs.utimesSync(mutex, t, t);
    };
    plant({ kind: "index-build", pid: 31, host: "test-host", startedAt: "t" });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
    expect(fs.existsSync(mutex)).toBe(true);
    plant(null, 1000);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
    expect(fs.existsSync(mutex)).toBe(true);
    fs.rmSync(mutex, { recursive: true });
    for (const [taker, age] of [[{ kind: "index-build", pid: 998, host: "test-host", startedAt: "t" }, 0], [null, 61_000]] as const) {
      plant(taker, age);
      expect(await withHeavyLock(d, "x", 1000, async () => 2)).toBe(2);
      expect(fs.existsSync(mutex)).toBe(false);
    }
    expect(slept).toBe(2);
    // The mutex vanished between the failed mkdir and its age check: just try again.
    plant(null);
    const realStat = fs.statSync;
    vi.spyOn(fs, "statSync").mockImplementation(((p: fs.PathLike, o?: fs.StatSyncOptions) => (String(p) === mutex ? undefined : realStat(p, o))) as typeof fs.statSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
  });

  it("backs off when the dead holder's lock is released, moved or replaced during the takeover", async () => {
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const plant = (): void => {
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    };
    const realMkdir = fs.mkdirSync;
    const realRename = fs.renameSync;
    const onMutex = (act: () => void): void => {
      vi.spyOn(fs, "mkdirSync").mockImplementation(((p: fs.PathLike, o?: fs.MakeDirectoryOptions) => {
        if (String(p) === `${dir}.reclaim`) act();
        return realMkdir(p, o);
      }) as typeof fs.mkdirSync);
    };
    // Released (by a human) before the mutex: the recheck finds no dir.
    plant();
    onMutex(() => { fs.rmSync(holderPath(root), { force: true }); if (fs.existsSync(dir)) fs.rmdirSync(dir); });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    vi.restoreAllMocks();
    // The rename finds the dir gone.
    plant();
    vi.spyOn(fs, "renameSync").mockImplementationOnce(() => { throw Object.assign(new Error("gone"), { code: "ENOENT" }); });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    vi.restoreAllMocks();
    expect(fs.readdirSync(dir)).toEqual([]); // the mark is cleaned up
    // The dir was replaced between the recheck and the rename: put the new one back.
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
      fs.rmSync(from, { recursive: true, force: true });
      realMkdir(from);
      realRename(from, to);
    }) as typeof fs.renameSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    expect(fs.existsSync(dir)).toBe(true);
    expect(fs.readdirSync(dir)).toEqual([]);
    expect(fs.readdirSync(path.dirname(dir)).filter((n) => n.includes(".stale-"))).toEqual([]);
    vi.restoreAllMocks();
    fs.rmdirSync(dir);
    // Released between the recheck and the mark: back off. Any other write error is thrown.
    plant();
    const realWrite = fs.writeFileSync;
    const onMark = (err: string): void => {
      vi.spyOn(fs, "writeFileSync").mockImplementation(((f: fs.PathOrFileDescriptor, data: string, o?: fs.WriteFileOptions) => {
        if (String(f).includes(".reclaim-")) throw Object.assign(new Error(err), { code: err });
        return realWrite(f, data, o);
      }) as typeof fs.writeFileSync);
    };
    onMark("ENOENT");
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    vi.restoreAllMocks();
    onMark("EACCES");
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("EACCES");
    vi.restoreAllMocks();
    expect(fs.readdirSync(dir)).toEqual([]);
    expect(fs.existsSync(`${dir}.reclaim`)).toBe(false);
    fs.rmdirSync(dir);
    // A waiter that wasn't reclaiming took the freed dir first.
    plant();
    vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
      realRename(from, to);
      realMkdir(from);
    }) as typeof fs.renameSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    expect(fs.existsSync(dir)).toBe(true);
    expect(fs.existsSync(`${dir}.reclaim`)).toBe(false);
  });

  it("puts back a dir that was swapped in at the rename (it holds no mark of ours)", async () => {
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    const realMkdir = fs.mkdirSync;
    const realRename = fs.renameSync;
    // The marked dir goes (rm -r) and another holder's dir is what the rename moves.
    vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
      fs.rmSync(from, { recursive: true, force: true });
      realMkdir(from);
      realRename(from, to);
    }) as typeof fs.renameSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    vi.restoreAllMocks();
    expect(fs.existsSync(dir)).toBe(true);
    expect(fs.readdirSync(dir)).toEqual([]);
    expect(fs.readdirSync(path.dirname(dir)).filter((n) => n.includes(".stale-"))).toEqual([]);
  });

  describe("once the dead holder's dir is marked", () => {
    const record = (pid: number): string => JSON.stringify({ kind: "index-build", pid, host: "test-host", startedAt: `t${pid}` });
    const setup = (logs: string[] = []) => {
      const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }), log: (l) => logs.push(l) });
      const root = awStateDir(d);
      const dir = heavyLockDir(root, d.env);
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(holderPath(root), record(999));
      return { d, root, dir };
    };
    // Runs `act` just before the mark is written, or just after it with `after`.
    const aroundMark = (act: () => void, after = false): void => {
      const realWrite = fs.writeFileSync;
      vi.spyOn(fs, "writeFileSync").mockImplementation(((f: fs.PathOrFileDescriptor, data: string, o?: fs.WriteFileOptions) => {
        const marking = String(f).includes(".reclaim-");
        if (marking && !after) act();
        realWrite(f, data, o);
        if (marking && after) act();
      }) as typeof fs.writeFileSync);
    };
    const leftovers = (dir: string): string[] => fs.readdirSync(path.dirname(dir)).filter((n) => n.includes(".stale-") || n.endsWith(".reclaim"));

    it("backs off when the dir was replaced, with the freed inode number, between the recheck and the mark", async () => {
      const { d, root, dir } = setup();
      const ino = fs.statSync(dir).ino;
      // The dead holder's dir goes and a live sindri holder takes the name (Linux reuses the inode).
      aroundMark(() => {
        fs.rmSync(holderPath(root));
        fs.rmdirSync(dir);
        fs.mkdirSync(dir);
        fs.writeFileSync(holderPath(root), record(4243));
        const later = new Date(Date.now() + 5000);
        fs.utimesSync(holderPath(root), later, later);
      });
      const realStat = fs.statSync;
      vi.spyOn(fs, "statSync").mockImplementation(((p: fs.PathLike, o?: fs.StatSyncOptions) => {
        const st = realStat(p, o);
        // Same inode and no usable birth time (birth = ctime, as without statx): only the record tells.
        return st !== undefined && String(p) === dir ? Object.assign(st, { ino, birthtimeMs: st.ctimeMs }) : st;
      }) as typeof fs.statSync);
      await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
      vi.restoreAllMocks();
      expect(fs.readdirSync(dir)).toEqual([]);
      expect(JSON.parse(fs.readFileSync(holderPath(root), "utf8")).pid).toBe(4243);
      expect(leftovers(dir)).toEqual([]);
    });

    it("backs off when the marked dir is not the one checked, or is gone", async () => {
      const { d, root, dir } = setup();
      // Same inode, another birth time: a replacement on a filesystem that keeps birth times.
      const realStat = fs.statSync;
      let marked = false;
      aroundMark(() => { marked = true; }, true);
      vi.spyOn(fs, "statSync").mockImplementation(((p: fs.PathLike, o?: fs.StatSyncOptions) => {
        const st = realStat(p, o);
        return st !== undefined && marked && String(p) === dir ? Object.assign(st, { birthtimeMs: Number(st.birthtimeMs) + 1 }) : st;
      }) as typeof fs.statSync);
      await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
      vi.restoreAllMocks();
      expect(fs.readdirSync(dir)).toEqual([]);
      // Removed outright (rm -r) right after the mark. (The backed-off mark left the old record
      // older than the dir, which reads as no holder: write it again.)
      fs.writeFileSync(holderPath(root), record(999));
      aroundMark(() => fs.rmSync(dir, { recursive: true }), true);
      await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
      vi.restoreAllMocks();
      expect(fs.existsSync(dir)).toBe(false);
      expect(leftovers(dir)).toEqual([]);
    });

    it("takes a dead holder's lock where there is no real birth time: 0, or ctime (Node without statx)", async () => {
      for (const birth of [(st: fs.Stats | fs.BigIntStats) => Number(st.ctimeMs), () => 0]) {
        const { d, dir } = setup();
        const realStat = fs.statSync;
        vi.spyOn(fs, "statSync").mockImplementation(((p: fs.PathLike, o?: fs.StatSyncOptions) => {
          const st = realStat(p, o);
          // The platform-wide condition: the lock dir and its parent both lack a real birth time.
          return st !== undefined && (String(p) === dir || String(p) === path.dirname(dir)) ? Object.assign(st, { birthtimeMs: birth(st) }) : st;
        }) as typeof fs.statSync);
        expect(await withHeavyLock(d, "x", 0, async () => 2)).toBe(2);
        vi.restoreAllMocks();
        expect(leftovers(dir)).toEqual([]);
      }
    });

    it("backs off on a replacement whose birth time equals its ctime when the filesystem keeps birth times", async () => {
      const { d, root, dir } = setup();
      const ino = fs.statSync(dir).ino;
      // A record-less holder (locks.sh, the wrapper) takes the name between the recheck and the
      // mark, reusing the inode; within one coarse timestamp tick its birth time equals the
      // ctime the mark gives it. The parent shows the filesystem keeps real birth times.
      aroundMark(() => {
        fs.rmSync(holderPath(root));
        fs.rmdirSync(dir);
        fs.mkdirSync(dir);
      });
      const realStat = fs.statSync;
      vi.spyOn(fs, "statSync").mockImplementation(((p: fs.PathLike, o?: fs.StatSyncOptions) => {
        const st = realStat(p, o);
        return st !== undefined && String(p) === dir ? Object.assign(st, { ino, birthtimeMs: st.ctimeMs }) : st;
      }) as typeof fs.statSync);
      await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
      vi.restoreAllMocks();
      expect(fs.readdirSync(dir)).toEqual([]);
      expect(leftovers(dir)).toEqual([]);
    });

    it("backs off when a holder record appears on a lock that had none", async () => {
      const { d, root, dir } = setup();
      fs.rmSync(holderPath(root));
      const t = new Date(d.now().getTime() - 7 * 3_600_000);
      fs.utimesSync(dir, t, t);
      aroundMark(() => fs.writeFileSync(holderPath(root), record(4243)));
      await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
      vi.restoreAllMocks();
      expect(fs.readdirSync(dir)).toEqual([]);
      expect(leftovers(dir)).toEqual([]);
    });

    it("takes the lock when its holder released it meanwhile: the marked dir can't have changed hands", async () => {
      const { d, root, dir } = setup();
      // The holder's own release removes its record; its rmdir fails on the mark.
      aroundMark(() => fs.rmSync(holderPath(root)), true);
      expect(await withHeavyLock(d, "x", 0, async () => fs.readdirSync(dir))).toEqual([]);
      vi.restoreAllMocks();
      expect(fs.existsSync(dir)).toBe(false);
      expect(leftovers(dir)).toEqual([]);
    });

    it("clears a mark a crashed takeover left in the dir, and removes its own when the rename throws", async () => {
      const { d, root, dir } = setup();
      fs.writeFileSync(path.join(dir, ".reclaim-01OLD"), "");
      const t = new Date(d.now().getTime() - 7 * 3_600_000);
      fs.utimesSync(dir, t, t);
      fs.rmSync(holderPath(root));
      expect(await withHeavyLock(d, "x", 0, async () => fs.readdirSync(dir))).toEqual([]);
      expect(fs.existsSync(dir)).toBe(false);
      expect(leftovers(dir)).toEqual([]);
      fs.mkdirSync(dir);
      fs.writeFileSync(holderPath(root), record(999));
      vi.spyOn(fs, "renameSync").mockImplementationOnce(() => { throw Object.assign(new Error("EACCES"), { code: "EACCES" }); });
      await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("EACCES");
      vi.restoreAllMocks();
      expect(fs.readdirSync(dir)).toEqual([]);
      expect(leftovers(dir)).toEqual([]);
    });

    it("puts a moved dir back without replacing a newer lock, and says so when the name is taken", async () => {
      const logs: string[] = [];
      const { d, dir } = setup(logs);
      const realMkdir = fs.mkdirSync;
      const realRename = fs.renameSync;
      // The marked dir goes (rm -r), a new holder's dir is moved instead, and a third takes the name.
      vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
        fs.rmSync(from, { recursive: true, force: true });
        realMkdir(from);
        realRename(from, to);
        realMkdir(from);
      }) as typeof fs.renameSync);
      await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
      vi.restoreAllMocks();
      const stale = fs.readdirSync(path.dirname(dir)).filter((n) => n.includes(".stale-"));
      expect(stale).toHaveLength(1);
      expect(fs.existsSync(dir)).toBe(true);
      expect(logs).toContainEqual(`the heavy-job lock changed hands during a takeover; left ${path.join(path.dirname(dir), stale[0] ?? "")} aside`);
    });
  });

  it("keeps the mutex until the holder is written, frees the dir when that write fails, and never removes another taker's mutex", async () => {
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const plant = (): void => {
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    };
    plant();
    const realWrite = fs.writeFileSync;
    vi.spyOn(fs, "writeFileSync").mockImplementation(((p: fs.PathOrFileDescriptor, data: string) => {
      if (String(p) === holderPath(root)) {
        expect(fs.existsSync(`${dir}.reclaim`)).toBe(true);
        throw new Error("disk full");
      }
      realWrite(p, data);
    }) as typeof fs.writeFileSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("disk full");
    expect(fs.existsSync(dir)).toBe(false);
    expect(fs.existsSync(`${dir}.reclaim`)).toBe(false);
    vi.restoreAllMocks();
    // Our mutex was cleared and retaken by another taker mid-reclaim: leave theirs.
    plant();
    const realRename = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
      fs.rmSync(`${dir}.reclaim`, { recursive: true });
      fs.mkdirSync(`${dir}.reclaim`);
      realRename(from, to);
    }) as typeof fs.renameSync);
    expect(await withHeavyLock(d, "x", 0, async () => 3)).toBe(3);
    expect(fs.existsSync(`${dir}.reclaim`)).toBe(true);
  });

  it("releases only its own lock, and a failed release never masks the job's result", async () => {
    const logs: string[] = [];
    const d = makeDeps({ log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    // Someone else's holder now names the lock: leave it to them.
    await withHeavyLock(d, "x", 0, async () => {
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "other", pid: 31, host: "test-host", startedAt: "t2" }));
    });
    expect(slot1(root, d.now, d.env).holder).toMatchObject({ kind: "other" });
    fs.rmSync(holderPath(root));
    fs.rmdirSync(dir);
    expect(await withHeavyLock(d, "x", 0, async () => { fs.rmdirSync(dir); return 7; })).toBe(7);
    expect(logs).toEqual([expect.stringContaining(`could not release the heavy-job lock ${dir}`)]);
  });
});

describe("heavy-job lock slots", () => {
  const LOCKS = (cmd: string): string => execFileSync("bash", ["-c", `source "${LOCKS_SH}"; ${cmd}`], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();

  it("has two slots by default: <lock> and <lock>.2; AW_HEAVY_SLOTS sets the count, a bad value keeps the default", () => {
    const root = "/state";
    const base = heavyLockDir(root, {});
    expect(heavyLockDirs(root, {})).toEqual([base, `${base}.2`]);
    expect(heavyLockDirs(root, { AW_HEAVY_SLOTS: "3" })).toEqual([base, `${base}.2`, `${base}.3`]);
    expect(heavyLockDirs(root, { AW_HEAVY_SLOTS: "1" })).toEqual([base]);
    for (const bad of ["0", "-1", "two", "1.5", ""]) expect(heavySlots({ AW_HEAVY_SLOTS: bad })).toBe(2);
  });

  it("lets a second job in while one holds slot 1, says busy (SND-INDEX-001) when both are held, and leaves nothing behind", async () => {
    const logs: string[] = [];
    const d = anySlotDeps({ log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const [one, two] = heavyLockDirs(root, d.env);
    await withHeavyLock(d, "test-suite", 0, async () => {
      await withHeavyLock(d, "index-build", 0, async () => {
        const state = heavyLockState(root, d.now, d.env);
        expect(state.map((s) => [s.dir, s.held, s.holder?.kind])).toEqual([[one, true, "test-suite"], [two, true, "index-build"]]);
        const err = await withHeavyLock(d, "third", 2000, async () => 0).catch((e: unknown) => e);
        expect((err as SindriError).code).toBe("SND-INDEX-001");
        expect((err as SindriError).message).toMatch(/^the heavy-job lock is busy \(all 2 slots: slot 1 \(test-suite, pid 4242, since [^)]+\); held \d+ min \| slot 2 \(index-build, pid 4242, since [^)]+\); held \d+ min\)$/);
      });
      // Slot 2 is free again: the next job takes it.
      expect(await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now, d.env)[1].holder?.kind)).toBe("next");
    });
    expect(logs).toEqual(["waiting for a heavy-job lock slot (all 2 busy)"]);
    expect(fs.readdirSync(path.dirname(one))).toEqual([]);
  });

  it("counts a locks.sh holder of <lock> as slot 1, and locks.sh's acquire_any_lock sees sindri's slots", async () => {
    const d = anySlotDeps();
    const [one, two] = heavyLockDirs(awStateDir(d), d.env);
    fs.mkdirSync(path.dirname(one), { recursive: true });
    LOCKS(`acquire_lock "${one}" 0`);
    expect(await withHeavyLock(d, "index-build", 0, async () => heavyLockState(awStateDir(d), d.now, d.env).map((s) => s.held))).toEqual([true, true]);
    await withHeavyLock(d, "index-build", 0, async () => {
      expect(LOCKS(`acquire_any_lock "${one}" 2 0 || echo busy`)).toBe("busy");
    });
    LOCKS(`release_lock "${one}"`);
    await withHeavyLock(d, "index-build", 0, async () => {
      expect(LOCKS(`acquire_any_lock "${one}" 2 0`)).toBe(two);
      LOCKS(`release_lock "${two}"`);
    });
  });

  it("takes a slot over per slot: a dead holder of slot 2, or slot 2 held over 6 h, while slot 1's live holder is left alone", async () => {
    const logs: string[] = [];
    const d = anySlotDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }), log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const [one, two] = heavyLockDirs(root, d.env);
    const plant = (dir: string, holder: object | null, hours = 0): void => {
      fs.mkdirSync(dir, { recursive: true });
      const t = new Date(d.now().getTime() - hours * 3_600_000);
      fs.utimesSync(dir, t, t);
      if (holder !== null) {
        fs.writeFileSync(`${dir}.holder.json`, JSON.stringify(holder));
        fs.utimesSync(`${dir}.holder.json`, t, t);
      }
    };
    plant(one, { kind: "live", pid: 4243, host: "test-host", startedAt: "t" });
    plant(two, { kind: "index-build", pid: 999, host: "test-host", startedAt: "t" });
    const seen = await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now, d.env).map((s) => s.holder));
    expect(seen[0]).toMatchObject({ kind: "live", reclaimed: null });
    expect(seen[1]).toMatchObject({ kind: "next", reclaimed: "dead pid 999 (index-build)" });
    plant(two, null, 7);
    expect(await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now, d.env)[1].holder?.reclaimed)).toBe("a lock held over 6 h with no holder record");
    expect(logs).toEqual([
      "reclaiming the heavy-job lock slot 2 left by dead pid 999 (index-build)",
      "reclaiming the heavy-job lock slot 2 left by a lock held over 6 h with no holder record",
    ]);
    expect(heavyLockState(root, d.now, d.env).map((s) => s.held)).toEqual([true, false]);
    expect(fs.readFileSync(`${one}.holder.json`, "utf8")).toContain('"live"');
  });

  it("never runs two jobs of the same kind at once: the second waits for the first, not for a slot", async () => {
    const logs: string[] = [];
    const d = anySlotDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }), log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const [one, two] = heavyLockDirs(root, d.env);
    const other = anySlotDeps({ ...d, system: fakeSystem({ pid: 5151, pidAlive: (p) => p !== 999 }) });
    await withHeavyLock(other, "index-build:web", 0, async () => {
      const err = await withHeavyLock(d, "index-build:web", 1000, async () => 0).catch((e: unknown) => e);
      expect((err as SindriError).code).toBe("SND-INDEX-001");
      expect((err as SindriError).message).toMatch(/^the heavy-job lock is busy \(index-build:web already runs in another slot, pid 5151, since /);
      // Its slot went back: nothing of it is left, and another kind still gets in.
      expect(fs.existsSync(two)).toBe(false);
      expect(await withHeavyLock(d, "index-build:api", 0, async () => fs.existsSync(two))).toBe(true);
    });
    expect(logs).toEqual(["waiting for the other index-build:web to finish"]);
    // A dead twin doesn't count (its slot is taken over as usual).
    fs.mkdirSync(one, { recursive: true });
    fs.writeFileSync(`${one}.holder.json`, JSON.stringify({ kind: "index-build:web", pid: 999, host: "test-host", startedAt: "t" }));
    expect(await withHeavyLock(d, "index-build:web", 0, async () => heavyLockState(root, d.now, d.env)[0].holder?.reclaimed)).toBe("dead pid 999 (index-build:web)");
    expect(fs.readdirSync(path.dirname(one))).toEqual([]);
  });
});
