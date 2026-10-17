namespace Aw.Forge.Core.Credentials;

/// <summary>Where a forge credential comes from.</summary>
public enum ForgeCredentialKind
{
    /// <summary>An environment variable holding a long-lived token.</summary>
    Env = 1,

    /// <summary>A command run per call that prints a token.</summary>
    Command = 2,
}
