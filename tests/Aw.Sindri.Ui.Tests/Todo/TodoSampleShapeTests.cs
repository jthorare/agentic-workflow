using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The app's hand-made todo sample carries the same JSON keys as the golden the server serves, so a field added to the API can't miss the sample.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoSampleShapeTests
{
    [Fact]
    public void The_sample_has_the_served_golden_top_level_keys() => AssertSame("", Object(Parse(TodoSample.List)), Object(Parse(GoldenFiles.Api("todos", "list"))));

    [Fact]
    public void The_sample_has_the_served_golden_asOf_keys() => AssertSame("asOf", Object(Parse(TodoSample.List)["asOf"]), Object(Parse(GoldenFiles.Api("todos", "list"))["asOf"]));

    [Fact]
    public void The_sample_has_the_served_golden_counts_keys() => AssertSame("counts", Object(Parse(TodoSample.List)["counts"]), Object(Parse(GoldenFiles.Api("todos", "list"))["counts"]));

    [Fact]
    public void The_sample_has_the_served_golden_todo_keys() =>
        AssertSame("todos[]", Union(Parse(TodoSample.List)["todos"], static t => t), Union(Parse(GoldenFiles.Api("todos", "list"))["todos"], static t => t));

    [Fact]
    public void The_sample_has_the_served_golden_link_keys() =>
        AssertSame("todos[].links[]", Union(Parse(TodoSample.List)["todos"], static t => t?["links"]), Union(Parse(GoldenFiles.Api("todos", "list"))["todos"], static t => t?["links"]));

    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;   // why: the sample and the golden are JSON documents

    private static SortedSet<string> Object(JsonNode? node) => new(node!.AsObject().Select(static p => p.Key), StringComparer.Ordinal);

    // The union over all elements: a null field may be left out of one todo and present in another.
    private static SortedSet<string> Union(JsonNode? array, Func<JsonNode?, JsonNode?> pick)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var element in array!.AsArray())
        {
            var inner = pick(element);
            if (inner is JsonArray many)
            {
                foreach (var one in many)
                {
                    keys.UnionWith(Object(one));
                }
            }
            else if (inner is JsonObject)
            {
                keys.UnionWith(Object(inner));
            }
        }

        return keys;
    }

    private static void AssertSame(string where, SortedSet<string> sample, SortedSet<string> served)
    {
        var onlyServed = served.Except(sample, StringComparer.Ordinal).ToList();
        var onlySample = sample.Except(served, StringComparer.Ordinal).ToList();
        Assert.True(onlyServed.Count == 0 && onlySample.Count == 0,
            $"{(where.Length == 0 ? "top level" : where)}: missing from TodoSample: [{string.Join(", ", onlyServed)}]; missing from the served golden: [{string.Join(", ", onlySample)}]");
    }
}
