using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Batching;

/// <summary>
/// The call went through. A question the model answered in a shape the adapter can't use (wrong type, a choice outside the
/// criteria) is absent from <paramref name="Answers"/>: the caller treats it as undecided.
/// </summary>
/// <param name="Answers">The usable answers by question key.</param>
/// <param name="Usage">The cost, when the API reported it.</param>
public sealed record BatchDecided(IReadOnlyDictionary<string, BatchAnswer> Answers, BatchUsage? Usage = null) : ProviderResult("model");
