using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Branches by directory, in memory; any other directory has none. It lists the directories it was asked about.</summary>
internal sealed class FakeBranchReader : IBranchReader
{
    private readonly Dictionary<string, string?> _branches = new(StringComparer.Ordinal);
    private readonly List<string> _asked = [];

    public IReadOnlyList<string> Asked => _asked;

    public FakeBranchReader With(string directory, string? branch)
    {
        _branches[directory] = branch;
        return this;
    }

    public string? Branch(string directory)
    {
        _asked.Add(directory);
        return _branches.GetValueOrDefault(directory);
    }
}
