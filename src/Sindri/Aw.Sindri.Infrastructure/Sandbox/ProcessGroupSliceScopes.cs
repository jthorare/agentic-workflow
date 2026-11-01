using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

#pragma warning disable MA0182 // until the macOS launcher (SB-9c) registers it
/// <summary>ISliceScopes over process groups (macOS, SB-9b): the live units are the units with a group id file whose group still has a process.</summary>
/// <param name="directory">The folder of the group files.</param>
internal sealed class ProcessGroupSliceScopes(string directory) : ISliceScopes
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> Live(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. ProcessGroupFiles.Units(directory).Where(u => ProcessGroupFiles.ReadPgid(directory, u) is { } pgid && ProcessGroupFiles.Alive(pgid))]);
}
#pragma warning restore MA0182
