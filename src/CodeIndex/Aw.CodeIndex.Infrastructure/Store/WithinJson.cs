using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>The one wire shape the reader binds: a JSON array of paths for <c xml:lang="en">json_each</c> (concept pins spec §3.5).</summary>
[JsonSerializable(typeof(string[]))]
internal sealed partial class WithinJson : JsonSerializerContext;
