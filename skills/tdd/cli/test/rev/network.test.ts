// Proof that nothing leaves the machine without --push, and that no child gets a key. `tdd rev` runs end to end on a
// copy of the demo fixture packet with fake keys of every kind in the environment it starts with (the model and push
// keys, Linear, gh, AWS, a database url with a password, and arbitrary *_TOKEN / *_SECRET / *_KEY names). Every child
// is a logging shim (git, node, python3 and bun on PATH, the eval's python, the runner's bun) that records the secret
// names in its environment before it runs the real program: none may have one, except load-db's bun (DATABASE_URL).
// The run is also watched three ways for the network:
//   1. a preload traps fetch, sockets, TLS, DNS and Bun.connect in the runner's own process (loopback allowed);
//   2. every proxy variable points at a local recorder, so any child's HTTP client (Python's urllib, requests, httpx,
//      boto3) that tries to connect is counted there;
//   3. on macOS the whole process tree runs under sandbox-exec with outbound traffic denied except loopback and unix
//      sockets (Postgres stays reachable). The page's Google Fonts requests from artifact_check's Chromium are blocked
//      there too; they carry no packet data and no key, and the run tolerates them.
// Then: the recorder and the trap saw nothing, the push was skipped as not asked, the eval ran offline without
// --push, no push.json exists, run.json says pushed: false, and no fake key is in any written file or output.
import { afterAll, describe, expect, test } from "bun:test";
import { chmodSync, cpSync, existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const CLI = join(import.meta.dir, "..", "..");
const PRELOAD = join(import.meta.dir, "no-network.ts");
const DEMO = join(CLI, "test", "lint", "fixtures", "base", "demo-packet");
const SANDBOX = process.platform === "darwin" && existsSync("/usr/bin/sandbox-exec");
const PROFILE = '(version 1)(allow default)(deny network-outbound)(allow network-outbound (remote ip "localhost:*"))(allow network-outbound (remote unix-socket))';
const FAKES = {
  BRAINTRUST_API_KEY: "bt-fake-key-0000-not-a-real-key", ANTHROPIC_API_KEY: "sk-ant-fake-0000-not-a-real-key",
  OPENAI_API_KEY: "sk-fake-openai-0000-not-real", LINEAR_API_KEY: "lin_api_fake_0000_not_a_real_key", GH_TOKEN: "ghp_fake0000notarealtoken",
  GITHUB_TOKEN: "ghs_fake0000notarealtoken", AWS_ACCESS_KEY_ID: "AKIAFAKE0000NOTREAL", AWS_SECRET_ACCESS_KEY: "aws-fake-secret-0000-not-real",
  MY_SERVICE_TOKEN: "svc-fake-token-0000-not-real", SOME_SECRET: "some-fake-secret-0000-not-real", ANY_SIGNING_KEY: "fake-signing-key-0000-not-real",
};
const TEST_DB = process.env["TDD_TEST_DATABASE_URL"] ?? "postgres://localhost/tdd_system_test";
const VENV_PY = join(process.env["HOME"] ?? "", ".local", "share", "tdd-skill", "venv", "bin", "python");

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

/** Shims that log "<name>\t<secret names in its environment>\t<argv>" to $TDD_SHIM_LOG, then run the real program. */
function shims(dir: string): Record<string, string> {
  mkdirSync(dir, { recursive: true });
  const real: Record<string, string> = {
    git: Bun.which("git") ?? "/usr/bin/git", node: Bun.which("node") ?? "node", python3: Bun.which("python3") ?? "python3",
    bun: process.execPath, "eval-python": existsSync(VENV_PY) ? VENV_PY : (Bun.which("python3") ?? "python3"),
  };
  const out: Record<string, string> = {};
  for (const [name, target] of Object.entries(real)) {
    const p = join(dir, name);
    writeFileSync(p, [
      "#!/bin/sh",
      "names=$(/usr/bin/env | /usr/bin/sed -n 's/^\\([A-Za-z_][A-Za-z0-9_]*\\)=.*/\\1/p' | /usr/bin/grep -E '^AWS_|_KEY$|_TOKEN$|_SECRET$|_PASSWORD$|^DATABASE_URL$' | /usr/bin/sort | /usr/bin/tr '\\n' ' ')",
      `printf '%s\\t%s\\t%s\\n' '${name}' "$names" "$*" >> "$TDD_SHIM_LOG"`,
      `exec '${target}' "$@"`,
      "",
    ].join("\n"));
    chmodSync(p, 0o755);
    out[name] = p;
  }
  return out;
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
  test("nothing connects, nothing is pushed, no child gets a key, no key is written or printed", async () => {
    const packet = join(root, "house", "system-design", "demo-packet");
    mkdirSync(join(root, "house", "system-design"), { recursive: true });
    cpSync(DEMO, packet, { recursive: true });
    const log = join(root, "net.log");
    const shimLog = join(root, "shims.log");
    const sh = shims(join(root, "shims"));
    const rec = recorder();
    let r: { code: number; out: string; err: string };
    try {
      // the runner itself is started with the real bun, its children through the shims
      const p = Bun.spawn(sandboxed([process.execPath, "--preload", PRELOAD, join(CLI, "bin", "tdd.ts"), "rev", packet, "--no-shots", "--no-gh"]), {
        env: env({
          ...FAKES, ...proxyEnv(rec.port), TDD_NET_LOG: log, DATABASE_URL: TEST_DB, TDD_GENERATED: "2026-01-05T00:00:00Z",
          // TDD_PYTHON: the interpreter tdd eval's lint scorer runs lint_packet.py with
          TDD_SHIM_LOG: shimLog, TDD_REV_BUN: sh["bun"]!, TDD_EVAL_PYTHON: sh["eval-python"]!, TDD_PYTHON: sh["eval-python"]!, PATH: `${join(root, "shims")}:${process.env["PATH"] ?? ""}`,
        }),
        stdout: "pipe", stderr: "pipe",
      });
      const [out, err] = await Promise.all([new Response(p.stdout).text(), new Response(p.stderr).text()]);
      r = { code: await p.exited, out, err };
    } finally {
      rec.stop();
    }
    expect(rec.hits).toEqual([]);
    // every child, the in-process lint's git included: no secret, except DATABASE_URL for load-db's bun
    const children = readFileSync(shimLog, "utf8").trim().split("\n").map((l) => l.split("\t"));
    if (process.env["TDD_REV_SHIM_LOG_OUT"]) writeFileSync(process.env["TDD_REV_SHIM_LOG_OUT"], readFileSync(shimLog, "utf8"));
    const by = (name: string) => children.filter((c) => c[0] === name);
    expect(by("git").length).toBeGreaterThan(0);
    expect(by("bun").length).toBeGreaterThanOrEqual(3);
    expect(by("eval-python").length + by("python3").length).toBeGreaterThan(0);
    // tdd eval runs as a child of the shimmed bun, and its own children (node, bun, python, git) go through the shims too
    expect(by("bun").some((c) => (c[2] ?? "").includes(" eval "))).toBe(true);
    for (const [name, secrets, argv] of children) {
      const loadDb = name === "bun" && (argv ?? "").includes(" load-db ");
      expect([name, (argv ?? "").slice(0, 60), (secrets ?? "").trim()]).toEqual([name, (argv ?? "").slice(0, 60), loadDb ? "DATABASE_URL" : ""]);
    }
    expect(existsSync(log) ? readFileSync(log, "utf8") : "").toBe("");
    const lines = readFileSync(join(packet, "build", ".rev", "journal.jsonl"), "utf8").trim().split("\n").map((l) => JSON.parse(l));
    const steps = Object.fromEntries(lines.filter((e) => e.event === "step").map((e) => [e.step, e]));
    expect(steps["push"].outcome).toBe("skipped");
    expect(steps["push"].reason).toContain("--push not given");
    expect(lines[0].offline).toBe(true);
    expect(lines[0].keys).toEqual(expect.arrayContaining(Object.keys(FAKES)));
    expect(lines[0].eval).toBe("tdd eval (src/eval)");
    expect(steps["eval"].outcome).toBe("ok");
    const cmd: string[] = steps["eval"].cmds[0];
    expect(cmd[2]).toBe("eval");
    expect(cmd).not.toContain("--push");
    expect(cmd.slice(-3)).toEqual(["--no-gh", "--skip", "linear,aws"]);
    expect(JSON.parse(readFileSync(join(packet, "build", "eval", "run.json"), "utf8")).pushed).toBe(false);
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
