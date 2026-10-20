using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Features.Plan;
using Aw.Sindri.Host.Commands.Line;
using Aw.Sindri.Host.Commands.Scoping;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The line's local verbs (line spec Appendix D, LN-API4): 'design check', 'plan check' and 'line desk'. With --facts they run in this process over a directory of packet
/// files (class local: no instance, no server); with an address they are clients of the control plane, and a serve that is down is SND-API-001, never started.
/// Exit 0 pass, 1 findings, 2 usage, a tool error or serve not running.
/// </summary>
internal static class LineCommandTable
{
    private const string DesignUsage = "usage: sindri design check (<address> | --facts <dir>) [--scope <dir>] [--repo <dir>] [--style <pack.json>] [--lenses <pack.json>] [--stage draft|evidence] [--json]";
    private const string PlanUsage = "usage: sindri plan check (<address> | --facts <dir>) [--design <dir>] [--repo <dir>] [--stage draft|evidence] [--json]";
    private const string DesignShowUsage = "usage: sindri design show (<address> | --facts <dir>) [--scope <dir>] [--repo <dir>] [--style <pack.json>] [--lenses <pack.json>] [--json]";
    private const string PlanShowUsage = "usage: sindri plan show (<address> | --facts <dir>) [--design <dir>] [--repo <dir>] [--json]";
    private const string DeskUsage = "usage: sindri line desk <design.check|design.evidence|plan.check|plan.evidence> --facts <dir> --out <dir> [--packet-id <id> --repo-slug <owner/name> --base-sha <sha>] [--scope-hash <h>] [--design-hash <h>] [--parallel <n>] [--max-stack <n>] [--default-branch <b>] [--scope <dir>] [--design <dir>] [--repo <dir>] [--style <pack.json>] [--lenses <pack.json>] [--json]";

