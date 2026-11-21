using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The headless window, shell and fake API a key-frame test runs against; frames are saved under frames/ as u3-*.png.</summary>
public abstract class FrameHost : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly Type _owner;

    protected FrameHost()
    {
        _owner = GetType();
    }

    private protected FakeSindriApi Server { get; } = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private protected FakeTimeProvider Clock { get; } = new(A.Noon);

    private protected FakeMotionPreference Motion { get; } = new();

    private protected FakeFilePicker Files { get; } = new();

    private protected static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the host's resources.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stop.Dispose();
        }
    }

    private protected async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(_owner.Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    /// <summary>The folder chooser the shell is given: available unless a test turns it off.</summary>
    private protected FakeFolderPicker Folders { get; } = new();

    private protected async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(Func<ShellViewModel, bool>? ready = null, double? width = null, double? height = null)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, Server, Clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard(), Motion, new WindowPorts(NullLauncher.Instance, Files, Folders));
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        if (width is { } w)
        {
            window.Width = w;
        }

        if (height is { } h)
        {
            window.Height = h;
        }

        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && !shell.Queue.IsLoading && (ready?.Invoke(shell) ?? true));
        return (window, shell);
    }

    // The frame's disclosures are closed until asked for; a test that reads what is inside opens them first.
    private protected static void OpenDisclosures(Window window)
    {
        for (var pass = 0; pass < 3; pass++)   // an opened disclosure may hold another
        {
            foreach (var expander in window.GetVisualDescendants().OfType<Expander>().ToList())
            {
                expander.IsExpanded = true;
            }

            Settle(window);
        }
    }

    private protected static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private protected static async Task Until(Window window, Func<bool> condition, string what = "the view never reached the expected state")
    {
        for (var i = 0; i < 3000 && !condition(); i++)
        {
            await Task.Delay(1, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), what);
    }

    private protected static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private protected static bool Visible(Visual within, string name) => within.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)?.IsEffectivelyVisible == true;

    private protected static void Frame(Window window, string name, string prefix = "u3-")
    {
        Settle(window);
        FrameFile.Save(window, prefix + name);
    }
}
