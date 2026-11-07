using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>Save and approve (<see cref="ReviewSettingsViewModel"/>, continued): the entry holds only what the owner set or changed, and a refusal lands under the field it names.</summary>
internal sealed partial class ReviewSettingsViewModel
{
    /// <summary>Sends the entry with approve: true. An unreadable "decided by" stops the save with its error under the checks.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (_read is not { } read || _url is not { } url || IsSaving || !CanChange)
        {
            return;
        }

        (Note, Announcement) = (string.Empty, string.Empty);
        ClearErrors();
        if (Entry() is not { } entry)
        {
            return;
        }

        if (await Send(new ReviewSettingsSave(read.Project, Lines(ReposText), Name.Trim(), entry, Approve: true, read.Hash), url, cancellationToken).ConfigureAwait(true) is not { } answer)
        {
            return;   // the sheet moved to another project while the save was out: its answer belongs to nobody here
        }

        if (answer.IsError)
        {
            Refused(answer.Error);
            return;
        }

        (Note, Announcement) = answer.Value.Approved ? (string.Empty, Labels.ReviewSettingsSavedApproved) : (Labels.ReviewSettingsPending(answer.Value.PendingCount), string.Empty);
        _snapshot = null;   // saved: the read below replaces every edit
        await Read(read.Project, cancellationToken).ConfigureAwait(true);
    }

    // Sends the save; null when the sheet switched projects meanwhile (the answer is dropped, and the new project's saving state is left alone).
    private async Task<Result<ReviewSettingsSaved>?> Send(ReviewSettingsSave request, Uri url, CancellationToken cancellationToken)
    {
        var generation = _generation;
        IsSaving = true;
        try
        {
            var answer = await api.SaveReviewSettings(url, request, cancellationToken).ConfigureAwait(true);
            return generation == _generation ? answer : null;
        }
        finally
        {
            if (generation == _generation)
            {
                IsSaving = false;
            }
        }
    }

    // Only a key the entry already sets, or the owner changed, goes out; a reset key or one left at its inherited value stays out.
    private ReviewSettingsEntry? Entry()
    {
        if (Checks() is not { } checks)
        {
            return null;
        }

        return new ReviewSettingsEntry(
            Extends: Extends.Trim() is { Length: > 0 } extends ? extends : null,
            Panels: Sent(PanelsKey) ? [.. PanelRows.Where(p => p.Visible).Select(p => p.Id)] : null,
            Groups: Sent(GroupsKey) ? Groups() : null,
            Collapse: Sent(CollapseKey) ? Lines(CollapseText) : null,
            Risky: Sent(RiskyKey) ? Risky() : null,
            Checks: Sent(ChecksKey) ? checks : null);
    }

    private List<ReviewSettingsGroup> Groups() =>
        [.. GroupRows.Where(g => g.Name.Trim().Length > 0 || g.GlobsText.Trim().Length > 0).Select(g => new ReviewSettingsGroup(g.Name.Trim(), Lines(g.GlobsText)))];

    private List<ReviewSettingsRisk> Risky() =>
        [.. RiskRows.Where(r => r.GlobsText.Trim().Length > 0 || r.Why.Trim().Length > 0).Select(r => new ReviewSettingsRisk(Lines(r.GlobsText), r.Why.Trim()))];

    private bool Sent(ReviewSettingsKey key) => !key.IsReset && (key.WasOwn || !string.Equals(Signature(key.Id), key.Baseline, StringComparison.Ordinal));

    // The check rows as the API's records; null (with the error under the checks) when a "decided by" can't be read.
    private List<ReviewSettingsCheck>? Checks()
    {
        List<ReviewSettingsCheck> checks = [];
        foreach (var row in CheckRows.Where(c => c.Id.Trim().Length > 0 || c.Title.Trim().Length > 0 || c.NeedText.Trim().Length > 0))
        {
            var need = string.Equals(row.NeedText, row.OriginalNeedText, StringComparison.Ordinal) && row.Need is not null ? row.Need : ReviewSettingsNeed.Parse(row.NeedText);
            if (need is null)
            {
                ChecksKey.Error = Labels.ReviewSettingsNeedBad(row.Id.Trim());
                return null;
            }

            var when = Lines(row.WhenText);
            checks.Add(new ReviewSettingsCheck(row.Id.Trim(), row.Title.Trim(), when.Length > 0 ? when : null, need));
        }

        return checks;
    }

    private const string StaleHash = "SND-PROFILE-003";

    private void Refused(Error error)
    {
        if (error.Kind == ErrorKind.Forbidden)
        {
            (CanChange, SaveError) = (false, Labels.InstanceRefused(error));
        }
        else if (string.Equals(error.Code, StaleHash, StringComparison.Ordinal))
        {
            IsStale = true;
        }
        else if (FieldOf(error) is { } field)
        {
            Place(field, error);
        }
        else
        {
            SaveError = Labels.InstanceRefused(error);
        }
    }

    // The detail goes under the field the refusal names: a key's editor, extends, the repos or the entry's name; anything else under the buttons.
    private void Place(string field, Error error)
    {
        var head = (field.StartsWith("entry.", StringComparison.Ordinal) ? field["entry.".Length..] : field).Split('.', '[')[0];
        if (Keys.FirstOrDefault(k => string.Equals(k.Id, head, StringComparison.Ordinal)) is { } key)
        {
            key.Error = error.Message;
            return;
        }

        switch (head)
        {
            case "extends": ExtendsError = error.Message; break;
            case "repos": ReposError = error.Message; break;
            case "settings": NameError = error.Message; break;
            default: SaveError = Labels.InstanceRefused(error); break;
        }
    }

    // The request key a 400 is about, from the error's "field" member.
    private static string? FieldOf(Error error) =>
        error.Extensions?.Where(m => string.Equals(m.Key, "field", StringComparison.Ordinal) && m.Value.ValueKind == JsonValueKind.String).Select(m => m.Value.GetString()).FirstOrDefault();
}
