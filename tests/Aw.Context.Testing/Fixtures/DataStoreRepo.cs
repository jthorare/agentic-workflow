namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// The files of a small repo for the data-store pass: two schema files declaring three tables (quoted, IF NOT EXISTS, schema-qualified),
/// code files that read, write, do both through a query builder, and only mention a table in a comment or a log text, and a test file.
/// </summary>
public static class DataStoreRepo
{
    /// <summary>The repo's name in the code map.</summary>
    public const string Repo = "aw-split-panes";

    /// <summary>Path to text, in the order a listing would give them.</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["db/schema/001_core.sql"] = "CREATE TABLE \"shared_link\" (id integer primary key);\nCREATE TABLE IF NOT EXISTS audit_event (id integer);\n",
        ["db/schema/002_billing.sql"] = "create table billing.invoice_line (id integer);\n",
        ["src/links/read.ts"] = "export const find = (db: Db) => db.query(\"SELECT id FROM shared_link WHERE token = ?\");\n",
        ["src/links/write.ts"] = "export const audit = (db: Db) => db.run(\"INSERT INTO audit_event (id) VALUES (?)\");\n",
        ["src/billing/lines.ts"] = "export const load = (k: Knex, id: number) => k.from('invoice_line').where({ id }).first();\nexport const save = (k: Knex, id: number) => k.table('invoice_line').insert({ id });\n",
        ["src/links/log.ts"] = "// audit_event is written elsewhere\nexport const done = () => logger.info(\"cleaned up shared_link rows\");\n",
        ["tests/links.test.ts"] = "db.query(\"SELECT * FROM shared_link\");\n",
    };
}
