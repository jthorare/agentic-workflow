using System.Globalization;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>Runs the build and the tests of a toolkit as real child processes, bounded in time.</summary>
/// <param name="runner">Runs one process.</param>
internal sealed class ProcessToolkitCommands(IToolRunner runner) : IToolkitCommands
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(30);

    public ValueTask<ToolkitCommandResult> Run(string workingDir, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argv);
        var ran = runner.Run(argv[0], [.. argv.Skip(1)], _timeout, new ToolRunOptions(WorkingDirectory: workingDir), cancellationToken);
        var (output, error) = ran.Outcome switch
        {
            ToolOutcome.Missing => ($"{argv[0]} is not installed", string.Empty),
            ToolOutcome.TimedOut => ($"{argv[0]} ran past {_timeout.TotalMinutes.ToString("0", CultureInfo.InvariantCulture)} minutes", string.Empty),
            ToolOutcome.Exited or ToolOutcome.Cancelled => (ran.Output, ran.Error),
            _ => (ran.Output, ran.Error),
        };
        return ValueTask.FromResult(new ToolkitCommandResult(ran.Outcome == ToolOutcome.Exited ? ran.ExitCode : -1, output, error));
    }
}
