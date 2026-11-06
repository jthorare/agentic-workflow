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

    /// <summary>Gets or sets an error that Find, Open, Update and PushBranch answer when set.</summary>
    public Error? Failure { get; set; }

    /// <summary>Gets the requests Open received, in order.</summary>
    public IList<OpenPull> Opened { get; } = [];

    /// <summary>Gets the branch pushes PushBranch received, in order.</summary>
    public IList<PullBranchPush> Pushed { get; } = [];

    /// <summary>Gets or sets what PushBranch answers when the lease holds.</summary>
    public PullPushOutcome PushOutcome { get; set; } = PullPushOutcome.Pushed;

    /// <summary>Gets the (link, title, body) triples Update received, in order.</summary>
    public IList<(string Link, string? Title, string? Body)> Updated { get; } = [];

    /// <summary>Gets the operations called, in order.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Gets the log CheckLog answers for a check by name; a check not listed answers "log of NAME".</summary>
    public IDictionary<string, string> CheckLogs { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the merges asked for, in order.</summary>
    public IList<PullMerge> Merges { get; } = [];

    /// <summary>Gets or sets a status Merge answers instead of merging; null merges when the expected head holds.</summary>
    public PullMergeStatus? MergeStatus { get; set; }

    /// <summary>Gets or sets an outcome Push answers instead of checking its lease; null checks it.</summary>
    public PullPushOutcome? PushAnswer { get; set; }

    /// <summary>Gets the pushes asked for, in order.</summary>
    public IList<PullPush> Pushes { get; } = [];

    /// <summary>Gets the re-runs asked for (link and commit), in order.</summary>
    public IList<(string Link, string Sha)> Reruns { get; } = [];

    /// <summary>Gets or sets the error <see cref="Self"/> answers, or null for the login.</summary>
    public Error? SelfError { get; set; }

    /// <summary>Gets or sets a task <see cref="Self"/> awaits first (a test makes the forge hang with it).</summary>
    public Func<CancellationToken, Task>? BeforeSelf { get; set; }

    /// <summary>Gets the default branch per repository; a repository not listed has "main".</summary>
    public IDictionary<PullRepo, string> DefaultBranches { get; } = new Dictionary<PullRepo, string>();

    /// <summary>Gets how many links each <see cref="Digest"/> call carried, in order.</summary>
    public IList<int> DigestSizes { get; } = [];

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

    /// <summary>Puts an open pull request on the fake forge, as if the request had opened it.</summary>
    /// <param name="request">What it was opened with.</param>
    /// <returns>The pull request.</returns>
    public OpenedPull Seed(OpenPull request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pull = Add(request.Repo, request.Head, request.HeadSha);
        pull.Title = request.Title;
        pull.Body = request.Body;
        pull.Draft = request.Draft;
        pull.Labels = [.. request.Labels ?? []];
        pull.Marker = request.Marker;
        pull.Base = request.Base;
        _tips[$"{request.Repo.Host}/{request.Repo.Owner}/{request.Repo.Name}#{request.Head}"] = request.HeadSha;
        return new OpenedPull(pull.Link, pull.Number);
    }

    /// <inheritdoc />
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
    {
        Calls.Add("Get");
        return Done(Locate(link).Map(p => new PullView(p.Link, p.State, p.HeadSha, p.Mergeable, p.ReviewDecision, p.Threads.Count(static t => t.Kind == PullCommentKind.Thread && !t.Resolved), p.Checks ?? new PullChecks(p.FailingChecks.Count > 0 ? PullChecksState.Failing : p.ChecksState, p.ChecksSha ?? p.HeadSha, [.. p.FailingChecks], Passed: 0, Pending: 0), p.Labels, p.Base, p.BaseSha)));
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken)
    {
        Calls.Add("CheckLog");
        return Done(Locate(link).Map(_ => CheckLogs.TryGetValue(check, out var log) ? log : $"log of {check}"));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);
        Calls.Add("Push");
        Pushes.Add(push);
        return Done(Locate(push.Link).Map(p =>
        {
            if (PushAnswer is { } forced)
            {
                return forced;
            }

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
        Reruns.Add((link, sha));
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
        pull.Threads.Add(new PullThread(key, PullCommentKind.Thread, "reviewer", "MEMBER", IsBot: false, Resolved: false, Outdated: false, "src/a.cs", body, [], new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero).AddMinutes(pull.Threads.Count), Line: 12, EditedAt: null, $"{pull.Link}#{key}"));
        return key;
    }

    /// <inheritdoc />
    public async ValueTask<Result<string>> Self(CancellationToken cancellationToken)
    {
        Calls.Add("Self");
        if (BeforeSelf is { } wait)
        {
            await wait(cancellationToken).ConfigureAwait(false);
        }

        return SelfError is { } error ? error : "sindri";
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
        Merges.Add(request);
        return Done(Locate(request.Link).Map(p =>
        {
            if (MergeStatus is { } forced)
            {
                return new PullMergeOutcome(forced, MergedSha: null, "set by the test");
            }

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
        Pushed.Add(push);
        if (Failure is { } failure)
        {
            return Done<PullPushOutcome>(failure);
        }

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

        if (PushOutcome != PullPushOutcome.Pushed)
        {
            return Done(Results.Success(PushOutcome));
        }

        _tips[key] = push.Sha;
        return Done(Results.Success(PullPushOutcome.Pushed));
    }

    /// <inheritdoc />
    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Calls.Add("Find");
        if (Failure is { } failure)
        {
            return Done<OpenedPull?>(failure);
        }

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

        Opened.Add(request);
        return Done(Results.Success(Seed(request)));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken)
    {
        Calls.Add("Update");
        if (Failure is { } failure)
        {
            return Done<Unit>(failure);
        }

        Updated.Add((link, title, body));
        return Done(Locate(link).Map(p =>
        {
            p.Title = title ?? p.Title;
            p.Body = body ?? p.Body;
            return Unit.Value;
        }));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Retarget(string link, string newBase, CancellationToken cancellationToken)
    {
        Calls.Add("Retarget");
        return Done(Locate(link).Map(p =>
        {
            p.Base = newBase;
            return Unit.Value;
        }));
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> DefaultBranch(PullRepo repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        Calls.Add("DefaultBranch");
        return Done(Refuse(repo.Host) is { } refused ? refused : Results.Success(DefaultBranches.TryGetValue(repo, out var branch) ? branch : "main"));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<PullDigest>>> Digest(IReadOnlyList<string> links, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(links);
        Calls.Add("Digest");
        if (Failure is { } failure)
        {
            return Done<IReadOnlyList<PullDigest>>(failure);
        }

        var digests = new List<PullDigest>(links.Count);
        foreach (var link in links)
        {
            var located = Locate(link);
            if (located.IsError)
            {
                return Done<IReadOnlyList<PullDigest>>(located.Error);
            }

            var p = located.Value;
            var checks = p.FailingChecks.Count > 0 ? PullChecksState.Failing : PullChecksState.None;
            digests.Add(new PullDigest(p.Link, p.State, p.HeadSha, checks, p.Threads.Count, $"{p.HeadSha}|{p.State}|{checks}|{p.Threads.Count}|{p.Base}"));
        }

        for (var from = 0; from < links.Count; from += IPullRequests.DigestBatchSize)
        {
            DigestSizes.Add(Math.Min(IPullRequests.DigestBatchSize, links.Count - from));
        }

        return Done(Results.Success<IReadOnlyList<PullDigest>>(digests));
    }

    /// <summary>Plays the forge: sets the changed files of the pull request at a link, with each file's text at the head by path.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="files">The files.</param>
    /// <param name="texts">The text of each file at the head, by path.</param>
    public void SeedFiles(string link, IReadOnlyList<PullFile> files, IReadOnlyDictionary<string, string> texts)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(texts);
        var pull = Locate(link).Value;
        pull.Files = files;
        pull.FileTexts.Clear();
        foreach (var (path, text) in texts)
        {
            pull.FileTexts[path] = text;
        }
    }

    /// <summary>Plays CI: sets the check runs Get reports on the pull request at a link.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="runs">The runs.</param>
    public void SeedRuns(string link, IReadOnlyList<PullCheckRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var pull = Locate(link).Value;
        var failing = runs.Where(static r => r.Conclusion == "failure").Select(static r => r.Name).ToList();
        pull.Checks = new PullChecks(failing.Count > 0 ? PullChecksState.Failing : PullChecksState.Passing, pull.HeadSha, failing, runs.Count - failing.Count, Pending: 0, runs);
    }

    /// <inheritdoc />
    public ValueTask<Result<PullFiles>> Files(string link, CancellationToken cancellationToken)
    {
        Calls.Add("Files");
        return Done(Locate(link).Map(p => new PullFiles(p.Files, CapReached: false)));
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> FileText(string link, string path, string sha, CancellationToken cancellationToken)
    {
        Calls.Add("FileText");
        return Done(Locate(link).Bind(p => FileTextOf(p, path)));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullReviewOutcome>> SubmitReview(PullReview review, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);
        Calls.Add("SubmitReview");
        return Done(Locate(review.Link).Map(p =>
        {
            if (p.HeadSha != review.ExpectedHead)
            {
                return PullReviewOutcome.HeadMoved;
            }

            if (p.Reviews.ContainsKey(review.Marker))
            {
                return PullReviewOutcome.AlreadyDelivered;
            }

            p.Reviews[review.Marker] = review;
            var key = string.Create(CultureInfo.InvariantCulture, $"review-{p.Reviews.Count}");
            p.Threads.Add(new PullThread(key, PullCommentKind.Review, "sindri", "MEMBER", IsBot: false, Resolved: false, Outdated: false, Path: null, review.Body, [review.Marker], new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero).AddMinutes(p.Threads.Count), Line: null, EditedAt: null, $"{p.Link}#{key}"));
            return PullReviewOutcome.Posted;
        }));
    }

    private static Result<string> FileTextOf(FakePull pull, string path)
    {
        if (pull.FileTexts.TryGetValue(path, out var text))
        {
            return text;
        }

        return SindriErrors.ForgeNotFound.With($"no text for '{path}'");
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
