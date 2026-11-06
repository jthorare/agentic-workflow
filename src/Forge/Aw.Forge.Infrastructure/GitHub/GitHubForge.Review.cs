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
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
        var capped = all.Count >= ForgeLimits.PullFileCap;
        var more = pages.Value.More || all.Count > ForgeLimits.PullFileCap;
        if (capped && !more)
        {
            // why: exactly 3000 files listed and no next page; the pull's own count says whether GitHub holds more than it lists.
            var state = await ReadPull(pull, cancellationToken).ConfigureAwait(false);
            if (state.IsError)
            {
                return state.Error;
            }

            more = state.Value.Long("changed_files") > ForgeLimits.PullFileCap;
        }

        return new PullFiles(files, CapReached: capped && more);
    }

    public async ValueTask<Result<string>> FileText(ForgeRef pull, string path, string sha, CancellationToken cancellationToken)
    {
        if (_api.Guard(pull.Host) is { } refused)
        {
            return refused;
        }

        var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        var reply = await _api.CallRaw(pull.Host, $"{RepoPath(pull)}/contents/{escaped}?ref={Uri.EscapeDataString(sha)}", cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (!reply.Value.Ok)
        {
            return GitHubApi.Fail(reply.Value);
        }

        return Decode(reply.Value, path, sha);
    }

    // why: a file comes back as raw bytes; a JSON answer that is a file object (base64) is read too. Text that is not UTF-8 (a binary file) is not text: not found, as in the world.
    private static Result<string> Decode(GitHubReply reply, string path, string sha)
    {
        var bytes = reply.Raw;
        if (bytes is null)
        {
            if (reply.Body.Str("type") != "file" || reply.Body.Str("encoding") != "base64")
            {
                return ForgeErrors.NotFound.With($"'{path}' at {sha} is not a file with text");
            }

            try
            {
                bytes = Convert.FromBase64String(reply.Body.Str("content"));
            }
            catch (FormatException)
            {
                return ForgeErrors.NotFound.With($"'{path}' at {sha} has no readable text");
            }
        }

        try
        {
            return TextCap.Cut(_strictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return ForgeErrors.NotFound.With($"'{path}' at {sha} is not UTF-8 text");
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

        var reviews = await _api.All(pull.Host, Inv.S($"{RepoPath(pull)}/pulls/{pull.Number}/reviews"), cancellationToken).ConfigureAwait(false);
        if (reviews.IsError)
        {
            return reviews.Error;
        }

        if (reviews.Value.Any(r => ForgeMarker.IsIn(r.Str("body"), request.Marker)))
        {
            return ReviewOutcome.AlreadyDelivered;
        }

        var state = await ReadPull(pull, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        return state.Value.At("head")?.Str("sha") != request.ExpectedHead
            ? ReviewOutcome.HeadMoved
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
            : posted.Value.Status == 422 && request.Comments.Count > 0 && NamesALine(posted.Value.Body) ? ReviewOutcome.LineRejected
            : posted.Value.Status == 422 ? Unprocessable(posted.Value.Body)
            : GitHubApi.Fail(posted.Value);
    }

    // why: GitHub puts the reason of a 422 in "errors" (the "message" is just "Unprocessable Entity"), so the error carries both.
    private static Error Unprocessable(JsonElement body) =>
        ForgeErrors.Rejected.With(Inv.S($"422: {body.Str("message")} {(body.At("errors") is { } errors ? errors.GetRawText() : string.Empty)}").TrimEnd());

    // why: a 422 on a review has many causes (approving your own pull request, a stale commit_id, an empty body on REQUEST_CHANGES); only the ones about a comment's line or path mean "re-anchor the comments".
    private static bool NamesALine(JsonElement body)
    {
        string[] markers = ["could not be resolved", "must be part of the diff", "pull_request_review_thread.line", "path"];
        var text = body.Str("message") + " " + (body.At("errors") is { } errors ? errors.GetRawText() : string.Empty);
        return markers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
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
