using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class FakeMeetingSourceContractTests : MeetingSourceContract
{
    protected override IMeetingSource Create(IReadOnlyList<Meeting> meetings)
    {
        var source = new FakeMeetingSource();
        foreach (var meeting in meetings)
        {
            source.Meetings.Add(meeting);
        }

        return source;
    }
}
