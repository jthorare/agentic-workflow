using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The <see cref="ILinearLiveReads"/> over the read client (Linear hub spec 2026-11-04 D6, §5.5): one request per call with the read key, never the store. 'Entity not found' (SND-CONTEXT-073) is a missing record, so null;
/// every other failure of the client passes through. An issue outside the profile's teams counts as missing, as it does for the feed.
/// </summary>
/// <param name="linear">The factory of Linear's parts.</param>
internal sealed class LinearLiveReads(LinearSources linear) : ILinearLiveReads
{
    /// <inheritdoc />
    public async ValueTask<Result<LinearIssueView?>> Issue(LinearSettings settings, string idOrKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrKey);
        var data = await Read(settings, "LiveIssue", idOrKey, new LinearIssueData(), cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        if (data.Value.Issue is not { } issue || !Listed(settings, issue))
        {
            return Results.Success<LinearIssueView?>(value: null);
        }

        return LinearIssueViews.Of(issue) is { } view
            ? Results.Success<LinearIssueView?>(view)
            : ContextErrors.LinearUnexpectedReply.With($"Linear answered issue {issue.Id} without a usable key, state or team; the live-read query may need updating.");
    }

    /// <inheritdoc />
    public async ValueTask<Result<LinearCommentView?>> Comment(LinearSettings settings, ExternalId commentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var data = await Read(settings, "LiveComment", commentId.Value, new LinearCommentData(), cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        return Results.Success<LinearCommentView?>(data.Value.Comment is { Issue: { } issue } comment
            ? new LinearCommentView(ExternalId.From(comment.Id), ExternalId.From(issue.Id), comment.Body)
            : null);
    }

    /// <inheritdoc />
    public async ValueTask<Result<bool>> HasAttachment(LinearSettings settings, ExternalId issueId, Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(url);
        var data = await Read(settings, "LiveAttachments", issueId.Value, new LinearIssueData(), cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        return Results.Success(data.Value.Issue?.Attachments?.Nodes.Any(a => Same(a.Url, url)) == true);
    }

    // why: Linear may hand the link back normalized, so the comparison is on the absolute form too
    private static bool Same(string stored, Uri url) =>
        string.Equals(stored, url.OriginalString, StringComparison.Ordinal) || (Uri.TryCreate(stored, UriKind.Absolute, out var parsed) && parsed.Equals(url));

    private static bool Listed(LinearSettings settings, LinearIssue issue) =>
        settings.Teams.Count == 0 || (issue.Team?.Key is { } key && settings.Teams.Contains(key, StringComparer.Ordinal));

    // why: Linear answers a missing record with a GraphQL error, which the client maps to 073; here that is an answer ('none'), not a failure
    private async ValueTask<Result<T>> Read<T>(LinearSettings settings, string operation, string id, T none, CancellationToken cancellationToken)
    {
        var data = await linear.CreateClient(settings).Query<T>(operation, LinearFilters.ById(id), linear.Deadline(), cancellationToken).ConfigureAwait(false);
        return data.IsError && data.Error.Code == ContextErrors.LinearRecordNotFound.Code ? none : data;
    }
}
