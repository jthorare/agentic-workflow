using Aw.Context.Core.Extract;
using Aw.Context.Core.Extract.Imports;

namespace Aw.Context.Tests.Extract.Imports;

public sealed class TsSymbolResolverTests
{
    private static readonly RepoFile[] _files =
    [
        new("tsconfig.json", "{ \"compilerOptions\": { \"baseUrl\": \".\", \"paths\": { \"@/*\": [\"src/*\"] } } }"),
        new("src/db/schema.ts", "export const posts = 1;\nexport function load() {}\nexport class Repo {}\nconst hidden = 2;\nexport { hidden as visible };\n"),
        new("src/db/index.ts", "export * from './schema';\nexport { posts as articles } from './schema';\n"),
        new("src/db/loop-a.ts", "export * from './loop-b';\n"),
        new("src/db/loop-b.ts", "export * from './loop-a';\n"),
        new("src/use/direct.ts", "import { posts } from '../db/schema';\nimport type { Repo } from '../db/schema';\n"),
        new("src/use/renamed.ts", "import { posts as p, load } from '@/db/schema';\n"),
        new("src/use/barrel.ts", "import { posts, articles } from '@/db';\n"),
        new("src/use/namespace.ts", "import * as schema from '@/db';\n"),
        new("src/use/other.ts", "import { posts } from 'drizzle-orm';\nimport { gone } from '@/db/loop-a';\n"),
        new("src/use/alias-export.ts", "import { visible } from '@/db/schema';\n"),
    ];

    private static readonly TsSymbolResolver _resolver = new([.. _files.Select(f => new SourceFile(f.Path, f.Text))]);

    private static string? Show(ResolvedSymbol? symbol) => symbol is null ? null : symbol.File + "#" + symbol.Name;

    [Fact, Trait("Lane", "unit")]
    public void A_relative_named_import_resolves_to_the_declaring_file() =>
        Assert.Equal("src/db/schema.ts#posts", Show(_resolver.Resolve("src/use/direct.ts", "posts")));

    [Fact, Trait("Lane", "unit")]
    public void A_renamed_import_through_an_alias_resolves_under_the_name_it_is_exported_as() =>
        Assert.Equal(["src/db/schema.ts#posts", "src/db/schema.ts#load"], [Show(_resolver.Resolve("src/use/renamed.ts", "p")), Show(_resolver.Resolve("src/use/renamed.ts", "load"))]);

    [Fact, Trait("Lane", "unit")]
    public void A_barrel_re_export_and_an_export_star_both_reach_the_declaring_file() =>
        Assert.Equal(["src/db/schema.ts#posts", "src/db/schema.ts#posts"], [Show(_resolver.Resolve("src/use/barrel.ts", "posts")), Show(_resolver.Resolve("src/use/barrel.ts", "articles"))]);

    [Fact, Trait("Lane", "unit")]
    public void A_namespace_member_resolves_through_the_namespace_import() =>
        Assert.Equal(["src/db/schema.ts#posts", null], [Show(_resolver.Member("src/use/namespace.ts", "schema", "posts")), Show(_resolver.Member("src/use/namespace.ts", "schema", "missing"))]);

    [Fact, Trait("Lane", "unit")]
    public void A_name_exported_from_another_local_name_resolves_to_that_local_name() =>
        Assert.Equal("src/db/schema.ts#hidden", Show(_resolver.Resolve("src/use/alias-export.ts", "visible")));

    [Fact, Trait("Lane", "unit")]
    public void A_package_outside_the_repo_a_name_never_imported_and_a_type_import_resolve_to_nothing() =>
        Assert.Equal([null, null, null], [Show(_resolver.Resolve("src/use/other.ts", "posts")), Show(_resolver.Resolve("src/use/direct.ts", "nothing")), Show(_resolver.Resolve("src/use/direct.ts", "Repo"))]);

    [Fact, Trait("Lane", "unit")]
    public void Two_files_that_export_star_from_each_other_end_in_nothing_and_not_in_a_loop() =>
        Assert.Null(_resolver.Resolve("src/use/other.ts", "gone"));
}
