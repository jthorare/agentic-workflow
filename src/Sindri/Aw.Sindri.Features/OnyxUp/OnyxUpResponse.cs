using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>
/// What `sindri onyx up` ended with. JSON: `{ port, url, version, warnings: [string], steps: [{ step, outcome }] }` with `outcome` one of `ran`, `skipped`, `needs-human`,
/// one entry per step in the order they run (fetch, config, pull, start).
/// </summary>
/// <param name="Port">The loopback port Onyx's web server is on.</param>
/// <param name="Url">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="Version">The release the running Onyx reports.</param>
/// <param name="Warnings">Preflight checks that warned and didn't block.</param>
/// <param name="Steps">What each step did, in order.</param>
public sealed record OnyxUpResponse(int Port, string Url, string Version, IReadOnlyList<string> Warnings, IReadOnlyList<OnyxStepResult> Steps)
{
    /// <summary>Gets the names of the steps that did work this run.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Ran => Named(OnyxStepOutcome.Ran);

    /// <summary>Gets the names of the steps whose inputs and results were already as needed.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Skipped => Named(OnyxStepOutcome.Skipped);

    /// <summary>Gets the names of the steps that wait for the human. Reserved for P3's account setup: no step produces it yet, so it is empty today.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> NeedsHuman => Named(OnyxStepOutcome.NeedsHuman);

    private List<string> Named(OnyxStepOutcome outcome) => [.. Steps.Where(s => s.Outcome == outcome).Select(static s => s.Step)];
}
