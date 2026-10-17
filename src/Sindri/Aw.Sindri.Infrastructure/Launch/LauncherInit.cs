using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary><c lang="text">sindri launch init</c> and <c lang="text">sindri launch show</c> (API P10a, U7; launcher spec §4 "The generator").</summary>
internal sealed class LauncherInit(LauncherInitParts parts) : ILaunchSetup
{
    private static readonly System.Text.Json.JsonSerializerOptions _pretty = AwJson.CreateOptions(LauncherJsonContext.Default, indented: true);

    /// <inheritdoc/>
    public async ValueTask<Result<LaunchInitReport>> Init(LaunchInitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chosen = request.Viewer is { Length: > 0 } name ? Named(name) : await TerminalDetection.Detect(parts.Detection, cancellationToken).ConfigureAwait(false);
        if (chosen is null)
        {
            return KernelErrors.Invalid.With($"'{request.Viewer}' is not a viewer (fix: print, warp or one of {string.Join(", ", parts.Detection.Templates.Select(static t => t.Id))})");
        }

        var json = Render(chosen);
        if (LauncherConfigFile.Read(LauncherStartup.Builtins(), Encoding.UTF8.GetBytes(json)) is { IsError: true } invalid)
        {
            return invalid.Error;
        }

        var path = parts.Catalog.Path;
        if (request.ToStdout)
        {
            return new LaunchInitReport(chosen.Label, chosen.Reason, Path: null, json);
        }

        if (File.Exists(path) && !request.Force)
        {
            return KernelErrors.Conflict.With($"{path} already exists (fix: sindri launch init --force replaces it, or --stdout prints the new one)");
        }

        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);   // why: a full path has a directory
        OwnerOnly.WriteAtomically(path, Encoding.UTF8.GetBytes(json));
        return new LaunchInitReport(chosen.Label, chosen.Reason, path, json);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<LaunchShowReport>> Show(AgentId? only, CancellationToken cancellationToken)
    {
        var config = parts.Catalog.Current;
        var chosen = only is null ? config.Agents : [.. config.Agents.Where(a => a.Id.Equals(only.Value))];
        if (only is not null && chosen.Count == 0)
        {
            return KernelErrors.NotFound.With($"{only.Value.Value} is not in the catalog (fix: sindri launch show lists the entries)");
        }

        var approved = await parts.Approved(cancellationToken).ConfigureAwait(false);
        var entries = new List<LaunchShowEntry>();
        foreach (var entry in chosen)
        {
            var resolved = await parts.ResolveBin(entry.Bin, cancellationToken).ConfigureAwait(false);
            var check = StepApproval.Check(entry, parts.BuiltinHash(entry.Id), approved?.StepArgvHashes(entry.Id) ?? []);
            var park = check.IsSuccess ? null : $"Steps on `{entry.Id.Value}` will park: approve `agents.{entry.Id.Value}.stepArgvHash: {entry.ArgvHash}`";
            entries.Add(new LaunchShowEntry(entry.Id, entry.Source, entry.Bin, resolved, entry.ArgvHash, check.IsSuccess, park));
        }

        var (terminal, source) = await Terminal(config, cancellationToken).ConfigureAwait(false);
        return new LaunchShowReport(terminal, source, parts.Catalog.Path, entries, Problems(config, parts.Catalog.Path));
    }

    private async ValueTask<(string Terminal, string Source)> Terminal(LauncherConfig config, CancellationToken cancellationToken)
    {
        if (config.Viewer is { } viewer && config.AttendedProblems.Count == 0)
        {
            return (viewer.Kind == "exec-template" ? $"{viewer.Name} (exec-template)" : viewer.Kind, "file");
        }

        var detected = await TerminalDetection.Detect(parts.Detection, cancellationToken).ConfigureAwait(false);
        return (detected.Kind == "print" ? "print" : $"{detected.Label} ({detected.Kind})", "detected");
    }

    private static List<string> Problems(LauncherConfig config, string path)
    {
        var problems = new List<string>();
        if (config.Problem is { } problem)
        {
            problems.Add(problem);
        }

        problems.AddRange(config.AttendedProblems);
        problems.AddRange(config.Dropped.Select(static d => $"agents.{d.Id} is dropped (line {d.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)}): {d.Reason}"));
        problems.AddRange(LauncherFilePermissions.Warnings(path));
        return problems;
    }

    private DetectedTerminal? Named(string name) => name switch
    {
        "print" => new DetectedTerminal("print", "print", Template: null, "--viewer"),
        "warp" => new DetectedTerminal("warp", "Warp", Template: null, "--viewer"),
        _ => parts.Detection.Templates.FirstOrDefault(t => string.Equals(t.Id, name, StringComparison.OrdinalIgnoreCase)) is { } template
            ? new DetectedTerminal("exec-template", template.Name, template, "--viewer")
            : null,
    };

    private static string Render(DetectedTerminal chosen)
    {
        var viewer = chosen.Template is { } template ? new ViewerSection("exec-template") { Name = template.Name, Argv = template.Argv } : new ViewerSection(chosen.Kind);
        return Encoding.UTF8.GetString(AwJson.Serialize(new LauncherDocument(Schema: 1) { Viewer = viewer }, _pretty)) + "\n";
    }
}
