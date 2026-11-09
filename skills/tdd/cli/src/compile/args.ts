// build_tdd.py's argv: the same flags, usage text (the module docstring, verbatim), messages and exit codes.
import * as py from "../py";
import { SystemExit } from "./errors";

/** build_tdd.__doc__, verbatim: sys.exit(__doc__) prints it as the usage text. */
export const DOC = "Compile a technical design document from a markdown source in the house TDD template into content.json: the\nstructured document the React renderer (../app) turns into the claude.ai artifact page and write_agents.py turns into\nthe agents/ lane.\n\nUsage:\n  build_tdd.py <source.md> <out-dir> [--meta <meta.json>] [--diagrams <dir>] [--prs <dir>] [--schema <dir>] [--no-copy] [--strict]\n\nReuses ~/.claude/skills/system-design/scripts/build_doc.py (the markdown subset, inline renderer, assets) and\nbuild_prs.py (the prs/pr-NN.md loader) as libraries; nothing in system-design is changed. Set SYSTEM_DESIGN_SKILL to\npoint at another copy of that skill.\n\nSource conventions (templates/tdd-template.md explains each with an example):\n  # Title                                        the page title (meta.json carries the header fields)\n  ## Section title {chip: text; #id; lead: no}   numbered 1..n; the first paragraph is the lead unless lead: no\n  ### Subsection {#id}                           a navigable subsection\n  ::: kind Title · one-liner {attrs} ... :::     containers: cards, persona, duo, today, fold, group, decisions,\n                                                 decision, options, steps, cols, col, glossary, callout (nest with ::::)\n  [diagram: key · Title {fit; #id}]              a figure; the next [legend: label = tone | ...] and [reading: ...] lines\n                                                 belong to it; diagrams/<key>.hotspots.json adds hover hotspots\n  ![alt](mockups/x.png \"Caption\")                a mock shot (inside a persona box) or a still\n  `LEG/File.ts:12-40`                            a path:line citation; linked to repo_url at sha when meta.json has them\n  D7, E3, F3.2, R2, PR 7, OS-01, API             reference tokens, linked to their definition when the page defines one\n  Trailing {small}, {muted}, {lead}, {#id} on a paragraph set its class or id.  [chip:tone text] is an inline chip.\n\nOutput: <out-dir>/content.json (meta, nav, sections as a block tree with inline HTML strings, defs for hover cards,\ndecisions, citations, glossary, schema tables, reference tokens linked and unlinked) and <out-dir>/content.files.json\n(the publish manifest: published path -> source for every image and diagram, plus missing and warnings). Referenced\nfiles are copied into <out-dir> at their published paths unless --no-copy. Exit 0; 1 on bad input, or any warning with\n--strict.";

export interface Args {
  src: string;
  out: string;
  meta: string | null;
  diagrams: string | null;
  prs: string | null;
  schema: string | null;
  no_copy: boolean;
  strict: boolean;
}

const FLAGS: Record<string, "meta" | "diagrams" | "prs" | "schema"> = { "--meta": "meta", "--diagrams": "diagrams", "--prs": "prs", "--schema": "schema" };
const BOOLS: Record<string, "no_copy" | "strict"> = { "--no-copy": "no_copy", "--strict": "strict" };

/** build_tdd.parse_args: throws SystemExit(1, message) where the Python calls sys.exit(message). */
export function parse_args(argv: string[]): Args {
  const a: Omit<Args, "src" | "out"> = { meta: null, diagrams: null, prs: null, schema: null, no_copy: false, strict: false };
  const pos: string[] = [];
  let i = 0;
  while (i < argv.length) {
    const x = argv[i]!;
    if (Object.hasOwn(FLAGS, x)) {
      if (i + 1 >= argv.length) throw new SystemExit(1, `${x} needs a value`);
      a[FLAGS[x]!] = argv[i + 1]!;
      i += 2;
    } else if (Object.hasOwn(BOOLS, x)) {
      a[BOOLS[x]!] = true;
      i += 1;
    } else if (x.startsWith("--")) {
      throw new SystemExit(1, `unknown option ${x}\n${DOC}`);
    } else {
      pos.push(x);
      i += 1;
    }
  }
  if (pos.length !== 2) throw new SystemExit(1, DOC);
  const [src, out] = pos as [string, string];
  if (!py.io.isfile(src)) throw new SystemExit(1, `${src}: no such file`);
  return { ...a, src, out };
}
