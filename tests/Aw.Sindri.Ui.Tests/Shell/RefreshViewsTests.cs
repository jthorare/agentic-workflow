using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The followed views' re-read after an event (#2775): the reads overlap instead of waiting on each other, and one view that throws stops no other.</summary>
[Lane(TestLane.Unit)]
public sealed class RefreshViewsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private HealthViewModel Health() => new(new InMemoryEndpointRecords(), _server, TimeProvider.System, new FakeClipboard());

    [Fact]
    public Task A_slow_view_does_not_hold_back_the_next_views_read() => UiThread.Run(async () =>
    {
        var health = Health();
        var slow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;
        health.Follow(async (_, token) => await slow.Task.WaitAsync(token));
        health.Follow((_, _) =>
        {
            secondStarted = true;
            return Task.CompletedTask;
        });

        var refresh = health.RefreshViews(_api, Token);

        Assert.True(secondStarted);
        Assert.False(refresh.IsCompleted);
        slow.SetResult();
        await refresh;
        Assert.Equal(1, health.ViewReads);
    }, Token);

    [Fact]
    public Task A_view_that_throws_stops_no_other_and_its_exception_comes_out_once_all_have_ended() => UiThread.Run(async () =>
    {
        var health = Health();
        var after = false;
        health.Follow((_, _) => throw new InvalidOperationException("the view broke"));
        health.Follow(async (_, _) =>
        {
            await Task.Yield();
            after = true;
        });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => health.RefreshViews(_api, Token));

        Assert.Equal("the view broke", thrown.Message);
        Assert.True(after);
        Assert.Equal(1, _server.MapPublishCalls);
        Assert.Equal(0, health.ViewReads);   // as before: a refresh a view threw in is not counted as handled
    }, Token);
}
