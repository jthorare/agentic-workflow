using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

public sealed class BuildMapRecognizerTests
{
    private const string Section = """
        {"repos":["aw-split-panes"],"extract":{"recognizers":[{"kind":"flag","glob":"**/*.yaml","pattern":"rollout: (?<name>[a-z-]+)","keyFrom":"name"}]}}
        """;

    private static Dictionary<string, string> Files() => new(MapWorld.BuildFiles, StringComparer.Ordinal)
    {
        ["config/flags.yaml"] = "rollout: link-expiry\n",
        ["config/secrets/flags.yaml"] = "rollout: hidden-flag\n",
        ["private/flags.yaml"] = "rollout: private-flag\n",
    };

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_recognizer_proposes_the_surface_and_without_it_the_build_proposes_none()
    {
        using var world = new MapWorld(new LeakyRepoReader(MapWorld.Head, Files()));

        var with = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith(Section), MapWorld.None)).Value;
        var without = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None)).Value;

        var flag = Assert.Single(with.Proposed, p => string.Equals(p.Key.Value, "surface:flag/link-expiry", StringComparison.Ordinal));
        Assert.Equal(0.8, flag.Confidence);
        Assert.Equal(["code:config/flags.yaml"], flag.Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.DoesNotContain(without.Proposed, p => p.Key.Value.StartsWith("surface:flag/", StringComparison.Ordinal));
        Assert.Contains(with.Proposed, p => string.Equals(p.Key.Value, "surface:flag/private-flag", StringComparison.Ordinal));   // the control: without a deny glob the private file is read
        Assert.DoesNotContain("recognizers", with.Degraded, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_file_is_never_read_for_a_recognizer()
    {
        var reader = new LeakyRepoReader(MapWorld.Head, Files());
        using var world = new MapWorld(reader);
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };

        var report = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith(Section, [repo]), MapWorld.None)).Value;

        Assert.DoesNotContain(reader.Reads, r => r.StartsWith("private/", StringComparison.Ordinal) || r.Contains("secrets/", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Proposed, p => p.Key.Value.EndsWith("private-flag", StringComparison.Ordinal) || p.Key.Value.EndsWith("hidden-flag", StringComparison.Ordinal));
        Assert.Contains(report.Proposed, p => string.Equals(p.Key.Value, "surface:flag/link-expiry", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recognizer_that_times_out_says_degraded_recognizers_and_the_build_still_writes_its_draft()
    {
        var files = Files();
        files["config/flags.yaml"] = new string('a', 60) + "!";
        using var world = new MapWorld(new LeakyRepoReader(MapWorld.Head, files));
        // A lookahead keeps this pattern on the backtracking engine (LinearRegex's fallback), the only path that can still time out.
        var section = """{"repos":["aw-split-panes"],"extract":{"recognizers":[{"kind":"flag","glob":"config/*.yaml","pattern":"^(?=a)(?<name>(a+)+)$","keyFrom":"name"}]}}""";

        var report = await world.Map.BuildMap(progress: null, MapWorld.HumanWith(section), MapWorld.None);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        Assert.Contains("recognizers", report.Value.Degraded, StringComparer.Ordinal);
        Assert.Contains(report.Value.Proposed, p => string.Equals(p.Key.Value, "surface:command/close-pane", StringComparison.Ordinal));
        Assert.Equal(MapVersionStatus.Draft, report.Value.Draft.Status);
    }
}
