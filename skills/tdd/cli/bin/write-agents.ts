// tdd agents: write_agents.py in TypeScript (same argv, usage text, lane, --check problem list, summary line, exit codes).
import { BUDGET, Lane } from "../src/agents/lane";
import { SystemExit, check, write } from "../src/agents/write";
import { abspath, isdir, pjoin, repr, str as S, utf8Len } from "../src/agents/_py";
import { WRITE_AGENTS_DOC } from "../src/agents/texts";

function fail(msg: string): number {
  process.stderr.write(msg + "\n");
  return 1;
}

export async function main(argv: string[]): Promise<number> {
  const a = { packet: null as string | null, out: null as string | null, budget: BUDGET, check: false, link: false, force: false, quiet: false };
  const pos: string[] = [];
  let i = 0;
  while (i < argv.length) {
    const x = argv[i]!;
    if (x === "--packet" || x === "--out" || x === "--budget-tokens") {
      if (i + 1 >= argv.length) return fail(`${x} needs a value`);
      const v = argv[i + 1]!;
      if (x === "--budget-tokens") {
        const t = v.trim();
        if (!/^[+-]?[0-9]+(?:_[0-9]+)*$/.test(t)) return fail(`ValueError: invalid literal for int() with base 10: ${repr(v)}`);
        a.budget = Number(t.replaceAll("_", ""));
      } else if (x === "--packet") a.packet = v;
      else a.out = v;
      i += 2;
    } else if (x === "--check" || x === "--link-images" || x === "--force" || x === "--quiet") {
      if (x === "--check") a.check = true;
      else if (x === "--link-images") a.link = true;
      else if (x === "--force") a.force = true;
      else a.quiet = true;
      i += 1;
    } else if (x.startsWith("--")) return fail(`unknown option ${x}\n${WRITE_AGENTS_DOC}`);
    else {
      pos.push(x);
      i += 1;
    }
  }
  if (pos.length !== 1) return fail(WRITE_AGENTS_DOC);
  if (!isdir(pos[0]!)) return fail(`${pos[0]}: no such directory`);
  try {
    if (a.check) {
      const problems = check(pos[0]!, a.out);
      for (const p of problems) process.stdout.write(`stale: ${p}\n`);
      if (problems.length) return 1;
      process.stdout.write(`agents lane current: ${abspath(a.out || pjoin(pos[0]!, "agents"))}\n`);
      return 0;
    }
    const r = write(pos[0]!, a.packet, a.out, a.budget, !a.link, a.force);
    const lane = r.lane;
    for (const w of lane.warn) process.stderr.write(`warning: ${w}\n`);
    const texts = [...lane.files].filter(([, v]) => typeof v === "string").map(([f]) => f);
    if (lane instanceof Lane) {
      const [lp, lt] = lane.largest;
      const sections = lane.c.get("sections");
      process.stdout.write(`wrote ${r.out} (${r.mode}): ${lane.files.size} files (${lane.files.size - texts.length} image copies), ` +
        `${lane.slices.length} section slices for ${Array.isArray(sections) ? sections.length : 0} sections, ${lane.decisionIndex.length} decisions, ` +
        `${lane.prIndex.length} PRs, ${lane.citations.length} citations, ${texts.filter((f) => f.startsWith("sql/")).length} sql, ` +
        `${texts.filter((f) => f.startsWith("code/")).length} code, ${lane.tables.length} schema tables, ${lane.imageRows.length} images, ` +
        `map ${lane.mapCounts.entities} entities/${lane.mapCounts.edges} edges; largest slice ${S(lp)} ~${lt} tokens; ` +
        `${lane.overBudget.length} over the ${lane.budget}-token budget; README ${utf8Len(lane.files.get("README.md") as string)} bytes\n`);
    } else {
      process.stdout.write(`wrote ${r.out} (${r.mode}): ${lane.files.size} files, ${lane.slices.length} section slices, ${lane.counts.sql} sql, ` +
        `${lane.counts.code} code, ${lane.counts.decisions} decisions, ${lane.imageRows.length} images; ${lane.overBudget.length} over budget\n`);
    }
    return 0;
  } catch (e) {
    if (e instanceof SystemExit) {
      if (e.message) process.stderr.write(e.message + "\n");
      return e.code;
    }
    throw e;
  }
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
