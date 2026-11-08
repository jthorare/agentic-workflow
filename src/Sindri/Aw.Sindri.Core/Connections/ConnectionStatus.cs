namespace Aw.Sindri.Core.Connections;

/// <summary>Where a connection stands. Wire names: 'connected', 'needs-sign-in', 'error'.</summary>
public enum ConnectionStatus
{
    /// <summary>The secret is stored and last worked.</summary>
    Connected,

    /// <summary>The owner has to sign in again.</summary>
    NeedsSignIn,

    /// <summary>The last use or test failed for another reason (see the status reason).</summary>
    Error,
}
