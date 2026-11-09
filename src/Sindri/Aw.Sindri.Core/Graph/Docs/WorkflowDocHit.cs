namespace Aw.Sindri.Core.Graph.Docs;

/// <summary>A page that answers a question, and how well.</summary>
public sealed class WorkflowDocHit
{
    /// <summary>Makes a hit.</summary>
    /// <param name="page">The page.</param>
    /// <param name="score">How well it matches; higher is better.</param>
    public WorkflowDocHit(WorkflowDocPage page, double score)
    {
        ArgumentNullException.ThrowIfNull(page);
        Page = page;
        Score = score;
    }

    /// <summary>The page.</summary>
    public WorkflowDocPage Page { get; }

    /// <summary>The match score; higher is better.</summary>
    public double Score { get; }
}
