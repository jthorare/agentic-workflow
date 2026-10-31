using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The Seatbelt form of a plan (sandbox spec §3.1, SB-9): Seatbelt has no bind mounts, no tmpfs and no network namespace, so it can only allow or deny REAL paths.
/// Every remapped entry names its host path, the empty home becomes a per-launch folder of links, and what a bind would have hidden is denied, with the allowed entries after the deny that covers them (the last matching rule wins).
/// </summary>
internal static class SeatbeltPlan
{
    internal static SandboxPlan Rewrite(SandboxPlan plan, PlanContext c)
    {
        var rewriter = new Rewriter(c);
        var mounts = rewriter.Mounts(plan.Mounts);
        return plan with
        {
            Mounts = mounts,
            Environment = rewriter.Environment(plan.Environment),
            Inner = InnerSandbox.Off,   // why: the agent CLIs run Seatbelt themselves on macOS and a profile nested in a profile is expected to fail; to be proven on a Mac before this changes
        };
    }

    /// <summary>The rule for a Seatbelt plan: a link sits inside the run area or the launch's own session home (where control.json is linked) and never on its own target, because the run-area preparer replaces whatever is at a link's path.</summary>
    /// <param name="plan">The rewritten plan.</param>
    /// <param name="runArea">The launch's run area.</param>
    /// <param name="sessionHome">The launch's session home.</param>
    /// <returns>SND-LAUNCH-016 when a link breaks the rule, else null.</returns>
    internal static Error? Check(SandboxPlan plan, string runArea, string sessionHome)
    {
        var bad = plan.Mounts.FirstOrDefault(m => m.Kind == PlanMountKind.Link && (string.Equals(m.Path, m.Source, StringComparison.Ordinal) || !(Under(m.Path, runArea) || Under(m.Path, sessionHome))));
        return bad is null ? null : SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsDeny, $"the link '{bad.Path}' would replace a path outside the run area and the session home or point at itself");
    }

    private static bool Under(string path, string directory) =>
        string.Equals(path, directory, StringComparison.Ordinal) || path.StartsWith(directory.TrimEnd('/') + "/", StringComparison.Ordinal);

    private sealed class Rewriter(PlanContext c)
    {
        private readonly List<(string From, string To)> _rebase = [];
        private readonly List<PlanMount> _out = [];
        private readonly HashSet<PlanMount> _moved = new(ReferenceEqualityComparer.Instance);   // real paths that may sit inside a directory the stack denies later: they go back after that deny
        private readonly string _homeDir = c.RunArea + "/home";
        private readonly string _current = c.StateDir + "/bin/cs/current";
        private readonly string _control = c.SessionHome + "/control.json";
        private readonly List<string> _linked = [];   // the inside paths a link already stands for: anything below one reaches the owner's folder through it
        private int _homeStart;
        private int _homeEnd;

        internal List<PlanMount> Mounts(IReadOnlyList<PlanMount> all)
        {
            var ownerCount = c.OwnerDenied().Count;
            var body = all.Take(all.Count - ownerCount).ToList();
            _homeStart = body.FindIndex(m => m.Kind == PlanMountKind.Tmpfs && string.Equals(m.Path, c.Home, StringComparison.Ordinal));
            _homeEnd = body.FindIndex(m => m.Kind == PlanMountKind.Deny && string.Equals(m.Path, c.StateDir, StringComparison.Ordinal));
            for (var i = 0; i < body.Count; i++)
            {
                Add(body[i], i);
            }

            _out.AddRange(all.Skip(all.Count - ownerCount));   // the owner's denies stay the last word
            return _out;
        }

        internal List<PlanEnvVar> Environment(IReadOnlyList<PlanEnvVar> env)
        {
            var result = env.Select(e => string.Equals(e.Name, "HOME", StringComparison.Ordinal) ? e with { Value = _homeDir } : e.Value is { } v ? e with { Value = Rebase(v) } : e).ToList();
            if (!result.Exists(e => string.Equals(e.Name, "AW_STATE_DIR", StringComparison.Ordinal)))
            {
                var at = result.FindIndex(e => string.Equals(e.Name, "HOME", StringComparison.Ordinal)) + 1;
                result.Insert(at, new PlanEnvVar("AW_STATE_DIR", c.StateDir, EnvironmentAllowlist.Names["AW_STATE_DIR"]));   // why: the hooks default to $HOME/.agentic-workflow, and HOME is no longer the real one
            }

            // why: the real logs, probe and digests folders are denied here, so the hooks write to the run area's spool. Seatbelt only: on Bubblewrap the hooks' defaults already land on the mounts, and the extra --setenv pairs would push a launch's argv toward tmux's command size cap.
            var hooks = result.FindIndex(e => string.Equals(e.Name, "AW_STATE_DIR", StringComparison.Ordinal)) + 1;
            result.InsertRange(hooks, [HookDir("AW_LOG_DIR", "logs"), HookDir("AW_PROBE_DIR", "probe"), HookDir("AW_DIGEST_DIR", "digests")]);

            return result;
        }

        private PlanEnvVar HookDir(string name, string folder) => new(name, Rebase($"{c.StateDir}/{folder}"), EnvironmentAllowlist.Names[name]);

        private void Add(PlanMount m, int index)
        {
            if (Dropped(m))
            {
                return;
            }

            if (m.Kind is PlanMountKind.Tmpfs or PlanMountKind.Deny)
            {
                AddDeny(m);
            }
            else if (string.Equals(m.Path, _control, StringComparison.Ordinal))
            {
                AddControl();
            }
            else if (m.Source is { } real)
            {
                AddRemapped(m, real);
            }
            else
            {
                _out.Add(m);
                AddHomeLink(m, index);
                if (m.Kind == PlanMountKind.ReadWrite && string.Equals(m.Path, c.Request.Host.SindriHome + "/launches", StringComparison.Ordinal))
                {
                    _out.Add(new PlanMount(c.Request.Host.SindriHome + "/launch/pgroups", PlanMountKind.ReadWrite, Reason: "the watchdog's process-group files: session wrap writes the memory-kill record here and removes its own group id file (no cgroups on macOS)"));
                }
            }
        }

        // Seatbelt needs none of these: its /dev, /proc and /run are the host's, and there is no net directory (the agent reaches the real ports).
        private static bool Dropped(PlanMount m) =>
            m.Kind is PlanMountKind.Dev or PlanMountKind.Proc || string.Equals(m.Path, "/run/sindri/net", StringComparison.Ordinal) || (m.Kind == PlanMountKind.Tmpfs && string.Equals(m.Path, "/run", StringComparison.Ordinal));

        private void AddDeny(PlanMount m)
        {
            var isHome = string.Equals(m.Path, c.Home, StringComparison.Ordinal);
            var deny = new PlanMount(m.Path, PlanMountKind.Deny, Reason: isHome ? "the owner's real home: HOME is this run's own folder, and only the entries below are allowed" : m.Reason);
            _out.Add(deny);
            var back = _out.Where(x => _moved.Contains(x) && Under(x.Path, deny.Path)).ToList();
            _ = _out.RemoveAll(x => _moved.Contains(x) && Under(x.Path, deny.Path));
            _out.AddRange(back);
            if (isHome)
            {
                var home = new PlanMount(_homeDir, PlanMountKind.PerLaunchDir, Reason: "HOME for this run: links to the home entries the session may use, thrown away with the run");
                _ = _moved.Add(home);
                _out.Add(home);
            }
        }

        private void AddControl()
        {
            var endpoint = c.Request.Host.SindriHome + "/control.json";
            _out.Add(new PlanMount(endpoint, PlanMountKind.ReadOnly, Reason: "the real endpoint file the session home's control.json links to"));
            _out.Add(new PlanMount(_control, PlanMountKind.Link, endpoint, Reason: "a link to the real endpoint file: no forwarder, the agent reaches Sindri's own port"));
        }

        private void AddRemapped(PlanMount m, string real)
        {
            if (string.Equals(m.Path, "/tmp", StringComparison.Ordinal))
            {
                var temp = SandboxPaths.SeatbeltTemp(c.Request.Launch);   // why: a short path, so a unix socket under TMPDIR fits in 104 bytes
                _rebase.Add((m.Path, temp));
                var scratch = new PlanMount(temp, PlanMountKind.PerLaunchDir, Reason: m.Reason);
                _ = _moved.Add(scratch);
                _out.Add(scratch);
                return;
            }

            if (string.Equals(m.Path, _current, StringComparison.Ordinal))
            {
                _out.Add(m with { Source = null, Reason = "the wrappers exec into 'current': the real link exists, and its stamp is allowed above" });
                return;
            }

            var logins = c.Request.Login?.Mounts.Select(x => x.SandboxPath) ?? [];
            if (logins.Any(r => Under(m.Path, r) && !string.Equals(m.Path, r, StringComparison.Ordinal)) && !_linked.Exists(l => Under(m.Path, l)))
            {
                _linked.Add(m.Path);
                _out.Add(new PlanMount(Rebase(m.Path), PlanMountKind.Link, real, Reason: m.Reason));   // the CLI writes under its config directory, so that folder is a link into the owner's
            }

            _rebase.Add((m.Path, real));
            var host = m with { Path = real, Source = null };
            _ = _moved.Add(host);
            _out.Add(host);
        }

        private void AddHomeLink(PlanMount m, int index)
        {
            var inZone = index > _homeStart && (_homeEnd < 0 || index < _homeEnd);
            if (inZone && m.Kind is PlanMountKind.ReadOnly or PlanMountKind.ReadWrite && m.Path.StartsWith(c.Home + "/", StringComparison.Ordinal))
            {
                _out.Add(new PlanMount($"{_homeDir}/{m.Path[(c.Home.Length + 1)..]}", PlanMountKind.Link, m.Path, Reason: m.Reason, Optional: m.Optional));
            }
        }

        private string Rebase(string path)
        {
            foreach (var (from, to) in _rebase.OrderByDescending(r => r.From.Length))
            {
                if (Under(path, from))
                {
                    return to + path[from.Length..];
                }
            }

            return path;
        }
    }
}
