using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.ModelCheck;

/// <summary>
/// Doctor's 'model:check' line (model-check design §4.3): whether the model check can run, read from the installed <c language="text">tlc.sh doctor</c> with the environment the step gives TLC, so the
/// PATH is the one the step will see. A missing runner, java or jar is a warning with the fix, never a failure: the step stops for the owner until it is fixed.
/// </summary>
/// <param name="setup">Where the runner is and the environment it gets.</param>
/// <param name="runner">Runs tlc.sh.</param>
internal sealed class ModelCheckDoctorChecks(ModelCheckSetup setup, IToolRunner runner) : IDoctorChecks
{
    internal const string Name = "model:check";

    private const string Stops = "the model check stops for you until it is fixed";
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(20);

    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if (setup.Runner is null || !File.Exists(setup.TlcSh))
        {
            return Done(NotInstalled());
        }

        var run = runner.Run("env", ["-i", $"PATH={setup.Path}", $"HOME={setup.Home}", $"AW_STATE_DIR={setup.StateDir}", setup.TlcSh, "doctor"], _budget, cancellationToken: cancellationToken);
        var lines = (run.Output + run.Error).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = lines.FirstOrDefault() ?? "tlc.sh doctor printed nothing";
        if (run is { Outcome: ToolOutcome.Exited, ExitCode: 0 })
        {
            return Done(new DoctorCheck(Name, CheckState.Ok, first.StartsWith("tlc: ", StringComparison.Ordinal) ? first[5..] : first, Fix: null));
        }

        var fixLine = lines.FirstOrDefault(static l => l.Contains("fix:", StringComparison.Ordinal));
        return Done(new DoctorCheck(Name, CheckState.Warn, $"{Problem(fixLine ?? first)}; {Stops}", FixOf(fixLine) ?? "run the installed tlc.sh doctor"));
    }

    private static DoctorCheck NotInstalled() => new(Name, CheckState.Warn, $"the TLC runner isn't installed next to sindri; {Stops}", "run ./setup.sh from the agentic-workflow checkout (it installs the runner and java)");

    // tlc.sh writes 'java isn't installed (fix: ...)': the words before the fix, and the fix on its own.
    private static string Problem(string line)
    {
        var plain = line.StartsWith("tlc: ", StringComparison.Ordinal) ? line[5..] : line;
        var at = plain.IndexOf("(fix:", StringComparison.Ordinal);
        return (at > 0 ? plain[..at] : plain).Trim(' ', ':', ';');
    }

    private static string? FixOf(string? line) => line?[(line.IndexOf("fix:", StringComparison.Ordinal) + 4)..].Trim(' ', ')');

    private static ValueTask<IReadOnlyList<DoctorCheck>> Done(DoctorCheck check) => ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([check]);
}
