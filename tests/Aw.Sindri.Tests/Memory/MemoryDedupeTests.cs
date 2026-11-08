using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The write-time rule: the threshold over tests/fixtures/memory/pairs.json (hand-made 4-dimension vectors standing in for model output).</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryDedupeTests
{
    private static readonly float[] _probe = [1f, 0.25f, 0.02f, 0f];
    private static readonly string[] _ops = ["ops"];

    private sealed record Pair(string Kind, string Left, string Right, float[] LeftVector, float[] RightVector);

    private static IReadOnlyList<Pair> Pairs()
    {
        var path = Path.Combine(RepoRoot(), "tests", "fixtures", "memory", "pairs.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.GetProperty("pairs").EnumerateArray().Select(static p => new Pair(
            p.GetProperty("kind").GetString()!,
            p.GetProperty("left").GetString()!,
            p.GetProperty("right").GetString()!,
            [.. p.GetProperty("leftVector").EnumerateArray().Select(static x => x.GetSingle())],
            [.. p.GetProperty("rightVector").EnumerateArray().Select(static x => x.GetSingle())]))];
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test binaries");
    }

    [Fact]
    public void The_threshold_separates_the_near_pairs_from_the_related_pairs_in_the_fixture_set()
    {
        var pairs = Pairs();
        Assert.Contains(pairs, static p => p.Kind == "near");
        Assert.Contains(pairs, static p => p.Kind == "related");
        Assert.Contains(pairs, static p => p.Kind == "unrelated");

        foreach (var pair in pairs)
        {
            var similarity = MemoryDedupe.Similarity(pair.LeftVector, pair.RightVector);
            Assert.Equal(pair.Kind == "near", similarity >= MemoryDedupe.Threshold);
        }
    }

    [Fact]
    public void Tags_are_the_same_set_when_case_order_and_repeats_differ()
    {
        Assert.True(MemoryDedupe.SameTags(["Deploy", "ops"], ["ops", "deploy", "ops"]));
        Assert.False(MemoryDedupe.SameTags(["deploy"], ["deploy", "ops"]));
    }

    [Fact]
    public void A_duplicate_is_the_most_similar_fact_with_the_same_tags()
    {
        var near = MemorySamples.Fact("near", tags: ["ops"], vector: [1f, 0.2f, 0f, 0f]);
        var otherTags = MemorySamples.Fact("other", tags: ["dev"], vector: [1f, 0.2f, 0f, 0f]);

        var found = MemoryDedupe.FindDuplicate(_probe, _ops, [otherTags, near]);

        Assert.Equal("near", found?.Key);
    }
}
