using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Graph;

/// <summary>
/// The "Did you mean" choices carry map labels on two paths (#3045): a question's suggestions, and the choices extension of SND-CONTEXT-003 and -037
/// that reaches agents through MCP. Both scrub a label the same way, so a planted secret never leaves on either.
/// </summary>
public sealed class AnchorChoiceScrubTests : IDisposable
{
    private const string Scrubbed = "Tab groups [REDACTED:fake]";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private Task<MapImported> Import() => _world.ImportYaml(MapWorld.Yaml(
        "schemaVersion: 1",
        "kind: authored",
        "repo: aw-split-panes",
        "nodes:",
        "  - {key: \"feature:restore\", label: \"Restore\"}",
        "  - {key: \"concept:tabs.tab-groups\", label: \"Tab groups " + FakeScrubber.Secret + "\"}",
        "  - {key: \"concept:tabs.tab-groups-2\", label: \"Tab groups " + FakeScrubber.Secret + "\"}"));

    private static List<string> ChoiceLabels(Kernel.Error error)
    {
        var member = Assert.Single(error.Extensions ?? Kernel.ErrorExtensions.Empty, m => m.Key == "choices");
        return [.. member.Value.EnumerateArray().Select(e => e.GetProperty("label").GetString() ?? string.Empty)];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_name_lists_its_choices_with_scrubbed_labels()
    {
        await Import();

        var shown = await _world.Engine.ShowNode("tab groups zzz", MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, shown.Error.Code);
        Assert.Contains(Scrubbed, ChoiceLabels(shown.Error));
        Assert.All(ChoiceLabels(shown.Error), l => Assert.DoesNotContain(FakeScrubber.Secret, l, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_name_shared_by_two_nodes_lists_its_choices_with_scrubbed_labels()
    {
        await Import();

        var shown = await _world.Engine.ShowNode("Tab groups " + FakeScrubber.Secret, MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None);

        Assert.Equal(ContextErrors.AmbiguousAnchor.Code, shown.Error.Code);
        Assert.Equal([Scrubbed, Scrubbed], ChoiceLabels(shown.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_question_lists_its_suggestions_with_the_same_scrubbed_labels()
    {
        await Import();

        var result = (await _world.Engine.Query(new ContextQuery("restore tab groups"), MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None)).Value;

        var labels = (result.Suggestions ?? []).Select(s => s.Label).ToList();
        Assert.Contains(Scrubbed, labels);
        Assert.All(labels, l => Assert.DoesNotContain(FakeScrubber.Secret, l, StringComparison.Ordinal));
    }
}
