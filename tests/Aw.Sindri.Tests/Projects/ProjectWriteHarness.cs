using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The four project write services over an in-memory ledger and views, with a scrubber that masks 'sekret-' words.</summary>
internal sealed partial class ProjectWriteHarness
{
    internal const string Secret = "sekret-12345";

    internal ViewsHarness Views { get; } = new();

    internal SliceHarness Slice => Views.Slice;

    internal AttachProjectService Attach() => new(Views.Views, Slice.Store, Slice.Held, Slice.Events, new MaskingScrubber(), Slice.Callers, Slice.Ids);

    internal ChangeProjectService Change() => new(Views.Views, Slice.Store, Slice.Held, Slice.Events, new MaskingScrubber(), Slice.Callers);

    internal DetachProjectService Detach() => new(Views.Views, Slice.Store, Slice.Held, Slice.Events, Slice.Callers);

    internal HideProjectService Hide() => new(Views.Views, Slice.Store, Slice.Held, Slice.Events, Slice.Callers);

    internal async Task Lease() => _ = await Slice.HoldLease();

    internal IDisposable Human() => Slice.Callers.Enter(A.Caller().Human().Build());

    /// <summary>Puts a hand-made project (version 1) in the view.</summary>
    internal Task SeedHandMade() =>
        Views.Fold(EventTypes.ProjectAttached, new ProjectAttached(Slice.Ids.New<CommandId>(), ProjectRows.HandMadeId, Linear: null, "Kitchen", ProjectState.Planned, [], Note: null, Target: null));

    /// <summary>Puts an attached Linear project (version 1) in the view.</summary>
    internal Task SeedAttached() =>
        Views.Fold(EventTypes.ProjectAttached, new ProjectAttached(Slice.Ids.New<CommandId>(), ProjectRows.LinearId, ProjectRows.LinearKey, Name: null, State: null, [], Note: null, Target: null));

    internal Task SeedHidden(long version, bool hidden) =>
        Views.Fold(EventTypes.ProjectHidden, new ProjectHidden(Slice.Ids.New<CommandId>(), ProjectRows.LinearKey, hidden, version));

    internal T Payload<T>(LedgerEvent recorded) => Slice.Events.Payload<T>(recorded).Value;

    private sealed partial class MaskingScrubber : ITextScrubber
    {
        public string Scrub(string text) => Word.Replace(text, "[redacted]");

        [GeneratedRegex("sekret-[0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        private static partial Regex Word { get; }
    }
}
