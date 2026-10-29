namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// The words a person typing in a sandboxed session's tmux pane or first screen sees (sandbox spec §7): the status line, the takeover banner and the refusal
/// sentence. The goldens 'Golden/Sandbox/statusline.txt', 'statusline-attended.txt', 'takeover-banner.txt' and 'refusal.txt' are the contract; a test pins them to this code.
/// </summary>
public static class SandboxStatusLine
{
    /// <summary>The place in a template where the tmux prefix goes.</summary>
    public const string PrefixToken = "<prefix>";

    /// <summary>A status line stays under this many characters with any ordinary prefix (spec §7: under 80).</summary>
    public const int MaxLength = 79;

    /// <summary>The banner of a Step run's takeover first screen.</summary>
    public static string TakeoverBanner => "Sandboxed session. Pushes and secrets are blocked. The clock is paused while you're here. To push or read a secret, open a new window: it is outside the sandbox. `sindri sandbox exec` shows what the agent saw.";

    /// <summary>The sentence a refused action returns, addressed to whoever is typing; it carries no rule id.</summary>
    public static string Refusal => "Blocked by the sandbox. You can do this yourself in a new terminal window.";

    /// <summary>The status line with <see cref="PrefixToken"/> still in it.</summary>
    /// <param name="attended">True for a session the owner opened (SB19).</param>
    /// <returns>The template.</returns>
    public static string Template(bool attended) => attended
        ? "Sandboxed (attended): push and secrets blocked. Outside it: <prefix> c"
        : "Sandboxed: push and secrets blocked. Outside it: <prefix> c";

    /// <summary>How the person reads a tmux prefix option: 'C-b' is "Ctrl-b"; anything else is shown as it is.</summary>
    /// <param name="tmuxPrefix">The value of tmux's 'prefix' option.</param>
    /// <returns>The label.</returns>
    public static string PrefixLabel(string tmuxPrefix)
    {
        ArgumentNullException.ThrowIfNull(tmuxPrefix);
        return tmuxPrefix is ['C', '-', var key] ? $"Ctrl-{key}" : tmuxPrefix;
    }

    /// <summary>The status line for a session whose tmux prefix option is <paramref name="tmuxPrefix"/>.</summary>
    /// <param name="attended">True for a session the owner opened.</param>
    /// <param name="tmuxPrefix">The value of tmux's 'prefix' option.</param>
    /// <returns>The line to show.</returns>
    public static string Render(bool attended, string tmuxPrefix) =>
        Template(attended).Replace(PrefixToken, PrefixLabel(tmuxPrefix), StringComparison.Ordinal);
}
