using Aw.Sindri.Core.Graph.Templates;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="ICiSourceLookup"/> that answers one fixed source, or none, and counts how often it was asked (#3534).</summary>
/// <param name="source">The source to answer, or null for "no CI source".</param>
public sealed class FakeCiSourceLookup(GitHubCiSource? source = null) : ICiSourceLookup
{
    /// <summary>How many times the lookup was asked.</summary>
    public int Calls { get; private set; }

    public ValueTask<GitHubCiSource?> Find(CancellationToken cancellationToken)
    {
        Calls++;
        return ValueTask.FromResult(source);
    }
}
