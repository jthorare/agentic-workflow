using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.GetInstance;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The instance view on the wire (API spec §3.8): a golden file of two built views, and the wire name of every member of every layout enum.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceViewWireTests
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true, NewLine = "\n" };

    [Theory]
    [InlineData("desk-task")]
    [InlineData("parked-drift")]
    public void The_built_view_serializes_to_its_golden_file(string name)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(StatusCases.View(name), _options));
        var json = JsonSerializer.Serialize(doc.RootElement, _indented) + "\n";

        Golden.Match(json, typeof(InstanceViewWireTests).Assembly, $"Layout/Golden/instance-view-{name}.json", Normalization.None);
    }

    [Fact]
    public void Every_layout_enum_member_has_its_wire_name()
    {
        Assert.Equal(["incident", "triage", "work"], Wire<RecipeUrgency>(), StringComparer.Ordinal);
        Assert.Equal(["done", "active", "pending"], Wire<StateGlyph>(), StringComparer.Ordinal);
        Assert.Equal(["parked", "escalated", "taken-over"], Wire<EngineOwnedKind>(), StringComparer.Ordinal);
        Assert.Equal(["path", "transition-list"], Wire<PathOrdering>(), StringComparer.Ordinal);
        Assert.Equal(["text", "resume", "grant", "grantTokens", "accept", "cancel-only"], Wire<AnswerKind>(), StringComparer.Ordinal);
        Assert.Equal(["parks", "escalates"], Wire<TimeoutKind>(), StringComparer.Ordinal);
        Assert.Equal(["input", "output"], Wire<FactDirection>(), StringComparer.Ordinal);
        Assert.Equal(["produced", "in-progress", "pending"], Wire<FactStatus>(), StringComparer.Ordinal);
        Assert.Equal(
            ["request", "step-report", "pause", "job", "decision", "desk", "local", "issue"],
            Wire<ActionClass>(),
            StringComparer.Ordinal);
    }

    private static string[] Wire<T>()
        where T : struct, Enum =>
        [.. Enum.GetValues<T>().OrderBy(static v => Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture)).Select(static v => JsonSerializer.Serialize(v, _options).Trim('"'))];
}
