using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>
/// The one decision of whether an index source's token is there: its tokenPointer is set and resolves to a value. Doctor's 'context:index-*' rows and the setup list's sources step both read it,
/// so they never disagree about what "connected" means. The value itself never leaves the resolver.
/// </summary>
public static class SourceTokens
{
    /// <summary>Looks the token up.</summary>
    /// <param name="tokenPointer">The profile's token pointer, or null when the source names none.</param>
    /// <param name="secrets">Resolves the pointer.</param>
    /// <returns>Null when no pointer is set; otherwise the pointer's parse and resolve (an error says why it doesn't).</returns>
    public static Result<string>? Look(string? tokenPointer, ISecretPointers secrets) =>
        tokenPointer is null ? null : SecretPointer.Parse(tokenPointer).Bind(secrets.Resolve);

    /// <summary>Whether the token is set and resolves.</summary>
    /// <param name="tokenPointer">The profile's token pointer, or null.</param>
    /// <param name="secrets">Resolves the pointer.</param>
    /// <returns>True when it does.</returns>
    public static bool Connected(string? tokenPointer, ISecretPointers secrets) => Look(tokenPointer, secrets) is { IsSuccess: true };
}
