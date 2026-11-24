using Aw.Context.Core.Extract;
using Aw.Context.Core.Extract.Imports;

namespace Aw.Context.Tests.Extract.Imports;

/// <summary>
/// Issue 4043, three resolver gaps from the review of #4038: a null 'exports' target blocks its subpath (no folder guess), 'export type *' re-exports like
/// 'export *', and two wildcard keys with the same prefix are tried longer key first, as Node orders them.
/// </summary>
public sealed class TsExportsGapsTests
{
    private static readonly RepoFile[] _files =
    [
        new("packages/gate/package.json", "{ \"name\": \"@fx/gate\", \"exports\": { \"./*\": \"./lib/*.ts\", \"./internal/*\": null } }"),
        new("packages/gate/lib/Clock.ts", ""),
        new("packages/gate/internal/x.ts", ""),
        new("packages/tie/package.json", "{ \"name\": \"@fx/tie\", \"exports\": { \"./a/*\": \"./x/*\", \"./a/*.js\": \"./y/*.ts\" } }"),
        new("packages/tie/x/foo.ts", ""),
        new("packages/tie/y/foo.ts", ""),
        new("lib/Types.ts", "export const Shape = 1;\n"),
        new("lib/Shapes.ts", "export class Square {}\n"),
        new("lib/index.ts", "export type * as Types from \"./Types.ts\";\nexport type * from \"./Shapes.ts\";\n"),
        new("app/blocked.ts", "import { x } from '@fx/gate/internal/x';\n"),
        new("app/open.ts", "import { now } from '@fx/gate/Clock';\n"),
        new("app/tie.ts", "import { foo } from '@fx/tie/a/foo.js';\n"),
        new("app/types.ts", "import { Types, Square } from \"../lib/index.ts\";\n"),
    ];

    private static readonly TsSymbolResolver _resolver = new([.. _files.Select(f => new SourceFile(f.Path, f.Text))]);

    private static List<string> Targets(string from) => [.. ImportEdges.Find(_files).Pairs.Where(p => p.From == from).Select(p => p.To)];

    private static string? Show(ResolvedSymbol? symbol) => symbol is null ? null : symbol.File + "#" + symbol.Name;

    [Fact, Trait("Lane", "unit")]
    public void A_null_target_blocks_its_subpath_and_the_folder_guess_gives_no_edge() =>
        Assert.Empty(Targets("app/blocked.ts"));

    [Fact, Trait("Lane", "unit")]
    public void A_subpath_beside_a_blocked_one_still_resolves() =>
        Assert.Equal(["packages/gate/lib/Clock.ts"], Targets("app/open.ts"));

    [Fact, Trait("Lane", "unit")]
    public void Two_wildcard_keys_with_the_same_prefix_try_the_longer_key_first() =>
        Assert.Equal(["packages/tie/y/foo.ts"], Targets("app/tie.ts"));

    [Fact, Trait("Lane", "unit")]
    public void A_type_only_star_and_namespace_re_export_resolve_like_their_value_forms() =>
        Assert.Equal(
            ["lib/Types.ts#*", "lib/Shapes.ts#Square"],
            [Show(_resolver.Resolve("app/types.ts", "Types")), Show(_resolver.Resolve("app/types.ts", "Square"))]);
}
