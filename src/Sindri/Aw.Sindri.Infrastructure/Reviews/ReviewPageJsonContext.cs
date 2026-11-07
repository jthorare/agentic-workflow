using System.Text.Json.Serialization;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Reviews;

/// <summary>The page snapshot's JSON context (used through AwJson.CreateOptions): the model and each panel's rows, which <see cref="ReviewPagePanelConverter"/> picks by the panel's id.</summary>
[JsonSerializable(typeof(CachedReviewPage))]
[JsonSerializable(typeof(ReviewPageModel))]
[JsonSerializable(typeof(RequiredChecksData))]
[JsonSerializable(typeof(DocMapData))]
[JsonSerializable(typeof(TestPlanData))]
[JsonSerializable(typeof(CiData))]
[JsonSerializable(typeof(EvidenceData))]
[JsonSerializable(typeof(RiskData))]
[JsonSerializable(typeof(ConversationData))]
internal sealed partial class ReviewPageJsonContext : JsonSerializerContext;
