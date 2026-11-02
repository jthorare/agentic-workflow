using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>The live scopes of the slice, set by the test (a launch's start adds its own).</summary>
public sealed class FakeSliceScopes : ISliceScopes
{
    private readonly List<string> _live = [];
    private readonly Lock _gate = new();

    /// <summary>Adds a live scope.</summary>
    /// <param name="unit">The scope unit.</param>
    public void Add(string unit)
    {
        lock (_gate)
        {
            _live.Add(unit);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> Live(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<string>>([.. _live]);
        }
    }
}
