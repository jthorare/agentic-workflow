using System.Text;
using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ICodexRollouts: the one fake, kept honest by CodexRolloutsContract.</summary>
public sealed class InMemoryCodexRollouts : ICodexRollouts
{
    private readonly Dictionary<string, (string Content, DateTimeOffset At)> _files = new(StringComparer.Ordinal);

    /// <summary>Sets a session's rollout.</summary>
    /// <param name="session">The session id.</param>
    /// <param name="content">The whole text.</param>
    /// <param name="at">When it was last written.</param>
    public void Put(string session, string content, DateTimeOffset at) => _files[session] = (content, at);

    /// <inheritdoc />
    public ValueTask<Result<TranscriptTail?>> Tail(SessionId session, int maxBytes, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(session.Value, out var file))
        {
            return Done.Of<TranscriptTail?>(value: null);
        }

        var bytes = Encoding.UTF8.GetBytes(file.Content);
        var start = Math.Max(0, bytes.Length - maxBytes);
        var lines = Encoding.UTF8.GetString(bytes, start, bytes.Length - start).Split('\n').Select(static l => l.TrimEnd('\r')).ToList();
        if (start > 0 && lines.Count > 0)
        {
            lines.RemoveAt(0);
        }

        return Done.Of<TranscriptTail?>(new TranscriptTail($"/codex/sessions/2026/11/07/rollout-x-{session.Value}.jsonl", file.At, [.. lines.Where(static l => l.Length > 0)]));
    }
}
