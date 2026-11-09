#!/usr/bin/env bun
// tdd load-db: load a built, linted packet into the system database (D17, D23, D24). The loader runs after lint and
// never before: a build without a current build/lint.json is refused. The compiler never writes to the database.
import { applySchema, connect, writePlan, type Counts } from "../src/db/load";
import { planLoad, SURFACES, type Plan, type Surface } from "../src/db/plan";

const USAGE = `usage: tdd load-db <packet-dir> [--build <dir>] [--database-url <url>] [--dry-run] [--json]
       tdd load-db <packet-dir> --published <artifact|notion|other> <url> [--build <dir>] [--database-url <url>] [--dry-run] [--json]

  Loads <packet>/build (or --build <dir>) into the system database: the packet, one revision row per build
  (packet, rev, content.json generated), and that build's decisions, reference links, citations, lint findings,
  eval runs and publications, replaced in one transaction so a reload never duplicates. Refuses (exit 1) when
  build/lint.json is missing, older than build/content.json, or written for another build (D24: after lint).
  --published records that the build on disk (its content.json generated stamp) was just published on that surface:
  the build is loaded (or reloaded) and marked in the same transaction; only an artifact publish sets
  revision.published_at.
  --database-url defaults to $DATABASE_URL, then postgres://localhost/tdd_system. --dry-run reads and plans only.
  Exit 0 loaded, 1 refused or failed, 2 usage.`;

type Args = {
  packet: string; build?: string; databaseUrl?: string; dryRun: boolean; json: boolean;
  published?: { surface: Surface; url: string };
};

function parse(argv: string[]): Args | string {
  const pos: string[] = [];
  const a: Partial<Args> & { dryRun: boolean; json: boolean } = { dryRun: false, json: false };
  for (let i = 0; i < argv.length; i++) {
    const t = argv[i]!;
    const value = (flag: string): string | undefined => {
      const v = argv[++i];
      return v === undefined || v.startsWith("--") ? undefined : v;
    };
    if (t === "--build") { const v = value(t); if (!v) return "--build needs a directory"; a.build = v; }
    else if (t === "--database-url") { const v = value(t); if (!v) return "--database-url needs a url"; a.databaseUrl = v; }
    else if (t === "--dry-run") a.dryRun = true;
    else if (t === "--json") a.json = true;
    else if (t === "--published") {
      const surface = value(t), url = value(t);
      if (!surface || !url) return "--published needs a surface and a url";
      if (!(SURFACES as readonly string[]).includes(surface)) return `--published surface must be one of ${SURFACES.join(", ")}`;
      if (!/^https?:\/\/\S+$/.test(url)) return "--published url must be an http(s) url";
      a.published = { surface: surface as Surface, url };
    }
    else if (t.startsWith("-")) return `unknown option ${t}`;
    else pos.push(t);
  }
  if (pos.length !== 1) return pos.length ? `one packet folder, not ${pos.length}` : "a packet folder is required";
  return { ...a, packet: pos[0]! } as Args;
}

/** Where the rows go, without credentials. */
function dbLabel(url: string | undefined): string {
  const u = url || process.env["DATABASE_URL"] || "postgres://localhost/tdd_system";
  try { const p = new URL(u); return `${p.hostname}${p.port ? ":" + p.port : ""}${p.pathname}`; } catch { return "(database url)"; }
}

const TABLES = ["packet", "revision", "decision", "reference_link", "citation", "lint_finding", "eval_run", "publication"] as const;
function countsOf(plan: Plan): Counts {
  return {
    packet: 1, revision: 1, decision: plan.decisions.length, reference_link: plan.referenceLinks ? plan.referenceLinks.length : null,
    citation: plan.citations.length, lint_finding: plan.lintFindings.length, eval_run: plan.evalRuns.length,
    publication: plan.publications.length,
  };
}
const line = (c: Counts) => TABLES.map((t) => `${t} ${c[t] === null ? "skipped" : c[t]}`).join(", ");
const lintLine = (p: Plan) => `lint ${p.revision.lint_errors} error(s), ${p.revision.lint_warnings} warning(s)`;

export async function main(argv: string[]): Promise<number> {
  if (argv.includes("--help") || argv.includes("-h")) { console.log(USAGE); return 0; }
  const args = parse(argv);
  if (typeof args === "string") { console.error(`tdd load-db: ${args}\n${USAGE}`); return 2; }
  const out = (o: Record<string, unknown>, text: string, err = false) => {
    if (args.json) console.log(JSON.stringify(o));
    else (err ? console.error : console.log)(text);
  };
  const refused = (reason: string) => { out({ status: "refused", reason }, `tdd load-db: refused: ${reason}`, true); return 1; };

  const plan = planLoad(args.packet, { build: args.build });
  if (!plan.ok) return refused(plan.reason);
  const name = `${plan.packet.repo_slug}/${plan.packet.slug} rev ${plan.revision.rev} (build ${plan.revision.generated.replace(".000Z", "Z")})`;
  const base = { packet: `${plan.packet.repo_slug}/${plan.packet.slug}`, rev: plan.revision.rev, generated: plan.revision.generated, notes: plan.notes };
  if (args.dryRun) {
    const c = countsOf(plan);
    if (args.published) c.publication = new Set([...plan.publications.map((x) => x.surface), args.published.surface]).size;
    const pub = args.published ? `; would record ${args.published.surface} published now` : "";
    out({ status: "dry-run", ...base, rows: c, published: args.published ?? null },
      `tdd load-db ${name}: dry run, nothing written; would write ${line(c)} (${lintLine(plan)})${pub}`);
    if (!args.json) for (const n of plan.notes) console.error(`note: ${n}`);
    return 0;
  }
  const db = connect(args.databaseUrl);
  try {
    await applySchema(db);
    const res = await writePlan(db, plan, { publish: args.published });
    const { revisionId, newest, ...c } = res;
    const pub = args.published
      ? `; ${args.published.surface} publish recorded on this build${args.published.surface === "artifact" ? ", revision.published_at set" : ""}`
      : "";
    out({ status: args.published ? "published" : "loaded", ...base, database: dbLabel(args.databaseUrl), revision_id: revisionId,
      newest_build: newest, rows: c, lint: { errors: plan.revision.lint_errors, warnings: plan.revision.lint_warnings },
      published: args.published ?? null },
      `tdd load-db ${name} -> ${dbLabel(args.databaseUrl)}: ${line(c)} (${lintLine(plan)})${pub}`);
    if (!args.json) for (const n of plan.notes) console.error(`note: ${n}`);
    return 0;
  } catch (e) {
    out({ status: "failed", ...base, error: (e as Error).message }, `tdd load-db: failed: ${(e as Error).message}`, true);
    return 1;
  } finally {
    await db.close();
  }
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
