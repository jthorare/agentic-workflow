// AGENTS: write_agents.check(build_dir) over build/agents, in-process through the TypeScript agents lane (src/agents);
// skipped with a note on a compiled lint or without build/agents (lint_packet.Lint.r_agents). A check that raises is an
// AGENTS/check-failed finding with the exception as Python would name it, never a crash.
import { Effect } from "effect";
import * as py from "../../py";
import { Packet } from "../packet";
import { Agents } from "../services";
import { get, isDict, pyException, str, truthy } from "../pyops";

export const agents = Effect.gen(function* () {
  const p = yield* Packet;
  const lane = yield* Agents;
  const adir = py.path.join(p.build, "agents");
  if (p.compiled || !p.fs.isdir(adir)) {
    p.note(`AGENTS: no ${p.rel(adir)}; skipped`);
    return;
  }
  let res: readonly unknown[];
  try {
    res = lane.check(p.build);
  } catch (e) {
    const [name, msg] = pyException(e);
    p.add("AGENTS", "check-failed", "error", `write_agents.check raised ${name}: ${msg}`, p.rel(adir));
    return;
  }
  for (const q of res ?? []) {
    if (isDict(q)) {
      const sev = py.lower(str(get(q, "severity", "error"))).startsWith("warn") ? "warn" : "error";
      const check = truthy(get(q, "check")) ? str(get(q, "check")) : "problem";
      const msg = truthy(get(q, "message")) ? get(q, "message") : truthy(get(q, "msg")) ? get(q, "msg") : py.json.dumps(q);
      const file = truthy(get(q, "file")) ? str(get(q, "file")) : p.rel(adir);
      const line = get(q, "line");
      p.add("AGENTS", check, sev, msg, file, typeof line === "number" ? line : null);
    } else {
      p.add("AGENTS", "problem", "error", str(q), p.rel(adir));
    }
  }
});
