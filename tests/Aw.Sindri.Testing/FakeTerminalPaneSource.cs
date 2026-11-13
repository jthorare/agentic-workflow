using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Testing;

/// <summary>
/// The pane source that plays a script of frames (agents spec §15.8): the first frame at once, the next one right after each key, text or paste, none
/// on its own. <see cref="FromFixture"/> plays 'tests/fixtures/terminal-ws/claude-session.jsonl', recorded from a real Claude Code session.
/// </summary>
/// <param name="host">The pane the streams report.</param>
/// <param name="script">The frames, in order.</param>
public sealed class FakeTerminalPaneSource(TerminalHost host, IEnumerable<TerminalFrame> script) : ITerminalPaneSource
{
    private readonly List<TerminalFrame> _script = [.. script];

    /// <summary>The pane the streams report.</summary>
    public TerminalHost Host { get; } = host;

    /// <summary>When set, <see cref="Open"/> answers this error (SND-AGENT-015 or 016).</summary>
    public Error? OpenFails { get; set; }

    /// <summary>
    /// When true, typed text (not keys, not paste) is echoed like a tty does: the next frame is the last one with the text added to its first row, instead of the next
    /// scripted frame. Lets the contract check that typed text arrives.
    /// </summary>
    public bool EchoText { get; set; }

    /// <summary>When true, a stream's frame pump throws <see cref="OperationCanceledException"/> after its first frame without any cancel: a slow capture that hit the source's call budget.</summary>
    public bool CaptureTimesOut { get; set; }

    /// <summary>When true, typing text or a key throws <see cref="OperationCanceledException"/> without any cancel: a slow send that hit the source's call budget.</summary>
    public bool SendTimesOut { get; set; }

    /// <summary>What the streams were sent, in order: 'key:Enter', 'text:hi', 'paste:…', 'size:120x36'.</summary>
    public IList<string> Sent { get; } = [];

    /// <summary>The number of streams disposed (a dispose detaches only).</summary>
    public int Detached { get; private set; }

    /// <summary>A source over the frames of the recorded Claude Code session.</summary>
    /// <returns>The source.</returns>
    public static FakeTerminalPaneSource FromFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "terminal-ws", "claude-session.jsonl");
        var frames = new List<TerminalFrame>();
        string session = "fx", socket = "sindri-launch", pane = "%0";
        foreach (var line in File.ReadLines(path))
        {
            using var doc = JsonDocument.Parse(line);
            var msg = doc.RootElement.GetProperty("msg");
            switch (msg.GetProperty("t").GetString())
            {
                case "ready":
                    var h = msg.GetProperty("host");
                    (socket, session, pane) = (h.GetProperty("socket").GetString()!, h.GetProperty("session").GetString()!, h.GetProperty("pane").GetString()!);
                    break;
                case "frame":
                    var cursor = msg.GetProperty("cursor");
                    frames.Add(new TerminalFrame(
                        msg.GetProperty("seq").GetInt64(),
                        [.. msg.GetProperty("rows").EnumerateArray().Select(r => r.GetString()!)],
                        cursor.GetProperty("x").GetInt32(), cursor.GetProperty("y").GetInt32(),
                        msg.GetProperty("w").GetInt32(), msg.GetProperty("h").GetInt32(),
                        msg.GetProperty("alt").GetInt32() == 1, msg.GetProperty("inMode").GetInt32() == 1, msg.GetProperty("dead").GetInt32() == 1));
                    break;
                default:
                    break;
            }
        }

        return new FakeTerminalPaneSource(new TerminalHost(socket, session, pane), frames);
    }

    /// <inheritdoc/>
    public ValueTask<Result<IOpenTerminalPane>> Open(SessionRef session, HostSize? size, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IOpenTerminalPane>>(OpenFails is { } error ? error : new Stream(this));

    private sealed class Stream : IOpenTerminalPane
    {
        private readonly FakeTerminalPaneSource _owner;
        private readonly Channel<TerminalFrame> _frames = Channel.CreateUnbounded<TerminalFrame>();
        private int _next;
        private TerminalFrame? _last;
        private string _typed = string.Empty;

        public Stream(FakeTerminalPaneSource owner)
        {
            _owner = owner;
            Capture();
        }

        public TerminalHost Host => _owner.Host;

        public async IAsyncEnumerable<TerminalFrame> Frames([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _frames.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_frames.Reader.TryRead(out var frame))
                {
                    yield return frame;
                    if (_owner.CaptureTimesOut)
                    {
                        throw new OperationCanceledException("the capture hit its call budget");
                    }

                    if (frame.Dead)
                    {
                        yield break;
                    }
                }
            }
        }

        public ValueTask<Result<Unit>> SendKey(string key, CancellationToken cancellationToken)
        {
            if (_owner.SendTimesOut)
            {
                throw new OperationCanceledException("the send hit its call budget");
            }

            var checkedKey = TerminalKeys.Check(key);
            if (checkedKey.IsError)
            {
                return ValueTask.FromResult(checkedKey);
            }

            return Sent($"key:{key}");
        }

        public ValueTask<Result<Unit>> SendText(string text, CancellationToken cancellationToken)
        {
            if (_owner.SendTimesOut)
            {
                throw new OperationCanceledException("the send hit its call budget");
            }

            if (text.Length > 4096)
            {
                return Refuse("text is over 4 KiB");
            }

            if (!_owner.EchoText || _last is null)
            {
                return Sent($"text:{text}");
            }

            _typed += text;
            _owner.Sent.Add($"text:{text}");
            var rows = _last.Rows.ToList();
            rows[0] += _typed;
            _last = _last with { Seq = _last.Seq + 1, Rows = rows };
            _ = _frames.Writer.TryWrite(_last);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        public ValueTask<Result<Unit>> Paste(string text, CancellationToken cancellationToken) =>
            text.Length > 256 * 1024 ? Refuse("paste is over 256 KiB") : Sent($"paste:{text}");

        public ValueTask<Result<Unit>> Resize(HostSize size, CancellationToken cancellationToken)
        {
            if (size.Cols is < 20 or > 400 || size.Rows is < 5 or > 200)
            {
                return Refuse("size is out of range");
            }

            _owner.Sent.Add(string.Create(CultureInfo.InvariantCulture, $"size:{size.Cols}x{size.Rows}"));
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        public ValueTask DisposeAsync()
        {
            _owner.Detached++;
            _ = _frames.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        private static ValueTask<Result<Unit>> Refuse(string why) =>
            ValueTask.FromResult<Result<Unit>>(SindriErrors.InvalidInput.With(why));

        private ValueTask<Result<Unit>> Sent(string what)
        {
            _owner.Sent.Add(what);
            Capture();
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        private void Capture()
        {
            if (_next < _owner._script.Count)
            {
                _last = _owner._script[_next++];
                _ = _frames.Writer.TryWrite(_last);
            }
        }
    }
}
