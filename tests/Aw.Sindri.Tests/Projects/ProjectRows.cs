using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Project rows for the decision and fold tests.</summary>
internal static class ProjectRows
{
    internal static readonly ProjectId HandMadeId = ProjectId.From("prj_01J9Z3K5M7Q2R4T6V8WXYZ0123");
    internal static readonly ProjectId LinearId = ProjectId.From("prj_01J9Z3K5M7Q2R4T6V8WXYZ0124");
    internal const string LinearUrl = "https://linear.app/acme/project/checkout-rewrite-a1b2c3d4e5f6";
    internal const string LinearKey = "linear:project/6f2a9c1e-7b34-4d58-9e0a-3c5d8b1f2a47";

    internal static ProjectAttachedRow HandMade(long version = 1) =>
        new(HandMadeId, Linear: null, "Kitchen", ProjectState.Planned, Links: [], Note: null, Target: null, version, KnownInstants.Epoch, FinishedAt: null, KnownInstants.Epoch, Sequence: 1);

    internal static ProjectAttachedRow Attached(long version = 1) =>
        new(LinearId, LinearKey, Name: null, State: null, Links: [], Note: null, Target: null, version, KnownInstants.Epoch, FinishedAt: null, KnownInstants.Epoch, Sequence: 1);

    internal static LinearProjectFact Fact(string key = LinearKey, string? url = LinearUrl, bool leadIsOwner = false) =>
        new(key, "Checkout rewrite", Description: null, url, StateRaw: "started", Health: null, DueAt: null, Lead: null, leadIsOwner, KnownInstants.Epoch, ClosedAt: null, OpenIssues: 3, Counting: false, LastActivity: KnownInstants.Epoch);

    internal static ProjectFactsSnapshot Snapshot(params LinearProjectFact[] catalog) => new(Owner: null, [], catalog, ["ENG", "OPS"], FirstReadDone: true);
}
