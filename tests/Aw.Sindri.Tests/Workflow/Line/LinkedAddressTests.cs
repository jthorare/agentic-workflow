using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>A linked instance's address, '{line}/phase.{id}.{attempt}' (line spec LN-WF1), and the link back to the line.</summary>
public sealed class LinkedAddressTests
{
    private static readonly InstanceAddress _line = InstanceAddress.From("T-3/line/1");

    [Fact, Trait("Lane", "unit")]
    public void A_linked_address_is_the_lines_with_the_recipe_the_phase_id_and_the_attempt()
    {
        var address = _line.Linked(RecipeId.From("workflow.phase"), "p2", attempt: 3);

        Assert.Equal("T-3/line/1/phase.p2.3", address.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_linked_address_parses_back_and_names_the_line_as_its_parent()
    {
        Assert.True(InstanceAddress.TryFrom("T-3/line/1/phase.api-layer.2", out var address));

        Assert.Equal(_line, address.LinkedParent());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("T-3/line/1")]
    [InlineData("T-3/line/1/scoping.2")]
    [InlineData("T-3/line/1/phase.p2.1/build.4")]
    public void A_root_or_a_child_is_linked_to_nothing(string address)
        => Assert.Null(InstanceAddress.From(address).LinkedParent());

    [Theory, Trait("Lane", "unit")]
    [InlineData("T-3/line/1/phase.P2.1")]
    [InlineData("T-3/line/1/phase.p2.0")]
    [InlineData("T-3/line/1/phase..1")]
    public void An_address_with_a_bad_phase_id_or_attempt_is_refused(string address)
        => Assert.False(InstanceAddress.TryFrom(address, out _));
}
