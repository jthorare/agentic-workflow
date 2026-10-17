using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Onyx;

// A project with no files (ComposeProject.ByLabel) is found and stopped through `docker ps` and `docker stop` alone: compose without its files would look for a
// compose file in the caller's working directory, which is nothing of ours. Containers are named by the project label compose puts on each of them.
internal sealed partial class DockerCliRuntime
{
    private const string ProjectLabel = "com.docker.compose.project";
    private const string ServiceLabel = "com.docker.compose.service";

    /// <inheritdoc />
    public Result<long> MemoryUsedOutside(ComposeProject project, CancellationToken cancellationToken)
    {
        if (LabelFilter(project) is not { } filter)
        {
            return NotOurs(project).Error;
        }

        var ours = Direct(["ps", "-q", "--filter", filter], new Call("ps", _timeout), cancellationToken);
        if (ours.IsError)
        {
            return ours.Error;
        }

        var stats = Direct(["stats", "--no-stream", "--format", "{{.ID}}|{{.MemUsage}}"], new Call("stats", _timeout), cancellationToken);
        if (stats.IsError)
        {
            return stats.Error;
        }

        var own = Split(ours.Value).ToHashSet(StringComparer.Ordinal);
        long total = 0;
        foreach (var line in Split(stats.Value))
        {
            var bar = line.IndexOf('|', StringComparison.Ordinal);
            if (bar <= 0 || !TryBytes(line[(bar + 1)..].Split('/')[0], out var bytes))
            {
                return SindriErrors.OnyxComposeFailed.With("`docker stats` printed a memory figure Sindri can't read; update Docker, then run this again");
            }

            total += own.Contains(line[..bar]) ? 0 : bytes;
        }

        return total;
    }

    // `1.5GiB`, `512MiB`, `1024KiB`, `2048B`, `1.5GB`: a number and a unit, binary (KiB) or decimal (kB, MB, GB).
    private static bool TryBytes(string text, out long bytes)
    {
        bytes = 0;
        var trimmed = text.Trim();
        var cut = trimmed.Length;
        while (cut > 0 && char.IsAsciiLetter(trimmed[cut - 1]))
        {
            cut--;
        }

        long? unit = trimmed[cut..] switch
        {
            "B" => 1,
            "kB" or "KB" => 1_000,
            "KiB" => 1L << 10,
            "MB" => 1_000_000,
            "MiB" => 1L << 20,
            "GB" => 1_000_000_000,
            "GiB" => 1L << 30,
            "TB" => 1_000_000_000_000,
            "TiB" => 1L << 40,
            _ => null,
        };
        if (unit is not { } size || !double.TryParse(trimmed[..cut], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount < 0)
        {
            return false;
        }

        bytes = (long)Math.Round(amount * size, MidpointRounding.AwayFromZero);
        return true;
    }

    private Result<Unit> StopByLabel(ComposeProject project, CancellationToken cancellationToken)
    {
        var live = LabelFilter(project) is { } filter ? Direct(["ps", "-q", "--filter", filter], new Call("ps", _shortTimeout), cancellationToken) : NotOurs(project);
        if (live.IsError)
        {
            return live.Error;
        }

        var ids = Split(live.Value);
        if (ids.Count == 0)
        {
            return Results.Ok();
        }

        if (ids.Exists(static id => !IsContainerId(id)))
        {
            return SindriErrors.OnyxComposeFailed.With("`docker ps -q` printed something that isn't a container id; check `docker ps`, then run this again");
        }

        return Direct(["stop", .. ids], new Call("stop", _stopTimeout, CutShort: true, Stop: true), cancellationToken).Map(static _ => Unit.Value);
    }

    private Result<IReadOnlyList<ComposeContainer>> PsByLabel(ComposeProject project, CancellationToken cancellationToken)
    {
        if (LabelFilter(project) is not { } filter)
        {
            return NotOurs(project).Error;
        }

        var listed = Direct(["ps", "-a", "--filter", filter, "--format", $$$"""{{.ID}}|{{.State}}|{{.Label "{{{ServiceLabel}}}"}}|{{.Image}}|{{.Ports}}"""], new Call("ps", _shortTimeout), cancellationToken);
        if (listed.IsError)
        {
            return listed.Error;
        }

        List<ComposeContainer> containers = [];
        foreach (var line in Split(listed.Value))
        {
            var parts = line.Split('|', 5);
            if (parts.Length != 5 || parts[1].Length == 0 || parts[2].Length == 0)
            {
                return SindriErrors.OnyxComposeFailed.With("`docker ps` printed a container Sindri can't read; update Docker, then run this again");
            }

            containers.Add(new ComposeContainer(parts[2], parts[1], Health: null, parts[3], PortsOf(parts[4])));
        }

        return containers;
    }

    private Result<string> Direct(string[] arguments, Call call, CancellationToken cancellationToken)
    {
        var tail = new Tail();
        var run = runner.Run("docker", arguments, call.Timeout, new ToolRunOptions(tail.Add), cancellationToken);
        return Failed(run, tail, call, cancellationToken) is { } error ? error : run.Output;
    }

    // Only a name Sindri made is put in a filter: a recorded name can't smuggle another project or an option into the command.
    private static string? LabelFilter(ComposeProject project) => ComposeProject.IsOurs(project.Name) ? $"label={ProjectLabel}={project.Name}" : null;

    private static Result<string> NotOurs(ComposeProject project)
        => SindriErrors.OnyxComposeFailed.With($"refusing to act on \"{project.Name}\": it isn't a compose project Sindri names");

    private static List<string> Split(string text) => [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static bool IsContainerId(string id) => id.Length is >= 12 and <= 64 && id.All(static c => char.IsAsciiHexDigitLower(c));

    // `127.0.0.1:3000->80/tcp, [::]:3000->80/tcp`: one publisher per published single port; an exposed-only port and a range are skipped.
    private static List<ComposePublisher> PortsOf(string text)
    {
        List<ComposePublisher> publishers = [];
        foreach (var entry in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var arrow = entry.IndexOf("->", StringComparison.Ordinal);
            var colon = arrow < 0 ? -1 : entry.LastIndexOf(':', arrow);
            var slash = arrow < 0 ? -1 : entry.IndexOf('/', arrow);
            if (colon < 0 || slash < 0
                || !int.TryParse(entry.AsSpan(colon + 1, arrow - colon - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var published)
                || !int.TryParse(entry.AsSpan(arrow + 2, slash - arrow - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var target))
            {
                continue;
            }

            publishers.Add(new ComposePublisher(entry[..colon].Trim('[', ']'), published, target));
        }

        return publishers;
    }
}
