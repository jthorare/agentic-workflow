using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The sheet's read and what it draws (<see cref="LinearIssueSheetViewModel"/>, continued).</summary>
internal sealed partial class LinearIssueSheetViewModel
{
    private const string NotSyncedCode = "SND-LINEAR-005";

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Key.Length == 0)
        {
            return;
        }

        var generation = ++_generation;
        var answer = await Api.LinearIssue(url, Key, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another issue, took over
        }

        if (answer.IsSuccess)
        {
            // The owner's edits, a save out or a 409 on screen stay as they are: a refresh must not replace what they are typing.
            if (!(IsShown && (HasChanges || IsLocked || HasConflict)))
            {
                Draw(answer.Value, keepEdits: false);
            }

            await LoadLookups(answer.Value, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    private void Draw(LinearIssueOne issue, bool keepEdits)
    {
        Title = issue.Title;
        Fields =
        [
            Field("state", issue.State.Name),
            Field("priority", Labels.LinearPriority(issue.Priority)),
            Field("assignee", issue.Assignee?.Name ?? Labels.LinearUnassigned),
            Field("cycle", issue.Cycle?.Name),
            Field("labels", issue.Labels.Count > 0 ? string.Join(", ", issue.Labels.Select(l => l.Name)) : null),
            Field("project", issue.Project?.Name),
        ];
        (PrText, _pr) = issue.Pr is { } pr ? (Labels.LinearPrChip(pr.Number, pr.Review), GitHubPr(pr.Url)) : (string.Empty, null);
        CanOpenPr = _pr is not null;
        Description = string.IsNullOrWhiteSpace(issue.Description) ? [] : LinearText.Lines(issue.Description, Open);
        var now = _clock.GetUtcNow();
        Comments = [.. issue.Comments.Select(c => new LinearCommentViewModel(c.Author.Name, Labels.AgoWords(now - c.CreatedAt), LinearText.Lines(c.Body, Open), c.Sindri))];
        RemovedNote = issue.Removed ? Labels.LinearIssueRemoved(issue.Key) : string.Empty;
        (ProblemText, Note) = (string.Empty, string.Empty);
        DrawEdit(issue, keepEdits);
        Phase = FramePhase.Shown;
    }

    private static LinearIssueFieldViewModel Field(string field, string? value) => new(Labels.LinearIssueField(field), value ?? Labels.LinearFieldNone);

    // https://github.com/owner/repo/pull/N is a PR the Reviews tab can open; any other address is shown as text.
    private static (string Repo, int Number)? GitHubPr(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) is [var owner, var repo, "pull", var number]
        && int.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0
            ? (owner + "/" + repo, n)
            : null;

    private Task<bool> Open(Uri uri, CancellationToken cancellationToken) => LinkLaunch.Open(uri, cancellationToken);

    private void Failed(Error error)
    {
        var notSynced = string.Equals(error.Code, NotSyncedCode, StringComparison.Ordinal);
        if (IsShown && (!notSynced || HasChanges || IsLocked))
        {
            // The fields stay: they are the last read's. A not-synced answer only says Sindri lost the issue; typed edits or a save out are not thrown away for it.
            Note = notSynced ? string.Empty : Labels.LinearSindriDown;
            return;
        }

        Clear();
        _linearAddress = notSynced ? LinearAddress(error) : null;
        ProblemText = notSynced ? Labels.LinearIssueNotSynced(Key) : Labels.LinearSindriDown;
        Phase = notSynced ? FramePhase.NotFound : FramePhase.Refused;
        OnChanged(nameof(CanOpenInLinear));
    }

    private void Clear()
    {
        (Title, ProblemText, Note, RemovedNote, PrText) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (Fields, Description, Comments, _pr, CanOpenPr) = ([], [], [], null, false);
        _linearAddress = null;
        ClearEdit();
    }

    // The 404's url (Ph3 reads contract (e)): https on linear.app or a subdomain of it, judged by the punycode host so a
    // lookalike can't pass, and no user name; anything else, or none, offers no button.
    private static Uri? LinearAddress(Error error) =>
        error.Extensions?.FirstOrDefault(m => string.Equals(m.Key, "url", StringComparison.Ordinal)).Value is { ValueKind: System.Text.Json.JsonValueKind.String } url
        && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && uri.UserInfo.Length == 0
        && (string.Equals(uri.IdnHost, "linear.app", StringComparison.OrdinalIgnoreCase) || uri.IdnHost.EndsWith(".linear.app", StringComparison.OrdinalIgnoreCase))
            ? uri
            : null;
}
