using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>An event a wait state can wait for (spec §2), registered with AddEvent.</summary>
/// <param name="Kind">The event's kind, for example 'claim.created'.</param>
/// <param name="Producer">The actor that may write it: a service or adapter, for example 'system:dispatcher'.</param>
/// <param name="Route">Whether it reaches only the frame of the command it answers, or the item's instances.</param>
/// <param name="Outcomes">The outcomes it carries in its 'outcome' field.</param>
/// <param name="Facts">The facts it records.</param>
public sealed record EventDescriptor(
    EventKind Kind,
    string Producer,
    EventRoute Route,
    IReadOnlyList<OutcomeName> Outcomes,
    IReadOnlyList<FactKey> Facts);
