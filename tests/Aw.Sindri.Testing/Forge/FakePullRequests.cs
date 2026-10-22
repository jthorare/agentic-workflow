using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>
/// An in-memory <see cref="IPullRequests"/> over a list of pull requests on allowed hosts. Held to <see cref="PullRequestsContract"/>. A test seeds a pull request
/// with <see cref="Add"/> and reads what the port did from <see cref="Calls"/> and <see cref="Pulls"/>.
/// </summary>
/// <param name="hosts">The hosts the profile allows.</param>
public sealed class FakePullRequests(IEnumerable<string> hosts) : IPullRequests
{
    private readonly HashSet<string> _hosts = new(hosts, StringComparer.Ordinal);
    private readonly List<FakePull> _pulls = [];
    private readonly Dictionary<string, string> _tips = new(StringComparer.Ordinal);
    private readonly HashSet<string> _delivered = new(StringComparer.Ordinal);

    /// <summary>Gets the operations called, in order.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Gets the pull requests, with what Open and Update gave them.</summary>
    public IReadOnlyList<FakePull> Pulls => _pulls;

    /// <summary>Adds an open pull request.</summary>
    /// <param name="repo">The repository.</param>
    /// <param name="head">The head branch.</param>
    /// <param name="headSha">The head's tip.</param>
    /// <returns>The pull request.</returns>
    public FakePull Add(PullRepo repo, string head, string headSha)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var pull = new FakePull(repo, _pulls.Count + 1, head, headSha);
        _pulls.Add(pull);
        return pull;
    }

    /// <inheritdoc />
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
    {
        Calls.Add("Get");
        return Done(Locate(link).Map(p => new PullView(p.Link, p.State, p.HeadSha, Mergeable: true, ReviewDecision: null, UnresolvedThreads: p.Threads.Count(static t => t.Kind == PullCommentKind.Thread && !t.Resolved), new PullChecks(PullChecksState.None, p.HeadSha, []))));
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken)
    {
        Calls.Add("CheckLog");
        return Done(Locate(link).Map(_ => $"log of {check}"));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);
        Calls.Add("Push");
        return Done(Locate(push.Link).Map(p =>
        {
            if (p.HeadSha != push.LeaseSha)
            {
                return PullPushOutcome.HeadMoved;
            }

            p.HeadSha = push.Sha;
            return PullPushOutcome.Pushed;
        }));
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken)
    {
        Calls.Add("RerunChecks");
        return Done(Locate(link).Map(_ => 0));
    }

    /// <summary>Plays the reviewer: adds an inline thread to the pull request at a link.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="body">The thread's text.</param>
    /// <returns>The thread's key.</returns>
    public string SeedThread(string link, string body)
    {
        var pull = Locate(link).Value;
        var key = string.Create(CultureInfo.InvariantCulture, $"thread-{pull.Threads.Count + 1}");
        pull.Threads.Add(new PullThread(key, PullCommentKind.Thread, "reviewer", "MEMBER", IsBot: false, Resolved: false, Outdated: false, "src/a.cs", body, []));
        return key;
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<PullThread>>> Threads(string link, CancellationToken cancellationToken)
    {
        Calls.Add("Threads");
        return Done(Locate(link).Map<IReadOnlyList<PullThread>>(p => [.. p.Threads]));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullMergeOutcome>> Merge(PullMerge request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Calls.Add("Merge");
        return Done(Locate(request.Link).Map(p =>
        {
            if (p.State != PullState.Open)
            {
                return new PullMergeOutcome(PullMergeStatus.Blocked, MergedSha: null, "already merged or closed");
            }

            if (p.HeadSha != request.ExpectedHead)
            {
                return new PullMergeOutcome(PullMergeStatus.HeadMoved, MergedSha: null, Reason: null);
            }

            p.State = PullState.Merged;
            return new PullMergeOutcome(PullMergeStatus.Merged, new string('m', 40), Reason: null);
        }));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullDelivery>> Reply(PullReply request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Calls.Add("Reply");
        return Done(Locate(request.Link).Map(_ => Deliver($"reply|{request.Link}|{request.ThreadKey}|{request.Marker}")));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullDelivery>> React(PullReaction request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Calls.Add("React");
        return Done(Locate(request.Link).Map(_ => Deliver($"react|{request.Link}|{request.ThreadKey}|{request.Marker}")));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullPushOutcome>> PushBranch(PullBranchPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);
        Calls.Add("PushBranch");
        if (Refuse(push.Repo.Host) is { } refused)
        {
            return Done<PullPushOutcome>(refused);
        }

        var key = $"{push.Repo.Host}/{push.Repo.Owner}/{push.Repo.Name}#{push.Branch}";
        var has = _tips.TryGetValue(key, out var tip);
        if (push.LeaseSha is null ? has : tip != push.LeaseSha)
        {
            return Done(Results.Success(PullPushOutcome.HeadMoved));
        }

        _tips[key] = push.Sha;
        return Done(Results.Success(PullPushOutcome.Pushed));
    }

    /// <inheritdoc />
    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Calls.Add("Find");
        if (Refuse(query.Repo.Host) is { } refused)
        {
            return Done<OpenedPull?>(refused);
        }

        var found = _pulls.FirstOrDefault(p => p.State == PullState.Open && p.Repo == query.Repo && p.Head == query.Head && (query.Marker is null || p.Marker == query.Marker));
        return Done(Results.Success(found is null ? null : new OpenedPull(found.Link, found.Number)));
    }

    /// <inheritdoc />
    public ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Calls.Add("Open");
        if (Refuse(request.Repo.Host) is { } refused)
        {
            return Done<OpenedPull>(refused);
        }

        var pull = Add(request.Repo, request.Head, request.HeadSha);
        pull.Title = request.Title;
        pull.Body = request.Body;
        pull.Draft = request.Draft;
        pull.Base = request.Base;
        pull.Marker = request.Marker;
        return Done(Results.Success(new OpenedPull(pull.Link, pull.Number)));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken)
    {
        Calls.Add("Update");
        return Done(Locate(link).Map(p =>
        {
            p.Title = title ?? p.Title;
            p.Body = body ?? p.Body;
            return Unit.Value;
        }));
    }

    private PullDelivery Deliver(string key) => _delivered.Add(key) ? PullDelivery.Delivered : PullDelivery.AlreadyDelivered;

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private Error? Refuse(string host) =>
        _hosts.Contains(host) ? null : SindriErrors.ForgeRefused.With($"host '{host}' is not in the forge hosts");

    private Result<FakePull> Locate(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return SindriErrors.ForgeRefused.With("link: not an absolute URL");
        }

        if (Refuse(uri.IdnHost) is { } refused)
        {
            return refused;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not [var owner, var name, "pull", var number] || !int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return SindriErrors.ForgeRefused.With("path: expected owner/repo/pull/number");
        }

        var repo = new PullRepo(uri.IdnHost, owner, name);
        return _pulls.FirstOrDefault(p => p.Repo == repo && p.Number == n) is { } pull
            ? pull
            : SindriErrors.ForgeNotFound.With(string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $"no pull request {owner}/{name}#{n}"));
    }
}
