using System.Text.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>Every reference to another instance carries its opaque id beside the address (CSC4-5); the status verbs are a closed set with fixed wire names (CSC4-6).</summary>
public sealed class WorkflowViewIdsAndVerbsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_child_row_parent_ref_and_status_child_carries_the_id_of_its_address()
    {
        var details = WorkflowViewFixtures.AllDetails();
        var rows = details.SelectMany(d => d.Children).SelectMany(g => g.Workstreams.SelectMany(w => w.Rows).Concat(g.Rows)).ToList();
        var parents = details.Select(d => d.Parent).OfType<ParentRef>().ToList();
        var childs = WorkflowViewFixtures.AllStatuses().Select(s => s.Child).OfType<StatusChild>().ToList();

        Assert.NotEmpty(rows);
        Assert.NotEmpty(parents);
        Assert.NotEmpty(childs);
        Assert.All(rows, r => Assert.Equal(WorkflowInstanceId.Of(r.Address), r.Id));
        Assert.All(parents, p => Assert.Equal(WorkflowInstanceId.Of(p.Address), p.Id));
        Assert.All(childs, c => Assert.Equal(WorkflowInstanceId.Of(c.Address), c.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_status_verb_serializes_to_its_wire_name_and_back()
    {
        foreach (var verb in Enum.GetValues<StatusVerb>())
        {
            var json = JsonSerializer.Serialize(verb);
            Assert.Equal($"\"{StatusVerbNames.Of(verb)}\"", json);
            Assert.Equal(verb, JsonSerializer.Deserialize<StatusVerb>(json));
        }
    }
}
