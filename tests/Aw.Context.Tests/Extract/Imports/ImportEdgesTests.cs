using System.Globalization;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Extract.Imports;

namespace Aw.Context.Tests.Extract.Imports;

public sealed class ImportEdgesTests
{
    private static readonly RepoFile[] _typeScript =
    [
        new("tsconfig.json", "{ // paths\n \"compilerOptions\": { \"baseUrl\": \".\", \"paths\": { \"@app/*\": [\"src/*\"], }, }, }"),
        new("packages/core/package.json", "{ \"name\": \"@fx/core\", \"main\": \"src/index.ts\" }"),
        new("packages/core/src/index.ts", "export const core = 1;"),
        new("packages/core/src/lib/deep.ts", "export const deep = 1;"),
        new("src/app.ts", string.Join('\n',
            "import { a } from './util';",
            "import b from './lib/b.js';",
            "import c from './widgets';",
            "import { core } from '@fx/core';",
            "import { deep } from '@fx/core/lib/deep';",
            "import { al } from '@app/shared/thing';",
            "const r = require('./req');",
            "const d = await import('./dyn');",
            "// import x from './commented';",
            "/* import y from './blocked'; */",
            "import react from 'react';",
            "import self from './app';")),
        new("src/util.ts", ""),
        new("src/lib/b.js", ""),
        new("src/widgets/index.ts", ""),
        new("src/shared/thing.ts", ""),
        new("src/req.ts", ""),
        new("src/dyn.ts", ""),
        new("src/commented.ts", ""),
        new("src/blocked.ts", ""),
        new("node_modules/react/index.js", ""),
    ];

    private static readonly RepoFile[] _csharp =
    [
        new("app/Program.cs", "using System;\nusing Fx.Models;\nglobal using Fx.Util;\nusing static Fx.Util.Helpers;\nusing Alias = Fx.Models.A;\n"),
        new("src/Models/A.cs", "namespace Fx.Models;\npublic class A {}"),
        new("src/Models/B.cs", "namespace Fx.Models\n{\n public class B {}\n}"),
        new("src/Util/H.cs", "namespace Fx.Util;\npublic static class Helpers {}"),
        new("src/Other/O.cs", "namespace Fx.Other;\npublic class O {}"),
    ];

    private static string[] Shape(ImportScan scan) => [.. scan.Pairs.Select(p => $"{p.From}>{p.To}")];

    private static RepoFile[] Declaring(string ns, int count, string consumer) =>
    [
        new(consumer, $"using {ns};"),
        .. Enumerable.Range(0, count).Select(i => new RepoFile($"decl/F{i.ToString("D2", CultureInfo.InvariantCulture)}.cs", $"namespace {ns};")),
    ];

    [Fact, Trait("Lane", "unit")]
    public void TypeScript_imports_resolve_relative_workspace_and_alias_specifiers()
    {
        var scan = ImportEdges.Find(_typeScript);

        Assert.Equal(
        [
            "src/app.ts>packages/core/src/index.ts",
            "src/app.ts>packages/core/src/lib/deep.ts",
            "src/app.ts>src/dyn.ts",
            "src/app.ts>src/lib/b.js",
            "src/app.ts>src/req.ts",
            "src/app.ts>src/shared/thing.ts",
            "src/app.ts>src/util.ts",
            "src/app.ts>src/widgets/index.ts",
        ], Shape(scan));
        Assert.Equal(0, scan.SkippedNamespaces);
    }

    [Fact, Trait("Lane", "unit")]
    public void CSharp_usings_reach_every_file_that_declares_the_namespace()
    {
        var scan = ImportEdges.Find(_csharp);

        Assert.Equal(
        [
            "app/Program.cs>src/Models/A.cs",
            "app/Program.cs>src/Models/B.cs",
            "app/Program.cs>src/Util/H.cs",
        ], Shape(scan));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_namespace_declared_in_more_files_than_the_cap_makes_no_edges_and_is_counted()
    {
        var scan = ImportEdges.Find(Declaring("Big.Ns", ImportEdges.MaxNamespaceFiles + 1, "use/User.cs"));

        Assert.Empty(scan.Pairs);
        Assert.Equal(1, scan.SkippedNamespaces);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_namespace_declared_in_exactly_the_cap_still_makes_its_edges()
    {
        var scan = ImportEdges.Find(Declaring("Edge.Ns", ImportEdges.MaxNamespaceFiles, "use/User.cs"));

        Assert.Equal(ImportEdges.MaxNamespaceFiles, scan.Pairs.Count);
        Assert.Equal(0, scan.SkippedNamespaces);
    }
}
