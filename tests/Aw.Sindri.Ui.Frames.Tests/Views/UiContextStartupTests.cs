using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Review run 3, M1: the app's factory builds the shell before Avalonia installs its synchronization context, so a view model
/// that captured "the current context" there captured none, and every timer callback then ran on its own pool thread.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class UiContextStartupTests
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "carried to the test thread and rethrown there")]
    private static T OnBareThread<T>(Func<T> body)
    {
        var done = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                Assert.Null(SynchronizationContext.Current);   // the AppBuilder.Configure factory's state in the real app
                done.SetResult(body());
            }
            catch (Exception e)
            {
                done.SetException(e);
            }
        });
        thread.Start();
        thread.Join();
        return done.Task.GetAwaiter().GetResult();
    }

    // The app's factory runs before Avalonia installs its context, so the bodies below run with none on the session thread. They never
    // build an Avalonia object on a thread of their own: a thread that reads Dispatcher.UIThread while the headless session has it
    // unbound (between two tests) takes the dispatcher, and the next test's compositor fails with "a different thread owns it".
    private static Task<T> OnUiWithoutContext<T>(Func<Task<T>> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(UiContextStartupTests).Assembly).Dispatch(async () =>
        {
            var installed = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncContext: null);
            try
            {
                return await body();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(installed);
            }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_app_factory_leaves_the_UI_context_installed_for_the_view_models_it_builds()
    {
        var platform = new SindriPlatform(new InMemoryEndpointRecords(), new InMemoryHumanToken(), new InMemoryPreferencesStore(), new FakeMotionPreference(), new FakeSingleInstance(new FakeInstanceChannel()), new FakeTray());

        var after = await OnUiWithoutContext(() =>
        {
            Assert.Null(SynchronizationContext.Current);
            using var app = SindriApp.Create(platform, new FakeTimeProvider(A.Noon));
            return Task.FromResult(SynchronizationContext.Current);
        });

        Assert.NotNull(after);
    }

    [Fact]
    public async Task The_OS_motion_setting_changing_on_a_pool_thread_after_the_factory_reaches_the_UI_thread()
    {
        var motion = new FakeMotionPreference();
        var platform = new SindriPlatform(new InMemoryEndpointRecords(), new InMemoryHumanToken(), new InMemoryPreferencesStore(), motion, new FakeSingleInstance(new FakeInstanceChannel()), new FakeTray());

        var (thrown, reduced) = await OnUiWithoutContext(async () =>
        {
            using var app = SindriApp.Create(platform, new FakeTimeProvider(A.Noon));
            var failure = await Record.ExceptionAsync(() => Task.Run(() => motion.Set(reduce: true), TestContext.Current.CancellationToken));
            Dispatcher.UIThread.RunJobs();
            return (failure, app.Shell!.NeedsYou.Seen.ReducedMotion);
        });

        Assert.Null(thrown);
        Assert.True(reduced);
    }

    [Fact]
    public void A_post_from_another_thread_with_no_UI_context_fails_loudly_instead_of_running_there()
    {
        var ui = OnBareThread(static () => new UiContext());

        var thrown = Record.Exception(() => Task.Run(() => ui.PostOrInline(static () => { }), TestContext.Current.CancellationToken).GetAwaiter().GetResult());

        _ = Assert.IsType<InvalidOperationException>(thrown);
    }

    [Fact]
    public void A_post_on_the_thread_that_built_it_runs_inline_with_no_UI_context()
    {
        var ran = OnBareThread(static () =>
        {
            var ran = false;
            new UiContext().PostOrInline(() => ran = true);
            return ran;
        });

        Assert.True(ran);
    }
}
