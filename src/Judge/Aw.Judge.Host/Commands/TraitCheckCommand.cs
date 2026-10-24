using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckTraits;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge trait-check --edit --file &lt;p&gt;… | --stop [--base ref] | --scan [--path p…] [--trait n…]' (trait-check.sh calls
/// the first two), with --budget-ms, --min-p and --concurrency. Exit 0 nothing to report or skipped, 2 findings
/// (edit and stop: the message on stderr; scan: JSON lines on stdout), 3 Jev unavailable, 64 usage.
/// </summary>
internal static class TraitCheckCommand
{
    internal const string Name = "trait-check";
    internal const string Synopsis = "trait-check --edit --file <path>… | --stop [--base <ref>] | --scan [--path <path>…] [--trait <name>…]";

    private const int FindingsExit = 2;
    private const int JevUnavailableExit = 3;
    private const int MaxListed = 10;

    internal static async Task<int> Run(IReadOnlyList<string> args, IReadOnlyCollection<ProviderName>? allowed, IServiceProvider services)
    {
        if (TraitCheckArguments.Parse(args, allowed) is not { } request)
        {
            return CommandOutput.Usage(Synopsis);
        }

        var service = services.GetRequiredService<IService<CheckTraitsRequest, CheckTraitsResponse>>();
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Emit(1, "", result.Error.Message) : Report(request.Mode, result.Value);
    }

    private static int Report(TraitCheckMode mode, CheckTraitsResponse response)
    {
        if (response.JevUnavailable)
        {
            return CommandOutput.Emit(JevUnavailableExit, "", $"trait-check: jev unavailable ({response.Reason})");
        }

        if (response.Findings.Count == 0)
        {
            return CommandOutput.Emit(0, "");
        }

        return mode == TraitCheckMode.Scan
            ? CommandOutput.Emit(FindingsExit, ScanLines(response.Findings), $"trait-check: {N(response.Findings.Count)} finding{Plural(response.Findings.Count)} from {N(response.Requests)} Jev requests")
            : CommandOutput.Emit(FindingsExit, "", Message(mode, response.Findings));
    }

    // One JSON line per scan finding, highest probability first, for tooling to triage.
    private static string ScanLines(IReadOnlyList<TraitFinding> findings) => string.Join(
        '\n',
        findings.Select(finding => new JsonObject
        {
            ["trait"] = finding.Trait,
            ["severity"] = finding.Severity,
            ["path"] = finding.File,
            ["lines"] = new JsonArray(finding.StartLine, finding.EndLine),
            ["p"] = Math.Round(finding.Probability, 3, MidpointRounding.ToEven),
        }.ToJsonString()));

    // What the hook hands back to the agent.
    private static string Message(TraitCheckMode mode, IReadOnlyList<TraitFinding> findings)
    {
        var count = $"{N(findings.Count)} project trait{Plural(findings.Count)}";
        var head = mode == TraitCheckMode.Edit
            ? $"trait-check: your last edit likely breaks {count}. Fix each one that applies, or say in one line why it doesn't, then continue."
            : $"trait-check: before you finish, this branch's changes likely break {count} marked severity: block. Fix each one that applies and re-run the relevant checks. If a finding is wrong, say why in one line.";
        var items = findings.Take(MaxListed).Select((finding, index) =>
        {
            List<string> lines =
            [
                $"{N(index + 1)}. {finding.Trait} — {finding.File} lines {N(finding.StartLine)}-{N(finding.EndLine)} (p={finding.Probability.ToString("F2", CultureInfo.InvariantCulture)})",
                $"   Rule: {finding.Message}",
            ];
            if (finding.Guidance is not null)
            {
                lines.Add($"   Fix: {finding.Guidance}");
            }

            lines.Add($"   Details: {finding.Details}");
            return string.Join('\n', lines);
        });
        var more = findings.Count > MaxListed ? [$"…and {N(findings.Count - MaxListed)} more."] : Array.Empty<string>();
        return string.Join('\n', new[] { head, "" }.Concat(items).Concat(more));
    }

    private static string N(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Plural(int number) => number == 1 ? "" : "s";
}
