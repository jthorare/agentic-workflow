using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>
/// The per-repo index files as SQLite databases (spec §4.1): one immutable file per published build and a pointer file
/// that is the only thing ever replaced, so a reader sees the old index or the new one and a killed build leaves the live
/// index alone.
/// </summary>
internal sealed class SqliteIndexStore(string directory, TimeProvider time, IRepoIndexLock locks) : IIndexStore
{
    private readonly IndexFiles _files = new(directory);

    public async ValueTask<Result<IndexHeader?>> ReadHeader(RepoName repo, CancellationToken cancellationToken)
    {
        var opened = await Open(repo, "Reading the index header", cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        if (opened.Value is not { } reader)
        {
            return (IndexHeader?)null;
        }

        await using (reader.ConfigureAwait(false))
        {
            return reader.Header;
        }
    }

    public async ValueTask<Result<IIndexReader>> OpenRead(RepoName repo, CancellationToken cancellationToken)
    {
        var opened = await Open(repo, "Opening the index", cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        return opened.Value is { } reader
            ? reader
            : CodeIndexErrors.IndexNotBuilt.With($"There is no index for '{repo.Value}' yet.");
    }

    // A build holds the repo's index lock from the copy of the live index to its dispose, so an embed run's chunk and a build never
    // interleave and neither publishes over the other's work; the build releases it, and a build that fails to start releases it here.
    public async ValueTask<Result<IIndexBuild>> BeginBuild(RepoName repo, bool incremental, CancellationToken cancellationToken)
    {
        var hold = await locks.Take(repo, CodeIndexRules.IndexLockWait, cancellationToken).ConfigureAwait(false);
        if (hold.IsError)
        {
            return hold.Error;
        }

        var begun = await SqliteFailure.Guard("Starting a build", async () => await StartBuild(repo, incremental, hold.Value, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        if (begun.IsError)
        {
            await hold.Value.DisposeAsync().ConfigureAwait(false);
        }

        return begun;
    }

    public ValueTask<Result<Unit>> Sweep(RepoName repo, CancellationToken cancellationToken)
    {
        _files.Sweep(repo, time.GetUtcNow());
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    private async ValueTask<Result<SqliteIndexReader?>> Open(RepoName repo, string what, CancellationToken cancellationToken)
    {
        var path = _files.LivePath(repo);
        if (path.IsError)
        {
            return path.Error;
        }

        if (path.Value is not { } file)
        {
            return (SqliteIndexReader?)null;
        }

        var reader = await SqliteFailure.Guard(what, async () => await SqliteIndexReader.Open(file, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        return reader.IsError ? reader.Error : reader.Value;
    }

    private async ValueTask<Result<IIndexBuild>> StartBuild(RepoName repo, bool incremental, IAsyncDisposable hold, CancellationToken cancellationToken)
    {
        _files.EnsureDirectory();
        var previous = incremental ? await UsableLive(repo, cancellationToken).ConfigureAwait(false) : null;
        var ulid = Ulid.New(time);
        var path = _files.DbPath(repo, ulid);
        _files.RecordBuild(repo, ulid);
        var started = false;
        try
        {
            await CreateBuildFile(previous?.Path, path, cancellationToken).ConfigureAwait(false);
            var connection = await new SqliteConnectionFactory(new SqliteOptions(path)).Open(cancellationToken).ConfigureAwait(false);
            if (connection.IsError)
            {
                return connection.Error;
            }

            if (previous is null)
            {
                await IndexSchema.Create(connection.Value, cancellationToken).ConfigureAwait(false);
            }

            started = true;
            return new SqliteIndexBuild(new BuildTarget(_files, repo, ulid, hold), connection.Value, previous?.Header);
        }
        finally
        {
            if (!started)
            {
                IndexFiles.DeleteWithSidecars(path);
                _files.ClearBuild(repo, ulid);
            }
        }
    }

    /// <summary>The live index, when it can be copied: it opens, has this schema and passes a quick integrity check. Anything else starts the build over.</summary>
    private async Task<LiveIndex?> UsableLive(RepoName repo, CancellationToken cancellationToken)
    {
        var live = _files.LivePath(repo);
        if (live.IsError || live.Value is not { } path)
        {
            return null;
        }

        try
        {
            var opened = await SqliteIndexReader.Open(path, cancellationToken).ConfigureAwait(false);
            if (opened.IsError)
            {
                return null;
            }

            var reader = opened.Value;
            await using (reader.ConfigureAwait(false))
            {
                return await reader.QuickCheck(cancellationToken).ConfigureAwait(false) ? new LiveIndex(path, reader.Header) : null;
            }
        }
        catch (Exception exception) when (exception is SqliteException or InvalidDataException)
        {
            return null;
        }
    }

    private static async Task CreateBuildFile(string? copyFrom, string path, CancellationToken cancellationToken)
    {
        var target = IndexFiles.CreatePrivate(path);
        await using (target.ConfigureAwait(false))
        {
            if (copyFrom is not null)
            {
                var source = new FileStream(copyFrom, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                await using (source.ConfigureAwait(false))
                {
                    await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private sealed record LiveIndex(string Path, IndexHeader Header);
}
