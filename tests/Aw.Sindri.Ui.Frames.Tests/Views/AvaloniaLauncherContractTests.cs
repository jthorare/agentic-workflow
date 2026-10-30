using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The app's Avalonia launcher over a headless top-level, each call on the UI thread as the app makes it.</summary>
[Lane(TestLane.Integration)]
public sealed class AvaloniaLauncherContractTests : LauncherContract
{
    // Avalonia's headless platform opens nothing, so the refusals (no window, a relative path, a missing file) are what it can check.
    protected override bool CanLaunch => false;

    protected override ILauncher Create() => new OnUiThread(withWindow: true);

    protected override ILauncher CreateRefusing() => new OnUiThread(withWindow: false);

    protected override string ExistingFile(ILauncher launcher) => Path.GetFullPath(typeof(AvaloniaLauncherContractTests).Assembly.Location);

    [Fact]
    public async Task A_platform_that_throws_is_answered_false()
    {
        var launcher = new AvaloniaLauncher(() => throw new InvalidOperationException("no window"));

        Expect.True(!await launcher.OpenUri(new Uri("https://example.test/"), TestContext.Current.CancellationToken));
        Expect.True(!await launcher.OpenFile(ExistingFile(launcher), TestContext.Current.CancellationToken));
    }

    private static Task<T> OnUi<T>(Func<Task<T>> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaLauncherContractTests).Assembly)
            .Dispatch(body, TestContext.Current.CancellationToken);

    private sealed class OnUiThread(bool withWindow) : ILauncher
    {
        public Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken) =>
            OnUi(() => new AvaloniaLauncher(Top).OpenUri(uri, cancellationToken));

        public Task<bool> OpenFile(string path, CancellationToken cancellationToken) =>
            OnUi(() => new AvaloniaLauncher(Top).OpenFile(path, cancellationToken));

        private Window? _window;

        private Window? Top() => withWindow ? _window ??= new Window() : null;   // one window per launcher, made on the UI thread
    }
}
