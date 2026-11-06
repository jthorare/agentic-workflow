using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.World;

/// <summary>The review page's reads and the review post: the files, their texts and SubmitReview.</summary>
internal partial class WorldForge
{
    public ValueTask<Result<PullFiles>> Files(ForgeRef pull, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read(pull, (_, p) => Results.Success(new PullFiles(
            [.. p.Files.Select(f => new PullFile(f.Path, f.PreviousPath, f.Status, f.Additions, f.Deletions, f.Patch))],
            p.FilesCapReached))));

    public ValueTask<Result<string>> FileText(ForgeRef pull, string path, string sha, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read(pull, (_, p) => TextOf(p, path, sha)));

    public ValueTask<Result<ReviewOutcome>> SubmitReview(SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var bad = Guard(request.Pull.Host) ?? Cast(ForgeMarker.Validate(request.Marker));
        return Done(bad ?? Write(request.Pull, (_, p) => Post(p, request)));
    }

    public Result<Unit> SeedFiles(SeedFilesRequest request) =>
        Guard(request.Pull.Host) is { } refused
            ? refused
            : Write(request.Pull, (_, p) =>
            {
                p.Files = [.. request.Files.Select(f => new WorldFile
                {
                    Path = f.File.Path,
                    PreviousPath = f.File.PreviousPath,
                    Status = f.File.Status,
                    Additions = f.File.Additions,
                    Deletions = f.File.Deletions,
                    Patch = f.File.Patch,
                    Text = f.Text,
                    BaseText = f.BaseText,
                })];
                p.FilesCapReached = request.CapReached;
                return Results.Ok();
            });

    // why: the head's text is under the file's path; the base's text is under the path it had before a rename. Any other sha reads as the head.
    private static Result<string> TextOf(WorldPull p, string path, string sha)
    {
        var atBase = sha == p.BaseSha && sha != p.HeadSha;
        var file = atBase
            ? p.Files.FirstOrDefault(f => (f.PreviousPath ?? f.Path) == path)
            : p.Files.FirstOrDefault(f => f.Path == path);
        return (atBase ? file?.BaseText : file?.Text) is { } text
            ? TextCap.Cut(text)
            : ForgeErrors.NotFound.With($"no text for '{path}' at {sha}");
    }

    private Result<ReviewOutcome> Post(WorldPull p, SubmitReviewRequest request)
    {
        if (request.ExpectedHead != p.HeadSha)
        {
            return ReviewOutcome.HeadMoved;
        }

        if (p.Threads.Exists(t => t.Kind == CommentKind.Review && ForgeMarker.IsIn(t.Body, request.Marker)))
        {
            return ReviewOutcome.AlreadyDelivered;
        }

        var now = _clock.GetUtcNow();
        p.Threads.Add(new WorldThread
        {
            Key = "review:" + p.NextComment++.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Kind = CommentKind.Review,
            Author = actingLogin,
            Association = "OWNER",
            Body = ForgeMarker.Append(request.Body, request.Marker),
            CreatedAt = now,
        });
        foreach (var comment in request.Comments)
        {
            p.Threads.Add(new WorldThread
            {
                Key = "thread:" + p.NextComment++.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Kind = CommentKind.Thread,
                Author = actingLogin,
                Association = "OWNER",
                Path = comment.Path,
                Line = comment.Line,
                Body = comment.Body,
                CreatedAt = now,
            });
        }

        return ReviewOutcome.Posted;
    }
}
