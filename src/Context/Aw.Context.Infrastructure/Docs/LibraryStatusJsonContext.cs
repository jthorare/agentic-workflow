using System.Text.Json.Serialization;
using Aw.Context.Core.Library;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>The JSON of the library status file. Internal to Infrastructure.</summary>
[JsonSerializable(typeof(LibraryRootReport[]))]
internal sealed partial class LibraryStatusJsonContext : JsonSerializerContext;
