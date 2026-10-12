using Aw.Kernel;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>
/// The launcher with a scripted child: by default it starts one that keeps running; <see cref="ExitingWith"/> scripts one
/// that wrote its output to its log and exited. It counts launches, and <see cref="OnLaunch"/> runs on each.
/// </summary>
public sealed class FakeControlPlaneLauncher : IControlPlaneLauncher
{
    private string? _output;

    /// <summary>How many times Launch was called.</summary>
    public int Launches { get; private set; }

    /// <summary>The last child it launched.</summary>
    public FakeLaunchedProcess? Child { get; private set; }

    /// <summary>Runs on each launch (a test makes the probe answer, as a concurrent start would).</summary>
    public Action? OnLaunch { get; set; }

    /// <summary>A launcher whose child wrote <paramref name="output"/> to its log and exited.</summary>
    /// <param name="output">The child's log text.</param>
    /// <returns>The launcher.</returns>
    public static FakeControlPlaneLauncher ExitingWith(string output) => new() { _output = output };

    /// <inheritdoc/>
    public Result<ILaunchedProcess> Launch()
    {
        Launches++;
        var lastError = _output?.Split('\n').LastOrDefault(l => l.StartsWith("SND-", StringComparison.Ordinal)) ?? "no error line in the log";
        Child = new FakeLaunchedProcess(40_000 + Launches, lastError) { HasExited = _output is not null };
        OnLaunch?.Invoke();
        return Child;
    }
}
