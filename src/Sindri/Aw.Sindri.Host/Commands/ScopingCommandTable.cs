using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Host.Commands.Scoping;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'scoping' verbs (scoping spec §8.2): 'check', 'show' and 'desk'. With --facts they run in this process over a directory of fact
/// files (class local: no instance, no server); with an address they are clients of the control plane, and a serve that is down is
/// SND-API-001, never started. Exit 0 pass, 1 findings, 2 usage, a tool error or serve not running.
/// </summary>
internal static partial class ScopingCommandTable
{
    private const string CheckUsage = "usage: sindri scoping check (<address> | --facts <dir>) [--draft <dir>] [--stage draft|evidence] [--json]";
    private const string ShowUsage = "usage: sindri scoping show (<address> | --facts <dir>) [--json]";
    private const string DeskUsage = "usage: sindri scoping desk <block id> --facts <dir> --out <dir> [--repo-root <dir> [--notes-dir <dir>]] [--query <file>] [--lenses <file>] [--code <file>] [--history <file>] [--propose <file>] [--json]";

    /// <summary>What every scoping verb needs: the container, the output, the API client and the environment.</summary>
    internal sealed record Host(IServiceProvider Services, Output Output, RecipeDesk Desk, Func<string, string?> Environment);

    internal static Task<int> Run(string[] args, Host host, CancellationToken ct) => args switch
    {
        ["check", .. var rest] => Check(rest, host, ct),
        ["show", .. var rest] => Show(rest, host, ct),
        ["desk", .. var rest] => Desk(rest, host, ct),
        _ => Fail(host.Output, SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["scoping", .. args])}' is not a sindri command ({CheckUsage}; {ShowUsage}; {DeskUsage})")),
    };

    private static async Task<int> Fail(Output output, Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return 2;   // scoping verbs: a tool error and serve not running are both exit 2
    }

    private static async Task<int> Check(IReadOnlyList<string> args, Host host, CancellationToken ct)
    {
        var (services, output, desk, environment) = host;
        var parsed = ScopingArguments.Parse(args, "--facts", "--draft", "--stage");
        var stage = parsed?.Value("--stage") switch { null or "draft" => ScopeStage.Draft, "evidence" => ScopeStage.Evidence, _ => (ScopeStage?)null };
        if (parsed is null || stage is null || !OneSource(parsed))
        {
            return await Fail(output, SindriErrors.InvalidInput.With(CheckUsage)).ConfigureAwait(false);
        }

        var facts = await Facts(parsed, desk, environment, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return await Fail(output, facts.Error).ConfigureAwait(false);
        }

        var result = await services.GetRequiredService<IService<ScopingCheckRequest, ScopingCheckResponse>>()
            .Handle(new ScopingCheckRequest(facts.Value, stage.Value), ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await Fail(output, result.Error).ConfigureAwait(false);
        }

        var code = result.Value.Passed ? 0 : 1;
        return parsed.Json ? await output.Json(result.Value, code).ConfigureAwait(false) : await output.Text(ScopingText.Check(result.Value), code).ConfigureAwait(false);
    }

    private static async Task<int> Show(IReadOnlyList<string> args, Host host, CancellationToken ct)
    {
        var (services, output, desk, _) = host;
        var parsed = ScopingArguments.Parse(args, "--facts");
        if (parsed is null || !OneSource(parsed))
        {
            return await Fail(output, SindriErrors.InvalidInput.With(ShowUsage)).ConfigureAwait(false);
        }

        // an address is a client of API2, whose route fences a session's text itself; --facts runs here and fences for a session
        var view = parsed.Value("--facts") is not null
            ? await LocalView(services, desk, parsed, ct).ConfigureAwait(false)
            : await InstanceView(desk, parsed.Positional[0], ct).ConfigureAwait(false);
        if (view.IsError)
        {
            return await Fail(output, view.Error).ConfigureAwait(false);
        }

        return parsed.Json ? await output.Json(view.Value, 0).ConfigureAwait(false) : await output.Text(ScopingText.View(view.Value), 0).ConfigureAwait(false);
    }

    private static async Task<Result<ScopingView>> LocalView(IServiceProvider services, RecipeDesk desk, ScopingArguments parsed, CancellationToken ct)
    {
        var facts = await Facts(parsed, desk, static _ => null, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var result = await services.GetRequiredService<IService<ScopingShowRequest, ScopingView>>().Handle(new ScopingShowRequest(facts.Value), ct).ConfigureAwait(false);
        return result.IsError || desk.Client.SessionReason is null ? result : ScopingFencing.Fence(result.Value);
    }

    /// <summary>Exactly one of an address and --facts.</summary>
    private static bool OneSource(ScopingArguments parsed) => parsed.Positional.Count + (parsed.Value("--facts") is null ? 0 : 1) == 1 && parsed.Positional.Count <= 1;

    /// <summary>The facts to check or show: a directory (with the draft overlay), or an instance's through the control plane.</summary>
    private static async Task<Result<Dictionary<string, string>>> Facts(ScopingArguments parsed, RecipeDesk desk, Func<string, string?> environment, CancellationToken ct)
    {
        var draft = parsed.Value("--draft");
        if (parsed.Value("--facts") is { } directory)
        {
            if (draft is not null && ScopingFactDirectory.InGitWorktree(draft))
            {
                return ScopingFactDirectory.WorktreeRefusal("--draft", draft);
            }

            return await Overlay(await ScopingFactDirectory.Read(directory, ct).ConfigureAwait(false), draft, ct).ConfigureAwait(false);
        }

        var address = parsed.Positional[0];
        if (draft is not null && !InRunArea(draft, environment))
        {
            return ScopingErrors.DraftOutsideRunArea.With($"--draft {draft} is outside the instance's run area (fix: write the draft files under {RunAreaRoot(environment)})");
        }

        return await Overlay(await InstanceFacts(desk, address, ct).ConfigureAwait(false), draft, ct).ConfigureAwait(false);
    }

    private static async Task<Result<Dictionary<string, string>>> Overlay(Result<Dictionary<string, string>> facts, string? draft, CancellationToken ct)
    {
        if (facts.IsError || draft is null)
        {
            return facts;
        }

        var files = await ScopingFactDirectory.Read(draft, ct).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error;
        }

        foreach (var (key, text) in files.Value)
        {
            facts.Value[key] = text;
        }

        return facts;
    }

    private static string RunAreaRoot(Func<string, string?> environment)
    {
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } given
            ? given
            : Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } state ? state : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow"), "sindri-app");
        return Path.Combine(Path.GetFullPath(home), "workflow", "runs");
    }

    private static bool InRunArea(string path, Func<string, string?> environment)
    {
        var root = RunAreaRoot(environment) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal);
    }

    private static async Task<int> Desk(IReadOnlyList<string> args, Host host, CancellationToken ct)
    {
        var (services, output, _, _) = host;
        var parsed = ScopingArguments.Parse(args, "--facts", "--out", "--repo-root", "--notes-dir", "--query", "--lenses", "--code", "--history", "--propose");
        if (!DeskArguments(parsed, out var block, out var directory, out var outDirectory))
        {
            return await Fail(output, SindriErrors.InvalidInput.With(DeskUsage)).ConfigureAwait(false);
        }

        if (ScopingFactDirectory.InGitWorktree(outDirectory))
        {
            return await Fail(output, ScopingFactDirectory.WorktreeRefusal("--out", outDirectory)).ConfigureAwait(false);
        }

        var facts = await ScopingDeskInputs.Load(parsed, (block, directory, outDirectory), (services.GetRequiredService<IBriefFiles>(), services.GetRequiredService<TimeProvider>().GetUtcNow()), ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return await Fail(output, facts.Error).ConfigureAwait(false);
        }

        var result = await services.GetRequiredService<IService<ScopingDeskRequest, ScopingDeskResponse>>()
            .Handle(new ScopingDeskRequest(block, facts.Value), ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await Fail(output, result.Error).ConfigureAwait(false);
        }

        var report = await WriteReport(result.Value, outDirectory, (services.GetRequiredService<IPackets>(), services.GetRequiredService<JsonSerializerOptions>()), ct).ConfigureAwait(false);
        if (report.IsError)
        {
            return await Fail(output, report.Error).ConfigureAwait(false);
        }

        var code = result.Value.Findings.Count > 0 || string.Equals(result.Value.Outcome, "incomplete", StringComparison.Ordinal) ? 1 : 0;
        return parsed.Json
            ? await output.Json(report.Value, code).ConfigureAwait(false)
            : await output.Text(DeskLine(report.Value), code).ConfigureAwait(false);
    }

    private static bool DeskArguments([NotNullWhen(true)] ScopingArguments? parsed, out string block, out string directory, out string outDirectory)
    {
        block = directory = outDirectory = string.Empty;
        if (parsed is not { Positional: [var only] } || parsed.Value("--facts") is not { } facts || parsed.Value("--out") is not { } output)
        {
            return false;
        }

        (block, directory, outDirectory) = (only, facts, output);
        return true;
    }

    /// <summary>Writes the Step's fact files, its run-area files and report.json under the out directory; the evidence step's packet gets its hash from aw-packets.</summary>
    private static async Task<Result<ScopingDeskReport>> WriteReport(ScopingDeskResponse response, string outDirectory, (IPackets Packets, JsonSerializerOptions Json) context, CancellationToken ct)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, text) in response.Outputs)
        {
            await ScopingFactDirectory.Write(Path.Combine(outDirectory, key + ".json"), text + "\n", append: false, ct).ConfigureAwait(false);
            outputs["fact:" + key] = key + ".json";
        }

        foreach (var file in response.Files)
        {
            await ScopingFactDirectory.Write(Path.Combine(outDirectory, file.Path), file.Content, file.Append, ct).ConfigureAwait(false);
        }

        if (response.Files.Any(static f => f.Path.StartsWith(ScopePacketDirectory + "/", StringComparison.Ordinal)))
        {
            var hash = await PacketsCli.Hash(context.Packets, Path.Combine(outDirectory, ScopePacketDirectory), ct).ConfigureAwait(false);
            if (hash.IsError)
            {
                return hash.Error;
            }

            await ScopingFactDirectory.Write(Path.Combine(outDirectory, "scopePacket.json"), $"{{\"hash\":\"{hash.Value}\"}}\n", append: false, ct).ConfigureAwait(false);
            outputs["fact:scopePacket"] = "scopePacket.json";
        }

        var report = new ScopingDeskReport(response.Block, response.Outcome, outputs, [.. response.Files.Select(static f => f.Path)], response.Findings, response.Warnings);
        await ScopingFactDirectory.Write(Path.Combine(outDirectory, "report.json"), Encoding.UTF8.GetString(AwJson.Serialize(report, context.Json)) + "\n", append: false, ct).ConfigureAwait(false);
        return report;
    }

    private const string ScopePacketDirectory = "scope-packet";

    private static string DeskLine(ScopingDeskReport report) => $"{report.Block}: {report.Outcome}\n";
}
