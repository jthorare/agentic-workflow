using Aw.Sindri.Infrastructure.Notifications;

namespace Aw.Sindri.Tests.Notifications;

/// <summary>Records the OS notifications Sindri would show, so no test fires a real one.</summary>
internal sealed class FakeOsNotifier : IOsNotifier
{
    private readonly Lock _gate = new();
    private readonly List<(string Title, string Text)> _shown = [];

    /// <summary>Every notification shown, in order.</summary>
    public IReadOnlyList<(string Title, string Text)> Shown
    {
        get
        {
            lock (_gate)
            {
                return [.. _shown];
            }
        }
    }

    public ValueTask Show(string title, string text, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _shown.Add((title, text));
        }

        return ValueTask.CompletedTask;
    }
}
