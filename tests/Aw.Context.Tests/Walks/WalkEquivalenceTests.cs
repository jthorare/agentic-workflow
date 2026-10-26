using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Context.Core.Walks;
using Aw.Context.Testing.Code;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.History;
using Aw.Context.Tests.GetCode;
using Aw.Context.Tests.GetHistory;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.QueryContext;
using Aw.Kernel;

namespace Aw.Context.Tests.Walks;

/// <summary>
/// The lenses, code and history fills answer byte for byte the same at any degree of parallelism (issue 1101): one map with a dozen features, their surfaces, actions, things, files and tests is read
/// at degree 1 and at degree 8 through the engine, and the two answers are compared as the JSON the API writes.
/// </summary>
public sealed class WalkEquivalenceTests
{
    private const int Features = 12;
    private const string Header = "schemaVersion: 1\nkind: authored\nrepo: aw-split-panes\n";

    private static readonly string[] _anchors = [.. Enumerable.Range(0, Features).Select(i => "feature:area.f" + Num(i)), "thing:t0", "surface:screen/f3"];

    private static string Num(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static string Sha(int i) => i.ToString("x40", CultureInfo.InvariantCulture);

    private const string Other = "other";

    // With two repos, the odd features' files live in 'other' (the evidence names the repo) and the even ones in the map's own repo.
    private static string RepoOf(int feature, bool twoRepos) => twoRepos && feature % 2 == 1 ? Other : "aw-split-panes";

    private static string Yaml(bool twoRepos = false)
    {
        var nodes = new List<string>();
        var edges = new List<string>();
        for (var i = 0; i < Features; i++)
        {
            var n = Num(i);
            nodes.AddRange(
            [
                $"  - {{key: \"feature:area.f{n}\", label: \"Feature {n}\"}}",
                $"  - {{key: \"surface:screen/f{n}\", label: \"Screen {n}\"}}",
                $"  - {{key: \"action:area.f{n}.go\", label: \"Go {n}\"}}",
                $"  - {{key: \"thing:t{n}\", label: \"Thing {n}\"}}",
                $"  - {{key: \"code:src/area/f{n}.ts\", label: \"f{n}.ts\", provenance: deterministic}}",
                $"  - {{key: \"code:src/area/f{n}b.ts\", label: \"f{n}b.ts\", provenance: deterministic}}",
                $"  - {{key: \"code:tests/f{n}.test.ts\", label: \"f{n}.test.ts\", provenance: deterministic}}",
            ]);
            edges.AddRange(
            [
                $"  - {{from: \"surface:screen/f{n}\", to: \"feature:area.f{n}\", type: part-of}}",
                $"  - {{from: \"action:area.f{n}.go\", to: \"surface:screen/f{n}\", type: on}}",
                $"  - {{from: \"action:area.f{n}.go\", to: \"thing:t{n}\", type: writes}}",
                $"  - {{from: \"action:area.f{n}.go\", to: \"thing:t{Num((i + Features - 1) % Features)}\", type: reads}}",
                $"  - {{from: \"feature:area.f{n}\", to: \"feature:area.f{Num((i + 1) % Features)}\", type: depends-on}}",
                $"  - {{from: \"code:src/area/f{n}.ts\", to: \"feature:area.f{n}\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:{RepoOf(i, twoRepos)}/src/area/f{n}.ts\"}}]}}",
                $"  - {{from: \"code:src/area/f{n}b.ts\", to: \"feature:area.f{n}\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:{RepoOf(i, twoRepos)}/src/area/f{n}b.ts\"}}]}}",
            ]);
            if (i % 2 == 0)
            {
                edges.Add($"  - {{from: \"code:tests/f{n}.test.ts\", to: \"feature:area.f{n}\", type: tests, provenance: deterministic}}");
            }
        }

        return Header + "nodes:\n" + string.Join('\n', nodes) + "\nedges:\n" + string.Join('\n', edges) + "\n";
    }

    private static Dictionary<string, string> Files()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < Features; i++)
        {
            var n = Num(i);
            files[$"src/area/f{n}.ts"] = $"export const f{n} = {n};";
            files[$"src/area/f{n}b.ts"] = $"export const f{n}b = {n};";
            files[$"tests/f{n}.test.ts"] = $"import {{ f{n} }} from '../src/area/f{n}';";
        }

