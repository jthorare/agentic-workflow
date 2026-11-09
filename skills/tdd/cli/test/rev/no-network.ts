// Test preload (`bun --preload test/rev/no-network.ts bin/tdd.ts rev ...`): every way the runner's own process could
// open a connection off this machine (fetch, node:net / node:tls sockets, node:dns lookups, Bun.connect) records the
// attempt to $TDD_NET_LOG and fails. Loopback and unix sockets stay open: the system database is local.
import { appendFileSync } from "node:fs";
import dns from "node:dns";
import net from "node:net";
import tls from "node:tls";

const LOG = process.env["TDD_NET_LOG"] || "";
const LOCAL = /^(localhost|127\.\d+\.\d+\.\d+|::1|\[::1\])$/i;
const show = (x: unknown): string => {
  try {
    return typeof x === "string" ? x : x instanceof URL ? x.href : x instanceof Request ? x.url : JSON.stringify(x);
  } catch {
    return String(x);
  }
};
const hostOf = (x: unknown): string | null => {
  if (typeof x === "string") { try { return new URL(x).hostname; } catch { return x; } }
  if (x instanceof URL) return x.hostname;
  if (x instanceof Request) return new URL(x.url).hostname;
  if (x && typeof x === "object") {
    const o = x as { host?: unknown; hostname?: unknown; path?: unknown; unix?: unknown };
    if (typeof o.path === "string" || typeof o.unix === "string") return "localhost";
    return typeof o.hostname === "string" ? o.hostname : typeof o.host === "string" ? o.host : "localhost";
  }
  return null;
};
const record = (what: string): never => {
  if (LOG) appendFileSync(LOG, what + "\n");
  throw new Error(`network disabled in this test: ${what}`);
};
const guard = <F extends (...a: never[]) => unknown>(name: string, orig: F): F =>
  ((...args: unknown[]) => {
    const h = hostOf(args[0]);
    if (h !== null && LOCAL.test(h)) return (orig as unknown as (...a: unknown[]) => unknown)(...args);
    return record(`${name} ${show(args[0])}`);
  }) as unknown as F;

globalThis.fetch = (async (input: unknown) => record(`fetch ${show(input)}`)) as unknown as typeof fetch;
const sockConnect = net.Socket.prototype.connect;
net.Socket.prototype.connect = function (this: net.Socket, ...args: unknown[]) {
  const h = hostOf(args[0]);
  if (h !== null && LOCAL.test(h)) return (sockConnect as (...a: unknown[]) => net.Socket).apply(this, args);
  return record(`net.Socket.connect ${show(args[0])}`);
} as never;
(net as { connect: unknown }).connect = guard("net.connect", net.connect);
(net as { createConnection: unknown }).createConnection = guard("net.createConnection", net.createConnection);
(tls as { connect: unknown }).connect = guard("tls.connect", tls.connect);
(dns as { lookup: unknown }).lookup = guard("dns.lookup", dns.lookup);
(dns.promises as { lookup: unknown }).lookup = guard("dns.promises.lookup", dns.promises.lookup);
try {
  (Bun as unknown as { connect: unknown }).connect = guard("Bun.connect", Bun.connect);
} catch {
  // read-only in some builds; fetch and node:net cover the clients the runner uses
}
