using Aw.Context.Core.Extract.DataStores;

namespace Aw.Context.Tests.Extract;

public sealed class DrizzleAdapterTests
{
    private sealed class Scope(Dictionary<string, string>? stores = null, Dictionary<string, string>? members = null, Dictionary<string, string>? keys = null) : IStoreScope
    {
        public string? Store(string local) => stores is not null && stores.TryGetValue(local, out var s) ? s : null;

        public string? Member(string ns, string member) => members is not null && members.TryGetValue(ns + "." + member, out var s) ? s : null;

        public string? Key(string key) => keys is not null && keys.TryGetValue(key, out var s) ? s : null;
    }

    private static readonly DrizzleAdapter _adapter = new();

    private static Dictionary<string, string> Map(params (string Key, string Value)[] pairs) => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    private static string[] Uses(string text, IStoreScope scope) => [.. _adapter.Accesses(text, scope).Select(a => a.Store + ":" + a.Access).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public void The_tables_a_schema_file_declares_are_named_by_their_sql_name_in_any_dialect_and_quote_style() =>
        Assert.Equal(
            ["a_table", "b_table", "c_table"],
            _adapter.Declared("export const a = pgTable('a_table', {});\nexport const b = sqliteTable(\"b_table\", {});\nconst c = mysqlTable(`c_table`, {});\n"),
            StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void An_identifier_is_bound_to_its_sql_name_only_when_it_is_exported() =>
        Assert.Equal(
            ["posts=blog_posts"],
            _adapter.DeclaredBindings("export const posts = pgTable('blog_posts', {});\nconst local = pgTable('local_table', {});\n").Select(b => b.Identifier + "=" + b.Store),
            StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_has_a_zod_schema_and_no_table_declares_nothing() =>
        Assert.Empty(_adapter.Declared("export const form = z.object({ title: z.string() });\n"));

    [Fact, Trait("Lane", "unit")]
    public void Select_from_and_every_join_form_read_and_insert_update_delete_write()
    {
        var scope = new Scope(Map(("a", "t_a"), ("b", "t_b"), ("c", "t_c"), ("d", "t_d"), ("e", "t_e")), Map(("s.f", "t_f")));

        Assert.Equal(
            ["t_a:Read", "t_b:Read", "t_c:Write", "t_d:Write", "t_e:Write", "t_f:Read"],
            Uses("db.select().from(a).leftJoin(b, eq(a.id, b.id)); db.insert(c).values({}); db.update(d).set({}); db.delete(e); db.select().from(s.f);", scope),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_relational_query_reads_the_store_its_key_names() =>
        Assert.Equal(["t_posts:Read"], Uses("await db.query.posts.findMany({ limit: 5 }); await db.query.other.findFirst();", new Scope(keys: Map(("posts", "t_posts")))), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_name_that_is_not_a_declared_store_a_comment_and_a_string_are_no_use()
    {
        var scope = new Scope(Map(("posts", "t_posts")));

        Assert.Empty(Uses("Array.from(items); map.delete(key); // db.insert(posts)\n/* db.select().from(posts) */ log('db.update(posts)'.length);", scope));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_scope_a_name_that_is_a_stores_own_name_stands_for_it() =>
        Assert.Equal(["authors:Read"], _adapter.Accesses("db.select().from(authors)", new HashSet<string>(StringComparer.Ordinal) { "authors" }).Select(a => a.Store + ":" + a.Access), StringComparer.Ordinal);
}
