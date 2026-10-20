using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;
using Xunit;

namespace Aw.Sindri.Testing.Line;

/// <summary>The contract every <see cref="IOwnerDecisions"/> honours: no decisions store nothing, and a set either stores every decision or says why none was stored.</summary>
public abstract class OwnerDecisionsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static InstanceAddress Run => InstanceAddress.From("T-3/plan/1");

    /// <summary>The port under test.</summary>
    /// <returns>The port.</returns>
    protected abstract IOwnerDecisions Create();

    /// <summary>An empty set stores nothing and is no error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_decisions_store_nothing()
    {
        var result = await Create().Ingest(Run, "sha256:" + new string('a', 64), [], Token);

        Expect.True(result.IsSuccess);
        Expect.Equal(0, result.Value.Stored);
    }

    /// <summary>A set stores all of its decisions, or stores none and says why.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_set_is_stored_whole_or_skipped_with_a_reason()
    {
        var decisions = new[] { new OwnerDecision("Where does expiry run? On read.", ["surface:route/link-resolve"], "A1"), new OwnerDecision("What does the page show? Only the fact.", ["surface:screen/link-expired"], "A1") };

        var result = await Create().Ingest(Run, "sha256:" + new string('a', 64), decisions, Token);

        Expect.True(result.IsSuccess);
        Expect.True(result.Value.Stored == decisions.Length || (result.Value.Stored == 0 && !string.IsNullOrEmpty(result.Value.Skipped)));
    }
}
