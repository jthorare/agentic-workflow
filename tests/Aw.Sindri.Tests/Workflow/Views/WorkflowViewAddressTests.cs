using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>Every address in the goldens is the engine's form (WF spec 6.1), and the ids are the ones the engine mints.</summary>
public sealed class WorkflowViewAddressTests
{
    private static IEnumerable<string> Addresses()
    {
        foreach (var row in WorkflowViewFixtures.RootList().Instances)
        {
            yield return row.Address.Value;
        }

        foreach (var detail in WorkflowViewFixtures.AllDetails())
        {
            yield return detail.Address.Value;
            foreach (var group in detail.Children)
            {
                foreach (var child in group.Rows.Concat(group.Workstreams.SelectMany(w => w.Rows)))
                {
                    yield return child.Address.Value;
                }
            }
        }

        foreach (var status in WorkflowViewFixtures.AllStatuses())
        {
            yield return status.Header.Address.Value;
            if (status.Child is { } child)
            {
                yield return child.Address.Value;
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_golden_address_parses_as_an_engine_InstanceAddress()
    {
        var all = Addresses().Distinct(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(all);
        foreach (var address in all)
        {
            Assert.True(InstanceAddress.TryFrom(address, out _), address);
            Assert.DoesNotContain("workflow/", address, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_child_address_sits_under_its_parent_as_recipe_dot_entry()
    {
        foreach (var detail in WorkflowViewFixtures.AllDetails())
        {
            foreach (var child in detail.Children.SelectMany(g => g.Rows.Concat(g.Workstreams.SelectMany(w => w.Rows))))
            {
                Assert.Matches($"^{System.Text.RegularExpressions.Regex.Escape(detail.Address.Value)}/step\\.[1-9][0-9]*$", child.Address.Value);
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_address_properties_are_InstanceAddress_and_the_fact_query_takes_the_id()
    {
        Type[] views = [typeof(InstanceDetail), typeof(InstanceSummary), typeof(ParentRef), typeof(ChildRow), typeof(StatusChild), typeof(StatusHeader)];
        foreach (var view in views)
        {
            Assert.Equal(typeof(InstanceAddress), view.GetProperty("Address")!.PropertyType);
        }

        Assert.Equal(typeof(WorkflowInstanceId), typeof(GetInstanceFactRequest).GetProperty("Instance")!.PropertyType);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ids_are_derived_from_the_address_and_match_the_engine_vector()
    {
        Assert.Equal("WFI-1de747c7239d87e2a87d1a16b585b6e3", WorkflowInstanceId.Of(InstanceAddress.From("T-50/step/1")).Value);
        foreach (var row in WorkflowViewFixtures.RootList().Instances)
        {
            Assert.Equal(WorkflowInstanceId.Of(row.Address), row.Id);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_root_has_an_item() =>
         Assert.All(WorkflowViewFixtures.RootList().Instances, r => Assert.NotNull(r.ItemId));
}
