using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Views;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The app's reader of a later launch's messages over the desktop adapter, on the headless UI thread.</summary>
[Lane(TestLane.Integration)]
public sealed class InstanceListenerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(InstanceListenerTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, Token);

    // Review run 4 (julik-frontend-races-reviewer-1): the UI thread acts on a message just as its 1.5 s handling window
    // closes (here acting moves the clock past it). Acting and answering are one decision, so the sender hears that the
    // message was handled, never "not answering" about a message the app acted on.
    [Fact]
    public Task A_message_acted_on_as_its_handling_window_closes_is_answered_as_handled() => OnUi(async () =>
    {
        var clock = new FakeTimeProvider();
        var paths = UiStatePaths.In(Path.Combine(_dir, "ui"));
        await using var first = new SocketSingleInstance(paths, clock);
        await using var second = new SocketSingleInstance(paths, clock);
        Assert.True(await first.TryClaim(Token));
        var acted = new List<string>();
        var listener = new InstanceListener(first);
        listener.Deliver(text =>
        {
            acted.Add(text);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
        });
        using var stop = new CancellationTokenSource();
        var listening = listener.Listen(stop.Token);

        var sending = second.Send("item/ITM-01JC8", Token).AsTask();
        for (var i = 0; i < 500 && !sending.IsCompleted; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(["item/ITM-01JC8"], acted);
        Assert.True(await sending, "the app acted on the message, but its sender was told it wasn't handled");
        await stop.CancelAsync();
        await listening;
    });
}
