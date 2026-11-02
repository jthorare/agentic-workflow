namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.slack', checked (owned index spec §5.3, §5.4, Q7). The token is a secret pointer and <see cref="ToString"/> withholds it.</summary>
public sealed class SlackSource
{
    /// <summary>The channels read; never a direct message, never one not listed.</summary>
    public IReadOnlyList<SlackChannel> Channels { get; init; } = [];

    /// <summary>Slack's own retention in days, replacing 'context.index.retainDays' for this source; null follows the index.</summary>
    public int? RetainDays { get; init; }

    /// <summary>The secret pointer to the bot token, or null before 'sindri index connect slack'.</summary>
    public string? Token { get; init; }

    /// <summary>The workspace name in 'https://&lt;workspace&gt;.slack.com', which the record permalinks are built from; null until the profile names it.</summary>
    public string? Workspace { get; init; }

    /// <summary>The source without the token pointer.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"SlackSource {{ Channels = {Channels.Count}, RetainDays = {RetainDays}, Token = (withheld) }}";
}
