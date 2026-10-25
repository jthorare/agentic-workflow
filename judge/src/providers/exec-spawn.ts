import { execFile } from "node:child_process";

import type { Spawn } from "./cli-common.js";

/**
 * The real Spawn every agent-CLI provider runs through (cli.ts wires one per
 * binary). Providers never see child_process directly, so their tests inject
 * fakes; this adapter is exercised against a real `node` child instead.
 */
export function makeExecSpawn(bin: string): Spawn {
  return (args, opts) =>
    new Promise((resolve) => {
      const child = execFile(bin, args, { cwd: opts.cwd, env: opts.env, timeout: opts.timeoutMs, maxBuffer: 10 * 1024 * 1024 }, (error, stdout) => {
        if (error === null) return resolve({ stdout, code: 0, timedOut: false });
        if (error.code === "ENOENT") return resolve({ stdout: "", code: null, timedOut: false, notFound: true });
        resolve({ stdout, code: typeof error.code === "number" ? error.code : null, timedOut: error.killed === true });
      });
      // Every supported CLI waits on (or reads) stdin when it's an open pipe —
      // `claude -p` for up to 3s, `codex exec` appends it to the prompt. The
      // judge pipes input to the child only when a provider asks for it (opts.stdin,
      // which keeps big prompts out of argv); otherwise close it immediately.
      child.stdin?.on("error", () => undefined); // the child may exit before reading it all
      child.stdin?.end(opts.stdin);
    });
}
