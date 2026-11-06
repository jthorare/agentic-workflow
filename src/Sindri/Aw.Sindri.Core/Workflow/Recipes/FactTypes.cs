namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>The fact types of spec §3.3: they pick a viewer in the app and nothing else (RCP-TYPE-001).</summary>
public static class FactTypes
{
    /// <summary>The type of a fact that declares none.</summary>
    public const string Default = "text";

    /// <summary>The ten fact types.</summary>
    public static IReadOnlyList<string> All { get; } = ["text", "file", "item", "link", "scope-map", "packet", "evidence", "decision", "payload", "findings"];

    /// <summary>Whether <paramref name="type"/> is one of <see cref="All"/>.</summary>
    public static bool IsKnown(string type) => All.Contains(type, StringComparer.Ordinal);
}