    internal static Task<int> Run(string[] args, ScopingCommandTable.Host host, CancellationToken ct) => args switch
    {
        ["design", "check", .. var rest] => Check("design", rest, host, ct),
        ["plan", "check", .. var rest] => Check("plan", rest, host, ct),
        ["design", "show", .. var rest] => Show("design", rest, host, ct),
        ["plan", "show", .. var rest] => Show("plan", rest, host, ct),
        ["line", "desk", .. var rest] => LineDeskVerb.Run(rest, host, DeskUsage, ct),
        _ => Fail(host.Output, SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a sindri command ({DesignUsage}; {PlanUsage}; {DeskUsage})")),
    };

    internal static async Task<int> Fail(Output output, Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return 2;   // line verbs: a tool error and serve not running are both exit 2
    }

    private static async Task<int> Check(string station, IReadOnlyList<string> args, ScopingCommandTable.Host host, CancellationToken ct)
    {
        var (services, output, desk, _) = host;
        if (Parse(args) is not (var parsed, var stage))
        {
            return await Fail(output, SindriErrors.InvalidInput.With(station == "design" ? DesignUsage : PlanUsage)).ConfigureAwait(false);
        }

        if (parsed.Value("--facts") is not { } directory)
        {
            return await Fail(output, await AddressRefusal(desk, parsed.Positional[0], station, ct).ConfigureAwait(false)).ConfigureAwait(false);
        }

        var result = await Evaluate(station, (directory, parsed, stage), services, ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await Fail(output, result.Error).ConfigureAwait(false);
        }

        var code = result.Value.Passed ? 0 : 1;
        return parsed.Json ? await output.Json(result.Value, code).ConfigureAwait(false) : await output.Text(LineText.Check(result.Value), code).ConfigureAwait(false);
    }

    private static async Task<int> Show(string station, IReadOnlyList<string> args, ScopingCommandTable.Host host, CancellationToken ct)
    {
        var (services, output, desk, _) = host;
        var parsed = ScopingArguments.Parse(args, "--facts", "--scope", "--design", "--repo", "--style", "--lenses");
        if (parsed is null || parsed.Positional.Count + (parsed.Value("--facts") is null ? 0 : 1) != 1)
        {
            return await Fail(output, SindriErrors.InvalidInput.With(station == "design" ? DesignShowUsage : PlanShowUsage)).ConfigureAwait(false);
        }

        var view = parsed.Value("--facts") is { } directory
            ? await LocalView(station, directory, parsed, services, ct).ConfigureAwait(false)
            : await InstanceView(desk, parsed.Positional[0], ct).ConfigureAwait(false);
        if (view.IsError)
        {
            return await Fail(output, view.Error).ConfigureAwait(false);
        }

        var shown = desk.Client.SessionReason is null ? view.Value : DesignStopFencing.Fence(view.Value);
        return parsed.Json ? await output.Json(shown, 0).ConfigureAwait(false) : await output.Text(LineText.Stop(shown), 0).ConfigureAwait(false);
    }

    // 'design show' reads a design packet, so its files are the design's; 'plan show' reads a plan packet, with the design beside it through --design.
    private static async Task<Result<DesignStopView>> LocalView(string station, string directory, ScopingArguments parsed, IServiceProvider services, CancellationToken ct)
    {
        var facts = await LineFactDirectory.Read(directory, parsed, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var files = station == "design" ? facts.Value.ToDictionary(static p => IsBeside(p.Key) ? p.Key : "design/" + p.Key, static p => p.Value, StringComparer.Ordinal) : facts.Value;
        return await services.GetRequiredService<IService<DesignShowRequest, DesignStopView>>().Handle(new DesignShowRequest(files), ct).ConfigureAwait(false);
    }

    private static bool IsBeside(string key) => key.StartsWith("scope/", StringComparison.Ordinal) || key.StartsWith("pack/", StringComparison.Ordinal) || key.StartsWith("repo/", StringComparison.Ordinal) || key == "options.json";

    // The view of a workflow.plan instance through the route, a client of the control plane; a serve that is down is SND-API-001.
    private static async Task<Result<DesignStopView>> InstanceView(RecipeDesk desk, string address, CancellationToken ct)
    {
        var listed = await desk.Client.Get<Core.Workflow.Views.InstanceList>($"{InstanceReads.Instances}?address={Uri.EscapeDataString(address)}", ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.Value.Instances is not [var instance])
        {
            return WorkflowErrors.InstanceNotFound.With($"'{address}' names no one instance (fix: sindri workflow list)");
        }

        if (!string.Equals(instance.Recipe.Id.Value, "workflow.plan", StringComparison.Ordinal))
        {
            return LineErrors.WrongRecipe.With($"{address} runs {instance.Recipe.Id.Value} (fix: pass the address of a workflow.plan instance)");
        }

        var read = await desk.Client.Get<DesignStopView>($"{InstanceReads.Instances}/{instance.Id.Value}/design-stop", ct).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value.Value;
    }

    // The flags and the stage; null when a flag is unknown, or the address and --facts are both given or both missing.
    private static (ScopingArguments Flags, LineStage Stage)? Parse(IReadOnlyList<string> args)
    {
        var parsed = ScopingArguments.Parse(args, "--facts", "--scope", "--design", "--repo", "--style", "--lenses", "--stage");
        var stage = parsed?.Value("--stage") switch { null or "draft" => LineStage.Draft, "evidence" => LineStage.Evidence, _ => (LineStage?)null };
        return parsed is not null && stage is not null && parsed.Positional.Count + (parsed.Value("--facts") is null ? 0 : 1) == 1 ? (parsed, stage.Value) : null;
    }

    private static async Task<Result<LineCheckResponse>> Evaluate(string station, (string Directory, ScopingArguments Flags, LineStage Stage) input, IServiceProvider services, CancellationToken ct)
    {
        var (directory, flags, stage) = input;
        var facts = await LineFactDirectory.Read(directory, flags, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts.Error;
        }

        if (station == "plan")
        {
            await AddValidation(facts.Value, directory, flags.Value("--repo"), services.GetRequiredService<IPackets>(), ct).ConfigureAwait(false);
            return await services.GetRequiredService<IService<PlanCheckRequest, LineCheckResponse>>().Handle(new PlanCheckRequest(facts.Value, stage), ct).ConfigureAwait(false);
        }

        return await services.GetRequiredService<IService<DesignCheckRequest, LineCheckResponse>>().Handle(new DesignCheckRequest(facts.Value, stage), ct).ConfigureAwait(false);
    }

    // PLN-PKT-001 is aw-packets' answer: a directory that is a packet (it has a packet.json) is validated against the checkout; a tool that cannot run leaves the rule not run.
    private static async Task AddValidation(Dictionary<string, string> facts, string directory, string? repo, IPackets packets, CancellationToken ct)
    {
        if (repo is null || facts.ContainsKey("validation.json") || !facts.ContainsKey("packet.json"))
        {
            return;   // without the checkout aw-packets omits the base-commit rules, which would fail the plan for what it was not asked
        }

        var validation = await packets.Validate(new PacketRef(PacketKind.Plan, directory, repo), ct).ConfigureAwait(false);
        if (validation.IsError)
        {
            return;
        }

        var failures = new JsonArray();
        foreach (var failure in validation.Value.Failures)
        {
            LineJson.Append(failures, new JsonObject { ["rule"] = failure.Rule, ["subject"] = failure.Subject, ["message"] = failure.Message, ["fix"] = failure.Fix });
        }

        facts["validation.json"] = LineJson.Write(new JsonObject { ["passed"] = validation.Value.Passed, ["failures"] = failures });
    }

    // An address names an instance whose packet files are in its run area, which the API does not serve yet: the instance is still looked up, so a serve that is down and the wrong recipe answer as they should.
    private static async Task<Error> AddressRefusal(RecipeDesk desk, string address, string station, CancellationToken ct)
    {
        var recipe = $"workflow.{station}";
        var listed = await desk.Client.Get<Core.Workflow.Views.InstanceList>($"{InstanceReads.Instances}?address={Uri.EscapeDataString(address)}", ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.Value.Instances is not [var instance])
        {
            return WorkflowErrors.InstanceNotFound.With($"'{address}' names no one instance (fix: sindri workflow list)");
        }

        return string.Equals(instance.Recipe.Id.Value, recipe, StringComparison.Ordinal)
            ? SindriErrors.InvalidInput.With($"{address} is a {recipe} instance, whose packet files are in its run area and not on the API yet (fix: sindri {station} check --facts \"$AW_SINDRI_RUN_DIR\")")
            : LineErrors.WrongRecipe.With($"{address} runs {instance.Recipe.Id.Value} (fix: pass the address of a {recipe} instance)");
    }
}
