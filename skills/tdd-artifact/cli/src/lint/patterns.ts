// The linter's vocabulary. lint_packet.py imports build_tdd.py and takes TOKEN, CITE, token_id, Compiler.LINK_KEYS,
// LEGEND_TONES (as TONE_CLASSES) and bd.MARK from it; here they come from src/vocab.ts, the compiler's own copy. The
// linter's own patterns follow, each the Python source verbatim on the kernel's re (Python semantics), never a JS RegExp.
import * as py from "../py";

const re = py.re;

// The compiler's vocabulary, one copy (src/vocab.ts, PR 11): the linter reads the same objects the compiler links with.
export { CITE, LEGEND_TONES, LINK_KEYS, MARK, TOKEN, TONE_CLASSES, token_id } from "../vocab";

/** lint_packet.ATTRS: a trailing {attrs} block. */
export const ATTRS = re.compile(String.raw`\s*\{([^{}]*)\}\s*$`);
export const ISO_DATE = re.compile(String.raw`^\d{4}-\d{2}-\d{2}$`);
export const ANY_DATE = re.compile(String.raw`\b\d{4}-\d{2}-\d{2}\b`);

/** The rules, in report order (lint_packet.RULES). */
export const RULES = ["STRUCT", "DNUM", "REFS", "CITES", "DIAG", "SCHEMA", "FILES", "META", "AGENTS", "XDOC", "PARITY"] as const;
export type RuleId = (typeof RULES)[number];

export const FILE_LIMIT = 15 * 1024 * 1024;
export const PAGE_LIMIT = 16 * 1024 * 1024;
export const MEDIA_EXT = [".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".mp4", ".webm", ".mov"] as const;
export const CODE_EXT: ReadonlySet<string> = new Set(["ts", "tsx", "js", "mjs", "cjs", "py", "sql", "cs", "rb", "go", "rs", "sh", "css", "html", "graphql", "proto"]);

// The scaffold's placeholders (lint_packet.PLACEHOLDER_MARK, PLACEHOLDER_DATE, PLACEHOLDER_PROMPTS, PLACEHOLDER, COMMENT):
// what new_packet.py and `tdd new` write for the author to replace, in a fresh scaffold's order. FILES/placeholder and
// FILES/placeholder-text read them; test/lint/placeholders.test.ts keeps them equal to the Python and to the scaffold.
export const PLACEHOLDER_MARK = "PLACEHOLDER · REPLACE ME";
export const PLACEHOLDER_DATE = "YYYY-MM-DD";
export const PLACEHOLDER_PROMPTS: readonly string[] = [
  "<Title: the finding, not the topic>",
  "<The finding in two to four sentences: what changes, who it reaches first, and what stays exactly as it is today.>",
  "<the finding as the figure title>", "<one sentence that tells the reader where to start and what the arrows carry>", "<What is new>",
  "<The new tables, services or screens, in one short paragraph.>", "<What stays exactly as it is today.>", "<The one deliberate change>",
  "<A change to an existing system, and why it is worth it.>", "<Who sees it first, with a dated count and where the count came from.>", "<Role>",
  "<Moment>", "<What this person sees and does here, in two or three sentences: which screen, which component, and what it reads.>", "<State>",
  "<what the screen shows in that state, and why.>", "<What the shot shows, with the real names and numbers on it>",
  "<Caption: the state this shot shows>", "<What the shot shows>", "<Caption>", "<What the screen does today, in a few words>",
  "<Today's screen, as it is in production>", "<what today's screen does>", "<What changes for this person, in two sentences.>",
  "<the states and screens with no mock, and which built component each one reuses>",
  "<the PRD, the ticket and the threads, each with the date read>", "<The requirement, in the PRD's words>",
  "<The section, card or table that meets it>", "<The requirement>", "<What is still missing, and the open question that decides it (Q1)>",
  "<Why it is out of this design, and who agreed>", "<The flow in two or three sentences: the phases in order, and what each one reads and writes.>",
  "<What the reader should notice first, such as which step never runs inside a transaction>",
  "<the new and changed tables and what they reference>", "<Phase one>", "<ALIAS>",
  "<What the design adds here, and the existing code it hooks into.>", "<A detail of that step>", "<Another detail>", "<The next new step.>",
  "<Phase two>", "<A new step.>", "<An unchanged step this design relies on.>", "<The question, as the owner would ask it>",
  "<The background and the gap, with one concrete example in real numbers.>", "<Option A>", "<What A means, what it costs, and who it affects.>",
  "<Option B>", "<What B means, what it costs, and who it affects.>", "<The decision, in one line>", "<Owner>",
  "<The owner's answer in their words, then what it means for the design.>", "<In scope>", "<Out of scope, with the PRD non-goal it matches>",
  "<system name>", "<system response>", "<What it counts>", "<YYYY-MM-DD>", "<How each step is undone, and what stays written.>",
  "<Step one: dark behind the flag>", "<Step two>", "<new_table>", "<what one row is>",
  "<Why this shape: the alternatives, and the invariant the table keeps.>", "<new_column>", "<what it holds>", "<example id>", "<YYYY-MM-DD hh:mm>",
  "<What this row means>", "<What it stands for, and what it means in this design>", "<the owner's words>",
];
/** Any placeholder, longest first (sorted(..., key=len, reverse=True) is stable, as Array.prototype.sort is). */
export const PLACEHOLDER = re.compile(
  String.raw`(?<!\w)(?:` +
    [PLACEHOLDER_MARK, PLACEHOLDER_DATE, ...PLACEHOLDER_PROMPTS].sort((a, b) => py.cpLen(b) - py.cpLen(a)).map((t) => re.escape(t)).join("|") +
    ")",
);
/** build_doc.COMMENT: a comment on one line, which the compiler drops. */
export const COMMENT = re.compile(String.raw`^\s*<!--.*-->\s*$`);
