using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Linear's wire formats. Internal to Infrastructure; used only through <see cref="LinearWire.Options"/> (the AwJson profile).</summary>
[JsonSerializable(typeof(LinearRequest))]
[JsonSerializable(typeof(LinearReply<LinearProjectsData>))]
[JsonSerializable(typeof(LinearReply<LinearDocumentsData>))]
[JsonSerializable(typeof(LinearReply<LinearDocumentData>))]
[JsonSerializable(typeof(LinearReply<LinearUpdatesData>))]
[JsonSerializable(typeof(LinearReply<LinearIssuesData>))]
[JsonSerializable(typeof(LinearReply<LinearIssueData>))]
[JsonSerializable(typeof(LinearReply<LinearProbeData>))]
[JsonSerializable(typeof(LinearReply<LinearViewerData>))]
internal sealed partial class LinearWireJsonContext : JsonSerializerContext;
