// tdd eval: eval_packet.py in TypeScript (same argv, flags, usage text, files, table and exit codes): 0 after a run
// (scores are measurements, not gates), 1 when --push fails, 2 on bad usage. Nothing leaves the machine without --push.
import * as py from "../src/py";
import { type Opts, runEval } from "../src/eval/run";
import { EVAL_PACKET_DOC } from "../src/eval/texts";

const VALUE_FLAGS = new Set(["--build", "--out", "--project", "--experiment", "--base", "--labels", "--session", "--aws-log-groups", "--aws-alarm-prefix", "--probe", "--skip"]);
const BOOL_FLAGS = new Set(["--push", "--with-judge", "--with-agent-probe", "--run-tests", "--no-gh", "--no-shots", "--json"]);

const outLine = (s: string) => void process.stdout.write(s + "\n");
const errLine = (s: string) => void process.stderr.write(s + "\n");

/** eval_packet.parse: the options, or the exit code it ends with. */
export function parse(argv: string[]): Opts | number {
  const opts: Record<string, string | boolean> = {};
  const pos: string[] = [];
  let i = 0;
  while (i < argv.length) {
    const x = argv[i]!;
    if (x === "-h" || x === "--help") {
      outLine(EVAL_PACKET_DOC);
      return 0;
    }
    if (VALUE_FLAGS.has(x)) {
      if (i + 1 >= argv.length) {
        errLine(`${x} needs a value`);
        return 2;
      }
      opts[py.replace(x.slice(2), "-", "_")] = argv[i + 1]!;
      i += 2;
    } else if (BOOL_FLAGS.has(x)) {
      opts[py.replace(x.slice(2), "-", "_")] = true;
      i += 1;
    } else if (x.startsWith("--")) {
      errLine(`unknown option ${x}\n${EVAL_PACKET_DOC}`);
      return 2;
    } else {
      pos.push(x);
      i += 1;
    }
  }
  if (pos.length !== 1) {
    errLine(EVAL_PACKET_DOC);
    return 2;
  }
  const packet = py.path.abspath(pos[0]!);
  if (!py.io.isdir(packet)) {
    errLine(`${pos[0]}: no such packet directory`);
    return 2;
  }
  return { ...opts, packet } as Opts;
}

export async function main(argv: string[]): Promise<number> {
  const o = parse(argv);
  if (typeof o === "number") return o;
  return runEval(o, { out: outLine, err: errLine });
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
