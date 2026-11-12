using System.Globalization;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aw.Spike.T3335;

/// <summary>Candidate C: poll `capture-pane -p -e` (plus cursor/size in the same tmux call), draw the SGR runs, input through `send-keys`.</summary>
internal sealed class PaneC : Control, IPane
{
    private const string Mark = "@@STATE";
    private readonly bool _kick;
    private readonly int _pollMs;
    private readonly Channel<string[]> _sends = Channel.CreateUnbounded<string[]>();
    private readonly SemaphoreSlim _kickSignal = new(0, 1);
    private readonly Typeface _face = new("monospace");
    private const double FontSize = 14;
    private CancellationTokenSource? _cts;
    private string _session = "";
    private List<Run>[] _rows = [];
    private string _text = "";
    private int _cols = 100;
    private int _rowsN = 30;
    private int _cursorX;
    private int _cursorY;
    private int _history;
    private int _offset;
    private double _cw = 8;
    private double _ch = 17;
    private (int, int) _requested;

    public PaneC(bool kick)
    {
        _kick = kick;
        _pollMs = int.TryParse(Environment.GetEnvironmentVariable("AW_SPIKE_T3335_POLL_MS"), out var p) && p > 0 ? p : 50;
        Focusable = true;
        ClipToBounds = true;
        var m = Measure1("M");
        _cw = m.Width;
        _ch = m.Height;
    }

    string IPane.Name => _kick ? "c" : "c-nokick";
    public Control View => this;
    public (int Cols, int Rows) Grid => (_cols, _rowsN);
    public event Action? Updated;

    public Task StartAsync(string session)
    {
        _session = session;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => SendLoopAsync(_cts.Token), _cts.Token);
        _ = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
        Focus();
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask; // nothing attached: the session is untouched by construction
    }

    public void SendText(string text) => _sends.Writer.TryWrite(["send-keys", "-t", _session, "-l", "--", text]);

    public string VisibleText() => _text;

    public void ScrollToTop()
    {
        _offset = _history;
        Kick();
    }

    public void ScrollToBottom()
    {
        _offset = 0;
        Kick();
    }

    private void Kick()
    {
        if (_kickSignal.CurrentCount == 0)
        {
            try { _kickSignal.Release(); } catch (SemaphoreFullException) { }
        }
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var args in _sends.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await SpikeTmux.RunAsync(args).ConfigureAwait(false);
                if (_kick)
                {
                    Kick();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var off = _offset;
                var rowsN = _rowsN;
                var tail = $"{Mark} #{{cursor_x}} #{{cursor_y}} #{{history_size}} #{{pane_width}} #{{pane_height}}";
                var output = await SpikeTmux.RunAsync(
                    "capture-pane", "-t", _session, "-p", "-e", "-S", (-off).ToString(CultureInfo.InvariantCulture), "-E", (rowsN - 1 - off).ToString(CultureInfo.InvariantCulture),
                    ";", "display-message", "-t", _session, "-p", tail).ConfigureAwait(false);
                var cut = output.LastIndexOf(Mark, StringComparison.Ordinal);
                if (cut >= 0)
                {
                    var body = output[..cut];
                    var f = output[(cut + Mark.Length)..].Trim().Split(' ');
                    var cx = int.Parse(f[0], CultureInfo.InvariantCulture);
                    var cy = int.Parse(f[1], CultureInfo.InvariantCulture);
                    var hist = int.Parse(f[2], CultureInfo.InvariantCulture);
                    var rows = AnsiGrid.Parse(body.TrimEnd('\n'), rowsN);
                    var plain = StripToPlain(rows);
                    Dispatcher.UIThread.Post(() =>
                    {
                        _rows = rows;
                        _text = plain;
                        _cursorX = cx;
                        _cursorY = cy;
                        _history = hist;
                        InvalidateVisual();
                        Updated?.Invoke();
                    });
                }
                await Task.WhenAny(Task.Delay(_pollMs, ct), _kickSignal.WaitAsync(ct)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static string StripToPlain(List<Run>[] rows)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var row in rows)
        {
            var col = 0;
            foreach (var run in row)
            {
                sb.Append(' ', Math.Max(0, run.Col - col)).Append(run.Text);
                col = run.Col + run.Text.Length;
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private FormattedText Measure1(string s, IBrush? brush = null) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _face, FontSize, brush ?? Brushes.White);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var cols = Math.Max(20, (int)(e.NewSize.Width / _cw));
        var rows = Math.Max(5, (int)(e.NewSize.Height / _ch));
        if ((cols, rows) != _requested && _session.Length > 0)
        {
            _requested = (cols, rows);
            _cols = cols;
            _rowsN = rows;
            _sends.Writer.TryWrite(["resize-window", "-t", _session, "-x", cols.ToString(CultureInfo.InvariantCulture), "-y", rows.ToString(CultureInfo.InvariantCulture)]);
        }
        else if (_session.Length == 0)
        {
            _cols = cols;
            _rowsN = rows;
        }
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        for (var r = 0; r < _rows.Length; r++)
        {
            foreach (var run in _rows[r])
            {
                var fg = run.Fg == AnsiGrid.Default ? 0xFFDDDDDDu : run.Fg;
                var bg = run.Bg;
                if (run.Reverse)
                {
                    (fg, bg) = (bg == AnsiGrid.Default ? 0xFF000000u : bg, fg);
                }
                var x = run.Col * _cw;
                var y = r * _ch;
                if (bg != AnsiGrid.Default)
                {
                    ctx.FillRectangle(AnsiGrid.Brush(bg), new Rect(x, y, run.Text.Length * _cw, _ch));
                }
                var color = Color.FromUInt32(fg);
                if (run.Dim)
                {
                    color = Color.FromArgb(160, color.R, color.G, color.B);
                }
                var ft = Measure1(run.Text, new SolidColorBrush(color));
                if (run.Bold)
                {
                    ft.SetFontWeight(FontWeight.Bold);
                }
                if (run.Underline)
                {
                    ft.SetTextDecorations(TextDecorations.Underline);
                }
                ctx.DrawText(ft, new Point(x, y));
            }
        }
        if (_offset == 0)
        {
            ctx.DrawRectangle(null, new Pen(Brushes.LightGreen, 1), new Rect(_cursorX * _cw, _cursorY * _ch, _cw, _ch));
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _offset = Math.Clamp(_offset + (e.Delta.Y > 0 ? 3 : -3), 0, _history);
        Kick();
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!string.IsNullOrEmpty(e.Text))
        {
            SendText(e.Text);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var name = e.Key switch
        {
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Escape => "Escape",
            Key.Back => "BSpace",
            Key.Delete => "DC",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            _ => null,
        };
        if (name is null && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is >= Key.A and <= Key.Z)
        {
            name = "C-" + char.ToLowerInvariant((char)('A' + (e.Key - Key.A)));
        }
        if (name is not null)
        {
            _offset = 0;
            _sends.Writer.TryWrite(["send-keys", "-t", _session, name]);
            e.Handled = true;
        }
    }
}
