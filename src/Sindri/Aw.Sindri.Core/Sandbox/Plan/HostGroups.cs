namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The named host groups a session may reach (sandbox spec §3.2).</summary>
public static class HostGroups
{
    private const string Model = "model";
    private const string Packages = "packages";
    private const string Github = "github";
    private const string Repo = "repo";

    private static readonly string[] _packageHosts = ["api.nuget.org", "*.nuget.org", "registry.npmjs.org"];
    private static readonly string[] _githubHosts = ["github.com", "api.github.com"];

    /// <summary>Resolves the groups of one launch: built-in, the profile's, the repo's, minus what the Step removes.</summary>
    /// <param name="input">What the groups depend on.</param>
    /// <returns>The groups.</returns>
    public static IReadOnlyList<PlanGroup> Resolve(GroupsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hosts = KnownHosts(input);
        List<PlanGroup> groups = [.. Names(input).Select(n => new PlanGroup(n, hosts.TryGetValue(n, out var h) ? h : []))];
        if (input.Repo?.Hosts is { Count: > 0 } extra)
        {
            groups.Add(new PlanGroup(Repo, extra));
        }

        var omit = input.Omit ?? [];
        return [.. groups.Where(g => !omit.Contains(g.Name, StringComparer.Ordinal))];
    }

    private static Dictionary<string, IReadOnlyList<string>> KnownHosts(GroupsInput input)
    {
        var hosts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Model] = ModelHosts(input.Target.Value),
            [Packages] = _packageHosts,
            [Github] = _githubHosts,
            ["signing"] = [],
            ["releases"] = [],
        };
        foreach (var (name, profileHosts) in input.Settings?.Network?.Groups ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal))
        {
            hosts[name] = profileHosts;
        }

        return hosts;
    }

    private static List<string> Names(GroupsInput input)
    {
        List<string> names = [Model];
        if (input.WriteScope)
        {
            names.Add(Packages);
        }

        if (input.Github == GithubCredentials.AgentApp && !input.Attended)
        {
            names.Add(Github);
        }

        names.AddRange((input.Repo?.Groups ?? []).Where(g => !names.Contains(g, StringComparer.Ordinal) && RepoMayAdd(g, input)));
        return names;
    }

    /// <summary>Whether a host pattern ('api.nuget.org' or '*.nuget.org') matches a host.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="host">The host name.</param>
    /// <returns>True when it matches.</returns>
    public static bool Matches(string pattern, string host)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(host);
        return pattern.StartsWith("*.", StringComparison.Ordinal)
            ? host.Length > pattern.Length - 1 && host.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The group that allows <paramref name="host"/>, or null.</summary>
    /// <param name="groups">The plan's groups.</param>
    /// <param name="host">The host name.</param>
    /// <returns>The group, or null.</returns>
    public static PlanGroup? Allowing(IReadOnlyList<PlanGroup> groups, string host)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return groups.FirstOrDefault(g => g.Hosts.Any(p => Matches(p, host)));
    }

    private static bool RepoMayAdd(string group, GroupsInput input) => group switch
    {
        Github => input.Github == GithubCredentials.AgentApp && !input.Attended,
        Packages => input.WriteScope,
        _ => true,
    };

    private static string[] ModelHosts(string target) => target switch
    {
        "claude-code" => ["api.anthropic.com"],
        "codex" => ["api.openai.com", "chatgpt.com"],
        _ => [],
    };
}
