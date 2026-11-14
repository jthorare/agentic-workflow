using System.Runtime.CompilerServices;
using System.Text.Json;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory terminal connector: replays the "out" lines of A8's fixture (tests/fixtures/terminal-ws/claude-session.jsonl) or a script, records what was sent, and can refuse or close with a code.</summary>
internal sealed class FakeTerminalConnector : ITerminalConnector
{
    private readonly List<string> _sent = [];
    private readonly List<string> _connected = [];

    /// <summary>The server messages to play, in order; defaults to the fixture's.</summary>
    public IReadOnlyList<string>? Script { get; set; }

    /// <summary>The close code the link ends with after the script; null for 1001.</summary>
    public int? EndsWith { get; set; }

    /// <summary>When true, Connect answers null (the socket could not open).</summary>
    public bool Refuse { get; set; }

    /// <summary>Every client message sent through any link, in order.</summary>
    public IReadOnlyList<string> Sent => _sent;

    /// <summary>Every session connected, provider/id.</summary>
    public IReadOnlyList<string> Connected => _connected;

    /// <summary>The "out" messages of the fixture, as JSON text.</summary>
    public static IReadOnlyList<string> FixtureOut()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "terminal-ws", "claude-session.jsonl");
        var lines = new List<string>();
        foreach (var line in File.ReadLines(path))
        {
            using var doc = JsonDocument.Parse(line);
            if (string.Equals(doc.RootElement.GetProperty("dir").GetString(), "out", StringComparison.Ordinal))
            {
                lines.Add(doc.RootElement.GetProperty("msg").GetRawText());
            }
        }

        return lines;
    }

    public Task<ITerminalLink?> Connect(Uri api, string provider, string id, int cols, int rows, CancellationToken cancellationToken)
    {
        if (Refuse)
        {
            return Task.FromResult<ITerminalLink?>(null);
        }

        _connected.Add(provider + "/" + id);
        return Task.FromResult<ITerminalLink?>(new Link(this));
    }

    private sealed class Link(FakeTerminalConnector owner) : ITerminalLink
    {
        public int? CloseCode { get; private set; }

        public async IAsyncEnumerable<TerminalServerMessage> Read([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var json in owner.Script ?? FixtureOut())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return TerminalSocketMessages.Parse(json);
                await Task.Yield();
            }

            CloseCode = owner.EndsWith ?? 1001;
        }

        public Task<bool> Send(string json, CancellationToken cancellationToken)
        {
            owner._sent.Add(json);
            return Task.FromResult(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
