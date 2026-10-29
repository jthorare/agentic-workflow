
namespace Aw.Sindri.Host.Commands.OnyxPurge;

/// <summary>The two fields of an old `sindri onyx up`'s onyx/state.json the purge reads; everything else in the file is ignored.</summary>
/// <param name="Project">The compose project the stack ran under; null in a state written before it was recorded.</param>
/// <param name="KeyPointer">Where Sindri stored Onyx's key (`keychain:onyx/sindri-onyx` or a `file:` pointer); null before the key step ran.</param>
internal sealed record PurgeState(string? Project = null, string? KeyPointer = null);
