using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>K8 (Linux proof): the app never shows a raw catalog id such as "ApplyCommand:command" or "IssueNonce" as a label.</summary>
[Lane(TestLane.Unit)]
public sealed class ActionLabelsTests
{
    private static readonly CommandList _catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;

    // Actions of the recipe and workflow pages the app no longer has: the server still lists them, the app has no button for them and keeps no word of its own.
    private static readonly string[] _gone = ["ApproveAllRecipes", "ApproveRecipe", "RerunInstance"];

    [Fact]
    public void Every_action_id_in_the_catalog_the_app_still_offers_has_a_label_of_its_own()
    {
        Assert.All(_catalog.Rows.Where(r => !_gone.Contains(r.Action, StringComparer.Ordinal)), row =>
        {
            Assert.True(Labels.HasLabel(row.Action), row.Action + " has no label");
            Assert.DoesNotContain(':', Labels.Action(row.Action));
        });
    }

    [Fact]
    public void An_action_of_a_page_the_app_no_longer_has_still_reads_as_words() => Assert.All(_gone, id => Assert.DoesNotContain(':', Labels.Action(id)));

    [Fact]
    public void Every_label_is_its_own()
    {
        var labels = _catalog.Rows.Select(r => Labels.Action(r.Action)).ToList();

        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    // An id this build has no label for shows a humanised form of itself, never the raw "X:y".
    [Theory]
    [InlineData("SomeNewThing", "Some new thing")]
    [InlineData("ApplyCommand:pause-all", "Apply command pause all")]
    [InlineData("RunDoctor2", "Run doctor2")]
    public void An_id_without_a_label_is_humanised(string action, string label) => Assert.Equal(label, Labels.Action(action), StringComparer.Ordinal);
}
