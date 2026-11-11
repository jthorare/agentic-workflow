// Write a planned load into the system database on Bun's built-in Postgres client (bun 1.4.4: `SQL` from "bun").
// One transaction per load: upsert the packet, upsert the build's revision row, replace that build's child rows, and
// update the packet's decisions in place, so loading the same build twice leaves the same rows.
import { SQL } from "bun";
import type { Plan, Surface } from "./plan";

export const SCHEMA_VERSION = 1;
export const DEFAULT_DATABASE_URL = "postgres://localhost/tdd_system";
const SCHEMA_FILE = new URL("../../sql/schema.sql", import.meta.url);

export type Counts = {
  packet: number; revision: number; decision: number; reference_link: number | null; citation: number; lint_finding: number;
  eval_run: number; publication: number;
};

export function connect(url?: string | undefined): SQL {
  return new SQL(url || process.env["DATABASE_URL"] || DEFAULT_DATABASE_URL);
}

const SCHEMA_LOCK = 0x7464_6431; // pg_advisory lock key for applying the schema ("tdd1")

/**
 * Apply sql/schema.sql once and check the database is at the version this loader writes. The DDL runs only when the
 * database has no schema_version yet (or with force), inside one transaction behind an advisory lock: CREATE INDEX on a
 * table another loader is writing takes a ShareLock that deadlocks against that loader's row locks.
 */
export async function applySchema(db: SQL, opts: { force?: boolean } = {}): Promise<number> {
  const [have] = await db`SELECT to_regclass('public.schema_version') IS NOT NULL AS present`;
  if (opts.force || !have.present) {
    const ddl = await Bun.file(SCHEMA_FILE).text();
    await db.begin(async (tx) => {
      await tx`SELECT pg_advisory_xact_lock(${SCHEMA_LOCK})`;
      await tx.unsafe(ddl);
    });
  }
  const rows: { version: number }[] = await db`SELECT version FROM schema_version`;
  const version = rows[0]?.version;
  if (version !== SCHEMA_VERSION) throw new Error(`database schema_version is ${String(version)}; this loader writes version ${SCHEMA_VERSION}`);
  return version;
}

const CHUNK = 500;
async function insertAll(tx: SQL, table: string, rows: Record<string, unknown>[]): Promise<number> {
  for (let i = 0; i < rows.length; i += CHUNK) await tx`INSERT INTO ${tx(table)} ${tx(rows.slice(i, i + CHUNK))}`;
  return rows.length;
}

export type Publish = { surface: Surface; url: string };

/**
 * Load one planned build. With `publish`, also record that THIS build was just published on that surface, in the same
 * transaction: the publication row gets now(), and revision.published_at is set when the surface is the artifact (the
 * source of truth; a mirror alone does not publish a build). Returns the rows written per table (reference_link null
 * when the table was skipped).
 */
