using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Features.SkipSetupStep;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri setup [--json]': where "set up Sindri once" stands, a client of GET /sindri/setup. It reads the route and never starts the control plane (API spec §7), and it only reports:
/// every control that moves a step forward is the app's. Exit 0 when every step is done or skipped; 3 when every step that is not done waits on the person at the keyboard (state needs-you and no command
/// an agent can run: first-run spec decision 9; an agent stops and tells the owner); 1 when a step is still to do, running or failed, or when Sindri isn't running (SND-API-001); 2 on usage or any other error.
/// 'sindri setup skip accounts|first-workflow [--undo] [--json]' skips a step that may be skipped, or undoes the skip, through POST /sindri/setup/skip (any caller).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class SetupVerbs(ControlPlaneClient client, Output output)
{
    internal const string Usage = "setup [--json] | setup skip accounts|first-workflow [--undo] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is ["skip", .. var skip])
        {
            return await Skip(skip, ct).ConfigureAwait(false);
        }

        if (args.Any(static a => a != "--json"))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'setup' takes no argument but --json ({Usage})")).ConfigureAwait(false);
        }

        var answer = await client.Get<GetSetupResponse>("/sindri/setup", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = ExitCode(answer.Value.Value.Steps);
        return args.Length > 0
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(SetupText.Render(answer.Value.Value), code).ConfigureAwait(false);
    }

    // 0 every step done or skipped; 3 every other step waits on the person (needs-you with no command an agent can run); 1 otherwise.
    private static int ExitCode(IReadOnlyList<SetupStep> steps)
    {
        var open = steps.Where(static s => s.State is not (SetupState.Done or SetupState.Skipped)).ToList();
        return open.Count == 0 ? 0 : open.TrueForAll(static s => s.State == SetupState.NeedsYou && s.Cli is null) ? 3 : 1;
    }

    // 'skip accounts|first-workflow [--undo] [--json]': the word names the step; --undo clears the skip.
    private async Task<int> Skip(string[] args, CancellationToken ct)
    {
        var words = args.Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var flags = args.Where(static a => a.StartsWith("--", StringComparison.Ordinal)).ToList();
        SetupStepId? step = words switch
        {
            ["accounts"] => SetupStepId.Sources,
            ["first-workflow"] => SetupStepId.FirstWorkflow,
            _ => null,
        };
        if (step is not { } id || flags.Exists(static f => f is not ("--undo" or "--json")))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'setup skip {string.Join(' ', args)}' isn't this command ({Usage})")).ConfigureAwait(false);
        }

        var skipped = !flags.Contains("--undo");
        var answer = await client.Post<SkipSetupStepRequest, SkipSetupStepResponse>("/sindri/setup/skip", new SkipSetupStepRequest(id, skipped), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var name = words[0];
        return flags.Contains("--json")
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(skipped ? $"skipped {name}: it counts as done (undo: sindri setup skip {name} --undo)\n" : $"{name} is no longer skipped\n", 0).ConfigureAwait(false);
    }
}
