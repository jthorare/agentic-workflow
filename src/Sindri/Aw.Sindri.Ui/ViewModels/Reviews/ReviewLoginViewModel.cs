using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The "Set your GitHub login" prompt of the review page (review page spec §16): one field and Save, with the list prompt's words.</summary>
internal sealed class ReviewLoginViewModel(ISindriApi api) : ObservableObject
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The Save button's words.</summary>
    public string SaveText => Labels.ReviewsLoginSave;

    /// <summary>The field's name.</summary>
    public string FieldName => Labels.ReviewsLoginFieldName;
#pragma warning restore CA1822, MA0041

    /// <summary>The login being typed.</summary>
    public string Field
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSave));
        }
    } = string.Empty;

    /// <summary>True when Save can be pressed: the field has text.</summary>
    public bool CanSave => !string.IsNullOrWhiteSpace(Field);

    /// <summary>The server's detail when the save was refused; empty otherwise.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The line after a save that waits for a profile approval; empty otherwise.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Saves the login in the field.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the login is in effect (approved), so the page can read again.</returns>
    internal async Task<bool> Save(Uri url, CancellationToken cancellationToken)
    {
        if (!CanSave)
        {
            return false;
        }

        var saved = await api.SaveReviewLogin(url, new ReviewLoginSave(Field.Trim()), cancellationToken).ConfigureAwait(true);
        if (saved.IsError)
        {
            (Error, Note) = (Labels.InstanceRefused(saved.Error), string.Empty);
            return false;
        }

        (Error, Note) = (string.Empty, saved.Value.Approved ? string.Empty : Labels.ReviewsLoginPending);
        return saved.Value.Approved;
    }
}
