namespace Aw.Sindri.Core.LensPacks;

/// <summary>What a pack directory holds.</summary>
/// <param name="Files">Each file's path inside the pack and its text.</param>
/// <param name="Outside">The paths inside the pack of entries that resolve to somewhere outside it; their content is not in <paramref name="Files"/>.</param>
public sealed record PackDirectory(IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Outside);
