using Aw.Kernel;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>The spec folders of one workspace, as a test lists them; every pattern asked for is kept.</summary>
internal sealed class FakeSpecFolders : ISpecFolders
{
    private readonly List<SpecFolder> _folders = [];

    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);

    internal List<string> Patterns { get; } = [];

    /// <summary>Makes these repo paths not exist; every other path does.</summary>
    internal FakeSpecFolders Missing(params string[] paths)
    {
        _missing.UnionWith(paths);
        return this;
    }

    public ValueTask<bool> Exists(string workspace, string path, CancellationToken cancellationToken) => ValueTask.FromResult(!_missing.Contains(path));


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
