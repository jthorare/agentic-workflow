namespace Aw.Context.Core.Glossary;

/// <summary>The knobs of <see cref="GlossaryBuilder"/>.</summary>
public sealed record GlossaryRules
{
    /// <summary>Two feature centroids join one cluster at this cosine or above (single link).</summary>
    public double Similarity { get; init; } = 0.80;

    /// <summary>The terms kept per cluster.</summary>
    public int TermsPerCluster { get; init; } = 5;

    /// <summary>The most features clustered; later ones, in key order, are dropped and the glossary is marked truncated.</summary>
    public int MaxFeatures { get; init; } = 2000;

    /// <summary>Words that carry no meaning in code, never terms.</summary>
    public IReadOnlySet<string> Stop { get; init; } = GlossaryStoplist.Default;
}
