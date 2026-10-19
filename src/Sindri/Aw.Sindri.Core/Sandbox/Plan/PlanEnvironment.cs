using System.Globalization;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Builds the environment from the allowlist (sandbox spec §3.4): nothing is copied from the owner's environment except the named variables.</summary>
internal static class PlanEnvironment
{
    private const string FallbackPath = "/usr/local/bin:/usr/bin:/bin";

    internal static IReadOnlyList<PlanEnvVar> Build(PlanContext c)
    {
        var r = c.Request;
        var host = r.Host.Environment;
        string FromHost(string name, string fallback) => host.TryGetValue(name, out var v) && v.Length > 0 ? v : fallback;
        List<PlanEnvVar> env =
        [
            Var("PATH", $"{c.RunArea}/shims:{c.StateDir}/bin-r1b:{ToolPath(r.Host, FromHost("PATH", FallbackPath))}"),
            Var("HOME", c.Home),
            Var("LANG", FromHost("LANG", "C.UTF-8")),
            Var("TERM", FromHost("TERM", "xterm-256color")),
            Var("TMPDIR", "/tmp"),
            Var("USER", FromHost("USER", "owner")),
            Var("SHELL", FromHost("SHELL", "/bin/sh")),
            Var("DOTNET_ROOT", FromHost("DOTNET_ROOT", c.Home + "/.dotnet")),
            Var("DOTNET_CLI_HOME", "/tmp/dotnet-home"),
            Var("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1"),
            Var("DOTNET_CLI_TELEMETRY_OPTOUT", "1"),
            Var("DOTNET_NOLOGO", "1"),
            Var("NUGET_PACKAGES", FromHost("NUGET_PACKAGES", c.Home + "/.nuget/packages")),
            Var("NUGET_HTTP_CACHE_PATH", "/tmp/nuget-http"),
            Var("NVM_DIR", FromHost("NVM_DIR", c.Home + "/.nvm")),
            Var("npm_config_cache", c.Home + "/.npm"),
            Var("npm_config_logs_dir", "/tmp/npm-logs"),
            Var("npm_config_ignore_scripts", "true"),
            Var("DISABLE_TELEMETRY", "1"),
            Var("DISABLE_AUTOUPDATER", "1"),
            Var("CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC", "1"),
            Var("DOTNET_PROCESSOR_COUNT", Cpus(host)),
            Var("MSBUILDDISABLENODEREUSE", "1"),
            Var("DOTNET_CLI_USE_MSBUILD_SERVER", "0"),
        ];
        env.AddRange(StateDir(c));
        env.AddRange(GitSettings.ToEnvironment(Git(c), c.RunArea + "/gitconfig.empty"));
        env.AddRange(Proxy(r.Net.ProxyPort));
        env.AddRange(Sindri(c));
        env.AddRange((r.Login?.Environment ?? new Dictionary<string, string>(StringComparer.Ordinal)).OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => Var(e.Key, EnvironmentAllowlist.IsModelKey(e.Key) ? null : e.Value)));
        return env;
    }

    // The hooks default to $HOME/.agentic-workflow; anywhere else they need to be told.
    private static IEnumerable<PlanEnvVar> StateDir(PlanContext c) =>
        string.Equals(c.StateDir, c.Home + "/.agentic-workflow", StringComparison.Ordinal) ? [] : [Var("AW_STATE_DIR", c.StateDir)];

    internal static IReadOnlyList<GitSetting> Git(PlanContext c) => GitSettings.For(c.Worktree, c.RunArea + "/githooks");

    private static PlanEnvVar Var(string name, string? value) => new(name, value, EnvironmentAllowlist.Names[name]);

    private static string Cpus(IReadOnlyDictionary<string, string> host) =>
        host.TryGetValue("AW_HEAVY_CPUS", out var text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n.ToString(CultureInfo.InvariantCulture)
            : "4";

    private static string ToolPath(SandboxHostFacts host, string path) =>
        string.Join(':', path.Split(':', StringSplitOptions.RemoveEmptyEntries).Where(p => !Inside(p, host.StateDir) && !Inside(p, host.SindriHome)));

    private static bool Inside(string path, string dir) => string.Equals(path, dir, StringComparison.Ordinal) || path.StartsWith(dir + "/", StringComparison.Ordinal);

    private static IEnumerable<PlanEnvVar> Proxy(int port)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}");
        foreach (var name in new[] { "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "https_proxy", "http_proxy", "all_proxy" })
        {
            yield return Var(name, url);
        }

        yield return Var("NO_PROXY", "127.0.0.1,localhost,::1");
        yield return Var("no_proxy", "127.0.0.1,localhost,::1");
    }

    private static IEnumerable<PlanEnvVar> Sindri(PlanContext c)
    {
        var r = c.Request;
        yield return Var("AW_SANDBOX", "1");
        yield return Var("AW_SANDBOX_PLAN", c.RunArea + "/sandbox-plan.json");
        if (r.Instance is { } instance)
        {
            yield return Var("AW_SINDRI_INSTANCE", instance.Value);
        }

        yield return Var("AW_SINDRI_RUN_DIR", r.Paths.RunDirectory);
        if (c.Worktree is { } worktree)
        {
            yield return Var("AW_SINDRI_WORKTREE", worktree);
        }

        yield return Var("AW_SINDRI_BRANCH", r.RunNamespace + "/work");
        yield return Var("AW_SINDRI_CHILD", "1");
        yield return Var("AW_SINDRI_ITEM", r.Item.Value);
        yield return Var("AW_SINDRI_LAUNCH", r.Launch.Value);
        if (!string.Equals(r.Host.SindriHome, r.Host.StateDir + "/sindri-app", StringComparison.Ordinal))
        {
            yield return Var("AW_SINDRI_HOME", r.Host.SindriHome);
        }

        if (r.SlotHeld)
        {
            yield return Var("AW_SANDBOX_SLOT_HELD", "1");
        }

        yield return Var("AW_SINDRI_LAUNCH_SECRET", value: null);
    }
}
