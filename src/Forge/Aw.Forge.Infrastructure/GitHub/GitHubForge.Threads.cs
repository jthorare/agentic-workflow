using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.GitHub;

internal sealed partial class GitHubForge
{
    private const string ThreadsQuery =
        "query($o:String!,$r:String!,$n:Int!){repository(owner:$o,name:$r){pullRequest(number:$n){reviewThreads(first:100){nodes{isResolved isOutdated comments(first:1){nodes{databaseId}}}}}}}";

    public async ValueTask<Result<IReadOnlyList<ForgeThread>>> ListThreads(ForgeRef pull, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var inline = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/comments"), cancellationToken).ConfigureAwait(false);
        if (inline.IsError)
        {
            return inline.Error;
        }

        var reviews = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/reviews"), cancellationToken).ConfigureAwait(false);
        if (reviews.IsError)
        {
            return reviews.Error;
        }

        var comments = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/issues/{pull.Number}/comments"), cancellationToken).ConfigureAwait(false);
        if (comments.IsError)
        {
            return comments.Error;
        }

        var flags = await ThreadFlags(pull, cancellationToken).ConfigureAwait(false);
        if (flags.IsError)
        {
            return flags.Error;
        }

        return Results.Success(Assemble(inline.Value, reviews.Value, comments.Value, flags.Value));
    }

