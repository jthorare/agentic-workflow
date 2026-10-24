using System.Globalization;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Writes the UI driver's command lines (aw-ui-driver) for a desktop run: the driver's program and leading arguments, '--dir &lt;driver directory&gt;', the verb and its flags.</summary>
public static class DriverCommandLine
{
    /// <summary>How long 'ready' waits for the app's driver to answer, in milliseconds.</summary>
    public const int ReadyWaitMs = 30_000;

    /// <summary>The command that waits until the app's driver answers.</summary>
    /// <param name="driver">The driver's program and leading arguments.</param>
    /// <param name="directory">The driver directory the app was started with.</param>
    /// <returns>The command line.</returns>
    public static IReadOnlyList<string> Ready(IReadOnlyList<string> driver, string directory) =>
        [.. driver, "--dir", directory, "ready", "--wait-ms", ReadyWaitMs.ToString(CultureInfo.InvariantCulture)];

    /// <summary>The command that sets the window to the size of the run.</summary>
    /// <param name="driver">The driver's program and leading arguments.</param>
    /// <param name="directory">The driver directory.</param>
    /// <param name="size">The window size.</param>
    /// <returns>The command line.</returns>
    public static IReadOnlyList<string> Window(IReadOnlyList<string> driver, string directory, ViewportSize size) =>
        [.. driver, "--dir", directory, "window", "--width", Text(size.W), "--height", Text(size.H)];

    /// <summary>The commands one step runs, in order: its own verb, then the wait for its expected state (a wait-for step is that wait).</summary>
    /// <param name="driver">The driver's program and leading arguments.</param>
    /// <param name="directory">The driver directory.</param>
    /// <param name="step">The step.</param>
    /// <param name="framePath">Where a screenshot step saves the window (absolute).</param>
    /// <returns>The command lines; none for launch.</returns>
    public static IReadOnlyList<IReadOnlyList<string>> Step(IReadOnlyList<string> driver, string directory, DesktopStep step, string framePath)
    {
        string[] head = [.. driver, "--dir", directory];
        var selector = Selector(step.Target);
        var own = Own(step, selector, framePath);
        List<IReadOnlyList<string>> lines = [];
        if (own.Length > 0)
        {
            lines.Add([.. head, .. own]);
        }

        if (step.Action is DesktopAction.Click or DesktopAction.Type or DesktopAction.Key && step.ExpectedState is not null)
        {
            lines.Add([.. head, .. Wait(selector, step)]);
        }

        return lines;
    }

    private static string[] Own(DesktopStep step, string[] selector, string framePath) => step.Action switch
    {
        DesktopAction.Click => ["click", .. selector],
        DesktopAction.Type => ["type", .. selector, "--value", step.Value ?? string.Empty, .. Replace(step)],
        DesktopAction.Key => ["key", .. selector, "--chord", step.Value ?? string.Empty],
        DesktopAction.Screenshot => ["screenshot", .. selector, "--out", framePath],
        DesktopAction.WaitFor => Wait(selector, step),
        DesktopAction.Launch => [],
        _ => [],
    };

    private static string[] Replace(DesktopStep step) => step.Replace == true ? ["--replace"] : [];

    private static string[] Wait(string[] selector, DesktopStep step)
    {
        var expected = step.ExpectedState;
        List<string> flags = ["wait-for", .. selector, "--state", expected?.State ?? "visible"];
        if (expected?.Text is { } text)
        {
            flags.AddRange(["--expect-text", text]);
        }

        if (step.TimeoutMs is { } timeout)
        {
            flags.AddRange(["--wait-ms", Text(timeout)]);
        }

        return [.. flags];
    }

    private static string[] Selector(DesktopTarget? target)
    {
        if (target is null)
        {
            return [];
        }

        List<string> flags = [];
        (string Flag, string? Value)[] named =
        [
            ("--automation-id", target.Automation), ("--name", target.Name), ("--type", target.Type), ("--text", target.Text), ("--contains", target.Contains),
            ("--index", target.Index is { } index ? Text(index) : null),
        ];
        foreach (var (flag, value) in named)
        {
            if (value is not null)
            {
                flags.AddRange([flag, value]);
            }
        }

        return [.. flags];
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
}
