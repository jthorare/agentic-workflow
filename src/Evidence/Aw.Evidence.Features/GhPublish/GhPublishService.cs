using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Core.Runs;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.GhPublish;

/// <summary>
/// Reads the run, refuses unless its data is seeded and every image was reviewed, commits the images to the PR's evidence branch
/// through gh and writes pr-comment.md.
/// </summary>
internal sealed class GhPublishService(IRunFiles files, IGhRunner gh, JsonSerializerOptions json) : IService<GhPublishRequest, GhPublishResponse>
{
    public async ValueTask<Result<GhPublishResponse>> Handle(GhPublishRequest request, CancellationToken cancellationToken)
    {
        var run = await ReadRunAsync(request, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        var (provenance, images, comment) = run.Value;
        if (provenance != Provenance.Seeded)
        {
            return EvidenceErrors.PublishRefused.With(
                $"the run's data is {Name(provenance)}: only seeded data goes to GitHub (a script run needs --provenance seeded on a local host; a parity run carries its own)");
        }

        var contents = new List<(string Local, byte[] Content)>();
        foreach (var image in images)
        {
            var read = await files.ReadAsync(image, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            contents.Add((image, read.Value));
        }

        if (contents.Count == 0)
        {
            return EvidenceErrors.Invalid.With("the run has no images to publish");
        }

        string[] unreviewed = [.. images.Where(i => !request.Reviewed.Contains(i, StringComparer.Ordinal))];
        if (unreviewed.Length > 0)
        {
            return new GhPublishResponse(provenance, ReviewBlocked: true, unreviewed, Branch: null, Commit: null, new Dictionary<string, string>(StringComparer.Ordinal), CommentFile: null, []);
        }

        return await PushAsync(request, contents, comment, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<GhPublishResponse>> PushAsync(
        GhPublishRequest request, List<(string Local, byte[] Content)> contents, Func<IReadOnlyDictionary<string, string>, string> comment, CancellationToken cancellationToken)
    {
        var runId = RunPath.NameOf(request.RunDirectory);
        var branch = $"ui-evidence/pr-{request.Pr.ToString(CultureInfo.InvariantCulture)}";
        var prefix = $"pr-{request.Pr.ToString(CultureInfo.InvariantCulture)}/{runId}";
        var repoPaths = contents.Select(c => (c.Local, Path: $"{prefix}/{RunPath.NameOf(c.Local)}", c.Content)).ToList();

        var committed = await new EvidenceBranch(gh, request.Repo)
            .CommitAsync(branch, [.. repoPaths.Select(static p => (p.Path, p.Content))], $"ui-evidence: PR #{request.Pr.ToString(CultureInfo.InvariantCulture)} run {runId}", cancellationToken)
            .ConfigureAwait(false);
        if (committed.IsError)
        {
            return committed.Error;
        }

        var commit = committed.Value;
        var urls = repoPaths.ToDictionary(p => p.Local, p => $"https://github.com/{request.Repo}/blob/{commit}/{p.Path}?raw=true", StringComparer.Ordinal);
        var commentFile = RunPath.Combine(request.RunDirectory, "pr-comment.md");
        var written = await files.WriteAsync(commentFile, Encoding.UTF8.GetBytes(comment(urls)), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        string[] post = ["gh", "pr", "comment", request.Pr.ToString(CultureInfo.InvariantCulture), "--repo", request.Repo, "--body-file", commentFile];
        return new GhPublishResponse(Provenance.Seeded, ReviewBlocked: false, [], branch, commit, urls, commentFile, post);
    }

    // The run's provenance, its images in order (each once), and the comment that embeds them.
    private async ValueTask<Result<(Provenance Provenance, string[] Images, Func<IReadOnlyDictionary<string, string>, string> Comment)>> ReadRunAsync(
        GhPublishRequest request, CancellationToken cancellationToken)
    {
        var parity = await files.ReadAsync(RunPath.Combine(request.RunDirectory, ParitySummary.FileName), cancellationToken).ConfigureAwait(false);
        if (parity.IsSuccess)
        {
            return RunFileReader.Parity(parity.Value, json).Map(static s => (
                s.Provenance,
                s.Frames.Select(static f => f.Files.SideBySide).Distinct(StringComparer.Ordinal).ToArray(),
                (Func<IReadOnlyDictionary<string, string>, string>)(urls => Embed(ParityComments.GitHub(s), urls))));
        }

        if (parity.Error.Code != EvidenceErrors.FileNotFound.Code)
        {
            return parity.Error;
        }

        var script = await files.ReadAsync(RunPath.Combine(request.RunDirectory, RunSummary.FileName), cancellationToken).ConfigureAwait(false);
        return script.Bind(b => RunFileReader.Summary(b, json)).Map(s => (
            ClaimedProvenance(s, request.Provenance),
            s.Steps.Select(static x => x.Screenshot).Distinct(StringComparer.Ordinal).ToArray(),
            (Func<IReadOnlyDictionary<string, string>, string>)(urls => Embed(EvidenceComment.Render(s, note: null, urls), urls))));
    }

    // A run on a host other than this machine is never seeded, whatever the caller says.
    private static Provenance ClaimedProvenance(RunSummary summary, DbProvenance? claimed) =>
        ProvenanceRules.Effective(summary.Host, claimed ?? DbProvenance.Unknown) == DbProvenance.Seeded ? Provenance.Seeded : Provenance.Unknown;

    private static string Embed(string body, IReadOnlyDictionary<string, string> urls) =>
        string.Join('\n', [body, string.Empty, .. urls.Select(static u => $"![{RunPath.NameOf(u.Key)}]({u.Value})")]) + "\n";

    private static string Name(Provenance provenance) => provenance switch
    {
        Provenance.Seeded => "seeded",
        Provenance.Scrubbed => "scrubbed",
        Provenance.Unknown => "unknown",
        _ => "unknown",
    };
}
