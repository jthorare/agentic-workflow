namespace Aw.Sindri.Features.Docs;

/// <summary>What the docs check found.</summary>
/// <param name="Findings">The failing findings; none when the docs pass.</param>
internal sealed record DocsCheckResponse(IReadOnlyList<DocsCheckFinding> Findings)
{
    /// <summary>Whether the docs pass: exit 0.</summary>
    public bool Passed => Findings.Count == 0;
}
