using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'service.ran' payload (API spec C8): one per scheduled run that failed or did work, and the first ok after a failure (an idle run after an ok one
/// writes nothing); and one per manual run (RunService, API P6), which carries its job.
/// </summary>
/// <param name="CommandId">The run's command id, minted by its caller context.</param>
/// <param name="Service">The service.</param>
/// <param name="Slot">The scheduled slot it filled; for a manual run, when the run started.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
/// <param name="Job">The job of a manual run, or null for a scheduled one (then the member is left out, so earlier rows keep their shape).</param>
/// <param name="Detail">The failure's message, scrubbed and cut short, or null for an ok run (then the member is left out).</param>
public sealed record ServiceRan(CommandId CommandId, ServiceName Service, DateTimeOffset Slot, ServiceOutcome Outcome, string? Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JobId? Job = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null);
