
namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>The payload schema and sample of each pollable event kind that declares one (the GitHub kinds first): the one place validate, the block catalog and the poll read them from.</summary>
public static class EventKindSchemas
{
    /// <summary>Gets the schemas, one per kind, in catalog order.</summary>
    public static IReadOnlyList<EventKindSchema> All => throw new NotSupportedException();

    /// <summary>Looks up one kind's schema.</summary>
    /// <param name="kind">The catalog id ('github.push').</param>
    /// <returns>The schema, or null when the kind declares none.</returns>
    public static EventKindSchema? Find(string kind) => throw new NotSupportedException();
}
