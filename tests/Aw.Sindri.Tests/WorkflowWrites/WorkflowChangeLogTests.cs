using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.WorkflowWrites;

/// <summary>The store save and the event append are two steps; a failed append says the change is saved.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowChangeLogTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task A_failed_append_names_the_revision_that_stays_saved()
    {
        var payload = new WorkflowChangedPayload(Id, Slug, Revision: 4, SpecHash: null, By: "owner");

        var recorded = await _rig.Log.Record(EventTypes.WorkflowRevised, payload, Token);

        Assert.Equal(SindriErrors.NotRunning.Code, recorded.Error.Code);
        Assert.Contains("saved at revision 4 of 'drift-check'", recorded.Error.Message, StringComparison.Ordinal);
    }
}
