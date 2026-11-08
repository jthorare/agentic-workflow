using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>The Cancel confirmation sentence (owner decision 2026-10-14).</summary>
public sealed class ConfirmTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void Cancel_names_the_address_the_running_steps_and_the_children() =>
        Assert.Equal("Cancel ITM-01JK1/incident-response/1? 1 running step stops; 1 child is cancelled.", ConfirmText.Cancel(InstanceAddress.From("ITM-01JK1/incident-response/1"), 1, 1));

    [Fact, Trait("Lane", "unit")]
    public void Cancel_is_plural_except_at_one_provisional_owner_default_2026_10_14_night()
    {
        Assert.Equal("Cancel ITM-01JK1/incident-response/1? 2 running steps stop; 3 children are cancelled.", ConfirmText.Cancel(InstanceAddress.From("ITM-01JK1/incident-response/1"), 2, 3));
        Assert.Equal("Cancel ITM-01JK1/incident-response/1? 0 running steps stop; 0 children are cancelled.", ConfirmText.Cancel(InstanceAddress.From("ITM-01JK1/incident-response/1"), 0, 0));
        Assert.Equal("Cancel ITM-01JK1/incident-response/1? 1 running step stops; 2 children are cancelled.", ConfirmText.Cancel(InstanceAddress.From("ITM-01JK1/incident-response/1"), 1, 2));
        Assert.Equal("Cancel ITM-01JK1/incident-response/1? 2 running steps stop; 1 child is cancelled.", ConfirmText.Cancel(InstanceAddress.From("ITM-01JK1/incident-response/1"), 2, 1));
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_cancel_carries_a_confirmation()
    {
        var commands = WorkflowViewFixtures.StatusParkedChild().Commands;

        Assert.Equal(ConfirmText.Cancel(InstanceAddress.From("ITM-01JC8/item/1"), 0, 1), Assert.Single(commands, c => c.Verb == StatusVerb.Cancel).Confirm);
        Assert.All(commands.Where(c => c.Verb != StatusVerb.Cancel), c => Assert.Null(c.Confirm));
    }
}
