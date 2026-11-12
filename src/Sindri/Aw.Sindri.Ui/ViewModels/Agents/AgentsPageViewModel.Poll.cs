namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// When the Agents page reads (<see cref="AgentsPageViewModel"/>, continued; Q9): on events and every 5 s, only while the page is the one shown
/// and the window is not minimised. Running and idle flips are not events (collector spec CO3), so the poll is what shows them.
/// </summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>How often the page reads while it is shown.</summary>
    internal static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(5);

    private bool _shown;
    private bool _minimised;
    private IDisposable? _timer;

    /// <summary>True while the page is shown and the window is not minimised.</summary>
    internal bool IsActive => _shown && !_minimised;

    /// <summary>The page was shown or hidden (the shell's page changed).</summary>
    /// <param name="shown">True while the Agents page is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        _shown = shown;
        Settle(was);
    }

    /// <summary>The window was minimised or restored.</summary>
    /// <param name="minimised">True while minimised.</param>
    internal void SetMinimised(bool minimised)
    {
        var was = IsActive;
        _minimised = minimised;
        Settle(was);
    }

    // Becoming active reads at once and starts the poll; leaving it stops the poll. A read already on its way when it stops is not wanted.
    private void Settle(bool was)
    {
        if (IsActive == was)
        {
            return;
        }

        if (IsActive)
        {
            _timer = _clock.CreateTimer(_ => _ui.PostOrInline(Poll), state: null, PollEvery, PollEvery);
            Age();
            if (Url is { } url)
            {
                _ = Read(url, CancellationToken.None);
            }
        }
        else
        {
            _timer?.Dispose();
            _timer = null;
            _reads++;
        }
    }

    private void Poll()
    {
        if (IsActive && Url is { } url)
        {
            Age();
            _ = Read(url, CancellationToken.None);
        }
    }

    /// <summary>Words every row's "for" again from the clock now, while the page is active (the shell's one-second beat calls it).</summary>
    internal void Tick()
    {
        if (IsActive)
        {
            Age();
        }
    }

    // Time passes between reads: the ages are re-worded with the clock.
    private void Age()
    {
        foreach (var row in _rows.Values)
        {
            row.Age();
        }
    }
}
