using System.Text;
using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Text;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.GitHub;

internal sealed partial class GitHubForge
{
    public async ValueTask<Result<PullFiles>> Files(ForgeRef pull, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var pages = await _api.Pages(
            pull.Host,
            Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/files?per_page=100"),
            ForgeLimits.PullFileCap / 100,
            replies => replies.Sum(r => r.Body.Items().Count()) >= ForgeLimits.PullFileCap,
            cancellationToken).ConfigureAwait(false);
        if (pages.IsError)
        {
            return pages.Error;
        }

        var all = pages.Value.Replies.SelectMany(r => r.Body.Items()).ToList();
        var files = all.Take(ForgeLimits.PullFileCap).Select(ToFile).ToList();
        return new PullFiles(files, CapReached: all.Count > ForgeLimits.PullFileCap || (pages.Value.More && all.Count >= ForgeLimits.PullFileCap));
    }

    public async ValueTask<Result<string>> FileText(ForgeRef pull, string path, string sha, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        var reply = await _api.Call(pull.Host, HttpMethod.Get, $"{RepoPath(pull)}/contents/{escaped}?ref={Uri.EscapeDataString(sha)}", body: null, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (!reply.Value.Ok)
        {
            return GitHubApi.Fail(reply.Value);
        }

        var body = reply.Value.Body;
        if (body.Str("type") != "file" || body.Str("encoding") != "base64")
        {
            return ForgeErrors.NotFound.With($"'{path}' at {sha} is not a file with text");
        }

        try
        {
            return TextCap.Cut(Encoding.UTF8.GetString(Convert.FromBase64String(body.Str("content"))));
        }
        catch (FormatException)
        {
            return ForgeErrors.NotFound.With($"'{path}' at {sha} has no readable text");
        }
    }

    public async ValueTask<Result<ReviewOutcome>> SubmitReview(SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var pull = request.Pull;
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var valid = ForgeMarker.Validate(request.Marker);
        if (valid.IsError)
        {
            return valid.Error;
        }

        var state = await ReadPull(pull, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        if (state.Value.At("head")?.Str("sha") != request.ExpectedHead)
        {
            return ReviewOutcome.HeadMoved;
        }

        var reviews = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/reviews"), cancellationToken).ConfigureAwait(false);
        if (reviews.IsError)
        {
            return reviews.Error;
        }

        return reviews.Value.Any(r => ForgeMarker.IsIn(r.Str("body"), request.Marker))
            ? ReviewOutcome.AlreadyDelivered
            : await PostReview(request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ReviewOutcome>> PostReview(SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var pull = request.Pull;
        var posted = await _api.Call(pull.Host, HttpMethod.Post, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/reviews"), ReviewBody(request), cancellationToken).ConfigureAwait(false);
        if (posted.IsError)
        {
            return posted.Error;
        }

        return posted.Value.Ok ? ReviewOutcome.Posted
            : posted.Value.Status == 422 ? ReviewOutcome.LineRejected
            : GitHubApi.Fail(posted.Value);
    }

    private static byte[] ReviewBody(SubmitReviewRequest request) =>
        J.Object(w =>
        {
            w.WriteString("commit_id", request.ExpectedHead);
            w.WriteString("event", request.Verdict switch
            {
                ReviewVerdict.Approve => "APPROVE",
                ReviewVerdict.RequestChanges => "REQUEST_CHANGES",
                ReviewVerdict.Comment or _ => "COMMENT",
            });
            w.WriteString("body", ForgeMarker.Append(request.Body, request.Marker));
            if (request.Comments.Count > 0)
            {
                w.WriteStartArray("comments");
                foreach (var comment in request.Comments)
                {
                    w.WriteStartObject();
                    w.WriteString("path", comment.Path);
                    w.WriteNumber("line", comment.Line);
                    w.WriteString("side", "RIGHT");
                    w.WriteString("body", comment.Body);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }
        });

    private static PullFile ToFile(JsonElement json) =>
        new(
            json.Str("filename"),
            json.At("previous_filename") is { } previous ? previous.GetString() : null,
            json.Str("status") switch
            {
                "added" => PullFileStatus.Added,
                "modified" => PullFileStatus.Modified,
                "removed" => PullFileStatus.Removed,
                "renamed" => PullFileStatus.Renamed,
                "copied" => PullFileStatus.Copied,
                "unchanged" => PullFileStatus.Unchanged,
                _ => PullFileStatus.Changed,
            },
            (int)json.Long("additions"),
            (int)json.Long("deletions"),
            json.At("patch") is { ValueKind: JsonValueKind.String } patch ? patch.GetString() : null);
}