export async function writePlan(
  db: SQL, plan: Plan, opts: { publish?: Publish | undefined } = {},
): Promise<Counts & { revisionId: string; newest: boolean }> {
  return db.begin(async (tx) => {
    const p = plan.packet, r = plan.revision;
    const [pk] = await tx`
      INSERT INTO packet ${tx(p)}
      ON CONFLICT (repo_slug, slug) DO UPDATE SET title = EXCLUDED.title, owner = EXCLUDED.owner, status = EXCLUDED.status,
        status_label = EXCLUDED.status_label, packet_path = EXCLUDED.packet_path, updated_at = EXCLUDED.updated_at
      RETURNING id`;
    const packetId: string = pk.id;

    // published_at: a reload keeps a publish recorded after the fact (--published) when meta.json has none.
    const [rv] = await tx`
      INSERT INTO revision ${tx({ packet_id: packetId, ...r })}
      ON CONFLICT (packet_id, rev, generated) DO UPDATE SET rev_note = EXCLUDED.rev_note, sha = EXCLUDED.sha,
        sections = EXCLUDED.sections, diagrams = EXCLUDED.diagrams, decisions = EXCLUDED.decisions, prs = EXCLUDED.prs,
        schema_tables = EXCLUDED.schema_tables, refs_linked = EXCLUDED.refs_linked, refs_unlinked = EXCLUDED.refs_unlinked,
        citations = EXCLUDED.citations, lint_errors = EXCLUDED.lint_errors, lint_warnings = EXCLUDED.lint_warnings,
        page_bytes = EXCLUDED.page_bytes, bundle_gzip_bytes = EXCLUDED.bundle_gzip_bytes,
        published_at = COALESCE(EXCLUDED.published_at, revision.published_at), loaded_at = now()
      RETURNING id`;
    const revisionId: string = rv.id;

    // Child rows of this build: replaced whole.
    for (const t of ["reference_link", "citation", "lint_finding", "eval_run"]) await tx`DELETE FROM ${tx(t)} WHERE revision_id = ${revisionId}`;
    const withRev = <T extends object>(rows: T[]) => rows.map((x) => ({ revision_id: revisionId, ...x }));
    const reference_link = plan.referenceLinks ? await insertAll(tx, "reference_link", withRev(plan.referenceLinks)) : null;
    const citation = await insertAll(tx, "citation", withRev(plan.citations));
    const lint_finding = await insertAll(tx, "lint_finding", withRev(plan.lintFindings));
    const eval_run = await insertAll(tx, "eval_run", withRev(plan.evalRuns));

    // Publications named by meta.json are upserted per surface; a publish time recorded after the fact (--published)
    // survives a reload whose meta.json has none, and a surface recorded only by --published stays.
    const pubs = withRev(plan.publications);
    if (pubs.length) {
      await tx`INSERT INTO publication ${tx(pubs)}
        ON CONFLICT (revision_id, surface) DO UPDATE SET url = EXCLUDED.url, is_source_of_truth = EXCLUDED.is_source_of_truth,
          visibility = COALESCE(EXCLUDED.visibility, publication.visibility),
          published_at = COALESCE(EXCLUDED.published_at, publication.published_at)`;
    }
    const publish = opts.publish;
    if (publish) {
      await tx`
        INSERT INTO publication (revision_id, surface, url, is_source_of_truth, published_at)
        VALUES (${revisionId}, ${publish.surface}, ${publish.url}, ${publish.surface === "artifact"}, now())
        ON CONFLICT (revision_id, surface) DO UPDATE SET url = EXCLUDED.url, is_source_of_truth = EXCLUDED.is_source_of_truth,
          published_at = EXCLUDED.published_at`;
      if (publish.surface === "artifact") await tx`UPDATE revision SET published_at = now() WHERE id = ${revisionId}`;
    }
    const publication = new Set([...plan.publications.map((x) => x.surface), ...(publish ? [publish.surface] : [])]).size;

    // Decisions are per packet, updated in place by the newest loaded build; an older build only lowers first_rev.
    const [nw] = await tx`
      SELECT NOT EXISTS (SELECT 1 FROM revision WHERE packet_id = ${packetId} AND (rev, generated) > (${r.rev}, ${r.generated}::timestamptz)) AS newest`;
    const newest: boolean = nw.newest;
    const drows = plan.decisions.map((d) => ({ packet_id: packetId, revision_id: revisionId, first_rev: r.rev, ...d }));
    for (let i = 0; i < drows.length; i += CHUNK) {
      const chunk = tx(drows.slice(i, i + CHUNK));
      if (newest) {
        await tx`INSERT INTO decision ${chunk}
          ON CONFLICT (packet_id, prefix, number) DO UPDATE SET revision_id = EXCLUDED.revision_id, token = EXCLUDED.token,
            title = EXCLUDED.title, status = EXCLUDED.status, state = EXCLUDED.state, quote = EXCLUDED.quote,
            reading = EXCLUDED.reading, decided_by = EXCLUDED.decided_by, decided_on = EXCLUDED.decided_on,
            source = EXCLUDED.source, section_id = EXCLUDED.section_id, anchor = EXCLUDED.anchor,
            first_rev = LEAST(decision.first_rev, EXCLUDED.first_rev), updated_at = now()`;
      } else {
        await tx`INSERT INTO decision ${chunk}
          ON CONFLICT (packet_id, prefix, number) DO UPDATE SET first_rev = LEAST(decision.first_rev, EXCLUDED.first_rev)`;
      }
    }
    return {
      packet: 1, revision: 1, decision: drows.length, reference_link, citation, lint_finding, eval_run, publication, revisionId, newest,
    };
  });
}
