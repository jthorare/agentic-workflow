using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Code;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A repo with implementing files, test files and schema files, a map over it and the code lens asked of it.</summary>
internal sealed class CodeWorld : IDisposable
{
    internal const string Anchor = "feature:sharing";

    internal static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["src/sharing/links.ts"] = "export const sharedLink = 1; // table shared_link",
        ["src/sharing/routes.ts"] = "export const routes = [];",
        ["src/unrelated.ts"] = "export const nothing = 0;",
        ["tests/links.test.ts"] = "import { sharedLink } from '../src/sharing/links';",
        ["tests/routes.test.ts"] = "describe('routes', () => {});",
        ["tests/edge.test.ts"] = "import { sharedLink } from '../src/sharing/links';",
        ["tests/other.test.ts"] = "import { x } from '../src/sharing/links-extra';",
        ["migrations/002_sharing.sql"] = "create table shared_link (id int);",
        ["migrations/003_other.sql"] = "create table other (id int);",
        [".env"] = "TOKEN=hunter2 shared_link",
    };

    private const string Header = "schemaVersion: 1\nkind: authored\nrepo: aw-split-panes\n";

    internal CodeWorld(IReadOnlyDictionary<string, string>? files = null, Func<ICodeSearch, ICodeSearch>? decorate = null, Func<IRepoReader, IRepoReader>? decorateRepo = null, Func<ICodeSearch, Action, ICodeSearch>? decorateTimed = null, ICodeSymbolSource? symbols = null)
    {
        var plain = new FakeCodeSearch(MapWorld.Repos[0], MapWorld.Head, files ?? Files);
        Search = new RecordingCodeSearch(decorateTimed is not null ? decorateTimed(plain, Expire) : decorate is null ? plain : decorate(plain));
        World = new MapWorld(Wrap(decorateRepo, new Testing.Extract.FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files ?? Files)), intent: null, codeSearch: Search, symbols: symbols);
    }

    private static IRepoReader Wrap(Func<IRepoReader, IRepoReader>? decorate, IRepoReader inner) => decorate is null ? inner : decorate(inner);

    internal MapWorld World { get; }

    /// <summary>Runs the profile's deadline out: advances the world's fake clock past any deadline (no wall clock).</summary>
    internal void Expire() => World.Clock.Advance(TimeSpan.FromMinutes(5));

    internal RecordingCodeSearch Search { get; }

    public void Dispose() => World.Dispose();

    /// <summary>The sharing map: two implementing files, one edge test, a thing the feature writes.</summary>
    internal Task<Context.Core.Map.MapImported> ImportSharing(string? commit = null, string[]? extraNodes = null, string[]? extraEdges = null, bool edgeTest = true) =>
        World.ImportYaml(
            (commit is null ? string.Empty : $"commit: {commit}\n") + Header + "nodes:\n" + string.Join('\n',
            [
                "  - {key: \"feature:sharing\", label: \"Sharing\"}",
                "  - {key: \"surface:screen/share\", label: \"Share\"}",
                "  - {key: \"action:sharing.create\", label: \"Create a link\"}",
                "  - {key: \"thing:shared-link\", label: \"Shared link\"}",
                "  - {key: \"code:src/sharing/links.ts\", label: \"links.ts\", provenance: deterministic}",
                "  - {key: \"code:src/sharing/routes.ts\", label: \"routes.ts\", provenance: deterministic}",
                .. edgeTest ? (string[])["  - {key: \"code:tests/edge.test.ts\", label: \"edge.test.ts\", provenance: deterministic}"] : [],
                .. extraNodes ?? [],
            ]) + "\nedges:\n" + string.Join('\n',
            [
                "  - {from: \"surface:screen/share\", to: \"feature:sharing\", type: part-of}",
                "  - {from: \"action:sharing.create\", to: \"surface:screen/share\", type: on}",
                "  - {from: \"action:sharing.create\", to: \"thing:shared-link\", type: writes}",
                "  - {from: \"code:src/sharing/links.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/sharing/links.ts\"}]}",
                "  - {from: \"code:src/sharing/routes.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/sharing/routes.ts\"}]}",
                .. edgeTest ? (string[])["  - {from: \"code:tests/edge.test.ts\", to: \"feature:sharing\", type: tests, provenance: deterministic}"] : [],
                .. extraEdges ?? [],
            ]) + "\n");

    /// <summary>The 'context.code.testGlobs' a profile writes: the engine has no built-in list, so a test that expects test files sets them.</summary>
    internal const string TestGlobs = "[\"**/tests/**\",\"**/*.test.*\"]";

    /// <summary>A human's scope whose 'context.code.testGlobs' are <see cref="TestGlobs"/>.</summary>
    internal static ContextScope WithTestGlobs => MapWorld.HumanWith("{\"code\":{\"testGlobs\":" + TestGlobs + "}}");

    internal ValueTask<Result<CodeView>> Code(ContextScope? scope = null, string[]? anchors = null, bool draft = false, string? mapVersion = null) =>
        World.Engine.Code(anchors ?? [Anchor], draft, mapVersion, scope ?? WithTestGlobs, MapWorld.None);
}
