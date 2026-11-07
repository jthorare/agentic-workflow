using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// The pasted keys into the OS keychain, with their pointers (never the keys) in the live profile, as `sindri index connect linear` does for the read key. The write key is the difference: Context refuses a write key
/// in a file an agent session could read, so it goes to the keychain only, and when none takes it nothing is saved (SND-LINEAR-014). A loopback Linear (the fixture's) takes only a file pointer, so there the file is used.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="engine">Checks the context settings.</param>
/// <param name="writer">Stores a key.</param>
/// <param name="pointers">Reads a stored key back.</param>
/// <param name="tokens">Writes the read key's pointer and the teams.</param>
/// <param name="writeTokens">Writes the write key's pointer.</param>
internal sealed class CoreLinearConnectionStore(IProfileSource live, IContextEngine engine, ISecretWriter writer, ISecretPointers pointers, ISourceTokenProfileWriter tokens, ILinearWriteTokenWriter writeTokens)
    : ILinearConnectionStore
{
    private const string Service = "sindri-index";
    private const string Source = "linear";

    public async ValueTask<bool> IsReadKey(string key, CancellationToken cancellationToken)
    {
        var settings = await Settings(cancellationToken).ConfigureAwait(false);
        return settings is { IsSuccess: true, Value.Linear: { } linear }
            && SecretPointer.Parse(linear.Token).Bind(pointers.Resolve) is { IsSuccess: true } stored
            && string.Equals(stored.Value, key, StringComparison.Ordinal);
    }

    public ValueTask<Result<Unit>> SaveRead(string key, IReadOnlyList<string> teams, CancellationToken cancellationToken)
    {
        var stored = writer.Store(Service, "linear", key);
        return ValueTask.FromResult(stored.IsError ? stored.Error : Point(stored.Value, teams));
    }

    private Result<Unit> Point(SecretPointer stored, IReadOnlyList<string> teams)
    {
        var resolved = pointers.Resolve(stored);
        if (resolved.IsError)
        {
            return SindriErrors.IndexTokenUnresolved.With($"the key was stored at {stored} but that pointer does not resolve: {resolved.Error.Message}");
        }

        var written = tokens.SetToken(Source, stored.ToString());
        var listed = written.IsError ? written : tokens.SetTeams(Source, teams);
        return listed.IsError ? listed.Error : Unit.Value;
    }

    public async ValueTask<Result<Unit>> SaveWrite(string key, CancellationToken cancellationToken)
    {
        var settings = await Settings(cancellationToken).ConfigureAwait(false);
        var loopback = settings is { IsSuccess: true, Value.Linear.Url.IsLoopback: true };
        var stored = loopback ? writer.StoreInFile(Service, "linear-write", key) : writer.StoreInKeychain(Service, "linear-write", key);
        if (stored.IsError)
        {
            return SindriErrors.LinearNoKeychain.With("No keychain to keep the write key in (fix: unlock your login keyring, or put the key in 1Password and set writeToken to op://…)");
        }

        var written = writeTokens.SetWriteToken(Source, stored.Value.ToString());
        return written.IsError ? written.Error : Unit.Value;
    }

    // why: the keychain entry goes first, so a keychain that refuses leaves the pointer as it was (no half-removed key); a key never set, or kept somewhere else (a loopback fixture's file), has no entry to delete
    public async ValueTask<Result<bool>> RemoveWrite(CancellationToken cancellationToken)
    {
        var settings = await Settings(cancellationToken).ConfigureAwait(false);
        if (settings is { IsSuccess: true, Value.Linear.WriteToken: { } pointer } && pointer.StartsWith("keychain:", StringComparison.Ordinal))
        {
            var deleted = writer.DeleteFromKeychain(Service, "linear-write");
            if (deleted.IsError)
            {
                return deleted.Error;
            }
        }

        return writeTokens.RemoveWriteToken(Source);
    }

    private async ValueTask<Result<ContextSettings>> Settings(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        ProfileRepoEntry[] entries = [.. (loaded.Value.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        return engine.CheckSettings(loaded.Value.Profile.Context, entries);
    }
}
