using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Judge;

namespace Aw.Sindri.Testing.Judge;

/// <summary>The places a Step image may come from, given up front.</summary>
/// <param name="roots">The directories.</param>
public sealed class FakeStepImageRoots(params string[] roots) : IStepImageRoots
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<string>> Of(RepoName? repo, CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>(roots);
}
