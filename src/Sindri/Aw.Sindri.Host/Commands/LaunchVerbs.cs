using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri launch init [--viewer &lt;name&gt;] [--force] [--stdout]' and 'sindri launch show [&lt;id&gt;] [--json]' (API P10a, U7; launcher spec §4):
/// box-local, so they run without the control plane. Exit 0 ok, 1 refused (a file exists without --force), 2 usage or error.
/// 'launch probe' is the ProbeLaunchTargets row's (a job on the running control plane), not these.
/// </summary>
/// <param name="setup">The launcher's box-local side.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class LaunchVerbs(ILaunchSetup setup, Output output)
{
    internal const string Usage = "launch init [--viewer <name>] [--force] [--stdout] | launch show [<id>] [--json]";

    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["init", .. var flags] => Init(flags, ct),
        ["show", .. var rest] => Show(rest, ct),
        _ => Usage2(),
    };

    private static string Describe(LaunchInitReport report) =>
        report.Path is { } path ? $"Wrote {path}: {report.Viewer} ({report.Reason}). The next open uses it.\n" : report.Json;

    private async Task<int> Init(string[] flags, CancellationToken ct)
    {
        string? viewer = null;
        bool force = false, stdout = false;
        for (var i = 0; i < flags.Length; i++)
        {
            switch (flags[i])
            {
                case "--viewer" when i + 1 < flags.Length && !flags[i + 1].StartsWith("--", StringComparison.Ordinal):
                    viewer = flags[++i];
                    break;
                case "--force":
                    force = true;
                    break;
                case "--stdout":
                    stdout = true;
                    break;
                default:
                    return await Usage2().ConfigureAwait(false);
            }
        }

        var made = await setup.Init(new LaunchInitRequest(viewer, force, stdout), ct).ConfigureAwait(false);
        if (made.IsError)
        {
            var code = await output.Error(made.Error).ConfigureAwait(false);
            return made.Error.Kind == ErrorKind.Conflict ? 1 : code;
        }

        return await output.Text(stdout ? made.Value.Json.TrimEnd('\n') + "\n" : Describe(made.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Show(string[] rest, CancellationToken ct)
    {
        var json = rest.Contains("--json", StringComparer.Ordinal);
        var ids = rest.Where(static a => a != "--json").ToArray();
        if (ids.Length > 1 || ids.Any(static a => a.StartsWith("--", StringComparison.Ordinal)) || (ids.Length == 1 && !AgentId.TryFrom(ids[0], out _)))
        {
            return await Usage2().ConfigureAwait(false);
        }

        var shown = await setup.Show(ids.Length == 1 ? AgentId.From(ids[0]) : null, ct).ConfigureAwait(false);
        if (shown.IsError)
        {
            return await output.Error(shown.Error).ConfigureAwait(false);
        }

        return json ? await output.Json(shown.Value, 0).ConfigureAwait(false) : await output.Text(Render(shown.Value), 0).ConfigureAwait(false);
    }

    private static string Render(LaunchShowReport report)
    {
        var text = new StringBuilder($"Terminal: {report.Terminal} ({report.TerminalSource}) · {report.ConfigPath}\n");
        foreach (var e in report.Entries)
        {
            _ = text.Append($"{e.Id.Value}  {e.Source.ToString().ToLowerInvariant()}  {e.ResolvedBin ?? (e.Bin + " (not found)")}  {e.ArgvHash}  Steps: {(e.StepOk ? "ok" : "parked")}\n");
            if (e.ParkLine is { } line)
            {
                _ = text.Append("  ").Append(line).Append('\n');
            }
        }

        foreach (var problem in report.Problems)
        {
            _ = text.Append("warning: ").Append(problem).Append('\n');
        }

        return text.ToString();
    }

    private async Task<int> Usage2() => await output.Error(SindriErrors.UnknownVerb.With(Usage)).ConfigureAwait(false);
}
