// Proof that nothing leaves the machine without --push: with a (fake) BRAINTRUST_API_KEY in the environment, the
// TypeScript eval runs under a preload that traps fetch, sockets, TLS, DNS and Bun.connect; the trap log stays empty,
// no push.json is written, run.json says pushed: false, stdout ends with the "not pushed" line, and the key's value
// appears in no written file and no output (keys are Redacted config). The trap itself is checked first.
import { afterAll, describe, expect, test } from "bun:test";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { cleanEnv, cleanup, CLI, HAVE_PYTHON, packets, snapshot, tmp, TS_EVAL } from "./harness";

afterAll(cleanup);

const PRELOAD = join(CLI, "test", "eval", "no-network.ts");
const PACKETS = HAVE_PYTHON ? packets() : [];
const FAKE = "bt-fake-key-0000-not-a-real-key";

describe("the network trap", () => {
  test("records and blocks a fetch", () => {
    const log = join(tmp("trap"), "net.log");
    const r = Bun.spawnSync(["bun", "--preload", PRELOAD, "-e", "await fetch('https://example.invalid/x').catch(() => {}); console.log('done')"],
      { env: cleanEnv({ TDD_NET_LOG: log }), stdout: "pipe", stderr: "pipe" });
    expect(r.stdout.toString()).toContain("done");
    expect(readFileSync(log, "utf8")).toContain("fetch https://example.invalid/x");
  });
});

describe.skipIf(!PACKETS.length)("a key in the environment without --push sends nothing", () => {
  test("BRAINTRUST_API_KEY set, no --push", () => {
    const base = tmp("nonet");
    const pk = snapshot(PACKETS[0]!, base);
    const log = join(base, "net.log");
    const out = join(base, "out");
    const r = Bun.spawnSync(["bun", "--preload", PRELOAD, TS_EVAL, pk, "--no-shots", "--no-gh", "--out", out],
      { env: cleanEnv({ BRAINTRUST_API_KEY: FAKE, TDD_NET_LOG: log }), stdout: "pipe", stderr: "pipe" });
    const stdout = r.stdout.toString();
    expect(r.exitCode).toBe(0);
    expect(existsSync(log) ? readFileSync(log, "utf8") : "").toBe("");
    expect(existsSync(join(out, "push.json"))).toBe(false);
    expect(JSON.parse(readFileSync(join(out, "run.json"), "utf8")).pushed).toBe(false);
    expect(stdout.trimEnd().split("\n").at(-1)).toBe("not pushed: run.json holds the dataset record and 6 experiments a --push would send");
    let checked = 0;
    for (const d of readdirSync(out, { recursive: true, withFileTypes: true })) {
      if (!d.isFile() || d.name.endsWith(".png")) continue;
      const dir = (d as unknown as { parentPath?: string; path?: string }).parentPath ?? (d as unknown as { path: string }).path;
      expect(readFileSync(join(dir, d.name), "utf8")).not.toContain(FAKE);
      checked++;
    }
    expect(checked).toBeGreaterThanOrEqual(10);
    expect(stdout + r.stderr.toString()).not.toContain(FAKE);
  }, 120_000);
});