    public async ValueTask<Result<Delivery>> Reply(ReplyRequest request, CancellationToken cancellationToken)
    {
        var pull = request.Pull;
        var bad = ForgeMarker.Validate(request.Marker);
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        if (bad.IsError)
        {
            return bad.Error;
        }

        var inline = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/comments"), cancellationToken).ConfigureAwait(false);
        if (inline.IsError)
        {
            return inline.Error;
        }

        var issue = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/issues/{pull.Number}/comments"), cancellationToken).ConfigureAwait(false);
        if (issue.IsError)
        {
            return issue.Error;
        }

        if (inline.Value.Concat(issue.Value).Any(c => ForgeMarker.IsIn(c.Str("body"), request.Marker)))
        {
            return Delivery.AlreadyDelivered;
        }

        var body = J.Object(w => w.WriteString("body", ForgeMarker.Append(request.Text, request.Marker)));
        var (kind, number) = SplitKey(request.ThreadKey);
        var path = kind == "thread"
            ? Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/comments/{number}/replies")
            : Inv.S($"{RepoPath(pull)}/issues/{pull.Number}/comments");
        return await Post(pull.Host, path, body, success: Delivery.Delivered, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<Delivery>> React(ReactionRequest request, CancellationToken cancellationToken)
    {
        var pull = request.Pull;
        var bad = ForgeMarker.Validate(request.Marker);
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        if (bad.IsError)
        {
            return bad.Error;
        }

        var (kind, number) = SplitKey(request.ThreadKey);
        var path = kind switch
        {
            "thread" => Inv.S($"{RepoPath(pull)}/pulls/comments/{number}/reactions"),
            "comment" => Inv.S($"{RepoPath(pull)}/issues/comments/{number}/reactions"),
            _ => null,
        };
        if (path is null || number.Length == 0)
        {
            return ForgeErrors.Invalid.With("threadKey: thread:N or comment:N (a review body takes no reaction)");
        }

        var reply = await _api.Call(pull.Host, HttpMethod.Post, path, J.Object(w => w.WriteString("content", request.Reaction)), cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Status switch
        {
            201 => Delivery.Delivered,
            200 => Delivery.AlreadyDelivered,
            _ => GitHubApi.Fail(reply.Value),
        };
    }

    private static (string Kind, string Number) SplitKey(string key)
    {
        var colon = key.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (key, string.Empty) : (key[..colon], key[(colon + 1)..]);
    }

    private async ValueTask<Result<Delivery>> Post(string host, string path, byte[] body, Delivery success, CancellationToken cancellationToken)
    {
        var reply = await _api.Call(host, HttpMethod.Post, path, body, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return reply.Value.Ok ? success : GitHubApi.Fail(reply.Value);
    }

    private async ValueTask<Result<Dictionary<long, (bool Resolved, bool Outdated)>>> ThreadFlags(ForgeRef pull, CancellationToken cancellationToken)
    {
        var body = J.Object(w =>
        {
            w.WriteString("query", ThreadsQuery);
            w.WriteStartObject("variables");
            w.WriteString("o", pull.Owner);
            w.WriteString("r", pull.Repo);
            w.WriteNumber("n", pull.Number);
            w.WriteEndObject();
        });
        var reply = await _api.Query(pull.Host, body, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (!reply.Value.Ok || reply.Value.Body.At("errors") is not null)
        {
            return ForgeErrors.Rejected.With("the thread flags could not be read");
        }

        return ParseFlags(reply.Value.Body);
    }

    private static Dictionary<long, (bool Resolved, bool Outdated)> ParseFlags(JsonElement body)
    {
        var flags = new Dictionary<long, (bool Resolved, bool Outdated)>();
        var nodes = body.At("data")?.At("repository")?.At("pullRequest")?.At("reviewThreads")?.At("nodes") ?? default;
        foreach (var node in nodes.Items())
        {
            var first = (node.At("comments")?.At("nodes") ?? default).Items().FirstOrDefault();
            flags[first.Long("databaseId")] = (node.Flag("isResolved"), node.Flag("isOutdated"));
        }

        return flags;
    }

    private static IReadOnlyList<ForgeThread> Assemble(
        IReadOnlyList<JsonElement> inline,
        IReadOnlyList<JsonElement> reviews,
        IReadOnlyList<JsonElement> comments,
        Dictionary<long, (bool Resolved, bool Outdated)> flags)
    {
        var replies = inline.Where(c => c.At("in_reply_to_id") is not null).ToLookup(c => c.Long("in_reply_to_id"));
        var items = new List<(string When, ForgeThread Thread)>();
        foreach (var root in inline.Where(c => c.At("in_reply_to_id") is null))
        {
            var id = root.Long("id");
            var (resolved, outdated) = flags.GetValueOrDefault(id);
            var markers = ForgeMarker.Of(root.Str("body")).Concat(replies[id].SelectMany(r => ForgeMarker.Of(r.Str("body")))).ToList();
            items.Add((root.Str("created_at"), Thread(new ThreadParts(Inv.S($"thread:{id}"), CommentKind.Thread, root, resolved, outdated || root.At("position") is null, root.Str("path"), markers))));
        }

        foreach (var review in reviews.Where(r => r.Str("body").Length > 0))
        {
            items.Add((review.Str("submitted_at"), Thread(new ThreadParts(Inv.S($"review:{review.Long("id")}"), CommentKind.Review, review, Resolved: false, Outdated: false, Path: null, [.. ForgeMarker.Of(review.Str("body"))]))));
        }

        foreach (var comment in comments)
        {
            items.Add((comment.Str("created_at"), Thread(new ThreadParts(Inv.S($"comment:{comment.Long("id")}"), CommentKind.Comment, comment, Resolved: false, Outdated: false, Path: null, [.. ForgeMarker.Of(comment.Str("body"))]))));
        }

        return [.. items.OrderBy(i => i.When, StringComparer.Ordinal).Select(i => i.Thread)];
    }

    private sealed record ThreadParts(string Key, CommentKind Kind, JsonElement Json, bool Resolved, bool Outdated, string? Path, List<string> Markers);

    private static ForgeThread Thread(ThreadParts parts)
    {
        var (key, kind, json, resolved, outdated, path, markers) = parts;
        var user = json.At("user") ?? default;
        var isBot = user.Str("type") == "Bot" || user.Str("login").EndsWith("[bot]", StringComparison.Ordinal);
        return new ForgeThread(key, kind, user.Str("login"), json.Str("author_association"), isBot, resolved, outdated, path is { Length: > 0 } ? path : null, json.Str("body"), markers);
    }
}
