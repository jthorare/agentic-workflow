namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// One polite live region's state: a flag and a text. To speak, the flag goes off, the text goes to its resting text, the flag
/// goes on, and then the text is set, so the same sentence said twice is two changes a screen reader announces. The text is never
/// empty (an empty live region is not announced, and a nameless one reads as nothing).
/// </summary>
internal sealed class LiveLine : ObservableObject
{
    private string _resting;

    /// <summary>A region at rest.</summary>
    /// <param name="resting">The text while nothing is being said.</param>
    internal LiveLine(string resting)
    {
        (_resting, Text) = (resting, resting);
    }

    /// <summary>True while the region speaks (the flag changes before the text).</summary>
    internal bool IsLive { get; private set => Set(ref field, value); }

    /// <summary>The region's text.</summary>
    internal string Text { get; private set => Set(ref field, value); }

    /// <summary>Changes the resting text; the region shows it at once unless it is speaking.</summary>
    /// <param name="resting">The text while nothing is being said.</param>
    internal void Rest(string resting)
    {
        _resting = resting;
        if (!IsLive)
        {
            Text = resting;
        }
    }

    /// <summary>Speaks a sentence, again if it is the one just spoken.</summary>
    /// <param name="sentence">What to say.</param>
    internal void Say(string sentence)
    {
        if (sentence.Length == 0)
        {
            return;
        }

        IsLive = false;
        Text = _resting;
        IsLive = true;
        Text = sentence;
    }
}
