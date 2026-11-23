using System.Globalization;
using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ScanRepos;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri setup scan [--root DIR] [--depth N] [--json]' (first-run spec §3.3, §5.1): looks for repos under a folder of the home through GET /sindri/setup/repos and prints each as the walk finds it; --json prints
/// the route's NDJSON lines as they come. It needs the running control plane and never starts it. Exit 0 when the scan ends; 1 when Sindri isn't running or the answer stops before its last line; 2 on usage,
/// SND-SETUP-008 (a root outside the home folder) or SND-AUTH-002 (a session Sindri launched).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class SetupScanVerb(ControlPlaneClient client, Output output)
{
    internal const string Usage = "setup scan [--root DIR] [--depth N] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (Route(args) is not { } route)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'setup scan {string.Join(' ', args)}' isn't this command ({Usage})")).ConfigureAwait(false);
        }

        var json = args.Contains("--json");
        RepoScanEnd? end = null;
        await foreach (var line in client.Lines(route, ct).ConfigureAwait(false))
        {
            if (line.IsError)
            {
                return await output.Error(line.Error).ConfigureAwait(false);
            }

            end = output.Read<RepoScanEnd>(Encoding.UTF8.GetBytes(line.Value)) is { IsSuccess: true, Value.Done: true } closing ? closing.Value : null;
            await output.Out.WriteLineAsync(json ? line.Value : Text(line.Value, end)).ConfigureAwait(false);
        }

        return end is not null ? 0 : await output.Error(SindriErrors.NotRunning.With("the scan stopped before its last line (fix: sindri setup scan)")).ConfigureAwait(false);
    }

    // '--root DIR', '--depth N' and '--json' in any order, each once.
    private static string? Route(string[] args)
    {
        var query = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var known = args[i] switch
            {
                "--json" => true,
                "--root" when i + 1 < args.Length => Add(query, "root", args[++i]),
                "--depth" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out _) => Add(query, "depth", args[++i]),
                _ => false,
            };
            if (!known)
            {
                return null;
            }
        }

        return query.Count == 0 ? "/sindri/setup/repos" : "/sindri/setup/repos?" + string.Join('&', query);
    }

    private static bool Add(List<string> query, string name, string value)
    {
        var once = !query.Exists(q => q.StartsWith(name + "=", StringComparison.Ordinal));
        query.Add(name + "=" + Uri.EscapeDataString(value));
        return once;
    }

    private string Text(string line, RepoScanEnd? end)
    {
        if (end is not null)
        {
            var counts = string.Create(CultureInfo.InvariantCulture, $"Looked at {end.Folders} folders and found {end.Repos} repos. Add one: sindri repo add <path>");
            return end.KnownFailed ? counts + "\nCouldn't read your GitHub repos, so none is marked known (retry: sindri setup scan)" : counts;
        }

        if (output.Read<RepoScanRow>(Encoding.UTF8.GetBytes(line)) is not { IsSuccess: true } row)
        {
            return line;
        }

        var r = row.Value;
        var marks = string.Concat(r.Known ? "  known" : string.Empty, r.InProfile ? "  in profile" : string.Empty,
            r.Worktrees > 0 ? string.Create(CultureInfo.InvariantCulture, $"  +{r.Worktrees} worktrees") : string.Empty);
        return $"{r.Path}  {(r.Slug is null ? "(no origin)" : r.Host + "/" + r.Slug)}{marks}";
    }
}
