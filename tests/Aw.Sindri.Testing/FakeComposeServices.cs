using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>A compose reader that answers a scripted service list for one text and SND-ONYX-004 for any other. Held to <see cref="ComposeServicesContract"/>.</summary>
/// <param name="knownText">The one compose text it recognizes.</param>
/// <param name="services">The services it answers for that text.</param>
public sealed class FakeComposeServices(string knownText, IReadOnlyList<ComposeService> services) : IComposeServices
{
    /// <inheritdoc />
    public Result<IReadOnlyList<ComposeService>> Read(string composeText)
        => string.Equals(composeText, knownText, StringComparison.Ordinal)
            ? Results.Success(services)
            : SindriErrors.OnyxPinsMalformed.With("the compose file isn't valid YAML with services");
}
