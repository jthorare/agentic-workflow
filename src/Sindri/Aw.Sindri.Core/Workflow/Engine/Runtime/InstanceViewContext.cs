using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a view is built with besides the interpreter's state: what only the workflow knows. It holds no clock; the composers take the time they read at.</summary>
/// <param name="Version">The instance's version: the count of events mirrored into the ledger.</param>
/// <param name="Provenance">Who produced each fact and when; a fact with no entry shows no 'by' and 'at'.</param>
/// <param name="Children">The live children (a park or escalation mirroring a child names the first); the group rows of the view wait for engine P7.</param>
/// <param name="Contents">The stored text of the link inputs, read at the start; a link fact's ref shows its URL, host and title from it (API spec §3.8); none leaves the ref at its fact key.</param>
/// <param name="Injection">The open run's injection plan (metadata only); none when the Step has no inputs.</param>
/// <param name="StartedAt">When the instance started (the start's stamp); null for a history from before it was stamped.</param>
/// <param name="StartedBy">Who started it, in the actor form of the step runs; null when nobody is recorded.</param>
/// <param name="Subject">The subject the workflow named from the first input's stored content; null names it from the recipe alone (the fact key for a text, link or payload).</param>
/// <param name="StateVisits">The latest visit to each state the instance entered, by state id; null names none (a history replayed without visits), and the cells carry no times.</param>
/// <param name="Recommendation">The stored text of 'fact:recommendation', which a desk task's form turns into its recommended outcome; null names none.</param>
/// <param name="Again">The instance this one is a rerun of; null for any other.</param>
public sealed record InstanceViewContext(
    int Version = 0,
    IReadOnlyDictionary<FactKey, FactProvenance>? Provenance = null,
    IReadOnlyList<ChildView>? Children = null,
    SubjectRef? Subject = null,
    IReadOnlyDictionary<FactKey, string>? Contents = null,
    InjectionPlan? Injection = null,
    DateTimeOffset? StartedAt = null,
    string? StartedBy = null,
    IReadOnlyDictionary<string, StateVisit>? StateVisits = null,
    string? Recommendation = null,
    InstanceAddress? Again = null)
{
    /// <summary>How long each of the open run's Step inputs took to fill, in declared order (what the run's 'workflow.injection' row carries); null when none was kept.</summary>
    public IReadOnlyList<InputTiming>? Timings { get; init; }
}
