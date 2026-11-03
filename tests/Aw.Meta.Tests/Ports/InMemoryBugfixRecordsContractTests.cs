using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

/// <summary>The in-memory bugfix records pass the same contract as the state.json reader.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryBugfixRecordsContractTests : BugfixRecordsContract
{
    protected override IBugfixRecords Create(params BugfixCandidate[] candidates)
    {
        var fake = new InMemoryBugfixRecords();
        foreach (var candidate in candidates)
        {
            fake.Add(candidate);
        }

        return fake;
    }
}
