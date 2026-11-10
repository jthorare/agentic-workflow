using Aw.Context.Core.Linear;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>Gives Context's Linear read key Sindri's token order (#2508): the connected Linear connection first, the profile's pointer second, through the one <see cref="IConnectionCredentials"/> the github and slack feeds use.</summary>
/// <param name="credentials">The shared resolver.</param>
internal sealed class LinearReadCredential(IConnectionCredentials credentials) : ILinearReadCredential
{
    /// <inheritdoc />
    public string? TokenFor(string? profilePointer) => credentials.TokenFor("linear", profilePointer);
}
