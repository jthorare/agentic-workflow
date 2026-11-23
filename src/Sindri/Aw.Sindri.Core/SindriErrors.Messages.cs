using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-MSG-001: no Slack connection for this workspace (messages spec §10; 409, CLI exit 1). Fix: sindri messages connect.</summary>
    public static readonly ErrorDefinition MessagesNotConnected = new("SND-MSG-001", ErrorKind.Conflict, "Messages has no Slack connection (fix: sindri messages connect)");

    /// <summary>SND-MSG-002: Slack says the token is revoked or invalid ('invalid_auth', 'token_revoked'; 401, CLI exit 1). Fix: make a new token, sindri messages connect.</summary>
    public static readonly ErrorDefinition MessagesTokenInvalid = new("SND-MSG-002", ErrorKind.Forbidden, "Slack refused the token (fix: make a new token, sindri messages connect)");

    /// <summary>SND-MSG-003: the token lacks a scope; the message carries the scope name and the feature is off (403, CLI exit 1).</summary>
    public static readonly ErrorDefinition MessagesScopeMissing = new("SND-MSG-003", ErrorKind.Forbidden, "The Slack token lacks a scope (fix: add the scope in the Slack app, reinstall, reconnect)");

    /// <summary>SND-MSG-005: Slack rate-limited the method; the message carries the seconds to wait (429, CLI exit 1). Only that method waits.</summary>
    public static readonly ErrorDefinition MessagesRateLimited = new("SND-MSG-005", ErrorKind.RateLimited, "Slack is rate limiting this call");

    /// <summary>SND-MSG-019: the workspace blocks the Slack app, or a scope needs admin approval; the message carries Slack's reason (403, CLI exit 1).</summary>
    public static readonly ErrorDefinition MessagesAppBlocked = new("SND-MSG-019", ErrorKind.Forbidden, "The workspace blocks the Slack app (fix: ask the admin to approve the app)");

    /// <summary>SND-MSG-024: the token belongs to a different workspace than the connection (409, CLI exit 1).</summary>
    public static readonly ErrorDefinition MessagesWrongWorkspace = new("SND-MSG-024", ErrorKind.Conflict, "The token is for a different Slack workspace (fix: connect the right token)");

    /// <summary>SND-MSG-030: the pasted token is a bot, app-level or cookie token ('xoxb-', 'xapp-', 'xoxc-'), checked by prefix before any call (400, CLI exit 2).</summary>
    public static readonly ErrorDefinition MessagesNotUserToken = new("SND-MSG-030", ErrorKind.Invalid, "Messages needs the User OAuth Token (xoxp-) from OAuth & Permissions, User Token section");

    /// <summary>SND-MSG-033: Slack could not be reached (no network, DNS, a timeout); nothing was read (503, CLI exit 1).</summary>
    public static readonly ErrorDefinition MessagesSlackUnreachable = new("SND-MSG-033", ErrorKind.Unavailable, "Slack could not be reached");

    /// <summary>SND-MSG-034: a Slack message ts is not seconds, a dot and six digits ('1700000000.000100'); the request named a message that can't exist (400).</summary>
    public static readonly ErrorDefinition MessagesTsInvalid = new("SND-MSG-034", ErrorKind.Invalid, "A Slack message ts is seconds, a dot and six digits");

    /// <summary>The Messages codes (messages spec §10), for <see cref="All"/>.</summary>
    private static ErrorDefinition[] EveryMessages() =>
        [MessagesNotConnected, MessagesTokenInvalid, MessagesScopeMissing, MessagesRateLimited, MessagesAppBlocked, MessagesWrongWorkspace, MessagesNotUserToken, MessagesSlackUnreachable, MessagesTsInvalid];
}
