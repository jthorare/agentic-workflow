using System.Text.RegularExpressions;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The answer form of a paused run's card (F3 needs-you spec §3, §5): three options and the one button under them. Allow more tries, a hint
/// and a stop each go to the instance in one click, as ApplyCommand:answer-grant (with the stepper's rounds), ApplyCommand:answer-text and
/// ApplyCommand:answer-reject, once per press.
/// </summary>
internal sealed partial class PausedAnswerViewModel : ObservableObject
{
    internal const int MinTries = 1;
    internal const int MaxTries = 100;

    private static readonly string[] _all =
    [
        nameof(Option), nameof(ShowsMoreTries), nameof(MoreTriesSelected), nameof(HintSelected), nameof(StopSelected), nameof(ShowsStepper), nameof(ShowsHintBox),
        nameof(SendWord), nameof(CanSend), nameof(IsSending), nameof(IsSent), nameof(Refusal), nameof(HasRefusal), nameof(Line), nameof(HasLine),
    ];

    private readonly InstanceCommander _commands;
    private string? _instance;
    private string _cli = "";

    internal PausedAnswerViewModel(InstanceCommander commands)
    {
        _commands = commands;
    }

    /// <summary>The caption over the options.</summary>
    public string Caption { get; } = Labels.NeedsYouAnswerCaption;

    /// <summary>The words of the more-tries option.</summary>
    public string MoreTriesWord { get; } = Labels.NeedsYouMoreTries;

    /// <summary>Its help.</summary>
    public string MoreTriesHelp { get; } = Labels.NeedsYouMoreTriesHelp;

    /// <summary>The words of the hint option.</summary>
    public string HintWord { get; } = Labels.NeedsYouHintOption;

    /// <summary>Its help.</summary>
    public string HintHelp { get; } = Labels.NeedsYouHintHelp;

    /// <summary>The words of the stop option.</summary>
    public string StopWord { get; } = Labels.NeedsYouStopOption;

    /// <summary>Its help.</summary>
    public string StopHelp { get; } = Labels.NeedsYouStopHelp;

    /// <summary>The stepper's caption.</summary>
    public string ExtraTriesCaption { get; } = Labels.NeedsYouExtraTries;

    /// <summary>The stepper's more button name.</summary>
    public string MoreName { get; } = Labels.NeedsYouExtraTriesMore;

    /// <summary>The stepper's fewer button name.</summary>
    public string FewerName { get; } = Labels.NeedsYouExtraTriesFewer;

    /// <summary>The hint box's placeholder.</summary>
    public string HintPlaceholder { get; } = Labels.NeedsYouHintPlaceholder;

    /// <summary>The chosen option.</summary>
    public PausedAnswerOption Option { get; private set; } = PausedAnswerOption.Hint;

    /// <summary>True when the row serves a grant command, so more tries can be offered.</summary>
    public bool ShowsMoreTries => GrantFlag.IsMatch(_cli);

    /// <summary>True while more tries is chosen.</summary>
    public bool MoreTriesSelected => Option == PausedAnswerOption.MoreTries;

    /// <summary>True while a hint is chosen.</summary>
    public bool HintSelected => Option == PausedAnswerOption.Hint;

    /// <summary>True while a stop is chosen.</summary>
    public bool StopSelected => Option == PausedAnswerOption.Stop;

    /// <summary>True when the stepper shows (more tries is chosen).</summary>
    public bool ShowsStepper => MoreTriesSelected;

    /// <summary>True when the hint box shows.</summary>
    public bool ShowsHintBox => HintSelected;

    /// <summary>The extra tries the stepper holds.</summary>
    public int ExtraTries { get; private set; } = MinTries;

