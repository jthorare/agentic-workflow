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
