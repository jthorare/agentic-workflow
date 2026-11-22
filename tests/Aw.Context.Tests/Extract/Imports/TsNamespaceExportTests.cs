using Aw.Context.Core.Extract;
using Aw.Context.Core.Extract.Imports;

namespace Aw.Context.Tests.Extract.Imports;

/// <summary>Barrels made of 'export * as Ns from', the way Effect writes them (issue 2983): a name imported from such a barrel is the module itself.</summary>
public sealed class TsNamespaceExportTests
{
    private const string SqlClient = "packages/effect/src/sql/SqlClient.ts";

    private static readonly RepoFile[] _files =
    [
        new("packages/effect/package.json", "{ \"name\": \"effect\", \"exports\": { \".\": \"./src/index.ts\" } }"),
        new(SqlClient, "export const SqlClient = Context.Service<SqlClient>(\"effect/sql/SqlClient\");\nexport const make = () => 1;\n"),
        new("packages/effect/src/sql/index.ts", "export * as SqlClient from \"./SqlClient.ts\";\n"),
        new("packages/effect/src/index.ts", "export * from \"./sql/index.ts\";\nexport * as Sql from \"./sql/index.ts\";\n"),
        new("packages/effect/src/Clock.ts", "export const now = () => 0;\n"),
        new("packages/effect/src/legacy.ts", "export const Clock = 1;\nexport const tick = 2;\n"),
        new("packages/effect/src/time.ts", "export * as Clock from \"./Clock.ts\";\nexport * from \"./legacy.ts\";\n"),
        new("packages/effect/src/held.ts", "import * as Held from \"./Clock.ts\";\nexport { Held };\n"),
        new("app/one-level.ts", "import { SqlClient } from \"../packages/effect/src/sql/index.ts\";\nconst sql = yield* SqlClient.SqlClient;\n"),
        new("app/two-level.ts", "import { SqlClient, Sql } from \"effect\";\n"),
        new("app/clash.ts", "import { Clock, tick } from \"../packages/effect/src/time.ts\";\n"),
        new("app/held.ts", "import { Held } from \"../packages/effect/src/held.ts\";\n"),
    ];

    private static readonly TsSymbolResolver _resolver = new([.. _files.Select(f => new SourceFile(f.Path, f.Text))]);

    private static string? Show(ResolvedSymbol? symbol) => symbol is null ? null : symbol.File + "#" + symbol.Name;

    [Fact, Trait("Lane", "unit")]
    public void A_name_from_a_namespace_re_export_is_the_module_and_its_member_is_the_declaration() =>
        Assert.Equal(
            [SqlClient + "#*", SqlClient + "#SqlClient", null],
            [Show(_resolver.Resolve("app/one-level.ts", "SqlClient")), Show(_resolver.Member("app/one-level.ts", "SqlClient", "SqlClient")), Show(_resolver.Member("app/one-level.ts", "SqlClient", "missing"))]);

    [Fact, Trait("Lane", "unit")]
    public void A_namespace_re_export_reached_through_a_second_barrel_still_ends_on_the_declaring_file() =>
        Assert.Equal(
            [SqlClient + "#SqlClient", SqlClient + "#*", SqlClient + "#make"],
            [Show(_resolver.Member("app/two-level.ts", "SqlClient", "SqlClient")), Show(_resolver.Member("app/two-level.ts", "Sql", "SqlClient")), Show(_resolver.Member("app/two-level.ts", "SqlClient", "make"))]);

    [Fact, Trait("Lane", "unit")]
    public void A_namespace_export_wins_over_a_star_export_of_the_same_name_and_other_names_still_come_through_the_star() =>
        Assert.Equal(
            ["packages/effect/src/Clock.ts#*", "packages/effect/src/Clock.ts#now", "packages/effect/src/legacy.ts#tick"],
            [Show(_resolver.Resolve("app/clash.ts", "Clock")), Show(_resolver.Member("app/clash.ts", "Clock", "now")), Show(_resolver.Resolve("app/clash.ts", "tick"))]);

    [Fact, Trait("Lane", "unit")]
    public void A_namespace_import_exported_again_by_name_is_the_module() =>
        Assert.Equal(["packages/effect/src/Clock.ts#*", "packages/effect/src/Clock.ts#now"], [Show(_resolver.Resolve("app/held.ts", "Held")), Show(_resolver.Member("app/held.ts", "Held", "now"))]);

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_imports_a_service_through_a_namespace_barrel_depends_on_the_barrel_and_the_service_file()
    {
        var pairs = ImportEdges.Find(_files).Pairs.Select(p => $"{p.From}>{p.To}").ToList();

        Assert.Contains("app/one-level.ts>packages/effect/src/sql/index.ts", pairs);
        Assert.Contains("app/one-level.ts>" + SqlClient, pairs);
        Assert.Contains("app/two-level.ts>" + SqlClient, pairs);
    }
}
