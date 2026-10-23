namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// One recipe verb's button on the action bar (needs-you spec, stop 3 as amended): its words, and for a destructive verb the one-line question that
/// comes first. The press and the answer to the question go to the bar, which sends the served desk action; nothing here sends.
/// </summary>
internal sealed class DeskVerbViewModel : ObservableObject
{
    private readonly NeedsYouBarViewModel _bar;

    internal DeskVerbViewModel(DeskVerb verb, string subject, NeedsYouBarViewModel bar)
    {
        (Verb, IsDestructive, _bar) = (verb.Verb, verb.Destructive, bar);
        Label = Labels.VerbLabel(verb.Verb);
        Name = Labels.VerbName(Label, subject);
        Question = Labels.VerbConfirmQuestion(verb.Verb);
    }

    /// <summary>The verb, as the instance serves it.</summary>
    public string Verb { get; }

    /// <summary>The button's words.</summary>
    public string Label { get; }

    /// <summary>The button's accessible name: the words and the request.</summary>
    public string Name { get; }

    /// <summary>True when the verb asks first and is drawn in the Destructive style.</summary>
    public bool IsDestructive { get; }

    /// <summary>The one-line question a destructive verb asks.</summary>
    public string Question { get; }

    /// <summary>The question's confirm button.</summary>
    public string ConfirmLabel { get; } = Labels.VerbConfirm;

    /// <summary>The question's keep button.</summary>
    public string KeepLabel { get; } = Labels.VerbKeep;

    /// <summary>True while the question shows in place of the button.</summary>
    public bool IsConfirming { get; internal set { Set(ref field, value); OnChanged(nameof(ShowsButton)); } }

    /// <summary>True while the button itself shows.</summary>
    public bool ShowsButton => !IsConfirming;

    /// <summary>True when the verb can be pressed now: a request is shown and nothing is in flight.</summary>
    public bool IsEnabled => _bar.VerbsEnabled;

    /// <summary>Tells the view the bar's state changed.</summary>
    internal void Changed() => OnChanged(nameof(IsEnabled));

    /// <summary>The press: a destructive verb asks first, any other is sent at once.</summary>
    /// <returns>A task that completes when the press was handled.</returns>
    internal Task Press() => _bar.PressVerb(this);

    /// <summary>The question's confirm: sends the verb, once.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal Task Confirm() => _bar.ConfirmVerb(this);

    /// <summary>The question's keep: nothing is sent.</summary>
    internal void Keep() => IsConfirming = false;
}
