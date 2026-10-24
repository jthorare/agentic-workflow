namespace Aw.Evidence.Core.Scripts;

/// <summary>An element by ARIA role and exact accessible name.</summary>
/// <param name="Role">The lower-case role ('button', 'option').</param>
/// <param name="Name">The exact accessible name.</param>
/// <param name="Nth">Which of several matches (1-based), or null for exactly one.</param>
public sealed record RoleTarget(string Role, string Name, int? Nth) : StepTarget
{
    /// <inheritdoc/>
    public override string Label => Name;
}
