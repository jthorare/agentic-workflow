using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Linear's ids as typed ids.</summary>
internal static class LinearExternalIds
{
    /// <summary>The id, or null when Linear sent none or one that isn't a usable id.</summary>
    /// <param name="raw">The id as Linear spells it.</param>
    /// <returns>The typed id, or null.</returns>
    public static ExternalId? Of(string? raw) => raw is not null && ExternalId.TryFrom(raw, out var id) ? id : null;

    /// <summary>The id of a referenced user, project, cycle or label, or null.</summary>
    /// <param name="named">The reference, or null.</param>
    /// <returns>The typed id, or null.</returns>
    public static ExternalId? Of(LinearNamed? named) => Of(named?.Id);

    /// <summary>The id of a referenced issue, or null.</summary>
    /// <param name="issue">The reference, or null.</param>
    /// <returns>The typed id, or null.</returns>
    public static ExternalId? Of(LinearIssueRef? issue) => Of(issue?.Id);
}
