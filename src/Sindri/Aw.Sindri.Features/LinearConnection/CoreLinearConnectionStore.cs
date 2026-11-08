using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// The pasted keys into the OS keychain, with their pointers (never the keys) in the live profile, as `sindri index connect linear` does for the read key. The write key is the difference: Context refuses a write key
/// in a file an agent session could read, so it goes to the keychain only, and when none takes it nothing is saved (SND-LINEAR-014). A loopback Linear (the fixture's) takes only a file pointer, so there the file is used.
/// A save or a remove never half-applies: the profile edits are checked first (they would apply), the keychain is touched next, the edits last.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="writer">Stores a key.</param>
/// <param name="pointers">Reads a stored key back.</param>
/// <param name="tokens">Writes the read key's pointer and the teams.</param>
/// <param name="writeTokens">Writes the write key's pointer.</param>
/// <param name="editChecks">Says whether a profile edit would apply, before a key is touched.</param>
/// <param name="checks">Keeps the last check of each key; the write check goes when its key does.</param>
internal sealed class CoreLinearConnectionStore(IProfileSource live, ISecretWriter writer, ISecretPointers pointers, ISourceTokenProfileWriter tokens, ILinearWriteTokenWriter writeTokens, ILinearProfileEditCheck editChecks, ILinearCheckStore checks)
    : ILinearConnectionStore
{
    private const string Service = "sindri-index";
    private const string Source = "linear";
    private const string WriteAccount = "linear-write";

    public async ValueTask<bool> IsReadKey(string key, CancellationToken cancellationToken)
    {
        var pointer = await RawLinear("token", cancellationToken).ConfigureAwait(false);
        return pointer is not null
            && SecretPointer.Parse(pointer).Bind(pointers.Resolve) is { IsSuccess: true } stored
            && string.Equals(stored.Value, key, StringComparison.Ordinal);
    }

    // why: the edits are checked before the key is stored, so a refused one (a layout Sindri won't edit, no linear entry) never leaves the new key beside the old teams. The pointer's value does not change whether an edit applies, so a
    // keychain-shaped one stands in for what the writer will return
    public ValueTask<Result<string>> SaveRead(string key, IReadOnlyList<string> teams, CancellationToken cancellationToken)
    {
        var token = editChecks.CheckSetToken(Source, $"keychain:{Service}/{Source}");
        var fits = token.IsError ? token : editChecks.CheckSetTeams(Source, teams);
        if (fits.IsError)
        {
            return ValueTask.FromResult<Result<string>>(fits.Error);
        }

        var stored = writer.Store(Service, Source, key);
        return ValueTask.FromResult(stored.IsError ? stored.Error : Point(stored.Value, teams));
    }

    private Result<string> Point(SecretPointer stored, IReadOnlyList<string> teams)
    {
        var resolved = pointers.Resolve(stored);
        if (resolved.IsError)
        {
            return SindriErrors.IndexTokenUnresolved.With($"the key was stored at {stored} but that pointer does not resolve: {resolved.Error.Message}");
        }

        var written = tokens.SetToken(Source, stored.ToString());
        var listed = written.IsError ? written : tokens.SetTeams(Source, teams);
        return listed.IsError ? listed.Error : stored.ToString();
    }

    public async ValueTask<Result<string>> SaveWrite(string key, CancellationToken cancellationToken)
    {
        var loopback = Uri.TryCreate(await RawLinear("url", cancellationToken).ConfigureAwait(false), UriKind.Absolute, out var url) && url.IsLoopback;
        var fits = editChecks.CheckSetWriteToken(Source, $"keychain:{Service}/{WriteAccount}");
        if (fits.IsError)
        {
            return fits.Error;
        }

        var stored = loopback ? writer.StoreInFile(Service, WriteAccount, key) : writer.StoreInKeychain(Service, WriteAccount, key);
        if (stored.IsError)
        {
            return SindriErrors.LinearNoKeychain.With("No keychain to keep the write key in (fix: unlock your login keyring, or put the key in 1Password and set writeToken to op://…)");
        }

        var pointer = stored.Value.ToString();
        var written = writeTokens.SetWriteToken(Source, pointer);
        return written.IsError ? written.Error : pointer;
    }

    // why: the pointer is read raw from the loaded profile, not through the context settings check: an unrelated invalid setting must not make Remove skip the keychain delete and still drop the pointer. Order: check the pointer edit
    // would apply, delete the keychain entry (clearing its write check at once), then edit; a key never set, or kept somewhere else (a loopback fixture's file), has no entry to delete
    public async ValueTask<Result<bool>> RemoveWrite(CancellationToken cancellationToken)
    {
        var pointer = await RawLinear("writeToken", cancellationToken).ConfigureAwait(false);
        if (pointer is not null && pointer.StartsWith("keychain:", StringComparison.Ordinal))
        {
            var fits = editChecks.CheckRemoveWriteToken(Source);
            if (fits.IsError)
            {
                return fits.Error;
            }

            var deleted = writer.DeleteFromKeychain(Service, WriteAccount);
            if (deleted.IsError)
            {
                return SindriErrors.LinearNoKeychain.With($"Couldn't remove the write key from your keychain ({deleted.Error.Message}); nothing changed (fix: unlock your keychain and press Remove again)");
            }

            _ = await checks.ClearWrite(cancellationToken).ConfigureAwait(false);   // why: the key is gone, so a check saying it resolves must not outlive it, even if the pointer edit fails next
        }

        return writeTokens.RemoveWriteToken(Source);
    }

    // The string at context.sources.linear.<name> of the loaded profile; null when the profile, the entry or the key is missing or not a string.
    private async ValueTask<string?> RawLinear(string name, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var at = loaded.IsError ? null : loaded.Value.Profile.Context;
        foreach (var step in new[] { "sources", Source, name })
        {
            if (at is not { ValueKind: JsonValueKind.Object } node || !node.TryGetProperty(step, out var next))
            {
                return null;
            }

            at = next;
        }

        return at is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;
    }
}
