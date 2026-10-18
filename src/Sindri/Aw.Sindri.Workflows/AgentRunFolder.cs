using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Makes an agent run's folder and puts the Step's declared inputs in it before the launch (sandbox spec SB17). The folder is outside the repo, private to the owner, and keyed per run.</summary>
/// <param name="home">The Sindri home.</param>
/// <param name="contents">Reads a fact's content.</param>
internal sealed class AgentRunFolder(WorkflowsHome home, IFactContents contents)
{
    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const string FactPrefix = "fact:";

    /// <summary>Creates the run's folder (again, for a retried activity) and writes each declared input into it as a file named after its fact.</summary>
    /// <param name="run">The run.</param>
    /// <param name="inputs">The declared input facts the instance has, by key.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The folder, or why an input couldn't be written.</returns>
    public async ValueTask<Result<string>> Prepare(AgentStepRun run, IReadOnlyDictionary<string, FactValue> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(inputs);
        var folder = RunFolders.PathFor(home.Home, run.Workflow, run.Step.State, run.Run);
        _ = Directory.CreateDirectory(folder);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(folder, Private);
        }

        foreach (var (key, value) in inputs)
        {
            var name = key.StartsWith(FactPrefix, StringComparison.Ordinal) ? key[FactPrefix.Length..] : key;
            var content = await contents.Read(value, cancellationToken).ConfigureAwait(false);
            if (content.IsError)
            {
                return SindriErrors.LaunchInterrupted.With($"input {key} of {run.Step.State} can't be read for the run folder: {content.Error.Message}");
            }

            if (!RunFolders.Contains(folder, name))
            {
                return SindriErrors.LaunchInterrupted.With($"input {key} doesn't name a file inside the run folder");
            }

            await File.WriteAllBytesAsync(Path.Combine(folder, name), content.Value.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        return folder;
    }
}
