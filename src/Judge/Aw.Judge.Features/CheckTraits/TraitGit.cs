using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>git through the process runner. A run that didn't start or ran too long is exit code -1 with no output.</summary>
/// <param name="runner">Runs the programs.</param>
internal sealed class TraitGit(IProcessRunner runner)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    // A branch's whole diff can be large; past this the rest is dropped.
    private const int OutputLimit = 16 << 20;

    // 'git diff --no-index' exits 1 when the files differ, which is its success case, so any exit code is an answer.
    internal async ValueTask<(int Code, string Output)> Run(IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var result = await runner.Run(
            new ProcessRequest { FileName = "git", Arguments = arguments, WorkingDirectory = workingDirectory, Timeout = _timeout, OutputLimit = OutputLimit },
            cancellationToken).ConfigureAwait(false);
        return result is { End: ProcessEnd.Exited, ExitCode: { } code } ? (code, result.StandardOutput) : (-1, "");
    }
}
