using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.library' as written (review page spec §5.1): the state folder's root and the vaults.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LibrarySection(string? StateDir = null, bool? Models = null, IReadOnlyList<LibraryVaultSection?>? Vaults = null);
