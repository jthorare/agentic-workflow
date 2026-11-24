using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// <see cref="ICommandCredentials"/> over <see cref="IToolRunner"/>: `command:gh auth token` runs gh with the arguments split on spaces, no shell, as `ForgeCredentialSpec` does. The printed token is returned and never logged.
/// </summary>
/// <param name="runner">Runs the program.</param>
internal sealed class CommandCredentials(IToolRunner runner) : ICommandCredentials
{
    private const string Scheme = "command:";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public string? Token(string credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (!credential.StartsWith(Scheme, StringComparison.Ordinal))
        {
            return null;
        }

        var argv = credential[Scheme.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (argv.Length == 0)
        {
            return null;
        }

        var run = runner.Run(argv[0], argv[1..], _timeout);
        return run is { Outcome: ToolOutcome.Exited, ExitCode: 0 } && run.Output.Trim() is { Length: > 0 } token ? token : null;
    }
}
