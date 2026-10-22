using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>Where a desk row sits among the desk states of its recipe (the UI's "Approval n of of").</summary>
/// <param name="N">The 1-based position of the desk state the instance is parked in, in recipe order.</param>
/// <param name="Of">How many desk (human) states the recipe has.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NeedsYouStop(int N, int Of);
