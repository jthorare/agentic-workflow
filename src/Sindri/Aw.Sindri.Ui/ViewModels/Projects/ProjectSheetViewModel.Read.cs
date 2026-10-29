using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The sheet's read and what it draws (<see cref="ProjectSheetViewModel"/>, continued).</summary>
internal sealed partial class ProjectSheetViewModel
{
    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Id.Length == 0)
        {
            return;
        }

        var generation = ++_generation;
        var answer = await Api.Project(url, Id, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another project, took over
        }

        if (answer.IsSuccess)
        {
            Draw(answer.Value);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    // The owner's own links: a led or attached card's first link is Linear's.
    private static IEnumerable<string> OwnerLinks(ProjectCardView card) =>
        card.Links.Skip(string.Equals(card.Origin, "handMade", StringComparison.Ordinal) ? 0 : 1).Select(l => l.Url);

    private void Draw(ProjectOne one)
    {
        var card = one.Project;
        _one = one;
        Heading = card.Name;
        OriginChip = Labels.ProjectOriginChip(card.Origin);
        StateText = Labels.ProjectStateWord(card.State) + (card.StateSetByOwner ? " · " + Labels.ProjectSetByYou : string.Empty);
        HealthWord = Labels.ProjectHealthWord(card.Health);
        HealthTone = HealthWord is null ? ChipTone.Neutral : card.Health switch
        {
            "onTrack" => ChipTone.Ok,
            "atRisk" => ChipTone.Attn,
            _ => ChipTone.Fail,
        };
        DrawFacts(card);
        Description = string.IsNullOrWhiteSpace(card.Description) ? null : card.Description;
        Notes = string.IsNullOrWhiteSpace(card.Notes) ? null : card.Notes;
        Links = [.. card.Links.Select(Link)];
        Repos = [.. card.Repos.Select(Repo)];
        SelectedState = StateChoices.FirstOrDefault(s => string.Equals(s.Value, card.State, StringComparison.Ordinal));
        DrawActions(card, one.Hidden);
        (ProblemText, Note) = (string.Empty, string.Empty);
        Phase = FramePhase.Shown;
    }

    private void DrawFacts(ProjectCardView card)
    {
        LeadText = card.Lead is { } lead ? Labels.ProjectLead(lead.You, lead.Name) : null;
        TargetText = card.Target is { } target ? Labels.ProjectTarget(target) : null;
        IssuesText = card.Counting ? Labels.ProjectIssuesCounting : card.OpenIssues is { } issues ? Labels.ProjectOpenIssues(issues) : null;
        PrsText = card.OpenPrs is { } prs ? Labels.ProjectOpenPrs(prs) + (card.PrScope is { Length: > 0 } scope ? " " + scope : string.Empty) : null;
        ActivityText = card.LastActivity is { } at ? Labels.ProjectActiveOn(_clock.GetUtcNow() - at) : null;
        FinishedText = card.FinishedAt is { } finished ? Labels.ProjectFinishedOn(card.State, finished) : null;
    }

    // What the origin allows: a led card only hides, an attached or hand-made one edits, sets its state and is removed, a hidden one only comes back.
    private void DrawActions(ProjectCardView card, bool hidden)
    {
        var attached = string.Equals(card.Origin, "attached", StringComparison.Ordinal);
        var handMade = string.Equals(card.Origin, "handMade", StringComparison.Ordinal);
        var owned = !hidden && (attached || handMade);
        CanEdit = CanSetState = CanRemove = owned;
        CanEditName = owned && handMade;
        CanClearState = owned && attached && card.StateSetByOwner;
        CanHide = !hidden && string.Equals(card.Origin, "led", StringComparison.Ordinal);
        CanUnhide = hidden;
    }

    private ProjectLinkItem Link(ProjectLinkView link) => new(
        string.Equals(link.Label, "Linear", StringComparison.Ordinal) ? Labels.OpenIn("Linear") : link.Label,
        link.Url,
        MayOpen(link.Label, link.Url),
        Open);

    private ProjectLinkItem Repo(string repo) =>
        IsGitHubName(repo)
            ? new ProjectLinkItem(Labels.ProjectRepoPrs(repo), "https://github.com/" + repo + "/pulls", canOpen: true, Open)
            : new ProjectLinkItem(Labels.ProjectRepoNotSynced(repo), string.Empty, canOpen: false, Open);

    private Task<bool> Open(Uri uri, CancellationToken cancellationToken) => Launcher.OpenUri(uri, cancellationToken);

    // "owner/name", each part letters, digits, '.', '_' or '-'.
    private static bool IsGitHubName(string repo) =>
        repo.Split('/') is [{ Length: > 0 } owner, { Length: > 0 } name] && owner.Concat(name).All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    // Clickable only over https; a link labelled Linear or GitHub only on exactly that host.
    private static bool MayOpen(string label, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || uri.UserInfo.Length > 0)
        {
            return false;
        }

        return label switch
        {
            "Linear" => string.Equals(uri.Host, "linear.app", StringComparison.OrdinalIgnoreCase),
            "GitHub" => string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private void Failed(Error error)
    {
        if (IsShown)
        {
            Note = Labels.ProjectSheetStale(error);
            return;
        }

        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        var unknown = !unreadable && error.Kind is ErrorKind.NotFound or ErrorKind.Invalid;
        ProblemText = unknown ? Labels.ProjectUnknown : unreadable ? Labels.ProjectsUnreadable : Labels.InstanceRefused(error);
        Phase = unknown ? FramePhase.NotFound : unreadable ? FramePhase.Unreadable : FramePhase.Refused;
    }

    private void Clear()
    {
        _one = null;
        (Heading, StateText, ProblemText, Note, SaveError, RemoveQuestion, Announcement) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (OriginChip, HealthWord, LeadText, TargetText, IssuesText, PrsText, ActivityText, FinishedText, Description, Notes) = (null, null, null, null, null, null, null, null, null, null);
        (Links, Repos, SelectedState) = ([], [], null);
        (CanEdit, CanEditName, CanSetState, CanClearState, CanRemove, CanHide, CanUnhide) = (false, false, false, false, false, false, false);
        (IsEditing, IsConflict, IsConfirmingRemove) = (false, false, false);
    }
}
