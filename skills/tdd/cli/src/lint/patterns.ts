// The linter's vocabulary. lint_packet.py imports build_tdd.py and takes TOKEN, CITE, token_id, Compiler.LINK_KEYS,
// LEGEND_TONES (as TONE_CLASSES) and bd.MARK from it, so these mirror build_tdd.py, not lint_packet.py's own fallbacks.
// Every pattern is the Python source verbatim, compiled by the kernel's re (Python semantics), never a JS RegExp.
import * as py from "../py";

const re = py.re;

// ---------------------------------------------------------------- until vocab.ts lands (N6)
// These seven names are src/vocab.ts's (tdd/compile, PR 11). When it merges, this block becomes
// `export { TOKEN, token_id, CITE, LINK_KEYS, LEGEND_TONES, TONE_CLASSES, MARK } from "../vocab";`.

/** build_tdd.TOKEN: D7, E3, F3.2, R2, PR 7, PR 12a, OS-01 ... (group 1 is the token). */ // until vocab.ts lands (N6)
export const TOKEN = re.compile(String.raw`(?<![\w#/.\-])((?:D|E|F|R|A|M|Q|G|OS|LN|SS|NFR|ADR)-?\d+(?:\.\d+)?|PR\s?\d+[a-z]?)(?![\w\-]|\.\d)`);

/** build_tdd.token_id: "PR 7" -> "pr-7", "F3.2" -> "f3-2". */ // until vocab.ts lands (N6)
export function token_id(tok: string): string {
  let t = py.replace(py.lower(py.str.strip(tok)), " ", "-");
  t = re.sub(String.raw`^pr(\d)`, String.raw`pr-\1`, t);
  return py.replace(t, ".", "-");
}

/** build_tdd.CITE: `path/to/file.ts:12-40`. */ // until vocab.ts lands (N6)
export const CITE = re.compile(String.raw`^((?:[A-Z]{2,5}/)?[\w@./-]+\.(?:ts|tsx|js|mjs|cjs|py|sql|json|md|cs|yaml|yml|toml|rb|go|rs|sh|css|html|txt|graphql|proto))(?::(\d+)(?:-(\d+))?)?$`);

/** build_tdd.LEGEND_TONES. */ // until vocab.ts lands (N6)
export const LEGEND_TONES: ReadonlyMap<string, string> = new Map([
  ["new", "green"], ["changed", "green"], ["green", "green"], ["app", "grey"], ["grey", "grey"], ["gray", "grey"], ["existing", "grey"],
  ["engine", "blue"], ["blue", "blue"], ["toe", "blue"], ["data", "amber"], ["amber", "amber"], ["alert", "red"], ["red", "red"], ["hurts", "red"],
  ["violet", "violet"], ["purple", "violet"], ["dashed", "dashed"], ["proposed", "orange dashed"], ["orange", "orange dashed"], ["orange-dashed", "orange dashed"],
]);

/** lint_packet.TONE_CLASSES as the linter derives it from LEGEND_TONES. */ // until vocab.ts lands (N6)
export const TONE_CLASSES: ReadonlySet<string> = new Set([...LEGEND_TONES.values()].flatMap((v) => py.str.split(v)));

/** build_tdd.Compiler.LINK_KEYS: the content keys whose inline HTML the reference linker rewrites. */ // until vocab.ts lands (N6)
export const LINK_KEYS: ReadonlySet<string> = new Set([
  "html", "caption", "reading", "one", "sub", "bodyHtml", "summary", "status", "change", "rollback", "question", "title", "label", "ms", "note", "deps",
]);

/** The marker line as build_tdd.py patches bd.MARK. */ // until vocab.ts lands (N6)
export const MARK = re.compile(String.raw`^\s*\\?\[(embedded content|diagram|video|media|image|legend|reading)\s*:\s*(.*?)\s*\\?\]\s*$`, re.I);
// ---------------------------------------------------------------- end of the N6 block

/** lint_packet.ATTRS: a trailing {attrs} block. */
export const ATTRS = re.compile(String.raw`\s*\{([^{}]*)\}\s*$`);
export const ISO_DATE = re.compile(String.raw`^\d{4}-\d{2}-\d{2}$`);
export const ANY_DATE = re.compile(String.raw`\b\d{4}-\d{2}-\d{2}\b`);

/** The rules, in report order (lint_packet.RULES). PARITY joins last when N6's Python rule and the in-process compiler land. */
export const RULES = ["STRUCT", "DNUM", "REFS", "CITES", "DIAG", "SCHEMA", "FILES", "META", "AGENTS", "XDOC"] as const;
export type RuleId = (typeof RULES)[number] | "PARITY";

export const FILE_LIMIT = 15 * 1024 * 1024;
export const PAGE_LIMIT = 16 * 1024 * 1024;
export const MEDIA_EXT = [".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".mp4", ".webm", ".mov"] as const;
export const CODE_EXT: ReadonlySet<string> = new Set(["ts", "tsx", "js", "mjs", "cjs", "py", "sql", "cs", "rb", "go", "rs", "sh", "css", "html", "graphql", "proto"]);