        return files;
    }

    private static HistoryWorld Log(Func<int, bool>? featureKept = null) => new(MapWorld.Head, [.. Enumerable.Range(0, Features * 2).Where(i => featureKept?.Invoke(i / 2) ?? true).Select(i => new FileChange(Sha(i + 1), new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero).AddDays(i),
        i % 2 == 0 ? $"Merge pull request #{Num(i)} from fixture-labs/nts-{Num(100 + i)}-f{Num(i / 2)}" : $"Tidy f{Num(i / 2)} (#{Num(i)})", Parents: i % 2 == 0 ? 2 : 1,
        [$"src/area/f{Num(i / 2)}.ts", $"src/area/f{Num(i / 2)}b.ts"]))]);

    private static async Task<MapWorld> World(int degree, IFileHistory? history = null, bool twoRepos = false)
    {
        var files = Files();
        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files), codeSearch: new FakeCodeSearch(MapWorld.Repos[0], MapWorld.Head, files),
            history: history ?? new FakeFileHistory(MapWorld.Repos[0], Log()), walk: WalkOptions.Of(degree));
        _ = await world.ImportYaml(Yaml(twoRepos));
        return world;
    }

    // Two repos' histories; the repo named by hangOn never answers, so its read ends only at the deadline.
    private static ScriptedHistory TwoRepoHistory(string? hangOn)
    {
        var own = new FakeFileHistory(MapWorld.Repos[0], Log(feature => feature % 2 == 0));
        var other = new FakeFileHistory(MapWorld.Repos[1], Log(feature => feature % 2 == 1));
        return new ScriptedHistory(own, async (repo, query, token) =>
        {
            if (string.Equals(repo.Name.Value, hangOn, StringComparison.Ordinal))
            {
                await Task.Delay(Timeout.Infinite, token);
            }

            return await (string.Equals(repo.Name.Value, Other, StringComparison.Ordinal) ? other : own).Changes(repo, query, token);
        });
    }

    private static async Task<string> FillTwoRepoHistory(int degree, string? hangOn)
    {
        using var world = await World(degree, TwoRepoHistory(hangOn), twoRepos: true);
        var scope = hangOn is null ? MapWorld.Human : MapWorld.HumanWith("""{"deadline":"100ms"}""");
        var window = HistoryWindow.Of(since: null, before: null).Value;
        return Json(await Engines.Advancing(world.Clock, () => world.Engine.History(_anchors, window, MapRead.Current, scope, MapWorld.None).AsTask()));
    }

    private static string Json<T>(Result<T> answer)
    {
        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.Message : null);
        return JsonSerializer.Serialize(answer.Value);
    }

    private static async Task<string> FillLenses(int degree)
    {
        using var world = await World(degree);
        return Json(await world.Engine.LensesOf(_anchors, MapRead.Current, 2, MapWorld.Human, MapWorld.None));
    }

    private static async Task<string> FillCode(int degree)
    {
        using var world = await World(degree);
        return Json(await world.Engine.Code(_anchors, draft: false, mapVersion: null, CodeWorld.WithTestGlobs, MapWorld.None));
    }

    private static async Task<string> FillHistory(int degree)
    {
        using var world = await World(degree);
        return Json(await world.Engine.History(_anchors, HistoryWindow.Of(since: null, before: null).Value, MapRead.Current, MapWorld.Human, MapWorld.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_lenses_of_a_dozen_anchors_are_the_same_bytes_at_degree_1_and_8()
    {
        var serial = await FillLenses(degree: 1);
        var parallel = await FillLenses(degree: 8);

        Assert.Contains("feature:area.f11", serial, StringComparison.Ordinal);
        Assert.Equal(serial, parallel);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_code_lens_of_a_dozen_anchors_is_the_same_bytes_at_degree_1_and_8()
    {
        var serial = await FillCode(degree: 1);
        var parallel = await FillCode(degree: 8);

        Assert.Contains("src/area/f11.ts", serial, StringComparison.Ordinal);
        Assert.Equal(serial, parallel);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_history_of_a_dozen_anchors_is_the_same_bytes_at_degree_1_and_8()
    {
        var serial = await FillHistory(degree: 1);
        var parallel = await FillHistory(degree: 8);

        Assert.Contains("nts-", serial, StringComparison.Ordinal);
        Assert.Equal(serial, parallel);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_history_over_two_repos_is_the_same_bytes_at_degree_1_and_8()
    {
        var serial = await FillTwoRepoHistory(degree: 1, hangOn: null);
        var parallel = await FillTwoRepoHistory(degree: 8, hangOn: null);

        Assert.Contains("\"other\"", serial, StringComparison.Ordinal);
        Assert.Equal(serial, parallel);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_history_is_the_same_bytes_when_the_first_repo_hits_the_deadline_and_the_second_does_not()
    {
        var serial = await FillTwoRepoHistory(degree: 1, hangOn: "aw-split-panes");
        var parallel = await FillTwoRepoHistory(degree: 8, hangOn: "aw-split-panes");

        Assert.Contains("SND-CONTEXT-160", serial, StringComparison.Ordinal);
        Assert.Equal(serial, parallel);
    }
}
