using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'workflow:' block (engine spec §7.2 S1-D11; engine P4 adds 'deskTimeout' and 'eventHold', P6 'effects'
/// and 'dispatchDelay', P7 theirs). Each time is a duration ('1m', '4h', '2d'); a key left out takes its default
/// (<see cref="WorkflowTimes.Defaults"/>, <see cref="EffectSettings.DefaultDispatchDelay"/>).
/// </summary>
/// <param name="DeskTimeout">The floor of every desk Step's timeout (W20; default 2d). The dev profile sets 1m so a desk wait parks fast.</param>
/// <param name="EventHold">How long a buffered event signal waits for its wait before it is dropped with 'workflow.expired' (§5.4; default 24h).</param>
/// <param name="Effects">The effect allowlist (§5.7, RCP-EFFECT-001): the 'effect.*' ids a recipe may dispatch. Absent: RCP-EFFECT-001 is skipped and every recipe effect is refused at dispatch (SND-WF-011). Engine P6 unit (d) checks it.</param>
/// <param name="DispatchDelay">The undo window before an effect's activity runs (§5.7; default 10s; the dev profile sets 60s). Engine P6 unit (d) checks it.</param>
/// <param name="Forge">The forge port's settings (PR babysitter spec W27): adapter, hosts and credential source.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkflowSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeskTimeout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EventHold = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Effects = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DispatchDelay = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ForgeSettings? Forge = null);
