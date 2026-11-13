using Aw.Kernel;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Tests.StartSession;

internal sealed class FakeOneOffWorktrees : IOneOffWorktrees
{
    private readonly HashSet<string> _live = new(StringComparer.Ordinal);

    public Error? MakeFails { get; set; }

    public string? Status { get; set; }

    public List<string> Names { get; } = [];

    public List<string> Removed { get; } = [];

    public IReadOnlySet<string> Live => _live;

    public ValueTask<Result<OneOffWorktree>> Make(string repoPath, string name, CancellationToken cancellationToken)
    {
        if (MakeFails is { } failure)
        {
            MakeFails = null;
            return ValueTask.FromResult<Result<OneOffWorktree>>(failure);
        }

        Names.Add(name);
        var path = "/wt/demo/" + name;
        _ = _live.Add(path);
        return ValueTask.FromResult<Result<OneOffWorktree>>(new OneOffWorktree(path, name));
    }

    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        if (Status is not null)
        {
            return ValueTask.FromResult<Result<Unit>>(Core.SindriErrors.WorktreeDirty.With("uncommitted changes"));
        }

        Removed.Add(path);
        _ = _live.Remove(path);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public ValueTask<string> StatusSummary(string path, CancellationToken cancellationToken) => ValueTask.FromResult(Status ?? string.Empty);
}
