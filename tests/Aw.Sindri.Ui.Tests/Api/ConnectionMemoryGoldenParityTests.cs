using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Api.Memory;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// The JSON property names of the app's connection and memory records equal the keys of the goldens the server serves, recursively: a field the server
/// adds or renames fails here, even when the app would still read the answer (the app ignores what it doesn't know). A missing golden fails, with its slug named.
/// Key names only: a golden's null fields give no value kind to compare.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionMemoryGoldenParityTests
{
    // Each slug's shape, with the scenarios that hold another one. Problem bodies (ProblemDetails) are skipped.
    private static readonly (string Slug, Type Default, (string Scenario, Type Shape)[] Others)[] _routes =
    [
        ("connections", typeof(ConnectionList), [("added-key", typeof(ConnectionAdded)), ("added-oauth", typeof(ConnectionAdded))]),
        ("connections-id", typeof(ConnectionOne), [("removed", typeof(ConnectionRemoved))]),
        ("connections-id-test", typeof(ConnectionTested), []),
        ("connections-id-signin", typeof(ConnectionAdded), []),
        ("memories", typeof(MemoryList), [("created", typeof(MemoryCreated))]),
        ("memories-id-search", typeof(MemorySearchView), []),
        ("memories-id-facts", typeof(MemoryFactWritten), [("forgot", typeof(MemoryForgotten)), ("forgot-tag", typeof(MemoryForgotten))]),
    ];

    [Fact]
    public void The_apps_records_have_the_keys_of_the_served_goldens()
    {
        var goldens = GoldenFiles.All().Where(g => g.Kind == "Api").ToList();
        var missing = new List<string>();
        var checkedCount = 0;
        foreach (var (slug, standard, others) in _routes)
        {
            var served = goldens.Where(g => string.Equals(g.Slug, slug, StringComparison.Ordinal) && !IsProblem(g.Body)).ToList();
            if (served.Count == 0)
            {
                missing.Add(slug);
                continue;
            }

            foreach (var (_, _, scenario, body) in served)
            {
                var shape = others.FirstOrDefault(o => string.Equals(o.Scenario, scenario, StringComparison.Ordinal)).Shape ?? standard;
                var golden = JsonNode.Parse(body);
                var expected = Keys(golden, string.Empty);
                var actual = Names(shape);
                Assert.True(
                    expected.SetEquals(actual),
                    $"{slug}/{scenario} as {shape.Name}: only in the golden [{string.Join(", ", expected.Except(actual, StringComparer.Ordinal))}], only in the app's record [{string.Join(", ", actual.Except(expected, StringComparer.Ordinal))}]");
                checkedCount++;
            }
        }

        Assert.True(missing.Count == 0, $"goldens missing for: {string.Join(", ", missing)} ({checkedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} checked)");
        Assert.True(checkedCount > 0);
    }

    private static bool IsProblem(string body) => JsonNode.Parse(body) is JsonObject o && o.ContainsKey("title") && o.ContainsKey("status") && o.ContainsKey("traceId");

    // The golden's key paths ('connection.name'): an object's keys, recursively; an array's elements merged; a dictionary-valued 'config' is data, so its keys stop at 'config'.
    private static HashSet<string> Keys(JsonNode? node, string prefix)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, child) in obj)
                {
                    var path = prefix.Length == 0 ? name : prefix + "." + name;
                    _ = keys.Add(path);
                    if (!string.Equals(name, "config", StringComparison.Ordinal))
                    {
                        keys.UnionWith(Keys(child, path));
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    keys.UnionWith(Keys(item, prefix));
                }

                break;
            default:
                break;
        }

        return keys;
    }

    // The same paths from the app's record: each property's wire name, recursing into object and list-of-object properties (a dictionary is data).
    private static HashSet<string> Names(Type type)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Walk(SindriApiClient.Json.GetTypeInfo(type), string.Empty, keys, 0);
        return keys;
    }

    private static void Walk(JsonTypeInfo info, string prefix, HashSet<string> keys, int depth)
    {
        var target = info.Kind == JsonTypeInfoKind.Enumerable && info.ElementType is { } element ? SindriApiClient.Json.GetTypeInfo(element) : info;
        if (target.Kind != JsonTypeInfoKind.Object || depth > 6)
        {
            return;
        }

        foreach (var property in target.Properties)
        {
            var path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            _ = keys.Add(path);
            Walk(SindriApiClient.Json.GetTypeInfo(property.PropertyType), path, keys, depth + 1);
        }
    }
}
