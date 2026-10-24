using System.Text.Json.Serialization;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Features.CheckProvenance;
using Aw.Evidence.Features.Doctor;
using Aw.Evidence.Features.GhPublish;
using Aw.Evidence.Features.InstallBrowser;
using Aw.Evidence.Features.ParityPlan;
using Aw.Kernel;

namespace Aw.Evidence.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(UiScript))]
[JsonSerializable(typeof(ApiScript))]
[JsonSerializable(typeof(ApiSummary))]
[JsonSerializable(typeof(DbScript))]
[JsonSerializable(typeof(DbSummary))]
[JsonSerializable(typeof(TargetsFile))]
[JsonSerializable(typeof(DesignManifest))]
[JsonSerializable(typeof(DesktopManifest))]
[JsonSerializable(typeof(DesktopSummary))]
[JsonSerializable(typeof(RunSummary))]
[JsonSerializable(typeof(ParitySummary))]
[JsonSerializable(typeof(VerdictCache))]
[JsonSerializable(typeof(VerdictCacheKey))]
[JsonSerializable(typeof(ParityPlanResponse))]
[JsonSerializable(typeof(IReadOnlyList<ExistingAttachment>))]
[JsonSerializable(typeof(SignedUpload))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(RunPublishPlan))]
[JsonSerializable(typeof(DoctorResponse))]
[JsonSerializable(typeof(CheckProvenanceResponse))]
[JsonSerializable(typeof(InstallBrowserResponse))]
[JsonSerializable(typeof(GhPublishResponse))]
internal sealed partial class EvidenceJsonContext : JsonSerializerContext;
