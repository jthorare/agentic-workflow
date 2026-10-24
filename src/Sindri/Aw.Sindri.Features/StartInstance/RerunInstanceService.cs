using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>
/// 'RerunInstance': reads the instance, refuses unless <see cref="Reruns"/> offers the rerun (a live, finished or child instance is an input error), reads back the input facts it was
/// started with from their stored content, and starts the same recipe version on the same item as the next attempt through the start service, with <c language="text">again</c> set to the old address.
/// The recipe is pinned to the old instance's hash, so the rerun runs what the first run ran.
/// </summary>
/// <param name="directory">Resolves the instance.</param>
/// <param name="reader">Reads the instance.</param>
/// <param name="contents">Reads the stored input facts back.</param>
/// <param name="start">The start command through its pipeline, which checks the approval, the claim and the attempt.</param>
internal sealed class RerunInstanceService(IInstanceDirectory directory, IInstanceReader reader, IFactContents contents, IService<StartInstanceRequest, StartInstanceResponse> start)
    : IService<RerunInstanceRequest, StartInstanceResponse>
{
    public async ValueTask<Result<StartInstanceResponse>> Handle(RerunInstanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return InstanceScope.Unknown(address.Error, request.Instance);
        }

        var read = await reader.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return InstanceScope.Unknown(read.Error, request.Instance);
        }

        var view = read.Value;
        if (Reruns.Of(view).Count == 0 || view.ItemId is not { } item)
        {
            return SindriErrors.InvalidInput.With($"{view.Address.Value} can't be run again (fix: only a root instance that ended failed, unresolved or cancelled can; start a new one with sindri workflow start)");
        }

        var facts = await Inputs(view, cancellationToken).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var recipe = $"{view.Recipe.Id.Value}@{view.Recipe.Hash.Value}";
        return await start.Handle(new StartInstanceRequest(recipe, item.Value, NextAttempt: true, facts.Value) { Again = view.Address }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The input facts the instance was given, read back from their stored content; an input with a generate template is left to be generated again.</summary>
    private async ValueTask<Result<IReadOnlyDictionary<FactKey, StartFact>>> Inputs(InstanceView view, CancellationToken cancellationToken)
    {
        var facts = new Dictionary<FactKey, StartFact>();
        foreach (var input in view.Inputs.Where(static i => i.Present && i.Required))
        {
            if (view.FactShas?.GetValueOrDefault(input.Key) is not { } sha || !FactKey.TryFrom(input.Key, out var key))
            {
                return SindriErrors.InvalidInput.With($"{view.Address.Value} holds no stored content for input {input.Key} (fix: start a new instance with sindri workflow start)");
            }

            var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            facts[key] = new StartFact(input.Key[(input.Key.IndexOf(':', StringComparison.Ordinal) + 1)..], bytes.Value);
        }

        return facts;
    }
}
