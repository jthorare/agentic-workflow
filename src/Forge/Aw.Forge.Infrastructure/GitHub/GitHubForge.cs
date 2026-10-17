using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Credentials;
using Aw.Forge.Infrastructure.Git;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Forge.Infrastructure.GitHub;

/// <summary>
/// The forge port over GitHub's REST and GraphQL APIs (github.com, or a GitHub Enterprise Server host). Reads here; writes in
/// GitHubForge.Write.cs and the conversation in GitHubForge.Threads.cs. Every operation refuses a host outside the allowed set
/// before it builds a request.
/// </summary>
internal sealed partial class GitHubForge(GitHubApi api, ICredentialSource credentials, GitPusher git, IRedactor redactor) : IForge
{
    private readonly GitHubApi _api = api;

    public ForgeKind Kind => ForgeKind.GitHub;

    /// <summary>Requests this adapter has sent, for tests that prove a refusal sent none.</summary>
    internal int RequestsSent => _api.Sent;

    public Result<ForgeRef> Parse(string? link) => ForgeRef.Parse(link, ForgeKind.GitHub, _api.Hosts);

    public async ValueTask<Result<PullSnapshot>> GetPull(ForgeRef pull, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var state = await ReadPull(pull, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        var json = state.Value;
        var head = json.At("head") ?? default;
        var checks = await Checks(pull, head.Str("sha"), cancellationToken).ConfigureAwait(false);
        if (checks.IsError)
        {
            return checks.Error;
        }

        var decision = await ReviewDecision(pull, cancellationToken).ConfigureAwait(false);
        return decision.IsError ? decision.Error : Snapshot(pull, json, checks.Value, decision.Value);
    }

    public async ValueTask<Result<IReadOnlyList<PullDigest>>> GetPullDigest(IReadOnlyList<ForgeRef> pulls, CancellationToken cancellationToken)
    {
        var digests = new List<PullDigest>();
        foreach (var pull in pulls)
        {
            if (_api.Guard(pull.Host) is { } refused)
            {
                return refused;
            }

            var state = await ReadPull(pull, cancellationToken).ConfigureAwait(false);
            if (state.IsError)
            {
                return state.Error;
            }

            var checks = await Checks(pull, state.Value.At("head")?.Str("sha") ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (checks.IsError)
            {
                return checks.Error;
            }

            digests.Add(Digest(pull, state.Value, checks.Value));
        }

        return digests;
    }

    public async ValueTask<Result<string>> CheckLog(ForgeRef pull, string check, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var state = await ReadPull(pull, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        var runs = await CheckRuns(pull, state.Value.At("head")?.Str("sha") ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (runs.IsError)
        {
            return runs.Error;
        }

        foreach (var run in runs.Value.Where(r => r.Str("name") == check))
        {
            var text = string.Join('\n', new[] { run.Str("output", "title"), run.Str("output", "summary"), run.Str("output", "text") }.Where(s => s.Length > 0));
            var tail = text.Length > ForgeLimits.LogExcerptCharacters ? text[^ForgeLimits.LogExcerptCharacters..] : text;
            return redactor.Scrub(tail).Text;
        }

        return ForgeErrors.NotFound.With($"no check named '{check}'");
    }

    public async ValueTask<Result<string>> DefaultBranch(ForgeRepo repo, CancellationToken cancellationToken)
    {
        if (_api.Guard(repo.Host) is { } refused)
        {
            return refused;
        }

        var reply = await _api.Call(repo.Host, HttpMethod.Get, $"/repos/{repo.Owner}/{repo.Name}", body: null, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Ok ? reply.Value.Body.Str("default_branch") : GitHubApi.Fail(reply.Value);
    }

    public async ValueTask<Result<PushOutcome>> Push(PushRequest request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Remote, UriKind.Absolute, out var remote) || remote.Scheme != Uri.UriSchemeHttps || remote.UserInfo.Length > 0)
        {
            return ForgeErrors.Invalid.With("remote: an https URL without credentials");
        }

        if (_api.Guard(remote.IdnHost.ToLowerInvariant()) is { } refused)
        {
            return refused;
        }

        var token = await credentials.Token(remote.IdnHost.ToLowerInvariant(), refresh: false, cancellationToken).ConfigureAwait(false);
        return token.IsError
            ? token.Error
            : await git.Push(request, GitPusher.BasicHeader(token.Value), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsDecision(string state) =>
        string.Equals(state, "APPROVED", StringComparison.Ordinal)
        || string.Equals(state, "CHANGES_REQUESTED", StringComparison.Ordinal)
        || string.Equals(state, "DISMISSED", StringComparison.Ordinal);

    private static string RepoPath(ForgeRef pull) => $"/repos/{pull.Owner}/{pull.Repo}";

    private async ValueTask<Result<JsonElement>> ReadPull(ForgeRef pull, CancellationToken cancellationToken)
    {
        var reply = await _api.Call(pull.Host, HttpMethod.Get, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}"), body: null, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Ok ? reply.Value.Body : GitHubApi.Fail(reply.Value);
    }

    private async ValueTask<Result<IReadOnlyList<JsonElement>>> CheckRuns(ForgeRef pull, string sha, CancellationToken cancellationToken)
    {
        var reply = await _api.Call(pull.Host, HttpMethod.Get, $"{RepoPath(pull)}/commits/{Uri.EscapeDataString(sha)}/check-runs?per_page=100&filter=latest", body: null, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Ok ? Results.Success<IReadOnlyList<JsonElement>>([.. (reply.Value.Body.At("check_runs") ?? default).Items()]) : GitHubApi.Fail(reply.Value);
    }

    private async ValueTask<Result<ChecksSummary>> Checks(ForgeRef pull, string sha, CancellationToken cancellationToken)
    {
        var runs = await CheckRuns(pull, sha, cancellationToken).ConfigureAwait(false);
        return runs.IsError ? runs.Error : Summarise(sha, runs.Value);
    }

    private static ChecksSummary Summarise(string sha, IReadOnlyList<JsonElement> runs)
    {
        string[] bad = ["failure", "timed_out", "cancelled", "action_required", "startup_failure"];
        var failing = runs.Where(r => bad.Contains(r.Str("conclusion"))).Select(r => r.Str("name")).ToList();
        var state = runs.Count == 0 ? ChecksState.None
            : failing.Count > 0 ? ChecksState.Failing
            : runs.Any(r => r.Str("status") != "completed") ? ChecksState.Pending
            : ChecksState.Passing;
        return new ChecksSummary(state, sha, failing);
    }

    private async ValueTask<Result<string?>> ReviewDecision(ForgeRef pull, CancellationToken cancellationToken)
    {
        var reviews = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/reviews"), cancellationToken).ConfigureAwait(false);
        if (reviews.IsError)
        {
            return reviews.Error;
        }

        var latest = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var review in reviews.Value.Where(r => IsDecision(r.Str("state"))))
        {
            latest[review.Str("user", "login")] = review.Str("state");
        }

        return latest.ContainsValue("CHANGES_REQUESTED") ? "CHANGES_REQUESTED"
            : latest.ContainsValue("APPROVED") ? "APPROVED"
            : null;
    }

    private static PullSnapshot Snapshot(ForgeRef pull, JsonElement json, ChecksSummary checks, string? decision)
    {
        var head = json.At("head") ?? default;
        var baseBranch = json.At("base") ?? default;
        var status = json.Flag("merged") ? PullStatus.Merged : json.Str("state") == "closed" ? PullStatus.Closed : PullStatus.Open;
        var fork = head.At("repo") is not { } repo || repo.Str("full_name") != (baseBranch.At("repo") ?? default).Str("full_name");
        var isDefault = (baseBranch.At("repo") ?? default).Str("default_branch") == baseBranch.Str("ref");
        return new PullSnapshot(
            pull,
            json.Str("html_url"),
            json.Str("title"),
            json.Str("user", "login"),
            status,
            json.Flag("draft"),
            new BaseBranch(baseBranch.Str("ref"), baseBranch.Str("sha"), isDefault),
            new HeadBranch(head.Str("ref"), head.Str("sha"), fork),
            json.NullableFlag("mergeable"),
            decision,
            checks);
    }

    private static PullDigest Digest(ForgeRef pull, JsonElement json, ChecksSummary checks)
    {
        var status = json.Flag("merged") ? PullStatus.Merged : json.Str("state") == "closed" ? PullStatus.Closed : PullStatus.Open;
        var head = json.At("head")?.Str("sha") ?? string.Empty;
        var count = (int)(json.Long("comments") + json.Long("review_comments"));
        return new PullDigest(pull, status, head, checks.State, count, Inv.S($"{json.Str("updated_at")}|{head}|{checks.State}|{count}"));
    }
}
