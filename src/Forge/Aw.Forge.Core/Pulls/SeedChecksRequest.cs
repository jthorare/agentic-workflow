using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>Sets a pull request's checks in a fake or file forge.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="State">The overall state.</param>
/// <param name="Failing">The names of the failing checks.</param>
/// <param name="Logs">Check name to log text.</param>
public sealed record SeedChecksRequest(ForgeRef Pull, ChecksState State, IReadOnlyList<string> Failing, IReadOnlyDictionary<string, string> Logs);
