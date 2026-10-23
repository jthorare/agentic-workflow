using Aw.Sindri.Infrastructure.Notifications;

namespace Aw.Sindri.Tests.Notifications;

/// <summary>Records the OS notifications Sindri would show, so no test fires a real one.</summary>
internal sealed class FakeOsNotifier : IOsNotifier
{
    private readonly Lock _gate = new();
    private readonly List<(string Title, string Text)> _shown = [];
    private readonly List<string?> _links = [];

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

    /// <summary>The deep link each notification carried, in order (null for none).</summary>
    public IReadOnlyList<string?> Links
    {
        get
        {
            lock (_gate)
            {
                return [.. _links];
            }
        }
    }

    public ValueTask Show(string title, string text, string? deepLink, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _shown.Add((title, text));
            _links.Add(deepLink);
        }

        return ValueTask.CompletedTask;
    }
}
