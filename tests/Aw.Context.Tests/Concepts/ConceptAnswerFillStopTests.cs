using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Concepts;

/// <summary>Issue 4110: a publish fill writes context.db after the import returned, so stopping the host must cancel it and wait for it; a fill still writing while the home is deleted failed the delete with 'Directory not empty'.</summary>
[Trait("Lane", "unit")]
public sealed class ConceptAnswerFillStopTests
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Disposing_the_container_cancels_a_running_fill_and_waits_for_it_to_end()
    {
        var ct = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConceptAnswerFills>(_ => new ConceptAnswerFills(async (scope, token) =>
        {
            started.SetResult();
            try
            {
                await never.Task.WaitAsync(token);   // a fill that only ends when it is told to stop
            }
            finally
            {
                ended.SetResult();
            }
        }));
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IConceptAnswerFills>().Start(MapWorld.HumanVia(ContextClient.Http));
        await started.Task.WaitAsync(_patience, ct);

        await provider.DisposeAsync().AsTask().WaitAsync(_patience, ct);

        Assert.True(ended.Task.IsCompleted, "the container's dispose returned while the fill was still running");
    }

    [Fact]
    public async Task A_sync_dispose_cancels_a_running_fill_and_returns_without_waiting_for_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fills = new ConceptAnswerFills(async (scope, token) =>
        {
            started.SetResult(token);
            await release.Task;   // why: ignores the token, so a dispose that waited for the fill would never return
            ended.SetResult();
        });
        fills.Start(MapWorld.HumanVia(ContextClient.Http));
        var token = await started.Task.WaitAsync(_patience, ct);

#pragma warning disable MA0042 // why: the sync Dispose is the behavior under test
        fills.Dispose();
#pragma warning restore MA0042

        Assert.True(token.IsCancellationRequested, "the sync dispose did not cancel the running fill");
        Assert.False(ended.Task.IsCompleted, "the sync dispose waited for the fill");
        release.SetResult();
        await ended.Task.WaitAsync(_patience, ct);
    }
}
