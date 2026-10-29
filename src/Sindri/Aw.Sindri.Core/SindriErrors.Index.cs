using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-INDEX-009: a source's token pointer does not resolve, the source refused the token, or a file: pointer is wider than 0600.</summary>
    public static readonly ErrorDefinition IndexTokenUnresolved = new("SND-INDEX-009", ErrorKind.Invalid, "A source token pointer does not resolve, the token was refused, or its file is wider than 0600");

    /// <summary>SND-INDEX-010: a repo listed in the profile is unreadable with the token; the message names each one.</summary>
    public static readonly ErrorDefinition IndexRepoUnreadable = new("SND-INDEX-010", ErrorKind.Unavailable, "A listed repo is unreadable with this token");

    /// <summary>SND-INDEX-011: a repo or host is not in the profile; no request was sent.</summary>
    public static readonly ErrorDefinition IndexNotInProfile = new("SND-INDEX-011", ErrorKind.Invalid, "The source, repo or host is not in the profile; no request was sent");

    /// <summary>SND-INDEX-014: `sindri onyx down --purge` could not remove one of the things an old `onyx up` left (a container, volume, keychain entry or the onyx/ directory); the message names it and the fix.</summary>
    public static readonly ErrorDefinition IndexOnyxLeftover = new("SND-INDEX-014", ErrorKind.Unavailable, "Removing what an old `sindri onyx up` left on this box failed");
}
