using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>Merges a pull request only if its head is still the one the caller looked at.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="Method">The merge method.</param>
/// <param name="ExpectedHead">The head sha the caller proved.</param>
public sealed record MergeRequest(ForgeRef Pull, MergeMethod Method, string ExpectedHead);
