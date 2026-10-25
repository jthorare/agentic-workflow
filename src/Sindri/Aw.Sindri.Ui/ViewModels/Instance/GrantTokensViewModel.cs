using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The Grant tokens control of a budget-parked instance (per-round budgets brief, 2026-10-21; engine spec W7, §4.2): an amount field that starts at
/// the served suggestion and one Grant click that sends 'POST …/commands' with 'grant-tokens' and the amount, once. A refusal (an amount outside the
/// bound, SND-AUTH-002 for an agent session) is shown in the server's own words.
/// </summary>
internal sealed class GrantTokensViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly Func<Uri?> _url;
    private readonly string _instanceId;
    private readonly string _address;

    /// <summary>The control over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="instance">The instance's id (the route) and its address (the grant's subject).</param>
    /// <param name="served">The status answer's served command line, which carries the suggested amount ("--grant-tokens 1M").</param>
    internal GrantTokensViewModel(ISindriApi api, Func<Uri?> url, (string InstanceId, string Address) instance, string served)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(url);
        (_api, _url, _instanceId, _address) = (api, url, instance.InstanceId, instance.Address);
        Suggested = SuggestionOf(served);
        Amount = Suggested;
    }

    /// <summary>Raised once the grant was applied: the frame reads the instance again.</summary>
    internal event EventHandler? Granted;

    /// <summary>The amount the park suggested ("1M"), as the served command spells it.</summary>
    public string Suggested { get; }

    /// <summary>The label over the amount field and on the button.</summary>
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    public string Title => Labels.GrantTokens;

    /// <summary>The amount field's accessible name.</summary>
    public string AmountName => "Tokens to grant for this round";
#pragma warning restore CA1822, MA0041

    /// <summary>The tokens to grant as the person writes them ("1M", "500k" or a plain number); it starts at the suggestion.</summary>
    public string Amount
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(AmountTokens));
            OnChanged(nameof(HasAmount));
            OnChanged(nameof(ShowsAmountProblem));
            OnChanged(nameof(CanGrant));
        }
    } = string.Empty;

    /// <summary>The amount as a number; null when it is not a whole number of tokens of at least 1.</summary>
    public int? AmountTokens => TokenCount.TryParse(Amount, out var tokens) ? tokens : null;

    /// <summary>True when the amount reads as tokens.</summary>
    public bool HasAmount => AmountTokens is not null;

    /// <summary>True when the field holds text that is not an amount.</summary>
    public bool ShowsAmountProblem => Amount.Trim().Length > 0 && !HasAmount;

    /// <summary>The server's refusal in its own words (an amount outside the bound, an agent session); empty when none.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the grant is on its way.</summary>
    public bool IsSending
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanGrant));
        }
    }

    /// <summary>True once the grant was applied.</summary>
    public bool IsDone
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanGrant));
        }
    }

    /// <summary>True when the Grant button can be pressed: an amount, nothing on its way, not granted yet.</summary>
    public bool CanGrant => HasAmount && !IsSending && !IsDone;

    /// <summary>Sends the grant, once: the guard is set before the first await.</summary>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Grant()
    {
        if (!CanGrant || AmountTokens is not { } tokens || _url() is not { } api)
        {
            return;
        }

        (IsSending, Error) = (true, string.Empty);
        Result<ApplyCommandAccepted> applied;
        try
        {
            applied = await _api.ApplyCommand(api, _instanceId, ApplyCommandBody.GrantTokens(_address, tokens), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            IsSending = false;
        }

        if (applied.IsError)
        {
            Error = applied.Error.Message;
            return;
        }

        IsDone = true;
        Granted?.Invoke(this, EventArgs.Empty);
    }

    // "--grant-tokens 1M" gives "1M"; a command without the flag gives nothing to suggest.
    private static string SuggestionOf(string served)
    {
        var words = served.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var at = Array.IndexOf(words, "--grant-tokens");
        return at >= 0 && at + 1 < words.Length && TokenCount.TryParse(words[at + 1], out _) ? words[at + 1] : string.Empty;
    }
}
