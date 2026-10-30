using System.Runtime.CompilerServices;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Avalonia.Headless resets the dispatcher at the start of every test (Dispatcher.ResetBeforeUnitTests) and the first
/// thread to read Dispatcher.UIThread afterwards owns it. A listener a test left running that read the static from a
/// pool thread in that window gave the dispatcher to the pool thread, and the next test's compositor failed with "The calling
/// thread cannot access this object because a different thread owns it". So the listener uses the dispatcher it captured on
/// the UI thread and never reads the static from its own thread.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InstanceListenerThreadTests
{
    [Fact]
    public async Task A_message_read_on_a_pool_thread_while_the_dispatcher_is_unbound_does_not_give_it_to_that_thread()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(InstanceListenerThreadTests).Assembly);
        var instance = new ScriptedInstance();
        using var stop = new CancellationTokenSource();
        Task? listening = null;

        var dispatched = session.Dispatch(() =>
        {
            var listener = new InstanceListener(instance);
            listener.Deliver(static _ => { });
            listening = listener.Listen(stop.Token);
            ResetDispatcher();   // the window between two tests, in which nothing owns the dispatcher yet

            instance.Arrive("focus");   // no await from here: its continuation would post to the dispatcher just reset
            RealTime.Sleep(TimeSpan.FromMilliseconds(300));   // the listener reads the message at once; this is its grace

            return Dispatcher.UIThread.CheckAccess();
        }, TestContext.Current.CancellationToken);

        // Bounded: with the dispatcher bound to a dead pool thread the session never runs the job, and a red test must fail, not hang.
        var finished = await Task.WhenAny(dispatched, Task.Delay(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
        await stop.CancelAsync();
        Assert.True(ReferenceEquals(finished, dispatched), "the session never ran the job: the listener's pool thread took the dispatcher");
        var ownedByUiThread = await dispatched;
        Assert.True(ownedByUiThread, "the listener's pool thread read Dispatcher.UIThread first and now owns the dispatcher");
        _ = listening;
    }

    // Avalonia.Headless does this at the start of every Dispatch; the method is internal to Avalonia.
    private static void ResetDispatcher() =>
        typeof(Dispatcher).GetMethod("ResetBeforeUnitTests", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.Invoke(obj: null, parameters: null);

    private sealed class ScriptedInstance : ISingleInstance
    {
        private readonly TaskCompletionSource<InstanceMessage> _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arrive(string text) => _next.SetResult(new InstanceMessage(text, static () => true, CancellationToken.None));

        public async IAsyncEnumerable<InstanceMessage> Messages([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return await _next.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public int? HolderProcessId() => null;

        public void Retire()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
