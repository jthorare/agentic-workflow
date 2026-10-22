using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// A run's one status (F3 workflows spec §4): a glyph beside a sentence-case word, in the kind's colours. The glyph and the
/// word are both always there, so the status is never carried by colour alone; the running glyph turns, and stands still
/// under reduced motion (<see cref="StateIcon"/>). The word is the accessible name, on the wrapper (a TextBlock's own
/// name is ignored).
/// </summary>
internal sealed class StatusBadge : Border
{
    /// <summary>The status to draw.</summary>
    public static readonly StyledProperty<RunStatus?> StatusProperty = AvaloniaProperty.Register<StatusBadge, RunStatus?>(nameof(Status));

    // Every colour class the status can wear; exactly one is set at a time.
    private static readonly string[] _classes = ["running", "attn", "violet", "ok", "fail", "neutral", "faint"];

    private readonly StateIcon _glyph = new();
    private readonly TextBlock _word = new();

    static StatusBadge()
    {
        _ = StatusProperty.Changed.AddClassHandler<StatusBadge>((badge, _) => badge.Apply());
    }

    /// <summary>Initializes a new instance of the <see cref="StatusBadge"/> class, empty and hidden until it has a status.</summary>
    public StatusBadge()
    {
        Classes.Add("status-badge");
        Child = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { _glyph, _word } };
        IsVisible = false;
    }

    /// <summary>Gets or sets the status.</summary>
    public RunStatus? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    private void Apply()
    {
        foreach (var name in _classes)
        {
            _ = Classes.Remove(name);
        }

        IsVisible = Status is not null;
        _word.Text = Status?.Word;
        _glyph.Icon = Status?.Icon;
        _glyph.Spins = Status?.Kind == RunStatusKind.Running;
        AutomationProperties.SetName(this, Status?.Word);
        if (Status is { } status)
        {
            Classes.Add(status.Class);
        }
    }
}
