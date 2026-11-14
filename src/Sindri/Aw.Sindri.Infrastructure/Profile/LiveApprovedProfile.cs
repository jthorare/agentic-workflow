using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The profile runtime reads once approval is gone (#2258 PR 1): the live file, validated on each read (<see cref="IProfileSource.Load"/> checks it). A missing file is "no profile" (null), as an
/// absent approval was; an invalid one is its SND-PROFILE error, so nothing runs on a profile that wouldn't pass validation. With a <paramref name="path"/>, the answer is kept while the file's
/// last write time, length and file mode stay the same (writes go by atomic rename, so an edit changes one of them; a chmod changes only the mode, #3429); without one,
/// every read parses.
/// </summary>
/// <param name="source">The live profile.</param>
/// <param name="path">The profile file <paramref name="source"/> reads, for the change check; null turns the cache off.</param>
internal sealed class LiveApprovedProfile(IProfileSource source, string? path = null) : IApprovedProfile, IProfileCache
{
    private Cached? _cached;
    private int _generation;

    internal IProfileSource Source { get; } = source;

    public async ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
    {
        var generation = Volatile.Read(ref _generation);
        var stamp = Stamp();
        if (stamp is not null && Volatile.Read(ref _cached) is { } hit && hit.Stamp == stamp && hit.Generation == generation)
        {
            return hit.Answer;
        }

        var answer = Convert(await Source.Load(cancellationToken).ConfigureAwait(false));
        if (stamp is not null && Stamp() == stamp && Volatile.Read(ref _generation) == generation)   // nothing wrote the file while it was read
        {
            Volatile.Write(ref _cached, new Cached(stamp, generation, answer));
        }

        return answer;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        _ = Interlocked.Increment(ref _generation);
        Volatile.Write(ref _cached, null);
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
            return info.Exists ? new FileStamp(info.LastWriteTimeUtc, info.Length, OperatingSystem.IsWindows() ? UnixFileMode.None : info.UnixFileMode) : null;
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

    private sealed record FileStamp(DateTime LastWriteUtc, long Length, UnixFileMode Mode);

    private sealed record Cached(FileStamp Stamp, int Generation, Result<LoadedProfile?> Answer);
}
