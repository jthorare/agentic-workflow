using System.Security.Cryptography;
using System.Text;
using Aw.Forge.Core;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Git;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Forge.Infrastructure.World;

/// <summary>
/// The forge port over a <see cref="IWorldStore"/>: what the fake (memory) and the file adapter (a JSON file) both do. It keeps
/// the real forge's rules that callers depend on: a host outside the allowed set is refused before anything is read, a reply or
/// reaction lands once per marker, a merge checks the expected head, and every change moves the pull request's version stamp.
/// A push goes through git when a <see cref="GitPusher"/> is given, otherwise it is simulated against the world's branch tips.
/// </summary>
internal partial class WorldForge(ForgeKind kind, ForgeHosts hosts, IWorldStore store, IRedactor redactor, GitPusher? git, string actingLogin = "sindri", TimeProvider? clock = null) : IForge, IForgeSeed
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private int _requests;

    /// <summary>How many calls got past the host check and touched the store.</summary>
    internal int Requests => Volatile.Read(ref _requests);

    public ForgeKind Kind => kind;

    public Result<ForgeRef> Parse(string? link) => ForgeRef.Parse(link, kind, hosts);

    public ValueTask<Result<PullSnapshot>> GetPull(ForgeRef pull, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read(pull, Snapshot));

    public ValueTask<Result<IReadOnlyList<PullDigest>>> GetPullDigest(IReadOnlyList<ForgeRef> pulls, CancellationToken cancellationToken)
    {
        var digests = new List<PullDigest>();
        foreach (var pull in pulls)
        {
            var one = Guard(pull.Host) ?? Read(pull, (_, p) => Results.Success(Digest(pull, p)));
            if (one.IsError)
            {
                return Done<IReadOnlyList<PullDigest>>(one.Error);
            }

            digests.Add(one.Value);
        }

        return Done<IReadOnlyList<PullDigest>>(digests);
    }

    public ValueTask<Result<IReadOnlyList<ForgeThread>>> ListThreads(ForgeRef pull, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read(pull, (_, p) => Results.Success<IReadOnlyList<ForgeThread>>([.. p.Threads.Select(t => ToThread(kind, p, t)).OrderBy(t => t.CreatedAt).ThenBy(t => t.Key, StringComparer.Ordinal)])));

    public ValueTask<Result<string>> Self(CancellationToken cancellationToken) => Done(Results.Success(actingLogin));

    public ValueTask<Result<string>> CheckLog(ForgeRef pull, string check, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read(pull, (_, p) => p.Logs.TryGetValue(check, out var log)
            ? Results.Success(Excerpt(log))
            : Results.Failure<string>(ForgeErrors.NotFound.With($"check '{check}' has no log"))));

    public ValueTask<Result<Delivery>> Reply(ReplyRequest request, CancellationToken cancellationToken)
    {
        var bad = Guard(request.Pull.Host) ?? Cast(ForgeMarker.Validate(request.Marker));
        return Done(bad ?? Write(request.Pull, (_, p) => ReplyTo(p, request)));
    }

    public ValueTask<Result<Delivery>> React(ReactionRequest request, CancellationToken cancellationToken)
    {
        var bad = Guard(request.Pull.Host) ?? Cast(ForgeMarker.Validate(request.Marker));
        return Done(bad ?? Write(request.Pull, (_, p) => ReactTo(p, request)));
    }

    public ValueTask<Result<ForgeRef?>> FindPull(FindPullQuery query, CancellationToken cancellationToken) =>
        Done(Guard(query.Repo.Host) ?? Count(() => Results.Success(store.Read(state => state.Pulls
            .Where(p => p.Status == PullStatus.Open && Same(p, query.Repo) && p.HeadRef == query.Head
                && (query.Author is null || p.Author == query.Author)
                && (query.Marker is null || ForgeMarker.IsIn(p.Body, query.Marker)))
            .Select(p => new ForgeRef(kind, p.Host, p.Owner, p.Repo, p.Number))
            .FirstOrDefault()))));
    public ValueTask<Result<string>> DefaultBranch(ForgeRepo repo, CancellationToken cancellationToken) =>
        Done(Guard(repo.Host) ?? Count(() => Results.Success(store.Read(state =>
            state.Repos.FirstOrDefault(r => r.Key == repo.Key)?.DefaultBranch ?? "main"))));

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private static Error? Cast(Result<Unit> result) => result.IsError ? result.Error : null;

    private static bool Same(WorldPull p, ForgeRepo repo) =>
        string.Equals(p.Host, repo.Host, StringComparison.Ordinal)
        && string.Equals(p.Owner, repo.Owner, StringComparison.OrdinalIgnoreCase)
        && string.Equals(p.Repo, repo.Name, StringComparison.OrdinalIgnoreCase);

    private static string Tip(string remote, string branch) => remote + "#" + branch;

    private static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..40];

    private Result<T> Count<T>(Func<Result<T>> work)
    {
        _ = Interlocked.Increment(ref _requests);
        return work();
    }

    private Error? Guard(string host) =>
        hosts.Allows(host) ? null : ForgeErrors.HostNotAllowed.With($"host '{host}' is not in the forge hosts");

    private Result<TResult> Read<TResult>(ForgeRef pull, Func<WorldState, WorldPull, Result<TResult>> read) =>
        Count(() => store.Read(state => Find(state, pull) is { } p ? read(state, p) : Missing(pull)));

    private Result<TResult> Write<TResult>(ForgeRef pull, Func<WorldState, WorldPull, Result<TResult>> write) =>
        Count(() => store.Write(state =>
        {
            if (Find(state, pull) is not { } p)
            {
                return Missing(pull);
            }

            var result = write(state, p);
            if (result.IsSuccess)
            {
                p.Stamp++;
            }

            return result;
        }));

    private static Error Missing(ForgeRef pull) => ForgeErrors.NotFound.With($"no pull request {pull.Owner}/{pull.Repo}#{pull.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

    private static WorldPull? Find(WorldState state, ForgeRef pull) =>
        state.Pulls.FirstOrDefault(p => p.Number == pull.Number && Same(p, pull.Repository));
}
