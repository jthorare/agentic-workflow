using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>
/// The park card on a Needs-you row whose Step can't start because of the sandbox (sandbox spec §8). Its name is "Needs you: Step x can't
/// start, " and the first clause of the served sentence, never the reason code; the whole sentence and its copyable command sit under it, and
/// Check again is the shared <see cref="Check"/>. A gray or waiting blocker is never red.
/// </summary>
internal sealed class SandboxParkCardViewModel : ObservableObject
{
    private static readonly string[] _clauseEnds = [", ", ". ", ": ", " ("];

    /// <summary>The card for one parked row.</summary>
    /// <param name="blocker">The served blocker.</param>
    /// <param name="step">The Step's name (the row's title).</param>
    /// <param name="check">Check again, shared with the Health card.</param>
    internal SandboxParkCardViewModel(SandboxBlockerLineView blocker, string step, SandboxCheckViewModel check)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        Check = check;
        Row = SandboxBlockerRow.Of(blocker);
        Name = Labels.SandboxParkName(step, FirstClause(blocker.Text));
        Sentence = blocker.Text;
        (Severity, Glyph) = blocker.State switch
        {
            "gray" => (Labels.SandboxSeverityNotYet, StateGlyph.Stale),
            "wait" => (Labels.SandboxSeverityWaiting, StateGlyph.Stale),
            _ => (Labels.SandboxSeverityBlocked, StateGlyph.Down),
        };
    }

    /// <summary>Check again, its outcome and the resume list.</summary>
    public SandboxCheckViewModel Check { get; }

    /// <summary>The card's accessible name.</summary>
    public string Name { get; }

    /// <summary>The whole sentence, shown before the buttons.</summary>
    public string Sentence { get; }

    /// <summary>The sentence and the command to copy.</summary>
    public SandboxBlockerRow Row { get; }

    /// <summary>The severity as a word.</summary>
    public string Severity { get; }

    /// <summary>The severity as a mark.</summary>
    public StateGlyph Glyph { get; }

    /// <summary>The sentence up to its first comma, full stop, colon or bracket, without code marks.</summary>
    /// <param name="sentence">The served sentence.</param>
    /// <returns>For example "This machine has no sandbox tool".</returns>
    internal static string FirstClause(string sentence)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        var cut = _clauseEnds.Select(m => sentence.IndexOf(m, StringComparison.Ordinal)).Where(i => i > 0).DefaultIfEmpty(-1).Min();
        var clause = cut > 0 ? sentence[..cut] : sentence.TrimEnd('.');
        return clause.Replace("`", string.Empty, StringComparison.Ordinal);
    }
}
