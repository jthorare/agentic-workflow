using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Infrastructure;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The last save-time check of each Linear key in <c language="text">&lt;home&gt;/linear-checks.json</c> (owner-only, written atomically). Pointers, states and times only: never a key. Why a file and not the ledger or the profile: the
/// check is the Settings page's own memory, not a fact other parts replay, and the profile must not change when a key is merely looked at. A file that is missing or doesn't parse reads as never checked, which the page shows as 'unchecked'.
/// </summary>
/// <param name="home">Sindri's state folder.</param>
internal sealed class FileLinearCheckStore(ISindriHome home) : ILinearCheckStore
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(LinearChecksJsonContext.Default);
    private static readonly Lock _gate = new();

    private readonly string _filePath = Path.Combine(home.Path, "linear-checks.json");

    public ValueTask<Result<LinearStoredChecks>> Read(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<LinearStoredChecks>>(Load());
        }
    }

    public ValueTask<Result<Unit>> RecordRead(LinearStoredCheck check, CancellationToken cancellationToken) => ValueTask.FromResult(Change(stored => stored with { Read = check }));

    public ValueTask<Result<Unit>> RecordWrite(LinearStoredCheck check, CancellationToken cancellationToken) => ValueTask.FromResult(Change(stored => stored with { Write = check }));

    public ValueTask<Result<Unit>> ClearWrite(CancellationToken cancellationToken) => ValueTask.FromResult(Change(static stored => stored with { Write = null }));

    private Result<Unit> Change(Func<LinearStoredChecks, LinearStoredChecks> change)
    {
        lock (_gate)
        {
            try
            {
                OwnerOnly.EnsureDirectory(home.Path);
                OwnerOnly.WriteAtomically(_filePath, AwJson.Serialize(change(Load()), _json));
                return Aw.Kernel.Results.Success(Unit.Value);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return SindriErrors.ServiceFailed.With("the Linear key checks can't be written: " + e.Message);
            }
        }
    }

    private LinearStoredChecks Load()
    {
        var none = new LinearStoredChecks(Read: null, Write: null);
        try
        {
            if (!File.Exists(_filePath))
            {
                return none;
            }

            var parsed = AwJson.Deserialize<LinearStoredChecks>(File.ReadAllBytes(_filePath), _json);
            return parsed.IsError ? none : parsed.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return none;
        }
    }
}
