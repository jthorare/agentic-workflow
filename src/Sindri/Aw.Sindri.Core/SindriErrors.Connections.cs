using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-CONN-001: no connection has that name.</summary>
    public static readonly ErrorDefinition ConnectionNotFound = new("SND-CONN-001", ErrorKind.NotFound, "Sindri has no connection with that name");

    /// <summary>SND-CONN-002: the connection has to be signed in again.</summary>
    public static readonly ErrorDefinition ConnectionNeedsSignIn = new("SND-CONN-002", ErrorKind.Conflict, "The connection needs a new sign-in");

    /// <summary>SND-CONN-003: nobody finished the browser sign-in in 5 minutes.</summary>
    public static readonly ErrorDefinition ConnectionSignInTimeout = new("SND-CONN-003", ErrorKind.Unavailable, "The sign-in was not finished in time");

    /// <summary>SND-CONN-004: the callback's state is not the one this sign-in made.</summary>
    public static readonly ErrorDefinition ConnectionStateMismatch = new("SND-CONN-004", ErrorKind.Forbidden, "The sign-in callback did not match this sign-in");

    /// <summary>SND-CONN-005: the provider refused to refresh the token.</summary>
    public static readonly ErrorDefinition ConnectionRefreshFailed = new("SND-CONN-005", ErrorKind.Unavailable, "The provider refused to refresh the connection's token");

    /// <summary>SND-CONN-006: the connection's secret could not be stored, read or deleted.</summary>
    public static readonly ErrorDefinition ConnectionSecretUnavailable = new("SND-CONN-006", ErrorKind.Unavailable, "The connection's secret store is not available");

    /// <summary>SND-CONN-007: the preset needs a client id and none was given.</summary>
    public static readonly ErrorDefinition ConnectionNeedsClientId = new("SND-CONN-007", ErrorKind.Invalid, "This preset needs a client id");

    /// <summary>SND-CONN-008: the connection name is not '&lt;service&gt;:&lt;label&gt;' in lowercase.</summary>
    public static readonly ErrorDefinition ConnectionNameInvalid = new("SND-CONN-008", ErrorKind.Invalid, "A connection name is <service>:<label>, lowercase letters, digits and '-'");

    /// <summary>SND-CONN-009: the owner denied consent at the provider.</summary>
    public static readonly ErrorDefinition ConnectionConsentDenied = new("SND-CONN-009", ErrorKind.Forbidden, "Consent was denied at the provider");

    /// <summary>SND-CONN-010: a connection with that name exists.</summary>
    public static readonly ErrorDefinition ConnectionExists = new("SND-CONN-010", ErrorKind.Conflict, "A connection with that name already exists");

    /// <summary>SND-CONN-011: the connection is key-based and has no browser sign-in to run again.</summary>
    public static readonly ErrorDefinition ConnectionKindNoSignIn = new("SND-CONN-011", ErrorKind.Invalid, "This connection uses a key, not a browser sign-in");

    /// <summary>SND-CONN-201: the handoff inbox answered something that breaks the contract (the message names the field).</summary>
    public static readonly ErrorDefinition InboxContractViolation = new("SND-CONN-201", ErrorKind.Invalid, "The handoff inbox's answer breaks the inbox contract");

    /// <summary>SND-CONN-202: another caller claimed the offer first. Informational: nothing starts.</summary>
    public static readonly ErrorDefinition InboxClaimLost = new("SND-CONN-202", ErrorKind.Conflict, "Another caller claimed the offer first");

    /// <summary>SND-CONN-203: the offer is not there any more (unknown or expired).</summary>
    public static readonly ErrorDefinition InboxOfferGone = new("SND-CONN-203", ErrorKind.NotFound, "The handoff inbox has no such offer any more");

    /// <summary>SND-CONN-204: the handoff inbox did not take the result.</summary>
    public static readonly ErrorDefinition InboxSubmitFailed = new("SND-CONN-204", ErrorKind.Unavailable, "The handoff inbox did not take the result");

    /// <summary>SND-CONN-205: the handoff inbox is out of reach or did not answer in time.</summary>
    public static readonly ErrorDefinition InboxUnavailable = new("SND-CONN-205", ErrorKind.Unavailable, "The handoff inbox is out of reach");

    /// <summary>SND-CONN-206: the inbox's host is not on the allowlist, or its URL is not https (http is allowed for loopback only).</summary>
    public static readonly ErrorDefinition InboxHostNotAllowed = new("SND-CONN-206", ErrorKind.Forbidden, "The handoff inbox's host is not allowed");
}
