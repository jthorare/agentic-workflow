using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'LoadRecipe' retries a store read that failed (a busy or I/O blip may pass), but not forever: a read that never succeeds ends the instance failed with the read named, instead of leaving it at 'starting' (#2191).</summary>
[Lane(TestLane.Integration)]
public sealed class LoadRecipeReadFailureTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string Read = "the approved recipe could not be read: the approvals store is locked";

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task An_instance_whose_LoadRecipe_read_always_fails_ends_failed_naming_the_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        await using var plain = _harness.Sindri();
        var start = await WorkflowsHarness.RecipeStart(plain, "workflow.item", "T-2191", ct);
        await using var sindri = _harness.Sindri(services => services.Replace(ServiceDescriptor.Singleton<IApprovedRecipes>(new LockedStore(plain.GetRequiredService<IApprovedRecipes>()))));
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, start);

            // why: the time-skipping server jumps the retry backoff while this waits, so bounded retries end in moments and unbounded ones never do
            var failed = await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), ct));

            Assert.Contains(Read, Messages(failed), StringComparison.Ordinal);
        }, ct);
    }

    private static string Messages(Exception failure)
    {
        var messages = new List<string>();
        var e = failure;
        while (e is not null)
        {
            messages.Add(e.Message);
            e = e.InnerException;
        }

        return string.Join(" | ", messages);
    }

    // Latest answers as the real store does (the start reads it); every Find, the read LoadRecipe makes, fails the way a locked store does.
    private sealed class LockedStore(IApprovedRecipes real) : IApprovedRecipes
    {
        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => real.Latest(id, cancellationToken);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ApprovedRecipe>>(SindriErrors.ApprovalsUnavailable.With(Read));

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => real.Check(recipe, cancellationToken);
    }
}
