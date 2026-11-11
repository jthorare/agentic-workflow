// The system-database loader. Inputs are two synthetic fixture packets under test/db/fixtures (an <org>-web-app packet
// on today's producers, and a house packet on the older compiler and linter), copied into a temp tree with the same
// <repo>/system-design/<slug> layout so each test can stamp file times and rewrite inputs freely. Real packets can be
// added with TDD_REAL_PACKETS=<dir>[:<dir>...]; they get the same count checks on a snapshot taken at start.
// Plan-level tests need no database; the rest run against tdd_system_test (or $TDD_TEST_DATABASE_URL) and skip with a
// warning when it is not reachable.
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { SQL } from "bun";
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, utimesSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join } from "node:path";
import { applySchema, writePlan } from "../../src/db/load";
import { packetStatus, parseDecisionStatus, planLoad, type Plan } from "../../src/db/plan";

type J = Record<string, any>;
const TEST_URL = process.env["TDD_TEST_DATABASE_URL"] ?? "postgres://localhost/tdd_system_test";
const CLI = join(import.meta.dir, "..", "..");
const FIX = join(import.meta.dir, "fixtures");
const FIXTURES = {
  current: join(FIX, "org-web-app/system-design/sample-ranking"),
  older: join(FIX, "house/system-design/sample-system"),
};
const REAL = (process.env["TDD_REAL_PACKETS"] ?? "").split(":").filter((p) => p && existsSync(join(p, "build/content.json")));
const TABLES = ["publication", "eval_run", "lint_finding", "citation", "reference_link", "decision", "revision", "packet", "schema_version"];

if (!new URL(TEST_URL).pathname.endsWith("_test")) throw new Error(`refusing to reset ${TEST_URL}: the test database name must end in _test`);
const reachable = await (async () => {
  const db = new SQL(TEST_URL);
  try { await db`SELECT 1`; return true; } catch { return false; } finally { await db.close(); }
})();
if (!reachable) console.warn(`load-db database tests skipped: ${TEST_URL} is not reachable (createdb tdd_system_test)`);

const readJ = (p: string): J => JSON.parse(readFileSync(p, "utf8"));
const writeJ = (p: string, v: unknown) => writeFileSync(p, JSON.stringify(v, null, 1));
const root = mkdtempSync(join(tmpdir(), "tdd-load-db-"));
let seq = 0;

