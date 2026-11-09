// Read one built, linted packet into the rows the system database holds (D17, D23, D24). Pure file reading: no
// database here, so `--dry-run` and the tests can plan a load without a connection. The loader runs after lint: a build
// whose build/lint.json is missing, older than build/content.json, or written for another build is refused.
import { existsSync, readdirSync, readFileSync, realpathSync, statSync } from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, join, resolve } from "node:path";
import { gzipSync } from "bun";

type Json = Record<string, unknown>;

export type PacketRow = {
  repo_slug: string; slug: string; title: string; owner: string; status: PacketStatus; status_label: string | null;
  packet_path: string; updated_at: string | null;
};
export type RevisionRow = {
  rev: number; generated: string; rev_note: string; sha: string | null; sections: number; diagrams: number; decisions: number;
  prs: number; schema_tables: number; refs_linked: number; refs_unlinked: number; citations: number; lint_errors: number;
  lint_warnings: number; page_bytes: number | null; bundle_gzip_bytes: number | null; published_at: string | null;
};
export type DecisionRow = {
  token: string; prefix: string; number: number; title: string; status: string; state: "done" | "open" | "held";
  quote: string | null; reading: string | null; decided_by: string | null; decided_on: string | null; source: string | null;
  section_id: string | null; anchor: string;
};
export type ReferenceLinkRow = { token: string; family: string; section_id: string; anchor: string | null; linked: boolean };
export type CitationRow = {
  text: string; path: string; line_from: number | null; line_to: number | null; section_id: string; anchor: string | null;
  resolved_at: Resolved; checked_sha: string | null; checked_on: string | null;
};
export type LintFindingRow = {
  family: string; rule: string; severity: "error" | "warning" | "info"; where_: string | null; line: number | null; message: string;
  linted_at: string;
};
export type EvalRunRow = {
  kind: string; scorer: string; value: number | null; status: "ok" | "not_run" | "error"; method: string; reason: string | null;
  metrics: Json | null; braintrust_experiment: string | null; ran_at: string;
};
export type PublicationRow = {
  surface: Surface; url: string; is_source_of_truth: boolean; visibility: string | null; published_at: string | null;
};

export type PacketStatus = "draft" | "in_review" | "approved" | "as_built" | "superseded";
export type Surface = "artifact" | "notion" | "other";
export type Resolved = "sha" | "head" | "working_tree" | "packet" | "missing" | "unknown";
export const SURFACES: readonly Surface[] = ["artifact", "notion", "other"];
const RESOLVED: readonly Resolved[] = ["sha", "head", "working_tree", "packet", "missing", "unknown"];

export type Plan = {
  ok: true;
  packetDir: string;
  buildDir: string;
  packet: PacketRow;
  revision: RevisionRow;
  decisions: DecisionRow[];
  referenceLinks: ReferenceLinkRow[] | null;   // null: content.json has no refs.links (older compiler), table skipped
  citations: CitationRow[];
  lintFindings: LintFindingRow[];
  evalRuns: EvalRunRow[];
  publications: PublicationRow[];
  notes: string[];
};
export type Refusal = { ok: false; reason: string };

export class Refused extends Error {}
function refuse(reason: string): never { throw new Refused(reason); }

function readJson(path: string, what: string): Json {
  let v: unknown;
  try { v = JSON.parse(readFileSync(path, "utf8")); } catch (e) { return refuse(`${what} is not readable JSON (${(e as Error).message})`); }
  if (!v || typeof v !== "object" || Array.isArray(v)) return refuse(`${what} is not a JSON object`);
  return v as Json;
}
const obj = (v: unknown): Json => (v && typeof v === "object" && !Array.isArray(v) ? (v as Json) : {});
const arr = (v: unknown): unknown[] => (Array.isArray(v) ? v : []);
const str = (v: unknown): string | null => (typeof v === "string" && v !== "" ? v : null);
const int = (v: unknown): number | null => (typeof v === "number" && Number.isInteger(v) ? v : null);
const num = (v: unknown): number | null => (typeof v === "number" && Number.isFinite(v) ? v : null);
const real = (p: string): string | null => { try { return realpathSync(p); } catch { return null; } };

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const ANY_DATE = /\b(\d{4}-\d{2}-\d{2})\b/;
/** A meta.json date ("2026-11-08") or timestamp, as an ISO timestamp in UTC; null when absent or unparseable. */
export function toTimestamp(v: unknown): string | null {
  const s = str(v);
  if (!s) return null;
  const iso = ISO_DATE.test(s) ? `${s}T00:00:00Z` : s;
  return Number.isNaN(Date.parse(iso)) ? null : new Date(iso).toISOString();
}

