using Aw.Forge.Core;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.Credentials;

/// <summary>The host check both credential sources share: a credential is bound to one host (spec 10.2) and never sent to another.</summary>
internal static class BoundHost
{
    internal static bool Matches(string bound, string host) =>
        string.Equals(bound.Trim(), host.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static Error Refusal(string host) =>
        ForgeErrors.Unauthorized.With($"the credential is not bound to host '{host}'");
}
