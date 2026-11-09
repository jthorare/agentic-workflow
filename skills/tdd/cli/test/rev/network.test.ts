// Proof that nothing leaves the machine without --push. `tdd rev` runs end to end on a copy of the demo fixture packet
// with fake keys in the environment (BRAINTRUST_API_KEY, ANTHROPIC_API_KEY, LINEAR_API_KEY), and is watched three ways:
//   1. a preload traps fetch, sockets, TLS, DNS and Bun.connect in the runner's own process (loopback allowed);
//   2. every proxy variable points at a local recorder, so any child's HTTP client (Python's urllib, requests, httpx,
//      boto3) that tries to connect is counted there;
//   3. on macOS the whole process tree runs under sandbox-exec with outbound traffic denied except loopback and unix
//      sockets (Postgres stays reachable). The page's Google Fonts requests from artifact_check's Chromium are blocked
//      there too; they carry no packet data and no key, and the run tolerates them.
// Then: the recorder and the trap saw nothing, the push was skipped as not asked, the eval ran offline without
// --push, no push.json exists, run.json says pushed: false, and no fake key is in any written file or output.
import { afterAll, describe, expect, test } from "bun:test";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const CLI = join(import.meta.dir, "..", "..");
const PRELOAD = join(import.meta.dir, "no-network.ts");
const DEMO = join(CLI, "test", "lint", "fixtures", "base", "demo-packet");
const SANDBOX = process.platform === "darwin" && existsSync("/usr/bin/sandbox-exec");
const PROFILE = '(version 1)(allow default)(deny network-outbound)(allow network-outbound (remote ip "localhost:*"))(allow network-outbound (remote unix-socket))';
const FAKES = { BRAINTRUST_API_KEY: "bt-fake-key-0000-not-a-real-key", ANTHROPIC_API_KEY: "sk-ant-fake-0000-not-a-real-key", LINEAR_API_KEY: "lin_api_fake_0000_not_a_real_key" };
const TEST_DB = process.env["TDD_TEST_DATABASE_URL"] ?? "postgres://localhost/tdd_system_test";

const root = mkdtempSync(join(tmpdir(), "tdd-rev-net-"));
afterAll(() => rmSync(root, { recursive: true, force: true }));

/** A TCP recorder on loopback: every connection's first line (a proxy CONNECT or GET) is kept. */
function recorder() {
  const hits: string[] = [];
  const server = Bun.listen({
    hostname: "127.0.0.1",
    port: 0,
    socket: {
      open() {},
      data(socket, data) {
        hits.push(new TextDecoder().decode(data).split("\r\n")[0] ?? "");
        socket.end();
      },
    },
  });
  return { hits, port: server.port, stop: () => server.stop(true) };
}

function env(extra: Record<string, string>): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) {
    if (v === undefined || /(_API_KEY|_TOKEN|_SECRET)$/.test(k) || /^(HTTPS?|ALL|NO)_PROXY$/i.test(k)) continue;
    out[k] = v;
  }
  return { ...out, ...extra };
}
const proxyEnv = (port: number) => {
  const u = `http://127.0.0.1:${port}`;
  return { HTTP_PROXY: u, HTTPS_PROXY: u, ALL_PROXY: u, http_proxy: u, https_proxy: u, all_proxy: u, NO_PROXY: "", no_proxy: "" };
};
const sandboxed = (cmd: string[]) => (SANDBOX ? ["/usr/bin/sandbox-exec", "-p", PROFILE, ...cmd] : cmd);

