namespace Aw.Sindri.Infrastructure.Evolve;

/// <summary>One 'proposal' row as read, before its body is parsed.</summary>
/// <param name="Id">The id column.</param>
/// <param name="State">The state column.</param>
/// <param name="Tier">The tier column.</param>
/// <param name="Attempts">The attempt count.</param>
/// <param name="HeldBy">The layer that held it last, or null.</param>
/// <param name="Body">The original proposal as JSON.</param>
/// <param name="Origin">Where it came from.</param>
/// <param name="CreatedAt">When it was first saved, ISO 8601.</param>
/// <param name="UpdatedAt">When it last changed, ISO 8601.</param>
/// <param name="Sessions">The source sessions as a JSON array, or null.</param>
internal sealed record ProposalRow(string Id, string State, string Tier, int Attempts, string? HeldBy, string Body, string Origin, string CreatedAt, string UpdatedAt, string? Sessions);
