namespace Aw.Sindri.Features.LinearConnection;

/// <summary>What Linear says a key may do.</summary>
internal enum LinearKeyScope
{
    /// <summary>Linear does not say (a personal API key carries no readable scope); the first write tells.</summary>
    Unknown,

    /// <summary>Linear says the key can write.</summary>
    Writable,
}