    /// <summary>The hint typed.</summary>
    public string HintText
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSend));
        }
    } = "";

    /// <summary>The button's words: "Send answer".</summary>
    public string SendWord { get; } = Labels.NeedsYouSendAnswer;

    /// <summary>True while an answer is in flight.</summary>
    public bool IsSending { get; private set; }

    /// <summary>True once the answer was taken.</summary>
    public bool IsSent { get; private set; }

    /// <summary>The served refusal of the last answer, as served; empty for none.</summary>
    public string Refusal { get; private set; } = "";

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasRefusal => Refusal.Length > 0;

    /// <summary>The line under the button: sent.</summary>
    public string Line => IsSent ? Labels.NeedsYouAnswerSent : "";

    /// <summary>True when the line shows.</summary>
    public bool HasLine => Line.Length > 0;

    /// <summary>True when the button can be pressed: not sending, not sent, and the chosen option has what it needs.</summary>
    public bool CanSend => _instance is not null && !IsSending && !IsSent && Option switch
    {
        PausedAnswerOption.MoreTries => ShowsMoreTries,
        PausedAnswerOption.Hint => !string.IsNullOrWhiteSpace(HintText),
        PausedAnswerOption.Stop => true,
        _ => false,
    };

    /// <summary>Shows a paused run's row; another run starts fresh, the same one is updated in place.</summary>
    /// <param name="entry">The row; null, or a row that is not a paused run, for none.</param>
    internal void Show(NeedsYouEntry? entry)
    {
        var instance = entry is not null && entry.Id.StartsWith("park:", StringComparison.Ordinal) && entry.Subject is { } subject && string.Equals(subject.Kind, "instance", StringComparison.Ordinal) ? subject.Id : null;
        var same = instance is not null && string.Equals(instance, _instance, StringComparison.Ordinal);
        (_instance, _cli) = (instance, entry?.Cli ?? "");
        if (!same)
        {
            (IsSent, IsSending, Refusal, HintText, ExtraTries) = (false, false, "", "", MinTries);
            Option = ShowsMoreTries ? PausedAnswerOption.MoreTries : PausedAnswerOption.Hint;
        }

        Refresh();
    }

    /// <summary>Chooses an option; more tries is only offered when the row serves a grant command.</summary>
    /// <param name="option">The option.</param>
    internal void Choose(PausedAnswerOption option)
    {
        if (option == PausedAnswerOption.MoreTries && !ShowsMoreTries)
        {
            return;
        }

        (Option, Refusal) = (option, "");
        Refresh();
    }

    /// <summary>The stepper's more button.</summary>
    internal void Increase() => Step(1);

    /// <summary>The stepper's fewer button.</summary>
    internal void Decrease() => Step(-1);

    private void Step(int by)
    {
        ExtraTries = Math.Clamp(ExtraTries + by, MinTries, MaxTries);
        OnChanged(nameof(ExtraTries));
    }

    /// <summary>The button, or Ctrl+Enter: sends the grant, the hint or the stop, once per press.</summary>
    /// <returns>A task that completes when the press was handled.</returns>
    internal async Task Send()
    {
        if (!CanSend || _instance is not { } instance)
        {
            return;
        }

        var body = Option switch
        {
            PausedAnswerOption.MoreTries => new ApplyCommandBody(instance, CommandKind.Answer, AnswerVariant.Grant, Amount: ExtraTries),
            PausedAnswerOption.Hint => new ApplyCommandBody(instance, CommandKind.Answer, AnswerVariant.Text, HintText.Trim()),
            PausedAnswerOption.Stop => new ApplyCommandBody(instance, CommandKind.Answer, AnswerVariant.Reject),
            _ => throw new InvalidOperationException("an option the answer form doesn't know"),
        };
        (IsSending, Refusal) = (true, "");   // set before the first await: a second press finds it busy
        Refresh();
        var answer = await _commands.Send(body).ConfigureAwait(true);
        if (!string.Equals(_instance, instance, StringComparison.Ordinal))
        {
            return;   // another run is shown now, and it owns the flag
        }

        IsSending = false;
        if (answer is { IsError: true } refused)
        {
            Refusal = refused.Error.Message;
        }
        else if (answer is not null)
        {
            IsSent = true;
        }

        Refresh();
    }

    private void Refresh()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }

        OnChanged(nameof(ExtraTries));
    }

    [GeneratedRegex(@"--grant\s+\d+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GrantFlag { get; }
}
