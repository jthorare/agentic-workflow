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
}