/** meta.status ("Draft for review", "As built", ...) on the packet table's five states. */
export function packetStatus(label: string | null): PacketStatus {
  const low = (label ?? "draft").trim().toLowerCase();
  if (low.startsWith("as built")) return "as_built";
  if (/^(approved|accepted|reviewed)/.test(low)) return "approved";
  if (/^(superseded|withdrawn)/.test(low)) return "superseded";
  if (/\breview\b/.test(low)) return "in_review";
  return "draft";
}

function homeRel(p: string): string {
  const h = homedir();
  return p === h ? "~" : p.startsWith(h + "/") ? "~" + p.slice(h.length) : p;
}

/** The status line of a decision box: who decided, when, and where it was said, when the line says so. */
export function parseDecisionStatus(status: string): { by: string | null; on: string | null; source: string | null } {
  const parts = status.split(" · ").map((s) => s.trim());
  const i = parts.findIndex((p) => ANY_DATE.test(p));
  if (i < 0) return { by: null, on: null, source: null };
  const seg = parts[i]!;
  const on = seg.match(ANY_DATE)![1]!;
  const by = seg.slice(0, seg.indexOf(on)).replace(/[\s,;:·-]+$/, "").trim() || null;
  const rest = parts.slice(i + 1).join(" · ").trim();
  // A trailing segment names where it was said ("comment on this page", "in chat") or is a note ("direction, not
  // built"); only the first kind is a source.
  const source = rest && /\b(comment|chat|session|notion|meeting|thread|slack|email|call|review)\b/i.test(rest) ? rest : null;
  return { by, on, source };
}

/** A token's family as the compiler writes it in refs.links[] (the prefix: D, PR, OS); used only when an entry has none. */
export function tokenFamily(token: string): string {
  return token.match(/^(PR|[A-Z]+)(?=-?\d)/)?.[1] ?? "glossary";
}

const SEVERITY: Record<string, LintFindingRow["severity"]> = { error: "error", warn: "warning", warning: "warning", info: "info", note: "info" };
const EVAL_STATUS: Record<string, EvalRunRow["status"]> = { ok: "ok", not_run: "not_run", "not run": "not_run", error: "error" };

/** The largest inline classic <script> of the page (the app bundle), gzip -9 bytes; null when there is none. */
function bundleGzipBytes(html: string): number | null {
  let best: string | null = null;
  for (const m of html.matchAll(/<script(\s[^>]*)?>([\s\S]*?)<\/script>/g)) {
    const attrs = m[1] ?? "";
    if (/\bsrc\s*=|application\/json/i.test(attrs)) continue;
    if (best === null || m[2]!.length > best.length) best = m[2]!;
  }
  return best ? gzipSync(Buffer.from(best, "utf8"), { level: 9 }).byteLength : null;
}

export type PlanOptions = { build?: string | undefined };

/** Plan a load of one packet; a Refusal says why the build cannot be loaded. */
export function planLoad(packetDirIn: string, opts: PlanOptions = {}): Plan | Refusal {
  try {
    return buildPlan(packetDirIn, opts);
  } catch (e) {
    if (e instanceof Refused) return { ok: false, reason: e.message };
    throw e;
  }
}

