using Aw.Kernel;

namespace Aw.Sindri.Host.Commands.OnyxPurge;

/// <summary>Deletes one OS keychain entry.</summary>
internal interface IPurgeKeychain
{
    /// <summary>Deletes the entry; an entry that is already gone is not an error.</summary>
    /// <param name="service">The keychain service (`onyx`).</param>
    /// <param name="account">The keychain account (`sindri-onyx`).</param>
    /// <returns>Ok, or SND-INDEX-014.</returns>
    public Result<Unit> Remove(string service, string account);
}
