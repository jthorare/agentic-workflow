using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IWorkspaceRebase"/> in memory: a workspace answers what a test scripted for it. Held to <see cref="WorkspaceRebaseContract"/>.</summary>
public sealed class FakeWorkspaceRebase : IWorkspaceRebase
{
    private readonly Dictionary<string, RebaseOutcome> _scripts = new(StringComparer.Ordinal);

    /// <summary>Gets the calls made, in order.</summary>
    public IList<(string Workspace, string Onto, string? OldBase)> Calls { get; } = [];

    /// <summary>Makes <paramref name="workspace"/> exist and answer <paramref name="outcome"/>.</summary>
    /// <param name="workspace">The path.</param>
    /// <param name="outcome">What a rebase of it ends as.</param>
    public void Script(string workspace, RebaseOutcome outcome) => _scripts[workspace] = outcome;

    /// <inheritdoc/>
    public ValueTask<Result<RebaseOutcome>> Rebase(string workspace, string onto, string? oldBase, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        Calls.Add((workspace, onto, oldBase));
        return ValueTask.FromResult<Result<RebaseOutcome>>(_scripts.TryGetValue(workspace, out var outcome) ? outcome : WorkflowErrors.EffectFailed.With($"{workspace} is not a worktree"));
    }
}
