using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The app's Avalonia clipboard over a headless window's clipboard, each call on the UI thread as the app makes it.</summary>
[Lane(TestLane.Integration)]
public sealed class AvaloniaClipboardContractTests : ClipboardContract
{
    // Avalonia's headless platform accepts a write and keeps nothing, so the write's acknowledgement and the refusal are what it can check.
    protected override bool CanReadBack => false;

    protected override IClipboard Create() => new OnUiThread(withWindow: true);

    protected override IClipboard CreateRefusing() => new OnUiThread(withWindow: false);

    protected override Task<string?> Held(IClipboard clipboard) => ((OnUiThread)clipboard).Text();

    private static Task<T> OnUi<T>(Func<Task<T>> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaClipboardContractTests).Assembly)
            .Dispatch(body, TestContext.Current.CancellationToken);

    // The window is made, used and closed inside the UI thread's own calls.
    private sealed class OnUiThread(bool withWindow) : IClipboard
    {
        private Window? _window;

        public Task<bool> SetText(string text, CancellationToken cancellationToken) => OnUi(async () =>
        {
            if (withWindow && _window is null)
            {
                _window = new Window();
                _window.Show();
            }

            return await new AvaloniaClipboard(() => _window).SetText(text, cancellationToken);
        });

        internal Task<string?> Text() => OnUi(async () =>
            _window?.Clipboard is { } clipboard ? await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard) : null);
    }
}
