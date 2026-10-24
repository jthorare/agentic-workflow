using System.Text;
using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Publishing;
using Aw.Kernel;

namespace Aw.Evidence.Features.GhPublish;

/// <summary>
/// Writes one commit onto a repository branch through the git data API (blob, tree, commit, ref) with gh: no checkout, no working tree,
/// never a force push. The commit goes on top of the branch when it exists; the branch is created when it does not.
/// </summary>
internal sealed class EvidenceBranch(IGhRunner gh, string repo)
{
    private static readonly string[] _post = ["--method", "POST"];

    /// <summary>Commits <paramref name="files"/> onto <paramref name="branch"/>.</summary>
    /// <param name="branch">The branch, e.g. ui-evidence/pr-7.</param>
    /// <param name="files">Repository path to content.</param>
    /// <param name="message">The commit message.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>The new commit's sha, AW-EVIDENCE-025 when gh answered an error, or AW-EVIDENCE-015 when it would not start.</returns>
    public async ValueTask<Result<string>> CommitAsync(string branch, IReadOnlyList<(string Path, byte[] Content)> files, string message, CancellationToken cancellationToken)
    {
        var parent = await ParentAsync(branch, cancellationToken).ConfigureAwait(false);
        if (parent.IsError)
        {
            return parent.Error;
        }

        var tree = await TreeAsync(parent.Value.Tree, files, cancellationToken).ConfigureAwait(false);
        if (tree.IsError)
        {
            return tree.Error;
        }

        var commit = await CallAsync("commits", _post, Body(w =>
        {
            w.WriteString("message", message);
            w.WriteString("tree", tree.Value);
            w.WriteStartArray("parents");
            if (parent.Value.Commit is { } head)
            {
                w.WriteStringValue(head);
            }

            w.WriteEndArray();
        }), "sha", cancellationToken).ConfigureAwait(false);
        return await commit.BindAsync(sha => MoveBranchAsync(branch, parent.Value.Commit is not null, sha, cancellationToken)).ConfigureAwait(false);
    }

    private string Git(string path) => $"repos/{repo}/git/{path}";

    private async ValueTask<Result<string>> MoveBranchAsync(string branch, bool exists, string sha, CancellationToken cancellationToken)
    {
        var moved = exists
            ? await CallAsync($"refs/heads/{branch}", ["--method", "PATCH"], Body(w => w.WriteString("sha", sha)), answer: null, cancellationToken: cancellationToken).ConfigureAwait(false)
            : await CallAsync("refs", _post, Body(w => { w.WriteString("ref", $"refs/heads/{branch}"); w.WriteString("sha", sha); }), answer: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        return moved.Map(_ => sha);
    }

    // A new tree on top of the branch's (when it exists), holding every file as a fresh blob.
    private async ValueTask<Result<string>> TreeAsync(string? baseTree, IReadOnlyList<(string Path, byte[] Content)> files, CancellationToken cancellationToken)
    {
        var entries = new List<(string Path, string Sha)>();
        foreach (var (path, content) in files)
        {
            var blob = await CallAsync("blobs", _post, Body(w => { w.WriteString("content", Convert.ToBase64String(content)); w.WriteString("encoding", "base64"); }), "sha", cancellationToken).ConfigureAwait(false);
            if (blob.IsError)
            {
                return blob.Error;
            }

            entries.Add((path, blob.Value));
        }

        return await CallAsync("trees", _post, Body(w =>
        {
            if (baseTree is not null)
            {
                w.WriteString("base_tree", baseTree);
            }

            w.WriteStartArray("tree");
            foreach (var (path, sha) in entries)
            {
                w.WriteStartObject();
                w.WriteString("path", path);
                w.WriteString("mode", "100644");
                w.WriteString("type", "blob");
                w.WriteString("sha", sha);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }), "sha", cancellationToken).ConfigureAwait(false);
    }

    // The branch's head commit and that commit's tree; both null when the branch does not exist yet.
    private async ValueTask<Result<(string? Commit, string? Tree)>> ParentAsync(string branch, CancellationToken cancellationToken)
    {
        var ran = await gh.RunAsync(new GhCall(["api", Git($"ref/heads/{branch}")]), cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        if (ran.Value.ExitCode != 0)
        {
            return ran.Value.Stderr.Contains("404", StringComparison.Ordinal) ? (null, null) : Failed("ref", ran.Value);
        }

        var head = Read(ran.Value.Stdout, "object", "sha");
        if (head.IsError)
        {
            return head.Error;
        }

        var commit = await gh.RunAsync(new GhCall(["api", Git($"commits/{head.Value}")]), cancellationToken).ConfigureAwait(false);
        if (commit.IsError)
        {
            return commit.Error;
        }

        return commit.Value.ExitCode != 0
            ? Failed("commits", commit.Value)
            : Read(commit.Value.Stdout, "tree", "sha").Map(tree => ((string?)head.Value, (string?)tree));
    }

    private async ValueTask<Result<string>> CallAsync(string path, string[] method, string body, string? answer, CancellationToken cancellationToken)
    {
        var ran = await gh.RunAsync(new GhCall(["api", .. method, Git(path), "--input", "-"], body), cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        if (ran.Value.ExitCode != 0)
        {
            return Failed(path, ran.Value);
        }

        return answer is null ? string.Empty : Read(ran.Value.Stdout, answer);
    }

    private static Result<string> Read(string json, params string[] pointer)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var element = document.RootElement;
            foreach (var name in pointer)
            {
                element = element.GetProperty(name);
            }

            return element.GetString() is { Length: > 0 } value ? value : EvidenceErrors.GhPublishFailed.With($"gh answered no {string.Join('.', pointer)}");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return EvidenceErrors.GhPublishFailed.With($"gh answered something that is not the git data API's reply ({ex.Message})");
        }
    }

    private static string Body(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Error Failed(string step, GhOutcome outcome) =>
        EvidenceErrors.GhPublishFailed.With($"gh api git/{step} failed: {(outcome.Stderr.Length > 0 ? outcome.Stderr.Trim() : outcome.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture))}");
}
