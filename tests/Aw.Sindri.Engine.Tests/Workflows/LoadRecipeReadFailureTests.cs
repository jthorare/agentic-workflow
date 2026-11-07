using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'LoadRecipe' retries a store read that failed (a busy or I/O blip may pass), but not forever: a read that never succeeds ends the instance failed with the read named, instead of leaving it at 'starting' (#2191).</summary>
[Lane(TestLane.Integration)]
public sealed class LoadRecipeReadFailureTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string Read = "the approved recipe could not be read: the approvals store is locked";

    private const int Attempts = 3;

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
        var store = new LockedStore(plain.GetRequiredService<IApprovedRecipes>());
        await using var sindri = _harness.Sindri(services => services.Replace(ServiceDescriptor.Singleton<IApprovedRecipes>(store)));
        await WorkflowsHarness.HoldLease(sindri, ct);
        // why: the production policy backs off 1 s to 1 min; a local activity's backoff under a minute sleeps in the worker in real time, which the time-skipping server can't skip, so this worker alone retries 3 times at 10 ms
        var options = (TemporalWorkerOptions)sindri.GetRequiredService<TemporalWorkerOptions>().Clone();
        options.Interceptors = [.. options.Interceptors ?? [], new ShortLoadRetries(Attempts)];
        using var worker = new TemporalWorker(env.Client, options);

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

    private sealed class ShortLoadRetries(int attempts) : IWorkerInterceptor
    {
        public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) => new Inbound(nextInterceptor, attempts);

        private sealed class Inbound(WorkflowInboundInterceptor next, int attempts) : WorkflowInboundInterceptor(next)
        {
            public override void Init(WorkflowOutboundInterceptor outbound) => base.Init(new Outbound(outbound, attempts));
        }

        private sealed class Outbound(WorkflowOutboundInterceptor next, int attempts) : WorkflowOutboundInterceptor(next)
        {
            public override Task<TResult> ScheduleLocalActivityAsync<TResult>(ScheduleLocalActivityInput input)
            {
                ArgumentNullException.ThrowIfNull(input);
                if (!string.Equals(input.Activity, "LoadRecipe", StringComparison.Ordinal))
                {
                    return base.ScheduleLocalActivityAsync<TResult>(input);
                }

                var options = (LocalActivityOptions)input.Options.Clone();
                options.RetryPolicy = new RetryPolicy { MaximumAttempts = attempts, InitialInterval = TimeSpan.FromMilliseconds(10), BackoffCoefficient = 1, MaximumInterval = TimeSpan.FromMilliseconds(10) };
                return base.ScheduleLocalActivityAsync<TResult>(input with { Options = options });
            }
        }
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
