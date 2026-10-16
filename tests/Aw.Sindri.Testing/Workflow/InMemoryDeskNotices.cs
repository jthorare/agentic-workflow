using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The desk's notices in memory: each identity once, in the order sent.</summary>
public sealed class InMemoryDeskNotices : IDeskNotices
{
    private readonly Lock _gate = new();
    private readonly List<DeskNotice> _sent = [];

    /// <summary>The notices sent, once per identity, in order.</summary>
    public IReadOnlyList<DeskNotice> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Send(DeskNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        lock (_gate)
        {
            if (_sent.Exists(n => string.Equals(n.Identity, notice.Identity, StringComparison.Ordinal)))
            {
                return ValueTask.FromResult<Result<bool>>(false);
            }

            _sent.Add(notice);
            return ValueTask.FromResult<Result<bool>>(true);
        }
    }
}