/** Copy a packet's loader inputs into <root>/<n>/<repo>/system-design/<slug>; lint.json points at the copy. */
function snapshot(src: string, slug = basename(src)): string {
  const dst = join(root, String(++seq), basename(dirname(dirname(src))), "system-design", slug);
  mkdirSync(join(dst, "build", "eval"), { recursive: true });
  copyFileSync(join(src, "meta.json"), join(dst, "meta.json"));
  copyFileSync(join(src, "build/content.json"), join(dst, "build/content.json"));
  if (existsSync(join(src, "build/tdd.html"))) copyFileSync(join(src, "build/tdd.html"), join(dst, "build/tdd.html"));
  const lintSrc = existsSync(join(src, "build/lint.json")) ? join(src, "build/lint.json") : join(src, "build/eval/lint.json");
  writeJ(join(dst, "build/lint.json"), { ...readJ(lintSrc), packet: dst, build: join(dst, "build") });
  const ev = join(src, "build/eval");
  if (existsSync(ev)) {
    for (const f of readdirSync(ev)) if (/^output\..+\.json$|^push\.json$|^history\.jsonl$/.test(f)) copyFileSync(join(ev, f), join(dst, "build/eval", f));
  }
  stamp(dst);
  return dst;
}
/** File times in pipeline order: content.json, the page, lint.json, eval outputs, push.json (or lint first, to go stale). */
function stamp(dst: string, lintFirst = false) {
  const t = Date.now() / 1000 - 600;
  const at = (p: string, s: number) => { if (existsSync(p)) utimesSync(p, s, s); };
  at(join(dst, "build/content.json"), t + 1);
  at(join(dst, "build/tdd.html"), t + 2);
  at(join(dst, "build/lint.json"), lintFirst ? t : t + 3);
  for (const f of readdirSync(join(dst, "build/eval"))) at(join(dst, "build/eval", f), f === "push.json" ? t + 5 : t + 4);
}
/** A new build of the same rev: content.json (and the page's embedded stamp) moved `hours` later, lint fresh. */
function rebuild(src: string, hours: number): { dst: string; generated: string } {
  const dst = snapshot(src);
  const c = readJ(join(dst, "build/content.json"));
  const generated = new Date(Date.parse(c.generated) + hours * 3600_000).toISOString().replace(".000Z", "Z");
  writeJ(join(dst, "build/content.json"), { ...c, generated });
  const page = join(dst, "build/tdd.html");
  if (existsSync(page)) writeFileSync(page, readFileSync(page, "utf8").replace(c.generated, generated));
  const hist = join(dst, "build/eval/history.jsonl");
  if (existsSync(hist)) writeFileSync(hist, readFileSync(hist, "utf8").replace(c.generated, generated));
  stamp(dst);
  return { dst, generated };
}
function mustPlan(dst: string): Plan {
  const p = planLoad(dst);
  if (!p.ok) throw new Error(`refused: ${p.reason}`);
  return p;
}
function refusal(dst: string): string {
  const p = planLoad(dst);
  if (p.ok) throw new Error("expected a refusal");
  return p.reason;
}
function cli(...args: string[]) {
  const r = Bun.spawnSync(["bun", "bin/tdd.ts", "load-db", ...args], { cwd: CLI, env: { ...process.env, DATABASE_URL: TEST_URL } });
  return { code: r.exitCode, out: r.stdout.toString(), err: r.stderr.toString() };
}

const base = { current: snapshot(FIXTURES.current), older: snapshot(FIXTURES.older) };
afterAll(() => rmSync(root, { recursive: true, force: true }));