function buildPlan(packetDirIn: string, opts: PlanOptions): Plan {
  const packetDir = resolve(packetDirIn);
  if (!existsSync(packetDir) || !statSync(packetDir).isDirectory()) refuse(`${packetDirIn} is not a packet folder`);
  const buildDir = resolve(opts.build ?? join(packetDir, "build"));
  const notes: string[] = [];

  // ---- inputs and the D24 gate: after lint, never before
  const metaPath = join(packetDir, "meta.json");
  if (!existsSync(metaPath)) refuse(`${homeRel(metaPath)} is missing`);
  const contentPath = join(buildDir, "content.json");
  if (!existsSync(contentPath)) refuse(`${homeRel(contentPath)} is missing: build the packet first`);
  const lintPath = join(buildDir, "lint.json");
  if (!existsSync(lintPath)) {
    refuse(`${homeRel(lintPath)} is missing: the loader runs after lint (D24); run ` +
      `\`lint_packet.py ${homeRel(packetDir)} --json ${homeRel(lintPath)}\` first`);
  }
  const lintM = statSync(lintPath).mtimeMs, contentM = statSync(contentPath).mtimeMs;
  const meta = readJson(metaPath, "meta.json");
  const content = readJson(contentPath, "build/content.json");
  const lint = readJson(lintPath, "build/lint.json");
  const generated = toTimestamp(content["generated"]) ?? refuse("build/content.json has no `generated` stamp (the build key)");
  // Lint belongs to this build: by the content.json stamp lint.json recorded (content_generated) when it has one, else
  // by lint.json's own timestamp against the build's `generated`, else by file mtimes (lint.json written after
  // content.json).
  const lintFor = toTimestamp(lint["content_generated"]);
  const lintAt = toTimestamp(lint["linted_at"] ?? lint["generated"]);
  if (lintFor !== null) {
    if (lintFor !== generated) refuse(`build/lint.json linted build ${String(lint["content_generated"])}, not this build (${String(content["generated"])}): lint this build before loading it (D24)`);
  } else if (lintAt !== null) {
    if (lintAt < generated) refuse(`build/lint.json (linted ${lintAt}) is older than this build (${generated}): lint this build before loading it (D24)`);
  } else if (lintM < contentM) {
    refuse(`${homeRel(lintPath)} is older than content.json (lint ${new Date(lintM).toISOString()}, build ` +
      `${new Date(contentM).toISOString()}): lint this build before loading it (D24)`);
  }
  if (lint["compiled"] === true) refuse("build/lint.json linted a temporary compile (compiled: true), not this build");
  const lintPacket = str(lint["packet"]);
  if (lintPacket && real(lintPacket) !== real(packetDir)) refuse(`build/lint.json was written for another packet (${lintPacket})`);
  const lintBuild = str(lint["build"]);
  if (lintBuild && real(join(lintBuild, "content.json")) !== real(contentPath)) {
    refuse(`build/lint.json was written for another build (${lintBuild})`);
  }

  // ---- packet
  const slug = basename(packetDir);
  const repoSlug = basename(dirname(dirname(packetDir)));
  const people = arr(meta["people"]).map(obj);
  const owner = str(meta["owner"]) ?? str(people.find((p) => String(p["role"] ?? "").toLowerCase() === "author")?.["name"])
    ?? str(people[0]?.["name"]) ?? refuse("meta.json names no owner and no Author in people");
  const statusLabel = str(meta["status"]);
  const packet: PacketRow = {
    repo_slug: repoSlug, slug, title: str(meta["title"]) ?? str(content["titleText"]) ?? slug, owner,
    status: packetStatus(statusLabel), status_label: statusLabel, packet_path: homeRel(packetDir),
    updated_at: toTimestamp(meta["updated"]),
  };

  // ---- revision (one row per build)
  const rev = int(meta["rev"]) ?? refuse("meta.json has no integer rev");
  const cmeta = obj(content["meta"]);
  if (cmeta["rev"] !== undefined && cmeta["rev"] !== null && cmeta["rev"] !== rev) {
    refuse(`build/content.json was compiled at rev ${String(cmeta["rev"])} but meta.json is at rev ${rev}: rebuild first`);
  }
  const stats = obj(content["stats"]);
  const refs = obj(content["refs"]);
  const cites = arr(content["cites"]).map(obj);
  const decisionsIn = arr(content["decisions"]).map(obj);
  const findings = arr(lint["findings"]).map(obj);
  const counts = obj(lint["counts"]);
  const pagePath = join(buildDir, "tdd.html");
  let pageBytes: number | null = null, bundle: number | null = null;
  if (existsSync(pagePath)) {
    // Only a page rendered from this content.json: newer than it, and carrying the same `generated` when it embeds one.
    const html = readFileSync(pagePath, "utf8");
    const embedded = html.match(/"generated"\s*:\s*"([^"]+)"/)?.[1] ?? null;
    const fresh = statSync(pagePath).mtimeMs >= contentM && (embedded === null || toTimestamp(embedded) === generated);
    if (fresh) {
      pageBytes = statSync(pagePath).size;
      bundle = bundleGzipBytes(html);
    } else notes.push(`page_bytes: build/tdd.html is from another build (${embedded ?? "older than content.json"}); left NULL`);
  } else notes.push("page_bytes: build/tdd.html absent");
  // meta.published is the date of the packet's last publish, and it is not moved on every rebuild: it counts as this
  // build's publish only when it is not earlier than the build itself. A date without a time reads as midnight UTC, so
  // a publish dated the build's own day does not count (it could be the publish of an earlier build that day).
  // Otherwise published_at stays NULL until a publish is recorded (`tdd load-db <packet> --published artifact <url>`).
  const metaPublished = toTimestamp(meta["published"]);
  const fits = (t: string | null) => (t !== null && t >= generated ? t : null);
  const publishedAt = fits(metaPublished);
  if (metaPublished && !publishedAt) {
    notes.push(`published_at: meta.json published ${String(meta["published"])} is not after this build (${generated}); left NULL until a publish is recorded`);
  }
  const linkedCount = int(refs["linked"]) ?? arr(refs["links"]).filter((l) => obj(l)["linked"] !== false).length;
  const unlinked = arr(refs["unlinked"]).filter((u): u is string => typeof u === "string");
  const revision: RevisionRow = {
    rev, generated, rev_note: str(meta["rev_note"]) ?? "", sha: str(meta["sha"]),
    sections: int(stats["sections"]) ?? arr(content["sections"]).length,
    diagrams: int(stats["diagrams"]) ?? 0, decisions: int(stats["decisions"]) ?? decisionsIn.length, prs: int(stats["prs"]) ?? 0,
    schema_tables: arr(content["schema"]).length, refs_linked: linkedCount, refs_unlinked: new Set(unlinked).size,
    citations: cites.length,
    lint_errors: int(counts["errors"]) ?? findings.filter((f) => f["severity"] === "error").length,
    lint_warnings: int(counts["warnings"]) ?? findings.filter((f) => SEVERITY[String(f["severity"])] === "warning").length,
    page_bytes: pageBytes, bundle_gzip_bytes: bundle, published_at: publishedAt,
  };

  // ---- decisions (per packet; the owner's words verbatim when the compiler emits them)
  const seen = new Set<string>();
  const decisions: DecisionRow[] = decisionsIn.map((d) => {
    const token = str(d["id"]) ?? refuse("a content.json decision has no id");
    const m = token.match(/^([A-Z]+)-?(\d+)/) ?? refuse(`decision id ${token} has no number`);
    const prefix = m[1]!, number = Number(m[2]);
    const key = `${prefix}${number}`;
    if (seen.has(key)) refuse(`decision ${token} appears twice in build/content.json`);
    seen.add(key);
    const state = String(d["state"] ?? "open");
    if (state !== "done" && state !== "open" && state !== "held") refuse(`decision ${token} has state ${state}; expected done, open or held`);
    const status = typeof d["status"] === "string" ? d["status"] : "";
    const parsed = parseDecisionStatus(status);
    const quote = typeof d["quote"] === "string" && d["quote"] !== "" ? d["quote"] : null;   // verbatim: never trimmed
    // Who, when and where come from the status line's dated segment ("Deferred · Owner, 2026-01-06" -> Owner): the
    // compiler's decidedBy takes the first segment ("Deferred", "Open") and is not used. A tentative status
    // ("Provisional · Owner, 2026-01-06", "Open · ...") has no decider yet; a named, dated status counts even when the
    // compiler's state is open ("Owner, 2026-01-08 · direction, not built").
    const tentative = /^(open|provisional|proposed|waiting)\b/i.test(status.trim());
    const decided = state !== "open" || (parsed.by !== null && parsed.on !== null && !tentative);
    const decidedOn = str(d["decidedOn"]);
    if (decidedOn !== null && !isDate(decidedOn)) refuse(`decision ${token} has decidedOn ${decidedOn}, not a calendar date`);
    return {
      token, prefix, number, title: str(d["question"]) ?? str(d["title"]) ?? token, status, state,
      quote, reading: typeof d["reading"] === "string" && d["reading"] !== "" ? d["reading"] : null,
      decided_by: decided ? parsed.by : null,
      decided_on: decided ? decidedOn ?? parsed.on : null,
      source: decided ? parsed.source : null,
      section_id: str(d["section"]), anchor: str(d["anchor"]) ?? token.toLowerCase(),
    };
  });
  if (decisions.length && !decisionsIn.some((d) => "quote" in d)) {
    notes.push("decision: content.json decisions carry no quote/reading (older compiler); quote and reading left NULL");
  }

  // ---- reference links (content.json refs.links[] from the compiler; skipped when absent)
  let referenceLinks: ReferenceLinkRow[] | null = null;
  if (Array.isArray(refs["links"])) {
    referenceLinks = (refs["links"] as unknown[]).map(obj).map((l) => {
      const token = str(l["token"]) ?? refuse("a refs.links entry has no token");
      const linked = l["linked"] !== false;
      return {
        token, family: str(l["family"]) ?? tokenFamily(token), section_id: str(l["section"]) ?? refuse(`refs.links ${token} has no section`),
        anchor: linked ? str(l["anchor"]) : null, linked,
      };
    });
  } else {
    notes.push(`reference_link: skipped, content.json has no refs.links (older compiler; ${linkedCount} linked, ${unlinked.length} unlinked counted on the revision)`);
  }

  // ---- citations (content.json cites; where they resolved comes from lint.json citations[] when present)
  const lintedAt = lintAt ?? new Date(lintM).toISOString();
  const lintedOn = lintedAt.slice(0, 10);
  const lintCites = Array.isArray(lint["citations"]) ? (lint["citations"] as unknown[]).map(obj) : null;
  const exact = new Map<string, Json>(), byPath = new Map<string, Json>();
  for (const c of lintCites ?? []) {
    const k = `${String(c["path"])}|${String(c["from"] ?? null)}|${String(c["to"] ?? null)}`;
    exact.set(`${k}|${String(c["section"] ?? null)}`, c);
    if (!byPath.has(k)) byPath.set(k, c);
  }
  if (!lintCites) notes.push("citation: lint.json has no citations[] (older linter); resolved_at 'unknown'");
  const citations: CitationRow[] = cites.map((c) => {
    const path = str(c["path"]) ?? str(c["text"]) ?? refuse("a content.json citation has no path");
    const from = int(c["from"]), to = int(c["to"]);
    const k = `${path}|${String(from)}|${String(to)}`;
    const hit = exact.get(`${k}|${String(c["section"] ?? null)}`) ?? byPath.get(k);
    let resolved: Resolved = "unknown";
    if (hit) {
      const r = String(hit["resolved"] ?? "unknown");
      if (!(RESOLVED as readonly string[]).includes(r)) refuse(`lint.json citation ${path} has resolved ${r}; expected ${RESOLVED.join(", ")}`);
      resolved = r as Resolved;
    }
    return {
      text: str(c["text"]) ?? path, path, line_from: from, line_to: to, section_id: str(c["section"]) ?? "", anchor: str(c["anchor"]),
      resolved_at: resolved, checked_sha: hit ? str(hit["checked_sha"]) : null, checked_on: resolved === "unknown" ? null : lintedOn,
    };
  });

  // ---- lint findings
  const lintFindings: LintFindingRow[] = findings.map((f) => {
    const family = str(f["rule"]) ?? refuse("a lint.json finding has no rule");
    const check = str(f["check"]) ?? family;
    const sev = SEVERITY[String(f["severity"])] ?? refuse(`lint.json finding ${check} has severity ${String(f["severity"])}`);
    return {
      family, rule: check.includes("/") ? check : `${family}/${check}`, severity: sev, where_: str(f["file"]), line: int(f["line"]),
      message: typeof f["message"] === "string" ? f["message"] : "", linted_at: lintedAt,
    };
  });

  // ---- eval runs: build/eval/output.*.json, only the outputs that scored this build
  const evalRuns: EvalRunRow[] = [];
  const evalDir = join(buildDir, "eval");
  if (existsSync(evalDir)) {
    const label = `${repoSlug}/${slug}@rev${rev}`;
    const outputs = readdirSync(evalDir).filter((f) => /^output\..+\.json$/.test(f)).sort();
    const push = existsSync(join(evalDir, "push.json")) ? readJson(join(evalDir, "push.json"), "build/eval/push.json") : {};
    const experiments = arr(push["experiments"]).map(obj).map((e) => str(e["name"])).filter((n): n is string => !!n);
    const scoredBuild = lastHistoryBuild(join(evalDir, "history.jsonl"), label);
    const skipped: string[] = [];
    const scorerFile = new Map<string, string>();
    const pushM = existsSync(join(evalDir, "push.json")) ? statSync(join(evalDir, "push.json")).mtimeMs : -1;
    let stalePush = false;
    for (const f of outputs) {
      const out = readJson(join(evalDir, f), `build/eval/${f}`);
      const kind = str(out["kind"]) ?? f.replace(/^output\.|\.json$/g, "");
      const outLabel = str(out["packet"]);
      const scores = arr(out["scores"]).map(obj);
      if (outLabel && outLabel !== label) { skipped.push(`${f} (${outLabel}, ${scores.length} scores)`); continue; }
      if (scoredBuild && toTimestamp(scoredBuild) !== generated) { skipped.push(`${f} (scored build ${scoredBuild}, ${scores.length} scores)`); continue; }
      const ranAt = toTimestamp(out["generated"]) ?? lintedAt;
      // <stem>-<kind>, stem <slug>-rev<rev> by default (eval_packet.py --experiment); never another rev's experiment
      const sameRev = (n: string) => (n.match(/-rev(\d+)-/)?.[1] ?? String(rev)) === String(rev);
      // push.json carries no build stamp: it names this run's experiments only when written after the outputs.
      const pushFresh = pushM >= statSync(join(evalDir, f)).mtimeMs;
      if (!pushFresh && experiments.length) stalePush = true;
      const experiment = !pushFresh ? null : experiments.find((n) => n === `${slug}-rev${rev}-${kind}`)
        ?? experiments.find((n) => n.endsWith(`-${kind}`) && sameRev(n)) ?? null;
      for (const s of scores) {
        const scorer = str(s["name"]) ?? refuse(`a score in build/eval/${f} has no name`);
        const dup = scorerFile.get(`${kind}/${scorer}`);
        if (dup) refuse(`eval scorer ${kind}/${scorer} appears in both build/eval/${dup} and build/eval/${f}`);
        scorerFile.set(`${kind}/${scorer}`, f);
        const status = EVAL_STATUS[String(s["status"]).trim().toLowerCase()]
          ?? refuse(`build/eval/${f} ${scorer} has status ${String(s["status"])}; expected ok, not run or error`);
        evalRuns.push({
          kind, scorer, value: num(s["value"]), status, method: typeof s["method"] === "string" ? s["method"] : "",
          reason: str(s["reason"]), metrics: s["metrics"] && typeof s["metrics"] === "object" ? obj(s["metrics"]) : null,
          braintrust_experiment: experiment, ran_at: ranAt,
        });
      }
    }
    if (stalePush) notes.push("eval_run: build/eval/push.json is older than the outputs; braintrust_experiment left NULL");
    if (skipped.length) notes.push(`eval_run: skipped ${skipped.length} output(s) that did not score this build (${label}, ${content["generated"]}): ${skipped.join("; ")}`);
  } else notes.push("eval_run: build/eval absent");

  // ---- publications from meta.json (artifact = the source of truth; Notion mirror)
  const publications: PublicationRow[] = [];
  const vis = str(meta["visibility"]);
  const visibility = vis && ["private", "org", "public"].includes(vis) ? vis : null;
  const artifact = str(meta["artifact_url"]);
  if (artifact) publications.push({ surface: "artifact", url: artifact, is_source_of_truth: true, visibility, published_at: revision.published_at });
  const notionRaw = meta["notion_url"] ?? meta["notion"];
  const notion = str(notionRaw) ?? str(obj(notionRaw)["url"]);
  if (notion) {
    publications.push({ surface: "notion", url: notion, is_source_of_truth: false, visibility,
      published_at: fits(toTimestamp(meta["notion_published"])) ?? revision.published_at });
  }

  const plan: Plan = { ok: true, packetDir, buildDir, packet, revision, decisions, referenceLinks, citations, lintFindings, evalRuns, publications, notes };
  validate(plan);
  return plan;
}

