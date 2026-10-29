using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// The instances parked on a sandbox code (sandbox spec §6): a 'launch' park (-015, -016), or a no-runtime park whose question names an SND-LAUNCH code (-014, -017).
/// An instance that can't be read now is left out: it is not offered for a resume it might not take.
/// </summary>
/// <param name="instances">The open instances.</param>
/// <param name="reader">Reads one instance.</param>
/// <param name="views">The items view, for the title of the item a park runs for; null leaves the title off.</param>
internal sealed class SandboxParkedInstances(IWorkflowInstances instances, IInstanceReader reader, IViewReader? views = null) : ISandboxParks
{
    private const string LaunchCode = "SND-LAUNCH-";

    public async ValueTask<IReadOnlyList<ParkedOnSandbox>> Parked(CancellationToken cancellationToken)
    {
        var listed = await instances.List(new InstanceFilter(State: EngineStates.Parked), ended: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return [];
        }

        var found = new List<ParkedOnSandbox>();
        foreach (var row in listed.Value)
        {
            if (!InstanceAddress.TryParse(row.Address, provider: null, out var address))
            {
                continue;
            }

            var view = await reader.Get(address, cancellationToken).ConfigureAwait(false);
            if (view.IsSuccess && view.Value.Question is { Kind: SuspensionKind.Park } question && OnSandbox(question))
            {
                var item = address.Value.Split('/', 2)[0];
                found.Add(new ParkedOnSandbox(view.Value.Id, view.Value.Since, item, await TitleOf(item, cancellationToken).ConfigureAwait(false)));
            }
        }

        return found;
    }

    /// <summary>The item's title, or null when the items view is unavailable or has no such item: a park is listed either way.</summary>
    private async ValueTask<string?> TitleOf(string item, CancellationToken cancellationToken)
    {
        if (views is null)
        {
            return null;
        }

        var row = await views.Get<ItemRow>(ViewNames.Items, item, cancellationToken).ConfigureAwait(false);
        return row.IsSuccess ? row.Value?.Title : null;
    }

    /// <summary>
    /// A no-runtime park carries no typed sandbox code on the view, so its question text is the link: TransitionDecision.ParkFor (Core/Workflow/Engine/Rules/TransitionDecision.Launching.cs)
    /// writes the refusal's code into it ("... can't start: SND-LAUNCH-014 (reason): ..."). SandboxParkedInstancesTests builds parks through that code to pin it.
    /// </summary>
    private static bool OnSandbox(OpenQuestion question) =>
        question.Reason == ParkReason.Launch || (question.Reason == ParkReason.NoRuntime && question.Question.Contains(LaunchCode, StringComparison.Ordinal));
}
