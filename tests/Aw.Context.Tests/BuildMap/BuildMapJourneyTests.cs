using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

public sealed class BuildMapJourneyTests
{
    private const string Spec = "test(\"open\", async ({ page }) => {\n  await page.goto(\"/links\");\n});\n";

    private static async Task<LeakyRepoReader> Build(Dictionary<string, string> extra)
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal);
        foreach (var (path, text) in extra)
        {
            files[path] = text;
        }

        var reader = new LeakyRepoReader(MapWorld.Head, files);
        using var world = new MapWorld(reader);
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };
        _ = await world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [repo]), MapWorld.None);
        return reader;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_journey_is_read_but_not_one_under_a_denied_path()
    {
        var reader = await Build(new(StringComparer.Ordinal) { ["e2e/open.spec.ts"] = Spec, ["private/e2e/secret.spec.ts"] = Spec });

        Assert.Contains("e2e/open.spec.ts", reader.Reads, StringComparer.Ordinal);
        Assert.DoesNotContain("private/e2e/secret.spec.ts", reader.Reads, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task At_most_the_file_cap_of_journeys_are_read()
    {
        var many = Enumerable.Range(0, Journeys.MaxFiles + 5).ToDictionary(i => string.Create(CultureInfo.InvariantCulture, $"e2e/n{i:D4}.spec.ts"), _ => Spec, StringComparer.Ordinal);

        var reader = await Build(many);

        Assert.Equal(Journeys.MaxFiles, reader.Reads.Count(Journeys.IsJourney));
        Assert.DoesNotContain("e2e/n0504.spec.ts", reader.Reads, StringComparer.Ordinal);
    }
}
