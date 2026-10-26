using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The files an agent Step wrote to its output folder (<see cref="AgentRecovery.OutputFolder"/> in the run directory), read when its session ended without a report.</summary>
internal interface IStepOutputs
{
    /// <summary>The names of the files in the run directory's output folder.</summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="since">Only files written at or after this time; null for every file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The names, in name order; empty when the folder isn't there; an error when it can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> Names(string runDirectory, DateTimeOffset? since, CancellationToken cancellationToken);

    /// <summary>One output file's content, at most <see cref="IFactStore.MaxBytes"/>.</summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="name">A name <see cref="Names"/> returned.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content, or why it can't be read.</returns>
    public ValueTask<Result<ReadOnlyMemory<byte>>> Read(string runDirectory, string name, CancellationToken cancellationToken);
}
