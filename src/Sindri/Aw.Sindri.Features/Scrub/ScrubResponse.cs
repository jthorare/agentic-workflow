namespace Aw.Sindri.Features.Scrub;

/// <summary>What 'sindri scrub' prints on success.</summary>
/// <param name="Output">Standard input redacted (stdin), the empty string (staged, nothing found), or the sentence saying where the hook went (install).</param>
/// <param name="HookPath">The hook file written or replaced; null for the other two forms.</param>
public sealed record ScrubResponse(string Output, string? HookPath = null);
