using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'seed checks' command: sets the checks on the head.</summary>
internal static class SeedChecksCommand
{
    internal const string Name = "seed checks";

    internal static readonly CommandSpec Spec = new(
        "seed checks <link> --state none|pending|passing|failing [--failing <name,name>] [--passed <n>] [--pending <n>] [--runs <name:conclusion[:app],...>]", ["state"], ["failing", "passed", "pending", "runs"], [], Positionals: 1);

    internal static Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var seed = services.GetRequiredService<IForgeSeed>();
        Result<ChecksState> state = Enum.TryParse<ChecksState>(line["state"], ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : ForgeErrors.Invalid.With("state: none, pending, passing or failing");
        var failing = (line["failing"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = forge.Parse(line.Positionals[0]).Bind(pull => state.Bind(s => Count(line, "passed").Bind(passed => Count(line, "pending").Bind(pending => Runs(line).Bind(runs =>
            seed.SeedChecks(new SeedChecksRequest(pull, s, failing, new Dictionary<string, string>(StringComparer.Ordinal), passed, pending, runs)))))))
            .Map(_ => new Applied(Ok: true));
        return Task.FromResult(CommandOutput.Write(result, json));
    }

    // why: the runs carry no sha here; the forge fills in the pull request's head when it stores them.
    private static Result<IReadOnlyList<CheckRun>?> Runs(CommandLine line)
    {
        if (line["runs"] is not { } text)
        {
            return Results.Success<IReadOnlyList<CheckRun>?>(value: null);
        }

        var runs = new List<CheckRun>();
        foreach (var entry in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', 3, StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                return ForgeErrors.Invalid.With("runs: name:conclusion[:app],...");
            }

            runs.Add(new CheckRun(parts[0], parts.Length == 3 && parts[2].Length > 0 ? parts[2] : null, parts[1], string.Empty));
        }

        return Results.Success<IReadOnlyList<CheckRun>?>(runs);
    }

    private static Result<int> Count(CommandLine line, string option) =>
        line[option] is not { } text
            ? 0
            : int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)
            ? count
            : ForgeErrors.Invalid.With($"{option}: a whole number");
}
