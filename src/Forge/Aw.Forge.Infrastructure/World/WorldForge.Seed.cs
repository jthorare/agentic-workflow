using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.World;

/// <summary>Seeding, snapshots and the reply and reaction rules.</summary>
internal partial class WorldForge
{
    public Result<string> SeedThread(SeedThreadRequest request) =>
        Guard(request.Pull.Host) is { } refused
            ? refused
            : Write(request.Pull, (_, p) =>
            {
                var key = request.Kind.ToString().ToLowerInvariant() + ":" + p.NextComment++.ToString(System.Globalization.CultureInfo.InvariantCulture);
                p.Threads.Add(new WorldThread
                {
                    Key = key,
                    Kind = request.Kind,
                    Author = request.Author,
                    Association = request.Association,
                    IsBot = request.IsBot,
                    Path = request.Path,
                    Body = request.Body,
                    CreatedAt = request.CreatedAt ?? _clock.GetUtcNow(),
                    Line = request.Line,
                    EditedAt = request.EditedAt,
                });
                return Results.Success(key);
            });

    public Result<Unit> SeedChecks(SeedChecksRequest request) =>
        Guard(request.Pull.Host) is { } refused
            ? refused
            : Write(request.Pull, (_, p) =>
            {
                p.Checks = request.State;
                p.Failing = [.. request.Failing];
                p.Logs = new Dictionary<string, string>(request.Logs, StringComparer.Ordinal);
                return Results.Ok();
            });

    public Result<Unit> AdvanceHead(ForgeRef pull, string sha) =>
        Guard(pull.Host) is { } refused
            ? refused
            : Write(pull, (state, p) =>
            {
                if (p.HeadRemote is not null)
                {
                    state.Tips[Tip(p.HeadRemote, p.HeadRef)] = sha;
                }

                p.HeadSha = sha;
                p.ChecksSha = sha;
                p.Checks = ChecksState.None;
                p.Failing = [];
                return Results.Ok();
            });

    private Result<PullSnapshot> Snapshot(WorldState state, WorldPull p)
    {
        var isDefault = (state.Repos.FirstOrDefault(r => r.Key == Key(p))?.DefaultBranch ?? "main") == p.BaseRef;
        var pull = new ForgeRef(kind, p.Host, p.Owner, p.Repo, p.Number);
        return new PullSnapshot(
            pull,
            pull.Link,
            p.Title,
            p.Author,
            p.Status,
            p.Draft,
            new BaseBranch(p.BaseRef, p.BaseSha, isDefault),
            new HeadBranch(p.HeadRef, p.HeadSha, p.Fork),
            p.Mergeable,
            p.ReviewDecision,
            new ChecksSummary(p.Checks, p.ChecksSha, [.. p.Failing]),
            [.. p.Labels]);
    }

    private static PullDigest Digest(ForgeRef pull, WorldPull p) =>
        new(pull, p.Status, p.HeadSha, p.Checks, p.Threads.Sum(t => 1 + t.Replies.Count), p.Stamp.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string Key(WorldPull p) => new ForgeRepo(p.Host, p.Owner, p.Repo).Key;

    private static ForgeThread ToThread(ForgeKind kind, WorldPull p, WorldThread t) =>
        new(t.Key, t.Kind, t.Author, t.Association, t.IsBot, t.Resolved, t.Outdated, t.Path, t.Body,
            [.. ForgeMarker.Of(t.Body).Concat(t.Replies.SelectMany(r => ForgeMarker.Of(r)))],
            t.CreatedAt, t.Line, t.EditedAt, new ForgeRef(kind, p.Host, p.Owner, p.Repo, p.Number).Link + "#" + t.Key);

    private string Excerpt(string log)
    {
        var tail = log.Length > ForgeLimits.LogExcerptCharacters ? log[^ForgeLimits.LogExcerptCharacters..] : log;
        return redactor.Scrub(tail).Text;
    }

    private Result<Delivery> ReplyTo(WorldPull p, ReplyRequest request)
    {
        if (request.ThreadKey == "pull")
        {
            if (p.Threads.Exists(t => t.Kind == CommentKind.Comment && ForgeMarker.IsIn(t.Body, request.Marker)))
            {
                return Delivery.AlreadyDelivered;
            }

            p.Threads.Add(new WorldThread
            {
                Key = "comment:" + p.NextComment++.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Kind = CommentKind.Comment,
                Author = actingLogin,
                Association = "OWNER",
                Body = ForgeMarker.Append(request.Text, request.Marker),
                CreatedAt = _clock.GetUtcNow(),
            });
            return Delivery.Delivered;
        }

        if (p.Threads.FirstOrDefault(t => t.Key == request.ThreadKey) is not { } thread)
        {
            return ForgeErrors.NotFound.With($"no thread '{request.ThreadKey}'");
        }

        if (ForgeMarker.IsIn(thread.Body, request.Marker) || thread.Replies.Exists(r => ForgeMarker.IsIn(r, request.Marker)))
        {
            return Delivery.AlreadyDelivered;
        }

        thread.Replies.Add(ForgeMarker.Append(request.Text, request.Marker));
        return Delivery.Delivered;
    }

    private static readonly HashSet<string> _reactions = new(["+1", "-1", "laugh", "confused", "heart", "hooray", "rocket", "eyes"], StringComparer.Ordinal);

    private static Result<Delivery> ReactTo(WorldPull p, ReactionRequest request)
    {
        if (!_reactions.Contains(request.Reaction))
        {
            return ForgeErrors.Invalid.With("reaction: +1, -1, laugh, confused, heart, hooray, rocket or eyes");
        }

        if (p.Threads.FirstOrDefault(t => t.Key == request.ThreadKey) is not { } thread)
        {
            return ForgeErrors.NotFound.With($"no thread '{request.ThreadKey}'");
        }

        var entry = request.Reaction + ":" + request.Marker;
        if (thread.Reactions.Contains(entry))
        {
            return Delivery.AlreadyDelivered;
        }

        thread.Reactions.Add(entry);
        return Delivery.Delivered;
    }
}
