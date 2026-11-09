using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>
/// The app's own samples of the note setup answers (<see cref="TodoNoteSetupSample"/>) hold the same keys as Sindri's goldens for check, set and stop (todo Ph3b),
/// so a card test on a sample can't pass on a shape the server never sends. The check golden also names the vault and the note it looked at; the app doesn't read those.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteSetupSampleParityTests
{
    private static readonly string[] _checkOnlyInGolden = ["note", "vault"];

    [Fact]
    public void The_check_sample_has_the_golden_keys_but_the_vault_and_the_note()
    {
        var golden = Keys(GoldenFiles.Node("todos-note-check", "counts"));
        var sample = Keys(JsonNode.Parse(TodoNoteSetupSample.Check(cantImport: [4]))!);

        Assert.Equal(_checkOnlyInGolden, golden.Except(sample, StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(sample.Except(golden, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("todos-note-set", "changed")]
    [InlineData("todos-note-set", "unchanged")]
    [InlineData("todos-note-stop", "changed")]
    [InlineData("todos-note-stop", "unchanged")]
    public void The_set_and_stop_sample_has_the_golden_keys(string slug, string scenario)
    {
        var golden = Keys(GoldenFiles.Node(slug, scenario));
        var sample = Keys(JsonNode.Parse(TodoNoteSetupSample.ChangedTo(new string('0', 64)))!);

        Assert.Equal(golden.Order(StringComparer.Ordinal), sample.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    // Every key path in the document: "a", "a.b", and "a[].b" for the objects inside an array.
    private static List<string> Keys(JsonNode node, string prefix = "")
    {
        var keys = new List<string>();
        switch (node)
        {
            case JsonObject o:
                foreach (var (name, value) in o)
                {
                    var path = prefix.Length == 0 ? name : prefix + "." + name;
                    keys.Add(path);
                    if (value is not null)
                    {
                        keys.AddRange(Keys(value, path));
                    }
                }

                break;
            case JsonArray a:
                foreach (var item in a.OfType<JsonNode>())
                {
                    keys.AddRange(Keys(item, prefix + "[]"));
                }

                break;
            default:
                break;   // a value has no keys
        }

        return [.. keys.Distinct(StringComparer.Ordinal)];
    }
}
