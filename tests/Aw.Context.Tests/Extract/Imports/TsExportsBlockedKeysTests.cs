using Aw.Context.Core.Extract;

namespace Aw.Context.Tests.Extract.Imports;

/// <summary>
/// Issue 4095, follow-ups from the review of #4094: only the first matching 'exports' key can block a subpath, and an exact null key or a conditions object
/// whose leaves are all null blocks it too.
/// </summary>
public sealed class TsExportsBlockedKeysTests
{
    private static readonly RepoFile[] _files =
    [
        new("packages/pkg/package.json", "{ \"name\": \"@fx/pkg\", \"exports\": { \"./*\": null, \"./public/*\": \"./dist/public/*.js\" } }"),
        new("packages/pkg/src/public/foo.ts", ""),
        new("packages/exact/package.json", "{ \"name\": \"@fx/exact\", \"exports\": { \"./internal\": null, \"./*\": \"./lib/*.ts\" } }"),
        new("packages/exact/lib/internal.ts", ""),
        new("packages/exact/internal.ts", ""),
        new("packages/cond/package.json", "{ \"name\": \"@fx/cond\", \"exports\": { \"./secret/*\": { \"import\": null, \"default\": null } } }"),
        new("packages/cond/secret/a.ts", ""),
        new("app/later.ts", "import { foo } from '@fx/pkg/public/foo';\n"),
        new("app/exact.ts", "import { x } from '@fx/exact/internal';\n"),
        new("app/cond.ts", "import { a } from '@fx/cond/secret/a';\n"),
    ];

    private static List<string> Targets(string from) => [.. ImportEdges.Find(_files).Pairs.Where(p => p.From == from).Select(p => p.To)];

    [Fact, Trait("Lane", "unit")]
    public void A_later_blocked_key_does_not_stop_the_folder_guess_of_the_first_match() =>
        Assert.Equal(["packages/pkg/src/public/foo.ts"], Targets("app/later.ts"));

    [Fact, Trait("Lane", "unit")]
    public void An_exact_null_key_blocks_its_subpath_before_a_wildcard_key() =>
        Assert.Empty(Targets("app/exact.ts"));

    [Fact, Trait("Lane", "unit")]
    public void A_conditions_object_whose_leaves_are_all_null_blocks_its_subpath() =>
        Assert.Empty(Targets("app/cond.ts"));
}
