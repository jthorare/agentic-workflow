// The tdd vocabulary, one copy (research/bun-port-2026.md section 1.3, PR 11): the reference-token pattern and its
// anchor id, the path:line citation pattern, the legend tones, the keys the reference linker walks, the tags it never
// links inside, the tdd marker line and the SQL keywords the code renderer marks. Compiler, linter and eval import
// these from here, so the drift between build_tdd.py, lint_packet.py and eval/ev_common.py cannot recur.
// Each pattern is the Python source verbatim, compiled by the kernel's re (Python semantics: Unicode \w \d \s \b,
// $ before a final newline), never by a JS RegExp.
import * as py from "./py";

const re = py.re;

/** build_tdd.TOKEN: D7, E3, F3.2, R2, PR 7, PR 12a, OS-01 ... (group 1 is the token). */
export const TOKEN_SOURCE = String.raw`(?<![\w#/.\-])((?:D|E|F|R|A|M|Q|G|OS|LN|SS|NFR|ADR)-?\d+(?:\.\d+)?|PR\s?\d+[a-z]?)(?![\w\-]|\.\d)`;
export const TOKEN = re.compile(TOKEN_SOURCE);

/** build_tdd.token_id: "PR 7" -> "pr-7", "F3.2" -> "f3-2", "D-7" -> "d-7". */
export function token_id(tok: string): string {
  let t = py.replace(py.lower(py.str.strip(tok)), " ", "-");
  t = re.sub(String.raw`^pr(\d)`, String.raw`pr-\1`, t);
  return py.replace(t, ".", "-");
}

/** build_tdd.CITE: `path/to/file.ts:12-40` (group 1 path, 2 from, 3 to). */
export const CITE_SOURCE = String.raw`^((?:[A-Z]{2,5}/)?[\w@./-]+\.(?:ts|tsx|js|mjs|cjs|py|sql|json|md|cs|yaml|yml|toml|rb|go|rs|sh|css|html|txt|graphql|proto))(?::(\d+)(?:-(\d+))?)?$`;
export const CITE = re.compile(CITE_SOURCE);

/** build_tdd.LEGEND_TONES: a legend label's tone word -> the swatch classes. */
export const LEGEND_TONES: ReadonlyMap<string, string> = new Map([
  ["new", "green"], ["changed", "green"], ["green", "green"], ["app", "grey"], ["grey", "grey"], ["gray", "grey"], ["existing", "grey"],
  ["engine", "blue"], ["blue", "blue"], ["toe", "blue"], ["data", "amber"], ["amber", "amber"], ["alert", "red"], ["red", "red"], ["hurts", "red"],
  ["violet", "violet"], ["purple", "violet"], ["dashed", "dashed"], ["proposed", "orange dashed"], ["orange", "orange dashed"], ["orange-dashed", "orange dashed"],
]);

/** The tone classes a legend swatch may carry (lint_packet.TONE_CLASSES, derived as the linter derives it). */
export const TONE_CLASSES: ReadonlySet<string> = new Set([...LEGEND_TONES.values()].flatMap((v) => py.str.split(v)));

/** build_tdd.Compiler.LINK_KEYS: the content keys whose inline HTML the reference linker rewrites. */
export const LINK_KEYS: ReadonlySet<string> = new Set([
  "html", "caption", "reading", "one", "sub", "bodyHtml", "summary", "status", "change", "rollback", "question", "title", "label", "ms", "note", "deps",
]);

/** build_tdd.SKIP_TAGS: no reference is linked inside these elements. */
export const SKIP_TAGS: ReadonlySet<string> = new Set([
  "a", "code", "pre", "h1", "h2", "h3", "h4", "h5", "h6", "summary", "script", "template", "style", "button", "dt", "title",
]);

/** The marker line as build_tdd.py patches bd.MARK: system-design's kinds plus legend and reading. */
export const MARK_SOURCE = String.raw`^\s*\\?\[(embedded content|diagram|video|media|image|legend|reading)\s*:\s*(.*?)\s*\\?\]\s*$`;
export const MARK = re.compile(MARK_SOURCE, re.I);

/** build_tdd.SQL_KW: the keywords node_code marks in sql / psql / postgres fences. */
export const SQL_KW = String.raw`\b(CREATE TABLE|CREATE( UNIQUE)? INDEX|ALTER TABLE|PRIMARY KEY|FOREIGN KEY|REFERENCES|NOT NULL|NULL|CHECK|CONSTRAINT|UNIQUE|DEFAULT|ON DELETE|ON CONFLICT|CASCADE|SET NULL|NO ACTION|GENERATED ALWAYS AS|STORED|DO UPDATE SET|WHERE|SELECT|FROM|INSERT INTO|VALUES|UPDATE|DELETE|AND|OR|IN|IS|DISTINCT FROM|TRIGGER|BEFORE|AFTER|FOR EACH ROW|EXECUTE|FUNCTION|RETURNS|BEGIN|END|IF|THEN|RAISE|RETURN|ADD COLUMN|DROP|TEXT|UUID|INTEGER|SMALLINT|NUMERIC|BOOLEAN|JSONB|DATE|TIMESTAMPTZ)\b`;