describe("plan (no database)", () => {
  test("decision status lines, packet status labels", () => {
    expect(parseDecisionStatus("Deferred · Owner, 2026-01-06")).toEqual({ by: "Owner", on: "2026-01-06", source: null });
    expect(parseDecisionStatus("Owner, 2026-01-05 · in chat")).toEqual({ by: "Owner", on: "2026-01-05", source: "in chat" });
    expect(parseDecisionStatus("Owner, 2026-01-07 · direction, not built")).toEqual({ by: "Owner", on: "2026-01-07", source: null });
    expect(parseDecisionStatus("Open · needs the vendor")).toEqual({ by: null, on: null, source: null });
    expect(packetStatus("Draft for review")).toBe("in_review");
    expect(packetStatus("As built")).toBe("as_built");
    expect(packetStatus(null)).toBe("draft");
  });

  test("today's producers: decisions, links, citations, page, publish date, eval", () => {
    const p = mustPlan(base.current);
    const content = readJ(join(base.current, "build/content.json"));
    const d = Object.fromEntries(p.decisions.map((x) => [x.token, x]));
    expect(d["D1"]!.quote).toBe("\"We can  keep it local first.\"");          // verbatim, double space kept
    expect(d["D1"]!.reading).toBe("Read as: a local database first, hosted later.");
    expect([d["D2"]!.decided_by, d["D2"]!.decided_on]).toEqual(["Owner", "2026-01-06"]);   // not the compiler's "Deferred"
    expect([d["D3"]!.decided_by, d["D3"]!.decided_on]).toEqual([null, null]);
    expect([d["D4"]!.decided_by, d["D4"]!.decided_on]).toEqual(["Owner", "2026-01-07"]);   // named and dated, compiler state open
    expect([d["R2"]!.decided_by, d["R2"]!.decided_on]).toEqual([null, null]);              // provisional
    expect([d["D2"]!.prefix, d["R2"]!.prefix, d["D2"]!.number, d["R2"]!.number]).toEqual(["D", "R", 2, 2]);
    expect(p.referenceLinks!.length).toBe(content.refs.links.length);
    expect(p.referenceLinks!.filter((l) => !l.linked).map((l) => [l.token, l.anchor])).toEqual([["R4", null]]);
    expect(p.citations.map((c) => c.resolved_at)).toEqual(["sha", "sha", "packet", "missing"]);
    expect(p.citations[0]!.checked_sha).toBe("0123456789");
    expect(p.revision.page_bytes).toBe(statSync(join(base.current, "build/tdd.html")).size);
    expect(p.revision.bundle_gzip_bytes).toBeGreaterThan(0);
    expect(p.revision.published_at).toBe("2026-01-10T00:00:00.000Z");                      // after the build
    expect(p.publications.map((x) => [x.surface, x.is_source_of_truth])).toEqual([["artifact", true], ["notion", false]]);
    expect(p.evalRuns.length).toBe(5);
    expect(p.evalRuns.map((e) => e.braintrust_experiment)).toEqual([...Array(3).fill("sample-ranking-rev4-artifact"), ...Array(2).fill("sample-ranking-rev4-tokens")]);
    expect(p.evalRuns.find((e) => e.scorer === "artifact_check")).toMatchObject({ status: "not_run", reason: "no browser here", value: null });
  });

  test("the older compiler and linter: tables skipped or left unknown, stale page and eval set aside", () => {
    const p = mustPlan(base.older);
    expect(p.referenceLinks).toBeNull();
    expect(p.notes.join(" ")).toContain("reference_link: skipped");
    expect(p.decisions.every((x) => x.quote === null)).toBe(true);
    expect(p.citations.every((x) => x.resolved_at === "unknown" && x.checked_on === null)).toBe(true);
    expect(p.revision.page_bytes).toBeNull();                                                 // the page embeds an older build
    expect(p.notes.join(" ")).toContain("page_bytes: build/tdd.html is from another build");
    expect(p.revision.published_at).toBeNull();                                               // a same-day date is not after the build
    expect(p.evalRuns.length).toBe(0);
    expect(p.notes.join(" ")).toContain("house/sample-system@rev1");
    expect(p.packet.status).toBe("as_built");
  });

  test("a stale push.json names no experiment", () => {
    const dst = snapshot(FIXTURES.current);
    const t = Date.now() / 1000 - 900;
    utimesSync(join(dst, "build/eval/push.json"), t, t);
    const p = mustPlan(dst);
    expect(p.evalRuns.every((e) => e.braintrust_experiment === null)).toBe(true);
  });

  test("refuses without a lint.json of this build (D24)", () => {
    const noLint = snapshot(FIXTURES.current);
    rmSync(join(noLint, "build/lint.json"));
    expect(refusal(noLint)).toContain("lint.json is missing");

    const stale = snapshot(FIXTURES.current);
    stamp(stale, true);
    expect(refusal(stale)).toContain("older than content.json");

    const other = snapshot(FIXTURES.current);
    writeJ(join(other, "build/lint.json"), { ...readJ(join(other, "build/lint.json")), build: join(base.older, "build") });
    stamp(other);
    expect(refusal(other)).toContain("another build");

    const stampFor = snapshot(FIXTURES.current);                // lint.json says which build it linted
    writeJ(join(stampFor, "build/lint.json"), { ...readJ(join(stampFor, "build/lint.json")), content_generated: "2026-01-01T00:00:00Z" });
    stamp(stampFor);
    expect(refusal(stampFor)).toContain("linted build 2026-01-01T00:00:00Z");
    writeJ(join(stampFor, "build/lint.json"), { ...readJ(join(stampFor, "build/lint.json")), content_generated: "2026-01-08T10:00:00Z" });
    stamp(stampFor, true);                                     // the recorded stamp wins over file times
    expect(planLoad(stampFor).ok).toBe(true);

    const lintedAt = snapshot(FIXTURES.current);                // lint.json's own time against the build's stamp
    writeJ(join(lintedAt, "build/lint.json"), { ...readJ(join(lintedAt, "build/lint.json")), linted_at: "2026-01-08T09:00:00Z" });
    stamp(lintedAt);
    expect(refusal(lintedAt)).toContain("older than this build");
  });

  test("refuses values Postgres would reject, so a dry run tells the truth", () => {
    const bad = (edit: (dst: string) => void) => { const dst = snapshot(FIXTURES.current); edit(dst); stamp(dst); return refusal(dst); };
    const content = (dst: string, f: (c: J) => J) => writeJ(join(dst, "build/content.json"), f(readJ(join(dst, "build/content.json"))));
    const lint = (dst: string, f: (c: J) => J) => writeJ(join(dst, "build/lint.json"), f(readJ(join(dst, "build/lint.json"))));
    expect(bad((d) => content(d, (c) => ({ ...c, decisions: c.decisions.map((x: J) => x.id === "D1" ? { ...x, decidedOn: "2026-02-30" } : x) }))))
      .toContain("not a calendar date");
    expect(bad((d) => lint(d, (l) => ({ ...l, findings: [{ ...l.findings[0], message: "a\u0000b" }] })))).toContain("NUL byte");
    expect(bad((d) => lint(d, (l) => ({ ...l, findings: [{ ...l.findings[0], line: 3_000_000_000 }] })))).toContain("past the integer");
    expect(bad((d) => copyFileSync(join(d, "build/eval/output.artifact.json"), join(d, "build/eval/output.artifact-old.json"))))
      .toContain("appears in both");
  });
});

