using Aw.Kernel;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>The spec folders of one workspace, as a test lists them; every pattern asked for is kept.</summary>
internal sealed class FakeSpecFolders : ISpecFolders
{
    private readonly List<SpecFolder> _folders = [];

    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);
    private bool _strict;

    internal List<string> Patterns { get; } = [];

    /// <summary>Makes these repo paths not exist; every other path does.</summary>
    internal FakeSpecFolders Missing(params string[] paths)
    {
        _missing.UnionWith(paths);
        return this;
    }

    /// <summary>Makes only the files and folders of the listed spec folders exist (the contract's view); by default every path exists unless <see cref="Missing"/> names it.</summary>
    internal FakeSpecFolders Strict()
    {
        _strict = true;
        return this;
    }

    public ValueTask<bool> Exists(string workspace, string path, CancellationToken cancellationToken)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path.StartsWith('/') || segments.Length == 0 || segments.Contains("..", StringComparer.Ordinal))
        {
            return ValueTask.FromResult(false);
        }

        var clean = string.Join('/', segments);
        return ValueTask.FromResult(!_missing.Contains(path) && (!_strict || _folders.Exists(f => f.Path == clean || f.Path.StartsWith(clean + "/", StringComparison.Ordinal) || f.Files.Any(file => file.Kind == SpecFileKind.Regular && f.Path + "/" + file.Name == clean))));
    }


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
