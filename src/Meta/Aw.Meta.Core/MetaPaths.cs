using System.Globalization;

namespace Aw.Meta.Core;

/// <summary>Where the meta keeps files and which programs it calls, resolved from the environment by Infrastructure.</summary>
/// <param name="StateDirectory">$AW_STATE_DIR/meta.</param>
/// <param name="GatesDirectory">Gate trees and step logs: &lt;state&gt;/gates.</param>
/// <param name="Heavy">The heavy-job wrapper, or null when it isn't installed (heavy steps then run directly).</param>
/// <param name="Gh">The GitHub CLI program.</param>
/// <param name="Home">The user's home directory ("~" in a worktree root); empty when unknown.</param>
/// <param name="UserConfig">The user config folder: $XDG_CONFIG_HOME, else &lt;home&gt;/.config; empty when unknown.</param>
/// <param name="Reserve">The installed meta-gate-reserve (bin-r1b/meta-gate-reserve), or null when unknown.</param>
/// <param name="Toolkit">The toolkit checkout the scripts come from ($AW_TOOLKIT, else ~/.agentic-workflow/toolkit), or null when unknown.</param>
public sealed record MetaPaths(string StateDirectory, string GatesDirectory, string? Heavy, string Gh, string Home = "", string UserConfig = "", string? Reserve = null, string? Toolkit = null)
{
    /// <summary>One pull request's gate directory: gates/&lt;pr&gt;.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The directory.</returns>
    public string GateDirectory(int pr) => string.Create(CultureInfo.InvariantCulture, $"{GatesDirectory}/{pr}");
}
