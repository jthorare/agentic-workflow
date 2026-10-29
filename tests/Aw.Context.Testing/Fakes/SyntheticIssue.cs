namespace Aw.Context.Testing.Fakes;

/// <summary>A synthetic Linear issue (the backtest world seeds them as stored records); everything in it is made up.</summary>
/// <param name="Id">The Linear issue id (a UUID).</param>
/// <param name="Key">The tracker key, 'SPL-201'.</param>
/// <param name="Title">The title.</param>
/// <param name="CreatedAt">When the issue was created.</param>
/// <param name="UpdatedAt">When it was last edited.</param>
/// <param name="Chunks">The text, one string per chunk (at least one).</param>
public sealed record SyntheticIssue(string Id, string Key, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<string> Chunks);
