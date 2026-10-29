using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Project rows for the decision and fold tests.</summary>
internal static class ProjectRows
{
    internal static readonly ProjectId HandMadeId = ProjectId.From("prj_01J9Z3K5M7Q2R4T6V8WXYZ0123");
    internal static readonly ProjectId LinearId = ProjectId.From("prj_01J9Z3K5M7Q2R4T6V8WXYZ0124");
    internal const string LinearKey = "linear:project/6f2a9c1e-7b34-4d58-9e0a-3c5d8b1f2a47";

    internal static ProjectAttachedRow HandMade(long version = 1) =>
        new(HandMadeId, Linear: null, "Kitchen", ProjectState.Planned, Links: [], Note: null, Target: null, version, KnownInstants.Epoch, FinishedAt: null, KnownInstants.Epoch, Sequence: 1);

    internal static ProjectAttachedRow Attached(long version = 1) =>
        new(LinearId, LinearKey, Name: null, State: null, Links: [], Note: null, Target: null, version, KnownInstants.Epoch, FinishedAt: null, KnownInstants.Epoch, Sequence: 1);
}
