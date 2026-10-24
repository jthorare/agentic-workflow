using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IDesktopApps that starts nothing: it records each launch, counts the apps running and hands out apps that live until disposed.</summary>
public sealed class FakeDesktopApps : IDesktopApps
{
    private readonly List<AppLaunch> _launches = [];

    /// <summary>Every launch, in order.</summary>
    public IReadOnlyList<AppLaunch> Launches => _launches;

    /// <summary>Programs (the first element of a command) that will not start.</summary>
    public ISet<string> WontStart { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>How many apps are running now.</summary>
    public int Running { get; private set; }

    /// <summary>The display line every started app reports.</summary>
    public string Display { get; set; } = "display: xvfb-run (virtual X server)";

    /// <summary>When set, a started app has already ended with this (what <see cref="IRunningApp.Ended"/> says), as a crashed app does.</summary>
    public string? EndedAtStart { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<IRunningApp>> StartAsync(AppLaunch launch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _launches.Add(launch);
        if (WontStart.Contains(launch.Argv[0]))
        {
            return ValueTask.FromResult<Result<IRunningApp>>(EvidenceErrors.RunFailed.With($"{launch.Argv[0]} could not start"));
        }

        Running++;
        return ValueTask.FromResult<Result<IRunningApp>>(new App(Display, EndedAtStart, () => Running--));
    }

    private sealed class App(string display, string? endedAtStart, Action stopped) : IRunningApp
    {
        private bool _disposed;

        public string Display { get; } = display;

        public string? Ended() => _disposed ? "was stopped" : endedAtStart;

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                stopped();
            }

            return ValueTask.CompletedTask;
        }
    }
}
