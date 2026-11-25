using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Tests.WholeMap;

/// <summary>The concept index stays fast on a big repo with no concepts (#4363): every member lands in 'concept:other'.</summary>
public sealed class ConceptIndexScaleTests
{
    private const int Members = 16_000;

    // The index re-sorted the member list after every join, so 'concept:other' cost N^2 log N (8,000 members took 80 s).
    [Fact, Trait("Lane", "unit")]
    public async Task Indexing_16000_members_with_no_concept_takes_under_two_seconds()
    {
        var known = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        for (var i = 0; i < Members; i++)
        {
            var key = NodeKey.From("feature:f" + i.ToString(CultureInfo.InvariantCulture));
            known[key.Value] = new GraphNode(key, "F" + i.ToString(CultureInfo.InvariantCulture));
        }

        var task = Task.Run(() => ConceptIndex.Of(known, []), TestContext.Current.CancellationToken);
        var done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        var finished = ReferenceEquals(done, task);

        Assert.True(finished, "ConceptIndex.Of took over 2 s for 16,000 members (quadratic sort per join, #4363)");
        var members = (await task).MembersOf(ConceptKeys.Other);
        Assert.Equal(Members, members.Count);
        var expected = known.Values.OrderBy(n => n.Key.Kind).ThenBy(n => n.Key.Value, StringComparer.Ordinal).Select(n => n.Key.Value).ToList();
        Assert.Equal(expected, members);
    }
}
