using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IDefaultBranch"/> in memory: the default branch a test gave for a checkout; a checkout it was not told about is an error. Held to <see cref="DefaultBranchContract"/>.</summary>
public sealed class FakeDefaultBranch : IDefaultBranch
{
    private readonly Dictionary<string, string> _branches = new(StringComparer.Ordinal);

    /// <summary>The checkouts asked about.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Holds the default branch of a checkout.</summary>
    /// <param name="checkout">The checkout.</param>
    /// <param name="branch">Its default branch.</param>
    /// <returns>The same instance.</returns>
    public FakeDefaultBranch With(string checkout, string branch)
    {
        _branches[checkout] = branch;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Of(string checkout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(checkout);
        return ValueTask.FromResult(_branches.TryGetValue(checkout, out var branch)
            ? Results.Success(branch)
            : SindriErrors.InvalidInput.With($"{checkout} has no default branch the fake knows"));
    }
}
