using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's Grant tokens control (<see cref="InstanceFrameViewModel"/>, continued): in the header's actions area while the instance is parked on its budget.</summary>
internal sealed partial class InstanceFrameViewModel
{
    private string _grantSignature = string.Empty;

    /// <summary>The Grant tokens control; null unless the instance is parked on its budget and the answer is the token grant itself (a mirrored child's grant is the child's to give).</summary>
    public GrantTokensViewModel? Grant { get; private set => Set(ref field, value); }

    // A re-read serves the same answer again: the control stays (with an amount the person is editing) unless the answer changed.
    private void ShowGrant(InstanceDetail detail)
    {
        if (detail.Current.Status.Answer is not { Kind: AnswerKind.GrantTokens, Via: null } answer || _id is not { } id)
        {
            (Grant, _grantSignature) = (null, string.Empty);
            return;
        }

        var signature = string.Join('|', id, detail.Address, answer.Cli);
        if (Grant is not null && string.Equals(signature, _grantSignature, StringComparison.Ordinal))
        {
            return;
        }

        var grant = new GrantTokensViewModel(api, () => _url, (id, detail.Address), answer.Cli);
        grant.Granted += async (_, _) => await AfterGrant().ConfigureAwait(true);
        (Grant, _grantSignature) = (grant, signature);
    }

    // The grant was applied: read the instance again, so the park is gone from the frame.
    private async Task AfterGrant()
    {
        if (_url is not null)
        {
            await Read(CancellationToken.None).ConfigureAwait(true);
        }
    }
}
