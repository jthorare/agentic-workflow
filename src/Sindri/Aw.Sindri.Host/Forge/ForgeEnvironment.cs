using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Host.Forge;

/// <summary>
/// The forge's environment with the connected GitHub row behind its default credential (first-run spec decision 14, the lead's pick for a device-flow connect): when `workflow.forge.credential` is unset the
/// github adapter reads AW_FORGE_TOKEN, and when that variable is unset too this answers the token of the connected github connection (<see cref="IConnectionCredentials"/>). A set variable still wins,
/// and every other name reads the real environment. No `keychain:` scheme reaches the profile.
/// </summary>
internal static class ForgeEnvironment
{
    private const string DefaultVariable = "AW_FORGE_TOKEN";

    /// <summary>Wraps the environment.</summary>
    /// <param name="environment">The real environment.</param>
    /// <param name="credentials">The connected connections' tokens.</param>
    /// <returns>The environment the forge adapter reads.</returns>
    internal static Func<string, string?> WithConnection(Func<string, string?> environment, IConnectionCredentials credentials) =>
        name => environment(name) is { Length: > 0 } value
            ? value
            : string.Equals(name, DefaultVariable, StringComparison.Ordinal) ? credentials.TokenFor("github", profilePointer: null) : environment(name);
}
