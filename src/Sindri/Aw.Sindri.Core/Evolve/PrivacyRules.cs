namespace Aw.Sindri.Core.Evolve;

/// <summary>The owner's deny lists and the private names, as the gate reads them. Core reads no file: the caller passes every set.</summary>
/// <param name="DenyTerms">privacy.denyTerms: names that never go public.</param>
/// <param name="DenyPatterns">privacy.denyPatterns: .NET regexes, matched case-insensitively; one that doesn't compile holds every proposal.</param>
/// <param name="PrivateNames">Names from private repos (path segments now, code index symbols later).</param>
/// <param name="CommonWords">Common English words, lowercased, taken out of the private names; empty when no word list is found, which holds more, never fewer.</param>
internal sealed record PrivacyRules(IReadOnlyList<string> DenyTerms, IReadOnlyList<string> DenyPatterns, IReadOnlyCollection<string> PrivateNames, IReadOnlySet<string> CommonWords);
