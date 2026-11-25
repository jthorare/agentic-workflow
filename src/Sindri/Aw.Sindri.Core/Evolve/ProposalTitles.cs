namespace Aw.Sindri.Core.Evolve;

/// <summary>The dedupe form of a proposal's title, so case, punctuation and look-alike spellings don't make a second row for the same target.</summary>
public static class ProposalTitles
{
    /// <summary>The title's dedupe form.</summary>
    /// <param name="title">The title.</param>
    /// <returns>Its lowercased words joined by one space (<see cref="PrivacyText.Words"/>).</returns>
    public static string Normalized(string title) => string.Join(' ', PrivacyText.Words(title));
}
