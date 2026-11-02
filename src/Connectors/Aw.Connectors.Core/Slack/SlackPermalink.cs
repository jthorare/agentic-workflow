namespace Aw.Connectors.Core.Slack;

/// <summary>The permalink of a Slack message, built from the workspace name (no chat.getPermalink call).</summary>
public static class SlackPermalink
{
    /// <summary>'https://&lt;workspace&gt;.slack.com/archives/&lt;channel&gt;/p&lt;ts without the dot&gt;'.</summary>
    /// <param name="workspace">The workspace name (checked by <see cref="SlackSettings"/>).</param>
    /// <param name="channel">The channel id.</param>
    /// <param name="ts">The message's ts.</param>
    /// <returns>The https permalink.</returns>
    public static string For(string workspace, string channel, string ts)
        => $"https://{workspace}.slack.com/archives/{channel}/p{ts.Replace(".", string.Empty, StringComparison.Ordinal)}";
}