describe.skipIf(!reachable)("database", () => {
  let db: SQL;
  const counts = async (): Promise<Record<string, number>> => {
    const out: Record<string, number> = {};
    for (const t of TABLES) out[t] = Number((await db`SELECT count(*)::int AS n FROM ${db(t)}`)[0].n);
    return out;
  };
  const byRev = async (table: string, revisionId: string) =>
    Number((await db`SELECT count(*)::int AS n FROM ${db(table)} WHERE revision_id = ${revisionId}`)[0].n);
  const revisionOf = async (slug: string, generated: string) =>
    (await db`SELECT r.* FROM revision r JOIN packet p ON p.id = r.packet_id WHERE p.slug = ${slug} AND r.generated = ${generated}::timestamptz`)[0];

  beforeAll(async () => {
    db = new SQL(TEST_URL);
    await db.unsafe(`SET client_min_messages = warning; ${TABLES.map((t) => `DROP TABLE IF EXISTS ${t} CASCADE;`).join(" ")}`);
    await applySchema(db);
  });
  afterAll(async () => { await db?.close(); });

  test("schema applies again idempotently and keeps one schema_version row", async () => {
    expect(await applySchema(db, { force: true })).toBe(1);
    expect(await applySchema(db)).toBe(1);
    expect((await db`SELECT version FROM schema_version`).length).toBe(1);
    const cols = await db`SELECT is_nullable FROM information_schema.columns WHERE table_name = 'revision' AND column_name = 'published_at'`;
    expect(cols[0].is_nullable).toBe("YES");
  });

  const sources: [string, string][] = [["current fixture", base.current], ["older fixture", base.older], ...REAL.map((p) => [`real ${basename(p)}`, snapshot(p)] as [string, string])];
  for (const [name, dst] of sources) {
    test(`${name}: rows equal the inputs, and a second load writes no duplicates`, async () => {
      const content = readJ(join(dst, "build/content.json")), lint = readJ(join(dst, "build/lint.json")), meta = readJ(join(dst, "meta.json"));
      const plan = mustPlan(dst);
      const first = await writePlan(db, plan);
      const rid = first.revisionId;
      const [pk] = await db`SELECT p.id, p.repo_slug, p.slug FROM packet p JOIN revision r ON r.packet_id = p.id WHERE r.id = ${rid}`;
      expect(`${pk.repo_slug}/${pk.slug}`).toBe(`${basename(dirname(dirname(dst)))}/${basename(dst)}`);
      expect(Number((await db`SELECT count(*)::int AS n FROM decision WHERE packet_id = ${pk.id}`)[0].n)).toBe(content.decisions.length);
      if (Array.isArray(content.refs.links)) {
        expect(await byRev("reference_link", rid)).toBe(content.refs.links.length);
        const unl = await db`SELECT DISTINCT token FROM reference_link WHERE revision_id = ${rid} AND NOT linked ORDER BY token`;
        expect(unl.map((r: J) => r.token)).toEqual([...content.refs.unlinked].sort());
      } else expect(first.reference_link).toBeNull();
      expect(await byRev("citation", rid)).toBe(content.cites.length);
      expect(await byRev("lint_finding", rid)).toBe(lint.findings.length);
      expect(await byRev("eval_run", rid)).toBe(plan.evalRuns.length);
      expect(await byRev("publication", rid)).toBe((meta.artifact_url ? 1 : 0) + (meta.notion_url ? 1 : 0));
      const [rv] = await db`SELECT * FROM revision WHERE id = ${rid}`;
      expect([rv.rev, rv.decisions, rv.prs, rv.sections, rv.diagrams, rv.schema_tables, rv.citations, rv.lint_errors, rv.lint_warnings])
        .toEqual([meta.rev, content.stats.decisions, content.stats.prs, content.stats.sections, content.stats.diagrams, content.schema.length,
          content.cites.length, lint.counts.errors, lint.counts.warnings]);
      expect(new Date(rv.generated).toISOString()).toBe(new Date(content.generated).toISOString());

      const before = await counts();
      const second = await writePlan(db, mustPlan(dst));
      expect(second.revisionId).toBe(rid);
      expect(await counts()).toEqual(before);
      expect(Number((await db`SELECT count(*)::int AS n FROM revision WHERE packet_id = ${pk.id}`)[0].n)).toBe(1);
    });
  }

  test("eval_run equals the scores across output.*.json for the build they scored, and a reload replaces them", async () => {
    const res = await writePlan(db, mustPlan(base.current));
    const expected = readdirSync(join(base.current, "build/eval")).filter((f) => f.startsWith("output."))
      .reduce((n, f) => n + readJ(join(base.current, "build/eval", f)).scores.length, 0);
    expect(await byRev("eval_run", res.revisionId)).toBe(expected);
    const metrics = await db`SELECT metrics FROM eval_run WHERE revision_id = ${res.revisionId} AND scorer = 'session_tokens'`;
    expect(metrics[0].metrics).toEqual({ output_tokens: 1200, usd_api_equivalent: 0.5 });
    await writePlan(db, mustPlan(base.current));
    expect(await byRev("eval_run", res.revisionId)).toBe(expected);
  });

  test("decisions: the quote verbatim; a later build owns them in place, an older reload does not take them back", async () => {
    await writePlan(db, mustPlan(base.current));
    const [d1] = await db`SELECT d.* FROM decision d JOIN packet p ON p.id = d.packet_id WHERE p.slug = 'sample-ranking' AND d.token = 'D1'`;
    expect(d1.quote).toBe("\"We can  keep it local first.\"");
    const later = rebuild(FIXTURES.current, 1);
    const res = await writePlan(db, mustPlan(later.dst));
    expect(res.newest).toBe(true);
    const [pk] = await db`SELECT packet_id FROM revision WHERE id = ${res.revisionId}`;
    expect(Number((await db`SELECT count(*)::int AS n FROM revision WHERE packet_id = ${pk.packet_id}`)[0].n)).toBe(2);
    expect(Number((await db`SELECT count(*)::int AS n FROM decision WHERE packet_id = ${pk.packet_id}`)[0].n)).toBe(5);
    const owner = async () => (await db`SELECT DISTINCT revision_id FROM decision WHERE packet_id = ${pk.packet_id}`).map((r: J) => r.revision_id);
    expect(await owner()).toEqual([res.revisionId]);
    expect((await writePlan(db, mustPlan(base.current))).newest).toBe(false);
    expect(await owner()).toEqual([res.revisionId]);
  });

  test("--published marks the build on disk, loading it first; a mirror does not set published_at", async () => {
    await writePlan(db, mustPlan(base.older));
    const loaded = await revisionOf("sample-system", "2026-01-08T12:00:00Z");
    const next = rebuild(FIXTURES.older, 2);                    // built and linted, never loaded
    expect(await revisionOf("sample-system", next.generated)).toBeUndefined();

    const notion = cli(next.dst, "--published", "notion", "https://example.com/notion/sample-system", "--json");
    expect(notion.code).toBe(0);
    const n = JSON.parse(notion.out);
    expect([n.status, new Date(n.generated).toISOString()]).toEqual(["published", new Date(next.generated).toISOString()]);
    const fresh = await revisionOf("sample-system", next.generated);
    expect(fresh.id).toBe(n.revision_id);
    expect(fresh.published_at).toBeNull();
    expect((await db`SELECT url FROM publication WHERE revision_id = ${fresh.id} AND surface = 'notion'`)[0].url).toBe("https://example.com/notion/sample-system");

    const art = cli(next.dst, "--published", "artifact", "https://example.com/artifact/sample-system");
    expect(art.code).toBe(0);
    expect(art.out).toContain("artifact publish recorded on this build");
    expect((await revisionOf("sample-system", next.generated)).published_at).not.toBeNull();
    expect((await revisionOf("sample-system", "2026-01-08T12:00:00Z")).published_at).toEqual(loaded.published_at);  // untouched

    await writePlan(db, mustPlan(next.dst));                     // a reload keeps the recorded publish
    expect((await revisionOf("sample-system", next.generated)).published_at).not.toBeNull();
    expect(await byRev("publication", fresh.id)).toBe(2);

    const stale = rebuild(FIXTURES.older, 3);
    stamp(stale.dst, true);
    const refused = cli(stale.dst, "--published", "artifact", "https://example.com/artifact/sample-system");
    expect(refused.code).toBe(1);
    expect(await revisionOf("sample-system", stale.generated)).toBeUndefined();
  });

  test("a failing load rolls back whole and leaves the previous load as it was", async () => {
    const plan = mustPlan(base.current);
    await writePlan(db, plan);
    const before = await counts();
    const [rv] = await db`SELECT r.loaded_at FROM revision r JOIN packet p ON p.id = r.packet_id WHERE p.slug = 'sample-ranking' AND r.generated = '2026-01-08T10:00:00Z'`;
    const broken: Plan = { ...plan, lintFindings: [{ ...plan.lintFindings[0]!, severity: "bogus" as "error" }] };
    await expect(writePlan(db, broken)).rejects.toThrow();
    expect(await counts()).toEqual(before);
    const [rv2] = await db`SELECT r.loaded_at FROM revision r JOIN packet p ON p.id = r.packet_id WHERE p.slug = 'sample-ranking' AND r.generated = '2026-01-08T10:00:00Z'`;
    expect(rv2.loaded_at).toEqual(rv.loaded_at);
  });

  test("the CLI refuses a build without lint.json and writes nothing", async () => {
    const before = await counts();
    const noLint = snapshot(FIXTURES.current);
    rmSync(join(noLint, "build/lint.json"));
    const r = cli(noLint);
    expect(r.code).toBe(1);
    expect(r.err).toContain("refused");
    expect(r.err).toContain("D24");
    expect(cli(noLint, "--json").code).toBe(1);
    expect(await counts()).toEqual(before);
  });
});

test("usage errors exit 2; a dry run never connects", () => {
  expect(cli().code).toBe(2);
  expect(cli(base.current, "--bogus").code).toBe(2);
  expect(cli(base.current, "--published", "wiki", "https://example.com/x").code).toBe(2);
  expect(cli(base.current, base.older).code).toBe(2);
  const dry = cli(base.current, "--dry-run", "--database-url", "postgres://localhost:1/unreachable");
  expect(dry.code).toBe(0);
  expect(dry.out).toContain("dry run, nothing written");
  expect(dry.out).toContain("lint 1 error(s), 2 warning(s)");
});
