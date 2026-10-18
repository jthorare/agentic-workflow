namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The environment allowlist (sandbox spec §3.4): one list in code with a reason per name, and the secret-pattern check.</summary>
public static class EnvironmentAllowlist
{
    private const string GitConfigKey = "GIT_CONFIG_KEY_";
    private const string GitConfigValue = "GIT_CONFIG_VALUE_";

    private static readonly string[] _secretSuffixes = ["_TOKEN", "_KEY", "_SECRET"];
    private static readonly string[] _secretPrefixes = ["AWS_", "GH_", "GITHUB_", "AGENT_APP_"];
    private static readonly string[] _credentialNames = ["CLAUDE_CONFIG_DIR", "CODEX_HOME", "ANTHROPIC_API_KEY", "OPENAI_API_KEY"];
    private static readonly string[] _secretExceptions = ["AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY", "OPENAI_API_KEY"];

    /// <summary>Every name the plan may set, with the reason it is on the list. 'GIT_CONFIG_KEY_n' and 'GIT_CONFIG_VALUE_n' are listed by <see cref="IsListed"/>.</summary>
    public static IReadOnlyDictionary<string, string> Names { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PATH"] = "the toolchain directories behind the shim directory",
        ["HOME"] = "the empty home the session runs in",
        ["LANG"] = "locale",
        ["TERM"] = "terminal type for the tmux pane",
        ["TMPDIR"] = "the private scratch directory",
        ["USER"] = "tools read the user name",
        ["SHELL"] = "tools start the user's shell",
        ["DOTNET_ROOT"] = "the .NET SDK location",
        ["DOTNET_CLI_HOME"] = "the CLI's first-run files go to scratch, not the home",
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "no first-run banner",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "no telemetry",
        ["DOTNET_NOLOGO"] = "no banner",
        ["NUGET_PACKAGES"] = "the shared package cache",
        ["NUGET_HTTP_CACHE_PATH"] = "the HTTP cache goes to scratch",
        ["NVM_DIR"] = "node version manager location",
        ["npm_config_cache"] = "the shared npm cache",
        ["npm_config_logs_dir"] = "npm logs go to scratch",
        ["npm_config_ignore_scripts"] = "install scripts are off",
        ["NODE_OPTIONS"] = "only when the repo sets it",
        ["DISABLE_TELEMETRY"] = "the inner policy turns telemetry off",
        ["DISABLE_AUTOUPDATER"] = "no auto-update call to the download host (spike item 11)",
        ["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "the inner policy turns update checks off",
        ["GIT_CONFIG_COUNT"] = "the sandbox's git settings at command-line scope",
        ["GIT_CONFIG_GLOBAL"] = "an empty file: the owner's .gitconfig is absent",
        ["CLAUDE_CONFIG_DIR"] = "the credential source's agent home",
        ["CODEX_HOME"] = "the credential source's agent home",
        ["ANTHROPIC_API_KEY"] = "the target's own model credential (API-key mode), from the credential source only",
        ["OPENAI_API_KEY"] = "the target's own model credential (API-key mode), from the credential source only",
        ["DOTNET_PROCESSOR_COUNT"] = "heavy sets it only outside a held slot, so the sandbox always does",
        ["MSBUILDDISABLENODEREUSE"] = "no resident MSBuild nodes",
        ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "no MSBuild server",
        ["HTTPS_PROXY"] = "the egress proxy",
        ["HTTP_PROXY"] = "the egress proxy",
        ["ALL_PROXY"] = "the egress proxy",
        ["https_proxy"] = "the egress proxy",
        ["http_proxy"] = "the egress proxy",
        ["all_proxy"] = "the egress proxy",
        ["NO_PROXY"] = "keeps the Sindri port off the proxy",
        ["no_proxy"] = "keeps the Sindri port off the proxy",
        ["AW_SANDBOX"] = "tells the agent it is sandboxed",
        ["AW_SANDBOX_PLAN"] = "the value-free summary file 'sandbox explain' reads",
        ["AW_SANDBOX_SLOT_HELD"] = "the Step already holds a heavy slot: the shims run the real tool",
        ["AW_SINDRI_INSTANCE"] = "the workflow instance",
        ["AW_SINDRI_RUN_DIR"] = "the run directory",
        ["AW_SINDRI_WORKTREE"] = "the worktree",
        ["AW_SINDRI_BRANCH"] = "the run's branch",
        ["AW_SINDRI_CHILD"] = "marks a Sindri-launched process",
        ["AW_SINDRI_ITEM"] = "the item",
        ["AW_SINDRI_LAUNCH"] = "the launch",
        ["AW_SINDRI_HOME"] = "only when it is not the default",
        ["AW_SINDRI_LAUNCH_SECRET"] = "the launch secret, supplied by session wrap at start",
    };

    /// <summary>Whether a credential source may set <paramref name="name"/>: only the agent home and the target's own model key (sandbox spec §3.4).</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True when it may.</returns>
    public static bool IsCredentialName(string name) => _credentialNames.Contains(name, StringComparer.Ordinal);

    /// <summary>Whether <paramref name="name"/> is a model API key: its value is supplied by the launcher at start and never stored in the plan or its hash.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True for ANTHROPIC_API_KEY and OPENAI_API_KEY.</returns>
    public static bool IsModelKey(string name) => string.Equals(name, "ANTHROPIC_API_KEY", StringComparison.Ordinal) || string.Equals(name, "OPENAI_API_KEY", StringComparison.Ordinal);

    /// <summary>Whether a name looks like a secret: *_TOKEN, *_KEY, *_SECRET, *PASSWORD*, AWS_*, GH_*, GITHUB_*, AGENT_APP_*.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True when it does.</returns>
    public static bool IsSecretShaped(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _secretSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            || _secretPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            || name.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a name is on the list (the numbered git settings count).</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True when it is.</returns>
    public static bool IsListed(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Names.ContainsKey(name)
            || (name.StartsWith(GitConfigKey, StringComparison.Ordinal) && name.AsSpan(GitConfigKey.Length).IndexOfAnyExceptInRange('0', '9') < 0)
            || (name.StartsWith(GitConfigValue, StringComparison.Ordinal) && name.AsSpan(GitConfigValue.Length).IndexOfAnyExceptInRange('0', '9') < 0);
    }

    /// <summary>The names that are secret-shaped and not on the list with a reason.</summary>
    /// <param name="names">Variable names.</param>
    /// <returns>The offenders, in order.</returns>
    public static IReadOnlyList<string> Violations(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return [.. names.Where(n => IsSecretShaped(n) && !_secretExceptions.Contains(n, StringComparer.Ordinal))];
    }
}
