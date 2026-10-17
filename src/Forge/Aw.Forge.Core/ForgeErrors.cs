using Aw.Kernel;

namespace Aw.Forge.Core;

/// <summary>The Forge error catalogue (codes AW-FORGE-NNN).</summary>
public static class ForgeErrors
{
    /// <summary>AW-FORGE-001: a link, ref or request failed validation; the message names the part.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-FORGE-001", ErrorKind.Invalid, "Forge input failed validation");

    /// <summary>AW-FORGE-002: the host isn't in the profile's forge hosts; no request was sent.</summary>
    public static readonly ErrorDefinition HostNotAllowed = new("AW-FORGE-002", ErrorKind.Forbidden, "The forge host is not in the allowed hosts");

    /// <summary>AW-FORGE-003: the forge credential is missing, expired or rejected.</summary>
    public static readonly ErrorDefinition Unauthorized = new("AW-FORGE-003", ErrorKind.Forbidden, "The forge credential is missing or was rejected");

    /// <summary>AW-FORGE-004: the pull request, repository, thread or check isn't there.</summary>
    public static readonly ErrorDefinition NotFound = new("AW-FORGE-004", ErrorKind.NotFound, "The forge has no such object");

    /// <summary>AW-FORGE-005: the forge or the network is unavailable.</summary>
    public static readonly ErrorDefinition Unavailable = new("AW-FORGE-005", ErrorKind.Unavailable, "The forge is unavailable");

    /// <summary>AW-FORGE-006: the forge is rate limiting this credential.</summary>
    public static readonly ErrorDefinition RateLimited = new("AW-FORGE-006", ErrorKind.RateLimited, "The forge is rate limiting requests");

    /// <summary>AW-FORGE-007: the forge refused a well-formed request.</summary>
    public static readonly ErrorDefinition Rejected = new("AW-FORGE-007", ErrorKind.Invalid, "The forge rejected the request");

    /// <summary>AW-FORGE-008: git could not push (not a lease refusal, which is a PushOutcome).</summary>
    public static readonly ErrorDefinition PushFailed = new("AW-FORGE-008", ErrorKind.Unavailable, "The push failed");

    /// <summary>Every Forge error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [Invalid, HostNotAllowed, Unauthorized, NotFound, Unavailable, RateLimited, Rejected, PushFailed];
}
