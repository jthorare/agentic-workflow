// `tdd rev` on the real packets ($TDD_PARITY_PACKETS, ":"-separated; skipped when unset or without python3): on a copy
// at <tmp>/<repo>/system-design/<slug> (no build/), every step is ok (push skipped as not asked; the eval, load-db and
// artifact-check may be skipped only with a named reason: no venv, no database, no checker), then SKILL.md's Python
// steps run on a fresh copy at the same path (lint.json records the packet path), and content.json, the agents/ tree
// and lint.json must be byte-identical. TDD_GENERATED and TZ are pinned for both.
import { afterAll, describe, expect, test } from "bun:test";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, renameSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join, relative } from "node:path";

const CLI = join(import.meta.dir, "..", "..");
const SKILL = join(CLI, "..");
const HAVE_PYTHON = Bun.spawnSync(["python3", "-I", "-c", "pass"]).exitCode === 0;
const PACKETS = (process.env["TDD_PARITY_PACKETS"] ?? "").split(":").filter((p) => p && existsSync(p));
const GENERATED = "2026-11-09T06:00:00Z";
const TEST_DB = process.env["TDD_TEST_DATABASE_URL"] ?? "postgres://localhost/tdd_system_test";

const root = mkdtempSync(join(tmpdir(), "tdd-rev-packets-"));
afterAll(() => rmSync(root, { recursive: true, force: true }));

const ENV = (): Record<string, string> => {
  const e: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) if (v !== undefined && !/(_API_KEY|_TOKEN|_SECRET)$/.test(k)) e[k] = v;
  return { ...e, TDD_GENERATED: GENERATED, TZ: "UTC", DATABASE_URL: TEST_DB };
};
function sh(cmd: string[], cwd: string) {
  const r = Bun.spawnSync(cmd, { cwd, env: ENV(), stdout: "pipe", stderr: "pipe" });
  return { code: r.exitCode ?? -1, out: r.stdout.toString(), err: r.stderr.toString() };
}
function copy(src: string, dst: string) {
  rmSync(dst, { recursive: true, force: true });
  mkdirSync(dirname(dst), { recursive: true });
  cpSync(src, dst, { recursive: true, filter: (p) => p !== join(src, "build") });
}
function tree(dir: string): Record<string, string> {
  const out: Record<string, string> = {};
  for (const d of readdirSync(dir, { recursive: true, withFileTypes: true })) {
    if (!d.isFile()) continue;
    const base = (d as unknown as { parentPath?: string; path?: string }).parentPath ?? (d as unknown as { path: string }).path;
    const p = join(base, d.name);
    out[relative(dir, p)] = new Bun.CryptoHasher("sha256").update(readFileSync(p)).digest("hex");
  }
  return out;
}

describe.skipIf(!HAVE_PYTHON || PACKETS.length === 0)("tdd rev on real packets ($TDD_PARITY_PACKETS)", () => {
  for (const src of PACKETS) {
    const slug = basename(src);
    test(`${slug}: every step ok, and the page inputs match SKILL.md's Python steps byte for byte`, () => {
      const work = join(root, basename(dirname(dirname(src))), "system-design", slug);
      const kept = join(root, `${slug}.rev-build`);
      copy(src, work);
      const rev = sh(["bun", join(CLI, "bin", "tdd.ts"), "rev", work, "--no-shots", "--no-gh", "--json"], CLI);
      const report = JSON.parse(rev.out) as { exit: number; steps: { step: string; outcome: string; detail: string }[] };
      const by = Object.fromEntries(report.steps.map((s) => [s.step, s]));
      for (const s of ["compile", "app-build", "hydrate", "agents", "lint"]) expect([s, by[s]!.outcome]).toEqual([s, "ok"]);
      expect(by["push"]!.outcome).toBe("skipped");
      for (const s of ["eval", "load-db", "artifact-check"]) {
        if (by[s]!.outcome !== "ok") expect([s, by[s]!.outcome, by[s]!.detail]).toEqual([s, "skipped", expect.stringContaining("not run")]);
      }
      expect(report.exit).toBe(0);
      renameSync(join(work, "build"), kept);

      copy(src, work);
      const py = ["python3", "-I"];
      expect(sh([...py, join(SKILL, "scripts", "build_tdd.py"), `${slug}.md`, "build"], work).code).toBe(0);
      expect(sh(["bun", "run", "build.ts", join(work, "build")], join(SKILL, "app")).code).toBe(0);
      expect(sh([...py, join(SKILL, "scripts", "write_agents.py"), "build"], work).code).toBe(0);
      expect([0, 1]).toContain(sh([...py, join(SKILL, "scripts", "lint_packet.py"), work, "--json", join(work, "build", "lint.json")], work).code);

      for (const f of ["content.json", "lint.json"]) {
        expect([f, readFileSync(join(kept, f), "utf8") === readFileSync(join(work, "build", f), "utf8")]).toEqual([f, true]);
      }
      expect(tree(join(kept, "agents"))).toEqual(tree(join(work, "build", "agents")));
    }, 900_000);
  }
});
