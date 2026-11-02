using Aw.Kernel;

namespace Aw.Connectors.Core;

/// <summary>The Connectors error catalogue (codes AW-CONNECTORS-NNN). No message carries a token.</summary>
public static class ConnectorsErrors
{
    /// <summary>AW-CONNECTORS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-CONNECTORS-001", ErrorKind.Invalid, "Connectors input failed validation");

    /// <summary>AW-CONNECTORS-002.</summary>
    public static readonly ErrorDefinition Unauthorized = new("AW-CONNECTORS-002", ErrorKind.Forbidden, "The source rejected the token (401)");

    /// <summary>AW-CONNECTORS-003.</summary>
    public static readonly ErrorDefinition HostRefused = new("AW-CONNECTORS-003", ErrorKind.Forbidden, "The source host is an IP literal or resolves to a private, loopback or link-local address, and the profile does not allow it");

    /// <summary>AW-CONNECTORS-004.</summary>
    public static readonly ErrorDefinition RedirectRefused = new("AW-CONNECTORS-004", ErrorKind.Forbidden, "The source answered with a redirect, which is never followed");

    /// <summary>AW-CONNECTORS-005.</summary>
    public static readonly ErrorDefinition Unreachable = new("AW-CONNECTORS-005", ErrorKind.Unavailable, "The source could not be reached or did not answer in time");

    /// <summary>AW-CONNECTORS-006.</summary>
    public static readonly ErrorDefinition BadReply = new("AW-CONNECTORS-006", ErrorKind.Unavailable, "The source answered with a status or a shape the connector does not read");

    /// <summary>AW-CONNECTORS-007.</summary>
    public static readonly ErrorDefinition AccessDenied = new("AW-CONNECTORS-007", ErrorKind.Forbidden, "The token may not read this resource (403 that is not a rate limit)");

    /// <summary>AW-CONNECTORS-008.</summary>
    public static readonly ErrorDefinition NotFound = new("AW-CONNECTORS-008", ErrorKind.NotFound, "The repo does not exist, or the token cannot see it");

    /// <summary>AW-CONNECTORS-009.</summary>
    public static readonly ErrorDefinition RateLimited = new("AW-CONNECTORS-009", ErrorKind.RateLimited, "The source is rate limited; the next pull resumes from the cursor");

    /// <summary>SND-INDEX-013 (the owned index spec §12.3 calls it 006, which the embed server already has): a Slack channel that is not in the profile, or a direct message. No request was sent.</summary>
    public static readonly ErrorDefinition ChannelNotAllowed = new("SND-INDEX-013", ErrorKind.Forbidden, "A channel is not in the profile, or is a direct message; no request was sent");

    /// <summary>Every Connectors error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid, Unauthorized, HostRefused, RedirectRefused, Unreachable, BadReply, AccessDenied, NotFound, RateLimited, ChannelNotAllowed];
}
