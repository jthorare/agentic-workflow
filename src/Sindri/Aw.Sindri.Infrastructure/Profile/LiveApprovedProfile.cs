using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The profile runtime reads once approval is gone (#2258 PR 1): the live file, validated on each read (<see cref="IProfileSource.Load"/> checks it). A missing file is "no profile" (null), as an
/// absent approval was; an invalid one is its SND-PROFILE error, so nothing runs on a profile that wouldn't pass validation. With a <paramref name="path"/>, the answer is kept while the file's
/// last write time and length stay the same (writes go by atomic rename, so an edit changes one of them); without one, every read parses.
/// </summary>
/// <param name="source">The live profile.</param>
/// <param name="path">The profile file <paramref name="source"/> reads, for the change check; null turns the cache off.</param>
internal sealed class LiveApprovedProfile(IProfileSource source, string? path = null) : IApprovedProfile
{
    private Cached? _cached;

    internal IProfileSource Source { get; } = source;

    public async ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
    {
        var stamp = Stamp();
        if (stamp is not null && Volatile.Read(ref _cached) is { } hit && hit.Stamp == stamp)
        {
            return hit.Answer;
        }

        var answer = Convert(await Source.Load(cancellationToken).ConfigureAwait(false));
        if (stamp is not null && Stamp() == stamp)   // the file did not move while it was read
        {
            Volatile.Write(ref _cached, new Cached(stamp, answer));
        }

        return answer;
    }

    private static Result<LoadedProfile?> Convert(Result<LoadedProfile> loaded)
    {
        if (loaded.IsSuccess)
        {
            return Results.Success<LoadedProfile?>(loaded.Value);
        }

        return loaded.Error.Code == SindriErrors.ProfileMissing.Code ? Results.Success<LoadedProfile?>(value: null) : loaded.Error;
    }

    private FileStamp? Stamp()
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed record FileStamp(DateTime LastWriteUtc, long Length);

    private sealed record Cached(FileStamp Stamp, Result<LoadedProfile?> Answer);
}
