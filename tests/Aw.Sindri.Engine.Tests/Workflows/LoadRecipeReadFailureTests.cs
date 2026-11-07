using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'LoadRecipe' retries a store read that failed (a busy or I/O blip may pass), but not forever: a read that never succeeds ends the instance failed with the read named, instead of leaving it at 'starting' (#2191).</summary>
[Lane(TestLane.Integration)]
public sealed class LoadRecipeReadFailureTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string Read = "the approved recipe could not be read: the approvals store is locked";

    private const int Attempts = 3;

    private readonly WorkflowsHarness _harness = new();
    private readonly RetryPolicy _production = RecipeWorkflow.LoadRetries;

    public void Dispose()
    {
        RecipeWorkflow.LoadRetries = _production;
        _harness.Dispose();
    }

    [Fact(Timeout = 120_000)]
    public async Task An_instance_whose_LoadRecipe_read_always_fails_ends_failed_naming_the_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        await using var plain = _harness.Sindri();
        // why: the production policy backs off 1 s to 1 min; a local activity's backoff under a minute sleeps in the worker in real time, which the time-skipping server can't skip
        RecipeWorkflow.LoadRetries = new RetryPolicy { MaximumAttempts = Attempts, InitialInterval = TimeSpan.FromMilliseconds(10), BackoffCoefficient = 1, MaximumInterval = TimeSpan.FromMilliseconds(10) };
        var start = await WorkflowsHarness.RecipeStart(plain, "workflow.item", "T-2191", ct);
        var store = new LockedStore(plain.GetRequiredService<IApprovedRecipes>());
        await using var sindri = _harness.Sindri(services => services.Replace(ServiceDescriptor.Singleton<IApprovedRecipes>(store)));
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, start);

            WorkflowFailedException failed;
            try
            {
                failed = await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), ct));
            }
            catch (Xunit.Sdk.XunitException notFailed)
            {
                // why: 0 reads means the stub never took effect, so the timeout says nothing about the retries
                Assert.Fail(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the instance did not end failed; the store was read {store.Finds} times. {notFailed.Message}"));
                throw;
            }

            Assert.Contains(Read, Messages(failed), StringComparison.Ordinal);
            Assert.Equal(Attempts, store.Finds);
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
        private int _finds;

        public int Finds => Volatile.Read(ref _finds);

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => real.Latest(id, cancellationToken);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _finds);
            return ValueTask.FromResult<Result<ApprovedRecipe>>(SindriErrors.ApprovalsUnavailable.With(Read));
        }

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => real.Check(recipe, cancellationToken);
    }
}
