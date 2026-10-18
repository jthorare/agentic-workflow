using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What the pieces of the builder share: the request and the values resolved from it once.</summary>
internal sealed record PlanContext(
    SandboxRequest Request,
    string? WritablePath,
    ModelLoginSource Login,
    GithubCredentials Github,
    PlanLimits Limits,
    IReadOnlyList<PlanGroup> Groups,
    SandboxRepoSettings Repo)
{
    internal const string Always = "always blocked: ";

    internal bool WriteScope => WritablePath is not null;

    internal string Home => Request.Host.Home;

    internal string StateDir => Request.Host.StateDir;

    internal string RunArea => Request.Paths.RunArea;

    internal string? Worktree => Request.Paths.Worktree;

    internal static PlanContext From(SandboxRequest request)
    {
        var settings = request.Settings;
        var repo = RepoDeclaration.For(settings, request.Repo);
        var writable = request.Attended ? request.Paths.Worktree : request.Policy!.WritablePath;   // why: Build checks a Step carries its policy
        var github = request.Attended ? GithubCredentials.None : settings?.Credentials?.Github ?? SandboxDefaults.Github;
        var limits = LimitsResolver.Resolve(new LimitsInput(
            settings?.Limits, settings?.Attended, request.Attended, request.StepTimeout, request.LongMax, request.Host.TotalMemoryBytes, request.Host.CpuCores));
        var groups = HostGroups.Resolve(new GroupsInput(settings, repo, request.Target, writable is not null, request.Attended, github, request.OmitGroups));
        return new PlanContext(request, writable, LoginSourceResolver.Resolve(settings, request.Target, request.Repo, request.Policy?.LoginSource), github, limits, groups, repo);
    }

    /// <summary>Every path the session can't see or read, in the order the mount stack denies them: permanent ones first, then the owner's.</summary>
    internal IReadOnlyList<DeniedPath> Denied()
    {
        var host = Request.Host;
        List<DeniedPath> denied =
        [
            new(StateDir, Always + "the ledger, profile, tokens and other runs' areas live here"),
        ];
        if (!string.Equals(host.SindriHome, StateDir, StringComparison.Ordinal))
        {
            denied.Add(new DeniedPath(host.SindriHome, Always + "Sindri's own state: the ledger, tokens and other runs' areas"));
        }

        denied.Add(new DeniedPath("/var/run/docker.sock", Always + "the Docker socket is root on the host"));
        denied.Add(new DeniedPath("/run/podman", Always + "the Podman socket is root on the host"));
        denied.Add(new DeniedPath("/run/user/" + host.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture), Always + "the ssh, gpg and dbus agent sockets sign as the owner"));
        denied.Add(new DeniedPath("/etc/ssh", Always + "host keys"));
        denied.Add(new DeniedPath("/root", Always + "not the session's"));
        denied.AddRange(host.OtherHomes.Select(h => new DeniedPath(h, Always + "other users' files")));
        if (host.IsMacOs)
        {
            denied.Add(new DeniedPath(Home + "/Library/Keychains", Always + "the owner's logins and keys"));
        }

        return denied;
    }

    /// <summary>The owner's extra denies: the profile's 'sandbox.fs.denyExtra' and the line's capability policy. They are the last word in the stack.</summary>
    internal IReadOnlyList<DeniedPath> OwnerDenied() =>
    [
        .. (Request.Settings?.Fs?.DenyExtra ?? []).Select(p => new DeniedPath(p, "blocked by sandbox.fs.denyExtra in the profile")),
        .. (Request.ExtraDeny ?? []).Select(p => new DeniedPath(p, "blocked by the line's capability policy")),
    ];
}
