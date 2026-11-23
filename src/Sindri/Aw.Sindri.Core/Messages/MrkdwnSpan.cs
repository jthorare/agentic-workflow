using System.Text;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// One span of mrkdwn text (messages spec §6.3). Text stays escaped as Slack sends it ('&amp;lt;' for '&lt;'), so a span writes back exactly what it read;
/// <see cref="Display"/> is the text to show. Bold, italic, strike, a quote and a list line hold their inner spans in <see cref="Children"/>.
/// </summary>
/// <param name="Kind">What the span is.</param>
/// <param name="Text">Its text, without the marks around it: a link's label (or its URL when it has none), a mention's id, an emoji's name.</param>
public sealed record MrkdwnSpan(MrkdwnKind Kind, string Text)
{
    // by kind: what is written before and after the inner text; a quote and a list line write their Marker instead of an opening mark
    private static readonly string[] _open = ["", "*", "_", "~", "`", "```", "", "<", "<@", "<#", "<!", ":", ""];
    private static readonly string[] _close = ["", "*", "_", "~", "`", "```", "", ">", ">", ">", ">", ":", ""];

    /// <summary>Gets the spans inside a bold, italic, strike, quote or list span; empty for the rest, which write <see cref="MrkdwnSpan.Text"/>.</summary>
    public IReadOnlyList<MrkdwnSpan> Children { get; init; } = [];

    /// <summary>Gets what a link or mention points at: the URL, the user or channel id, or 'here' for a broadcast; null means <see cref="MrkdwnSpan.Text"/>.</summary>
    public string? Target { get; init; }

    /// <summary>Gets the text after '|' in a link or mention ('&lt;url|label&gt;'); null when it has none.</summary>
    public string? Label { get; init; }

    /// <summary>Gets a quote's or list line's marker as written ('&gt; ', '&amp;gt;', '&gt;&gt;&gt;', '• ', '2. '); null writes '&gt; ' or '• '.</summary>
    public string? Marker { get; init; }

    /// <summary>Gets the text to show: entities read ('&amp;lt;' is '&lt;'), bidi and control characters but tab and newline dropped (spec §11).</summary>
    public string Display => Mrkdwn.ToDisplay(Text);

    internal bool IsAngle => Kind is >= MrkdwnKind.Link and <= MrkdwnKind.Broadcast;

    private bool IsLine => Kind is MrkdwnKind.Quote or MrkdwnKind.ListItem;

    // a link counts by its label only: a '?' in a URL is no question
    private bool HasWords => Kind is MrkdwnKind.Text or MrkdwnKind.Bold or MrkdwnKind.Italic or MrkdwnKind.Strike or MrkdwnKind.Quote or MrkdwnKind.ListItem or MrkdwnKind.Link;

    /// <summary>Writes the span back as mrkdwn.</summary>
    /// <param name="into">The text so far.</param>
    internal void WriteTo(StringBuilder into)
    {
        var kind = (uint)Kind < (uint)_open.Length ? (int)Kind : 0;
        into.Append(IsLine ? Marker ?? DefaultMarker() : _open[kind]);
        if (IsAngle)
        {
            into.Append(Target ?? Text);
            if (Label is not null)
            {
                into.Append('|').Append(Label);
            }
        }
        else
        {
            WriteInner(into);
        }

        into.Append(_close[kind]);
    }

    /// <summary>Writes the words a reader sees, for the '?' and request-phrase rules: no code, no URL, no mention id, no emoji name.</summary>
    /// <param name="into">The words so far.</param>
    internal void WriteWords(StringBuilder into)
    {
        if (!HasWords)
        {
            return;
        }

        if (Children.Count == 0)
        {
            into.Append(Mrkdwn.ToDisplay(Kind == MrkdwnKind.Link ? Label ?? string.Empty : Text));
            return;
        }

        foreach (var child in Children)
        {
            child.WriteWords(into);
        }
    }

    private void WriteInner(StringBuilder into)
    {
        if (Children.Count == 0)
        {
            into.Append(Text);
            return;
        }

        foreach (var child in Children)
        {
            child.WriteTo(into);
        }
    }

    private string DefaultMarker() => Kind == MrkdwnKind.Quote ? "> " : "• ";
}
