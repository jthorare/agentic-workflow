using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// Approved snapshots under &lt;home&gt;/approved: one directory per hash holding the canonical profile.json, and a
/// 'latest' file naming the most recent hash. A snapshot whose content no longer matches its hash isn't trusted.
/// </summary>
internal sealed class FileApprovals(SindriSettings settings, JsonSerializerOptions options) : IApprovals
{
    private readonly string _latestPath = Path.Combine(settings.ApprovalsDirectory, "latest");

    public async ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
    {
        try
        {
            return await Read(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ApprovalsUnavailable.With($"the approval under {settings.ApprovalsDirectory} can't be read: {e.Message} (fix: check its permissions, or sindri profile approve)");
        }
    }

    public ValueTask<Result<Unit>> Save(LoadedProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            Write(profile);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.ApprovalsUnavailable.With(
                $"the approval can't be saved under {settings.ApprovalsDirectory}: {e.Message} (fix: check its permissions and free space)"));
        }
    }

    private async ValueTask<Result<LoadedProfile?>> Read(CancellationToken cancellationToken)
    {
        if (!File.Exists(_latestPath))
        {
            return Results.Success<LoadedProfile?>(value: null);
        }

        var hashText = (await File.ReadAllTextAsync(_latestPath, cancellationToken).ConfigureAwait(false)).Trim();
        if (!ProfileHash.TryFrom(hashText, out var hash))
        {
            return SindriErrors.ProfileNotApproved.With($"{_latestPath} doesn't name an approval (fix: sindri profile approve)");
        }

        var snapshot = SnapshotPath(hash);
        if (!File.Exists(snapshot))
        {
            return SindriErrors.ProfileNotApproved.With($"{snapshot} is missing (fix: sindri profile approve)");
        }

        var loaded = ProfileReading.FromJson(await File.ReadAllBytesAsync(snapshot, cancellationToken).ConfigureAwait(false), snapshot, options);
        if (loaded.IsError || loaded.Value.Hash != hash)
        {
            return SindriErrors.ProfileNotApproved.With($"{snapshot} doesn't match its approval hash (fix: sindri profile approve)");
        }

        return loaded.Value;
    }

    private void Write(LoadedProfile profile)
    {
        OwnerOnly.EnsureDirectory(settings.Home);
        OwnerOnly.EnsureDirectory(settings.ApprovalsDirectory);
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(SnapshotPath(profile.Hash))!);   // why: a snapshot path always has a parent
        OwnerOnly.WriteAtomically(SnapshotPath(profile.Hash), ProfileReading.Canonical(profile.Profile, options));
        OwnerOnly.WriteAtomically(_latestPath, Encoding.UTF8.GetBytes(profile.Hash.Value + "\n"));
    }

    private string SnapshotPath(ProfileHash hash) => Path.Combine(settings.ApprovalsDirectory, hash.Value, "profile.json");
}
