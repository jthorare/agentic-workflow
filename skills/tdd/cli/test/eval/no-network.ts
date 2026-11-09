// Test preload (`bun --preload test/eval/no-network.ts ...`): every way this process could open a connection
// (fetch, node:net / node:tls sockets, node:dns lookups, Bun.connect) records the attempt to $TDD_NET_LOG and fails.
// network.test.ts runs the eval under it and asserts the log stays empty.
import { appendFileSync } from "node:fs";
import dns from "node:dns";
import net from "node:net";
import tls from "node:tls";

const LOG = process.env.TDD_NET_LOG || "";
const record = (what: string): never => {
  if (LOG) appendFileSync(LOG, what + "\n");
  throw new Error(`network disabled in this test: ${what}`);
};
const show = (x: unknown): string => {
  try {
    return typeof x === "string" ? x : x instanceof URL ? x.href : x instanceof Request ? x.url : JSON.stringify(x);
  } catch {
    return String(x);
  }
};

globalThis.fetch = (async (input: unknown) => record(`fetch ${show(input)}`)) as unknown as typeof fetch;
net.Socket.prototype.connect = function (this: net.Socket, ...args: unknown[]) {
  return record(`net.Socket.connect ${show(args[0])}`);
} as never;
(net as { connect: unknown }).connect = (...args: unknown[]) => record(`net.connect ${show(args[0])}`);
(net as { createConnection: unknown }).createConnection = (...args: unknown[]) => record(`net.createConnection ${show(args[0])}`);
(tls as { connect: unknown }).connect = (...args: unknown[]) => record(`tls.connect ${show(args[0])}`);
(dns as { lookup: unknown }).lookup = (...args: unknown[]) => record(`dns.lookup ${show(args[0])}`);
(dns.promises as { lookup: unknown }).lookup = async (...args: unknown[]) => record(`dns.promises.lookup ${show(args[0])}`);
try {
  (Bun as unknown as { connect: unknown }).connect = (...args: unknown[]) => record(`Bun.connect ${show(args[0])}`);
} catch {
  // Bun.connect is read-only in some builds; fetch and node:net cover every client this eval uses
}
