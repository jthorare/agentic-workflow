namespace Aw.Context.Testing.Linear;

/// <summary>The optional columns of a seeded issue.</summary>
public sealed record FakeLinearIssueSeed
{
    /// <summary>The description.</summary>
    public string? Description { get; init; }

    /// <summary>The workflow state's name ('Todo' by default).</summary>
    public string State { get; init; } = "Todo";

    /// <summary>The assignee's name, which must be a seeded member.</summary>
    public string? Assignee { get; init; }

    /// <summary>0 (none) to 4 (low).</summary>
    public int Priority { get; init; }

    /// <summary>The estimate.</summary>
    public int? Estimate { get; init; }

    /// <summary>The cycle's name, which must be a seeded cycle.</summary>
    public string? Cycle { get; init; }

    /// <summary>The project's name, which must be a seeded project.</summary>
    public string? Project { get; init; }

    /// <summary>Label names, which must be seeded labels.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];
}
