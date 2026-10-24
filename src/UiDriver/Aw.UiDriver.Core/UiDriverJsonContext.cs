using System.Text.Json.Serialization;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Core;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, by the in-app server and by the client.</summary>
[JsonSerializable(typeof(TreeRequest))]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(PropsRequest))]
[JsonSerializable(typeof(ClickRequest))]
[JsonSerializable(typeof(TypeRequest))]
[JsonSerializable(typeof(KeyRequest))]
[JsonSerializable(typeof(ScrollRequest))]
[JsonSerializable(typeof(WaitForRequest))]
[JsonSerializable(typeof(ScreenshotRequest))]
[JsonSerializable(typeof(WindowRequest))]
[JsonSerializable(typeof(ActionRequest))]
[JsonSerializable(typeof(TreeResponse))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(PropsResponse))]
[JsonSerializable(typeof(ActedResponse))]
[JsonSerializable(typeof(WaitedResponse))]
[JsonSerializable(typeof(WindowResponse))]
[JsonSerializable(typeof(DriverFailure))]
[JsonSerializable(typeof(DriverEndpoint))]
internal sealed partial class UiDriverJsonContext : JsonSerializerContext;
