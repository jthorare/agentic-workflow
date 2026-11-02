using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Testing.Slack;

/// <summary>Builds a <see cref="SlackMessage"/> with deterministic defaults (see <see cref="A.SlackMessage"/>).</summary>
public sealed class SlackMessageBuilder
{
    private readonly string _ts;
    private string? _threadTs;
    private string? _author = "Ada";
    private string _text = "Deploy is green.";
    private int _replyCount;
    private string? _subtype;

    internal SlackMessageBuilder(string ts)
    {
        _ts = ts;
    }

    /// <summary>Makes it a reply in the thread whose root has <paramref name="rootTs"/>.</summary>
    /// <param name="rootTs">The root's ts.</param>
    /// <returns>This builder.</returns>
    public SlackMessageBuilder InThread(string rootTs)
    {
        _threadTs = rootTs;
        return this;
    }

    /// <summary>Sets the text.</summary>
    /// <param name="text">The mrkdwn.</param>
    /// <returns>This builder.</returns>
    public SlackMessageBuilder WithText(string text)
    {
        _text = text;
        return this;
    }

    /// <summary>Sets the author's display name.</summary>
    /// <param name="author">The name, or null for none.</param>
    /// <returns>This builder.</returns>
    public SlackMessageBuilder By(string? author)
    {
        _author = author;
        return this;
    }

    /// <summary>Sets the reply count a reader reports for a root (the fake server counts the replies it holds and ignores this).</summary>
    /// <param name="count">The count.</param>
    /// <returns>This builder.</returns>
    public SlackMessageBuilder WithReplies(int count)
    {
        _replyCount = count;
        return this;
    }

    /// <summary>Sets Slack's subtype.</summary>
    /// <param name="subtype">'channel_join' and so on.</param>
    /// <returns>This builder.</returns>
    public SlackMessageBuilder WithSubtype(string subtype)
    {
        _subtype = subtype;
        return this;
    }

    /// <summary>Builds the message.</summary>
    /// <returns>The message.</returns>
    public SlackMessage Build() => new(_ts, _threadTs, _author, _text, _replyCount, _subtype);

    /// <summary>Builds the message.</summary>
    /// <param name="builder">The builder.</param>
    public static implicit operator SlackMessage(SlackMessageBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Build();
    }
}
