using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>What the needs-you rows add up to outside the list (UI spec §5): the window title, the dock badge, the tray, the live region.</summary>
internal sealed partial class ShellViewModel
{
    private readonly AttentionAnnouncer _announcer = new();
    private readonly LiveLine _line = new(Labels.Sindri);
    private NeedsYouFeed? _needsYou;
    private string? _heldClear;   // while one feed update runs: the "<id> cleared" it has to say, joined with the rise (one sentence, as the queue's)
    private bool _collecting;

    /// <summary>The window's title: "(N) Needs you · Sindri" while rows need the user, else "Sindri".</summary>
    public string WindowTitle => Attention.Title;

    /// <summary>The summary the title, the dock badge and the tray follow; it changes with the list and with the shell's state.</summary>
    internal AttentionSummary Attention { get; private set; }

    /// <summary>True while the Needs-you live region speaks (the flag changes before the text).</summary>
    public bool IsNeedsYouAnnouncing => _line.IsLive;

    /// <summary>The Needs-you live region's text: "N now need you" for each rise, "&lt;id&gt; cleared" for a selected row that left, "&lt;address&gt; done" for an instance that finished; else its resting text.</summary>
    public string NeedsYouAnnouncement => _line.Text;

    // The one Needs-you region: the three kinds of line share it, each said once (LiveLine).
    private void InitAnnouncements()
    {
        _line.PropertyChanged += (_, e) => OnChanged(string.Equals(e.PropertyName, nameof(LiveLine.IsLive), StringComparison.Ordinal) ? nameof(IsNeedsYouAnnouncing) : nameof(NeedsYouAnnouncement));
        NeedsYou.PropertyChanged += (_, e) => SayNeedsYou(e.PropertyName);
        Strip.PropertyChanged += (_, e) => SayIf(e.PropertyName, nameof(WorkflowsStripViewModel.Announcement), Strip.Announcement);
    }

    private void SayIf(string? changed, string name, string sentence)
    {
        if (string.Equals(changed, name, StringComparison.Ordinal))
        {
            _line.Say(sentence);
        }
    }

    private void SayNeedsYou(string? changed)
    {
        if (_collecting && string.Equals(changed, nameof(NeedsYouViewModel.Announcement), StringComparison.Ordinal))
        {
            _heldClear = NeedsYou.Announcement;
            return;
        }

        SayIf(changed, nameof(NeedsYouViewModel.Announcement), NeedsYou.Announcement);
    }

    /// <summary>Takes the needs-you list as the API serves it now.</summary>
    /// <param name="feed">The list.</param>
    internal void ShowNeedsYou(NeedsYouFeed feed)
    {
        _needsYou = feed;
        _collecting = true;
        _heldClear = null;
        try
        {
            UpdateNeedsYou();
        }
        finally
        {
            _collecting = false;
        }

        var rise = Observe();
        var sentence = _heldClear is { } cleared ? (rise is null ? cleared : $"{cleared}, {rise}") : rise;
        _heldClear = null;
        if (sentence is not null)
        {
            _line.Say(sentence);
        }
    }

    // Works the summary out again from the last list and the current state; the title, the badge and the tray follow it.
    private void Reattend()
    {
        if (Observe() is { } sentence)
        {
            _line.Say(sentence);
        }
    }

    // The summary again; the rise it has to announce, if any.
    private string? Observe()
    {
        var before = _announcer.Announcements;
        Attention = AttentionSummary.For(_needsYou, State, _clock.GetUtcNow(), Health.Sessions, _needsYou?.Rows.Count(ShownOnHome));
        _announcer.Observe(Attention.Badge, AttentionSummary.IsCounted(State));
        Changed(nameof(Attention), nameof(WindowTitle));
        return _announcer.Announcements != before ? _announcer.Line : null;
    }
}
