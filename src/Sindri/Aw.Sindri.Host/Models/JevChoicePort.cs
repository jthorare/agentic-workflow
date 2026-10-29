#pragma warning disable CS9113, MA0182 // the stub until the commit that implements it
using Aw.Context.Core.Models;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The context engine's choice port over Judge's batch classifier (model routing spec §6.7): Jev picks one option for each question of a pack. It sends only the strings Context already scrubbed (the judge's own redactor and cap run on top), reads the
/// pick setting from the APPROVED profile at every call (never the live file), and writes one 'context.model.called' event per call.
/// </summary>
/// <param name="classifier">Judge's batch classifier (built on first use).</param>
/// <param name="providers">The judge's 'providers' config (its 'inputCapChars'), or null for the defaults.</param>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="jobs">The job the call runs under.</param>
/// <param name="log">Where the call's event is written.</param>
/// <param name="ids">Names each call's event.</param>
/// <param name="clock">Dates and times the call.</param>
internal sealed class JevChoicePort(
    Func<IBatchClassifier> classifier,
    Func<CancellationToken, ValueTask<ProvidersConfig?>> providers,
    IApprovedModels models,
    IJobContextAccessor jobs,
    IModelCallLog log,
    IIdGenerator ids,
    TimeProvider clock) : IChoicePort
{
    public ChoiceModel Identity => throw new NotSupportedException();

    public ValueTask<int> InputCapChars(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<ChoiceAnswer>>> Pick(ChoiceKind kind, IReadOnlyList<ChoiceQuestion> questions, int capChars, CancellationToken cancellationToken) => throw new NotSupportedException();
}
