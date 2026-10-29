using Aw.Kernel;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The sheet's writes (<see cref="ProjectSheetViewModel"/>, continued): each sends the version the sheet read.</summary>
internal sealed partial class ProjectSheetViewModel
{
    /// <summary>Saves the form's fields.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (_one is not { } one || !IsEditing)
        {
            return;
        }

        var card = one.Project;
        var target = EditTarget.Trim();
        var links = EditLinks.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var change = new ProjectChange(
            card.Version,
            Name: CanEditName && !string.Equals(EditName.Trim(), card.Name, StringComparison.Ordinal) ? EditName.Trim() : null,
            Links: links.SequenceEqual(OwnerLinks(card), StringComparer.Ordinal) ? null : links,
            Target: target.Length > 0 && !string.Equals(target, card.Target, StringComparison.Ordinal) ? target : null,
            ClearTarget: target.Length == 0 && card.Target is not null);
        if (await Send((url, ct) => Api.ChangeProject(url, card.Id, change, ct), cancellationToken).ConfigureAwait(true))
        {
            await Saved(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Sets the owner's state to the picked one.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task SetState(CancellationToken cancellationToken)
    {
        if (_one is not { } one || SelectedState is not { } state)
        {
            return;
        }

        var change = new ProjectChange(one.Project.Version, State: state.Value);
        if (await Send((url, ct) => Api.ChangeProject(url, one.Project.Id, change, ct), cancellationToken).ConfigureAwait(true))
        {
            await Saved(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Gives the state back to Linear's.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task ClearState(CancellationToken cancellationToken)
    {
        if (_one is not { } one)
        {
            return;
        }

        var change = new ProjectChange(one.Project.Version, ClearState: true);
        if (await Send((url, ct) => Api.ChangeProject(url, one.Project.Id, change, ct), cancellationToken).ConfigureAwait(true))
        {
            await Saved(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Removes the attachment.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task Remove(CancellationToken cancellationToken)
    {
        if (_one is not { } one || !CanRemove)
        {
            return;
        }

        var detach = new ProjectDetach(one.Project.Version);
        if (await Send((url, ct) => Api.DetachProject(url, one.Project.Id, detach, ct), cancellationToken).ConfigureAwait(true))
        {
            IsConfirmingRemove = false;
            Finish(ProjectDone.Removed, Labels.ProjectRemoved(Heading), wentToFinished: false);
        }
    }

    /// <summary>Hides the led project.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task Hide(CancellationToken cancellationToken)
    {
        if (_one is not { } one || !CanHide)
        {
            return;
        }

        var hide = new ProjectHide(one.Project.Id, Hidden: true, one.Project.Version);
        if (await Send((url, ct) => Api.HideProject(url, hide, ct), cancellationToken).ConfigureAwait(true))
        {
            Finish(ProjectDone.Hidden, Labels.ProjectHidden(Heading), wentToFinished: false);
        }
    }

    /// <summary>Brings a hidden project back.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task Unhide(CancellationToken cancellationToken)
    {
        if (_one is not { } one || !CanUnhide)
        {
            return;
        }

        var hide = new ProjectHide(one.Project.Id, Hidden: false, one.Project.Version);
        if (await Send((url, ct) => Api.HideProject(url, hide, ct), cancellationToken).ConfigureAwait(true))
        {
            Finish(ProjectDone.Unhidden, Labels.ProjectUnhidden(Heading), IsFinished(one.Project.State));
        }
    }

    // Sends one write; true when it was accepted. A refusal stays under the button with the fields kept, a stale version says so and offers Reload.
    private async Task<bool> Send(Func<Uri, CancellationToken, Task<Result<ProjectAcceptedView>>> call, CancellationToken cancellationToken)
    {
        if (IsSaving || _url is not { } url)
        {
            return false;
        }

        (SaveError, IsConflict, IsSaving) = (string.Empty, false, true);
        Result<ProjectAcceptedView> answer;
        try
        {
            answer = await call(url, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsSaving = false;
        }

        if (answer.IsSuccess)
        {
            return true;
        }

        IsConflict = string.Equals(answer.Error.Code, Conflict, StringComparison.Ordinal);
        SaveError = IsConflict ? Labels.ProjectChangedElsewhere : Labels.ProjectWriteFailed(answer.Error);
        return false;
    }

    private async Task Saved(CancellationToken cancellationToken)
    {
        IsEditing = false;
        Announcement = Labels.ProjectSaved(Heading);
        await Read(cancellationToken).ConfigureAwait(true);
    }

    private void Finish(ProjectDone kind, string said, bool wentToFinished)
    {
        Announcement = said;
        Done?.Invoke(this, new ProjectDoneEventArgs(kind, Id, said, wentToFinished));
    }

    private static bool IsFinished(string? state) =>
        string.Equals(state, "done", StringComparison.Ordinal) || string.Equals(state, "canceled", StringComparison.Ordinal);

    private const string Conflict = "SND-PROJECT-409";
}
