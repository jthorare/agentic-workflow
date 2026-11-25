namespace Aw.Sindri.Core.Messages;

/// <summary>What a mrkdwn span is (messages spec §6.3): the subset Sindri reads and writes back; anything else stays text.</summary>
public enum MrkdwnKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>*bold*.</summary>
    Bold,

    /// <summary>_italic_.</summary>
    Italic,

    /// <summary>~strike~.</summary>
    Strike,

    /// <summary>`code`.</summary>
    Code,

    /// <summary>A ``` block.</summary>
    Pre,

    /// <summary>A '&gt;' quote line.</summary>
    Quote,

    /// <summary>A &lt;url|label&gt; link.</summary>
    Link,

    /// <summary>A &lt;@U…&gt; mention of a person.</summary>
    UserMention,

    /// <summary>A &lt;#C…|name&gt; mention of a channel.</summary>
    ChannelMention,

    /// <summary>A &lt;!here&gt;, &lt;!channel&gt; or &lt;!everyone&gt; broadcast.</summary>
    Broadcast,

    /// <summary>An :emoji: name.</summary>
    Emoji,

    /// <summary>A list line: '• ', '- ', '* ' or '1. ' at the start of a line, the marker kept as written.</summary>
    ListItem,
}