describe("the watchers work", () => {
  test("the preload records a fetch", () => {
    const log = join(root, "trap.log");
    const r = Bun.spawnSync(["bun", "--preload", PRELOAD, "-e", "await fetch('https://example.invalid/x').catch(() => {}); console.log('done')"],
      { env: env({ TDD_NET_LOG: log }), stdout: "pipe", stderr: "pipe" });
    expect(r.stdout.toString()).toContain("done");
    expect(readFileSync(log, "utf8")).toContain("fetch https://example.invalid/x");
  });
  test.skipIf(!Bun.which("python3"))("the recorder sees a child's HTTPS request through the proxy variables", async () => {
    const rec = recorder();
    try {
      // async: the recorder lives on this process's event loop, which spawnSync would block
      const p = Bun.spawn(sandboxed(["python3", "-I", "-c", "import urllib.request\ntry: urllib.request.urlopen('https://api.braintrust.dev/v1', timeout=5)\nexcept Exception: pass"]),
        { env: env(proxyEnv(rec.port)), stdout: "pipe", stderr: "pipe" });
      await p.exited;
      expect(rec.hits.join("\n")).toContain("CONNECT api.braintrust.dev:443");
    } finally {
      rec.stop();
    }
  }, 30_000);
});

describe("tdd rev with fake keys and no --push", () => {
  test("nothing connects, nothing is pushed, no key is written or printed", async () => {
    const packet = join(root, "house", "system-design", "demo-packet");
    mkdirSync(join(root, "house", "system-design"), { recursive: true });
    cpSync(DEMO, packet, { recursive: true });
    const log = join(root, "net.log");
    const rec = recorder();
    let r: { code: number; out: string; err: string };
    try {
      const p = Bun.spawn(sandboxed(["bun", "--preload", PRELOAD, join(CLI, "bin", "tdd.ts"), "rev", packet, "--no-shots", "--no-gh"]), {
        env: env({ ...FAKES, ...proxyEnv(rec.port), TDD_NET_LOG: log, DATABASE_URL: TEST_DB, TDD_GENERATED: "2026-01-05T00:00:00Z" }),
        stdout: "pipe", stderr: "pipe",
      });
      const [out, err] = await Promise.all([new Response(p.stdout).text(), new Response(p.stderr).text()]);
      r = { code: await p.exited, out, err };
    } finally {
      rec.stop();
    }
    expect(rec.hits).toEqual([]);
    expect(existsSync(log) ? readFileSync(log, "utf8") : "").toBe("");
    const lines = readFileSync(join(packet, "build", ".rev", "journal.jsonl"), "utf8").trim().split("\n").map((l) => JSON.parse(l));
    const steps = Object.fromEntries(lines.filter((e) => e.event === "step").map((e) => [e.step, e]));
    expect(steps["push"].outcome).toBe("skipped");
    expect(steps["push"].reason).toContain("--push not given");
    expect(lines[0].offline).toBe(true);
    expect(lines[0].keys).toEqual(expect.arrayContaining(Object.keys(FAKES)));
    if (steps["eval"].outcome === "ok") {
      const cmd: string[] = steps["eval"].cmds[0];
      expect(cmd).not.toContain("--push");
      expect(cmd.slice(-3)).toEqual(["--no-gh", "--skip", "linear,aws"]);
      expect(JSON.parse(readFileSync(join(packet, "build", "eval", "run.json"), "utf8")).pushed).toBe(false);
    } else {
      expect(steps["eval"].reason).toContain("not run");
    }
    expect(existsSync(join(packet, "build", "eval", "push.json"))).toBe(false);
    for (const s of ["compile", "app-build", "hydrate", "agents", "lint"]) expect(steps[s].outcome).toBe("ok");
    expect(r!.code).toBe(0);
    let checked = 0;
    for (const d of readdirSync(packet, { recursive: true, withFileTypes: true })) {
      if (!d.isFile() || /\.(png|jpe?g|gif|webp)$/i.test(d.name)) continue;
      const dir = (d as unknown as { parentPath?: string; path?: string }).parentPath ?? (d as unknown as { path: string }).path;
      const text = readFileSync(join(dir, d.name), "latin1");
      for (const v of Object.values(FAKES)) expect(text.includes(v)).toBe(false);
      checked++;
    }
    expect(checked).toBeGreaterThan(30);
    for (const v of Object.values(FAKES)) expect((r!.out + r!.err).includes(v)).toBe(false);
  }, 300_000);
});
