using Aw.Kernel;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>The spec folders of one workspace, as a test lists them; every pattern asked for is kept.</summary>
internal sealed class FakeSpecFolders : ISpecFolders
{
    private readonly List<SpecFolder> _folders = [];

    internal List<string> Patterns { get; } = [];

    internal FakeSpecFolders Add(SpecFolder folder)
    {
        _folders.Add(folder);
        return this;
    }

    public ValueTask<Result<IReadOnlyList<SpecFolder>>> List(string workspace, string pattern, CancellationToken cancellationToken)
    {
        Patterns.Add(pattern);
        return ValueTask.FromResult<Result<IReadOnlyList<SpecFolder>>>(_folders.OrderBy(static f => f.Path, StringComparer.Ordinal).ToList());
    }
}
