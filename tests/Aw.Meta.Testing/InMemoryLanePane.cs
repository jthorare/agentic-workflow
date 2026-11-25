using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILanePane: sessions are scripted per test and the wake lines are kept.</summary>
public sealed class InMemoryLanePane : ILanePane
{
    private readonly Dictionary<string, PaneState> _panes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _screens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pids = new(StringComparer.Ordinal);
    private readonly List<(string Session, string Line)> _woken = [];

    /// <summary>Every (session, line) typed, in order.</summary>
    public IReadOnlyList<(string Session, string Line)> Woken => _woken;

    /// <summary>Makes <see cref="Capture"/> return this screen for the session; text typed and not yet submitted shows in the last prompt line, as in the real input box.</summary>
    /// <param name="session">The session name.</param>
    /// <param name="screen">What the pane shows.</param>
    public void Show(string session, string screen) => _screens[session] = screen;

    /// <summary>When set, every wake fails with this message and types nothing.</summary>
    public string? WakeFailure { get; set; }

    /// <summary>Makes a session exist.</summary>
    /// <param name="session">The session name.</param>
    /// <param name="command">The command running in its pane.</param>
    /// <param name="clients">How many clients are attached.</param>
    public void Put(string session, string command, int clients = 0) => _panes[session] = new PaneState(command, clients);

    /// <summary>How many Enter presses are swallowed (the text stays in the input box) before one submits.</summary>
    public int SwallowEnters { get; set; }

    /// <summary>How many leading characters the next typed text loses before it reaches the input box, as when a lane's prompt reads them as keys (#3443).</summary>
    public int EatNextTyped { get; set; }

    /// <summary>When set, <see cref="ClearInput"/> leaves the input box as it is, as when the agent ignores Ctrl+U.</summary>
    public bool IgnoreClear { get; set; }

    /// <summary>How many times the input box was cleared.</summary>
    public int Clears { get; private set; }

    /// <summary>The text typed into the session's input box and not yet submitted or cleared.</summary>
    /// <param name="session">The session name.</param>
    /// <returns>The text, empty when the box is empty.</returns>
    public string InputBox(string session) => _pending.GetValueOrDefault(session, string.Empty);

    /// <summary>How many Enter presses were made.</summary>
    public int Enters { get; private set; }

    /// <summary>Sets the pid of the process a session's pane started.</summary>
    /// <param name="session">The session name.</param>
    /// <param name="pid">The pid.</param>
    public void SetPid(string session, int pid) => _pids[session] = pid;

    /// <summary>Ends a session.</summary>
    /// <param name="session">The session name.</param>
    public void Remove(string session) => _ = _panes.Remove(session);

    /// <inheritdoc />
    public ValueTask<Result<PaneState?>> Probe(string session, CancellationToken cancellationToken) =>
        Done.Of(_panes.GetValueOrDefault(session));

    /// <inheritdoc />
    public ValueTask<Result<int?>> PanePid(string session, CancellationToken cancellationToken) =>
        Done.Of<int?>(_panes.ContainsKey(session) && _pids.TryGetValue(session, out var pid) ? pid : null);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Type(string session, string text, CancellationToken cancellationToken)
    {
        if (WakeFailure is not null || !_panes.ContainsKey(session))
        {
            return Done.Failed<Unit>(MetaErrors.CommandFailed.With(WakeFailure ?? $"no such session: {session}"));
        }

        var eaten = Math.Min(EatNextTyped, text.Length);
        EatNextTyped = 0;
        _pending[session] = _pending.GetValueOrDefault(session, string.Empty) + text[eaten..];
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> PressEnter(string session, CancellationToken cancellationToken)
    {
        if (!_panes.ContainsKey(session))
        {
            return Done.Failed<Unit>(MetaErrors.CommandFailed.With($"no such session: {session}"));
        }

        Enters++;
        if (SwallowEnters > 0)
        {
            SwallowEnters--;
        }
        else if (_pending.Remove(session, out var text))
        {
            _woken.Add((session, text));
        }

        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> ClearInput(string session, CancellationToken cancellationToken)
    {
        if (!_panes.ContainsKey(session))
        {
            return Done.Failed<Unit>(MetaErrors.CommandFailed.With($"no such session: {session}"));
        }

        Clears++;
        if (!IgnoreClear)
        {
            _ = _pending.Remove(session);
        }

        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> Capture(string session, CancellationToken cancellationToken) =>
        !_panes.ContainsKey(session) ? Done.Failed<string>(MetaErrors.CommandFailed.With($"no such session: {session}"))
        : _screens.TryGetValue(session, out var screen) ? Done.Of(WithTyped(screen, _pending.GetValueOrDefault(session, string.Empty)))
        : Done.Of("some output\n" + new string('─', 20) + "\n❯ " + _pending.GetValueOrDefault(session, string.Empty) + "\n" + new string('─', 20) + "\n");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Wake(string session, string line, CancellationToken cancellationToken)
    {
        if (WakeFailure is not null || !_panes.ContainsKey(session))
        {
            var reason = WakeFailure ?? $"no such session: {session}";
            return Done.Failed<Unit>(MetaErrors.CommandFailed.With(reason));
        }

        _woken.Add((session, line));
        return Done.Unit();
    }

    private static string WithTyped(string screen, string typed)
    {
        if (typed.Length == 0)
        {
            return screen;
        }

        var lines = screen.Split('\n');
        var at = Array.FindLastIndex(lines, static l => l.StartsWith('❯'));
        if (at >= 0)
        {
            lines[at] = "❯ " + typed;
        }

        return string.Join('\n', lines);
    }
}
