namespace Aw.Sindri.Core.Events;

/// <summary>The values of <see cref="ModelPicked.By"/>.</summary>
public static class ModelPickedBy
{
    /// <summary>Jev chose among several candidates.</summary>
    public const string Jev = "jev";

    /// <summary>Only one candidate was left.</summary>
    public const string Constraint = "constraint";

    /// <summary>The rules fallback chose, because Jev could not.</summary>
    public const string Rules = "rules";

    /// <summary>No candidates: the agent's own default model.</summary>
    public const string Default = "default";
}
