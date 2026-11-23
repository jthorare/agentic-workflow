namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The row a link in message text stands for (messages spec §9.3, #3021): what the local store holds for one row key. Its text comes from the row's source
/// (GitHub, Linear) and is untrusted.
/// </summary>
/// <param name="Key">The row key ('gh:pr:acme/harbor#3120').</param>
/// <param name="Title">The row's title.</param>
/// <param name="State">The row's state word ('open', 'merged', 'closed').</param>
public sealed record LinkRow(string Key, string Title, string State)
{
    /// <summary>Gets the CI state word ('passing', 'failing', 'running'), or null when the row has no CI.</summary>
    public string? Ci { get; init; }

    /// <summary>Gets who has the next move ('you', a person, 'reviewer', 'CI', 'agent'), or null.</summary>
    public string? WaitsOn { get; init; }

    /// <summary>Gets when the row started to need the owner, for its age, or null.</summary>
    public DateTimeOffset? Since { get; init; }
}
