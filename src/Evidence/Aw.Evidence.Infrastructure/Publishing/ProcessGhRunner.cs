using System.Collections;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Publishing;

/// <summary>
/// gh (found on PATH unless a path is given) through the package's process runner, with a time limit. gh finds its login in the inherited environment; GH_REPO is dropped (the
/// repository is always in the call's path) and prompts are off.
/// </summary>
internal sealed class ProcessGhRunner(string executable = "gh") : IGhRunner
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    /// <inheritdoc/>
    public async ValueTask<Result<GhOutcome>> RunAsync(GhCall call, CancellationToken cancellationToken)
    {
        var spec = new ProcessSpec([executable, .. call.Args], call.Stdin, Environment(), WorkingDirectory: null);
        var outcome = await ProcessRunner.RunAsync(spec, _timeout, EvidenceErrors.RunFailed, cancellationToken).ConfigureAwait(false);
        return outcome.Map(static o => new GhOutcome(o.ExitCode, o.StandardOutput, o.StandardError));
    }

    private static Dictionary<string, string> Environment()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value && name != "GH_REPO")
            {
                environment[name] = value;
            }
        }

        environment["GH_PROMPT_DISABLED"] = "1";
        return environment;
    }
}
