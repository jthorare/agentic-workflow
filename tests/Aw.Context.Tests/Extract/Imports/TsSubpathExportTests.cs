using Aw.Context.Core.Extract;

namespace Aw.Context.Tests.Extract.Imports;

/// <summary>Subpath imports of a workspace package resolved through its package.json 'exports' map, '"./*"' patterns included (issue 2983).</summary>
public sealed class TsSubpathExportTests
{
    private static readonly RepoFile[] _files =
    [
        new("packages/kit/package.json", "{ \"name\": \"@fx/kit\", \"exports\": { \".\": \"./lib/index.ts\", \"./sql\": \"./lib/sql/index.ts\", \"./*\": \"./lib/*.ts\" } }"),
        new("packages/kit/lib/index.ts", ""),
        new("packages/kit/lib/Clock.ts", ""),
        new("packages/kit/lib/sql/index.ts", ""),
        new("packages/bare/package.json", "{ \"name\": \"@fx/bare\", \"exports\": { \"./*\": \"./lib/*\" } }"),
        new("packages/bare/lib/Clock.ts", ""),
        new("packages/compiled/package.json", "{ \"name\": \"@fx/compiled\", \"exports\": { \"./*\": \"./lib/*.js\" } }"),
        new("packages/compiled/lib/Clock.ts", ""),
        new("packages/cond/package.json", "{ \"name\": \"@fx/cond\", \"exports\": { \"./*\": { \"types\": \"./types/*.d.ts\", \"import\": \"./lib/*.mjs\", \"default\": \"./lib/*.js\" } } }"),
        new("packages/cond/types/Clock.d.ts", ""),
        new("packages/cond/lib/Clock.mjs", ""),
        new("app/with-extension.ts", "import { now } from '@fx/kit/Clock';\n"),
        new("app/exact.ts", "import { sql } from '@fx/kit/sql';\n"),
        new("app/no-extension.ts", "import { now } from '@fx/bare/Clock';\n"),
        new("app/compiled.ts", "import { now } from '@fx/compiled/Clock';\n"),
        new("app/conditions.ts", "import { now } from '@fx/cond/Clock';\n"),
        new("app/missing.ts", "import { gone } from '@fx/kit/Missing';\nimport { gone2 } from '@fx/bare/deep/Missing';\n"),
    ];

    private static List<string> Targets(string from) => [.. ImportEdges.Find(_files).Pairs.Where(p => p.From == from).Select(p => p.To)];

    [Fact, Trait("Lane", "unit")]
    public void A_star_pattern_with_an_extension_in_its_target_resolves_the_subpath() =>
        Assert.Equal(["packages/kit/lib/Clock.ts"], Targets("app/with-extension.ts"));

    [Fact, Trait("Lane", "unit")]
    public void An_exact_subpath_key_resolves_to_its_target() =>
        Assert.Equal(["packages/kit/lib/sql/index.ts"], Targets("app/exact.ts"));

    [Fact, Trait("Lane", "unit")]
    public void A_star_pattern_without_an_extension_or_with_a_compiled_js_target_resolves_to_the_ts_source() =>
        Assert.Equal(["packages/bare/lib/Clock.ts", "packages/compiled/lib/Clock.ts"], [.. Targets("app/no-extension.ts"), .. Targets("app/compiled.ts")]);

    [Fact, Trait("Lane", "unit")]
    public void A_conditions_object_resolves_to_its_code_target_before_its_type_declarations() =>
        Assert.Equal(["packages/cond/lib/Clock.mjs"], Targets("app/conditions.ts"));

    [Fact, Trait("Lane", "unit")]
    public void A_pattern_that_matches_no_file_gives_no_edge() =>
        Assert.Empty(Targets("app/missing.ts"));
}
