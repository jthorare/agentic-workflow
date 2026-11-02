namespace Aw.Judge.Core.Questions;

/// <summary>One model Jev may pick for a step.</summary>
/// <param name="Key">The answer's name: m1, m2, ... in cheapest-first order (a model id never has to fit the name pattern).</param>
/// <param name="Model">The model id Sindri launches.</param>
/// <param name="Tier">Its band: cheap, standard or strong.</param>
/// <param name="Price">Its price, or null when unknown.</param>
public sealed record ModelPickCandidate(string Key, string Model, string Tier, double? Price = null);
