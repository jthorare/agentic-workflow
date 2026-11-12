using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Aw.Spike.T3335;

/// <summary>AW_SPIKE_T3335=1: run the measurements in the open window (needs a display; xvfb is fine), write measure.json, exit.
/// Env: AW_SPIKE_T3335_OUT (dir), AW_SPIKE_T3335_MODES (default a,c,c-nokick), AW_SPIKE_T3335_KEYS (default 60, at least 50),
/// AW_SPIKE_T3335_AOT_WARNINGS (echoed into the json when the publish script passes it).</summary>
internal static class Measure
{
    private const string KeyChars = "QWERTYUIOPASDFGHJKLZXCVBNM";

    public static async Task RunAndExitAsync(MainWindow window, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var code = 0;
        var outDir = Environment.GetEnvironmentVariable("AW_SPIKE_T3335_OUT") ?? Path.Combine(AppContext.BaseDirectory, "measure-out");
        Directory.CreateDirectory(outDir);
        try
        {
            await Settle();
            var modes = (Environment.GetEnvironmentVariable("AW_SPIKE_T3335_MODES") ?? "a,c,c-nokick").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var keys = Math.Max(50, int.TryParse(Environment.GetEnvironmentVariable("AW_SPIKE_T3335_KEYS"), out var k) ? k : 60);
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                await Header(w);
                w.WriteStartArray("modes");
                foreach (var mode in modes)
                {
                    await RunMode(window, w, mode, keys);
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            await File.WriteAllBytesAsync(Path.Combine(outDir, "measure.json"), stream.ToArray());
        }
        catch (Exception ex)
        {
            code = 1;
            await File.WriteAllTextAsync(Path.Combine(outDir, "measure-error.txt"), ex.ToString());
        }
        finally
        {
            SpikeTmux.KillServer();
        }
        lifetime.Shutdown(code);
    }

    private static async Task Header(Utf8JsonWriter w)
    {
        var exe = Environment.ProcessPath;
        w.WriteString("tmux_version", (await SpikeTmux.RunAsync("-V")).Trim());
        w.WriteString("os", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        w.WriteString("devolutions_control_assembly", typeof(Devolutions.Terminal.TermControl).Assembly.GetName().Version?.ToString());
        if (exe is not null)
        {
            w.WriteNumber("binary_bytes", new FileInfo(exe).Length);
            w.WriteNumber("publish_dir_bytes", Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories).Sum(static f => new FileInfo(f).Length));
        }
        var aot = Environment.GetEnvironmentVariable("AW_SPIKE_T3335_AOT_WARNINGS");
        if (aot is not null)
        {
            w.WriteString("aot_warnings", aot);
        }
    }

    private static async Task Settle()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private static async Task RunMode(MainWindow window, Utf8JsonWriter w, string mode, int keys)
    {
        w.WriteStartObject();
        w.WriteString("mode", mode);
        try
        {
            window.Width = 1000;
            window.Height = 640;
            await SpikeTmux.KillSessionAsync(MainWindow.Session);
            var sw = Stopwatch.StartNew();
            var pane = await window.ShowPaneAsync(mode);
            var prompt = await WaitFor(pane, static t => t.Contains('$'), 8000);
            w.WriteBoolean("prompt_seen", prompt);
            w.WriteNumber("startup_to_prompt_ms", Math.Round(sw.Elapsed.TotalMilliseconds, 1));
            w.WriteNumber("grid_cols", pane.Grid.Cols);
            w.WriteNumber("grid_rows", pane.Grid.Rows);

            await Latency(pane, w, keys);
            await Scroll(window, pane, w);
            await Resize(window, pane, w);
            await Detach(pane, w);
        }
        catch (Exception ex)
        {
            w.WriteString("error", ex.Message);
        }
        w.WriteEndObject();
    }

    private static async Task Latency(IPane pane, Utf8JsonWriter w, int keys)
    {
        pane.SendText("cat\r");
        await Task.Delay(400);
        var samples = new List<double>();
        var timeouts = 0;
        var acc = "";
        for (var i = 0; i < keys; i++)
        {
            var c = KeyChars[i % KeyChars.Length];
            acc += c;
            var ms = await TypeAndWait(pane, c, acc, 3000);
            if (double.IsNaN(ms))
            {
                timeouts++;
            }
            else
            {
                samples.Add(ms);
            }
            await Task.Delay(30);
        }
        samples.Sort();
        w.WriteStartObject("keystroke_to_paint_ms");
        w.WriteNumber("keys", keys);
        w.WriteNumber("samples", samples.Count);
        w.WriteNumber("timeouts", timeouts);
        if (samples.Count > 0)
        {
            w.WriteNumber("p50", Math.Round(Pct(samples, 0.50), 1));
            w.WriteNumber("p95", Math.Round(Pct(samples, 0.95), 1));
            w.WriteNumber("min", Math.Round(samples[0], 1));
            w.WriteNumber("max", Math.Round(samples[^1], 1));
        }
        w.WriteEndObject();
        pane.SendText("\u0003");
        await Task.Delay(200);
    }

    private static async Task<double> TypeAndWait(IPane pane, char c, string expected, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sw = Stopwatch.StartNew();
        var frameRequested = false;
        void Check()
        {
            if (frameRequested || !pane.VisibleText().Contains(expected, StringComparison.Ordinal))
            {
                return;
            }
            frameRequested = true;
            var top = Avalonia.Controls.TopLevel.GetTopLevel(pane.View);
            if (top is null)
            {
                tcs.TrySetResult(sw.Elapsed.TotalMilliseconds);
                return;
            }
            top.RequestAnimationFrame(_ => tcs.TrySetResult(sw.Elapsed.TotalMilliseconds));
        }
        void Handler() => Check();
        pane.Updated += Handler;
        try
        {
            sw.Restart();
            pane.SendText(c.ToString());
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            return done == tcs.Task ? tcs.Task.Result : double.NaN;
        }
        finally
        {
            pane.Updated -= Handler;
        }
    }

    private static async Task Scroll(MainWindow window, IPane pane, Utf8JsonWriter w)
    {
        _ = window;
        pane.SendText("clear; seq 1 500\r");
        var drawn = await WaitFor(pane, static t => HasLine(t, "500") || HasLine(t, "499"), 5000);
        w.WriteStartObject("scroll");
        w.WriteBoolean("output_drawn", drawn);
        pane.ScrollToTop();
        var top = await WaitFor(pane, static t => HasLine(t, "1"), 2500);
        w.WriteBoolean("control_scroll_reaches_line_1", top);
        pane.ScrollToBottom();
        await Task.Delay(300);
        if (pane.Name == "a")
        {
            // The attached terminal only holds what tmux repaints; tmux's own history is reached through copy-mode.
            await SpikeTmux.RunAsync("copy-mode", "-t", MainWindow.Session);
            await SpikeTmux.RunAsync("send-keys", "-t", MainWindow.Session, "-X", "history-top");
            var viaTmux = await WaitFor(pane, static t => HasLine(t, "1"), 2500);
            w.WriteBoolean("tmux_copy_mode_reaches_line_1", viaTmux);
            await SpikeTmux.RunAsync("send-keys", "-t", MainWindow.Session, "-X", "cancel");
        }
        w.WriteEndObject();
        await Task.Delay(200);
    }

    private static async Task Resize(MainWindow window, IPane pane, Utf8JsonWriter w)
    {
        var before = pane.Grid;
        window.Width = 760;
        window.Height = 480;
        var ok = false;
        (int Cols, int Rows) after = before;
        string reported = "";
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 4000)
        {
            await Task.Delay(60);
            after = pane.Grid;
            reported = (await SpikeTmux.RunAsync("display-message", "-t", MainWindow.Session, "-p", "#{window_width}x#{window_height}")).Trim();
            if (after != before && reported == $"{after.Cols}x{after.Rows}")
            {
                ok = true;
                break;
            }
        }
        w.WriteStartObject("resize");
        w.WriteString("grid_before", $"{before.Cols}x{before.Rows}");
        w.WriteString("grid_after", $"{after.Cols}x{after.Rows}");
        w.WriteString("tmux_window_size", reported);
        w.WriteNumber("settled_ms", sw.ElapsedMilliseconds);
        w.WriteBoolean("ok", ok);
        w.WriteEndObject();
    }

    private static async Task Detach(IPane pane, Utf8JsonWriter w)
    {
        await pane.CloseAsync();
        await Task.Delay(600);
        var kept = await SpikeTmux.HasSessionAsync(MainWindow.Session);
        await SpikeTmux.KillSessionAsync(MainWindow.Session);
        await Task.Delay(200);
        var gone = !await SpikeTmux.HasSessionAsync(MainWindow.Session);
        w.WriteBoolean("detach_keeps_session", kept);
        w.WriteBoolean("kill_removes_session", gone);
    }

    private static bool HasLine(string text, string line)
    {
        foreach (var l in text.Split('\n'))
        {
            if (l.Trim() == line)
            {
                return true;
            }
        }
        return false;
    }

    private static async Task<bool> WaitFor(IPane pane, Func<string, bool> cond, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler()
        {
            if (cond(pane.VisibleText()))
            {
                tcs.TrySetResult(true);
            }
        }
        pane.Updated += Handler;
        try
        {
            if (cond(pane.VisibleText()))
            {
                return true;
            }
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            return done == tcs.Task;
        }
        finally
        {
            pane.Updated -= Handler;
        }
    }

    private static double Pct(List<double> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];
}
