using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'sandbox.denied' payload (sandbox spec §8): the 1st, 10th and 100th denial of each (kind, target) per run. A denied command line is
/// never recorded whole: rule id and tool only. The counts the app shows come from the run's denial counter; these events are the ledger's cadence.
/// Key 'sandbox.denied:&lt;launch&gt;:&lt;kind&gt;:&lt;targetHash&gt;:&lt;count&gt;'.
/// </summary>
/// <param name="Launch">The launch.</param>
/// <param name="Kind">Network, policy or path.</param>
/// <param name="Target">A host, a path or a tool name, redacted and truncated.</param>
/// <param name="Rule">The rule that denied it.</param>
/// <param name="Overflow">True for the one overflow event after 200 distinct targets in a run (spec §3.6); its Target is empty, its Rule is "overflow" and its Count is the denials not counted by target. Left out otherwise.</param>
/// <param name="Count">The running count (1, 10 or 100).</param>
public sealed record SandboxDenied(LaunchId Launch, DenialKind Kind, string Target, string Rule, int Count,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Overflow = false);
