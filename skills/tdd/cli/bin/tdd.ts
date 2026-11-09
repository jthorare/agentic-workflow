#!/usr/bin/env bun
// tdd: one entry for the skill's commands. Each subcommand lives in bin/<name>.ts and is loaded only when asked for,
// so a command that is not ported yet reports so instead of failing on import. The Python scripts remain the
// documented path until the cutover (research/bun-port-2026.md §5.3); during the overlap both must agree byte for byte.
const COMMANDS: Record<string, { file: string; python: string; help: string }> = {
  build:    { file: "build-tdd.ts",        python: "scripts/build_tdd.py",         help: "compile <slug>.md + lanes into build/content.json" },
  lint:     { file: "lint-packet.ts",      python: "scripts/lint_packet.py",       help: "lint a packet (eleven rules, errors first)" },
  agents:   { file: "write-agents.ts",     python: "scripts/write_agents.py",      help: "write the agents/ lane from content.json" },
  strip:    { file: "strip-page.ts",       python: "scripts/strip_page.py",        help: "strip a built page to plain html, text and outline" },
  hotspots: { file: "diagram-hotspots.ts", python: "scripts/diagram_hotspots.py",  help: "list the boxes of a tldraw SVG into <key>.hotspots.json" },
  annotate: { file: "annotate-hotspots.ts",python: "scripts/annotate_hotspots.py", help: "propose href / image / note per box" },
  new:      { file: "new-packet.ts",       python: "scripts/new_packet.py",        help: "scaffold a packet from the template" },
  eval:     { file: "eval-packet.ts",      python: "scripts/eval_packet.py",       help: "score a revision; push to Braintrust only with --push" },
  "load-db":{ file: "tdd-load-db.ts",      python: "",                             help: "load a built, linted packet into the system database (D24)" },
  rev:      { file: "rev.ts",              python: "",                             help: "run one revision end to end" },
};

const [cmd, ...rest] = Bun.argv.slice(2);
if (!cmd || cmd === "--help" || cmd === "-h") {
  console.log("usage: tdd <command> [args]\n");
  for (const [name, c] of Object.entries(COMMANDS)) console.log(`  ${name.padEnd(9)} ${c.help}`);
  process.exit(cmd ? 0 : 2);
}
const c = COMMANDS[cmd];
if (!c) { console.error(`tdd: unknown command ${cmd}`); process.exit(2); }
const path = new URL(`./${c.file}`, import.meta.url);
if (!(await Bun.file(path).exists())) {
  console.error(`tdd ${cmd}: not ported yet${c.python ? `; run python3 -I ${c.python} instead` : ""}`);
  process.exit(2);
}
const mod = await import(path.href);
const main = mod.main ?? mod.default;
if (typeof main !== "function") { console.error(`tdd ${cmd}: ${c.file} exports no main()`); process.exit(2); }
process.exit((await main(rest)) ?? 0);
