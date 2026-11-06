using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>One 'context.sources.library.vaults' entry as written.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LibraryVaultSection(string? Name = null, string? Path = null, IReadOnlyList<string?>? Deny = null, bool? Models = null);