const INT4 = 2147483647;
const INT_KEYS = new Set(["rev", "sections", "diagrams", "decisions", "prs", "schema_tables", "refs_linked", "refs_unlinked", "citations",
  "lint_errors", "lint_warnings", "page_bytes", "bundle_gzip_bytes", "number", "line_from", "line_to", "line"]);
/** Refuse what Postgres would reject, so a dry run says what the load will do: NUL bytes, integers past int4. */
function validate(plan: Plan): void {
  const tables: [string, readonly object[]][] = [
    ["packet", [plan.packet]], ["revision", [plan.revision]], ["decision", plan.decisions], ["reference_link", plan.referenceLinks ?? []],
    ["citation", plan.citations], ["lint_finding", plan.lintFindings], ["eval_run", plan.evalRuns], ["publication", plan.publications],
  ];
  for (const [table, rows] of tables) {
    for (const row of rows) {
      for (const [k, v] of Object.entries(row)) {
        if (typeof v === "string" && v.includes("\u0000")) refuse(`${table}.${k} contains a NUL byte, which Postgres text cannot hold`);
        if (v && typeof v === "object" && JSON.stringify(v).includes("\\u0000")) refuse(`${table}.${k} contains a NUL byte, which jsonb cannot hold`);
        if (typeof v === "number" && INT_KEYS.has(k) && Math.abs(v) > INT4) refuse(`${table}.${k} is ${v}, past the integer column's range`);
      }
    }
  }
}

function isDate(s: string): boolean {
  return ISO_DATE.test(s) && !Number.isNaN(Date.parse(`${s}T00:00:00Z`)) && new Date(`${s}T00:00:00Z`).toISOString().slice(0, 10) === s;
}

/** The content.json `generated` stamp the last eval run of this label scored (history.jsonl), if recorded. */
function lastHistoryBuild(path: string, label: string): string | null {
  if (!existsSync(path)) return null;
  let found: string | null = null;
  for (const line of readFileSync(path, "utf8").split("\n")) {
    if (!line.trim()) continue;
    try {
      const r = JSON.parse(line) as Json;
      if (r["packet"] === label) found = str(r["content_generated"]);
    } catch { /* a torn line is not a record */ }
  }
  return found;
}
